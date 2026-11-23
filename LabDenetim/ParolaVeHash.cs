// Bu dosya Modül 4'tür: "Parola Politikası ve Bütünlük Doğrulama" (savunma amaçlı).
//   1) ParolaPolitikasi : yazılan bir parolanın kurumsal karmaşıklık kurallarına uyup uymadığını ÖLÇER (uzunluk, karakter çeşitliliği, bilinen zayıf kalıplar).
//      Parola YALNIZCA bellekte ve bu bilgisayarda değerlendirilir: internete gönderilmez, dosyaya yazılmaz, günlüğe düşmez. (Sızıntı sorgusu ayrı ve onaylı bir araçtır.)
//   2) HashAraci        : bir dosyanın MD5 / SHA-1 / SHA-256 / SHA-512 özetini hesaplar ve üreticinin yayınladığı özetle karşılaştırır (indirilen dosya bozulmuş/değiştirilmiş mi?).
// Bu dosya hiçbir saldırı/kırma işlevi içermez: parola tahmin etmez, hash kırmaz; yalnızca ölçer ve doğrular.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Kurum politikası (değerler dosyada saklanır; parola ASLA saklanmaz).
internal sealed record ParolaPolitika(int MinUzunluk = 12, int MinSinif = 3, List<string>? Yasakli = null)
{
    // Yasaklı sözcükler (kullanıcı adı, şirket adı gibi): parola bunları içeremez.
    public List<string> YasakliListe => Yasakli ?? new();
}

// Değerlendirme sonucu.
internal sealed record ParolaSonucu(bool Uygun, int Puan, string Seviye, double EntropiBit, List<string> Ihlaller, List<string> Oneriler);

// Parola ölçer.
internal static class ParolaPolitikasi
{
    // Politika dosyası.
    public static string Yol => BulutKlasor.Dosya("parola_politikasi.json");
    // Sınırlar (kötü niyetli çok uzun girdiye karşı).
    public const int EnUzunIncelenen = 256, EnAzUzunluk = 8, EnCokUzunluk = 64;

    // Politikayı okur (yok/bozuksa varsayılan: 12 karakter, 4 sınıftan en az 3'ü).
    public static ParolaPolitika Yukle() { var p = Depo.Yukle<ParolaPolitika>(Yol); return p != null ? Duzelt(p) : new ParolaPolitika(); }
    // Politikayı yazar.
    public static void Kaydet(ParolaPolitika p) { Depo.YazJson(Yol, Duzelt(p)); }
    // Geçersiz değerleri güvenli aralığa çeker.
    public static ParolaPolitika Duzelt(ParolaPolitika p) => new(Math.Clamp(p.MinUzunluk, EnAzUzunluk, EnCokUzunluk), Math.Clamp(p.MinSinif, 1, 4), (p.Yasakli ?? new()).Select(x => x.Trim()).Where(x => x.Length >= 3 && x.Length <= 40).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList());

    // Çok bilinen zayıf parola kökleri (küçük harf, Türkçe karakterler sadeleşmiş). 5+ harfli olanlar parolanın İÇİNDE geçse de zayıf sayılır.
    private static readonly string[] Zayif =
    {
        "password", "passw0rd", "parola", "sifre", "admin", "administrator", "root", "qwerty", "qwertyuiop", "asdfgh", "zxcvbn", "letmein", "welcome", "iloveyou", "monkey", "dragon", "master", "login", "secret", "football", "baseball", "superman", "batman", "sunshine", "princess", "shadow", "freedom", "whatever", "trustno1",
        "istanbul", "ankara", "izmir", "bursa", "antalya", "turkiye", "turkey", "ataturk", "galatasaray", "fenerbahce", "besiktas", "trabzonspor", "aslan", "kartal", "canarya", "sevgilim", "askim", "ailem", "annem", "babam", "kardesim", "merhaba", "selam", "deneme", "test1234", "kullanici", "yonetici", "bilgisayar", "internet",
        "12345678", "123456789", "1234567890", "12345", "123456", "111111", "000000", "121212", "654321", "987654321", "abc123", "abcd1234", "qazwsx", "1q2w3e", "1qaz2wsx", "q1w2e3", "changeme", "default", "guest"
    };
    // Klavye sıraları (ileri ve geri 4+ harfli parçalar zayıf).
    private static readonly string[] Siralar = { "qwertyuiop", "asdfghjkl", "zxcvbnm", "1234567890", "abcdefghijklmnopqrstuvwxyz", "qazwsxedcrfv", "1q2w3e4r5t", "ertyuiopgu" };

