// Bu dosya yeni arayüzün TİPOGRAFİ ROLLERİNİ tek yerden sunar: Display, Heading, Subheading, Body, Caption, Mono.
// Yazı tipleri bir kez üretilip önbellekte tutulur (her çizimde new Font yok); uygulama kapanırken topluca serbest bırakılır.
// Dönen Font'lar PAYLAŞILIR: çağıran onları Dispose ETMEMELİDİR.
// Boyutlar punto cinsindendir; Windows yüksek DPI'da ölçeği kendisi uygular.

// Programımızın adı.
namespace LabDenetim;

// Tipografi rolleri.
internal enum FontRol { Display, Heading, Subheading, Body, Caption, Mono }

// Önbellekli yazı tipi sağlayıcısı.
internal static class FontProvider
{
    private static readonly Dictionary<(FontRol, FontStyle), Font> Onbellek = new();
    private static readonly object Kilit = new();
    private static bool _kayitli;
    private static string? _monoAilesi;

    // Rolün boyutu (punto) ve stili.
    public static (float Punto, FontStyle Stil) Olcu(FontRol rol) => rol switch
    {
        FontRol.Display => (20f, FontStyle.Bold),
        FontRol.Heading => (14f, FontStyle.Bold),
        FontRol.Subheading => (11f, FontStyle.Bold),
        FontRol.Body => (10f, FontStyle.Regular),
        FontRol.Caption => (8.5f, FontStyle.Regular),
        _ => (9.5f, FontStyle.Regular)
    };

    // Rolün yazı tipi (paylaşımlı; Dispose etmeyin).
    public static Font Al(FontRol rol) => Al(rol, Olcu(rol).Stil);

    // Rolün aynı boyutta, belirtilen stilde (ör. seçili öğe için kalın) yazı tipi (paylaşımlı; Dispose etmeyin).
    public static Font Al(FontRol rol, FontStyle stil)
    {
        lock (Kilit)
        {
            if (Onbellek.TryGetValue((rol, stil), out var f)) return f;
            var (punto, _) = Olcu(rol);
            f = new Font(rol == FontRol.Mono ? MonoAilesi() : "Segoe UI", punto, stil, GraphicsUnit.Point);
            Onbellek[(rol, stil)] = f;
            if (!_kayitli) { _kayitli = true; Application.ApplicationExit += (_, _) => Temizle(); }
            return f;
        }
    }

    // Eşit aralıklı yazı tipi ailesi: Cascadia Mono varsa o, yoksa Consolas.
    private static string MonoAilesi()
    {
        if (_monoAilesi != null) return _monoAilesi;
        using var koleksiyon = new System.Drawing.Text.InstalledFontCollection();
        _monoAilesi = koleksiyon.Families.Any(a => string.Equals(a.Name, "Cascadia Mono", StringComparison.OrdinalIgnoreCase)) ? "Cascadia Mono" : "Consolas";
        return _monoAilesi;
    }

    // Çizim sırasında ÇAĞIRANA AİT geçici yazı tipi (boyut çizim alanına göre değişiyorsa, piksel biriminde ya da simge ailesi gerekiyorsa). Çağıran `using` ile Dispose ETMELİDİR.
    public static Font Gecici(string aile, float boyut, FontStyle stil = FontStyle.Regular, GraphicsUnit birim = GraphicsUnit.Point) => new Font(aile, boyut, stil, birim);

    // Önbelleği boşaltır ve yazı tiplerini serbest bırakır (uygulama kapanırken ve testlerde).
    public static void Temizle()
    {
        lock (Kilit) { foreach (var f in Onbellek.Values) f.Dispose(); Onbellek.Clear(); }
    }

    // Önbellekteki yazı tipi sayısı (test için).
    internal static int OnbellekSayisi { get { lock (Kilit) return Onbellek.Count; } }
}
