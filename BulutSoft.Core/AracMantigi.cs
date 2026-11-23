// Bu dosya "Güvenlik Araç Kutusu"nun beynidir (ekrandan bağımsız, test edilebilir):
// link/phishing analizi, tarayıcı eklenti denetimi, yerel kara liste (IP itibarı), parola sızıntı kontrolü (k-anonimlik),
// dosya bütünlüğü izleme (FIM), vaka defteri, kendini koruma (mühür), tanılama paketi ve bildirim kanalı (webhook).
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Ortak klasör ve küçük yardımcılar.
internal static class BulutKlasor
{
    // Programın tüm kalıcı dosyalarının klasörü (testte değiştirilebilir).
    public static string Kok { get; set; } = BrandConfig.RoamingKlasor;
    // Bu klasörde bir dosya yolu verir.
    public static string Dosya(string ad) => Path.Combine(Kok, ad);
}

// ---------------------------------------------------------------- 1) LİNK / PHISHING ANALİZİ
// Bir linkin ne kadar şüpheli göründüğünü, linke GİTMEDEN (internete çıkmadan) yalnızca yazısına bakarak değerlendirir.
internal static class LinkAnalizi
{
    // Sonuç: risk puanı (0-100) ve gerekçeler.
    public sealed record Sonuc(int Risk, string Seviye, List<string> Nedenler);

    // Sık taklit edilen markalar ve gerçek alan adları.
    private static readonly (string Marka, string[] Gercek)[] Markalar =
    {
        ("paypal", new[] { "paypal.com" }), ("google", new[] { "google.com", "google.com.tr", "gmail.com", "youtube.com" }), ("microsoft", new[] { "microsoft.com", "live.com", "office.com", "outlook.com" }),
        ("apple", new[] { "apple.com", "icloud.com" }), ("amazon", new[] { "amazon.com", "amazon.com.tr" }), ("netflix", new[] { "netflix.com" }), ("instagram", new[] { "instagram.com" }),
        ("facebook", new[] { "facebook.com" }), ("whatsapp", new[] { "whatsapp.com" }), ("turkcell", new[] { "turkcell.com.tr" }), ("garanti", new[] { "garantibbva.com.tr" }),
        ("isbank", new[] { "isbank.com.tr" }), ("ziraat", new[] { "ziraatbank.com.tr" }), ("ptt", new[] { "ptt.gov.tr" }), ("edevlet", new[] { "turkiye.gov.tr" }), ("e-devlet", new[] { "turkiye.gov.tr" })
    };

    // Link kısaltma servisleri.
    private static readonly string[] Kisaltici = { "bit.ly", "tinyurl.com", "t.co", "goo.gl", "is.gd", "cutt.ly", "rb.gy", "ow.ly" };
    // Şüpheli uzantılar.
    private static readonly string[] SupheliUzanti = { ".zip", ".top", ".xyz", ".click", ".tk", ".ml", ".gq", ".cf", ".work", ".country", ".support" };
    // Dikkat çeken kelimeler.
    private static readonly string[] Kelimeler = { "login", "signin", "verify", "secure", "update", "account", "banking", "wallet", "confirm", "giris", "giriş", "parola", "sifre", "şifre", "dogrula", "doğrula", "odeme", "ödeme" };

