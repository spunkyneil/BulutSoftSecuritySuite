// Bu dosya Form1'in "tarama çalışma alanı"ndan DIŞARI taşınan özelliklerin (kanıt paketi, altın profil, kabul edilen risk, mühür denetimi, nöbetçi cihaz kontrolü...) Form1'e bağlandığı TEK SINIRDIR.
// Eskiden bu özellikler Form1'in partial dosyalarıydı ve Form1'in her alanına (düğme, tablo, _bulgular...) doğrudan erişiyordu; şimdi yalnızca bu arayüzdeki dar yüzeyi görürler,
// böylece Form1 olmadan (sahte bir ITaramaBaglami ile) sınanabilirler. Form1 arayüzü Form1.cs içinde açık (explicit) uygular.

// Programımızın adı.
namespace LabDenetim;

// Tarama çalışma alanının dışarıya açtığı dar yüzey.
internal interface ITaramaBaglami
{
    // Pencere (ShowDialog ve mesaj kutularının sahibi).
    IWin32Window Sahip { get; }
    // Ekrandaki bulgular (yalnızca okunur görünüm).
    IReadOnlyList<Bulgu> Bulgular { get; }
    // Hedef adres kutusundaki değer (kırpılmış).
    string Hedef { get; set; }
    // Ekrandaki kırmızı (kritik) bulgu sayısı.
    int Kirmizi { get; }
    // Bir tarama sürüyor mu?
    bool TaramaSuruyor { get; }
    // Güvenlik puanı (0-100).
    int Puan { get; }
    // Canlı günlüğe satır yazar.
    void Gunluk(string mesaj, Color renk);
    // Pencere hazır mı (tutamaç var, atılmamış)? Arka plan olaylarında arayüze dokunmadan önce sorulur.
    bool PencereHazir { get; }
    // Verilen işi (pencere hazırsa) arayüz iş parçacığında çalıştırır.
    void UiIsle(Action is_);
    // Sistem tepsisi simgesi görünürse balon gösterir (uyari: uyarı simgesi, değilse bilgi).
    void TepsiBalonu(int ms, string baslik, string metin, bool uyari);
    // Pencere kapandığında çalışacak bir iş kaydeder.
    void KapanirkenCalistir(Action is_);
    // Durum yazısını değiştirir; istenirse günlüğe de yazar.
    void Durum(string metin, bool gunluge = true, Color? renk = null);
    // Ekrandaki sonuç listesine bir bulgu ekler.
    void BulguEkle(Bulgu b);
    // Rapor üst bilgisi (hedef, tarih, profil, puan...).
    RaporBilgisi RaporBilgisiOlustur();
    // Bir bulgunun rapordaki "ne demek / nasıl / komut" açıklaması.
    (string NeDemek, string Nasil, string Komut) RaporAciklayici(Bulgu b);
}

// Özelliklerin ortak renkleri (tema değişince canlı çevrilir; Form1'in statik alanlarına bağımlılık yok).
internal static class TaramaRenkleri
{
    public static Color Yesil => TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusSuccess));
    public static Color Kirmizi => TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusDanger));
    public static Color Elektrik => DesignTokens.Ozgun(TokenRol.StatusInfo);
    public static Color Simsek => DesignTokens.Ozgun(TokenRol.AccentPrimary);
}
