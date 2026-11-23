// Bu dosya kabuğa konan "eski ekran" sayfalarının tutucusudur (eski adı FormSayfaAdaptoru): SayfaDenetimi (UserControl) üretir, kabuğun çalışma alanına koyar (Dock=Fill) ve IModulePage sözleşmesine uydurur.
// Artık hiçbir Form gömülmez (TopLevel=false hilesi kalktı); sayfalar gerçek denetimdir. Tutucu:
//  • Sayfayı ilk açılışta üretir; sayfadan ayrılınca canlı bırakır (durum korunur); sayfa kendini kapatırsa (Close/DialogResult) DenetimKapandi olayını verir ve bir sonraki açılışta yeniden üretir.
//  • Yalnızca modal kapanış için konmuş alt "Kapat" düğmelerini gizler (gerçek işlem düğmeleri aynen kalır).
//  • ShowDialog dönüşüne bağlı iş akışları için DenetimHazir (oluşunca) ve DenetimKapandi (kapanınca, DialogResult ile) köprülerini sunar.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Eski ekranı sayfa olarak barındırır.
internal sealed class DenetimSayfasi : UserControl, IModulePage, IKendiRenginiYoneten, IThemeAware
{
    private readonly Func<SayfaDenetimi> _uret;
    private SayfaDenetimi? _denetim;

    public DenetimSayfasi(string sayfaId, string baslik, NavGrup grup, Func<SayfaDenetimi> denetimUret, Eylem? gerekliYetki = null, bool sadeceUzman = false)
    {
        SayfaId = sayfaId; Baslik = baslik; Grup = grup; GerekliYetki = gerekliYetki; SadeceUzman = sadeceUzman; _uret = denetimUret;
        Dock = DockStyle.Fill; BackColor = DesignTokens.Renk(TokenRol.SurfaceApp);
    }

    public string SayfaId { get; }
    public string Baslik { get; }
    public NavGrup Grup { get; }
    public Eylem? GerekliYetki { get; }
    public bool SadeceUzman { get; }
    public Control Gorunum => this;

    // İçerideki sayfa (henüz üretilmediyse ya da kapandıysa null).
    public SayfaDenetimi? Denetim => _denetim is { IsDisposed: false } ? _denetim : null;

    // Sayfa üretilip gösterildikten hemen sonra çağrılır (köprü bağlamak için).
    public Action<SayfaDenetimi>? DenetimHazir { get; set; }

    // İçerideki sayfa kendini kapatınca (düğmeyle ya da DialogResult ile) çağrılır; DialogResult sayfanın kapanıştaki değeridir.
    // Denetim: kapanan sayfa (özellikleri, ör. GecmisFormu.Secilen, okunabilir; ShowDialog dönüşüne bağlı iş akışları buradan yürür).
    public event Action<DenetimSayfasi, SayfaDenetimi, DialogResult>? DenetimKapandi;

    // true: sayfa her açıldığında yeniden üretilir (anlık görüntü gösteren ekranlar: Durum, Olay, Geçmiş, Kalkan). false: canlı kalır (aracın çalışan durumu korunur).
    public bool HerAcilistaYenile { get; set; }

    public Task AcildigindaAsync(CancellationToken ct)
    {
        if (Denetim != null && HerAcilistaYenile && _acikOlmus) { var eski = _denetim; _denetim = null; try { Controls.Remove(eski); eski!.Dispose(); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "DenetimSayfasi.Yenile"); } }
        if (Denetim == null) DenetimiUretVeGoster();
        _acikOlmus = true;
        return Task.CompletedTask;
    }
    private bool _acikOlmus;

    // Sayfadan ayrılırken çağrılır (örn. Sensör & Bildirim Merkezi'nde değişen ayarların Kolay göstergelere ve bildirim zilne yansıması). Sayfa canlı kalır.
    public Action? Kapanirken { get; set; }

    public Task KapanirkenAsync()
    {
        try { Kapanirken?.Invoke(); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "DenetimSayfasi.Kapanirken"); }
        return Task.CompletedTask;
    }

    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Invalidate(true); }

    // Sayfayı üretir, çalışma alanına koyar ve gösterir.
    private void DenetimiUretVeGoster()
    {
        var f = _uret();
        _denetim = f;
        f.Dock = DockStyle.Fill;
        Controls.Add(f);
        ModalKapatDugmeleriniGizle(f);
        f.Kapandi += (d, sonuc) =>
        {
            if (ReferenceEquals(_denetim, d)) _denetim = null;
            if (!IsDisposed) { Controls.Remove(d); DenetimKapandi?.Invoke(this, d, sonuc); }
            // Kapat düğmesinin kendi tıklama işleyicisi içindeyken atılmasın: işleyici bitince at.
            if (d.IsHandleCreated && !d.IsDisposed) d.BeginInvoke(new Action(() => { if (!d.IsDisposed) d.Dispose(); })); else d.Dispose();
        };
        f.SayfaAcildi();
        DenetimHazir?.Invoke(f);
    }

    // Yalnızca "Kapat" yazan (modal kapanış için konmuş) düğmeleri gizler. "Kapat (tek tık)" gibi gerçek işlem düğmelerine dokunmaz.
    internal static void ModalKapatDugmeleriniGizle(Control kok)
    {
        foreach (Control c in kok.Controls)
        {
            if (c is Button b && ModalKapatMi(b)) b.Visible = false;
            if (c.HasChildren) ModalKapatDugmeleriniGizle(c);
        }
    }

    // Düğme yalnızca pencereyi kapatmak için mi? (başındaki simge/boşluk sayılmaz)
    internal static bool ModalKapatMi(Button b)
    {
        string t = new string(b.Text.SkipWhile(ch => !char.IsLetter(ch)).ToArray()).Trim();
        return string.Equals(t, "Kapat", StringComparison.OrdinalIgnoreCase);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _denetim is { IsDisposed: false }) { try { _denetim.Dispose(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "DenetimSayfasi.Dispose"); } }
        base.Dispose(disposing);
    }
}
