// Bu dosya ürünün MARKA bilgisinin (ürün adı, şirket adı, kısa başlık, destek e-postası, veri klasörü adı, vurgu rengi) TEK kaynağıdır.
// Varsayılanlar mevcut BulutSoft değerleridir; programın yanında "brand.json" varsa geçerli alanlar bunların üzerine yazılır (white-label).
// Geçersiz alan sessizce varsayılana döner ve HataKayit'a yazılır; bozuk dosya programı asla durdurmaz.
// Core'da System.Drawing olmadığı için vurgu rengi "#RRGGBB" metni olarak tutulur; arayüz bunu Color'a çevirir.
// NOT: yalnızca GÖRÜNEN metinler ve veri klasörü adı markalanır; Windows hizmet adı, pipe adı, olay günlüğü kaynağı ve kayıt defteri anahtarları sabit kalır (kurulum bunlara bağlıdır).
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Marka yapılandırması.
public sealed class BrandConfig
{
    // Varsayılan değerler (mevcut ürün).
    public const string VarsayilanUrunAdi = "BulutSoft Security Suite";
    public const string VarsayilanSirketAdi = "BulutSoft Güvenlik Teknolojileri";
    public const string VarsayilanKisaBaslik = "BulutSoft";
    public const string VarsayilanDestekEposta = "bulutsoftsecurity@info.com";
    public const string VarsayilanVeriKlasoru = "BulutSoft";
    public const string VarsayilanVurgu = "#6366F1";

    // Ürünün tam adı (sürümsüz).
    public string UrunAdi { get; init; } = VarsayilanUrunAdi;
    // Şirket adı.
    public string SirketAdi { get; init; } = VarsayilanSirketAdi;
    // Dar yerlerde kullanılan kısa başlık (sol menü logosu, açılış).
    public string KisaBaslik { get; init; } = VarsayilanKisaBaslik;
    // Destek e-posta adresi.
    public string DestekEposta { get; init; } = VarsayilanDestekEposta;
    // %AppData% / %ProgramData% altındaki veri klasörünün adı (yalnızca ad; yol ayırıcı içeremez).
    public string VeriKlasoruAdi { get; init; } = VarsayilanVeriKlasoru;
    // Vurgu rengi, "#RRGGBB".
    public string VurguRengi { get; init; } = VarsayilanVurgu;
    // Dosya okunurken reddedilen alanların açıklamaları (boşsa her şey geçerliydi).
    public IReadOnlyList<string> Uyarilar { get; init; } = Array.Empty<string>();

    // brand.json'da kabul edilen alanlar (adlar büyük/küçük harfe duyarsız).
    private sealed class Dosya
    {
        public string? UrunAdi { get; set; }
        public string? SirketAdi { get; set; }
        public string? KisaBaslik { get; set; }
        public string? DestekEposta { get; set; }
        public string? VeriKlasoruAdi { get; set; }
        public string? VurguRengi { get; set; }
    }

