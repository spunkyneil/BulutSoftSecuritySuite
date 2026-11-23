// Bu dosya HTTP, TLS ve DNS kontrollerini SENSÖRDEN (Kali) çalıştırır. Mimari: sensördeki ajan yalnızca VERİ TOPLAR (başlıklar, sertifika bilgisi,
// çözülen adresler); bulguların yorumlanması (önem, açıklama, öneri) bu dosyada, mevcut yerel modüllerle AYNI mantık ve Türkçe metinlerle yapılır.
// Böylece sensörden gelen sonuçlar yerel taramayla aynı biçimde tabloya, radara, geçmişe, KVKK/ISO eşleştirmesine ve raporlara akar.
// Güvenlik: hedef Windows'ta ve sensör ajanında AYRI AYRI özel ağ kontrolünden geçer; yönlendirmeler özel ağ dışına çıkarsa reddedilir;
// ajana giden argümanlar Base64 JSON'dur (kabuğa metin enjekte edilemez).
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Sensörden toplanan ham veriler.
internal sealed record HttpVeri(string Url, Dictionary<string, string> Basliklar, List<string> Cerezler);
internal sealed record TlsVeri(string Surum, string Sifre, string Subject, string Issuer, DateTime? Bitis, string AnahtarTur, int AnahtarBit, string Imza, string Dogrulama, Dictionary<string, string> Eski);
internal sealed record DnsVeri(string Alan, List<string> Ana, List<(string Ad, List<string> Ips)> Alt);

// Sensör ağ kontrolleri.
internal static class SensorAg
{
    // ---- Doğrulama ----
    // Ad/alan adı biçimi (ajandakiyle aynı kural).
    public static bool AdGecerli(string s) => !string.IsNullOrEmpty(s) && s.Length <= 253 && Regex.IsMatch(s, @"^[A-Za-z0-9]([A-Za-z0-9.\-]{0,251}[A-Za-z0-9])?$");

