// Bu dosya DIŞ AĞ PASİF GÖZLEMİ bu bilgisayardan (yerelden) yapar. Yalnızca standart bir tarayıcının yapacağı şeyleri yapar:
//   • Ana sayfaya bir GET isteği (gövde okunmaz, sadece başlıklar), http:// adresine bir GET (yönlendirme var mı diye), security.txt için en çok 2 GET,
//   • bir TLS el sıkışması (sertifikayı okumak için), • sistem DNS'ine TXT/MX/NS/CAA sorguları.
// KİLİTLER: port taraması, form/yük gönderme, kaba kuvvet YOKTUR. Toplam HTTP isteği en çok 8. Yönlendirmeler elle izlenir (yalnızca AYNI ana bilgisayara, en çok 3).
// Bağlantı anında hedef adres YENİDEN doğrulanır (Kapsam.CozGenelAsync): gözlem sırasında DNS iç ağa çevrilse bile bağlanılmaz (DNS yeniden bağlama savunması).
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

// Programımızın adı.
namespace LabDenetim;

// Yerel pasif toplayıcı.
internal static class PasifToplayici
{
    // En çok HTTP isteği.
    public const int EnCokIstek = 8;
    // Tarayıcı benzeri, dürüst kimlik (kim olduğunu saklamaz).
    private const string Kimlik = "Mozilla/5.0 (compatible; BulutSoft-PasifGozlem/1.0; yalnizca-pasif-gozlem)";

    // İstek sayacı (tek gözlem için).
    internal sealed class Sayac { public int N; public void Al() { if (++N > EnCokIstek) throw new InvalidOperationException("Pasif gözlem istek sınırına (" + EnCokIstek + ") ulaşıldı; kalan kontroller atlandı."); } }

