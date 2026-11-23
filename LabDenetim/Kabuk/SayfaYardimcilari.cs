// Bu dosya kabuğun SAYFA YARDIMCILARINI içerir: mevcut bir kontrolü sayfa olarak barındıran BolumSayfasi (Form1'in eski ekran parçalarını yeni kabuğa taşır),
// Kolay/Uzman moda göre iki sayfa arasında geçen KolayUzmanSayfasi ve moda/sekme bilgisine duyarlı sayfalar için küçük arayüzler.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Görünüm modu değişince haber alması gereken sayfa.
internal interface IModDuyarli { void ModDegisti(ModTuru mod); }

// Sekme kimliğiyle sekmeye geçilebilen sayfa (kabuk GitAsync(sayfa, sekme) bunu kullanır).
internal interface ISekmeGecisi { Task<bool> SekmeGitAsync(string sekmeId); }

// Sayfa içi sekme bilgisini (ekmek kırıntısı için) veren sayfa.
internal interface ISekmeBilgisi { string? AktifSekmeBasligi { get; } event Action? SekmeBilgisiDegisti; }

// Mevcut bir kontrolü (ör. Form1'in tarama çalışma alanı) sayfa olarak barındırır: sayfa açılırken kontrolü kendi altına taşır (aynı kontrolü paylaşan sayfalar sırayla kullanır).
internal sealed class BolumSayfasi : TokenPanel, IModulePage
{
    private readonly Func<Control> _icerik; private readonly Action? _acildi, _kapandi;

    private readonly Size _enAzIcerik;

    // enAzIcerik: eski ekranın rahat görünmesi için istediği en küçük alan (96 DPI piksel); pencere daha küçükse sayfa İÇİNDE kaydırılır, kabuk (menü, üst çubuk) etkilenmez.
    public BolumSayfasi(string sayfaId, string baslik, NavGrup grup, Func<Control> icerik, Action? acildi = null, Action? kapandi = null, Eylem? gerekliYetki = null, bool sadeceUzman = false, Size enAzIcerik = default) : base(TokenRol.SurfaceApp)
    {
        SayfaId = sayfaId; Baslik = baslik; Grup = grup; GerekliYetki = gerekliYetki; SadeceUzman = sadeceUzman; _icerik = icerik; _acildi = acildi; _kapandi = kapandi; Dock = DockStyle.Fill;
        _enAzIcerik = enAzIcerik; if (enAzIcerik != default) AutoScroll = true;
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); AzamiAlaniUygula(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); AzamiAlaniUygula(); }
    private void AzamiAlaniUygula() { if (_enAzIcerik != default) AutoScrollMinSize = new Size(DesignTokens.Px(this, _enAzIcerik.Width), DesignTokens.Px(this, _enAzIcerik.Height)); }

    public string SayfaId { get; }
    public string Baslik { get; }
    public NavGrup Grup { get; }
    public Eylem? GerekliYetki { get; }
    public bool SadeceUzman { get; }
    public Control Gorunum => this;

    public Task AcildigindaAsync(CancellationToken ct)
    {
        var c = _icerik();
        if (c.Parent != this) { c.Dock = DockStyle.Fill; Controls.Add(c); }
        c.Visible = true; _acildi?.Invoke();
        return Task.CompletedTask;
    }

    public Task KapanirkenAsync() { _kapandi?.Invoke(); return Task.CompletedTask; }
}

// Kolay modda bir sayfayı, Uzman modda başka bir sayfayı gösterir (ör. Durum Panosu: Kolay panel / Uzman sekmeleri).
internal sealed class KolayUzmanSayfasi : TokenPanel, IModulePage, IModDuyarli, ISekmeBilgisi, ISekmeGecisi
{
    // Kolay modda sekme yoktur (sade panel kalır); Uzman modda istenen sekmeye geçilir.
    public Task<bool> SekmeGitAsync(string sekmeId) => _mod == ModTuru.Kolay ? Task.FromResult(true) : _uzman is ISekmeGecisi g ? g.SekmeGitAsync(sekmeId) : Task.FromResult(false);

    private readonly SayfaTanimi _tanim; private readonly IModulePage _kolay, _uzman;
    private ModTuru _mod = ModTuru.Uzman; private bool _acik; private CancellationToken _ct;

    public KolayUzmanSayfasi(SayfaTanimi tanim, IModulePage kolay, IModulePage uzman) : base(TokenRol.SurfaceApp)
    {
        _tanim = tanim; _kolay = kolay; _uzman = uzman; Dock = DockStyle.Fill;
        foreach (var p in new[] { kolay, uzman }) { p.Gorunum.Dock = DockStyle.Fill; p.Gorunum.Visible = false; Controls.Add(p.Gorunum); if (p is ISekmeBilgisi b) b.SekmeBilgisiDegisti += () => SekmeBilgisiDegisti?.Invoke(); }
    }

    public string SayfaId => _tanim.Id;
    public string Baslik => _tanim.Baslik;
    public NavGrup Grup => _tanim.Grup;
    public Eylem? GerekliYetki => _tanim.Yetki;
    public bool SadeceUzman => _tanim.SadeceUzman;
    public Control Gorunum => this;
    private IModulePage Aktif => _mod == ModTuru.Kolay ? _kolay : _uzman;
    public string? AktifSekmeBasligi => (Aktif as ISekmeBilgisi)?.AktifSekmeBasligi;
    public event Action? SekmeBilgisiDegisti;

    public async Task AcildigindaAsync(CancellationToken ct)
    {
        _ct = ct; _acik = true; _kolay.Gorunum.Visible = _mod == ModTuru.Kolay; _uzman.Gorunum.Visible = _mod != ModTuru.Kolay;
        await Aktif.AcildigindaAsync(ct);
    }

    public async Task KapanirkenAsync() { _acik = false; await Aktif.KapanirkenAsync(); }

    public void ModDegisti(ModTuru mod) => GuvenliCalistir.Baslat(() => ModDegistiAsync(mod), "SayfaYardimcilari.ModDegisti");
    public async Task ModDegistiAsync(ModTuru mod)
    {
        if (mod == _mod) return;
        var eski = Aktif; _mod = mod; foreach (var p in new[] { _kolay, _uzman }) if (p is IModDuyarli d) d.ModDegisti(mod);
        _kolay.Gorunum.Visible = mod == ModTuru.Kolay; _uzman.Gorunum.Visible = mod != ModTuru.Kolay; SekmeBilgisiDegisti?.Invoke();
        if (!_acik) return;
        try { await eski.KapanirkenAsync(); await Aktif.AcildigindaAsync(_ct); }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "KolayUzmanSayfasi.ModDegisti"); }
    }
}
