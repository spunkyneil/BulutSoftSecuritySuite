// Bu dosya Form1'e "Kolay Görünüm / Uzman Görünümü" geçişini ekler. MEVCUT HİÇBİR ÖZELLİK SİLİNMEZ: Uzman Görünümü eski ekranın aynısıdır; Kolay Görünüm yalnızca üstüne sade bir katman koyar.
//  • En üstte büyük geçiş anahtarı; ilk açılışta KOLAY. Tercih kalıcıdır (arayuz_modu.json).
//  • Kolay'da karmaşık tablolar/radar/30+ menü gizlenir, 3 kartlı sade panel gösterilir; Uzman'a geçince her şey geri gelir.
//  • Kart işleri (bilgisayar kontrolü, dış site incelemesi, rapor) mevcut tarama motorlarını kullanır ve sonucu Uzman tablosuna da yazar.
using System.Drawing.Drawing2D;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// İki parçalı büyük geçiş anahtarı.
internal sealed class ModAnahtari : Control, IThemeAware
{
    // Tema değişince yeniden çizilir (renkler çizim anında TemaYonetici'den okunur).
    public void TemaUygula() => Invalidate();
    internal ModTuru _secili = ModTuru.Kolay;
    // Seçim değişince (kullanıcı tıklayınca) çağrılır.
    public event Action<ModTuru>? Secildi;
    public ModAnahtari() { Size = new Size(560, 42); Cursor = Cursors.Hand; TabStop = true; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); AccessibleName = "Görünüm seçimi"; AccessibleRole = AccessibleRole.RadioButton; }
    // Seçili mod (dışarıdan ayarlanınca olay vermez).
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ModTuru Secili { get => _secili; set { _secili = value; Invalidate(); } }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right || base.IsInputKey(k);
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); Sec(e.X < Width / 2 ? ModTuru.Kolay : ModTuru.Uzman); Focus(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Left) Sec(ModTuru.Kolay); else if (e.KeyCode == Keys.Right) Sec(ModTuru.Uzman); }
    internal void Sec(ModTuru m) { if (m == _secili) return; _secili = m; Invalidate(); Secildi?.Invoke(m); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(TemaYonetici.Zemin(this)); int w = Width / 2;
        using var f = FontProvider.Gecici("Segoe UI", 11f, FontStyle.Bold);
        void Parca(Rectangle r, bool secili, Color renk, string yazi, bool nokta)
        {
            using var yol = Yuvarlak(r, 10); using (var b = new SolidBrush(TemaYonetici.C(secili ? renk : DesignTokens.Ozgun(TokenRol.SurfaceInput)))) g.FillPath(b, yol); using (var p = new Pen(TemaYonetici.C(secili ? renk : DesignTokens.Ozgun(TokenRol.BorderStrong)), 1.5f)) g.DrawPath(p, yol);
            Color yaziRenk = secili ? Color.White : TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted));
            if (nokta) { using var b = new SolidBrush(secili ? Color.White : TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusSuccess))); g.FillEllipse(b, r.X + 16, r.Y + r.Height / 2 - 6, 12, 12); }
            else { using var p = new Pen(yaziRenk, 2f); int cx = r.X + 22, cy = r.Y + r.Height / 2; g.DrawEllipse(p, cx - 6, cy - 6, 12, 12); for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; g.DrawLine(p, (float)(cx + 6 * Math.Cos(a)), (float)(cy + 6 * Math.Sin(a)), (float)(cx + 9 * Math.Cos(a)), (float)(cy + 9 * Math.Sin(a))); } }
            TextRenderer.DrawText(g, yazi, f, new Rectangle(r.X + 38, r.Y, r.Width - 44, r.Height), yaziRenk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        Parca(new Rectangle(1, 1, w - 3, Height - 3), _secili == ModTuru.Kolay, DesignTokens.Ozgun(TokenRol.StatusSuccess), "Kolay Görünüm (Önerilen)", true);
        Parca(new Rectangle(w + 2, 1, w - 3, Height - 3), _secili == ModTuru.Uzman, DesignTokens.Ozgun(TokenRol.AccentPrimary), "Uzman Görünümü", false);
        if (Focused) using (var p = new Pen(TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusInfo)), 2f)) { using var y = Yuvarlak(new Rectangle(0, 0, Width - 1, Height - 1), 11); g.DrawPath(p, y); }
    }
    internal static GraphicsPath Yuvarlak(Rectangle r, int y) { var p = new GraphicsPath(); int d = y * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
}
