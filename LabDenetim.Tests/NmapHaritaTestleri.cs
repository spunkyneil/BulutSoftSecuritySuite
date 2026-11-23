// Hafta 3 testleri: nmap XML çözücü, harita birleştirme, dosya deposu, hedef doğrulama ve sensör cevabı çözümü.
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class NmapHaritaTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    // Örnek nmap çıktısı: iki cihaz (biri MAC'li web sunucusu, biri Windows bilgisayar) ve bir kapalı cihaz.
    const string Ornek = """
<?xml version="1.0"?>
<nmaprun scanner="nmap" args="nmap -sV -oX - -F 192.168.56.0/24" start="1795000000">
<host><status state="up"/><address addr="192.168.56.10" addrtype="ipv4"/><address addr="08:00:27:aa:bb:cc" addrtype="mac" vendor="PCS Systemtechnik"/>
<hostnames><hostname name="web01.lab" type="PTR"/></hostnames>
<ports><port protocol="tcp" portid="80"><state state="open"/><service name="http" product="Apache httpd" version="2.4.49" extrainfo="(Ubuntu)"><cpe>cpe:/a:apache:http_server:2.4.49</cpe></service></port>
<port protocol="tcp" portid="22"><state state="open"/><service name="ssh" product="OpenSSH" version="8.2p1"/></port>
<port protocol="tcp" portid="23"><state state="closed"/><service name="telnet"/></port></ports></host>
<host><status state="up"/><address addr="192.168.56.20" addrtype="ipv4"/>
<ports><port protocol="tcp" portid="445"><state state="open"/><service name="microsoft-ds"/></port><port protocol="tcp" portid="3389"><state state="open"/><service name="ms-wbt-server"/></port></ports></host>
<host><status state="down"/><address addr="192.168.56.30" addrtype="ipv4"/></host>
</nmaprun>
""";

    [Fact] public void Parse_CihazVeHizmetleriOkur()
    {
        var s = NmapXml.Parse(Ornek);
        Assert.Equal("192.168.56.0/24", s.Target); Assert.Equal(2, s.Hosts.Count);   // "down" cihaz atlanır
        var w = s.Hosts[0]; Assert.Equal("192.168.56.10", w.Address); Assert.Equal("08:00:27:AA:BB:CC", w.Mac); Assert.Equal("web01.lab", w.Hostname); Assert.Equal("PCS Systemtechnik", w.Vendor);
        var ap = w.Services.Single(x => x.Port == 80); Assert.Equal("Apache httpd", ap.Product); Assert.Equal("2.4.49", ap.Version); Assert.Equal("cpe:/a:apache:http_server:2.4.49", ap.Cpe[0]);
        Assert.Equal("closed", w.Services.Single(x => x.Port == 23).State);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1795000000).UtcDateTime, s.ScannedAtUtc);
    }

    // Kali'de gerçek nmap 7.99 çıktısının başı (DOCTYPE, stil sayfası, yorum ve kaçışlı tire içerir).
    [Fact] public void Parse_GercekNmapBasligi_DoctypeIleOkunur()
    {
        string x = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE nmaprun>\n<?xml-stylesheet href=\"file:///usr/share/nmap/nmap.xsl\" type=\"text/xsl\"?>\n<!-- Nmap 7.99 scan initiated -->\n<nmaprun scanner=\"nmap\" args=\"/usr/lib/nmap/nmap -&#45;privileged -sV -F -oX - 192.168.56.101\" start=\"1795031478\" version=\"7.99\" xmloutputversion=\"1.05\"><host><status state=\"up\"/><address addr=\"192.168.56.101\" addrtype=\"ipv4\"/><ports><port protocol=\"tcp\" portid=\"22\"><state state=\"open\"/><service name=\"ssh\" product=\"OpenSSH\"/></port></ports></host></nmaprun>";
        var s = NmapXml.Parse(x);
        Assert.Equal("192.168.56.101", s.Target); Assert.Equal(22, Assert.Single(Assert.Single(s.Hosts).Services).Port);
    }

    [Fact] public void Parse_XxeVeDtd_Reddedilir()
    {
        string kotu = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY a SYSTEM \"file:///C:/Windows/win.ini\">]><nmaprun args=\"&a;\"></nmaprun>";
        Assert.Throws<XmlException>(() => NmapXml.Parse(kotu));
    }

    [Fact] public void Parse_BozukVeYanlisKok_Hata()
    {
        Assert.Throws<XmlException>(() => NmapXml.Parse("<html></html>"));
        Assert.ThrowsAny<XmlException>(() => NmapXml.Parse("<nmaprun><host>"));
    }

    [Fact] public void Parse_GecersizDegerleriAtlar_VeSinirlar()
    {
        string x = "<nmaprun args=\"t\"><host><status state=\"up\"/><address addr=\"10.0.0.5\" addrtype=\"ipv4\"/><address addr=\"zz:zz\" addrtype=\"mac\"/><ports><port protocol=\"tcp\" portid=\"99999\"><state state=\"open\"/></port><port protocol=\"evil\" portid=\"80\"><state state=\"open\"/><service name=\"ht\u007ftp\"><cpe>javascript:1</cpe></service></port></ports></host><host><status state=\"up\"/><address addr=\"fe80::1\" addrtype=\"ipv6\"/></host></nmaprun>";
        var s = NmapXml.Parse(x);
        Assert.Single(s.Hosts); Assert.Null(s.Hosts[0].Mac);                       // bozuk MAC yok sayılır, IPv6-only cihaz atlanır
        var p = Assert.Single(s.Hosts[0].Services); Assert.Equal(80, p.Port); Assert.Equal("tcp", p.Proto); Assert.Equal("http", p.Service); Assert.Empty(p.Cpe);
    }

    [Fact] public void Ac_SikistirmaBombasi_Reddedilir()
    {
        var buyuk = new byte[NmapXml.EnCokXmlKarakter + 100_000]; Array.Fill(buyuk, (byte)'a');
        using var ms = new MemoryStream(); using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, true)) z.Write(buyuk);
        Assert.Throws<InvalidDataException>(() => NmapXml.Ac(Convert.ToBase64String(ms.ToArray())));
        Assert.Throws<InvalidDataException>(() => NmapXml.Ac(new string('A', 800_000)));
    }

    // Sensör cevabı gibi sıkıştırılmış JSON üretir.
    static JsonElement Cevap(string xml)
    {
        using var ms = new MemoryStream(); using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(Encoding.UTF8.GetBytes(xml));
        return JsonDocument.Parse(JsonSerializer.Serialize(new { t = "nmap", hedef = "192.168.56.0/24", profil = "hizli", xmlz = Convert.ToBase64String(ms.ToArray()) })).RootElement;
    }
    [Fact] public void SensorCevabi_Cozulur()
    {
        var s = SensorNmap.Coz(Cevap(Ornek), T0); Assert.Equal(2, s.Hosts.Count);
        Assert.Throws<InvalidOperationException>(() => SensorNmap.Coz(JsonDocument.Parse("{\"t\":\"nmap\"}").RootElement, T0));
        Assert.Throws<InvalidOperationException>(() => SensorNmap.Coz(JsonDocument.Parse("{\"xmlz\":\"!!!\"}").RootElement, T0));
        Assert.Throws<InvalidOperationException>(() => SensorNmap.Coz(Cevap("<x/>"), T0));
    }

    [Theory]
    [InlineData("192.168.56.0/24", true)] [InlineData("192.168.56.101", true)] [InlineData("10.0.0.0/25", true)] [InlineData("172.16.5.9/32", true)]
    [InlineData("8.8.8.8", false)] [InlineData("8.8.8.0/24", false)] [InlineData("192.168.0.0/16", false)] [InlineData("10.0.0.0/8", false)] [InlineData("192.168.1.1; ls", false)]
    [InlineData("", false)] [InlineData("127.0.0.1", false)] [InlineData("scanme.nmap.org", false)] [InlineData("::1", false)] [InlineData("192.168.1", false)] [InlineData("192.168.1.0/33", false)]
    public void Hedef_YalnizcaOzelAg_VeEnCok24(string hedef, bool gecerli) => Assert.Equal(gecerli, SensorNmap.HedefHatasi(hedef) == null);

    // ---- Harita birleştirme ----
    [Fact] public void Merge_YeniVarlik_KimlikRolAgirlikAltAg()
    {
        var m = NetworkMapBuilder.Merge(null, NmapXml.Parse(Ornek), "kali", T0);
        Assert.Equal(2, m.Assets.Count); Assert.Equal("192.168.56.0/24", Assert.Single(m.Subnets).Cidr);
        var w = m.Assets.Single(a => a.AssetId == "mac:08:00:27:AA:BB:CC"); Assert.Equal("server", w.Role); Assert.Equal(0.8, w.AssetWeight); Assert.Equal("nmap@kali", w.Provenance);
        var p = m.Assets.Single(a => a.AssetId == "ip:192.168.56.20"); Assert.Equal("workstation", p.Role); Assert.Equal(0.5, p.AssetWeight);
    }

    [Fact] public void Merge_TekrarTarama_AgirligiKorur_HizmetleriYeniler_EskiCihazlariSilmez()
    {
        var m1 = NetworkMapBuilder.Merge(null, NmapXml.Parse(Ornek), "kali", T0);
        m1 = NetworkMapBuilder.AgirlikAyarla(m1, "ip:192.168.56.20", 0.95);
        // İkinci tarama yalnızca web sunucusunu görür ve 22. kapı kapanmıştır.
        var ikinci = new NormalizedScan("x", T0.AddDays(1), new() { new NmapHost("192.168.56.10", "08:00:27:AA:BB:CC", null, null, null, new() { new ServiceEndpoint(80, "tcp", "open", "http", "Apache httpd", "2.4.58", null, Array.Empty<string>()) }) });
        var m2 = NetworkMapBuilder.Merge(m1, ikinci, "kali2", T0.AddDays(1));
        Assert.Equal(2, m2.Assets.Count); Assert.Equal(m1.MapId, m2.MapId);
        var w = m2.Assets.Single(a => a.AssetId == "mac:08:00:27:AA:BB:CC"); Assert.Single(w.Services); Assert.Equal("2.4.58", w.Services[0].Version); Assert.Equal("web01.lab", w.Hostname);   // ad eskisinden korunur
        Assert.Equal("nmap@kali,nmap@kali2", w.Provenance);
        Assert.Equal(0.95, m2.Assets.Single(a => a.AssetId == "ip:192.168.56.20").AssetWeight);   // taranmayan cihaz aynen kalır
        Assert.Equal(m1.Assets.Count, m1.Assets.Count);                                          // önceki harita değişmez
    }

    [Fact] public void Merge_IpKaydi_MacBulunursaYukselir_AgirlikKorunur()
    {
        var once = new NormalizedScan("x", T0, new() { new NmapHost("192.168.56.50", null, null, null, null, new()) });
        var m1 = NetworkMapBuilder.AgirlikAyarla(NetworkMapBuilder.Merge(null, once, "k", T0), "ip:192.168.56.50", 0.9);
        var sonra = new NormalizedScan("x", T0, new() { new NmapHost("192.168.56.50", "AA:BB:CC:DD:EE:FF", null, null, null, new()) });
        var m2 = NetworkMapBuilder.Merge(m1, sonra, "k", T0.AddHours(1));
        var a = Assert.Single(m2.Assets); Assert.Equal("mac:AA:BB:CC:DD:EE:FF", a.AssetId); Assert.Equal(0.9, a.AssetWeight);
    }

    [Fact] public void Merge_IpDegisirse_MacileEslesir_AdresListesiGuncellenir()
    {
        var m1 = NetworkMapBuilder.Merge(null, new NormalizedScan("x", T0, new() { new NmapHost("192.168.56.60", "AA:AA:AA:AA:AA:AA", null, null, null, new()) }), "k", T0);
        var m2 = NetworkMapBuilder.Merge(m1, new NormalizedScan("x", T0, new() { new NmapHost("192.168.56.61", "AA:AA:AA:AA:AA:AA", null, null, null, new()) }), "k", T0);
        var a = Assert.Single(m2.Assets); Assert.Equal(new[] { "192.168.56.61", "192.168.56.60" }, a.Addresses);
    }

    [Fact] public void AgirlikAyarla_Sinirlar()
    {
        var m = NetworkMapBuilder.Merge(null, NmapXml.Parse(Ornek), "k", T0); string id = m.Assets[0].AssetId;
        Assert.Equal(1.0, NetworkMapBuilder.AgirlikAyarla(m, id, 5).Assets[0].AssetWeight); Assert.Equal(0.0, NetworkMapBuilder.AgirlikAyarla(m, id, -2).Assets[0].AssetWeight); Assert.Equal(0.5, NetworkMapBuilder.AgirlikAyarla(m, id, double.NaN).Assets[0].AssetWeight);
    }

    [Fact] public void RolTahmin_Kurallari()
    {
        ServiceEndpoint P(int n) => new(n, "tcp", "open", "x", null, null, null, Array.Empty<string>());
        Assert.Equal("server", NetworkMapBuilder.RolTahmin(new[] { P(443) }, null)); Assert.Equal("server", NetworkMapBuilder.RolTahmin(Array.Empty<ServiceEndpoint>(), "Microsoft Windows Server 2022"));
        Assert.Equal("workstation", NetworkMapBuilder.RolTahmin(new[] { P(445) }, null)); Assert.Equal("network", NetworkMapBuilder.RolTahmin(Array.Empty<ServiceEndpoint>(), "MikroTik RouterOS")); Assert.Equal("unknown", NetworkMapBuilder.RolTahmin(Array.Empty<ServiceEndpoint>(), null));
    }

    // ---- Depo ----
    [Fact] public void Depo_YazOku_VeBozukDosyaNull()
    {
        string eski = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "harita_" + Guid.NewGuid().ToString("N"));
        BulutKlasor.Kok = gecici;
        try
        {
            Assert.Null(AssetStore.Load());
            var m = NetworkMapBuilder.Merge(null, NmapXml.Parse(Ornek), "kali", T0); AssetStore.Save(m);
            var g = AssetStore.Load()!; Assert.Equal(m.MapId, g.MapId); Assert.Equal(2, g.Assets.Count); Assert.Equal("Apache httpd", g.Assets.First(a => a.Services.Any(s => s.Port == 80)).Services.First(s => s.Port == 80).Product);
            File.WriteAllText(AssetStore.Yol, "{bozuk"); Assert.Null(AssetStore.Load());
        }
        finally { BulutKlasor.Kok = eski; try { Directory.Delete(gecici, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "NmapHaritaTestleri.temizlik"); } }
    }
}
