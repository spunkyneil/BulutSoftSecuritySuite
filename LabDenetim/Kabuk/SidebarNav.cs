// Bu dosya kabuğun 4 GRUPLU SOL NAVİGASYONUDUR (236 px; kompakt kipte yalnızca ikonlar). Özel çizimli tek kontrol: grup başlıkları, vektör ikonlu öğeler, seçili/üzerinde/odak durumları,
// rolün kullanamadığı öğelerde kilit işareti ve açıklama. Öğeler NavigasyonModeli kataloğundan, görünüm moduna göre gelir. Tüm renkler DesignTokens, yazı tipleri FontProvider'dandır.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Sol navigasyon.
internal sealed class SidebarNav : KabukKontrol
{
    private enum SatirTuru { Grup, Oge }
    private sealed record Satir(SatirTuru Tur, int Y, int Yukseklik, NavGrup Grup, SayfaTanimi? Sayfa);

    private ModTuru _mod = ModTuru.Uzman; private string? _secili; private bool _kompakt; private int _vurgulu = -1, _odak = -1, _kaydirma; private bool _altDugmeUzerinde;
    private List<Satir> _satirlar = new(); private int _icerikYuksekligi;
    private readonly ToolTip _ipucu = new() { InitialDelay = 300 };

    public SidebarNav() { TabStop = true; SetStyle(ControlStyles.Selectable, true); AccessibleRole = AccessibleRole.List; AccessibleName = "Ana gezinme"; }

    // Kilitsiz bir öğe seçildi.
    public event Action<string>? SayfaSecildi;
    // Rolün kullanamadığı öğeye tıklandı.
    public event Action<string>? KilitliTiklandi;
    // Kompakt/geniş kip değişti.
    public event Action<bool>? KompaktDegisti;

    protected override TokenRol ZeminRolu => TokenRol.SurfaceSidebar;

    public ModTuru Mod { get => _mod; set { _mod = value; Yenile(); } }
    public string? Secili { get => _secili; set { _secili = value; Invalidate(); } }
    public bool Kompakt { get => _kompakt; set { if (_kompakt == value) return; _kompakt = value; _kaydirma = 0; Yenile(); KompaktDegisti?.Invoke(value); } }

    // Geniş/kompakt genişlik (piksel).
    public int HedefGenislik => Px(_kompakt ? 64 : 236);
    // Tüm öğelerin kaydırmadan sığması için gereken yükseklik (marka + öğeler + alt düğme).
    public int GerekenYukseklik => MarkaYuksekligi + _icerikYuksekligi + AltYuksekligi;
    // Şu an menüde görünen öğe sayısı.
    public int OgeSayisi => _satirlar.Count(s => s.Tur == SatirTuru.Oge);

    private int MarkaYuksekligi => Px(64);
    private int AltYuksekligi => Px(44);
    private int OgeYuksekligi => Px(40);

    // Öğeleri moda göre yeniden dizer.
    public void Yenile()
    {
        var l = new List<Satir>(); int y = 0;
        foreach (var grup in Enum.GetValues<NavGrup>())
        {
            var oge = NavigasyonModeli.Gorunenler(_mod).Where(p => p.Grup == grup).ToList(); if (oge.Count == 0) continue;
            int gy = _kompakt ? Px(DesignTokens.SpaceMd) : Px(32); l.Add(new Satir(SatirTuru.Grup, y, gy, grup, null)); y += gy;
            foreach (var p in oge) { l.Add(new Satir(SatirTuru.Oge, y, OgeYuksekligi, grup, p)); y += OgeYuksekligi + Px(DesignTokens.SpaceXs / 2); }
        }
        _satirlar = l; _icerikYuksekligi = y + Px(DesignTokens.SpaceSm); _vurgulu = -1;
        if (_odak < 0 || _odak >= l.Count || l[_odak].Tur != SatirTuru.Oge) _odak = l.FindIndex(s => s.Tur == SatirTuru.Oge && s.Sayfa!.Id == _secili);
        KaydirmayiSinirla(); Invalidate();
    }

    private int GorunurYukseklik => Math.Max(0, Height - MarkaYuksekligi - AltYuksekligi);
    private void KaydirmayiSinirla() { int en = Math.Max(0, _icerikYuksekligi - GorunurYukseklik); _kaydirma = Math.Clamp(_kaydirma, 0, en); }
    protected override void Boyutlandir() { Yenile(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); KaydirmayiSinirla(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); _kaydirma -= Math.Sign(e.Delta) * OgeYuksekligi; KaydirmayiSinirla(); Invalidate(); }

