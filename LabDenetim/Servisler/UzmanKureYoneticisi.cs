// Uzman Görünümün harita küresi ve geçiş şeridi. Eskiden Form1'in UzmanKure.cs partial dosyasıydı; artık ayrı sınıf: ana pencereden yalnızca TemaliForm, tarama ekranı,
// kabukta sayfaya gitme eylemi, küre yenileme ve kalkan tıklama eylemleri alır.

// Programımızın adı.
namespace LabDenetim;

// Harita küresi yöneticisi.
internal sealed class UzmanKureYoneticisi
{
    private readonly TemaliForm _f; private readonly TaramaSayfasi _tarama; private readonly Action<string, string?, bool> _kabukGit; private readonly Action _kureYansit, _kalkanTiklandi;
    // kureYansit: küre panellerine durumu yollar; kalkanTiklandi: kalkana tıklanınca çalışacak eylem (Nöbetçi/koruma hizmeti).
    public UzmanKureYoneticisi(TemaliForm f, TaramaSayfasi tarama, Action<string, string?, bool> kabukGit, Action kureYansit, Action kalkanTiklandi) { _f = f; _tarama = tarama; _kabukGit = kabukGit; _kureYansit = kureYansit; _kalkanTiklandi = kalkanTiklandi; }

    internal KureWebPaneli? _uzmanKure; internal Panel? _kureSerit; private Button? _btnKureRadar, _btnKureHarita; private Label? _lblIsik;
    // Varsayılan: harita küresi. Açılamazsa (WebView2 yoksa) kendiliğinden klasik radara döner.
    internal bool _haritaKure = true;
    internal readonly GorunurZamanlayici _isikSaat = new() { Interval = 8000 }; private bool _isikOkunuyor;   // görsel ışık yoklaması: küre görünmüyorsa durur

