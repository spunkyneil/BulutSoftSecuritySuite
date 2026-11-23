// Bu dosya komut şablonlarının HEDEFİNİ doğrulayan katmandır: izin dışı hedefe hiçbir komut gönderilmez.
// Kurallar (kategoriden bağımsız temel kural):
//   • IPv4 adresi  : yalnızca özel ağ aralıkları (RFC 1918: 10/8, 172.16/12, 192.168/16) VEYA "izinli hedef listesi"ndeki adres/aralık.
//   • Alan adı     : yalnızca izinli hedef listesindeki TAM eşleşme (alt alan adları otomatik izinli SAYILMAZ; "musteri-staging.example" gibi).
//   • Diğer her şey: IPv6, kapı eki, yol, aralık, boşluk, denetim karakteri, "localhost", 127.x, 169.254.x, 100.64.x ... reddedilir.
// Kategoriye özgü ek kurallar genişleme noktasıyla (KategoriKuraliKaydet) eklenir; temel kuralı gevşetemez, yalnızca daraltır.
// Bu katman yalnızca YAZILAN adı doğrular. Alan adının çözüldüğü IP'nin de izinli olması (DNS yönlendirmesi) ajan tarafında ayrıca denetlenmelidir.
// Red durumunda neden metni döner; çağıran taraf bunu olay günlüğüne yazar.
using System.Globalization;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Hedef doğrulama.
internal static class HedefDogrulama
{
    // Aynı anda birden çok iş parçacığı kullanabilir.
    private static readonly object Kilit = new();
    // İzinli alan adları (küçük harf, tam eşleşme).
    private static readonly HashSet<string> IzinliAlanlar = new(StringComparer.Ordinal);
    // İzinli IPv4 adres/aralıkları (ağ adresi + maske).
    private static readonly List<(uint Ag, uint Maske, string Metin)> IzinliAglar = new();
    // Kategori → ek kural (hata metni ya da null).
    private static readonly Dictionary<string, Func<string, string?>> KategoriKurallari = new(StringComparer.OrdinalIgnoreCase);
    // Alan adı biçimi: en az iki etiket, her etiket 1-63 karakter, harf/rakam/tire; baş/son tire yok.
    private static readonly Regex AlanDeseni = new(@"\A(?=.{1,253}\z)[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // ---------------------------------------------------------------- Temel adres işlemleri
    // "a.b.c.d" biçimini KATI çözer: 4 parça, her biri 0-255, baştaki sıfır YOK ("010.0.0.1" ve "10.1" gibi belirsiz biçimler reddedilir).
    internal static bool Ipv4Coz(string s, out uint deger)
    {
        // Başlangıç.
        deger = 0; if (string.IsNullOrEmpty(s)) return false;
        var p = s.Split('.'); if (p.Length != 4) return false;
        uint d = 0;
        foreach (var x in p)
        {
            if (x.Length is < 1 or > 3 || !x.All(char.IsAsciiDigit) || (x.Length > 1 && x[0] == '0')) return false;
            int n = int.Parse(x, CultureInfo.InvariantCulture); if (n > 255) return false;
            d = (d << 8) | (uint)n;
        }
        deger = d; return true;
    }

    // RFC 1918 özel ağ mı? (127.x, 169.254.x, 100.64.x ve 0.x bilerek DEĞİL.)
    internal static bool OzelAgMi(uint d) => (d >> 24) == 10 || (d >> 20) == 0xAC1 || (d >> 16) == 0xC0A8;

    // Alan adı biçimi geçerli mi? (küçük harfe çevrilmiş metin beklenir; son etiket en az bir harf içermeli: "1.2.3.4" alan adı sayılmaz.)
    internal static bool AlanAdiGecerli(string s)
    {
        try { if (!AlanDeseni.IsMatch(s)) return false; } catch (RegexMatchTimeoutException) { return false; }
        return s[(s.LastIndexOf('.') + 1)..].Any(char.IsAsciiLetter);
    }

    // ---------------------------------------------------------------- İzinli hedef listesi ("TestHedefleri")
    // Listeye bir girdi ekler: tek IPv4, IPv4 aralığı ("192.0.2.0/24"; önek 24-32) veya alan adı. Geçersizse ArgumentException.
    public static void TestHedefiEkle(string girdi)
    {
        // Boş girdi.
        if (string.IsNullOrWhiteSpace(girdi)) throw new ArgumentException("İzinli hedef girdisi boş.", nameof(girdi));
        girdi = girdi.Trim();
        // Aralık.
        int k = girdi.IndexOf('/');
        if (k >= 0)
        {
            string onek = girdi[(k + 1)..];
            if (!Ipv4Coz(girdi[..k], out uint ag) || !Regex.IsMatch(onek, @"\A\d{2}\z") || int.Parse(onek, CultureInfo.InvariantCulture) is < 24 or > 32) throw new ArgumentException("Aralık 'a.b.c.d/24' ile '/32' arasında geçerli bir IPv4 aralığı olmalı (geniş aralıklar kabul edilmez).", nameof(girdi));
            uint maske = uint.MaxValue << (32 - int.Parse(onek, CultureInfo.InvariantCulture));
            lock (Kilit) IzinliAglar.Add((ag & maske, maske, girdi));
            return;
        }
        // Tek IPv4.
        if (Ipv4Coz(girdi, out uint tek)) { lock (Kilit) IzinliAglar.Add((tek, uint.MaxValue, girdi)); return; }
        // Alan adı.
        string ad = girdi.ToLowerInvariant();
        if (!AlanAdiGecerli(ad)) throw new ArgumentException("Girdi geçerli bir IPv4 adresi, IPv4 aralığı ya da alan adı (örn. musteri-staging.example) değil.", nameof(girdi));
        lock (Kilit) IzinliAlanlar.Add(ad);
    }

    // Listeyi boşaltır.
    public static void TestHedefleriniTemizle() { lock (Kilit) { IzinliAlanlar.Clear(); IzinliAglar.Clear(); } }

    // Listenin kopyası (denetim/gösterim için).
    public static IReadOnlyList<string> TestHedefleri() { lock (Kilit) return IzinliAglar.Select(x => x.Metin).Concat(IzinliAlanlar).ToList(); }

    // Satır satır bir dosyadan yükler ('#' ile başlayan ve boş satırlar atlanır). Geçersiz satırlar yüklenmez ve döndürülür. Dosya yoksa hiçbir şey yapılmaz.
    public static (int Eklenen, List<string> Reddedilen) DosyadanYukle(string yol)
    {
        // Sonuç.
        int eklenen = 0; var red = new List<string>();
        if (string.IsNullOrEmpty(yol) || !File.Exists(yol)) return (0, red);
        foreach (var ham in File.ReadLines(yol))
        {
            string s = ham.Trim(); if (s.Length == 0 || s[0] == '#') continue;
            try { TestHedefiEkle(s); eklenen++; } catch (ArgumentException) { red.Add(s.Length > 80 ? s[..80] : s); }
        }
        return (eklenen, red);
    }

    // ---------------------------------------------------------------- Kategori genişleme noktası
    // Bir kategori için EK kural kaydeder (hedef kabul edilecekse null, reddedilecekse neden metni döndürmeli). Temel kuraldan sonra çalışır.
    public static void KategoriKuraliKaydet(string kategori, Func<string, string?> kural)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kategori); ArgumentNullException.ThrowIfNull(kural);
        lock (Kilit) KategoriKurallari[kategori] = kural;
    }

    // Kategori kuralını kaldırır.
    public static void KategoriKuraliniKaldir(string kategori) { lock (Kilit) KategoriKurallari.Remove(kategori ?? ""); }

    // ---------------------------------------------------------------- Şablonun hedef türü
    // Şablonun istediği hedef türüne uyuyor mu? ("yok": hedef boş olmalı; "ip": yalnızca IPv4; "alan": yalnızca alan adı; "ip-alan": ikisi de.) Temel doğrulamadan ÖNCE/SONRA çağrılabilir.
    public static string? TurHatasi(string tur, string hedef)
    {
        // Hedef almayan şablon.
        if (tur == "yok") return string.IsNullOrEmpty(hedef) ? null : "Bu şablon hedef almaz; hedef boş olmalı.";
        // IPv4 biçimi mi?
        bool ipMi = !string.IsNullOrEmpty(hedef) && hedef.All(c => char.IsAsciiDigit(c) || c == '.');
        if (tur == "ip" && !ipMi) return "Bu şablon bir IPv4 adresi ister (alan adı değil).";
        if (tur == "alan" && ipMi) return "Bu şablon bir alan adı ister (IP adresi değil).";
        return null;
    }

    // ---------------------------------------------------------------- Ana doğrulama
    // Hedef uygun değilse neden metnini verir; uygunsa null. "hedef", ajana gönderilecek BİREBİR metin olmalıdır (burada kırpılmaz; boşluk içeren hedef reddedilir).
    public static string? HedefHatasi(string kategori, string hedef)
    {
        // Boş ve uzunluk.
        if (string.IsNullOrEmpty(hedef)) return "Hedef boş.";
        if (hedef.Length > 253) return "Hedef çok uzun.";
        // Yalnızca harf, rakam, nokta ve tire: boşluk, denetim karakteri, kapı eki, IPv6, yol ve aralık kabul edilmez.
        foreach (char c in hedef) if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '-')) return "Hedef yalnızca harf, rakam, '.' ve '-' içerebilir; boşluk, kapı eki, IPv6, adres aralığı ve yol desteklenmez (tek bir adres ya da alan adı yazın).";
        if (hedef[0] is '-' or '.') return "Hedef '-' veya '.' ile başlayamaz.";
        // Yalnızca rakam ve noktadan oluşuyorsa IPv4 olarak değerlendirilir.
        if (hedef.All(c => char.IsAsciiDigit(c) || c == '.'))
        {
            if (!Ipv4Coz(hedef, out uint ip)) return $"Hedef geçerli bir IPv4 adresi değil: {hedef}";
            bool izinli; lock (Kilit) izinli = IzinliAglar.Any(x => (ip & x.Maske) == x.Ag);
            if (!OzelAgMi(ip) && !izinli) return $"Hedef özel ağ (RFC 1918) dışında ve izinli hedef listesinde değil: {hedef}";
        }
        else
        {
            // Alan adı: yalnızca listedeki TAM eşleşme.
            string ad = hedef.ToLowerInvariant();
            if (!AlanAdiGecerli(ad)) return $"Hedef geçerli bir alan adı değil: {hedef}";
            bool izinli; lock (Kilit) izinli = IzinliAlanlar.Contains(ad);
            if (!izinli) return $"Alan adı izinli hedef listesinde değil: {hedef}";
        }
        // Kategoriye özgü ek kural (hata verirse güvenli tarafta kalınır: reddedilir).
        Func<string, string?>? kural; lock (Kilit) KategoriKurallari.TryGetValue(kategori ?? "", out kural);
        if (kural != null)
        {
            try { var h = kural(hedef); if (h != null) return h; }
            catch (Exception) { return "Kategori kuralı değerlendirilemedi; hedef güvenlik için reddedildi."; }
        }
        // Uygun.
        return null;
    }
}
