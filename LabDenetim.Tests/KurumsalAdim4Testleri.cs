// Faz 2 Adım 4 testleri: VeriMaskeleyici (özel IP son oktet / kullanıcı adı / yerel yol), Claude onay penceresi (maskelenmiş önizleme = gönderilecek metin, varsayılan Vazgeç),
// Kolay Görünüm ilk göstergesi (sensör yoksa Son Tarama Durumu), Görsel Demo Yayları (varsayılan kapalı, kure.html'de yalnızca açıkken örnek notu ve yay üretimi) ve kısayol/erişim kopmadı denetimleri.
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class VeriMaskeleyiciTestleri
{
    private static MaskeSecenekleri Sec(bool ip = true, bool ad = true, bool yol = true, string[]? adlar = null, string[]? bilgisayar = null) => new() { OzelIpSonOktet = ip, KullaniciAdlari = ad, YerelYollar = yol, KullaniciAdListesi = adlar ?? Array.Empty<string>(), BilgisayarAdListesi = bilgisayar ?? Array.Empty<string>() };

    [Theory]
    [InlineData("Hedef 192.168.1.15 açık", "Hedef 192.168.1.x açık")]
    [InlineData("10.20.30.40:8080 ve 172.16.5.9", "10.20.30.x:8080 ve 172.16.5.x")]
    [InlineData("172.31.255.254 / 169.254.7.8", "172.31.255.x / 169.254.7.x")]
    [InlineData("genel 8.8.8.8, 172.32.0.1, 127.0.0.1, 1.2.3.4", "genel 8.8.8.8, 172.32.0.1, 127.0.0.1, 1.2.3.4")]
    [InlineData("sürüm 10.0.19045.1234 ve 999.168.1.1", "sürüm 10.0.19045.1234 ve 999.168.1.1")]
    [InlineData("cümle sonu 192.168.0.1.", "cümle sonu 192.168.0.x.")]
    public void OzelIp_SonOktetiGizlenir_GenelIpVeSurumlereDokunulmaz(string girdi, string beklenen) => Assert.Equal(beklenen, VeriMaskeleyici.Maskele(girdi, Sec()).Metin);

    [Theory]
    [InlineData(@"Dosya C:\Users\ali\Documents\rapor.txt bulundu", @"Dosya C:\…\rapor.txt bulundu")]
    [InlineData(@"yol D:\Veri Klasörü\Alt Klasör\yedek.zip.", @"yol D:\…\yedek.zip.")]
    [InlineData(@"C:\Program Files\Uygulama\bin\a.exe, E:\x\", @"C:\…\a.exe, E:\…\")]
    [InlineData(@"kök C:\ tek başına kalır", @"kök C:\ tek başına kalır")]
    public void YerelYol_SurucuVeDosyaAdiKalir_AraKlasorlerGizlenir(string girdi, string beklenen) => Assert.Equal(beklenen, VeriMaskeleyici.Maskele(girdi, Sec()).Metin);

    [Fact]
    public void KullaniciAdlari_YolIcindeVeListedeki_SozcukOlarakGizlenir()
    {
        var s = VeriMaskeleyici.Maskele(@"Kullanıcı Ali oturumda, makine DESKTOP-9X1 ve C:\Users\Ali\x.txt; alinin değil, alici", Sec(yol: false, adlar: new[] { "Ali" }, bilgisayar: new[] { "DESKTOP-9X1" }));
        Assert.Equal(@"Kullanıcı <kullanici> oturumda, makine <bilgisayar> ve C:\Users\<kullanici>\x.txt; alinin değil, alici", s.Metin);   // 'alinin'/'alici' sözcüğün parçası olduğu için dokunulmaz
        Assert.Equal(3, s.AdSayisi);
        Assert.Equal(@"yol C:\…\x.txt", VeriMaskeleyici.Maskele(@"yol C:\Users\Ali\x.txt", Sec(adlar: new[] { "Ali" })).Metin);   // yol maskesi açıkken kullanıcı adı yolla birlikte gider
        Assert.Equal("ab ali", VeriMaskeleyici.Maskele("ab ali", Sec(adlar: new[] { "ab" })).Metin);   // 3 karakterden kısa adlar yanlış eşleşmesin diye atlanır
    }

    [Fact]
    public void SecenekKapaliysa_ILgiliOgeKorunur_SayaclarDogru()
    {
        string metin = @"10.0.0.5 ve C:\Users\ali\a.txt";
        Assert.Equal(@"10.0.0.5 ve C:\…\a.txt", VeriMaskeleyici.Maskele(metin, Sec(ip: false)).Metin);
        Assert.Equal(@"10.0.0.x ve C:\Users\ali\a.txt", VeriMaskeleyici.Maskele(metin, Sec(yol: false, ad: false)).Metin);
        var tam = VeriMaskeleyici.Maskele(metin, Sec()); Assert.Equal(1, tam.IpSayisi); Assert.Equal(1, tam.YolSayisi); Assert.Equal(2, tam.Toplam);
        Assert.Equal(metin, VeriMaskeleyici.Maskele(metin, Sec(false, false, false)).Metin);
    }

    [Fact]
    public void Maskeleme_TekrarUygulaninca_MetinDegismez_NullVeBosGuvenli()
    {
        string metin = "Hedef 192.168.1.15\r\nYol: C:\\Users\\ali\\Belgeler\\x.docx\r\nKullanıcı: ali";
        var bir = VeriMaskeleyici.Maskele(metin, Sec(adlar: new[] { "ali" })); var iki = VeriMaskeleyici.Maskele(bir.Metin, Sec(adlar: new[] { "ali" }));
        Assert.Equal(bir.Metin, iki.Metin); Assert.DoesNotContain("ali", bir.Metin.Replace("<kullanici>", ""), StringComparison.Ordinal); Assert.DoesNotContain("192.168.1.15", bir.Metin, StringComparison.Ordinal);
        Assert.Equal("", VeriMaskeleyici.Maskele(null, Sec()).Metin); Assert.Equal("", VeriMaskeleyici.Maskele("", Sec()).Metin);
    }

    [Fact]
    public void Varsayilan_BuMakineninKullaniciVeBilgisayarAdiniIcerir_HepsiAcik()
    {
        var v = MaskeSecenekleri.Varsayilan(); Assert.True(v.OzelIpSonOktet && v.KullaniciAdlari && v.YerelYollar); Assert.Contains(Environment.UserName, v.KullaniciAdListesi); Assert.Contains(Environment.MachineName, v.BilgisayarAdListesi);
    }

    [Fact]
    public void GercekGibiTaramaOzeti_HicOzelIpVeYolKalmaz()
    {
        string ozet = "Hedef: 192.168.56.101 (Kali)\r\n- Açık port 445 (SMB) 192.168.56.1 üzerinden erişilebilir\r\n- Başlangıç: C:\\Users\\ornekkullanici\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\run.bat\r\n- Hizmet: C:\\Program Files\\EpicOnlineServices\\x.exe (tırnaksız yol)\r\n- Kullanıcı ornekkullanici yönetici";
        var s = VeriMaskeleyici.Maskele(ozet, Sec(adlar: new[] { "ornekkullanici" }));
        Assert.DoesNotMatch(@"\b(?:192\.168|10\.\d+|172\.(?:1[6-9]|2\d|3[01]))\.\d+\.\d+\b", s.Metin); Assert.DoesNotContain("ornekkullanici", s.Metin, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("AppData", s.Metin, StringComparison.Ordinal); Assert.DoesNotContain("EpicOnlineServices", s.Metin, StringComparison.Ordinal);
        Assert.Contains("Açık port 445 (SMB)", s.Metin, StringComparison.Ordinal); Assert.Contains("run.bat", s.Metin, StringComparison.Ordinal);   // bulgunun anlamı korunur
    }
}

[Collection("Veri")]
public class KurumsalAdim4Testleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    private static void Sta(Action a)
    {
        Exception? hata = null; var t = new Thread(() => { try { a(); } catch (Exception e) { hata = e; } }); t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException(); if (hata != null) throw new Exception("Sınama hatası: " + hata, hata);
    }

    // ---------- Kolay Görünüm ilk gösterge ----------
    [Fact]
    public void KolayIlkGosterge_SensorYoksa_SonTaramaDurumu_VarsaKaliSensor()
    {
        var kayit = new GecmisKayit("a", new DateTime(2026, 11, 22, 9, 5, 0), "127.0.0.1", true, "Hızlı", 8, 3, 85, 0, 2, 6, 0, new List<Bulgu>());
        var eski = new GecmisKayit("b", new DateTime(2026, 11, 1, 9, 5, 0), "127.0.0.1", true, "Hızlı", 8, 3, 40, 3, 0, 5, 0, new List<Bulgu>());
        var g = KolayGostergeleri.Birinci(false, ("Aktif", 0), new[] { eski, kayit });
        Assert.Equal("Son Tarama Durumu", g.Baslik); Assert.True(g.SonTarama); Assert.Equal("22.11.2026 09:05 · puan 85/100", g.Yazi); Assert.Equal(1, g.Renk);   // en yeni kayıt, sarı bulgu → uyarı rengi
        var k = KolayGostergeleri.Birinci(true, ("Aktif", 0), new[] { kayit }); Assert.Equal("Kali Sensör", k.Baslik); Assert.False(k.SonTarama); Assert.Equal("Aktif", k.Yazi);
        Assert.Equal(("Henüz tarama yapılmadı", 3), KolayGostergeleri.SonTarama(Array.Empty<GecmisKayit>())); Assert.Equal(("Henüz tarama yapılmadı", 3), KolayGostergeleri.SonTarama(null));
        Assert.Equal(2, KolayGostergeleri.SonTarama(new[] { eski }).Renk); Assert.Equal(0, KolayGostergeleri.SonTarama(new[] { new GecmisKayit("c", DateTime.Now, "x", true, "Hızlı", 1, 1, 100, 0, 0, 5, 0, new List<Bulgu>()) }).Renk);
    }

    // ---------- Görsel demo yayları ----------
    [Fact]
    public void GorselAyarlar_VarsayilanlarKorumaVeGizlilikOdakli_KaliciVeOlayVerir()
    {
        string eskiKok = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "gorsel_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(gecici);
        try
        {
            BulutKlasor.Kok = gecici; GorselAyarlar.Sifirla(); int olay = 0; Action a = () => olay++; GorselAyarlar.Degisti += a;
            try
            {
                Assert.False(GorselAyarlar.DemoYaylar); Assert.True(GorselAyarlar.ClaudeMaskele);   // demo yaylar KAPALI, Claude maskeleme AÇIK
                GorselAyarlar.ClaudeMaskele = false; Assert.Equal(1, olay); GorselAyarlar.ClaudeMaskele = false; Assert.Equal(1, olay);   // aynı değer olay vermez
                GorselAyarlar.Sifirla(); Assert.False(GorselAyarlar.DemoYaylar); Assert.False(GorselAyarlar.ClaudeMaskele);   // dosyadan kalıcı okunur; demo yaylar HİÇBİR koşulda açılmaz (D-düzeltme: örnek yaylar gerçek trafik gibi görünüp yanıltmasın)
                File.WriteAllText(GorselAyarlar.Yol, "bozuk{"); GorselAyarlar.Sifirla(); Assert.False(GorselAyarlar.DemoYaylar); Assert.True(GorselAyarlar.ClaudeMaskele);   // bozuk dosya varsayılana döner
            }
            finally { GorselAyarlar.Degisti -= a; }
        }
        finally { GorselAyarlar.Sifirla(); BulutKlasor.Kok = eskiKok; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "KurumsalAdim4Testleri.temizlik"); } }
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void KureHtml_DemoYaylarVarsayilanKapali_OrnekNotuVeYayUretimiYalnizcaAcikken()
    {
        string? kok = Kok(); if (kok == null) return;
        string html = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kure", "kure.html"));
        Assert.Contains("demoYay: false", html, StringComparison.Ordinal);                                                          // varsayılan kapalı
        Assert.Contains("if (state.demoYay) for (let n = 0;", html, StringComparison.Ordinal);                                     // yay üretimi demo'ya bağlı
        Assert.Contains("(state.demoYay ? ' · Yaylar örnek görseldir' : '')", html, StringComparison.Ordinal);                     // not yalnızca demo açıkken
        Assert.Single(Regex.Matches(html, "Yaylar örnek görseldir"));                                                      // başka hiçbir sabit yerde geçmez
        Assert.DoesNotContain("for (let i = 0; i < 30; i++) { spawnArc();", html.Replace("function demoYaylariHazirla() { for (let i = 0; i < 30; i++) { spawnArc();", ""), StringComparison.Ordinal);   // açılışta kendiliğinden ön yay üretilmez
        Assert.Contains("o.demoYay", html, StringComparison.Ordinal);
        string panel = File.ReadAllText(Path.Combine(kok, "LabDenetim", "KureWebPaneli.cs")); Assert.Contains("\\\"demoYay\\\":", panel, StringComparison.Ordinal);
    }

    // ---------- Claude onay penceresi ----------
    [Fact]
    public void ClaudeOnayFormu_MaskelenmisOnizlemeGonderilecekMetinleAynidir_KapatilirsaHamGosterilir()
    {
        Sta(() =>
        {
            string eskiKok = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "claude_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(gecici);
            try
            {
                BulutKlasor.Kok = gecici; GorselAyarlar.Sifirla();
                string metin = "Hedef 192.168.1.15\r\nDosya C:\\Users\\ali\\Belgeler\\x.docx\r\nKullanıcı ali";
                using var f = new ClaudeOnayFormu(metin, new MaskeSecenekleri { KullaniciAdListesi = new[] { "ali" } });
                Assert.True(f.MaskeleAcik);                                                                   // varsayılan AÇIK
                Assert.Contains("192.168.1.x", f.OnizlemeMetni, StringComparison.Ordinal); Assert.DoesNotContain("192.168.1.15", f.OnizlemeMetni, StringComparison.Ordinal); Assert.DoesNotContain("Belgeler", f.OnizlemeMetni, StringComparison.Ordinal); Assert.Contains("<kullanici>", f.OnizlemeMetni, StringComparison.Ordinal);
                Assert.Equal(f.OnizlemeMetni, f.GonderilecekMetin);                                           // gönderilecek metin önizlemeyle birebir aynı
                f.MaskeleAcik = false; Assert.Equal(metin, f.OnizlemeMetni); Assert.Equal(metin, f.GonderilecekMetin);   // kapatılırsa ham metin görünür (ve onu gönderir)
                var vazgec = f.CancelButton as Button; Assert.Equal("Vazgeç", vazgec?.Text); Assert.Same(vazgec, f.ActiveControl);   // varsayılan seçili düğme Vazgeç: Enter ile kazara gönderim yok
            }
            finally { GorselAyarlar.Sifirla(); BulutKlasor.Kok = eskiKok; try { Directory.Delete(gecici, true); } catch (IOException hkt2) { HataKayit.Sessiz(hkt2, "KurumsalAdim4Testleri.temizlik"); } }
        });
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void YapayZeka_GonderimdenOnceMaskelenmisOnayAlir_MaskelenmisMetniGonderir()
    {
        string? kok = Kok(); if (kok == null) return;
        string kod = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kontroller", "TaramaSayfasi.YapayZeka.cs"));
        Assert.Contains("ClaudeOnayFormu.Sor(this, AnalizMetniOlustur(tek, false))", kod, StringComparison.Ordinal);
        Assert.Contains("SistemTalimati, gonderilecek,", kod, StringComparison.Ordinal);                                // ham metin değil, onaylanan (maskelenmiş) metin gider
        Assert.DoesNotContain("SistemTalimati, AnalizMetniOlustur(tek, false)", kod, StringComparison.Ordinal);
        Assert.DoesNotContain("_gonderimOnaylandi = true;", kod, StringComparison.Ordinal);                             // oturum başına tek onay yok: her gönderimde önizleme
    }

    // ---------- Marka bağları ----------
    [Fact, Trait("Tur", "KaynakKurali")]
    public void MarkaBilgisiGorunenYerlerde_BrandConfigtenGelir()
    {
        string? kok = Kok(); if (kok == null) return;
        string Oku(string p) => File.ReadAllText(Path.Combine(kok, p));
        Assert.Contains("BrandConfig.Aktif.KisaBaslik", Oku(@"LabDenetim\Kabuk\SidebarNav.cs"), StringComparison.Ordinal);       // sol menü logosu
        Assert.Contains("UrunBilgisi.Ad", Oku(@"LabDenetim\Acilis.cs"), StringComparison.Ordinal);                                // açılış
        Assert.Contains("BrandConfig.Aktif.KisaBaslik", Oku(@"LabDenetim\Kontroller\Karsilama.cs"), StringComparison.Ordinal);    // giriş/karşılama başlığı
        Assert.Contains("BrandConfig.Aktif.DestekEposta", Oku(@"LabDenetim\HakkindaFormu.cs"), StringComparison.Ordinal);         // Hakkımızda
        Assert.Contains("UrunBilgisi.Ad + \"; yerel", Oku(@"LabDenetim\HakkindaFormu.cs"), StringComparison.Ordinal);
        Assert.Contains("BrandConfig.Aktif.UrunAdi", Oku(@"BulutSoft.Core\UrunBilgisi.cs"), StringComparison.Ordinal);
    }
}