    // Linki analiz eder.
    public static Sonuc Analiz(string girdi)
    {
        // Boşluk ve satır sonlarını temizle.
        string s = (girdi ?? "").Trim();
        // Nedenler ve puan.
        var n = new List<string>(); int p = 0;
        // Boşsa.
        if (s.Length == 0) return new(0, "Boş", new List<string> { "Bir link yazın." });
        // Şema yoksa ekle ki ayrıştırılabilsin.
        string tam = s.Contains("://") ? s : "http://" + s;
        // Geçerli bir adres değilse.
        if (!Uri.TryCreate(tam, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.Host)) return new(60, "Şüpheli", new List<string> { "Bu geçerli bir internet adresi gibi görünmüyor." });
        // Alan adı küçük harfle.
        string h = u.Host.ToLowerInvariant();
        // Şifresiz http.
        if (u.Scheme == "http") { p += 10; n.Add("Bağlantı şifresiz (http). Parola veya kart bilgisi girmeyin."); }
        // @ işareti: gerçek adresi gizlemek için kullanılır.
        if (s.Contains('@')) { p += 30; n.Add("Adreste @ işareti var: asıl hedef sonrasındaki kısımdır, öncesi aldatmacadır."); }
        // IP adresiyle link.
        if (IPAddress.TryParse(h.Trim('[', ']'), out _)) { p += 30; n.Add("Alan adı yerine sayısal IP adresi kullanılmış. Gerçek siteler genellikle isim kullanır."); }
        // Punycode / Türkçe-olmayan harfler (homograf saldırısı).
        if (h.Contains("xn--") || h.Any(c => c > 127)) { p += 30; n.Add("Alan adında özel/benzer görünen harfler var (örn. Kiril 'а' ile Latin 'a'). Taklit olabilir."); }
        // Kısaltılmış link.
        if (Kisaltici.Any(k => h == k)) { p += 20; n.Add("Kısaltılmış link: gideceği yer görünmüyor. Güvendiğiniz kişiden gelmediyse açmayın."); }
        // Şüpheli uzantı.
        if (SupheliUzanti.Any(k => h.EndsWith(k, StringComparison.Ordinal))) { p += 20; n.Add("Alan adı uzantısı (" + h[h.LastIndexOf('.')..] + ") sahtekârlıkta sık kullanılır."); }
        // Çok fazla alt alan.
        int nokta = h.Count(c => c == '.'); if (nokta >= 4) { p += 15; n.Add("Çok fazla alt alan var; gerçek sitenin adı başa eklenip sahte alan adı arkada gizlenmiş olabilir."); }
        // Çok tire.
        if (h.Count(c => c == '-') >= 3) { p += 10; n.Add("Alan adında çok sayıda tire var."); }
        // Olağandışı kapı numarası.
        if (!u.IsDefaultPort) { p += 10; n.Add($"Standart olmayan kapı ({u.Port}) kullanılmış."); }
        // Çok uzun link.
        if (s.Length > 150) { p += 10; n.Add("Link çok uzun; gerçek adresi gizlemek için olabilir."); }
        // Marka taklidi: marka adı alan adında geçiyor ama alan adı gerçek değil.
        foreach (var (marka, gercek) in Markalar)
            if (h.Contains(marka) && !gercek.Any(g => h == g || h.EndsWith("." + g, StringComparison.Ordinal))) { p += 35; n.Add($"\"{marka}\" markasını andırıyor ama resmî alan adı değil (resmî: {gercek[0]})."); break; }
        // Dikkat çekici kelimeler.
        var kel = Kelimeler.Where(k => (u.AbsolutePath + u.Query + h).Contains(k, StringComparison.OrdinalIgnoreCase)).Distinct().Take(3).ToList(); if (kel.Count > 0) { p += 10; n.Add("Dikkat çekici kelimeler: " + string.Join(", ", kel) + " (hesap/parola isteyen sayfalarda sık görülür)."); }
        // Yüzde ile kodlanmış çok karakter.
        if (s.Count(c => c == '%') >= 6) { p += 10; n.Add("Çok sayıda kodlanmış (%) karakter var."); }
        // Hiç neden yoksa.
        if (n.Count == 0) n.Add("Belirgin bir şüphe işareti bulunmadı. Bu, linkin güvenli olduğunun garantisi değildir; yine de beklemediğiniz mesajlardaki linklere dikkat edin.");
        // Puan sınırı.
        p = Math.Min(100, p);
        // Sonuç.
        return new(p, p >= 60 ? "Yüksek risk" : p >= 30 ? "Şüpheli" : p >= 10 ? "Dikkat" : "Düşük risk", n);
    }
}

