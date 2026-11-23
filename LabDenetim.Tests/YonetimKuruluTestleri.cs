// Bu dosya Yönetim Kurulu Risk Özeti raporunun testlerini içerir.
namespace LabDenetim.Tests;

// Rapor testleri.
public class YonetimKuruluTestleri
{
    // Bulgu üretir.
    private static Bulgu B(string m, string t, Onem o, string oneri = "Öneri") => new(m, t, o, "d", oneri);

    // Başlıklar ve içerik bulunur.
    [Fact] public void Html_BolumleriVar()
    {
        var gecmis = Enumerable.Range(0, 4).Select(i => new GecmisKayit("i" + i, DateTime.Now.AddDays(-i), "10.0.0.5", true, "S", 1, 1, 50 + i * 5, 1, 0, 0, 0, new List<Bulgu>())).ToList();
        string h = YonetimKuruluRaporu.Html("10.0.0.5", DateTime.Now, 65, new List<Bulgu> { B("Port", "Açık kapı: 3389 (RDP)", Onem.Yuksek), B("TLS", "Sertifika", Onem.Orta) }, gecmis);
        Assert.Contains("Yönetim Kurulu Siber Risk Özeti", h); Assert.Contains("30 / 60 / 90 Günlük Eylem Planı", h); Assert.Contains("KVKK m.12/1", h); Assert.Contains("İş etkisi", h); Assert.Contains("<svg", h); Assert.Contains("1 kritik riskin 30 gün", h);
    }
    // SVG sayıları nokta ayırıcılı (kültürden bağımsız).
    [Fact] public void Html_SvgNoktaAyiraci()
    {
        var gecmis = Enumerable.Range(0, 3).Select(i => new GecmisKayit("i" + i, DateTime.Now.AddDays(-i), "h", true, "S", 1, 1, 40 + i * 7, 0, 0, 0, 0, new List<Bulgu>())).ToList();
        var eski = System.Globalization.CultureInfo.CurrentCulture; System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
        try { string h = YonetimKuruluRaporu.Html("h", DateTime.Now, 50, new List<Bulgu>(), gecmis); var pts = System.Text.RegularExpressions.Regex.Match(h, "points='([^']*)'").Groups[1].Value; Assert.DoesNotContain(",,", pts); Assert.All(pts.Split(' '), n => Assert.Equal(2, n.Split(',').Length)); }
        finally { System.Globalization.CultureInfo.CurrentCulture = eski; }
    }
    // HTML enjeksiyonu engellenir.
    [Fact] public void Html_Kodlama() { string h = YonetimKuruluRaporu.Html("<script>x</script>", DateTime.Now, 10, new List<Bulgu> { B("Port", "<img src=x onerror=alert(1)>", Onem.Yuksek, "<b>öneri</b>") }, new List<GecmisKayit>()); Assert.DoesNotContain("<script>x", h); Assert.DoesNotContain("<img src=x", h); Assert.Contains("&lt;img", h); }
    // Bulgu yokken de çökmez.
    [Fact] public void Html_BosBulgu() => Assert.Contains("Düzeltilmesi gereken bir risk görünmüyor", YonetimKuruluRaporu.Html("h", DateTime.Now, 100, new List<Bulgu>(), new List<GecmisKayit>()));
    // İş etkisi her modül için dolu.
    [Fact] public void Etki_Dolu() { foreach (var m in new[] { "Port", "HTTP", "TLS", "Girdi", "DNS", "Hosts", "Kalıcılık", "Karne", "Wi-Fi", "Hesap", "Yapılandırma", "Zincir", "Referans", "?" }) Assert.False(string.IsNullOrWhiteSpace(YonetimKuruluRaporu.Etki(m))); }
}
