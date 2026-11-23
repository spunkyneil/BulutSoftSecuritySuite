// Kanıt paketinin DIŞ güvenceleri (sertifika gerektirmeyen): (1) hizmetteki gizli anahtarla HMAC (".hmac"), (2) RFC 3161 güvenilir ZAMAN DAMGASI (".tsr").
// Neden? Zip içindeki SHA-256 listesini paketi değiştiren kişi yeniden üretebilir. HMAC anahtarı yalnızca koruma hizmetinde (SYSTEM) durur: kullanıcı hesabıyla çalışan biri sahte HMAC üretemez.
// Zaman damgası ise paketin O ANDA var olduğunu üçüncü bir tarafın (TSA) imzasıyla kanıtlar; yerel saati değiştirmek bunu taklit edemez.
// GİZLİLİK: zaman damgası için İNTERNETE ÇIKILIR ve yalnızca zip'in SHA-256 özeti (64 hane) TSA'ya gönderilir; içerik, dosya adı, kullanıcı ve bilgisayar adı GÖNDERİLMEZ. Kullanıcı onayı olmadan çağrılmaz.
// DÜRÜST SINIR: ".hmac" doğrulaması yalnızca AYNI bilgisayardaki koruma hizmetiyle yapılabilir (başka makineye taşınan pakette yalnızca .sha256 ve .tsr doğrulanır). Kod imzalama sertifikası kullanılmıyor.
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

internal static class KanitImzasi
{
    // Bilinen zaman damgası sunucuları (sırayla denenir). Kullanıcı kanit_ayar.json içindeki "TsaUrl" ile ilk sıraya kendi sunucusunu koyabilir (yalnızca http/https).
    private static readonly string[] Varsayilan = { "https://freetsa.org/tsr", "http://timestamp.digicert.com" };
    public static string[] Tsalar { get { try { var y = BulutKlasor.Dosya("kanit_ayar.json"); if (File.Exists(y) && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(y)) is { } d && d.TryGetValue("TsaUrl", out var u) && Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return new[] { u }.Concat(Varsayilan).Distinct().ToArray(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KanitImzasi.KanitImzasi.ctor"); } return Varsayilan; } }

    // Zaman damgası alınamadıysa nedenini paketin yanına yazar (paket yine de geçerli; "alınamadı" bilgisi paketle birlikte kalır).
    public static void ZamanDamgasiYokNotuYaz(string zip, string neden) { try { File.WriteAllText(zip + ".zaman-damgasi-yok.txt", $"Zaman damgası ALINAMADI ({DateTime.UtcNow:O} UTC).{Environment.NewLine}Neden: {neden}{Environment.NewLine}Bu paketin var olma zamanı yalnızca bilgisayarın yerel saatine dayanır; bağımsız olarak kanıtlanamaz.", new System.Text.UTF8Encoding(false)); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "KanitImzasi.ZamanDamgasiYokNotuYaz"); } }

    internal sealed record HmacKaydi(string Ozet, string Meta, string Imza);

    // Paketin özet ve künyesini koruma hizmetine imzalatır ve ".hmac" dosyasına yazar. Hizmet yoksa null (dosya yazılmaz).
    public static string? HmacYaz(string zip, string operatorAd)
    {
        try
        {
            string ozet = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))); string meta = $"op={operatorAd};ver={typeof(KanitImzasi).Assembly.GetName().Version?.ToString(3)};zaman={DateTime.UtcNow:O};dosya={Path.GetFileName(zip)}";
            var r = ServisIstemci.Gonder("paket-imzala", new() { ["ozet"] = ozet, ["meta"] = meta }); if (r?.Tamam != true || r.Veri == null || !r.Veri.TryGetValue("imza", out var imza)) return null;
            string yol = zip + ".hmac"; Depo.Yaz(yol, JsonSerializer.Serialize(new HmacKaydi(ozet, meta, imza), JsonAyarlari.Girintili)); return yol;
        }
        catch { return null; }
    }

