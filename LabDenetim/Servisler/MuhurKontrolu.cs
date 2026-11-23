// Açılışta mühür kontrolü: mühür basılmışsa ve dosya değişmişse olay günlüğüne yazar (mühür yoksa hiçbir şey yapmaz). Eskiden Form1'in AracKutusu.cs partial dosyasındaydı.

// Programımızın adı.
namespace LabDenetim;

// Mühür açılış denetimi.
internal static class MuhurKontrolu
{
    // Değişmiş/silinmiş korumalı dosya varsa kritik satır yazar; hata olsa da açılış bozulmasın.
    public static void AcilistaDenetle(ITaramaBaglami _b)
    {
        // Hata olsa da açılış bozulmasın.
        try { var bozuk = Muhur.Dogrula().Where(x => x.Durum is "DEĞİŞMİŞ" or "SİLİNMİŞ").Select(x => x.Dosya).ToList(); if (bozuk.Count > 0) _b.Gunluk("KRİTİK -> Kendini koruma: izinsiz değişmiş/silinmiş ayar dosyası: " + string.Join(", ", bozuk), TaramaRenkleri.Kirmizi); }
        // Sessiz geç.
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "MuhurKontrolu.AcilistaDenetle"); }
    }
}
