// Dış ağ çağrılarının dayanıklılık testleri (Faz 4): internet kesik / DNS / TLS / sunucu yanıtsız durumlarında çağrılar arayüzü kilitlemeden, anlaşılır Türkçe hata ve kesin zaman aşımıyla biter.
// Gerçek ağ KULLANILMAZ: sahte HttpMessageHandler ile (AgIstemciFabrikasi.TestIsleyicisi) ve küçültülmüş zaman çarpanıyla sınanır.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using LabDenetim.V2;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public sealed class AgDayanikliligiTestleri : IDisposable
{
    private readonly ITestOutputHelper _cikti;
    public AgDayanikliligiTestleri(ITestOutputHelper cikti) { _cikti = cikti; AgIstemciFabrikasi.ZamanCarpani = 0.02; }   // 8-60 sn → 0,16-1,2 sn
    public void Dispose() { AgIstemciFabrikasi.TestIsleyicisi = null; AgIstemciFabrikasi.ZamanCarpani = 1.0; AgIstemciFabrikasi.Sifirla(); }

    // Sahte sunucu davranışları.
    private sealed class SahteIsleyici : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _f; public SahteIsleyici(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) => _f = f;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => _f(r, ct);
    }
    private static void Kur(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) => AgIstemciFabrikasi.TestIsleyicisi = _ => new SahteIsleyici(f);
    private static async Task<HttpResponseMessage> Takil(HttpRequestMessage _, CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); return null!; }
    private static Task<HttpResponseMessage> Hata(SocketError kod) => throw new HttpRequestException("x", new SocketException((int)kod));

    [Fact]
    public void ZamanAsimiTablosu_KesinDegerler()
    {
        var beklenen = new (AgProfili, int, int)[] { (AgProfili.ClaudeAnaliz, 15, 60), (AgProfili.KevBeslemesi, 15, 60), (AgProfili.IpSizintisi, 6, 8), (AgProfili.ParolaSizintisi, 8, 10), (AgProfili.Webhook, 8, 12), (AgProfili.ZamanDamgasi, 8, 10) };
        foreach (var (p, baglanti, toplam) in beklenen) { var a = AgIstemciFabrikasi.Ayar(p); Assert.Equal(TimeSpan.FromSeconds(baglanti), a.Baglanti); Assert.Equal(TimeSpan.FromSeconds(toplam), a.Toplam); }
        Assert.Equal(TimeSpan.FromSeconds(90 * 0.02), AgIstemciFabrikasi.ClaudeAkisBostaSure);   // çarpanla ölçeklenir; üretimde 90 sn
    }

    [Theory]
    [InlineData(SocketError.HostNotFound, "Dns", "DNS")]
    [InlineData(SocketError.ConnectionRefused, "BaglantiYok", "bağlantı kurulamadı")]
    [InlineData(SocketError.NetworkUnreachable, "BaglantiYok", "İnternet bağlantınızı")]
    public async Task InternetKesik_Dns_BaglantiHatasi_AnlasilirTurkceAgHatasi(SocketError kod, string tur, string parca)
    {
        Kur((_, _) => Hata(kod));
        var h = await Assert.ThrowsAsync<AgHatasi>(() => AgIstemciFabrikasi.MetinAlAsync(AgProfili.KevBeslemesi, "https://www.cisa.gov/x"));
        Assert.Equal(tur, h.Tur.ToString()); Assert.Contains(parca, h.Message, StringComparison.Ordinal); Assert.Contains("CISA KEV", h.Message, StringComparison.Ordinal); Assert.DoesNotContain("System.", h.Message, StringComparison.Ordinal);   // ham istisna adı kullanıcıya sızmaz
    }

    [Fact]
    public async Task TlsHatasi_TurkceMesajVerir()
    {
        Kur((_, _) => throw new HttpRequestException("ssl", new AuthenticationException("sertifika")));
        var h = await Assert.ThrowsAsync<AgHatasi>(() => AgIstemciFabrikasi.MetinAlAsync(AgProfili.ZamanDamgasi, "https://freetsa.org/tsr"));
        Assert.Equal(AgHatasiTuru.Tls, h.Tur); Assert.Contains("sertifika/TLS", h.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SunucuYanitsiz_ZamanAsimindaBiter_CagriBlokeEtmez()
    {
        Kur(Takil);
        var sw = Stopwatch.StartNew(); var gorev = AgIstemciFabrikasi.MetinAlAsync(AgProfili.IpSizintisi, "https://api.ipify.org");
        Assert.False(gorev.IsCompleted);                    // çağrı hemen döner: çağıranı (arayüz iş parçacığını) bloke etmez
        long donus = sw.ElapsedMilliseconds; Assert.True(donus < 100, "çağrı bloke etti: " + donus + " ms");
        var h = await Assert.ThrowsAsync<AgHatasi>(() => gorev); sw.Stop();
        Assert.Equal(AgHatasiTuru.ZamanAsimi, h.Tur); Assert.Contains("yanıt vermedi", h.Message, StringComparison.Ordinal);
        Assert.InRange(sw.ElapsedMilliseconds, 100, 2000); _cikti.WriteLine($"[ÖLÇÜM] yanıtsız sunucu (IpSizintisi, çarpan 0,02 → beklenen ≈160 ms): {sw.ElapsedMilliseconds} ms'de AgHatasi");
    }

    [Fact]
    public async Task KullaniciIptali_AgHatasiDegil_OperationCanceledOlur()
    {
        Kur(Takil); using var cts = new CancellationTokenSource(); var gorev = AgIstemciFabrikasi.MetinAlAsync(AgProfili.ParolaSizintisi, "https://api.pwnedpasswords.com/range/ABCDE", null, cts.Token);
        cts.CancelAfter(30); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gorev);
    }

    [Fact]
    public async Task HttpDurumuHatasi_Turkce_ParolaSorgusuSunucuHatasindaAgHatasi()
    {
        Kur((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var h = await Assert.ThrowsAsync<AgHatasi>(() => ParolaSizinti.SorAsync("parola"));
        Assert.Equal(AgHatasiTuru.HttpDurumu, h.Tur); Assert.Contains("HTTP 503", h.Message, StringComparison.Ordinal); Assert.Contains("pwnedpasswords", h.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpSizintiKaynagi_AgYokkenNullDoner_TestiCokertmez()
    {
        Kur((_, _) => Hata(SocketError.HostNotFound));
        var k = new IpSizintiTesti.Kaynaklar(); Assert.Null(await k.Al("https://api.ipify.org?format=json", CancellationToken.None));
        Kur(Takil); Assert.Null(await k.Al("https://api.ipify.org?format=json", CancellationToken.None));
    }

    [Fact]
    public async Task KevIndirme_YanitsizSunucuda_AgHatasiFirlatir()
    {
        var h = await Assert.ThrowsAsync<AgHatasi>(() => KevIndirici.IndirAsync(new SahteIsleyici(Takil), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(AgHatasiTuru.ZamanAsimi, h.Tur);
    }

    [Fact]
    public async Task WebhookGonderimi_AgYokkenVeYanitsizken_FalseDoner_Cokmez()
    {
        Kur((_, _) => Hata(SocketError.ConnectionRefused));
        Assert.False(await Webhook.GonderAsync("https://discord.com/api/webhooks/1/abc", "deneme"));
        Kur(Takil); Assert.False(await Webhook.GonderAsync("https://discord.com/api/webhooks/1/abc", "deneme"));
    }

    [Fact]
    public async Task ZamanDamgasi_SunucuYanitsiz_TurkceNotlaNullDoner()
    {
        string zip = Path.Combine(Path.GetTempPath(), "tsa_" + Guid.NewGuid().ToString("N") + ".zip"); File.WriteAllBytes(zip, new byte[] { 1, 2, 3 });
        try { var (z, not) = await KanitImzasi.ZamanDamgasiAlAsync(zip, "https://freetsa.org/tsr", CancellationToken.None, new SahteIsleyici(Takil)); Assert.Null(z); Assert.Contains("yanıt vermedi", not, StringComparison.Ordinal); }
        finally { File.Delete(zip); }
    }

    [Fact]
    public async Task ClaudeAkisi_AgYokkenVeParcaArasiBostaKalinca_AnlasilirClaudeHatasi()
    {
        Kur((_, _) => Hata(SocketError.HostNotFound));
        var h1 = await Assert.ThrowsAsync<ClaudeHatasi>(() => ClaudeIstemci.AnalizAsync("anahtar", "model", "sistem", "soru", _ => { }, CancellationToken.None)); Assert.Contains("DNS", h1.Message, StringComparison.Ordinal);
        // Başlık gelir ama gövde akışı takılır: boşta kalma sınırı (90 sn × çarpan) ile kesilir.
        Kur((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new TakilanAkis()) }));
        var h2 = await Assert.ThrowsAsync<ClaudeHatasi>(() => ClaudeIstemci.AnalizAsync("anahtar", "model", "sistem", "soru", _ => { }, CancellationToken.None)); Assert.Contains("yarıda kesildi", h2.Message, StringComparison.Ordinal);
    }
    private sealed class TakilanAkis : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => throw new NotSupportedException(); public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { } public override int Read(byte[] b, int o, int c) => throw new NotSupportedException(); public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long v) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default) { await Task.Delay(Timeout.Infinite, ct); return 0; }
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void KaynakTaramasi_DisServisIstemcileriYalnizcaFabrikadan_YerelTaramaIstemcileriZamanAsimliDir()
    {
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null; i++, kok = Path.GetDirectoryName(kok)) if (File.Exists(Path.Combine(kok, "Directory.Build.props"))) break;
        if (kok == null) return;
        // Yerel/özel ağ hedeflerini tarayan istemciler kendi işleyicisini kullanır; hepsinde açık Timeout olmalı.
        var yerel = new HashSet<string> { "AktifHttp.cs", "SahiplikDogrulama.cs", "PasifToplayici.cs", "HttpDenetimi.cs", "GirdiDogrulamaTesti.cs" };
        var ihlal = new List<string>();
        foreach (var proje in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
            {
                if (Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]")) continue; string ad = Path.GetFileName(f); if (ad == "AgIstemciFabrikasi.cs") continue; string metin = File.ReadAllText(f);
                if (!Regex.IsMatch(metin, @"new HttpClient\s*[({]")) continue;
                if (!yerel.Contains(ad)) ihlal.Add(ad + " (dış servis istemcisi AgIstemciFabrikasi dışında)"); else if (!Regex.IsMatch(metin, @"Timeout\s*=\s*TimeSpan\.From")) ihlal.Add(ad + " (açık Timeout yok)");
            }
        Assert.True(ihlal.Count == 0, string.Join(" | ", ihlal));
    }
}
