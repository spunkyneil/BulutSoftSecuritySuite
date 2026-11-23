// Bu dosya "Aktif Web Denetimi"dir: YALNIZCA İÇ AĞ hedeflerinde çalışan, kontrollü sondalar gönderen HTTP/HTTPS denetçisi.
// Kapsam kapısı dış ağ hedeflerinde bu modülü KOD SEVİYESİNDE kilitler (dış ağda yalnızca pasif gözlem vardır).
// Güvenlik ilkeleri: yalnızca GET, OPTIONS ve TRACE gönderilir (PUT/DELETE/POST ASLA gönderilmez; yalnızca OPTIONS'taki Allow duyurusu okunur);
// gövde saklanmaz (kanıt olarak yalnızca durum kodu, boyut ve "içerik imzası uyuyor mu" bilgisi tutulur); hız sınırı ~5 istek/sn; en çok 300 istek;
// yönlendirmeler izlenmez; bağlantı anında hedefin özel ağda kaldığı yeniden doğrulanır.
// Yanlış alarm savunması: sunucu her adrese 200 dönüyorsa ("soft 404") sayfa, rastgele bir adresin cevabıyla karşılaştırılır; aynıysa "yok" sayılır.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Aktif denetimin ham gözlemi: pasif olgulara ek olarak "probe:*" olguları.
internal sealed record AktifGozlem(string Host, string Url, DateTime Zaman, int IstekSayisi, HttpGozlem? AnaSayfa, Dictionary<string, string?> Olgular, List<string> Notlar);

// Aktif HTTP denetçisi.
internal static class AktifHttp
{
    // Sınırlar.
    public const int EnCokIstek = 300, VarsayilanHizMs = 200, GovdeLimit = 65536;
    // Dürüst kimlik.
    private const string Kimlik = "BulutSoft-AktifDenetim/1.0 (yalnizca-ic-ag-yetkili-denetim)";
    // Sahte, hiçbir yerde tanımlı olmayan kaynak adresi (CORS sondası için; ".invalid" alan adı asla çözümlenmez).
    private const string SahteOrigin = "https://bulutsoft-sonda.invalid";

    // Hassas yol listesi: (yol, tür). Tür içerik doğrulamasını belirler.
    private static readonly (string Yol, string Tur)[] HassasYollar =
    {
        ("/.git/HEAD", "git"), ("/.env", "env"), ("/phpinfo.php", "phpinfo"), ("/server-status", "status"), ("/.DS_Store", "dsstore"), ("/admin", "admin"),
        ("/backup.zip", "dosya"), ("/backup.sql", "dosya"), ("/db.sql", "dosya"), ("/dump.sql", "dosya"), ("/config.php.bak", "dosya"), ("/web.config.bak", "dosya"), ("/site.tar.gz", "dosya")
    };
    // Dizin listeleme için bakılan klasörler.
    private static readonly string[] Klasorler = { "/", "/uploads/", "/files/", "/backup/", "/images/" };
    // Sürüm sızıntısı ve hata dökümü kalıpları.
    private static readonly Regex SurumKalibi = new(@"(Apache|nginx|Microsoft-IIS|PHP|Tomcat|Jetty|lighttpd|OpenSSL|Express)[/ (]+\d+(\.\d+)+", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex DokumKalibi = new(@"(Stack trace:|Traceback \(most recent call last\)|\bat [A-Za-z0-9_.$]+\([^)]*\) in |Exception in thread|System\.[A-Za-z.]+Exception|org\.apache\.[A-Za-z.]+Exception)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex DizinKalibi = new(@"<title>Index of /|<h1>Index of /|Directory listing for /|\[To Parent Directory\]", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    // Yalnızca özel ağ adreslerine bağlanan işleyici (bağlantı anında yeniden doğrulama: DNS değişse bile genel internete çıkılamaz).
    // dis=true: SAHİPLİĞİ DOĞRULANMIŞ internet sitesi (yalnızca GENEL adreslere bağlanır); false: iç ağ (yalnızca özel adreslere bağlanır).
    private static SocketsHttpHandler GuvenliIsleyici(bool dis = false)
    {
        var h = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(6), PooledConnectionLifetime = TimeSpan.FromSeconds(20), AutomaticDecompression = DecompressionMethods.None, MaxResponseHeadersLength = 64 };
        h.ConnectCallback = async (ctx, ct) =>
        {
            // Çözülen TÜM adresler beklenen ağda olmalı (iç ağ kipinde özel, doğrulanmış dış kipte genel).
            var adr = dis ? await Kapsam.CozGenelAsync(ctx.DnsEndPoint.Host, ct) : IPAddress.TryParse(ctx.DnsEndPoint.Host, out var ip) ? new[] { ip } : await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
            if (adr.Length == 0 || (!dis && !adr.All(LabKapsam.IpLabdaMi))) throw new HttpRequestException("Hedef beklenen ağın dışına çözüldü; bağlantı reddedildi.");
            var s = new Socket(adr[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try { await s.ConnectAsync(new IPEndPoint(adr[0], ctx.DnsEndPoint.Port), ct); return new NetworkStream(s, true); } catch { s.Dispose(); throw; }
        };
        // Sertifika hatası burada engel değildir (iç ağda kendi imzalı sertifika yaygın; başlıklar okunur, veri gönderilmez).
        h.SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true };
        return h;
    }

    // İçerik imzasının özeti (soft-404 karşılaştırması için; içerik saklanmaz).
    private static string Ozet(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Regex.Replace(s.Length > 1024 ? s[..1024] : s, @"\s+", " ")))).Substring(0, 16);

