// Bu dosya programın AÇILIŞ (loading) ekranını, açılıştaki gerçek hazırlık adımlarını ve uygulama simgesini (Logo) içerir.
// Açılış ekranı en az 5 saniye görünür. Adımlar süs değildir: tehdit veritabanı gerçekten yüklenir, kimlik dosyası ve mühürler gerçekten denetlenir.
using System.Diagnostics;
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// Açılış hazırlığının topladığı bilgiler.
internal sealed class AcilisBilgisi
{
    // Kimlik dosyasının durumu (Program bunu kullanır).
    public KimlikDurumu Kimlik { get; set; }
    // Mühürlü dosyalardan değişmiş/silinmiş olanların sayısı.
    public int BozukMuhur { get; set; }
    // OpenSSH istemcisi var mı?
    public bool SshVar { get; set; }
    // Kayıtlı sensör ve tarama geçmişi sayıları.
    public int Sensor { get; set; } public int Gecmis { get; set; }
}

// Açılış adımlarını çalıştıran yardımcı.
internal static class Acilis
{
    // Her adım: ekranda görünecek kısa ad ve işi.
    public static List<(string Ad, Action<AcilisBilgisi> Is)> Adimlar() => new()
    {
        ("Tehdit Veritabanı", b => { LabDenetim.TehditVeritabani.Yukle(); }),
        ("Kimlik ve Yetki Kayıtları", b => b.Kimlik = KimlikDeposu.Durum()),
        ("Ayar Dosyası Muhafızı", b => { try { b.BozukMuhur = Muhur.Dogrula().Count(x => x.Durum is "DEĞİŞMİŞ" or "SİLİNMİŞ"); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Acilis.Adimlar"); } }),
        ("OpenSSH İstemcisi", b => b.SshVar = File.Exists(@"C:\Windows\System32\OpenSSH\ssh.exe")),
        ("Sensörler ve Tarama Geçmişi", b => { try { b.Sensor = SensorDeposu.Yukle().Count; b.Gecmis = GecmisDeposu.Listele().Count; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Acilis.Adimlar"); } }),
        ("Kali Sensör Denetimi", b => { if (!Bildirim.Kapali) KaliDurumYoneticisi.ArkaPlandaBaslat(); }),
        ("Kalkanlar ve Arayüz", b => { })
    };

    // Ekransız (splash olmadan) hazırlık: kullanıcı değiştirirken kullanılır.
    public static AcilisBilgisi Hazirla() { var b = new AcilisBilgisi(); foreach (var a in Adimlar()) a.Is(b); return b; }
}

// Uygulama simgesi (koyu daire + sarı halka + şimşek): çizim ve pencere simgesi.
internal static class Logo
{
    // Simgeyi verilen dikdörtgene çizer. parlama: 0..1 halka parlaklığı; ışıma: dış parlama halkaları çizilsin mi?
    public static void Ciz(Graphics g, Rectangle r, float parlama = 0.5f, bool isima = true)
    {
        // Pürüzsüz çizim.
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        // Dış parlama halkaları (şimşek sarısı, şeffaf).
        if (isima) for (int i = 4; i >= 1; i--) { int pay = (int)(i * r.Width * 0.035f); using var k = new Pen(DesignTokens.Saydam(DesignTokens.Ozgun(TokenRol.AccentPrimary), Math.Min(255, (int)(10 + 30 * parlama) / i)), 2f); g.DrawEllipse(k, r.X - pay, r.Y - pay, r.Width + 2 * pay, r.Height + 2 * pay); }
        // Koyu daire zemin.
        using (var f = new SolidBrush(DesignTokens.Ozgun(TokenRol.SurfaceApp))) g.FillEllipse(f, r);
        // Sarı çerçeve.
        using (var k = new Pen(DesignTokens.Ozgun(TokenRol.AccentPrimary), Math.Max(1.5f, r.Width / 16f))) g.DrawEllipse(k, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        // Şimşek işareti.
        using var yazi = FontProvider.Gecici("Segoe UI Symbol", r.Width * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        // Ortala.
        TextRenderer.DrawText(g, "⚡", yazi, new Rectangle(r.X, r.Y + (int)(r.Height * 0.04f), r.Width, r.Height), DesignTokens.Ozgun(TokenRol.AccentPrimary), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // Pencere/görev çubuğu simgesi için ÖZEL tasarım: küçük boyutta (16 px) bile okunsun diye sarı dolu daire + koyu şimşek.
    // (Koyu zeminli simgeler küçükken PowerShell/terminal simgelerine benziyordu; bu tasarım belirgin biçimde farklıdır.)
    public static void CizIkon(Graphics g, Rectangle r)
    {
        // Pürüzsüz çizim.
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        // Sarı-turuncu dolu daire.
        using (var f = new LinearGradientBrush(r, DesignTokens.Ozgun(TokenRol.StatusWarning), DesignTokens.Ozgun(TokenRol.StatusWarning), 90f)) g.FillEllipse(f, r);
        // İnce koyu dış çizgi (açık zeminlerde de seçilsin).
        using (var k = new Pen(DesignTokens.Saydam(DesignTokens.Ozgun(TokenRol.StatusWarningBg), 150), Math.Max(1f, r.Width / 22f))) g.DrawEllipse(k, r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
        // Koyu şimşek.
        using var yazi = FontProvider.Gecici("Segoe UI Symbol", r.Width * 0.66f, FontStyle.Bold, GraphicsUnit.Pixel);
        // Ortala.
        TextRenderer.DrawText(g, "⚡", yazi, new Rectangle(r.X, r.Y + (int)(r.Height * 0.03f), r.Width, r.Height), DesignTokens.Ozgun(TokenRol.SurfaceCard), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    // Uygulama simgesi: exe içine GÖMÜLÜ app.ico (çok boyutlu, net); okunamazsa çizilen simge. Açılış, giriş ve tüm pencereler bunu kullanır.
    private static Icon? _ikon;
    public static Icon Ikon => _ikon ??= Yukle();

    // Simgeyi yükler.
    private static Icon Yukle()
    {
        // Gömülü çok boyutlu simge dosyası (ProcessPath'a güvenilmez: PowerShell veya dotnet altında çalışırsa onların simgesini verirdi).
        try { using var s = typeof(Logo).Assembly.GetManifestResourceStream("app.ico"); if (s != null) return new Icon(s); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "Acilis.Yukle"); }
        // Yedek: 32x32 çizim.
        using var bmp = new Bitmap(32, 32); using (var g = Graphics.FromImage(bmp)) CizIkon(g, new Rectangle(1, 1, 29, 29));
        // Simgeye çevir.
        return Icon.FromHandle(bmp.GetHicon());
    }
}

// Açılış (loading) penceresi: çerçevesiz, yuvarlak köşeli, saf GDI+; giriş ekranıyla aynı boyut ve konumda olduğundan geçiş sıçramasızdır.
internal sealed class AcilisFormu : KarsilamaFormu
{
    // En az görünme süresi (ms).
    public const int AzamiSure = 5000;
    // Her adımın altında görünen kısa açıklama (adımlarla aynı sırada).
    private static readonly string[] Ipuclari =
    {
        "Yerel zafiyet ve kural veritabanı doğrulanıyor...",
        "Şifreli kimlik bütünlüğü kontrol ediliyor...",
        "Atomik yedekleme ve yapılandırma okunuyor...",
        "Uzak sensör haberleşme bileşeni denetleniyor...",
        "Önceki denetim kayıtları indeksleniyor...",
        "İsteğe bağlı uzman sensör durumu kontrol ediliyor...",
        "Tema motoru ve güvenlik arayüzü hazırlanıyor..."
    };

    // Süre ölçer ve çizim sayacı.
    private readonly Stopwatch _saat = Stopwatch.StartNew(); private readonly System.Windows.Forms.Timer _zaman = new() { Interval = 30 };
    // Adımlar arka planda yürür; ekran bu alanları okur.
    private volatile bool _bitti; private volatile int _yapilan; private readonly List<(string Ad, Action<AcilisBilgisi> Is)> _adimlar = Acilis.Adimlar(); private volatile string _ozet = "";
    private double _goster; private readonly Sag _sag;
    // Hazırlık sonucu.
    public AcilisBilgisi Bilgi { get; } = new();
    // Kullanıcı açılışta pencereyi kapattı (program kapanır).
    public bool Iptal { get; private set; }

    // Pencereyi kurar.
    public AcilisFormu() : base(510)
    {
        Text = UrunBilgisi.Ad; TopMost = true; Marka.OzellikSoluk = true;
        _sag = new Sag(this) { CikisIstendi = () => { Iptal = true; _zaman.Stop(); DialogResult = DialogResult.Cancel; Close(); } }; Yerlestir(_sag);
        // Her karede yüzdeyi ilerlet ve yeniden çiz; süre dolunca ve adımlar bitince kapat.
        _zaman.Tick += (_, _) =>
        {
            if (_saat.ElapsedMilliseconds >= AzamiSure && _bitti && _goster >= 99.5) { _zaman.Stop(); DialogResult = DialogResult.OK; Close(); return; }
            _goster = Math.Max(_goster, _goster + (Hedef() - _goster) * 0.22); _sag.Invalidate();
        };
        // Başlayınca adımları arka planda çalıştır.
        Shown += (_, _) => { _zaman.Start(); Task.Run(Calistir); };
        // Kaynakları bırak.
        FormClosed += (_, _) => _zaman.Dispose();
    }

    // Hedef yüzde: biten adımlar + sürmekte olan adımın zamana göre kısmı (asla %100'ü iş bitmeden aşmaz).
    private double Hedef()
    {
        if (_bitti) return 100; int n = _adimlar.Count, y = _yapilan; double ilk = AzamiSure * 0.9 * y / n, son = AzamiSure * 0.9 * (y + 1) / n; double f = Math.Clamp((_saat.ElapsedMilliseconds - ilk) / (son - ilk), 0, 0.92);
        return Math.Min(99, 100.0 * (y + f) / n);
    }

    // Adımları sırayla çalıştırır (süre 5 sn'ye yayılır ki her adım okunabilsin).
    private void Calistir()
    {
        // Her adım.
        foreach (var (_, is_) in _adimlar)
        {
            // İşi çalıştır; hata olursa açılış durmasın.
            try { is_(Bilgi); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "Acilis.Calistir"); }
            // Bu adımın bitiş hedefi: toplam sürenin %90'ına yayılır.
            long hedef = (long)(AzamiSure * 0.9 * (_yapilan + 1) / _adimlar.Count); long kalan = hedef - _saat.ElapsedMilliseconds; if (kalan > 0) Thread.Sleep((int)kalan);
            // Sayaç.
            _yapilan++;
        }
        // Özet: yalnızca değişmiş ayar dosyası varsa uyarı olarak gösterilir.
        _ozet = Bilgi.BozukMuhur > 0 ? $"UYARI: {Bilgi.BozukMuhur} ayar dosyası değişmiş" : "Tüm güvenlik bileşenleri hazır. Giriş ekranına geçiliyor...";
        // Bitti.
        _bitti = true;
    }

    // Sağ panel: "Sistem Hazırlanıyor", yüzde rozeti, 7 bileşen satırı, parıltılı ilerleme çubuğu ve sayaç.
    private sealed class Sag : SagPanelTemel
    {
        private readonly AcilisFormu _f;
        public Sag(AcilisFormu f) { _f = f; Width = Genislik - MarkaGenisligi; }

        protected override void Ciz(Graphics g, KTema t)
        {
            int w = IcGenislik; long ms = _f._saat.ElapsedMilliseconds; int yuzde = _f._bitti && _f._goster >= 99.5 ? 100 : Math.Min(_f._bitti ? 100 : 99, (int)Math.Round(_f._goster)); bool bitti = _f._bitti; int yapilan = bitti ? _f._adimlar.Count : _f._yapilan;
            // Başlık ve açıklama.
            TextRenderer.DrawText(g, "Sistem Hazırlanıyor", FBaslik, new Rectangle(Kenar, 66, w - 90, 30), t.Metin, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            KCizim.Yaz(g, "Güvenlik bileşenleri doğrulanıyor, lütfen bekleyin.", FAciklama, new Rectangle(Kenar, 98, w - 90, 18), t.Soluk);
            // Yüzde rozeti (sağ üst; üst şeritin altında).
            var rozet = new Rectangle(Kenar + w - 66, 70, 66, 28); Color rc = bitti && yuzde >= 100 ? t.Basari : t.Ana; KCizim.Doldur(g, rozet, 9, KTema.A(rc, 31)); KCizim.Cerceve(g, rozet, 9, KTema.A(rc, 72));
            TextRenderer.DrawText(g, "%" + yuzde, FMono, rozet, rc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            // Bileşen satırları.
            int y = 128; int n = _f._adimlar.Count;
            for (int i = 0; i < n; i++, y += 41)
            {
                bool biten = i < yapilan, surer = !bitti && i == yapilan; var satir = new Rectangle(Kenar, y, w, 35);
                if (surer) { KCizim.Doldur(g, satir, 10, KTema.A(t.Ana, 31)); KCizim.Cerceve(g, satir, 10, KTema.A(t.Ana, 82)); }
                var im = new PointF(Kenar + 21, y + 17.5f);
                if (biten) { using var b = new SolidBrush(KTema.A(t.Basari, 36)); g.FillEllipse(b, im.X - 9, im.Y - 9, 18, 18); using var k = new Pen(t.Basari, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; g.DrawLines(k, new[] { new PointF(im.X - 4, im.Y), new PointF(im.X - 1, im.Y + 3), new PointF(im.X + 4, im.Y - 3) }); }
                else if (surer) { using var k = new Pen(KTema.A(t.Ana, 72), 2f); g.DrawEllipse(k, im.X - 7, im.Y - 7, 14, 14); using var k2 = new Pen(t.Ana, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawArc(k2, im.X - 7, im.Y - 7, 14, 14, ms / 700f * 360f % 360f - 90, 90); }
                else { using var b = new SolidBrush(t.Kenar); g.FillEllipse(b, im.X - 3, im.Y - 3, 6, 6); }
                TextRenderer.DrawText(g, _f._adimlar[i].Ad, biten || surer ? FSatirK : FSatir, new Rectangle(Kenar + 42, y, w - 160, 35), biten || surer ? t.Metin : t.Soluk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                // Durum yazısı / "Hazır" rozeti.
                if (biten) { var pil = new Rectangle(Kenar + w - 60, y + 8, 50, 20); KCizim.Doldur(g, pil, 10, KTema.A(t.Basari, 36)); TextRenderer.DrawText(g, "Hazır", FKucuk, pil, t.Basari, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding); }
                else TextRenderer.DrawText(g, surer ? "Yükleniyor..." : "Bekliyor", FKucuk, new Rectangle(Kenar + w - 130, y, 120, 35), surer ? t.Ana : t.Soluk, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            // Parıltılı ilerleme çubuğu.
            var iz = new Rectangle(Kenar, 436, w, 8); KCizim.Doldur(g, iz, 4, t.Yuksek); KCizim.Cerceve(g, iz, 4, t.Kenar);
            int dolu = (int)(iz.Width * yuzde / 100.0);
            if (dolu >= 6)
            {
                var d = new Rectangle(iz.X, iz.Y, dolu, iz.Height); using var yol = KCizim.Yol(d, 4);
                using (var cb = new LinearGradientBrush(new Rectangle(iz.X, iz.Y, Math.Max(iz.Width, 2), iz.Height), t.Ana, t.Basari, 0f) { InterpolationColors = new ColorBlend { Colors = new[] { t.Ana, t.Cam, t.Basari }, Positions = new[] { 0f, 0.5f, 1f } } }) g.FillPath(cb, yol);
                var eski = g.Clip; g.SetClip(yol, CombineMode.Intersect); float kx = d.X - 90 + (ms % 1400) / 1400f * (d.Width + 180); using (var pb = new LinearGradientBrush(new RectangleF(kx - 45, d.Y, 90, d.Height), Color.Transparent, Color.Transparent, 0f) { InterpolationColors = new ColorBlend { Colors = new[] { Color.Transparent, DesignTokens.Saydam(Color.White, 96), Color.Transparent }, Positions = new[] { 0f, 0.5f, 1f } } }) g.FillRectangle(pb, kx - 45, d.Y, 90, d.Height); g.Clip = eski;
            }
            // İpucu ve sayaç.
            string ipucu = bitti ? _f._ozet : Ipuclari[Math.Min(_f._yapilan, Ipuclari.Length - 1)]; bool uyari = bitti && _f.Bilgi.BozukMuhur > 0;
            TextRenderer.DrawText(g, ipucu, FNot, new Rectangle(Kenar, 452, w - 70, 18), uyari ? t.Kehribar : t.Soluk, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, $"{yapilan}/{n}", FMono, new Rectangle(Kenar + w - 60, 450, 60, 20), t.Metin2, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
