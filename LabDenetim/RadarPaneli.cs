// Bu dosya "Canlı Ağ ve Port Radarı"nı GDI+ ile çizer: ortada hedef makine, halkalarda açık kapılar/servisler (neon düğümler).
// Radar kolu döner, yeni bulunan kapılar büyüyerek belirir, düğüme tıklayınca ilgili sonucun ayrıntısına gidilir.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

// Radardaki bir düğümün seviyesi (renk belirler).
internal enum RadarSeviye { Guvenli, Uyari, Kritik }

// Radardaki bir düğüm: kapı numarası, servis adı, seviye.
internal sealed record RadarDugum(int Port, string Servis, RadarSeviye Seviye);

// Radar çizim kutusu.
internal sealed class RadarPaneli : Control, IThemeAware
{
    // Tema değişince yeniden çizilir (renkler çizim anında TemaYonetici'den okunur).
    public void TemaUygula() => Invalidate();
    // Bir düğüme tıklanınca kapı numarasını haber verir.
    public event Action<int>? DugumTiklandi;

    // Gösterilen düğümler.
    private List<RadarDugum> _dugumler = new();
    // Merkezdeki hedefin adı/IP'si.
    private string _hedef = "127.0.0.1";
    // Şu an tarama sürüyor mu?
    private bool _taramada;
    // Radar kolunun açısı (derece, yukarıdan saat yönünde).
    private float _kol;
    // Merkezin nabız (büyüyüp küçülme) fazı.
    private float _nabiz;
    // Her düğümün belirme ilerlemesi (0 → 1).
    private readonly Dictionary<int, float> _dogus = new();
    // Her düğümün "radar kolu geçti" parıltısı (1 → 0).
    private readonly Dictionary<int, float> _parilti = new();
    // Düğümlerin ekrandaki yerleri (çizimden çizime saklanır; tıklama için).
    private readonly List<(RadarDugum D, PointF Yer, float Aci)> _yerler = new();
    // Fare şu an hangi düğmenin üstünde? (-1 = hiçbiri)
    private int _ustte = -1;
    // Animasyonu sürükleyen zamanlayıcı (saniyede ~30 kare).
    private readonly GorunurZamanlayici _zaman = new() { Interval = 33 };   // görsel: sayfa gizliyken/simge durumunda durur
    // Yazı tipleri (bir kez üretilip tekrar kullanılır).
    private readonly Font _fBaslik = new("Segoe UI", 11f, FontStyle.Bold), _fKucuk = new("Segoe UI", 8.5f), _fKalin = new("Segoe UI", 8.5f, FontStyle.Bold), _fIp = new("Consolas", 10f, FontStyle.Bold);

    // Radarı kurar.
    public RadarPaneli()
    {
        // Titremesiz, kendi çizdiğimiz kutu.
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // Koyu zemin.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar);
        // Her kare: kolu döndür, nabzı ilerlet, belirmeleri ve parıltıları güncelle.
        _zaman.Tick += (_, _) =>
        {
            // Radar kolu saniyede yaklaşık 70 derece döner.
            _kol = (_kol + (_taramada ? 1.6f : 0.8f) * 2f) % 360f;
            // Nabız fazı.
            _nabiz += 0.09f;
            // Kayan yazı soldan sağa ilerler.
            _kayma += 1.5f;
            // Belirme ilerlemesi: 0'dan 1'e.
            foreach (var k in _dogus.Keys.ToList()) _dogus[k] = Math.Min(1f, _dogus[k] + 0.055f);
            // Parıltı yavaşça söner.
            foreach (var k in _parilti.Keys.ToList()) _parilti[k] *= 0.93f;
            // Radar kolu bir düğümün üstünden geçtiyse parlat.
            foreach (var y in _yerler)
            {
                // Kol ile düğüm arasındaki açı farkı.
                float fark = (_kol - y.Aci + 360f) % 360f;
                // Kol az önce geçtiyse parıltıyı tam yap.
                if (fark < 8f) _parilti[y.D.Port] = 1f;
            }
            // Yeniden çiz.
            Invalidate();
        };
        // Görünür olunca animasyonu başlat, gizlenince durdur (işlemciyi yormasın).
        _zaman.Sahip = this; _zaman.Start();   // görünürlük/simge durumu kararını zamanlayıcı kendisi verir
    }

