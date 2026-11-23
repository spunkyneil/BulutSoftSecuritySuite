// Bu dosya Kolay Görünümün alt durum şeridindeki İLK göstergenin (kurumsal KOBİ için sadeleştirilmiş) içeriğini belirler (saf mantık; birim testlenir):
//  • Kullanıcı gerçekten bir uzak sensör (Kali/VM) yapılandırdıysa: "Kali Sensör" kartı (gerçek sensör durumu).
//  • Yapılandırmadıysa: "Son Tarama Durumu" kartı (son taramanın tarihi + puan özeti) — jargonsuz, herkesin anlayacağı bilgi.
// Renk kodları KolayModPaneli.Gosterge ile aynı: 0 yeşil, 1 sarı, 2 kırmızı, 3 nötr.
using System.Globalization;

// Programımızın adı.
namespace LabDenetim;

// Kolay Görünüm gösterge içerikleri.
internal static class KolayGostergeleri
{
    // Birinci göstergenin başlığı: sensör varsa "Kali Sensör", yoksa "Son Tarama Durumu".
    public const string SensorBasligi = "Kali Sensör";
    public const string SonTaramaBasligi = "Son Tarama Durumu";

    // Birinci gösterge: sensör yapılandırılmışsa sensör durumu, aksi halde son tarama özeti.
    public static (string Baslik, string Yazi, int Renk, bool SonTarama) Birinci(bool sensorYapilandirilmis, (string Yazi, int Renk) sensorDurumu, IEnumerable<GecmisKayit>? gecmis)
    {
        if (sensorYapilandirilmis) return (SensorBasligi, sensorDurumu.Yazi, sensorDurumu.Renk, false);
        var son = SonTarama(gecmis); return (SonTaramaBasligi, son.Yazi, son.Renk, true);
    }

    // Son taramanın (en yeni kayıt) tarih + puan özeti ve rengi.
    public static (string Yazi, int Renk) SonTarama(IEnumerable<GecmisKayit>? gecmis)
    {
        var k = gecmis?.OrderByDescending(x => x.Tarih).FirstOrDefault();
        if (k == null) return ("Henüz tarama yapılmadı", 3);
        string tarih = k.Tarih.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        int renk = k.Kirmizi > 0 ? 2 : k.Sari > 0 ? 1 : 0;
        return ($"{tarih} · puan {k.Puan}/100", renk);
    }
}
