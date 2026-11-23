// Bu dosya Kolay Görünüm ana ekranındaki "canlı 3B Türkiye merkezli siber küre + koruma kalkanı" panelini içerir.
// Görüntü, programla birlikte gelen yerel Kure klasöründeki kure.html dosyasıdır (WebView2 ile gösterilir); hiçbir dış adrese bağlanmaz.
// Tema, güvenlik durumu ve kalkan bilgisi programdan JavaScript'e gönderilir; sayfada kullanıcıya gösterilen düğme yoktur.
// Kalkana tıklanınca programa "kalkan-tikla" iletisi gelir; panel kendisi hiçbir şeyi açıp kapatmaz.
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// Programımızın adı.
namespace LabDenetim;

// Kürenin renk/durum bilgisi.
internal enum KureDurumu { Guvende, Dikkat, Kritik }

// WebView2 içinde çalışan canlı küre paneli. WebView2 çalışma zamanı yoksa panel boş kalır, program etkilenmez.
internal sealed class KureWebPaneli : Panel
{
    private const string Sunucu = "kure.bulutsoft.local";
    private WebView2? _web; private bool _hazir, _basladi;
    // Animasyon duraklatma: panel görünmezken (başka sekme/sayfa), ana pencere küçültülmüşken ya da gizliyken sayfadaki requestAnimationFrame döngüsü durdurulur (bsDuraklat).
    private bool _duraklatildi; private Form? _sahipForm;
    private bool _koyu = true, _kalkan = true, _salt; private KureDurumu _durum = KureDurumu.Guvende; private string _hizmet = "";

    // Örnek (demo) yaylar görünsün mü? Varsayılan KAPALI: yalnızca gerçek koruma durumu ve gerçek olay ışığı gösterilir; "Yaylar örnek görseldir" notu da yalnızca açıkken çıkar.
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool DemoYaylar { get; set; }

    // Başlatma hatası olursa kısa açıklaması (destek ve sınama için).
    public string? SonHata { get; private set; }

    // Kullanıcı kalkana tıkladı (program Nöbetçi Modu'nu açıp kapatır).
    public event Action? KalkanTiklandi;
    // Kullanıcı bildirim ışığına tıkladı (program Olay Zaman Çizgisi'ni açar).
    public event Action? IsikTiklandi;
    // Son gönderilen ışık özeti (sayfa yeniden yüklenirse tekrar yollanır).
    private string _isikJson = "null";

    // İmleç panelin dışına çıkınca sayfaya haber verir (WebView2 çıkış olayını her zaman iletmez; küre yeniden dönsün).
    private readonly GorunurZamanlayici _imlecSaat = new() { Interval = 150 }; private bool _imlecIcerde = true;
    // WebView2 çekirdeği yalnızca arayüz iş parçacığında, atılmamış ve hazır panelde güvenle okunur; aksi halde null döner (çağıran sessizce vazgeçer).
    private CoreWebView2? Cekirdek()
    {
        if (!_hazir || IsDisposed || _web == null || _web.IsDisposed || !IsHandleCreated || InvokeRequired) return null;
        try { return _web.CoreWebView2; } catch (Exception hk9) { HataKayit.Sessiz(hk9, "KureWebPaneli.Cekirdek"); return null; }
    }

