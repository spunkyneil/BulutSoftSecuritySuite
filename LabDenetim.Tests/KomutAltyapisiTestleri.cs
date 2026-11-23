// Ortak komut çalıştırma altyapısı testleri: şablon kaydı ve parametre doğrulama, hedef doğrulama, sonuç/ayrıştırıcı ve (sahte ssh ile) çalıştırıcı akışı.
// Gerçek sensör gerekmez: ssh yerine kısa bir .cmd betiği kullanılır (SensorKomutlari.SshYoluGecici).
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace LabDenetim.Tests;

// Statik kayıtlar ve BulutKlasor/ssh yolu paylaşıldığı için diğer "Veri" testleriyle sırayla çalışır.
[Collection("Veri")]
public class KomutAltyapisiTestleri : IDisposable
{
    public KomutAltyapisiTestleri() { Temizle(); }
    public void Dispose() { Temizle(); }
    private static void Temizle() { KomutSablonuDeposu.Temizle(); HedefDogrulama.TestHedefleriniTemizle(); HedefDogrulama.KategoriKuraliniKaldir("web"); HedefDogrulama.KategoriKuraliniKaldir("ag"); HedefDogrulama.KategoriKuraliniKaldir("bozuk"); }

    // Kısa şablon üretici.
    private static KomutSablonu Sablon(string ad = "ornek", string kategori = "genel", bool sudo = false, int zaman = 30, ParametreKurali[]? p = null, string ayirici = "satir-listesi", string arac = "", string paket = "") => new(ad, kategori, sudo, 5, zaman, p ?? Array.Empty<ParametreKurali>(), ayirici, arac, paket);

    // ================================================================ KomutSablonu
    [Fact] public void Ekle_GecerliSablonu_Getirir()
    {
        KomutSablonuDeposu.Ekle(Sablon("ornek-1", p: new[] { new ParametreKurali("kip", "[a-z]+", "hizli") }));
        var s = KomutSablonuDeposu.Getir("ornek-1"); Assert.NotNull(s); Assert.Equal("genel", s!.Kategori); Assert.Single(s.Parametreler);
        Assert.Null(KomutSablonuDeposu.Getir("yok-boyle")); Assert.Null(KomutSablonuDeposu.Getir(null)); Assert.Single(KomutSablonuDeposu.Hepsi());
    }

