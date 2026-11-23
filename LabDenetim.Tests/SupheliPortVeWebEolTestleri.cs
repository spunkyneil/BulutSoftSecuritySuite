// Modül 7 (trafikte şüpheli port/klasör vurgusu) ve Modül 3 (web sunucusu sürüm başlığından eski yazılım) testleri.
using LabDenetim.Moduller;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class SupheliPortVeWebEolTestleri
{
    [Theory]
    [InlineData(6667)] [InlineData(6697)] [InlineData(9050)] [InlineData(3333)] [InlineData(14444)] [InlineData(2323)] [InlineData(4444)] [InlineData(31337)]
    public void Supheli_Port_Orta_Ve_Aciklamali(int port)
    {
        var (s, n) = TrafikIzleyici.Degerlendir(port, true); Assert.Equal(Onem.Orta, s); Assert.Contains($"Şüpheli port ({port})", n);
    }

    [Fact]
    public void Eski_Davranis_Bozulmadi()
    {
        Assert.Equal(Onem.Bilgi, TrafikIzleyici.Degerlendir(443, true).Seviye); Assert.Equal(Onem.Dusuk, TrafikIzleyici.Degerlendir(80, true).Seviye); Assert.Equal(Onem.Orta, TrafikIzleyici.Degerlendir(3389, true).Seviye); Assert.Equal(Onem.Dusuk, TrafikIzleyici.Degerlendir(51234, true).Seviye);
    }

    [Fact]
    public void Gecici_Klasorden_Internete_Cikan_Program_En_Az_Orta()
    {
        var (s, n) = TrafikIzleyici.Degerlendir(443, true, @"C:\Users\Ali\AppData\Local\Temp\x7.exe"); Assert.Equal(Onem.Orta, s); Assert.Contains("geçici/indirilenler", n); Assert.Contains("HTTPS", n);
        // Yerel ağa çıkış ya da normal klasör yükseltmez.
        Assert.Equal(Onem.Bilgi, TrafikIzleyici.Degerlendir(443, false, @"C:\Users\Ali\Downloads\a.exe").Seviye); Assert.Equal(Onem.Bilgi, TrafikIzleyici.Degerlendir(443, true, @"C:\Program Files\X\x.exe").Seviye); Assert.Equal(Onem.Bilgi, TrafikIzleyici.Degerlendir(443, true, "").Seviye);
        // Zaten Orta olan seviye düşmez.
        Assert.Equal(Onem.Orta, TrafikIzleyici.Degerlendir(4444, true, @"C:\Users\Public\a.exe").Seviye);
    }

    [Fact]
    public void Yol_Kontrolu_Buyuk_Kucuk_Harfe_Duyarsiz() { Assert.True(TrafikIzleyici.SupheliYolMu(@"C:\USERS\ALI\DOWNLOADS\A.EXE")); Assert.False(TrafikIzleyici.SupheliYolMu(null!)); Assert.False(TrafikIzleyici.SupheliYolMu("")); }

    // ---- Modül 3 ----
    static HttpGozlem H(params (string, string)[] b) => new("https://ornek.com/", 200, b.ToDictionary(x => x.Item1, x => x.Item2, StringComparer.OrdinalIgnoreCase), new(), new(), "bilinmiyor");
    static readonly DateTime Simdi = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Server_Basligindan_Eski_Apache_Ve_PHP_Bulunur()
    {
        var l = DesteksizYazilim.WebBulgulari(H(("Server", "Apache/2.2.15 (CentOS)"), ("X-Powered-By", "PHP/5.3.3")), Simdi);
        Assert.Equal(2, l.Count); Assert.Contains(l, x => x.RuleId == "EOL-APACHE" && x.Onem == Onem.Yuksek && x.Kanit.StartsWith("server:")); Assert.Contains(l, x => x.RuleId == "EOL-PHP" && x.Kanit.StartsWith("x-powered-by:"));
        Assert.All(l, x => { Assert.Equal("Eski Yazılım", x.Modul); Assert.Contains("ServerTokens", x.Cozum); Assert.Contains("doğrulay", x.Cozum); Assert.NotNull(x.ToBulgu()); });
    }

    [Fact]
    public void Guncel_Yok_Veya_Surumsuz_Baslikta_Bulgu_Yok()
    {
        Assert.Empty(DesteksizYazilim.WebBulgulari(H(("Server", "nginx/1.25.3")), Simdi)); Assert.Empty(DesteksizYazilim.WebBulgulari(H(("Server", "Apache")), Simdi)); Assert.Empty(DesteksizYazilim.WebBulgulari(H(), Simdi)); Assert.Empty(DesteksizYazilim.WebBulgulari(null, Simdi));
        Assert.Empty(DesteksizYazilim.WebBulgulari(H(("Server", "   ")), Simdi));
    }

    [Fact]
    public void Eski_Nginx_Eski_Surum_Olarak_Orta_Ve_Ayni_Surum_Iki_Basliktaysa_Tek_Bulgu()
    {
        var l = DesteksizYazilim.WebBulgulari(H(("Server", "nginx/1.14.0")), Simdi); Assert.Single(l); Assert.Equal(Onem.Orta, l[0].Onem);
        Assert.Single(DesteksizYazilim.WebBulgulari(H(("Server", "Apache/2.2.15"), ("X-Powered-By", "Apache/2.2.15")), Simdi));
    }
}
