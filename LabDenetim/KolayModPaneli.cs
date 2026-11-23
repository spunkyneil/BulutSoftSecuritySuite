// Bu dosya KOLAY GÖRÜNÜM'ün ekranlarını çizer: 3 büyük eylem kartı, konuşan ilerleme, trafik ışığı karnesi, durum göstergeleri ve site sahibine gönderilecek mektup penceresi.
// Panel yalnızca GÖRÜNÜMDÜR: asıl işleri Form1 yapar (panel ona "Func/Action" kancalarıyla bağlanır). Hiçbir sistem ayarını değiştirmez.
// Simgeler yazı tipine bağlı olmasın diye (emoji/yazı tipi sorunu) GDI+ ile ÇİZİLİR.
using BulutSoftSecuritySuite.Kontroller;
using System.Drawing.Drawing2D;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Konuşan ilerleme metinleri (ekrandan bağımsız: test edilebilir).
internal static class KonusanIlerleme
{
    // Dış web sitesi incelemesinin 4 adımı.
    public static readonly string[] WebAdimlari =
    {
        "Siteye güvenli bir ziyaretçi gibi bağlanılıyor...",
        "Güvenlik kilidi ve kimlik kartı (SSL) kontrol ediliyor...",
        "Site sahibinin iletişim adresi (security.txt) aranıyor...",
        "Anlaşılır özet ve bilgilendirme mektubu hazırlanıyor..."
    };
    // Bilgisayar kontrolünün 4 adımı.
    public static readonly string[] BilgisayarAdimlari =
    {
        "Bilgisayarınızdaki programlar ve hizmetler okunuyor...",
        "Açık kapılar ve ağ hizmetleri kontrol ediliyor...",
        "Windows güncellemeleri ve kurulu yazılımlar inceleniyor...",
        "Anlaşılır güvenlik karnesi hazırlanıyor..."
    };

    // "2/4: ..." biçiminde cümle (adım 1'den başlar).
    public static string Cumle(string[] adimlar, int adim) { adim = Math.Clamp(adim, 1, adimlar.Length); return $"{adim}/{adimlar.Length}: {adimlar[adim - 1]}"; }

    // Pasif toplayıcının teknik mesajını (HTTP/TLS/DNS/security.txt) 1-3 numaralı adıma çevirir; tanınmazsa 0 (adım değişmez).
    public static int WebAdimi(string? toplayiciMesaji)
    {
        var m = toplayiciMesaji ?? "";
        if (m.Contains("security.txt", StringComparison.OrdinalIgnoreCase)) return 3;
        if (m.Contains("TLS", StringComparison.OrdinalIgnoreCase) || m.Contains("DNS", StringComparison.OrdinalIgnoreCase) || m.Contains("sertifika", StringComparison.OrdinalIgnoreCase)) return 2;
        if (m.Contains("HTTP", StringComparison.OrdinalIgnoreCase) || m.Contains("ana sayfa", StringComparison.OrdinalIgnoreCase)) return 1;
        return 0;
    }
}

// Site sahibine mektup metni (sade Türkçe; düzenlenebilir bir yazı olarak gösterilir).
internal static class MektupMetni
{
    // Mektubu üretir. ad/iletisim boşsa yer tutucu bırakılır (kullanıcı pencerede düzenler).
    public static string Uret(PasifGozlem g, List<PasifBulgu> bulgular, string ad, string iletisim)
    {
        var sb = new StringBuilder(); var sirali = bulgular.OrderByDescending(b => b.Etki).ThenBy(b => b.RuleId).ToList(); int dk = EtikBildirim.ToplamDakika(bulgular);
        sb.AppendLine($"Konu: {EtikBildirim.Konu(g)}").AppendLine();
        sb.AppendLine("Sayın Güvenlik / Bilgi İşlem Sorumlusu,").AppendLine();
        sb.AppendLine($"İyi niyetle ve yalnızca yardımcı olmak amacıyla yazıyorum. {g.Host} adresinizi, sıradan bir internet ziyaretçisinin görebileceği kadarıyla (hiçbir sisteme girmeden, şifre denemeden, yük bindirmeden) inceledim ve iyileştirilebilecek bazı noktalar fark ettim.").AppendLine();
        if (sirali.Count == 0) sb.AppendLine("Bu dışarıdan bakışta düzeltilmesi gereken bir nokta görmedim. Emeğinize teşekkür ederim.").AppendLine();
        else
        {
            sb.AppendLine($"Gördüğüm {sirali.Count} öneri şunlar (en önemlisi başta):").AppendLine(); int n = 0;
            foreach (var b in sirali)
            {
                var h = HalkDiliCevirici.Cevir(b.Modul, b.Baslik, "", b.Onem);
                sb.AppendLine($"{++n}) {b.Baslik}").AppendLine($"   Ne demek: {h.BuNeDemek}").AppendLine($"   Olası zarar: {h.Zarar}").AppendLine($"   Önerim: {b.Cozum}");
                if (!string.IsNullOrWhiteSpace(b.Komut)) sb.AppendLine($"   Örnek ayar: {b.Komut}");
                sb.AppendLine();
            }
            sb.AppendLine($"Hepsinin birlikte tahmini düzeltme süresi, deneyimli bir sistem yöneticisi için yaklaşık {EtikBildirim.SureYazi(dk).TrimStart('~')} (test dahil değil).").AppendLine();
        }
        sb.AppendLine("Bu bilgileri yalnızca size iletiyorum; başkalarıyla veya kamuyla paylaşmayacağım. Düzeltmeleri yaptığınızda isterseniz aynı kontrolü tekrarlayıp sonucu paylaşabilirim. Bu notlar dışarıdan görülebilen bilgilere dayanıyor; sizin tarafınızda zaten çözülmüş veya yanlış anlaşılmış noktalar olabilir, bunun için şimdiden özür dilerim.").AppendLine();
        sb.AppendLine("Saygılarımla,").AppendLine(string.IsNullOrWhiteSpace(ad) ? "[Adınız]" : ad).AppendLine(string.IsNullOrWhiteSpace(iletisim) ? "[İletişim bilginiz]" : iletisim);
        return sb.ToString();
    }
}

