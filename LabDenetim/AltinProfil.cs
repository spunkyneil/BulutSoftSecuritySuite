// Bu dosya "Altın Profil" (Güvenli Referans) özelliğini çalıştırır: kullanıcı sistemini temizleyip iyi bir tarama sonucu aldığında
// bu durumu kilitler (altin_profil.json). Sonraki her tarama bu temiz referansla kıyaslanır ve sapma olursa uyarılır.
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Kilitlenen temiz referansın bilgi kartı: hedef, tarih, puan, açık kapılar ve o günkü sorun listesi.
internal sealed record AltinProfilKaydi(DateTime Tarih, string Hedef, int Puan, List<int> Kapilar, List<string> Sorunlar);

// Altın Profil'in sonuç karşılaştırması.
internal sealed record AltinFarki(List<int> YeniKapilar, List<string> YeniSorunlar, List<int> KapananKapilar);

// Altın profili bilgisayarda saklayan ve karşılaştıran yardımcı.
internal static class AltinProfilDeposu
{
    // Dosyanın yolu: %AppData%\BulutSoft\altin_profil.json (testte değiştirilebilir).
    public static string Yol { get; set; } = Path.Combine(BrandConfig.RoamingKlasor, "altin_profil.json");
    // JSON'u okunaklı yazmak için ayar.
    private static readonly JsonSerializerOptions Ayar = new() { WriteIndented = true };

    // Bulgulardan açık kapı numaralarını çıkarır ("Açık port 445 (SMB)" başlıklarından).
    public static List<int> KapilariCikar(IEnumerable<Bulgu> bulgular) =>
        bulgular.Where(b => !b.Atlandi && b.Modul == "Port" && b.Baslik.StartsWith("Açık port ", StringComparison.Ordinal))
            .Select(b => System.Text.RegularExpressions.Regex.Match(b.Baslik, @"Açık port (\d+)")).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(p => p).ToList();

    // Sorun anahtarları: yalnızca yeşil olmayan ve bizim ürettiğimiz uyarı olmayan sonuçlar.
    public static List<string> SorunlariCikar(IEnumerable<Bulgu> bulgular) =>
        bulgular.Where(b => !b.Atlandi && b.Onem != Onem.Bilgi && b.Modul is not ("Zincir" or "Referans")).Select(b => b.Modul + "|" + b.Baslik).Distinct().ToList();

    // Kayıtlı altın profili okur; yoksa veya bozuksa null verir.
    public static AltinProfilKaydi? Yukle()
    {
        // Okuma bozulabilir.
        return Depo.Yukle<AltinProfilKaydi>(Yol);
    }

    // Verilen sonuçları altın profil olarak kilitler (dosyaya yazar).
    public static AltinProfilKaydi Kilitle(string hedef, int puan, IReadOnlyCollection<Bulgu> bulgular)
    {
        // Kaydı hazırla.
        var k = new AltinProfilKaydi(DateTime.Now, hedef, puan, KapilariCikar(bulgular), SorunlariCikar(bulgular));
        // Klasör yoksa oluştur.
        Depo.YazJson(Yol, k, Ayar);
        // Kaydı geri ver.
        return k;
    }

    // Altın profili siler.
    public static void Sil() { try { if (File.Exists(Yol)) File.Delete(Yol); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "AltinProfil.Sil", true); } }

    // Şimdiki sonuçları altın profille kıyaslar: yeni açılan kapılar, yeni sorunlar, kapanan kapılar.
    public static AltinFarki Karsilastir(AltinProfilKaydi altin, IReadOnlyCollection<Bulgu> simdi)
    {
        // Şimdiki kapılar ve sorunlar.
        var kapilar = KapilariCikar(simdi); var sorunlar = SorunlariCikar(simdi);
        // Farkları hesapla.
        return new AltinFarki(kapilar.Except(altin.Kapilar).ToList(), sorunlar.Except(altin.Sorunlar).ToList(), altin.Kapilar.Except(kapilar).ToList());
    }
}
