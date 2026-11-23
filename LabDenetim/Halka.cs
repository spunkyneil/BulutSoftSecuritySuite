// Bu dosya "Güvenlik Puanı" kartındaki dairesel kadranı (gauge / puan halkası) çizer.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// Puanı (0-100) parlak bir halka olarak gösteren özel çizim kutusu.
internal sealed class PuanHalkasi : Control, IThemeAware
{
    // Tema değişince yeniden çizilir (renkler çizim anında TemaYonetici'den okunur).
    public void TemaUygula() => Invalidate();
    // Halkanın şu an ekranda gösterdiği değer (yumuşak geçiş için hedefe yavaşça yaklaşır).
    private float _gosterilen;
    // Ulaşılmak istenen puan; yoksa (tarama yapılmadıysa) halka boş görünür.
    private int? _hedef;
    // Yumuşak geçişi sağlayan zamanlayıcı.
    private readonly System.Windows.Forms.Timer _zaman = new() { Interval = 16 };

    // Halkayı kurar.
    public PuanHalkasi()
    {
        // Titremesiz çizim ve saydam arka plan ayarları.
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        // Arka planı saydam yap: altındaki kartın rengi görünsün.
        BackColor = Color.Transparent;
        // Her zaman tıkında gösterilen değeri hedefe biraz yaklaştır.
        _zaman.Tick += (_, _) =>
        {
            // Hedef yoksa durdur.
            if (_hedef is null) { _zaman.Stop(); return; }
            // Hedefle şimdiki değer arasındaki fark.
            float fark = _hedef.Value - _gosterilen;
            // Fark çok küçüldüyse tam hedefe otur ve dur.
            if (Math.Abs(fark) < 0.4f) { _gosterilen = _hedef.Value; _zaman.Stop(); }
            // Değilse farkın %15'i kadar ilerle (yavaşlayarak yaklaşma etkisi).
            else _gosterilen += fark * 0.15f;
            // Yeniden çiz.
            Invalidate();
        };
    }

    // Gösterilecek puan: null ise halka boş, sayı ise o kadar dolu.
    // (Tasarımcıda görünmesin ve kayda geçmesin diye iki işaret konuldu.)
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int? Puan
    {
        // Mevcut hedefi ver.
        get => _hedef;
        // Yeni hedefi ayarla.
        set
        {
            // Yeni hedefi sakla.
            _hedef = value;
            // Kadran puanı ANLIK göstersin: gösterilen değer hemen puana eşitlenir (puan yoksa boş).
            _gosterilen = value ?? 0;
            // Eski yumuşak geçiş zamanlayıcısı artık gerekmiyor.
            _zaman.Stop();
            // Hemen yeniden çiz.
            Invalidate();
        }
    }

    // Halkayı çizer.
    protected override void OnPaint(PaintEventArgs e)
    {
        // Çizim yüzeyi.
        var g = e.Graphics;
        // Kenarlar pürüzsüz olsun.
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Halkanın kalınlığı.
        const float kalin = 8f;
        // Halkanın çapı: boş yere göre küçük olanı al; üstte başlık için 22 piksel yer bırak.
        float cap = Math.Min(Width - 8, Height - 26) - kalin;
        // Çok küçükse çizme.
        if (cap < 20) return;
        // Halkanın yatayda ortalanmış, dikeyde başlığın altındaki yeri.
        var alan = new RectangleF((Width - cap) / 2f, 22 + kalin / 2f, cap, cap);
        // Halkanın boş (iz) kısmı: koyu gri-mavi, uçları yuvarlak. Alt tarafı açık bir yay (135°'den 270°).
        using (var iz = new Pen(TemaYonetici.Mod == TemaModu.Koyu ? DesignTokens.Ozgun(TokenRol.SurfaceElevated) : ColorTranslator.FromHtml("#CBD5E1"), kalin) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(iz, alan, 135, 270);
        // Puan yoksa sadece boş halkayı ve "--" yazısını göster.
        Color renk = TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted));
        // Puan varsa rengi puana göre seç: 80+ yeşil, 50+ turuncu, altı kırmızı.
        if (_hedef is int hedef) renk = TemaYonetici.C(hedef >= 80 ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : hedef >= 50 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusDanger));
        // Dolu kısmın uzunluğu (derece): 270 derecenin puan oranı kadarı.
        float tarama = 270f * Math.Clamp(_gosterilen, 0f, 100f) / 100f;
        // Dolu kısım varsa çiz.
        if (_hedef is not null && tarama >= 1f)
        {
            // Parlama (glow) efekti: daha kalın ve saydam bir yay altta.
            using (var parlama = new Pen(DesignTokens.Saydam(renk, 60), kalin + 7) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(parlama, alan, 135, tarama);
            // Asıl parlak dolu yay.
            using (var kalem = new Pen(renk, kalin) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(kalem, alan, 135, tarama);
        }
        // Ortadaki yazıyı hazırla: puan varsa sayı, yoksa çizgi.
        string metin = _hedef is null ? "--" : Math.Round(_gosterilen).ToString("0");
        // Büyük puan yazısı.
        using var font = FontProvider.Gecici("Segoe UI", 15f, FontStyle.Bold);
        // Yazıyı halkanın ortasına, biraz yukarıya koy.
        var yaziAlani = new Rectangle((int)alan.X, (int)alan.Y, (int)alan.Width, (int)alan.Height - 8);
        // Puanı çiz.
        TextRenderer.DrawText(g, metin, font, yaziAlani, _hedef is null ? TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)) : TemaYonetici.C(Color.White), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        // Küçük "/100" yazısı.
        using var kucuk = FontProvider.Gecici("Segoe UI", 7.5f, FontStyle.Bold);
        // "/100" yazısının yeri: puanın hemen altı.
        var altAlan = new Rectangle((int)alan.X, (int)(alan.Y + alan.Height / 2f + 8), (int)alan.Width, 14);
        // "/100" yazısını çiz.
        TextRenderer.DrawText(g, "/100", kucuk, altAlan, TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)), TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
    }

    // Bellek temizliği: zamanlayıcıyı da kapat.
    protected override void Dispose(bool disposing)
    {
        // Zamanlayıcıyı temizle.
        if (disposing) _zaman.Dispose();
        // Windows'un kendi temizliği.
        base.Dispose(disposing);
    }
}