// ---------------------------------------------------------------- 2) TARAYICI EKLENTİ DENETİMİ
// Chrome/Edge/Brave'e kurulu eklentileri okur (yalnızca okur) ve tehlikeli izinleri işaretler.
internal static class EklentiDenetimi
{
    // Bulunan eklenti.
    public sealed record Eklenti(string Tarayici, string Ad, string Kimlik, string Surum, List<string> Riskler);

    // Dikkat edilecek izinler ve açıklamaları.
    private static readonly Dictionary<string, string> Riskli = new()
    {
        ["<all_urls>"] = "Tüm sitelerde sayfa içeriğini okuyabilir/değiştirebilir", ["*://*/*"] = "Tüm sitelerde sayfa içeriğini okuyabilir/değiştirebilir", ["webRequest"] = "Ağ isteklerini izleyebilir", ["webRequestBlocking"] = "Ağ isteklerini değiştirebilir",
        ["cookies"] = "Çerezleri (oturum bilgilerini) okuyabilir", ["history"] = "Gezinti geçmişini okuyabilir", ["tabs"] = "Açık sekmeleri ve adreslerini görebilir", ["nativeMessaging"] = "Bilgisayardaki programlarla konuşabilir",
        ["debugger"] = "Tarayıcıyı hata ayıklayıcı gibi kontrol edebilir", ["proxy"] = "Tüm trafiği başka bir sunucuya yönlendirebilir", ["management"] = "Diğer eklentileri yönetebilir", ["clipboardRead"] = "Panonuzdaki kopyaladığınız metni okuyabilir", ["downloads"] = "Dosya indirebilir"
    };

    // Taranacak tarayıcı klasörleri (testte değiştirilebilir).
    public static List<(string Ad, string Yol)> Kokler { get; set; } = new()
    {
        ("Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data")),
        ("Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data")),
        ("Brave", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data"))
    };

    // Tüm eklentileri tarar.
    public static List<Eklenti> Tara()
    {
        // Sonuç.
        var l = new List<Eklenti>();
        // Her tarayıcı.
        foreach (var (ad, kok) in Kokler)
        {
            // Klasör yoksa geç.
            if (!Directory.Exists(kok)) continue;
            // Her profil (Default, Profile 1...).
            foreach (var profil in SafeDirs(kok))
            {
                // Eklentiler klasörü.
                string ek = Path.Combine(profil, "Extensions"); if (!Directory.Exists(ek)) continue;
                // Her eklenti kimliği klasörü.
                foreach (var kimlik in SafeDirs(ek))
                {
                    // En son sürüm klasörü.
                    var surum = SafeDirs(kimlik).OrderByDescending(x => x).FirstOrDefault(); if (surum == null) continue;
                    // Manifest dosyası.
                    string mf = Path.Combine(surum, "manifest.json"); if (!File.Exists(mf)) continue;
                    // Ayrıştır; bozuksa geç.
                    try { l.Add(Oku(ad, Path.GetFileName(kimlik), File.ReadAllText(mf))); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "AracMantigi.Tara"); }
                }
            }
        }
        // Çok riskli olan üstte.
        return l.OrderByDescending(e => e.Riskler.Count).ThenBy(e => e.Ad).ToList();
    }

    // Klasör listesini hata vermeden verir.
    private static string[] SafeDirs(string yol) { try { return Directory.GetDirectories(yol); } catch { return Array.Empty<string>(); } }

    // Manifest metninden eklenti bilgisini çıkarır.
    public static Eklenti Oku(string tarayici, string kimlik, string json)
    {
        // JSON'u aç.
        using var d = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }); var k = d.RootElement;
        // Ad (çok dilli "__MSG_..." ise kimlik gösterilir).
        string ad = k.TryGetProperty("name", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString()! : kimlik; if (ad.StartsWith("__MSG_", StringComparison.Ordinal)) ad = "(adı yerelleştirilmiş) " + kimlik;
        // Sürüm.
        string sv = k.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "?";
        // Tüm izinleri topla.
        var izin = new List<string>(); foreach (var alan in new[] { "permissions", "host_permissions", "optional_permissions" }) if (k.TryGetProperty(alan, out var pa) && pa.ValueKind == JsonValueKind.Array) foreach (var e in pa.EnumerateArray()) if (e.ValueKind == JsonValueKind.String) izin.Add(e.GetString()!);
        // İçerik betiği eşleşmeleri de tüm sitelere erişim anlamına gelebilir.
        if (k.TryGetProperty("content_scripts", out var cs) && cs.ValueKind == JsonValueKind.Array) foreach (var c in cs.EnumerateArray()) if (c.TryGetProperty("matches", out var m) && m.ValueKind == JsonValueKind.Array) foreach (var e in m.EnumerateArray()) if (e.ValueKind == JsonValueKind.String) izin.Add(e.GetString()!);
        // Riskleri bul.
        var risk = izin.Distinct().Where(Riskli.ContainsKey).Select(x => $"{x}: {Riskli[x]}").Distinct().ToList();
        // Sonuç.
        return new Eklenti(tarayici, ad, kimlik, sv, risk);
    }
}

