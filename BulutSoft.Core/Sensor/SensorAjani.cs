// Sensör ajanı: olay kaydı, sürüm ve gömülü Python kaynağı.
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

internal sealed record AjanOlayi(string T, int Kapi, string Baslik, int Biten, int Toplam, int Acik, double Sure, string Mesaj, string Sunucu, string Python, string Sistem, string Surum, string Ip);

// Sensör ajanı: sensöre ssh ile gönderilen, yalnızca standart kütüphaneyi kullanan küçük Python programı. Kaynak, derlemeye GÖMÜLÜ "ajan.py" dosyasıdır (Sensor\ajan.py).
internal static class SensorAjani
{
    // Ajan sürümü (hello cevabında gelir).
    public const string Surum = "6";

    // Ajanın kaynağı. Sensörde DİSKE YAZILMAZ: ssh üzerinden "python3 -" ile standart girişten çalıştırılır; bağlantı bitince iz kalmaz.
    // Ajan da kendi başına özel ağ kontrolü yapar (savunma katmanı): internet adreslerini taramayı reddeder.
    public static string Kaynak { get; } = Yukle();

    private static string Yukle()
    {
        using var s = typeof(SensorAjani).Assembly.GetManifestResourceStream("ajan.py") ?? throw new InvalidOperationException("Gömülü sensör ajanı (ajan.py) bulunamadı.");
        using var r = new StreamReader(s, new UTF8Encoding(false)); return r.ReadToEnd().Replace("\r\n", "\n");
    }
}
