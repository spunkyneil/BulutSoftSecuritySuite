// Bu dosya DIŞ AĞ PASİF GÖZLEM için veri modellerini ve iki saf yardımcıyı içerir:
//  • SecurityTxt: RFC 9116 "/.well-known/security.txt" dosyasını çözer ve sorunlarını bulur.
//  • DnsIstemci: Windows'un yerleşik çözücüsünün veremediği TXT/MX/CAA/NS kayıtları için küçük bir DNS istemcisi (SPF ve DMARC için).
//    Yalnızca bu bilgisayarın KENDİ sistem DNS sunucularına sorar (üçüncü taraf bir DNS'e veya "DNS over HTTPS"e hiçbir şey göndermez).
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// ---------------- Gözlem modelleri ----------------
// Bir DNS kaydı (veri metin olarak).
internal sealed record DnsKaydi(string Ad, ushort Tur, string Veri);

// HTTP gözlemi (başlıklar küçük harf anahtarlı).
internal sealed record HttpGozlem(string Url, int Durum, Dictionary<string, string> Basliklar, List<string> Cerezler, List<string> Yonlendirmeler, string HttpYonlendirme);

// TLS gözlemi.
internal sealed record TlsGozlem(string Surum, string Sifre, string Subject, string Issuer, DateTime? Baslangic, DateTime? Bitis, List<string> ZincirHatalari, List<string> San, string ImzaAlg, string AnahtarTur, int AnahtarBit, bool IsimUyumu);

// DNS gözlemi (SPF/DMARC/MX/CAA/NS).
internal sealed record DnsGozlem(string OrgAlan, List<string> Mx, List<string> Ns, string? Spf, string? Dmarc, List<string> Caa, string Hata);

// security.txt sonucu.
internal sealed record SecurityTxtSonucu(bool Bulundu, string? Url, Dictionary<string, List<string>> Alanlar, List<string> Sorunlar);

// Bütün gözlem.
internal sealed record PasifGozlem(string Host, DateTime Zaman, string Yer, HttpGozlem? Http, TlsGozlem? Tls, DnsGozlem? Dns, SecurityTxtSonucu SecTxt, List<string> Notlar);

// ---------------- security.txt (RFC 9116) ----------------
// security.txt çözücü.
internal static class SecurityTxt
{
    // Dosya metnini çözer. https: dosya https üzerinden mi geldi; icerikTuru: Content-Type başlığı.
    public static SecurityTxtSonucu Coz(string govde, string? url, bool https, string? icerikTuru, DateTime simdiUtc)
    {
        // Alanlar ve sorunlar.
        var alanlar = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); var sorun = new List<string>();
        // İmzalı (PGP clearsign) dosyada yalnızca imzalı gövde okunur.
        bool govdeIci = !govde.Contains("-----BEGIN PGP SIGNED MESSAGE-----"), basliktaydi = !govdeIci;
        // Satır satır.
        foreach (var ham in govde.Replace("\r", "").Split('\n'))
        {
            // PGP başlığı: boş satıra kadar atla.
            if (!govdeIci) { if (ham.StartsWith("-----BEGIN PGP SIGNED MESSAGE-----", StringComparison.Ordinal)) continue; if (basliktaydi) { if (ham.Trim().Length == 0) basliktaydi = false; continue; } }
            // İmza başladıysa dur.
            if (ham.StartsWith("-----BEGIN PGP SIGNATURE-----", StringComparison.Ordinal)) break;
            // Yorum ve boş satırlar.
            var s = ham.Trim(); if (s.Length == 0 || s.StartsWith('#')) continue;
            // "Alan: değer".
            var m = Regex.Match(s, @"^(?<a>[A-Za-z][A-Za-z\-]{1,40}):\s*(?<d>.+)$"); if (!m.Success) continue;
            // Kaydet (değer en çok 500 karakter).
            string a = m.Groups["a"].Value, d = m.Groups["d"].Value.Trim(); if (d.Length > 500) d = d[..500]; if (!alanlar.TryGetValue(a, out var l)) alanlar[a] = l = new(); if (l.Count < 20) l.Add(d);
        }
        // Zorunlu alanlar.
        if (!alanlar.TryGetValue("Contact", out var contactDegerleri)) sorun.Add("Contact alanı yok (RFC 9116'da zorunlu).");
        else foreach (var c in contactDegerleri) if (!Regex.IsMatch(c, @"^(mailto:|https://|tel:)", RegexOptions.IgnoreCase) && !Regex.IsMatch(c, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) sorun.Add($"Contact değeri geçerli bir URI değil: {c}");
        // Expires.
        if (!alanlar.TryGetValue("Expires", out var ex)) sorun.Add("Expires alanı yok (RFC 9116'da zorunlu).");
        else if (!DateTimeOffset.TryParse(ex[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var bit)) sorun.Add($"Expires tarihi okunamadı: {ex[0]}");
        else if (bit.UtcDateTime < simdiUtc) sorun.Add($"Expires tarihi geçmiş ({bit:yyyy-MM-dd}); dosya güncel değil.");
        else if (bit.UtcDateTime > simdiUtc.AddYears(1).AddDays(7)) sorun.Add("Expires tarihi 1 yıldan uzak (RFC 9116 daha kısa süre önerir).");
        // Bağlantı güvenliği ve tür.
        if (!https) sorun.Add("security.txt https üzerinden sunulmalı (RFC 9116).");
        if (!string.IsNullOrEmpty(icerikTuru) && !icerikTuru.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase)) sorun.Add($"Content-Type text/plain olmalı (şu an: {icerikTuru}).");
        // Sonuç.
        return new SecurityTxtSonucu(true, url, alanlar, sorun);
    }