    private void ImlecKontrol()
    {
        var c = Cekirdek(); if (c == null || !Visible) return;
        bool ic; try { ic = RectangleToScreen(ClientRectangle).Contains(Cursor.Position); } catch (InvalidOperationException hk10) { HataKayit.Sessiz(hk10, "KureWebPaneli.ImlecKontrol"); return; }
        if (ic == _imlecIcerde) return; _imlecIcerde = ic;
        if (!ic) { try { _ = c.ExecuteScriptAsync("window.bsImlec && window.bsImlec(false);"); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KureWebPaneli.ImlecKontrol"); } }
    }

    // Uzman Görünümdeki bildirim ışığını günceller (null: ışık söner). Yalnızca Uyarı/Kritik olaylar ışık yakar.
    public void Isik(IsikOzeti? ozet) { _isikJson = KureIsiklari.Json(ozet); IsikGonder(); }
    private void IsikGonder() => GuvenliCalistir.Baslat(() => IsikGonderAsync(), "KureWebPaneli.IsikGonder");
    private async Task IsikGonderAsync()
    {
        var c = Cekirdek(); if (c == null) return;
        try { await c.ExecuteScriptAsync("window.bsIsik && window.bsIsik(" + _isikJson + ");"); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "KureWebPaneli.IsikGonder"); }
    }

    public KureWebPaneli()
    {
        Dock = DockStyle.Fill; BackColor = ArkaRenk(true); TabStop = false;
        VisibleChanged += (_, _) => { BaslatDene(); DuraklatmayiYansit(); };
        HandleCreated += (_, _) => { BaslatDene(); SahipFormuBagla(); };
        ParentChanged += (_, _) => SahipFormuBagla();
        _imlecSaat.Sahip = this; _imlecSaat.Tick += (_, _) => ImlecKontrol(); _imlecSaat.Start();
        // Tema değişince aktif tasarım tokenları sayfaya (CSS değişkenlerine) yeniden gönderilir (olay tabanlı; pencere taraması yok).
        TemaYonetici.TemaDegisti += TemaDegistiOlayi;
    }

    // ---- Animasyon duraklatma (olay tabanlı: panel görünürlüğü ve ana pencerenin küçültülmesi) ----
    // Panel gerçekten ekranda mı? (Visible üst öğeleri de kapsar; küçültülmüş pencere Visible kalır, ayrıca denetlenir.)
    internal bool EkrandaMi => Visible && IsHandleCreated && !IsDisposed && _sahipForm is not { WindowState: FormWindowState.Minimized } && _sahipForm is not { Visible: false };

    // Ana pencerenin küçültülme/gizlenme olaylarına bağlanır (yeniden bağlanırken eskisi bırakılır).
    private void SahipFormuBagla()
    {
        var f = FindForm(); if (ReferenceEquals(f, _sahipForm)) return;
        if (_sahipForm != null) { _sahipForm.Resize -= SahipFormuDegisti; _sahipForm.VisibleChanged -= SahipFormuDegisti; }
        _sahipForm = f; if (f != null) { f.Resize += SahipFormuDegisti; f.VisibleChanged += SahipFormuDegisti; }
        DuraklatmayiYansit();
    }
    private void SahipFormuDegisti(object? s, EventArgs e) => DuraklatmayiYansit();

    // Duraklatma durumunu sayfaya bildirir (yalnızca değişince; sayfa hazır olunca zorla bir kez).
    private void DuraklatmayiYansit(bool zorla = false)
    {
        bool dur = !EkrandaMi; if (!zorla && dur == _duraklatildi) return; _duraklatildi = dur;
        var c = Cekirdek(); if (c == null) return;
        try { _ = c.ExecuteScriptAsync(DuraklatBetigi(dur)); } catch (Exception hk) { HataKayit.Sessiz(hk, "KureWebPaneli.DuraklatmayiYansit"); }
    }
    internal static string DuraklatBetigi(bool dur) => "window.bsDuraklat && window.bsDuraklat(" + (dur ? "true" : "false") + ");";
    internal bool Hazir => _hazir;
    internal bool Duraklatildi => _duraklatildi;
    // Sınama/tanılama: sayfada betik çalıştırır (WebView2 hazır değilse null).
    internal Task<string>? BetikCalistir(string js) => Cekirdek()?.ExecuteScriptAsync(js);

    // Tema olayı: tokenları sayfaya yollar (arayüz iş parçacığında).
    private void TemaDegistiOlayi()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(TokenGonder); } else TokenGonder();
    }

    // Aktif DesignTokens renklerini kure.html'e JSON ileti olarak yollar (sayfa :root CSS değişkenlerini günceller).
    private void TokenGonder()
    {
        var c = Cekirdek(); if (c == null) return;
        try { c.PostWebMessageAsJson(DesignTokens.KureJson(DesignTokens.KoyuMu)); } catch (Exception hk8) { HataKayit.Sessiz(hk8, "KureWebPaneli.TokenGonder"); }
    }

    // Açılışta beyaz parıltı olmasın diye temaya göre zemin rengi (#141B2E Koyu / #F0F4F8 Açık).
    private static Color ArkaRenk(bool koyu) => DesignTokens.Renk(TokenRol.SurfaceApp, koyu);

    // Temayı, güvenlik durumunu ve kalkan bilgisini günceller (yalnızca değişiklik varsa sayfaya gönderilir).
    // hizmet: koruma hizmeti durumu ("calisiyor", "kurulu", "yok"; boşsa gösterilmez).
    public void Ayarla(bool koyu, KureDurumu durum, bool kalkan, bool saltOkunur, string hizmet = "")
    {
        _koyu = koyu; _durum = durum; _kalkan = kalkan; _salt = saltOkunur; _hizmet = hizmet;
        BackColor = ArkaRenk(koyu); TokenGonder();
        try { if (_web != null) _web.DefaultBackgroundColor = ArkaRenk(koyu); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "KureWebPaneli.Ayarla"); }
        Gonder();
    }

    // Sayfaya durumu JSON olarak yollar.
    private void Gonder() => GuvenliCalistir.Baslat(() => GonderAsync(), "KureWebPaneli.Gonder");
    private async Task GonderAsync()
    {
        var c = Cekirdek(); if (c == null) return;
        string durum = _durum switch { KureDurumu.Kritik => "kritik", KureDurumu.Dikkat => "dikkat", _ => "guvende" };
        var js = new StringBuilder("window.bsAyarla && window.bsAyarla({\"tema\":\"").Append(_koyu ? "koyu" : "acik").Append("\",\"durum\":\"").Append(durum)
            .Append("\",\"kalkan\":").Append(_kalkan ? "true" : "false").Append(",\"saltOkunur\":").Append(_salt ? "true" : "false").Append(",\"demoYay\":").Append(DemoYaylar ? "true" : "false").Append(",\"hizmet\":\"").Append(_hizmet is "calisiyor" or "kurulu" or "yok" ? _hizmet : "").Append("\"});");
        try { await c.ExecuteScriptAsync(js.ToString()); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "KureWebPaneli.Gonder"); }
    }

    // Görünür olunca (bir kez) WebView2'yi başlatır. Hata olursa sessizce vazgeçer.
    private void BaslatDene() => GuvenliCalistir.Baslat(() => BaslatDeneAsync(), "KureWebPaneli.BaslatDene");
    private async Task BaslatDeneAsync()
    {
        if (_basladi || DesignMode || !Visible || !IsHandleCreated) return;
        _basladi = true;
        try
        {
            string klasor = Path.Combine(Path.GetDirectoryName(typeof(KureWebPaneli).Assembly.Location) ?? AppContext.BaseDirectory, "Kure");
            if (!File.Exists(Path.Combine(klasor, "kure.html"))) return;
            string veri = Path.Combine(BrandConfig.YerelKlasor, "WebView2");
            var ortam = await CoreWebView2Environment.CreateAsync(null, veri);
            var web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = ArkaRenk(_koyu), TabStop = false };
            Controls.Add(web); _web = web;
            await web.EnsureCoreWebView2Async(ortam);
            var c = web.CoreWebView2; var a = c.Settings;
            // Yalnızca gösterim: içerik menüsü, geliştirici araçları, yakınlaştırma, durum çubuğu, hızlı tuşlar ve otomatik doldurma kapalı.
            a.AreDefaultContextMenusEnabled = false; a.AreDevToolsEnabled = false; a.IsZoomControlEnabled = false; a.IsStatusBarEnabled = false;
            a.AreBrowserAcceleratorKeysEnabled = false; a.IsGeneralAutofillEnabled = false; a.IsPasswordAutosaveEnabled = false; a.IsSwipeNavigationEnabled = false; a.IsBuiltInErrorPageEnabled = false;
            c.SetVirtualHostNameToFolderMapping(Sunucu, klasor, CoreWebView2HostResourceAccessKind.Deny);
            // Başka adrese gitmeyi, yeni pencere açmayı, izin isteklerini ve indirmeleri engelle.
            c.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://" + Sunucu + "/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
            c.NewWindowRequested += (_, e) => e.Handled = true;
            c.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            c.DownloadStarting += (_, e) => e.Cancel = true;
            c.WebMessageReceived += (_, e) => { try { string m = e.TryGetWebMessageAsString(); if (m == "kalkan-tikla") KalkanTiklandi?.Invoke(); else if (m == "isik-tikla") IsikTiklandi?.Invoke(); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "KureWebPaneli.BaslatDene"); } };
            c.NavigationCompleted += (_, _) => { _hazir = true; TokenGonder(); Gonder(); IsikGonder(); DuraklatmayiYansit(true); };
            c.Navigate("https://" + Sunucu + "/kure.html");
        }
        catch (Exception ex) { SonHata = ex.GetType().Name + ": " + ex.Message; /* WebView2 çalışma zamanı yoksa panel boş kalır. */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { TemaYonetici.TemaDegisti -= TemaDegistiOlayi; if (_sahipForm != null) { _sahipForm.Resize -= SahipFormuDegisti; _sahipForm.VisibleChanged -= SahipFormuDegisti; _sahipForm = null; } try { _imlecSaat.Stop(); _imlecSaat.Dispose(); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "KureWebPaneli.Dispose", true); } try { _web?.Dispose(); } catch (Exception hk7) { HataKayit.Sessiz(hk7, "KureWebPaneli.Dispose"); } }
        base.Dispose(disposing);
    }
}
