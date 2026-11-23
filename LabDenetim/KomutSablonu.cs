// Bu dosya uzak Linux konağında (sensör) SSH üzerinden çalışabilecek komutların ŞABLON kaydını ve parametre doğrulamasını içerir. Araç-agnostiktir: hiçbir somut araç adı içermez.
// İlke: SERBEST KOMUT YOKTUR. Bir şablon yalnızca ad, kategori, süre sınırları ve parametre KURALLARINDAN oluşur; gerçek argüman listesi sensör ajanında SABİTTİR (SensorArac.cs ile aynı yaklaşım).
// Windows tarafı yalnızca kurallardan geçmiş DEĞERLERİ ajana gönderir; değerler hiçbir zaman bir komut metnine birleştirilmez (kabuğa metin enjekte edilemez).
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Bir parametrenin kuralı: ad, değerin TAMAMININ uyması gereken desen (düzenli ifade) ve varsayılan değer. Boş varsayılan = parametre ZORUNLU.
// Min/Max verilirse değer ayrıca 0-999999999 arası bir tam sayı olmalı ve bu aralıkta kalmalıdır.
internal sealed record ParametreKurali(string Ad, string DogrulamaDeseni, string Varsayilan, int? Min = null, int? Max = null);

// Bir komut şablonu. Arac/Paket isteğe bağlıdır: boşsa araç adı Ad'dır, paket adı da araç adıdır (kurulu mu denetimi ve "apt install" için kullanılır).
// HedefTuru: "ip" (yalnızca IPv4), "alan" (yalnızca alan adı), "ip-alan" (ikisi de) ya da "yok" (şablon hedef almaz; hedef boş olmalı).
internal sealed record KomutSablonu(string Ad, string Kategori, bool SudoGerekli, int TahminiSaniye, int ZamanAsimiSn, IReadOnlyList<ParametreKurali> Parametreler, string AyrastiriciAdi, string Arac = "", string Paket = "", string HedefTuru = "ip-alan")
{
    // Sensörde aranacak çalıştırılabilir dosyanın adı.
    public string AracAdi => string.IsNullOrEmpty(Arac) ? Ad : Arac;
    // Kurulu değilse kurulacak paketin adı.
    public string PaketAdi => string.IsNullOrEmpty(Paket) ? AracAdi : Paket;
}

