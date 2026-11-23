using System.IO;

namespace LabDenetim;

// Pilot (salt okunur) kurulum anahtarı: açıkken program makinede HİÇBİR değişiklik yapmaz
// (güvenlik duvarı kuralı, süreç sonlandırma, başlangıç öğesi kapatma, yönetici komutu). Tarama, rapor ve görme çalışır.
// Açma yolları (BT yöneticisi için): ortam değişkeni BULUTSOFT_SALT_OKUNUR=1 ya da
// %ProgramData%\BulutSoft\salt_okunur.flag dosyası. Normal kullanıcı ProgramData altına yazamadığı için kapatamaz.
internal static class PilotMod
{
    public const string OrtamDegiskeni = "BULUTSOFT_SALT_OKUNUR";
    public const string BayrakAdi = "salt_okunur.flag";

    // Makine genelindeki bayrak klasörü (testte değiştirilebilir).
    public static string MakineKlasoru { get; set; } = BrandConfig.OrtakKlasor;

    // Salt okunur mod açık mı? Okunamazsa güvenli tarafta kalıp AÇIK sayılmaz ama hata da fırlatılmaz.
    public static bool SaltOkunur
    {
        get
        {
            try
            {
                var o = Environment.GetEnvironmentVariable(OrtamDegiskeni);
                if (o is "1" || string.Equals(o, "true", StringComparison.OrdinalIgnoreCase)) return true;
                return File.Exists(Path.Combine(MakineKlasoru, BayrakAdi));
            }
            catch { return false; }
        }
    }

    public const string Mesaj = "Bu kurulum PİLOT (SALT OKUNUR) modunda: program bu bilgisayarda hiçbir değişiklik yapmaz. Tarama, rapor ve görüntüleme çalışır. Değişiklik yapan işlemler için BT yöneticiniz modu kapatmalıdır.";
}