    // Karşılaştırma için sadeleştirir: küçük harf, Türkçe karakterler, yaygın "leet" yazımları (p@ssw0rd -> password).
    internal static IEnumerable<string> Sadelestir(string s)
    {
        // Türkçe karakterler ve küçük harf.
        var b = new StringBuilder(); foreach (var c in s.ToLowerInvariant()) b.Append(c switch { 'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ç' => 'c', 'ö' => 'o', 'ü' => 'u', _ => c });
        string t = b.ToString();
        // Leet: 1 hem 'i' hem 'l' olabilir; iki varyant üretilir.
        string Leet(string x, char bir) => new(x.Select(c => c switch { '@' => 'a', '4' => 'a', '0' => 'o', '3' => 'e', '$' => 's', '5' => 's', '7' => 't', '!' => 'i', '1' => bir, _ => c }).ToArray());
        yield return t; yield return Leet(t, 'i'); yield return Leet(t, 'l');
    }

    // Parolada ardışık/klavye dizisi var mı? (ileri veya geri, en az 4 karakter)
    internal static bool DiziVarMi(string s)
    {
        string k = s.ToLowerInvariant();
        for (int i = 0; i + 4 <= k.Length; i++)
        {
            string p = k.Substring(i, 4); string r = new(p.Reverse().ToArray());
            if (Siralar.Any(x => x.Contains(p, StringComparison.Ordinal) || x.Contains(r, StringComparison.Ordinal))) return true;
        }
        return false;
    }

    // Aynı karakter 3+ kez art arda mı, ya da kısa bir parça tekrar ediyor mu (abcabc)?
    internal static bool TekrarVarMi(string s)
    {
        if (Regex.IsMatch(s, @"(.)\1\1", RegexOptions.None, TimeSpan.FromMilliseconds(100))) return true;
        for (int u = 1; u <= s.Length / 2 && u <= 4; u++) if (s.Length % u == 0 && s.Length >= u * 3) { string p = s[..u]; if (string.Concat(Enumerable.Repeat(p, s.Length / u)) == s) return true; }
        return false;
    }