// Büyük, tıklanabilir eylem kartı (çizimli simge + başlık + açıklama).
internal sealed class EylemKarti : Control, IThemeAware
{
    // Tema değişince yeniden çizilir (renkler çizim anında TemaYonetici'den okunur).
    public void TemaUygula() => Invalidate();
    private readonly int _simge; private readonly string _baslik, _alt; private bool _ustunde;
    // Tüm kartların başlıkları (yazı boyutu en uzun başlığa göre ortak seçilir).
    private static readonly List<string> _tumBasliklar = new();
    // Simge: 0 = kalkan, 1 = küre, 2 = rapor.
    public EylemKarti(int simge, string baslik, string alt)
    {
        lock (_tumBasliklar) _tumBasliklar.Add(baslik);
        _simge = simge; _baslik = baslik; _alt = alt; Cursor = Cursors.Hand; Size = new Size(420, 88); Margin = new Padding(8, 10, 8, 6);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); TabStop = true; AccessibleName = baslik; AccessibleDescription = alt; AccessibleRole = AccessibleRole.PushButton;
        MouseEnter += (_, _) => { _ustunde = true; Invalidate(); }; MouseLeave += (_, _) => { _ustunde = false; Invalidate(); }; GotFocus += (_, _) => Invalidate(); LostFocus += (_, _) => Invalidate();
        KeyDown += (_, e) => { if (e.KeyCode is Keys.Enter or Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } };
    }
    protected override bool IsInputKey(Keys k) => k is Keys.Enter or Keys.Space || base.IsInputKey(k);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(TemaYonetici.Zemin(this));
        var r = new Rectangle(2, 2, Width - 5, Height - 5); using var yol = Yuvarlak(r, 18);
        Color zemin = TemaYonetici.C(_ustunde ? DesignTokens.Ozgun(TokenRol.SurfaceElevated) : DesignTokens.Ozgun(TokenRol.SurfaceCard)), cerceve = TemaYonetici.C(_ustunde || Focused ? DesignTokens.Ozgun(TokenRol.StatusInfo) : DesignTokens.Ozgun(TokenRol.BorderStrong));
        using (var b = new SolidBrush(zemin)) g.FillPath(b, yol); using (var p = new Pen(cerceve, _ustunde || Focused ? 2.5f : 1.5f)) g.DrawPath(p, yol);
        // Simge dairesi (solda) ve çizim.
        var daire = new Rectangle(16, (Height - 64) / 2, 64, 64); using (var b = new SolidBrush(TemaYonetici.C(DesignTokens.Ozgun(TokenRol.AccentPrimary)))) g.FillEllipse(b, daire);
        // Simge çizimleri 96 pikselik daire için yazıldı; küçülen daireye orantılı ölçeklenir.
        float olcek = 64f / 96f, dx = daire.X + daire.Width / 2f, dy = daire.Y + daire.Height / 2f; var eski = g.Transform; g.TranslateTransform(dx, dy); g.ScaleTransform(olcek, olcek); g.TranslateTransform(-dx, -dy);
        using (var kalem = new Pen(TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextPrimary)), 4f / olcek * 0.85f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) SimgeCiz(g, kalem, daire);
        g.Transform = eski;
        // Başlık: simgenin sağında, dikey ortalı (yatay geniş kartta tek satıra sığar).
        // Başlık tek satıra sığacak en büyük yazı boyutunu (13 → 11 punto) seçer; hiç sığmazsa 11 punto ve rahat satır aralığıyla iki satıra bölünür.
        var yazi = new Rectangle(90, 6, Math.Max(40, Width - 90 - 12), Height - 12);
        Font? f1 = null;
        for (float pt = 13f; pt >= 11f; pt -= 0.5f)
        {
            var aday = FontProvider.Gecici("Segoe UI", pt, FontStyle.Bold);
            // Üç kartın başlığı AYNI boyutta olsun: ölçü en uzun başlığa göre alınır.
            string olcu = _baslik; lock (_tumBasliklar) foreach (var s in _tumBasliklar) if (s.Length > olcu.Length) olcu = s;
            if (TextRenderer.MeasureText(g, olcu, aday, new Size(int.MaxValue, yazi.Height), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width <= yazi.Width) { f1 = aday; break; }
            aday.Dispose();
        }
        bool tekSatir = f1 != null; f1 ??= FontProvider.Al(FontRol.Subheading, FontStyle.Bold);
        try { TextRenderer.DrawText(g, _baslik, f1, yazi, TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextPrimary)), TextFormatFlags.Left | (tekSatir ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak) | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter); } finally { if (tekSatir) f1.Dispose(); }   // yalnızca sığdırma için ürettiğimiz geçici yazı tipi atılır; paylaşımlı rol yazı tipi ASLA
    }

    private void SimgeCiz(Graphics g, Pen k, Rectangle d)
    {
        float cx = d.X + d.Width / 2f, cy = d.Y + d.Height / 2f;
        switch (_simge)
        {
            case 0: // Kalkan + tik.
                { var nok = new PointF[] { new(cx - 24, cy - 24), new(cx, cy - 32), new(cx + 24, cy - 24), new(cx + 22, cy + 8), new(cx, cy + 32), new(cx - 22, cy + 8) }; g.DrawPolygon(k, nok); g.DrawLines(k, new PointF[] { new(cx - 11, cy), new(cx - 3, cy + 9), new(cx + 13, cy - 8) }); break; }
            case 1: // Küre (daire + boylam + enlem).
                { var r = new RectangleF(cx - 30, cy - 30, 60, 60); g.DrawEllipse(k, r); g.DrawEllipse(k, new RectangleF(cx - 13, cy - 30, 26, 60)); g.DrawLine(k, cx - 30, cy, cx + 30, cy); g.DrawArc(k, new RectangleF(cx - 26, cy - 15, 52, 30), 0, 180); break; }
            default: // Rapor kâğıdı + çubuk grafik.
                { g.DrawRectangle(k, cx - 24, cy - 32, 48, 64); g.DrawLine(k, cx - 14, cy + 20, cx - 14, cy + 6); g.DrawLine(k, cx, cy + 20, cx, cy - 6); g.DrawLine(k, cx + 14, cy + 20, cx + 14, cy - 18); break; }
        }
    }

    private static GraphicsPath Yuvarlak(Rectangle r, int yaricap)
    {
        var p = new GraphicsPath(); int d = yaricap * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

// Durum göstergesi (renkli nokta + iki satır yazı).
internal sealed class DurumGostergesi : Control, IThemeAware
{
    // Tema değişince yeniden çizilir (renkler çizim anında TemaYonetici'den okunur).
    public void TemaUygula() => Invalidate();
    private string _baslik = "", _deger = "—"; private Color _renk = DesignTokens.Ozgun(TokenRol.TextMuted);
    public DurumGostergesi() { Size = new Size(300, 56); Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    // Günceller: renk 0 = yeşil, 1 = sarı, 2 = kırmızı, 3 = gri (bilinmiyor).
    public void Ayarla(string baslik, string deger, int renk) { _baslik = baslik; _deger = deger; _renk = renk switch { 0 => DesignTokens.Ozgun(TokenRol.StatusSuccess), 1 => DesignTokens.Ozgun(TokenRol.StatusWarning), 2 => DesignTokens.Ozgun(TokenRol.StatusDanger), _ => DesignTokens.Ozgun(TokenRol.TextMuted) }; AccessibleName = baslik + ": " + deger; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(TemaYonetici.Zemin(this));
        using (var b = new SolidBrush(TemaYonetici.C(_renk))) g.FillEllipse(b, 6, Height / 2 - 9, 18, 18);
        using var f1 = FontProvider.Gecici("Segoe UI", 9f, FontStyle.Bold); using var f2 = FontProvider.Gecici("Segoe UI", 10.5f, FontStyle.Bold);
        TextRenderer.DrawText(g, _baslik, f1, new Rectangle(34, 6, Width - 40, 20), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _deger, f2, new Rectangle(34, 26, Width - 40, 24), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextPrimary)), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

// Dış web sitesi incelemesinin sonucu (Form1 üretir, panel gösterir).
internal sealed record WebSonucu(PasifGozlem Gozlem, List<PasifBulgu> Bulgular, List<Bulgu> TabloBulgulari);

// Kolay Görünüm paneli.
internal sealed class KolayModPaneli : Panel
{
    // Form1'in bağlayacağı kancalar. Döndürülen null = iptal/başarısız (panel ana ekrana döner).
    public Func<IProgress<string>, CancellationToken, Task<List<Bulgu>?>>? BilgisayariKontrolEt;
    public Func<IProgress<string>, CancellationToken, Task<WebSonucu?>>? SiteyiIncele;
    public Action? RaporAl; public Action? UzmanaGec; public Action<WebSonucu>? MektupAc; public Action<int>? GostergeTiklandi;

    // Ekranlar.
    private readonly Panel _ana = new(), _calisiyor = new(), _karne = new(); private readonly FlowLayoutPanel _kartlar = new();
    private readonly Label _karilama = new(), _karneBaslik = new(), _karneOzet = new(), _karneNot = new(); private readonly Button _geri = new EfektliButon(), _uzman = new EfektliButon(), _mektup = new EfektliButon();
    private readonly CanliTaramaKontrolu _tarama = new() { Dock = DockStyle.Fill }; private int _taramaAsama = 1, _taramaYuzde; private bool _taramaBilgisayar = true; private readonly System.Windows.Forms.Timer _yuzdeSaat = new() { Interval = 60 }; private DateTime _asamaBasi; private double[] _asamaSure = new double[4]; private double[] _asamaGercek = new double[4]; private readonly PuanHalkasi _halka = new(); private readonly FlowLayoutPanel _en3 = new();
    private readonly DurumGostergesi[] _gostergeler = { new(), new(), new(), new() }; private readonly FlowLayoutPanel _gosterge = new();
    private CancellationTokenSource? _iptal; private WebSonucu? _sonWeb; private Karne? _sonKarne;
    // Son karne (Kart 3 ve testler için).
    public Karne? SonKarne => _sonKarne;
    // Kartların altındaki canlı küre ve koruma kalkanı paneli.
    public KureWebPaneli Kure { get; } = new();

    public KolayModPaneli()
    {
        Dock = DockStyle.Fill; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Body); Visible = false;
        // --- Ana ekran: karşılama + 3 kart + göstergeler ---
        _ana.Dock = DockStyle.Fill; _karilama.Dock = DockStyle.Top; _karilama.Height = 78; _karilama.TextAlign = ContentAlignment.MiddleCenter; _karilama.Font = FontProvider.Al(FontRol.Display, FontStyle.Bold); _karilama.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _karilama.Text = "Ne yapmak istersiniz?\r\nAşağıdan birini seçin; gerisini program sizin için yapar.";
        _kartlar.Dock = DockStyle.Top; _kartlar.Height = 118; _kartlar.WrapContents = true; _kartlar.FlowDirection = FlowDirection.LeftToRight; _kartlar.AutoScroll = true; _kartlar.Padding = new Padding(0, 6, 0, 0);
        var k1 = new EylemKarti(0, "Bilgisayarımı ve Şirketimi Kontrol Et", "Bilgisayarınızdaki ve yerel ağınızdaki açık kapıları, virüs tuzaklarını ve zayıf ayarları tek tuşla kontrol eder.");
        var k2 = new EylemKarti(1, "Bir Web Sitesini İncele ve Sahibine Bildir", "İnternetteki bir web sitesine hiç zarar vermeden dışarıdan bakar, eksiklerini bulur ve site sahibine göndereceğiniz teşekkür mektubunu hazırlar.");
        var k3 = new EylemKarti(2, "Durum Karnesi ve Resmi Rapor Al", "Şirket yönetimine veya KVKK denetçisine sunabileceğiniz 1 sayfalık anlaşılır güvenlik karnesini indirir.");
        k1.Click += (_, _) => GuvenliCalistir.Baslat(() => CalistirAsync(true), "KolayModPaneli"); k2.Click += (_, _) => GuvenliCalistir.Baslat(() => CalistirAsync(false), "KolayModPaneli"); k3.Click += (_, _) => RaporAl?.Invoke();
        _kartlar.Controls.AddRange(new Control[] { k1, k2, k3 });
        _gosterge.Dock = DockStyle.Bottom; _gosterge.Height = 66; _gosterge.Padding = new Padding(10, 6, 0, 0); _gosterge.WrapContents = true;
        for (int i = 0; i < 4; i++) { int ix = i; _gostergeler[i].Click += (_, _) => GostergeTiklandi?.Invoke(ix); _gostergeler[i].Ayarla(new[] { KolayGostergeleri.SonTaramaBasligi, "IP Güvenlik Sızıntı Testi", "Bildirim Kanalı", "Koruma Hizmeti" }[i], "Henüz kontrol edilmedi", 3); _gosterge.Controls.Add(_gostergeler[i]); }
        // Kartlarla alt durum şeridi arasındaki boş alana canlı küre + koruma kalkanı (önce eklenen dolgu alanı kalanı alır).
        _ana.Controls.Add(Kure); _ana.Controls.Add(_kartlar); _ana.Controls.Add(_gosterge); _ana.Controls.Add(_karilama);
        // Alt gösterge şeridi: kutular genişliğe göre daralır (en az 150 px); sığmazsa alt satıra sarılır ve şerit yüksekliği buna göre büyür (sağdan kesilmez).
        _ana.Resize += (_, _) =>
        {
            int bos = Math.Max(1, _ana.ClientSize.Width - _gosterge.Padding.Horizontal), gw = Math.Clamp(bos / 4 - 10, 150, 300), sutun = Math.Max(1, bos / (gw + 10)), satir = (int)Math.Ceiling(4.0 / sutun);
            foreach (var g in _gostergeler) g.Width = gw; _gosterge.Height = 6 + satir * 62;
        };
        _ana.Resize += (_, _) => { int kw = Math.Clamp((_ana.ClientSize.Width - 3 * 16 - 24) / 3, 280, 480); foreach (var kk in _kartlar.Controls.OfType<EylemKarti>()) kk.Width = kw; _kartlar.Padding = new Padding(Math.Max(0, (_ana.ClientSize.Width - 3 * (kw + 16)) / 2), 6, 0, 0); Kure.Visible = _ana.ClientSize.Height - _kartlar.Height - _gosterge.Height - _karilama.Height >= 150; };
        // --- Çalışıyor ekranı: konuşan ilerleme ---
        _calisiyor.Dock = DockStyle.Fill; _calisiyor.Visible = false;
        // Tarama sırasında tam ekran canlı tarama bileşeni (Vazgeç düğmesi bileşenin içindedir); tema programdan gelir.
        _tarama.KoyuTema = TemaYonetici.Mod == TemaModu.Koyu; _tarama.VazgecTiklandi += (_, _) => _iptal?.Cancel();
        TemaYonetici.Degisti += TaramaTemasi;
        _calisiyor.Controls.Add(_tarama); _yuzdeSaat.Tick += (_, _) => YuzdeAdimi();
        // --- Karne ekranı ---
        _karne.Dock = DockStyle.Fill; _karne.Visible = false; _karne.AutoScroll = true;
        _halka.Size = new Size(190, 190); _karneBaslik.AutoSize = false; _karneBaslik.Font = FontProvider.Al(FontRol.Display, FontStyle.Bold); _karneBaslik.Size = new Size(900, 44); _karneOzet.AutoSize = false; _karneOzet.Size = new Size(900, 46); _karneOzet.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted); _karneOzet.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular);
        _en3.FlowDirection = FlowDirection.LeftToRight; _en3.WrapContents = true; _en3.AutoSize = true; _en3.MaximumSize = new Size(1200, 0);
        Stil(_geri, "Ana Ekrana Dön", 190); Stil(_uzman, "Ayrıntılar (Uzman Görünümü)", 270); _geri.Click += (_, _) => AnaGoster(); _uzman.Click += (_, _) => UzmanaGec?.Invoke();
        _mektup.Text = "Site Sahibine Gönderilecek Bilgilendirme Mektubunu Hazırla"; _mektup.Size = new Size(640, 54); _mektup.FlatStyle = FlatStyle.Flat; _mektup.FlatAppearance.BorderSize = 0; _mektup.BackColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _mektup.ForeColor = Color.White; _mektup.Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold); _mektup.Cursor = Cursors.Hand; _mektup.Visible = false; _mektup.Click += (_, _) => { if (_sonWeb != null) MektupAc?.Invoke(_sonWeb); };
        _karne.Controls.AddRange(new Control[] { _halka, _karneBaslik, _karneOzet, _en3, _geri, _uzman, _mektup }); _karne.Resize += (_, _) => KarneDuzenle();
        Controls.Add(_ana); Controls.Add(_calisiyor); Controls.Add(_karne);
    }

    // Ortak düğme stili.
    private static void Stil(Button b, string yazi, int genislik) { b.Text = yazi; b.Size = new Size(genislik, 42); b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; }

    // Göstergeleri günceller (Form1 çağırır).
    public void Gosterge(int sira, string baslik, string deger, int renk) { if (sira >= 0 && sira < 4) _gostergeler[sira].Ayarla(baslik, deger, renk); }

    // Ana ekrana (3 kart) döner.
    public void AnaGoster() { _yuzdeSaat.Stop(); _calisiyor.Visible = false; _karne.Visible = false; _ana.Visible = true; }

    // Bir eylemi başlatır: ilerleme ekranı → karne ekranı. Hata/iptalde ana ekrana döner.
    private async Task CalistirAsync(bool bilgisayar)
    {
        if (_calisiyor.Visible) return; if ((bilgisayar ? BilgisayariKontrolEt : SiteyiIncele) == null) return;
        _iptal = new CancellationTokenSource(); _ana.Visible = false; _karne.Visible = false; _calisiyor.Visible = true; TaramaBaslat(bilgisayar);
        var ilerleme = new Progress<string>(TaramaIlerlemesi);
        try
        {
            List<Bulgu>? liste; _sonWeb = null;
            if (bilgisayar) liste = await BilgisayariKontrolEt!(ilerleme, _iptal.Token);
            else { _sonWeb = await SiteyiIncele!(ilerleme, _iptal.Token); liste = _sonWeb?.TabloBulgulari; }
            if (liste == null) { AnaGoster(); return; }
            await TaramaTamamlaAsync();
            KarneGoster(KarneHesabi.Hesapla(liste), _sonWeb);
        }
        catch (OperationCanceledException) { AnaGoster(); }
        catch (Exception ex) { AnaGoster(); TemaliMesaj.Goster(this, "Bir sorun oldu: " + ex.Message, "İşlem tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // Karneyi çizer (Form1 de doğrudan çağırabilir).
    public void KarneGoster(Karne k, WebSonucu? web = null)
    {
        _yuzdeSaat.Stop(); _sonKarne = k; _sonWeb = web; _halka.Puan = k.Puan;
        Color renk = k.Renk == 0 ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : k.Renk == 1 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusDanger); _karneBaslik.ForeColor = renk; _karneBaslik.Text = k.Baslik; _karneOzet.Text = k.Ozet; _karneBaslik.AccessibleName = "Güvenlik karnesi: " + k.Baslik;
        foreach (Control c in _en3.Controls) c.Dispose(); _en3.Controls.Clear(); int n = 0;
        foreach (var m in k.EnOnemli3) _en3.Controls.Add(MaddeKarti(++n, m));
        _mektup.Visible = web != null; _ana.Visible = false; _calisiyor.Visible = false; _karne.Visible = true; KarneDuzenle();
    }

    // "En önemli 3 adım" kartı.
    private static Control MaddeKarti(int sira, KarneMaddesi m)
    {
        var p = new Panel { Size = new Size(380, 300), Margin = new Padding(8), BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard), Padding = new Padding(14) }; Color s = m.Onem == Onem.Yuksek ? DesignTokens.Ozgun(TokenRol.StatusDanger) : m.Onem == Onem.Orta ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusInfo);
        var serit = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = s };
        var baslik = new Label { Dock = DockStyle.Top, Height = 62, UseMnemonic = false, Padding = new Padding(10, 0, 0, 0), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), Text = $"{sira}. {m.Halk.Baslik}" };
        var govde = new Label { Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(10, 4, 0, 0), ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary), Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular), Text = $"Bu ne demek?\r\n{m.Halk.BuNeDemek}\r\n\r\nBana ne zararı olur?\r\n{m.Halk.Zarar}\r\n\r\nNe yapmalıyım?\r\n{m.Yapilacak}" };
        p.Controls.Add(govde); p.Controls.Add(baslik); p.Controls.Add(serit); return p;
    }

    // Çalışıyor ekranını ortalar.
    // Aşama alt açıklamaları ve telemetri satırları.
    private static readonly string[] BilgisayarAltAciklama =
    {
        "Güvenlik ayarları, kullanıcı hesapları ve açılışta çalışan programların denetimi",
        "Bilgisayarda dışarıdan erişilebilen riskli kapıların taranması",
        "Hizmetler, Windows güncelleme eksikleri, kurulu yazılımlar ve bilinen açıklar",
        "Bulgulardan puanlı karne üretilir, tablo doldurulur, “Kontrol tamamlandı” bildirimi çıkar"
    };
    private static readonly string[] BilgisayarTelemetri =
    {
        "[SALT OKUNUR] Güvenlik ayarları, hesaplar ve başlangıç programları inceleniyor...",
        "[AĞ DENETİMİ] Dışarıdan erişilebilen dinleme kapıları kontrol ediliyor...",
        "[YAZILIM DENETİMİ] Güncelleme eksikleri ve kurulu yazılımlar yerel veritabanıyla karşılaştırılıyor...",
        "[RAPORLAMA] Bulgular sınıflandırılıp güvenlik puanı hesaplanıyor..."
    };
    private static readonly string[] WebAltAciklama =
    {
        "Siteye zarar vermeden, sıradan bir ziyaretçi gibi yalnızca dışarıdan bakılıyor",
        "Güvenlik sertifikası ve e-posta kayıtları (DNS) okunuyor",
        "Site sahibinin iletişim dosyası (security.txt) aranıyor",
        "Bulgulardan anlaşılır özet ve bilgilendirme mektubu hazırlanıyor"
    };

    // ---- Gerçek zamanlı yüzde ----
    // Tarama motoru yalnızca aşama başında haber verir; aşamanın içi tek parça iş olduğundan yüzde şöyle üretilir:
    // her aşamanın yüzde aralığı, o aşamanın önceki taramalarda ölçülen gerçek süresiyle orantılıdır (ilk kez varsayılan süreler);
    // aşama içinde yüzde süreyle yavaşlayarak ilerler ama aşamanın sonuna ASLA gelmez (iş bitmeden aşama bitmiş görünmez);
    // motor sonraki aşamayı bildirince yüzde birer ikişer artarak oraya yetişir; iş bitince 100'e akıcı biçimde tamamlanır.
    // Ölçülen süreler %LocalAppData%\BulutSoft\tarama_sureleri.txt dosyasında tutulur; sonraki taramalar daha gerçekçi olur.
    private static readonly double[] VarsayilanBilgisayarSure = { 3, 2, 20, 1.5 };
    private static readonly double[] VarsayilanWebSure = { 4, 5, 4, 2 };
    private static string SureDosyasi(bool bilgisayar) => Path.Combine(BrandConfig.YerelKlasor, bilgisayar ? "tarama_sureleri.txt" : "tarama_sureleri_web.txt");

    // Kayıtlı süreleri okur (yoksa ya da bozuksa varsayılan).
    private static double[] SureleriOku(bool bilgisayar)
    {
        var v = (double[])(bilgisayar ? VarsayilanBilgisayarSure : VarsayilanWebSure).Clone();
        try
        {
            var d = File.ReadAllText(SureDosyasi(bilgisayar)).Split(';');
            if (d.Length == 4) for (int i = 0; i < 4; i++) if (double.TryParse(d[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) && x is >= 0.3 and <= 900) v[i] = x;
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "KolayModPaneli.SureleriOku"); }
        return v;
    }

    // Aşama sınırları (yüzde): 1 ile 99 arası, aşama sürelerine orantılı.
    private double[] Sinirlar()
    {
        double top = _asamaSure.Sum(), t = 1; var b = new double[5]; b[0] = 1;
        for (int i = 0; i < 4; i++) { t += 98.0 * _asamaSure[i] / top; b[i + 1] = t; }
        return b;
    }

    // Tarama bileşenini ilk duruma getirir (bilgisayar kontrolü ya da dış site incelemesi) ve yüzde saatini %1'den başlatır.
    private void TaramaBaslat(bool bilgisayar)
    {
        _taramaBilgisayar = bilgisayar; _taramaAsama = 1; _taramaYuzde = 1; _asamaSure = SureleriOku(bilgisayar); _asamaGercek = new double[4]; _asamaBasi = DateTime.UtcNow;
        _tarama.AsamaEtiketleri = bilgisayar ? new[] { "1/4 Sistem", "2/4 Ağ & Kapılar", "3/4 Güncellemeler", "4/4 Güvenlik Karnesi" } : new[] { "1/4 Bağlantı", "2/4 Sertifika ve DNS", "3/4 İletişim Dosyası", "4/4 Özet ve Mektup" };
        _tarama.DurumGuncelle(1, 1, "Başlıyor...", (bilgisayar ? BilgisayarAltAciklama : WebAltAciklama)[0], bilgisayar ? BilgisayarTelemetri[0] : "[DIŞ İNCELEME] Hazırlanıyor...");
        _yuzdeSaat.Start();
    }

    // Saat vuruşu: hedef yüzdeyi hesaplar, gösterilen yüzdeyi azar azar ilerletir (asla geri gitmez, sıçramaz).
    private void YuzdeAdimi()
    {
        if (!_calisiyor.Visible) { _yuzdeSaat.Stop(); return; }
        var b = Sinirlar(); int a = _taramaAsama - 1; double t = (DateTime.UtcNow - _asamaBasi).TotalSeconds;
        double hedef = b[a] + (b[a + 1] - b[a] - 1) * (1 - Math.Exp(-t / Math.Max(0.3, _asamaSure[a])));
        int h = Math.Clamp((int)Math.Floor(hedef), 1, 99); int fark = h - _taramaYuzde;
        if (fark <= 0) return;
        _taramaYuzde += fark > 12 ? 3 : fark > 5 ? 2 : 1;
        _tarama.DurumGuncelle(Math.Min(_taramaYuzde, 99), _taramaAsama, "");
    }

    // İş bitti: yüzdeyi akıcı biçimde 100'e tamamlar, süreleri kaydeder.
    private async Task TaramaTamamlaAsync()
    {
        _yuzdeSaat.Stop();
        _asamaGercek[Math.Clamp(_taramaAsama - 1, 0, 3)] = (DateTime.UtcNow - _asamaBasi).TotalSeconds; SureleriKaydet();
        _tarama.DurumGuncelle(_taramaYuzde, 4, "Tamamlanıyor...", null, "[TAMAMLANDI] Sonuçlar hazırlandı.");
        while (_taramaYuzde < 100) { _taramaYuzde = Math.Min(100, _taramaYuzde + Math.Max(1, (100 - _taramaYuzde) / 12)); _tarama.DurumGuncelle(_taramaYuzde, 4, ""); await Task.Delay(35); }
        await Task.Delay(500);
    }

    // Ölçülen aşama sürelerini eski değerlerle karıştırarak (yarı yarıya) saklar.
    private void SureleriKaydet()
    {
        try
        {
            var yeni = new double[4]; for (int i = 0; i < 4; i++) yeni[i] = _asamaGercek[i] > 0.05 ? Math.Clamp(0.5 * _asamaSure[i] + 0.5 * _asamaGercek[i], 0.3, 900) : _asamaSure[i];
            string d = SureDosyasi(_taramaBilgisayar); Directory.CreateDirectory(Path.GetDirectoryName(d)!);
            Depo.Yaz(d, string.Join(';', yeni.Select(x => x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
        }
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "KolayModPaneli.SureleriKaydet"); }
    }

    // Tarama akışının "k/4: ..." mesajlarını bileşene aktarır; aşama dışı mesajlar (örn. derin kontrol) telemetri satırında gösterilir.
    private void TaramaIlerlemesi(string m)
    {
        var kes = m.IndexOf('/');
        if (kes > 0 && int.TryParse(m[..kes], out int a) && a is >= 1 and <= 4)
        {
            if (a > _taramaAsama) { _asamaGercek[_taramaAsama - 1] = (DateTime.UtcNow - _asamaBasi).TotalSeconds; _asamaBasi = DateTime.UtcNow; _taramaAsama = a; }
            _tarama.DurumGuncelle(_taramaYuzde, a, m, (_taramaBilgisayar ? BilgisayarAltAciklama : WebAltAciklama)[a - 1], _taramaBilgisayar ? BilgisayarTelemetri[a - 1] : "[DIŞ İNCELEME] " + m[(kes + 1)..].TrimStart(':', ' '));
        }
        else _tarama.DurumGuncelle(_taramaYuzde, _taramaAsama, "", null, m);
    }

    // Program teması değişince tarama bileşeni de güncellenir.
    private void TaramaTemasi() { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(() => { if (!IsDisposed) _tarama.KoyuTema = TemaYonetici.Mod == TemaModu.Koyu; })); }
    protected override void Dispose(bool disposing) { if (disposing) { TemaYonetici.Degisti -= TaramaTemasi; _yuzdeSaat.Dispose(); } base.Dispose(disposing); }

    // Karne ekranını ortalar ve sırayla dizer.
    private void KarneDuzenle()
    {
        int w = _karne.ClientSize.Width; _halka.Location = new Point((w - _halka.Width) / 2, 12); _karneBaslik.Width = Math.Min(w - 30, 1000); _karneBaslik.TextAlign = ContentAlignment.MiddleCenter; _karneBaslik.Location = new Point((w - _karneBaslik.Width) / 2, _halka.Bottom + 6);
        _karneOzet.Width = _karneBaslik.Width; _karneOzet.TextAlign = ContentAlignment.TopCenter; _karneOzet.Location = new Point(_karneBaslik.Left, _karneBaslik.Bottom + 2);
        _en3.MaximumSize = new Size(Math.Max(400, w - 30), 0); _en3.Location = new Point(Math.Max(10, (w - Math.Min(_en3.PreferredSize.Width, w - 20)) / 2), _karneOzet.Bottom + 8);
        int y = _en3.Bottom + 12; _mektup.Location = new Point((w - _mektup.Width) / 2, y); if (_mektup.Visible) y = _mektup.Bottom + 12;
        int toplam = _geri.Width + _uzman.Width + 14; _geri.Location = new Point((w - toplam) / 2, y); _uzman.Location = new Point(_geri.Right + 14, y);
    }
}

