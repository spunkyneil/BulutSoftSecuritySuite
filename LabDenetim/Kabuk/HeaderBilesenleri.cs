// Bu dosya üst çubuğun (GlobalHeader) küçük özel çizimli bileşenlerini içerir: başlık+ekmek kırıntısı, komut/arama tetikleyici, Kolay|Uzman segmenti, ikon düğmesi (rozetli), kullanıcı düğmesi.
// Hepsi renkleri DesignTokens'tan, yazı tiplerini FontProvider'dan alır; sabit piksel yerine DPI'a göre ölçeklenen Px() kullanır.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Aktif sayfa başlığı ve ekmek kırıntısı.
internal sealed class BaslikAlani : KabukKontrol
{
    private string _baslik = "", _kirinti = "";
    public BaslikAlani() { AutoSize = true; }
    public string Baslik { get => _baslik; set { _baslik = value; PerformLayout(); Parent?.PerformLayout(); Invalidate(); AccessibleName = value; } }
    public string Kirinti { get => _kirinti; set { _kirinti = value; Parent?.PerformLayout(); Invalidate(); } }
    public override Size GetPreferredSize(Size proposedSize) => new(Math.Max(Px(240), Math.Max(Cizim.Genislik(_baslik, FontRol.Heading), Cizim.Genislik(_kirinti, FontRol.Caption)) + Px(DesignTokens.SpaceXl)), Px(52));
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Cizim.Metin(g, _kirinti, FontRol.Caption, new Rectangle(0, Px(6), Width, Px(14)), DesignTokens.TextMuted);
        Cizim.Metin(g, _baslik, FontRol.Heading, new Rectangle(0, Px(20), Width, Px(26)), DesignTokens.TextPrimary);
    }
}

// Tek tıkla işlem alan düğme tabanı (hover, odak halkası, Enter/Space).
internal abstract class KabukDugmesi : KabukKontrol
{
    protected bool Uzerinde;
    protected KabukDugmesi() { TabStop = true; SetStyle(ControlStyles.Selectable, true); Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.PushButton; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Uzerinde = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Uzerinde = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override bool IsInputKey(Keys k) => EtkinlestirmeTusu(k) || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (EtkinlestirmeTusu(e.KeyCode)) { e.Handled = true; OnClick(EventArgs.Empty); } }
    protected void OdakHalkasi(Graphics g, RectangleF r) { if (Focused && ShowFocusCues) Cizim.Cerceve(g, r, DesignTokens.RadiusMd, DesignTokens.AccentPrimary, 2f); }
}

// Komut/arama tetikleyicisi: tıklanınca Komut Paleti açılır (Ctrl+K).
internal sealed class AramaTetikleyici : KabukDugmesi
{
    public string Yazi { get; set; } = "Ctrl+K — Komut veya bulgu ara…";
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g);
        int w = Math.Min(Width - Px(DesignTokens.SpaceLg), Px(520)), h = Px(36); if (w < Px(120)) return;
        var r = new Rectangle((Width - w) / 2, (Height - h) / 2, w, h);
        Cizim.Doldur(g, r, DesignTokens.RadiusMd, DesignTokens.SurfaceInput); Cizim.Cerceve(g, r, DesignTokens.RadiusMd, Uzerinde ? DesignTokens.BorderStrong : DesignTokens.BorderSubtle);
        Ikonlar.Ciz(g, IkonAdi.Ara, new RectangleF(r.X + Px(DesignTokens.SpaceMd), r.Y + (h - Px(16)) / 2f, Px(16), Px(16)), DesignTokens.TextMuted, 1.5f);
        Cizim.Metin(g, Yazi, FontRol.Body, new Rectangle(r.X + Px(36), r.Y, r.Width - Px(36) - Px(DesignTokens.SpaceMd), h), DesignTokens.TextMuted);
        OdakHalkasi(g, r);
    }
    // Yalnızca kapsül içi tıklama sayılır (kenarlar boş alan).
    protected override void OnMouseClick(MouseEventArgs e) { int w = Math.Min(Width - Px(DesignTokens.SpaceLg), Px(520)), h = Px(36); if (new Rectangle((Width - w) / 2, (Height - h) / 2, w, h).Contains(e.Location)) base.OnMouseClick(e); }
}