    // Karakter sınıfları: küçük, büyük, rakam, özel (Türkçe harfler harf sayılır).
    internal static (bool Kucuk, bool Buyuk, bool Rakam, bool Ozel) Siniflar(string s) => (s.Any(char.IsLower), s.Any(char.IsUpper), s.Any(char.IsDigit), s.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) || char.IsWhiteSpace(c)));

    // Ölçer. kullaniciAdi: verilirse parola onu içeremez.
    public static ParolaSonucu Degerlendir(string? parola, ParolaPolitika? politika = null, string? kullaniciAdi = null)
    {
        var pol = Duzelt(politika ?? new ParolaPolitika()); string p = parola ?? ""; var ihlal = new List<string>(); var oneri = new List<string>();
        if (p.Length == 0) return new(false, 0, "Parola yazılmadı", 0, new() { "Parola boş." }, new());
        if (p.Length > EnUzunIncelenen) p = p[..EnUzunIncelenen];
        // 1) Uzunluk.
        if (p.Length < pol.MinUzunluk) ihlal.Add($"Çok kısa: {p.Length} karakter (en az {pol.MinUzunluk} olmalı).");
        // 2) Karakter çeşitliliği.
        var (ku, bu, ra, oz) = Siniflar(p); int sinif = (ku ? 1 : 0) + (bu ? 1 : 0) + (ra ? 1 : 0) + (oz ? 1 : 0);
        if (sinif < pol.MinSinif) { var eksik = new List<string>(); if (!ku) eksik.Add("küçük harf"); if (!bu) eksik.Add("büyük harf"); if (!ra) eksik.Add("rakam"); if (!oz) eksik.Add("özel karakter (örn. ! ? . -)"); ihlal.Add($"Yeterince çeşitli değil: {sinif}/4 karakter sınıfı (en az {pol.MinSinif} gerekir). Eksik: {string.Join(", ", eksik)}."); }
        // 3) Bilinen zayıf parola/kök.
        string zayifBulunan = ""; foreach (var v in Sadelestir(p)) { foreach (var w in Zayif) if (v == w || (w.Length >= 5 && v.Contains(w, StringComparison.Ordinal))) { zayifBulunan = w; break; } if (zayifBulunan.Length > 0) break; }
        bool zayif = zayifBulunan.Length > 0; if (zayif) ihlal.Add("Çok bilinen/tahmin edilebilir bir sözcük veya kalıp içeriyor (saldırganların ilk denediği listelerde bulunur).");
        // 4) Dizi ve tekrar.
        bool dizi = DiziVarMi(p); if (dizi) ihlal.Add("Ardışık veya klavye sırası içeriyor (1234, abcd, qwer, asdf gibi)."); bool tekrar = TekrarVarMi(p); if (tekrar) ihlal.Add("Aynı karakter art arda tekrar ediyor veya kısa bir parça yinelenmiş.");
        // 5) Yasaklı sözcükler (kullanıcı/şirket adı).
        foreach (var y in pol.YasakliListe.Concat(string.IsNullOrWhiteSpace(kullaniciAdi) || kullaniciAdi.Trim().Length < 3 ? Array.Empty<string>() : new[] { kullaniciAdi.Trim() }))
            if (Sadelestir(p).Any(v => v.Contains(Sadelestir(y).First(), StringComparison.Ordinal))) { ihlal.Add("Kurumun/kullanıcının adını (\"" + y + "\") içeriyor."); break; }
        // 6) Yıl sonu eki: tek başına ihlal değil, öneri.
        if (Regex.IsMatch(p, @"(19|20)\d\d$", RegexOptions.None, TimeSpan.FromMilliseconds(100))) oneri.Add("Sonuna yıl eklemek yaygın bir tahmin yöntemidir; bunun yerine uzunluğu artırın.");
        // Entropi tahmini: havuz boyutu × etkin uzunluk; tekrarlar ve diziler etkin uzunluğu düşürür.
        int havuz = (ku ? 29 : 0) + (bu ? 29 : 0) + (ra ? 10 : 0) + (oz ? 33 : 0); if (havuz == 0) havuz = 10; int farkli = p.Distinct().Count(); double etkin = farkli + (p.Length - farkli) * 0.3; if (dizi) etkin *= 0.6; if (zayif) etkin = Math.Min(etkin, 6);
        double bit = Math.Round(etkin * Math.Log2(havuz), 1);
        // Puan: entropiden (80 bit ≈ 100) ve ihlaller düşer.
        int puan = (int)Math.Clamp(bit / 80.0 * 100, 0, 100); if (zayif) puan = Math.Min(puan, 15); if (p.Length < pol.MinUzunluk) puan = Math.Min(puan, 45); if (ihlal.Count > 0) puan = Math.Min(puan, 69);
        string seviye = puan < 30 ? "Çok zayıf" : puan < 50 ? "Zayıf" : puan < 70 ? "Orta" : puan < 85 ? "İyi" : "Güçlü";
        // Öneriler.
        if (ihlal.Count > 0 || puan < 70) { oneri.Add("En iyi yöntem: birbiriyle ilgisiz 4-5 sözcükten oluşan uzun bir parola cümlesi (örn. \"bulut-masa-kalem-yagmur-42\"). Uzunluk, karmaşık görünen kısa parolalardan çok daha güçlüdür."); oneri.Add("Her hesap için ayrı parola kullanın ve bir parola yöneticisinden yararlanın; mümkünse iki aşamalı doğrulamayı açın."); }
        return new(ihlal.Count == 0, puan, seviye, bit, ihlal, oneri);
    }
}

// Özet türleri.
internal enum HashTuru { MD5, SHA1, SHA256, SHA512 }

// Dosya özeti hesaplayıcı ve karşılaştırıcı.
internal static class HashAraci
{
    // Türün hex uzunluğu.
    public static int HexUzunluk(HashTuru t) => t switch { HashTuru.MD5 => 32, HashTuru.SHA1 => 40, HashTuru.SHA256 => 64, _ => 128 };
    // Güvenlik notu: zayıf özetler kazara bozulmayı bulur, kasıtlı değişikliği DEĞİL.
    public static string Not(HashTuru t) => t is HashTuru.MD5 or HashTuru.SHA1 ? $"{t}: çarpışma saldırılarına açık olduğu için kasıtlı değişikliği kanıtlamaz; yalnızca kazara bozulmayı (indirme hatası) gösterir. Güvenlik için SHA-256 veya SHA-512 kullanın." : $"{t}: güvenli kabul edilir.";

