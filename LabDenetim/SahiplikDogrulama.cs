// Bu dosya "ALAN ADI SAHİPLİĞİ DOĞRULAMA"dır. İnternetteki bir sitede nazik aktif denetim yapılabilmesi için, kullanıcının o siteyi gerçekten KONTROL ETTİĞİNİN kanıtlanması gerekir.
// Kanıt iki yoldan biridir (ikisi de yalnızca site sahibinin yapabileceği şeylerdir):
//   1) Sitenin /.well-known/bulutsoft-dogrulama.txt adresinde, programın verdiği gizli kodu içeren bir dosya yayınlamak, veya
//   2) Alan adının DNS'ine _bulutsoft.<alanadı> adıyla, aynı kodu içeren bir TXT kaydı eklemek.
// Doğrulama HER denetimde canlı olarak yeniden yapılır ve yalnızca bellekte 60 dakika geçerlidir (dosya silinirse denetim yapılamaz). Yalnızca alan adının KENDİSİ için geçerlidir (alt alanlar ayrıca doğrulanır).
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Bir alan adı için saklanan kayıt: gizli kod ve son başarılı doğrulama.
internal sealed record SahiplikKaydi(string Host, string Belirtec, DateTime Olusturma, DateTime? SonDogrulama, string Yontem);

// Doğrulama sonucu.
internal sealed record SahiplikSonucu(bool Dogrulandi, string Yontem, string Mesaj);

// Doğrulayıcı.
internal static class SahiplikDogrulama
{
    // Sabitler.
    public const string DosyaYolu = "/.well-known/bulutsoft-dogrulama.txt", DnsOn = "_bulutsoft"; public const int OturumDakika = 60;
    // Test için değiştirilebilir kaynaklar: dosya içeriğini okur (okunamazsa null); TXT kayıtlarını verir.
    public static Func<string, CancellationToken, Task<string?>> DosyaOku { get; set; } = CanliDosyaOku;
    public static Func<string, CancellationToken, Task<List<string>>> TxtOku { get; set; } = CanliTxtOku;
    // Bellekteki canlı doğrulamalar (alan adı -> doğrulama zamanı).
    private static readonly Dictionary<string, DateTime> Oturum = new(StringComparer.OrdinalIgnoreCase); private static readonly object Kilit = new();
    // Kayıt dosyası.
    private static string Yol => BulutKlasor.Dosya("sahiplik.json");

