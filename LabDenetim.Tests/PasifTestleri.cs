// Bu dosya dış ağ PASİF gözlem özelliğinin testlerini içerir: kapsam kapısı (iç/dış/reddedilen), modül kilidi, hız sınırı, security.txt, DNS paket çözme,
// JSON kural motoru, sensör çıktısı çözme, etik rapor güvenliği, kayan pencereli brute-force ve DPAPI mührü. Gerçek internete HİÇ çıkmaz.
using System.Net;
using System.Text.Json;

// Test alanı.
namespace LabDenetim.Tests;

// Kapsam ve pasif gözlem testleri (statik durum paylaştığı için seri çalışır).
[Collection("Veri")]
public class PasifTestleri : IDisposable
{
    // Eski çözücü geri konsun diye saklanır.
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _eskiCozucu = Kapsam.Cozucu;
    // Temizlik.
    public void Dispose() { Kapsam.Cozucu = _eskiCozucu; Kapsam.HizSifirla(); }
    // Sahte çözücü kurar.
    private static void Coz(params string[] ipler) => Kapsam.Cozucu = (_, _) => Task.FromResult(ipler.Select(IPAddress.Parse).ToArray());

    // ---- Genel IP sınıflandırması ----
    [Theory, InlineData("8.8.8.8", true), InlineData("93.184.216.34", true), InlineData("10.1.2.3", false), InlineData("192.168.1.1", false), InlineData("172.16.0.1", false), InlineData("172.32.0.1", true), InlineData("127.0.0.1", false), InlineData("169.254.169.254", false), InlineData("100.64.0.1", false), InlineData("203.0.113.9", false), InlineData("224.0.0.1", false), InlineData("0.0.0.0", false), InlineData("::1", false), InlineData("fe80::1", false), InlineData("fd00::1", false), InlineData("2001:db8::1", false), InlineData("2606:2800:220:1::1", true), InlineData("::ffff:10.0.0.1", false), InlineData("::ffff:8.8.8.8", true)]
    public void GenelIp(string ip, bool beklenen) => Assert.Equal(beklenen, Kapsam.GenelIpMi(IPAddress.Parse(ip)));

    // ---- Ana bilgisayar çıkarma ----
    [Theory, InlineData("https://Example.com/x?y=1", "example.com"), InlineData("example.com", "example.com"), InlineData("http://[2606:2800::1]/", "2606:2800::1"), InlineData("8.8.8.8", "8.8.8.8")]
    public void HostCikar_Gecerli(string girdi, string beklenen) => Assert.Equal(beklenen, Kapsam.HostCikar(girdi));
    [Theory, InlineData(""), InlineData("   "), InlineData("ftp://a.com"), InlineData("http://user:pw@a.com"), InlineData("a b.com"), InlineData("-a.com"), InlineData("a..com;rm")]
    public void HostCikar_Gecersiz(string girdi) => Assert.Null(Kapsam.HostCikar(girdi));

