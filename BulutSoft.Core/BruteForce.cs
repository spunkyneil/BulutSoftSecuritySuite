// Bu dosya "Giriş Saldırı Radarı"nın beynidir (ekrandan bağımsız, test edilebilir).
// Amaç: kısa sürede art arda hatalı parola deneyen IP adreslerini (kaba kuvvet / brute-force, parola püskürtme) bulmak.
// Kaynaklar: (1) bu Windows'un Güvenlik olay günlüğü, olay 4625 "Hesap oturum açamadı" (RDP, SMB, ağ girişleri),
//            (2) sensör (Kali/Linux) üzerindeki SSH günlüğü (journalctl; yalnızca onaylı sabit komutla, SSH üzerinden).
// Savunma: seçilen IP'leri Windows Güvenlik Duvarı'nda engelleme komutu üretir. Engelleme SİSTEMİ DEĞİŞTİRİR; bu yüzden hiçbir zaman
// otomatik çalışmaz: kullanıcı onayı + yönetici izni (UAC) gerekir. Yerel/kendi adresleriniz asla engellenmez.
using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Programımızın adı.
namespace LabDenetim;

// Tek bir hatalı giriş denemesi.
internal sealed record GirisDenemesi(DateTime Zaman, string Ip, string Kullanici, string Kaynak);

// Bir saldırgan adres özeti.
internal sealed record SaldiriKaynagi(string Ip, int Sayi, int KullaniciSayisi, List<string> Kullanicilar, DateTime Ilk, DateTime Son, HashSet<string> Kaynaklar, string Seviye, int EnYogunPencere = 0);

// Brute-force analiz ve savunma yardımcıları.
internal static class BruteForce
{
    // Olay günlüğü okunamadığında (yetki yok vb.) atılan anlaşılır hata.
    public sealed class KaynakHatasi : Exception { public bool YetkiYok { get; } public KaynakHatasi(string m, bool yetkiYok = false) : base(m) { YetkiYok = yetkiYok; } }

    // ---- Analiz ----
    // Denemeleri IP'ye göre gruplayıp eşiği aşanları (çoktan aza) verir. Seviye: çok yoğun veya çok sayıda farklı kullanıcı denemişse "Yüksek".
    // KAYAN PENCERE (isteğe bağlı): kayan verilirse bir IP'nin şüpheli sayılması için herhangi bir "kayan" uzunluğundaki aralıkta (örn. 2 dakika) en az "esik" deneme yapmış olması gerekir.
    // Böylece günlere yayılmış masum hatalı girişler (yanlış parola) saldırı sayılmaz; kısa sürede yoğunlaşan denemeler yakalanır. kayan boşsa eski davranış (pencere içindeki toplam sayı) aynen sürer.
    public static List<SaldiriKaynagi> Analiz(IEnumerable<GirisDenemesi> denemeler, DateTime simdi, TimeSpan pencere, int esik, TimeSpan? kayan = null)
    {
        // Pencere içindekiler, IP'ye göre.
        var gruplar = denemeler.Where(d => d.Zaman >= simdi - pencere && d.Zaman <= simdi.AddMinutes(5)).GroupBy(d => d.Ip);
        // Sonuç.
        var l = new List<SaldiriKaynagi>();
        foreach (var g in gruplar)
        {
            // Eşik altı atlanır.
            int n = g.Count(); if (n < esik) continue;
            // Kayan pencere: en yoğun aralıktaki deneme sayısı (iki işaretçi yöntemi); eşik altındaysa atla.
            int yogun = kayan.HasValue ? EnYogunAralik(g.Select(x => x.Zaman), kayan.Value) : 0; if (kayan.HasValue && yogun < esik) continue;
            // Kullanıcılar: en çok denenen üç ad.
            var kul = g.Select(x => x.Kullanici).Where(x => x.Length > 0).GroupBy(x => x.ToLowerInvariant()).OrderByDescending(x => x.Count()).Select(x => x.Key).ToList();
            // Seviye.
            string sev = n >= esik * 5 || kul.Count >= 5 || (kayan.HasValue && yogun >= esik * 3) ? "Yüksek" : "Orta";
            // Ekle.
            l.Add(new SaldiriKaynagi(g.Key, n, kul.Count, kul.Take(3).ToList(), g.Min(x => x.Zaman), g.Max(x => x.Zaman), g.Select(x => x.Kaynak).ToHashSet(), sev, yogun));
        }
        // En çok denemeden aza.
        return l.OrderByDescending(x => x.Sayi).ToList();
    }

    // Verilen zamanlar içinde, uzunluğu "aralik" olan herhangi bir aralığa sığan en çok deneme sayısı.
    public static int EnYogunAralik(IEnumerable<DateTime> zamanlar, TimeSpan aralik)
    {
        // Sıralı liste.
        var z = zamanlar.OrderBy(x => x).ToList(); int en = 0, bas = 0;
        // Her bitiş noktası için başlangıcı ilerlet.
        for (int son = 0; son < z.Count; son++) { while (z[son] - z[bas] > aralik) bas++; en = Math.Max(en, son - bas + 1); }
        // Sonuç.
        return en;
    }

