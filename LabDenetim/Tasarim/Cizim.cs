// Bu dosya yeni kabuk bileşenlerinin ORTAK ÇİZİM YARDIMCILARIDIR: yuvarlak köşeli dikdörtgen, kalite ayarı, tokenlı metin çizimi.
// Renk ve yazı tipi her zaman DesignTokens / FontProvider'dan alınır; burada sabit renk yoktur.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// Çizim yardımcıları.
internal static class Cizim
{
    // Yuvarlak köşeli dikdörtgen yolu (çağıran Dispose eder).
    public static GraphicsPath Yuvarlak(RectangleF r, float yaricap)
    {
        var p = new GraphicsPath(); float d = Math.Max(0.01f, Math.Min(yaricap * 2f, Math.Min(r.Width, r.Height)));
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }

    // Yumuşak kenar ayarı (tüm özel çizimler önce bunu çağırır).
    public static void Kalite(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; }

    // Yuvarlak köşeli dolu şekil.
    public static void Doldur(Graphics g, RectangleF r, float yaricap, Color renk)
    {
        using var yol = Yuvarlak(r, yaricap); using var f = new SolidBrush(renk); g.FillPath(f, yol);
    }

    // Yuvarlak köşeli çerçeve (içeri doğru çizilir, kenara taşmaz).
    public static void Cerceve(Graphics g, RectangleF r, float yaricap, Color renk, float kalinlik = 1f)
    {
        var ic = new RectangleF(r.X + kalinlik / 2f, r.Y + kalinlik / 2f, r.Width - kalinlik, r.Height - kalinlik);
        using var yol = Yuvarlak(ic, yaricap); using var k = new Pen(renk, kalinlik); g.DrawPath(k, yol);
    }

    // Tipografi rolüyle metin çizer (GDI; tek satır, taşarsa "…").
    public static void Metin(Graphics g, string metin, FontRol rol, Rectangle alan, Color renk, TextFormatFlags bayrak = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding, FontStyle? stil = null)
    {
        if (string.IsNullOrEmpty(metin)) return;
        var f = stil == null ? FontProvider.Al(rol) : FontProvider.Al(rol, stil.Value);
        // '&' işareti kısayol öneki sayılmasın ("Ağ & Cihaz" aynen yazılsın).
        TextRenderer.DrawText(g, metin, f, alan, renk, bayrak | TextFormatFlags.NoPrefix);
    }

    // Metni KELİME SINIRLARINDA satırlara böler (saf işlev: olc, bir metnin piksel genişliğini veren ölçü işlevidir). Kelime ortadan bölünmez; tek başına satıra sığmayan çok uzun kelime kendi satırında kalır.
    // enFazla > 0 ve metin daha uzunsa son satır "…" ile biter.
    public static List<string> KelimeSatirlari(Func<string, int> olc, string metin, int genislik, int enFazla = 0)
    {
        var satirlar = new List<string>(); var kelimeler = metin.Replace("\r", "").Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries); string cari = "";
        foreach (var k in kelimeler)
        {
            string aday = cari.Length == 0 ? k : cari + " " + k;
            if (cari.Length == 0 || olc(aday) <= genislik) cari = aday; else { satirlar.Add(cari); cari = k; }
        }
        if (cari.Length > 0) satirlar.Add(cari);
        if (enFazla > 0 && satirlar.Count > enFazla)
        {
            string son = string.Join(" ", satirlar.Skip(enFazla - 1)); satirlar = satirlar.Take(enFazla - 1).ToList();
            while (son.Length > 1 && olc(son + "…") > genislik) { int bosluk = son.LastIndexOf(' '); son = bosluk > 0 ? son[..bosluk] : son[..^1]; }
            satirlar.Add(son + "…");
        }
        return satirlar;
    }

    // Tipografi rolüyle, kelime sınırında satır atlayarak (en fazla enFazla satır; taşarsa "…") üstten başlayıp metin çizer. GDI'nın dar alandaki kelime ortası kırılmasını önler.
    public static void MetinSarili(Graphics g, string metin, FontRol rol, Rectangle alan, Color renk, int enFazla, FontStyle? stil = null)
    {
        if (string.IsNullOrEmpty(metin)) return;
        var f = stil == null ? FontProvider.Al(rol) : FontProvider.Al(rol, stil.Value);
        const TextFormatFlags olcBayrak = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        int satirYuk = TextRenderer.MeasureText(g, "Ağ", f, new Size(int.MaxValue, int.MaxValue), olcBayrak).Height; int y = alan.Y;
        foreach (var s in KelimeSatirlari(x => TextRenderer.MeasureText(g, x, f, new Size(int.MaxValue, int.MaxValue), olcBayrak).Width, metin, alan.Width, enFazla))
        {
            if (y + satirYuk > alan.Bottom + 2) break;
            TextRenderer.DrawText(g, s, f, new Rectangle(alan.X, y, alan.Width, satirYuk), renk, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine); y += satirYuk;
        }
    }

    // Metnin belirtilen stildeki (ör. kalın) piksel genişliği.
    public static int Genislik(string metin, FontRol rol, FontStyle stil) => TextRenderer.MeasureText(metin, FontProvider.Al(rol, stil), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

    // Metnin piksel genişliği.
    public static int Genislik(string metin, FontRol rol) => TextRenderer.MeasureText(metin, FontProvider.Al(rol), new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
}
