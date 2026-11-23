// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// FORM KUTUSU TESTİ MODÜLÜ: KENDİ test sitenizdeki bir kutucuğa zararsız deneme yazıları gönderir.
// Amaç: site, gelen yazıları kontrolsüz kabul edip geri yansıtıyor mu, hata mesajıyla içini ele veriyor mu görmek.
// Hiçbir zarar vermez, içeri girmeye çalışmaz; yalnızca laboratuvar adreslerine çalışır.
public static class GirdiDogrulamaTesti
{
    // Gönderilecek zararsız deneme yazıları: (testin adı, gönderilecek yazı).
    static readonly (string Ad, string Yuk)[] Yukler =
    {
        // Sayfaya kodlanmadan yansırsa, başkasının kodu sayfaya sokulabilir demektir (işaretli, zararsız bir etiket).
        ("Yansıma testi", "<labxss7731>\"'"),
        // Tek tırnak veritabanı hatası çıkarıyor mu bakar.
        ("Veritabanı hata testi", "'"),
        // Üst klasörlere çıkmayı deneyen yol (dosya sızıntısı var mı bakar).
        ("Klasör dışına çıkma testi", "..%2f..%2fetc%2fpasswd"),
        // Şablon motoru girdiyi hesaplıyor mu bakar (7*7 = 49 çıkarsa evet).
        ("Hesaplama testi", "{{7*7}}"),
    };
    // Sayfada görülürse "site içini ele veriyor" anlamına gelen hata cümleleri.
    static readonly string[] HataIzleri = { "SQL syntax", "SQLSTATE", "ORA-0", "Unclosed quotation", "System.Data", "Stack trace", "Traceback (most recent", "root:x:0:0" };

    // Verilen adresteki kutucuğu test eder.
    public static async Task<List<Bulgu>> TestEtAsync(string url, string parametre, CancellationToken ct)
    {
        // Yazılan adresi parçalarına ayır.
        var uri = new Uri(url);
        // Güvenlik kilidi: adres laboratuvar dışındaysa burada hata verip durur.
        await LabKapsam.DogrulaVeCozAsync(uri.Host, ct);
        // Bulguları toplayacağımız boş liste.
        var liste = new List<Bulgu>();
        // Laboratuvarda kendi imzalı sertifikalara izin ver.
        using var isleyici = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        // Web isteği aracı; 8 saniye bekler.
        using var http = new HttpClient(isleyici) { Timeout = TimeSpan.FromSeconds(8) };

        // Her deneme yazısı için sırayla test et.
        foreach (var (ad, yuk) in Yukler)
        {
            // Adreste zaten soru işareti varsa & ile, yoksa ? ile ekleme yap.
            var ayirac = uri.Query.Length > 0 ? "&" : "?";
            // Test adresini oluştur (kutucuk adı = deneme yazısı).
            var hedef = $"{url}{ayirac}{Uri.EscapeDataString(parametre)}={Uri.EscapeDataString(yuk)}";
            // İstek başarısız olursa programı çökertme.
            try
            {
                // Siteye isteği gönder.
                var yanit = await http.GetAsync(hedef, ct);
                // Sitenin döndürdüğü sayfa yazısını oku.
                var govde = await yanit.Content.ReadAsStringAsync(ct);
                // Deneme etiketimiz sayfada olduğu gibi duruyorsa site girdiyi kodlamıyor demektir.
                if (ad == "Yansıma testi" && govde.Contains("<labxss7731>"))
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Girdi", "Girilen yazı sayfaya olduğu gibi yansıyor", Onem.Yuksek, $"'{parametre}' kutucuğuna yazılan etiket kodlanmadan sayfaya basıldı (başkasının zararlı kodu da böyle sokulabilir).", "Sayfaya basılan her kullanıcı yazısını güvenli biçime çevirin (HTML encode)."));
                // 7*7 yerine 49 görünüyorsa site girdiyi bir formül gibi çalıştırıyor demektir.
                if (ad == "Hesaplama testi" && govde.Contains("49") && !govde.Contains("{{7*7}}"))
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Girdi", "Girilen yazı site tarafından hesaplanıyor", Onem.Yuksek, "{{7*7}} yazıldı, sayfada 49 göründü.", "Kullanıcı yazısını asla şablonun içine doğrudan koymayın."));
                // Sayfada içini ele veren bir hata cümlesi var mı diye bak.
                var iz = HataIzleri.FirstOrDefault(h => govde.Contains(h, StringComparison.OrdinalIgnoreCase));
                // Varsa bulguyu ekle.
                if (iz != null)
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Girdi", $"Site hata mesajıyla içini ele veriyor ({ad})", Onem.Yuksek, $"Sayfada '{iz}' ifadesi görüldü.", "Ayrıntılı hataları kullanıcıya göstermeyin; veritabanı sorgularında hazır (parametreli) sorgu kullanın."));
                // Sunucu hatası (500 ve üstü) verdiyse bildir.
                if ((int)yanit.StatusCode >= 500)
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Girdi", $"Site çöktü / hata verdi: {ad}", Onem.Orta, $"Sunucu cevap kodu: {(int)yanit.StatusCode}", "Gelen yazıları doğrulayın ve hataları düzgün karşılayın."));
            }
            // İstek zaman aşımına uğradı veya hata verdi (kullanıcı durdurmadıysa).
            catch (Exception ex) when ((ex is HttpRequestException || ex is TaskCanceledException) && !ct.IsCancellationRequested)
            {
                // Bunu bilgi olarak not et.
                liste.Add(new Bulgu("Girdi", $"İstek gönderilemedi: {ad}", Onem.Bilgi, ex.Message, "Site adresinin doğru ve sitenin açık olduğundan emin olun."));
            }
        }
        // Hiç sorun bulunmadıysa güzel haber ver.
        if (!liste.Any(b => b.Onem != Onem.Bilgi))
            // Bulguyu listeye ekle.
            liste.Add(new Bulgu("Girdi", "Basit denemelerde sorun görülmedi", Onem.Bilgi, "Kutucuk: " + parametre, "Bu yalnızca basit bir testtir; kod incelemesiyle desteklemek iyi olur."));
        // Listeyi geri ver.
        return liste;
    }
}
