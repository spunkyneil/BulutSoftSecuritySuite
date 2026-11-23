// Bu dosya Claude yapay zekasıyla konuşan kodları içerir: anahtarı güvenle saklama ve Anthropic API'sine istek gönderme.
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Ayarlar: seçili mod ve model adı.
internal sealed record YzAyar(string Mod, string Model);

// API anahtarını ve ayarları bu bilgisayarda güvenle saklar.
internal static class ApiDeposu
{
    // Varsayılan Claude modeli (API Ayarı'ndan değiştirilebilir).
    public const string VarsayilanModel = "claude-sonnet-5-5";

    // Dosyaların durduğu klasör (kullanıcının Uygulama Verileri klasörü).
    internal static string? KlasorGecersiz { get; set; }   // yalnızca sınama: gerçek kullanıcı klasörüne dokunmadan denemek için
    private static string Klasor => KlasorGecersiz ?? BrandConfig.RoamingKlasor;
    // Şifreli anahtar dosyası.
    private static string AnahtarYolu => Path.Combine(Klasor, "api.dat");
    // Ayar dosyası (anahtar İÇERMEZ).
    private static string AyarYolu => Path.Combine(Klasor, "ayar.json");

    // Kayıtlı bir API anahtarı var mı?
    public static bool AnahtarVarMi() { try { return File.Exists(AnahtarYolu); } catch { return false; } }

    // Kayıtlı anahtarı okur; yoksa veya okunamazsa boş döner (program hata vermez).
    // DPAPI entropisi (gizli değildir; amaç, aynı Windows hesabındaki başka uygulamaların bu dosyayı kendiliğinden çözmesini zorlaştırmaktır).
    internal static readonly byte[] AnahtarEntropisi = Encoding.UTF8.GetBytes("BulutSoft-ClaudeAnahtar-v1");

    public static string? AnahtarOku()
    {
        // Okuma başarısız olabilir.
        try
        {
            // Dosya yoksa anahtar yok.
            if (!File.Exists(AnahtarYolu)) return null;
            // Şifreli baytları oku.
            var sifreli = File.ReadAllBytes(AnahtarYolu);
            // Windows hesabınıza özel şifreyi çöz (başka kullanıcı açamaz). Uygulamaya özgü entropi kullanılır (Microsoft DPAPI önerisi): aynı hesaptaki başka bir uygulama anahtarı kolayca çözemez.
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(sifreli, AnahtarEntropisi, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException)
            {
                // Eski sürümde entropisiz kaydedilmiş anahtar: bir kez çözülür ve entropili biçimde yeniden yazılır (göç).
                var eski = ProtectedData.Unprotect(sifreli, null, DataProtectionScope.CurrentUser); string metin = Encoding.UTF8.GetString(eski);
                try { Depo.YazBayt(AnahtarYolu, ProtectedData.Protect(eski, AnahtarEntropisi, DataProtectionScope.CurrentUser)); } catch (IOException hk) { HataKayit.Sessiz(hk, "ClaudeApi.AnahtarOku.Goc"); }
                return metin;
            }
        }
        // Okunamazsa anahtar yok say.
        catch { return null; }
    }

    // Anahtarı Windows hesabınıza özel olarak şifreleyip kaydeder.
    public static void AnahtarKaydet(string anahtar)
    {
        // Klasör yoksa oluştur.
        Directory.CreateDirectory(Klasor);
        // Anahtarı şifrele.
        var sifreli = ProtectedData.Protect(Encoding.UTF8.GetBytes(anahtar.Trim()), AnahtarEntropisi, DataProtectionScope.CurrentUser);
        // Şifreli haliyle dosyaya yaz.
        Depo.YazBayt(AnahtarYolu, sifreli);
    }

