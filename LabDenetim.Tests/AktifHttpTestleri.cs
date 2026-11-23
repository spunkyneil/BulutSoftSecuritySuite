// Aktif Web Denetimi testleri: kapsam kilidi, yalnızca izinli yöntemler, soft-404 yanlış alarm savunması, içerik saklamama, hız sınırı, doğrulama.
using System.Net;
using System.Text;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

// Sahte web sunucusu: yönlendirme tablosuna göre cevap verir ve tüm istekleri kaydeder.
internal sealed class SahteSunucu : HttpMessageHandler
{
    public List<(string Yontem, string Yol)> Kayit = new();
    public Dictionary<string, (int Kod, string Govde, Dictionary<string, string>? Baslik)> Yollar = new();
    public Func<HttpRequestMessage, HttpResponseMessage?>? Ozel;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        lock (Kayit) Kayit.Add((r.Method.Method, r.RequestUri!.AbsolutePath));
        var o = Ozel?.Invoke(r); if (o != null) return Task.FromResult(o);
        var (kod, govde, baslik) = Yollar.TryGetValue(r.RequestUri!.AbsolutePath, out var v) ? v : (404, "Not Found", null);
        var y = new HttpResponseMessage((HttpStatusCode)kod) { Content = new StringContent(govde, Encoding.UTF8, "text/html") };
        if (baslik != null) foreach (var (k, h) in baslik) { if (!y.Headers.TryAddWithoutValidation(k, h)) { y.Content.Headers.Remove(k); y.Content.Headers.TryAddWithoutValidation(k, h); } }
        return Task.FromResult(y);
    }
}

