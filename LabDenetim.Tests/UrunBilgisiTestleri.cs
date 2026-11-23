// Sürüm tek kaynak testleri: arayüzdeki/raporlardaki sürüm etiketi Directory.Build.props içindeki <Version> değerinden gelir; kaynak kodda sabit sürüm metni kalmaz.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class UrunBilgisiTestleri
{
    private static string? Kok()
    {
        string? d = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d;
        return null;
    }

    [Fact]
    public void Surum_BicimiDogru_VeEtiketleriTutarli()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", UrunBilgisi.Surum);
        Assert.Equal("v" + UrunBilgisi.Surum, UrunBilgisi.SurumEtiketi);
        Assert.Equal("BulutSoft Security Suite " + UrunBilgisi.SurumEtiketi, UrunBilgisi.AdVeSurum);
        Assert.Equal(UrunBilgisi.Surum, TaramaCalistirici.Surum());
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Surum_DirectoryBuildPropsIleAyni_TekKaynak()
    {
        string? kok = Kok(); if (kok == null) return;   // kaynak ağacı yoksa atla
        var m = Regex.Match(File.ReadAllText(Path.Combine(kok, "Directory.Build.props")), @"<Version>\s*(\d+\.\d+\.\d+)\s*</Version>");
        Assert.True(m.Success); Assert.Equal(m.Groups[1].Value, UrunBilgisi.Surum);
        // Proje dosyalarında ayrı <Version> olmamalı (tek kaynak).
        foreach (var p in new[] { @"LabDenetim\LabDenetim.csproj", @"BulutSoft.Core\BulutSoft.Core.csproj", @"BulutSoftGuardService\BulutSoftGuardService.csproj" })
            Assert.DoesNotContain("<Version>", File.ReadAllText(Path.Combine(kok, p)), StringComparison.Ordinal);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kaynakta_SabitSurumEtiketiKalmamistir()
    {
        string? kok = Kok(); if (kok == null) return;
        var sabit = new Regex(@"""[^""\n]*\b(Suite|Security Suite)\s+v\d+\.\d+|""v\d+\.\d+(\.\d+)?""|<Version>");
        var kotu = new List<string>();
        foreach (var d in new[] { "LabDenetim", "BulutSoft.Core" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, d), "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                if (Path.GetFileName(f) == "UrunBilgisi.cs") continue;   // tek okuma noktası: açıklama satırlarında örnek sürüm geçer
                if (sabit.IsMatch(File.ReadAllText(f))) kotu.Add(Path.GetFileName(f));
            }
        Assert.True(kotu.Count == 0, "Sabit sürüm metni (UrunBilgisi kullanın): " + string.Join(", ", kotu));
    }
}
