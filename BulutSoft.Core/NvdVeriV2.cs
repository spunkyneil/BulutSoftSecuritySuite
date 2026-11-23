// Bu dosya NVD (Ulusal Zafiyet Veritabanı) verisini ÇEVRİMDIŞI içe aktarır ve eşleştirme için kompakt bir indekse çevirir.
// Girdi: NVD API 2.0 biçiminde JSON dosyaları (kullanıcı kendisi indirir/hazırlar; uygulama envanter bilgisini NVD'ye GÖNDERMEZ: hangi yazılımın kurulu olduğu cihazdan çıkmaz).
// Yalnızca uygulama (part "a") CPE eşleşmeleri ve "vulnerable=true" olanlar alınır. Dosya TAMAMEN doğrulanmadan indeks yazılmaz; bozuksa eski indeks korunur (atomik).
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// V2 ad alanı.
namespace LabDenetim.V2;

// Bir CVE'nin bir ürün için etkilenen sürüm aralığı (null = sınırsız).
internal sealed record NvdAralik(string Cve, double Cvss, string? BasDahil, string? BasHaric, string? BitDahil, string? BitHaric, string Aciklama);

// NVD indeksi: "vendor:product" → aralıklar.
internal sealed class NvdIndeks
{
    public Dictionary<string, List<NvdAralik>> Urunler { get; set; } = new();
    public string KaynakOzeti { get; set; } = "";     // içe aktarılan dosyaların birleşik SHA-256 özeti
    public string Zaman { get; set; } = "";           // içe aktarma zamanı (UTC, ISO)
    public int CveSayisi { get; set; }
    public int DosyaSayisi { get; set; }
    public int AralikSayisi { get; set; }             // toplam aralık kaydı (sınır denetimi için)

    // Sınırlar.
    public const long EnCokDosyaBayt = 150_000_000; public const int EnCokAralik = 2_000_000;
    public static string Yol => BulutKlasor.Dosya("nvd_indeks.json");
    private static readonly JsonSerializerOptions Sec = new() { WriteIndented = false };

    // Ürün anahtarı: küçük harf, ters eğik çizgiler kaldırılır ("notepad\+\+" → "notepad++").
    public static string Anahtar(string vendor, string product) => (vendor.Replace("\\", "") + ":" + product.Replace("\\", "")).ToLowerInvariant();

    // Bir NVD 2.0 JSON dosyasının metnini çözer ve verilen indekse EKLER (indeks üzerinde değişiklik, başarısızsa çağıran bu kopyayı atar). Hata: KatalogHatasi.
    public static int Ekle(NvdIndeks hedef, string json)
    {
        JsonDocument d; try { d = JsonDocument.Parse(json); } catch (JsonException ex) { throw new KatalogHatasi("NVD dosyası geçerli JSON değil: " + ex.Message); }
        int cve = 0;
        using (d)
        {
            var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("vulnerabilities", out var v) || v.ValueKind != JsonValueKind.Array) throw new KatalogHatasi("NVD 2.0 biçimi değil: 'vulnerabilities' dizisi yok.");
            foreach (var oge in v.EnumerateArray())
            {
                if (!oge.TryGetProperty("cve", out var c) || c.ValueKind != JsonValueKind.Object) throw new KatalogHatasi("Bozuk kayıt: 'cve' nesnesi yok.");
                string id = c.TryGetProperty("id", out var idv) && idv.ValueKind == JsonValueKind.String ? idv.GetString()!.ToUpperInvariant() : ""; if (!Regex.IsMatch(id, @"^CVE-\d{4}-\d{4,7}$")) throw new KatalogHatasi("Geçersiz CVE numarası: " + (id.Length > 25 ? id[..25] : id));
                // Açıklama (İngilizce, kısa) ve en yüksek CVSS puanı.
                string acik = ""; if (c.TryGetProperty("descriptions", out var ds) && ds.ValueKind == JsonValueKind.Array) foreach (var x in ds.EnumerateArray()) if (x.TryGetProperty("lang", out var lg) && lg.GetString() == "en" && x.TryGetProperty("value", out var vv) && vv.ValueKind == JsonValueKind.String) { acik = Temiz(vv.GetString()!, 160); break; }
                double cvss = 0; if (c.TryGetProperty("metrics", out var m) && m.ValueKind == JsonValueKind.Object) foreach (var p in m.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.Array) foreach (var e in p.Value.EnumerateArray()) if (e.TryGetProperty("cvssData", out var cd) && cd.TryGetProperty("baseScore", out var bs) && bs.TryGetDouble(out var sc) && sc is >= 0 and <= 10) cvss = Math.Max(cvss, sc);
                bool eklendi = false;
                if (c.TryGetProperty("configurations", out var cf) && cf.ValueKind == JsonValueKind.Array)
                    foreach (var konf in cf.EnumerateArray()) if (konf.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
                        foreach (var nd in nodes.EnumerateArray()) if (nd.TryGetProperty("cpeMatch", out var cm) && cm.ValueKind == JsonValueKind.Array)
                            foreach (var e in cm.EnumerateArray())
                            {
                                if (!(e.TryGetProperty("vulnerable", out var vl) && vl.ValueKind == JsonValueKind.True) || !e.TryGetProperty("criteria", out var cr) || cr.ValueKind != JsonValueKind.String) continue;
                                var p = Bol(cr.GetString()!); if (p.Length < 6 || p[0] != "cpe" || p[2] != "a") continue;   // yalnızca uygulamalar
                                string anahtar = Anahtar(p[3], p[4]); string? Al(string ad) => e.TryGetProperty(ad, out var z) && z.ValueKind == JsonValueKind.String ? z.GetString() : null;
                                string? bd = Al("versionStartIncluding"), bh = Al("versionStartExcluding"), td = Al("versionEndIncluding"), th = Al("versionEndExcluding");
                                // Aralık yoksa ve sürüm belirtilmişse tek sürüm; ikisi de yoksa ("*") bu ürünün TÜM sürümleri etkilidir.
                                if (bd == null && bh == null && td == null && th == null && p[5] is not ("*" or "-" or "")) { bd = p[5]; td = p[5]; }
                                if (!hedef.Urunler.TryGetValue(anahtar, out var l)) hedef.Urunler[anahtar] = l = new();
                                if (hedef.AralikSayisi >= EnCokAralik) throw new KatalogHatasi("İndeks sınırına ulaşıldı.");
                                l.Add(new NvdAralik(id, cvss, bd, bh, td, th, acik)); hedef.AralikSayisi++; eklendi = true;
                            }
                if (eklendi) cve++;
            }
        }
        hedef.CveSayisi += cve; return cve;
    }
    // CPE metnini bölür ("\:" kaçışlı iki nokta korunur).
    private static string[] Bol(string s) => Regex.Split(s, @"(?<!\\):");
    private static string Temiz(string s, int en) { s = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim(); return s.Length > en ? s[..en] : s; }