    private static readonly JsonSerializerOptions Secenek = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly Regex VeriKlasoruDeseni = new(@"^[A-Za-z0-9][A-Za-z0-9 ._-]{0,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EpostaDeseni = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RenkDeseni = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] YasakAdlar = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
    private const int EnBuyukDosya = 16 * 1024;

    private static readonly object Kilit = new();
    private static BrandConfig? _aktif;

    // brand.json'un TEK ortak konumu: %ProgramData%\BulutSoft\brand.json. Arayüz ve koruma hizmeti (BulutSoftGuardService, SYSTEM) aynı dosyayı okur; böylece veri klasörü adı
    // iki süreç arasında asla ayrışmaz. Klasör adı markadan DEĞİL sabit varsayılandan gelir (marka bu dosyadan okunduğu için). Dosyaya yönetici olmayan biri yazabilse bile
    // veri klasörü adı sıkı doğrulanır (yol ayırıcı, "..", sürücü/ADS iki nokta, ayrılmış adlar reddedilir → güvenli varsayılan) ve bağlantı (reparse point) dosyalar okunmaz.
    public static string DosyaYolu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), VarsayilanVeriKlasoru, "brand.json");

    // Geçerli marka (ilk kullanımda brand.json okunur; yoksa varsayılan).
    public static BrandConfig Aktif
    {
        get { lock (Kilit) { return _aktif ??= DosyadanYukle(DosyaYolu); } }
        internal set { lock (Kilit) { _aktif = value; } }
    }

    // Varsayılan marka.
    public static BrandConfig Varsayilan => new();

    // Veri klasörleri: tüm programın tek yerden okuduğu kökler (varsayılan: ...\BulutSoft).
    public static string RoamingKlasor => Aktif.RoamingYolu;
    public static string OrtakKlasor => Aktif.OrtakYolu;
    public static string YerelKlasor => Aktif.YerelYolu;
    // Bu marka örneğinin veri klasörleri (testlenebilir; statikler Aktif'e bakar).
    public string RoamingYolu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), VeriKlasoruAdi);
    public string OrtakYolu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), VeriKlasoruAdi);
    public string YerelYolu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), VeriKlasoruAdi);

    // Dosyayı okur; yoksa/bozuksa/çok büyükse varsayılan döner.
    public static BrandConfig DosyadanYukle(string yol)
    {
        try
        {
            var fi = new FileInfo(yol);
            if (!fi.Exists) return Varsayilan;
            if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint)) { HataKayit.Sessiz(new InvalidDataException("brand.json bir bağlantı (symlink/junction) olamaz."), "BrandConfig.DosyadanYukle"); return Varsayilan; }
            if (fi.Length > EnBuyukDosya) { HataKayit.Sessiz(new InvalidDataException("brand.json çok büyük (en çok 16 KB)."), "BrandConfig.DosyadanYukle"); return Varsayilan; }
            return Oku(File.ReadAllText(yol));
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "BrandConfig.DosyadanYukle"); return Varsayilan; }
    }

    // JSON metnini doğrulayıp varsayılanın üzerine yazar (saf mantık; testlenir).
    public static BrandConfig Oku(string json)
    {
        Dosya? d;
        try { d = JsonSerializer.Deserialize<Dosya>(json, Secenek); }
        catch (JsonException hk2) { HataKayit.Sessiz(hk2, "BrandConfig.Oku"); return new BrandConfig { Uyarilar = new[] { "brand.json okunamadı (geçersiz JSON); varsayılan marka kullanıldı." } }; }
        if (d == null) return Varsayilan;
        var uyari = new List<string>();
        string Metin(string ad, string? deger, string varsayilan, int en)
        {
            if (deger == null) return varsayilan;
            string t = deger.Trim();
            if (t.Length == 0 || t.Length > en || t.Any(char.IsControl)) { uyari.Add($"{ad}: boş, çok uzun ({en} karakter) ya da denetim karakteri içeriyor; varsayılan kullanıldı."); return varsayilan; }
            return t;
        }
        string eposta = Metin("DestekEposta", d.DestekEposta, VarsayilanDestekEposta, 120);
        if (d.DestekEposta != null && !EpostaDeseni.IsMatch(eposta)) { uyari.Add("DestekEposta: geçerli bir e-posta adresi değil; varsayılan kullanıldı."); eposta = VarsayilanDestekEposta; }
        string klasor = Metin("VeriKlasoruAdi", d.VeriKlasoruAdi, VarsayilanVeriKlasoru, 40);
        if (d.VeriKlasoruAdi != null && klasor != VarsayilanVeriKlasoru && (!VeriKlasoruDeseni.IsMatch(klasor) || klasor.EndsWith('.') || klasor.EndsWith(' ') || YasakAdlar.Contains(klasor.Split('.')[0].TrimEnd(), StringComparer.OrdinalIgnoreCase)))   // "NUL.txt" da Windows'ta ayrılmış aygıt adıdır
        { uyari.Add("VeriKlasoruAdi: yalnızca harf, rakam, boşluk, '.', '_' ve '-' içerebilir (yol ayırıcı olamaz); varsayılan kullanıldı."); klasor = VarsayilanVeriKlasoru; }
        string renk = VarsayilanVurgu;
        if (d.VurguRengi != null) { string r = d.VurguRengi.Trim(); if (RenkDeseni.IsMatch(r)) renk = r.ToUpperInvariant(); else uyari.Add("VurguRengi: '#RRGGBB' biçiminde olmalı; varsayılan kullanıldı."); }
        foreach (var u in uyari) HataKayit.Sessiz(new InvalidDataException(u), "BrandConfig.Oku");
        return new BrandConfig
        {
            UrunAdi = Metin("UrunAdi", d.UrunAdi, VarsayilanUrunAdi, 60), SirketAdi = Metin("SirketAdi", d.SirketAdi, VarsayilanSirketAdi, 80), KisaBaslik = Metin("KisaBaslik", d.KisaBaslik, VarsayilanKisaBaslik, 30),
            DestekEposta = eposta, VeriKlasoruAdi = klasor, VurguRengi = renk, Uyarilar = uyari
        };
    }
}
