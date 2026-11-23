// Bu dosya Kali araç şablonlarının TEK KAYNAĞINI (gömülü kali_araclari.json) yükler, doğrular ve iki yere dağıtır:
//   1) Windows tarafı: KomutSablonuDeposu'na KomutSablonu olarak kaydedilir (parametre kuralları, süreler, hedef türü).
//   2) Sensör tarafı: aynı JSON metni KomutAjani'nın içine gömülür (gerçek argüman listeleri YALNIZCA burada tanımlıdır; C# ve Python'da ikili tanım yoktur).
// Tablo kasıtlı olarak yalnızca programın içine gömülüdür: kullanıcı dosyasıyla geçersiz kılınamaz (argüman listesi kod gibidir; yerel bir saldırgan araç ekleyemesin).
// Her tanım yüklenirken sıkı doğrulanır; tek bir hatalı tanım tüm tabloyu reddeder (kısmi/yarım yükleme yok).
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Tablodaki bir parametre. Tur: "" (düz değer), "dosya" (sensörde ~/bulutsoft_inceleme içindeki dosya adı), "arayuz" (sensörde var olan ağ arayüzü).
internal sealed record TanimParametre(string Ad, string Desen, string? Varsayilan, int? Min = null, int? Max = null, string? Tur = null);

// Tablodaki bir araç tanımı. Dogrulandi: gerçek Kali'de denenip doğrulandı mı (şimdilik hiçbiri değil: bayraklar belgeye göre yazıldı).
internal sealed record KomutTanimi(string Ad, string Kategori, string Arac, string? Paket, bool Sudo, int Tahmini, int ZamanAsimi, string HedefTuru, string? Ayirici, bool Internet, bool ArkaPlan, bool Dogrulandi, string? Aciklama, List<TanimParametre>? Parametreler, List<string>? Argv, Dictionary<string, string>? Ortam);

// Tablo yükleyici.
internal static class KomutKaydi
{
    // Gömülü kaynak adı.
    public const string KaynakAdi = "kali_araclari.json";
    private static readonly JsonSerializerOptions Js = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly Regex YerTutucu = new(@"\{([a-z_]+)\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex OrtamAdi = new(@"\A[A-Z_][A-Z0-9_]{0,31}\z", RegexOptions.CultureInvariant);
    private static readonly object Kilit = new();
    private static List<KomutTanimi>? _tanimlar; private static string? _json;

    // Yüklenmiş mi?
    public static bool Yuklendi { get { lock (Kilit) return _tanimlar != null; } }

    // Gömülü tablonun doğrulanmış tanımları (ilk erişimde yüklenir; hatalıysa InvalidOperationException).
    public static IReadOnlyList<KomutTanimi> Tanimlar { get { Yukle(); lock (Kilit) return _tanimlar!; } }

    // Sensör ajanına gömülecek tablo metni (doğrulanmış JSON).
    public static string AjanTablosuJson { get { Yukle(); lock (Kilit) return _json!; } }

    // Gömülü kaynağı bir kez okur ve doğrular.
    private static void Yukle()
    {
        // Zaten yüklü.
        lock (Kilit) { if (_tanimlar != null) return; }
        string metin; using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(KaynakAdi)) { if (s == null) throw new InvalidOperationException("Gömülü araç tablosu (" + KaynakAdi + ") bulunamadı."); using var r = new StreamReader(s, System.Text.Encoding.UTF8); metin = r.ReadToEnd(); }
        var liste = Coz(metin);
        lock (Kilit) { _tanimlar ??= liste; _json ??= metin; }
    }