    // Adres geçerli ve anlamlı mı? (boş, "-", yerel döngü, belirsiz adresler atlanır)
    public static bool IpGecerli(string s) => IPAddress.TryParse(s, out var ip) && !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any);

    // ---- Windows olay 4625 ----
    // Tek bir 4625 olayının XML'ini çözer (IP yoksa/geçersizse null).
    public static GirisDenemesi? Win4625Ayristir(string xml)
    {
        // Bozuk XML güvenle atlanır.
        try
        {
            // XML ve ad alanı.
            var d = XDocument.Parse(xml); XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
            // Alanları ada göre al.
            string Al(string ad) => d.Descendants(ns + "Data").FirstOrDefault(x => (string?)x.Attribute("Name") == ad)?.Value?.Trim() ?? "";
            // IP: "-" veya boşsa yerel/arabirim girişidir, atla.
            string ip = Al("IpAddress"); if (!IpGecerli(ip)) return null;
            // Zaman (UTC → yerel).
            var z = d.Descendants(ns + "TimeCreated").FirstOrDefault()?.Attribute("SystemTime")?.Value; if (!DateTime.TryParse(z, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) return null;
            // Giriş türü: 10 = RDP, 3 = ağ.
            string tur = Al("LogonType") switch { "10" => "RDP", "3" => "Ağ/SMB", "7" => "Kilit açma", _ => "Windows" };
            // Sonuç.
            return new GirisDenemesi(t.ToLocalTime(), ip, Temiz(Al("TargetUserName")), tur);
        }
        // Çözülemedi.
        catch { return null; }
    }

    // Güvenlik günlüğünden son N dakikadaki 4625 olaylarını okur. Yönetici izni yoksa KaynakHatasi(YetkiYok) atar.
    public static List<GirisDenemesi> WindowsOku(TimeSpan pencere)
    {
        // Sonuç.
        var l = new List<GirisDenemesi>();
        // Sorgu: son N milisaniye, olay 4625.
        try
        {
            // Sorgu metni.
            var q = new EventLogQuery("Security", PathType.LogName, $"*[System[(EventID=4625) and TimeCreated[timediff(@SystemTime) <= {(long)pencere.TotalMilliseconds}]]]");
            // Okuyucu.
            using var r = new EventLogReader(q);
            // Her olay (en çok 20000).
            for (EventRecord? e = r.ReadEvent(); e != null && l.Count < 20000; e = r.ReadEvent()) using (e) { var d = Win4625Ayristir(e.ToXml()); if (d != null) l.Add(d); }
        }
        // Güvenlik günlüğünü okumak için yönetici izni gerekir.
        catch (UnauthorizedAccessException) { throw new KaynakHatasi("Windows Güvenlik günlüğünü okumak için yönetici izni gerekir.", true); }
        catch (EventLogException ex) when (ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("erişim", StringComparison.OrdinalIgnoreCase) || ex.HResult == unchecked((int)0x80070005)) { throw new KaynakHatasi("Windows Güvenlik günlüğünü okumak için yönetici izni gerekir.", true); }
        catch (EventLogNotFoundException) { return l; }
        // Sonuç.
        return l;
    }

    // ---- Sensör SSH günlüğü ----
    // "Failed password for [invalid user] KULLANICI from IP port N" ve "Failed publickey ..." satırları.
    private static readonly Regex SshDeseni = new(@"Failed (?:password|publickey) for (?:invalid user )?(?<u>\S+) from (?<ip>[0-9a-fA-F:.]+) port", RegexOptions.Compiled);

    // Bir journalctl (short-iso) satırını çözer (ilgili değilse null).
    public static GirisDenemesi? SshSatirAyristir(string satir)
    {
        // Çok uzun satırı alma.
        if (string.IsNullOrWhiteSpace(satir) || satir.Length > 2000) return null;
        // Desen.
        var m = SshDeseni.Match(satir); if (!m.Success) return null;
        // IP.
        string ip = m.Groups["ip"].Value; if (!IpGecerli(ip)) return null;
        // Zaman: ilk sözcük ISO (örn. 2026-11-18T14:02:11+0300); ofsete iki nokta eklenir.
        string ilk = satir.Split(' ', 2)[0]; ilk = Regex.Replace(ilk, @"([+-]\d\d)(\d\d)$", "$1:$2"); if (!DateTimeOffset.TryParse(ilk, null, System.Globalization.DateTimeStyles.None, out var t)) return null;
        // Sonuç.
        return new GirisDenemesi(t.LocalDateTime, ip, Temiz(m.Groups["u"].Value), "SSH");
    }

    // Kullanıcı adındaki denetim karakterlerini temizler ve kısaltır (günlük sahte satır oluşturamasın).
    private static string Temiz(string s) { var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim(); return t.Length > 40 ? t[..40] : t; }

    // Sensörde çalışacak SABİT komut (yalnızca sayısal dakika alır; girdi doğrulanır).
    public static string SshGunlukKomutu(int dakika) => $"journalctl -u ssh -u sshd --since '{Math.Clamp(dakika, 5, 1440)} min ago' -o short-iso --no-pager 2>&1 | tail -n 8000";

    // Sensörden SSH denemelerini okur.
    public static async Task<List<GirisDenemesi>> SensorOkuAsync(SensorProfili p, TimeSpan pencere, CancellationToken ct)
    {
        // Sonuç ve ham çıktı ipucu.
        var l = new List<GirisDenemesi>(); bool izinIpucu = false;
        // Çalıştır.
        var (kod, hata) = await SensorTaramaYurutucu.CalistirAsync(p, SshGunlukKomutu((int)pencere.TotalMinutes), null, s => { var d = SshSatirAyristir(s); if (d != null) l.Add(d); else if (s.Contains("not seeing messages", StringComparison.OrdinalIgnoreCase)) izinIpucu = true; }, ct, 60);
        // Bağlanamadıysa anlaşılır hata.
        if (kod != 0 && l.Count == 0) throw new KaynakHatasi("Sensör günlüğü okunamadı: " + SensorCiktiAyristirici.HataAciklama(hata));
        // Yetki ipucu: kullanıcı günlük okuyamıyor.
        if (izinIpucu && l.Count == 0) throw new KaynakHatasi("Sensör kullanıcısı sistem günlüğünü okuyamıyor. Sensörde şunu çalıştırın: sudo usermod -aG systemd-journal,adm <kullanıcı> (sonra yeniden giriş).");
        // Sonuç.
        return l;
    }

    // ---- Engelleme ----
    // Bu bilgisayarın kendi adresleri (asla engellenmez).
    public static HashSet<string> YerelAdresler()
    {
        // Sonuç.
        var s = new HashSet<string>();
        // Hata olursa boş kalsın.
        try { foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) foreach (var u in ni.GetIPProperties().UnicastAddresses) s.Add(u.Address.ToString()); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "BruteForce.YerelAdresler"); }
        // Ağ altyapısı da korunur: varsayılan ağ geçidi, DNS ve DHCP sunucuları (yanlışlıkla engellenirse bilgisayar ağdan kopar).
        try { foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) { var p = ni.GetIPProperties(); foreach (var g in p.GatewayAddresses) s.Add(g.Address.ToString()); foreach (var d in p.DnsAddresses) s.Add(d.ToString()); foreach (var h in p.DhcpServerAddresses) s.Add(h.ToString()); } } catch (Exception hk2) { HataKayit.Sessiz(hk2, "BruteForce.YerelAdresler"); }
        // Sonuç.
        return s;
    }

    // IP için güvenlik duvarı kural adı (yalnızca harf/rakam/tire).
    public static string KuralAdi(string ip) => "BulutSoft-BF-" + Regex.Replace(ip, @"[^0-9a-fA-F]", "-");

    // IP özel/yerel ağdan mı? (engellenirse kendi ağınızdaki cihazlar kesilebilir: ekstra uyarı)
    public static bool OzelAg(string ip) => IPAddress.TryParse(ip, out var a) && LabKapsam.IpLabdaMi(a);

    // Engellenebilir mi? Değilse nedenini verir (null = engellenebilir).
    public static string? EngelHatasi(string ip, ISet<string> korunan)
    {
        // Geçerlilik.
        if (!IpGecerli(ip)) return "geçersiz adres";
        // Normalleştirilmiş adres karşılaştırması.
        string norm = IPAddress.Parse(ip).ToString();
        // Kendi adresiniz veya korunan adresler (sensör vb.).
        if (korunan.Contains(norm)) return "bu bilgisayarın/sensörün kendi adresi";
        // Çok noktaya yayın / yayın adresi: tek bir saldırgan olamaz, engellemek anlamsız ve zararlı olabilir.
        var yay = IPAddress.Parse(ip); if (yay.IsIPv6Multicast || (yay.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (yay.GetAddressBytes()[0] >= 224 || yay.GetAddressBytes()[0] == 0))) return "yayın / çok noktaya yayın adresi";
        // Bağlantı-yerel adres.
        var a = IPAddress.Parse(ip); if (a.IsIPv6LinkLocal || (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && a.GetAddressBytes()[0] == 169 && a.GetAddressBytes()[1] == 254)) return "bağlantı-yerel adres";
        // Sorun yok.
        return null;
    }

    // Verilen IP'leri engelleyen tek bir komut zinciri (önce aynı adlı eski kuralı siler). Hatalı adresler atlanır.
    public static string EngelZinciri(IEnumerable<string> ipler, ISet<string> korunan) => string.Join(" & ", ipler.Where(i => EngelHatasi(i, korunan) == null).Select(i => IPAddress.Parse(i).ToString()).Distinct().Select(i =>
        $"netsh advfirewall firewall delete rule name={KuralAdi(i)} >nul 2>&1 & netsh advfirewall firewall add rule name={KuralAdi(i)} dir=in action=block remoteip={i} protocol=any"));

    // Şu an engelli (BulutSoft-BF-) IP kural adları.
    public static HashSet<string> EngelliKurallar() => GuvenlikDuvari.Listele().Where(k => k.Ad.StartsWith("BulutSoft-BF-", StringComparison.Ordinal)).Select(k => k.Ad).ToHashSet();
}
