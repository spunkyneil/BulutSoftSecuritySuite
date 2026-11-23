// Bu dosya kabuğun GLOBAL ÜST ÇUBUĞUDUR (52 px, DPI'a göre ölçeklenir): solda sayfa başlığı + ekmek kırıntısı, ortada Ctrl+K komut/arama tetikleyicisi,
// sağda Kolay|Uzman anahtarı, tema ikonu, bildirim zili (son 30 dk Uyarı/Kritik rozeti) ve kullanıcı menüsü.
// Yerleşim TableLayoutPanel'dir; sabit koordinat yoktur. Üst çubuk yalnızca olay verir (iş mantığı kabukta/Form1'de).

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Kullanıcı menüsü öğeleri.
internal enum KullaniciMenuOgesi { Hesap, Lisans, KullaniciDegistir, Yardim, Tur, Hakkinda, Cikis }

// Global üst çubuk.
internal sealed class GlobalHeader : TableLayoutPanel, IKendiRenginiYoneten, IThemeAware
{
    private readonly BaslikAlani _baslik = new(); private readonly AramaTetikleyici _ara = new();
    private readonly KabukAkis _sag = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill };
    private readonly ModSegmenti _mod = new(); private readonly IkonDugmesi _tema = new(IkonAdi.Ay), _zil = new(IkonAdi.Zil); private readonly KullaniciDugmesi _kullanici = new();
    private readonly ToolTip _ipucu = new() { AutoPopDelay = 8000, InitialDelay = 400 };

    public GlobalHeader()
    {
        DoubleBuffered = true; Dock = DockStyle.Top; Margin = Padding.Empty; ColumnCount = 3; RowCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        BackColor = DesignTokens.Renk(TokenRol.SurfaceApp);
        _baslik.Dock = DockStyle.Fill; _baslik.Margin = Padding.Empty; _ara.Dock = DockStyle.Fill; _ara.Margin = Padding.Empty; _ara.AccessibleName = "Komut veya bulgu ara (Ctrl+K)";
        foreach (Control c in new Control[] { _mod, _tema, _zil, _kullanici }) { c.Margin = Padding.Empty; _sag.Controls.Add(c); }
        Controls.Add(_baslik, 0, 0); Controls.Add(_ara, 1, 0); Controls.Add(_sag, 2, 0);
        _ipucu.SetToolTip(_ara, "Komut paletini açar: sayfalara git, tarama başlat, ayarları bul (Ctrl+K)");
        _ipucu.SetToolTip(_tema, "Koyu / Açık tema"); _ipucu.SetToolTip(_zil, "Son 30 dakikadaki Uyarı ve Kritik olaylar — tıklayınca Olay Zaman Çizgisi açılır"); _ipucu.SetToolTip(_mod, "Kolay: sade görünüm · Uzman: tüm araçlar");
        _tema.AccessibleName = "Temayı değiştir"; _zil.AccessibleName = "Bildirimler";
        _ara.Click += (_, _) => AramaTiklandi?.Invoke(); _mod.Secildi += m => ModSecildi?.Invoke(m); _tema.Click += (_, _) => TemaTiklandi?.Invoke(); _zil.Click += (_, _) => ZilTiklandi?.Invoke(); _kullanici.Click += (_, _) => KullaniciMenusuAc();
        TemaIkonunuGuncelle(); _kullanici.Yenile();
    }

    // Olaylar.
    public event Action? AramaTiklandi, TemaTiklandi, ZilTiklandi;
    public event Action<ModTuru>? ModSecildi;
    public event Action<KullaniciMenuOgesi>? KullaniciMenuSecildi;

    // Sayfa başlığı ve ekmek kırıntısı.
    public string Baslik { get => _baslik.Baslik; set => _baslik.Baslik = value; }
    public string Kirinti { get => _baslik.Kirinti; set => _baslik.Kirinti = value; }
    // Görünüm modu.
    public ModTuru Mod { get => _mod.Secili; set => _mod.Secili = value; }
    // Bildirim rozeti: son 30 dakikadaki Uyarı+Kritik sayısı; kritik varsa kırmızı.
    public void BildirimAyarla(int sayi, bool kritikVar) { _zil.Rozet = sayi; _zil.RozetKritik = kritikVar; _zil.AccessibleName = sayi > 0 ? $"Bildirimler: {sayi} uyarı" : "Bildirimler"; }
    // Kullanıcı adı/rol değişti.
    public void KullaniciYenile() => _kullanici.Yenile();

    // Üst çubuk yüksekliği ve iç boşluklar DPI'a göre kurulur.
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); OlculeriKur(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); OlculeriKur(); }
    private void OlculeriKur() { Height = DesignTokens.Px(this, 52); Padding = new Padding(DesignTokens.Px(this, DesignTokens.SpaceLg), 0, DesignTokens.Px(this, DesignTokens.SpaceMd), 1); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); using var k = new Pen(DesignTokens.BorderSubtle); e.Graphics.DrawLine(k, 0, Height - 1, Width, Height - 1);
    }

    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); TemaIkonunuGuncelle(); Invalidate(true); }

    // Koyu temadayken Güneş (açığa geç), açıkta Ay (koyuya geç) gösterilir.
    private void TemaIkonunuGuncelle() => _tema.Ikon = DesignTokens.KoyuMu ? IkonAdi.Gunes : IkonAdi.Ay;

    // Kullanıcı menüsünün içeriği (gruplar ayraçla ayrılır): her KullaniciMenuOgesi tam bir kez yer almalı (test denetler).
    internal static readonly (string Yazi, KullaniciMenuOgesi Oge)[][] MenuGruplari =
    {
        new[] { ("Hesabım ve Kullanıcılar", KullaniciMenuOgesi.Hesap), ("Lisans", KullaniciMenuOgesi.Lisans), ("Kullanıcı Değiştir", KullaniciMenuOgesi.KullaniciDegistir) },
        new[] { ("Yardım  (F1)", KullaniciMenuOgesi.Yardim), ("Tanıtım Turu", KullaniciMenuOgesi.Tur), ("Hakkımızda & Destek", KullaniciMenuOgesi.Hakkinda) },
        new[] { ("Programdan Çıkış", KullaniciMenuOgesi.Cikis) }
    };

    // Kullanıcı menüsünü düğmenin altında açar (her açılışta güncel tema renkleriyle kurulur).
    private void KullaniciMenusuAc()
    {
        var menu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new TokenRenkTablosu()) { RoundedEdges = false }, Font = FontProvider.Al(FontRol.Body), BackColor = DesignTokens.SurfaceCard, ForeColor = DesignTokens.TextPrimary, ShowImageMargin = false };
        void Ekle(string yazi, KullaniciMenuOgesi oge) { var m = new ToolStripMenuItem(yazi) { ForeColor = DesignTokens.TextPrimary, Padding = new Padding(DesignTokens.Px(this, DesignTokens.SpaceSm), DesignTokens.Px(this, DesignTokens.SpaceXs), DesignTokens.Px(this, DesignTokens.SpaceSm), DesignTokens.Px(this, DesignTokens.SpaceXs)) }; m.Click += (_, _) => KullaniciMenuSecildi?.Invoke(oge); menu.Items.Add(m); }
        for (int g = 0; g < MenuGruplari.Length; g++) { if (g > 0) menu.Items.Add(new ToolStripSeparator()); foreach (var (yazi, oge) in MenuGruplari[g]) Ekle(yazi, oge); }
        menu.Closed += (_, _) => menu.BeginInvoke(new Action(menu.Dispose));
        menu.Show(_kullanici, new Point(0, _kullanici.Height));
    }

    protected override void Dispose(bool disposing) { if (disposing) _ipucu.Dispose(); base.Dispose(disposing); }
}
