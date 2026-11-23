// Bu dosya ortak JSON serileştirme ayarlarını tutar: her serileştirmede yeni bir JsonSerializerOptions üretmek (CA1869) önbelleği boşa harcar;
// bu nesneler bir kez üretilip yeniden kullanılır (kullanıldıktan sonra değiştirilemez olurlar, bu yüzden paylaşmak güvenlidir).
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Ortak JSON ayarları.
internal static class JsonAyarlari
{
    // Okunaklı (girintili) çıktı: kullanıcı dosyaları, mühürlü kayıtlar.
    public static readonly JsonSerializerOptions Girintili = new() { WriteIndented = true };
    // Sıkı (tek satır) çıktı: imza hesaplanan veri.
    public static readonly JsonSerializerOptions Girintisiz = new() { WriteIndented = false };
    // Türkçe karakterleri \uXXXX'e çevirmeyen çıktı (Teams/Discord/Telegram bildirim gövdesi, okunaklı olsun diye).
    public static readonly JsonSerializerOptions GevsekKacis = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
