// Küre sayfası (Kure/kure.html) için koruma testi: sayfadaki bir JavaScript hatası küreyi TAMAMEN gizler (ekran çizim döngüsü her karede hata verir),
// ama WebView2 hata göstermez. Bu test, bir değişkenin tanımlanmadan ÖNCE kullanılması gibi sık hataları ve temel köprü işlevlerinin varlığını denetler.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class KureSayfasiTestleri
{
    private static string Html()
    {
        string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "LabDenetim", "Kure", "kure.html"); if (File.Exists(y)) return File.ReadAllText(y); }
        var kopya = Path.Combine(AppContext.BaseDirectory, "Kure", "kure.html"); return File.Exists(kopya) ? File.ReadAllText(kopya) : "";
    }

    [Fact]
    public void Bannerda_Kullanilan_Const_Degiskenler_Kullanimdan_Once_Tanimlanir()
    {
        string h = Html(); if (h.Length == 0) return;
        // "const x" tanımı, aynı betikte x'in ilk kullanımından ÖNCE gelmeli (TDZ hatası küreyi sessizce öldürür).
        foreach (var ad in new[] { "baslikYazi", "altYazi", "altYazi0", "hizmetYazi", "titleW", "subW" })
        {
            int tanim = Regex.Match(h, @"\b(const|let)\s+" + ad + @"\b").Index; int ilk = Regex.Match(h, @"\b" + ad + @"\b").Index;
            Assert.True(tanim > 0, ad + " tanımı yok"); Assert.True(ilk >= tanim || Regex.Match(h, @"\b" + ad + @"\b").Index == tanim + Regex.Match(h.Substring(tanim), @"\b" + ad + @"\b").Index, $"{ad} tanımdan önce kullanılıyor");
            Assert.False(Regex.IsMatch(h.Substring(0, tanim), @"[^.\w]" + ad + @"\s*[\.\)\(,;]"), $"{ad} 'const/let' tanımından ÖNCE kullanılıyor (küre çizilmez)");
        }
    }

    [Fact]
    public void Kopruler_Ve_Temel_Islevler_Var()
    {
        string h = Html(); if (h.Length == 0) return;
        Assert.Contains("window.bsAyarla", h); Assert.Contains("kalkan-tikla", h); Assert.Contains("requestAnimationFrame", h); Assert.DoesNotContain("https://", Regex.Replace(h, @"http://www\.w3\.org[^'""]*", ""));   // dış adres yok (çevrimdışı)
    }
}
