// Bu dosya lisans testlerini içerir: imza doğrulama (yerel ECDSA ve OpenSSL/Kali uyumluluğu), makine bağlama, süre, deneme ve saat geri alma.
// Testler gerçek lisansa/kayıt defterine dokunmaz (geçici klasör ve ayrı test anahtarı kullanılır).
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace LabDenetim.Tests;

// Lisans testleri.
[Collection("Veri")]
public class LisansTestleri : IDisposable
{
    // Geçici klasör ve eski ayarlar.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_lisans_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiReg = Lisans.RegistryYolu; private readonly string _eskiMakine = Lisans.MakineKlasoru;
    // Kurulum: gerçek deneme kaydına dokunmamak için ayrı kayıt defteri yolu.
    public LisansTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; Lisans.MakineKlasoru = Path.Combine(_t, "makine"); Lisans.RegistryYolu = @"Software\BulutSoft\LicTest_" + Guid.NewGuid().ToString("N"); }
    // Temizlik.
    public void Dispose() { Lisans.MakineKlasoru = _eskiMakine; try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(Lisans.RegistryYolu, false); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "LisansTestleri.temizlik"); } Lisans.RegistryYolu = _eskiReg; BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "LisansTestleri.temizlik"); } }

    // Test anahtarı ve imzalayıcı (yerel ECDSA, DER biçimi: OpenSSL ile aynı).
    private static readonly ECDsa Anahtar = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private static string GenelPem => Anahtar.ExportSubjectPublicKeyInfoPem();
    private static string Imzala(string veri) => Convert.ToBase64String(Anahtar.SignData(Encoding.ASCII.GetBytes(veri), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
    private static string Paket(LisansBilgi b) => Lisans.PaketOlustur(b, Imzala);
    private static readonly DateTime Simdi = new(2026, 11, 18, 12, 0, 0);

    // Makine kimliği kararlı ve biçimli.
    [Fact] public void MakineKimligi_Kararli() { string a = Lisans.MakineKimligi("guid-1"), b = Lisans.MakineKimligi("guid-1"), c = Lisans.MakineKimligi("guid-2"); Assert.Equal(a, b); Assert.NotEqual(a, c); Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){3}$", a); }

    // Geçerli lisans.
    [Fact] public void Lisans_Gecerli()
    {
        var s = Lisans.Degerlendir(Paket(new("Müşteri A.Ş.", "AAAA-BBBB-CCCC-DDDD", Simdi, Simdi.AddDays(30), "Pro")), GenelPem, "AAAA-BBBB-CCCC-DDDD", Simdi);
        Assert.Equal(LisansDurumu.Lisansli, s.Durum); Assert.Equal(30, s.KalanGun);
    }
    // Süresiz sahip lisansı.
    [Fact] public void Lisans_Sahip() { var s = Lisans.Degerlendir(Paket(new("Sahip", "M", Simdi, null, "Sahip")), GenelPem, "M", Simdi.AddYears(10)); Assert.Equal(LisansDurumu.Sahip, s.Durum); }
    // Başka makine reddedilir.
    [Fact] public void Lisans_BaskaMakine() { var s = Lisans.Degerlendir(Paket(new("X", "AAAA", Simdi, null, "Pro")), GenelPem, "BBBB", Simdi); Assert.Equal(LisansDurumu.Gecersiz, s.Durum); Assert.Contains("başka bir bilgisayar", s.Mesaj); }
    // Makine geneli deneme: başka bir Windows kullanıcısı (kendi AppData'sı ve HKCU'su boş) süreyi sıfırlayamaz.
    [Fact] public void Deneme_Yeni_Kullanici_Profili_Suresi_Sifirlayamaz()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0); var (bas0, _, _) = Lisans.DenemeDurumu(t0); Assert.Equal(t0, bas0); Assert.True(File.Exists(Path.Combine(Lisans.MakineKlasoru, "deneme.dat")));
        // Yeni kullanıcı simülasyonu: kullanıcıya özel dosya ve kayıt defteri silinir, makine kaydı kalır.
        File.Delete(Path.Combine(BulutKlasor.Kok, "deneme.dat")); Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(Lisans.RegistryYolu, false);
        var (bas1, _, _) = Lisans.DenemeDurumu(t0.AddDays(10)); Assert.Equal(t0, bas1);
        Assert.Equal(4, Lisans.DenemeGun - (int)Math.Floor((t0.AddDays(10) - bas1).TotalDays));   // kalan 4 gün; 14 gün yeniden başlamadı
    }
    // Makine kaydı da silinirse ama kullanıcı kaydı duruyorsa süre yine sıfırlanmaz (en erken başlangıç kazanır).
    [Fact] public void Deneme_Makine_Kaydi_Silinse_Kullanici_Kaydi_Korur()
    {
        var t0 = new DateTime(2026, 11, 1, 12, 0, 0); Lisans.DenemeDurumu(t0); File.Delete(Path.Combine(Lisans.MakineKlasoru, "deneme.dat"));
        Assert.Equal(t0, Lisans.DenemeDurumu(t0.AddDays(5)).Baslangic);
    }
    // Makine geneli lisans dosyası (ProgramData): kullanıcıya özel dosya yoksa bu okunur; kurum lisansı tüm kullanıcılara geçerli olur.
    [Fact] public void Makine_Geneli_Lisans_Dosyasi_Kullaniciya_Ozel_Dosya_Olmadan_Okunur()
    {
        Butunluk.YazGuvenli(Lisans.OtoriteYolu, GenelPem); Directory.CreateDirectory(Lisans.MakineKlasoru);
        File.WriteAllText(Lisans.MakineLisansYolu, Paket(new("Kurum A.Ş.", Lisans.KurumMakinesi, DateTime.Now.Date, DateTime.Now.Date.AddDays(100), "Pro")));
        Assert.False(File.Exists(Lisans.LisansYolu));
        var s = Lisans.Durum(); Assert.Equal(LisansDurumu.Lisansli, s.Durum); Assert.Equal("Kurum A.Ş.", s.Bilgi!.Musteri);
    }
    // Kurum lisansı: imzalı "*" makine alanı her bilgisayarda geçerli, süresi yine denetlenir.
    [Fact] public void Lisans_Kurum_Her_Makinede_Gecerli_Ama_Suresi_Dolar()
    {
        string p = Paket(new("Kurum A.Ş.", Lisans.KurumMakinesi, Simdi, Simdi.AddDays(10), "Pro"));
        Assert.Equal(LisansDurumu.Lisansli, Lisans.Degerlendir(p, GenelPem, "XXXX", Simdi).Durum); Assert.Equal(LisansDurumu.Lisansli, Lisans.Degerlendir(p, GenelPem, "YYYY", Simdi).Durum);
        Assert.Equal(LisansDurumu.LisansSuresiDoldu, Lisans.Degerlendir(p, GenelPem, "XXXX", Simdi.AddDays(11)).Durum);
    }
    // Tek makineli lisansın makine alanı "*" yapılırsa imza bozulur (kurum lisansına dönüştürme saldırısı).
    [Fact] public void Lisans_Tek_Makine_Yildiza_Cevrilemez()
    {
        string p = Paket(new("X", "AAAA", Simdi, null, "Pro")); var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(p)!;
        d["veri"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new LisansBilgi("X", "*", Simdi, null, "Pro"))));
        Assert.Equal(LisansDurumu.Gecersiz, Lisans.Degerlendir(System.Text.Json.JsonSerializer.Serialize(d), GenelPem, "BBBB", Simdi).Durum);
    }
    // Süresi dolmuş.
    [Fact] public void Lisans_SuresiDolmus() => Assert.Equal(LisansDurumu.LisansSuresiDoldu, Lisans.Degerlendir(Paket(new("X", "M", Simdi, Simdi.AddDays(5), "Pro")), GenelPem, "M", Simdi.AddDays(6)).Durum);
    // İçerik değiştirilirse imza bozulur (örn. bitiş tarihi uzatma denemesi).
    [Fact] public void Lisans_Degistirilmis()
    {
        string p = Paket(new("X", "M", Simdi, Simdi.AddDays(5), "Pro")); var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(p)!;
        var b2 = new LisansBilgi("X", "M", Simdi, Simdi.AddYears(50), "Pro"); d["veri"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(b2)));
        var s = Lisans.Degerlendir(System.Text.Json.JsonSerializer.Serialize(d), GenelPem, "M", Simdi); Assert.Equal(LisansDurumu.Gecersiz, s.Durum); Assert.Contains("imza", s.Mesaj, StringComparison.OrdinalIgnoreCase);
    }
    // Başka otoritenin (başka anahtarın) imzası reddedilir: keygen engeli.
    [Fact] public void Lisans_BaskaOtorite()
    {
        using var yabanci = ECDsa.Create(ECCurve.NamedCurves.nistP256); string p = Lisans.PaketOlustur(new("X", "M", Simdi, null, "Pro"), v => Convert.ToBase64String(yabanci.SignData(Encoding.ASCII.GetBytes(v), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)));
        Assert.Equal(LisansDurumu.Gecersiz, Lisans.Degerlendir(p, GenelPem, "M", Simdi).Durum);
    }
    // Bozuk/boş girdiler çökmez.
    [Theory, InlineData(""), InlineData("bozuk"), InlineData("{}"), InlineData("{\"veri\":\"x\",\"imza\":\"y\"}")] public void Lisans_BozukGirdi(string metin) => Assert.Equal(LisansDurumu.Gecersiz, Lisans.Degerlendir(metin, GenelPem, "M", Simdi).Durum);
    // Otorite kurulu değilse.
    [Fact] public void Lisans_OtoriteYok() => Assert.Equal(LisansDurumu.Gecersiz, Lisans.Degerlendir(Paket(new("X", "M", Simdi, null, "Pro")), null, "M", Simdi).Durum);

    // OpenSSL (Kali'deki aynı komut biçimi) imzası C# tarafından doğrulanır. Git'in openssl'i yoksa test atlanır.
    [Trait("Category", "Integration")]
    [OpenSslFact] public void OpenSsl_Uyumlulugu()
    {
        string ssl = TestOpenSsl.Yol!;   // [OpenSslFact]: OpenSSL yoksa test AÇIKÇA atlanır
        string ozel = Path.Combine(_t, "ozel.pem"), genel = Path.Combine(_t, "genel.pem");
        Calistir(ssl, $"ecparam -name prime256v1 -genkey -noout -out \"{ozel}\""); Calistir(ssl, $"ec -in \"{ozel}\" -pubout -out \"{genel}\"");
        string paket = Lisans.PaketOlustur(new("OpenSSL Müşteri", "M-1", Simdi, Simdi.AddDays(10), "Pro"), veri => { string v = Path.Combine(_t, "v.txt"), s = Path.Combine(_t, "s.bin"); File.WriteAllText(v, veri, Encoding.ASCII); Calistir(ssl, $"dgst -sha256 -sign \"{ozel}\" -out \"{s}\" \"{v}\""); return Convert.ToBase64String(File.ReadAllBytes(s)); });
        var sonuc = Lisans.Degerlendir(paket, File.ReadAllText(genel), "M-1", Simdi); Assert.Equal(LisansDurumu.Lisansli, sonuc.Durum); Assert.Equal(10, sonuc.KalanGun);
    }
    // Süreç çalıştırır.
    private static void Calistir(string exe, string arg) { using var p = Process.Start(new ProcessStartInfo(exe, arg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true })!; p.WaitForExit(20000); Assert.Equal(0, p.ExitCode); }

    // Deneme: ilk çalıştırmada 14 gün, sonra azalır, bitince DenemeBitti.
    [Fact] public void Deneme_Surer_ve_Biter()
    {
        var s0 = Lisans.Durum(Simdi); Assert.Equal(LisansDurumu.Deneme, s0.Durum); Assert.Equal(14, s0.KalanGun);
        Assert.Equal(9, Lisans.Durum(Simdi.AddDays(5)).KalanGun); Assert.Equal(LisansDurumu.DenemeBitti, Lisans.Durum(Simdi.AddDays(15)).Durum);
    }
    // Saat geri alınırsa deneme uzamaz.
    [Fact] public void Deneme_SaatGeri()
    {
        Lisans.Durum(Simdi); var ileri = Lisans.Durum(Simdi.AddDays(10)); Assert.Equal(4, ileri.KalanGun);
        var geri = Lisans.Durum(Simdi); Assert.Equal(4, geri.KalanGun); Assert.Contains("geri alınmış", geri.Mesaj);
    }
    // Dosya silinse de kayıt defterinden başlangıç korunur (sıfırlama zorlaşır).
    [Fact] public void Deneme_DosyaSilinirse()
    {
        Lisans.Durum(Simdi); Lisans.Durum(Simdi.AddDays(8)); File.Delete(Path.Combine(_t, "deneme.dat"));
        Assert.Equal(6, Lisans.Durum(Simdi.AddDays(8)).KalanGun);
    }
    // Yeni tarama izni: yalnızca bitmiş deneme/lisans kısıtlanır; diğer her şey serbest.
    [Fact] public void Kisitlama_YalnizcaYeniTarama()
    {
        Assert.True(Lisans.YeniTaramaIzni(new(LisansDurumu.Deneme, 5, "", null)).Izinli); Assert.True(Lisans.YeniTaramaIzni(new(LisansDurumu.Sahip, -1, "", null)).Izinli); Assert.True(Lisans.YeniTaramaIzni(new(LisansDurumu.Lisansli, 9, "", null)).Izinli);
        Assert.False(Lisans.YeniTaramaIzni(new(LisansDurumu.DenemeBitti, 0, "bitti", null)).Izinli); Assert.False(Lisans.YeniTaramaIzni(new(LisansDurumu.LisansSuresiDoldu, 0, "doldu", null)).Izinli);
    }
}
