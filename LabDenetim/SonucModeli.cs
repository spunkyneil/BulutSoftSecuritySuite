// Bu dosya komut şablonu çalıştırmalarının SONUÇ KAYDINI ve çıktıyı bulgulara çeviren ayrıştırıcıları içerir. Araç-agnostiktir.
// Sensörden gelen çıktı GÜVENİLMEZ kabul edilir: denetim karakterleri temizlenir, uzunluklar sınırlanır, ham çıktı ayrı bir dosyaya yazılır.
// Not: "AracSonucu" adı SensorArac.cs'te (sabit beş araç için) zaten kullanıldığından bu genel kayıt "KomutSonucu" adını taşır.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Bir şablon çalıştırmasının sonucu. CikisKodu: aracın çıkış kodu; -2 = çalıştırılmadı (atlandı), 124 = zaman aşımı.
// Komut: şablon çağrısının özeti (gerçek argüman listesi sensör ajanında sabittir).
internal sealed record KomutSonucu(string SablonAdi, string Hedef, string Komut, DateTime Baslangic, TimeSpan Sure, int CikisKodu, string HamCiktiDosyasi, List<Bulgu> Bulgular);

// Çıktıyı bulgulara çeviren ayrıştırıcı. Şablonun AyrastiriciAdi alanı bunlardan birini seçer.
internal interface IAyirici
{
    // Depodaki kayıt adı.
    string Ad { get; }
    // Aracın (temizlenmiş) çıktısından bulgu listesi üretir. Bulgunun Modul alanı şablon adıdır.
    List<Bulgu> Ayristir(string sablonAdi, string hedef, string cikti);
}

// Örnek ayrıştırıcı 1: her boş olmayan satır bir "Bilgi" bulgusu olur (en çok 200; fazlası özet satırıyla bildirilir).
internal sealed class SatirListesiAyirici : IAyirici
{
    public const int EnFazlaBulgu = 200;
    public string Ad => "satir-listesi";
    public List<Bulgu> Ayristir(string sablonAdi, string hedef, string cikti)
    {
        // Boş çıktı.
        var l = new List<Bulgu>(); if (string.IsNullOrWhiteSpace(cikti)) return l;
        int toplam = 0;
        foreach (var ham in cikti.Split('\n'))
        {
            // Temizle ve boşları atla.
            string s = SensorArac.Temizle(ham, 500).Trim(); if (s.Length == 0) continue; toplam++;
            if (l.Count < EnFazlaBulgu) l.Add(new Bulgu(sablonAdi, s.Length > 80 ? s[..80] + "…" : s, Onem.Bilgi, $"Hedef: {hedef}\n{s}", "Ham çıktı dosyasıyla birlikte inceleyin."));
        }
        // Fazlası.
        if (toplam > EnFazlaBulgu) l.Add(new Bulgu(sablonAdi, "Çıktı kısaltıldı", Onem.Bilgi, $"{toplam} satırın ilk {EnFazlaBulgu} tanesi bulguya çevrildi; kalanı ham çıktı dosyasındadır.", "Ham çıktı dosyasına bakın."));
        return l;
    }
}

// Örnek ayrıştırıcı 2: "anahtar: değer" veya "anahtar=değer" satırları bir "Bilgi" bulgusu olur (başlık = anahtar, ayrıntı = değer; en çok 200).
internal sealed class AnahtarDegerAyirici : IAyirici
{
    public const int EnFazlaBulgu = 200;
    private static readonly Regex Satir = new(@"\A([^:=\s][^:=]{0,59}?)\s*[:=]\s*(\S.*)\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public string Ad => "anahtar-deger";
    public List<Bulgu> Ayristir(string sablonAdi, string hedef, string cikti)
    {
        // Boş çıktı.
        var l = new List<Bulgu>(); if (string.IsNullOrWhiteSpace(cikti)) return l;
        int toplam = 0;
        foreach (var ham in cikti.Split('\n'))
        {
            // Satırı ayrıştır (desen zaman aşımına uğrarsa satır atlanır).
            string s = SensorArac.Temizle(ham, 500).Trim(); if (s.Length == 0) continue;
            Match m; try { m = Satir.Match(s); } catch (RegexMatchTimeoutException) { continue; }
            if (!m.Success) continue; toplam++;
            if (l.Count < EnFazlaBulgu) l.Add(new Bulgu(sablonAdi, m.Groups[1].Value.Trim(), Onem.Bilgi, $"Hedef: {hedef}\n{m.Groups[2].Value.Trim()}", "Ham çıktı dosyasıyla birlikte inceleyin."));
        }
        // Fazlası.
        if (toplam > EnFazlaBulgu) l.Add(new Bulgu(sablonAdi, "Çıktı kısaltıldı", Onem.Bilgi, $"{toplam} satırın ilk {EnFazlaBulgu} tanesi bulguya çevrildi; kalanı ham çıktı dosyasındadır.", "Ham çıktı dosyasına bakın."));
        return l;
    }
}

// Ayrıştırıcı kayıtları.
internal static class AyiriciDeposu
{
    // Şablonda ayrıştırıcı adı boşsa ya da bulunamazsa kullanılan ayrıştırıcı.
    public const string Varsayilan = "satir-listesi";
    private static readonly object Kilit = new();
    private static readonly Dictionary<string, IAyirici> Kayitlar = new(StringComparer.Ordinal) { ["satir-listesi"] = new SatirListesiAyirici(), ["anahtar-deger"] = new AnahtarDegerAyirici() };

