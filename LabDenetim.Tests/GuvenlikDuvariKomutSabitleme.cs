// Güvenlik duvarı komut biçimlerini SABİTLER: Sandbox'ta gerçek netsh ile denenen komutlarla (test_paketi2\KurulumTesti2.ps1) kodun ürettiği komutlar aynı olmalı.
// Kod komutu değiştirirse bu test başarısız olur ve Sandbox betiğinin güncellenmesi gerekir.
using Xunit;

namespace LabDenetim.Tests;

public class GuvenlikDuvariKomutSabitleme
{
    public const string IpEngel = "netsh advfirewall firewall delete rule name=BulutSoft-BF-203-0-113-9 >nul 2>&1 & netsh advfirewall firewall add rule name=BulutSoft-BF-203-0-113-9 dir=in action=block remoteip=203.0.113.9 protocol=any";
    public const string PortEngel = "netsh advfirewall firewall delete rule name=BulutSoft-Test-65001 >nul 2>&1 & netsh advfirewall firewall add rule name=BulutSoft-Test-65001 dir=in action=block protocol=TCP localport=65001";
    public const string Sil = "netsh advfirewall firewall delete rule name=BulutSoft-BF-203-0-113-9 & netsh advfirewall firewall delete rule name=BulutSoft-Test-65001";

    [Fact] public void Ip_Engel_Komutu_Sabit() => Assert.Equal(IpEngel, BruteForce.EngelZinciri(new[] { "203.0.113.9" }, new HashSet<string>()));
    [Fact] public void Port_Engel_Komutu_Sabit() => Assert.Equal(PortEngel, GuvenlikDuvari.EkleZinciri(new[] { 65001 }, "BulutSoft-Test-"));
    [Fact] public void Silme_Komutu_Sabit() => Assert.Equal(Sil, GuvenlikDuvari.SilZinciri(new[] { "BulutSoft-BF-203-0-113-9", "BulutSoft-Test-65001" }));
}