// ---------------------------------------------------------------- 3) YEREL KARA LİSTE (IP İTİBARI)
// Kendi tuttuğunuz kara listeye (kara_liste.txt) göre bir IP'nin kötü bilinen listede olup olmadığına bakar. İnternete sormaz.
internal static class KaraListe
{
    // Dosya yolu.
    public static string Yol => BulutKlasor.Dosya("kara_liste.txt");

    // Dosyayı okur: her satır bir IP veya CIDR (ör. 203.0.113.0/24); # ile başlayanlar açıklamadır.
    public static List<string> Oku() { try { return File.Exists(Yol) ? File.ReadAllLines(Yol).Select(x => x.Split('#')[0].Trim()).Where(x => x.Length > 0).ToList() : new List<string>(); } catch { return new List<string>(); } }

    // IP bu giriş (tek IP veya CIDR) içinde mi?
    public static bool Eslesir(IPAddress ip, string giris)
    {
        // CIDR mı?
        var parca = giris.Split('/');
        // Ağ adresi geçerli değilse hayır.
        if (!IPAddress.TryParse(parca[0], out var ag) || ag.AddressFamily != ip.AddressFamily) return false;
        // Tek IP.
        if (parca.Length == 1) return ag.Equals(ip);
        // Önek uzunluğu.
        if (!int.TryParse(parca[1], out int bit)) return false; var a = ag.GetAddressBytes(); var b = ip.GetAddressBytes(); if (bit < 0 || bit > a.Length * 8) return false;
        // Bit bit karşılaştır.
        for (int i = 0; i < bit; i++) if (((a[i / 8] >> (7 - i % 8)) & 1) != ((b[i / 8] >> (7 - i % 8)) & 1)) return false;
        // Hepsi tuttu.
        return true;
    }

    // IP için sonuç yazısı verir (kara listede mi, yerel mi).
    public static string Kontrol(string metin, List<string>? liste = null)
    {
        // IP geçerli mi?
        if (!IPAddress.TryParse(metin.Trim(), out var ip)) return "Geçerli bir IP adresi değil.";
        // Yerel adres.
        var b = ip.GetAddressBytes(); bool yerel = IPAddress.IsLoopback(ip) || (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && (b[0] == 10 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 169 && b[1] == 254));
        // Liste.
        liste ??= Oku(); var e = liste.FirstOrDefault(x => Eslesir(ip, x));
        // Sonuç.
        return e != null ? $"KARA LİSTEDE: {ip} (eşleşen kayıt: {e})" : yerel ? $"{ip} yerel/özel bir adres; kara liste kontrolü gerekmez." : $"{ip} kara listenizde YOK ({liste.Count} kayıt kontrol edildi). Not: Bu yalnızca sizin listenizdir; \"temiz\" demek değildir.";
    }

