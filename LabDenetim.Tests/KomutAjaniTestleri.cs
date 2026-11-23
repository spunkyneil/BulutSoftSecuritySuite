// Komut ajanı (KomutAjani.cs, gerçek Python ile) ve gömülü araç tablosu (kali_araclari.json + KomutKaydi.cs) testleri.
// Ajan testleri sahte araç olarak Python'un kendisini kullanır (yalnızca standart kütüphane); Python yoksa ilgili testler atlanır (diğer ajan testleriyle aynı yöntem).
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace LabDenetim.Tests;

// Statik kayıtları (şablon deposu, izinli hedefler) paylaştığı için diğer "Veri" testleriyle sırayla çalışır.
[Collection("Veri")]
public class KomutAjaniTestleri : IDisposable
{
    public KomutAjaniTestleri() { KomutSablonuDeposu.Temizle(); HedefDogrulama.TestHedefleriniTemizle(); }
    public void Dispose() { KomutSablonuDeposu.Temizle(); HedefDogrulama.TestHedefleriniTemizle(); }

    private static readonly string Py = TestPython.Yol ?? "python";   // Python yoksa Python'a bağlı testler [PythonFact] ile AÇIKÇA ATLANIR (sessizce geçmez)

    // Ajanın tüm çıktı satırları.
    private sealed record Cevap(int Kod, List<JsonElement> Satirlar, string Hata)
    {
        public IEnumerable<string> Cikti(string akis) => Satirlar.Where(s => s.GetProperty("t").GetString() == "cikti" && s.GetProperty("akis").GetString() == akis).Select(s => s.GetProperty("s").GetString()!);
        public JsonElement? Tur(string t) => Satirlar.Where(s => s.GetProperty("t").GetString() == t).Select(s => (JsonElement?)s).FirstOrDefault();
        public string? HataMesaji => Tur("error")?.GetProperty("m").GetString();
        public int? BitisKodu => Tur("done")?.GetProperty("kod").GetInt32();
    }

    // Şablon ve parametre üreticileri (ajanın tablo biçimi).
    private static object Sab(string ad, string[] argv, string hedefTuru = "yok", int zaman = 30, bool sudo = false, object[]? prm = null, bool arkaPlan = false, Dictionary<string, string>? ortam = null, string arac = "", string paket = "") =>
        new { ad, hedefTuru, zamanAsimi = zaman, sudo, arkaPlan, arac = arac == "" ? argv[0] : arac, paket, parametreler = prm ?? Array.Empty<object>(), argv, ortam };
    private static object P(string ad, string desen, string tur = "", int? min = null, int? max = null) => new { ad, desen, tur, min, max };
    private static string Tablo(params object[] s) => JsonSerializer.Serialize(s);
    private static object Istek(string sablon, string hedef = "", string[]? arg = null, string[]? izinli = null, int? zaman = null, string mod = "komut") => new { mod, sablon, hedef, arg = arg ?? Array.Empty<string>(), izinli = izinli ?? Array.Empty<string>(), zaman };

