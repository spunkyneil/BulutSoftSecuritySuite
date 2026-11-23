// Bu dosya DIŞ AĞ ÇIKIŞLARININ TEK MERKEZİDİR: Anthropic, CISA KEV, IP sızıntı testi servisleri, pwnedpasswords, bildirim webhook'ları ve zaman damgası sunucuları buradan geçer.
// Her profilin AÇIK bir bağlantı ve toplam zaman aşımı vardır; istemciler paylaşımlıdır (soket tükenmesi ve her istekte yeni bağlantı yok); çağıranın CancellationToken'ı desteklenir;
// internet kesik, DNS/TLS sorunu ya da sunucu yanıtsız olduğunda arayüzü kilitlemeden anlaşılır TÜRKÇE hata (AgHatasi) fırlatılır.
// Yerel/özel ağ hedeflerine yönelik tarama istemcileri (AktifHttp, HttpDenetimi...) kendi özel işleyicilerini kullanır; onların zaman aşımları rapordaki tabloda ayrıca listelenir.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;

// Programımızın adı.
namespace LabDenetim;

// Dış servis profilleri.
internal enum AgProfili { ClaudeAnaliz, KevBeslemesi, IpSizintisi, ParolaSizintisi, Webhook, ZamanDamgasi }

// Bir profilin ayarı: servis adı (hata mesajı için), bağlantı ve toplam (başlıklar/gövde dahil) zaman aşımı, yönlendirme izlensin mi.
internal sealed record AgProfilAyari(string Servis, TimeSpan Baglanti, TimeSpan Toplam, bool Yonlendirme);

// Hata türü.
internal enum AgHatasiTuru { ZamanAsimi, BaglantiYok, Dns, Tls, HttpDurumu, Beklenmeyen }

// Anlaşılır Türkçe mesajlı ağ hatası (iç istisna korunur).
internal sealed class AgHatasi : Exception
{
    public AgHatasiTuru Tur { get; }
    public AgProfili Profil { get; }
    public AgHatasi(AgHatasiTuru tur, AgProfili profil, string mesaj, Exception? ic = null) : base(mesaj, ic) { Tur = tur; Profil = profil; }
}