    // Şu an kurulu TCP bağlantılarının uzak adreslerini kara listeye karşı tarar.
    public static List<string> BaglantilariTara(List<string>? liste = null)
    {
        // Liste.
        liste ??= Oku(); var sonuc = new List<string>();
        // Aktif bağlantılar.
        foreach (var c in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections())
        {
            // Eşleşen kayıt.
            var e = liste.FirstOrDefault(x => Eslesir(c.RemoteEndPoint.Address, x));
            // Varsa ekle.
            if (e != null) sonuc.Add($"{c.RemoteEndPoint} (durum: {c.State}) ← kara liste kaydı: {e}");
        }
        // Sonuç.
        return sonuc;
    }
}

// ---------------------------------------------------------------- 4) PAROLA SIZINTI KONTROLÜ (k-anonimlik)
// Parolanın SHA-1 özetinin yalnızca İLK 5 HARFİ Have I Been Pwned'e sorulur. Parolanın kendisi ve tam özeti bilgisayardan ASLA çıkmaz.
internal static class ParolaSizinti
{
    // Parolanın SHA-1 özetini BÜYÜK harf onaltılık olarak verir.
    public static string Ozet(string parola) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(parola)));

    // Sunucunun cevabından (SONEK:SAYI satırları) bizim sonekimizin kaç kez sızdığını bulur.
    public static long Say(string cevap, string sonek)
    {
        // Her satır.
        foreach (var satir in cevap.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // SONEK:SAYI biçimi.
            var p = satir.Trim().Split(':'); if (p.Length == 2 && p[0].Equals(sonek, StringComparison.OrdinalIgnoreCase) && long.TryParse(p[1], out long n)) return n;
        }
        // Bulunamadı.
        return 0;
    }

    // İnternete sorar ve kaç kez sızdığını verir (-1: sorulamadı).
    public static async Task<long> SorAsync(string parola, CancellationToken ct = default)
    {
        // Özet ve bölümler.
        string oz = Ozet(parola), onek = oz[..5], sonek = oz[5..];
        // İstemci.
        // Yalnızca 5 harflik önek gönderilir (merkezi ağ istemcisi: bağlantı 8 sn, toplam 10 sn).
        string c = await AgIstemciFabrikasi.MetinAlAsync(AgProfili.ParolaSizintisi, "https://api.pwnedpasswords.com/range/" + onek, b => b.Add("Add-Padding", "true"), ct);
        // Say.
        return Say(c, sonek);
    }
}

// ---------------------------------------------------------------- 5) DOSYA BÜTÜNLÜĞÜ İZLEME (FIM)
// Seçtiğiniz klasördeki dosyaların SHA-256 özetlerini "temel" olarak saklar; sonra değişen/yeni/silinen dosyaları gösterir.
internal static class DosyaButunlugu
{
    // Sınırlar: çok büyük klasörler programı yormasın.
    public const int EnFazlaDosya = 5000; public const long EnFazlaBoyut = 100L * 1024 * 1024;

    // Karşılaştırma sonucu.
    public sealed record Fark(List<string> Degisen, List<string> Yeni, List<string> Silinen, int Toplam, bool Kesildi);

