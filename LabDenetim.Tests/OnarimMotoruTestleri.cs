// Hafta 2 testleri: doğrulama servisi (çoktan-bire), onarım motoru, gözetimsiz politika, yanlış alarm takibi.
// Tüm eylemler SAHTE (bellekte): gerçek sistemde hiçbir şey değiştirilmez.
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

// Sahte eylem: çağrıları sayar, istenirse hata fırlatır.
internal sealed class SahteEylem : IRemediationAction
{
    public string Id => "sahte";
    public bool YedekBasarili = true, UygulaHata, GeriAlHata;
    public List<string> Cagrilar = new();
    public Task<BackupResult> BackupAsync(CancellationToken ct) { Cagrilar.Add("backup"); return Task.FromResult(new BackupResult(YedekBasarili, "mem://yedek", YedekBasarili ? "ok" : "disk dolu")); }
    public Task ApplyAsync(CancellationToken ct) { Cagrilar.Add("apply"); if (UygulaHata) throw new InvalidOperationException("patladı"); return Task.CompletedTask; }
    public Task RollbackAsync(BackupResult y, CancellationToken ct) { Cagrilar.Add("rollback"); if (GeriAlHata) throw new InvalidOperationException("geri alınamadı"); return Task.CompletedTask; }
}

// Sahte kontrol: verilen kimlik → sonuç tablosunu döndürür (tabloda yoksa bildirmez).
internal sealed class SahteKontrol : IVerificationCheck
{
    public string Id { get; }
    public Dictionary<string, VerificationOutcome> Sonuc = new(); public bool Patlat; public int Cagri;
    public SahteKontrol(string id) { Id = id; }
    public Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> r, CancellationToken ct) { Cagri++; if (Patlat) throw new InvalidOperationException("x"); return Task.FromResult<IReadOnlyDictionary<string, VerificationOutcome>>(Sonuc); }
}

