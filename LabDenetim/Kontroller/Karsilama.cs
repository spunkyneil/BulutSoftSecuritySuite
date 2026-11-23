// Bu dosya Açılış (loading) ve Giriş ekranlarının ORTAK görünümünü içerir (saf C# / GDI+, WebView2 ve harici paket yok, tamamen çevrimdışı):
//  - KTema: iki temanın (Koyu / Açık) renkleri; Koyu/Açık seçimi TemaYonetici'den okunur, sayfa zemini paletin "Sayfa" rolünden gelir.
//  - MarkaPaneli: sol marka paneli (düz atlas dünya haritası, siber nöron ağı, canlı şimşek logosu).
//  - KarsilamaFormu: çerçevesiz, 16 px yuvarlak köşeli, fareyle sürüklenebilen ortak pencere iskeleti.
//  - SagPanelTemel: sağ panelin ortak üst şeridi (Açık/Koyu Tema düğmesi ve kapat) ve tıklanabilir bölge altyapısı.
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// İki temanın renkleri. Zemin (Sayfa) rengi TemaYonetici paletinden alınır; böylece Koyu #141B2E ve Açık #F0F4F8 birebir eşleşir.
internal sealed class KTema
{
    public bool Koyu { get; }
    public Color Marka, Metin, Metin2, Soluk, Kenar, Yuksek, Ana, Ana2, Cam, Basari, Tehlike, Simsek, Kehribar, LogoIc1, LogoIc2;
    // Sağ panelin zemini.
    public Color Form => Palet.Renk(PaletRol.Sayfa, Koyu);

    private KTema(bool koyu, string marka, string metin, string metin2, string soluk, string kenar, string yuksek, string ana, string ana2, string cam, string basari, string tehlike, string simsek, string kehribar, string logoIc1, string logoIc2)
    {
        Koyu = koyu; Marka = H(marka); Metin = H(metin); Metin2 = H(metin2); Soluk = H(soluk); Kenar = H(kenar); Yuksek = H(yuksek); Ana = H(ana); Ana2 = H(ana2); Cam = H(cam); Basari = H(basari); Tehlike = H(tehlike); Simsek = H(simsek); Kehribar = H(kehribar); LogoIc1 = H(logoIc1); LogoIc2 = H(logoIc2);
    }

    private static Color H(string s) => ColorTranslator.FromHtml(s);
    private static readonly KTema D = new(true, "#101729", "#E8ECF4", "#A0B0C4", "#7C8CA3", "#27334F", "#1F2942", "#6366F1", "#7C3AED", "#06B6D4", "#10B981", "#EF4444", "#FACC15", "#FBBF24", "#1E2942", "#0E1526");
    private static readonly KTema L = new(false, "#EEF2FF", "#0F172A", "#334155", "#64748B", "#CBD5E1", "#E6ECF4", "#4F46E5", "#6D28D9", "#0891B2", "#059669", "#DC2626", "#EAB308", "#B45309", "#1E293B", "#0F172A");
    // Şu anki tema.
    public static KTema Simdi => TemaYonetici.Mod == TemaModu.Koyu ? D : L;
    // Verilen temanın paleti (kendi Koyu/Açık bayrağını taşıyan denetimler için).
    public static KTema Sec(bool koyu) => koyu ? D : L;
    // Rengin saydamlığını ayarlar (a: 0-255).
    public static Color A(Color c, int a) => DesignTokens.Saydam(c, Math.Clamp(a, 0, 255));
    // Rengi bir başka renkle karıştırır (t: 0 = a, 1 = b).
    public static Color Karistir(Color a, Color b, float t) => DesignTokens.Karistir(a, b, t);
}