    // ---- Sınıflandırma ----
    [Fact] public async Task Sinifla_IcAg() { Coz("192.168.56.101"); Assert.Equal((int)KapsamModu.IcAg, (int)(await Kapsam.SiniflaAsync("lab.local", default)).Mod); }
    [Fact] public async Task Sinifla_Dis() { Coz("93.184.216.34"); var k = await Kapsam.SiniflaAsync("https://example.com/", default); Assert.Equal((int)KapsamModu.DisAgPasif, (int)k.Mod); Assert.Equal("example.com", k.Host); }
    // DNS yeniden bağlama: bir adres özel, biri genel → reddedilir.
    [Fact] public async Task Sinifla_Karisik_Reddedilir() { Coz("93.184.216.34", "127.0.0.1"); Assert.Equal((int)KapsamModu.Reddedildi, (int)(await Kapsam.SiniflaAsync("evil.example", default)).Mod); }
    [Fact] public async Task Sinifla_BulutMetaVeri_Reddedilir() { Coz("169.254.169.254"); Assert.Equal((int)KapsamModu.Reddedildi, (int)(await Kapsam.SiniflaAsync("meta.example", default)).Mod); }
    [Fact] public async Task Sinifla_CozulemezVeBiçimsiz()
    {
        Kapsam.Cozucu = (_, _) => throw new System.Net.Sockets.SocketException(11001); Assert.Equal((int)KapsamModu.Reddedildi, (int)(await Kapsam.SiniflaAsync("yok.example", default)).Mod);
        Assert.Equal((int)KapsamModu.Reddedildi, (int)(await Kapsam.SiniflaAsync("a b;c", default)).Mod);
    }
    // IP yazılırsa çözücü hiç çağrılmaz.
    [Fact] public async Task Sinifla_IpDogrudan() { Kapsam.Cozucu = (_, _) => throw new Exception("çağrılmamalı"); Assert.Equal((int)KapsamModu.DisAgPasif, (int)(await Kapsam.SiniflaAsync("8.8.8.8", default)).Mod); Assert.Equal((int)KapsamModu.IcAg, (int)(await Kapsam.SiniflaAsync("10.0.0.5", default)).Mod); }
    // Bağlantı anı yeniden doğrulama: DNS iç ağa dönerse istisna.
    [Fact] public async Task CozGenel_IcAdreseDonerse_Ihlal() { Coz("10.0.0.7"); await Assert.ThrowsAsync<KapsamIhlaliException>(() => Kapsam.CozGenelAsync("x.example", default)); Coz("93.184.216.34"); Assert.Single(await Kapsam.CozGenelAsync("x.example", default)); }

    // ---- Modül kilidi ----
    [Fact] public void ModulKilidi()
    {
        // Dış ağda yalnızca pasif modüller.
        foreach (var t in new[] { ModulTuru.PortTarama, ModulTuru.ZayifAyar, ModulTuru.GirdiTesti, ModulTuru.KabaKuvvet, ModulTuru.SistemDenetimi, ModulTuru.IcHttp, ModulTuru.IcTls, ModulTuru.IcDns }) { Assert.False(Kapsam.Izinli(KapsamModu.DisAgPasif, t), t.ToString()); Assert.Throws<KapsamIhlaliException>(() => Kapsam.KilitliyseDur(KapsamModu.DisAgPasif, t)); }
        foreach (var t in new[] { ModulTuru.PasifHttp, ModulTuru.PasifTls, ModulTuru.PasifDns, ModulTuru.SecurityTxt }) Assert.True(Kapsam.Izinli(KapsamModu.DisAgPasif, t));
        // İç ağda hepsi (eski davranış).
        foreach (var t in Enum.GetValues<ModulTuru>()) Assert.True(Kapsam.Izinli(KapsamModu.IcAg, t));
        // Reddedilen modda hiçbiri.
        foreach (var t in Enum.GetValues<ModulTuru>()) Assert.False(Kapsam.Izinli(KapsamModu.Reddedildi, t));
    }
    // Pasif toplayıcı iç ağ modunda çalışmayı reddeder.
    [Fact] public async Task Toplayici_IcAgdaReddeder() => await Assert.ThrowsAsync<KapsamIhlaliException>(() => PasifToplayici.GozlemYapAsync(new KapsamKarari(KapsamModu.IcAg, "lab.local", Array.Empty<IPAddress>(), ""), null, default));

    // ---- Hız sınırı ----
    [Fact] public void HizSiniri()
    {
        var t = new DateTime(2026, 11, 18, 12, 0, 0); Assert.True(Kapsam.GozlemHakki(t).Izinli); Assert.False(Kapsam.GozlemHakki(t.AddSeconds(5)).Izinli);
        // 15 sn arayla 20 gözlem hakkı, 21. reddedilir.
        Kapsam.HizSifirla(); for (int i = 0; i < 20; i++) Assert.True(Kapsam.GozlemHakki(t.AddSeconds(i * 16)).Izinli, "i=" + i); Assert.False(Kapsam.GozlemHakki(t.AddSeconds(20 * 16)).Izinli);
        // Bir saat geçince yeniden.
        Assert.True(Kapsam.GozlemHakki(t.AddHours(2)).Izinli);
    }
    [Theory, InlineData("meb.gov.tr", true), InlineData("x.gov", true), InlineData("army.mil", true), InlineData("ankara.bel.tr", true), InlineData("example.com", false), InlineData("govern.com", false)]
    public void HassasAlan(string h, bool b) => Assert.Equal(b, Kapsam.HassasAlanMi(h));

