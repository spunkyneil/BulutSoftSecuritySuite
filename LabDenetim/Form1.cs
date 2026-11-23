// Bu dosya, butonlara basılınca ne olacağını anlatan kodları içerir.
// Raporu tarayıcıda açmak için gerekli araç.
using System.Diagnostics;
// Denetim modüllerini (kapı yoklama, web koruması vb.) kullanmak için ilgili klasörü çağır.
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Ana pencere: programı açınca gördüğünüz ekran.
public partial class Form1 : TemaliForm, ITaramaSahibi
{
    // ---- ITaramaSahibi: tarama ekranının bu pencereden gördüğü dar yüzey ----
    IWin32Window ITaramaSahibi.Pencere => this;
    bool ITaramaSahibi.PencereHazir => IsHandleCreated && !IsDisposed;
    void ITaramaSahibi.UiIsle(Action is_) { if (IsHandleCreated && !IsDisposed) BeginInvoke(is_); }
    void ITaramaSahibi.TepsiBalonu(int ms, string baslik, string metin, bool uyari) { var b = _nobet?.bildirim; if (b != null && b.Visible) b.ShowBalloonTip(ms, baslik, metin, uyari ? ToolTipIcon.Warning : ToolTipIcon.Info); }
    void ITaramaSahibi.KapanirkenCalistir(Action is_) => FormClosed += (_, _) => is_();
    void ITaramaSahibi.DurumYaz(string metin) => lblDurum.Text = metin;
    void ITaramaSahibi.DurumZemini(Color renk) => tblDurum.BackColor = renk;
    void ITaramaSahibi.DurumYazisiRengi(Color renk) => lblDurum.ForeColor = renk;
    bool ITaramaSahibi.KabukHazir => _kabuk != null;
    async Task<bool> ITaramaSahibi.SayfayaGitAsync(string sayfa, string? sekme) => _kabuk != null && await _kabuk.GitAsync(sayfa, sekme);
    SavunmaDurumServisi ITaramaSahibi.Savunma => _savunma;
    void ITaramaSahibi.KolayGostergeleriYenile() => _kolayMod?.GostergeleriYenile();
    void ITaramaSahibi.KureYansit() => _kolayMod?.KureYansit();
    void ITaramaSahibi.UzmanKureKur() => _kure.Kur();
    void ITaramaSahibi.UzmanKureGorunum(Gorunum g) => _kure.GorunumDegisti(g);
    bool ITaramaSahibi.HaritaKureAcik => _kure._haritaKure;


