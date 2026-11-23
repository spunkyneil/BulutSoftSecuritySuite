// Bu dosya YALNIZCA GÖRSEL/ARAYÜZ zamanlayıcıları için akıllı zamanlayıcıdır: radar çizimi, canlı trafik tablosu, küre imleç/ışık yoklaması, kart sayaçları gibi işler, sahibi olan kontrol
// ekranda görünmüyorsa (başka sayfa/sekme, gizli) ya da pencere simge durumundaysa DURAKLATILIR; görünür olunca kaldığı yerden sürer. Karar OLAY tabanlıdır (VisibleChanged, ParentChanged,
// HandleCreated, pencere boyut/durum değişimi) — pencere taraması yoktur.
// ÖNEMLİ: Güvenlik/koruma zamanlayıcıları (Nöbetçi Modu, tuzak/fidye izleme, koruma hizmeti yoklaması, zamanlanmış tarama, Brute-Force otomatik yenileme, Kali sağlık denetimi, bildirim sayacı)
// bu sınıfı KULLANMAZ: uygulama simge durumundayken veya sayfa gizliyken de kesintisiz çalışır (bir test bu ayrımı denetler).
// Üyeler System.Windows.Forms.Timer ile aynı adlardadır (Interval, Start, Stop, Enabled, Tick, Dispose) ki mevcut kod değişmeden geçsin.

// Programımızın adı.
namespace LabDenetim;

// Görünürlüğe duyarlı arayüz zamanlayıcısı.
internal sealed class GorunurZamanlayici : IDisposable
{
    private readonly System.Windows.Forms.Timer _t = new();
    private Control? _sahip; private Form? _form; private bool _istenen, _atildi;

    public GorunurZamanlayici() { _t.Tick += (_, e) => Tick?.Invoke(this, e); }

    // Zamanlayıcı tetiklendi.
    public event EventHandler? Tick;
    // Aralık (ms).
    public int Interval { get => _t.Interval; set => _t.Interval = value; }
    // Sahip tarafından ÇALIŞMASI istenmiş mi (Start/Stop). Gerçekten çalışıp çalışmadığı Calisiyor'dur.
    public bool Enabled { get => _istenen; set { if (value) Start(); else Stop(); } }
    // Şu an fiilen tetikleniyor mu (istenmiş VE sahip görünür VE pencere simge durumunda değil).
    public bool Calisiyor => _t.Enabled;

    // Görünürlüğü izlenecek kontrol (genellikle zamanlayıcıyı kullanan panel/form).
    public Control? Sahip
    {
        get => _sahip;
        set
        {
            if (ReferenceEquals(_sahip, value)) return;
            if (_sahip != null) { _sahip.VisibleChanged -= Degisti; _sahip.ParentChanged -= SahipParentDegisti; _sahip.HandleCreated -= Degisti; _sahip.HandleDestroyed -= Degisti; _sahip.Disposed -= SahipAtildi; }
            _sahip = value; FormuBagla();
            if (_sahip != null) { _sahip.VisibleChanged += Degisti; _sahip.ParentChanged += SahipParentDegisti; _sahip.HandleCreated += Degisti; _sahip.HandleDestroyed += Degisti; _sahip.Disposed += SahipAtildi; }
            Guncelle();
        }
    }

    public void Start() { _istenen = true; Guncelle(); }
    public void Stop() { _istenen = false; Guncelle(); }

    // Karar kuralı (saf; test edilir): istenmiş, sahip kurulu/görünür, bir pencereye bağlı ve pencere simge durumunda değil.
    internal static bool CalismaliMi(bool istenen, bool sahipVar, bool atildi, bool handle, bool gorunur, bool pencereVar, bool simgeDurumunda) =>
        istenen && sahipVar && !atildi && handle && gorunur && pencereVar && !simgeDurumunda;

    private void Guncelle()
    {
        if (_atildi) return;
        bool calis = _sahip == null ? _istenen : CalismaliMi(_istenen, true, _sahip.IsDisposed, _sahip.IsHandleCreated, _sahip.Visible, _sahip.FindForm() != null, _form?.WindowState == FormWindowState.Minimized);
        if (calis != _t.Enabled) _t.Enabled = calis;
    }

    private void Degisti(object? s, EventArgs e) => Guncelle();
    private void SahipParentDegisti(object? s, EventArgs e) { FormuBagla(); Guncelle(); }
    private void SahipAtildi(object? s, EventArgs e) { _t.Stop(); }

    // Pencere (simge durumu için) boyut/durum olaylarına bağlanır; sahip başka pencereye geçerse eskisinden ayrılır.
    private void FormuBagla()
    {
        var yeni = _sahip?.FindForm();
        if (ReferenceEquals(yeni, _form)) return;
        if (_form != null) { _form.Resize -= Degisti; _form.VisibleChanged -= Degisti; }
        _form = yeni;
        if (_form != null) { _form.Resize += Degisti; _form.VisibleChanged += Degisti; }
    }

    public void Dispose()
    {
        if (_atildi) return; _atildi = true;
        if (_sahip != null) { _sahip.VisibleChanged -= Degisti; _sahip.ParentChanged -= SahipParentDegisti; _sahip.HandleCreated -= Degisti; _sahip.HandleDestroyed -= Degisti; _sahip.Disposed -= SahipAtildi; }
        if (_form != null) { _form.Resize -= Degisti; _form.VisibleChanged -= Degisti; }
        _t.Stop(); _t.Dispose();
    }
}
