// Paket 5: Kali sensörü İSTEĞE BAĞLI (Uzman Paketi). Sensör yokken ürün hata/eksik göstermez, jargonsuz "kullanılmıyor" der ve ağa hiç çıkmaz.
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class UzmanPaketiTestleri : IDisposable
{
    private readonly Func<List<SensorProfili>> _eskiSensorler = KaliDurumYoneticisi.Sensorler; private readonly Func<SensorProfili, CancellationToken, Task<SensorSonuc>> _eskiSina = KaliDurumYoneticisi.Sina;
    public void Dispose() { KaliDurumYoneticisi.Sensorler = _eskiSensorler; KaliDurumYoneticisi.Sina = _eskiSina; }
    static SensorProfili P(string ad = "kali1") => new(ad, "192.168.56.20", 22, "u", "k");

    [Fact]
    public void Etkin_Yalnizca_Sensor_Varsa()
    {
        Assert.False(UzmanPaketi.Etkin(null)); Assert.False(UzmanPaketi.Etkin(new List<SensorProfili>())); Assert.True(UzmanPaketi.Etkin(new[] { P() }));
    }

    [Fact]
    public void Kolay_Gosterge_Sensor_Yokken_Notr_Ve_Jargonsuz_Sensor_Varken_Gercek_Durum()
    {
        var pasif = UzmanPaketi.KolayGosterge(false, ("Aktif", 0)); Assert.Equal(("Uzman Sensör (İsteğe Bağlı)", "Kullanılmıyor", 3), pasif); Assert.DoesNotContain("Kali", pasif.Baslik + pasif.Yazi);
        Assert.Equal(("Kali Durumu", "Kapalı / Bağlantı Bekleniyor", 2), UzmanPaketi.KolayGosterge(true, ("Kapalı / Bağlantı Bekleniyor", 2)));
    }

    [Fact]
    public async Task Sensor_Yokken_Kontrol_Aga_Cikmaz_Hata_Vermez_Ve_Uzman_Gorunumune_Yonlendirir()
    {
        int sorgu = 0; KaliDurumYoneticisi.Sensorler = () => new(); KaliDurumYoneticisi.Sina = (_, _) => { sorgu++; return Task.FromResult(new SensorSonuc(true, "", "", true, "")); };
        var r = await KaliDurumYoneticisi.KontrolEtAsync();
        Assert.Equal(KaliDurumu.SensorYok, r.Durum); Assert.Equal(0, sorgu);   // SSH denemesi yok
        Assert.Equal(("Kullanılmıyor", 3), KaliDurumYoneticisi.Gosterge(r));
        Assert.Contains("Uzman Görünümü", r.Ayrinti); Assert.Contains("isteğe bağlı", r.Ayrinti); Assert.DoesNotContain("Sol menüden", r.Ayrinti);   // Kolay görünümde gizli menüye yönlendirmez
        Assert.DoesNotContain("hata", r.Ayrinti, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("sorun", r.Ayrinti, StringComparison.OrdinalIgnoreCase);
    }
}