    // Bulunamadı sonucu.
    public static SecurityTxtSonucu Yok() => new(false, null, new(StringComparer.OrdinalIgnoreCase), new());

    // İletişim adresleri (mailto: ve https:), sıralı ve tekrarsız.
    public static List<string> Iletisimler(SecurityTxtSonucu s) => s.Alanlar.TryGetValue("Contact", out var l) ? l.Select(x => x.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? x[7..].Split('?')[0] : x).Distinct().ToList() : new();
}

// ---------------- DNS ----------------
// Küçük DNS istemcisi.
internal static class DnsIstemci
{
    // Sunucu listesi (testte değiştirilebilir).
    public static Func<IEnumerable<IPAddress>> Sunucular { get; set; } = SistemSunuculari;

    // Bu bilgisayarın sistem DNS sunucuları (yalnızca IPv4, çalışan kartlar).
    public static List<IPAddress> SistemSunuculari()
    {
        // Sonuç.
        var l = new List<IPAddress>();
        // Okuma hata verebilir.
        try { foreach (var n in NetworkInterface.GetAllNetworkInterfaces()) { if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue; foreach (var d in n.GetIPProperties().DnsAddresses) if (d.AddressFamily == AddressFamily.InterNetwork && !l.Contains(d)) l.Add(d); } } catch (Exception hk1) { HataKayit.Sessiz(hk1, "PasifModel.SistemSunuculari"); }
        // Sonuç.
        return l;
    }

    // Sorgu paketi (RD bayrağı açık, tek soru).
    internal static byte[] Paket(string ad, ushort tur, ushort id)
    {
        // Başlık.
        var m = new List<byte> { (byte)(id >> 8), (byte)id, 0x01, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 };
        // Ad: etiketler.
        foreach (var e in ad.TrimEnd('.').Split('.')) { var b = Encoding.ASCII.GetBytes(e); if (b.Length is 0 or > 63) throw new ArgumentException("geçersiz DNS adı"); m.Add((byte)b.Length); m.AddRange(b); }
        // Sonlandırıcı, tür, sınıf (IN).
        m.Add(0); m.Add((byte)(tur >> 8)); m.Add((byte)tur); m.Add(0); m.Add(1);
        // Sonuç.
        return m.ToArray();
    }

    // Paketten ad okur (sıkıştırma işaretçilerini izler; sonsuz döngüye karşı sınırlı).
    private static string OkuAd(byte[] m, ref int i)
    {
        // Etiketler.
        var et = new List<string>(); int gec = i; bool atladi = false; int koruma = 0;
        while (true)
        {
            // Sınır ve döngü koruması.
            if (gec >= m.Length || ++koruma > 64) throw new FormatException("bozuk DNS adı");
            // Uzunluk baytı.
            int u = m[gec];
            // Bitiş.
            if (u == 0) { if (!atladi) i = gec + 1; break; }
            // İşaretçi.
            if ((u & 0xC0) == 0xC0) { if (gec + 1 >= m.Length) throw new FormatException("bozuk işaretçi"); int hedef = ((u & 0x3F) << 8) | m[gec + 1]; if (!atladi) i = gec + 2; atladi = true; gec = hedef; continue; }
            // Etiket.
            if (gec + 1 + u > m.Length) throw new FormatException("bozuk etiket"); et.Add(Encoding.ASCII.GetString(m, gec + 1, u)); gec += 1 + u;
        }
        // Sonuç.
        return string.Join(".", et);
    }