// Şablon deposu + doğrulama.
internal static class KomutSablonuDeposu
{
    // Bir değer en çok bu kadar karakter olabilir.
    public const int EnUzunDeger = 256;
    // Desen değerlendirmesi için süre sınırı (kötü niyetli/hatalı desenle program kilitlenmesin).
    private static readonly TimeSpan DesenSuresi = TimeSpan.FromMilliseconds(250);
    // Ad kuralları: küçük harf, rakam, nokta, alt çizgi, tire (dosya yolu ve ajan için güvenli).
    private static readonly Regex AdDeseni = new(@"\A[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ParametreAdDeseni = new(@"\A[a-z][a-z0-9_]{0,31}\z", RegexOptions.CultureInvariant);
    private static readonly Regex AracDeseni = new(@"\A[A-Za-z0-9][A-Za-z0-9._+-]{0,63}\z", RegexOptions.CultureInvariant);
    private static readonly Regex PaketDeseni = new(@"\A[a-z0-9][a-z0-9.+-]{1,63}\z", RegexOptions.CultureInvariant);

    // Kayıtlar (aynı anda birden çok iş parçacığı kullanabilir).
    private static readonly object Kilit = new();
    private static readonly Dictionary<string, KomutSablonu> Kayitlar = new(StringComparer.Ordinal);

    // Şablonun kendisi kurallara uyuyor mu? Uymuyorsa nedenini verir, uyuyorsa null.
    public static string? SablonHatasi(KomutSablonu? s)
    {
        // Boş şablon.
        if (s == null) return "Şablon boş.";
        // Ad ve kategori.
        if (!AdDeseni.IsMatch(s.Ad ?? "")) return "Şablon adı küçük harf, rakam, '.', '_' veya '-' içermeli ve en çok 64 karakter olmalı.";
        if (!AdDeseni.IsMatch(s.Kategori ?? "")) return "Kategori adı küçük harf, rakam, '.', '_' veya '-' içermeli ve en çok 64 karakter olmalı.";
        // Süreler.
        if (s.ZamanAsimiSn is < 1 or > 21600) return "Zaman aşımı 1-21600 saniye arasında olmalı.";
        if (s.TahminiSaniye is < 0 or > 86400) return "Tahmini süre 0-86400 saniye arasında olmalı.";
        // Ayrıştırıcı adı boş olabilir (varsayılan ayrıştırıcı kullanılır).
        if (s.AyrastiriciAdi == null || (s.AyrastiriciAdi.Length > 0 && !AdDeseni.IsMatch(s.AyrastiriciAdi))) return "Ayrıştırıcı adı geçersiz.";
        // Araç ve paket adları kabuğa gidebilecek tek yerdir (kurulu mu denetimi, apt): sıkı kural.
        if (!AracDeseni.IsMatch(s.AracAdi)) return "Araç adı harf, rakam, '.', '_', '+' veya '-' içermeli (en çok 64 karakter).";
        if (!PaketDeseni.IsMatch(s.PaketAdi)) return "Paket adı küçük harf, rakam, '.', '+' veya '-' içermeli (2-64 karakter).";
        // Hedef türü.
        if (s.HedefTuru is not ("ip" or "alan" or "ip-alan" or "yok")) return "Hedef türü 'ip', 'alan', 'ip-alan' veya 'yok' olmalı.";
        // Parametreler.
        if (s.Parametreler == null || s.Parametreler.Count > 16) return "Parametre listesi yok veya 16'dan fazla.";
        var gorulen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in s.Parametreler)
        {
            if (k == null || !ParametreAdDeseni.IsMatch(k.Ad ?? "")) return "Parametre adı küçük harfle başlamalı; küçük harf, rakam, '_' içerebilir (en çok 32 karakter).";
            if (!gorulen.Add(k.Ad!)) return $"'{k.Ad}' parametresi iki kez tanımlı.";
            if (string.IsNullOrEmpty(k.DogrulamaDeseni) || k.DogrulamaDeseni.Length > 200) return $"'{k.Ad}' parametresinin deseni boş veya 200 karakterden uzun.";
            // Desen derlenebilmeli.
            try { _ = new Regex(k.DogrulamaDeseni, RegexOptions.CultureInvariant, DesenSuresi); } catch (ArgumentException) { return $"'{k.Ad}' parametresinin deseni geçerli bir düzenli ifade değil."; }
            // Sayı sınırları tutarlı olmalı.
            if ((k.Min is < 0 or > 999999999) || (k.Max is < 0 or > 999999999) || (k.Min > k.Max)) return $"'{k.Ad}' parametresinin sayı sınırları geçersiz.";
            // Varsayılan değer de kendi kuralından geçmeli.
            if (k.Varsayilan == null) return $"'{k.Ad}' parametresinin varsayılanı null olamaz (zorunlu için boş metin verin).";
            if (k.Varsayilan.Length > 0) { var h = DegerHatasi(k, k.Varsayilan); if (h != null) return $"'{k.Ad}' parametresinin varsayılan değeri kendi kuralına uymuyor: {h}"; }
        }
        // Sorun yok.
        return null;
    }

    // Şablonu kaydeder. Geçersizse ArgumentException, aynı adlı şablon varsa InvalidOperationException (sessizce üzerine YAZILMAZ).
    public static void Ekle(KomutSablonu sablon)
    {
        // Doğrula.
        var h = SablonHatasi(sablon); if (h != null) throw new ArgumentException(h, nameof(sablon));
        // Çağıranın elindeki liste sonradan değiştirilip doğrulama atlatılmasın: kayıttan önce kopyala.
        var kopya = sablon with { Parametreler = sablon.Parametreler.ToArray() };
        // Ekle.
        lock (Kilit) { if (!Kayitlar.TryAdd(kopya.Ad, kopya)) throw new InvalidOperationException($"'{kopya.Ad}' adlı şablon zaten kayıtlı (sessizce üzerine yazılmaz)."); }
    }

    // Adıyla şablonu verir; yoksa null.
    public static KomutSablonu? Getir(string? ad) { if (ad == null) return null; lock (Kilit) return Kayitlar.TryGetValue(ad, out var s) ? s : null; }