    // Savunma durumu (Acil İzolasyon + Nöbetçi) tek kaynağı: olay tabanlı servis; Savunma Merkezi çubuğu, tepsi ve küre ona abone olur.
    internal readonly SavunmaDurumServisi _savunma = new();
    public Form1()
    {
        // Ekrandaki butonları ve kutuları çiz.
        InitializeComponent();
        // Dışarı taşınan özellikler (Servisler\*Eylemi) tarama ekranında kurulur: bileşenler oluştuktan hemen sonra, ilk kullanımdan önce.
        _tarama.EylemleriKur();
        _yardim = new YardimEylemleri(this, () => { if (_tarama._analizIptal == null && _tarama.SecimBulgusu() is Bulgu secili) _tarama.AsistanGoster(secili); });
        // BulutSoft kıyafetini (koyu tema, marka başlığı, imza) giydir.
        _tarama.TemaDuzeniniKur();
        // Ekranın üstüne 4 istatistik kartını ekle.
        _tarama.KartlariKur();
        // Ekranın altına siyah "Canlı Sistem Günlüğü" kutusunu ekle.
        _tarama.GunlukKur();
        // Profil kutusunu çalıştır ve "Standart" profili uygula.
        _tarama.ProfilKur();
        // Sağdaki Siber İlk Yardım Asistanı'nı başlangıç yazısıyla doldur.
        _tarama.AsistanSifirla();
        // Mod seçiciyi (Yerel & Manuel / Claude API) başlat ve son kullanılan modu geri yükle.
        _tarama.YapayZekaKur();
        // Filtre kutularına (YEŞİL / SARI / KIRMIZI) tıklamayı bağla.
        _tarama.FiltreKur();
        // Canlı Ağ ve Port Radarı'nı kur; açılışta radar (Panel) görünümü seçili olsun.
        _tarama.TrafikKur();
        // Sistem tepsisi ve nöbetçi modunu hazırla.
        _kure = new UzmanKureYoneticisi(this, _tarama, (s, k, z) => KabukGit(s, k, z), () => _kolayMod?.KureYansit(), () => _nobet.KalkanTiklandiAsync());
        _nobet = new NobetciEylemi(this, components!, _tarama, _savunma, () => _kolayMod?.KureYansit(), () => _kolayMod?.GostergeleriYenile()); _nobet.Kur();
        // Acil izolasyon düğmesini güvenlik duvarının gerçek durumuna göre boya.
        _tarama._acil.Yenile();
        // USB takma/çıkarma dinleyicisini (USB Kalkanı) başlat.
        _tarama._usb.Kur();
        // Tuzak (honeypot, canary, fidye alarmı) olaylarını bağla.
        _tarama._tuzak.Kur();
        // Arama kutusunu ve "kabul edilen risk" menüsünü bağla.
        _tarama._arama.Kur();
        // Altın Profil durum yazısını doldur.
        _tarama._altin.DurumYaz();
        // Kısayolları ve zamanlayıcıyı kur.
        PencereKur();
        _tarama.GorunumKur();
        // Kurumsal kabuğu (sol menü, üst çubuk, sayfalar) bağla; eski ekran parçaları sayfalara taşınır.
        KabukBagla();
        // Tüm yazılarda "&" işareti kaybolmasın.
        TaramaSayfasi.MnemonicKapat(this);
        // Pencere ekrana ilk geldiğinde, daha önce gösterilmediyse tanıtım turunu göster.
        Shown += (_, _) => _yardim.TurIlkAcilista();
    }

    // (Tabloda satır seçilince çalışan kod artık Asistan.cs dosyasındadır.)

    // ---- Kurumsal kabuk (AppShell) bağlantıları ----
    // Bağlantı tablosu Kabuk\KabukBaglantiFabrikasi'ndedir; sayfa, sekme, yetki ve köprü mantığı Kabuk\ klasöründedir (CLAUDE.md kural 2).
    internal AppShell? _kabuk;
    internal UzmanKureYoneticisi _kure = null!;   // Uzman Görünüm harita küresi (Servisler\UzmanKureYoneticisi)
    internal KolayModEylemleri _kolayMod = null!;   // Kolay Görünüm kartları, küre yansıtma, Kali/Merkez göstergeleri (Servisler\KolayModEylemleri, KaliGostergeleri)
    internal KisayolEylemleri _kisayol = null!;   // Komut Paleti, zamanlama, başarılar, eğitim modu (Servisler\KisayolEylemleri)
    internal NobetciEylemi _nobet = null!;      // Nöbetçi modu, tepsi, koruma hizmeti izleme (Servisler\NobetciEylemi)
    internal YardimEylemleri _yardim = null!;   // Yardım / Hakkımızda / Tanıtım Turu (Kabuk\YardimEylemleri)
    internal OturumEylemleri _oturum = null!;   // kullanıcı menüsü eylemleri (Kabuk\OturumEylemleri)