    // ---- security.txt (RFC 9116) ----
    private static readonly DateTime Simdi = new(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
    [Fact] public void SecTxt_Gecerli() { var s = SecurityTxt.Coz("# yorum\nContact: mailto:guvenlik@example.com\nContact: https://example.com/report\nExpires: 2027-03-01T00:00:00Z\nPreferred-Languages: tr, en\n", "https://example.com/.well-known/security.txt", true, "text/plain; charset=utf-8", Simdi); Assert.True(s.Bulundu); Assert.Empty(s.Sorunlar); Assert.Equal(new[] { "guvenlik@example.com", "https://example.com/report" }, SecurityTxt.Iletisimler(s)); }
    [Fact] public void SecTxt_Sorunlar() { var s = SecurityTxt.Coz("Contact: bozuk deger\nExpires: 2020-01-01T00:00:00Z\n", null, false, "text/html", Simdi); Assert.Contains(s.Sorunlar, x => x.Contains("geçerli bir URI")); Assert.Contains(s.Sorunlar, x => x.Contains("geçmiş")); Assert.Contains(s.Sorunlar, x => x.Contains("https")); Assert.Contains(s.Sorunlar, x => x.Contains("text/plain")); }
    [Fact] public void SecTxt_ContactYokExpiresYok() { var s = SecurityTxt.Coz("Encryption: https://a.com/k.asc", "u", true, null, Simdi); Assert.Contains(s.Sorunlar, x => x.StartsWith("Contact alanı yok")); Assert.Contains(s.Sorunlar, x => x.StartsWith("Expires alanı yok")); }
    [Fact] public void SecTxt_PgpImzali() { var s = SecurityTxt.Coz("-----BEGIN PGP SIGNED MESSAGE-----\nHash: SHA256\n\nContact: mailto:a@b.co\nExpires: 2027-01-01T00:00:00Z\n-----BEGIN PGP SIGNATURE-----\nabc\n-----END PGP SIGNATURE-----", "u", true, "text/plain", Simdi); Assert.Empty(s.Sorunlar); Assert.Single(s.Alanlar["Contact"]); }
    [Fact] public void SecTxt_AsiriUzunDegerKesilir() { var s = SecurityTxt.Coz("Contact: mailto:" + new string('a', 5000) + "@b.co\nExpires: 2027-01-01T00:00:00Z", "u", true, "text/plain", Simdi); Assert.True(s.Alanlar["Contact"][0].Length <= 500); }

    // ---- DNS paketi ----
    [Fact] public void Dns_PaketBicimi()
    {
        var p = DnsIstemci.Paket("example.com", 16, 0x1234); Assert.Equal(0x12, p[0]); Assert.Equal(0x34, p[1]); Assert.Equal(7, p[12]); Assert.Equal(0, p[^5]); Assert.Equal(16, p[^3]); Assert.Equal(1, p[^1]);
        Assert.Throws<ArgumentException>(() => DnsIstemci.Paket(new string('a', 64) + ".com", 16, 1));
    }
    // Sahte cevap: TXT kaydı (adı sıkıştırma işaretçisiyle).
    private static byte[] TxtCevap(ushort id, string metin) { var l = new List<byte> { (byte)(id >> 8), (byte)id, 0x81, 0x80, 0, 1, 0, 1, 0, 0, 0, 0 }; foreach (var e in new[] { "example", "com" }) { l.Add((byte)e.Length); l.AddRange(System.Text.Encoding.ASCII.GetBytes(e)); } l.AddRange(new byte[] { 0, 0, 16, 0, 1 }); l.AddRange(new byte[] { 0xC0, 0x0C, 0, 16, 0, 1, 0, 0, 0, 60 }); var m = System.Text.Encoding.UTF8.GetBytes(metin); l.Add(0); l.Add((byte)(m.Length + 1)); l.Add((byte)m.Length); l.AddRange(m); return l.ToArray(); }
    [Fact] public void Dns_TxtCoz() { var r = DnsIstemci.Coz(TxtCevap(7, "v=spf1 -all"), 7, out bool kesik); Assert.False(kesik); Assert.Single(r); Assert.Equal("v=spf1 -all", r[0].Veri); }
    [Fact] public void Dns_YanlisKimlik_Hata() => Assert.Throws<FormatException>(() => DnsIstemci.Coz(TxtCevap(7, "x"), 8, out _));
    [Fact] public void Dns_BozukVeCokKisa_Cokmez() { Assert.Throws<FormatException>(() => DnsIstemci.Coz(new byte[] { 1, 2, 3 }, 1, out _)); var kotu = TxtCevap(9, "abc"); kotu[12 + 0] = 0xC0; kotu[12 + 1] = 12; Assert.ThrowsAny<Exception>(() => DnsIstemci.Coz(kotu, 9, out _)); }
    [Theory, InlineData("www.example.com", "example.com"), InlineData("a.b.example.com.tr", "example.com.tr"), InlineData("example.com", "example.com"), InlineData("x.co.uk", "x.co.uk")]
    public void OrgAlani(string h, string b) => Assert.Equal(b, DnsIstemci.OrgAlani(h));
    [Fact] public void OrgAlani_IpIcinYok() => Assert.Null(DnsIstemci.OrgAlani("8.8.8.8"));

    // ---- Kural motoru ----
    [Fact] public void Kurallar_GomuluYuklenir_Gecerli()
    {
        var k = PasifKurallar.Yukle(out var uyari, "[]"); Assert.Empty(uyari); Assert.True(k.Count >= 28, "kural sayısı: " + k.Count); Assert.Equal(k.Count, k.Select(x => x.Id).Distinct().Count());
        // Her kuralda KVKK veya ISO eşlemesi ve düzeltme metni var.
        Assert.All(k, x => { Assert.NotEmpty(x.Fix); Assert.True(x.EtaMinutes > 0, x.Id); });
    }
    // Örnek gözlem kurucu.
    private static PasifGozlem Gozlem(Dictionary<string, string>? bas = null, List<string>? cerez = null, TlsGozlem? tls = null, DnsGozlem? dns = null, SecurityTxtSonucu? sec = null, string yon = "evet") => new("example.com", Simdi, "test", new HttpGozlem("https://example.com/", 200, bas ?? new(StringComparer.OrdinalIgnoreCase), cerez ?? new(), new(), yon), tls, dns, sec ?? SecurityTxt.Yok(), new());
    [Fact] public void Kural_BaslikEksikleri()
    {
        var b = PasifKurallar.Degerlendir(Gozlem(), null, Simdi).Select(x => x.RuleId).ToList();
        Assert.Contains("HDR-HSTS-MISSING", b); Assert.Contains("HDR-CSP-MISSING", b); Assert.Contains("SECTXT-MISSING", b); Assert.DoesNotContain("REDIR-HTTP-TO-HTTPS", b);
    }
    [Fact] public void Kural_IyiBaslikBulguUretmez()
    {
        var bas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["strict-transport-security"] = "max-age=31536000; includeSubDomains", ["content-security-policy"] = "default-src 'self'; frame-ancestors 'none'", ["x-content-type-options"] = "nosniff", ["referrer-policy"] = "no-referrer", ["permissions-policy"] = "camera=()" };
        var b = PasifKurallar.Degerlendir(Gozlem(bas), null, Simdi).Select(x => x.RuleId).ToList(); Assert.DoesNotContain("HDR-HSTS-MISSING", b); Assert.DoesNotContain("HDR-HSTS-WEAK", b); Assert.DoesNotContain("HDR-CSP-MISSING", b); Assert.DoesNotContain(b, x => x.StartsWith("HDR-XFO") || x.StartsWith("HDR-FRAME"));
    }
    [Fact] public void Kural_ZayifHstsVeSizintilar()
    {
        var bas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["strict-transport-security"] = "max-age=100", ["server"] = "Apache/2.4.1", ["x-powered-by"] = "PHP/5.6" };
        var b = PasifKurallar.Degerlendir(Gozlem(bas), null, Simdi); var id = b.Select(x => x.RuleId).ToList(); Assert.Contains("HDR-HSTS-WEAK", id); Assert.Contains(id, x => x.Contains("SERVER")); Assert.Contains(id, x => x.Contains("POWERED"));
    }
    [Fact] public void Kural_HttpYonlendirmeYok() => Assert.Contains(PasifKurallar.Degerlendir(Gozlem(yon: "hayir"), null, Simdi), x => x.RuleId == "REDIR-HTTP-TO-HTTPS");
    // Çerez: bayrak eksikse her çerez için bulgu; DEĞER bulgularda/kanıtta ASLA görünmez.
    [Fact] public void Kural_Cerez_DegerSizmaz()
    {
        var b = PasifKurallar.Degerlendir(Gozlem(cerez: new() { "sid=SUPERGIZLIOTURUM123; Path=/", "tema=koyu; Secure; HttpOnly; SameSite=Lax" }), null, Simdi); var c = b.Where(x => x.RuleId.StartsWith("COOKIE")).ToList();
        Assert.NotEmpty(c); Assert.All(c, x => { Assert.DoesNotContain("SUPERGIZLIOTURUM123", x.Kanit + x.Ne + x.Baslik + x.Cozum); Assert.Contains("sid", x.Baslik + x.Kanit); }); Assert.DoesNotContain(c, x => (x.Baslik + x.Kanit).Contains("tema"));
        Assert.Equal("sid=<gizlendi>;Path=/", PasifKurallar.CerezMaskele("sid=SUPERGIZLIOTURUM123; Path=/"));
    }
    private static TlsGozlem Tls(DateTime bitis, bool isim = true, string konu = "CN=example.com", string veren = "CN=Iyi CA", string imza = "sha256WithRSAEncryption", int bit = 2048, string surum = "TLS 1.3", params string[] zincir) => new(surum, "X", konu, veren, Simdi.AddYears(-1), bitis, zincir.ToList(), new() { "example.com" }, imza, "rsaEncryption", bit, isim);
    [Fact] public void Kural_Tls_Sorunlar()
    {
        var b = PasifKurallar.Degerlendir(Gozlem(tls: Tls(Simdi.AddDays(-3), false, "CN=a", "CN=a", "sha1WithRSAEncryption", 1024, "TLS 1.0", "UntrustedRoot")), null, Simdi).Select(x => x.RuleId).ToList();
        Assert.Contains("TLS-EXPIRED", b); Assert.Contains("TLS-NAME-MISMATCH", b); Assert.Contains("TLS-SELF-SIGNED", b); Assert.Contains("TLS-SHA1", b); Assert.Contains("TLS-OLD-PROTOCOL", b); Assert.Contains("TLS-CHAIN", b); Assert.Contains(b, x => x.StartsWith("TLS-WEAK") || x.Contains("RSA"));
    }
    [Fact] public void Kural_Tls_YakindaBitecek_VeSaglam()
    {
        Assert.Contains(PasifKurallar.Degerlendir(Gozlem(tls: Tls(Simdi.AddDays(10))), null, Simdi), x => x.RuleId.Contains("EXPIR") && x.RuleId != "TLS-EXPIRED");
        Assert.DoesNotContain(PasifKurallar.Degerlendir(Gozlem(tls: Tls(Simdi.AddDays(200))), null, Simdi), x => x.Modul == "TLS");
    }
    [Fact] public void Kural_Dns()
    {
        // MX yoksa (alan adı e-posta almıyor) SPF/DMARC eksikliği raporlanmaz.
        Assert.DoesNotContain(PasifKurallar.Degerlendir(Gozlem(dns: new DnsGozlem("example.com", new(), new(), null, null, new(), "")), null, Simdi), x => x.RuleId is "DNS-SPF-MISSING" or "DNS-DMARC-MISSING");
        var yok = PasifKurallar.Degerlendir(Gozlem(dns: new DnsGozlem("example.com", new() { "10 mx.example.com" }, new(), null, null, new(), "")), null, Simdi).Select(x => x.RuleId).ToList(); Assert.Contains("DNS-SPF-MISSING", yok); Assert.Contains("DNS-DMARC-MISSING", yok); Assert.Contains(yok, x => x.StartsWith("DNS-CAA"));
        var kotu = PasifKurallar.Degerlendir(Gozlem(dns: new DnsGozlem("example.com", new(), new(), "v=spf1 +all", "v=DMARC1; p=none", new(), "")), null, Simdi).Select(x => x.RuleId).ToList(); Assert.Contains("DNS-SPF-OPEN", kotu); Assert.Contains("DNS-DMARC-NONE", kotu);
        var iyi = PasifKurallar.Degerlendir(Gozlem(dns: new DnsGozlem("example.com", new(), new(), "v=spf1 include:_spf.x.com -all", "v=DMARC1; p=reject", new() { "0 issue \"letsencrypt.org\"" }, "")), null, Simdi); Assert.DoesNotContain(iyi, x => x.Modul == "DNS");
    }
    [Fact] public void Kural_SecTxtSorunu() => Assert.Contains(PasifKurallar.Degerlendir(Gozlem(sec: SecurityTxt.Coz("Contact: mailto:a@b.co\nExpires: 2020-01-01T00:00:00Z", "u", true, "text/plain", Simdi)), null, Simdi), x => x.RuleId == "SECTXT-PROBLEMS");
    // Kullanıcı kuralı: ekleme, geçersiz kılma, devre dışı bırakma, geçersizleri atlama.
    [Fact] public void Kural_KullaniciGecersizKilma()
    {
        string js = """
        [ { "id": "HDR-HSTS-MISSING", "enabled": false },
          { "id": "OZEL-1", "module": "HTTP", "applies_to": "all", "fact": "header:x-ozel", "op": "missing", "impact": 3, "title": "X-Ozel yok", "what": "yok", "why": "w", "fix": "ekle", "eta_minutes": 5 },
          { "id": "BOZUK", "fact": "a", "op": "komut_calistir", "impact": 3, "title": "t", "what": "w" },
          { "id": "X", "fact": "a", "op": "regex", "value": "(", "impact": 1, "title": "t", "what": "w" } ]
        """;
        var k = PasifKurallar.Yukle(out var uyari, js); Assert.DoesNotContain(k, x => x.Id == "HDR-HSTS-MISSING"); Assert.Contains(k, x => x.Id == "OZEL-1"); Assert.DoesNotContain(k, x => x.Id is "BOZUK" or "X"); Assert.Equal(2, uyari.Count(u => u.Contains("atlandı")));
    }
    [Fact] public void Kural_BozukKullaniciDosyasi_GomuluSurer() { var k = PasifKurallar.Yukle(out var uyari, "{bozuk json"); Assert.True(k.Count >= 28); Assert.NotEmpty(uyari); }
    [Theory, InlineData("lt", "5", "10", true), InlineData("lt", "15", "10", false), InlineData("gt", "15", "10", true), InlineData("equals", "A", "a", true), InlineData("contains", "xAbcx", "abc", true), InlineData("regex", "TLS 1.0", "^TLS 1\\.[01]$", true), InlineData("missing", "", null, true), InlineData("present", " ", null, false), InlineData("lt", "abc", "10", false)]
    public void Kosul(string op, string deger, string? bek, bool sonuc) => Assert.Equal(sonuc, PasifKurallar.Kosul(deger, op, bek));
    [Fact] public void EtkiOnem() { Assert.Equal(Onem.Yuksek, PasifKurallar.OnemOlarak(8)); Assert.Equal(Onem.Orta, PasifKurallar.OnemOlarak(5)); Assert.Equal(Onem.Dusuk, PasifKurallar.OnemOlarak(2)); Assert.Equal(Onem.Bilgi, PasifKurallar.OnemOlarak(1)); }

    // ---- Sensör çıktısı çözme ----
    private const string SensorJson = """
    {"t":"pasif","host":"example.com","ips":["93.184.216.34"],"notlar":["bir not"],"http_yon":"hayir",
     "http":{"url":"https://example.com/","durum":200,"basliklar":{"Server":"nginx/1.0","X-Evil":"<script>alert(1)</script>"},"cerezler":["sid=ABC; Path=/"],"yonler":[]},
     "tls":{"surum":"TLSv1.3","sifre":"X","subject":"CN=example.com","issuer":"CN=example.com","bitis":"2026-11-20T00:00:00","baslangic":"2025-01-01T00:00:00","imza":"sha1WithRSAEncryption","anahtar_tur":"rsaEncryption","anahtar_bit":1024,"san":"DNS:example.com, DNS:www.example.com","dogrulama":"self-signed certificate"},
     "dns":{"org":"example.com","txt":["v=spf1 +all","baska"],"dmarc":["v=DMARC1; p=none"],"mx":["10 mx.example.com"],"ns":["a.iana-servers.net"],"caa":[]},
     "sectxt":{"url":"https://example.com/.well-known/security.txt","govde":"Contact: mailto:sec@example.com\nExpires: 2027-01-01T00:00:00Z","tur":"text/plain","https":true}}
    """;
    [Fact] public void SensorPasif_Coz_VeDegerlendir()
    {
        using var d = JsonDocument.Parse(SensorJson); var g = SensorPasif.Coz(d.RootElement, "kali", Simdi);
        Assert.Equal("example.com", g.Host); Assert.StartsWith("Sensör", g.Yer); Assert.Equal("hayir", g.Http!.HttpYonlendirme); Assert.Equal("v=spf1 +all", g.Dns!.Spf); Assert.Equal("v=DMARC1; p=none", g.Dns.Dmarc); Assert.True(g.SecTxt.Bulundu); Assert.Empty(g.SecTxt.Sorunlar); Assert.Equal(new[] { "sec@example.com" }, SecurityTxt.Iletisimler(g.SecTxt));
        var id = PasifKurallar.Degerlendir(g, null, Simdi).Select(x => x.RuleId).ToList(); foreach (var beklenen in new[] { "REDIR-HTTP-TO-HTTPS", "TLS-SELF-SIGNED", "TLS-SHA1", "DNS-SPF-OPEN", "DNS-DMARC-NONE", "TLS-CHAIN" }) Assert.Contains(beklenen, id);
        Assert.DoesNotContain("SECTXT-MISSING", id);
    }
    // Sensörden gelen bozuk/beklenmeyen tipler çökertmez.
    [Fact] public void SensorPasif_BozukVeri_Cokmez() { using var d = JsonDocument.Parse("{\"t\":\"pasif\",\"host\":5,\"http\":\"x\",\"tls\":{\"bitis\":12,\"anahtar_bit\":\"abc\"},\"dns\":[],\"sectxt\":{\"govde\":null},\"notlar\":{}}"); var g = SensorPasif.Coz(d.RootElement, "k", Simdi); Assert.Equal("", g.Host); Assert.Null(g.Http); Assert.NotNull(g.Tls); Assert.Null(g.Dns); PasifKurallar.Degerlendir(g, null, Simdi); }

    // ---- Etik Bildirim Raporu ----
    [Fact] public void Etik_Rapor_Guvenli_VeYapici()
    {
        using var d = JsonDocument.Parse(SensorJson); var g = SensorPasif.Coz(d.RootElement, "kali", Simdi); var b = PasifKurallar.Degerlendir(g, null, Simdi);
        var md = EtikBildirim.Markdown(g, b, "Ali | <b>", ""); var html = EtikBildirim.Html(g, b, "Ali <script>", "");
        Assert.Contains("sec@example.com", md); Assert.Contains("Tahmini toplam düzeltme süresi", md); Assert.Contains("hiçbir sisteme girilmemiş", md); Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("<b>", md); Assert.DoesNotContain("ABC", md + html);
        Assert.True(EtikBildirim.ToplamDakika(b) > 0);
    }
    [Fact] public void Etik_Md_Temizler() { Assert.DoesNotContain("\n", EtikBildirim.Md("a\nb|c`d<e>")); Assert.DoesNotContain("|", EtikBildirim.Md("a|b").Replace("\\|", "")); }
    [Fact] public void Etik_SureYazi() { Assert.Equal("~30 dakika", EtikBildirim.SureYazi(30)); Assert.Equal("~2 saat", EtikBildirim.SureYazi(120)); Assert.Equal("~1 saat 15 dakika", EtikBildirim.SureYazi(75)); }

    // ---- Kayan pencereli brute-force ----
    [Fact] public void BruteForce_KayanPencere()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0); var l = new List<GirisDenemesi>();
        // Saldırgan: 2 dakikada 6 deneme.
        for (int i = 0; i < 6; i++) l.Add(new GirisDenemesi(now.AddSeconds(-i * 15), "203.0.113.5", "admin", "RDP"));
        // Masum: 6 deneme ama saatlere yayılmış.
        for (int i = 0; i < 6; i++) l.Add(new GirisDenemesi(now.AddMinutes(-i * 9), "198.51.100.7", "ayse", "RDP"));
        var kayan = BruteForce.Analiz(l, now, TimeSpan.FromHours(2), 5, TimeSpan.FromMinutes(2)); Assert.Single(kayan); Assert.Equal("203.0.113.5", kayan[0].Ip); Assert.Equal(6, kayan[0].EnYogunPencere);
        // Eski davranış (kayan yok): ikisi de yakalanır.
        Assert.Equal(2, BruteForce.Analiz(l, now, TimeSpan.FromHours(2), 5).Count);
        Assert.Equal(0, BruteForce.EnYogunAralik(Array.Empty<DateTime>(), TimeSpan.FromMinutes(2))); Assert.Equal(3, BruteForce.EnYogunAralik(new[] { now, now.AddSeconds(60), now.AddSeconds(120), now.AddMinutes(10) }, TimeSpan.FromMinutes(2)));
    }

    // ---- CANLI deneme (varsayılan KAPALI): yalnızca BULUT_CANLI_TEST=1 ise IANA'nın example.com sitesine pasif gözlem yapar ----
    [Fact] public async Task Canli_ExampleCom()
    {
        // Ortam değişkeni yoksa hiçbir şey yapma (testler internete çıkmasın).
        if (Environment.GetEnvironmentVariable("BULUT_CANLI_TEST") != "1") return;
        // Gerçek çözücü geri konur.
        Kapsam.Cozucu = _eskiCozucu; var k = await Kapsam.SiniflaAsync("example.com", default); Assert.Equal((int)KapsamModu.DisAgPasif, (int)k.Mod);
        // Gözlem ve kurallar.
        var g = await PasifToplayici.GozlemYapAsync(k, m => Console.WriteLine("ILERLEME: " + m), default); var b = PasifKurallar.Degerlendir(g);
        Console.WriteLine($"CANLI: http={g.Http?.Durum} tls={g.Tls?.Surum} bitis={g.Tls?.Bitis:u} imza={g.Tls?.ImzaAlg} zincir=[{string.Join(",", g.Tls?.ZincirHatalari ?? new())}] spf={g.Dns?.Spf} dmarc={g.Dns?.Dmarc} mx={g.Dns?.Mx.Count} sectxt={g.SecTxt.Bulundu} http_yon={g.Http?.HttpYonlendirme} notlar=[{string.Join(" | ", g.Notlar)}]");
        foreach (var x in b) Console.WriteLine($"  [{x.Onem}] {x.RuleId}: {x.Baslik}");
        Assert.NotNull(g.Http); Assert.NotNull(g.Tls);
    }

    // ---- DPAPI mührü ----
    [Fact] public void Butunluk_DegisiklikYakalanir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bs_but_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); string f = Path.Combine(dir, "kimlik.json");
        try
        {
            File.WriteAllText(f, "{\"a\":1}"); Assert.Equal(ButunlukDurumu.Yok, Butunluk.Kontrol(f)); Assert.True(Butunluk.GuvenliMi(f)); Assert.Equal(ButunlukDurumu.Gecerli, Butunluk.Kontrol(f));
            // Elle düzenleme → bozuk.
            File.WriteAllText(f, "{\"a\":2}"); Assert.Equal(ButunlukDurumu.Bozuk, Butunluk.Kontrol(f)); Assert.False(Butunluk.GuvenliMi(f));
            // Mührü başka dosyaya kopyalama işe yaramaz (dosya adına bağlı).
            string g = Path.Combine(dir, "baska.json"); File.WriteAllText(g, "{\"a\":2}"); File.Copy(Butunluk.MuhurYolu(f), Butunluk.MuhurYolu(g)); Butunluk.Muhurla(f); File.Copy(Butunluk.MuhurYolu(f), Butunluk.MuhurYolu(g), true); Assert.Equal(ButunlukDurumu.Bozuk, Butunluk.Kontrol(g));
            // Bozuk mühür içeriği.
            File.WriteAllText(Butunluk.MuhurYolu(f), "!!!"); Assert.Equal(ButunlukDurumu.Bozuk, Butunluk.Kontrol(f));
        }
        finally { try { Directory.Delete(dir, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "PasifTestleri.temizlik"); } }
    }
}
