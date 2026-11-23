// Bulgu Şeması v2 testleri: şema doğrulama, skor, kararlı kimlik, JSON gidiş-dönüş, eski Bulgu köprüsü ve zincirli denetim izi.
using LabDenetim;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

public class FindingV2Testleri
{
    // Geçerli örnek bir bulgu üretir.
    private static Finding Ornek(Func<Finding, Finding>? degistir = null)
    {
        var t = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        var f = new Finding(Finding.KararliKimlik("SW-KEV-001", "mac:aa", "ornek"), "SW-KEV-001", "Yazılım", "mac:aa", "Eski sürüm",
            new Explanation("Eski sürüm bulundu", "Bilinen açık var"), new[] { "sürüm 1.0" }, 8, 0.5, 40,
            new ComplianceTags(new[] { "KVKK m.12" }, new[] { "A.8.8" }, new[] { "T1190" }),
            new Remediation(RemediationType.Manual, "Güncelleyin", null, false, false, null, "CHK-SW", new VerificationPolicy(0, 1, "reopen")),
            new FalsePositiveTracking(FpState.None, "SW-KEV-001", null, null, null, 0, 0), FindingState.Open, t, t);
        return degistir == null ? f : degistir(f);
    }

    [Fact] public void Gecerli_Bulgu_HataVermez() => Assert.Empty(FindingSchema.Validate(Ornek()));

    [Fact] public void Skor_Formul_VeSinirlar()
    {
        Assert.Equal(40, Finding.HesaplaSkor(8, 0.5));
        Assert.Equal(100, Finding.HesaplaSkor(99, 5));   // sınırlanır
        Assert.Equal(0, Finding.HesaplaSkor(-3, 1));
    }

    [Fact] public void YanlisSkor_Reddedilir() => Assert.Contains(FindingSchema.Validate(Ornek(f => f with { Score = 41 })), x => x.Contains("score"));

    [Fact] public void Auto_YedeksizReddedilir()
    {
        var r = Ornek(f => f with { Remediation = f.Remediation with { Type = RemediationType.Auto, Command = "x" } });
        Assert.Contains(FindingSchema.Validate(r), x => x.Contains("auto"));
    }

    [Fact] public void Auto_TamBilgiyle_Gecer()
    {
        var yedek = new BackupPlan("kural seti", "netsh export", "C:\\y", true);
        var r = Ornek(f => f with { Remediation = f.Remediation with { Type = RemediationType.Auto, Command = "netsh ...", RequiresUac = true, RollbackSupported = true, BackupPlan = yedek } });
        Assert.Empty(FindingSchema.Validate(r));
    }

    [Fact] public void Assisted_KomutsuzReddedilir() => Assert.Contains(FindingSchema.Validate(Ornek(f => f with { Remediation = f.Remediation with { Type = RemediationType.Assisted } })), x => x.Contains("command"));

    [Fact] public void RollbackOnFail_DesteksizReddedilir() => Assert.Contains(FindingSchema.Validate(Ornek(f => f with { Remediation = f.Remediation with { VerificationPolicy = new VerificationPolicy(0, 1, "reopen_and_rollback") } })), x => x.Contains("rollback"));

    [Fact] public void EtiketsizUyumluluk_Reddedilir() => Assert.Contains(FindingSchema.Validate(Ornek(f => f with { Compliance = new ComplianceTags(Array.Empty<string>(), new[] { "A.8.8" }, Array.Empty<string>()) })), x => x.Contains("KVKK"));

    [Fact] public void YanlisAlarmSayisi_Tutarsizsa_Reddedilir() => Assert.Contains(FindingSchema.Validate(Ornek(f => f with { FalsePositiveTracking = f.FalsePositiveTracking with { RuleFpCount = 5, RuleTotalCount = 2 } })), x => x.Contains("rule_fp_count"));

    [Fact] public void Zaman_Tersse_Reddedilir() => Assert.Contains(FindingSchema.Validate(Ornek(f => f with { LastSeen = f.FirstSeen.AddDays(-1) })), x => x.Contains("last_seen"));

    [Fact] public void KararliKimlik_AyniGirdiAyniSonuc()
    {
        Assert.Equal(Finding.KararliKimlik("a", "b", "c"), Finding.KararliKimlik("a", "b", "c"));
        Assert.NotEqual(Finding.KararliKimlik("a", "b", "c"), Finding.KararliKimlik("a", "b", "d"));
        Assert.Equal(64, Finding.KararliKimlik("a", "b", "c").Length);
    }

    [Fact] public void EskiOnem_Kademeleri()
    {
        Assert.Equal(Onem.Yuksek, Ornek(f => f with { Score = 70 }).EskiOnem);
        Assert.Equal(Onem.Orta, Ornek(f => f with { Score = 40 }).EskiOnem);
        Assert.Equal(Onem.Dusuk, Ornek(f => f with { Score = 15 }).EskiOnem);
        Assert.Equal(Onem.Bilgi, Ornek(f => f with { Score = 14 }).EskiOnem);
    }

    [Fact] public void Json_GidisDonus_SnakeCase()
    {
        var f = Ornek();
        string j = FindingSchema.Json(f);
        Assert.Contains("\"rule_id\"", j); Assert.Contains("\"remediation_type\": \"manual\"", j); Assert.Contains("\"verification_check_id\"", j);
        var g = FindingSchema.Oku(j)!;
        Assert.Equal(f.Id, g.Id); Assert.Equal(f.Remediation.Type, g.Remediation.Type); Assert.Empty(FindingSchema.Validate(g));
    }