// Çizim yardımcıları.
internal static class KCizim
{
    // Yuvarlak köşeli dikdörtgen yolu.
    public static GraphicsPath Yol(RectangleF r, float yaricap)
    {
        var p = new GraphicsPath(); float d = Math.Min(yaricap * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static void Doldur(Graphics g, RectangleF r, float yaricap, Color c) { using var p = Yol(r, yaricap); using var f = new SolidBrush(c); g.FillPath(f, p); }
    public static void Cerceve(Graphics g, RectangleF r, float yaricap, Color c, float kalinlik = 1f) { using var p = Yol(r, yaricap); using var k = new Pen(c, kalinlik); g.DrawPath(k, p); }
    // Yazı yazar (GDI, net; yazı rengi saydam olmaz).
    public static void Yaz(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl = TextFormatFlags.Left | TextFormatFlags.VerticalCenter) => TextRenderer.DrawText(g, s, f, r, c, fl | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    public static void YazCok(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags fl = TextFormatFlags.Left | TextFormatFlags.Top) => TextRenderer.DrawText(g, s, f, r, c, fl | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
}

// Sol marka paneli: canlı, saf GDI+ (30 ms'lik zamanlayıcı).
internal sealed class MarkaPaneli : Control, IThemeAware
{
    // Tema değişince yeniden çizilir (renkler çizim anında TemaYonetici'den okunur).
    public void TemaUygula() => Invalidate();
    private sealed class Noron { public float X, Y, Vx, Vy, R, Yuk, Faz; }
    private sealed class Sinyal { public Noron Kimden = null!, Kime = null!; public float T, Hiz; public bool Simsek; }
    // Düz atlas için sadeleştirilmiş kıta sınırları (enlem, boylam çiftleri).
    private static readonly double[][] Kitalar =
    {
        new double[]{36,-9,43,-9,48,-5,51,2,55,8,60,5,69,18,70,32,64,42,58,30,52,22,47,32,42,28,36,27,36,36,41,42,44,39,39,45,37,36,35,24,41,19,45,13,40,18,44,9,42,3,36,-5},
        new double[]{50,-5,56,-5,58,-2,52,2,50,0},
        new double[]{68,35,73,75,74,115,68,165,60,162,52,138,42,131,35,127,38,121,30,122,21,113,14,108,2,103,15,97,22,90,9,77,22,68,26,56,30,48,38,44,45,48,55,58,65,45},
        new double[]{30,35,32,42,28,48,21,59,13,44,16,42},
        new double[]{35,-6,37,10,31,32,20,38,11,51,1,44,-12,40,-27,33,-34,20,-28,16,-15,12,4,9,5,-5,14,-17,28,-11},
        new double[]{69,-160,70,-125,63,-90,55,-60,44,-66,36,-76,25,-81,29,-89,24,-97,15,-87,9,-79,20,-105,33,-118,48,-125,60,-145},
        new double[]{11,-75,9,-60,0,-49,-8,-35,-22,-42,-38,-57,-54,-68,-48,-75,-20,-71,-4,-81},
        new double[]{-12,131,-12,143,-22,150,-34,151,-37,142,-32,124,-21,114}
    };
    private static readonly Random Rnd = new();
    private static readonly List<(float U, float V, bool Tr, float Faz)> Noktalar = Hazirla();

    private static bool Icinde(double lat, double lon, double[] poly)
    {
        bool ic = false; int n = poly.Length / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double yi = poly[i * 2], xi = poly[i * 2 + 1], yj = poly[j * 2], xj = poly[j * 2 + 1];
            if (((yi > lat) != (yj > lat)) && (lon < (xj - xi) * (lat - yi) / ((yj - yi) == 0 ? 0.0001 : (yj - yi)) + xi)) ic = !ic;
        }
        return ic;
    }

    private static List<(float, float, bool, float)> Hazirla()
    {
        var l = new List<(float, float, bool, float)>();
        for (double lat = -54; lat <= 72; lat += 4.5)
            for (double lon = -168; lon <= 168; lon += 5.5)
            {
                bool kara = false; foreach (var k in Kitalar) if (Icinde(lat, lon, k)) { kara = true; break; }
                if (kara) l.Add(((float)((lon + 180) / 360), (float)((90 - lat) / 155), lat is >= 35 and <= 43 && lon is >= 25 and <= 45, (float)(Rnd.NextDouble() * Math.PI * 2)));
            }
        return l;
    }

    private readonly List<Noron> _noronlar = new(); private readonly List<Sinyal> _sinyaller = new();
    private readonly System.Windows.Forms.Timer _zaman = new() { Interval = 30 };
    private float _t, _ozAlfa = 1f; private bool _soluk;
    private readonly string _surum = UrunBilgisi.SurumEtiketi;
    private readonly Font _fAd = new("Segoe UI", 25f, FontStyle.Bold, GraphicsUnit.Pixel), _fAlt = new("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel), _fSlogan = new("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel), _fOz = new("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel), _fMono = new("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Pixel), _fAlt2 = new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);

    // Giriş ekranında özellik maddeleri parlak, açılışta soluk görünür.
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool OzellikSoluk { get => _soluk; set => _soluk = value; }

    public MarkaPaneli()
    {
        Dock = DockStyle.Left; Width = 310; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); TabStop = false;
        _zaman.Tick += (_, _) => Adim(); _zaman.Start();
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) KarsilamaFormu.Surukle(this); };
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); NoronlariKur(); }

    private void NoronlariKur()
    {
        _noronlar.Clear(); _sinyaller.Clear(); if (Width < 50 || Height < 50) return;
        for (int i = 0; i < 28; i++) _noronlar.Add(new Noron { X = (float)(Rnd.NextDouble() * Width), Y = (float)(Rnd.NextDouble() * Height), Vx = (float)((Rnd.NextDouble() - 0.5) * 0.28), Vy = (float)((Rnd.NextDouble() - 0.5) * 0.28), R = (float)(1.5 + Rnd.NextDouble() * 1.6), Yuk = (float)Rnd.NextDouble(), Faz = (float)(Rnd.NextDouble() * Math.PI * 2) });
    }

    // Bir animasyon adımı.
    private void Adim()
    {
        if (IsDisposed || !Visible) return;
        _t += 0.03f; _ozAlfa += ((_soluk ? 0.45f : 1f) - _ozAlfa) * 0.15f;
        foreach (var n in _noronlar) { n.X += n.Vx; n.Y += n.Vy; if (n.X < 4 || n.X > Width - 4) n.Vx *= -1; if (n.Y < 4 || n.Y > Height - 4) n.Vy *= -1; n.Yuk = Math.Max(0.2f, n.Yuk - 0.012f); }
        for (int i = 0; i < _noronlar.Count; i++)
            for (int j = i + 1; j < _noronlar.Count; j++)
            {
                var a = _noronlar[i]; var b = _noronlar[j];
                if (_sinyaller.Count < 14 && Math.Abs(a.X - b.X) < 96 && Math.Abs(a.Y - b.Y) < 96 && Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) < 96 && Rnd.NextDouble() < 0.0028)
                {
                    bool ters = Rnd.NextDouble() < 0.5; _sinyaller.Add(new Sinyal { Kimden = ters ? b : a, Kime = ters ? a : b, Hiz = (float)(0.022 + Rnd.NextDouble() * 0.022), Simsek = Rnd.NextDouble() < 0.45 }); (ters ? b : a).Yuk = 1f;
                }
            }
        for (int i = _sinyaller.Count - 1; i >= 0; i--) { var s = _sinyaller[i]; s.T += s.Hiz; if (s.T >= 1) { s.Kime.Yuk = 1f; _sinyaller.RemoveAt(i); } }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var t = KTema.Simdi; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; int w = Width, h = Height;
        using (var f = new SolidBrush(t.Marka)) g.FillRectangle(f, ClientRectangle);
        Atlas(g, t, w, h); Noronlar(g, t);
        LogoCiz(g, t, w / 2f, 100f);
        // Marka yazıları.
        TextRenderer.DrawText(g, BrandConfig.Aktif.KisaBaslik, _fAd, new Rectangle(0, 166, w, 32), t.Metin, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        Aralikli(g, "SECURITY SUITE", _fAlt, new Rectangle(0, 200, w, 18), t.Koyu ? t.Simsek : t.Ana, 2.4f);
        TextRenderer.DrawText(g, "Kurumsal Siber Savunma Platformu", _fSlogan, new Rectangle(0, 222, w, 20), t.Soluk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        // Özellik maddeleri.
        string[] oz = { "Yerel Ağ ve Kapı Güvenlik Denetimi", "Canlı Ağ Trafiği ve Olay İzleme", "Rol Tabanlı Yetkilendirme", "Adli Kanıt Paketi ve KVKK Raporu" }; int y = 268; float al = _ozAlfa;
        foreach (var s in oz)
        {
            var r = new RectangleF(26, y, w - 52, 32);
            KCizim.Doldur(g, r, 8, KTema.A(t.Ana, (int)(13 * al))); KCizim.Cerceve(g, r, 8, KTema.A(t.Ana, (int)(26 * al)));
            using (var b = new SolidBrush(KTema.A(t.Ana, (int)(60 * al)))) g.FillEllipse(b, 36, y + 16 - 7, 14, 14);
            using (var b = new SolidBrush(KTema.A(t.Ana, (int)(255 * al)))) g.FillEllipse(b, 40, y + 16 - 3.5f, 7, 7);
            TextRenderer.DrawText(g, s, _fOz, new Rectangle(60, y, w - 90, 32), KTema.Karistir(t.Marka, t.Metin2, al), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            y += 42;
        }
        // Alt şerit: çevrimdışı ibaresi ve sürüm rozeti.
        using (var k = new Pen(t.Kenar)) g.DrawLine(k, 26, h - 46, w - 26, h - 46);
        TextRenderer.DrawText(g, "Çevrimdışı · Yerel", _fAlt2, new Rectangle(26, h - 40, 150, 22), t.Soluk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        var pil = new Rectangle(w - 26 - 56, h - 39, 56, 20); KCizim.Doldur(g, pil, 10, t.Yuksek); KCizim.Cerceve(g, pil, 10, t.Kenar); TextRenderer.DrawText(g, _surum, _fMono, pil, t.Metin2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        KarsilamaFormu.PencereCercevesi(g, this);
    }

    // Harf aralıklı yazı.
    private static void Aralikli(Graphics g, string s, Font f, Rectangle r, Color c, float aralik)
    {
        float toplam = 0; var gen = new float[s.Length];
        for (int i = 0; i < s.Length; i++) { gen[i] = TextRenderer.MeasureText(g, s[i].ToString(), f, new Size(100, 40), TextFormatFlags.NoPadding | TextFormatFlags.NoClipping).Width - 2; if (s[i] == ' ') gen[i] = 4; toplam += gen[i] + aralik; }
        float x = r.X + (r.Width - toplam + aralik) / 2f;
        for (int i = 0; i < s.Length; i++) { if (s[i] != ' ') TextRenderer.DrawText(g, s[i].ToString(), f, new Point((int)x - 1, r.Y), c, TextFormatFlags.NoPadding | TextFormatFlags.NoClipping); x += gen[i] + aralik; }
    }

    // Düz atlas dünya haritası (nokta matris) ve hafif koordinat ızgarası; Türkiye bölgesi hafifçe parlar.
    private void Atlas(Graphics g, KTema t, int w, int h)
    {
        float mapW = w * 1.18f, mapH = mapW * 0.48f, ox = (w - mapW) * 0.5f - w * 0.04f, oy = 36f;
        using (var k = new Pen(KTema.A(t.Ana, t.Koyu ? 18 : 20), 0.6f))
        {
            for (int i = 0; i <= 4; i++) { float y = oy + mapH / 4 * i; g.DrawLine(k, 0, y, w, y); }
            for (int j = 0; j <= 6; j++) { float x = w / 6f * j; g.DrawLine(k, x, oy, x, oy + mapH); }
        }
        using var br = new SolidBrush(Color.Black);
        foreach (var d in Noktalar)
        {
            float px = ox + d.U * mapW, py = oy + d.V * mapH; if (px < -4 || px > w + 4 || py < 0 || py > h) continue;
            if (d.Tr)
            {
                float nabiz = 0.55f + 0.45f * (float)Math.Sin(_t * 3.2 + d.Faz); br.Color = t.Koyu ? KTema.A(t.Simsek, (int)((0.55 + nabiz * 0.4) * 255)) : KTema.A(t.Ana, (int)((0.45 + nabiz * 0.35) * 255));
                g.FillEllipse(br, px - 1.6f, py - 1.6f, 3.2f, 3.2f);
            }
            else
            {
                float par = 0.72f + 0.28f * (float)Math.Sin(_t * 1.4 + d.Faz);
                br.Color = t.Koyu ? DesignTokens.Saydam(ColorTranslator.FromHtml("#38BDF8"), (int)(0.34 * par * 255)) : DesignTokens.Saydam(ColorTranslator.FromHtml("#475569"), (int)(0.34 * par * 255));
                g.FillRectangle(br, px - 0.85f, py - 0.85f, 1.7f, 1.7f);
            }
        }
    }

    // Siber nöronlar, aralarındaki sinaps çizgileri ve akan elektrik sinyalleri.
    private void Noronlar(Graphics g, KTema t)
    {
        Color cam = t.Cam, simsek = t.Simsek, bag = t.Ana;
        for (int i = 0; i < _noronlar.Count; i++)
            for (int j = i + 1; j < _noronlar.Count; j++)
            {
                var a = _noronlar[i]; var b = _noronlar[j]; float dx = a.X - b.X, dy = a.Y - b.Y; if (Math.Abs(dx) > 96 || Math.Abs(dy) > 96) continue; float d = (float)Math.Sqrt(dx * dx + dy * dy); if (d >= 96) continue;
                using var k = new Pen(KTema.A(bag, (int)((1 - d / 96) * (t.Koyu ? 0.22 : 0.16) * 255)), 0.75f); g.DrawLine(k, a.X, a.Y, b.X, b.Y);
            }
        foreach (var s in _sinyaller)
        {
            float ix = s.Kimden.X + (s.Kime.X - s.Kimden.X) * s.T, iy = s.Kimden.Y + (s.Kime.Y - s.Kimden.Y) * s.T; float t2 = Math.Max(0, s.T - 0.18f); float tx = s.Kimden.X + (s.Kime.X - s.Kimden.X) * t2, ty = s.Kimden.Y + (s.Kime.Y - s.Kimden.Y) * t2;
            var c = s.Simsek ? simsek : cam; using (var k = new Pen(KTema.A(c, 200), 1.6f)) g.DrawLine(k, tx, ty, ix, iy);
            using var b = new SolidBrush(t.Koyu ? Color.White : c); g.FillEllipse(b, ix - 2f, iy - 2f, 4f, 4f);
        }
        foreach (var n in _noronlar)
        {
            if (n.Yuk > 0.35f) { using var k = new Pen(KTema.A(simsek, (int)(n.Yuk * 0.45 * 255)), 1f); float r = n.R + 3.2f * n.Yuk; g.DrawEllipse(k, n.X - r, n.Y - r, r * 2, r * 2); }
            using var f = new SolidBrush(n.Yuk > 0.55f ? KTema.A(simsek, (int)((0.55 + n.Yuk * 0.4) * 255)) : KTema.A(cam, (int)((t.Koyu ? 0.36 : 0.32) * 255))); g.FillEllipse(f, n.X - n.R, n.Y - n.R, n.R * 2, n.R * 2);
        }
    }

    // Canlı şimşek logosu: nefes alan aura, zıt yönlerde dönen iki halka, çember ve titreşen şimşek.
    private void LogoCiz(Graphics g, KTema t, float cx, float cy)
    {
        // Aura (3 sn'lik nefes).
        float nef = 0.5f + 0.5f * (float)Math.Sin(_t * 2 * Math.PI / 3.0); float olcek = 0.95f + 0.13f * nef, ar = 64f * olcek;
        using (var yol = new GraphicsPath()) { yol.AddEllipse(cx - ar, cy - ar, ar * 2, ar * 2); using var pg = new PathGradientBrush(yol); pg.CenterColor = KTema.A(t.Simsek, (int)((t.Koyu ? 0.35 : 0.28) * 255 * (0.78f + 0.22f * nef))); pg.SurroundColors = new[] { DesignTokens.Saydam(t.Simsek, 0) }; pg.InterpolationColors = new ColorBlend { Colors = new[] { DesignTokens.Saydam(t.Simsek, 0), KTema.A(t.Ana, 36), KTema.A(t.Simsek, (int)((t.Koyu ? 0.35 : 0.28) * 255)) }, Positions = new[] { 0f, 0.45f, 1f } }; g.FillPath(pg, yol); }
        // Dönen ince enerji halkası (6 sn) ve ters yönde kesikli iç halka (10 sn).
        float aci = _t / 6f * 360f; using (var k = new Pen(t.Simsek, 1.5f)) g.DrawArc(k, cx - 54, cy - 54, 108, 108, aci - 135, 90); using (var k = new Pen(KTema.A(t.Ana, 140), 1.5f)) g.DrawArc(k, cx - 54, cy - 54, 108, 108, aci - 45, 90);
        using (var k = new Pen(KTema.A(t.Simsek, 90), 1f) { DashPattern = new[] { 3f, 3f } }) { k.DashOffset = _t * 6; g.DrawEllipse(k, cx - 46, cy - 46, 92, 92); }
        // Şimşek çemberi.
        var ic = new RectangleF(cx - 38, cy - 38, 76, 76);
        using (var yol = new GraphicsPath()) { yol.AddEllipse(ic); using var pg = new PathGradientBrush(yol) { CenterColor = t.LogoIc1, SurroundColors = new[] { t.LogoIc2 }, CenterPoint = new PointF(ic.X + ic.Width * 0.3f, ic.Y + ic.Height * 0.3f) }; g.FillPath(pg, yol); }
        for (int i = 3; i >= 1; i--) using (var k = new Pen(KTema.A(t.Simsek, 16), 2f + i * 2f)) g.DrawEllipse(k, ic.X, ic.Y, ic.Width, ic.Height);
        using (var k = new Pen(t.Simsek, 2.5f)) g.DrawEllipse(k, ic.X + 1.25f, ic.Y + 1.25f, ic.Width - 2.5f, ic.Height - 2.5f);
        // Şimşek (4 sn'de bir kısa parlama).
        float dng = _t % 4f / 4f; float pr = dng is > 0.92f and < 0.98f ? 1.06f : 1f; float s = 34f / 24f * pr;
        PointF P(float x, float y) => new(cx + (x - 12f) * s, cy + (y - 12f) * s);
        var poli = new[] { P(13, 2), P(4.5f, 13.2f), P(10.7f, 13.2f), P(9.8f, 22), P(19.5f, 10), P(13.2f, 10) };
        using (var k = new Pen(KTema.A(t.Simsek, pr > 1 ? 150 : 90), 5f) { LineJoin = LineJoin.Round }) g.DrawPolygon(k, poli);
        using (var b = new SolidBrush(t.Simsek)) g.FillPolygon(b, poli);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _zaman.Dispose(); _fAd.Dispose(); _fAlt.Dispose(); _fSlogan.Dispose(); _fOz.Dispose(); _fMono.Dispose(); _fAlt2.Dispose(); }
        base.Dispose(disposing);
    }
}

// Açılış ve Giriş pencerelerinin ortak iskeleti: çerçevesiz, 16 px yuvarlak köşe, fareyle sürüklenebilir, solda canlı marka paneli.
internal abstract class KarsilamaFormu : TemaliForm
{
    public const int Genislik = 820, MarkaGenisligi = 310, Yaricap = 16;
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
    protected readonly MarkaPaneli Marka = new();
    private readonly Action _temaYenile;

    protected KarsilamaFormu(int yukseklik)
    {
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(Genislik, yukseklik); BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); DoubleBuffered = true; ShowInTaskbar = true; Icon = Logo.Ikon; KeyPreview = true;
        _temaYenile = () => { if (!IsDisposed) Invalidate(true); }; TemaYonetici.Degisti += _temaYenile; FormClosed += (_, _) => TemaYonetici.Degisti -= _temaYenile;
    }

    // Sağ paneli ve marka panelini yerleştirir (önce Fill, sonra Left).
    protected void Yerlestir(Control sag) { sag.Dock = DockStyle.Fill; Controls.Add(sag); Controls.Add(Marka); }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e); var eski = Region; using var yol = KCizim.Yol(new RectangleF(0, 0, Width, Height), Yaricap); Region = new Region(yol); eski?.Dispose();
    }

    // Pencereyi fareyle tutup sürüklemeyi başlatır.
    public static void Surukle(Control c) { var f = c.FindForm(); if (f == null || !f.IsHandleCreated) return; ReleaseCapture(); SendMessage(f.Handle, 0xA1, 2, 0); }

    // Pencerenin yuvarlak dış çizgisini (kontrolün parçası kadar) çizer.
    public static void PencereCercevesi(Graphics g, Control c)
    {
        var f = c.FindForm(); if (f == null) return; var t = KTema.Simdi; var onceki = g.Transform;
        g.TranslateTransform(-c.Left, -c.Top); using (var k = new Pen(t.Kenar)) { using var yol = KCizim.Yol(new RectangleF(0.5f, 0.5f, f.ClientSize.Width - 1, f.ClientSize.Height - 1), Yaricap); g.DrawPath(k, yol); }
        g.Transform = onceki;
    }
}

