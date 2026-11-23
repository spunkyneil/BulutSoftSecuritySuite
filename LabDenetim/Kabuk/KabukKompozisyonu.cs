// Bu dosya KABUĞUN KOMPOZİSYON KÖKÜDÜR: Form1'in verdiği eski ekran parçalarını ve eylemleri (KabukBaglantilari) alır; 10 sayfayı, sayfa içi sekmeleri, Savunma Merkezi üst çubuğunu,
// Raporlar sayfasını, üst çubuk olaylarını ve ShowDialog dönüşüne bağlı iş akışı köprülerini kurar ve AppShell döndürür.
// Böylece Form1'e yeni iş mantığı girmez: Form1 yalnızca "hangi kontrol / hangi eylem" bağlantılarını listeler (CLAUDE.md kural 2).
//
// Köprüler (eski modal akışlar kopmasın):
//  • Geçmiş › "Ekrana Yükle" → Form1.YukleKayit + Tarama Merkezi › Yeni Tarama + Asistan sekmesine geçiş (Kolay modda Uzman'a geçilir).
//  • Aktif Kalkanlarım'da kural kaldırılınca / Savunma Merkezi açılınca Acil İzolasyon durumu tazelenir.
//  • Sensör & Bildirim Merkezi kapanınca Kolay göstergeler ve bildirim rozeti tazelenir; Uzak Sensörler kapanınca ana ekrandaki sensör seçimi yenilenir.
//  • Ağ Haritası "pasif gözleme geç" deyip kapanınca Tarama Merkezi açılır.

// Programımızın adı.
namespace LabDenetim;

// Form1'in kabuğa verdiği bağlantılar (eski ekran parçaları ve eylemler).
internal sealed class KabukBaglantilari
{
    // ---- Eski ekran parçaları ----
    public required Control TaramaAlani { get; init; }    // hedef çubuğu, kartlar, tablo + asistan, günlük (eski sağ çalışma alanı)
    public required Control RadarAlani { get; init; }     // klasik radar + harita küresi
    public required Control KolayPanel { get; init; }     // Kolay Görünüm paneli
    public required SavunmaDurumServisi Savunma { get; init; }   // Acil İzolasyon + Nöbetçi durumu (olay tabanlı servis)
    // ---- Eski işleyiciler ----
    public required Action<Gorunum> GorunumSec { get; init; }
    public required Action KolayAnaGoster { get; init; }
    public required Action AcilTikla { get; init; }
    public required Action NobetTikla { get; init; }
    public required Action AcilDurumuYenile { get; init; }
    public required Action KolayGostergeleriYenile { get; init; }
    public required Action SensorYerYenile { get; init; }
    public required Action<GecmisKayit> KayitYukle { get; init; }
    public required Action<ModTuru> ModAyarla { get; init; }
    public required Action KomutPaleti { get; init; }
    // ---- Eski pencerelerin üreticileri (sayfaya gömülür) ----
    public required Func<SayfaDenetimi> DurumFormu { get; init; }
    public required Func<SayfaDenetimi> OlayFormu { get; init; }
    public required Func<SayfaDenetimi> GecmisFormu { get; init; }
    public required Func<SayfaDenetimi> CihazFormu { get; init; }
    public required Func<SayfaDenetimi> HaritaFormu { get; init; }
    public required Func<SayfaDenetimi> KaliciFormu { get; init; }
    public required Func<SayfaDenetimi> KalkanFormu { get; init; }
    public required Func<SayfaDenetimi> TuzakFormu { get; init; }
    public required Func<SayfaDenetimi> GozetimsizFormu { get; init; }
    public required Func<SayfaDenetimi> BruteFormu { get; init; }
    public required Func<SayfaDenetimi> AracFormu { get; init; }
    public required Func<SayfaDenetimi> SensorFormu { get; init; }
    public required Func<SayfaDenetimi> MerkezFormu { get; init; }
    // ---- Raporlar ----
    public required Action HtmlRapor { get; init; }
    public required Action PdfRapor { get; init; }
    public required Action YoneticiOzeti { get; init; }
    public required Action KanitPaketi { get; init; }
    public required Action SiemCef { get; init; }
    public required Action SiemJson { get; init; }
    // ---- Kullanıcı menüsü (modal pencereler) ----
    public required Action Hesap { get; init; }
    public required Action Lisans { get; init; }
    public required Action KullaniciDegistir { get; init; }
    public required Action Yardim { get; init; }
    public required Action Tur { get; init; }
    public required Action Hakkinda { get; init; }
    public required Action Cikis { get; init; }
}