    internal void KabukBagla()
    {
        // Klasik radar ve harita küresi, Durum Panosu › Radar / Küre sekmesinde kendi kabında durur.
        var fabrika = new KabukBaglantiFabrikasi(this, _tarama, _savunma, _kolayMod, _nobet, _kisayol, _oturum, _yardim, (s, k, z) => KabukGit(s, k, z)); var radarAlani = fabrika.RadarAlani(_kure);
        _kabuk = KabukKompozisyonu.Kur(fabrika.Olustur(radarAlani), ArayuzModu.Mevcut);
        tblKok.Controls.Add(_kabuk, 0, 0); tblKok.SetColumnSpan(_kabuk, 2);
        // Henüz bir sayfaya yerleşmemiş (hiç açılmamış) eski parçalar da pencereyle birlikte atılır.
        Disposed += (_, _) => { _kolayMod._kolay.Dispose(); radarAlani.Dispose(); _tarama.Dispose(); foreach (var z in new IDisposable[] { _kisayol._zamanSayaci, _nobet._nobetZaman, _nobet._hizmetZaman, _kolayMod._kali._kaliZaman, _tarama._trafikZaman, _kure._isikSaat, _tarama._zamanlayici }) z.Dispose(); };   // form alanı olan zamanlayıcılar pencereyle birlikte atılır
        Shown += (_, _) => KabukGit("durum");
    }

    // Pencere düzeyi bağlantılar (kısayollar, oturum başlığı, pencere hafızası, zamanlayıcı, mod): ana pencere açılırken bir kez çağrılır.
    internal void PencereKur()
    {
        // Açılışta ayar dosyalarının mührünü kontrol et (mühür yoksa sessiz).
        Shown += (_, _) => MuhurKontrolu.AcilistaDenetle(_tarama);
        // Pencere konumunu hatırla.
        PencereHafizasi.Bagla(this);
        // Programdan çıkış (menüden ya da pencere kapatılarak) kullanıcı günlüğüne yazılır.
        FormClosed += (_, _) => KullaniciGunlugu.Yaz("Programdan çıktı");
        // Başlıkta giriş yapan kullanıcıyı göster.
        _oturum = new OturumEylemleri(this, () => !_tarama.btnBaslat.Enabled, m => _tarama.Gunluk(m, TaramaSayfasi.Elektrik), TaramaSayfasi.PencereBasligi); _oturum.BaslikYaz();
        // Ana ekrana "Tarama yeri" seçimini ekle.
        _tarama._sensorYeri.Kur();
        // Komut Paleti, zamanlanmış tarama, başarılar ve eğitim modu; her dakika zamanlamaya bakılır.
        _kisayol = new KisayolEylemleri(this, _tarama, (s, k, z) => KabukGit(s, k, z), _yardim, _oturum, _nobet); _kisayol.SayaciBaslat();
        // Tema değişince pencere yeniden çizilsin (yeni kabuk bileşenleri kendi renklerini DesignTokens'tan alır).
        TemaYonetici.Degisti += () => Invalidate(true);
        // Kolay/Uzman görünüm geçişi (en son kurulur).
        _kolayMod = new KolayModEylemleri(this, _tarama, (s, k, z) => KabukGit(s, k, z), () => _kabuk, _nobet, _kure); _kolayMod.Kur();
    }

    // Tuş kombinasyonlarını yakalar.
    protected override bool ProcessCmdKey(ref Message msg, Keys tus)
    {
        switch (tus)
        {
            // F5: taramayı başlat (boştaysa).
            case Keys.F5: _tarama.TaramaKisayolu(); return true;
            // Ctrl+F: sonuç tablosunda ara.
            case Keys.Control | Keys.F: _tarama.SonucAraKisayolu(); return true;
            // Ctrl+K: komut paleti.
            case Keys.Control | Keys.K: _kisayol.KomutPaletiAc(); return true;
            // Ctrl+D: durum panosu.
            case Keys.Control | Keys.D: KabukGit("durum", "pano"); return true;
            // Ctrl+E: olay zaman çizgisi.
            case Keys.Control | Keys.E: KabukGit("olay", null, true); return true;
            // F1: yardım.
            case Keys.F1: _yardim.Yardim(); return true;
        }
        // Diğer tuşlar normal çalışsın.
        return base.ProcessCmdKey(ref msg, tus);
    }

    // Kabukta sayfaya (ve isteğe bağlı sekmeye) gider; komut paleti, kısayollar, zil ve menü hep bu yolu kullanır.
    internal void KabukGit(string sayfa, string? sekme = null, bool zorla = false) { if (_kabuk != null) _ = _kabuk.GitAsync(sayfa, sekme, zorla); }

}
