// Bu dosya IP SIZINTI TESTİ modülüdür: bu bilgisayarın (ve isteğe bağlı olarak Kali sensörünün) internete çıkarken DIŞARIDAN nasıl göründüğünü ölçer:
// gerçek dış IP, ülke/şehir/servis sağlayıcı, DNS isteklerini hangi çözücünün gördüğü, IPv6 durumu ve VPN/WARP/proxy işaretleri; sonra olası SIZINTIYI değerlendirir.
// GİZLİLİK: test, üçüncü taraf halka açık servislere (ipify, ipwho.is, Cloudflare, Akamai DNS adı) bağlanır; bu servisler dış IP adresinizi görür (testin doğası budur).
// Bu yüzden her test KULLANICI ONAYIYLA çalışır (arayüzde varsayılan "Hayır"); onaysız hiçbir istek gönderilmez. Bilgisayardan bu servislere başka hiçbir kişisel veri gönderilmez.
// Ham servis cevapları GÜVENİLMEZ veri sayılır: biçim doğrulanır, uzunluk sınırlanır.
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Bir IP adresinin dış bilgisi.
internal sealed record IpBilgisi(string Ip, string Ulke, string Sehir, string Servis);

// Testin sonucu (Windows veya Kali için).
internal sealed record IpSizintiSonucu(string Taraf, IpBilgisi? Genel, IpBilgisi? Ipv6, string CozucuIp, IpBilgisi? Cozucu, List<string> SistemDns, bool TunelArayuzu, string TunelAdi, bool Proxy, string Warp, bool InternetYok, List<string> Notlar, DateTime Zaman);

// Değerlendirme.
internal enum SizintiDurumu { Bilinmiyor, Temiz, Dikkat, Sizinti, InternetYok }
internal sealed record SizintiDegerlendirmesi(SizintiDurumu Durum, string Kisa, List<string> Maddeler);

// Ham servis cevaplarından sonuç üretir ve değerlendirir (saf işlevler: test edilebilir).
internal static class IpSizintiTesti
{
    // Son sonuçlar (arayüz göstergesi için). Windows ve Kali ayrı tutulur.
    public static IpSizintiSonucu? SonWindows { get; private set; } public static IpSizintiSonucu? SonKali { get; private set; } public static SizintiDegerlendirmesi? SonDeger { get; private set; }
    public static event Action? Degisti;

    // Kullanılan hizmetler (onay penceresinde gösterilir).
    public static readonly string[] Hizmetler = { "api.ipify.org / api6.ipify.org (dış IP adresiniz)", "ipwho.is (IP'nin ülke ve servis sağlayıcı bilgisi)", "www.cloudflare.com/cdn-cgi/trace (WARP/proxy durumu)", "whoami.akamai.net (DNS çözücünüzün adresi)" };