    // Cevabı çözer. kesik: TC bayrağı (TCP ile yeniden denenmeli). Hatalı/uyumsuz cevapta FormatException.
    internal static List<DnsKaydi> Coz(byte[] c, ushort id, out bool kesik)
    {
        // Başlık.
        if (c.Length < 12 || (ushort)((c[0] << 8) | c[1]) != id) throw new FormatException("DNS cevabı eşleşmiyor");
        // Bayraklar ve sayılar.
        kesik = (c[2] & 0x02) != 0; int rcode = c[3] & 0x0F; int qd = (c[4] << 8) | c[5], an = (c[6] << 8) | c[7]; var l = new List<DnsKaydi>();
        // Hata kodu (3 = isim yok → boş liste).
        if (rcode != 0) return l;
        // Soruları atla.
        int i = 12; for (int q = 0; q < qd; q++) { OkuAd(c, ref i); i += 4; }
        // Cevaplar.
        for (int k = 0; k < an && i < c.Length; k++)
        {
            // Sahip, tür, sınıf, TTL, uzunluk.
            string ad = OkuAd(c, ref i); if (i + 10 > c.Length) throw new FormatException("kısa kayıt"); ushort tur = (ushort)((c[i] << 8) | c[i + 1]); int rl = (c[i + 8] << 8) | c[i + 9]; i += 10; if (i + rl > c.Length) throw new FormatException("kısa veri"); int r = i;
            // Türe göre veri.
            string veri = tur switch
            {
                16 => TxtOku(c, r, rl),
                15 => $"{(c[r] << 8) | c[r + 1]} {OkuAdAt(c, r + 2)}",
                2 => OkuAdAt(c, r),
                257 => rl >= 2 && 2 + c[r + 1] <= rl ? $"{c[r]} {Encoding.ASCII.GetString(c, r + 2, c[r + 1])} \"{Encoding.UTF8.GetString(c, r + 2 + c[r + 1], rl - 2 - c[r + 1])}\"" : "",
                1 when rl == 4 => new IPAddress(c.AsSpan(r, 4)).ToString(),
                28 when rl == 16 => new IPAddress(c.AsSpan(r, 16)).ToString(),
                _ => ""
            };
            // Ekle.
            if (veri.Length > 0) l.Add(new DnsKaydi(ad, tur, veri)); i += rl;
        }
        // Sonuç.
        return l;
    }

    // Belirli konumdan ad okur.
    private static string OkuAdAt(byte[] m, int konum) { int i = konum; return OkuAd(m, ref i); }

    // TXT: uzunluk önekli parçaları birleştirir.
    private static string TxtOku(byte[] c, int r, int rl) { var sb = new StringBuilder(); int p = r, son = r + rl; while (p < son) { int u = c[p++]; if (p + u > son) break; sb.Append(Encoding.UTF8.GetString(c, p, u)); p += u; } return sb.ToString(); }

    // Sorgular: önce UDP, kesilmişse TCP; her sistem sunucusunu sırayla dener.
    public static async Task<List<DnsKaydi>> SorgulaAsync(string ad, ushort tur, CancellationToken ct)
    {
        // Rastgele kimlik.
        ushort id = (ushort)Random.Shared.Next(1, 65535); var paket = Paket(ad, tur, id); Exception? son = null;
        // Her sunucu.
        foreach (var s in Sunucular().Take(3))
        {
            // Hata olursa sonrakini dene.
            try
            {
                // UDP.
                using var udp = new UdpClient(AddressFamily.InterNetwork); await udp.SendAsync(paket, paket.Length, new IPEndPoint(s, 53));
                var al = udp.ReceiveAsync(ct).AsTask(); if (await Task.WhenAny(al, Task.Delay(3000, ct)) != al) throw new TimeoutException("DNS zaman aşımı");
                var cevap = (await al).Buffer; var liste = Coz(cevap, id, out bool kesik); if (!kesik) return liste;
                // Kesildiyse TCP.
                using var tcp = new TcpClient(); await tcp.ConnectAsync(s, 53, ct); var ns = tcp.GetStream(); await ns.WriteAsync(new byte[] { (byte)(paket.Length >> 8), (byte)paket.Length }.Concat(paket).ToArray(), ct);
                var uz = new byte[2]; await ns.ReadExactlyAsync(uz, ct); var buf = new byte[(uz[0] << 8) | uz[1]]; await ns.ReadExactlyAsync(buf, ct); return Coz(buf, id, out _);
            }
            catch (OperationCanceledException) { throw; } catch (Exception ex) { son = ex; }
        }
        // Hiçbiri çalışmadıysa.
        throw new InvalidOperationException("DNS sorgusu yapılamadı" + (son != null ? ": " + son.Message : " (sistem DNS sunucusu yok)"));
    }

    // Kurumsal (apex) alan adını tahmin eder: SPF/DMARC kayıtları genelde burada durur. (Tam "public suffix" listesi değil; yaygın ikili uzantılar kapsanır.)
    private static readonly HashSet<string> IkiSeviye = new(StringComparer.OrdinalIgnoreCase) { "com.tr", "net.tr", "org.tr", "gov.tr", "edu.tr", "k12.tr", "gen.tr", "biz.tr", "info.tr", "bel.tr", "av.tr", "dr.tr", "pol.tr", "name.tr", "tv.tr", "web.tr", "co.uk", "org.uk", "ac.uk", "gov.uk", "com.au", "net.au", "org.au", "co.jp", "com.br", "co.nz", "co.za", "com.cn" };
    public static string? OrgAlani(string host)
    {
        // IP için yok.
        if (IPAddress.TryParse(host, out _)) return null;
        // Etiketler.
        var e = host.Trim('.').Split('.'); if (e.Length <= 2) return string.Join(".", e);
        // İkili uzantı (örn. com.tr) ise son üç etiket.
        return IkiSeviye.Contains(e[^2] + "." + e[^1]) ? string.Join(".", e[^3..]) : string.Join(".", e[^2..]);
    }
}
