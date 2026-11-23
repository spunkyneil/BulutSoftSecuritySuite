// IP Sızıntı Testi: ham servis cevaplarının güvenli çözümü, tünel/VPN/WARP işareti, DNS ve IPv6 sızıntı değerlendirmesi, Kali karşılaştırması, veri toplama (SAHTE ağla: gerçek internet isteği yok).
using System.Text.Json;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class IpSizintiTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_ip_" + Guid.NewGuid().ToString("N")); private readonly string _eskiOlay = OlayDeposu.Yol, _eski = BulutKlasor.Kok;
    public IpSizintiTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); IpSizintiTesti.Sifirla(); }
    public void Dispose() { OlayDeposu.Yol = _eskiOlay; BulutKlasor.Kok = _eski; IpSizintiTesti.Sifirla(); try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "IpSizintiTestleri.temizlik"); } }
    static readonly DateTime T0 = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Local);

    static string Ipify(string ip) => "{\"ip\":\"" + ip + "\"}";
    static string Whois(string ulke, string org, string sehir = "Istanbul") => "{\"success\":true,\"country\":\"" + ulke + "\",\"city\":\"" + sehir + "\",\"connection\":{\"asn\":1,\"org\":\"" + org + "\",\"isp\":\"x\"}}";
    static IpSizintiSonucu Win(string ip = "203.0.113.5", string org = "Turk Telekom", string cozucu = "203.0.113.53", string cozOrg = "Turk Telekom", string[]? arayuz = null, bool proxy = false, string? trace = "ip=203.0.113.5\nwarp=off\n", string? ip6 = null)
        => IpSizintiTesti.Coz("Windows", Ipify(ip), ip6 == null ? null : Ipify(ip6), trace, cozucu, Whois("TR", org), Whois("TR", cozOrg), new[] { "192.168.1.1" }, arayuz ?? new[] { "Ethernet Intel(R) Ethernet" }, proxy, T0);

    // ---- Güvenli çözümleme ----
    [Theory] [InlineData("{\"ip\":\"203.0.113.5\"}", "203.0.113.5")] [InlineData("{\"ip\":\"2001:db8::1\"}", "2001:db8::1")] [InlineData("{\"ip\":\"999.1.1.1\"}", "")] [InlineData("{\"ip\":\"<script>\"}", "")] [InlineData("{bozuk", "")] [InlineData("[]", "")] [InlineData("", "")] [InlineData(null, "")] [InlineData("{\"ip\":123}", "")]
    public void IpifyCoz_GuvenilmezVeri(string? json, string beklenen) => Assert.Equal(beklenen, IpSizintiTesti.IpifyCoz(json));
    [Fact] public void IpifyCoz_AsiriUzunCevap_Reddedilir() => Assert.Equal("", IpSizintiTesti.IpifyCoz("{\"ip\":\"1.2.3.4\",\"x\":\"" + new string('a', 5000) + "\"}"));
    [Fact] public void WhoisCoz_AlanlarTemizlenir()
    {
        var w = IpSizintiTesti.WhoisCoz("1.2.3.4", "{\"country\":\"Tür\\u0001kiye\",\"city\":\"Ankara\",\"connection\":{\"org\":\"" + new string('x', 500) + "\"}}")!; Assert.Equal("Türkiye", w.Ulke); Assert.Equal(80, w.Servis.Length);
        Assert.Null(IpSizintiTesti.WhoisCoz("1.2.3.4", "{bozuk")); Assert.Null(IpSizintiTesti.WhoisCoz("", Whois("TR", "x")));
        Assert.Equal("Org1", IpSizintiTesti.WhoisCoz("1.2.3.4", "{\"country\":\"TR\",\"connection\":{\"isp\":\"Org1\"}}")!.Servis);   // org yoksa isp
    }
    [Fact] public void TraceCoz_Alanlar() { var d = IpSizintiTesti.TraceCoz("fl=1\nip=1.2.3.4\nwarp=plus\nloc=TR\nbozuk satir\n"); Assert.Equal("plus", d["warp"]); Assert.Equal("1.2.3.4", d["ip"]); Assert.Empty(IpSizintiTesti.TraceCoz(new string('a', 5000))); }

    // ---- Değerlendirme ----
    [Fact] public void TunelsuzNormalDurum_Temiz_VeAciklayiciNot()
    {
        var d = IpSizintiTesti.Degerlendir(Win()); Assert.Equal(SizintiDurumu.Temiz, d.Durum); Assert.Contains(d.Maddeler, x => x.Contains("203.0.113.5") && x.Contains("TR")); Assert.Contains(d.Maddeler, x => x.Contains("sızıntı değildir")); Assert.Equal(("Temiz", 0), IpSizintiTesti.Gosterge(d));
    }
    [Fact] public void Vpn_DnsBaskaServistenGorunuyor_Sizinti()
    {
        var d = IpSizintiTesti.Degerlendir(Win(org: "Mullvad VPN AB", cozOrg: "Turk Telekom", arayuz: new[] { "Mullvad Tunnel wireguard" })); Assert.Equal(SizintiDurumu.Sizinti, d.Durum); Assert.Contains(d.Maddeler, x => x.Contains("OLASI DNS SIZINTISI")); Assert.Equal(("Olası sızıntı!", 2), IpSizintiTesti.Gosterge(d));
    }
    [Fact] public void Vpn_DnsAyniServisten_Temiz()
    {
        Assert.Equal(SizintiDurumu.Temiz, IpSizintiTesti.Degerlendir(Win(org: "AS39351 Mullvad VPN AB", cozOrg: "Mullvad VPN", arayuz: new[] { "wg0 wireguard" })).Durum);
        Assert.Equal(SizintiDurumu.Temiz, IpSizintiTesti.Degerlendir(Win(org: "Cloudflare, Inc.", cozOrg: "AS13335 Cloudflare", trace: "warp=on\n")).Durum);   // WARP, aynı servis sağlayıcı
    }
    [Fact] public void Warp_TraceIleAlgilanir()
    {
        var s = Win(trace: "warp=on\nip=203.0.113.5\n"); Assert.True(s.TunelArayuzu); Assert.Equal("Cloudflare WARP", s.TunelAdi); Assert.False(Win(trace: "warp=off\n").TunelArayuzu);
    }
    [Fact] public void Vpn_VeIpv6_Dikkat()
    {
        var d = IpSizintiTesti.Degerlendir(Win(org: "VPNco", cozOrg: "VPNco", arayuz: new[] { "tailscale0" }, ip6: "2001:db8::7")); Assert.Equal(SizintiDurumu.Dikkat, d.Durum); Assert.Contains(d.Maddeler, x => x.Contains("IPv6")); Assert.StartsWith("Dikkat", IpSizintiTesti.Gosterge(d).Yazi);
        Assert.Equal(SizintiDurumu.Temiz, IpSizintiTesti.Degerlendir(Win(ip6: "2001:db8::7")).Durum);   // tünel yokken IPv6 sorun değil
    }
    [Fact] public void Proxy_DnsFarkli_Sizinti() => Assert.Equal(SizintiDurumu.Sizinti, IpSizintiTesti.Degerlendir(Win(org: "ProxyHost", cozOrg: "Turk Telekom", proxy: true)).Durum);
    [Fact] public void InternetYok_Uygulanamadi()
    {
        var s = IpSizintiTesti.Coz("Windows", null, null, null, "", null, null, Array.Empty<string>(), Array.Empty<string>(), false, T0); Assert.True(s.InternetYok); var d = IpSizintiTesti.Degerlendir(s); Assert.Equal(SizintiDurumu.InternetYok, d.Durum); Assert.Equal(("Test uygulanamadı", 3), IpSizintiTesti.Gosterge(d));
    }
    [Fact] public void TeknikSorunlar_SahteDnsCevabi_YoksayilirVeSizintiUretmez()
    {
        var s = IpSizintiTesti.Coz("Windows", Ipify("203.0.113.5"), null, "warp=off", "<b>kotu</b>", null, null, new[] { "yok", "8.8.8.8", "8.8.8.8" }, new[] { "Ethernet" }, false, T0);
        Assert.Equal("", s.CozucuIp); Assert.Equal(new[] { "8.8.8.8" }, s.SistemDns); Assert.Equal(SizintiDurumu.Temiz, IpSizintiTesti.Degerlendir(s).Durum); Assert.Contains(IpSizintiTesti.Degerlendir(s).Maddeler, x => x.Contains("belirlenemedi"));
    }

    // ---- Kali karşılaştırması ----
    static IpSizintiSonucu Kali(string ip = "198.51.100.9", string org = "Tailscale Exit", string[]? ar = null, bool internet = true)
        => internet ? IpSizintiTesti.Coz("Kali", Ipify(ip), null, "warp=off", "198.51.100.53", Whois("DE", org), Whois("DE", org), new[] { "100.100.100.100" }, ar ?? new[] { "eth0", "tailscale0" }, false, T0) : IpSizintiTesti.Coz("Kali", null, null, null, "", null, null, new string[0], new[] { "eth0" }, false, T0);
    [Fact] public void Kali_TunelBekleniyor_AyniIp_Sizinti()
    {
        var d = IpSizintiTesti.Degerlendir(Win(ip: "203.0.113.5"), Kali(ip: "203.0.113.5"), true); Assert.Equal(SizintiDurumu.Sizinti, d.Durum); Assert.Contains(d.Maddeler, x => x.Contains("AYNI dış IP"));
    }
    [Fact] public void Kali_AyriIpVeTunelArayuzu_Temiz_TunelYoksaDikkat()
    {
        Assert.Equal(SizintiDurumu.Temiz, IpSizintiTesti.Degerlendir(Win(), Kali(), true).Durum);
        var d = IpSizintiTesti.Degerlendir(Win(), Kali(ar: new[] { "eth0" }), true); Assert.Equal(SizintiDurumu.Dikkat, d.Durum); Assert.Contains(d.Maddeler, x => x.Contains("tünel/VPN arayüzü bulunamadı"));
    }
    [Fact] public void Kali_InternetYok_TunelBekleniyorsaDikkat_Degilse_Temiz()
    {
        Assert.Equal(SizintiDurumu.Dikkat, IpSizintiTesti.Degerlendir(Win(), Kali(internet: false), true).Durum); var d = IpSizintiTesti.Degerlendir(Win(), Kali(internet: false), false); Assert.Equal(SizintiDurumu.Temiz, d.Durum); Assert.Contains(d.Maddeler, x => x.Contains("host-only"));
    }
    [Fact] public void Kali_DnsSizintisi() => Assert.Equal(SizintiDurumu.Sizinti, IpSizintiTesti.Degerlendir(Win(), IpSizintiTesti.Coz("Kali", Ipify("198.51.100.9"), null, null, "8.8.8.8", Whois("DE", "TailnetExit"), Whois("US", "Google LLC"), new[] { "8.8.8.8" }, new[] { "tailscale0" }, false, T0), true).Durum);

    [Fact] public void KaliCoz_AjanCevabi_ArayuzSuzme_GuvenilmezVeri()
    {
        var j = JsonDocument.Parse("{\"t\":\"ipcheck\",\"ipify\":\"{\\\"ip\\\":\\\"198.51.100.9\\\"}\",\"ipify6\":null,\"trace\":\"warp=off\",\"cozucu\":\"198.51.100.53\",\"whois_ip\":\"" + Whois("DE", "Hetzner").Replace("\"", "\\\"") + "\",\"whois_dns\":null,\"dns\":[\"100.100.100.100\",\"x<\"],\"arayuzler\":[\"lo:unknown\",\"eth0:up\",\"tailscale0:unknown\",\"wg1:down\"]}").RootElement;
        var s = IpSizintiTesti.KaliCoz(j, T0); Assert.Equal("Kali", s.Taraf); Assert.Equal("198.51.100.9", s.Genel!.Ip); Assert.Equal("Hetzner", s.Genel.Servis); Assert.True(s.TunelArayuzu); Assert.Equal("tailscale0", s.TunelAdi); Assert.Equal(new[] { "100.100.100.100" }, s.SistemDns);   // "wg1:down" ve "lo" sayılmadı
        Assert.True(IpSizintiTesti.KaliCoz(JsonDocument.Parse("{\"t\":\"ipcheck\",\"ipify\":null,\"ipify6\":null,\"trace\":null,\"cozucu\":\"\",\"dns\":[],\"arayuzler\":[]}").RootElement, T0).InternetYok);
        Assert.True(IpSizintiTesti.KaliCoz(JsonDocument.Parse("[]").RootElement, T0).InternetYok);   // bozuk biçim çökmez
    }

    // ---- Veri toplama (sahte ağ) ----
    [Fact] public async Task WindowsTesti_YalnizcaBeyanEdilenHizmetlereGider_VeriGondermez()
    {
        var istekler = new List<string>(); var k = new IpSizintiTesti.Kaynaklar
        {
            Al = (url, ct) => { istekler.Add(url); return Task.FromResult<string?>(url.Contains("ipwho.is/203") ? Whois("TR", "Turk Telekom") : url.Contains("ipwho.is/") ? Whois("TR", "Turk Telekom") : url.Contains("api6") ? null : url.Contains("api.ipify") ? Ipify("203.0.113.5") : url.Contains("cloudflare") ? "warp=off\n" : null); },
            Coz = (ad, ct) => { istekler.Add("dns:" + ad); return Task.FromResult("203.0.113.53"); }, Yerel = () => (new List<string> { "192.168.1.1" }, new List<string> { "Ethernet" }, false)
        };
        var s = await IpSizintiTesti.WindowsTestiAsync(k, default);
        Assert.Equal("203.0.113.5", s.Genel!.Ip); Assert.Equal("Turk Telekom", s.Cozucu!.Servis);
        var izinli = new[] { "api.ipify.org", "api6.ipify.org", "www.cloudflare.com", "ipwho.is", "dns:whoami.akamai.net" };
        Assert.All(istekler, i => Assert.Contains(izinli, h => i.Contains(h)));   // yalnızca onay penceresinde sayılan hizmetler
        Assert.DoesNotContain(istekler, i => i.Contains("192.168.") || i.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || i.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase));   // yerel bilgi dışarı sızmaz
        Assert.Equal(4, IpSizintiTesti.Hizmetler.Length);
    }
    [Fact] public async Task WindowsTesti_InternetYoksa_Cokmez()
    {
        var k = new IpSizintiTesti.Kaynaklar { Al = (u, c) => Task.FromResult<string?>(null), Coz = (a, c) => Task.FromResult(""), Yerel = () => (new List<string>(), new List<string>(), false) };
        var s = await IpSizintiTesti.WindowsTestiAsync(k, default); Assert.True(s.InternetYok); Assert.Equal(SizintiDurumu.InternetYok, IpSizintiTesti.Degerlendir(s).Durum);
    }
    [Fact] public async Task Iptal_FirlatilirVeSessizYutulmaz()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel(); var k = new IpSizintiTesti.Kaynaklar { Al = (u, c) => { c.ThrowIfCancellationRequested(); return Task.FromResult<string?>(null); }, Coz = (a, c) => Task.FromResult(""), Yerel = () => (new List<string>(), new List<string>(), false) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => IpSizintiTesti.WindowsTestiAsync(k, cts.Token));
    }
    [Fact] public void YerelBilgi_Cokmez() { var (d, a, p) = IpSizintiTesti.YerelBilgi(); Assert.NotNull(d); Assert.NotNull(a); }   // gerçek ağ arayüzü okuma (ağ isteği yok)

    // ---- Kaydet / olay ----
    [Fact] public void Kaydet_OlayVerir_SizintidaKritikOlayYazar()
    {
        int olay = 0; void k() => olay++; IpSizintiTesti.Degisti += k;
        try
        {
            var w = Win(org: "VPNco", cozOrg: "Baska", arayuz: new[] { "wg0" }); var d = IpSizintiTesti.Degerlendir(w); IpSizintiTesti.Kaydet(w, null, d);
            Assert.Equal(1, olay); Assert.Same(w, IpSizintiTesti.SonWindows); Assert.Equal(SizintiDurumu.Sizinti, IpSizintiTesti.SonDeger!.Durum); using var jd = JsonDocument.Parse(File.ReadAllLines(OlayDeposu.Yol).Last()); Assert.Equal("Kritik", jd.RootElement.GetProperty("Seviye").GetString()); Assert.Contains("sızıntı", jd.RootElement.GetProperty("Mesaj").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally { IpSizintiTesti.Degisti -= k; }
    }
}
