// JSON dosyasına kaydederken bazı alanları dışarıda bırakmak için gerekli araç.
using System.Text.Json.Serialization;

// Programımızın adı (tüm dosyalar aynı adı kullanır).
namespace LabDenetim;

// Önem derecesi: sonucun ne kadar tehlikeli olduğunu söyleyen 4 seçenek (düşükten yükseğe doğru).
public enum Onem { Bilgi, Dusuk, Orta, Yuksek }

// Bulgu: tabloda görünen tek bir sonucun bilgi kartı.
// Modul = hangi kontrol buldu, Baslik = ne bulundu, Onem = ne kadar tehlikeli,
// Detay = ayrıntı, Oneri = nasıl düzeltilir.
public record Bulgu(string Modul, string Baslik, Onem Onem, string Detay, string Oneri)
{
    // Bu sonuç "kontrol yapılamadı" notu mu? (Bunlar "güvenli" sayılmaz; sadece bakılamamıştır.)
    [JsonIgnore] public bool Atlandi => Baslik == "Bu kontrol çalıştırılamadı";
}

// Rapor başlığında görünen tarama bilgileri (hedef, tarih, süre, puan, sayılar).
public record RaporBilgisi(string Hedef, DateTime Tarih, string Profil, int KapiSayisi, TimeSpan Sure, int Puan, int Kirmizi, int Sari, int Yesil, int Atlanan);

// Geçmiş Operasyonlar listesindeki bir kaydın tamamı: tarama bilgileri + bulunan tüm sonuçlar.
public record GecmisKayit(string Id, DateTime Tarih, string Hedef, bool Yerel, string Profil, int KapiSayisi, int SureSn, int Puan, int Kirmizi, int Sari, int Yesil, int Atlanan, List<Bulgu> Bulgular);