public class OnarimMotoruTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    // Otomatik, yedekli, geri alınabilir örnek bulgu.
    static Finding Auto(string id = "f1", string chk = "CHK-FW", string? sinif = null, bool uac = false, string onFail = "reopen", int deneme = 1, string rule = "BF-001")
    {
        double imp = 8, ast = 0.5;
        return new Finding(id, rule, "Brute", "ip:1", "Saldırgan IP", new Explanation("a", "b"), new[] { "k" }, imp, ast, Finding.HesaplaSkor(imp, ast),
            new ComplianceTags(new[] { "KVKK m.12" }, new[] { "A.8.20" }, Array.Empty<string>()),
            new Remediation(RemediationType.Auto, "Engelle", "netsh ...", uac, true, new BackupPlan("kural", "export", "mem", true), chk, new VerificationPolicy(0, deneme, onFail), sinif),
            new FalsePositiveTracking(FpState.None, rule, null, null, null, 0, 0), FindingState.Open, T0, T0);
    }

    // Motor + parçalar kurar.
    static (RemediationEngine m, AuditTrail iz, SahteKontrol k, VerificationService v) Kur(UnattendedPolicy? p = null, string chk = "CHK-FW")
    {
        var iz = new AuditTrail(); var v = new VerificationService(); var k = new SahteKontrol(chk); v.Kaydet(k);
        return (new RemediationEngine(iz, v, p, (_, _) => Task.CompletedTask, () => T0), iz, k, v);
    }
    static Task<bool> Evet(string _) => Task.FromResult(true);
    static Task<bool> Hayir(string _) => Task.FromResult(false);
    static string[] Eylemler(AuditTrail iz) => iz.Kayitlar.Select(x => x.Action).ToArray();

    // ---- Doğrulama servisi ----
    [Fact] public async Task Dogrulama_HepsiGecerse_Pass()
    {
        var (_, _, k, v) = Kur(); var a = Auto("a"); var b = Auto("b");
        k.Sonuc["a"] = VerificationOutcome.Pass; k.Sonuc["b"] = VerificationOutcome.Pass;
        var r = await v.CalistirAsync("CHK-FW", new[] { a, b }, default);
        Assert.Equal(VerificationOutcome.Pass, r.Aggregate);
    }
    [Fact] public async Task Dogrulama_KismiBasari_Fail_VeDurumlar()
    {
        var (_, _, k, v) = Kur(); var a = Auto("a"); var b = Auto("b");
        k.Sonuc["a"] = VerificationOutcome.Pass; k.Sonuc["b"] = VerificationOutcome.Fail;
        var r = await v.CalistirAsync("CHK-FW", new[] { a, b }, default);
        Assert.Equal(VerificationOutcome.Fail, r.Aggregate);
        var d = VerificationService.DurumUygula(new[] { a, b }, r, T0);
        Assert.Equal(FindingState.Verified, d[0].State); Assert.Equal(FindingState.Open, d[1].State);
    }
    [Fact] public async Task Dogrulama_EksikSonuc_Error_Sayilir()
    {
        var (_, _, k, v) = Kur(); k.Sonuc["a"] = VerificationOutcome.Pass;
        var r = await v.CalistirAsync("CHK-FW", new[] { Auto("a"), Auto("b") }, default);
        Assert.Equal(VerificationOutcome.Error, r.PerFinding["b"]); Assert.Equal(VerificationOutcome.Error, r.Aggregate);
    }
    [Fact] public async Task Dogrulama_KontrolPatlarsa_Error_VeBilinmeyenKontrol()
    {
        var (_, _, k, v) = Kur(); k.Patlat = true;
        Assert.Equal(VerificationOutcome.Error, (await v.CalistirAsync("CHK-FW", new[] { Auto("a") }, default)).Aggregate);
        Assert.Equal(VerificationOutcome.Error, (await v.CalistirAsync("YOK", new[] { Auto("a") }, default)).Aggregate);
    }

    // ---- Onarım motoru ----
    [Fact] public async Task Motor_BasariliAkis_SiraliVeKapanir()
    {
        var (m, iz, k, _) = Kur(); var e = new SahteEylem(); k.Sonuc["f1"] = VerificationOutcome.Pass;
        var r = await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "ali", default);
        Assert.Equal(OnarimSonucu.Dogrulandi, r.Sonuc); Assert.Equal(FindingState.Closed, r.Bulgular.Single(x => x.Id == "f1").State);
        Assert.Equal(new[] { "backup", "apply" }, e.Cagrilar);
        Assert.Equal(new[] { "remediation_planned", "approved", "backup_taken", "applied", "verify_run", "verified", "closed" }, Eylemler(iz));
        Assert.True(iz.Dogrula().Saglam);
    }
    [Fact] public async Task Motor_OnayYoksa_HicBirSeyYapmaz()
    {
        var (m, iz, _, _) = Kur(); var e = new SahteEylem();
        Assert.Equal(OnarimSonucu.Reddedildi, (await m.YurutAsync(Auto(), new[] { Auto() }, e, Hayir, "ali", default)).Sonuc);
        Assert.Equal(OnarimSonucu.Reddedildi, (await m.YurutAsync(Auto(), new[] { Auto() }, e, null, "ali", default)).Sonuc);
        Assert.Empty(e.Cagrilar); Assert.Contains("rejected", Eylemler(iz));
    }
    [Fact] public async Task Motor_KuruCalistirma_EylemeDokunmaz()
    {
        var (m, iz, _, _) = Kur(); var e = new SahteEylem();
        var r = await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "ali", default, kuruCalistirma: true);
        Assert.Equal(OnarimSonucu.KuruCalistirma, r.Sonuc); Assert.Empty(e.Cagrilar); Assert.Equal(new[] { "remediation_planned", "dry_run" }, Eylemler(iz));
    }
    [Fact] public async Task Motor_YedekBasarisiz_DegisiklikYok()
    {
        var (m, iz, _, _) = Kur(); var e = new SahteEylem { YedekBasarili = false };
        var r = await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "ali", default);
        Assert.Equal(OnarimSonucu.YedekHatasi, r.Sonuc); Assert.Equal(new[] { "backup" }, e.Cagrilar); Assert.Contains("failed", Eylemler(iz));
    }
    [Fact] public async Task Motor_UygulamaHatasi_GeriAlir()
    {
        var (m, iz, _, _) = Kur(); var e = new SahteEylem { UygulaHata = true };
        var r = await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "ali", default);
        Assert.Equal(OnarimSonucu.UygulamaHatasi, r.Sonuc); Assert.Equal(new[] { "backup", "apply", "rollback" }, e.Cagrilar); Assert.Contains("rolled_back", Eylemler(iz));
    }
    [Fact] public async Task Motor_GeriAlmaDaBasarisiz_AcikcaKaydeder()
    {
        var (m, iz, _, _) = Kur(); var e = new SahteEylem { UygulaHata = true, GeriAlHata = true };
        await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "ali", default);
        Assert.Contains(iz.Kayitlar, x => x.Action == "failed" && x.Detail.Contains("GERİ ALMA BAŞARISIZ"));
    }
    [Fact] public async Task Motor_DogrulamaBasarisiz_Reopen()
    {
        var (m, _, k, _) = Kur(); var e = new SahteEylem(); k.Sonuc["f1"] = VerificationOutcome.Fail;
        var r = await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "ali", default);
        Assert.Equal(OnarimSonucu.DogrulamaBasarisizAcildi, r.Sonuc); Assert.Equal(FindingState.Open, r.Bulgular[0].State); Assert.DoesNotContain("rollback", e.Cagrilar);
    }
    [Fact] public async Task Motor_DogrulamaBasarisiz_ReopenAndRollback()
    {
        var (m, _, k, _) = Kur(); var e = new SahteEylem(); k.Sonuc["f1"] = VerificationOutcome.Fail;
        var r = await m.YurutAsync(Auto(onFail: "reopen_and_rollback"), new[] { Auto(onFail: "reopen_and_rollback") }, e, Evet, "ali", default);
        Assert.Equal(OnarimSonucu.DogrulamaBasarisizGeriAlindi, r.Sonuc); Assert.Contains("rollback", e.Cagrilar); Assert.Equal(FindingState.Open, r.Bulgular[0].State);
    }
    [Fact] public async Task Motor_Yeniden_Dener()
    {
        var (m, iz, k, _) = Kur(); k.Sonuc["f1"] = VerificationOutcome.Fail;
        await m.YurutAsync(Auto(deneme: 3), new[] { Auto(deneme: 3) }, new SahteEylem(), Evet, "ali", default);
        Assert.Equal(3, k.Cagri); Assert.Equal(3, Eylemler(iz).Count(x => x == "verify_run"));
    }
    [Fact] public async Task Motor_CoktanBire_DigerBulguyuDaDogrular()
    {
        var (m, _, k, _) = Kur(); k.Sonuc["f1"] = VerificationOutcome.Pass; k.Sonuc["f2"] = VerificationOutcome.Fail;
        var r = await m.YurutAsync(Auto("f1"), new[] { Auto("f1"), Auto("f2") }, new SahteEylem(), Evet, "ali", default);
        Assert.Equal(OnarimSonucu.Dogrulandi, r.Sonuc); Assert.Equal(FindingState.Closed, r.Bulgular.Single(x => x.Id == "f1").State); Assert.Equal(FindingState.Open, r.Bulgular.Single(x => x.Id == "f2").State);
    }
    [Fact] public async Task Motor_ManuelVeGecersiz_Yurutulmez()
    {
        var (m, _, _, _) = Kur(); var e = new SahteEylem();
        var manuel = Auto() with { Remediation = Auto().Remediation with { Type = RemediationType.Manual, Command = null } };
        Assert.Equal(OnarimSonucu.ElleDuzeltme, (await m.YurutAsync(manuel, new[] { manuel }, e, Evet, "a", default)).Sonuc);
        var bozuk = Auto() with { Score = 1 };
        Assert.Equal(OnarimSonucu.GecersizBulgu, (await m.YurutAsync(bozuk, new[] { bozuk }, e, Evet, "a", default)).Sonuc);
        Assert.Empty(e.Cagrilar);
    }

    // ---- Gözetimsiz politika ----
    [Fact] public void Politika_VarsayilanKapali_VeSabitListe()
    {
        var p = new UnattendedPolicy();
        Assert.Equal(GozetimsizKarar.Kapali, p.Karar("fw_block_bruteforce", "r", false, T0));
        Assert.Equal(GozetimsizKarar.BilinmeyenSinif, p.Karar("process_kill", "r", false, T0));
        Assert.Throws<ArgumentException>(() => p.Etkinlestir("process_kill", "a", true, T0));
        Assert.Throws<ArgumentException>(() => p.Etkinlestir("emergency_isolation", "a", true, T0));
    }
    [Fact] public void Politika_YalnizcaYonetici_VeIzeYazilir()
    {
        var iz = new AuditTrail(); var p = new UnattendedPolicy(null, iz);
        Assert.Throws<UnauthorizedAccessException>(() => p.Etkinlestir("reverify", "stajyer", false, T0));
        p.Etkinlestir("reverify", "yonetici", true, T0); p.Kapat("reverify", "yonetici", T0);
        Assert.Equal(new[] { "policy_changed", "policy_changed" }, Eylemler(iz)); Assert.Equal(GozetimsizKarar.Kapali, p.Karar("reverify", "r", false, T0));
    }
    [Fact] public void Politika_KuruDonemi_SonraIzinli_SonraHizSiniri()
    {
        var p = new UnattendedPolicy(); p.Etkinlestir("fw_block_bruteforce", "y", true, T0, saatteEnCok: 2);
        Assert.Equal(GozetimsizKarar.KuruCalistirma, p.Karar("fw_block_bruteforce", "r", false, T0.AddDays(6)));
        var gun8 = T0.AddDays(8); Assert.Equal(GozetimsizKarar.Izinli, p.Karar("fw_block_bruteforce", "r", false, gun8));
        p.KullanimKaydet("fw_block_bruteforce", gun8); p.KullanimKaydet("fw_block_bruteforce", gun8);
        Assert.Equal(GozetimsizKarar.HizSiniri, p.Karar("fw_block_bruteforce", "r", false, gun8.AddMinutes(10)));
        Assert.Equal(GozetimsizKarar.Izinli, p.Karar("fw_block_bruteforce", "r", false, gun8.AddHours(2)));
    }
    [Fact] public void Politika_UacliIcinYardimciSart()
    {
        var p = new UnattendedPolicy(); p.Etkinlestir("fw_block_bruteforce", "y", true, T0);
        Assert.Equal(GozetimsizKarar.YardimciYok, p.Karar("fw_block_bruteforce", "r", true, T0.AddDays(9)));
        p.YardimciKayitli = true; Assert.Equal(GozetimsizKarar.Izinli, p.Karar("fw_block_bruteforce", "r", true, T0.AddDays(9)));
    }
    [Fact] public void Politika_YanlisAlarmYuksekseKuralCikar()
    {
        var fp = new FalsePositiveTracker(0.3, 10); for (int i = 0; i < 10; i++) fp.Bulgu("BF-001"); for (int i = 0; i < 4; i++) fp.YanlisAlarm("BF-001");
        var p = new UnattendedPolicy(fp); p.Etkinlestir("fw_block_bruteforce", "y", true, T0);
        Assert.Equal(GozetimsizKarar.GozdenGecirmeGerekli, p.Karar("fw_block_bruteforce", "BF-001", false, T0.AddDays(9)));
        Assert.Equal(GozetimsizKarar.Izinli, p.Karar("fw_block_bruteforce", "DIGER", false, T0.AddDays(9)));
    }
    [Fact] public void FpTakipci_AzOrnekteKararVermez()
    {
        var fp = new FalsePositiveTracker(0.3, 10); fp.Bulgu("R"); fp.YanlisAlarm("R");
        Assert.False(fp.GozdenGecir("R")); Assert.Equal(1.0, fp.Oran("R"));
    }

    // ---- Gözetimsiz çalıştırma (motor + politika) ----
    static Finding Gozetimsiz(bool uac = false) => Auto(sinif: "fw_block_bruteforce", uac: uac);
    [Fact] public async Task Gozetimsiz_KapaliysaEngellenir_OnayIstenmez()
    {
        var p = new UnattendedPolicy(); var (m, _, _, _) = Kur(p); var e = new SahteEylem(); bool soruldu = false;
        var r = await m.YurutAsync(Gozetimsiz(), new[] { Gozetimsiz() }, e, _ => { soruldu = true; return Task.FromResult(true); }, "system", default, gozetimsiz: true);
        Assert.Equal(OnarimSonucu.Engellendi, r.Sonuc); Assert.Empty(e.Cagrilar); Assert.False(soruldu);
    }
    [Fact] public async Task Gozetimsiz_SinifsizBulgu_Engellenir()
    {
        var p = new UnattendedPolicy(); p.Etkinlestir("fw_block_bruteforce", "y", true, T0.AddDays(-30)); var (m, _, _, _) = Kur(p); var e = new SahteEylem();
        Assert.Equal(OnarimSonucu.Engellendi, (await m.YurutAsync(Auto(), new[] { Auto() }, e, Evet, "system", default, gozetimsiz: true)).Sonuc); Assert.Empty(e.Cagrilar);
    }
    [Fact] public async Task Gozetimsiz_KuruDonemde_YalnizcaPlan()
    {
        var p = new UnattendedPolicy(); p.Etkinlestir("fw_block_bruteforce", "y", true, T0); var (m, iz, _, _) = Kur(p); var e = new SahteEylem();
        Assert.Equal(OnarimSonucu.KuruCalistirma, (await m.YurutAsync(Gozetimsiz(), new[] { Gozetimsiz() }, e, null, "system", default, gozetimsiz: true)).Sonuc);
        Assert.Empty(e.Cagrilar); Assert.Contains("dry_run", Eylemler(iz));
    }
    [Fact] public async Task Gozetimsiz_IzinliysaCalisir_VeHizSayilir()
    {
        var p = new UnattendedPolicy(); p.Etkinlestir("fw_block_bruteforce", "y", true, T0.AddDays(-30), saatteEnCok: 1); var (m, iz, k, _) = Kur(p); k.Sonuc["f1"] = VerificationOutcome.Pass; var e = new SahteEylem();
        Assert.Equal(OnarimSonucu.Dogrulandi, (await m.YurutAsync(Gozetimsiz(), new[] { Gozetimsiz() }, e, null, "system", default, gozetimsiz: true)).Sonuc);
        Assert.Contains(iz.Kayitlar, x => x.Action == "approved" && x.Actor.StartsWith("policy:"));
        // İkinci çalıştırma saat sınırına takılır.
        Assert.Equal(OnarimSonucu.Engellendi, (await m.YurutAsync(Gozetimsiz(), new[] { Gozetimsiz() }, new SahteEylem(), null, "system", default, gozetimsiz: true)).Sonuc);
    }

    // ---- Şema eki ----
    [Fact] public void Sema_GozetimsizSinif_KuraliSikilasir()
    {
        Assert.Empty(FindingSchema.Validate(Gozetimsiz()));
        Assert.Contains(FindingSchema.Validate(Gozetimsiz(uac: true)), x => x.Contains("yardımcı"));
        Assert.Empty(FindingSchema.Validate(Gozetimsiz(uac: true), yardimciKayitli: true));
        var asist = Gozetimsiz() with { Remediation = Gozetimsiz().Remediation with { Type = RemediationType.Assisted } };
        Assert.Contains(FindingSchema.Validate(asist), x => x.Contains("type=auto"));
        var yedeksiz = Gozetimsiz() with { Remediation = Gozetimsiz().Remediation with { RollbackSupported = false, BackupPlan = null, Type = RemediationType.Manual, Command = null } };
        Assert.NotEmpty(FindingSchema.Validate(yedeksiz));
    }
}
