// Bu dosya eski "Form" ekranlarının (Durum, Olay, Geçmiş, Cihaz, Harita, Kalıcılık, Kalkan, Tuzak, Gözetimsiz, Giriş Radarı, Araç Kutusu, Sensör, Merkez) GERÇEK UserControl sayfa tabanıdır (D5).
// Eskiden bu ekranlar TemaliForm idi ve FormSayfaAdaptoru ile "TopLevel=false" yapılıp kabuğa gömülüyordu; artık doğrudan kabuğun çalışma alanına konan denetimlerdir.
// Sınıf, eski kodun kullandığı küçük "Form benzeri" yüzeyi (Close, DialogResult, Shown, FormClosing/FormClosed, StartPosition) korur; böylece 3000 satırlık ekran mantığı değişmeden taşınır.
// Renk/yazı tipi kuralları aynıdır: tema, ana pencerenin kaydı üzerinden alt denetimlere uygulanır (CLAUDE.md kural 3-4).

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
using System.ComponentModel;

// Programımızın adı.
namespace LabDenetim;

// Kabuk sayfası olarak çalışan eski ekranların ortak tabanı.
internal abstract class SayfaDenetimi : UserControl
{
    private bool _kapandi, _acildi;

    // Pencere konumu ipucu (sayfa olduğu için yok sayılır; eski kurucu kodu uyumu için durur).
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FormStartPosition StartPosition { get; set; }

    // En az içerik boyutu: sayfa kabuğun çalışma alanına konduğu için eski Form.MinimumSize gibi sayfayı büyütüp KESMEZ; alan küçükse sayfa KAYDIRILIR (AutoScrollMinSize).
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size MinimumSize { get => AutoScrollMinSize; set { AutoScrollMinSize = value; AutoScroll = value != Size.Empty; } }

    // Ekranın "sonucu" (eski ShowDialog dönüşü): Geçmiş "Ekrana Yükle", Harita "pasif gözleme geç" gibi akışlar bunu okur.
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DialogResult DialogResult { get; set; }

    // Sayfa ilk kez gösterilince (kabuk sayfayı açınca) bir kez çalışır: ilk veri yükleme, odak temizleme gibi işler.
    public event EventHandler? Shown;
    // Sayfa kapanırken (Close ya da atılma) bir kez çalışır: arka plan işlerini iptal etmek için.
    public event EventHandler? FormClosing;
    // Sayfa kapandıktan sonra (Close ya da atılma) bir kez çalışır: zamanlayıcı/olay aboneliklerini bırakmak için.
    public event EventHandler? FormClosed;
    // Sayfa kendini kapatınca (düğmeyle ya da DialogResult ile) barındıran sayfa tutucusuna haber verir.
    internal event Action<SayfaDenetimi, DialogResult>? Kapandi;

    // Sayfayı kapatır (eski Form.Close karşılığı).
    public void Close()
    {
        if (_kapandi) return; _kapandi = true;
        FormClosing?.Invoke(this, EventArgs.Empty); FormClosed?.Invoke(this, EventArgs.Empty);
        Kapandi?.Invoke(this, DialogResult);
    }

    // Barındıran tutucu sayfayı ekrana koyduktan sonra çağırır (bir kez): Shown olayını verir.
    internal void SayfaAcildi() { if (_acildi) return; _acildi = true; Shown?.Invoke(this, EventArgs.Empty); }

    // Kapatılmadan atılırsa (sayfa yenilenirken ya da pencere kapanırken) kapanış olayları yine bir kez çalışır.
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_kapandi) { _kapandi = true; FormClosing?.Invoke(this, EventArgs.Empty); FormClosed?.Invoke(this, EventArgs.Empty); }
        base.Dispose(disposing);
    }
}
