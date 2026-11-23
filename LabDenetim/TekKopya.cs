// Tek kopya kilidi: aynı kullanıcı için programın ikinci kopyası açılırsa ortak ayar/olay dosyaları üst üste yazılmasın.
// Windows'un adlandırılmış "Mutex" nesnesiyle yapılır; ek dosya, ek izin veya yönetici hakkı gerekmez.
using System.Security.Cryptography;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Tek kopya kilidi.
internal static class TekKopya
{
    // Veri klasörüne özel kilit adı: farklı veri klasörleri (örn. testler) birbirini engellemez.
    public static string Ad(string veriKlasoru) => "Local\\BulutSoft_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(veriKlasoru.ToLowerInvariant())))[..16];

    // Kilidi almaya çalışır. bekleMs: kilit doluysa en çok bu kadar bekle ("Kullanıcı Değiştir" ile yeniden başlatmada eski kopyanın kapanması için).
    // Alınırsa true ve tutucu verir (program bitene kadar canlı tutulmalı); doluysa false.
    public static bool Al(string ad, int bekleMs, out Mutex? tutucu)
    {
        // Hata durumunda tutucu yok.
        tutucu = null;
        try
        {
            // Oluştur ya da var olana bağlan.
            var m = new Mutex(true, ad, out bool yeni);
            // Biz oluşturduysak kilit bizde.
            if (yeni) { tutucu = m; return true; }
            // Başka kopya tutuyor: istenirse biraz bekle (kapanıyor olabilir).
            try { if (bekleMs > 0 && m.WaitOne(bekleMs)) { tutucu = m; return true; } }
            // Önceki kopya kilidi bırakmadan kapandıysa (çökme) kilit artık bizdedir.
            catch (AbandonedMutexException) { tutucu = m; return true; }
            // Alınamadı.
            m.Dispose(); return false;
        }
        // Yönetici olarak açılmış kopya, normal kopyaya erişimi reddeder: bu da "zaten açık" demektir.
        catch (UnauthorizedAccessException) { return false; }
        // Kilit hiç kurulamazsa programı engelleme (güvenli taraf): açılmasına izin ver.
        catch (Exception) { return true; }
    }
}