public class AktifHttpTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    const string Ic = "http://192.168.56.10:8080/";
    static async Task<AktifGozlem> Gozle(SahteSunucu s, string url = Ic, int hiz = 0) => await AktifHttp.GozlemAsync(url, s, null, hiz);
    static List<PasifBulgu> Bul(AktifGozlem g) => AktifHttp.Degerlendir(g, null, null, T0);

    // Çok açığı olan sahte sunucu.
    static SahteSunucu Zayif()
    {
        var s = new SahteSunucu();
        s.Yollar["/"] = (200, "<html>Ana</html>", new() { ["Server"] = "Apache/2.4.49 (Ubuntu)" });
        s.Yollar["/.git/HEAD"] = (200, "ref: refs/heads/main\n", null);
        s.Yollar["/.env"] = (200, "DB_PASSWORD=cokgizli123\nAPI_KEY=abc\n", null);
        s.Yollar["/phpinfo.php"] = (200, "<h1>PHP Version 8.1</h1>", null);
        s.Yollar["/admin"] = (200, "<form>giriş</form>", null);
        s.Yollar["/uploads/"] = (200, "<html><head><title>Index of /uploads</title></head>", null);
        s.Yollar["/backup.zip"] = (200, "PK-binary", new() { ["Content-Type"] = "application/zip" });
        s.Ozel = r =>
        {
            string yol = r.RequestUri!.AbsolutePath;
            if (r.Method == HttpMethod.Options) { var y = new HttpResponseMessage(HttpStatusCode.OK); y.Content.Headers.TryAddWithoutValidation("Allow", "GET, POST, PUT, DELETE, OPTIONS"); return y; }   // Allow bir İÇERİK başlığıdır (gerçek HttpClient da böyle verir)
            if (r.Method == HttpMethod.Trace) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("TRACE / HTTP/1.1\r\nHost: x") };
            if (r.Headers.TryGetValues("Origin", out var og)) { var y = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") }; y.Headers.TryAddWithoutValidation("Access-Control-Allow-Origin", og.First()); y.Headers.TryAddWithoutValidation("Access-Control-Allow-Credentials", "true"); return y; }
            if (yol.Contains("-yok")) return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("<h1>Not Found</h1><address>Apache/2.4.49 (Ubuntu) Server at x Port 80</address> at System.Web.Foo() in c:\\x.cs") };
            return null;
        };
        return s;
    }

    // ---- Kapsam kilidi ----
    [Theory]
    [InlineData("http://8.8.8.8/")] [InlineData("https://1.1.1.1:443/")] [InlineData("http://93.184.216.34/")]
    public async Task DisAg_Hedef_HicIstekGonderilmeden_Reddedilir(string url)
    {
        var s = new SahteSunucu(); await Assert.ThrowsAsync<KapsamIhlaliException>(() => Gozle(s, url));
        Assert.Empty(s.Kayit);   // ağa tek istek bile çıkmadı
    }
    [Fact] public async Task Gecersiz_Adres_Reddedilir()
    {
        var s = new SahteSunucu(); foreach (var u in new[] { "", "ftp://192.168.1.1/", "http://user:pw@192.168.1.1/", "bozuk" }) await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Gozle(s, u)); Assert.Empty(s.Kayit);
    }
    [Fact] public void Kapsam_IcHttp_DisAgdaKilitli() => Assert.Throws<KapsamIhlaliException>(() => Kapsam.KilitliyseDur(KapsamModu.DisAgPasif, ModulTuru.IcHttp));

    // ---- Güvenli yöntemler ----
    [Fact] public async Task YalnizcaGetOptionsTrace_GonderilirPutDeleteAsla()
    {
        var s = Zayif(); await Gozle(s);
        Assert.All(s.Kayit, k => Assert.Contains(k.Yontem, new[] { "GET", "OPTIONS", "TRACE" })); Assert.DoesNotContain(s.Kayit, k => k.Yontem is "PUT" or "DELETE" or "POST" or "PATCH");
        Assert.Contains(s.Kayit, k => k.Yontem == "OPTIONS"); Assert.Contains(s.Kayit, k => k.Yontem == "TRACE");
    }

    // ---- Açıkları bulur ----
    [Fact] public async Task ZayifSunucu_BeklenenBulgular()
    {
        var g = await Gozle(Zayif()); var l = Bul(g); var idler = l.Select(x => x.RuleId).ToHashSet();
        foreach (var beklenen in new[] { "ACT-TRACE-ENABLED", "ACT-METHOD-PUT", "ACT-METHOD-DELETE", "ACT-CORS-REFLECT", "ACT-CORS-REFLECT-CRED", "ACT-GIT-EXPOSED", "ACT-ENV-EXPOSED", "ACT-PHPINFO-EXPOSED", "ACT-ADMIN-OPEN", "ACT-BACKUP-EXPOSED", "ACT-DIR-LISTING", "ACT-ERR-VERSION", "ACT-ERR-STACKTRACE", "ACT-SERVER-VERSION" })
            Assert.True(idler.Contains(beklenen), "Bulgu yok: " + beklenen + " (var olanlar: " + string.Join(",", idler) + ")");
        // En yüksek etki: .env (9) ve CORS+çerez (8).
        Assert.Equal("ACT-ENV-EXPOSED", l[0].RuleId);
        Assert.Contains("/uploads/", g.Olgular["probe:dir_listing"]); Assert.Contains("/backup.zip", g.Olgular["probe:backup_found"]);
    }

    [Fact] public async Task Bulgular_SemayaUyar_VeIcerikRaporaKopyalanmaz()
    {
        var g = await Gozle(Zayif()); var f = AktifHttp.BulguUret(g, Bul(g), 0.5, T0);
        Assert.NotEmpty(f); Assert.All(f, x => Assert.Empty(FindingSchema.Validate(x)));
        string tum = string.Join("\n", f.SelectMany(x => x.Evidence.Append(x.Title).Append(x.Explanation.What)));
        Assert.DoesNotContain("cokgizli123", tum); Assert.DoesNotContain("API_KEY=abc", tum); Assert.DoesNotContain("refs/heads/main", tum);   // .env / .git içeriği saklanmadı
        Assert.All(f, x => Assert.Equal("web:192.168.56.10:8080", x.AssetId)); Assert.Equal(f.Count, f.Select(x => x.Id).Distinct().Count());
        Assert.Equal(f.Select(x => x.Id), AktifHttp.BulguUret(g, Bul(g), 0.5, T0).Select(x => x.Id));   // kararlı kimlik
    }

    // ---- Yanlış alarm savunması ----
    [Fact] public async Task Soft404_HerYoluAyniSayfaIleCevaplayanSunucu_BulguUretmez()
    {
        var s = new SahteSunucu { Ozel = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Tek sayfalık uygulama</html>") } };
        var g = await Gozle(s); Assert.Equal("true", g.Olgular["probe:soft404"]);
        Assert.DoesNotContain(Bul(g), x => x.RuleId is "ACT-GIT-EXPOSED" or "ACT-ENV-EXPOSED" or "ACT-ADMIN-OPEN" or "ACT-PHPINFO-EXPOSED" or "ACT-BACKUP-EXPOSED" or "ACT-DIR-LISTING");
        Assert.Contains(g.Notlar, n => n.Contains("soft 404"));
    }
    [Fact] public async Task Soft404_AmaGercekGitDosyasiFarkliIcerikle_YineBulunur()
    {
        var s = new SahteSunucu { Ozel = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(r.RequestUri!.AbsolutePath == "/.git/HEAD" ? "ref: refs/heads/main" : "<html>uygulama</html>") } };
        Assert.Contains(Bul(await Gozle(s)), x => x.RuleId == "ACT-GIT-EXPOSED");
    }
    [Fact] public async Task YetkiGerektirenYol_401_403_Bulgu_Degil()
    {
        var s = new SahteSunucu(); s.Yollar["/.env"] = (403, "Forbidden", null); s.Yollar["/admin"] = (401, "Unauthorized", null); s.Yollar["/.git/HEAD"] = (403, "x", null);
        var g = await Gozle(s); Assert.Equal("auth", g.Olgular["probe:path:/.env"]); Assert.DoesNotContain(Bul(g), x => x.RuleId.StartsWith("ACT-ENV") || x.RuleId == "ACT-ADMIN-OPEN" || x.RuleId == "ACT-GIT-EXPOSED");
    }
    [Fact] public async Task YanlisIcerik_YolVarAmaGitDegil_Bulgu_Degil()
    {
        var s = new SahteSunucu(); s.Yollar["/.git/HEAD"] = (200, "<html>Özel 404 sayfası</html>", null); s.Yollar["/.env"] = (200, "<html>sayfa bulunamadı</html>", null);
        Assert.DoesNotContain(Bul(await Gozle(s)), x => x.RuleId is "ACT-GIT-EXPOSED" or "ACT-ENV-EXPOSED");
    }
    [Fact] public async Task TemizSunucu_ProbeBulgusuYok()
    {
        var s = new SahteSunucu(); s.Yollar["/"] = (200, "<html>Merhaba</html>", null); var g = await Gozle(s);
        Assert.DoesNotContain(Bul(g), x => x.Modul == "Aktif HTTP"); Assert.Equal("false", g.Olgular["probe:trace_enabled"]); Assert.Equal("false", g.Olgular["probe:cors_reflect"]);
    }

    // ---- Sınırlar ----
    [Fact] public async Task IstekSayisi_Sinirli_VeRaporlanir()
    {
        var s = Zayif(); var g = await Gozle(s); Assert.True(g.IstekSayisi is > 10 and <= AktifHttp.EnCokIstek); Assert.Equal(s.Kayit.Count, g.IstekSayisi); Assert.Equal(g.IstekSayisi.ToString(), g.Olgular["probe:requests"]);
    }
    [Fact] public async Task HizSiniri_IsteklerArasiBekler()
    {
        var s = new SahteSunucu(); var sw = System.Diagnostics.Stopwatch.StartNew(); var g = await Gozle(s, Ic, 40); sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= (g.IstekSayisi - 1) * 40 * 0.8, $"Hız sınırı uygulanmadı: {g.IstekSayisi} istek {sw.ElapsedMilliseconds} ms");
    }
    [Fact] public async Task AnaSayfaYanitVermezse_AnlasilirHata()
    {
        var s = new SahteSunucu { Ozel = r => throw new HttpRequestException("bağlantı reddedildi") };
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Gozle(s)); Assert.Contains("bağlanılamadı", e.Message);
    }
    [Fact] public async Task Yonlendirme_Izlenmez()
    {
        var s = new SahteSunucu { Ozel = r => { var y = new HttpResponseMessage(HttpStatusCode.Redirect); y.Headers.Location = new Uri("http://8.8.8.8/gizli"); return y; } };
        var g = await Gozle(s); Assert.DoesNotContain(s.Kayit, k => k.Yol == "/gizli"); Assert.Contains("8.8.8.8", g.AnaSayfa!.Yonlendirmeler[0]);
    }
    [Fact] public async Task CerezKurali_PasifKurallarlaBirlesik()
    {
        var s = new SahteSunucu(); s.Yollar["/"] = (200, "<html>x</html>", new() { ["Set-Cookie"] = "oturum=abc123; Path=/" });
        var l = Bul(await Gozle(s)); Assert.Contains(l, x => x.Modul == "HTTP"); Assert.DoesNotContain(l, x => x.Kanit.Contains("abc123"));   // çerez değeri maskelenir
    }

    // ---- Doğrulama: açık kapatılınca yeniden çalıştırma ----
    [Fact] public async Task Dogrulama_AcikKapaninca_Pass_KapanmayanOpen()
    {
        var s = Zayif(); var g1 = await Gozle(s); var once = AktifHttp.BulguUret(g1, Bul(g1), 0.5, T0).Where(f => f.RuleId is "ACT-ENV-EXPOSED" or "ACT-GIT-EXPOSED").ToList(); Assert.Equal(2, once.Count);
        s.Yollar.Remove("/.env");   // .env düzeltildi, .git hâlâ açık
        var v = new VerificationService(); v.Kaydet(new AktifHttpDogrulamaKontrolu(async ct => { var g = await Gozle(s); return AktifHttp.BulguUret(g, Bul(g), 0.5, T0); }));
        var r = await v.CalistirAsync(AktifHttp.KontrolKimligi, once, default); Assert.Equal(VerificationOutcome.Fail, r.Aggregate);
        var d = VerificationService.DurumUygula(once, r, T0); Assert.Equal(FindingState.Verified, d.Single(f => f.RuleId == "ACT-ENV-EXPOSED").State); Assert.Equal(FindingState.Open, d.Single(f => f.RuleId == "ACT-GIT-EXPOSED").State);
    }

    // Gerçek HTTP yığınıyla (sahte işleyici YOK) kendi bilgisayarımızdaki geçici bir Python sunucusuna karşı deneme. Varsayılan kapalı: BULUT_CANLI_TEST=1.
    [Trait("Category", "Integration")]
    [PythonFact("BULUT_CANLI_TEST")] public async Task Canli_YerelPythonSunucusu_GercekYigin()
    {
        string py = TestPython.Yol!;   // [PythonFact("BULUT_CANLI_TEST")]: Python yoksa veya BULUT_CANLI_TEST=1 değilse test AÇIKÇA atlanır
        string dizin = Path.Combine(Path.GetTempPath(), "canli_http_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dizin); File.WriteAllText(Path.Combine(dizin, ".env"), "ORNEK_ANAHTAR=sahte-deger\n"); File.WriteAllText(Path.Combine(dizin, "index.html"), "<html>test</html>");
        var psi = new System.Diagnostics.ProcessStartInfo(py) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dizin }; foreach (var a in new[] { "-m", "http.server", "18765", "--bind", "127.0.0.1" }) psi.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(psi)!;
        try
        {
            await Task.Delay(1500);
            var g = await AktifHttp.GozlemAsync("http://127.0.0.1:18765/", null, null, 0);   // gerçek işleyici, gerçek soket
            var l = Bul(g); Console.WriteLine($"CANLI-HTTP: istek={g.IstekSayisi} bulgu={l.Count} soft404={g.Olgular["probe:soft404"]} -> " + string.Join(", ", l.Select(x => x.RuleId)));
            Assert.Contains(l, x => x.RuleId == "ACT-ENV-EXPOSED"); Assert.Contains(l, x => x.RuleId == "ACT-DIR-LISTING" || x.RuleId == "ACT-SERVER-VERSION");
        }
        finally { try { p.Kill(true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "AktifHttpTestleri.temizlik"); } try { Directory.Delete(dizin, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "AktifHttpTestleri.temizlik"); } }
    }

    // ---- Kural dosyası ----
    [Fact] public void AktifKurallar_GomuluDosya_GecerliVeBenzersiz()
    {
        var l = PasifKurallar.Yukle(out var uyari, null, "aktif_kurallar.json", Path.Combine(Path.GetTempPath(), "yok_" + Guid.NewGuid().ToString("N") + ".json"));
        Assert.Empty(uyari); Assert.True(l.Count >= 15); Assert.Equal(l.Count, l.Select(x => x.Id).Distinct().Count()); Assert.All(l, k => Assert.Equal("Aktif HTTP", k.Module));
        Assert.All(l, k => { Assert.NotEmpty(k.Kvkk); Assert.NotEmpty(k.Iso); });
    }
}