    // Harita küresini ve geçiş şeridini kurar. GorunumKur'dan bir kez çağrılır.
    internal void Kur()
    {
        _uzmanKure = new KureWebPaneli { Dock = DockStyle.Fill, Visible = false };
        _uzmanKure.KalkanTiklandi += _kalkanTiklandi;
        // Işığa tıklayınca Olay Zaman Çizgisi açılır.
        _uzmanKure.IsikTiklandi += () => { if (_f.IsHandleCreated && !_f.IsDisposed) _f.BeginInvoke(() => _kabukGit("olay", null, true)); };
        _kureSerit = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar), Padding = new Padding(6, 3, 6, 3) };
        _btnKureRadar = SeritDugmesi("Klasik radar", "Kapı düğümlerini gösteren radar görünümü (son taramanın gerçek sonucu).");
        _btnKureHarita = SeritDugmesi("Harita küresi", "Ülkelerin üstüne gelince şekli parlar, tıklayınca odaklanır, tekerlekle yakınlaşır. Uyarı/Kritik olaylar ışık yakar.");
        _lblIsik = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Font = FontProvider.Al(FontRol.Caption), UseMnemonic = false, Padding = new Padding(0, 0, 8, 0) };
        _btnKureRadar.Click += (_, _) => HaritaKureSec(false); _btnKureHarita.Click += (_, _) => HaritaKureSec(true);
        GorselAyarlar.Degisti += () => { if (_f.IsHandleCreated && !_f.IsDisposed) _f.BeginInvoke(SeritBoya); };
        // Sıra: dolgu önce, kenara dayalı şerit en arkada (kenar kontrolleri önce yerleşir, dolgu kalanı alır).
        _kureSerit.Controls.Add(_lblIsik); _kureSerit.Controls.Add(_btnKureHarita); _kureSerit.Controls.Add(_btnKureRadar);
        _tarama.pnlOrta.Controls.Add(_uzmanKure); _tarama.pnlOrta.Controls.Add(_kureSerit); _tarama.pnlOrta.Controls.SetChildIndex(_kureSerit, _tarama.pnlOrta.Controls.Count - 1);
        // Saat hep çalışır ama küre görünmüyorsa hiçbir şey okumaz; Uzman/Kolay geçişinde küre yeniden görününce ışık hemen tazelenir.
        _isikSaat.Sahip = _uzmanKure; _isikSaat.Tick += (_, _) => KureSaatAdimi(); _isikSaat.Start();
        _uzmanKure.VisibleChanged += (_, _) => { if (_uzmanKure.Visible) { _kureYansit(); IsikYenile(); } };
        SeritBoya();
    }

    // Şeritteki küçük düğme.
    internal Button SeritDugmesi(string yazi, string ipucuMetni)
    {
        var b = new Button { Text = yazi, Dock = DockStyle.Left, Width = 112, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), UseMnemonic = false, TabStop = false };
        b.FlatAppearance.BorderSize = 1; _tarama.ipucu.SetToolTip(b, ipucuMetni); return b;
    }

    // Seçili düğmeyi vurgular.
    internal void SeritBoya()
    {
        if (_btnKureRadar == null || _btnKureHarita == null) return;
        void Boya(Button b, bool secili) { b.BackColor = secili ? DesignTokens.Ozgun(TokenRol.AccentPrimary) : DesignTokens.Ozgun(TokenRol.SurfaceCard); b.ForeColor = secili ? DesignTokens.Ozgun(TokenRol.TextPrimary) : DesignTokens.Ozgun(TokenRol.TextMuted); b.FlatAppearance.BorderColor = secili ? DesignTokens.Ozgun(TokenRol.StatusInfo) : DesignTokens.Ozgun(TokenRol.BorderStrong); }
        Boya(_btnKureRadar, !_haritaKure); Boya(_btnKureHarita, _haritaKure);
    }

    // Klasik radar / harita küresi seçimi.
    internal void HaritaKureSec(bool harita)
    {
        _haritaKure = harita; SeritBoya(); _tarama.GorunumSec(_tarama._gorunum);
    }

    // Görünüm değişince çağrılır: şerit ve küre yalnızca ◼ Panel (radar) görünümünde, küre de yalnızca harita seçiliyken görünür.
    internal void GorunumDegisti(Gorunum g)
    {
        if (_uzmanKure == null || _kureSerit == null) return;
        bool panel = g == Gorunum.Radar, goster = panel && _haritaKure;
        _kureSerit.Visible = panel; _uzmanKure.Visible = goster;
        if (goster) { _kureYansit(); IsikYenile(); }
    }

    // Birkaç saniyede bir: WebView2 açılamadıysa klasik radara dön, değilse ışığı tazele.
    internal void KureSaatAdimi()
    {
        if (_uzmanKure == null || !_uzmanKure.Visible) return;
        if (_uzmanKure.SonHata != null) { _btnKureHarita!.Enabled = false; _lblIsik!.Text = "Harita küresi açılamadı (WebView2 yok); klasik radar gösteriliyor"; HaritaKureSec(false); return; }
        IsikYenile();
    }

    // Olay günlüğünü arka planda okuyup ışığı günceller (arayüz donmasın).
    internal void IsikYenile() => GuvenliCalistir.Baslat(() => IsikYenileAsync(), "UzmanKure.IsikYenile");
    internal async Task IsikYenileAsync()
    {
        if (_uzmanKure == null || !_uzmanKure.Visible || _isikOkunuyor) return; _isikOkunuyor = true;
        try
        {
            var ozet = await Task.Run(() => KureIsiklari.Ozetle(ServisBaglami.OlayOkuyucu.SonUyariKritik(KureIsiklari.Pencere, DateTime.Now), DateTime.Now));
            if (_f.IsDisposed || _uzmanKure == null || _lblIsik == null) return;
            _uzmanKure.Isik(ozet);
            _lblIsik.ForeColor = ozet == null ? DesignTokens.Ozgun(TokenRol.TextMuted) : ozet.Kritik > 0 ? DesignTokens.Ozgun(TokenRol.StatusDanger) : DesignTokens.Ozgun(TokenRol.StatusWarning);
            _lblIsik.Text = ozet == null ? "Son 30 dakikada Uyarı veya Kritik olay yok" : $"Son 30 dk: {ozet.Kritik} Kritik · {ozet.Uyari} Uyarı  (ışığa tıklayınca Olay Zaman Çizgisi açılır)";
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "UzmanKure.IsikYenile"); }
        finally { _isikOkunuyor = false; }
    }
}