    // Kayıtlı anahtarı siler.
    public static void AnahtarSil() { try { if (File.Exists(AnahtarYolu)) File.Delete(AnahtarYolu); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "ClaudeApi.AnahtarSil", true); } }

    // Ayarları okur; dosya yoksa varsayılanları verir.
    public static YzAyar AyarOku()
    {
        // Okuma başarısız olabilir.
        try { if (File.Exists(AyarYolu)) return JsonSerializer.Deserialize<YzAyar>(File.ReadAllText(AyarYolu)) ?? new YzAyar("yerel", VarsayilanModel); }
        // Bozuk dosya varsa varsayılana dön.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "ClaudeApi.AyarOku"); }
        // Varsayılan: Yerel mod.
        return new YzAyar("yerel", VarsayilanModel);
    }

    // Ayarları kaydeder (kaydedilemezse sessizce geçer).
    public static void AyarKaydet(YzAyar ayar)
    {
        // Yazma başarısız olabilir.
        try { Directory.CreateDirectory(Klasor); Depo.YazJson(AyarYolu, ayar); }
        // Hata olursa program çalışmaya devam etsin.
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "ClaudeApi.AyarKaydet", true); }
    }
}

// Claude ile konuşurken oluşan, kullanıcıya gösterilebilir hata.
internal sealed class ClaudeHatasi : Exception
{
    // Sorun anahtarla/krediyle ilgili mi? (Öyleyse program Yerel moda döner.)
    public bool YerelModaDon { get; }
    // Hatayı kurar.
    public ClaudeHatasi(string mesaj, bool yerelModaDon) : base(mesaj) { YerelModaDon = yerelModaDon; }
}

// Anthropic'in Claude API'sine (api.anthropic.com) canlı (akış halinde) istek gönderir.
internal static class ClaudeIstemci
{
    // Ağ çağrıları AgIstemciFabrikasi üzerinden gider: bağlantı 15 sn, yanıt başlığı 60 sn, akış sırasında parça arası boşta kalma 90 sn.