// Kolay | Uzman görünüm anahtarı.
internal sealed class ModSegmenti : KabukKontrol
{
    private ModTuru _secili = ModTuru.Kolay; private int _uzerinde = -1;
    public ModSegmenti() { AutoSize = true; TabStop = true; SetStyle(ControlStyles.Selectable, true); Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.RadioButton; AccessibleName = "Görünüm seçimi"; }
    public event Action<ModTuru>? Secildi;
    public ModTuru Secili { get => _secili; set { if (_secili != value) { _secili = value; Invalidate(); } } }
    public override Size GetPreferredSize(Size proposedSize) => new(Px(2 * 76 + 8), Px(52));
    private Rectangle Kapsul() { int h = Px(34); return new Rectangle(0, (Height - h) / 2, Math.Min(Width, Px(2 * 76 + 8)), h); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); var k = Kapsul(); int ic = Px(3), w = (k.Width - 2 * ic) / 2;
        Cizim.Doldur(g, k, DesignTokens.RadiusMd, DesignTokens.SurfaceInput); Cizim.Cerceve(g, k, DesignTokens.RadiusMd, DesignTokens.BorderSubtle);
        for (int i = 0; i < 2; i++)
        {
            var mod = i == 0 ? ModTuru.Kolay : ModTuru.Uzman; bool secili = mod == _secili; var r = new Rectangle(k.X + ic + i * w, k.Y + ic, w, k.Height - 2 * ic);
            if (secili) Cizim.Doldur(g, r, DesignTokens.RadiusSm, DesignTokens.AccentPrimary); else if (_uzerinde == i) Cizim.Doldur(g, r, DesignTokens.RadiusSm, DesignTokens.SurfaceElevated);
            Cizim.Metin(g, i == 0 ? "Kolay" : "Uzman", FontRol.Body, r, secili ? DesignTokens.TextOnAccent : DesignTokens.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding, secili ? FontStyle.Bold : null);
        }
        if (Focused && ShowFocusCues) Cizim.Cerceve(g, k, DesignTokens.RadiusMd, DesignTokens.AccentPrimary, 2f);
    }
    private int Isabet(Point p) { var k = Kapsul(); if (!k.Contains(p)) return -1; return p.X < k.X + k.Width / 2 ? 0 : 1; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = Isabet(e.Location); if (i != _uzerinde) { _uzerinde = i; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _uzerinde = -1; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); Focus(); int i = Isabet(e.Location); if (i >= 0) Sec(i == 0 ? ModTuru.Kolay : ModTuru.Uzman); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Left) { e.Handled = true; Sec(ModTuru.Kolay); } else if (e.KeyCode == Keys.Right) { e.Handled = true; Sec(ModTuru.Uzman); } }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    private void Sec(ModTuru m) { if (m == _secili) return; _secili = m; Invalidate(); Secildi?.Invoke(m); }
}

// Kare ikon düğmesi (tema, bildirim zili); isteğe bağlı sayı rozeti.
internal sealed class IkonDugmesi : KabukDugmesi
{
    private IkonAdi _ikon; private int _rozet;
    public IkonDugmesi(IkonAdi ikon) { _ikon = ikon; AutoSize = true; }
    public IkonAdi Ikon { get => _ikon; set { _ikon = value; Invalidate(); } }
    // Rozet sayısı (0: rozet yok).
    public int Rozet { get => _rozet; set { if (_rozet != value) { _rozet = value; Invalidate(); } } }
    public bool RozetKritik { get; set; }
    public override Size GetPreferredSize(Size proposedSize) => new(Px(44), Px(52));
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); int b = Px(36); var r = new Rectangle((Width - b) / 2, (Height - b) / 2, b, b);
        if (Uzerinde) Cizim.Doldur(g, r, DesignTokens.RadiusMd, DesignTokens.SurfaceElevated);
        Ikonlar.Ciz(g, _ikon, new RectangleF(r.X + (b - Px(20)) / 2f, r.Y + (b - Px(20)) / 2f, Px(20), Px(20)), Uzerinde ? DesignTokens.TextPrimary : DesignTokens.TextSecondary, 1.6f);
        if (_rozet > 0)
        {
            string s = _rozet > 99 ? "99+" : _rozet.ToString(System.Globalization.CultureInfo.InvariantCulture); int rw = Math.Max(Px(16), Cizim.Genislik(s, FontRol.Caption) + Px(8)), rh = Px(16);
            var rr = new Rectangle(r.Right - rw + Px(2), r.Y - Px(2), rw, rh);
            Cizim.Doldur(g, rr, rh / 2f, RozetKritik ? DesignTokens.StatusDanger : DesignTokens.StatusWarning);
            Cizim.Metin(g, s, FontRol.Caption, rr, RozetKritik ? DesignTokens.TextOnAccent : DesignTokens.SurfaceSidebar, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding, FontStyle.Bold);
        }
        OdakHalkasi(g, r);
    }
}