    [Fact] public void Ekle_AyniAdiIkinciKez_SessizceUzerineYazmaz()
    {
        KomutSablonuDeposu.Ekle(Sablon("ornek-2")); Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Ekle(Sablon("ornek-2", kategori: "baska")));
        Assert.Equal("genel", KomutSablonuDeposu.Getir("ornek-2")!.Kategori);
    }

    [Fact] public void Ekle_GecersizAlanlariReddeder()
    {
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("Buyuk Harf")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("a; id")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", kategori: "")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", zaman: 0)));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", zaman: 99999)));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", arac: "a b")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", arac: "x;id")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", paket: "Paket")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", ayirici: "Bozuk Ad")));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", p: new[] { new ParametreKurali("kip", "(", "") })));                       // geçersiz düzenli ifade
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", p: new[] { new ParametreKurali("kip", "[a-z]+", "ABC") })));                // varsayılan kuralına uymuyor
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", p: new[] { new ParametreKurali("kip", "x", ""), new ParametreKurali("kip", "x", "") })));   // yinelenen ad
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", p: new[] { new ParametreKurali("Kip", "x", "") })));
        Assert.Throws<ArgumentException>(() => KomutSablonuDeposu.Ekle(Sablon("ok", p: Enumerable.Range(0, 17).Select(i => new ParametreKurali("p" + i, "x", "")).ToArray())));   // 17 parametre
        Assert.Empty(KomutSablonuDeposu.Hepsi());
    }

    [Fact] public void Ekle_ListeSonradanDegistirilirse_KayitEtkilenmez()
    {
        var liste = new List<ParametreKurali> { new("kip", "[a-z]+", "hizli") };
        KomutSablonuDeposu.Ekle(new KomutSablonu("ornek-3", "genel", false, 5, 30, liste, "satir-listesi"));
        liste.Add(new ParametreKurali("sinsi", ".*", "x"));     // doğrulama atlatma denemesi
        Assert.Single(KomutSablonuDeposu.Getir("ornek-3")!.Parametreler);
    }

    [Fact] public void Uyarilastir_GecerliDegerler_SablonSirasiylaDoner()
    {
        var s = Sablon("ornek-4", p: new[] { new ParametreKurali("kapi", @"\d{1,5}", "80"), new ParametreKurali("kip", "[a-z]+", "") });
        Assert.Equal(new[] { "80", "hizli" }, KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kip"] = "hizli" }));
        Assert.Equal(new[] { "8080", "tam" }, KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kip"] = "tam", ["kapi"] = "8080" }));
    }

    [Theory]
    [InlineData("80; id")] [InlineData("80\n")] [InlineData("80\r\n")] [InlineData("$(id)")] [InlineData("abc")] [InlineData("-80")] [InlineData("")] [InlineData(" 80")] [InlineData("80 ")] [InlineData("8٠")]
    public void Uyarilastir_KuralDisiDegeri_Reddeder(string deger)
    {
        // Desen "-?\d+" bilerek eksi işaretine izin verir: yine de '-' ile başlayan değer genel kuralla reddedilmeli. Boş değer: zorunlu parametre eksik.
        var s = Sablon("ornek-5", p: new[] { new ParametreKurali("kapi", @"-?\d+", "") });
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kapi"] = deger }));
    }

    [Fact] public void Uyarilastir_TanimsizEksikVeUzunDegeri_Reddeder()
    {
        var s = Sablon("ornek-6", p: new[] { new ParametreKurali("kip", "a+", "") });
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kip"] = "aa", ["baska"] = "x" }));    // tanımsız parametre
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(s, null));                                                                    // zorunlu eksik
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kip"] = new string('a', 257) }));       // çok uzun
        Assert.Equal(new[] { new string('a', 256) }, KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kip"] = new string('a', 256) }));          // sınırda kabul
    }

    [Fact] public void Uyarilastir_FelaketGetirenDesen_ProgramiKilitlemez()
    {
        var s = Sablon("ornek-7", p: new[] { new ParametreKurali("kip", "(a+)+b", "") });
        var sw = Stopwatch.StartNew();
        Assert.Throws<InvalidOperationException>(() => KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["kip"] = new string('a', 40) }));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), "Desen değerlendirmesi süre sınırını aşmadı mı?");
    }

    [Fact] public void Uyarilastir_EnjeksiyonDegeriniKomutaBirlestirmez_YalnizcaListeVerir()
    {
        // Doğrulanmış değerler liste olarak döner; hiçbir yerde tek bir komut metnine birleştirilmez.
        var s = Sablon("ornek-8", p: new[] { new ParametreKurali("alan", @"[a-z0-9.\-]+", "") });
        var r = KomutSablonuDeposu.Uyarilastir(s, new Dictionary<string, string> { ["alan"] = "ornek.example" }); Assert.IsAssignableFrom<IReadOnlyList<string>>(r); Assert.Single(r);
    }

    // ================================================================ HedefDogrulama
    [Theory]
    [InlineData("10.0.0.1")] [InlineData("10.255.255.255")] [InlineData("172.16.0.1")] [InlineData("172.31.255.255")] [InlineData("192.168.56.101")] [InlineData("192.168.0.0")]
    public void Hedef_OzelAgAdresleriKabulEdilir(string h) => Assert.Null(HedefDogrulama.HedefHatasi("genel", h));

    [Theory]
    [InlineData("8.8.8.8")] [InlineData("172.15.255.255")] [InlineData("172.32.0.1")] [InlineData("192.167.1.1")] [InlineData("192.169.0.1")] [InlineData("11.0.0.1")]
    [InlineData("127.0.0.1")] [InlineData("169.254.1.1")] [InlineData("100.64.0.1")] [InlineData("0.0.0.0")] [InlineData("255.255.255.255")] [InlineData("224.0.0.1")]
    [InlineData("10.1")] [InlineData("10.0.0.256")] [InlineData("010.0.0.1")] [InlineData("1.2.3.4.5")] [InlineData("10.0.0")]
    [InlineData("10.0.0.1/24")] [InlineData("10.0.0.1:22")] [InlineData("[::1]")] [InlineData("::1")] [InlineData("fe80::1")] [InlineData("localhost")] [InlineData("musteri.example")]
    [InlineData("10.0.0.1 ")] [InlineData(" 10.0.0.1")] [InlineData("10.0.0.1\n")] [InlineData("10.0.0.1;id")] [InlineData("$(id)")] [InlineData("-10.0.0.1")] [InlineData(".10.0.0.1")]
    [InlineData("")] [InlineData("10.0.0.1١")] [InlineData("10.0.0.1&whoami")] [InlineData("10.0.0.1|id")]
    public void Hedef_KuralDisiHedefReddedilir(string h) => Assert.NotNull(HedefDogrulama.HedefHatasi("genel", h));

    [Fact] public void Hedef_NullHedef_Reddedilir() => Assert.NotNull(HedefDogrulama.HedefHatasi("genel", null!));

    [Fact] public void Hedef_AlanAdi_YalnizcaListedekiTamEslesme()
    {
        Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "musteri-staging.example"));        // liste boşken reddedilir
        HedefDogrulama.TestHedefiEkle("musteri-staging.example");
        Assert.Null(HedefDogrulama.HedefHatasi("genel", "musteri-staging.example"));
        Assert.Null(HedefDogrulama.HedefHatasi("genel", "Musteri-Staging.Example"));            // büyük/küçük harf fark etmez
        Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "www.musteri-staging.example"));     // alt alan adı otomatik izinli DEĞİL
        Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "evil-musteri-staging.example"));
        Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "musteri-staging.example."));        // sondaki nokta
        Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "musteri-staging.example.evil.com"));
    }

    [Fact] public void Hedef_IzinliListedekiGenelIpVeAralik_KabulEdilir()
    {
        HedefDogrulama.TestHedefiEkle("203.0.113.7"); HedefDogrulama.TestHedefiEkle("198.51.100.0/24");
        Assert.Null(HedefDogrulama.HedefHatasi("genel", "203.0.113.7")); Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "203.0.113.8"));
        Assert.Null(HedefDogrulama.HedefHatasi("genel", "198.51.100.77")); Assert.NotNull(HedefDogrulama.HedefHatasi("genel", "198.51.101.1"));
        Assert.Equal(2, HedefDogrulama.TestHedefleri().Count);
    }

    [Fact] public void TestHedefiEkle_GenisVeGecersizGirdileriReddeder()
    {
        foreach (var g in new[] { "0.0.0.0/0", "10.0.0.0/8", "192.168.0.0/16", "203.0.113.0/23", "203.0.113.0/33", "localhost", "1.2.3.4.5", "ornek", "a b.example", "x;id.example", "", "  ", "10.0.0.1/ab" })
            Assert.Throws<ArgumentException>(() => HedefDogrulama.TestHedefiEkle(g));
        Assert.Empty(HedefDogrulama.TestHedefleri());
    }

    [Fact] public void Hedef_KategoriKurali_YalnizcaDaraltirGevsetmez()
    {
        HedefDogrulama.KategoriKuraliKaydet("web", h => h == "10.0.0.9" ? "Bu kategori bu hedefe izin vermiyor." : null);
        Assert.Equal("Bu kategori bu hedefe izin vermiyor.", HedefDogrulama.HedefHatasi("web", "10.0.0.9"));
        Assert.Null(HedefDogrulama.HedefHatasi("web", "10.0.0.10")); Assert.Null(HedefDogrulama.HedefHatasi("ag", "10.0.0.9")); Assert.Null(HedefDogrulama.HedefHatasi("WEB ", "10.0.0.10"));   // başka kategori etkilenmez
        // "Her şeyi kabul eden" kural temel kuralı gevşetemez.
        HedefDogrulama.KategoriKuraliKaydet("ag", _ => null); Assert.NotNull(HedefDogrulama.HedefHatasi("ag", "8.8.8.8"));
        // Hata veren kural: güvenli tarafta kalınır (reddedilir).
        HedefDogrulama.KategoriKuraliKaydet("bozuk", _ => throw new InvalidOperationException("x")); Assert.NotNull(HedefDogrulama.HedefHatasi("bozuk", "10.0.0.1"));
    }

    [Fact] public void TestHedefleri_DosyadanYuklenir_GecersizSatirlarDondurulur()
    {
        string yol = Path.Combine(Path.GetTempPath(), "hedefler_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(yol, "# yorum\n\nmusteri-staging.example\n203.0.113.5\n10.0.0.0/8\nlocalhost\n");
            var (eklenen, red) = HedefDogrulama.DosyadanYukle(yol); Assert.Equal(2, eklenen); Assert.Equal(new[] { "10.0.0.0/8", "localhost" }, red);
            Assert.Null(HedefDogrulama.HedefHatasi("genel", "203.0.113.5")); Assert.Equal((0, 0), (HedefDogrulama.DosyadanYukle(yol + ".yok").Eklenen, HedefDogrulama.DosyadanYukle(yol + ".yok").Reddedilen.Count));
        }
        finally { try { File.Delete(yol); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "KomutAltyapisiTestleri.temizlik"); } }
    }

    // ================================================================ SonucModeli
    [Fact, Trait("Tur", "KaynakKurali")] public void HamCiktiYaz_GuvenliYolaYazar_VeYolKacisiOlmaz()
    {
        string eskiKok = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "sonuc_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = gecici;
        try
        {
            string y1 = SonucKaydi.HamCiktiYaz(@"..\..\kategori", "a/b\\c", "merhaba"), y2 = SonucKaydi.HamCiktiYaz(@"..\..\kategori", "a/b\\c", "ikinci");
            string kok = Path.GetFullPath(Path.Combine(gecici, SonucKaydi.Klasor)) + Path.DirectorySeparatorChar;
            Assert.StartsWith(kok, y1, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("..", y1[kok.Length..]);
            Assert.Equal("merhaba", File.ReadAllText(y1)); Assert.Equal("ikinci", File.ReadAllText(y2)); Assert.NotEqual(y1, y2);       // üzerine yazılmaz
            Assert.EndsWith(".txt", y1);
        }
        finally { BulutKlasor.Kok = eskiKok; try { Directory.Delete(gecici, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "KomutAltyapisiTestleri.temizlik"); } }
    }

    [Theory] [InlineData("con", "_con")] [InlineData("", "_")] [InlineData("a.b", "a_b")] [InlineData("..", "__")] [InlineData("ok-ad_1", "ok-ad_1")] [InlineData("NUL", "_NUL")] [InlineData("a b/c", "a_b_c")]
    public void GuvenliAd_Kurallari(string girdi, string beklenen) => Assert.Equal(beklenen, SonucKaydi.GuvenliAd(girdi));

    [Fact] public void SatirListesiAyirici_SatirlariBulguyaCevirir_VeSiniriUygular()
    {
        var a = new SatirListesiAyirici(); var l = a.Ayristir("ornek", "10.0.0.1", "ilk\n\nikinci\r\n");
        Assert.Equal(2, l.Count); Assert.All(l, b => { Assert.Equal("ornek", b.Modul); Assert.Equal(Onem.Bilgi, b.Onem); }); Assert.Equal("ilk", l[0].Baslik);
        var cok = a.Ayristir("ornek", "10.0.0.1", string.Join("\n", Enumerable.Range(0, 250).Select(i => "satir" + i)));
        Assert.Equal(SatirListesiAyirici.EnFazlaBulgu + 1, cok.Count); Assert.Equal("Çıktı kısaltıldı", cok[^1].Baslik);
        Assert.Empty(a.Ayristir("ornek", "h", "")); Assert.Empty(a.Ayristir("ornek", "h", "  \n \n"));
        // Denetim (ESC) karakterleri temizlenir.
        Assert.False(a.Ayristir("ornek", "h", "x\u001b[31m kirmizi")[0].Detay.Contains('\u001b'));   // (karakter aramasıyla: metin aramasında kültür kuralı denetim karakterini yok sayar)
    }

    [Fact] public void AnahtarDegerAyirici_AnahtarDegerSatirlariniAyirir()
    {
        var l = new AnahtarDegerAyirici().Ayristir("ornek", "10.0.0.1", "Sürüm: 1.2\nport=22\nrastgele satır\n:bos\nanahtar:   \n");
        Assert.Equal(2, l.Count); Assert.Equal("Sürüm", l[0].Baslik); Assert.Contains("1.2", l[0].Detay); Assert.Equal("port", l[1].Baslik); Assert.Contains("22", l[1].Detay);
    }

    [Fact] public void AyiriciDeposu_VarsayilanaDonerVeYenisiKaydedilir()
    {
        var a = AyiriciDeposu.GetirVeyaVarsayilan("yok-boyle-ayirici", out bool b1); Assert.False(b1); Assert.Equal(AyiriciDeposu.Varsayilan, a.Ad);
        AyiriciDeposu.GetirVeyaVarsayilan("", out bool b2); Assert.True(b2); AyiriciDeposu.GetirVeyaVarsayilan(null, out bool b3); Assert.True(b3);
        Assert.NotNull(AyiriciDeposu.Getir("anahtar-deger")); Assert.Throws<InvalidOperationException>(() => AyiriciDeposu.Kaydet(new AnahtarDegerAyirici()));
    }

    // ================================================================ KomutCalistirici (sahte ssh)
    private const string BetikSablonu = """
        @echo off
        >>"%~dp0cagri.log" echo %*
        echo %* | findstr /C:"apt-get install" >nul && (
          if exist "%~dp0yok_arac.flag" del "%~dp0yok_arac.flag"
          exit 0
        )
        echo %* | findstr /C:"command -v" >nul && (
          if exist "%~dp0yok_arac.flag" exit 1
          exit 0
        )
        echo %* | findstr /C:"sudo -n true" >nul && (
          if exist "%~dp0sudo_parola.flag" (
            echo sudo: a password is required 1>&2
            exit 1
          )
          exit 0
        )
        echo %* | findstr /C:"python3 - " >nul && (
          more >nul
          @@AJAN@@
          exit 0
        )
        exit 9
        """;

    // Sahte sensör ortamı: geçici klasör, anahtar dosyası, sahte ssh ve statik ayarların geri yüklenmesi.
    private sealed class Sahne : IDisposable
    {
        public readonly string Dizin = Path.Combine(Path.GetTempPath(), "komut_" + Guid.NewGuid().ToString("N")); public readonly SensorProfili Profil;
        private readonly string _eskiKok = BulutKlasor.Kok; private readonly string? _eskiSsh = SensorKomutlari.SshYoluGecici; private readonly int _eskiPay = KomutCalistirici.AjanPayiSn;
        public Sahne(string ajanGovdesi, bool aracYok = false, bool sudoParola = false)
        {
            Directory.CreateDirectory(Dizin); BulutKlasor.Kok = Dizin;
            string anahtar = Path.Combine(Dizin, "anahtar"); File.WriteAllText(anahtar, "x"); Profil = new SensorProfili("Test", "192.168.56.101", 22, "kali", anahtar);
            string betik = Path.Combine(Dizin, "ssh.cmd"); File.WriteAllText(betik, BetikSablonu.Replace("@@AJAN@@", ajanGovdesi).Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n"); SensorKomutlari.SshYoluGecici = betik;
            if (aracYok) File.WriteAllText(Path.Combine(Dizin, "yok_arac.flag"), "1"); if (sudoParola) File.WriteAllText(Path.Combine(Dizin, "sudo_parola.flag"), "1");
        }
        public string Cagrilar => File.Exists(Path.Combine(Dizin, "cagri.log")) ? File.ReadAllText(Path.Combine(Dizin, "cagri.log")) : "";
        public void Dispose() { SensorKomutlari.SshYoluGecici = _eskiSsh; BulutKlasor.Kok = _eskiKok; KomutCalistirici.AjanPayiSn = _eskiPay; try { Directory.Delete(Dizin, true); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "KomutAltyapisiTestleri.temizlik"); } }
    }

    private const string AjanBasarili = """
        echo {"t":"cikti","akis":"stdout","s":"surum: 1.2"}
        echo {"t":"cikti","akis":"stdout","s":"port=22"}
        echo {"t":"cikti","akis":"stderr","s":"uyari: ornek"}
        echo {"t":"done","kod":0}
        """;

    private static void SablonKaydet(string ad = "ornek-tarama", bool sudo = false, int zaman = 30, string ayirici = "anahtar-deger", string arac = "", string paket = "") =>
        KomutSablonuDeposu.Ekle(Sablon(ad, "genel", sudo, zaman, new[] { new ParametreKurali("kip", "[a-z]+", "hizli") }, ayirici, arac, paket));

    private static bool OlayVar(string seviye, string baslangic) => OlayDeposu.Oku().Any(o => o.Tur == "kali" && o.Seviye == seviye && o.Mesaj.StartsWith(baslangic, StringComparison.Ordinal));

    [Fact] public async Task Calistir_Basarili_SonucuVeKaydiOlusturur()
    {
        using var s = new Sahne(AjanBasarili); SablonKaydet();
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", new Dictionary<string, string> { ["kip"] = "tam" }, null, CancellationToken.None);
        Assert.Equal(0, r.CikisKodu); Assert.Equal("ornek-tarama", r.SablonAdi); Assert.Equal("192.168.56.1", r.Hedef); Assert.Contains("tam", r.Komut); Assert.True(r.Sure >= TimeSpan.Zero);
        Assert.Equal(2, r.Bulgular.Count); Assert.DoesNotContain(r.Bulgular, b => b.Atlandi);
        // Ham çıktı dosyası: stdout ve stderr ayrı bölümlerde.
        Assert.True(File.Exists(r.HamCiktiDosyasi)); string ham = File.ReadAllText(r.HamCiktiDosyasi);
        Assert.StartsWith(Path.Combine(s.Dizin, "KaliCikti", "genel", "ornek-tarama") + Path.DirectorySeparatorChar, r.HamCiktiDosyasi, StringComparison.OrdinalIgnoreCase);
        int so = ham.IndexOf("--- stdout ---", StringComparison.Ordinal), se = ham.IndexOf("--- stderr ---", StringComparison.Ordinal); Assert.True(so >= 0 && se > so);
        Assert.Contains("port=22", ham[so..se]); Assert.DoesNotContain("uyari: ornek", ham[so..se]); Assert.Contains("uyari: ornek", ham[se..]);
        // Çağrılar: kurulu mu → ajan; sudo yok.
        string log = s.Cagrilar; Assert.Contains("command -v ornek-tarama", log); Assert.Contains("python3 - run", log); Assert.DoesNotContain("sudo -n", log);
        // Ajana giden yük: Base64 JSON, değerler dizi olarak.
        string b64 = Regex.Match(log, @"python3 - run ([A-Za-z0-9+/=]+)").Groups[1].Value; using var yuk = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64))); var k = yuk.RootElement;
        Assert.Equal("komut", k.GetProperty("mod").GetString()); Assert.Equal("ornek-tarama", k.GetProperty("sablon").GetString()); Assert.Equal("192.168.56.1", k.GetProperty("hedef").GetString());
        Assert.Equal("tam", k.GetProperty("arg")[0].GetString()); Assert.False(k.TryGetProperty("sudo", out _)); Assert.Equal(JsonValueKind.Array, k.GetProperty("izinli").ValueKind); Assert.Equal(30, k.GetProperty("zaman").GetInt32());
        // Olay günlüğü: başlangıç ve bitiş.
        Assert.True(OlayVar("Bilgi", "KALİ KOMUT BAŞLADI")); Assert.True(OlayVar("Bilgi", "KALİ KOMUT BİTTİ"));
    }

    [Fact] public async Task Calistir_IzinDisiHedef_ReddederVeSensoreHicGitmez()
    {
        using var s = new Sahne(AjanBasarili); SablonKaydet();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "8.8.8.8", null, null, CancellationToken.None));
        Assert.Contains("özel ağ", ex.Message); Assert.Equal("", s.Cagrilar); Assert.True(OlayVar("Uyarı", "KALİ KOMUT REDDEDİLDİ"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "yetkisiz.example", null, null, CancellationToken.None)); Assert.Equal("", s.Cagrilar);
        // Alan adı izinli listeye alınınca doğrulama geçer (yine de çalıştırma bu testte denenmez).
        HedefDogrulama.TestHedefiEkle("yetkili.example"); Assert.Null(HedefDogrulama.HedefHatasi("genel", "yetkili.example"));
    }

    [Fact] public async Task Calistir_KuralDisiParametreVeKayitsizSablon_Reddeder()
    {
        using var s = new Sahne(AjanBasarili); SablonKaydet();
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", new Dictionary<string, string> { ["kip"] = "hizli; id" }, null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", new Dictionary<string, string> { ["baska"] = "x" }, null, CancellationToken.None));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "kayitsiz-sablon", "192.168.56.1", null, null, CancellationToken.None));
        Assert.Contains("beyaz liste", ex.Message); Assert.Equal("", s.Cagrilar);
    }

    [Fact] public async Task Calistir_AracKuruluDegil_OnaysizAtlanir()
    {
        using var s = new Sahne(AjanBasarili, aracYok: true); SablonKaydet(arac: "ornek-arac", paket: "ornek-paket"); string? soru = null;
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, q => { soru = q; return false; }, CancellationToken.None);
        Assert.Equal(KomutCalistirici.AtlandiKodu, r.CikisKodu); Assert.Single(r.Bulgular); Assert.True(r.Bulgular[0].Atlandi); Assert.Equal("", r.HamCiktiDosyasi);
        Assert.Contains("ornek-paket", soru); string log = s.Cagrilar; Assert.Contains("command -v ornek-arac", log); Assert.DoesNotContain("apt-get", log); Assert.DoesNotContain("python3 - run", log);
        // Geri çağrım verilmediyse de kurulum yapılmaz.
        var r2 = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None); Assert.Equal(KomutCalistirici.AtlandiKodu, r2.CikisKodu); Assert.DoesNotContain("apt-get", s.Cagrilar);
    }

    [Fact] public async Task Calistir_AracKuruluDegil_OnayliKurarVeCalistirir()
    {
        using var s = new Sahne(AjanBasarili, aracYok: true); SablonKaydet(arac: "ornek-arac", paket: "ornek-paket");
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, _ => true, CancellationToken.None);
        Assert.Equal(0, r.CikisKodu); Assert.Equal(2, r.Bulgular.Count);
        var satirlar = s.Cagrilar.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); int i1 = satirlar.FindIndex(x => x.Contains("command -v ornek-arac")), isudo = satirlar.FindIndex(x => x.Contains("sudo -n true")), iapt = satirlar.FindIndex(x => x.Contains("sudo -n apt-get install -y ornek-paket")), i2 = satirlar.FindLastIndex(x => x.Contains("command -v ornek-arac")), ia = satirlar.FindIndex(x => x.Contains("python3 - run"));
        Assert.True(i1 >= 0 && isudo > i1 && iapt > isudo && i2 > iapt && ia > i2, "Sıra: kurulu mu → sudo → kurulum → yeniden kurulu mu → ajan");
    }

    [Fact] public async Task Calistir_SudoParolaIstenirse_AnlasilirHataVerir()
    {
        using var s = new Sahne(AjanBasarili, sudoParola: true); SablonKaydet(sudo: true);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None));
        Assert.Contains("sudo", ex.Message); Assert.Contains("NOPASSWD", ex.Message); Assert.DoesNotContain("python3 - run", s.Cagrilar); Assert.True(OlayVar("Uyarı", "KALİ KOMUT BAŞARISIZ"));
        // Sudo gerekmeyen şablonda sudo hiç denenmez.
        KomutSablonuDeposu.Ekle(Sablon("sudosuz")); var r = await KomutCalistirici.CalistirAsync(s.Profil, "sudosuz", "192.168.56.1", null, null, CancellationToken.None); Assert.Equal(0, r.CikisKodu);
    }

    [Fact] public async Task Calistir_SifirDisiCikisVeCiktiYoksa_CalistirilamadiBulgusuEkler()
    {
        using var s = new Sahne("""
            echo {"t":"cikti","akis":"stderr","s":"hata olustu"}
            echo {"t":"done","kod":3}
            """); SablonKaydet();
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None);
        Assert.Equal(3, r.CikisKodu); Assert.Single(r.Bulgular); Assert.True(r.Bulgular[0].Atlandi); Assert.Contains("hata olustu", r.Bulgular[0].Detay); Assert.True(OlayVar("Uyarı", "KALİ KOMUT BİTTİ"));
        // Çıktı üretip sıfır dışı kodla biten araç hata sayılmaz (bazı araçlar bulgu varsa sıfır dışı döner).
    }

    [Fact] public async Task Calistir_SifirDisiCikisAmaCiktiVarsa_BulgulariKorur()
    {
        using var s = new Sahne("""
            echo {"t":"cikti","akis":"stdout","s":"bulgu: var"}
            echo {"t":"done","kod":1}
            """); SablonKaydet();
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None);
        Assert.Equal(1, r.CikisKodu); Assert.Single(r.Bulgular); Assert.False(r.Bulgular[0].Atlandi);
    }

    [Fact] public async Task Calistir_AjanHataBildirirse_Firlatir()
    {
        using var s = new Sahne("""
            echo {"t":"error","m":"bilinmeyen kontrol"}
            exit 2
            """); SablonKaydet();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None));
        Assert.Contains("bilinmeyen kontrol", ex.Message);
    }

    [Fact] public async Task Calistir_AjanCiktisizBitip_KodBildirmezse_BaglantiHatasiFirlatir()
    {
        using var s = new Sahne("exit 9"); SablonKaydet();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None)); Assert.Contains("çalışmadı", ex.Message);
    }

    [Fact] public async Task Calistir_CokBuyukCikti_KilitlenmezVeAkislarAyriToplanir()
    {
        // 4000 satır stdout + 4000 satır ajan-stderr JSON + ham ssh stderr (boru tamponlarını aşar): okuma ayrı ayrı yapılmazsa kilitlenirdi.
        using var s = new Sahne("""
            for /L %%i in (1,1,4000) do (
              echo {"t":"cikti","akis":"stdout","s":"satir %%i xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"}
              echo {"t":"cikti","akis":"stderr","s":"hata %%i yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy"}
              echo ham-ssh-hata %%i zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz 1>&2
            )
            echo {"t":"done","kod":0}
            """); SablonKaydet(ayirici: "satir-listesi", zaman: 120);
        var sw = Stopwatch.StartNew(); var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(110));
        Assert.Equal(0, r.CikisKodu); string ham = File.ReadAllText(r.HamCiktiDosyasi);
        Assert.Contains("satir 4000 ", ham); Assert.Contains("hata 4000 ", ham[ham.IndexOf("--- stderr ---", StringComparison.Ordinal)..]); Assert.Contains("--- ssh ---", ham);
        Assert.Equal(SatirListesiAyirici.EnFazlaBulgu + 1, r.Bulgular.Count);      // 4000 satır → 200 + özet
    }

    [Fact] public async Task Calistir_ZamanAsimi_SablondanAlinirVeKodVerir()
    {
        using var s = new Sahne("ping -n 60 127.0.0.1 >nul"); KomutCalistirici.AjanPayiSn = 0; SablonKaydet(zaman: 2);
        var sw = Stopwatch.StartNew(); var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(40));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), "Şablon süresi (2 sn) uygulanmalı; eski 180 sn sabiti değil"); Assert.Equal(KomutCalistirici.ZamanAsimiKodu, r.CikisKodu); Assert.Contains(r.Bulgular, b => b.Atlandi && b.Detay.Contains("2 saniye"));
        Assert.True(OlayVar("Uyarı", "KALİ KOMUT BİTTİ"));
    }

    [Fact] public async Task Calistir_Iptal_HizlicaDurur()
    {
        using var s = new Sahne("ping -n 60 127.0.0.1 >nul"); SablonKaydet(zaman: 600); using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
        var is_ = KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, cts.Token); await Task.Delay(1500); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => is_.WaitAsync(TimeSpan.FromSeconds(30))); Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30)); Assert.True(OlayVar("Bilgi", "KALİ KOMUT İPTAL EDİLDİ"));
    }

    // ---- Ajan bağlantısı (yeni): hedefsiz şablonlar, hedef türü, ajan zaman aşımı, gömülü tablonun tembel yüklenmesi.
    [Fact] public async Task Calistir_HedefsizSablon_HedefBosOlmaliVeCalisir()
    {
        using var s = new Sahne(AjanBasarili); KomutSablonuDeposu.Ekle(Sablon("hedefsiz", p: new[] { new ParametreKurali("kip", "[a-z]+", "hizli") }, ayirici: "anahtar-deger") with { HedefTuru = "yok" });
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "hedefsiz", "", null, null, CancellationToken.None); Assert.Equal(0, r.CikisKodu); Assert.Equal("", r.Hedef); Assert.Equal(2, r.Bulgular.Count);
        string b64 = Regex.Match(s.Cagrilar, @"python3 - run ([A-Za-z0-9+/=]+)").Groups[1].Value; using var yuk = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64))); Assert.Equal("", yuk.RootElement.GetProperty("hedef").GetString());
        // Hedefsiz şablona hedef verilirse reddedilir ve sensöre hiçbir şey gitmez.
        string once = s.Cagrilar; var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "hedefsiz", "10.0.0.1", null, null, CancellationToken.None)); Assert.Contains("hedef almaz", ex.Message); Assert.Equal(once, s.Cagrilar);
    }

    [Fact] public async Task Calistir_HedefTuruUymazsa_Reddeder()
    {
        using var s = new Sahne(AjanBasarili); HedefDogrulama.TestHedefiEkle("musteri.example");
        KomutSablonuDeposu.Ekle(Sablon("sadece-ip") with { HedefTuru = "ip" }); KomutSablonuDeposu.Ekle(Sablon("sadece-alan") with { HedefTuru = "alan" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "sadece-ip", "musteri.example", null, null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "sadece-alan", "192.168.56.1", null, null, CancellationToken.None)); Assert.Equal("", s.Cagrilar);
        Assert.Equal(0, (await KomutCalistirici.CalistirAsync(s.Profil, "sadece-alan", "musteri.example", null, null, CancellationToken.None)).CikisKodu);
        Assert.Equal(0, (await KomutCalistirici.CalistirAsync(s.Profil, "sadece-ip", "192.168.56.1", null, null, CancellationToken.None)).CikisKodu);
    }

    [Fact] public async Task Calistir_AjanZamanAsimiBildirirse_AtlandiBulgusuEkler()
    {
        using var s = new Sahne("""
            echo {"t":"cikti","akis":"stdout","s":"kismi: 1"}
            echo {"t":"cikti","akis":"stderr","s":"[ajan] zaman asimi (2 sn); surec sonlandirildi"}
            echo {"t":"done","kod":124,"zamanasimi":true}
            """); SablonKaydet(zaman: 2);
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, null, CancellationToken.None);
        Assert.Equal(KomutCalistirici.ZamanAsimiKodu, r.CikisKodu); Assert.Contains(r.Bulgular, b => b.Atlandi && b.Detay.Contains("2 saniye")); Assert.Contains(r.Bulgular, b => !b.Atlandi && b.Baslik == "kismi");   // kısmi çıktı korunur
    }

    [Fact] public async Task Calistir_GomuluTabloSablonu_TembelYuklenirVeGercekTabloyuAjanaGonderir()
    {
        // Depo boşken gömülü tablodaki bir şablon adıyla çağrı: tablo kendiliğinden yüklenir (alan adı izinli listede değilse hedef reddi gelir; "beyaz listede yok" DEĞİL).
        using var s = new Sahne(AjanBasarili);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.CalistirAsync(s.Profil, "dnsenum-alan", "musteri-staging.example", null, null, CancellationToken.None)); Assert.Contains("izinli hedef listesinde değil", ex.Message); Assert.Equal("", s.Cagrilar);
        HedefDogrulama.TestHedefiEkle("musteri-staging.example");
        var r = await KomutCalistirici.CalistirAsync(s.Profil, "dnsenum-alan", "musteri-staging.example", null, null, CancellationToken.None); Assert.Equal("dnsenum-alan", r.SablonAdi); Assert.Equal(0, r.CikisKodu);
        string log = s.Cagrilar; Assert.Contains("command -v dnsenum", log); Assert.Contains("python3 - run", log); Assert.DoesNotContain("sudo", log);
        // Payload'daki izinli liste ajana iletilir; sudo/argv iletilmez.
        string b64 = Regex.Match(log, @"python3 - run ([A-Za-z0-9+/=]+)").Groups[1].Value; using var yuk = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
        Assert.Equal("musteri-staging.example", yuk.RootElement.GetProperty("izinli")[0].GetString()); Assert.False(yuk.RootElement.TryGetProperty("argv", out _));
        // Sudo gerektiren gömülü şablonda parolasız sudo önkontrolü yapılır.
        var r2 = await KomutCalistirici.CalistirAsync(s.Profil, "netdiscover-ag", "192.168.56.0", null, null, CancellationToken.None); Assert.Equal(0, r2.CikisKodu); Assert.Contains("sudo -n true", s.Cagrilar);
    }

    // ---- Önizleme (arayüz onay penceresi): sensördeki ajandan çalışacak tam komut alınır; hiçbir şey çalıştırılmaz.
    [Fact] public async Task Onizle_AjanKomutunuDoner_ReddedilenHicGonderilmez()
    {
        using var s = new Sahne("""
            echo {"t":"onizle","argv":["sudo","-n","timeout","--kill-after=5","30","ornekarac","-c","5","a b"],"zaman":30}
            echo {"t":"done","kod":0}
            """); SablonKaydet(arac: "ornekarac");
        string komut = await KomutCalistirici.OnizleAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, CancellationToken.None);
        Assert.Equal("sudo -n timeout --kill-after=5 30 ornekarac -c 5 'a b'", komut);      // boşluklu öğe yalnızca ekranda tırnaklanır
        string log = s.Cagrilar; Assert.Contains("python3 - onizle", log); Assert.DoesNotContain("python3 - run", log); Assert.DoesNotContain("command -v", log);   // önizleme kurulu mu/sudo denemez
        // Reddedilen hedef/parametre: sensöre hiçbir şey gitmez ve olay günlüğüne "reddedildi" yazılmaz (önizleme sessizdir).
        string once = s.Cagrilar;
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.OnizleAsync(s.Profil, "ornek-tarama", "8.8.8.8", null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.OnizleAsync(s.Profil, "ornek-tarama", "192.168.56.1", new Dictionary<string, string> { ["kip"] = "x; id" }, CancellationToken.None));
        Assert.Equal(once, s.Cagrilar);
    }

    [Fact] public async Task Onizle_AjanHataBildirirseFirlatir()
    {
        using var s = new Sahne("""
            echo {"t":"error","m":"parametre kurala uymuyor: kip"}
            exit 2
            """); SablonKaydet();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.OnizleAsync(s.Profil, "ornek-tarama", "192.168.56.1", null, CancellationToken.None)); Assert.Contains("parametre kurala uymuyor", ex.Message);
        using var s2 = new Sahne("exit 9"); var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => KomutCalistirici.OnizleAsync(s2.Profil, "ornek-tarama", "192.168.56.1", null, CancellationToken.None)); Assert.Contains("alınamadı", ex2.Message);
    }
}
