// Bu dosya Açık ve Koyu temada çalışan, animasyonlu ve etkileşimli profesyonel düğmeyi (EfektliButon) içerir.
// Sistem Button sınıfından türer: PerformClick, DialogResult, Click olayı, Enabled, Tag ve mevcut tüm kod aynen çalışır.
// Efektler: üzerine gelince yumuşak geçiş + ışıma/gölge, basınca hafif çökme, tıklanan yerden yayılan dalga (ripple), klavye odak halkası.
// Renkler: Otomatik stilde düğmenin ŞU ANKİ BackColor/ForeColor/kenarlık renkleri kullanılır (tema yöneticisi zaten rolüne göre boyar);
// böylece Birincil / Hayalet / Tehlike ayrımı ve "seçili" gibi kodla verilen renkler olduğu gibi korunur.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// Düğmenin görünüm stili. Otomatik: mevcut renkleri kullan.
internal enum ButonStili { Otomatik, Birincil, Basari, Tehlike, Hayalet, TehlikeHayalet }

// Animasyonlu, temaya uyumlu düğme.
internal sealed class EfektliButon : Button
{
    private ButonStili _stil = ButonStili.Otomatik;
    // Üzerine gelme ve basılma ilerlemeleri (0..1), dalga animasyonu (0..1) ve konumu, yükleniyor yayı açısı.
    private float _hover, _basili, _dalga = 1f, _yay; private PointF _dalgaYeri; private bool _icinde, _basiliMi, _yukleniyor, _kesikli;
    private readonly System.Windows.Forms.Timer _zaman = new() { Interval = 15 };
    private readonly Action _temaKancasi;

    // Düğmenin stili (Otomatik: renkleri mevcut BackColor/ForeColor'dan al).
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
    public ButonStili Stil { get => _stil; set { _stil = value; Invalidate(); } }
    // Köşe yuvarlaklığı (piksel).
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
    public int Yaricap { get; set; } = 9;
    // Kenarlık kesik çizgili mi? (örneğin "+ Yeni Kural Ekle" düğmesi)
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
    public bool Kesikli { get => _kesikli; set { _kesikli = value; Invalidate(); } }
    // Yükleniyor: yazının solunda dönen yay gösterir (işlem sürerken). Tıklamayı engellemez; çağıran Enabled'ı ayarlar.
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Yukleniyor { get => _yukleniyor; set { _yukleniyor = value; Baslat(); Invalidate(); } }