    // Bir klasörün temel dosyasının yolu.
    public static string TemelYolu(string klasor) => BulutKlasor.Dosya(Path.Combine("fim", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(klasor).ToLowerInvariant())))[..16] + ".json"));

    // Klasördeki dosyaların özetlerini çıkarır (göreli yol → özet).
    public static (Dictionary<string, string> Ozetler, bool Kesildi) Tara(string klasor)
    {
        // Sonuç.
        var d = new Dictionary<string, string>(); bool kes = false;
        // Dosyaları gez (erişilemeyenleri atla).
        foreach (var f in new DirectoryInfo(klasor).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            // Sayı sınırı.
            if (d.Count >= EnFazlaDosya) { kes = true; break; }
            // Boyut sınırı: büyük dosyayı atla.
            if (f.Length > EnFazlaBoyut) continue;
            // Özet; dosya kilitliyse atla.
            try { using var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); d[Path.GetRelativePath(klasor, f.FullName)] = Convert.ToHexString(SHA256.HashData(fs)); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "AracMantigi.Tara"); }
        }
        // Sonuç.
        return (d, kes);
    }

    // Temeli kaydeder.
    public static int TemelOlustur(string klasor)
    {
        // Tara.
        var (d, _) = Tara(klasor); string y = TemelYolu(klasor);
        // Yaz.
        Depo.YazJson(y, d);
        // Sayı.
        return d.Count;
    }

    // Temelle şimdiki durumu karşılaştırır (temel yoksa null).
    public static Fark? Karsilastir(string klasor)
    {
        // Temel yoksa.
        string y = TemelYolu(klasor); if (!File.Exists(y)) return null;
        // Eski ve yeni.
        var eski = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(y)) ?? new(); var (yeni, kes) = Tara(klasor);
        // Farklar.
        return new Fark(yeni.Where(k => eski.TryGetValue(k.Key, out var o) && o != k.Value).Select(k => k.Key).OrderBy(x => x).ToList(), yeni.Keys.Except(eski.Keys).OrderBy(x => x).ToList(), eski.Keys.Except(yeni.Keys).OrderBy(x => x).ToList(), yeni.Count, kes);
    }
}

// ---------------------------------------------------------------- 6) VAKA DEFTERİ
// Bir olayı (şüpheli bulgu, USB, vb.) takip etmek için not defteri: durum + tarihli notlar. vakalar.json dosyasında saklanır.
internal sealed record VakaNotu(DateTime Zaman, string Metin);
// Vaka.
internal sealed record Vaka(string Id, DateTime Acilis, string Baslik, string Durum, List<VakaNotu> Notlar);

// Vaka deposu.
internal static class VakaDeposu
{
    // Dosya.
    public static string Yol => BulutKlasor.Dosya("vakalar.json");
    // Geçerli durumlar.
    public static readonly string[] Durumlar = { "Açık", "İncelemede", "Çözüldü", "Kapalı" };

    // Okur.
    public static List<Vaka> Yukle() => Depo.Yukle<List<Vaka>>(Yol) ?? new();
    // Yazar.
    public static void Kaydet(List<Vaka> v) => Depo.YazJson(Yol, v, new JsonSerializerOptions { WriteIndented = true });
    // Yeni vaka.
    public static Vaka Yeni(string baslik, string ilkNot) => new(DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture), DateTime.Now, baslik.Trim(), "Açık", string.IsNullOrWhiteSpace(ilkNot) ? new() : new() { new VakaNotu(DateTime.Now, ilkNot.Trim()) });
    // Düz metin dökümü (paylaşmak/arşivlemek için).
    public static string Metin(Vaka v) => $"VAKA: {v.Baslik}\r\nDurum: {v.Durum}\r\nAçılış: {v.Acilis:dd.MM.yyyy HH:mm}\r\n\r\n" + string.Join("\r\n", v.Notlar.Select(n => $"[{n.Zaman:dd.MM.yyyy HH:mm}] {n.Metin}"));
}

// ---------------------------------------------------------------- 7) KENDİNİ KORUMA (MÜHÜR)
// Programın kendi ayar/referans dosyalarının izinsiz değiştirilip değiştirilmediğini anlamak için dosyalara mühür basar.
// TEK MEKANİK: her dosya Butunluk'un dosya başına DPAPI mührüyle (".mac") mühürlenir; koruma hizmeti çalışıyorsa özet ayrıca HKLM ikinci kanalına yazılır.
// (Eskiden ayrı bir HMAC dosyası ve anahtarı (muhur.json / muhur.key) vardı; iki ayrı doğrulama yolu iki ayrı atlatma yüzeyi demekti, kaldırıldı.)
internal static class Muhur
{
    // Mühürlenecek dosyalar.
    public static readonly string[] Dosyalar = { "altin_profil.json", "kabul_edilen.json", "cihaz_envanteri.json", "zamanlama.json", "vakalar.json", "kara_liste.txt", "webhook.json", "sensorler.json" };
    // İkinci kanaldaki kayıt adı.
    private static string Kanal(string ad) => "dosya-" + ad.ToLowerInvariant();

