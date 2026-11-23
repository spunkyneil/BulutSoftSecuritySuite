// Bu dosya sayfa İÇİ SEKMELERİ yönetir: üstte sekme şeridi (rol ve moda göre görünen/kilitli sekmeler), altta seçili sekmenin sayfası.
// İsteğe bağlı bir "üst eylem çubuğu" (ör. Savunma Merkezi'ndeki kırmızı Acil İzolasyon çubuğu) sekmelerin üstünde sabit durur.
// Sekme içerikleri IModulePage'dir (UserControl ya da FormSayfaAdaptoru); yalnızca seçilen sekme açılır, diğerleri canlı ama gizli bekler.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Sekme şeridi.
internal sealed class SekmeSeridi : KabukKontrol
{
    private readonly List<(string Id, string Baslik, bool Kilitli)> _ogeler = new();
    private string? _secili; private int _vurgulu = -1;
    private readonly ToolTip _ipucu = new();

    public SekmeSeridi() { TabStop = true; SetStyle(ControlStyles.Selectable, true); AccessibleRole = AccessibleRole.PageTabList; AccessibleName = "Sayfa sekmeleri"; }

    // Sekme seçildi (kilitli olmayan).
    public event Action<string>? SekmeSecildi;
    // Kilitli sekmeye tıklandı.
    public event Action<string>? KilitliTiklandi;

    protected override TokenRol ZeminRolu => TokenRol.SurfaceApp;

    // Şeridin sekmelerini ve seçili olanı ayarlar.
    public void Ayarla(IEnumerable<(string Id, string Baslik, bool Kilitli)> ogeler, string? secili)
    {
        _ogeler.Clear(); _ogeler.AddRange(ogeler); _secili = secili; _vurgulu = -1; Invalidate();
    }
    public string? Secili { get => _secili; set { _secili = value; Invalidate(); } }

    protected override Size DefaultSize => new(400, 44);
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width, Px(44));

    private Rectangle[] Alanlar()
    {
        var l = new Rectangle[_ogeler.Count]; int x = Px(DesignTokens.SpaceLg);
        for (int i = 0; i < l.Length; i++)
        {
            int w = Cizim.Genislik(_ogeler[i].Baslik, FontRol.Body, FontStyle.Bold) + Px(DesignTokens.SpaceXl) + Px(DesignTokens.SpaceXs) + (_ogeler[i].Kilitli ? Px(18) : 0);   // seçili sekme kalın çizildiği için genişlik kalın yazıyla ölçülür
            l[i] = new Rectangle(x, 0, w, Height - 1); x += w + Px(DesignTokens.SpaceXs);
        }
        return l;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); var alanlar = Alanlar();
        using (var k = new Pen(DesignTokens.BorderSubtle)) g.DrawLine(k, 0, Height - 1, Width, Height - 1);
        for (int i = 0; i < alanlar.Length; i++)
        {
            var (id, baslik, kilitli) = _ogeler[i]; bool secili = id == _secili; var r = alanlar[i];
            if (i == _vurgulu && !secili) Cizim.Doldur(g, new RectangleF(r.X, r.Y + Px(6), r.Width, r.Height - Px(10)), DesignTokens.RadiusSm, DesignTokens.SurfaceElevated);
            Color yazi = kilitli ? DesignTokens.TextMuted : secili ? DesignTokens.TextPrimary : DesignTokens.TextSecondary;
            var metin = new Rectangle(r.X + Px(DesignTokens.SpaceMd), r.Y, r.Width - Px(DesignTokens.SpaceXl) - (kilitli ? Px(18) : 0), r.Height);
            Cizim.Metin(g, baslik, FontRol.Body, metin, yazi, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding, secili ? FontStyle.Bold : null);
            if (kilitli) Ikonlar.Ciz(g, IkonAdi.Kilit, new RectangleF(r.Right - Px(DesignTokens.SpaceMd) - Px(14), (Height - Px(14)) / 2f, Px(14), Px(14)), DesignTokens.TextMuted, 1.3f);
            if (secili) { using var f = new SolidBrush(DesignTokens.AccentPrimary); g.FillRectangle(f, r.X + Px(DesignTokens.SpaceSm), Height - Px(3) - 1, r.Width - Px(DesignTokens.SpaceMd), Px(3)); }
        }
        if (Focused && ShowFocusCues && alanlar.Length > 0) { int fi = Math.Max(0, _ogeler.FindIndex(o => o.Id == _secili)); var fr = alanlar[fi]; fr.Inflate(-1, -Px(5)); ControlPaint.DrawFocusRectangle(g, fr, DesignTokens.TextMuted, DesignTokens.SurfaceApp); }
    }

    private int Isabet(Point p) { var a = Alanlar(); for (int i = 0; i < a.Length; i++) if (a[i].Contains(p)) return i; return -1; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); int i = Isabet(e.Location);
        if (i != _vurgulu) { _vurgulu = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); if (i >= 0 && _ogeler[i].Kilitli) _ipucu.SetToolTip(this, NavigasyonModeli.KilitAciklamasi(ServisBaglami.Oturum.Rol)); else _ipucu.SetToolTip(this, ""); }
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (_vurgulu != -1) { _vurgulu = -1; Invalidate(); } }
    protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); Focus(); Sec(Isabet(e.Location)); }
    private void Sec(int i) { if (i < 0 || i >= _ogeler.Count) return; var o = _ogeler[i]; if (o.Kilitli) KilitliTiklandi?.Invoke(o.Id); else SekmeSecildi?.Invoke(o.Id); }
    protected override bool IsInputKey(Keys k) => k is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(k);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (_ogeler.Count == 0) return; int mevcut = Math.Max(0, _ogeler.FindIndex(o => o.Id == _secili)), yeni = mevcut;
        if (e.KeyCode == Keys.Right) yeni = Math.Min(_ogeler.Count - 1, mevcut + 1); else if (e.KeyCode == Keys.Left) yeni = Math.Max(0, mevcut - 1); else if (e.KeyCode == Keys.Home) yeni = 0; else if (e.KeyCode == Keys.End) yeni = _ogeler.Count - 1; else return;
        e.Handled = true; Sec(yeni);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void Dispose(bool disposing) { if (disposing) _ipucu.Dispose(); base.Dispose(disposing); }
}