    // Dosyayı TEK geçişte okuyup istenen tüm özetleri hesaplar (bellekte yalnızca 80 KB tampon). Okuma sırasında dosya kilitlense bile engellemez (paylaşımlı okuma).
    public static async Task<Dictionary<HashTuru, string>> DosyaOzetleriAsync(string yol, IEnumerable<HashTuru> turler, IProgress<int>? ilerleme, CancellationToken ct)
    {
        // Doğrulama.
        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol)) throw new FileNotFoundException("Dosya bulunamadı: " + yol);
        var liste = turler.Distinct().ToList(); if (liste.Count == 0) throw new ArgumentException("En az bir özet türü seçin.");
        // Her tür için artımlı hesaplayıcı.
        var hesaplar = liste.ToDictionary(t => t, t => IncrementalHash.CreateHash(t switch { HashTuru.MD5 => HashAlgorithmName.MD5, HashTuru.SHA1 => HashAlgorithmName.SHA1, HashTuru.SHA256 => HashAlgorithmName.SHA256, _ => HashAlgorithmName.SHA512 }));
        try
        {
            // Akış.
            await using var fs = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, true); long toplam = fs.Length, okunan = 0; var tampon = new byte[81920]; int son = -1, n;
            while ((n = await fs.ReadAsync(tampon.AsMemory(), ct)) > 0) { foreach (var h in hesaplar.Values) h.AppendData(tampon, 0, n); okunan += n; if (toplam > 0) { int yuzde = (int)(okunan * 100 / toplam); if (yuzde != son) { son = yuzde; ilerleme?.Report(yuzde); } } }
            return hesaplar.ToDictionary(k => k.Key, k => Convert.ToHexString(k.Value.GetHashAndReset()).ToLowerInvariant());
        }
        finally { foreach (var h in hesaplar.Values) h.Dispose(); }
    }

    // Kullanıcının yapıştırdığı özeti sadeleştirir: boşluk, iki nokta, tire atılır; "sha256:" önekleri ve "özet *dosyaadı" biçimi (sha256sum çıktısı) desteklenir; küçük harfe çevrilir.
    public static string Normalize(string? s)
    {
        string t = (s ?? "").Trim(); if (t.Length > 400) t = t[..400];
        // sha256sum/certutil biçimi: ilk sözcük özettir.
        t = Regex.Replace(t, @"^(?:md5|sha-?1|sha-?256|sha-?512)\s*[:=]\s*", "", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); var ilk = t.Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries); if (ilk.Length > 0 && ilk.Length > 1 && Regex.IsMatch(ilk[0].Replace(":", ""), "^[0-9a-fA-F]{32,128}$")) t = ilk[0];
        return new string(t.Where(c => Uri.IsHexDigit(c)).ToArray()).ToLowerInvariant();
    }

    // Uzunluğa göre özet türünü tahmin eder (tanınmazsa null).
    public static HashTuru? TurTahmin(string normalHex) => normalHex.Length switch { 32 => HashTuru.MD5, 40 => HashTuru.SHA1, 64 => HashTuru.SHA256, 128 => HashTuru.SHA512, _ => null };

    // Sabit zamanlı eşitlik (zamanlama sızıntısı olmasın).
    public static bool Esit(string a, string b) => a.Length == b.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    // Hesaplanan özetleri beklenenle karşılaştırır. Eşleşme / uyuşmazlık / tanınmayan biçim ayrı ayrı bildirilir.
    public static (bool? Eslesti, HashTuru? Tur, string Mesaj) Karsilastir(IReadOnlyDictionary<HashTuru, string> hesaplanan, string? beklenenHam)
    {
        // Normalleştir.
        string b = Normalize(beklenenHam); if (b.Length == 0) return (null, null, "Karşılaştırılacak özet yazılmadı.");
        var tur = TurTahmin(b); if (tur == null) return (null, null, $"Yazılan özetin uzunluğu ({b.Length} hane) tanınmıyor. MD5 = 32, SHA-1 = 40, SHA-256 = 64, SHA-512 = 128 hane olmalı.");
        if (!hesaplanan.TryGetValue(tur.Value, out var h)) return (null, tur, $"{tur} özeti hesaplanmadı; önce {tur} seçip hesaplayın.");
        return Esit(h, b) ? (true, tur, $"EŞLEŞTİ: dosyanın {tur} özeti, yazdığınız özetle birebir aynı. Dosya değişmemiş.") : (false, tur, $"UYUŞMUYOR: dosyanın {tur} özeti, yazdığınız özetten FARKLI. Dosya bozulmuş, eksik inmiş veya değiştirilmiş olabilir; kullanmayın ve resmî kaynaktan yeniden indirin.");
    }

    // sha256sum biçiminde çıktı satırı.
    public static string Satir(HashTuru t, string hex, string dosyaAdi) => $"{hex}  {dosyaAdi}   ({t})";
}