// Sağ panelin ortak tabanı: üstte Açık/Koyu Tema düğmesi ve kapat (✕); tıklanabilir bölgeler çizim sırasında kaydedilir (anlık arayüz).
internal abstract class SagPanelTemel : Control
{
    private readonly List<(Rectangle R, Action A)> _bolgeler = new(); private Point _fare = new(-1, -1);
    protected readonly Font FBaslik = new("Segoe UI", 21f, FontStyle.Bold, GraphicsUnit.Pixel), FAciklama = new("Segoe UI", 12.5f, FontStyle.Regular, GraphicsUnit.Pixel), FKucuk = new("Segoe UI", 11.5f, FontStyle.Bold, GraphicsUnit.Pixel), FMono = new("Consolas", 15f, FontStyle.Bold, GraphicsUnit.Pixel), FSatir = new("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel), FSatirK = new("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel), FNot = new("Segoe UI", 11.5f, FontStyle.Regular, GraphicsUnit.Pixel), FNotK = new("Segoe UI", 11.5f, FontStyle.Bold, GraphicsUnit.Pixel);
    // Kapat (✕) tıklanınca.
    public Action? CikisIstendi;
    protected const int Kenar = 34;
    protected int IcGenislik => Width - Kenar * 2;

    protected SagPanelTemel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); TabStop = false;
        TemaDinle();
    }

    private Action? _tema;
    private void TemaDinle() { _tema = () => { if (!IsDisposed) Invalidate(); }; TemaYonetici.Degisti += _tema; Disposed += (_, _) => TemaYonetici.Degisti -= _tema; }

    // Tıklanabilir bölge kaydeder; fare üzerindeyse true verir.
    protected bool Bolge(Rectangle r, Action a) { _bolgeler.Add((r, a)); return r.Contains(_fare); }
    protected bool Uzerinde(Rectangle r) => r.Contains(_fare);

    protected abstract void Ciz(Graphics g, KTema t);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; var t = KTema.Simdi; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; _bolgeler.Clear();
        using (var f = new SolidBrush(t.Form)) g.FillRectangle(f, ClientRectangle);
        Ciz(g, t); UstSerit(g, t); KarsilamaFormu.PencereCercevesi(g, this);
    }

    // Sağ üst: tema düğmesi ve kapat.
    private void UstSerit(Graphics g, KTema t)
    {
        var kapat = new Rectangle(Width - Kenar - 28, 24, 28, 28); bool kh = Bolge(kapat, () => CikisIstendi?.Invoke());
        if (kh) { KCizim.Doldur(g, kapat, 8, KTema.A(t.Tehlike, 38)); KCizim.Cerceve(g, kapat, 8, KTema.A(t.Tehlike, 76)); }
        using (var k = new Pen(kh ? t.Tehlike : t.Soluk, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(k, kapat.X + 9, kapat.Y + 9, kapat.Right - 9, kapat.Bottom - 9); g.DrawLine(k, kapat.Right - 9, kapat.Y + 9, kapat.X + 9, kapat.Bottom - 9); }
        string metin = t.Koyu ? "Açık Tema" : "Koyu Tema"; int tw = 108; var tema = new Rectangle(kapat.X - 8 - tw, 24, tw, 28); bool th = Bolge(tema, () => TemaYonetici.Degistir());
        KCizim.Doldur(g, tema, 8, t.Yuksek); KCizim.Cerceve(g, tema, 8, th ? t.Ana : t.Kenar);
        Color tc = th ? t.Metin : t.Metin2; TextRenderer.DrawText(g, metin, FKucuk, new Rectangle(tema.X + 28, tema.Y, tema.Width - 30, tema.Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        // Simge (vektör): koyu temadayken güneş (açık temaya geçilir), açık temadayken ay.
        float ix = tema.X + 14, iy = tema.Y + 14; using var ik = new Pen(tc, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (t.Koyu) { g.DrawEllipse(ik, ix - 3, iy - 3, 6, 6); for (int i = 0; i < 8; i++) { double an = i * Math.PI / 4; g.DrawLine(ik, (float)(ix + Math.Cos(an) * 5.2), (float)(iy + Math.Sin(an) * 5.2), (float)(ix + Math.Cos(an) * 7), (float)(iy + Math.Sin(an) * 7)); } }
        else { using var ay = new GraphicsPath(); ay.AddArc(ix - 6, iy - 6, 12, 12, 40, 280); ay.AddArc(ix - 3.5f, iy - 5.5f, 10, 11, 250, -190); using var ab = new SolidBrush(tc); g.FillPath(ab, ay); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); var eski = _bolgeler.FindIndex(b => b.R.Contains(_fare)); _fare = e.Location; var yeni = _bolgeler.FindIndex(b => b.R.Contains(_fare));
        Cursor = yeni >= 0 ? Cursors.Hand : Cursors.Default; if (eski != yeni) Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _fare = new Point(-1, -1); Cursor = Cursors.Default; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
        for (int i = _bolgeler.Count - 1; i >= 0; i--) if (_bolgeler[i].R.Contains(e.Location)) { var a = _bolgeler[i].A; BeginInvoke(a); return; }
        KarsilamaFormu.Surukle(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { FBaslik.Dispose(); FAciklama.Dispose(); FKucuk.Dispose(); FMono.Dispose(); FSatir.Dispose(); FSatirK.Dispose(); FNot.Dispose(); FNotK.Dispose(); }
        base.Dispose(disposing);
    }
}
