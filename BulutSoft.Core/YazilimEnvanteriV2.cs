// Bu dosya Hafta 6'nın envanter tarafıdır: kurulu yazılım listesi (salt-okunur), yazılım adı → CPE (vendor:product) eşleme ve sürüm karşılaştırma.
// CPE eşleme GÜVEN PUANLI çalışır: elle hazırlanmış (gömülü, kullanıcı genişletebilir) tablo yüksek güven (0,8-0,9) verir; tahmine dayalı eşleme düşük güven (0,4) verir.
// Düşük güvenli eşleşmeler hiçbir zaman "Yüksek" önem almaz (yanlış alarm savunması).
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

// V2 ad alanı.
namespace LabDenetim.V2;

// Sürüm karşılaştırma yardımcıları ("2.4.49" gibi noktalı sayısal sürümler).
internal static class SurumKiyasi
{
    // Baştaki noktalı sayıyı çözer ("v2.15.0-beta1" → 2,15,0; "23.01 (x64)" → 23,1). Çözülemezse null.
    public static int[]? Coz(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s.Trim().TrimStart('v', 'V'), @"^\d+(\.\d+){0,5}"); if (!m.Success) return null;
        try { return m.Value.Split('.').Select(int.Parse).ToArray(); } catch (OverflowException) { return null; }
    }

    // a ile b'yi karşılaştırır (<0, 0, >0); biri çözülemezse null.
    public static int? Kiyasla(string? a, string? b)
    {
        var x = Coz(a); var y = Coz(b); if (x == null || y == null) return null;
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++) { int p = i < x.Length ? x[i] : 0, q = i < y.Length ? y[i] : 0; if (p != q) return p.CompareTo(q); }
        return 0;
    }
}

// CPE adayı.
internal sealed record CpeAday(string Vendor, string Product, double Guven, string Yontem);

// Yazılım adı → CPE eşleyici.
internal static class CpeMapper
{
    // Tablo satırı.
    private sealed class Satir { public string Ad { get; set; } = ""; public string? Yayinci { get; set; } public string Vendor { get; set; } = ""; public string Product { get; set; } = ""; public double Guven { get; set; } = 0.8; }
    private static readonly JsonSerializerOptions Js = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    // Kullanıcı genişletme dosyası (aynı biçim; gömülü tabloya EKLENİR, öncelik kullanıcıdadır).
    public static string KullaniciDosyasi => BulutKlasor.Dosya("cpe_esleme.json");

    // Tabloyu yükler (gömülü + kullanıcı); bozuk satır atlanır.
    private static List<(Regex Ad, Regex? Yay, string Vendor, string Product, double Guven)> Tablo(string? kullaniciMetni = null)
    {
        var sonuc = new List<(Regex, Regex?, string, string, double)>();
        void Ekle(string json, bool basaEkle)
        {
            try
            {
                var yeni = new List<(Regex, Regex?, string, string, double)>();
                foreach (var s in JsonSerializer.Deserialize<List<Satir>>(json, Js) ?? new())
                {
                    if (string.IsNullOrWhiteSpace(s.Ad) || !Regex.IsMatch(s.Vendor ?? "", @"^[a-z0-9_.\-+]{1,60}$") || !Regex.IsMatch(s.Product ?? "", @"^[a-z0-9_.\-+]{1,60}$") || s.Guven is < 0 or > 1) continue;
                    try { yeni.Add((new Regex(s.Ad, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), string.IsNullOrWhiteSpace(s.Yayinci) ? null : new Regex(s.Yayinci, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)), s.Vendor!, s.Product!, s.Guven)); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "YazilimEnvanteriV2.Tablo"); }
                }
                if (basaEkle) sonuc.InsertRange(0, yeni); else sonuc.AddRange(yeni);
            }
            catch (Exception hk2) { HataKayit.Sessiz(hk2, "YazilimEnvanteriV2.Tablo"); }
        }
        try { using var st = typeof(CpeMapper).Assembly.GetManifestResourceStream("cpe_esleme.json"); if (st != null) Ekle(new StreamReader(st).ReadToEnd(), false); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "YazilimEnvanteriV2.Tablo"); }
        try { string? k = kullaniciMetni ?? (File.Exists(KullaniciDosyasi) && new FileInfo(KullaniciDosyasi).Length < 500_000 ? File.ReadAllText(KullaniciDosyasi) : null); if (k != null) Ekle(k, true); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "YazilimEnvanteriV2.Tablo"); }
        return sonuc;
    }
    private static List<(Regex Ad, Regex? Yay, string Vendor, string Product, double Guven)>? _onbellek;

    // Eşleme: önce tablo (yüksek güven), yoksa tahmin (düşük güven 0,4). Boş ad → null.
    public static CpeAday? Esle(string? ad, string? yayinci, string? kullaniciTablosu = null)
    {
        if (string.IsNullOrWhiteSpace(ad)) return null;
        var t = kullaniciTablosu != null ? Tablo(kullaniciTablosu) : (_onbellek ??= Tablo());
        foreach (var (a, y, v, p, g) in t) { try { if (a.IsMatch(ad) && (y == null || y.IsMatch(yayinci ?? ""))) return new CpeAday(v, p, g, "tablo"); } catch (RegexMatchTimeoutException hk5) { HataKayit.Sessiz(hk5, "YazilimEnvanteriV2.Esle"); } }
        // Tahmin: yayıncıdan firma, addan ürün (sürüm/mimari ekleri atılır).
        string vendor = Normalize(Regex.Replace(yayinci ?? "", @"\b(inc|ltd|llc|corp|corporation|gmbh|co|foundation|project|software|limited)\b\.?", "", RegexOptions.IgnoreCase)), product = Normalize(Regex.Replace(ad, @"\(.*?\)|\bv?\d+(\.\d+)+\b|\b(x64|x86|64-bit|32-bit)\b", "", RegexOptions.IgnoreCase));
        return vendor.Length > 0 && product.Length > 0 ? new CpeAday(vendor, product, 0.4, "tahmin") : null;
    }
    // "Foo Bar!" → "foo_bar".
    private static string Normalize(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9+.]+", "_").Trim('_');
    // Testte önbelleği sıfırlamak için.
    internal static void Sifirla() => _onbellek = null;
}

