using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Sensörden gelen çıktının AYRIŞTIRICISI: ajan JSON satırları, ön kontrol metni ve ssh hata metni. Sensörden gelen veri GÜVENİLMEZDİR: uzunluk/tür kontrolü yapılır, denetim karakterleri temizlenir.
internal static class SensorCiktiAyristirici
{
    // Bilinen olay türleri (başka türler yok sayılır).
    private static readonly HashSet<string> Turler = new() { "hello", "start", "open", "prog", "done", "error" };

    // Bir çıktı satırını çözer; JSON değilse veya tanınmayan türdeyse null verir (sensörden gelen veri GÜVENİLMEZ kabul edilir).
    public static AjanOlayi? Ayristir(string satir)
    {
        // Çok uzun veya boş satırı alma.
        if (string.IsNullOrWhiteSpace(satir) || satir.Length > 4096 || satir[0] != '{') return null;
        // Ayrıştırma bozulabilir.
        try
        {
            // JSON'u aç.
            using var d = JsonDocument.Parse(satir); var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object) return null;
            // Yardımcılar: alan okuma (tip uyuşmazsa varsayılan).
            string S(string a, int en = 200) => k.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.String ? Temizle(v.GetString() ?? "", en) : "";
            int I(string a) => k.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;
            double D(string a) => k.TryGetProperty(a, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double n) ? n : 0;
            // Tür kontrolü.
            string t = S("t", 20); if (!Turler.Contains(t)) return null;
            // Sonuç.
            return new AjanOlayi(t, I("p"), S("b", 80), I("d"), I("n"), I("open"), D("s"), S("m"), S("host"), S("py", 30), S("os"), S("v", 10), S("ip", 45));
        }
        // Bozuk JSON.
        catch { return null; }
    }

    // Sensörden gelen metindeki denetim karakterlerini temizler ve kısaltır (ekranda/günlükte sahte satır oluşturulmasın).
    public static string Temizle(string s, int en)
    {
        // Denetim karakterlerini boşluğa çevir ve kırp.
        var t = new string(s.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        // En fazla "en" karakter.
        return t.Length > en ? t[..en] : t;
    }

    // Ön kontrol çıktısını çözer.
    public static SensorSonuc OnKontrolCoz(IEnumerable<string> satirlar)
    {
        // Satırları temizle.
        var l = satirlar.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        // İşaret var mı?
        int i = l.IndexOf("BS-SENSOR-OK"); if (i < 0) return new(false, "", "", false, "Sensör beklenen cevabı vermedi.");
        // Sistem ve kullanıcı.
        string sis = i + 1 < l.Count ? Temizle(l[i + 1], 60) : "", kul = i + 2 < l.Count ? Temizle(l[i + 2], 32) : ""; bool py = l.Contains("PY-OK");
        // Özet.
        return new(true, sis, kul, py, $"Bağlantı başarılı: {sis}, kullanıcı: {kul}, Python3: {(py ? "var" : "YOK (ajan çalışamaz; sensöre python3 kurun)")}");
    }

    // ssh hata metnini sade Türkçeye çevirir.
    public static string HataAciklama(string h)
    {
        // Boşsa.
        if (string.IsNullOrWhiteSpace(h)) return "bilinmeyen sorun.";
        // Bilinen durumlar.
        if (h.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", StringComparison.OrdinalIgnoreCase) || h.Contains("Host key verification failed", StringComparison.OrdinalIgnoreCase)) return "SUNUCU PARMAK İZİ DEĞİŞMİŞ! Sensör yeniden kurulmadıysa bu bir ortadaki-adam saldırısı olabilir. Yeniden kurduysanız sensörü silip yeniden ekleyin. (" + h.Trim() + ")";
        if (h.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)) return "Anahtar reddedildi. Ortak anahtarı sensöre eklediniz mi? (\"Ortak anahtarı göster\" düğmesine bakın.)";
        if (h.Contains("refused", StringComparison.OrdinalIgnoreCase)) return "Bağlantı reddedildi. Sensörde SSH hizmeti açık mı (sudo systemctl start ssh) ve kapı numarası doğru mu?";
        if (h.Contains("timed out", StringComparison.OrdinalIgnoreCase) || h.Contains("No route", StringComparison.OrdinalIgnoreCase)) return "Sensöre ulaşılamadı. Sanal makine açık mı, adres doğru mu, VirtualBox ağ ayarı (Host-Only/Bridged) uygun mu?";
        if (h.Contains("UNPROTECTED PRIVATE KEY", StringComparison.OrdinalIgnoreCase)) return "Anahtar dosyasının izinleri çok geniş. Yalnızca sizin hesabınız okuyabilmeli.";
        // Bilinmeyen.
        return h.Trim().Replace("\r", " ").Replace("\n", " ");
    }
}