    // Mevcut dosyaları mühürler (yerel mühür + hizmet çalışıyorsa HKLM kaydı); kaç dosya mühürlendiğini verir.
    public static int Muhurle()
    {
        int n = 0;
        foreach (var ad in Dosyalar)
        {
            string y = BulutKlasor.Dosya(ad); if (!File.Exists(y)) continue;
            Butunluk.Muhurla(y); n++;
            if (ServisIstemci.Calisiyor) ServisIstemci.MuhurYaz(Kanal(ad), IkinciKanal.DosyaOzeti(y));
        }
        return n;
    }

    // Doğrular: her dosya için "Sağlam", "DEĞİŞMİŞ", "SİLİNMİŞ" veya "Mühürsüz (yeni)". Yerel mühür geçerli görünse bile HKLM kaydıyla uyuşmuyorsa (sahte mühür) DEĞİŞMİŞ sayılır.
    public static List<(string Dosya, string Durum)> Dogrula()
    {
        var s = new List<(string, string)>();
        foreach (var ad in Dosyalar)
        {
            string y = BulutKlasor.Dosya(ad); bool var = File.Exists(y); string? kayit = IkinciKanal.Oku(Kanal(ad)); bool yerelMuhurVar = File.Exists(Butunluk.MuhurYolu(y));
            if (!var) { if (yerelMuhurVar || kayit != null) s.Add((ad, "SİLİNMİŞ")); continue; }
            var yerel = Butunluk.Kontrol(y); bool kayitUyar = kayit != null && IkinciKanal.Esit(kayit, IkinciKanal.DosyaOzeti(y));
            string durum = yerel switch
            {
                ButunlukDurumu.Gecerli => kayit != null && !kayitUyar ? "DEĞİŞMİŞ" : "Sağlam",
                ButunlukDurumu.Bozuk => kayitUyar ? "Sağlam" : "DEĞİŞMİŞ",
                _ => kayit != null ? (kayitUyar ? "Sağlam" : "DEĞİŞMİŞ") : "Mühürsüz (yeni)"
            };
            if (durum == "Sağlam" && yerel != ButunlukDurumu.Gecerli) Butunluk.Muhurla(y);   // hizmet teyit ettiyse yerel mühür onarılır
            s.Add((ad, durum));
        }
        return s;
    }
}