    public EfektliButon()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
        _zaman.Tick += (_, _) => Adim();
        // Tema değişince yeniden boya.
        _temaKancasi = () => { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Invalidate)); };
        TemaYonetici.Degisti += _temaKancasi;
    }

    // Animasyon adımı: değerler hedeflerine yumuşakça yaklaşır; her şey durulunca zamanlayıcı durur.
    private void Adim()
    {
        float hHedef = _icinde && Enabled ? 1f : 0f, bHedef = _basiliMi && Enabled ? 1f : 0f;
        _hover += (hHedef - _hover) * 0.22f; _basili += (bHedef - _basili) * 0.35f;
        if (Math.Abs(_hover - hHedef) < 0.01f) _hover = hHedef; if (Math.Abs(_basili - bHedef) < 0.01f) _basili = bHedef;
        if (_dalga < 1f) _dalga = Math.Min(1f, _dalga + 0.045f);
        if (_yukleniyor) _yay = (_yay + 9f) % 360f;
        Invalidate();
        if (_hover == hHedef && _basili == bHedef && _dalga >= 1f && !_yukleniyor) _zaman.Stop();
    }
    private void Baslat() { if (!_zaman.Enabled) _zaman.Start(); }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _icinde = true; Baslat(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _icinde = false; _basiliMi = false; Baslat(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _basiliMi = true; _dalgaYeri = e.Location; _dalga = 0f; Baslat();
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _basiliMi = false; Baslat(); }
    // Klavye (Boşluk/Enter) ya da PerformClick ile tetiklenince dalga ortadan başlar.
    protected override void OnClick(EventArgs e) { if (_dalga >= 1f) { _dalgaYeri = new PointF(Width / 2f, Height / 2f); _dalga = 0f; Baslat(); } base.OnClick(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Baslat(); Invalidate(); }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); Invalidate(); }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); Invalidate(); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    // Karıştırma yardımcıları.
    private static Color Karistir(Color a, Color b, float t) => DesignTokens.Karistir(a, b, t);
    private static GraphicsPath Yuvarlak(RectangleF r, float y)
    {
        var p = new GraphicsPath(); float d = Math.Min(y * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    private static Color H(string s) => ColorTranslator.FromHtml(s);
    private static Color P(PaletRol rol, bool koyu) => Palet.Renk(rol, koyu);
    private static bool Esit(Color a, Color b) => a.R == b.R && a.G == b.G && a.B == b.B;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(TemaYonetici.Zemin(this));
        bool koyu = TemaYonetici.Mod == TemaModu.Koyu; float soluk = Enabled ? 1f : 0.45f;

        // Renkler: zemin (a→b geçişi), yazı, kenarlık ve ışıma rengi.
        Color zA, zB, yazi, kenar, isima; bool dolu;
        switch (_stil)
        {
            case ButonStili.Basari: dolu = true; zA = P(PaletRol.BasariSolid, koyu); zB = Karistir(zA, H("#10B981"), 0.6f); yazi = Color.White; kenar = Color.Transparent; isima = zA; break;
            case ButonStili.Tehlike: dolu = true; zA = P(PaletRol.TehlikeSolid, koyu); zB = Karistir(zA, H("#DC2626"), 0.7f); yazi = Color.White; kenar = Color.Transparent; isima = zA; break;
            case ButonStili.Hayalet: dolu = false; zA = Karistir(P(PaletRol.Hayalet, koyu), P(PaletRol.HayaletUstunde, koyu), _hover); zB = zA; yazi = Karistir(P(PaletRol.IkincilMetin, koyu), P(PaletRol.Metin, koyu), _hover); kenar = Karistir(P(PaletRol.Kenar, koyu), P(PaletRol.BirincilSolid, koyu), _hover); isima = P(PaletRol.BirincilSolid, koyu); break;
            case ButonStili.TehlikeHayalet: dolu = false; zA = Karistir(P(PaletRol.TehlikeArka, koyu), P(PaletRol.TehlikeSolid, koyu), _hover * 0.22f); zB = zA; yazi = P(PaletRol.TehlikeMetin, koyu); kenar = Karistir(P(PaletRol.TehlikeKenar, koyu), P(PaletRol.TehlikeSolid, koyu), _hover); isima = P(PaletRol.TehlikeSolid, koyu); break;
            case ButonStili.Birincil: dolu = true; zA = P(PaletRol.BirincilSolid, koyu); zB = P(PaletRol.Mor, koyu); if (!koyu) zB = H("#7C3AED"); yazi = Color.White; kenar = Color.Transparent; isima = zA; break;
            default:
            {
                // Otomatik: düğmenin mevcut renkleri. Palet "dolu" rollerinden biriyse dolu sayılır.
                zA = BackColor; yazi = ForeColor; isima = zA;
                PaletRol[] doluRoller = { PaletRol.BirincilSolid, PaletRol.Mor, PaletRol.BasariSolid, PaletRol.TehlikeSolid, PaletRol.BilgiSolid, PaletRol.UyariSolid };
                dolu = doluRoller.Any(r => Esit(zA, P(r, koyu)));
                kenar = FlatAppearance.BorderSize > 0 ? FlatAppearance.BorderColor : Color.Transparent;
                zB = dolu && Esit(zA, P(PaletRol.BirincilSolid, koyu)) ? (koyu ? P(PaletRol.Mor, true) : H("#7C3AED")) : zA;
                if (!dolu)
                {
                    // Hayalet: üzerine gelince hayalet-üstü zemine, kenarlık birincile geçer.
                    zA = Karistir(zA, P(PaletRol.HayaletUstunde, koyu), _hover * 0.9f); zB = zA;
                    if (kenar.A > 0) kenar = Karistir(kenar, Esit(kenar, P(PaletRol.TehlikeKenar, koyu)) ? P(PaletRol.TehlikeSolid, koyu) : P(PaletRol.BirincilSolid, koyu), _hover);
                    yazi = Karistir(yazi, P(PaletRol.Metin, koyu), Esit(yazi, P(PaletRol.TehlikeMetin, koyu)) ? 0f : _hover);
                    isima = P(PaletRol.BirincilSolid, koyu);
                }
                break;
            }
        }
        if (dolu) { zA = ControlPaint.Light(zA, 0.18f * _hover); zB = ControlPaint.Light(zB, 0.18f * _hover); }

        // Basılınca düğme hafif çöker; üzerine gelince hafifçe kalkar.
        float cok = _basili, yuk = -1.2f * _hover * (1f - cok);
        var alan = new RectangleF(4, 4, Width - 8, Height - 8); alan.Inflate(-1.0f * cok, -1.0f * cok); alan.Offset(0, yuk + 0.8f * cok);
        float yar = Math.Min(Yaricap, alan.Height / 2f);

        // Gölge (açık tema) ya da ışıma (koyu tema): üzerine gelince büyür.
        if (Enabled && _hover > 0.02f)
            for (int i = 3; i >= 1; i--)
            {
                float yay = i * 1.8f * _hover; var kutu = alan; kutu.Inflate(yay, yay); kutu.Offset(0, koyu ? 0 : 1.8f * _hover);
                int al = koyu ? (int)(16 * _hover * (4 - i) / 2.0) : (int)((dolu ? 14 : 8) * _hover) / i;
                using var yol = Yuvarlak(kutu, yar + yay); using var f = new SolidBrush(DesignTokens.Saydam(koyu ? isima : H("#0F172A"), Math.Clamp(al, 0, 90))); g.FillPath(f, yol);
            }

        using var govde = Yuvarlak(alan, yar);
        // Zemin.
        if (dolu) { using var br = new LinearGradientBrush(alan, DesignTokens.Saydam(zA, (int)(255 * soluk)), DesignTokens.Saydam(zB, (int)(255 * soluk)), 35f); g.FillPath(br, govde); }
        else { using var br = new SolidBrush(DesignTokens.Saydam(zA, (int)(255 * soluk))); g.FillPath(br, govde); }
        // Üst kenarda ince parlak çizgi (derinlik hissi) yalnız dolu düğmelerde.
        if (dolu) { var ust = alan; ust.Height = alan.Height / 2f; using var br = new LinearGradientBrush(ust, DesignTokens.Saydam(Color.White, (int)(50 + 30 * _hover)), DesignTokens.Saydam(Color.White, 0), 90f); var eski = g.Clip; g.SetClip(govde, CombineMode.Intersect); g.FillRectangle(br, ust); g.Clip = eski; }

        // Tıklama dalgası: tıklanan noktadan yayılır ve sönerek kaybolur.
        if (_dalga < 1f && Enabled)
        {
            var eski = g.Clip; g.SetClip(govde, CombineMode.Intersect);
            float maxR = (float)Math.Sqrt(Width * Width + Height * Height) * 0.9f, rr = maxR * (1f - (float)Math.Pow(1f - _dalga, 3));
            int al = (int)((dolu ? 70 : 45) * (1f - _dalga)); using var f = new SolidBrush(DesignTokens.Saydam(dolu ? Color.White : isima, al)); g.FillEllipse(f, _dalgaYeri.X - rr, _dalgaYeri.Y - rr, 2 * rr, 2 * rr); g.Clip = eski;
        }

        // Kenarlık (kesik çizgili olabilir).
        if (kenar.A > 0) { using var k = new Pen(DesignTokens.Saydam(kenar, (int)(255 * soluk)), 1.2f); if (_kesikli) k.DashStyle = DashStyle.Dash; g.DrawPath(k, govde); }
        // Klavye odak halkası.
        if (Focused && ShowFocusCues) { var halka = alan; halka.Inflate(2.6f, 2.6f); using var yol = Yuvarlak(halka, yar + 3); using var k = new Pen(DesignTokens.Saydam(P(PaletRol.BirincilSolid, koyu), 170), 2f); g.DrawPath(k, yol); }

        // Yazı (ve yükleniyor yayı).
        var yaziAlan = Rectangle.Round(alan); var renk = Enabled ? yazi : DesignTokens.Saydam(yazi, 150);
        var bayrak = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        bool sol = TextAlign is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft;
        if (_yukleniyor) { float c = 7f; using var k = new Pen(renk, 2f); g.DrawArc(k, yaziAlan.X + 12, yaziAlan.Y + yaziAlan.Height / 2f - c, 2 * c, 2 * c, _yay, 260f); yaziAlan = new Rectangle(yaziAlan.X + 32, yaziAlan.Y, yaziAlan.Width - 32, yaziAlan.Height); }
        if (sol) { yaziAlan = new Rectangle(yaziAlan.X + 12, yaziAlan.Y, yaziAlan.Width - 12, yaziAlan.Height); bayrak |= TextFormatFlags.Left; } else bayrak |= TextFormatFlags.HorizontalCenter;
        // Uzun metin tek satıra sığmıyorsa ve düğme yüksekliği iki satıra yetiyorsa kesmek (…) yerine alt satıra sarılır; kırpılma olmaz.
        var yaziKutusu = new Size(Math.Max(1, yaziAlan.Width), int.MaxValue);
        if (TextRenderer.MeasureText(g, Text, Font, yaziKutusu, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width > yaziAlan.Width)
        {
            var sarili = (bayrak & ~TextFormatFlags.EndEllipsis) | TextFormatFlags.WordBreak;
            if (TextRenderer.MeasureText(g, Text, Font, yaziKutusu, sarili).Height <= yaziAlan.Height) bayrak = sarili;
        }
        TextRenderer.DrawText(g, Text, Font, yaziAlan, renk, bayrak);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { TemaYonetici.Degisti -= _temaKancasi; _zaman.Dispose(); }
        base.Dispose(disposing);
    }
}