internal static class AgIstemciFabrikasi
{
    // KESİN ZAMAN AŞIMI TABLOSU (raporda da yayımlanır; testler bu tabloya karşı doğrular).
    public static AgProfilAyari Ayar(AgProfili p) => p switch
    {
        AgProfili.ClaudeAnaliz => new("Claude (Anthropic)", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), false),   // 60 sn = YANIT BAŞLIĞI süresi; akış sürerken parça arası boşta kalma sınırı ClaudeAkisBostaSure
        AgProfili.KevBeslemesi => new("CISA KEV beslemesi", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60), false),
        AgProfili.IpSizintisi => new("IP sızıntı testi servisi", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8), true),
        AgProfili.ParolaSizintisi => new("pwnedpasswords", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10), false),
        AgProfili.Webhook => new("Bildirim kanalı", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(12), false),
        AgProfili.ZamanDamgasi => new("Zaman damgası sunucusu", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10), false),
        _ => new("Dış servis", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10), false)
    };

    // Claude akışında iki veri parçası arasında beklenebilecek en uzun süre (aşılırsa akış kesilir).
    public static TimeSpan ClaudeAkisBostaSure => TimeSpan.FromTicks((long)(TimeSpan.FromSeconds(90).Ticks * ZamanCarpani));

    // Sınama kancaları: sahte işleyici ve zaman aşımı çarpanı (üretimde atanmaz).
    internal static Func<AgProfili, HttpMessageHandler?>? TestIsleyicisi { get; set; }
    internal static double ZamanCarpani { get; set; } = 1.0;
    private static TimeSpan Toplam(AgProfili p) => TimeSpan.FromTicks((long)(Ayar(p).Toplam.Ticks * ZamanCarpani));

    private static readonly Dictionary<AgProfili, HttpClient> Istemciler = new(); private static readonly object Kilit = new();

    // Profilin paylaşımlı HttpClient'ı (zaman aşımı istek başına CancellationTokenSource ile uygulanır; HttpClient.Timeout sonsuzdur).
    private static HttpClient Istemci(AgProfili p, HttpMessageHandler? ozel, out bool paylasimli)
    {
        if (ozel != null) { paylasimli = false; return new HttpClient(ozel, false) { Timeout = Timeout.InfiniteTimeSpan }; }   // çağıranın verdiği (çoğunlukla sınama) işleyici
        var sahte = TestIsleyicisi?.Invoke(p); if (sahte != null) { paylasimli = false; return new HttpClient(sahte, false) { Timeout = Timeout.InfiniteTimeSpan }; }
        paylasimli = true;
        lock (Kilit)
        {
            if (Istemciler.TryGetValue(p, out var c)) return c;
            var a = Ayar(p);
            var h = new SocketsHttpHandler { AllowAutoRedirect = a.Yonlendirme, UseCookies = false, UseProxy = true, ConnectTimeout = a.Baglanti, PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
            c = new HttpClient(h, true) { Timeout = Timeout.InfiniteTimeSpan }; c.DefaultRequestHeaders.UserAgent.ParseAdd("BulutSoft-SecuritySuite/1.0");   // kişisel/makine bilgisi içermeyen genel istemci adı
            return Istemciler[p] = c;
        }
    }

    // İsteği gönderir. Zaman aşımı = profilin toplam süresi (ResponseHeadersRead ise yalnızca yanıt başlıklarına kadar); ct iptali OperationCanceledException olarak aynen iletilir; diğer hatalar AgHatasi olur.
    public static async Task<HttpResponseMessage> GonderAsync(AgProfili profil, HttpRequestMessage istek, HttpCompletionOption secenek = HttpCompletionOption.ResponseContentRead, HttpMessageHandler? ozelIsleyici = null, CancellationToken ct = default)
    {
        if (istek.Headers.UserAgent.Count == 0) istek.Headers.UserAgent.ParseAdd("BulutSoft-SecuritySuite/1.0");   // kişisel/makine bilgisi içermeyen genel istemci adı (özel işleyicide de aynı)
        var c = Istemci(profil, ozelIsleyici, out bool paylasimli); using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(ct); zamanAsimi.CancelAfter(Toplam(profil));
        try { return await c.SendAsync(istek, secenek, zamanAsimi.Token).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not AgHatasi) { ct.ThrowIfCancellationRequested(); throw Cevir(ex, profil, zamanAsimi.IsCancellationRequested); }
        finally { if (!paylasimli) c.Dispose(); }
    }

    // GET ile metin okur; 2xx dışı AgHatasi(HttpDurumu). baslik: ek istek başlıkları.
    public static async Task<string> MetinAlAsync(AgProfili profil, string url, Action<HttpRequestHeaders>? baslik = null, CancellationToken ct = default)
    {
        using var istek = new HttpRequestMessage(HttpMethod.Get, url); baslik?.Invoke(istek.Headers);
        using var yanit = await GonderAsync(profil, istek, ct: ct).ConfigureAwait(false);
        if (!yanit.IsSuccessStatusCode) throw DurumHatasi(profil, (int)yanit.StatusCode);
        return await yanit.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    // POST (JSON/ikili içerik) gönderir; 2xx dışı DÖNDÜRÜLÜR (çağıran kodu inceler), ağ hataları AgHatasi olur. Yanıt çağıranda atılmalıdır.
    public static Task<HttpResponseMessage> YollaAsync(AgProfili profil, string url, HttpContent icerik, CancellationToken ct = default)
    {
        var istek = new HttpRequestMessage(HttpMethod.Post, url) { Content = icerik };
        return GonderAsync(profil, istek, ct: ct);
    }

    // HTTP durum kodu hatası.
    public static AgHatasi DurumHatasi(AgProfili profil, int kod) => new(AgHatasiTuru.HttpDurumu, profil, $"{Ayar(profil).Servis} hata döndürdü (HTTP {kod}).");

    // İstisnayı anlaşılır Türkçe AgHatasi'na çevirir. sureDoldu: bizim zaman aşımımız mı tetiklendi.
    public static AgHatasi Cevir(Exception ex, AgProfili profil, bool sureDoldu = false)
    {
        string s = Ayar(profil).Servis; int sn = (int)Math.Ceiling(Toplam(profil).TotalSeconds);
        if (sureDoldu || ex is TimeoutException || ex is TaskCanceledException || ex is OperationCanceledException)
            return new(AgHatasiTuru.ZamanAsimi, profil, $"{s} {sn} saniye içinde yanıt vermedi. İnternet bağlantınızı kontrol edin; sorun sürerse biraz sonra yeniden deneyin.", ex);
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is AuthenticationException) return new(AgHatasiTuru.Tls, profil, $"{s} ile güvenli bağlantı kurulamadı (sertifika/TLS). Sistem saatinizi ve ağ güvenlik yazılımınızı kontrol edin.", ex);
            if (e is SocketException se)
                return se.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData
                    ? new(AgHatasiTuru.Dns, profil, $"{s} adresi çözülemedi (DNS). İnternet bağlantınızı veya DNS ayarınızı kontrol edin.", ex)
                    : se.SocketErrorCode == SocketError.TimedOut
                        ? new(AgHatasiTuru.ZamanAsimi, profil, $"{s} ile bağlantı zaman aşımına uğradı. İnternet bağlantınızı kontrol edin.", ex)
                        : new(AgHatasiTuru.BaglantiYok, profil, $"{s} ile bağlantı kurulamadı. İnternet bağlantınızı kontrol edin (kurumsal ağda güvenlik duvarı ya da proxy engelliyor olabilir).", ex);
        }
        if (ex is HttpRequestException) return new(AgHatasiTuru.BaglantiYok, profil, $"{s} ile bağlantı kurulamadı. İnternet bağlantınızı kontrol edin.", ex);
        return new(AgHatasiTuru.Beklenmeyen, profil, $"{s} ile iletişimde beklenmeyen bir sorun oluştu: {ex.Message}", ex);
    }

    // Test/tanılama: paylaşımlı istemcileri bırakır.
    internal static void Sifirla() { lock (Kilit) { foreach (var c in Istemciler.Values) c.Dispose(); Istemciler.Clear(); } }
}