// ---------------------------------------------------------------- 8) TANILAMA PAKETİ
// Destek almak için: sürüm, işletim sistemi ve son olay kayıtlarını bir zip'e koyar. Parola, API anahtarı, tarama sonucu veya kişisel dosya KOYMAZ.
internal static class TanilamaPaketi
{
    // Paketi oluşturur.
    public static string Olustur(string hedefKlasor)
    {
        // Dosya adı.
        string y = Path.Combine(hedefKlasor, "BulutSoft_Tanilama_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".zip");
        // Zip yaz.
        using var z = ZipFile.Open(y, ZipArchiveMode.Create);
        // Özet dosyası.
        var o = z.CreateEntry("ozet.txt"); using (var w = new StreamWriter(o.Open(), new UTF8Encoding(false)))
        {
            // Sürüm ve sistem bilgisi.
            w.WriteLine("BulutSoft Security Suite " + (typeof(TanilamaPaketi).Assembly.GetName().Version?.ToString() ?? "?")); w.WriteLine("İşletim sistemi: " + Environment.OSVersion + (Environment.Is64BitOperatingSystem ? " (64 bit)" : "")); w.WriteLine(".NET: " + Environment.Version); w.WriteLine("Zaman: " + DateTime.Now.ToString("o"));
            // Hangi ayar dosyaları var (içerik değil, yalnızca var/yok ve boyut).
            foreach (var ad in Muhur.Dosyalar.Concat(new[] { "olaylar.jsonl", "pencere.json" })) { string f = BulutKlasor.Dosya(ad); w.WriteLine($"{ad}: {(File.Exists(f) ? new FileInfo(f).Length + " bayt" : "yok")}"); }
            // Geçmiş sayısı.
            w.WriteLine("Kayıtlı tarama sayısı: " + GecmisDeposu.Listele().Count);
        }
        // Son 200 olay satırı (olay mesajları kişisel bilgi içerebilir; kullanıcı zip'i göndermeden önce okuyabilsin diye düz metin).
        if (File.Exists(OlayDeposu.Yol)) { var e = z.CreateEntry("son_olaylar.txt"); using var w = new StreamWriter(e.Open(), new UTF8Encoding(false)); foreach (var s in File.ReadAllLines(OlayDeposu.Yol).TakeLast(200)) w.WriteLine(s); }
        // Yol.
        return y;
    }
}

// ---------------------------------------------------------------- 9) BİLDİRİM KANALI (WEBHOOK)
// KRİTİK bir olay olunca, sizin verdiğiniz https adresine (Slack/Discord/Teams uyumlu) kısa bir metin gönderir. Varsayılan KAPALI; açmak onay ister.
internal sealed record WebhookAyari(bool Aktif, string Adres);

// Webhook yardımcısı.
internal static class Webhook
{
    // Dosya.
    public static string Yol => BulutKlasor.Dosya("webhook.json");
    // Test sırasında gönderimi yakalamak için kanca.
    public static Func<string, string, Task>? TestKancasi { get; set; }

    // Okur (yoksa kapalı).
    public static WebhookAyari Yukle() { return Depo.Yukle<WebhookAyari>(Yol) ?? new(false, ""); }
    // Yazar.
    public static void Kaydet(WebhookAyari a) => Depo.YazJson(Yol, a);
    // Adres güvenli mi? (yalnızca https, kullanıcı bilgisi olmayan, yerel olmayan)
    public static bool AdresGecerli(string a) => Uri.TryCreate(a, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.UserInfo) && !u.IsLoopback;

    // Mesajı gönderir (hata verirse sessizce false döner; program asla bozulmaz).
    public static async Task<bool> GonderAsync(string adres, string mesaj)
    {
        // Güvenlik: geçersiz adrese gönderme.
        if (!AdresGecerli(adres)) return false;
        // Çok uzun olmasın.
        string m = mesaj.Length > 300 ? mesaj[..300] + "…" : mesaj;
        // Test.
        if (TestKancasi != null) { await TestKancasi(adres, m); return true; }
        // Gönder.
        try
        {
            // İstemci.
            // Hem Slack/Teams ("text") hem Discord ("content") alanı (merkezi ağ istemcisi).
            using var r = await AgIstemciFabrikasi.YollaAsync(AgProfili.Webhook, adres, new StringContent(JsonSerializer.Serialize(new { text = m, content = m }), Encoding.UTF8, "application/json"));
            // Başarı.
            return r.IsSuccessStatusCode;
        }
        // Hata.
        catch { return false; }
    }

    // Kritik olayı (ayar açıksa) arka planda bildirir.
    public static void KritikBildir(string mesaj)
    {
        // Yeni bildirim merkezi (Telegram/Discord/Teams/genel): ayarlara, seviyeye, tekrar ve saatlik sınıra göre kendisi karar verir.
        // Eski tek adresli ayar, yeni dosya yoksa "Genel" kanal olarak oraya içe alınır; böylece çift gönderim olmaz.
        WebhookYoneticisi.Olay("Olay Günlüğü", "Kritik", mesaj);
    }
}
