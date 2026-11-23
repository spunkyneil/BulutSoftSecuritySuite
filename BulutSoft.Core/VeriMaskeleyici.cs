// Bu dosya dış servislere (örn. "Claude AI ile Derin Analiz") GİDECEK metindeki kişisel/ağa özgü bilgileri YEREL olarak maskeler (KVKK güvenli çıkış). Saf mantıktır; ağ/dosya işlemi yapmaz.
// Maskelenenler (her biri ayrı seçenek, varsayılan hepsi AÇIK):
//  • Özel ağ IPv4 adreslerinin SON OKTETİ:  192.168.1.15 → 192.168.1.x  (10/8, 172.16/12, 192.168/16, 169.254/16). Genel (internet) IP'lere ve 127.x'e dokunulmaz.
//  • Yerel disk yolları: C:\Program Files\Uygulama\dosya.exe → C:\…\dosya.exe (sürücü harfi ve dosya adı kalır, ara klasörler ve içindeki kullanıcı adı gizlenir).
//  • Kullanıcı adları: "\Users\<ad>" (yol maskesi kapalıysa) + verilen kullanıcı/bilgisayar adı listesi  → <kullanici> / <bilgisayar>.
// Sıra: önce yollar (içindeki kullanıcı adıyla birlikte), sonra kalan kullanıcı/bilgisayar adları, en son IP'ler. İşlem tekrarlandığında metin değişmez (idempotent).
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Maskeleme seçenekleri.
public sealed class MaskeSecenekleri
{
    // Özel IP'lerin son oktetini maskele.
    public bool OzelIpSonOktet { get; init; } = true;
    // Kullanıcı adlarını (\Users\<ad> ve verilen adlar) maskele.
    public bool KullaniciAdlari { get; init; } = true;
    // Yerel disk yollarını maskele.
    public bool YerelYollar { get; init; } = true;
    // Metinde aranacak kullanıcı adları (örn. Environment.UserName); 3 karakterden kısa adlar yanlış eşleşmesin diye atlanır.
    public IReadOnlyCollection<string> KullaniciAdListesi { get; init; } = Array.Empty<string>();
    // Metinde aranacak bilgisayar adları (örn. Environment.MachineName).
    public IReadOnlyCollection<string> BilgisayarAdListesi { get; init; } = Array.Empty<string>();

    // Bu bilgisayarın kullanıcı ve bilgisayar adlarıyla, tüm maskeler AÇIK varsayılan.
    public static MaskeSecenekleri Varsayilan() => new() { KullaniciAdListesi = new[] { Environment.UserName }, BilgisayarAdListesi = new[] { Environment.MachineName } };
}

// Maskeleme sonucu ve sayaçlar (onay penceresinde "N öğe gizlendi" göstermek için).
public sealed record MaskeSonucu(string Metin, int IpSayisi, int YolSayisi, int AdSayisi)
{
    // Toplam maskelenen öğe.
    public int Toplam => IpSayisi + YolSayisi + AdSayisi;
}

// Veri maskeleyici.
public static class VeriMaskeleyici
{
    private const string KullaniciYeri = "<kullanici>", BilgisayarYeri = "<bilgisayar>";
    private static readonly Regex Ip = new(@"(?<![\d.])((?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d))\.((?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d))\.((?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d))\.((?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d))(?![\d])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex UsersYolu = new(@"(?i)(\\Users\\)(?!<kullanici>)([^\\/:*?""<>|\s]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // Sürücü harfi + (boşluk içerebilen) klasörler + boşluksuz son bileşen; "…" ile yazılmış yollar yeniden maskelenmez.
    private static readonly Regex Yol = new(@"(?<![A-Za-z0-9])([A-Za-z]):\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\s]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Metni seçeneklere göre maskeler; null/boş metin boş döner.
    public static MaskeSonucu Maskele(string? metin, MaskeSecenekleri? secenek = null)
    {
        secenek ??= MaskeSecenekleri.Varsayilan();
        if (string.IsNullOrEmpty(metin)) return new MaskeSonucu("", 0, 0, 0);
        string m = metin; int ad = 0, yol = 0, ip = 0;
        // 1) Yerel yollar (kullanıcı adı yolun içindeyse birlikte gizlenir).
        if (secenek.YerelYollar)
            m = Yol.Replace(m, x =>
            {
                string tam = x.Value, kuyruk = ""; int son = tam.Length;
                while (son > 0 && ".,;:)]}'".Contains(tam[son - 1])) son--;   // cümle sonu noktalaması yolun parçası değildir
                if (son < tam.Length) { kuyruk = tam[son..]; tam = tam[..son]; }
                int ayrac = tam.LastIndexOf('\\'); string dosya = ayrac >= 0 ? tam[(ayrac + 1)..] : "";
                if (tam.Length <= 3 && dosya.Length == 0) return x.Value;                 // "C:\" tek başına yol değildir
                yol++; return x.Groups[1].Value + @":\…\" + dosya + kuyruk;
            });
        // 2) Kalan kullanıcı/bilgisayar adları ("\Users\<ad>" yolu yerel yol maskesi kapalıysa burada yakalanır).
        if (secenek.KullaniciAdlari)
        {
            m = UsersYolu.Replace(m, x => { ad++; return x.Groups[1].Value + KullaniciYeri; });
            m = AdlariDegistir(m, secenek.KullaniciAdListesi, KullaniciYeri, ref ad);
            m = AdlariDegistir(m, secenek.BilgisayarAdListesi, BilgisayarYeri, ref ad);
        }
        if (secenek.OzelIpSonOktet)
            m = Ip.Replace(m, x =>
            {
                int a = int.Parse(x.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), b = int.Parse(x.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                bool ozel = a == 10 || (a == 172 && b is >= 16 and <= 31) || (a == 192 && b == 168) || (a == 169 && b == 254);
                if (!ozel) return x.Value;
                ip++; return $"{x.Groups[1].Value}.{x.Groups[2].Value}.{x.Groups[3].Value}.x";
            });
        return new MaskeSonucu(m, ip, yol, ad);
    }

    // Verilen adların (tam sözcük, büyük/küçük harf duyarsız) metindeki geçişlerini yer tutucuyla değiştirir.
    private static string AdlariDegistir(string m, IReadOnlyCollection<string> adlar, string yerTutucu, ref int sayac)
    {
        int s = sayac;
        foreach (var a in adlar.Where(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length >= 3).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length))
            m = Regex.Replace(m, @"(?<![\p{L}\p{N}_<])" + Regex.Escape(a) + @"(?![\p{L}\p{N}_>])", _ => { s++; return yerTutucu; }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        sayac = s; return m;
    }
}
