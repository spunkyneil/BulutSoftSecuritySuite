// Bu dosya sensörden HTTP/TLS/DNS kontrollerinin testlerini içerir (ağa çıkmaz; ajan çıktısı örnekleriyle yorumlama ve doğrulama mantığı).
using System.Text.Json;

namespace LabDenetim.Tests;

// Sensör ağ kontrolleri testleri.
public class SensorAgTestleri
{
    // Ajanın gerçek çıktısına benzeyen örnekler.
    private const string HttpJson = "{\"t\":\"http\",\"url\":\"http://10.0.0.5/\",\"durum\":200,\"basliklar\":{\"server\":\"nginx/1.24.0\",\"content-type\":\"text/html\"},\"cerezler\":[\"sid=abc; Path=/\"]}";
    private const string TlsJson = "{\"t\":\"tls\",\"surum\":\"TLSv1.3\",\"sifre\":\"TLS_AES_256_GCM_SHA384\",\"subject\":\"CN=x\",\"issuer\":\"CN=x\",\"bitis\":\"2020-01-01T00:00:00\",\"anahtar_tur\":\"rsaEncryption\",\"anahtar_bit\":1024,\"imza\":\"sha1WithRSAEncryption\",\"dogrulama\":\"self-signed certificate\",\"eski\":{\"TLS 1.0\":\"kabul\",\"TLS 1.1\":\"red\"}}";
    private const string DnsJson = "{\"t\":\"dns\",\"alan\":\"lab.local\",\"ana\":[\"10.0.0.1\"],\"alt\":[{\"ad\":\"admin.lab.local\",\"ips\":[\"10.0.0.2\"]},{\"ad\":\"www.lab.local\",\"ips\":[\"10.0.0.3\"]}]}";
    private static JsonElement J(string s) => JsonDocument.Parse(s).RootElement.Clone();

