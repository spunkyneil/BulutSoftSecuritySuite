// Hafta 5 testleri: kalıcı yanlış alarm deposu (+ gözetimsiz politika bağlantısı) ve KEV besleme deposu/onaylı indirici.
using System.Net;
using System.Text;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class FpVeKevTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    static Finding Bulgu(string kural, string anahtar) => BulguFabrikasi.Yap(kural, "Test", "host:T", anahtar, "başlık " + anahtar, "ne", "neden", new[] { "k" }, 5, 0.5, new[] { "A.8.9" }, Array.Empty<string>(), "çöz", "CHK-T", T0);

    // Bellek klasörünü geçici yapar.
    static void Gecici(Action is_)
    {
        string eski = BulutKlasor.Kok, g = Path.Combine(Path.GetTempPath(), "fpkev_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = g;
        try { is_(); } finally { BulutKlasor.Kok = eski; try { Directory.Delete(g, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "FpVeKevTestleri.temizlik"); } }
    }

    // ---- Yanlış alarm deposu ----
    [Fact] public void Fp_AyniBulgu_TekrarSayilmaz_IsaretleVeGeri()
    {
        var d = new FpDeposu(); var f = Bulgu("R-1", "a");
        d.Gor(new[] { f }); d.Gor(new[] { f }); d.Gor(new[] { f }); Assert.Equal((1, 0), d.Say("R-1"));   // her taramada tekrar sayılmaz
        Assert.True(d.Isaretle(f, "ali", "test cihazı", T0)); Assert.False(d.Isaretle(f, "ali", "x", T0)); Assert.Equal((1, 1), d.Say("R-1")); Assert.True(d.Isaretli(f.Id));
        Assert.True(d.Geri(f.Id)); Assert.Equal((1, 0), d.Say("R-1")); Assert.False(d.Geri(f.Id));
    }

    [Fact] public void Fp_Uygula_IsaretliBulgu_FalsePositive_VeSemaGecerli()
    {
        var d = new FpDeposu(); var a = Bulgu("R-1", "a"); var b = Bulgu("R-1", "b"); d.Gor(new[] { a, b }); d.Isaretle(a, "ali", "gerekçe", T0);
        var l = d.Uygula(new[] { a, b }); var ua = l.Single(x => x.Id == a.Id); var ub = l.Single(x => x.Id == b.Id);
        Assert.Equal(FindingState.FalsePositive, ua.State); Assert.Equal(FpState.Confirmed, ua.FalsePositiveTracking.State); Assert.Equal("ali", ua.FalsePositiveTracking.MarkedBy); Assert.Equal("gerekçe", ua.FalsePositiveTracking.Reason);
        Assert.Equal(FindingState.Open, ub.State); Assert.Equal(FpState.None, ub.FalsePositiveTracking.State); Assert.Equal((1, 2), (ub.FalsePositiveTracking.RuleFpCount, ub.FalsePositiveTracking.RuleTotalCount));
        Assert.All(l, x => Assert.Empty(FindingSchema.Validate(x)));
        // İşaret geri alınınca bulgu yeniden açılır.
        d.Geri(a.Id); Assert.Equal(FindingState.Open, d.Uygula(new[] { ua }).Single().State);
    }

    [Fact] public void Fp_OranYuksekseKural_GozdenGecirilmeli_Suspected_VeGozetimsizPolitikadanCikar()
    {
        var d = new FpDeposu(); var hepsi = Enumerable.Range(0, 12).Select(i => Bulgu("BF-9", "b" + i)).ToList(); d.Gor(hepsi);
        for (int i = 0; i < 4; i++) d.Isaretle(hepsi[i], "ali", "", T0);   // 4/12 = %33 ≥ eşik
        var l = d.Uygula(hepsi); Assert.Equal(FpState.Suspected, l.First(x => !d.Isaretli(x.Id)).FalsePositiveTracking.State);
        var p = new UnattendedPolicy(d.Takipci()); p.Etkinlestir("fw_block_bruteforce", "y", true, T0.AddDays(-30));
        Assert.Equal(GozetimsizKarar.GozdenGecirmeGerekli, p.Karar("fw_block_bruteforce", "BF-9", false, T0)); Assert.Equal(GozetimsizKarar.Izinli, p.Karar("fw_block_bruteforce", "BF-TEMIZ", false, T0));
    }

    [Fact] public void Fp_AzOrnekte_Suspected_Olmaz()
    {
        var d = new FpDeposu(); var l = Enumerable.Range(0, 3).Select(i => Bulgu("R-2", "c" + i)).ToList(); d.Gor(l); d.Isaretle(l[0], "a", "", T0); d.Isaretle(l[1], "a", "", T0);
        Assert.DoesNotContain(d.Uygula(l), x => x.FalsePositiveTracking.State == FpState.Suspected);   // 2/3 ama en az 10 örnek şartı
    }

    [Fact] public void Fp_Kalici_YazOku_VeBozukDosya()
    {
        Gecici(() =>
        {
            var d = new FpDeposu(); var f = Bulgu("R-3", "x"); d.Isaretle(f, "ali", "neden", T0); d.Kaydet();
            var y = FpDeposu.Yukle(); Assert.True(y.Isaretli(f.Id)); Assert.Equal((1, 1), y.Say("R-3")); Assert.False(File.Exists(FpDeposu.Yol + ".tmp"));
            File.WriteAllText(FpDeposu.Yol, "{bozuk"); Assert.False(FpDeposu.Yukle().Isaretli(f.Id));   // bozuk dosya boş depo verir, çökmez
        });
    }

    [Fact] public void Fp_Sinir_KuralBasinaEnCok()
    {
        var d = new FpDeposu(); d.Gor(Enumerable.Range(0, FpDeposu.KuralBasinaEnCok + 50).Select(i => Bulgu("R-4", "k" + i)));
        Assert.Equal(FpDeposu.KuralBasinaEnCok, d.Say("R-4").Toplam);
    }

    [Fact] public void YerelAkis_FpDeposuyla_BulgulariIsaretler()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Apps"); k.Hizmet.Add(new WinHizmeti("A", "A Hizmeti", 0, @"C:\Apps\a.exe", "LocalSystem", "Auto", "Running"));
        var d = new FpDeposu(); var s1 = YerelDenetimAkisi.Calistir(k, null, null, T0, d); var f = s1.Bulgular.First(x => x.RuleId == "SVC-WRITABLE-001");
        d.Isaretle(f, "ali", "test klasörü", T0); var s2 = YerelDenetimAkisi.Calistir(k, null, null, T0, d);
        Assert.Equal(FindingState.FalsePositive, s2.Bulgular.Single(x => x.Id == f.Id).State); Assert.Equal(1, d.Say("SVC-WRITABLE-001").Toplam);   // ikinci taramada tekrar sayılmadı
    }

    [Fact] public void BulguDeposu_Degistir_DigerVarliklariKorur()
    {
        Gecici(() =>
        {
            var a = BulguFabrikasi.Yap("R", "M", "host:A", "1", "t", "n", "w", new[] { "k" }, 5, 0.5, new[] { "A.8.9" }, Array.Empty<string>(), "c", "CHK", T0); var b = a with { AssetId = "web:B:80", Id = "x2" };
            BulguDeposu.Degistir("host:A", new[] { a }); BulguDeposu.Degistir("web:B:80", new[] { b }); Assert.Equal(2, BulguDeposu.Oku().Count);
            BulguDeposu.Degistir("host:A", Array.Empty<Finding>()); Assert.Equal("web:B:80", Assert.Single(BulguDeposu.Oku()).AssetId);
        });
    }

    // ---- KEV beslemesi ----
    const string Kev = """
{ "title":"CISA KEV", "catalogVersion":"2026.11.17", "dateReleased":"2026-11-17T14:00:00.000Z", "count":2, "vulnerabilities":[
 {"cveID":"CVE-2021-44228","vendorProject":"Apache","product":"Log4j2","vulnerabilityName":"Log4Shell","dateAdded":"2021-12-10","shortDescription":"x","requiredAction":"y","dueDate":"2021-12-24","knownRansomwareCampaignUse":"Known","notes":""},
 {"cveID":"cve-2023-0001","vendorProject":"Foo","product":"Bar","vulnerabilityName":"Test","dateAdded":"2023-01-05","dueDate":"","knownRansomwareCampaignUse":"Unknown"} ] }
""";

    [Fact] public void Kev_Cozulur_VeSorgulanir()
    {
        var s = VulnFeedStore.Coz(Kev); Assert.Equal("2026.11.17", s.Surum); Assert.Equal(2, s.Kayitlar.Count); Assert.Equal(64, s.Ozet.Length);
        Assert.True(s.KevMi("CVE-2021-44228")); Assert.True(s.KevMi(" cve-2021-44228 ")); Assert.True(s.KevMi("CVE-2023-0001")); Assert.False(s.KevMi("CVE-2020-0001"));
        Assert.True(s.Kayitlar["CVE-2021-44228"].FidyeKullanimi); Assert.Equal(new DateTime(2021, 12, 24), s.Kayitlar["CVE-2021-44228"].SonTarih); Assert.Null(s.Kayitlar["CVE-2023-0001"].SonTarih); Assert.False(s.Kayitlar["CVE-2023-0001"].FidyeKullanimi);
    }

    [Theory]
    [InlineData("")] [InlineData("<html>404</html>")] [InlineData("[]")] [InlineData("{\"catalogVersion\":\"x\"}")] [InlineData("{\"vulnerabilities\":[]}")] [InlineData("{\"catalogVersion\":\"x\",\"vulnerabilities\":[]}")]
    [InlineData("{\"catalogVersion\":\"x\",\"vulnerabilities\":[{\"cveID\":\"HATA\",\"dateAdded\":\"2021-12-10\"}]}")]
    [InlineData("{\"catalogVersion\":\"x\",\"vulnerabilities\":[{\"cveID\":\"CVE-2021-44228\",\"dateAdded\":\"10.12.2021\"}]}")]
    public void Kev_BozukGirdi_Reddedilir(string json) => Assert.Throws<KatalogHatasi>(() => VulnFeedStore.Coz(json));

    [Fact] public void Kev_IceAktar_Atomik_EskiBeslemeKorunur_VeMetaYazilir()
    {
        Gecici(() =>
        {
            Assert.Null(VulnFeedStore.Yukle()); var s = VulnFeedStore.IceAktar(Kev, "test", T0); Assert.Equal("2026.11.17", VulnFeedStore.Yukle()!.Surum); Assert.Equal("test", VulnFeedStore.Meta()!.Value.Kaynak); Assert.Contains("2026-11-18", VulnFeedStore.Meta()!.Value.Zaman);
            Assert.Throws<KatalogHatasi>(() => VulnFeedStore.IceAktar("{\"catalogVersion\":\"yeni\",\"vulnerabilities\":[{\"cveID\":\"x\"}]}", "test", T0));
            Assert.Equal("2026.11.17", VulnFeedStore.Yukle()!.Surum); Assert.False(File.Exists(VulnFeedStore.Yol + ".tmp")); Assert.Equal(s.Ozet, VulnFeedStore.Yukle()!.Ozet);
        });
    }

    // Sahte CISA sunucusu.
    sealed class SahteCisa : HttpMessageHandler
    {
        public List<HttpRequestMessage> Istekler = new(); public Func<HttpResponseMessage> Cevap = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Kev, Encoding.UTF8, "application/json") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Istekler.Add(r); return Task.FromResult(Cevap()); }
    }

    [Fact] public async Task Indirici_Basarili_TekGetIstegi_VeriGondermez()
    {
        string eski = BulutKlasor.Kok, g = Path.Combine(Path.GetTempPath(), "kevi_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = g;
        try
        {
            var s = new SahteCisa(); var f = await KevIndirici.IndirAsync(s, T0, default);
            var r = Assert.Single(s.Istekler); Assert.Equal(HttpMethod.Get, r.Method); Assert.Equal("www.cisa.gov", r.RequestUri!.Host); Assert.Equal("https", r.RequestUri.Scheme); Assert.Null(r.Content);   // gövde gönderilmez
            Assert.Equal(KevIndirici.Adres, r.RequestUri.ToString()); Assert.Empty(r.Headers.Authorization is null ? Array.Empty<string>() : new[] { "var" }); Assert.False(r.Headers.Contains("Cookie"));
            Assert.Equal("BulutSoft-SecuritySuite/1.0", r.Headers.UserAgent.ToString());   // makine/kullanıcı bilgisi yok
            Assert.Equal(2, f.Kayitlar.Count); Assert.Equal("2026.11.17", VulnFeedStore.Yukle()!.Surum);
        }
        finally { BulutKlasor.Kok = eski; try { Directory.Delete(g, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "FpVeKevTestleri.temizlik"); } }
    }

    [Fact] public async Task Indirici_Hatalar_EskiBeslemeyiKorur()
    {
        string eski = BulutKlasor.Kok, g = Path.Combine(Path.GetTempPath(), "kevh_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = g;
        try
        {
            VulnFeedStore.IceAktar(Kev, "ilk", T0);
            // 500 hatası.
            await Assert.ThrowsAsync<KatalogHatasi>(() => KevIndirici.IndirAsync(new SahteCisa { Cevap = () => new HttpResponseMessage(HttpStatusCode.InternalServerError) }, T0, default));
            // Yönlendirme izlenmez.
            await Assert.ThrowsAsync<KatalogHatasi>(() => KevIndirici.IndirAsync(new SahteCisa { Cevap = () => { var y = new HttpResponseMessage(HttpStatusCode.Redirect); y.Headers.Location = new Uri("https://kotu.example/x.json"); return y; } }, T0, default));
            // HTML döndü (örn. captive portal).
            await Assert.ThrowsAsync<KatalogHatasi>(() => KevIndirici.IndirAsync(new SahteCisa { Cevap = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>giriş yapın</html>") } }, T0, default));
            // Çok büyük.
            await Assert.ThrowsAsync<KatalogHatasi>(() => KevIndirici.IndirAsync(new SahteCisa { Cevap = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('a', (int)VulnFeedStore.EnCokBayt + 10)) } }, T0, default));
            Assert.Equal("2026.11.17", VulnFeedStore.Yukle()!.Surum); Assert.Equal("ilk", VulnFeedStore.Meta()!.Value.Kaynak);   // hepsinde eski besleme sağlam
        }
        finally { BulutKlasor.Kok = eski; try { Directory.Delete(g, true); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "FpVeKevTestleri.temizlik"); } }
    }

    [Theory]
    [InlineData("http://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json")] [InlineData("https://kotu.example/kev.json")] [InlineData("https://www.cisa.gov.kotu.example/x.json")] [InlineData("ftp://www.cisa.gov/x")] [InlineData("bozuk")]
    public async Task Indirici_IzinsizAdres_HicIstekGondermez(string adres)
    {
        var s = new SahteCisa(); await Assert.ThrowsAsync<KatalogHatasi>(() => KevIndirici.IndirAsync(s, T0, default, adres)); Assert.Empty(s.Istekler);
    }
}
