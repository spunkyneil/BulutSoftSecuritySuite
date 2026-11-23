// Bu dosya uygulama (arayüz) ile "BulutSoftGuard" Windows hizmeti arasındaki ORTAK kısmı içerir: protokol, ikinci mühür kanalı (HKLM) ve istemci.
// Neden hizmet? Kullanıcı hesabıyla çalışan bir zararlı, kullanıcının %AppData% dosyalarını ve DPAPI anahtarlarını okuyup yazabilir; bu yüzden yalnızca o dosyalarda tutulan
// mühür tek başına yeterli değildir. Hizmet SYSTEM olarak çalışır; mühür özetleri HKLM altında (kullanıcı OKUR, yalnızca SYSTEM/Yönetici YAZAR) saklanır.
// Arayüz "mühür yok / uyuşmuyor" gördüğünde ikinci kanala bakar: hizmette de kayıt yoksa ya da özet uyuşmuyorsa dosya BOZUK sayılır (sessiz ilk-kurulum dönüşü yok).
// DÜRÜST SINIR: hizmet kurulu değilse ya da durmuşsa bu ikinci kanal yoktur; program önceki (yalnızca yerel mühür) davranışına döner ve bunu açıkça gösterir.
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

// Programımızın adı.
namespace LabDenetim;

// Hizmet sabitleri.
internal static class KorumaProtokol
{
    public const string HizmetAdi = "BulutSoftGuard", PipeAdi = "BulutSoftGuard", HklmAnahtar = @"SOFTWARE\BulutSoft";
    public const string AppExeAdi = "BulutSoftSecuritySuite.exe";
}

// Pipe üzerinden giden istek ve cevap (tek satır JSON).
// V: protokol sürümü ("1"); eski istemciler alanı göndermezse varsayılan "1" sayılır.
internal sealed record ServisIstek(string Komut, Dictionary<string, string>? Arg = null, string V = "1");
internal sealed record ServisCevap(bool Tamam, string? Hata = null, Dictionary<string, string>? Veri = null, string V = "1");

// İkinci mühür kanalı: HKLM\SOFTWARE\BulutSoft\Muhur altında ad → özet. Okuma herkese açık; yazma yalnızca hizmet (SYSTEM) tarafından yapılır.
internal static class IkinciKanal
{
    // Testte ve geliştirmede başka bir kök (HKCU) kullanılabilir; ÜRETİMDE değiştirilmez (ortam değişkeniyle değil, yalnızca koddan).
    internal static RegistryKey Kok { get; set; } = Registry.LocalMachine;
    // Hizmetin HKLM düzeni: SOFTWARE\BulutSoft\Guard  (KimlikMuhurHash, KimlikMuhurZamani, KimlikIlkKurulum, KanalKuruldu, AnahtarSurum, dosya-* özetleri; alt anahtarlar: SensorHostKeys, Gizli).
    internal static string AltAnahtar { get; set; } = KorumaProtokol.HklmAnahtar + @"\Guard";
    internal static string HostKeyAnahtari => AltAnahtar + @"\SensorHostKeys"; internal static string GizliAnahtar => AltAnahtar + @"\Gizli";
    // Mantıksal ad → kayıt değeri adı ("kimlik" → KimlikMuhurHash).
    internal static string DegerAdi(string ad) => ad == "kimlik" ? "KimlikMuhurHash" : ad;

    // Kaydı okur (yoksa ya da erişilemezse null).
    public static string? Oku(string ad) { try { using var k = Kok.OpenSubKey(AltAnahtar, false); return k?.GetValue(DegerAdi(ad)) as string; } catch { return null; } }
    // Doğrudan değer adıyla okur (KimlikIlkKurulum gibi).
    public static object? OkuHam(string deger) { try { using var k = Kok.OpenSubKey(AltAnahtar, false); return k?.GetValue(deger); } catch { return null; } }

    // Kaydı yazar (yalnızca hizmet çağırır; yetkisiz süreçte false verir).
    public static bool Yaz(string ad, string deger) { try { using var k = Kok.CreateSubKey(AltAnahtar, true); k.SetValue(DegerAdi(ad), deger, RegistryValueKind.String); return true; } catch { return false; } }
    // Ek değer (zaman damgası, sürüm vb.) yazar; yalnızca hizmet çağırır.
    public static bool YazHam(string deger, object icerik, RegistryValueKind tur) { try { using var k = Kok.CreateSubKey(AltAnahtar, true); k.SetValue(deger, icerik, tur); return true; } catch { return false; } }

    // Dosyanın SHA-256 özeti (hex).
    public static string DosyaOzeti(string yol) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(yol)));
    public static bool Esit(string a, string b) => a.Length == b.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a.ToUpperInvariant()), Encoding.ASCII.GetBytes(b.ToUpperInvariant()));
}

