// Bu dosya CISA "Bilinen Sömürülen Açıklar" (KEV) listesinin ÇEVRİMDIŞI deposunu ve ONAYLI indiricisini içerir.
// İndirme yalnızca VERİ ÇEKME yönündedir: tek bir GET isteği, sabit HTTPS adresi (izinli sunucu listesi), bilgisayardan hiçbir veri gönderilmez.
// Her indirmeyi kullanıcı onaylar (arayüzde). Dosya TAMAMEN doğrulanmadan yazılmaz; bozuksa eski besleme korunur; SHA-256 özeti ve indirme zamanı kaydedilir.
// Dürüstlük: yalnızca KEV (sömürüldüğü bilinen açıklar) indirilir. NVD (CVE/CPE ayrıntıları) bu adımda indirilmez; Hafta 6'da eşleştirme tasarlanırken ayrıca ele alınacaktır.
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// V2 ad alanı.
namespace LabDenetim.V2;

// KEV listesindeki tek açık.
internal sealed record KevKaydi(string Cve, string Vendor, string Product, string Ad, DateTime Eklendi, DateTime? SonTarih, bool FidyeKullanimi);

// KEV besleme deposu.
internal sealed class VulnFeedStore
{
    // Besleme sürümü, yayın tarihi (metin), özet ve indirme bilgisi.
    public string Surum { get; }
    public string Yayin { get; }
    public string Ozet { get; }
    public IReadOnlyDictionary<string, KevKaydi> Kayitlar { get; }
    private VulnFeedStore(string surum, string yayin, string ozet, Dictionary<string, KevKaydi> k) { Surum = surum; Yayin = yayin; Ozet = ozet; Kayitlar = k; }

    // Sınırlar.
    public const int EnCokKayit = 10_000; public const long EnCokBayt = 20_000_000;

    public static string Yol => BulutKlasor.Dosya("kev_besleme.json");
    public static string MetaYol => BulutKlasor.Dosya("kev_besleme.meta.json");

    // Bir CVE sömürüldüğü bilinenler arasında mı?
    public bool KevMi(string cve) => Kayitlar.ContainsKey((cve ?? "").Trim().ToUpperInvariant());

    // JSON'u tamamen doğrulayarak çözer; hata varsa KatalogHatasi (kısmi veri kabul edilmez).
    public static VulnFeedStore Coz(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > EnCokBayt) throw new KatalogHatasi("KEV dosyası boş veya çok büyük.");
        JsonDocument d; try { d = JsonDocument.Parse(json); } catch (JsonException ex) { throw new KatalogHatasi("KEV dosyası geçerli JSON değil: " + ex.Message); }
        using (d)
        {
            var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object) throw new KatalogHatasi("KEV dosyası bir nesne olmalı.");
            string surum = Metin(k, "catalogVersion"), yayin = Metin(k, "dateReleased");
            if (surum.Length is 0 or > 40) throw new KatalogHatasi("catalogVersion eksik veya geçersiz.");
            if (!k.TryGetProperty("vulnerabilities", out var v) || v.ValueKind != JsonValueKind.Array) throw new KatalogHatasi("vulnerabilities dizisi eksik.");
            var liste = new Dictionary<string, KevKaydi>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in v.EnumerateArray())
            {
                if (liste.Count >= EnCokKayit) throw new KatalogHatasi("En çok " + EnCokKayit + " kayıt kabul edilir.");
                string cve = Metin(o, "cveID").ToUpperInvariant(); if (!Regex.IsMatch(cve, @"^CVE-\d{4}-\d{4,7}$")) throw new KatalogHatasi("Geçersiz CVE numarası: " + (cve.Length > 25 ? cve[..25] : cve));
                if (!Tarih(Metin(o, "dateAdded"), out var eklendi)) throw new KatalogHatasi($"{cve}: dateAdded yyyy-MM-dd biçiminde olmalı.");
                DateTime? son = Tarih(Metin(o, "dueDate"), out var sonT) ? sonT : null;
                liste[cve] = new KevKaydi(cve, Kes(Metin(o, "vendorProject"), 100), Kes(Metin(o, "product"), 100), Kes(Metin(o, "vulnerabilityName"), 200), eklendi, son, string.Equals(Metin(o, "knownRansomwareCampaignUse"), "Known", StringComparison.OrdinalIgnoreCase));
            }
            if (liste.Count == 0) throw new KatalogHatasi("KEV listesi boş.");
            return new VulnFeedStore(surum, yayin, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant(), liste);
        }
    }
    private static string Metin(JsonElement o, string ad) => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
    private static string Kes(string s, int en) { s = new string(s.Where(c => !char.IsControl(c)).ToArray()); return s.Length > en ? s[..en] : s; }
    private static bool Tarih(string s, out DateTime t) => DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out t);

    // İçe aktarır: önce doğrular, sonra ATOMİK yazar (hata olursa eski dosyalara dokunulmaz). Kaynak ve zaman meta dosyasına yazılır.
    public static VulnFeedStore IceAktar(string json, string kaynak, DateTime simdiUtc)
    {
        var s = Coz(json); Directory.CreateDirectory(BulutKlasor.Kok);
        Depo.Yaz(Yol, json); Depo.Yaz(MetaYol, JsonSerializer.Serialize(new { kaynak, indirme_zamani = simdiUtc.ToString("O"), ozet = s.Ozet, surum = s.Surum, adet = s.Kayitlar.Count })); return s;
    }

    // Kayıtlı beslemeyi yükler (yoksa/bozuksa null).
    public static VulnFeedStore? Yukle() { try { return File.Exists(Yol) ? Coz(File.ReadAllText(Yol)) : null; } catch { return null; } }
    // Meta bilgisi (indirme zamanı vb.), yoksa null.
    public static (string Kaynak, string Zaman)? Meta() { try { if (!File.Exists(MetaYol)) return null; using var d = JsonDocument.Parse(File.ReadAllText(MetaYol)); return (d.RootElement.GetProperty("kaynak").GetString() ?? "", d.RootElement.GetProperty("indirme_zamani").GetString() ?? ""); } catch { return null; } }
    // Kısa özet metni.
    public string Ozetle() => $"KEV listesi: sürüm {Surum}, {Kayitlar.Count} açık, özet {Ozet[..12]}";
}