    // Bağlantı anında yeniden doğrulayan HTTP istemcisi.
    private static HttpClient IstemciYap()
    {
        // Kilitli işleyici: proxy yok, otomatik yönlendirme yok, çerez saklama yok, sıkı zaman aşımı.
        var h = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(8), PooledConnectionLifetime = TimeSpan.FromSeconds(20), AutomaticDecompression = DecompressionMethods.None, MaxResponseHeadersLength = 32 };
        // Bağlantı: adresleri yeniden doğrula ve yalnızca genel adrese bağlan.
        h.ConnectCallback = async (ctx, ct) =>
        {
            // Genel adresler.
            var adr = await Kapsam.CozGenelAsync(ctx.DnsEndPoint.Host, ct); Exception? son = null;
            // Sırayla dene.
            foreach (var a in adr.Take(4)) { var s = new Socket(a.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true }; try { await s.ConnectAsync(new IPEndPoint(a, ctx.DnsEndPoint.Port), ct); return new NetworkStream(s, true); } catch (Exception ex) when (ex is not OperationCanceledException) { s.Dispose(); son = ex; } }
            // Hiçbiri olmadı.
            throw new HttpRequestException("Bağlanılamadı: " + son?.Message);
        };
        // Sertifika hatası gözleme engel değil (TLS ayrıca incelenir) ama burada güvenmek için değil, başlıkları okumak için kabul edilir; veri gönderilmez.
        h.SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true };
        // İstemci.
        var c = new HttpClient(h, true) { Timeout = TimeSpan.FromSeconds(15) }; c.DefaultRequestHeaders.UserAgent.ParseAdd(Kimlik); c.DefaultRequestHeaders.Accept.ParseAdd("text/html,text/plain;q=0.9,*/*;q=0.5"); return c;
    }

    // Tek GET (gövde yalnızca istenirse ve sınırlı okunur); yönlendirmeleri elle, aynı ana bilgisayara izler.
    private static async Task<(HttpResponseMessage? Yanit, string Govde, List<string> Yonler, string SonUrl)> GetAsync(HttpClient c, Sayac say, string url, string host, bool govdeOku, CancellationToken ct)
    {
        // Yönlendirme listesi.
        var yon = new List<string>(); string simdiki = url;
        // En çok 3 yönlendirme.
        for (int i = 0; i <= 3; i++)
        {
            // Sayaç ve istek.
            say.Al(); using var rq = new HttpRequestMessage(HttpMethod.Get, simdiki); var yanit = await c.SendAsync(rq, HttpCompletionOption.ResponseHeadersRead, ct);
            // Yönlendirme değilse bitir.
            int kod = (int)yanit.StatusCode; if (kod is < 300 or > 399 || yanit.Headers.Location == null)
            {
                // Gövde (en çok 32 KB).
                string govde = ""; if (govdeOku && kod == 200) { await using var st = await yanit.Content.ReadAsStreamAsync(ct); var buf = new byte[32768]; int n = 0, k; while (n < buf.Length && (k = await st.ReadAsync(buf.AsMemory(n), ct)) > 0) n += k; govde = System.Text.Encoding.UTF8.GetString(buf, 0, n); }
                return (yanit, govde, yon, simdiki);
            }
            // Hedef adres.
            var hedef = new Uri(new Uri(simdiki), yanit.Headers.Location); yon.Add($"{kod} → {hedef}"); yanit.Dispose();
            // Başka ana bilgisayara ya da http/https dışına gidilmez (izlenmez, yalnızca kaydedilir).
            if (!string.Equals(hedef.Host, host, StringComparison.OrdinalIgnoreCase) || hedef.Scheme is not ("http" or "https")) return (null, "", yon, simdiki);
            // Sonraki.
            simdiki = hedef.ToString();
        }
        // Çok yönlendirme.
        return (null, "", yon, simdiki);
    }

    // Çerez satırlarını alır.
    private static List<string> Cerezler(HttpResponseMessage y) => y.Headers.TryGetValues("Set-Cookie", out var v) ? v.Take(30).ToList() : new();

    // HTTP gözlemi.
    public static async Task<(HttpGozlem? Http, string HttpYon, List<string> Notlar)> HttpGozlemiAsync(string host, Sayac say, HttpClient c, CancellationToken ct)
    {
        // Notlar.
        var not = new List<string>();
        // Önce https.
        var (y, _, yon, son) = await GetAsync(c, say, "https://" + host + "/", host, false, ct); HttpGozlem? g = null;
        // Başlıklar.
        if (y != null) { var b = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); foreach (var h in y.Headers) b[h.Key.ToLowerInvariant()] = string.Join(", ", h.Value); foreach (var h in y.Content.Headers) b[h.Key.ToLowerInvariant()] = string.Join(", ", h.Value); g = new HttpGozlem(son, (int)y.StatusCode, b, Cerezler(y), yon, ""); y.Dispose(); }
        else not.Add("https:// ana sayfası okunamadı veya başka bir alan adına yönlendirildi" + (yon.Count > 0 ? " (" + yon[^1] + ")" : "") + ".");
        // http:// yönlendiriyor mu? (Tek istek, yönlendirme izlenmez.)
        string hy = "bilinmiyor";
        try
        {
            // İstek.
            say.Al(); using var rq = new HttpRequestMessage(HttpMethod.Head, "http://" + host + "/"); using var r = await c.SendAsync(rq, HttpCompletionOption.ResponseHeadersRead, ct);
            // Karar: 3xx ve Location https ise evet.
            int kod = (int)r.StatusCode; hy = kod is >= 300 and <= 399 && r.Headers.Location is { IsAbsoluteUri: true } l && l.Scheme == "https" ? "evet" : "hayir";
            // 80 portu cevap verip https'e gitmiyorsa "hayir"; bağlanamadıysa aşağıda "bilinmiyor" kalır.
        }
        catch (OperationCanceledException) { throw; } catch (KapsamIhlaliException) { throw; } catch { not.Add("http:// (80. port) cevap vermedi; yönlendirme kontrolü yapılamadı."); }
        // Sonuç.
        if (g != null) g = g with { HttpYonlendirme = hy };
        return (g, hy, not);
    }

    // Protokol adı.
    private static string ProtAdi(System.Security.Authentication.SslProtocols p) => p switch { System.Security.Authentication.SslProtocols.Tls13 => "TLS 1.3", System.Security.Authentication.SslProtocols.Tls12 => "TLS 1.2", System.Security.Authentication.SslProtocols.Tls11 => "TLS 1.1", System.Security.Authentication.SslProtocols.Tls => "TLS 1.0", _ => p.ToString() };

    // TLS gözlemi (yalnızca el sıkışma; veri gönderilmez).
    public static async Task<TlsGozlem?> TlsGozlemiAsync(string host, CancellationToken ct)
    {
        // Genel adresler (yeniden doğrulama) ve bağlantı.
        var adr = await Kapsam.CozGenelAsync(host, ct); using var tcp = new TcpClient(); using var zs = CancellationTokenSource.CreateLinkedTokenSource(ct); zs.CancelAfter(TimeSpan.FromSeconds(10)); await tcp.ConnectAsync(adr[0], 443, zs.Token);
        // Callback içinde okunan veriler.
        var hatalar = new List<string>(); bool isim = true; X509Certificate2? sertif = null;
        // SslStream.
        using var ssl = new SslStream(tcp.GetStream(), false, (_, cert, chain, err) =>
        {
            // Sertifika kopyası (callback sonrası geçersiz olmasın).
            if (cert != null) sertif = new X509Certificate2(cert);
            // Politika hataları.
            if (err.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)) isim = false; if (err.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable)) hatalar.Add("CertificateNotAvailable");
            // Zincir durumları (süre dolumu ayrı kuralla raporlanır).
            if (chain != null) foreach (var s in chain.ChainStatus) { var ad = s.Status.ToString(); if (ad is not ("NoError" or "NotTimeValid")) hatalar.Add(ad); }
            // Her durumda devam (yalnızca okuyoruz).
            return true;
        });
        // El sıkışma.
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = IPAddress.TryParse(host, out _) ? "" : host }, zs.Token);
        // Sertifika yoksa.
        if (sertif == null) return null;
        // Alt adlar (SAN).
        var san = new List<string>(); var ext = sertif.Extensions.OfType<X509Extension>().FirstOrDefault(e => e.Oid?.Value == "2.5.29.17"); if (ext != null) san = ext.Format(false).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Take(50).ToList();
        // Anahtar bilgisi.
        string tur = sertif.PublicKey.Oid.FriendlyName ?? ""; int bit = 0; try { using var rsa = sertif.GetRSAPublicKey(); if (rsa != null) bit = rsa.KeySize; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "PasifToplayici.TlsGozlemiAsync"); }
        // Sonuç.
        return new TlsGozlem(ProtAdi(ssl.SslProtocol), ssl.NegotiatedCipherSuite.ToString(), sertif.Subject, sertif.Issuer, sertif.NotBefore.ToUniversalTime(), sertif.NotAfter.ToUniversalTime(), hatalar.Distinct().ToList(), san, sertif.SignatureAlgorithm.FriendlyName ?? sertif.SignatureAlgorithm.Value ?? "", tur, bit, isim);
    }

    // DNS gözlemi (SPF, DMARC, MX, NS, CAA).
    public static async Task<DnsGozlem?> DnsGozlemiAsync(string host, CancellationToken ct)
    {
        // IP adreslerinde alan adı kayıtları olmaz.
        var org = DnsIstemci.OrgAlani(host); if (org == null) return null;
        // Sorgu yardımcısı: hata olursa boş liste (hata metni ayrıca tutulur).
        string hata = ""; async Task<List<DnsKaydi>> Q(string ad, ushort t) { try { return await DnsIstemci.SorgulaAsync(ad, t, ct); } catch (OperationCanceledException) { throw; } catch (Exception ex) { hata = ex.Message; return new(); } }
        // Sorgular.
        var txt = await Q(org, 16); var dm = await Q("_dmarc." + org, 16); var mx = await Q(org, 15); var ns = await Q(org, 2); var caa = await Q(org, 257);
        // Hiçbiri çalışmadıysa kullanılamadı.
        if (hata.Length > 0 && txt.Count == 0 && mx.Count == 0 && ns.Count == 0) return new DnsGozlem(org, new(), new(), null, null, new(), hata);
        // Sonuç.
        return new DnsGozlem(org, mx.Select(x => x.Veri).ToList(), ns.Select(x => x.Veri).ToList(), txt.Select(x => x.Veri).FirstOrDefault(v => v.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)), dm.Select(x => x.Veri).FirstOrDefault(v => v.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)), caa.Select(x => x.Veri).ToList(), hata);
    }

    // security.txt (RFC 9116): önce /.well-known/security.txt, sonra /security.txt.
    public static async Task<SecurityTxtSonucu> SecurityTxtAsync(string host, Sayac say, HttpClient c, CancellationToken ct)
    {
        // İki konum.
        foreach (var yol in new[] { "/.well-known/security.txt", "/security.txt" })
        {
            // Hata olursa sonrakine geç; istek sınırı hatası ise dur.
            try
            {
                // GET.
                var (y, govde, _, son) = await GetAsync(c, say, "https://" + host + yol, host, true, ct); if (y == null) continue;
                // Bulundu mu?
                using (y) { if ((int)y.StatusCode == 200 && govde.Contains("Contact", StringComparison.OrdinalIgnoreCase)) return SecurityTxt.Coz(govde, son, son.StartsWith("https", StringComparison.Ordinal), y.Content.Headers.ContentType?.ToString(), DateTime.UtcNow); }
            }
            catch (OperationCanceledException) { throw; } catch (KapsamIhlaliException) { throw; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "PasifToplayici.SecurityTxtAsync"); }
        }
        // Yok.
        return SecurityTxt.Yok();
    }

    // Tam gözlem. Önce kapsam kapısı: yalnızca DisAgPasif modu ve pasif modüllere izin verilir.
    public static async Task<PasifGozlem> GozlemYapAsync(KapsamKarari karar, Action<string>? ilerleme, CancellationToken ct)
    {
        // Mod kilidi (kod seviyesinde).
        foreach (var t in new[] { ModulTuru.PasifHttp, ModulTuru.PasifTls, ModulTuru.PasifDns, ModulTuru.SecurityTxt }) Kapsam.KilitliyseDur(karar.Mod, t); if (karar.Mod != KapsamModu.DisAgPasif) throw new KapsamIhlaliException("Pasif gözlem yalnızca dış ağ hedefleri içindir.");
        // Hazırlık.
        var notlar = new List<string>(); var say = new Sayac(); using var c = IstemciYap(); HttpGozlem? http = null; TlsGozlem? tls = null; DnsGozlem? dns = null; var sec = SecurityTxt.Yok();
        // 1) HTTP.
        ilerleme?.Invoke("HTTP başlıkları okunuyor…");
        try { var (g, _, n) = await HttpGozlemiAsync(karar.Host, say, c, ct); http = g; notlar.AddRange(n); } catch (OperationCanceledException) { throw; } catch (KapsamIhlaliException) { throw; } catch (Exception ex) { notlar.Add("HTTP gözlemi yapılamadı: " + ex.Message); }
        // 2) TLS.
        ilerleme?.Invoke("TLS sertifikası okunuyor…");
        try { tls = await TlsGozlemiAsync(karar.Host, ct); if (tls == null) notlar.Add("TLS sertifikası alınamadı."); } catch (OperationCanceledException) { throw; } catch (KapsamIhlaliException) { throw; } catch (Exception ex) { notlar.Add("TLS gözlemi yapılamadı (443. port kapalı olabilir): " + ex.Message); }
        // 3) DNS.
        ilerleme?.Invoke("DNS kayıtları (SPF/DMARC/CAA) okunuyor…");
        try { dns = await DnsGozlemiAsync(karar.Host, ct); if (dns?.Hata.Length > 0 && dns.Spf == null && dns.Dmarc == null) { notlar.Add("DNS sorguları yapılamadı: " + dns.Hata); dns = null; } } catch (OperationCanceledException) { throw; } catch (Exception ex) { notlar.Add("DNS gözlemi yapılamadı: " + ex.Message); }
        // 4) security.txt.
        ilerleme?.Invoke("security.txt aranıyor (RFC 9116)…");
        try { sec = await SecurityTxtAsync(karar.Host, say, c, ct); } catch (OperationCanceledException) { throw; } catch (KapsamIhlaliException) { throw; } catch (Exception ex) { notlar.Add("security.txt aranamadı: " + ex.Message); }
        // Sonuç.
        return new PasifGozlem(karar.Host, DateTime.UtcNow, "Bu bilgisayar", http, tls, dns, sec, notlar);
    }
}
