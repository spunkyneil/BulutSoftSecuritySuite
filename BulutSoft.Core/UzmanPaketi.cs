// Bu dosya ürünün iki paketini tanımlar: ÇEKİRDEK (her müşteride, Kali/sensör gerektirmez) ve UZMAN / MSP PAKETİ (isteğe bağlı Kali sensörü).
// Sensör tanımlı değilse hiçbir ekran "eksik" ya da "hata" gibi görünmez; özellik "kullanılmıyor" olarak, jargonsuz gösterilir.
namespace LabDenetim;

// Paket mantığı (saf ve test edilebilir).
internal static class UzmanPaketi
{
    // Paketin müşteriye görünen adı.
    public const string Ad = "Uzman Paketi";
    // Kolay panelde sensör yokken gösterge başlığı ve yazısı.
    public const string PasifBaslik = "Uzman Sensör (İsteğe Bağlı)", PasifYazi = "Kullanılmıyor";
    // Sensör tanımlı değilken açıklama (Kolay görünümde gizli olan menüye değil, Uzman Görünümüne yönlendirir).
    public const string PasifAciklama = "Uzman Paketi (isteğe bağlı) kullanılmıyor. Ürünün temel özellikleri Kali sensörü olmadan eksiksiz çalışır. Ağır taramaları ayrı bir sanal makineye taşımak isterseniz Uzman Görünümü'ne geçip \"Uzak Sensörler\" bölümünden bir sensör ekleyebilirsiniz.";

    // Uzman Paketi etkin mi? (en az bir sensör kayıtlı)
    public static bool Etkin(IEnumerable<SensorProfili>? sensorler) => sensorler != null && sensorler.Any();

    // Kolay panelin ilk göstergesi: sensör yoksa nötr (gri) ve jargonsuz; varsa gerçek Kali durumu.
    public static (string Baslik, string Yazi, int Renk) KolayGosterge(bool etkin, (string Yazi, int Renk) kali)
        => etkin ? ("Kali Durumu", kali.Yazi, kali.Renk) : (PasifBaslik, PasifYazi, 3);
}
