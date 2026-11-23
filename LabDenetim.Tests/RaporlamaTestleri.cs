// Hafta 8 testleri: derin denetim bulgularının mevcut raporlara bağlanması, Derin Denetim Raporu (HTML/JSON/PDF), kalibrasyon (puan düşürme) ve kural kalitesi.
using System.Text.Json;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class RaporlamaTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    static Finding F(string kural, string anahtar, double etki = 9, string modul = "Hizmet", string baslik = "Başlık", string[]? iso = null, string[]? mitre = null, string[]? kanit = null)
        => BulguFabrikasi.Yap(kural, modul, "host:TEST", anahtar, baslik + " " + anahtar, "ne bulundu", "neden önemli", kanit ?? new[] { "kanıt 1" }, etki, 1.0, iso ?? new[] { "A.8.9", "A.8.2" }, mitre ?? new[] { "T1574.009" }, "çözüm metni", "CHK-X", T0);

    // ---- Mevcut raporlara bağlantı ----
    [Fact] public void V2Kayit_Donustur_UyumlulukVeMitreEtiketiMevcutSiniflardaGorunur()
    {
        var b = V2Kayit.Donustur(F("SVC-UNQUOTED-001", "a", 9, "Hizmet", "Tırnaksız yol", new[] { "A.8.9", "A.8.2" }, new[] { "T1574.009" }));
        Assert.Equal(Onem.Yuksek, b.Onem); Assert.Equal("Hizmet", b.Modul);
        var u = Uyumluluk.Bul(b)!; Assert.Equal("KVKK m.12/1", u.KvkkKisa); Assert.Equal(new[] { "A.8.9", "A.8.2" }, u.IsoKodlari);
        var m = Mitre.Bul(b)!; Assert.Equal("T1574.009", m.Kimlik); Assert.Contains("Unquoted", m.Ad); Assert.Equal("Privilege Escalation", m.Taktik);
        Assert.Contains("A.8.9", Uyumluluk.Yazi(b)); Assert.Contains(Uyumluluk.Ozet(new[] { b }), o => o.Kontrol == "ISO A.8.2");
    }
    [Fact] public void V2Kayit_Bilgi_VeKayitsizBulgu_EtiketsizKalir()
    {
        var bilgi = V2Kayit.Donustur(F("SW-NOFEED-001", "x", 0, "Yazılım", "Veri yok")); Assert.Equal(Onem.Bilgi, bilgi.Onem); Assert.Null(Uyumluluk.Bul(bilgi)); Assert.Null(Mitre.Bul(bilgi));
        Assert.Null(V2Kayit.Etiket(new Bulgu("Hizmet", "hiç dönüştürülmemiş", Onem.Orta, "", ""))); Assert.Null(Uyumluluk.Bul(new Bulgu("Bilinmeyen Modül", "x", Onem.Orta, "", "")));   // eski davranış bozulmadı
    }
    [Fact] public void YonetimKuruluRaporu_YeniModuller_IsEtkisiVeUyumlulukIleGorunur()
    {
        var l = new[] { F("PATCH-MISSING-001", "p", 9, "Yama", "Güvenlik güncellemesi eksik", new[] { "A.8.8" }, new[] { "T1190" }), F("SW-KEV-001", "s", 9, "Yazılım", "Açıklı yazılım", new[] { "A.8.8", "A.8.19" }), F("ACT-ENV-EXPOSED", "e", 9, "Aktif HTTP", ".env açık", new[] { "A.8.4" }) }.Select(V2Kayit.Donustur).ToList();
        string html = System.Net.WebUtility.HtmlDecode(YonetimKuruluRaporu.Html("host", T0, 60, l, new()));
        Assert.Contains("Eksik güvenlik güncellemeleri", html); Assert.Contains("bilinen açıklar", html); Assert.Contains("yanlış yapılandırmalar", html); Assert.Contains("Teknik Zafiyetlerin Yönetimi", html); Assert.Contains("Yazılımın Kurulumu", html); Assert.Contains("Kaynak Koda Erişim", html);
    }
    [Fact] public void IsoSozlugu_YeniKontrolKodlari_AdlandirilmisOlur()
    {
        foreach (var kod in new[] { "A.8.3", "A.8.4", "A.8.13", "A.8.19" }) Assert.Contains(kod, Uyumluluk.IsoUzun(kod)); Assert.NotEqual("A.8.19", Uyumluluk.IsoUzun("A.8.19"));
    }

    // ---- Derin Denetim Raporu ----
    static DerinRaporGirdisi Girdi(List<Finding> l, FpDeposu? fp = null, bool veri = false, AuditTrail? iz = null)
    {
        KbCatalog? kat = null; VulnFeedStore? kev = null;
        if (veri) { kat = KbCatalog.Coz("{\"catalog_version\":\"2026-11-01-test\",\"builds\":{\"26100\":[{\"kb\":\"KB1000001\",\"released\":\"2026-01-01\",\"severity\":\"Low\",\"title\":\"x\"}]}}"); kev = VulnFeedStore.Coz("{\"catalogVersion\":\"2026.11.17\",\"vulnerabilities\":[{\"cveID\":\"CVE-2021-44228\",\"vendorProject\":\"A\",\"product\":\"B\",\"vulnerabilityName\":\"x\",\"dateAdded\":\"2021-12-10\"}]}"); }
        return new DerinRaporGirdisi("TESTPC", "ali", T0, l, fp ?? new FpDeposu(), kat, null, kev, iz);
    }

    [Fact] public void Html_BolumleriveSayilari()
    {
        var l = new List<Finding> { F("A-1", "1", 9), F("A-2", "2", 6, "Yazılım"), F("A-3", "3", 3, "Yama"), F("A-4", "4", 0, "Yazılım") };
        string h = System.Net.WebUtility.HtmlDecode(DerinRapor.Html(Girdi(l, null, true)));
        foreach (var s in new[] { "Derin Denetim Raporu", "TESTPC", "Özet", "Veri Kaynakları ve Sürümleri", "KVKK, ISO/IEC 27001 ve MITRE", "Bulgular", "Yanlış Alarm ve Kural Kalitesi", "Kapsam ve Sınırlar", "2026-11-01-test", "2026.11.17" }) Assert.Contains(s, h);
        Assert.Contains("<b>1</b>Yüksek", h); Assert.Contains("<b>1</b>Orta", h); Assert.Contains("<b>1</b>Düşük", h); Assert.Contains("<b>1</b>Bilgi", h);
        Assert.Contains("NVD zafiyet indeksi", h); Assert.Contains("Yüklü değil", h);   // NVD yok: açıkça yazılır
        Assert.Contains("ISO A.8.9", h); Assert.Contains("T1574.009", h); Assert.Contains("KVKK m.12/1", h); Assert.Contains("çözüm metni", h);
    }
    [Fact] public void Html_VerisizDurum_SessizTemizDemez()
    {
        string h = DerinRapor.Html(Girdi(new List<Finding>())); Assert.Contains("Listelenecek bulgu yok", h); Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(h, "Yüklü değil").Count);
    }
    [Fact] public void Html_KotuNiyetliIcerik_Kodlanir()
    {
        var f = F("X-1", "k", 9, "Hizmet", "<script>alert(1)</script>", null, null, new[] { "Yol: C:\\x\\<img src=x onerror=alert(2)>.exe" });
        string h = DerinRapor.Html(Girdi(new List<Finding> { f })); Assert.DoesNotContain("<script>alert", h); Assert.DoesNotContain("<img src=x", h); Assert.Contains("&lt;script&gt;", h);
    }
    [Fact] public void Html_YanlisAlarm_SayimaKatilmaz_AmaListelenir()
    {
        var a = F("R-1", "a"); var b = F("R-1", "b"); var fp = new FpDeposu(); fp.Gor(new[] { a, b }); fp.Isaretle(a, "ali", "test cihazı <b>", T0);
        string h = DerinRapor.Html(Girdi(fp.Uygula(new[] { a, b }), fp));
        Assert.Contains("<b>1</b>Yüksek", h); Assert.Contains("yanlış alarm</b> olarak işaretlendiği", h); Assert.Contains("test cihazı &lt;b&gt;", h); Assert.Contains("ali", h);
    }
    [Fact] public void Html_DenetimIzi_SaglamVeBozuk()
    {
        var iz = new AuditTrail(); iz.Ekle("ali", "approved", "f1", null, false, "x", T0); iz.Ekle("ali", "applied", "f1", null, false, "y", T0);
        Assert.Contains("sağlam", DerinRapor.Html(Girdi(new List<Finding>(), null, false, iz))); Assert.Contains("2 kayıt", DerinRapor.Html(Girdi(new List<Finding>(), null, false, iz)));
        Assert.DoesNotContain("Denetim İzi Bütünlüğü", DerinRapor.Html(Girdi(new List<Finding>())));
    }
    [Fact] public void Html_CokluBulgu_Hizli() { var l = Enumerable.Range(0, 500).Select(i => F("R-" + i % 20, "k" + i, 5 + i % 5)).ToList(); var sw = System.Diagnostics.Stopwatch.StartNew(); var h = DerinRapor.Html(Girdi(l)); Assert.True(sw.ElapsedMilliseconds < 3000); Assert.Contains("500 bulgu", h); }

    [Fact] public void Json_GeriOkunur_VeSemaGecerli()
    {
        var l = new List<Finding> { F("A-1", "1", 9), F("A-2", "2", 4, "Yama") }; string j = DerinRapor.Json(Girdi(l, null, true));
        using var d = JsonDocument.Parse(j); var k = d.RootElement; Assert.Equal("TESTPC", k.GetProperty("rapor").GetProperty("cihaz").GetString()); Assert.Equal(2, k.GetProperty("ozet").GetProperty("toplam").GetInt32()); Assert.Equal("2026-11-01-test", k.GetProperty("veri_kaynaklari").GetProperty("kb_katalog").GetProperty("surum").GetString());
        var geri = k.GetProperty("bulgular").EnumerateArray().Select(e => FindingSchema.Oku(e.GetRawText())!).ToList(); Assert.Equal(2, geri.Count); Assert.All(geri, f => Assert.Empty(FindingSchema.Validate(f))); Assert.Contains("\"rule_id\"", j);
    }
    [Fact] public void Pdf_Uretir()
    {
        string y = Path.Combine(Path.GetTempPath(), "derin_" + Guid.NewGuid().ToString("N") + ".pdf");
        try { DerinRapor.PdfYaz(y, Girdi(new List<Finding> { F("A-1", "1", 9), F("A-2", "2", 5, "Yazılım") })); var b = File.ReadAllBytes(y); Assert.True(b.Length > 1000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(b, 0, 4)); }
        finally { try { File.Delete(y); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "RaporlamaTestleri.temizlik"); } }
    }

    // ---- Kalibrasyon: puan düşürme ve kural kalitesi ----
    [Fact] public void Kalibrasyon_OranYuksekKural_PuanDuser_Idempotent_SemaGecerli_IsaretliDokunulmaz()
    {
        var fp = new FpDeposu(); var hepsi = Enumerable.Range(0, 12).Select(i => F("BF-1", "b" + i, 8)).ToList(); fp.Gor(hepsi);
        for (int i = 0; i < 4; i++) fp.Isaretle(hepsi[i], "ali", "", T0);   // %33
        var bir = fp.Uygula(hepsi); var iki = fp.Uygula(bir);
        var dusuk = bir.Where(x => !fp.Isaretli(x.Id)).ToList(); Assert.All(dusuk, x => { Assert.Equal(6.0, x.ImpactWeight); Assert.Equal(60, x.Score); Assert.Equal(1, x.Evidence.Count(e => e == FpDeposu.AzaltmaNotu)); Assert.Empty(FindingSchema.Validate(x)); });
        Assert.Equal(bir.Select(x => (x.Id, x.Score)), iki.Select(x => (x.Id, x.Score)));   // iki kez uygulamak puanı bir daha düşürmez
        Assert.All(bir.Where(x => fp.Isaretli(x.Id)), x => { Assert.Equal(8.0, x.ImpactWeight); Assert.Equal(FindingState.FalsePositive, x.State); });
    }
    [Fact] public void Kalibrasyon_TemizVeAzOrnekliKural_PuanDusmez()
    {
        var fp = new FpDeposu(); var l = Enumerable.Range(0, 3).Select(i => F("R-9", "a" + i, 8)).ToList(); fp.Gor(l); fp.Isaretle(l[0], "a", "", T0);
        Assert.All(fp.Uygula(l).Where(x => !fp.Isaretli(x.Id)), x => Assert.Equal(8.0, x.ImpactWeight));   // 3 örnek: yeterli veri yok
        var temiz = Enumerable.Range(0, 20).Select(i => F("R-8", "t" + i, 8)).ToList(); var fp2 = new FpDeposu(); fp2.Gor(temiz); Assert.All(fp2.Uygula(temiz), x => Assert.Equal(8.0, x.ImpactWeight));
    }
    [Fact] public void KuralKalitesi_Satirlar_SiraliVeDurumlu()
    {
        var fp = new FpDeposu(); var kotu = Enumerable.Range(0, 10).Select(i => F("K-1", "k" + i)).ToList(); var iyi = Enumerable.Range(0, 12).Select(i => F("I-1", "i" + i)).ToList(); var az = new[] { F("A-1", "a") };
        fp.Gor(kotu); fp.Gor(iyi); fp.Gor(az); for (int i = 0; i < 4; i++) fp.Isaretle(kotu[i], "x", "", T0); fp.Isaretle(iyi[0], "x", "", T0);
        var s = fp.KuralKalitesi(); Assert.Equal("K-1", s[0].Kural); Assert.Equal("Gözden geçirilmeli", s[0].Durum); Assert.Equal((10, 4), (s[0].Toplam, s[0].Fp)); Assert.Equal("İyi", s.Single(x => x.Kural == "I-1").Durum); Assert.Equal("Yetersiz örnek", s.Single(x => x.Kural == "A-1").Durum);
        Assert.Contains("%40", DerinRapor.Html(Girdi(new List<Finding>(), fp)));   // raporda kural kalitesi tablosu
    }
}
