// Köşede beliren tek bir uyarı kutusu (USB takıldı, tuzak alarmı...). Yenisi gelince eskisi kapatılır. Eskiden Form1'deki _usbKutu alanıydı; USB ve tuzak eylemleri paylaşır.

// Programımızın adı.
namespace LabDenetim;

// Tek uyarı kutusu yöneticisi.
internal sealed class UyariKutusu
{
    private UsbBildirimFormu? _kutu;

    // Eski kutuyu kapatıp yenisini odağı çalmadan gösterir.
    public void Goster(string baslik, string satir1, string satir2, Color renk, int sureMs)
    {
        try { _kutu?.Close(); } catch (Exception hk) { HataKayit.Sessiz(hk, "UyariKutusu.Goster"); }
        _kutu = new UsbBildirimFormu(baslik, satir1, satir2, renk, sureMs); _kutu.Show();
    }
}
