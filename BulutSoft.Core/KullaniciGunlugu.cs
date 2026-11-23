// Bu dosya KULLANICI ETKİNLİK GÜNLÜĞÜDÜR: hangi kullanıcının programda ne yaptığını ("Yönetici Tarama başlattı", "Nöbetçi Modu'nu açtı", "Canlı Ağ Trafiği sayfasını açtı") olay günlüğüne yazar.
// Oturum olayları (giriş/çıkış/yetkisiz deneme) ve bu eylem kayıtları "Olay Zaman Çizgisi"nde GÖRÜNMEZ; yalnızca "Hesabım ve Kullanıcılar" ekranında, seçilen kullanıcıya ait olarak listelenir.
// Kayıtlar mevcut zincirli (HMAC) olay günlüğüne yazılır; böylece değiştirilmeleri tespit edilebilir ve denetim izi kaybolmaz.
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Kullanıcı eylem günlüğü.
internal static class KullaniciGunlugu
{
    // Eylem kayıtlarının olay türü ("Olay Zaman Çizgisi" bu türü ve "Oturum"u gizler).
    public const string Tur = "Kullanıcı";
    // Oturum olaylarının türü.
    public const string OturumTuru = "Oturum";

    // Zaman çizgisinde gösterilmeyen (yalnızca kullanıcı ekranında görünen) olay türü mü?
    public static bool ZamanCizgisindeGizli(string tur) => tur == Tur || tur == OturumTuru;

    // Giriş yapmış kullanıcının bir eylemini yazar (oturum yoksa yazmaz; günlük hatası programı bozmaz).
    public static void Yaz(string eylem)
    {
        string? ad = ServisBaglami.Oturum.Aktif?.Ad; if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(eylem)) return;
        try { ServisBaglami.OlayYazici.Ekle(Tur, "Bilgi", $"KULLANICI -> {ad}: {eylem}"); }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "KullaniciGunlugu.Yaz", true); }
    }

    // Bir olay bu kullanıcıya mı ait? (eylem kaydı: "KULLANICI -> ad: ..."; oturum olayı: iletide kullanıcı adı geçer)
    public static bool KullaniciyaAitMi(OlayKaydi o, string ad)
    {
        if (string.IsNullOrWhiteSpace(ad)) return false;
        if (o.Tur == Tur) return o.Mesaj.StartsWith($"KULLANICI -> {ad}: ", StringComparison.Ordinal);
        if (o.Tur == OturumTuru) return Regex.IsMatch(o.Mesaj, $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(ad)}(?![\p{{L}}\p{{N}}_])", RegexOptions.CultureInvariant);
        return false;
    }

    // Kayıttan gösterilecek kısa metni çıkarır ("KULLANICI -> ad: " ve "OTURUM -> " önekleri atılır).
    public static string Metin(OlayKaydi o, string ad)
    {
        string onek = $"KULLANICI -> {ad}: "; if (o.Mesaj.StartsWith(onek, StringComparison.Ordinal)) return o.Mesaj[onek.Length..];
        return o.Mesaj.StartsWith("OTURUM -> ", StringComparison.Ordinal) ? "Oturum: " + o.Mesaj["OTURUM -> ".Length..] : o.Mesaj;
    }

    // Kullanıcının en yeni kayıtlarını (en yeniden eskiye) okur; en çok 3 sayfa (3000 olay) taranır.
    public static List<OlayKaydi> Oku(string ad, int enCok = 300)
    {
        var sonuc = new List<OlayKaydi>();
        try
        {
            for (int sayfa = 0; sayfa < 3 && sonuc.Count < enCok; sayfa++)
            {
                var (liste, toplam) = OlayDeposu.OkuSayfali(sayfa, 1000);
                sonuc.AddRange(liste.Where(o => KullaniciyaAitMi(o, ad)));
                if ((sayfa + 1) * 1000 >= toplam) break;
            }
        }
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "KullaniciGunlugu.Oku"); }
        return sonuc.Take(enCok).ToList();
    }
}
