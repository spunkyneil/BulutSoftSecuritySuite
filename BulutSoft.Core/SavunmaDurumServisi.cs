// Bu dosya SAVUNMA DURUMUNUN (Acil İzolasyon + Nöbetçi Modu) TEK KAYNAĞIDIR. Eskiden bu durum ana pencerenin görünmez menü düğmelerinin metin/renginde saklanıyordu; artık
// olay tabanlı bu serviste durur. Durumu yazanlar (güvenlik duvarı denetimi, nöbetçi açma/kapama) servise yazar; okuyanlar (Savunma Merkezi çubuğu, sistem tepsisi, küre/kalkan göstergesi)
// Degisti olayına abone olur — düğme metnini okuyarak durum anlama yoktur. Saf mantıktır (arayüz bağımlılığı yok): Core'da durur ve birim testlenir.
// İş parçacığı: durum kilitle korunur; olay, DEĞİŞİKLİĞİ YAPAN iş parçacığında (kilit dışında) tetiklenir — arayüz aboneleri kendi iş parçacığına aktarır.

// Programımızın adı.
namespace LabDenetim;

// Savunma durumu servisi.
public sealed class SavunmaDurumServisi
{
    private readonly object _kilit = new();
    private bool _acilAktif, _acilMesgul, _nobetAktif, _nobetMesgul;

    // Durum değişti (hangi alan olduğu önemsiz: aboneler güncel durumu okur).
    public event Action? Degisti;

    // Acil İzolasyon kuralları güvenlik duvarında etkin mi?
    public bool AcilAktif { get { lock (_kilit) return _acilAktif; } }
    // Acil İzolasyon işlemi (UAC/onay/uygulama) sürüyor mu? (sürerken tekrar tıklanamaz)
    public bool AcilMesgul { get { lock (_kilit) return _acilMesgul; } }
    // Nöbetçi Modu açık mı?
    public bool NobetAktif { get { lock (_kilit) return _nobetAktif; } }
    // Nöbetçi açma/kapama işlemi sürüyor mu?
    public bool NobetMesgul { get { lock (_kilit) return _nobetMesgul; } }

    public void AcilAyarla(bool aktif) => Yaz(ref _acilAktif, aktif);
    public void AcilMesgulAyarla(bool mesgul) => Yaz(ref _acilMesgul, mesgul);
    public void NobetAyarla(bool aktif) => Yaz(ref _nobetAktif, aktif);
    public void NobetMesgulAyarla(bool mesgul) => Yaz(ref _nobetMesgul, mesgul);

    // Değer gerçekten değiştiyse yazar ve olayı (kilit dışında) tetikler; abone hatası durumu bozmaz.
    private void Yaz(ref bool alan, bool deger)
    {
        lock (_kilit) { if (alan == deger) return; alan = deger; }
        try { Degisti?.Invoke(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SavunmaDurumServisi.Degisti"); }
    }
}
