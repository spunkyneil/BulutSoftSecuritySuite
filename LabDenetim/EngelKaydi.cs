// Bu dosya programın engellediği IP adreslerinin NE ZAMAN engellendiğini tutar (engel yaşı).
// Amaç: yanlışlıkla ya da süresi geçmiş engeller sonsuza dek kalmasın; kullanıcı eski engelleri görüp onayla kaldırabilsin.
// Kayıt yalnızca bilgi içindir: güvenlik duvarındaki gerçek kural esastır, kayıt silinse/bozulsa engel bozulmaz.
using System.Net;

// Programımızın adı.
namespace LabDenetim;

// Engel zamanı kaydı.
internal static class EngelKaydi
{
    // Varsayılan engel ömrü (gün): bundan eski engeller "eski" sayılır.
    public const int VarsayilanGun = 30;
    // Dosya yolu.
    public static string Yol => BulutKlasor.Dosya("engel_kayit.json");

    // Kaydı okur (IP → UTC zaman); yoksa/bozuksa boş.
    public static Dictionary<string, DateTime> Oku() => Depo.Yukle<Dictionary<string, DateTime>>(Yol) ?? new();

    // Başarıyla engellenen IP'leri o anki zamanla kaydeder (zaten kayıtlı olanın ilk zamanı korunur).
    public static void Ekle(IEnumerable<string> ipler, DateTime simdiUtc)
    {
        // Mevcut kayıt.
        var k = Oku();
        // Her geçerli IP.
        foreach (var ip in ipler) if (IPAddress.TryParse(ip, out var a)) k.TryAdd(a.ToString(), simdiUtc);
        // Yaz.
        Depo.YazJson(Yol, k);
    }

    // Kaldırılan IP'leri kayıttan siler.
    public static void Cikar(IEnumerable<string> ipler)
    {
        // Mevcut kayıt.
        var k = Oku(); bool degisti = false;
        // Sil.
        foreach (var ip in ipler) if (IPAddress.TryParse(ip, out var a)) degisti |= k.Remove(a.ToString());
        // Yalnızca değiştiyse yaz.
        if (degisti) Depo.YazJson(Yol, k);
    }

    // Engel yaşı (kayıt yoksa null: engel bu kayıttan önce konmuş olabilir).
    public static TimeSpan? Yas(string ip, IReadOnlyDictionary<string, DateTime> kayit, DateTime simdiUtc)
        => IPAddress.TryParse(ip, out var a) && kayit.TryGetValue(a.ToString(), out var z) ? (simdiUtc > z ? simdiUtc - z : TimeSpan.Zero) : null;

    // Ekranda görünen kısa metin: "Engelli · 3 gün" ya da kayıt yoksa "Engelli".
    public static string Metin(TimeSpan? yas) => yas == null ? "Engelli" : yas.Value.TotalDays >= 1 ? $"Engelli · {(int)yas.Value.TotalDays} gün" : "Engelli · bugün";

    // Süresi dolmuş engeller: güvenlik duvarında hâlâ var (engelliKurallar) VE kayıtta ömründen eski olanlar. Kaydı olmayan engele dokunulmaz.
    public static List<string> Eskiler(IReadOnlyDictionary<string, DateTime> kayit, IEnumerable<string> engelliKurallar, DateTime simdiUtc, int gun = VarsayilanGun)
    {
        // Kural adı → IP eşlemesi için kayıttaki her IP'nin kural adını üret.
        var kurallar = engelliKurallar.ToHashSet(StringComparer.Ordinal);
        // Süresi dolanlar.
        return kayit.Where(x => simdiUtc - x.Value >= TimeSpan.FromDays(gun) && kurallar.Contains(BruteForce.KuralAdi(x.Key))).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }
}