    [Fact] public void ToBulgu_EskiKayitaCevirir()
    {
        var b = Ornek().ToBulgu();
        Assert.Equal("Yazılım", b.Modul); Assert.Equal(Onem.Orta, b.Onem); Assert.Equal("Güncelleyin", b.Oneri);
    }

    [Theory]
    [InlineData(Onem.Yuksek)] [InlineData(Onem.Orta)] [InlineData(Onem.Dusuk)] [InlineData(Onem.Bilgi)]
    public void FromBulgu_GecerliFindingUretir_VeOnemiKorur(Onem o)
    {
        var b = new Bulgu("Port", "Açık kapı", o, "445 açık", "Kapatın");
        var f = Finding.FromBulgu(b, "ip:10.0.0.5", DateTime.UtcNow, 1.0);
        Assert.Empty(FindingSchema.Validate(f));
        Assert.Equal(RemediationType.Manual, f.Remediation.Type);
        // Varlık ağırlığı 1 iken eski önem geri dönüşümü aynı kalmalı.
        Assert.Equal(o, f.ToBulgu().Onem);
    }

    [Fact] public void FromBulgu_BosDetay_VeAyniGirdi_AyniKimlik()
    {
        var b = new Bulgu("HTTP", "Başlık eksik", Onem.Orta, "", "");
        var t = DateTime.UtcNow;
        var f1 = Finding.FromBulgu(b, "ip:1", t); var f2 = Finding.FromBulgu(b, "ip:1", t.AddHours(1));
        Assert.Empty(FindingSchema.Validate(f1)); Assert.Equal(f1.Id, f2.Id);
        Assert.NotEqual(f1.Id, Finding.FromBulgu(b, "ip:2", t).Id);
    }

    [Fact] public void CokluDogrulama_BirlestirmeKurali()
    {
        Assert.Equal(VerificationOutcome.Pass, CheckRunResult.Birlestir(new[] { VerificationOutcome.Pass, VerificationOutcome.Pass }));
        Assert.Equal(VerificationOutcome.Fail, CheckRunResult.Birlestir(new[] { VerificationOutcome.Pass, VerificationOutcome.Fail, VerificationOutcome.Error }));
        Assert.Equal(VerificationOutcome.Error, CheckRunResult.Birlestir(new[] { VerificationOutcome.Pass, VerificationOutcome.Error }));
        Assert.Equal(VerificationOutcome.NotRun, CheckRunResult.Birlestir(Array.Empty<VerificationOutcome>()));
    }

    [Fact] public void DenetimIzi_ZincirSaglam()
    {
        var iz = new AuditTrail();
        iz.Ekle("ali", "remediation_planned", "f1", "CHK", true, "plan"); iz.Ekle("ali", "approved", "f1", "CHK", true, "onay"); iz.Ekle("system", "applied", "f1", "CHK", true, "uygulandı");
        Assert.Equal(3, iz.Kayitlar.Count); Assert.Equal(AuditTrail.Baslangic, iz.Kayitlar[0].PrevHash); Assert.Equal(iz.Kayitlar[0].Hash, iz.Kayitlar[1].PrevHash);
        Assert.True(iz.Dogrula().Saglam);
    }

    [Fact] public void DenetimIzi_Kurcalama_Yakalanir()
    {
        var iz = new AuditTrail();
        iz.Ekle("ali", "approved", "f1", null, false, "a"); iz.Ekle("ali", "applied", "f1", null, false, "b"); iz.Ekle("ali", "verified", "f1", null, false, "c");
        var k = iz.Kayitlar.ToList();
        // Ortadaki kaydın ayrıntısı değiştirilir.
        k[1] = k[1] with { Detail = "sahte" };
        var (s, sira) = AuditTrail.Dogrula(k); Assert.False(s); Assert.Equal(2, sira);
        // Bir kayıt silinirse de bozulur.
        var sil = iz.Kayitlar.ToList(); sil.RemoveAt(0); Assert.False(AuditTrail.Dogrula(sil).Saglam);
    }

    [Fact] public void DenetimIzi_BilinmeyenEylem_Reddedilir() => Assert.Throws<ArgumentException>(() => new AuditTrail().Ekle("a", "format_c", null, null, false, ""));

    [Fact] public void DenetimIzi_Dosyaya_Yazilir_VeYenidenOkunur()
    {
        string yol = Path.Combine(Path.GetTempPath(), "iz_" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var a = new AuditTrail(yol); a.Ekle("u", "dry_run", "f", null, false, "x"); a.Ekle("u", "closed", "f", null, false, "y");
            // Yeniden açınca zincir devam eder ve sağlamdır.
            var b = new AuditTrail(yol); Assert.Equal(2, b.Kayitlar.Count); b.Ekle("u", "verified", "f", null, false, "z");
            Assert.True(new AuditTrail(yol).Dogrula().Saglam); Assert.Equal(3, new AuditTrail(yol).Kayitlar.Count);
        }
        finally { try { File.Delete(yol); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "FindingV2Testleri.temizlik"); } }
    }
}
