// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// WEB SİTESİ KORUMALARI MODÜLÜ: sitenin gönderdiği koruma etiketlerine (başlıklara) bakar.
public static class HttpDenetimi
{
    // Verilen web adresini denetler ve bulguların listesini verir.
    public static async Task<List<Bulgu>> DenetleAsync(string url, CancellationToken ct)
    {
        // Bulunanları toplayacağımız boş liste.
        var liste = new List<Bulgu>();
        // Laboratuvarda sertifikalar çoğu kez kendi kendine imzalıdır; burada hata verme, sertifika kontrolü ayrı modülde.
        using var isleyici = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        // Web isteği gönderecek tarayıcı benzeri araç; 8 saniyede cevap gelmezse vazgeçer.
        using var http = new HttpClient(isleyici) { Timeout = TimeSpan.FromSeconds(8) };
        // Siteye girip cevabını al.
        var yanit = await http.GetAsync(url, ct);
        // Cevaptaki tüm etiketleri (başlıkları) ad-değer sözlüğüne çevir.
        var basliklar = yanit.Headers.Concat(yanit.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(";", h.Value), StringComparer.OrdinalIgnoreCase);
        // Adres https ile mi başlıyor (şifreli mi)?
        bool https = url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        // Yardımcı: belirtilen koruma etiketi yoksa listeye bulgu ekler.
        void Eksik(string ad, Onem o, string oneri)
        {
            // Etiket sözlükte yoksa eksik demektir.
            if (!basliklar.ContainsKey(ad))
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("HTTP", $"Eksik koruma etiketi: {ad}", o, $"{url} cevabında {ad} etiketi yok.", oneri));
        }
        // Şifreli sitede "hep şifreli bağlan" etiketi olmalı.
        if (https) Eksik("Strict-Transport-Security", Onem.Orta, "Sunucu ayarlarına şunu ekleyin: Strict-Transport-Security: max-age=31536000; includeSubDomains");
        // İçerik güvenlik kuralı etiketi (siteye yabancı kod sokulmasını zorlaştırır).
        Eksik("Content-Security-Policy", Onem.Orta, "Kısıtlayıcı bir Content-Security-Policy kuralı tanımlayın (örn. default-src 'self').");
        // Tarayıcının dosya türünü tahmin etmesini engelleyen etiket.
        Eksik("X-Content-Type-Options", Onem.Dusuk, "X-Content-Type-Options: nosniff etiketini ekleyin.");
        // Siteniz başka bir sayfanın içine gizlice gömülemesin (tıklama kandırmacası) etiketi.
        if (!basliklar.ContainsKey("X-Frame-Options") && !(basliklar.TryGetValue("Content-Security-Policy", out var csp) && csp.Contains("frame-ancestors")))
            // Bulguyu listeye ekle.
            liste.Add(new Bulgu("HTTP", "Sitenin başka sayfaya gizlice gömülmesine karşı koruma yok", Onem.Orta, "X-Frame-Options veya frame-ancestors etiketi yok.", "X-Frame-Options: DENY ekleyin."));
        // Başka sitelere hangi bilginin gönderileceğini belirleyen etiket.
        Eksik("Referrer-Policy", Onem.Dusuk, "Referrer-Policy: no-referrer ekleyin.");

        // Sunucunun kendini ve sürümünü ele veren etiketlere bak.
        foreach (var h in new[] { "Server", "X-Powered-By", "X-AspNet-Version" })
            // Etiket varsa ve içinde sürüm numarası (rakam) geçiyorsa.
            if (basliklar.TryGetValue(h, out var v) && v.Any(char.IsDigit))
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("HTTP", $"Sunucu kendi sürümünü söylüyor: {h}", Onem.Dusuk, $"{h}: {v}", "Sürüm bilgisini cevaptan kaldırın (saldırgana ipucu vermesin)."));

        // Site şifresiz (http) ise bunu bildir.
        if (!https)
            // Bulguyu listeye ekle.
            liste.Add(new Bulgu("HTTP", "Site şifresiz konuşuyor (http)", Onem.Orta, $"{url} adresi şifresiz çalışıyor; arada dinleyen biri yazılanları okuyabilir.", "Siteyi https'e geçirin."));

        // Çerezlere (tarayıcıda saklanan küçük notlara) bak.
        if (yanit.Headers.TryGetValues("Set-Cookie", out var cerezler))
            // Her çerez için kontrol et.
            foreach (var c in cerezler)
            {
                // Çerezin adını al.
                var ad = c.Split('=')[0];
                // Şifreli sitede "Secure" bayrağı yoksa.
                if (https && !c.Contains("secure", StringComparison.OrdinalIgnoreCase))
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("HTTP", $"Çerez şifresiz yolda da gidebilir: {ad}", Onem.Orta, c, "Çereze Secure bayrağı ekleyin."));
                // "HttpOnly" bayrağı yoksa sayfadaki kodlar çerezi okuyabilir.
                if (!c.Contains("httponly", StringComparison.OrdinalIgnoreCase))
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("HTTP", $"Çerez sayfa kodlarınca okunabilir: {ad}", Onem.Orta, c, "Çereze HttpOnly bayrağı ekleyin."));
                // "SameSite" bayrağı yoksa başka siteler çerezi tetikleyebilir.
                if (!c.Contains("samesite", StringComparison.OrdinalIgnoreCase))
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("HTTP", $"Çerezde SameSite koruması yok: {ad}", Onem.Dusuk, c, "SameSite=Lax veya Strict ekleyin."));
            }
        // Hiç sorun çıkmadıysa güzel haber ver.
        if (liste.Count == 0) liste.Add(new Bulgu("HTTP", "Temel koruma etiketleri yerinde", Onem.Bilgi, url, "Yapılacak bir şey yok."));
        // Listeyi geri ver.
        return liste;
    }
}