    // Kayıtları okur (bozuksa boş).
    private static List<SahiplikKaydi> Yukle() { return Depo.Yukle<List<SahiplikKaydi>>(Yol) ?? new(); }
    // Kayıtları yazar (yazılamazsa sessizce geçer; gizli kod her seferinde yeniden üretilebilir).
    private static void Kaydet(List<SahiplikKaydi> l) { try { Depo.YazJson(Yol, l, new JsonSerializerOptions { WriteIndented = true }); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SahiplikDogrulama.Kaydet", true); } }

    // Alan adı için gizli kodu verir (yoksa üretir ve saklar). Kod tahmin edilemez: 128 bit rastgele.
    public static string Belirtec(string host)
    {
        // Normalleştir.
        host = (host ?? "").Trim().ToLowerInvariant();
        // Kilit altında oku/yaz.
        lock (Kilit)
        {
            var l = Yukle(); var k = l.FirstOrDefault(x => x.Host == host); if (k != null) return k.Belirtec;
            string b = "bulutsoft-dogrulama=" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(); l.Add(new SahiplikKaydi(host, b, DateTime.UtcNow, null, "")); Kaydet(l); return b;
        }
    }

    // Kök dizindeki alternatif dosya adı.
    public const string KokDosya = "/bulutsoft-dogrulama.txt";

    // Sayfadaki herhangi bir <meta ...> etiketinin içinde gizli kod var mı? (yalnızca etiket içi sayılır; yorum/yazı içi sayılmaz)
    public static bool MetaVarMi(string html, string belirtec) =>
        System.Text.RegularExpressions.Regex.Matches(html ?? "", @"<meta\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500)).Any(m => m.Value.Contains("bulutsoft-dogrulama", StringComparison.OrdinalIgnoreCase) && KodVar(m.Value, belirtec));

    // Bu alan adı şu an (son 60 dakikada) canlı olarak doğrulandı mı? Aktif denetim modülü kapıda bunu sorar.
    public static bool OturumDogrulu(string host, DateTime? simdi = null)
    {
        // Kilit altında bak.
        lock (Kilit) { return Oturum.TryGetValue((host ?? "").Trim().ToLowerInvariant(), out var t) && ((simdi ?? DateTime.UtcNow) - t).TotalMinutes < OturumDakika; }
    }

    // Testler için bellekteki doğrulamaları siler.
    internal static void OturumuTemizle() { lock (Kilit) Oturum.Clear(); }

    // Kullanıcıya gösterilecek yapılacaklar metni.
    public static string Talimat(string host, string belirtec) =>
        $"Siteyi gerçekten yönettiğinizi kanıtlamak için AŞAĞIDAKİ YOLLARDAN BİRİNİ yapın (hangisi kolayınıza geliyorsa):\r\n\r\n" +
        $"YOL 1 — Ana sayfaya meta etiketi (dosya yükleyemiyorsanız en kolayı; WordPress'te tema/başlık ekleme eklentisiyle)\r\n  Ana sayfanızın <head> bölümüne şu satırı ekleyin:\r\n  <meta name=\"bulutsoft-dogrulama\" content=\"{belirtec}\">\r\n\r\n" +
        $"YOL 2 — Dosya\r\n  İçinde yalnızca aşağıdaki kod yazan bir metin dosyasını şu adreslerden BİRİNDE yayınlayın:\r\n  https://{host}{DosyaYolu}\r\n  https://{host}{KokDosya}   (ilki engelliyse bunu kullanın)\r\n  Dosya içeriği:  {belirtec}\r\n\r\n" +
        $"YOL 3 — DNS kaydı\r\n  Alan adı panelinizde şu TXT kayıtlarından BİRİNİ ekleyin:\r\n  Ad: {DnsOn}.{host}   (veya panel '_' kabul etmezse: {host})\r\n  Tür: TXT\r\n  Değer: {belirtec}\r\n\r\n" +
        "Sonra \"Şimdi Doğrula\" düğmesine basın. Doğrulama bittikten sonra dosyayı/kaydı silebilirsiniz; ama her yeni derin denetimde yeniden gerekir.";

    // Canlı doğrulama. Başarılıysa 60 dakikalık oturum açılır ve kayıt güncellenir.
    public static async Task<SahiplikSonucu> DogrulaAsync(string host, CancellationToken ct)
    {
        // Alan adı.
        var h = Kapsam.HostCikar(host); if (h == null) return new(false, "", "Geçerli bir alan adı yazın.");
        // İnternet sitesi olmalı.
        KapsamKarari karar; try { karar = await Kapsam.SiniflaAsync(h, ct); } catch (OperationCanceledException) { throw; } catch { return new(false, "", "Alan adı çözümlenemedi."); }
        if (karar.Mod != KapsamModu.DisAgPasif) return new(false, "", karar.Mod == KapsamModu.IcAg ? "Bu adres kendi ağınızda; doğrulama gerekmez (iç ağ denetimi doğrudan yapılır)." : karar.Neden);
        // Gizli kod.
        string b = Belirtec(h); bool ip = IPAddress.TryParse(h, out _);
        // Yol 1: dosya, iki konumda (".well-known" bazı sunucularda nokta ile başlayan klasör olduğu için engelli olabilir; ikinci konum kök dizindir). Önce https, sonra http.
        var gorulen = new List<string>();
        foreach (var yol in new[] { DosyaYolu, KokDosya })
            foreach (var sema in new[] { "https", "http" })
            {
                string? icerik; try { icerik = await DosyaOku($"{sema}://{h}{yol}", ct); } catch (OperationCanceledException) { throw; } catch { icerik = null; }
                if (icerik != null && KodVar(icerik, b)) return Basari(h, "dosya", $"Doğrulandı: {sema}://{h}{yol} dosyasında gizli kod bulundu.");
                gorulen.Add(icerik == null ? $"{sema}://{h}{yol}: {(Sebep.TryGetValue($"{sema}://{h}{yol}", out var sb1) && sb1.Length > 0 ? sb1 : "okunamadı veya dosya yok")}" : $"{sema}://{h}{yol}: açıldı ama gizli kod yok");
            }
        // Yol 2: ana sayfaya meta etiketi (dosya yükleyemeyen WordPress/hazır barındırma kullanıcıları için en kolayı).
        foreach (var sema in new[] { "https", "http" })
        {
            string? sayfa; try { sayfa = await DosyaOku($"{sema}://{h}/", ct); } catch (OperationCanceledException) { throw; } catch { sayfa = null; }
            if (sayfa != null && MetaVarMi(sayfa, b)) return Basari(h, "meta", $"Doğrulandı: {sema}://{h}/ ana sayfasındaki meta etiketinde gizli kod bulundu.");
            gorulen.Add(sayfa == null ? $"{sema}://{h}/ ana sayfa: {(Sebep.TryGetValue($"{sema}://{h}/", out var sb2) && sb2.Length > 0 ? sb2 : "okunamadı")}" : $"{sema}://{h}/ ana sayfa: açıldı ama meta etiketi yok");
        }
        // Yol 3: DNS TXT (IP adresi için uygulanmaz): önce _bulutsoft.<ad>, bu olmazsa (bazı DNS panelleri '_' ile başlayan adı kabul etmez) alan adının kendisi.
        if (!ip)
            foreach (var ad in new[] { $"{DnsOn}.{h}", h })
            {
                List<string> txt; try { txt = await TxtOku(ad, ct); } catch (OperationCanceledException) { throw; } catch { txt = new(); }
                if (txt.Any(x => KodVar(x, b))) return Basari(h, "dns", $"Doğrulandı: {ad} TXT kaydında gizli kod bulundu.");
                gorulen.Add($"DNS TXT {ad}: {(txt.Count == 0 ? "kayıt yok" : "kayıt var ama gizli kod yok")}");
            }
        // Olmadı: hangi yolda ne görüldüğünü açıkça söyle.
        return new(false, "", "Gizli kod hiçbir yolda bulunamadı. Denenenler:\r\n  • " + string.Join("\r\n  • ", gorulen) + "\r\n\r\nİpucu: kodu birebir (başında 'bulutsoft-dogrulama=' ile) yazdığınızdan, dosyanın/etiketin herkese açık olduğundan ve DNS değişikliğinin yayılması için birkaç dakika geçtiğinden emin olun.");
    }

    // Başarıyı kaydeder.
    private static SahiplikSonucu Basari(string host, string yontem, string mesaj)
    {
        // Oturum aç ve kaydı güncelle.
        lock (Kilit) { Oturum[host.ToLowerInvariant()] = DateTime.UtcNow; var l = Yukle(); int i = l.FindIndex(x => x.Host == host.ToLowerInvariant()); if (i >= 0) { l[i] = l[i] with { SonDogrulama = DateTime.UtcNow, Yontem = yontem }; Kaydet(l); } }
        return new(true, yontem, mesaj);
    }

    // ---- Canlı kaynaklar ----
    // Dosyayı yalnızca GENEL internet adreslerine bağlanarak okur (en çok 3 yönlendirme, yalnızca aynı alan adına; en çok 2 KB).
    private static async Task<string?> CanliDosyaOku(string url, CancellationToken ct)
    {
        // Güvenli işleyici: bağlantı anında adresler yeniden doğrulanır.
        var h = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(8) };
        h.ConnectCallback = async (ctx, c) =>
        {
            // Yalnızca genel adresler.
            // Adreslerin HEPSİ sırayla denenir (ilk adres erişilemeyen bir IPv6 ise ikinciye geçilir).
            var adr = await Kapsam.CozGenelAsync(ctx.DnsEndPoint.Host, c); Exception? son = null;
            foreach (var a in adr.Take(6)) { var s = new Socket(a.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true }; try { await s.ConnectAsync(new IPEndPoint(a, ctx.DnsEndPoint.Port), c); return new NetworkStream(s, true); } catch (Exception ex) when (ex is not OperationCanceledException) { s.Dispose(); son = ex; } }
            throw new HttpRequestException("Bağlanılamadı: " + son?.Message);
        };
        // Sertifika hatası bu okuma için engel değil (kanıt gizli koddur; hiçbir veri gönderilmez).
        h.SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true };
        using var c = new HttpClient(h, true) { Timeout = TimeSpan.FromSeconds(12) }; c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; BulutSoft-SahiplikDogrulama/1.0)");
        // Yönlendirmeleri aynı site ailesi içinde izle; neden okunamadığını Sebep sözlüğüne yaz.
        var u = new Uri(url); Sebep[url] = "";
        try
        {
            for (int i = 0; i < 4; i++)
            {
                // İstek.
                using var y = await c.SendAsync(new HttpRequestMessage(HttpMethod.Get, u), HttpCompletionOption.ResponseHeadersRead, ct); int kod = (int)y.StatusCode;
                // Yönlendirme.
                if (kod is >= 300 and <= 399 && y.Headers.Location != null) { var hedef = new Uri(u, y.Headers.Location); if (!AyniAile(hedef.Host, u.Host) || hedef.Scheme is not ("http" or "https")) { Sebep[url] = $"başka alan adına yönlendiriyor ({hedef.Host})"; return null; } u = hedef; continue; }
                // Başarısızsa yok (kodu not et: 403 = güvenlik duvarı engeli, 404 = dosya yok).
                if (kod != 200) { Sebep[url] = kod == 403 ? "HTTP 403 (erişim engellendi; güvenlik duvarı/CDN botları engelliyor olabilir)" : kod == 404 ? "HTTP 404 (dosya bulunamadı)" : $"HTTP {kod}"; return null; }
                // En çok 64 KB oku ve metne çevir (Not Defteri'nin Unicode/UTF-16 kaydı da okunur).
                await using var st = await y.Content.ReadAsStreamAsync(ct); var buf = new byte[65536]; int n = 0, r; while (n < buf.Length && (r = await st.ReadAsync(buf.AsMemory(n), ct)) > 0) n += r; return MetneCevir(buf, n);
            }
            Sebep[url] = "çok fazla yönlendirme"; return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Sebep[url] = "bağlanılamadı (" + (ex.InnerException?.Message ?? ex.Message).Split('\n')[0] + ")"; return null; }
    }

    // Son okumaların başarısızlık nedenleri (adres -> neden); mesajda kullanıcıya gösterilir.
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Sebep = new();

    // Baytları metne çevirir: UTF-16 (BOM'lu), UTF-8 BOM'lu ve düz UTF-8'i tanır; NUL karakterleri atar.
    internal static string MetneCevir(byte[] b, int n)
    {
        // Kodlama tespiti.
        Encoding e = Encoding.UTF8; int baslangic = 0;
        if (n >= 2 && b[0] == 0xFF && b[1] == 0xFE) { e = Encoding.Unicode; baslangic = 2; }
        else if (n >= 2 && b[0] == 0xFE && b[1] == 0xFF) { e = Encoding.BigEndianUnicode; baslangic = 2; }
        else if (n >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) baslangic = 3;
        // Çevir ve temizle.
        return e.GetString(b, baslangic, n - baslangic).Replace("\0", "");
    }

    // İçerik gizli kodu taşıyor mu? Tam kod ya da yalnızca 32 haneli kısmı (kullanıcı öneki yazmayı unutmuş olabilir) kabul edilir.
    public static bool KodVar(string icerik, string belirtec) => icerik.Contains(belirtec, StringComparison.Ordinal) || icerik.Contains(belirtec[(belirtec.IndexOf('=') + 1)..], StringComparison.OrdinalIgnoreCase);

    // İki ad aynı site ailesinde mi? (ornek.com <-> www.ornek.com yönlendirmesi çok yaygındır; başka alan adına gidilmez.)
    public static bool AyniAile(string a, string b)
    {
        // "www." önekini at ve karşılaştır.
        static string Kok(string x) => x.ToLowerInvariant().StartsWith("www.") ? x[4..].ToLowerInvariant() : x.ToLowerInvariant();
        return Kok(a) == Kok(b);
    }

    // TXT kayıtlarını mevcut küçük DNS istemcisiyle okur.
    private static async Task<List<string>> CanliTxtOku(string ad, CancellationToken ct) => (await DnsIstemci.SorgulaAsync(ad, 16, ct)).Select(x => x.Veri).ToList();
}