    // ".hmac" dosyasını doğrular: zip'in özeti kayıttakiyle aynı mı ve imza hizmette geçerli mi? (sonuç, açıklama)
    public static (bool Gecerli, string Aciklama) HmacDogrula(string zip)
    {
        try
        {
            string yol = zip + ".hmac"; if (!File.Exists(yol)) return (false, ".hmac dosyası yok.");
            var k = JsonSerializer.Deserialize<HmacKaydi>(File.ReadAllText(yol)); if (k == null) return (false, ".hmac dosyası okunamadı.");
            if (!IkinciKanal.Esit(k.Ozet, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))))) return (false, "Paket DEĞİŞTİRİLMİŞ: özeti .hmac kaydıyla uyuşmuyor.");
            var r = ServisIstemci.Gonder("paket-dogrula", new() { ["ozet"] = k.Ozet, ["meta"] = k.Meta, ["imza"] = k.Imza });
            if (r == null) return (false, "Koruma hizmetine ulaşılamadı; imza doğrulanamadı (yalnızca aynı bilgisayarda doğrulanabilir).");
            return r.Tamam ? (true, "İmza geçerli. " + k.Meta) : (false, "İmza GEÇERSİZ: künye ya da özet değiştirilmiş.");
        }
        catch (Exception ex) { return (false, "Doğrulanamadı: " + ex.Message); }
    }

    // RFC 3161 zaman damgası ister, yanıtı doğrular (özet ve nonce eşleşmeli) ve zip'in yanına ".tsr" olarak yazar. Başarıda damga zamanı (UTC), hata olursa null + neden.
    public static async Task<(DateTimeOffset? Zaman, string Not)> ZamanDamgasiAlAsync(string zip, string tsaUrl, CancellationToken ct, HttpMessageHandler? isleyici = null)
    {
        try
        {
            byte[] ozet = SHA256.HashData(File.ReadAllBytes(zip)); var istek = Rfc3161TimestampRequest.CreateFromHash(ozet, HashAlgorithmName.SHA256, null, RandomNumberGenerator.GetBytes(8), true);
            using var icerik = new ByteArrayContent(istek.Encode()); icerik.Headers.ContentType = new MediaTypeHeaderValue("application/timestamp-query");
            using var tsaIstek = new HttpRequestMessage(HttpMethod.Post, tsaUrl) { Content = icerik }; using var yanit = await AgIstemciFabrikasi.GonderAsync(AgProfili.ZamanDamgasi, tsaIstek, HttpCompletionOption.ResponseContentRead, isleyici, ct); if (!yanit.IsSuccessStatusCode) return (null, $"TSA HTTP {(int)yanit.StatusCode} döndürdü.");
            byte[] govde = await yanit.Content.ReadAsByteArrayAsync(ct); if (govde.Length > 65536) return (null, "TSA yanıtı beklenenden büyük.");
            var jeton = istek.ProcessResponse(govde, out _);   // özet, algoritma ve nonce eşleşmezse istisna fırlatır
            Depo.YazBayt(zip + ".tsr", govde); return (jeton.TokenInfo.Timestamp, "TSA: " + tsaUrl);
        }
        catch (AgHatasi ag) { return (null, ag.Message); }
        catch (Exception ex) { return (null, ex.GetType().Name + ": " + ex.Message); }
    }

    // Var olan ".tsr" dosyasının zip'e ait olup olmadığını (özet eşleşmesi) ve damga zamanını döndürür. (TSA sertifika zinciri doğrulanmaz; bkz. DÜRÜST SINIR.)
    public static (bool Eslesiyor, DateTimeOffset? Zaman, string Not) ZamanDamgasiKontrol(string zip)
    {
        try
        {
            string yol = zip + ".tsr"; if (!File.Exists(yol)) return (false, null, ".tsr dosyası yok.");
            if (!Rfc3161TimestampToken.TryDecode(File.ReadAllBytes(yol), out var jeton, out _) || jeton == null) { if (!TryYanit(File.ReadAllBytes(yol), out jeton)) return (false, null, ".tsr okunamadı."); }
            bool es = jeton!.TokenInfo.HashAlgorithmId.Value == "2.16.840.1.101.3.4.2.1" && CryptographicOperations.FixedTimeEquals(jeton.TokenInfo.GetMessageHash().Span, SHA256.HashData(File.ReadAllBytes(zip)));
            return (es, jeton.TokenInfo.Timestamp, es ? "Özet eşleşiyor." : "Zaman damgası BAŞKA bir dosyaya ait ya da paket değiştirilmiş.");
        }
        catch (Exception ex) { return (false, null, "Doğrulanamadı: " + ex.Message); }
    }

    // TSA yanıtı (TimeStampResp) içinden jetonu çıkarmaya çalışır: yanıtın "timeStampToken" alanı CMS'tir; boş/başarısız yanıtlarda false.
    private static bool TryYanit(byte[] resp, out Rfc3161TimestampToken? jeton)
    {
        jeton = null;
        try { var okuyucu = new System.Formats.Asn1.AsnReader(resp, System.Formats.Asn1.AsnEncodingRules.DER); var seq = okuyucu.ReadSequence(); seq.ReadSequence(); var kalan = seq.ReadEncodedValue(); return Rfc3161TimestampToken.TryDecode(kalan, out jeton, out _); }
        catch { return false; }
    }
}