    private Rectangle OgeAlani(Satir s) => new(Px(DesignTokens.SpaceSm), MarkaYuksekligi + s.Y - _kaydirma, Width - 2 * Px(DesignTokens.SpaceSm), s.Yukseklik);
    private Rectangle AltDugmeAlani => new(0, Height - AltYuksekligi, Width, AltYuksekligi);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); var rol = ServisBaglami.Oturum.Rol;
        // Sağ kenar çizgisi.
        using (var k = new Pen(DesignTokens.BorderSubtle)) g.DrawLine(k, Width - 1, 0, Width - 1, Height);
        // Marka.
        MarkaCiz(g);
        // Kaydırılabilir bölge.
        var klip = g.Clip; g.SetClip(new Rectangle(0, MarkaYuksekligi, Width - 1, GorunurYukseklik));
        for (int i = 0; i < _satirlar.Count; i++)
        {
            var s = _satirlar[i]; var r = OgeAlani(s); if (r.Bottom < MarkaYuksekligi || r.Y > Height) continue;
            if (s.Tur == SatirTuru.Grup) { GrupCiz(g, s, r); continue; }
            OgeCiz(g, i, s, r, rol);
        }
        g.Clip = klip;
        AltDugmeCiz(g);
    }

    private void MarkaCiz(Graphics g)
    {
        int ikon = Px(26), x = _kompakt ? (Width - ikon) / 2 : Px(DesignTokens.SpaceLg);
        Ikonlar.Ciz(g, IkonAdi.Kalkan, new RectangleF(x, (MarkaYuksekligi - ikon) / 2f, ikon, ikon), DesignTokens.AccentPrimary, 2f);
        if (_kompakt) return;
        int tx = x + ikon + Px(DesignTokens.SpaceMd), tw = Width - tx - Px(DesignTokens.SpaceMd);
        Cizim.Metin(g, BrandConfig.Aktif.KisaBaslik, FontRol.Heading, new Rectangle(tx, Px(10), tw, Px(26)), DesignTokens.TextPrimary);
        Cizim.Metin(g, UrunBilgisi.SurumEtiketi, FontRol.Caption, new Rectangle(tx, Px(34), tw, Px(16)), DesignTokens.TextMuted);
    }

    private void GrupCiz(Graphics g, Satir s, Rectangle r)
    {
        if (_kompakt) { using var k = new Pen(DesignTokens.BorderSubtle); int cy = r.Y + r.Height / 2; g.DrawLine(k, Px(DesignTokens.SpaceMd), cy, Width - Px(DesignTokens.SpaceMd) - 1, cy); return; }
        Cizim.Metin(g, NavGrupBilgi.Baslik(s.Grup), FontRol.Caption, new Rectangle(r.X + Px(DesignTokens.SpaceMd), r.Y + Px(8), r.Width - Px(DesignTokens.SpaceMd), r.Height - Px(8)), DesignTokens.TextMuted, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding, FontStyle.Bold);
    }

    private void OgeCiz(Graphics g, int i, Satir s, Rectangle r, Rol? rol)
    {
        var p = s.Sayfa!; bool secili = p.Id == _secili, kilitli = NavigasyonModeli.Kilitli(p.Yetki, rol), uzerinde = i == _vurgulu;
        if (secili) { Cizim.Doldur(g, r, DesignTokens.RadiusMd, DesignTokens.Saydam(DesignTokens.AccentPrimary, 40)); using var f = new SolidBrush(DesignTokens.AccentPrimary); g.FillRectangle(f, r.X, r.Y + Px(9), Px(3), r.Height - Px(18)); }
        else if (uzerinde) Cizim.Doldur(g, r, DesignTokens.RadiusMd, DesignTokens.SurfaceElevated);
        Color ikonRenk = kilitli ? DesignTokens.TextMuted : secili ? DesignTokens.AccentPrimary : DesignTokens.TextSecondary;
        int ik = Px(20), ix = _kompakt ? r.X + (r.Width - ik) / 2 : r.X + Px(DesignTokens.SpaceMd);
        Ikonlar.Ciz(g, p.Ikon, new RectangleF(ix, r.Y + (r.Height - ik) / 2f, ik, ik), ikonRenk, 1.6f);
        if (!_kompakt)
        {
            int tx = ix + ik + Px(DesignTokens.SpaceMd), sag = r.Right - Px(DesignTokens.SpaceSm) - (kilitli ? Px(18) : 0);
            Cizim.Metin(g, p.Baslik, FontRol.Body, new Rectangle(tx, r.Y, Math.Max(0, sag - tx), r.Height), kilitli ? DesignTokens.TextMuted : secili ? DesignTokens.TextPrimary : DesignTokens.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding, secili ? FontStyle.Bold : null);
            if (kilitli) Ikonlar.Ciz(g, IkonAdi.Kilit, new RectangleF(r.Right - Px(DesignTokens.SpaceSm) - Px(14), r.Y + (r.Height - Px(14)) / 2f, Px(14), Px(14)), DesignTokens.TextMuted, 1.3f);
        }
        else if (kilitli) Ikonlar.Ciz(g, IkonAdi.Kilit, new RectangleF(r.Right - Px(14), r.Bottom - Px(14), Px(10), Px(10)), DesignTokens.TextMuted, 1.2f);
        if (Focused && ShowFocusCues && i == _odak) Cizim.Cerceve(g, r, DesignTokens.RadiusMd, DesignTokens.AccentPrimary, 2f);
    }

    private void AltDugmeCiz(Graphics g)
    {
        var r = AltDugmeAlani; using (var k = new Pen(DesignTokens.BorderSubtle)) g.DrawLine(k, 0, r.Y, Width - 1, r.Y);
        if (_altDugmeUzerinde) Cizim.Doldur(g, new RectangleF(r.X + Px(DesignTokens.SpaceSm), r.Y + Px(DesignTokens.SpaceXs), r.Width - 2 * Px(DesignTokens.SpaceSm) - 1, r.Height - 2 * Px(DesignTokens.SpaceXs)), DesignTokens.RadiusMd, DesignTokens.SurfaceElevated);
        int ik = Px(18), ix = _kompakt ? (Width - ik) / 2 : Px(DesignTokens.SpaceLg);
        Ikonlar.Ciz(g, _kompakt ? IkonAdi.ChevronSag : IkonAdi.ChevronSol, new RectangleF(ix, r.Y + (r.Height - ik) / 2f, ik, ik), DesignTokens.TextMuted, 1.6f);
        if (!_kompakt) Cizim.Metin(g, "Menüyü daralt", FontRol.Caption, new Rectangle(ix + ik + Px(DesignTokens.SpaceSm), r.Y, Width - ix - ik - Px(DesignTokens.SpaceLg), r.Height), DesignTokens.TextMuted);
    }

    // İmlecin altındaki satır dizini (öğe), yoksa -1.
    private int Isabet(Point p)
    {
        if (p.Y < MarkaYuksekligi || p.Y >= Height - AltYuksekligi) return -1;
        for (int i = 0; i < _satirlar.Count; i++) if (_satirlar[i].Tur == SatirTuru.Oge && OgeAlani(_satirlar[i]).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); int i = Isabet(e.Location); bool alt = AltDugmeAlani.Contains(e.Location);
        if (i != _vurgulu || alt != _altDugmeUzerinde)
        {
            _vurgulu = i; _altDugmeUzerinde = alt; Cursor = i >= 0 || alt ? Cursors.Hand : Cursors.Default; Invalidate();
            string ipucu = alt ? (_kompakt ? "Menüyü genişlet" : "") : i >= 0 ? (NavigasyonModeli.Kilitli(_satirlar[i].Sayfa!.Yetki, ServisBaglami.Oturum.Rol) ? NavigasyonModeli.KilitAciklamasi(ServisBaglami.Oturum.Rol) : _kompakt ? _satirlar[i].Sayfa!.Baslik : "") : "";
            _ipucu.SetToolTip(this, ipucu);
        }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (_vurgulu != -1 || _altDugmeUzerinde) { _vurgulu = -1; _altDugmeUzerinde = false; Invalidate(); } }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e); Focus();
        if (AltDugmeAlani.Contains(e.Location)) { Kompakt = !_kompakt; return; }
        int i = Isabet(e.Location); if (i >= 0) { _odak = i; Etkinlestir(i); }
    }
    private void Etkinlestir(int i)
    {
        var p = _satirlar[i].Sayfa!; if (NavigasyonModeli.Kilitli(p.Yetki, ServisBaglami.Oturum.Rol)) KilitliTiklandi?.Invoke(p.Id); else SayfaSecildi?.Invoke(p.Id);
    }

    protected override bool IsInputKey(Keys k) => k is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); var ogeler = _satirlar.Select((s, i) => (s, i)).Where(x => x.s.Tur == SatirTuru.Oge).Select(x => x.i).ToList(); if (ogeler.Count == 0) return;
        int pos = ogeler.IndexOf(_odak); if (pos < 0) pos = Math.Max(0, ogeler.FindIndex(i => _satirlar[i].Sayfa!.Id == _secili));
        switch (e.KeyCode)
        {
            case Keys.Down: pos = Math.Min(ogeler.Count - 1, pos + 1); break;
            case Keys.Up: pos = Math.Max(0, pos - 1); break;
            case Keys.Home: pos = 0; break;
            case Keys.End: pos = ogeler.Count - 1; break;
            case Keys.Enter or Keys.Space: e.Handled = true; _odak = ogeler[pos]; Etkinlestir(_odak); return;
            default: return;
        }
        e.Handled = true; _odak = ogeler[pos]; GorunurYap(_odak); Invalidate();
    }
    private void GorunurYap(int i)
    {
        var s = _satirlar[i]; int ust = s.Y, alt = s.Y + s.Yukseklik;
        if (ust < _kaydirma) _kaydirma = ust; else if (alt > _kaydirma + GorunurYukseklik) _kaydirma = alt - GorunurYukseklik; KaydirmayiSinirla();
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (_odak < 0) _odak = _satirlar.FindIndex(s => s.Tur == SatirTuru.Oge); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) _ipucu.Dispose(); base.Dispose(disposing); }
}
