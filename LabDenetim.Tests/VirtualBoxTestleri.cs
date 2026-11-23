// Bu dosya VirtualBox ağ listesi düzeltmelerinin testlerini içerir (VBoxManage çıktısı ayrıştırma ve hayalet cihaz birleştirme).
using LabDenetim.Moduller;

namespace LabDenetim.Tests;

// VirtualBox testleri.
public class VirtualBoxTestleri
{
    // Gerçek VBoxManage çıktısına benzeyen örnekler (bu bilgisayardan alındı, anonimleştirilmedi: yalnızca makine adı/MAC).
    private const string Liste = "\"kali-linux-2026.2-virtualbox-amd64\" {e7aff680-294a-46b8-8744-57732aaaaad2}\r\n\"Test Makinesi\" {11111111-2222-3333-4444-555555555555}\r\n";
    private const string Bilgi = "name=\"kali\"\r\nVMState=\"poweroff\"\r\nmacaddress1=\"0800275A87BC\"\r\nnic1=\"nat\"\r\nhostonlyadapter2=\"VirtualBox Host-Only Ethernet Adapter\"\r\nmacaddress2=\"080027BEC67B\"\r\nnic2=\"hostonly\"\r\nnic3=\"none\"\r\n";

    // Makine listesi.
    [Fact] public void Liste_Cozulur() { var l = VirtualBoxYardimcisi.ListeCoz(Liste); Assert.Equal(2, l.Count); Assert.Equal("kali-linux-2026.2-virtualbox-amd64", l[0].Ad); Assert.Equal("e7aff680-294a-46b8-8744-57732aaaaad2", l[0].Uuid); Assert.Empty(VirtualBoxYardimcisi.ListeCoz("")); }
    // Durum ve MAC adresleri (kullanılmayan kart hariç; ARP biçiminde).
    [Fact] public void Bilgi_Cozulur() { var (d, m) = VirtualBoxYardimcisi.BilgiCoz(Bilgi); Assert.Equal("poweroff", d); Assert.Equal(new[] { "08-00-27-5A-87-BC", "08-00-27-BE-C6-7B" }, m); }
    // Konuk IP'leri.
    [Fact] public void Ip_Cozulur() { var l = VirtualBoxYardimcisi.IpCoz("Name: /VirtualBox/GuestInfo/Net/0/V4/IP, value: 10.0.2.15, timestamp: 1, flags:\r\nName: /VirtualBox/GuestInfo/Net/1/V4/IP, value: 192.168.56.101, timestamp: 2, flags:\r\n"); Assert.Equal(new[] { "10.0.2.15", "192.168.56.101" }, l); Assert.Empty(VirtualBoxYardimcisi.IpCoz("No value set!")); }

    // Hayalet senaryosu: Kali KAPALI, ağda yalnızca VirtualBox DHCP sunucusu (.100) ve ana makine görünüyor.
    [Fact] public void Birlestir_Hayalet_DhcpSanalMakineDegil()
    {
        var tarama = new List<AgCihazi> { new("192.168.56.1", "Bu bilgisayar (ana makine)", "pc", "—", "VirtualBox Host-Only"), new("192.168.56.100", "VirtualBox sanal makine", "—", "08-00-27-6B-3A-54", "VirtualBox Host-Only") };
        var kali = new VBoxVm("Kali", "e7aff680-294a-46b8-8744-57732aaaaad2", "poweroff", new() { "08-00-27-5A-87-BC", "08-00-27-BE-C6-7B" });
        var s = VirtualBoxYardimcisi.Birlestir(tarama, new() { kali });
        Assert.Equal(3, s.Count); Assert.Contains(s, x => x.Vm == kali && x.Cihaz.Ip == "—" && x.Cihaz.Tur.Contains("Kapalı")); Assert.Contains(s, x => x.Cihaz.Ip == "192.168.56.100" && x.Vm == null && x.Cihaz.Tur.Contains("sanal makine DEĞİL")); Assert.Contains(s, x => x.Cihaz.Ip == "192.168.56.1");
    }
    // Çalışan Kali MAC'iyle eşleşir ve adı gösterilir.
    [Fact] public void Birlestir_CalisanMakine()
    {
        var tarama = new List<AgCihazi> { new("192.168.56.101", "VirtualBox sanal makine", "—", "08-00-27-BE-C6-7B", "VirtualBox Host-Only") };
        var kali = new VBoxVm("Kali", "e7aff680-294a-46b8-8744-57732aaaaad2", "running", new() { "08-00-27-5A-87-BC", "08-00-27-BE-C6-7B" });
        var s = VirtualBoxYardimcisi.Birlestir(tarama, new() { kali }); Assert.Single(s); Assert.Equal("Kali", s[0].Cihaz.Ad); Assert.Contains("Çalışıyor", s[0].Cihaz.Tur); Assert.Equal("192.168.56.101", s[0].Cihaz.Ip);
    }
    // VirtualBox kurulu değilse (vmler null) liste aynen kalır.
    [Fact] public void Birlestir_VirtualBoxYok() { var t = new List<AgCihazi> { new("10.0.0.5", "Ağ cihazı", "x", "AA-BB-CC-DD-EE-FF", "Ethernet") }; var s = VirtualBoxYardimcisi.Birlestir(t, null); Assert.Single(s); Assert.Null(s[0].Vm); }
    // Cevap vermeyen adres canlı sayılmaz (eski ARP kaydı).
    [Fact] public async Task TcpYasiyor_CevapsizAdres() => Assert.False(await AgKesfi.TcpYasiyor("192.0.2.77", CancellationToken.None));
    // Dinleyen yerel kapı canlı sayılır; reddeden (kapalı) kapı da canlı sayılır.
    [Fact] public async Task TcpYasiyor_YerelKapi()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); l.Start(); try { Assert.True(await AgKesfi.TcpYasiyor("127.0.0.1", CancellationToken.None)); } finally { l.Stop(); }
    }
    // Güvenlik: geçersiz UUID ile makine başlatılmaz / IP sorgulanmaz.
    [Fact] public void Uuid_Gecersiz() { Assert.False(VirtualBoxYardimcisi.Baslat("x; calc")); Assert.Empty(VirtualBoxYardimcisi.IpBul("../../x")); }
}
