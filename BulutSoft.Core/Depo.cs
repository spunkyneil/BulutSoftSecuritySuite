// Bu dosya programın JSON veri dosyalarını GÜVENLİ yazıp okuyan ortak katmandır.
// Amaç: elektrik kesintisi/çökme olsa bile dosya yarım kalmaz, bozulursa son sağlam kopyadan (".bak") geri dönülür.
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Ortak, atomik veri deposu.
internal static class Depo
{
    // Aynı süreçteki eşzamanlı yazımları sıraya sokar.
    private static readonly object Kilit = new();
    // BOM'suz UTF-8 (eski dosyalarla aynı biçim).
    private static readonly UTF8Encoding Utf8 = new(false);

    // Metin geçerli bir JSON mu?
    public static bool JsonMu(string? metin) { if (string.IsNullOrWhiteSpace(metin)) return false; try { using var _ = JsonDocument.Parse(metin); return true; } catch { return false; } }

    // Geçici dosya adı: sürece ve çağrıya özgü (iki süreç aynı dosyayı yazarken ortak ".tmp" çakışmasın).
    private static string GeciciAd(string yol) { ArtikTemizle(yol); return yol + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp"; }

    // Eski sürümden/çökmeden kalmış geçici dosyaları siler: sabit adlı ".tmp" ve 10 dakikadan eski benzersiz ".tmp"ler (yol başına süreç ömründe bir kez).
    private static readonly HashSet<string> Temizlenenler = new(StringComparer.OrdinalIgnoreCase);
    private static void ArtikTemizle(string yol)
    {
        try
        {
            if (File.Exists(yol + ".tmp")) File.Delete(yol + ".tmp");
            lock (Temizlenenler) if (!Temizlenenler.Add(yol)) return;
            var d = Path.GetDirectoryName(yol); if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return;
            foreach (var f in Directory.EnumerateFiles(d, Path.GetFileName(yol) + ".*.tmp")) if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > TimeSpan.FromMinutes(10)) File.Delete(f);
        }
        catch (Exception hk) when (hk is IOException or UnauthorizedAccessException) { HataKayit.Sessiz(hk, "Depo.ArtikTemizle"); }
    }

    // Yedek dosyanın yolu.
    public static string YedekYolu(string yol) => yol + ".bak";

    // Dosyayı güvenli yazar: önce ".tmp", sonra yer değiştirme. Mevcut dosya sağlam bir JSON ise ".bak" olarak saklanır.
    public static void Yaz(string yol, string icerik)
    {
        // Sıraya gir.
        lock (Kilit)
        {
            // Klasör.
            var d = Path.GetDirectoryName(yol); if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
            // Geçici dosya.
            string gecici = GeciciAd(yol); File.WriteAllBytes(gecici, Utf8.GetBytes(icerik));
            // Sağlam eski sürümü yedekle (bozuk dosya yedeğin üzerine yazılmaz).
            try { if (File.Exists(yol) && JsonMu(File.ReadAllText(yol))) File.Copy(yol, YedekYolu(yol), true); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Depo.Yaz"); }
            // Yer değiştir (olmazsa geçici dosya bırakılmaz).
            try { File.Move(gecici, yol, true); } catch (IOException) { try { File.Delete(gecici); } catch (IOException hk) { HataKayit.Sessiz(hk, "Depo.Yaz"); } throw; }
        }
    }

    // JSON nesnesini yazar.
    public static void YazJson<T>(string yol, T nesne, JsonSerializerOptions? ayar = null) => Yaz(yol, JsonSerializer.Serialize(nesne, ayar));

    // İkili (şifreli anahtar gibi) dosyayı atomik yazar (".tmp" + yer değiştirme).
    public static void YazBayt(string yol, byte[] veri)
    {
        // Sıraya gir.
        lock (Kilit)
        {
            // Klasör ve geçici dosya.
            var d = Path.GetDirectoryName(yol); if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
            string gecici = GeciciAd(yol); File.WriteAllBytes(gecici, veri); File.Move(gecici, yol, true);
        }
    }

    // Ana dosyadan, olmazsa ".bak" yedeğinden okur; ikisi de okunamazsa varsayılan değer verir.
    public static T? Yukle<T>(string yol, JsonSerializerOptions? ayar = null)
    {
        // Ana dosya sonra yedek.
        foreach (var y in new[] { yol, YedekYolu(yol) })
        {
            // Okunabiliyorsa dön.
            try { if (File.Exists(y)) { var v = JsonSerializer.Deserialize<T>(File.ReadAllText(y), ayar); if (v != null) return v; } } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Depo.Yukle"); }
        }
        // Hiçbiri yoksa.
        return default;
    }
}
