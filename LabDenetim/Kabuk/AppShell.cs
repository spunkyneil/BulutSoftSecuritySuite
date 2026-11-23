// Bu dosya KURUMSAL KABUĞUN ANA DÜZENİDİR: solda 4 gruplu gezinme, sağda üst çubuk + sayfa alanı. Tek merkezli navigasyon burada: sayfalar GitAsync(sayfaId, sekmeId) ile açılır;
// komut paleti, kısayollar, bildirim zili ve menü hep aynı yolu kullanır (eski modal ShowDialog açmaz). Yerleşim TableLayoutPanel'dir; sütun/satır ölçüleri DPI'a göre kurulur.
// Kabuk iş mantığı içermez: sayfa üretimi ve bağlama dışarıda (Form1) yapılır; kabuk yetki/mod denetimi, sayfa geçişi, başlık ve ekmek kırıntısını yönetir.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Kabuk.
internal sealed class AppShell : TableLayoutPanel, IKendiRenginiYoneten, IThemeAware
{
    private readonly TokenPanel _alan = new(TokenRol.SurfaceApp);
    private readonly TableLayoutPanel _sag = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty };
    private readonly Dictionary<string, IModulePage> _sayfalar = new(StringComparer.Ordinal);
    private IModulePage? _aktif; private CancellationTokenSource? _gecisIptal; private ModTuru _mod = ModTuru.Uzman; private bool _modUygulaniyor;

    public AppShell()
    {
        Dock = DockStyle.Fill; ColumnCount = 2; RowCount = 1; Margin = Padding.Empty; Padding = Padding.Empty; DoubleBuffered = true; BackColor = DesignTokens.Renk(TokenRol.SurfaceApp);
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236)); ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Kenar = new SidebarNav { Dock = DockStyle.Fill, Margin = Padding.Empty }; Ust = new GlobalHeader { Dock = DockStyle.Fill, Margin = Padding.Empty };
        _sag.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); _sag.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); _sag.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _alan.Dock = DockStyle.Fill; _alan.Margin = Padding.Empty;
        _sag.Controls.Add(Ust, 0, 0); _sag.Controls.Add(_alan, 0, 1);
        Controls.Add(Kenar, 0, 0); Controls.Add(_sag, 1, 0);
        Kenar.SayfaSecildi += async id => await GitAsync(id);
        Kenar.KilitliTiklandi += id => { var p = NavigasyonModeli.Bul(id); KabukYetki.Kontrol(FindForm(), p?.Yetki, p?.Baslik ?? id); };
        Kenar.KompaktDegisti += _ => OlculeriKur();
        Ust.ModSecildi += m => ModIstegi?.Invoke(m);
        Kenar.Mod = _mod; Ust.Mod = _mod; Kenar.Yenile();
    }

    // Sol gezinme ve üst çubuk (dışarıdan olay bağlamak için).
    public SidebarNav Kenar { get; }
    public GlobalHeader Ust { get; }

    // Kullanıcı üst çubukta modu değiştirmek istedi (Form1 ArayuzModu.Ayarla çağırır; kabuk modu Mod özelliğiyle günceller).
    public event Action<ModTuru>? ModIstegi;
    // Sayfa değişti (kimlik).
    public event Action<string>? SayfaDegisti;

    // Aktif sayfa kimliği.
    public string? AktifSayfaId => _aktif?.SayfaId;
    // Kayıtlı sayfa.
    public IModulePage? Sayfa(string id) => _sayfalar.TryGetValue(id, out var p) ? p : null;
    // Kayıtlı sayfa sayısı.
    public int SayfaSayisi => _sayfalar.Count;

    // Sayfayı kabuğa kaydeder (kimlik NavigasyonModeli kataloğunda olmalı).
    public void SayfaKaydet(IModulePage sayfa)
    {
        ArgumentNullException.ThrowIfNull(sayfa);
        if (NavigasyonModeli.Bul(sayfa.SayfaId) == null) throw new ArgumentException("Katalogda olmayan sayfa kimliği: " + sayfa.SayfaId, nameof(sayfa));
        _sayfalar[sayfa.SayfaId] = sayfa;
        if (sayfa is IModDuyarli d) d.ModDegisti(_mod);
        if (sayfa is ISekmeBilgisi b) b.SekmeBilgisiDegisti += () => { if (ReferenceEquals(_aktif, sayfa)) BaslikGuncelle(sayfa); };
    }

    // Görünüm modu (Kolay/Uzman): gezinme, üst çubuk ve sekmeler güncellenir; aktif sayfa bu modda görünmüyorsa Durum Panosu'na dönülür.
    public ModTuru Mod
    {
        get => _mod;
        set
        {
            if (_modUygulaniyor) return; _modUygulaniyor = true;
            try
            {
                _mod = value; Kenar.Mod = value; Ust.Mod = value;
                foreach (var p in _sayfalar.Values) if (p is IModDuyarli d) d.ModDegisti(value);
                if (_aktif != null && value == ModTuru.Kolay && NavigasyonModeli.Bul(_aktif.SayfaId) is { SadeceUzman: true } && _sayfalar.ContainsKey("durum") && !_zorlaAcik) _ = GitAsync("durum");
            }
            finally { _modUygulaniyor = false; }
        }
    }
    private bool _zorlaAcik;

    // Rol/oturum değişti: kilit işaretleri ve sekmeler yeniden hesaplanır.
    public void RolYenile() { Kenar.Yenile(); Ust.KullaniciYenile(); foreach (var p in _sayfalar.Values) if (p is SekmeliSayfa s) s.SeridiYenile(); }
    // Sayfaya (ve isteğe bağlı sekmeye) geçer. zorla: Kolay modda Uzman'a özgü sayfaya (ör. zilden Olay Zaman Çizgisi) de gidilebilsin.
    public async Task<bool> GitAsync(string sayfaId, string? sekmeId = null, bool zorla = false)
    {
        if (!_sayfalar.TryGetValue(sayfaId, out var sayfa)) return false;
        var (izinli, eksik) = NavigasyonModeli.Erisim(sayfaId, sekmeId, ServisBaglami.Oturum.Rol);
        if (!izinli) { var td = NavigasyonModeli.Bul(sayfaId); KabukYetki.Kontrol(FindForm(), eksik, td?.Baslik ?? sayfaId); return false; }
        bool ayni = ReferenceEquals(_aktif, sayfa);
        if (!ayni)
        {
            _gecisIptal?.Cancel(); _gecisIptal?.Dispose(); _gecisIptal = new CancellationTokenSource(); var ct = _gecisIptal.Token;
            if (_aktif != null) { try { await _aktif.KapanirkenAsync(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "AppShell.GitAsync"); } _aktif.Gorunum.Visible = false; }
            var v = sayfa.Gorunum; if (v.Parent != _alan) { v.Dock = DockStyle.Fill; _alan.Controls.Add(v); }
            v.Visible = true; v.BringToFront(); _aktif = sayfa; _zorlaAcik = zorla; Kenar.Secili = sayfaId; BaslikGuncelle(sayfa); SayfaDegisti?.Invoke(sayfaId);
            try { await sayfa.AcildigindaAsync(ct); }
            catch (OperationCanceledException hk3) { HataKayit.Sessiz(hk3, "AppShell.GitAsync.iptal"); }   // başka sayfa istendi: sayfa açılışı iptal edildi
            catch (Exception hk2) { HataKayit.Sessiz(hk2, "AppShell.GitAsync", true); TemaliMesaj.Goster(FindForm(), "\"" + sayfa.Baslik + "\" açılırken bir sorun oluştu. Ayrıntı olay günlüğüne yazıldı.", "Sayfa açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        if (sekmeId != null && sayfa is ISekmeGecisi s) { if (!await s.SekmeGitAsync(sekmeId)) return false; BaslikGuncelle(sayfa); }
        return true;
    }

    // Başlık ve ekmek kırıntısı: "Grup › Sayfa › Sekme".
    private void BaslikGuncelle(IModulePage s)
    {
        string? sekme = (s as ISekmeBilgisi)?.AktifSekmeBasligi;
        Ust.Baslik = sekme ?? s.Baslik; Ust.Kirinti = NavGrupBilgi.Adi(s.Grup) + " › " + s.Baslik;
    }

    // Sütun ve satır ölçüleri DPI'a göre.
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); OlculeriKur(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); OlculeriKur(); }
    private void OlculeriKur()
    {
        ColumnStyles[0].Width = Kenar.HedefGenislik; _sag.RowStyles[0].Height = DesignTokens.Px(this, 52); PerformLayout();
    }

    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Invalidate(true); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _gecisIptal?.Cancel(); _gecisIptal?.Dispose(); }
        base.Dispose(disposing);
    }
}