    // Claude'a bir soru gönderir; cevap geldikçe parcaGeldi fonksiyonunu çağırır.
    public static async Task AnalizAsync(string anahtar, string model, string sistem, string kullanici, Action<string> parcaGeldi, CancellationToken ct)
    {
        // İstek gövdesi: model, en fazla cevap uzunluğu, canlı akış, sistem talimatı ve kullanıcı sorusu.
        var govde = JsonSerializer.Serialize(new { model, max_tokens = 2500, stream = true, system = sistem, messages = new[] { new { role = "user", content = kullanici } } });
        // Hatalar kullanıcıya anlaşılır hale getirilecek.
        try
        {
            // İsteği hazırla.
            using var istek = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") { Content = new StringContent(govde, Encoding.UTF8, "application/json") };
            // API anahtarı başlığı.
            istek.Headers.Add("x-api-key", anahtar);
            // API sürüm başlığı.
            istek.Headers.Add("anthropic-version", "2023-06-01");
            // İsteği gönder; cevabın başlıkları gelir gelmez okumaya başla (canlı akış için).
            using var yanit = await AgIstemciFabrikasi.GonderAsync(AgProfili.ClaudeAnaliz, istek, HttpCompletionOption.ResponseHeadersRead, null, ct);
            // Başarısızsa hatayı anlaşılır hale getirip fırlat.
            if (!yanit.IsSuccessStatusCode) throw HataUret((int)yanit.StatusCode, await yanit.Content.ReadAsStringAsync(ct));
            // Cevap akışını aç.
            using var akis = await yanit.Content.ReadAsStreamAsync(ct);
            // Satır satır okuyucu.
            using var okuyucu = new StreamReader(akis, Encoding.UTF8);
            // Her satırı oku.
            string? satir;
            while (true)
            {
                // Parça arası boşta kalma sınırı: sunucu bağlantıyı açık tutup veri göndermezse akış kesilir.
                using var bosta = CancellationTokenSource.CreateLinkedTokenSource(ct); bosta.CancelAfter(AgIstemciFabrikasi.ClaudeAkisBostaSure);
                try { satir = await okuyucu.ReadLineAsync(bosta.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ClaudeHatasi("Claude yanıtı yarıda kesildi: " + (int)AgIstemciFabrikasi.ClaudeAkisBostaSure.TotalSeconds + " saniyedir veri gelmiyor. İnternet bağlantınızı kontrol edip yeniden deneyin.", false); }
                if (satir == null) break;
                // Yalnızca "data:" ile başlayan satırlar içerik taşır.
                if (!satir.StartsWith("data:", StringComparison.Ordinal)) continue;
                // "data:" kısmından sonrası JSON'dur.
                var json = satir[5..].Trim();
                // Boş veya bitiş işaretiyse atla.
                if (json.Length == 0 || json == "[DONE]") continue;
                // JSON'u çöz.
                using var belge = JsonDocument.Parse(json);
                // Kök öğe.
                var kok = belge.RootElement;
                // Olay türü.
                string tip = kok.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                // Yeni metin parçası geldiyse ekrana ilet.
                if (tip == "content_block_delta" && kok.TryGetProperty("delta", out var d) && d.TryGetProperty("text", out var tx)) parcaGeldi(tx.GetString() ?? "");
                // Akış içinde hata geldiyse fırlat.
                else if (tip == "error") throw HataUret(0, json);
            }
        }
        // Zaten anlaşılır hata ise aynen ilet.
        catch (ClaudeHatasi) { throw; }
        // Ağ hatası: fabrikanın anlaşılır Türkçe mesajı (zaman aşımı, DNS, TLS, bağlantı yok).
        catch (AgHatasi ag) { throw new ClaudeHatasi(ag.Message, false); }
        // Kullanıcı durdurduysa üst katman halleder.
        catch (OperationCanceledException) { throw; }
        // İnternet/bağlantı sorunu.
        catch (HttpRequestException) { throw new ClaudeHatasi("Claude'a bağlanılamadı. İnternet bağlantınızı kontrol edin.", false); }
        // Beklenmeyen başka bir sorun.
        catch (Exception ex) { throw new ClaudeHatasi("Claude ile konuşurken beklenmeyen bir sorun oldu: " + ex.Message, false); }
    }

    // Sunucunun hata cevabını sade Türkçe bir mesaja çevirir.
    private static ClaudeHatasi HataUret(int kod, string icerik)
    {
        // Sunucunun kendi hata mesajı (varsa).
        string sunucuMesaji = "";
        // JSON olmayabilir.
        try { using var b = JsonDocument.Parse(icerik); if (b.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m)) sunucuMesaji = m.GetString() ?? ""; }
        // Çözülemezse boş bırak.
        catch (Exception hk4) { HataKayit.Sessiz(hk4, "ClaudeApi.HataUret"); }
        // Kredi/bakiye sorunu mu? (Mesajda "credit" veya "balance" geçer.)
        bool kredi = sunucuMesaji.Contains("credit", StringComparison.OrdinalIgnoreCase) || sunucuMesaji.Contains("balance", StringComparison.OrdinalIgnoreCase);
        // Duruma göre sade mesaj.
        return kod switch
        {
            // Yetkisiz: anahtar yanlış.
            401 => new ClaudeHatasi("API anahtarı geçersiz veya yanlış yazılmış. \"⚙ API Ayarı\"ndan kontrol edin.", true),
            // Yasak.
            403 => new ClaudeHatasi("API anahtarınızın bu işlem için yetkisi yok.", true),
            // Model bulunamadı.
            404 => new ClaudeHatasi("Seçilen model bulunamadı. \"⚙ API Ayarı\"ndan model adını kontrol edin.", false),
            // Kredi bitmiş.
            400 when kredi => new ClaudeHatasi("API krediniz bitmiş görünüyor.", true),
            // Diğer 400 hataları.
            400 => new ClaudeHatasi("İstek kabul edilmedi: " + sunucuMesaji, false),
            // Kullanım sınırı.
            429 => new ClaudeHatasi("Kullanım sınırına ulaşıldı. Biraz bekleyip tekrar deneyin.", false),
            // Sunucu yoğun veya arızalı.
            >= 500 => new ClaudeHatasi("Claude servisi şu an yoğun veya erişilemiyor. Biraz sonra tekrar deneyin.", false),
            // Bilinmeyen durum.
            _ => new ClaudeHatasi(string.IsNullOrWhiteSpace(sunucuMesaji) ? $"Beklenmeyen bir cevap alındı (kod {kod})." : sunucuMesaji, kredi)
        };
    }
}
