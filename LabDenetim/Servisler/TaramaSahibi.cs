// Tarama ekranının BARINDIĞI pencereden (Form1) ihtiyaç duyduğu dar yüzey. Eskiden tarama ekranı Form1'e doğrudan başvuruyordu (Ana); artık yalnızca bu arayüzü bilir,
// böylece ekran sahte bir sahiple (pencere olmadan) kurulup sınanabilir. Form1 arayüzü Form1.cs içinde açık (explicit) uygular.

// Programımızın adı.
namespace LabDenetim;

// Tarama ekranının sahibi (ana pencere).
internal interface ITaramaSahibi
{
    // Ana pencere (iletişim kutularının sahibi).
    IWin32Window Pencere { get; }
    // Pencere hazır mı (tutamaç var, atılmamış)?
    bool PencereHazir { get; }
    // İşi pencerenin arayüz iş parçacığında çalıştırır (pencere hazırsa).
    void UiIsle(Action is_);
    // Sistem tepsisi simgesi görünürse balon gösterir.
    void TepsiBalonu(int ms, string baslik, string metin, bool uyari);
    // Pencere kapanırken çalışacak işi kaydeder.
    void KapanirkenCalistir(Action is_);
    // Alt durum çubuğu: metin ve renkler.
    void DurumYaz(string metin);
    void DurumZemini(Color renk);
    void DurumYazisiRengi(Color renk);
    // Kabuk gezintisi: kabuk hazır mı, sayfaya (sekmeye) git (başarılıysa true).
    bool KabukHazir { get; }
    Task<bool> SayfayaGitAsync(string sayfa, string? sekme);
    // Savunma durumu servisi (Acil İzolasyon / Nöbetçi), Kolay görünüm ve küre köprüleri.
    SavunmaDurumServisi Savunma { get; }
    void KolayGostergeleriYenile();
    void KureYansit();
    void UzmanKureKur();
    void UzmanKureGorunum(Gorunum g);
    bool HaritaKureAcik { get; }
}
