// IP engel yaşı kaydı ve altyapı koruması testleri. Gerçek veri klasörüne ve güvenlik duvarına dokunmaz.
using System.Net.NetworkInformation;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class EngelKaydiTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_engel_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    public EngelKaydiTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; }
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "EngelKaydiTestleri.temizlik"); } }
    private static readonly DateTime Z = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Ekle_Okur_Ve_Ilk_Zamani_Korur_Cikar_Siler()
    {
        EngelKaydi.Ekle(new[] { "203.0.113.5", "gecersiz" }, Z); EngelKaydi.Ekle(new[] { "203.0.113.5" }, Z.AddDays(5));
        var k = EngelKaydi.Oku(); Assert.Single(k); Assert.Equal(Z, k["203.0.113.5"]);
        EngelKaydi.Cikar(new[] { "203.0.113.5" }); Assert.Empty(EngelKaydi.Oku());
    }

    [Fact]
    public void Yas_Ve_Metin()
    {
        var k = new Dictionary<string, DateTime> { ["203.0.113.5"] = Z };
        Assert.Equal(TimeSpan.FromDays(3), EngelKaydi.Yas("203.0.113.5", k, Z.AddDays(3)));
        Assert.Null(EngelKaydi.Yas("198.51.100.1", k, Z)); Assert.Equal(TimeSpan.Zero, EngelKaydi.Yas("203.0.113.5", k, Z.AddDays(-1)));
        Assert.Equal("Engelli", EngelKaydi.Metin(null)); Assert.Equal("Engelli · bugün", EngelKaydi.Metin(TimeSpan.FromHours(5))); Assert.Equal("Engelli · 3 gün", EngelKaydi.Metin(TimeSpan.FromDays(3.5)));
    }

    [Fact]
    public void Eskiler_Yalnizca_Omru_Dolan_Ve_Kurali_Hala_Var_Olanlar()
    {
        var k = new Dictionary<string, DateTime> { ["203.0.113.5"] = Z.AddDays(-40), ["203.0.113.6"] = Z.AddDays(-5), ["203.0.113.7"] = Z.AddDays(-90) };
        var kurallar = new[] { BruteForce.KuralAdi("203.0.113.5"), BruteForce.KuralAdi("203.0.113.6"), "BulutSoft-BF-baska" };   // .7'nin kuralı yok (elle silinmiş)
        Assert.Equal(new[] { "203.0.113.5" }, EngelKaydi.Eskiler(k, kurallar, Z));
        Assert.Empty(EngelKaydi.Eskiler(new Dictionary<string, DateTime>(), kurallar, Z));   // kaydı olmayan engele dokunulmaz
    }

    [Fact]
    public void Bozuk_Kayit_Dosyasi_Programi_Bozmaz()
    {
        File.WriteAllText(EngelKaydi.Yol, "{yarim"); Assert.Empty(EngelKaydi.Oku());
        EngelKaydi.Ekle(new[] { "203.0.113.5" }, Z); Assert.Single(EngelKaydi.Oku());
    }

    [Theory]
    [InlineData("224.0.0.1")] [InlineData("239.255.255.250")] [InlineData("255.255.255.255")] [InlineData("0.1.2.3")] [InlineData("ff02::1")] [InlineData("169.254.10.10")] [InlineData("fe80::1")] [InlineData("127.0.0.1")] [InlineData("::1")] [InlineData("0.0.0.0")]
    public void Yayin_Yerel_Ve_Gecersiz_Adresler_Hic_Engellenemez(string ip) => Assert.NotNull(BruteForce.EngelHatasi(ip, new HashSet<string>()));

    [Fact]
    public void Kendi_Alt_Agdaki_Cihaz_Engellenebilir_Ama_Ozel_Ag_Uyarisi_Verilir_Korunan_Liste_Engellenmez()
    {
        var korunan = new HashSet<string> { "192.168.1.1", "192.168.1.10" };
        Assert.Null(BruteForce.EngelHatasi("192.168.1.77", korunan)); Assert.True(BruteForce.OzelAg("192.168.1.77"));   // onay penceresi ek uyarı gösterir
        Assert.NotNull(BruteForce.EngelHatasi("192.168.1.1", korunan)); Assert.NotNull(BruteForce.EngelHatasi("192.168.1.10", korunan));
        Assert.Null(BruteForce.EngelHatasi("203.0.113.9", korunan)); Assert.False(BruteForce.OzelAg("203.0.113.9"));
    }

    [Fact]
    public void Ag_Gecidi_Ve_Dns_Korunur_Engellenemez()
    {
        var korunan = BruteForce.YerelAdresler(); int kontrol = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            var p = ni.GetIPProperties();
            foreach (var g in p.GatewayAddresses) { if (!BruteForce.IpGecerli(g.Address.ToString())) continue; Assert.Contains(g.Address.ToString(), korunan); Assert.NotNull(BruteForce.EngelHatasi(g.Address.ToString(), korunan)); kontrol++; }
            foreach (var d in p.DnsAddresses) { if (!BruteForce.IpGecerli(d.ToString())) continue; Assert.Contains(d.ToString(), korunan); kontrol++; }
        }
        Assert.True(kontrol >= 0);   // makinede ağ geçidi yoksa döngü boş kalır; varsa hepsi korunur
    }
}