    // Düğümleri, hedefi ve tarama durumunu günceller.
    public void Guncelle(string hedef, IReadOnlyList<RadarDugum> dugumler, bool taramada)
    {
        // Yeni değerleri sakla.
        _hedef = string.IsNullOrWhiteSpace(hedef) ? "—" : hedef; _taramada = taramada; _dugumler = dugumler.ToList();
        // Yeni düğümler için belirme animasyonunu başlat.
        foreach (var d in _dugumler) if (!_dogus.ContainsKey(d.Port)) { _dogus[d.Port] = 0f; _parilti[d.Port] = 1f; }
        // Artık olmayan düğümlerin kayıtlarını sil.
        foreach (var k in _dogus.Keys.Where(k => _dugumler.All(d => d.Port != k)).ToList()) { _dogus.Remove(k); _parilti.Remove(k); }
        // Yeniden çiz.
        Invalidate();
    }

    // Seviyeye göre neon rengi verir.
    private static Color Renk(RadarSeviye s) => s switch { RadarSeviye.Kritik => TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusDanger)), RadarSeviye.Uyari => TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusWarning)), _ => TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusSuccess)) };

    // Seviyenin okunur adını verir.
    private static string SeviyeAdi(RadarSeviye s) => s switch { RadarSeviye.Kritik => "KRİTİK", RadarSeviye.Uyari => "UYARI / DİKKAT", _ => "GÜVENLİ" };

    // Kürenin merkezini ve yarıçapını hesaplar (başlık ve alttaki yazılar için yer bırakır).
    private (float Cx, float Cy, float R) Olcu()
    {
        float cx = Width / 2f, cy = 60f + (Height - 60f - 110f) / 2f;
        float r = Math.Clamp(Math.Min(Width, Height - 96f) * 0.34f, 82f, 205f);
        return (cx, cy, r);
    }

    // Düğümlerin ekrandaki yerlerini hesaplar: kritikler merkeze yakın, güvenliler dışta.
    private void Yerlestir()
    {
        // Eski yerleri temizle.
        _yerler.Clear();
        // Merkez ve yarıçap.
        var (cx, cy, r) = Olcu();
        // Her seviye için sayaç (aynı halkada düğümler açıyla yayılsın).
        var sayac = new Dictionary<RadarSeviye, int>();
        // Düğümleri kapı numarasına göre sırala (yerler kararlı kalsın).
        foreach (var d in _dugumler.OrderBy(x => x.Port))
        {
            // Bu halkadaki sıra numarası.
            sayac[d.Seviye] = sayac.GetValueOrDefault(d.Seviye) + 1;
            int k = sayac[d.Seviye];
            // Halka oranı: kritik 0.36, uyarı 0.64, güvenli 0.90.
            float oran = d.Seviye switch { RadarSeviye.Kritik => 1.17f, RadarSeviye.Uyari => 1.21f, _ => 1.25f };
            // Açı: altın açı (137,5°) ile yayılır; halka başına kaydırma var.
            float aci = (k * 137.5f + (int)d.Seviye * 53f + 18f) % 360f;
            // Küçük sapma: aynı halkada üst üste binmeyi azaltır.
            oran += ((d.Port % 5) - 2) * 0.008f;
            // Radyan ve konum (açı yukarıdan saat yönünde).
            double rad = aci * Math.PI / 180.0;
            _yerler.Add((d, new PointF(cx + (float)Math.Sin(rad) * r * oran, cy - (float)Math.Cos(rad) * r * oran), aci));
        }
    }

    // Kayan yazının kayma miktarı (piksel) ve animasyon saati.
    private float _kayma; private readonly System.Diagnostics.Stopwatch _saat = System.Diagnostics.Stopwatch.StartNew(); private long _sonCizimMs;
    // Etiket yazı tipi.
    private readonly Font _fEtiket = new("Consolas", 8.5f, FontStyle.Bold);
    // Yuvarlatılmış kapsül yolu (r verilmezse tam yuvarlak uçlu).
    private static GraphicsPath KapsulYolu(Rectangle k, int r = -1)
    {
        if (r < 0) r = k.Height / 2; int d = r * 2; var p = new GraphicsPath();
        p.AddArc(k.X, k.Y, d, d, 180, 90); p.AddArc(k.Right - d, k.Y, d, d, 270, 90); p.AddArc(k.Right - d, k.Bottom - d, d, d, 0, 90); p.AddArc(k.X, k.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    // Dönen siber küre ve fareyle sürükleme durumu.
    private readonly SiberKure _kure = new(); private bool _surukle; private Point _sonFare;

    // Radarı çizer.
    protected override void OnPaint(PaintEventArgs e)
    {
        // Çizim yüzeyi ve pürüzsüzlük.
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        // Arka planı boya.
        g.Clear(BackColor);
        // Düğüm yerlerini güncelle.
        Yerlestir();
        // Merkez ve yarıçap.
        var (cx, cy, r) = Olcu();

        // --- Merkezde dönen 3B siber küre (halkalar, dönen radar kolu, hareketli yaylar) ---
        bool koyuTema = TemaYonetici.Mod == TemaModu.Koyu;
        float rs = r;
        long simdi = _saat.ElapsedMilliseconds; float dt = _sonCizimMs == 0 ? 1f : Math.Clamp((simdi - _sonCizimMs) / 16.667f, 0.3f, 4f); _sonCizimMs = simdi;
        _kure.Ciz(g, cx, cy, rs, Width, Height, koyuTema, _taramada, _kol - 90f, dt);

        // --- Küre kenarından düğümlere kesikli kılavuz çizgiler ---
        foreach (var y in _yerler)
        {
            // Düğümün belirme ilerlemesi.
            float dogus = _dogus.GetValueOrDefault(y.D.Port, 1f);
            // Düğümün rengi.
            var c = Renk(y.D.Seviye);
            // Çizgi: belirdikçe parlar; fare üstündeyse daha parlak.
            using var kalem = new Pen(DesignTokens.Saydam(c, (int)(95 * dogus) + (_ustte >= 0 && _yerler[_ustte].D.Port == y.D.Port ? 90 : 0)), 1.6f);
            { kalem.DashPattern = new[] { 2f, 3f }; float ux = y.Yer.X - cx, uy = y.Yer.Y - cy, boy = MathF.Sqrt(ux * ux + uy * uy); if (boy > 1f) { float bx = cx + ux / boy * rs, by = cy + uy / boy * rs; g.DrawLine(kalem, bx, by, bx + (y.Yer.X - bx) * dogus, by + (y.Yer.Y - by) * dogus); } }
        }


        // --- Hedef IP kapsülü (kürenin altında): ⚡ + adres ---
        {
            string ip = _hedef; var olc = TextRenderer.MeasureText(g, ip, _fIp, Size.Empty, TextFormatFlags.NoPadding); int kw = olc.Width + 46, kh = 26;
            var kutu = new Rectangle((int)(cx - kw / 2f), Height - 98, kw, kh);
            using var yol = KapsulYolu(kutu);
            using (var zemin = new SolidBrush(koyuTema ? ColorTranslator.FromHtml("#161D30") : Color.White)) g.FillPath(zemin, yol);
            using (var kn = new Pen(koyuTema ? ColorTranslator.FromHtml("#2A3650") : ColorTranslator.FromHtml("#CBD5E1"), 1f)) g.DrawPath(kn, yol);
            using (var sim = FontProvider.Gecici("Segoe UI Symbol", 9f)) TextRenderer.DrawText(g, "⚡", sim, new Point(kutu.X + 12, kutu.Y + 5), ColorTranslator.FromHtml("#F97316"), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, ip, _fIp, new Point(kutu.X + 32, kutu.Y + 5), koyuTema ? ColorTranslator.FromHtml("#818CF8") : ColorTranslator.FromHtml("#4F46E5"), TextFormatFlags.NoPadding);
        }

        // --- Düğümler ---
        for (int i = 0; i < _yerler.Count; i++)
        {
            // Düğüm bilgileri.
            var (d, yer, _) = _yerler[i];
            // Belirme ve parıltı değerleri.
            float dogus = _dogus.GetValueOrDefault(d.Port, 1f), parilti = _parilti.GetValueOrDefault(d.Port, 0f);
            // Düğümün rengi ve yarıçapı (fare üstündeyse büyür; belirirken büyür).
            var c = Renk(d.Seviye); float yr = (i == _ustte ? 7.5f : 6f) * dogus;
            // Dış nabız halkası: kritikte, kol geçerken ya da üstündeyken.
            if (d.Seviye == RadarSeviye.Kritik || parilti > 0.05f || i == _ustte)
            {
                float nr = yr + 5f + (float)Math.Sin(_nabiz * 2.3 + i) * 2.5f + parilti * 7f;
                using var halka = new Pen(DesignTokens.Saydam(c, (int)Math.Clamp(Math.Max(0.28f, parilti * 0.75f) * 255, 0, 255)), 1.5f); g.DrawEllipse(halka, yer.X - nr, yer.Y - nr, 2 * nr, 2 * nr);
            }
            // Parlama, kart rengi halka ve renkli dolgu.
            using (var p = new SolidBrush(DesignTokens.Saydam(c, (int)(40 + 90 * parilti)))) g.FillEllipse(p, yer.X - yr * 2.2f, yer.Y - yr * 2.2f, yr * 4.4f, yr * 4.4f);
            using (var f = new SolidBrush(koyuTema ? ColorTranslator.FromHtml("#161D30") : Color.White)) g.FillEllipse(f, yer.X - yr - 2.5f, yer.Y - yr - 2.5f, 2 * yr + 5, 2 * yr + 5);
            using (var f = new SolidBrush(c)) g.FillEllipse(f, yer.X - yr, yer.Y - yr, 2 * yr, 2 * yr);
            // Etiket kapsülü: "445 SMB" (düğümün dışına doğru).
            if (dogus > 0.7f)
            {
                string etiket = $"{d.Port} {d.Servis}";
                int tw = TextRenderer.MeasureText(g, etiket, _fEtiket, Size.Empty, TextFormatFlags.NoPadding).Width, kw = tw + 14, kh = 20;
                bool sag = yer.X >= cx;
                var kutu = new Rectangle((int)(sag ? yer.X + 13 : yer.X - 13 - kw), (int)(yer.Y - kh / 2f), kw, kh);
                using var yol = KapsulYolu(kutu, 6);
                using (var zemin = new SolidBrush(DesignTokens.Saydam(koyuTema ? ColorTranslator.FromHtml("#161D30") : Color.White, 235))) g.FillPath(zemin, yol);
                using (var kn = new Pen(i == _ustte ? c : (koyuTema ? ColorTranslator.FromHtml("#2A3650") : ColorTranslator.FromHtml("#CBD5E1")), i == _ustte ? 1.4f : 1f)) g.DrawPath(kn, yol);
                TextRenderer.DrawText(g, etiket, _fEtiket, new Point(kutu.X + 7, kutu.Y + 3), koyuTema ? ColorTranslator.FromHtml("#E8ECF4") : ColorTranslator.FromHtml("#0F172A"), TextFormatFlags.NoPadding);
            }
        }

        // --- Üst sol başlık ---
        TextRenderer.DrawText(g, "CANLI AĞ VE PORT RADARI", _fBaslik, new Point(18, 12), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusInfo)), TextFormatFlags.NoPadding);
        // Alt başlık: hedef ve düğüm sayısı.
        TextRenderer.DrawText(g, $"Hedef: {_hedef}  ·  {_dugumler.Count} açık kapı/servis  ·  Düğüme tıklayın: ayrıntısına gidin", _fKucuk, new Point(18, 34), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)), TextFormatFlags.NoPadding);
        // Üst sağ durum yazısı: tarama sürerken yanıp söner.
        bool yanik = !_taramada || ((int)(_nabiz * 2) % 2 == 0);
        // Duruma göre yazı.
        TextRenderer.DrawText(g, _taramada ? "◉ TARANIYOR" : "◎ BEKLEMEDE", _fKalin, new Point(Width - 120, 14), _taramada ? (yanik ? TemaYonetici.C(DesignTokens.Ozgun(TokenRol.AccentPrimary)) : TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusWarningBg))) : TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)), TextFormatFlags.NoPadding);

        // --- Alarm simgesi: kritik düğüm varsa başlıkta yanıp söner ---
        int kritik = _dugumler.Count(d => d.Seviye == RadarSeviye.Kritik);
        if (kritik > 0)
        {
            bool yan = Math.Sin(_nabiz * 2.2) > -0.3;
            using var alarmYazi = FontProvider.Gecici("Segoe UI Emoji", 12f);
            if (yan) TextRenderer.DrawText(g, "🚨", alarmYazi, new Point(Width - 168, 8), Color.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, kritik + " KRİTİK", _fKalin, new Point(Width - 222, 14), Renk(RadarSeviye.Kritik), TextFormatFlags.NoPadding);
        }

        // --- Boş durum mesajı ---
        if (_dugumler.Count == 0)
        {
            // Mesaj: tarama sürüyorsa bekle, sürmüyorsa yönlendir.
            string mesaj = _taramada ? "Taranıyor... açık kapılar bulundukça burada düğüm olarak belirecek." : "Henüz tarama yapılmadı. \"⚡ TARAMAYI BAŞLAT\"a basın; açık kapılar burada düğüm olarak belirecek.";
            // Merkezin altında, radar alanı genişliğinde soldan sağa kayan yazı (aralıklı, kesintisiz tekrar).
            var bant = new Rectangle(18, Height - 52, Width - 36, 20);
            var renk = TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted));
            int yw = TextRenderer.MeasureText(g, mesaj, _fKucuk, Size.Empty, TextFormatFlags.NoPadding).Width, adim = yw + 60;
            // TextRenderer GDI+ kırpmasını dinlemez; kırpma için DrawString kullanılır.
            var eskiKirp = g.Clip; g.SetClip(bant); var eskiHint = g.TextRenderingHint; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var fir = new SolidBrush(renk))
                for (float x = bant.Left + _kayma % adim - adim; x < bant.Right; x += adim)
                    g.DrawString(mesaj, _fKucuk, fir, x, bant.Top, StringFormat.GenericTypographic);
            g.TextRenderingHint = eskiHint; g.Clip = eskiKirp;
        }

        // --- Alt sol açıklama (legend) ---
        int ly = Height - 28;
        // Üç renk için noktalar ve yazılar.
        (string Ad, Color Renk)[] oge = { ("Güvenli", Renk(RadarSeviye.Guvenli)), ("Dikkat / Uyarı", Renk(RadarSeviye.Uyari)), ("Kritik", Renk(RadarSeviye.Kritik)) };
        // Sol kenardan başla.
        int lx = 18;
        foreach (var (ad, renk) in oge)
        {
            // Renkli nokta.
            using (var f = new SolidBrush(renk)) g.FillEllipse(f, lx, ly + 3, 11, 11);
            // Yazı.
            TextRenderer.DrawText(g, ad, _fKucuk, new Point(lx + 16, ly + 1), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextSecondary)), TextFormatFlags.NoPadding);
            // Bir sonraki öğe için sağa kay.
            lx += 16 + TextRenderer.MeasureText(ad, _fKucuk).Width + 18;
        }
        // Ayrıntı ipucu (alt sağ).
        TextRenderer.DrawText(g, "Tablo ve asistan için soldaki ⚡ Yeni Tarama sekmesine geçin", _fKucuk, new Rectangle(Width - 380, ly + 1, 362, 18), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)), TextFormatFlags.Right | TextFormatFlags.NoPadding);

        // --- Fare üstündeki düğümün baloncuğu ---
        if (_ustte >= 0 && _ustte < _yerler.Count)
        {
            // Düğüm bilgileri.
            var (d, yer, _) = _yerler[_ustte];
            // Balon yazısı iki satır.
            string s1 = $"Port {d.Port} · {d.Servis}", s2 = $"{SeviyeAdi(d.Seviye)} — ayrıntı için tıklayın";
            // Balonun boyutu.
            int w = Math.Max(TextRenderer.MeasureText(s1, _fKalin).Width, TextRenderer.MeasureText(s2, _fKucuk).Width) + 20;
            // Balonun yeri: düğümün üstünde, kenarlardan taşmayacak şekilde.
            int bx = (int)Math.Clamp(yer.X - w / 2f, 8, Math.Max(8, Width - w - 8)), by = (int)Math.Max(60, yer.Y - 64);
            // Balon zemini ve çerçevesi.
            using (var zemin = new SolidBrush(TemaYonetici.C(DesignTokens.Saydam(DesignTokens.Ozgun(TokenRol.SurfaceCard), 235)))) g.FillRectangle(zemin, bx, by, w, 40);
            // Çerçeve renginde düğümün rengi.
            using (var cer = new Pen(Renk(d.Seviye), 1.5f)) g.DrawRectangle(cer, bx, by, w, 40);
            // İki satır yazı.
            TextRenderer.DrawText(g, s1, _fKalin, new Point(bx + 10, by + 4), TemaYonetici.C(Color.White), TextFormatFlags.NoPadding);
            // İkinci satır.
            TextRenderer.DrawText(g, s2, _fKucuk, new Point(bx + 10, by + 21), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted)), TextFormatFlags.NoPadding);
        }
    }

    // Fare hareket edince hangi düğümün üstünde olduğunu bul.
    protected override void OnMouseMove(MouseEventArgs e)
    {
        // Taban işlemi.
        base.OnMouseMove(e);
        // Küre sürükleniyorsa döndür.
        if (_surukle)
        {
            int dx = e.X - _sonFare.X, dy = e.Y - _sonFare.Y;
            _kure.RotLon = (_kure.RotLon + dx * 0.35f) % 360f; _kure.TiltLat = Math.Clamp(_kure.TiltLat + dy * 0.25f, -50f, 55f); _sonFare = e.Location; return;
        }
        // Üstteki düğümü ara.
        int bulunan = -1;
        // Her düğümü kontrol et.
        for (int i = 0; i < _yerler.Count; i++)
        {
            // Fareyle düğüm merkezi arasındaki uzaklık.
            float dx = e.X - _yerler[i].Yer.X, dy = e.Y - _yerler[i].Yer.Y;
            // 15 pikselden yakınsa bu düğümdür.
            if (dx * dx + dy * dy <= 15f * 15f) { bulunan = i; break; }
        }
        // Değiştiyse imleci ve görünümü güncelle.
        if (bulunan != _ustte) { _ustte = bulunan; Cursor = bulunan >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    // Düğümün üstünde değilse sürükleme başlar.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && _ustte < 0) { _surukle = true; _kure.Suruklenior = true; _sonFare = e.Location; Cursor = Cursors.SizeAll; Capture = true; }
    }

    // Bırakınca sürükleme biter, küre yine kendi kendine döner.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_surukle) { _surukle = false; _kure.Suruklenior = false; Cursor = Cursors.Default; Capture = false; }
    }

    // Fare kutudan çıkınca vurguyu kaldır.
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _ustte = -1; Cursor = Cursors.Default; Invalidate(); }

    // Tıklanınca üstündeki düğümün kapı numarasını haber ver.
    protected override void OnMouseClick(MouseEventArgs e)
    {
        // Taban işlemi.
        base.OnMouseClick(e);
        // Bir düğümün üstündeyse haber ver.
        if (_ustte >= 0 && _ustte < _yerler.Count) DugumTiklandi?.Invoke(_yerler[_ustte].D.Port);
    }

    // Bellek temizliği.
    protected override void Dispose(bool disposing)
    {
        // Zamanlayıcıyı ve yazı tiplerini temizle.
        if (disposing) { _zaman.Dispose(); _fBaslik.Dispose(); _fKucuk.Dispose(); _fKalin.Dispose(); _fIp.Dispose(); _fEtiket.Dispose(); }
        // Windows'un kendi temizliği.
        base.Dispose(disposing);
    }
}
