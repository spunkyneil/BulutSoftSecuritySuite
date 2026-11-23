// Bu dosya Uzman Görünümü harita küresindeki "bildirim ışığını" hesaplar: yalnızca Uyarı ve Kritik olaylar ışık yakar, Bilgi olayları yakmaz.
// Olay kayıtlarında coğrafi konum yoktur; bu yüzden ışık, bu programın çalıştığı yerde (Ankara/Türkiye) yanar. Uydurma konum gösterilmez.
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Işığın özeti: kaç Kritik, kaç Uyarı olay var ve en yenisi ne.
internal sealed record IsikOzeti(int Kritik, int Uyari, string SonSeviye, string SonMesaj, DateTime SonZaman);

// Olay listesinden ışık özetini çıkarır.
internal static class KureIsiklari
{
    // Bu süreden eski olaylar ışık yakmaz (ışık kendiliğinden söner).
    public static readonly TimeSpan Pencere = TimeSpan.FromMinutes(30);
    // Tooltip'te gösterilecek en uzun mesaj.
    private const int EnUzunMesaj = 140;

    // Son 30 dakikadaki Uyarı/Kritik olayları sayar; hiç yoksa null (ışık yanmaz).
    public static IsikOzeti? Ozetle(IEnumerable<OlayKaydi> olaylar, DateTime simdi)
    {
        int kritik = 0, uyari = 0; OlayKaydi? son = null;
        foreach (var o in olaylar)
        {
            if (o.Seviye != "Kritik" && o.Seviye != "Uyarı") continue;
            // Gelecekteki (saat kayması) ve pencereden eski olaylar sayılmaz.
            if (o.Zaman > simdi.AddMinutes(1) || simdi - o.Zaman > Pencere) continue;
            if (o.Seviye == "Kritik") kritik++; else uyari++;
            if (son == null || o.Zaman > son.Zaman) son = o;
        }
        if (son == null) return null;
        // Işığın rengi en yüksek seviyeye göre: Kritik varsa kırmızı, yoksa sarı.
        return new IsikOzeti(kritik, uyari, kritik > 0 ? "Kritik" : "Uyarı", Temizle(son.Mesaj), son.Zaman);
    }

    // Mesajdaki denetim karakterlerini atar ve kısaltır (sayfaya yalnızca düz metin gider).
    internal static string Temizle(string? m)
    {
        if (string.IsNullOrEmpty(m)) return "";
        var sb = new System.Text.StringBuilder(m.Length);
        foreach (char c in m) sb.Append(char.IsControl(c) ? ' ' : c);
        string s = sb.ToString().Trim();
        return s.Length > EnUzunMesaj ? s.Substring(0, EnUzunMesaj - 1) + "…" : s;
    }

    // Sayfaya gönderilecek JSON (özet yoksa "null": ışık söner).
    public static string Json(IsikOzeti? o) => o == null ? "null"
        : JsonSerializer.Serialize(new { kritik = o.Kritik, uyari = o.Uyari, seviye = o.SonSeviye, mesaj = o.SonMesaj, zaman = o.SonZaman.ToString("HH:mm") });
}