    // Denetimi yürütür. isleyici: testte sahte sunucu verilir. hizMs: istekler arası bekleme (varsayılan 200 ms ≈ 5 istek/sn).
    public static async Task<AktifGozlem> GozlemAsync(string url, HttpMessageHandler? isleyici = null, Action<string>? ilerleme = null, int hizMs = VarsayilanHizMs, CancellationToken ct = default)
    {
        // Adres biçimi: yalnızca http/https, kullanıcı bilgisi yok.
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || u.UserInfo.Length > 0) throw new InvalidOperationException("Geçersiz adres (http:// veya https:// ile başlamalı).");
        // KAPSAM KAPISI: yalnızca iç ağ. Dış ağ hedefinde HİÇBİR İSTEK gönderilmeden durur.
        var karar = await Kapsam.SiniflaAsync(u.Host, ct);
        // İSTİSNA: dış ağ hedefi, yalnızca alan adı sahipliği son 60 dakikada CANLI doğrulandıysa (dosya/DNS kanıtı) ve hız yavaşlatılarak denetlenebilir.
        bool dogrulanmisDis = karar.Mod == KapsamModu.DisAgPasif && SahiplikDogrulama.OturumDogrulu(karar.Host);
        if (karar.Mod != KapsamModu.IcAg && !dogrulanmisDis) throw new KapsamIhlaliException("Aktif web denetimi yalnızca iç ağ hedefleri ya da sahipliği doğrulanmış sitelerdir (aksi halde dış ağda yalnızca pasif gözlem yapılır): " + karar.Neden);
        if (!dogrulanmisDis) Kapsam.KilitliyseDur(karar.Mod, ModulTuru.IcHttp);
        // Doğrulanmış dış sitede istekler yavaşlatılır (en az 500 ms) ve yük bindirmemek için gösterilen hız sınırı korunur.
        if (dogrulanmisDis && hizMs < 500) hizMs = 500;
        // Kök adres.
        string kok = u.GetLeftPart(UriPartial.Authority); var o = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase); var notlar = new List<string>();
        int istek = 0;
        using var c = new HttpClient(isleyici ?? GuvenliIsleyici(dogrulanmisDis), isleyici == null) { Timeout = TimeSpan.FromSeconds(12) }; c.DefaultRequestHeaders.UserAgent.ParseAdd(dogrulanmisDis ? "BulutSoft-AktifDenetim/1.0 (site-sahibi-dogrulamali-denetim)" : Kimlik);

        // Tek istek: sayaç, hız sınırı, gövde sınırlı okuma (saklanmaz), hata → Kod=0.
        async Task<(int Kod, Dictionary<string, string> Baslik, List<string> Cerez, string Govde, string Konum)> Iste(HttpMethod m, string yol, Dictionary<string, string>? ek = null)
        {
            if (++istek > EnCokIstek) throw new InvalidOperationException("Aktif denetim istek sınırına (" + EnCokIstek + ") ulaştı; kalan sondalar atlandı.");
            if (istek > 1 && hizMs > 0) await Task.Delay(hizMs, ct);
            // Yalnızca izinli yöntemler (savunma amaçlı ikinci kontrol).
            if (m != HttpMethod.Get && m != HttpMethod.Options && m != HttpMethod.Trace) throw new InvalidOperationException("İzin verilmeyen yöntem: " + m);
            using var rq = new HttpRequestMessage(m, kok + yol); if (ek != null) foreach (var (k, v) in ek) rq.Headers.TryAddWithoutValidation(k, v);
            try
            {
                using var y = await c.SendAsync(rq, HttpCompletionOption.ResponseHeadersRead, ct);
                var b = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); foreach (var h in y.Headers.Concat(y.Content.Headers)) b[h.Key.ToLowerInvariant()] = string.Join(", ", h.Value);
                var cerez = y.Headers.TryGetValues("Set-Cookie", out var cv) ? cv.Take(30).ToList() : new List<string>();
                string govde = ""; if (m == HttpMethod.Get || m == HttpMethod.Trace) { await using var st = await y.Content.ReadAsStreamAsync(ct); var buf = new byte[GovdeLimit]; int n = 0, r; while (n < buf.Length && (r = await st.ReadAsync(buf.AsMemory(n), ct)) > 0) n += r; govde = Encoding.UTF8.GetString(buf, 0, n); }
                return ((int)y.StatusCode, b, cerez, govde, y.Headers.Location?.ToString() ?? "");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { notlar.Add($"{m} {yol}: istek başarısız ({ex.GetType().Name})."); return (0, new(), new(), "", ""); }
        }

        // 1) Ana sayfa (pasif başlık kuralları için) — başarısızsa denetim anlamsızdır.
        ilerleme?.Invoke("Ana sayfa okunuyor...");
        var ana = await Iste(HttpMethod.Get, "/"); if (ana.Kod == 0) throw new InvalidOperationException("Hedefe bağlanılamadı: " + (notlar.LastOrDefault() ?? u.Host));
        var gozlem = new HttpGozlem(kok + "/", ana.Kod, ana.Baslik, ana.Cerez, ana.Konum.Length > 0 ? new List<string> { $"{ana.Kod} → {ana.Konum}" } : new(), "bilinmiyor");
        // Sürüm bilgisi veren başlıklar.
        var surumler = new List<string>(); foreach (var ad in new[] { "server", "x-powered-by", "x-aspnet-version" }) if (ana.Baslik.TryGetValue(ad, out var hv) && Regex.IsMatch(hv, @"\d+\.\d+")) surumler.Add(hv);
        if (surumler.Count > 0) o["probe:server_version"] = string.Join("; ", surumler.Distinct());

        // 2) Soft-404 tabanı: var olmayan rastgele adres.
        ilerleme?.Invoke("Hata sayfası ve taban cevap inceleniyor...");
        var yok = await Iste(HttpMethod.Get, "/bulutsoft-" + Guid.NewGuid().ToString("N")[..10] + "-yok"); bool soft404 = yok.Kod == 200; string tabanOzet = soft404 ? Ozet(yok.Govde) : "";
        if (soft404) notlar.Add("Sunucu var olmayan adreslere de 200 dönüyor (soft 404): sayfa içeriği tabanla karşılaştırılarak değerlendirildi.");
        if (yok.Kod != 0)
        {
            var sm = SurumKalibi.Match(yok.Govde); if (sm.Success) o["probe:error_version"] = sm.Value; if (DokumKalibi.IsMatch(yok.Govde)) o["probe:error_stacktrace"] = "var";
        }

        // 3) OPTIONS: yalnızca Allow duyurusu okunur (PUT/DELETE GÖNDERİLMEZ).
        ilerleme?.Invoke("İzin verilen yöntemler okunuyor (OPTIONS)...");
        var op = await Iste(HttpMethod.Options, "/");
        if (op.Kod != 0 && op.Baslik.TryGetValue("allow", out var allow)) { o["probe:options_allow"] = allow; var yontemler = allow.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => x.ToUpperInvariant()).ToHashSet(); o["probe:method_put"] = yontemler.Contains("PUT") ? "true" : "false"; o["probe:method_delete"] = yontemler.Contains("DELETE") ? "true" : "false"; }

        // 4) TRACE: cevap isteği geri yansıtıyor mu?
        var tr = await Iste(HttpMethod.Trace, "/"); o["probe:trace_enabled"] = tr.Kod == 200 && tr.Govde.Contains("TRACE /", StringComparison.Ordinal) ? "true" : "false";

        // 5) CORS: sahte Origin yansıtılıyor mu?
        ilerleme?.Invoke("CORS davranışı denetleniyor...");
        var co = await Iste(HttpMethod.Get, "/", new() { ["Origin"] = SahteOrigin });
        bool yansir = co.Baslik.TryGetValue("access-control-allow-origin", out var acao) && string.Equals(acao.Trim(), SahteOrigin, StringComparison.Ordinal);
        o["probe:cors_reflect"] = yansir ? "true" : "false"; o["probe:cors_reflect_credentials"] = yansir && co.Baslik.TryGetValue("access-control-allow-credentials", out var acac) && string.Equals(acac.Trim(), "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";

        // 6) Hassas yollar: yalnızca varlık ve içerik imzası; içerik saklanmaz.
        var yedekler = new List<string>();
        foreach (var (yol, tur) in HassasYollar)
        {
            ilerleme?.Invoke("Hassas yol denetleniyor: " + yol);
            var r = await Iste(HttpMethod.Get, yol); string durum = "absent";
            if (r.Kod is 401 or 403) durum = "auth";
            else if (r.Kod == 200 && !(soft404 && Ozet(r.Govde) == tabanOzet))
            {
                string tip = r.Baslik.GetValueOrDefault("content-type", "").ToLowerInvariant(); long uzun = long.TryParse(r.Baslik.GetValueOrDefault("content-length"), out var l) ? l : r.Govde.Length;
                bool tamam = tur switch
                {
                    "git" => r.Govde.TrimStart().StartsWith("ref:", StringComparison.Ordinal),
                    "env" => Regex.IsMatch(r.Govde, @"(?m)^\s*[A-Za-z_][A-Za-z0-9_]*\s*=", RegexOptions.None, TimeSpan.FromMilliseconds(200)),
                    "phpinfo" => r.Govde.Contains("PHP Version", StringComparison.Ordinal),
                    "status" => r.Govde.Contains("Apache Server Status", StringComparison.OrdinalIgnoreCase) || r.Govde.Contains("Server Version:", StringComparison.Ordinal),
                    "dsstore" => r.Govde.Contains("Bud1", StringComparison.Ordinal),
                    "admin" => tip.Contains("html") || r.Govde.Length > 0,
                    "dosya" => !tip.Contains("html") && uzun > 0,
                    _ => false
                };
                if (tamam) { durum = "found"; if (tur == "dosya") yedekler.Add($"{yol} ({uzun} bayt)"); }
            }
            o["probe:path:" + yol] = durum;
        }
        if (yedekler.Count > 0) o["probe:backup_found"] = string.Join(", ", yedekler);

        // 7) Dizin listeleme.
        var listeler = new List<string>();
        foreach (var k in Klasorler)
        {
            ilerleme?.Invoke("Dizin listeleme denetleniyor: " + k);
            var r = k == "/" ? ana : await Iste(HttpMethod.Get, k); if (r.Kod == 200 && DizinKalibi.IsMatch(r.Govde) && !(soft404 && Ozet(r.Govde) == tabanOzet)) listeler.Add(k);
        }
        if (listeler.Count > 0) o["probe:dir_listing"] = string.Join(", ", listeler);

        o["probe:requests"] = istek.ToString(); o["probe:soft404"] = soft404 ? "true" : "false";
        return new AktifGozlem(u.Host, kok + "/", DateTime.UtcNow, istek, gozlem, o, notlar);
    }

    // Pasif kural dosyasından yalnızca HTTP başlık/çerez/CORS kuralları (iç ağ hedefinde DNS/TLS/security.txt anlamsızdır).
    private static bool HttpKurali(PasifKural k) => k.Fact.StartsWith("header:") || k.Fact is "frame_protection" or "cors_wildcard" or "cors_wildcard_credentials" or "hsts_max_age" or "cookie";

    // Gözlemi kurallara uygular (pasif başlık kuralları + aktif sonda kuralları).
    public static List<PasifBulgu> Degerlendir(AktifGozlem g, List<PasifKural>? aktif = null, List<PasifKural>? pasif = null, DateTime? simdi = null)
    {
        aktif ??= PasifKurallar.Yukle(out _, null, "aktif_kurallar.json", BulutKlasor.Dosya("aktif_kurallar.json"));
        pasif ??= PasifKurallar.Yukle(out _);
        var l = new List<PasifBulgu>();
        if (g.AnaSayfa != null) l.AddRange(PasifKurallar.Degerlendir(new PasifGozlem(g.Host, g.Zaman, "Windows (iç ağ)", g.AnaSayfa, null, null, SecurityTxt.Yok(), g.Notlar), pasif.Where(HttpKurali).ToList(), simdi));
        l.AddRange(PasifKurallar.DegerlendirOlgular(g.Olgular, aktif));
        return l.OrderByDescending(x => x.Etki).ThenBy(x => x.RuleId).ToList();
    }

    // Kural bulgularını şemaya uyan Finding'e çevirir (varlık kimliği: web:host:port).
    public static List<Finding> BulguUret(AktifGozlem g, IEnumerable<PasifBulgu> bulgular, double varlikAgirligi, DateTime simdi)
    {
        var u = new Uri(g.Url); string varlik = $"web:{g.Host}:{u.Port}";
        return bulgular.Select(b => BulguFabrikasi.Yap(b.RuleId, b.Modul, varlik, b.RuleId + "|" + b.Kanit, b.Baslik, b.Ne, b.Neden, new[] { b.Kanit, "Adres: " + g.Url },
            b.Etki, varlikAgirligi, b.Iso.Length > 0 ? b.Iso : new[] { "A.8.9" }, b.Mitre, b.Cozum + (string.IsNullOrWhiteSpace(b.Komut) ? "" : "\nÖrnek: " + b.Komut), KontrolKimligi, simdi)).ToList();
    }

    // Doğrulama kontrol kimliği (aynı kimliği tüm aktif HTTP bulguları paylaşır).
    public const string KontrolKimligi = "CHK-HTTP-ACTIVE";
}

// Aktif HTTP bulguları için doğrulama kontrolü: denetimi yeniden çalıştırır, bulgu hâlâ üretiliyor mu bakar.
internal sealed class AktifHttpDogrulamaKontrolu : IVerificationCheck
{
    private readonly Func<CancellationToken, Task<List<Finding>>> _yenidenCalistir;
    public AktifHttpDogrulamaKontrolu(Func<CancellationToken, Task<List<Finding>>> yenidenCalistir) { _yenidenCalistir = yenidenCalistir; }
    public string Id => AktifHttp.KontrolKimligi;
    public async Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct)
    {
        var simdiki = (await _yenidenCalistir(ct)).Select(x => x.Id).ToHashSet();
        return referans.ToDictionary(f => f.Id, f => simdiki.Contains(f.Id) ? VerificationOutcome.Fail : VerificationOutcome.Pass);
    }
}
