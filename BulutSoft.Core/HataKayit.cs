// Bu dosya "bilinçli bastırılan" istisnaların KAYDINI tutar (CLAUDE.md, kural 5: yeni boş catch bloğu yazılamaz).
// Kritik olmayan bir hata programın akışını bozmamalı, ama sessizce de kaybolmamalıdır: Debug derlemesinde çıktı penceresine yazılır,
// her bağlam için sayılır ve son hatalar bellekte tutulur (Tanılama/Destek bunlara bakabilir). Kritik akışlarda ayrıca olay günlüğüne
// (OlayDeposu) sınırlı sayıda kayıt düşer. Bu sınıf HİÇBİR ZAMAN istisna fırlatmaz.
using System.Collections.Concurrent;
using System.Diagnostics;

// Programımızın adı.
namespace LabDenetim;

// Bir bastırılmış hatanın kaydı.
public sealed record SessizHataKaydi(DateTime Zaman, string Baglam, string Tur, string Mesaj);

// Bastırılan hataların kayıt ve sayaç merkezi.
public static class HataKayit
{
    // Bellekte tutulan son hata sayısı üst sınırı.
    private const int EnFazlaKayit = 200;
    // Kritik akışta olay günlüğüne yazılacak ilk hata sayısı ve sonrasında kaçta bir yazılacağı (günlük şişmesin).
    private const int IlkKacOlay = 3, SonrasiHerKacta = 50;

    // Bağlam başına hata sayacı.
    private static readonly ConcurrentDictionary<string, int> Sayaclar = new(StringComparer.Ordinal);
    // Son hatalar (halka tampon).
    private static readonly Queue<SessizHataKaydi> Son = new(); private static readonly object Kilit = new();
    // Olay günlüğüne yazarken yeniden girişi (günlük yazarken oluşan hata yine günlüğe yazmaya çalışmasın) engeller.
    [ThreadStatic] private static bool _gunlugeYaziyor;

    // Kritik olmayan, bilinçle bastırılan bir istisnayı kaydeder. baglam: nerede olduğu ("OlayDeposu.Ekle" gibi).
    public static void Sessiz(Exception ex, string baglam) => Sessiz(ex, baglam, false);

    // kritik: true ise (ve sınırlar içinde) olay günlüğüne de bir uyarı satırı yazılır.
    public static void Sessiz(Exception ex, string baglam, bool kritik)
    {
        try
        {
            string b = string.IsNullOrWhiteSpace(baglam) ? "bilinmeyen" : baglam.Trim();
            string tur = ex?.GetType().Name ?? "Bilinmeyen", mesaj = ex?.Message ?? "";
            int n = Sayaclar.AddOrUpdate(b, 1, static (_, eski) => eski == int.MaxValue ? eski : eski + 1);
            lock (Kilit) { Son.Enqueue(new SessizHataKaydi(DateTime.Now, b, tur, mesaj)); while (Son.Count > EnFazlaKayit) Son.Dequeue(); }
            Debug.WriteLine($"[HataKayit] {b}: {tur}: {mesaj}");
            if (kritik && !_gunlugeYaziyor && (n <= IlkKacOlay || n % SonrasiHerKacta == 0))
            {
                _gunlugeYaziyor = true;
                try { OlayDeposu.Ekle("Sistem", "Uyarı", $"SESSİZ HATA ({b}) -> {tur}: {mesaj} [{n}. kez]"); }
                finally { _gunlugeYaziyor = false; }
            }
        }
        catch (Exception)
        {
            // Son çare: kayıt tutulurken oluşan hata programı etkilememeli; yalnızca Debug çıktısına yazılır.
            Debug.WriteLine("[HataKayit] kayıt sırasında beklenmeyen hata (yutuldu).");
        }
    }

    // Bir bağlamda şimdiye kadar bastırılan hata sayısı.
    public static int Sayi(string baglam) => Sayaclar.TryGetValue(baglam, out int n) ? n : 0;

    // Tüm bağlamların toplam hata sayısı.
    public static int Toplam => Sayaclar.Values.Sum();

    // Bağlam bağlam sayaçların kopyası.
    public static IReadOnlyDictionary<string, int> Ozet() => new Dictionary<string, int>(Sayaclar, StringComparer.Ordinal);

    // Son bastırılan hataların kopyası (en eskiden yeniye).
    public static IReadOnlyList<SessizHataKaydi> SonHatalar() { lock (Kilit) return Son.ToList(); }

    // Sayaçları ve kayıtları sıfırlar (testler için).
    internal static void Sifirla() { Sayaclar.Clear(); lock (Kilit) Son.Clear(); }
}
