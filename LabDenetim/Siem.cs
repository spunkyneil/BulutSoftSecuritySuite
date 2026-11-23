// Bu dosya sonuçları "SIEM" sistemlerinin (Splunk, Wazuh, Elastic, QRadar vb.) okuyabildiği iki biçimde dışa aktarır:
// CEF (tek satırlık standart güvenlik olay biçimi) ve JSON Lines (her satır bir JSON olay). Ağa hiçbir şey göndermez; yalnızca dosya yazar.
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// SIEM çıktısı üreten yardımcı.
internal static class SiemCikti
{
    // Önem derecesini CEF'in 0-10 ölçeğine çevirir.
    public static int CefOnem(Onem o) => o switch { Onem.Yuksek => 9, Onem.Orta => 6, Onem.Dusuk => 3, _ => 0 };

    // CEF başlık alanında "\" ve "|" karakterleri kaçırılmalıdır.
    private static string Baslik(string s) => (s ?? "").Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    // CEF uzantı alanında "\" ve "=" kaçırılır, satır sonları tek satıra indirilir.
    private static string Uzanti(string s) => (s ?? "").Replace("\\", "\\\\").Replace("=", "\\=").Replace("\r", " ").Replace("\n", " ");

    // Bir bulguyu tek satır CEF olayına çevirir.
    public static string CefSatir(Bulgu b, string hedef, DateTime zaman)
    {
        // Olay numarası: modül adından kararlı bir kısa kod.
        string sig = "BS-" + new string((b.Modul ?? "x").Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S').Replace('ğ', 'g').Replace('Ğ', 'G').Replace('ü', 'u').Replace('Ü', 'U').Replace('ö', 'o').Replace('Ö', 'O').Replace('ç', 'c').Replace('Ç', 'C').Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        // Zamanı milisaniye (epoch) olarak yaz; CEF'te "rt" alanı budur.
        long ms = new DateTimeOffset(zaman).ToUnixTimeMilliseconds();
        // MITRE etiketi.
        string mitre = Mitre.Yazi(b);
        // CEF: sürüm|üretici|ürün|sürüm|olayNo|ad|önem|uzantılar
        return $"CEF:0|BulutSoft|Security Suite|1.0|{Baslik(sig)}|{Baslik(b.Baslik)}|{CefOnem(b.Onem)}|rt={ms} dst={Uzanti(hedef)} cs1Label=modul cs1={Uzanti(b.Modul ?? "")} cs2Label=mitre cs2={Uzanti(mitre)} msg={Uzanti(b.Detay)} cs3Label=oneri cs3={Uzanti(b.Oneri)} cs4Label=uyumluluk cs4={Uzanti((Uyumluluk.Bul(b) is { } u ? u.KvkkKisa + "; ISO " + string.Join(", ", u.IsoKodlari) : ""))}";
    }

    // Tüm bulguları CEF dosyası olarak yazar (sorun olmayan "Bilgi" sonuçları da içerir; atlananlar hariç).
    public static int CefYaz(string yol, IEnumerable<Bulgu> bulgular, string hedef, DateTime zaman)
    {
        // Atlananları çıkar.
        var l = bulgular.Where(b => !b.Atlandi).ToList();
        // Dosyaya yaz (UTF-8, BOM'suz).
        File.WriteAllLines(yol, l.Select(b => CefSatir(b, hedef, zaman)), new UTF8Encoding(false));
        // Kaç olay yazıldı.
        return l.Count;
    }

    // Tüm bulguları JSON Lines olarak yazar (her satır bağımsız bir olay).
    public static int JsonYaz(string yol, IEnumerable<Bulgu> bulgular, string hedef, DateTime zaman)
    {
        // Atlananları çıkar.
        var l = bulgular.Where(b => !b.Atlandi).ToList();
        // Her bulgu için bir JSON satırı.
        var satirlar = l.Select(b =>
        {
            // Etiket (olabilir ya da olmayabilir).
            var e = Mitre.Bul(b);
            // Düz, SIEM dostu bir nesne.
            return JsonSerializer.Serialize(new
            {
                timestamp = zaman.ToString("o"),
                product = "BulutSoft Security Suite",
                target = hedef,
                module = b.Modul,
                title = b.Baslik,
                severity = b.Onem.ToString(),
                severity_score = CefOnem(b.Onem),
                detail = b.Detay,
                recommendation = b.Oneri,
                mitre_id = e?.Kimlik,
                mitre_name = e?.Ad,
                mitre_tactic = e?.Taktik,
                kvkk = Uyumluluk.Bul(b)?.KvkkKisa,
                iso27001 = Uyumluluk.Bul(b)?.IsoKodlari
            });
        });
        // Dosyaya yaz.
        File.WriteAllLines(yol, satirlar, new UTF8Encoding(false));
        // Kaç olay yazıldı.
        return l.Count;
    }
}
