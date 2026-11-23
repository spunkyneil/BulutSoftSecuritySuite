// Kurumsal kabuğun (AppShell) "hangi eski ekran parçası / hangi eylem" bağlantı tablosu. Eskiden Form1.KabukBagla'nın içindeydi; artık Form1 yalnızca denetleyicilerini verir,
// sayfa/sekme bağlantılarının listesi burada durur (sayfa, sekme, yetki ve köprü mantığı Kabuk\ klasöründedir; CLAUDE.md kural 2).

// Programımızın adı.
namespace LabDenetim;

// Kabuk bağlantı fabrikası.
internal sealed class KabukBaglantiFabrikasi
{
    private readonly TemaliForm _f; private readonly TaramaSayfasi _tarama; private readonly SavunmaDurumServisi _savunma; private readonly KolayModEylemleri _kolayMod; private readonly NobetciEylemi _nobet; private readonly KisayolEylemleri _kisayol; private readonly OturumEylemleri _oturum; private readonly YardimEylemleri _yardim; private readonly Action<string, string?, bool> _kabukGit;
    // f: ana pencere; kabukGit: kabukta sayfaya (sekmeye) gitme eylemi; diğerleri: bağlantıların çağıracağı denetleyiciler.
    public KabukBaglantiFabrikasi(TemaliForm f, TaramaSayfasi tarama, SavunmaDurumServisi savunma, KolayModEylemleri kolayMod, NobetciEylemi nobet, KisayolEylemleri kisayol, OturumEylemleri oturum, YardimEylemleri yardim, Action<string, string?, bool> kabukGit)
    { _f = f; _tarama = tarama; _savunma = savunma; _kolayMod = kolayMod; _nobet = nobet; _kisayol = kisayol; _oturum = oturum; _yardim = yardim; _kabukGit = kabukGit; }

    // Klasik radar ve harita küresi için kab; Durum Panosu › Radar / Küre sekmesinde durur.
    public Panel RadarAlani(UzmanKureYoneticisi kure)
    {
        var radarAlani = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        radarAlani.Controls.Add(_tarama.radar); radarAlani.Controls.Add(kure._uzmanKure!); radarAlani.Controls.Add(kure._kureSerit!); radarAlani.Controls.SetChildIndex(kure._kureSerit!, radarAlani.Controls.Count - 1);
        return radarAlani;
    }

    // Kabuk bağlantı tablosunu kurar.
    public KabukBaglantilari Olustur(Panel radarAlani)
    {
        return new KabukBaglantilari
        {
            TaramaAlani = _tarama, RadarAlani = radarAlani, KolayPanel = _kolayMod._kolay, Savunma = _savunma,
            GorunumSec = _tarama.GorunumSec, KolayAnaGoster = () => _kolayMod._kolay.AnaGoster(), AcilTikla = () => _tarama._acil.Degistir(), NobetTikla = () => _nobet.NobetModunuDegistir(_f, EventArgs.Empty),
            AcilDurumuYenile = _tarama._acil.Yenile, KolayGostergeleriYenile = _kolayMod.GostergeleriYenile, SensorYerYenile = _tarama._sensorYeri.Yenile, KayitYukle = _tarama.YukleKayit, ModAyarla = ArayuzModu.Ayarla, KomutPaleti = _kisayol.KomutPaletiAc,
            DurumFormu = () => new DurumFormu(_tarama._bulgular.ToList(), _tarama.txtHedef.Text.Trim(), _tarama.PuanHesapla(), _tarama._sonTarih), OlayFormu = () => new OlayFormu(), GecmisFormu = () => new GecmisFormu(),
            CihazFormu = () => new CihazFormu(m => _tarama.Gunluk(m, m.Contains("ALARM") ? TaramaSayfasi.RakamKirmizi : TaramaSayfasi.Elektrik)),
            HaritaFormu = () => new AgHaritasiFormu(m => _tarama.Gunluk(m, TaramaSayfasi.Elektrik), liste => { foreach (var b in liste) if (!_tarama._bulgular.Any(x => x.Modul == b.Modul && x.Baslik == b.Baslik)) _tarama.Ekle(b); }, _kolayMod._kali.DisAgPasifeGec),
            KaliciFormu = () => new KaliciFormu(m => _tarama.Gunluk(m, m.StartsWith("KRİTİK") ? TaramaSayfasi.RakamKirmizi : TaramaSayfasi.Elektrik)), KalkanFormu = () => new KalkanFormu(),
            TuzakFormu = () => new TuzakFormu(_tarama._tuzak.Honeypot, _tarama._tuzak.Canary, _tarama._tuzak.Fidye, m => _tarama.Gunluk(m, TaramaSayfasi.Elektrik)), GozetimsizFormu = () => new GozetimsizFormu(m => _tarama.Gunluk(m, TaramaSayfasi.Elektrik)),
            BruteFormu = () => new BruteForceFormu(m => _tarama.Gunluk(m, m.StartsWith("KRİTİK") ? TaramaSayfasi.RakamKirmizi : TaramaSayfasi.Elektrik)), AracFormu = () => new AracKutusuFormu(m => _tarama.Gunluk(m, m.StartsWith("KRİTİK") ? TaramaSayfasi.RakamKirmizi : TaramaSayfasi.Elektrik)),
            SensorFormu = () => new SensorFormu(m => _tarama.Gunluk(m, m.Contains("UYARI") ? TaramaSayfasi.RakamKirmizi : TaramaSayfasi.Elektrik)),
            MerkezFormu = () => new MerkezFormu(_kolayMod._kali._merkezSekme, m => _tarama.Gunluk(m, TaramaSayfasi.Elektrik), () => _kabukGit("lab", "sensor", false), () => _kabukGit("ag", "harita", false)),
            HtmlRapor = () => { KullaniciGunlugu.Yaz("HTML rapor oluşturmayı başlattı"); _tarama.btnHtml_Click(_tarama.btnHtml, EventArgs.Empty); }, PdfRapor = () => { KullaniciGunlugu.Yaz("PDF rapor oluşturmayı başlattı"); _tarama.btnPdf_Click(_tarama.btnPdf, EventArgs.Empty); }, YoneticiOzeti = _kolayMod.KolayRaporAl, KanitPaketi = () => { KullaniciGunlugu.Yaz("Adli kanıt paketi oluşturmayı başlattı"); _tarama._kanit.Tikla(); },
            SiemCef = () => SiemDisaAktar.Yaz(_f, true, _tarama._bulgular, _tarama.txtHedef.Text.Trim(), _tarama._sonTarih), SiemJson = () => SiemDisaAktar.Yaz(_f, false, _tarama._bulgular, _tarama.txtHedef.Text.Trim(), _tarama._sonTarih),
            Hesap = () => _oturum.HesapAc(), Lisans = () => _oturum.LisansAc(), KullaniciDegistir = () => _oturum.OturumDegistir(),
            Yardim = () => _yardim.Yardim(), Tur = () => _yardim.TurAc(), Hakkinda = () => _yardim.HakkindaAc(), Cikis = () => _f.Close()
        };
    }
}