    // Yeni ayrıştırıcı kaydeder (aynı ad varsa sessizce üzerine yazılmaz).
    public static void Kaydet(IAyirici a)
    {
        ArgumentNullException.ThrowIfNull(a);
        lock (Kilit) { if (!Kayitlar.TryAdd(a.Ad, a)) throw new InvalidOperationException($"'{a.Ad}' adlı ayrıştırıcı zaten kayıtlı."); }
    }

    // Adıyla ayrıştırıcıyı verir; yoksa null.
    public static IAyirici? Getir(string? ad) { if (ad == null) return null; lock (Kilit) return Kayitlar.TryGetValue(ad, out var a) ? a : null; }

    // Adı boşsa ya da bulunamazsa varsayılanı verir; "bulundu" adın gerçekten bulunup bulunmadığını söyler (boş ad "bulundu" sayılır).
    public static IAyirici GetirVeyaVarsayilan(string? ad, out bool bulundu)
    {
        // Boş ad: varsayılan.
        if (string.IsNullOrEmpty(ad)) { bulundu = true; return Getir(Varsayilan)!; }
        var a = Getir(ad); bulundu = a != null; return a ?? Getir(Varsayilan)!;
    }
}

// Ham çıktı dosyaları: "KaliCikti/{kategori}/{ad}/{tarih}.txt" (kullanıcı veri klasörü altında).
internal static class SonucKaydi
{
    // Klasör adı.
    public const string Klasor = "KaliCikti";
    // Windows'ta klasör adı olamayan adlar.
    private static readonly Regex Ayrilmis = new(@"\A(con|prn|aux|nul|com[1-9]|lpt[1-9])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Yol bileşeni olarak güvenli ad: yalnızca harf, rakam, '-' ve '_' (nokta hiç yok: "..", gizli ad ve yol kaçışı olamaz).
    internal static string GuvenliAd(string? s)
    {
        // Karakter karakter süz.
        var sb = new StringBuilder(); foreach (char c in s ?? "") sb.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        string r = sb.Length == 0 ? "_" : sb.ToString(); if (r.Length > 64) r = r[..64];
        return Ayrilmis.IsMatch(r) ? "_" + r : r;
    }

    // Ham çıktıyı yeni bir dosyaya yazar ve tam yolunu verir. Var olan dosyanın üzerine YAZMAZ.
    public static string HamCiktiYaz(string kategori, string ad, string cikti)
    {
        // Klasör kökün altında kalmalı (ek güvence).
        string kok = Path.GetFullPath(BulutKlasor.Dosya(Klasor));
        string dizin = Path.GetFullPath(Path.Combine(kok, GuvenliAd(kategori), GuvenliAd(ad)));
        if (!dizin.StartsWith(kok + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Çıktı klasörü beklenen kökün dışına çıkıyor.");
        Directory.CreateDirectory(dizin);
        // Tarihli, çakışmaz ad.
        string tarih = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        for (int i = 0; i < 100; i++)
        {
            string yol = Path.Combine(dizin, tarih + (i == 0 ? "" : "_" + i.ToString(CultureInfo.InvariantCulture)) + ".txt");
            if (File.Exists(yol)) continue;
            using var fs = new FileStream(yol, FileMode.CreateNew, FileAccess.Write, FileShare.None); using var w = new StreamWriter(fs, new UTF8Encoding(false)); w.Write(cikti ?? "");
            return yol;
        }
        throw new IOException("Çıktı dosyası için benzersiz ad üretilemedi.");
    }
}
