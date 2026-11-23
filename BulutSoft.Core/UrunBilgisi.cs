// Bu dosya ürünün sürüm bilgisinin TEK okuma noktasıdır. Sürüm, kökteki Directory.Build.props dosyasındaki <Version> değerinden gelir
// ve derlemeye AssemblyInformationalVersion olarak yazılır; arayüze, raporlara ve komut satırına sabit sürüm metni yazılmaz (CLAUDE.md).
using System.Reflection;

// Programımızın adı.
namespace LabDenetim;

// Ürün adı ve sürümü.
public static class UrunBilgisi
{
    // Ürünün adı (sürümsüz); BrandConfig'ten (brand.json) gelir.
    public static string Ad => BrandConfig.Aktif.UrunAdi;

    // Sürüm numarası, örneğin "1.5.3" (derleme meta verisindeki "+..." eki atılır).
    public static string Surum { get; } = Oku();

    // Arayüz etiketi, örneğin "v1.5.3".
    public static string SurumEtiketi => "v" + Surum;

    // Ad ve sürüm birlikte, örneğin "BulutSoft Security Suite v1.5.3".
    public static string AdVeSurum => Ad + " " + SurumEtiketi;

    // Sürümü derlemeden okur; okunamazsa derleme sürümüne, o da yoksa "0.0.0"a düşer.
    private static string Oku()
    {
        var a = typeof(UrunBilgisi).Assembly;
        string? bilgi = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(bilgi))
        {
            int ek = bilgi.IndexOf('+', StringComparison.Ordinal);
            string s = (ek >= 0 ? bilgi[..ek] : bilgi).Trim();
            if (s.Length > 0) return s;
        }
        var v = a.GetName().Version;
        return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