    // HTTP: eksik başlıklar, sürüm sızıntısı, şifresiz site, çerez bayrakları (yerel modülle aynı kurallar).
    [Fact] public void Http_Analiz()
    {
        var b = SensorAg.HttpAnaliz(SensorAg.HttpCoz(J(HttpJson))); var basliklar = b.Select(x => x.Baslik).ToList();
        Assert.Contains("Eksik koruma etiketi: Content-Security-Policy", basliklar); Assert.DoesNotContain("Eksik koruma etiketi: Strict-Transport-Security", basliklar); Assert.Contains("Sunucu kendi sürümünü söylüyor: Server", basliklar); Assert.Contains("Site şifresiz konuşuyor (http)", basliklar); Assert.Contains("Çerez sayfa kodlarınca okunabilir: sid", basliklar); Assert.All(b, x => Assert.Equal("HTTP", x.Modul));
    }
    // HTTP: https sitede HSTS ve Secure çerez kontrolleri; her şey yerindeyse bilgi bulgusu.
    [Fact] public void Http_Https_ve_Temiz()
    {
        var v = new HttpVeri("https://10.0.0.5/", new(StringComparer.OrdinalIgnoreCase) { ["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'", ["X-Content-Type-Options"] = "nosniff", ["Referrer-Policy"] = "no-referrer", ["Strict-Transport-Security"] = "max-age=1" }, new List<string> { "a=b; Secure; HttpOnly; SameSite=Lax" });
        var l = SensorAg.HttpAnaliz(v); Assert.Single(l); Assert.Equal(Onem.Bilgi, l[0].Onem);
        var kotu = SensorAg.HttpAnaliz(v with { Cerezler = new List<string> { "a=b" } }); Assert.Contains(kotu, x => x.Baslik.StartsWith("Çerez şifresiz yolda"));
    }
    // TLS: süresi bitmiş, zayıf anahtar, SHA-1, eski sürüm açık, güvenilmez, kendi imzalı.
    [Fact] public void Tls_Analiz()
    {
        var b = SensorAg.TlsAnaliz(SensorAg.TlsCoz(J(TlsJson)), new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc)); var t = b.Select(x => x.Baslik).ToList();
        Assert.Contains("Sertifika güvenilir değil", t); Assert.Contains("Sertifikanın süresi bitmiş", t); Assert.Contains("Sertifika kendi kendine verilmiş", t); Assert.Contains("Şifre anahtarı zayıf", t); Assert.Contains("Eski ve zayıf imza yöntemi (SHA-1)", t); Assert.Contains("Eski şifreleme sürümü açık: TLS 1.0", t); Assert.DoesNotContain("Eski şifreleme sürümü açık: TLS 1.1", t); Assert.Contains("Kullanılan şifreleme", t);
    }
    // TLS: sensör eski sürümleri deneyemediyse dürüstçe belirtilir (güvenli sanılmaz).
    [Fact] public void Tls_EskiDenenemedi() { var v = SensorAg.TlsCoz(J("{\"t\":\"tls\",\"surum\":\"TLSv1.3\",\"sifre\":\"X\",\"eski\":{\"TLS 1.0\":\"desteklenmiyor\",\"TLS 1.1\":\"desteklenmiyor\"}}")); var b = SensorAg.TlsAnaliz(v, DateTime.UtcNow); Assert.Contains(b, x => x.Baslik.Contains("denenemedi") && x.Onem == Onem.Bilgi); Assert.DoesNotContain(b, x => x.Baslik.StartsWith("Eski şifreleme sürümü açık")); }
    // TLS: EC anahtarı "zayıf RSA" sayılmaz; yakında bitecek sertifika düşük.
    [Fact] public void Tls_EcAnahtar_ve_YakindaBitecek()
    {
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc); var v = new TlsVeri("TLSv1.3", "X", "CN=a", "CN=b", now.AddDays(10), "id-ecPublicKey", 256, "ecdsa-with-SHA256", "", new());
        var b = SensorAg.TlsAnaliz(v, now); Assert.DoesNotContain(b, x => x.Baslik == "Şifre anahtarı zayıf"); Assert.Contains(b, x => x.Baslik == "Sertifika yakında bitecek" && x.Onem == Onem.Dusuk);
    }
    // DNS: hassas alt isimler düşük, diğerleri bilgi.
    [Fact] public void Dns_Analiz()
    {
        var b = SensorAg.DnsAnaliz(SensorAg.DnsCoz(J(DnsJson))); Assert.Equal(3, b.Count); Assert.Equal(Onem.Dusuk, b.First(x => x.Baslik.Contains("admin.lab.local")).Onem); Assert.Equal(Onem.Bilgi, b.First(x => x.Baslik.Contains("www.lab.local")).Onem); Assert.Equal("Ana isim kaydı", b[0].Baslik);
    }
    // Güvensiz/bozuk veri güvenle çözülür (kontrol karakterleri temizlenir, eksik alanlar boş).
    [Fact] public void Coz_BozukVeri()
    {
        var v = SensorAg.HttpCoz(J("{\"t\":\"http\",\"url\":\"u\\nx\",\"basliklar\":\"yanlis\",\"cerezler\":5}")); Assert.Empty(v.Basliklar); Assert.Empty(v.Cerezler); Assert.DoesNotContain('\n', v.Url);
        Assert.Empty(SensorAg.DnsCoz(J("{\"t\":\"dns\"}")).Alt);
    }

    // Hedef doğrulama: döngü adresi sensörün kendisidir, internet adresi reddedilir, kabuk karakterleri geçersiz.
    [Theory, InlineData("127.0.0.1"), InlineData("localhost"), InlineData("LOCALHOST"), InlineData("8.8.8.8"), InlineData("a b"), InlineData("x;id"), InlineData("-x"), InlineData("")]
    public void Hedef_Reddedilir(string h) => Assert.Throws<InvalidOperationException>(() => SensorAg.HedefKontrol(h, "Kali"));
    [Theory, InlineData("192.168.56.1"), InlineData("10.0.0.5"), InlineData("lab.local"), InlineData("web-01.lab.local")]
    public void Hedef_Kabul(string h) => SensorAg.HedefKontrol(h, "Kali");
    // URL doğrulama.
    [Theory, InlineData("ftp://10.0.0.5/"), InlineData("http://user:pw@10.0.0.5/"), InlineData("javascript:alert(1)"), InlineData("10.0.0.5"), InlineData("http://127.0.0.1/"), InlineData("http://8.8.8.8/")]
    public void Url_Reddedilir(string u) => Assert.Throws<InvalidOperationException>(() => SensorAg.UrlDogrula(u, "Kali"));
    [Fact] public void Url_Kabul() => Assert.Equal("10.0.0.5", SensorAg.UrlDogrula("https://10.0.0.5:8443/a?b=c", "Kali").Host);
    // Komut yalnızca Base64 karakterleri içerir (kabuk enjeksiyonu imkânsız).
    [Fact] public void Komut_KabukGuvenli() { string k = SensorAg.Komut(new { mod = "http", url = "http://10.0.0.5/?q=';rm -rf /;'$(id)`x`" }); Assert.Matches("^python3 - run [A-Za-z0-9+/=]+$", k); }
    // Ajan kaynağı yalnızca ASCII (ssh üzerinden kodlama sorunu çıkmaz) ve yeni modları içerir.
    [Fact] public void Ajan_Ascii_ve_Modlar() { Assert.All(SensorAjani.Kaynak, c => Assert.True(c < 128, "ASCII dışı karakter: " + c)); Assert.Contains("def http_topla", SensorAjani.Kaynak); Assert.Contains("def tls_topla", SensorAjani.Kaynak); Assert.Contains("def dns_topla", SensorAjani.Kaynak); Assert.Contains("def nmap_topla", SensorAjani.Kaynak); Assert.Contains("def ip_topla", SensorAjani.Kaynak); Assert.Contains("def arac_topla", SensorAjani.Kaynak); Assert.Equal("6", SensorAjani.Surum); }
}