// Kabuğu kurar.
internal static class KabukKompozisyonu
{
    // Kabuğu ve tüm sayfalarını kurar. Başlangıç sayfasına geçmek çağıranın işidir (GitAsync("durum")).
    public static AppShell Kur(KabukBaglantilari k, ModTuru baslangicModu)
    {
        var shell = new AppShell(); BildirimKaynagi? bildirim = null;
        DenetimSayfasi Form(string id, string baslik, NavGrup grup, Func<SayfaDenetimi> uret, bool yenile) => new(id, baslik, grup, uret) { HerAcilistaYenile = yenile };
        SekmeliSayfa Sekmeli(string id, IDictionary<string, IModulePage> icerik, Control? ust = null) => new(NavigasyonModeli.Bul(id)!, new Dictionary<string, IModulePage>(icerik), ust);

        // 1. GENEL BAKIŞ
        var kolayPanel = new BolumSayfasi("durum", "Durum Panosu", NavGrup.GenelBakis, () => k.KolayPanel, acildi: k.KolayAnaGoster, enAzIcerik: new Size(0, 640));
        var radar = new BolumSayfasi("durum/radar", "Radar / Küre", NavGrup.GenelBakis, () => k.RadarAlani, acildi: () => k.GorunumSec(Gorunum.Radar));
        var pano = Form("durum/pano", "Durum Ayrıntıları", NavGrup.GenelBakis, k.DurumFormu, yenile: true);
        shell.SayfaKaydet(new KolayUzmanSayfasi(NavigasyonModeli.Bul("durum")!, kolayPanel, Sekmeli("durum", new Dictionary<string, IModulePage> { ["radar"] = radar, ["pano"] = pano })));
        shell.SayfaKaydet(Form("olay", "Olay Zaman Çizgisi", NavGrup.GenelBakis, k.OlayFormu, yenile: true));

        // 2. DENETİM & KEŞİF
        var yeni = new BolumSayfasi("tarama/yeni", "Yeni Tarama + Asistan", NavGrup.DenetimKesif, () => k.TaramaAlani, acildi: () => k.GorunumSec(Gorunum.Tarama), enAzIcerik: new Size(0, 740));
        var gecmis = Form("tarama/gecmis", "Geçmiş Operasyonlar", NavGrup.DenetimKesif, k.GecmisFormu, yenile: true);
        gecmis.DenetimKapandi += (_, f, sonuc) => GuvenliCalistir.Baslat(async () => {
            // "Ekrana Yükle": kaydı ana ekrana getir ve kullanıcıyı Yeni Tarama + Asistan sekmesine geçir (Kolay modda Uzman'a geçilir; sekme orada).
            if (sonuc != DialogResult.OK || f is not GecmisFormu g || g.Secilen is not { } kayit) return;
            if (ArayuzModu.Mevcut == ModTuru.Kolay) k.ModAyarla(ModTuru.Uzman);
            k.KayitYukle(kayit); await shell.GitAsync("tarama", "yeni");
        }, "KabukKompozisyonu");
        shell.SayfaKaydet(Sekmeli("tarama", new Dictionary<string, IModulePage> { ["yeni"] = yeni, ["gecmis"] = gecmis }));
        var harita = Form("ag/harita", "Ağ Haritası", NavGrup.DenetimKesif, k.HaritaFormu, yenile: false);
        harita.DenetimKapandi += (_, _, _) => GuvenliCalistir.Baslat(() => shell.GitAsync("tarama", "yeni"), "KabukKompozisyonu.harita");   // "pasif gözleme geç" derse form kendini kapatır; sonuçlar Tarama Merkezi'nde görünür
        shell.SayfaKaydet(Sekmeli("ag", new Dictionary<string, IModulePage> { ["cihaz"] = Form("ag/cihaz", "Cihaz Envanteri", NavGrup.DenetimKesif, k.CihazFormu, yenile: false), ["harita"] = harita }));
        shell.SayfaKaydet(Form("kalici", "Kalıcılık Denetçisi", NavGrup.DenetimKesif, k.KaliciFormu, yenile: false));

        // 3. AKTİF SAVUNMA
        var kalkan = Form("savunma/kalkan", "Aktif Kalkanlarım", NavGrup.AktifSavunma, k.KalkanFormu, yenile: true);
        kalkan.DenetimHazir = f => { if (f is KalkanFormu kf) kf.KuralKaldirildi += k.AcilDurumuYenile; };   // kural kaldırılınca Acil İzolasyon durumu anında tazelenir
        var ust = new SavunmaUstCubuk(k.Savunma, k.AcilTikla, k.NobetTikla);
        shell.SayfaKaydet(Sekmeli("savunma", new Dictionary<string, IModulePage>
        {
            ["kalkan"] = kalkan, ["tuzak"] = Form("savunma/tuzak", "Tuzaklar & Fidye", NavGrup.AktifSavunma, k.TuzakFormu, yenile: false),
            ["nobet"] = Form("savunma/nobet", "Nöbetçi & Politika", NavGrup.AktifSavunma, k.GozetimsizFormu, yenile: false), ["brute"] = Form("savunma/brute", "Giriş Saldırı Radarı", NavGrup.AktifSavunma, k.BruteFormu, yenile: false)
        }, ust));
        shell.SayfaKaydet(new BolumSayfasi("trafik", "Canlı Ağ Trafiği", NavGrup.AktifSavunma, () => k.TaramaAlani, acildi: () => k.GorunumSec(Gorunum.Trafik), sadeceUzman: true, enAzIcerik: new Size(0, 740)));

        // 4. RAPORLAMA & ARAÇLAR
        shell.SayfaKaydet(new RaporlarSayfasi(k.HtmlRapor, k.PdfRapor, k.YoneticiOzeti, k.KanitPaketi, k.SiemCef, k.SiemJson));
        shell.SayfaKaydet(Form("arac", "Güvenlik Araç Kutusu", NavGrup.RaporlamaAraclar, k.AracFormu, yenile: false));
        var sensor = Form("lab/sensor", "Uzak Sensörler (VM)", NavGrup.RaporlamaAraclar, k.SensorFormu, yenile: false);
        sensor.DenetimKapandi += (_, _, _) => k.SensorYerYenile();
        var merkez = Form("lab/merkez", "Sensör & Bildirim Merkezi", NavGrup.RaporlamaAraclar, k.MerkezFormu, yenile: true);   // istenen sekmeyle (Kali/IP sızıntı/bildirim) her açılışta kurulur
        merkez.DenetimKapandi += (_, _, _) => { k.KolayGostergeleriYenile(); bildirim?.Yenile(); };
        merkez.Kapanirken = () => { k.KolayGostergeleriYenile(); bildirim?.Yenile(); };   // başka sayfaya/sekmeye geçerken de (yalnızca form kapanırken değil) tazelenir
        shell.SayfaKaydet(Sekmeli("lab", new Dictionary<string, IModulePage> { ["sensor"] = sensor, ["merkez"] = merkez }));

        // Üst çubuk olayları.
        shell.Ust.AramaTiklandi += k.KomutPaleti;
        shell.Ust.TemaTiklandi += () => { TemaYonetici.Degistir(); KullaniciGunlugu.Yaz("Temayı " + (TemaYonetici.Mod == TemaModu.Koyu ? "koyu" : "açık") + " yaptı"); };
        shell.Ust.ZilTiklandi += () => GuvenliCalistir.Baslat(() => shell.GitAsync("olay", null, zorla: true), "KabukKompozisyonu.zil");
        shell.Ust.KullaniciMenuSecildi += o =>
        {
            switch (o)
            {
                case KullaniciMenuOgesi.Hesap: k.Hesap(); shell.RolYenile(); break;
                case KullaniciMenuOgesi.Lisans: k.Lisans(); break;
                case KullaniciMenuOgesi.KullaniciDegistir: k.KullaniciDegistir(); break;
                case KullaniciMenuOgesi.Yardim: k.Yardim(); break;
                case KullaniciMenuOgesi.Tur: k.Tur(); break;
                case KullaniciMenuOgesi.Cikis: k.Cikis(); break;
                default: k.Hakkinda(); break;
            }
        };
        shell.ModIstegi += k.ModAyarla;
        // Savunma Merkezi her açılışta Acil İzolasyon durumunu güvenlik duvarından tazeler (kurallar program dışında da değişebilir).
        string? sonSayfa = null;
        shell.SayfaDegisti += id => { if (id == "savunma") k.AcilDurumuYenile(); if (id != sonSayfa) { sonSayfa = id; KullaniciGunlugu.Yaz((NavigasyonModeli.Bul(id)?.Baslik ?? id) + " sayfasını açtı"); } };
        // Bildirim zili: olay tabanlı sayaç (son 30 dk Uyarı/Kritik).
        bildirim = new BildirimKaynagi((n, kritik, _) => shell.Ust.BildirimAyarla(n, kritik)); shell.Disposed += (_, _) => bildirim.Dispose(); bildirim.Yenile();
        shell.Mod = baslangicModu;
        return shell;
    }
}