    // Tünel/VPN adı belirten kalıp.
    private static readonly Regex TunelKalibi = new(@"wireguard|tailscale|openvpn|tap-windows|wintun|\bwarp\b|vpn|nordlynx|proton|mullvad|zerotier|anyconnect|\btun\d|\bwg\d|\btap\d|\bppp\d", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    // IPv4/IPv6 metni geçerli bir IP mi? (güvenilmez veri)
    private static string IpAl(string? s) => s != null && s.Length <= 45 && IPAddress.TryParse(s.Trim(), out var ip) ? ip.ToString() : "";

    // {"ip":"..."} cevabından IP.
    public static string IpifyCoz(string? json) { try { if (string.IsNullOrWhiteSpace(json) || json.Length > 2000) return ""; using var d = JsonDocument.Parse(json); return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("ip", out var v) && v.ValueKind == JsonValueKind.String ? IpAl(v.GetString()) : ""; } catch { return ""; } }

    // ipwho.is cevabından ülke/şehir/servis sağlayıcı.
    public static IpBilgisi? WhoisCoz(string ip, string? json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 20000 || ip.Length == 0) return null; using var d = JsonDocument.Parse(json); var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object) return null;
            string S(JsonElement e, string ad, int en = 80) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? Temiz(v.GetString() ?? "", en) : "";
            string servis = ""; if (k.TryGetProperty("connection", out var c)) { servis = S(c, "org"); if (servis.Length == 0) servis = S(c, "isp"); }
            return new IpBilgisi(ip, S(k, "country"), S(k, "city"), servis);
        }
        catch { return null; }
    }
    private static string Temiz(string s, int en) { s = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim(); return s.Length > en ? s[..en] : s; }

    // Cloudflare trace metninden alanlar (warp=off|on|plus, ip=...).
    public static Dictionary<string, string> TraceCoz(string? metin)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (string.IsNullOrWhiteSpace(metin) || metin.Length > 4000) return d;
        foreach (var satir in metin.Split('\n').Take(40)) { int i = satir.IndexOf('='); if (i > 0 && i < 30) d[Temiz(satir[..i], 30)] = Temiz(satir[(i + 1)..], 60); }
        return d;
    }

    // Ham veriden sonuç. taraf: "Windows" veya "Kali". arayuzler: tünel adı aranacak arayüz adları. proxy: sistem/ortam proxy'si var mı.
    public static IpSizintiSonucu Coz(string taraf, string? ipifyJson, string? ipify6Json, string? trace, string? cozucuIp, string? whoisIpJson, string? whoisDnsJson, IEnumerable<string> sistemDns, IEnumerable<string> arayuzler, bool proxy, DateTime zaman)
    {
        string ip = IpifyCoz(ipifyJson), ip6 = IpifyCoz(ipify6Json), coz = IpAl(cozucuIp); var notlar = new List<string>(); var tr = TraceCoz(trace);
        var genel = ip.Length > 0 ? WhoisCoz(ip, whoisIpJson) ?? new IpBilgisi(ip, "", "", "") : null; var v6 = ip6.Length > 0 ? new IpBilgisi(ip6, "", "", "") : null;
        var cozucu = coz.Length > 0 ? WhoisCoz(coz, whoisDnsJson) ?? new IpBilgisi(coz, "", "", "") : null;
        var dns = sistemDns.Select(IpAl).Where(x => x.Length > 0).Distinct().Take(8).ToList();
        string tunel = arayuzler.FirstOrDefault(a => TunelKalibi.IsMatch(a)) ?? ""; string warp = tr.TryGetValue("warp", out var w) ? w.ToLowerInvariant() : "";
        bool internetYok = ip.Length == 0 && ip6.Length == 0 && trace == null;
        if (internetYok) notlar.Add("İnternete çıkılamadı (ağ kapalı, güvenlik duvarı veya host-only ağ): test uygulanamadı.");
        return new IpSizintiSonucu(taraf, genel, v6, coz, cozucu, dns, tunel.Length > 0 || warp is "on" or "plus", tunel.Length > 0 ? tunel : (warp is "on" or "plus" ? "Cloudflare WARP" : ""), proxy, warp, internetYok, notlar, zaman);
    }

    // Servis sağlayıcı adlarını karşılaştırmak için sadeleştirir ("AS13335 Cloudflare, Inc." → "cloudflare").
    private static string Sade(string? s) => Regex.Replace(Regex.Replace((s ?? "").ToLowerInvariant(), @"\bas\d+\b|\b(inc|llc|ltd|gmbh|corp|corporation|company|limited|communications|telecom|telekom)\b", ""), @"[^a-z0-9]", "");
    private static bool AyniServis(string? a, string? b) { string x = Sade(a), y = Sade(b); return x.Length >= 3 && y.Length >= 3 && (x == y || x.Contains(y) || y.Contains(x)); }

    // Sızıntı değerlendirmesi. kaliTunelBekleniyor: kullanıcı Kali'nin ayrı bir tünelden çıkmasını bekliyorsa true; kali: Kali sonucu (varsa).
    public static SizintiDegerlendirmesi Degerlendir(IpSizintiSonucu win, IpSizintiSonucu? kali = null, bool kaliTunelBekleniyor = false)
    {
        var m = new List<string>(); var durum = SizintiDurumu.Temiz;
        void Yukselt(SizintiDurumu d) { if (d > durum && d != SizintiDurumu.InternetYok) durum = d; }
        if (win.InternetYok) return new(SizintiDurumu.InternetYok, "Test uygulanamadı: internete çıkılamadı", new List<string> { "Bu bilgisayar test servislerine ulaşamadı." });
        // Bilgi: dış kimlik.
        if (win.Genel != null) m.Add($"Bu bilgisayarın dış IP adresi: {win.Genel.Ip}" + (win.Genel.Ulke.Length > 0 ? $" ({win.Genel.Ulke}{(win.Genel.Sehir.Length > 0 ? ", " + win.Genel.Sehir : "")})" : "") + (win.Genel.Servis.Length > 0 ? $" · servis sağlayıcı: {win.Genel.Servis}" : ""));
        if (win.CozucuIp.Length > 0) m.Add($"DNS isteklerinizi şu çözücü görüyor: {win.CozucuIp}" + (win.Cozucu?.Servis.Length > 0 ? $" ({win.Cozucu!.Servis})" : "")); else m.Add("DNS çözücüsü belirlenemedi.");
        if (win.SistemDns.Count > 0) m.Add("Sistemde ayarlı DNS sunucuları: " + string.Join(", ", win.SistemDns));
        if (win.TunelArayuzu) m.Add($"Tünel/VPN işareti bulundu: {win.TunelAdi}."); if (win.Proxy) m.Add("Sistem/ortam proxy ayarı bulundu.");
        // Sızıntı 1: tünel/VPN açıkken DNS'i başka bir servis sağlayıcı görüyor.
        if ((win.TunelArayuzu || win.Proxy) && win.Genel != null && win.Cozucu != null && win.Genel.Servis.Length > 0 && win.Cozucu.Servis.Length > 0 && !AyniServis(win.Genel.Servis, win.Cozucu.Servis)) { m.Add($"OLASI DNS SIZINTISI: dış IP'niz {win.Genel.Servis} üzerinden çıkıyor ama DNS isteklerinizi {win.Cozucu.Servis} görüyor. Tünel kullanıyorsanız DNS tünel dışına kaçıyor olabilir."); Yukselt(SizintiDurumu.Sizinti); }
        // Sızıntı 2: IPv6 ile ayrı bir kimlikle çıkılıyor (tünel yalnızca IPv4'ü taşıyor olabilir).
        if ((win.TunelArayuzu || win.Proxy) && win.Ipv6 != null) { m.Add($"UYARI: IPv6 ile de internete çıkılıyor ({win.Ipv6.Ip}). Tünel yalnızca IPv4'ü taşıyorsa gerçek kimliğiniz IPv6 üzerinden görünebilir."); Yukselt(SizintiDurumu.Dikkat); }
        // Kali karşılaştırması.
        if (kali != null)
        {
            if (kali.InternetYok) { m.Add("Kali sensörü internete çıkamıyor (host-only ağ olabilir): Kali tarafı test edilemedi."); if (kaliTunelBekleniyor) Yukselt(SizintiDurumu.Dikkat); }
            else
            {
                if (kali.Genel != null) m.Add($"Kali sensörünün dış IP adresi: {kali.Genel.Ip}" + (kali.Genel.Ulke.Length > 0 ? $" ({kali.Genel.Ulke})" : "") + (kali.Genel.Servis.Length > 0 ? $" · {kali.Genel.Servis}" : ""));
                if (kaliTunelBekleniyor && kali.Genel != null && win.Genel != null && kali.Genel.Ip == win.Genel.Ip) { m.Add("SIZINTI: Kali sensörü ayrı bir tünelden çıkmıyor; Windows ile AYNI dış IP adresini kullanıyor."); Yukselt(SizintiDurumu.Sizinti); }
                if (kaliTunelBekleniyor && !kali.TunelArayuzu) { m.Add("Kali'de tünel/VPN arayüzü bulunamadı (tailscale, wg, tun...)."); Yukselt(SizintiDurumu.Dikkat); }
                if ((kali.TunelArayuzu || kaliTunelBekleniyor) && kali.Genel != null && kali.Cozucu != null && kali.Genel.Servis.Length > 0 && kali.Cozucu.Servis.Length > 0 && !AyniServis(kali.Genel.Servis, kali.Cozucu.Servis)) { m.Add($"Kali'de olası DNS sızıntısı: dış IP {kali.Genel.Servis}, DNS çözücü {kali.Cozucu.Servis}."); Yukselt(SizintiDurumu.Sizinti); }
            }
        }
        string kisa = durum switch { SizintiDurumu.Sizinti => "Olası sızıntı bulundu", SizintiDurumu.Dikkat => "Dikkat edilecek nokta var", _ => "Temiz: belirgin bir sızıntı işareti yok" };
        if (durum == SizintiDurumu.Temiz && !win.TunelArayuzu && !win.Proxy) m.Add("Not: Tünel/VPN kullanmıyorsunuz; gerçek IP'niz doğal olarak dışarıdan görünür. Bu bir sızıntı değildir.");
        return new(durum, kisa, m);
    }

    // Göstergede görünecek (yazı, renk 0=yeşil 1=sarı 2=kırmızı 3=gri).
    public static (string Yazi, int Renk) Gosterge(SizintiDegerlendirmesi? d) => d == null ? ("Henüz test edilmedi (tıklayın)", 3) : d.Durum switch { SizintiDurumu.Temiz => ("Temiz", 0), SizintiDurumu.Dikkat => ("Dikkat: " + d.Kisa, 1), SizintiDurumu.Sizinti => ("Olası sızıntı!", 2), SizintiDurumu.InternetYok => ("Test uygulanamadı", 3), _ => ("Bilinmiyor", 3) };

    // Sonucu saklar ve olay verir.
    public static void Kaydet(IpSizintiSonucu win, IpSizintiSonucu? kali, SizintiDegerlendirmesi d) { SonWindows = win; SonKali = kali; SonDeger = d; try { Degisti?.Invoke(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "IpSizintiTesti.Kaydet"); } if (d.Durum == SizintiDurumu.Sizinti) { try { ServisBaglami.OlayYazici.Ekle("IP Sızıntı", "Kritik", "KRİTİK -> IP sızıntı testi olası sızıntı buldu: " + d.Maddeler.FirstOrDefault(x => x.Contains("SIZINTI") || x.Contains("SIZINTISI")) ); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "IpSizintiTesti.Kaydet"); } } }
    internal static void Sifirla() { SonWindows = null; SonKali = null; SonDeger = null; }

    // ---------------- Veri toplama (ağ erişimi: yalnızca ONAYLA çağrılmalı) ----------------
    // Ağ işlevleri (testte sahte verilir).
    public sealed class Kaynaklar
    {
        public Func<string, CancellationToken, Task<string?>> Al { get; set; } = async (url, ct) =>
        {
            try { using var istek = new HttpRequestMessage(HttpMethod.Get, url); istek.Headers.UserAgent.ParseAdd("BulutSoft-IpTesti/1.0"); using var r = await AgIstemciFabrikasi.GonderAsync(AgProfili.IpSizintisi, istek, HttpCompletionOption.ResponseHeadersRead, null, ct); if (!r.IsSuccessStatusCode) return null; var b = new byte[8192]; await using var s = await r.Content.ReadAsStreamAsync(ct); int n = 0, k; while (n < b.Length && (k = await s.ReadAsync(b.AsMemory(n), ct)) > 0) n += k; return System.Text.Encoding.UTF8.GetString(b, 0, n); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch (AgHatasi) { return null; }   // ağ yok / zaman aşımı: bu ölçüm "alınamadı" sayılır, test devam eder
        };
        public Func<string, CancellationToken, Task<string>> Coz { get; set; } = async (ad, ct) => { try { var a = await Dns.GetHostAddressesAsync(ad, ct); return a.FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? ""; } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch { return ""; } };
        public Func<(List<string> Dns, List<string> Arayuzler, bool Proxy)> Yerel { get; set; } = YerelBilgi;
    }

    // Bu bilgisayarın yerel ağ bilgisi: sistem DNS sunucuları, etkin arayüz adları, proxy var mı.
    public static (List<string> Dns, List<string> Arayuzler, bool Proxy) YerelBilgi()
    {
        var dns = new List<string>(); var ar = new List<string>();
        try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Loopback)) { ar.Add(n.Name + " " + n.Description + (n.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp ? " tun" : "")); try { dns.AddRange(n.GetIPProperties().DnsAddresses.Select(a => a.ToString())); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "IpSizintiTesti.YerelBilgi"); } } } catch (Exception hk4) { HataKayit.Sessiz(hk4, "IpSizintiTesti.YerelBilgi"); }
        bool proxy = false; try { var u = new Uri("https://api.ipify.org"); var p = WebRequest.GetSystemWebProxy().GetProxy(u); proxy = p != null && p != u; } catch (Exception hk5) { HataKayit.Sessiz(hk5, "IpSizintiTesti.YerelBilgi"); }
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTPS_PROXY")) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("https_proxy"))) proxy = true;
        return (dns.Distinct().ToList(), ar, proxy);
    }

    // Bu bilgisayar için testi çalıştırır (ağ erişimi YAPAR: çağıran onay almış olmalı).
    public static async Task<IpSizintiSonucu> WindowsTestiAsync(Kaynaklar k, CancellationToken ct)
    {
        var ipify = await k.Al("https://api.ipify.org?format=json", ct); var ip6 = await k.Al("https://api6.ipify.org?format=json", ct); var trace = await k.Al("https://www.cloudflare.com/cdn-cgi/trace", ct);
        string coz = await k.Coz("whoami.akamai.net", ct); string ip = IpifyCoz(ipify);
        var whoisIp = ip.Length > 0 ? await k.Al("https://ipwho.is/" + ip, ct) : null; var whoisDns = IpAl(coz).Length > 0 ? await k.Al("https://ipwho.is/" + IpAl(coz), ct) : null; var (dns, ar, proxy) = k.Yerel();
        return Coz("Windows", ipify, ip6, trace, coz, whoisIp, whoisDns, dns, ar, proxy, DateTime.Now);
    }

    // Kali sensörü için testi çalıştırır (ajan "ipcheck" kipi; Kali'nin ağ erişimiyle). Çağıran onay almış olmalı.
    public static async Task<IpSizintiSonucu> KaliTestiAsync(SensorProfili p, CancellationToken ct)
    {
        var d = await SensorAg.ToplaAsync(p, new { mod = "ipcheck" }, "ipcheck", ct); return KaliCoz(d, DateTime.Now);
    }

    // Ajan cevabını çözer (ayrı: test edilebilir). Gelen her şey güvenilmez veridir.
    public static IpSizintiSonucu KaliCoz(JsonElement k, DateTime zaman)
    {
        string? S(string ad) => k.ValueKind == JsonValueKind.Object && k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        List<string> L(string ad) => k.ValueKind == JsonValueKind.Object && k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => Temiz(x.GetString() ?? "", 60)).Take(30).ToList() : new();
        // Arayüz "ad:durum" biçimindedir; yalnızca UP veya UNKNOWN olanlar sayılır (tünel arayüzleri çoğu zaman 'unknown' gösterir).
        var ar = L("arayuzler").Where(x => !x.EndsWith(":down", StringComparison.OrdinalIgnoreCase) && !x.StartsWith("lo:")).Select(x => x.Split(':')[0]).ToList();
        return Coz("Kali", S("ipify"), S("ipify6"), S("trace"), S("cozucu"), S("whois_ip"), S("whois_dns"), L("dns"), ar, false, zaman);
    }
}