    // Tüm şablonların kopyası.
    public static IReadOnlyList<KomutSablonu> Hepsi() { lock (Kilit) return Kayitlar.Values.ToList(); }

    // Tüm kayıtları siler (testler ve yeniden yükleme için).
    public static void Temizle() { lock (Kilit) Kayitlar.Clear(); }

    // Tek değeri kuralına göre denetler. Hata varsa nedenini verir (DEĞERİN KENDİSİ mesaja yazılmaz: günlük kirletilemesin).
    internal static string? DegerHatasi(ParametreKurali k, string? v)
    {
        // Boş/çok uzun.
        if (v == null) return "değer yok.";
        if (v.Length > EnUzunDeger) return $"değer {EnUzunDeger} karakterden uzun.";
        // Yalnızca yazdırılabilir ASCII: satır sonu/denetim karakteri ve Unicode karakterler yok (\d gibi desenler .NET'te Arapça-Hint rakamlarını da eşleştirir; ajanda farklı yorumlanmasın).
        foreach (char c in v) if (c < ' ' || c > '~') return "değer yalnızca yazdırılabilir ASCII karakterler içerebilir (satır sonu, denetim ve Unicode karakterleri yok).";
        // '-' ile başlayan değer araca bayrak olarak ulaşabilir: her zaman reddedilir.
        if (v.Length > 0 && v[0] == '-') return "değer '-' ile başlayamaz (araç bayrağı gibi yorumlanabilir).";
        // Desen değerin TAMAMINA uymalı: \A ve \z kullanılır ('$' sonda satır sonuna izin verirdi).
        try { if (!Regex.IsMatch(v, @"\A(?:" + k.DogrulamaDeseni + @")\z", RegexOptions.CultureInvariant, DesenSuresi)) return "değer kuralına uymuyor."; }
        catch (RegexMatchTimeoutException) { return "kural değerlendirmesi zaman aşımına uğradı."; }
        catch (ArgumentException) { return "kural geçersiz."; }
        // Sayı sınırı (varsa): yalnızca ASCII rakam, en çok 9 hane, aralık içinde.
        if (k.Min != null || k.Max != null)
        {
            if (!Regex.IsMatch(v, @"\A[0-9]{1,9}\z", RegexOptions.CultureInvariant)) return "değer bir tam sayı olmalı.";
            long n = long.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
            if ((k.Min != null && n < k.Min) || (k.Max != null && n > k.Max)) return $"değer {k.Min?.ToString() ?? "-"} ile {k.Max?.ToString() ?? "-"} arasında olmalı.";
        }
        return null;
    }

    // Kullanıcı girdilerini şablonun kurallarından geçirir ve doğrulanmış değerleri şablonun parametre SIRASIYLA verir.
    // Kural dışı değer, şablonda tanımsız parametre ya da eksik zorunlu parametre → InvalidOperationException. Serbest metin birleştirme YOKTUR.
    public static IReadOnlyList<string> Uyarilastir(KomutSablonu sablon, IReadOnlyDictionary<string, string>? girdiler)
    {
        // Boş şablon.
        ArgumentNullException.ThrowIfNull(sablon);
        girdiler ??= new Dictionary<string, string>();
        // Şablonda olmayan parametre sessizce yutulmaz.
        foreach (var ad in girdiler.Keys) if (!sablon.Parametreler.Any(k => k.Ad == ad)) throw new InvalidOperationException($"'{sablon.Ad}' şablonunda tanımlı olmayan bir parametre gönderildi.");
        // Her parametre kendi kuralından geçer.
        var sonuc = new List<string>(sablon.Parametreler.Count);
        foreach (var k in sablon.Parametreler)
        {
            // Değer: girdi yoksa varsayılan; varsayılan da boşsa zorunlu parametre eksik.
            string? v = girdiler.TryGetValue(k.Ad, out var g) && g != null ? g : k.Varsayilan;
            if (string.IsNullOrEmpty(v)) throw new InvalidOperationException($"'{sablon.Ad}' şablonu: '{k.Ad}' parametresi zorunlu.");
            var h = DegerHatasi(k, v); if (h != null) throw new InvalidOperationException($"'{sablon.Ad}' şablonu, '{k.Ad}' parametresi: {h}");
            sonuc.Add(v);
        }
        return sonuc;
    }
}