// Site sahibine mektup penceresi: sol "Kime?", sağ "Gönderilecek nazik mesaj"; Kopyala / PDF.
internal sealed class MektupFormu : TemaliForm
{
    private readonly TextBox _mesaj = new(); private readonly PasifGozlem _g; private readonly List<PasifBulgu> _b;

    public MektupFormu(PasifGozlem g, List<PasifBulgu> bulgular, string gonderenAd, string gonderenIletisim)
    {
        _g = g; _b = bulgular; Color zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), soluk = DesignTokens.Ozgun(TokenRol.TextMuted);
        Text = "Site Sahibine Bilgilendirme Mektubu"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1080, 680); MinimumSize = new Size(860, 520); BackColor = zemin; ForeColor = yazi; Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular); Icon = Logo.Ikon;
        // Sol: kime.
        var sol = new Panel { Dock = DockStyle.Left, Width = 330, Padding = new Padding(16), BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar) };
        var alicilar = EtikBildirim.Alicilar(g); var l1 = new Label { Text = "Kime Gönderilecek?", Dock = DockStyle.Top, Height = 34, Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), UseMnemonic = false };
        var l2 = new Label { Dock = DockStyle.Top, Height = 150, ForeColor = alicilar.Count > 0 ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusWarning), Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold), UseMnemonic = false, Text = alicilar.Count > 0 ? string.Join("\r\n", alicilar) : "Sitede güvenlik iletişim adresi (security.txt) bulunamadı.\r\n\r\nKurumun resmi iletişim kanalını (web sitesindeki \"İletişim\" sayfası, bilgi işlem veya KVKK sorumlusu) kullanın." };
        var l3 = new Label { Dock = DockStyle.Top, Height = 120, ForeColor = soluk, UseMnemonic = false, Text = "Adres, sitenin kendi yayınladığı güvenlik iletişim dosyasından (security.txt) alındı. Mektubu e-posta programınıza yapıştırıp gönderin; program kendisi hiçbir şey göndermez." };
        var adr = new EfektliButon { Text = "Adresi Kopyala", Dock = DockStyle.Top, Height = 40, Enabled = alicilar.Count > 0 }; Stil(adr, DesignTokens.Ozgun(TokenRol.SurfaceElevated)); adr.Click += (_, _) => Kopyala(string.Join("; ", alicilar.Select(a => a.Replace("mailto:", ""))), "Adres kopyalandı.");
        sol.Controls.Add(adr); sol.Controls.Add(l3); sol.Controls.Add(l2); sol.Controls.Add(l1);
        // Sağ: mesaj.
        var sag = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) }; var l4 = new Label { Text = "Gönderilecek Nazik Mesaj", Dock = DockStyle.Top, Height = 34, Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), UseMnemonic = false };
        _mesaj.Multiline = true; _mesaj.ScrollBars = ScrollBars.Vertical; _mesaj.Dock = DockStyle.Fill; _mesaj.BackColor = kutu; _mesaj.ForeColor = yazi; _mesaj.BorderStyle = BorderStyle.FixedSingle; _mesaj.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular); _mesaj.Text = MektupMetni.Uret(g, bulgular, gonderenAd, gonderenIletisim); _mesaj.SelectionStart = 0; _mesaj.SelectionLength = 0;
        var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(0, 10, 0, 0) };
        var kop = new EfektliButon { Text = "Mesajı Kopyala", Size = new Size(200, 44) }; var pdf = new EfektliButon { Text = "PDF Olarak İndir", Size = new Size(200, 44) }; Stil(kop, DesignTokens.Ozgun(TokenRol.StatusSuccess)); Stil(pdf, DesignTokens.Ozgun(TokenRol.AccentPrimary));
        kop.Click += (_, _) => Kopyala(_mesaj.Text, "Mesaj kopyalandı. E-posta programınıza yapıştırabilirsiniz."); pdf.Click += (_, _) => PdfYaz();
        var not = new Label { AutoSize = true, ForeColor = soluk, Margin = new Padding(14, 12, 0, 0), UseMnemonic = false, Text = "Köşeli parantezli yerleri (ör. [Adınız]) kendinize göre düzenleyin." };
        alt.Controls.AddRange(new Control[] { kop, pdf, not }); sag.Controls.Add(_mesaj); sag.Controls.Add(alt); sag.Controls.Add(l4);
        Controls.Add(sag); Controls.Add(sol);
    }
    private static void Stil(Button b, Color renk) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = renk; b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 10, 0); }
    private void Kopyala(string metin, string bilgi) { try { Clipboard.SetText(metin); Bildirim.Goster("Kopyalandı", bilgi, BildirimTuru.Basari); } catch (Exception ex) { TemaliMesaj.Goster(this, "Panoya kopyalanamadı: " + ex.Message, "Kopyala", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    private void PdfYaz()
    {
        using var d = new SaveFileDialog { Title = "Mektubu PDF olarak kaydet", Filter = "PDF (*.pdf)|*.pdf", FileName = $"Bilgilendirme_Mektubu_{_g.Host}_{DateTime.Now:yyyyMMdd}.pdf" }; if (d.ShowDialog(this) != DialogResult.OK) return;
        try { EtikBildirim.PdfYaz(d.FileName, _g, _b); Bildirim.Goster("PDF kaydedildi", Path.GetFileName(d.FileName), BildirimTuru.Basari); } catch (Exception ex) { TemaliMesaj.Goster(this, "PDF yazılamadı: " + ex.Message, "PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
