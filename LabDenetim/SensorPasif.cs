// Bu dosya dış ağ PASİF gözlemini UZAK SENSÖRDEN (SSH + gömülü Python ajanı v3) yaptırır.
// Ajan yalnızca VERİ TOPLAR (başlıklar, sertifika, DNS kayıtları, security.txt metni); yorumlama bu bilgisayarda, aynı JSON kural motoruyla yapılır
// (böylece yerel ve sensör sonuçları aynı kurallarla değerlendirilir). Sensörden gelen her şey GÜVENİLMEZ veri kabul edilir: tür/uzunluk sınırlanır.
// Ajan hedefi sensörde yeniden çözer, HEPSİ genel internet adresi değilse reddeder ve bağlantıyı doğruladığı IP'ye sabitler (DNS yeniden bağlama savunması).
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Sensör üzerinden pasif gözlem.
internal static class SensorPasif
{
    // Sensörden pasif gözlem alır. Önce bu bilgisayarda kapsam kapısından geçirir (yalnızca dış ağ hedefleri).
    public static async Task<PasifGozlem> GozlemAsync(SensorProfili p, string host, CancellationToken ct)
    {
        // Biçim ve döngü adresi (sensörün kendisi) kontrolü.
        if (!SensorAg.AdGecerli(host)) throw new InvalidOperationException("Geçersiz ad/adres: " + host);
        // Kapsam kapısı: yalnızca genel internet hedefi.
        var karar = await Kapsam.SiniflaAsync(host, ct); if (karar.Mod != KapsamModu.DisAgPasif) throw new KapsamIhlaliException("Pasif gözlem yalnızca dış ağ hedefleri içindir: " + karar.Neden);
        // Kilit.
        foreach (var t in new[] { ModulTuru.PasifHttp, ModulTuru.PasifTls, ModulTuru.PasifDns, ModulTuru.SecurityTxt }) Kapsam.KilitliyseDur(karar.Mod, t);
        // Ajanı çalıştır.
        var d = await SensorAg.ToplaAsync(p, new { mod = "pasif", host = karar.Host, org = DnsIstemci.OrgAlani(karar.Host) ?? "" }, "pasif", ct);
        // Çöz.
        var g = Coz(d, p.Ad, DateTime.UtcNow); if (!string.Equals(g.Host, karar.Host, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Sensör başka bir hedef için cevap verdi; sonuç atıldı.");
        // Sonuç.
        return g;
    }

    // JSON yardımcıları (tip uyuşmazlığına dayanıklı, uzunluk sınırlı).
    private static string S(JsonElement k, string ad, int en = 2000) => k.ValueKind == JsonValueKind.Object && k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? SensorCiktiAyristirici.Temizle(v.GetString() ?? "", en) : "";
    // Çok satırlı gövde (security.txt): satır sonları KORUNUR, diğer denetim karakterleri atılır, uzunluk sınırlanır.
    private static string Govde(JsonElement k, string ad, int en) { if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty(ad, out var v) || v.ValueKind != JsonValueKind.String) return ""; var t = new string((v.GetString() ?? "").Where(c => c is '\n' or '\r' or '\t' || !char.IsControl(c)).ToArray()); return t.Length > en ? t[..en] : t; }
    private static int I(JsonElement k, string ad) => k.ValueKind == JsonValueKind.Object && k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;
    private static List<string> L(JsonElement k, string ad, int en = 2000, int adet = 50) => k.ValueKind == JsonValueKind.Object && k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => SensorCiktiAyristirici.Temizle(x.GetString() ?? "", en)).Take(adet).ToList() : new();
    private static JsonElement O(JsonElement k, string ad) => k.ValueKind == JsonValueKind.Object && k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.Object ? v : default;
    private static DateTime? Tarih(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : null;

    // Ajan çıktısını PasifGozlem'e çevirir (test edilebilsin diye ayrı).
    internal static PasifGozlem Coz(JsonElement k, string yer, DateTime simdiUtc)
    {
        // Ana bilgisayar ve notlar.
        string host = S(k, "host", 253); var notlar = L(k, "notlar", 300, 20);
        // HTTP.
        HttpGozlem? http = null; var h = O(k, "http");
        if (h.ValueKind == JsonValueKind.Object)
        {
            // Başlıklar (küçük harf, en çok 100).
            var b = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (h.TryGetProperty("basliklar", out var bh) && bh.ValueKind == JsonValueKind.Object) foreach (var x in bh.EnumerateObject().Take(100)) if (x.Value.ValueKind == JsonValueKind.String) b[x.Name.ToLowerInvariant()] = SensorCiktiAyristirici.Temizle(x.Value.GetString() ?? "", 2000);
            // Kayıt.
            string hy = S(k, "http_yon", 12); http = new HttpGozlem(S(h, "url", 500), I(h, "durum"), b, L(h, "cerezler", 1000, 30), L(h, "yonler", 300, 5), hy is "evet" or "hayir" ? hy : "bilinmiyor");
        }
        // TLS.
        TlsGozlem? tls = null; var t = O(k, "tls");
        if (t.ValueKind == JsonValueKind.Object)
        {
            // Doğrulama metninden zincir hataları ve isim uyumu (süre dolumu ayrı kuralla raporlanır).
            string dog = S(t, "dogrulama", 300).ToLowerInvariant(); var zh = new List<string>(); bool isim = true;
            if (dog.Contains("hostname mismatch") || dog.Contains("ip address mismatch")) isim = false;
            else if (dog.Contains("self-signed") || dog.Contains("self signed")) zh.Add("UntrustedRoot");
            else if (dog.Contains("unable to get local issuer") || dog.Contains("unable to get issuer")) zh.Add("PartialChain");
            else if (dog.Length > 0 && !dog.Contains("expired")) zh.Add("ChainError");
            // Alt adlar.
            var san = S(t, "san", 1000).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Take(50).ToList();
            // Kayıt.
            tls = new TlsGozlem(S(t, "surum", 20), S(t, "sifre", 80), S(t, "subject", 300), S(t, "issuer", 300), Tarih(S(t, "baslangic", 30)), Tarih(S(t, "bitis", 30)), zh, san, S(t, "imza", 80), S(t, "anahtar_tur", 80), I(t, "anahtar_bit"), isim);
        }
        // DNS.
        DnsGozlem? dns = null; var d = O(k, "dns");
        if (d.ValueKind == JsonValueKind.Object)
        {
            // TXT içinden SPF, DMARC içinden DMARC.
            string? spf = L(d, "txt").FirstOrDefault(v => v.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)), dm = L(d, "dmarc").FirstOrDefault(v => v.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase));
            dns = new DnsGozlem(S(d, "org", 253), L(d, "mx", 300, 20), L(d, "ns", 300, 20), spf, dm, L(d, "caa", 300, 20), "");
        }
        // security.txt (yorumlama bu bilgisayarda).
        var sec = SecurityTxt.Yok(); var s = O(k, "sectxt");
        if (s.ValueKind == JsonValueKind.Object) { string govde = Govde(s, "govde", 20000); string url = S(s, "url", 500); bool https = s.TryGetProperty("https", out var hv) && hv.ValueKind == JsonValueKind.True; sec = SecurityTxt.Coz(govde, url, https, S(s, "tur", 100), simdiUtc); }
        // Sonuç.
        return new PasifGozlem(host, simdiUtc, "Sensör: " + yer, http, tls, dns, sec, notlar);
    }
}
