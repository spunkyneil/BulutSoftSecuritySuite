// Bu dosya "Raporlar & Kanıt Paketi" sayfasıdır: tek ekranda tüm çıktı türleri (HTML, PDF, Yönetici/Kurul Özeti, Adli Kanıt .ZIP, SIEM CEF/JSONL) kartlar olarak durur.
// Sayfa yalnızca arayüzdür; her kart, Form1'deki mevcut rapor işleyicilerini (yetki, boş sonuç uyarısı, kaydetme penceresi aynen) çağırır.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Tek bir çıktı türünü anlatan tıklanabilir kart.
internal sealed class RaporKarti : KabukDugmesi
{
    private readonly IkonAdi _ikon; private readonly string _baslik, _aciklama, _eylem;
    public RaporKarti(IkonAdi ikon, string baslik, string aciklama, string eylem) { _ikon = ikon; _baslik = baslik; _aciklama = aciklama; _eylem = eylem; AccessibleName = baslik; Dock = DockStyle.Fill; AutoSize = true; }
    public override Size GetPreferredSize(Size proposedSize) => new(Math.Max(Px(280), proposedSize.Width), Px(116));
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); var r = new Rectangle(0, 0, Width - Px(DesignTokens.SpaceMd), Height - Px(DesignTokens.SpaceMd));
        Cizim.Doldur(g, r, DesignTokens.RadiusLg, Uzerinde ? DesignTokens.SurfaceElevated : DesignTokens.SurfaceCard); Cizim.Cerceve(g, r, DesignTokens.RadiusLg, Uzerinde ? DesignTokens.AccentPrimary : DesignTokens.BorderSubtle);
        int rozet = Px(44), x = r.X + Px(DesignTokens.SpaceLg), y = r.Y + Px(DesignTokens.SpaceLg);
        Cizim.Doldur(g, new RectangleF(x, y, rozet, rozet), DesignTokens.RadiusMd, DesignTokens.Saydam(DesignTokens.AccentPrimary, 40));
        Ikonlar.Ciz(g, _ikon, new RectangleF(x + (rozet - Px(24)) / 2f, y + (rozet - Px(24)) / 2f, Px(24), Px(24)), DesignTokens.AccentPrimary, 1.8f);
        int tx = x + rozet + Px(DesignTokens.SpaceMd), tw = r.Right - tx - Px(DesignTokens.SpaceLg);
        Cizim.Metin(g, _baslik, FontRol.Subheading, new Rectangle(tx, y - Px(2), tw, Px(24)), DesignTokens.TextPrimary);
        Cizim.MetinSarili(g, _aciklama, FontRol.Caption, new Rectangle(tx, y + Px(24), tw, Px(36)), DesignTokens.TextSecondary, 2);   // kelime sonunda satır atlar (EndEllipsis kelimeyi ortadan böler)
        Cizim.Metin(g, _eylem + "  ›", FontRol.Body, new Rectangle(tx, r.Bottom - Px(30), tw, Px(22)), DesignTokens.AccentPrimary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding, FontStyle.Bold);
        OdakHalkasi(g, r);
    }
}

// Raporlar sayfası.
internal sealed class RaporlarSayfasi : TableLayoutPanel, IModulePage, IKendiRenginiYoneten, IThemeAware
{
    // Kartlar: HTML, PDF, Yönetici/Kurul Özeti, Adli Kanıt Paketi, SIEM CEF, SIEM JSON Lines (her biri mevcut işleyiciyi çalıştırır).
    public RaporlarSayfasi(Action html, Action pdf, Action yonetici, Action kanit, Action siemCef, Action siemJson)
    {
        Dock = DockStyle.Fill; ColumnCount = 2; RowCount = 4; Margin = Padding.Empty; AutoScroll = true; DoubleBuffered = true; BackColor = DesignTokens.Renk(TokenRol.SurfaceApp);
        Padding = new Padding(DesignTokens.SpaceLg, DesignTokens.SpaceLg, DesignTokens.SpaceSm, DesignTokens.SpaceLg);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); for (int i = 0; i < 3; i++) RowStyles.Add(new RowStyle(SizeType.AutoSize)); RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // son satır boş artan yeri alır; kartlar uzamaz
        var kartlar = new (IkonAdi, string, string, string, Action)[]
        {
            (IkonAdi.Rapor, "Web Sayfası Raporu (HTML)", "Renkli, ayrıntılı tarama raporu. Her bulgu için \"bu ne demek, nasıl çözerim\" açıklaması içerir.", "HTML oluştur", html),
            (IkonAdi.Rapor, "PDF Raporu", "Aynı raporun yazdırılabilir PDF sürümü; yönetime ya da denetçiye iletmek için.", "PDF oluştur", pdf),
            (IkonAdi.Panel, "Yönetici / Kurul Özeti", "Teknik olmayan yöneticiler için tek sayfalık güvenlik karnesi (HTML) ya da ayrıntılı PDF.", "Özeti oluştur", yonetici),
            (IkonAdi.Kalkan, "Adli Kanıt Paketi (.ZIP)", "Bulgular, olay günlüğü ve özetleri mühürlü tek pakette toplar; olay sonrası inceleme için.", "Paketi hazırla", kanit),
            (IkonAdi.Olay, "SIEM Çıktısı — CEF", "Kurumsal SIEM sistemlerine aktarmak için CEF biçiminde bulgu dosyası.", "CEF yaz", siemCef),
            (IkonAdi.Olay, "SIEM Çıktısı — JSON Lines", "Log toplayıcılar ve analiz araçları için satır satır JSON bulgu dosyası.", "JSONL yaz", siemJson),
        };
        for (int i = 0; i < kartlar.Length; i++) { var (ikon, baslik, aciklama, eylem, islem) = kartlar[i]; var k = new RaporKarti(ikon, baslik, aciklama, eylem) { Margin = Padding.Empty }; k.Click += (_, _) => islem(); Controls.Add(k, i % 2, i / 2); }
    }

    public string SayfaId => "rapor";
    public string Baslik => "Raporlar & Kanıt Paketi";
    public NavGrup Grup => NavGrup.RaporlamaAraclar;
    public Eylem? GerekliYetki => Eylem.Rapor;
    public bool SadeceUzman => false;
    public Control Gorunum => this;
    public Task AcildigindaAsync(CancellationToken ct) => Task.CompletedTask;
    public Task KapanirkenAsync() => Task.CompletedTask;
    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Invalidate(true); }
}