// Onaylı indirici (onayı çağıran arayüz alır; bu sınıf yalnızca güvenli indirmeyi yapar).
internal static class KevIndirici
{
    // Sabit adres ve izinli sunucu.
    public const string Adres = "https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json";
    public static readonly string[] IzinliSunucular = { "www.cisa.gov" };

    // Beslemeyi indirir, doğrular ve kaydeder. isleyici: testte sahte sunucu. Hata olursa KatalogHatasi / HttpRequestException fırlatır ve ESKİ BESLEME KORUNUR.
    public static async Task<VulnFeedStore> IndirAsync(HttpMessageHandler? isleyici, DateTime simdiUtc, CancellationToken ct, string adres = Adres)
    {
        // Yalnızca HTTPS ve izinli sunucu (adres parametresi yalnızca testte farklı verilir; yine de aynı denetimden geçer).
        if (!Uri.TryCreate(adres, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || !IzinliSunucular.Contains(u.Host, StringComparer.OrdinalIgnoreCase)) throw new KatalogHatasi("İzin verilmeyen adres: yalnızca " + string.Join(", ", IzinliSunucular) + " (HTTPS).");
        // Merkezi ağ istemcisi (bağlantı 15 sn, yanıt başlığı 60 sn, yönlendirme izlenmez); isleyici yalnızca testte sahte sunucu için verilir.
        using var istek = new HttpRequestMessage(HttpMethod.Get, u);
        using var y = await AgIstemciFabrikasi.GonderAsync(AgProfili.KevBeslemesi, istek, HttpCompletionOption.ResponseHeadersRead, isleyici, ct);
        // Yönlendirme izlenmez (başka bir adrese gitmeyiz).
        if ((int)y.StatusCode is >= 300 and < 400) throw new KatalogHatasi("Sunucu yönlendirme döndürdü; güvenlik için izlenmedi.");
        if (!y.IsSuccessStatusCode) throw new KatalogHatasi($"İndirme başarısız: HTTP {(int)y.StatusCode}.");
        if (y.Content.Headers.ContentLength is long len && len > VulnFeedStore.EnCokBayt) throw new KatalogHatasi("Dosya çok büyük.");
        // Sınırlı okuma.
        await using var st = await y.Content.ReadAsStreamAsync(ct); using var ms = new MemoryStream(); var buf = new byte[16384]; int n;
        while ((n = await st.ReadAsync(buf, ct)) > 0) { ms.Write(buf, 0, n); if (ms.Length > VulnFeedStore.EnCokBayt) throw new KatalogHatasi("Dosya çok büyük."); }
        return VulnFeedStore.IceAktar(Encoding.UTF8.GetString(ms.ToArray()), adres, simdiUtc);
    }
}