    // Tablo metnini çözer ve her tanımı doğrular. Herhangi bir hata varsa HEPSİ reddedilir (InvalidOperationException; tüm hatalar iletide).
    internal static List<KomutTanimi> Coz(string json)
    {
        // Çözümleme.
        List<KomutTanimi>? liste;
        try { liste = JsonSerializer.Deserialize<List<KomutTanimi>>(json, Js); } catch (JsonException ex) { throw new InvalidOperationException("Araç tablosu çözülemedi: " + ex.Message); }
        if (liste == null || liste.Count == 0) throw new InvalidOperationException("Araç tablosu boş.");
        // Doğrulama.
        var hatalar = new List<string>(); var adlar = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in liste)
        {
            var h = t == null ? "boş kayıt" : TanimHatasi(t); if (h == null && !adlar.Add(t!.Ad)) h = "ad tablodaki başka bir tanımla aynı";
            if (h != null) hatalar.Add($"{t?.Ad ?? "?"}: {h}");
        }
        if (hatalar.Count > 0) throw new InvalidOperationException("Araç tablosu geçersiz: " + string.Join(" | ", hatalar));
        return liste;
    }

    // Tanımı KomutSablonu'na çevirir.
    internal static KomutSablonu Sablona(KomutTanimi t) => new(t.Ad, t.Kategori, t.Sudo, t.Tahmini, t.ZamanAsimi,
        (t.Parametreler ?? new()).Select(p => new ParametreKurali(p.Ad, p.Desen, p.Varsayilan ?? "", p.Min, p.Max)).ToArray(), t.Ayirici ?? "", t.Arac, string.IsNullOrEmpty(t.Paket) ? t.Arac.ToLowerInvariant() : t.Paket, t.HedefTuru);

    // Tek tanımın tutarlılığı: şablon kuralları + argüman listesi + yer tutucular + hedef/dosya/arayüz/arka plan kuralları. Hata yoksa null.
    internal static string? TanimHatasi(KomutTanimi t)
    {
        // Genel şablon kuralları (ad, kategori, süreler, parametre adları ve desenleri, araç/paket adı, hedef türü).
        if (string.IsNullOrEmpty(t.Arac)) return "araç adı boş";
        var h = KomutSablonuDeposu.SablonHatasi(Sablona(t)); if (h != null) return h;
        // Ayrıştırıcı kayıtlı olmalı (boşsa varsayılan).
        if (!string.IsNullOrEmpty(t.Ayirici) && AyiriciDeposu.Getir(t.Ayirici) == null) return $"'{t.Ayirici}' ayrıştırıcısı kayıtlı değil";
        // Argüman listesi.
        var argv = t.Argv; if (argv == null || argv.Count == 0) return "argüman listesi boş";
        if (argv[0] != t.Arac) return "argüman listesinin ilk öğesi araç adıyla aynı olmalı (kurulu mu denetimi bu ada bakar)";
        var parametreler = t.Parametreler ?? new(); var adlar = parametreler.Select(p => p.Ad).ToHashSet(StringComparer.Ordinal); var kullanilan = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in argv)
        {
            // Yazdırılabilir ASCII (satır sonu ve Unicode yok).
            if (o == null || o.Any(c => c < ' ' || c > '~')) return "argüman yalnızca yazdırılabilir ASCII karakterler içerebilir";
            // Yer tutucular: yalnızca hedef, inceleme, zaman ve tanımlı parametreler; başka kıvrık ayraç yok.
            foreach (Match m in YerTutucu.Matches(o)) { string ad = m.Groups[1].Value; if (ad is "hedef" or "inceleme" or "zaman") kullanilan.Add(ad); else if (adlar.Contains(ad)) kullanilan.Add(ad); else return $"bilinmeyen yer tutucu {{{ad}}}"; }
            if (YerTutucu.Replace(o, "").IndexOfAny(new[] { '{', '}' }) >= 0) return "argümanda geçersiz kıvrık ayraç var";
        }
        // Her parametre kullanılmalı.
        foreach (var p in parametreler) { if (!kullanilan.Contains(p.Ad)) return $"'{p.Ad}' parametresi argüman listesinde kullanılmıyor"; if (p.Tur is not (null or "" or "dosya" or "arayuz")) return $"'{p.Ad}' parametresinin türü geçersiz"; if (p.Tur == "dosya" && !kullanilan.Contains("inceleme")) return "dosya parametresi {inceleme} yer tutucusunu gerektirir"; }
        // Hedef kuralları.
        bool hedefVar = kullanilan.Contains("hedef"); if (t.HedefTuru == "yok" && hedefVar) return "hedef almayan şablonda {hedef} kullanılamaz"; if (t.HedefTuru != "yok" && !hedefVar) return "hedef alan şablonda {hedef} kullanılmalı";
        // Arka plan (masaüstü) araçları: hedefsiz, sudo'suz.
        if (t.ArkaPlan && (t.HedefTuru != "yok" || t.Sudo)) return "arka planda açılan araç hedef almamalı ve sudo kullanmamalı";
        // Ortam değişkenleri.
        foreach (var (k, v) in t.Ortam ?? new()) { if (!OrtamAdi.IsMatch(k) || v == null || v.Any(c => c < ' ' || c > '~')) return "ortam değişkeni geçersiz"; if (k is "PATH" or "LD_PRELOAD" or "LD_LIBRARY_PATH") return $"{k} ortam değişkeni değiştirilemez"; }
        return null;
    }

    // Tablodaki her tanımı KomutSablonuDeposu'na kaydeder (zaten kayıtlı adlar atlanır). Kaydedilen sayıyı verir.
    public static int Hazirla()
    {
        int n = 0;
        foreach (var t in Tanimlar) { if (KomutSablonuDeposu.Getir(t.Ad) != null) continue; KomutSablonuDeposu.Ekle(Sablona(t)); n++; }
        return n;
    }

    // Adıyla tanımı verir (yoksa null).
    public static KomutTanimi? Tanim(string ad) => Tanimlar.FirstOrDefault(t => t.Ad == ad);
}