// Yazılım envanteri toplayıcı (salt-okunur: kayıt defterindeki Uninstall anahtarları).
internal static class SoftwareInventoryCollector
{
    // Okunacak kayıt defteri konumları: (kök, yol).
    private static readonly (RegistryHive Kok, RegistryView Gorunum, string Yol)[] Konumlar =
    {
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
    };

    // Gerçek makineden okur; sistem bileşenleri, güncelleme satırları ve adsız kayıtlar atlanır. Her yazılım CPE adayıyla zenginleştirilir.
    public static List<InstalledSoftware> Topla()
    {
        var l = new Dictionary<string, InstalledSoftware>(StringComparer.OrdinalIgnoreCase);
        foreach (var (kok, gorunum, yol) in Konumlar)
        {
            try
            {
                using var taban = RegistryKey.OpenBaseKey(kok, gorunum); using var u = taban.OpenSubKey(yol); if (u == null) continue;
                foreach (var ad in u.GetSubKeyNames())
                {
                    try
                    {
                        using var k = u.OpenSubKey(ad); if (k == null) continue;
                        string? isim = (k.GetValue("DisplayName") as string)?.Trim(); if (string.IsNullOrEmpty(isim) || (k.GetValue("SystemComponent") is int sc && sc == 1) || !string.IsNullOrEmpty(k.GetValue("ParentKeyName") as string) || Regex.IsMatch(isim, @"^(Update for|Security Update|Hotfix)\b", RegexOptions.IgnoreCase)) continue;
                        string? surum = (k.GetValue("DisplayVersion") as string)?.Trim(), yay = (k.GetValue("Publisher") as string)?.Trim(), konum = (k.GetValue("InstallLocation") as string)?.Trim();
                        var cpe = CpeMapper.Esle(isim, yay); string anahtar = (isim + "|" + surum).ToLowerInvariant();
                        l[anahtar] = new InstalledSoftware(Temiz(isim, 200) ?? isim, Temiz(surum, 60), Temiz(yay, 100), Temiz(konum, 260), $"{kok}\\{ad}", cpe == null ? null : $"{cpe.Vendor}:{cpe.Product}", cpe?.Guven ?? 0);
                    }
                    catch (Exception hk6) { HataKayit.Sessiz(hk6, "YazilimEnvanteriV2.Topla"); }
                }
            }
            catch (Exception hk7) { HataKayit.Sessiz(hk7, "YazilimEnvanteriV2.Topla"); }
        }
        return l.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
    private static string? Temiz(string? s, int en) { if (s == null) return null; s = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim(); return s.Length == 0 ? null : s.Length > en ? s[..en] : s; }
}