    // Dosyaları içe aktarır: TÜMÜ doğrulanmadan indeks yazılmaz; sonuç atomik yazılır. Mevcut indeksle BİRLEŞTİRİR (aynı CVE tekrarlanmaz).
    public static NvdIndeks IceAktar(IEnumerable<string> dosyaYollari, DateTime simdiUtc)
    {
        var yeni = Yukle() ?? new NvdIndeks(); var ozet = SHA256.Create(); int adet = 0;
        foreach (var y in dosyaYollari)
        {
            var fi = new FileInfo(y); if (!fi.Exists) throw new KatalogHatasi("Dosya bulunamadı: " + Path.GetFileName(y)); if (fi.Length > EnCokDosyaBayt) throw new KatalogHatasi($"{fi.Name}: dosya çok büyük (en çok {EnCokDosyaBayt / 1_000_000} MB).");
            string metin = File.ReadAllText(y, Encoding.UTF8); var gecici = new NvdIndeks();   // önce boş indekse: doğrulama; başarılıysa birleştirilir
            Ekle(gecici, metin); Birlestir(yeni, gecici); ozet.TransformBlock(Encoding.UTF8.GetBytes(metin), 0, Encoding.UTF8.GetByteCount(metin), null, 0); adet++;
        }
        ozet.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        yeni.KaynakOzeti = Convert.ToHexString(ozet.Hash!).ToLowerInvariant(); yeni.Zaman = simdiUtc.ToString("O"); yeni.DosyaSayisi += adet; yeni.CveSayisi = yeni.Urunler.Values.SelectMany(x => x).Select(x => x.Cve).Distinct().Count();
        Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, JsonSerializer.Serialize(yeni, Sec)); return yeni;
    }

    // Aynı (ürün, CVE, aralık) tekrar eklenmez.
    private static void Birlestir(NvdIndeks hedef, NvdIndeks kaynak)
    {
        foreach (var (a, l) in kaynak.Urunler) { if (!hedef.Urunler.TryGetValue(a, out var h)) hedef.Urunler[a] = h = new(); foreach (var r in l) if (!h.Contains(r)) { if (hedef.AralikSayisi >= EnCokAralik) throw new KatalogHatasi("İndeks sınırına ulaşıldı."); h.Add(r); hedef.AralikSayisi++; } }
    }

    // Kayıtlı indeksi yükler (yoksa/bozuksa null).
    public static NvdIndeks? Yukle() { try { return File.Exists(Yol) ? JsonSerializer.Deserialize<NvdIndeks>(File.ReadAllText(Yol), Sec) : null; } catch { return null; } }
    // Kısa özet.
    public string Ozetle() => $"NVD indeksi: {CveSayisi} CVE, {Urunler.Count} ürün, {DosyaSayisi} dosya, özet {(KaynakOzeti.Length >= 12 ? KaynakOzeti[..12] : KaynakOzeti)}";
}