// Kullanıcı menüsü düğmesi (ad + rol + ok).
internal sealed class KullaniciDugmesi : KabukDugmesi
{
    public KullaniciDugmesi() { AutoSize = true; }
    private string Ad => ServisBaglami.Oturum.Aktif?.Ad ?? "Oturum yok";
    private string Rol => ServisBaglami.Oturum.Aktif == null ? "" : Yetki.RolAdi(ServisBaglami.Oturum.Aktif.Rol);
    public void Yenile() { AccessibleName = "Kullanıcı menüsü: " + Ad; Parent?.PerformLayout(); Invalidate(); }
    public override Size GetPreferredSize(Size proposedSize) => new(Px(DesignTokens.SpaceMd) + Px(22) + Px(DesignTokens.SpaceSm) + Math.Min(Px(170), Math.Max(Cizim.Genislik(Ad, FontRol.Body), Cizim.Genislik(Rol, FontRol.Caption)) + Px(DesignTokens.SpaceMd)) + Px(DesignTokens.SpaceSm) + Px(14) + Px(DesignTokens.SpaceMd), Px(52));
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); int h = Px(40); var r = new Rectangle(0, (Height - h) / 2, Width - 1, h);
        if (Uzerinde) Cizim.Doldur(g, r, DesignTokens.RadiusMd, DesignTokens.SurfaceElevated);
        int x = r.X + Px(DesignTokens.SpaceMd);
        Ikonlar.Ciz(g, IkonAdi.Kullanici, new RectangleF(x, r.Y + (h - Px(22)) / 2f, Px(22), Px(22)), DesignTokens.AccentPrimary, 1.6f); x += Px(22) + Px(DesignTokens.SpaceSm);
        int tw = r.Right - x - Px(14) - Px(DesignTokens.SpaceMd) - Px(DesignTokens.SpaceSm);
        Cizim.Metin(g, Ad, FontRol.Body, new Rectangle(x, r.Y + Px(3), tw, h / 2), DesignTokens.TextPrimary, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding, FontStyle.Bold);
        Cizim.Metin(g, Rol, FontRol.Caption, new Rectangle(x, r.Y + h / 2, tw, h / 2 - Px(3)), DesignTokens.TextMuted, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        Ikonlar.Ciz(g, IkonAdi.ChevronAsagi, new RectangleF(r.Right - Px(DesignTokens.SpaceMd) - Px(14), r.Y + (h - Px(14)) / 2f, Px(14), Px(14)), DesignTokens.TextMuted, 1.5f);
        OdakHalkasi(g, r);
    }
}

// Menü renk tablosu: açılır menüler DesignTokens renklerinde görünür.
internal sealed class TokenRenkTablosu : ProfessionalColorTable
{
    public TokenRenkTablosu() { UseSystemColors = false; }
    public override Color ToolStripDropDownBackground => DesignTokens.SurfaceCard;
    public override Color ImageMarginGradientBegin => DesignTokens.SurfaceCard;
    public override Color ImageMarginGradientMiddle => DesignTokens.SurfaceCard;
    public override Color ImageMarginGradientEnd => DesignTokens.SurfaceCard;
    public override Color MenuBorder => DesignTokens.BorderStrong;
    public override Color MenuItemBorder => DesignTokens.Saydam(DesignTokens.AccentPrimary, 120);
    public override Color MenuItemSelected => DesignTokens.SurfaceElevated;
    public override Color MenuItemSelectedGradientBegin => DesignTokens.SurfaceElevated;
    public override Color MenuItemSelectedGradientEnd => DesignTokens.SurfaceElevated;
    public override Color MenuItemPressedGradientBegin => DesignTokens.SurfaceElevated;
    public override Color MenuItemPressedGradientEnd => DesignTokens.SurfaceElevated;
    public override Color SeparatorDark => DesignTokens.BorderSubtle;
    public override Color SeparatorLight => DesignTokens.BorderSubtle;
}
