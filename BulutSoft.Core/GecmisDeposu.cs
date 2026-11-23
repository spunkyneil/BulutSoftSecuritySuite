// Bu dosya "Geçmiş Operasyonlar"ı çalıştırır: her tarama bitince kaydedilir, program kapanıp açılsa da silinmez,
// sol menüdeki "Geçmiş Operasyonlar" ile listelenir ve tıklanınca eski sonuçlar ekrana gelir.
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Önceki iki taramanın karşılaştırma sonucu.
internal sealed record Karsilastirma(string Ozet, List<string> Yeni, List<string> Kapanan, int PuanFarki);

// Geçmiş kayıtlarını bilgisayarda saklayan ve okuyan yardımcı.
internal static class GecmisDeposu
{
    // Kayıtların durduğu klasör (kullanıcının Uygulama Verileri klasörü).
    private static string Klasor => Path.Combine(BrandConfig.RoamingKlasor, "gecmis");
    // JSON'u okunaklı yazmak için ayar.
    private static readonly JsonSerializerOptions Ayar = new() { WriteIndented = true };
    // En fazla kaç kayıt saklansın (eskiler silinir).
    private const int EnFazla = 100;

    // Yeni bir kaydı dosyaya yazar.
    public static void Kaydet(GecmisKayit k)
    {
        // Klasör yoksa oluştur.
        Directory.CreateDirectory(Klasor);
        // Kaydı JSON olarak yaz.
        Depo.YazJson(Path.Combine(Klasor, k.Id + ".json"), k, Ayar);
        // Çok kayıt biriktiyse eskileri temizle.
        Temizle();
    }

    // Tüm kayıtları en yeniden eskiye doğru listeler. Bozuk bir dosya varsa onu atlar.
    public static List<GecmisKayit> Listele()
    {
        // Sonuç listesi.
        var liste = new List<GecmisKayit>();
        // Klasör okunamazsa boş liste dön.
        try
        {
            // Klasör yoksa hiç kayıt yok.
            if (!Directory.Exists(Klasor)) return liste;
            // Her dosyayı oku.
            foreach (var dosya in Directory.GetFiles(Klasor, "*.json"))
            {
                // Bozuk dosya diğerlerini engellemesin.
                try { var k = JsonSerializer.Deserialize<GecmisKayit>(File.ReadAllText(dosya)); if (k != null) liste.Add(k); }
                // Okunamayan dosyayı atla.
                catch (Exception hk1) { HataKayit.Sessiz(hk1, "GecmisDeposu.Listele"); }
            }
        }
        // Klasöre erişilemezse sessizce geç.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "GecmisDeposu.Listele"); }
        // En yeniden eskiye sırala.
        return liste.OrderByDescending(k => k.Tarih).ToList();
    }

    // Tek bir kaydı siler.
    public static void Sil(string id) { try { File.Delete(Path.Combine(Klasor, id + ".json")); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "GecmisDeposu.Sil"); } }

    // Tüm kayıtları siler.
    public static void HepsiniSil() { try { if (Directory.Exists(Klasor)) foreach (var d in Directory.GetFiles(Klasor, "*.json")) File.Delete(d); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "GecmisDeposu.HepsiniSil"); } }

    // En yeni 100 kaydı tutar, fazlasını siler.
    private static void Temizle()
    {
        // Silme başarısız olabilir.
        try { foreach (var eski in Listele().Skip(EnFazla)) Sil(eski.Id); }
        // Hata olursa geç.
        catch (Exception hk5) { HataKayit.Sessiz(hk5, "GecmisDeposu.Temizle"); }
    }

    // İki taramayı karşılaştırır: yeni çıkan ve kapanan sorunları bulur (yeşiller ve atlananlar sayılmaz).
    public static Karsilastirma Karsilastir(GecmisKayit yeni, GecmisKayit onceki)
    {
        // Bir kaydın "sorun" listesi: modül + başlık anahtarı.
        static HashSet<string> Sorunlar(GecmisKayit k) => k.Bulgular.Where(b => b.Onem != Onem.Bilgi && !b.Atlandi).Select(b => b.Modul + "|" + b.Baslik).ToHashSet();
        // Yeni ve önceki sorun kümeleri.
        var y = Sorunlar(yeni); var o = Sorunlar(onceki);
        // Yeni çıkanlar: yenide var, öncekinde yok.
        var yeniler = y.Except(o).Select(s => s.Split('|', 2)[1]).ToList();
        // Kapananlar: öncekinde var, yenide yok.
        var kapananlar = o.Except(y).Select(s => s.Split('|', 2)[1]).ToList();
        // Puan farkı.
        int fark = yeni.Puan - onceki.Puan;
        // Özet cümlesi.
        string ozet = $"Önceki taramaya göre ({onceki.Tarih:dd.MM HH:mm}): {yeniler.Count} yeni sorun, {kapananlar.Count} sorun kapanmış, puan {fark:+#;-#;0}.";
        // Sonucu ver.
        return new Karsilastirma(ozet, yeniler, kapananlar, fark);
    }
}