// Sekmeli sayfa.
internal sealed class SekmeliSayfa : TableLayoutPanel, IModulePage, IKendiRenginiYoneten, IThemeAware, IModDuyarli, ISekmeBilgisi, ISekmeGecisi
{
    private readonly SayfaTanimi _tanim;
    private readonly Dictionary<string, IModulePage> _icerik;
    private readonly SekmeSeridi _serit = new(); private readonly TokenPanel _alan = new(TokenRol.SurfaceApp);
    private readonly Control? _ust;
    private ModTuru _mod = ModTuru.Uzman; private string? _aktifSekme; private CancellationToken _ct; private bool _acik;

    // tanim: kataloktaki sayfa; icerikler: sekme kimliği → sekmenin sayfası; ustCubuk: sekmelerin üstünde sabit duran kontrol (isteğe bağlı).
    public SekmeliSayfa(SayfaTanimi tanim, IReadOnlyDictionary<string, IModulePage> icerikler, Control? ustCubuk = null)
    {
        _tanim = tanim; _icerik = new Dictionary<string, IModulePage>(icerikler); _ust = ustCubuk;
        Dock = DockStyle.Fill; ColumnCount = 1; RowCount = 3; Margin = Padding.Empty; Padding = Padding.Empty; BackColor = DesignTokens.Renk(TokenRol.SurfaceApp);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        RowStyles.Add(new RowStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        if (_ust != null) { _ust.Dock = DockStyle.Fill; Controls.Add(_ust, 0, 0); }
        _serit.Dock = DockStyle.Fill; Controls.Add(_serit, 0, 1);
        _alan.Dock = DockStyle.Fill; Controls.Add(_alan, 0, 2);
        _serit.SekmeSecildi += async id => await SekmeGitAsync(id);
        _serit.KilitliTiklandi += id => { var s = _tanim.Sekmeler.FirstOrDefault(x => x.Id == id); KabukYetki.Kontrol(FindForm(), s?.Yetki, s?.Baslik ?? _tanim.Baslik); };
        SeridiYenile();
    }

    public string SayfaId => _tanim.Id;
    public string Baslik => _tanim.Baslik;
    public NavGrup Grup => _tanim.Grup;
    public Eylem? GerekliYetki => _tanim.Yetki;
    public bool SadeceUzman => _tanim.SadeceUzman;
    public Control Gorunum => this;

    // Seçili sekme kimliği.
    public string? AktifSekme => _aktifSekme;
    // Seçili sekmenin başlığı (ekmek kırıntısı için).
    public string? AktifSekmeBasligi => _tanim.Sekmeler.FirstOrDefault(s => s.Id == _aktifSekme)?.Baslik;
    // Sekme değişti (kabuk ekmek kırıntısını günceller).
    public event Action? SekmeBilgisiDegisti;

    // Görünüm modu değişince sekmeler yeniden hesaplanır; aktif sekme bu modda görünmüyorsa ilk görünen sekmeye geçilir.
    public ModTuru Mod
    {
        get => _mod;
        set
        {
            _mod = value; SeridiYenile();
            if (_aktifSekme != null && !NavigasyonModeli.GorunenSekmeler(_tanim, _mod).Any(s => s.Id == _aktifSekme))
            {
                var ilk = NavigasyonModeli.GorunenSekmeler(_tanim, _mod).FirstOrDefault(s => _icerik.ContainsKey(s.Id));
                if (ilk != null) _ = SekmeGitAsync(ilk.Id);
            }
        }
    }
    public void ModDegisti(ModTuru mod) => Mod = mod;

    // Rol değişince (oturum) kilitler yeniden hesaplanır.
    public void SeridiYenile()
    {
        var gorunen = NavigasyonModeli.GorunenSekmeler(_tanim, _mod).Where(s => _icerik.ContainsKey(s.Id)).ToList();
        var rol = ServisBaglami.Oturum.Rol;
        _serit.Ayarla(gorunen.Select(s => (s.Id, s.Baslik, NavigasyonModeli.Kilitli(s.Yetki, rol))), _aktifSekme);
        _serit.Visible = gorunen.Count > 0;
    }

    public async Task AcildigindaAsync(CancellationToken ct)
    {
        _ct = ct; _acik = true; SeridiYenile();
        if (_aktifSekme != null && _icerik.ContainsKey(_aktifSekme)) { await _icerik[_aktifSekme].AcildigindaAsync(ct); return; }
        // İlk açılış: kilitli olmayan ilk görünen sekme.
        var rol = ServisBaglami.Oturum.Rol;
        var ilk = NavigasyonModeli.GorunenSekmeler(_tanim, _mod).FirstOrDefault(s => _icerik.ContainsKey(s.Id) && !NavigasyonModeli.Kilitli(s.Yetki, rol));
        if (ilk != null) await SekmeGitAsync(ilk.Id);
    }

    public async Task KapanirkenAsync()
    {
        _acik = false;
        if (_aktifSekme != null && _icerik.TryGetValue(_aktifSekme, out var s)) await s.KapanirkenAsync();
    }

    // Belirtilen sekmeye geçer (yetki ve mod denetimiyle). Başarılıysa true.
    public async Task<bool> SekmeGitAsync(string sekmeId)
    {
        if (!_icerik.TryGetValue(sekmeId, out var sayfa)) return false;
        var tanim = _tanim.Sekmeler.FirstOrDefault(s => s.Id == sekmeId);
        if (tanim != null && !KabukYetki.Kontrol(FindForm(), tanim.Yetki, tanim.Baslik)) return false;
        if (_aktifSekme == sekmeId) return true;
        if (_aktifSekme != null && _icerik.TryGetValue(_aktifSekme, out var eski)) { await eski.KapanirkenAsync(); eski.Gorunum.Visible = false; }
        _aktifSekme = sekmeId; _serit.Secili = sekmeId;
        var v = sayfa.Gorunum;
        if (v.Parent != _alan) { v.Dock = DockStyle.Fill; _alan.Controls.Add(v); }
        v.Visible = true; v.BringToFront();
        SekmeBilgisiDegisti?.Invoke();
        if (_acik) await sayfa.AcildigindaAsync(_ct);
        return true;
    }

    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Invalidate(true); }
}