    // Sensör için hedef uygun mu? "localhost"/127.x sensörün KENDİSİDİR; Windows sanılıp yanılınmasın.
    public static void HedefKontrol(string host, string sensorAdi)
    {
        // Geçerli biçim.
        if (!AdGecerli(host)) throw new InvalidOperationException("Geçersiz ad/adres: " + host);
        // Döngü adresi.
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip)))
            throw new InvalidOperationException($"Tarama yeri \"{sensorAdi}\" sensörü seçili, ama hedef \"{host}\" sensörün KENDİSİDİR, Windows değil.\r\n\r\nSensörün erişebildiği adresi yazın (örn. 192.168.56.1) ya da Tarama yeri olarak \"Bu bilgisayar\"ı seçin.");
        // IP ise özel ağ olmalı (internetteki başkalarını taramayı engeller).
        if (IPAddress.TryParse(host, out var a) && !LabKapsam.IpLabdaMi(a)) throw new InvalidOperationException("Hedef özel ağ dışında; güvenlik için işlem durduruldu: " + host);
    }

    // Web adresini doğrular ve ayrıştırır.
    public static Uri UrlDogrula(string url, string sensorAdi)
    {
        // Biçim.
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500 || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || u.UserInfo.Length > 0) throw new InvalidOperationException("Geçersiz web adresi (http:// veya https:// ile başlamalı).");
        // Hedef.
        HedefKontrol(u.Host, sensorAdi); return u;
    }

    // Ajanın tek veri satırı için üst sınır (pasif gözlem ve nmap çıktıları eski 4096 sınırından büyük olabilir).
    public const int EnUzunSatir = 1_000_000;

    // Ajana verilecek komut: python3 - run <Base64(JSON)> (yalnızca Base64 karakterleri içerir: kabuk güvenli).
    public static string Komut(object arguman) => "python3 - run " + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(arguman)));

    // ---- Çalıştırma ----
    // Ajanı çalıştırıp tek veri olayını (JsonElement) verir; hata olursa anlaşılır InvalidOperationException atar.
    public static async Task<JsonElement> ToplaAsync(SensorProfili p, object arguman, string beklenenTur, CancellationToken ct)
    {
        // Çıktı.
        JsonElement? veri = null; string? ajanHata = null;
        // Çalıştır.
        var (kod, hata) = await SensorTaramaYurutucu.CalistirAsync(p, Komut(arguman), SensorAjani.Kaynak, satir =>
        {
            // JSON olmayan veya çok uzun satırlar atlanır (sensörden gelen veri güvenilmez kabul edilir).
            if (satir.Length is 0 or > EnUzunSatir || satir[0] != '{') return;
            try { using var d = JsonDocument.Parse(satir); var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("t", out var t) || t.ValueKind != JsonValueKind.String) return; string tur = t.GetString() ?? ""; if (tur == beklenenTur) veri = k.Clone(); else if (tur == "error") ajanHata = k.TryGetProperty("m", out var m) && m.ValueKind == JsonValueKind.String ? AjanTemiz(m.GetString() ?? "") : "bilinmeyen hata"; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SensorAg.ToplaAsync"); }
        }, ct, 180, EnUzunSatir);
        // İptal.
        ct.ThrowIfCancellationRequested();
        // Ajan hata bildirdiyse.
        if (ajanHata != null) throw new InvalidOperationException($"Sensör ({p.Ad}) kontrolü başarısız: {ajanHata}");
        // Bağlantı/çalışma hatası.
        if (kod != 0 || veri == null) throw new InvalidOperationException($"Sensör ({p.Ad}) kontrolü çalışmadı: " + (hata.Length > 0 ? SensorCiktiAyristirici.HataAciklama(hata) : "cevap alınamadı") + "\r\n\r\nSensör bağlantısını \"Uzak Sensörler\"den sınayın (ajan sürümü 2 gerekir) veya Tarama yeri olarak \"Bu bilgisayar\"ı seçin.");
        // Veri.
        return veri.Value;
    }

    // Ajan hata metnini ekranda güvenli kılar.
    private static string AjanTemiz(string s) => SensorCiktiAyristirici.Temizle(s, 300);

    // ---- Ayrıştırma ----
    // JSON alanı yardımcıları.
    private static string S(JsonElement k, string ad) => k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? SensorCiktiAyristirici.Temizle(v.GetString() ?? "", 1000) : "";
    private static int I(JsonElement k, string ad) => k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;
    private static List<string> L(JsonElement k, string ad) => k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => SensorCiktiAyristirici.Temizle(x.GetString() ?? "", 1000)).Take(200).ToList() : new();

    // HTTP verisi.
    public static HttpVeri HttpCoz(JsonElement k)
    {
        // Başlıklar (küçük harf).
        var b = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (k.TryGetProperty("basliklar", out var h) && h.ValueKind == JsonValueKind.Object) foreach (var p in h.EnumerateObject().Take(100)) if (p.Value.ValueKind == JsonValueKind.String) b[p.Name] = SensorCiktiAyristirici.Temizle(p.Value.GetString() ?? "", 2000);
        // Sonuç.
        return new HttpVeri(S(k, "url"), b, L(k, "cerezler"));
    }

    // TLS verisi.
    public static TlsVeri TlsCoz(JsonElement k)
    {
        // Eski sürüm sonuçları.
        var e = new Dictionary<string, string>(); if (k.TryGetProperty("eski", out var h) && h.ValueKind == JsonValueKind.Object) foreach (var p in h.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.String) e[p.Name] = p.Value.GetString() ?? "";
        // Bitiş tarihi (GMT).
        DateTime? bitis = DateTime.TryParse(S(k, "bitis"), null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var d) ? d : null;
        // Sonuç.
        return new TlsVeri(S(k, "surum"), S(k, "sifre"), S(k, "subject"), S(k, "issuer"), bitis, S(k, "anahtar_tur"), I(k, "anahtar_bit"), S(k, "imza"), S(k, "dogrulama"), e);
    }

    // DNS verisi.
    public static DnsVeri DnsCoz(JsonElement k)
    {
        // Alt isimler.
        var alt = new List<(string, List<string>)>(); if (k.TryGetProperty("alt", out var a) && a.ValueKind == JsonValueKind.Array) foreach (var x in a.EnumerateArray().Take(50)) if (x.ValueKind == JsonValueKind.Object) alt.Add((S(x, "ad"), L(x, "ips")));
        // Sonuç.
        return new DnsVeri(S(k, "alan"), L(k, "ana"), alt);
    }

    // ---- Analiz (yerel modüllerle aynı mantık ve metinler) ----
    // HTTP bulguları.
    public static List<Bulgu> HttpAnaliz(HttpVeri v)
    {
        // Liste ve yardımcılar.
        var liste = new List<Bulgu>(); string url = v.Url; var bas = v.Basliklar; bool https = url.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        // Eksik etiket.
        void Eksik(string ad, Onem o, string oneri) { if (!bas.ContainsKey(ad)) liste.Add(new Bulgu("HTTP", $"Eksik koruma etiketi: {ad}", o, $"{url} cevabında {ad} etiketi yok.", oneri)); }
        // Kurallar.
        if (https) Eksik("Strict-Transport-Security", Onem.Orta, "Sunucu ayarlarına şunu ekleyin: Strict-Transport-Security: max-age=31536000; includeSubDomains");
        Eksik("Content-Security-Policy", Onem.Orta, "Kısıtlayıcı bir Content-Security-Policy kuralı tanımlayın (örn. default-src 'self').");
        Eksik("X-Content-Type-Options", Onem.Dusuk, "X-Content-Type-Options: nosniff etiketini ekleyin.");
        if (!bas.ContainsKey("X-Frame-Options") && !(bas.TryGetValue("Content-Security-Policy", out var csp) && csp.Contains("frame-ancestors"))) liste.Add(new Bulgu("HTTP", "Sitenin başka sayfaya gizlice gömülmesine karşı koruma yok", Onem.Orta, "X-Frame-Options veya frame-ancestors etiketi yok.", "X-Frame-Options: DENY ekleyin."));
        Eksik("Referrer-Policy", Onem.Dusuk, "Referrer-Policy: no-referrer ekleyin.");
        // Sürüm sızıntısı.
        foreach (var h in new[] { "Server", "X-Powered-By", "X-AspNet-Version" }) if (bas.TryGetValue(h, out var s) && s.Any(char.IsDigit)) liste.Add(new Bulgu("HTTP", $"Sunucu kendi sürümünü söylüyor: {h}", Onem.Dusuk, $"{h}: {s}", "Sürüm bilgisini cevaptan kaldırın (saldırgana ipucu vermesin)."));
        // Şifresiz site.
        if (!https) liste.Add(new Bulgu("HTTP", "Site şifresiz konuşuyor (http)", Onem.Orta, $"{url} adresi şifresiz çalışıyor; arada dinleyen biri yazılanları okuyabilir.", "Siteyi https'e geçirin."));
        // Çerezler.
        foreach (var c in v.Cerezler)
        {
            // Ad.
            var ad = c.Split('=')[0];
            if (https && !c.Contains("secure", StringComparison.OrdinalIgnoreCase)) liste.Add(new Bulgu("HTTP", $"Çerez şifresiz yolda da gidebilir: {ad}", Onem.Orta, c, "Çereze Secure bayrağı ekleyin."));
            if (!c.Contains("httponly", StringComparison.OrdinalIgnoreCase)) liste.Add(new Bulgu("HTTP", $"Çerez sayfa kodlarınca okunabilir: {ad}", Onem.Orta, c, "Çereze HttpOnly bayrağı ekleyin."));
            if (!c.Contains("samesite", StringComparison.OrdinalIgnoreCase)) liste.Add(new Bulgu("HTTP", $"Çerezde SameSite koruması yok: {ad}", Onem.Dusuk, c, "SameSite=Lax veya Strict ekleyin."));
        }
        // Hiç sorun yoksa.
        if (liste.Count == 0) liste.Add(new Bulgu("HTTP", "Temel koruma etiketleri yerinde", Onem.Bilgi, url, "Yapılacak bir şey yok."));
        // Sonuç.
        return liste;
    }

    // TLS bulguları.
    public static List<Bulgu> TlsAnaliz(TlsVeri v, DateTime simdiUtc)
    {
        // Liste.
        var liste = new List<Bulgu>();
        // Güvenilirlik.
        if (v.Dogrulama.Length > 0) liste.Add(new Bulgu("TLS", "Sertifika güvenilir değil", Onem.Orta, "Sorun türü: " + v.Dogrulama, "Güvenilir bir kuruluştan (test ortamında kendi kurumunuzdan) geçerli sertifika alın."));
        // Süre.
        if (v.Bitis is { } b) { if (b < simdiUtc) liste.Add(new Bulgu("TLS", "Sertifikanın süresi bitmiş", Onem.Yuksek, $"Bitiş tarihi: {b:d}", "Sertifikayı yenileyin (kimlik kartı süresi dolmuş gibi).")); else if (b < simdiUtc.AddDays(30)) liste.Add(new Bulgu("TLS", "Sertifika yakında bitecek", Onem.Dusuk, $"Bitiş tarihi: {b:d}", "Yenilemeyi planlayın.")); }
        // Kendi kendine verilmiş.
        if (v.Subject.Length > 0 && v.Subject == v.Issuer) liste.Add(new Bulgu("TLS", "Sertifika kendi kendine verilmiş", Onem.Dusuk, v.Subject, "Gerçek kullanımda güvenilir bir kuruluştan sertifika alın."));
        // Zayıf RSA anahtarı.
        if (v.AnahtarTur.Contains("rsa", StringComparison.OrdinalIgnoreCase) && v.AnahtarBit is > 0 and < 2048) liste.Add(new Bulgu("TLS", "Şifre anahtarı zayıf", Onem.Yuksek, $"{v.AnahtarBit} bit", "En az 2048 bitlik anahtar kullanın."));
        // SHA-1 imza.
        if (v.Imza.Contains("sha1", StringComparison.OrdinalIgnoreCase)) liste.Add(new Bulgu("TLS", "Eski ve zayıf imza yöntemi (SHA-1)", Onem.Yuksek, v.Imza, "SHA-256 veya daha yenisiyle sertifikayı yeniden oluşturun."));
        // Kullanılan şifreleme.
        liste.Add(new Bulgu("TLS", "Kullanılan şifreleme", Onem.Bilgi, $"{v.Surum}, {v.Sifre}", "Yapılacak bir şey yok."));
        // Eski sürümler.
        foreach (var (ad, durum) in v.Eski) if (durum == "kabul") liste.Add(new Bulgu("TLS", $"Eski şifreleme sürümü açık: {ad}", Onem.Yuksek, $"{ad} kabul ediliyor.", "TLS 1.0 ve 1.1'i kapatın; sadece TLS 1.2 ve üstüne izin verin."));
        // Sensör eski sürümleri deneyemediyse dürüstçe söyle (\"reddedildi\" sanılmasın).
        if (v.Eski.Values.Any(x => x == "desteklenmiyor")) liste.Add(new Bulgu("TLS", "Eski TLS sürümleri (1.0/1.1) denenemedi", Onem.Bilgi, "Sensörün şifreleme kütüphanesi eski sürümlerle bağlantı denemeye izin vermiyor.", "Eski sürümlerin kapalı olduğunu sunucu ayarından doğrulayın."));
        // Sonuç.
        return liste;
    }

    // DNS bulguları.
    public static List<Bulgu> DnsAnaliz(DnsVeri v)
    {
        // Liste.
        var liste = new List<Bulgu> { new("DNS", "Ana isim kaydı", Onem.Bilgi, string.Join(", ", v.Ana), "Yapılacak bir şey yok.") };
        // Alt isimler.
        foreach (var (ad, ips) in v.Alt)
        {
            // Hassas isimler.
            string on = ad.Split('.')[0]; bool hassas = on is "admin" or "dev" or "test" or "staging" or "backup" or "old" or "db" or "git";
            // Bulgu.
            liste.Add(new Bulgu("DNS", $"Alt isim bulundu: {ad}", hassas ? Onem.Dusuk : Onem.Bilgi, string.Join(", ", ips), hassas ? "Dışarıya açık olması gerekmiyorsa bu kaydı silin veya erişimi kısıtlayın." : "Yapılacak bir şey yok."));
        }
        // Sonuç.
        return liste;
    }

    // ---- Üst düzey çağrılar ----
    // HTTP kontrolü.
    public static async Task<List<Bulgu>> HttpAsync(SensorProfili p, string url, CancellationToken ct) { var u = UrlDogrula(url, p.Ad); var d = await ToplaAsync(p, new { mod = "http", url = u.AbsoluteUri }, "http", ct); return HttpAnaliz(HttpCoz(d) with { Url = u.AbsoluteUri }); }

    // TLS kontrolü.
    public static async Task<List<Bulgu>> TlsAsync(SensorProfili p, string host, int port, CancellationToken ct) { HedefKontrol(host, p.Ad); if (port is < 1 or > 65535) throw new InvalidOperationException("Geçersiz kapı: " + port); var d = await ToplaAsync(p, new { mod = "tls", host, port }, "tls", ct); return TlsAnaliz(TlsCoz(d), DateTime.UtcNow); }

    // DNS kontrolü.
    public static async Task<List<Bulgu>> DnsAsync(SensorProfili p, string alan, CancellationToken ct) { HedefKontrol(alan, p.Ad); var d = await ToplaAsync(p, new { mod = "dns", domain = alan }, "dns", ct); return DnsAnaliz(DnsCoz(d)); }
}