// Hizmeti yükseltilmiş (UAC) başlatır; çıkış kodu OKUNUR (0 = başladı, 1056 = zaten çalışıyor). Sonuç (başarılı mı, kullanıcıya gösterilecek metin).
internal static class HizmetYonetimi
{
    public static async Task<(bool Basarili, string Metin)> BaslatAsync()
    {
        var (basladi, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync("sc.exe start " + KorumaProtokol.HizmetAdi); if (!basladi) return (false, hata);
        int kod = GuvenlikDuvari.SonCikisKodu; ServisIstemci.Sifirla(); await Task.Delay(1500);
        bool ayakta = await Task.Run(() => ServisIstemci.Calisiyor);
        if (kod == 0 || kod == 1056) return ayakta ? (true, kod == 1056 ? "Hizmet zaten çalışıyordu." : "Hizmet başlatıldı ve yanıt veriyor.") : (true, "Başlatma komutu başarılı (çıkış kodu " + kod + ") ama hizmet henüz yanıt vermiyor; birkaç saniye sonra yeniden bakın.");
        return (false, kod switch { 1060 => "Hizmet kurulu değil (çıkış kodu 1060). Kurulum paketini yeniden çalıştırın ya da yönetici PowerShell'de HizmetKur.ps1 çalıştırın.", 5 => "Erişim reddedildi (çıkış kodu 5): yönetici izni gerekli.", _ => "Hizmet başlatılamadı (sc.exe çıkış kodu " + kod + ")." });
    }
}

// Arayüzün hizmetle konuşan istemcisi.
internal static class ServisIstemci
{
    // Testte sahte hizmet için başka bir boru adı kullanılabilir.
    internal static string PipeAdi { get; set; } = KorumaProtokol.PipeAdi;
    private static DateTime _sonKontrol = DateTime.MinValue; private static bool _sonCalisiyor;

    // Hizmete bir komut yollar. Hizmet yoksa ya da yanıt vermezse null döner (sessizce).
    public static ServisCevap? Gonder(string komut, Dictionary<string, string>? arg = null, int zamanAsimiMs = 2000)
    {
        try
        {
            using var c = new NamedPipeClientStream(".", PipeAdi, PipeDirection.InOut, PipeOptions.None); c.Connect(zamanAsimiMs);
            using var w = new StreamWriter(c, new UTF8Encoding(false), 4096, true) { AutoFlush = true }; using var r = new StreamReader(c, Encoding.UTF8, false, 4096, true);
            w.WriteLine(JsonSerializer.Serialize(new ServisIstek(komut, arg)));
            var t = r.ReadLineAsync(); if (!t.Wait(zamanAsimiMs + 3000) || t.Result == null) return null;
            return JsonSerializer.Deserialize<ServisCevap>(t.Result);
        }
        catch { return null; }
    }

    // Hizmet şu an yanıt veriyor mu? (5 sn önbellek)
    public static bool Calisiyor { get { if ((DateTime.UtcNow - _sonKontrol).TotalSeconds > 5) { _sonCalisiyor = Gonder("ping", null, 600)?.Tamam == true; _sonKontrol = DateTime.UtcNow; } return _sonCalisiyor; } }
    // Hizmete bağlanamazsa 5 sn arayla en çok 3 kez dener (arayüzü kilitlemez: await). Yanıt gelirse hemen döner; hâlâ yoksa null.
    public static async Task<ServisCevap?> GonderYenidenDeneAsync(string komut, Dictionary<string, string>? arg = null, int deneme = 3, int beklemeMs = 5000)
    {
        for (int i = 0; i < deneme; i++) { var c = await Task.Run(() => Gonder(komut, arg, 1200)); if (c != null) return c; if (i < deneme - 1) await Task.Delay(beklemeMs); }
        return null;
    }

    // Önbelleği sıfırlar.
    public static void Sifirla() { _sonKontrol = DateTime.MinValue; }

    // Hizmet kurulu mu? (kayıt defterindeki hizmet kaydı)
    public static bool Kurulu { get { try { using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + KorumaProtokol.HizmetAdi); return k != null; } catch { return false; } } }

    // Ekranda gösterilecek kısa durum.
    public static string DurumMetni => Calisiyor ? "Koruma hizmeti ÇALIŞIYOR (arayüz kapalıyken de koruma sürer)" : Kurulu ? "Koruma hizmeti kurulu ama ÇALIŞMIYOR" : "Koruma hizmeti KURULU DEĞİL (koruma yalnızca program açıkken çalışır)";

    // Mühür özetini hizmete kaydettirir (yalnızca bu uygulamanın exe'sinden gelen istek kabul edilir).
    public static bool MuhurYaz(string ad, string ozet) => Gonder("muhur-yaz", new() { ["ad"] = ad, ["ozet"] = ozet })?.Tamam == true;
}