    // Ajanı gerçek Python ile çalıştırır (betik stdin'den, tıpkı ssh üzerinden olduğu gibi). ev: sahte ev klasörü (~/bulutsoft_inceleme için).
    private static Cevap Ajan(string tablo, object istek, string mod = "run", string? ev = null)
    {
        var psi = new ProcessStartInfo(Py) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        psi.ArgumentList.Add("-"); psi.ArgumentList.Add(mod); psi.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(istek))));
        if (ev != null) { psi.Environment["USERPROFILE"] = ev; psi.Environment["HOME"] = ev; }
        using var p = Process.Start(psi)!; var err = p.StandardError.ReadToEndAsync();
        p.StandardInput.Write(KomutAjani.Kaynak(tablo)); p.StandardInput.Close();
        var l = new List<JsonElement>(); string? s;
        while ((s = p.StandardOutput.ReadLine()) != null) if (s.Length > 0 && s[0] == '{') { using var d = JsonDocument.Parse(s); l.Add(d.RootElement.Clone()); }
        p.WaitForExit(120000); return new Cevap(p.ExitCode, l, err.Result);
    }

    // ================================================================ Ajan (gerçek Python)
    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_StdoutStderrAyriAkislarVeCikisKodu()
    {
        var c = Ajan(Tablo(Sab("py-cikti", new[] { Py, "-c", "import sys; print('merhaba'); print('hata', file=sys.stderr); sys.exit(3)" })), Istek("py-cikti"));
        Assert.Equal(new[] { "merhaba" }, c.Cikti("stdout")); Assert.Equal(new[] { "hata" }, c.Cikti("stderr")); Assert.Equal(3, c.BitisKodu); Assert.False(c.Tur("done")!.Value.GetProperty("zamanasimi").GetBoolean()); Assert.Null(c.HataMesaji); Assert.Equal(0, c.Kod);
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_HedefVeParametreleriYenidenDogrular()
    {
        string t = Tablo(Sab("py-param", new[] { Py, "-c", "import sys; print(sys.argv[1], sys.argv[2])", "{hedef}", "{n}" }, "ip", prm: new[] { P("n", @"\d{1,3}", min: 1, max: 500) }));
        // Geçerli çağrı.
        var ok = Ajan(t, Istek("py-param", "192.168.56.1", new[] { "7" })); Assert.Equal(new[] { "192.168.56.1 7" }, ok.Cikti("stdout")); Assert.Equal(0, ok.BitisKodu);
        // İzinli listedeki genel adres kabul edilir, listede olmayan edilmez.
        Assert.Equal(0, Ajan(t, Istek("py-param", "8.8.8.8", new[] { "7" }, new[] { "8.8.8.0/24" })).BitisKodu);
        Assert.Contains("ozel ag", Ajan(t, Istek("py-param", "8.8.8.8", new[] { "7" })).HataMesaji);
        // Reddedilen hedefler.
        foreach (var h in new[] { "", "example.com", "192.168.56.1\n", "010.0.0.1", "10.1", "10.0.0.1;id", "127.0.0.1x", "-10.0.0.1", "192.168.56.1 " }) Assert.NotNull(Ajan(t, Istek("py-param", h, new[] { "7" })).HataMesaji);
        // Reddedilen parametreler: aralık dışı, enjeksiyon, '-' ile başlayan, Unicode rakam, sayı yanlış.
        foreach (var a in new[] { "501", "0", "7; id", "-5", "\u0667", "7\n", "abc", "" }) Assert.NotNull(Ajan(t, Istek("py-param", "192.168.56.1", new[] { a })).HataMesaji);
        Assert.NotNull(Ajan(t, Istek("py-param", "192.168.56.1", new[] { "7", "8" })).HataMesaji); Assert.NotNull(Ajan(t, Istek("py-param", "192.168.56.1")).HataMesaji);   // parametre sayısı uyuşmuyor
        // Bilinmeyen şablon ve kip.
        Assert.Contains("bilinmeyen sablon", Ajan(t, Istek("yok-boyle-sablon", "192.168.56.1")).HataMesaji); Assert.Contains("bilinmeyen kontrol", Ajan(t, Istek("py-param", "192.168.56.1", new[] { "7" }, mod: "baska")).HataMesaji);
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_AlanAdiHedefiYalnizcaIzinliListedenKabulEder()
    {
        string t = Tablo(Sab("py-alan", new[] { Py, "-c", "print('tamam')", "{hedef}" }, "alan"));
        Assert.Contains("izinli listede degil", Ajan(t, Istek("py-alan", "musteri.example")).HataMesaji);
        Assert.Equal(0, Ajan(t, Istek("py-alan", "musteri.example", izinli: new[] { "musteri.example" })).BitisKodu);
        Assert.NotNull(Ajan(t, Istek("py-alan", "www.musteri.example", izinli: new[] { "musteri.example" })).HataMesaji);      // alt alan adı otomatik izinli değil
        Assert.Contains("alan adi ister", Ajan(t, Istek("py-alan", "10.0.0.1")).HataMesaji);                                   // IP, alan adı şablonuna verilemez
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_ZamanAsimi_SurecOldurulurVeKodVerir()
    {
        string t = Tablo(Sab("py-uyku", new[] { Py, "-c", "import time; time.sleep(30)" }, zaman: 2)); var sw = Stopwatch.StartNew();
        var c = Ajan(t, Istek("py-uyku")); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20)); Assert.Equal(124, c.BitisKodu); Assert.True(c.Tur("done")!.Value.GetProperty("zamanasimi").GetBoolean()); Assert.Contains(c.Cikti("stderr"), x => x.Contains("zaman asimi"));
        // İstek süreyi kısaltabilir ama uzatamaz.
        string t2 = Tablo(Sab("py-uyku", new[] { Py, "-c", "import time; time.sleep(30)" }, zaman: 100)); sw.Restart(); Assert.Equal(124, Ajan(t2, Istek("py-uyku", zaman: 1)).BitisKodu); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20));
        sw.Restart(); Assert.Equal(124, Ajan(t, Istek("py-uyku", zaman: 500)).BitisKodu); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20));   // 500 istendi ama şablon 2 sn
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_BuyukCikti_KilitlenmezVeHerSatirGelir()
    {
        string t = Tablo(Sab("py-buyuk", new[] { Py, "-c", "import sys\nfor i in range(20000):\n    print('satir', i, 'x' * 40)\n    sys.stderr.write('e%d\\n' % i)" }, zaman: 120));
        var sw = Stopwatch.StartNew(); var c = Ajan(t, Istek("py-buyuk")); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(100));
        Assert.Equal(20000, c.Cikti("stdout").Count()); Assert.Equal(20000, c.Cikti("stderr").Count()); Assert.Equal(0, c.BitisKodu);
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_KuruluOlmayanAraci_KurulumIpucuylaBildirir()
    {
        var c = Ajan(Tablo(Sab("yok-arac", new[] { "bs-yok-boyle-arac-xyz" }, paket: "bs-paket")), Istek("yok-arac")); Assert.Contains("sensorde kurulu degil", c.HataMesaji); Assert.Contains("sudo apt install bs-paket", c.HataMesaji);
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_Onizle_SudoSarmaliniVeYerTutuculariGosterir_Calistirmaz()
    {
        string t = Tablo(Sab("onizle-sudo", new[] { "ornekarac", "-c", "{n}", "{hedef}" }, "ip", 30, true, new[] { P("n", @"\d{1,4}") }));
        var c = Ajan(t, Istek("onizle-sudo", "192.168.56.1", new[] { "5" }), "onizle");   // araç yok: çalıştırılsaydı hata verirdi
        Assert.Null(c.HataMesaji); Assert.Equal(new[] { "sudo", "-n", "timeout", "--kill-after=5", "30", "ornekarac", "-c", "5", "192.168.56.1" }, c.Tur("onizle")!.Value.GetProperty("argv").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal("10", Ajan(t, Istek("onizle-sudo", "192.168.56.1", new[] { "5" }, zaman: 10), "onizle").Tur("onizle")!.Value.GetProperty("argv")[4].GetString());   // süre kısaltılır
        Assert.Equal(new[] { "ornekarac", "-c", "5", "192.168.56.1" }, Ajan(Tablo(Sab("onizle-sudosuz", new[] { "ornekarac", "-c", "{n}", "{hedef}" }, "ip", 30, false, new[] { P("n", @"\d{1,4}") })), Istek("onizle-sudosuz", "192.168.56.1", new[] { "5" }), "onizle").Tur("onizle")!.Value.GetProperty("argv").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_IstekteSudoAlaniniYokSayar()
    {
        // Tablo sudo istemiyorsa, istek "sudo":true gönderse bile sarmal eklenmez (ajan tablosuna güvenir, isteğe değil).
        string t = Tablo(Sab("sudosuz", new[] { "ornekarac" })); var istek = new { mod = "komut", sablon = "sudosuz", hedef = "", arg = Array.Empty<string>(), sudo = true, zaman = 5 };
        Assert.Equal(new[] { "ornekarac" }, Ajan(t, istek, "onizle").Tur("onizle")!.Value.GetProperty("argv").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_DosyaParametresi_YalnizcaIncelemeKlasorundenOkur()
    {
        string ev = Path.Combine(Path.GetTempPath(), "inc_" + Guid.NewGuid().ToString("N")); string d = Path.Combine(ev, "bulutsoft_inceleme"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.bin"), "icerik-1"); Directory.CreateDirectory(Path.Combine(d, "altklasor"));
        try
        {
            string t = Tablo(Sab("py-dosya", new[] { Py, "-c", "import sys; print(open(sys.argv[1], 'rb').read().decode())", "{inceleme}/{dosya}" }, prm: new[] { P("dosya", "[A-Za-z0-9][A-Za-z0-9._-]{0,99}", "dosya") }));
            Assert.Equal(new[] { "icerik-1" }, Ajan(t, Istek("py-dosya", arg: new[] { "a.bin" }), ev: ev).Cikti("stdout"));
            Assert.Contains("dosya bulunamadi", Ajan(t, Istek("py-dosya", arg: new[] { "yok.bin" }), ev: ev).HataMesaji);                 // yok
            Assert.Contains("dosya bulunamadi", Ajan(t, Istek("py-dosya", arg: new[] { "altklasor" }), ev: ev).HataMesaji);               // klasör dosya değildir
            foreach (var a in new[] { "..", "..\\a.bin", "../a.bin", "a.bin/../a.bin", "C:\\Windows\\win.ini", "/etc/passwd", "-a.bin" }) Assert.NotNull(Ajan(t, Istek("py-dosya", arg: new[] { a }), ev: ev).HataMesaji);   // kaçış denemeleri
        }
        finally { try { Directory.Delete(ev, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "KomutAjaniTestleri.temizlik"); } }
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_ArkaPlanAraci_BaslatirVeBekletmez()
    {
        string t = Tablo(Sab("py-arka", new[] { Py, "-c", "import time; time.sleep(6)" }, arkaPlan: true)); var sw = Stopwatch.StartNew();
        var c = Ajan(t, Istek("py-arka")); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5.5)); Assert.Equal(0, c.BitisKodu); Assert.Contains(c.Cikti("stdout"), x => x.Contains("baslatildi"));
        // Hemen biten arka plan süreci hata kodunu bildirir.
        var c2 = Ajan(Tablo(Sab("py-arka2", new[] { Py, "-c", "import sys; sys.exit(4)" }, arkaPlan: true)), Istek("py-arka2")); Assert.Equal(4, c2.BitisKodu); Assert.Contains(c2.Cikti("stderr"), x => x.Contains("hemen sonlandi"));
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_ArayuzParametresi_ArayuzListesiOkunamazsaReddeder()
    {
        if (Directory.Exists("/sys/class/net")) return;   // Linux'ta bu test anlamsız (gerçek arayüz listesi var)
        var c = Ajan(Tablo(Sab("py-arayuz", new[] { Py, "-c", "print(1)", "{a}" }, prm: new[] { P("a", "[a-z0-9]+", "arayuz") })), Istek("py-arayuz", arg: new[] { "eth0" })); Assert.Contains("arayuz", c.HataMesaji);
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_AnsiTemizler_OrtamDegiskeniniAktarir_VeUzunSatiriKisaltir()
    {
        string t = Tablo(Sab("py-ortam", new[] { Py, "-c", "import os; print('\\x1b[31mkirmizi\\x1b[0m'); print(os.environ.get('BS_TEST')); print('y' * 300000)" }, ortam: new Dictionary<string, string> { ["BS_TEST"] = "deger" }));
        var c = Ajan(t, Istek("py-ortam")).Cikti("stdout").ToList(); Assert.Equal("kirmizi", c[0]); Assert.Equal("deger", c[1]); Assert.True(c[2].Length < 201000); Assert.EndsWith("(kisaltildi)", c[2]);
    }

    // ================================================================ Gömülü tablo
    [Fact] public void GomuluTablo_YuklenirGecerliVeBenzersiz()
    {
        var l = KomutKaydi.Tanimlar; Assert.True(l.Count >= 23); Assert.Equal(l.Count, l.Select(x => x.Ad).Distinct().Count());
        foreach (var t in l) { Assert.Null(KomutKaydi.TanimHatasi(t)); Assert.Equal(t.Arac, t.Argv![0]); }
        // Beklenen araçlar ve özellikler.
        Assert.All(new[] { "maltego-ac", "burpsuite-ac" }, a => { var t = KomutKaydi.Tanim(a)!; Assert.True(t.ArkaPlan); Assert.Equal(":0", t.Ortam!["DISPLAY"]); Assert.Equal("yok", t.HedefTuru); Assert.False(t.Sudo); });
        Assert.Equal(new[] { "netdiscover-ag", "tcpdump-yakala", "tor-baslat", "tor-durdur" }, l.Where(x => x.Sudo).Select(x => x.Ad).OrderBy(x => x).ToArray());
        Assert.All(l.Where(x => x.HedefTuru == "alan"), x => Assert.True(x.Internet));
        // Ajan tablosu geçerli bir JSON dizisidir ve ajan kaynağına gömülür.
        using var d = JsonDocument.Parse(KomutKaydi.AjanTablosuJson); Assert.Equal(l.Count, d.RootElement.GetArrayLength()); string k = KomutAjani.Kaynak(KomutKaydi.AjanTablosuJson); Assert.DoesNotContain("@@TABLO@@", k); Assert.Contains("def main()", k);
    }

    [Fact] public void Hazirla_SablonlariKaydeder_ParametreKurallariCalisir()
    {
        Assert.Equal(KomutKaydi.Tanimlar.Count, KomutKaydi.Hazirla()); Assert.Equal(0, KomutKaydi.Hazirla());   // ikinci çağrı yinelenen eklemez
        var h = KomutSablonuDeposu.Getir("theharvester-alan")!; Assert.Equal("theHarvester", h.AracAdi); Assert.Equal("theharvester", h.PaketAdi); Assert.Equal("alan", h.HedefTuru);
        Assert.Equal(new[] { "crtsh", "50" }, KomutSablonuDeposu.Uyarilastir(h, new Dictionary<string, string> { ["kaynak"] = "crtsh", ["limit"] = "50" }));
        Assert.Equal(new[] { "crtsh", "100" }, KomutSablonuDeposu.Uyarilastir(h, null));
        foreach (var kotu in new[] { "501", "0", "5; id", "\u0665" }) Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(h, new Dictionary<string, string> { ["limit"] = kotu }));
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(h, new Dictionary<string, string> { ["kaynak"] = "crtsh; id" }));
        var tc = KomutSablonuDeposu.Getir("tcpdump-yakala")!; foreach (var kotu in new[] { "0", "5001", "-1", "10 -w x" }) Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(tc, new Dictionary<string, string> { ["paket_sayisi"] = kotu }));
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(KomutSablonuDeposu.Getir("tshark-pcap-ozet")!, new Dictionary<string, string> { ["dosya"] = "../x.pcap" }));
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void GomuluTablo_ButunSablonlarAjandaOnizlenir_ArgumanListesiTutarli()
    {
        // Tablodaki HER şablon, gerçek ajanla (çalıştırmadan) önizlenir: hedef/parametre/dosya doğrulaması ve yer tutucu değiştirme tabloyla birebir uyumlu olmalı.
        string ev = Path.Combine(Path.GetTempPath(), "inc_" + Guid.NewGuid().ToString("N")); string d = Path.Combine(ev, "bulutsoft_inceleme"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "ornek.pcap"), "x"); File.WriteAllText(Path.Combine(d, "ornek.bin"), "x");
        try
        {
            int denenen = 0;
            foreach (var t in KomutKaydi.Tanimlar)
            {
                var prm = t.Parametreler ?? new(); if (prm.Any(p => p.Tur == "arayuz") && !Directory.Exists("/sys/class/net")) continue;   // arayüz listesi yalnızca Linux'ta var
                string hedef = t.HedefTuru switch { "yok" => "", "alan" => "musteri-staging.example", _ => "192.168.56.101" };
                var arg = prm.Select(p => !string.IsNullOrEmpty(p.Varsayilan) ? p.Varsayilan! : p.Desen.Contains("pcap") ? "ornek.pcap" : "ornek.bin").ToArray();
                var c = Ajan(KomutKaydi.AjanTablosuJson, Istek(t.Ad, hedef, arg, new[] { "musteri-staging.example" }), "onizle", ev);
                var on = c.Tur("onizle"); Assert.True(on != null, $"{t.Ad}: önizleme alınamadı: {c.HataMesaji}");
                var argv = on!.Value.GetProperty("argv").EnumerateArray().Select(x => x.GetString()!).ToList();
                int i = 0; if (t.Sudo) { Assert.Equal(new[] { "sudo", "-n", "timeout", "--kill-after=5", t.ZamanAsimi.ToString() }, argv.Take(5).ToArray()); i = 5; }
                Assert.Equal(t.Arac, argv[i]); Assert.DoesNotContain(argv, a => a.Contains('{') || a.Contains('}')); Assert.Equal(t.Argv!.Count, argv.Count - i); denenen++;
            }
            Assert.True(denenen >= 20, "önizlenen şablon sayısı: " + denenen);
        }
        finally { try { Directory.Delete(ev, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "KomutAjaniTestleri.temizlik"); } }
    }

    // Geçerli tek tanım (bozma denemeleri için temel).
    private const string Temel = "[{\"ad\":\"ornek-arac\",\"kategori\":\"genel\",\"arac\":\"ornekarac\",\"paket\":\"ornekarac\",\"sudo\":false,\"tahmini\":5,\"zamanAsimi\":30,\"hedefTuru\":\"ip\",\"ayirici\":\"satir-listesi\",\"internet\":false,\"arkaPlan\":false,\"dogrulandi\":false,\"aciklama\":\"x\",\"parametreler\":[{\"ad\":\"n\",\"desen\":\"[0-9]+\",\"varsayilan\":\"1\"}],\"argv\":[\"ornekarac\",\"-n\",\"{n}\",\"{hedef}\"]}]";

    [Fact] public void Coz_GecerliTanimiKabulEder()
    {
        Assert.Single(KomutKaydi.Coz(Temel));
    }

    [Theory]
    [InlineData("\"{n}\"", "\"{x}\"")]                                                  // bilinmeyen yer tutucu
    [InlineData("\"argv\":[\"ornekarac\"", "\"argv\":[\"baska\"")]                       // ilk öğe araç adıyla aynı değil
    [InlineData("\"-n\",\"{n}\",", "\"-n\",")]                                           // kullanılmayan parametre
    [InlineData("\"hedefTuru\":\"ip\"", "\"hedefTuru\":\"yok\"")]                        // hedefsiz şablonda {hedef}
    [InlineData(",\"{hedef}\"]", "]")]                                                   // hedefli şablonda {hedef} yok
    [InlineData("\"hedefTuru\":\"ip\"", "\"hedefTuru\":\"rastgele\"")]                   // geçersiz hedef türü
    [InlineData("\"-n\"", "\"-n\\n\"")]                                                  // satır sonu
    [InlineData("\"-n\"", "\"-ş\"")]                                                     // Unicode
    [InlineData("\"{n}\"", "\"{n}{\"")]                                                  // artık kıvrık ayraç
    [InlineData("\"[0-9]+\"", "\"(\"")]                                                  // geçersiz düzenli ifade
    [InlineData("\"satir-listesi\"", "\"yok-boyle-ayirici\"")]                           // kayıtsız ayrıştırıcı
    [InlineData("\"ad\":\"ornek-arac\"", "\"ad\":\"Ornek Arac\"")]                       // geçersiz ad
    [InlineData("\"arac\":\"ornekarac\"", "\"arac\":\"\"")]                              // boş araç
    [InlineData("\"varsayilan\":\"1\"", "\"varsayilan\":\"x\"")]                         // varsayılan kuralına uymuyor
    [InlineData("\"varsayilan\":\"1\"", "\"varsayilan\":\"1\",\"tur\":\"rastgele\"")]     // geçersiz parametre türü
    [InlineData("\"varsayilan\":\"1\"", "\"varsayilan\":\"1\",\"tur\":\"dosya\"")]        // dosya parametresi {inceleme} gerektirir
    [InlineData("\"internet\":false", "\"internet\":false,\"ortam\":{\"PATH\":\"/x\"}")]   // PATH değiştirilemez
    [InlineData("\"internet\":false", "\"internet\":false,\"ortam\":{\"kucuk\":\"x\"}")]   // geçersiz ortam adı
    [InlineData("\"zamanAsimi\":30", "\"zamanAsimi\":0")]                                // geçersiz süre
    [InlineData("\"sudo\":false,\"tahmini\":5", "\"sudo\":true,\"tahmini\":5,\"hedefTuru2\":1")]   // (geçerli kalır: bilinmeyen alan yok sayılır; aşağıda ayrıca ele alınır)
    public void Coz_BozukTanimiReddeder(string eski, string yeni)
    {
        string metin = Temel.Replace(eski, yeni); Assert.NotEqual(Temel, metin);
        if (yeni.Contains("hedefTuru2")) { Assert.Single(KomutKaydi.Coz(metin)); return; }   // bilinmeyen JSON alanı zararsızdır
        Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz(metin));
    }

    [Fact] public void Coz_ArkaPlanVeSudo_BosVeBozukTablo_YinelenenAdi_Reddeder()
    {
        Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz(Temel.Replace("\"sudo\":false", "\"sudo\":true").Replace("\"arkaPlan\":false", "\"arkaPlan\":true").Replace("\"hedefTuru\":\"ip\"", "\"hedefTuru\":\"yok\"").Replace(",\"{hedef}\"]", "]")));
        Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz("[]")); Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz("{")); Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz("null"));
        string ikili = "[" + Temel.Trim('[', ']') + "," + Temel.Trim('[', ']') + "]"; var ex = Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz(ikili)); Assert.Contains("aynı", ex.Message);
        // Bir hatalı tanım TÜM tabloyu reddeder (kısmi yükleme yok).
        string karma = "[" + Temel.Trim('[', ']') + "," + Temel.Trim('[', ']').Replace("ornek-arac", "ornek-arac2").Replace("\"{n}\"", "\"{x}\"") + "]"; Assert.Throws<InvalidOperationException>(() => KomutKaydi.Coz(karma));
    }

    [Fact] public void TurHatasi_HedefTurunuDenetler()
    {
        Assert.Null(HedefDogrulama.TurHatasi("yok", "")); Assert.NotNull(HedefDogrulama.TurHatasi("yok", "10.0.0.1")); Assert.Null(HedefDogrulama.TurHatasi("ip", "10.0.0.1")); Assert.NotNull(HedefDogrulama.TurHatasi("ip", "ornek.example"));
        Assert.Null(HedefDogrulama.TurHatasi("alan", "ornek.example")); Assert.NotNull(HedefDogrulama.TurHatasi("alan", "10.0.0.1")); Assert.Null(HedefDogrulama.TurHatasi("ip-alan", "10.0.0.1")); Assert.Null(HedefDogrulama.TurHatasi("ip-alan", "ornek.example"));
    }

    [Fact] public void ParametreSayiSiniri_ErkenReddedilir()
    {
        var s = new KomutSablonu("sayi", "genel", false, 5, 30, new[] { new ParametreKurali("n", @"\d+", "5", 1, 10) }, "satir-listesi");
        KomutSablonuDeposu.Ekle(s); Assert.Equal(new[] { "10" }, KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["n"] = "10" }));
        foreach (var kotu in new[] { "0", "11", "1000000000", "\u0665" }) Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["n"] = kotu }));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(new KomutSablonu("sayi2", "genel", false, 5, 30, new[] { new ParametreKurali("n", @"\d+", "5", 10, 1) }, "satir-listesi")));      // min > max
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(new KomutSablonu("sayi3", "genel", false, 5, 30, new[] { new ParametreKurali("n", @"\d+", "50", 1, 10) }, "satir-listesi")));     // varsayılan aralık dışı
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(new KomutSablonu("sayi4", "genel", false, 5, 30, Array.Empty<ParametreKurali>(), "satir-listesi", HedefTuru: "rastgele")));
    }
}
