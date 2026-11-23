// Bu dosya KÜRESEL STATİK BAĞIMLILIKLARIN (Oturum.Aktif ve OlayDeposu) arayüz arkasına alındığı yerdir (test izolasyonu).
// Çekirdek servisler ve arayüz kabuğu (AppShell bileşenleri) oturumu ve olay günlüğünü doğrudan statik sınıflardan değil, ServisBaglami üzerindeki arayüzlerden kullanır;
// üretimde varsayılanlar mevcut statik sınıfa yönlenir (davranış aynı), testlerde sahte uygulama atanıp geri alınır (Sifirla). ArayuzModu ve TemaYonetici bu turda kapsam dışıdır.
// Core, UI paketine başvurmaz: burada yalnızca oturum/olay soyutlamaları vardır.

// Programımızın adı.
namespace LabDenetim;

// Geçerli oturum bilgisi (kim giriş yaptı, rolü ne).
internal interface IOturumBaglami
{
    // Etkin kullanıcı (giriş yoksa null). Yazılabilir: "Kullanıcı Değiştir" oturumu kapatır.
    Kullanici? Aktif { get; set; }
    // Etkin kullanıcının rolü (giriş yoksa null).
    Rol? Rol { get; }
}

// Olay günlüğünü OKUYAN taraf (küre ışığı, bildirim zili, durum panosu).
internal interface IOlayOkuyucu
{
    // En yeni N olay (en yeniden eskiye).
    List<OlayKaydi> SonOlaylar(int adet);
    // Pencere içindeki (örn. son 30 dk) Uyarı/Kritik olaylar.
    List<OlayKaydi> SonUyariKritik(TimeSpan pencere, DateTime simdi);
    // Sayfalı tam okuma (Olay Zaman Çizgisi): sayfa 0 = en yeni; döner: o sayfanın olayları ve toplam kayıt sayısı.
    (List<OlayKaydi> Sayfa, int Toplam) OkuSayfali(int sayfa, int boyut);
    // Yeni olay eklendiğinde (herhangi bir iş parçacığında) tetiklenir; parametre seviyedir.
    event Action<string> Eklendi;
}

// Olay günlüğüne YAZAN taraf.
internal interface IOlayYazici
{
    void Ekle(string tur, string seviye, string mesaj);
}

// Varsayılan: statik Oturum.
internal sealed class StatikOturumBaglami : IOturumBaglami
{
    public Kullanici? Aktif { get => Oturum.Aktif; set => Oturum.Aktif = value; }
    public Rol? Rol => Oturum.Aktif?.Rol;
}

// Varsayılan: statik OlayDeposu (artımlı okuma önbelleğiyle).
internal sealed class OlayDeposuBaglantisi : IOlayOkuyucu, IOlayYazici
{
    public List<OlayKaydi> SonOlaylar(int adet) => OlayDeposu.SonOlaylar(adet);
    public List<OlayKaydi> SonUyariKritik(TimeSpan pencere, DateTime simdi) => OlayDeposu.SonUyariKritik(pencere, simdi);
    public (List<OlayKaydi> Sayfa, int Toplam) OkuSayfali(int sayfa, int boyut) => OlayDeposu.OkuSayfali(sayfa, boyut);
    public event Action<string> Eklendi { add => OlayDeposu.Eklendi += value; remove => OlayDeposu.Eklendi -= value; }
    public void Ekle(string tur, string seviye, string mesaj) => OlayDeposu.Ekle(tur, seviye, mesaj);
}

// Servis erişim noktası: varsayılanlar statik sınıflara yönlenir; testler atayıp Sifirla ile geri alır.
internal static class ServisBaglami
{
    private static readonly OlayDeposuBaglantisi VarsayilanOlay = new();
    public static IOturumBaglami Oturum { get; set; } = new StatikOturumBaglami();
    public static IOlayOkuyucu OlayOkuyucu { get; set; } = VarsayilanOlay;
    public static IOlayYazici OlayYazici { get; set; } = VarsayilanOlay;

    // Varsayılanlara döner (test sonu).
    public static void Sifirla() { Oturum = new StatikOturumBaglami(); OlayOkuyucu = VarsayilanOlay; OlayYazici = VarsayilanOlay; }
}
