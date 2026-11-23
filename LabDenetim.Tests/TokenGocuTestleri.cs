// Token göçü testleri (Faz 3 Adım 6a): eski formlardaki sabit renk/yazı tipi kalmadı, kurumsal dil tutarlı, token renkleri Palet tablosunda kayıtlı.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class TokenGocuTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    // Yorum olmayan kod satırları (obj/bin hariç).
    private static IEnumerable<(string Dosya, int No, string Satir)> KodSatirlari(string proje)
    {
        string? kok = Kok(); if (kok == null) yield break;
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
        {
            if (Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]")) continue;
            var satirlar = File.ReadAllLines(f);
            for (int i = 0; i < satirlar.Length; i++) { string s = satirlar[i].TrimStart(); if (s.StartsWith("//")) continue; yield return (Path.GetFileName(f), i + 1, satirlar[i]); }
        }
    }

    [Fact]
    public void DogrudanColorFromArgb_YalnizcaTemaMotorundaVar()
    {
        var izinli = new HashSet<string> { "DesignTokens.cs", "Palet.cs", "TemaYonetici.cs" };   // renk üreten/çeviren çekirdek
        var ihlal = KodSatirlari("LabDenetim").Where(x => !izinli.Contains(x.Dosya) && x.Satir.Contains("Color.FromArgb(")).Select(x => $"{x.Dosya}:{x.No}").ToList();
        Assert.True(ihlal.Count == 0, "Doğrudan Color.FromArgb: " + string.Join(", ", ihlal.Take(10)));
    }

    [Fact]
    public void DogrudanNewFont_YalnizcaFontProviderdaVar()
    {
        var ihlal = KodSatirlari("LabDenetim").Where(x => x.Dosya != "FontProvider.cs" && Regex.IsMatch(x.Satir, @"\bnew Font\(")).Select(x => $"{x.Dosya}:{x.No}").ToList();
        Assert.True(ihlal.Count == 0, "Doğrudan new Font: " + string.Join(", ", ihlal.Take(10)));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void PaylasimliYaziTipi_AsloDisposeEdilmez_UsingYalnizcaGeciciIcin()
    {
        // FontProvider.Al paylaşımlıdır: aynı satırda `using` ile birlikte kullanılamaz (atılırsa tüm uygulamada yazı tipi bozulur ve çizim kilitlenir); geçici ihtiyaç için FontProvider.Gecici vardır.
        var ihlal = KodSatirlari("LabDenetim").Where(x => x.Dosya != "FontProvider.cs" && x.Satir.Contains("FontProvider.Al(") && Regex.IsMatch(x.Satir, @"\busing\s+(var\s+\w+|\(\w+)\s*=?\s*FontProvider\.Al\(")).Select(x => $"{x.Dosya}:{x.No}").ToList();
        Assert.True(ihlal.Count == 0, "Paylaşımlı yazı tipi using ile: " + string.Join(", ", ihlal));
        // Ayrıca `using (f1)` / `.Dispose()` ile atılan değişken Al() sonucunu da tutabiliyorsa dosyada Al ile birlikte görülemez: KolayModPaneli'ndeki eski hata kalıbı.
        string? kok = Kok(); if (kok == null) return;
        Assert.DoesNotContain("using (f1) TextRenderer", File.ReadAllText(Path.Combine(kok, "LabDenetim", "KolayModPaneli.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void KurumsalDil_KullaniciMetinlerindeLaboratuvarVeSanalLabYok()
    {
        var ihlal = KodSatirlari("LabDenetim").Concat(KodSatirlari("BulutSoft.Core")).Where(x => Regex.IsMatch(x.Satir, @"laboratuvar|Sanal Lab", RegexOptions.IgnoreCase) && x.Satir.Contains('"')).Select(x => $"{x.Dosya}:{x.No}").ToList();
        Assert.True(ihlal.Count == 0, "Eski 'laboratuvar' dili: " + string.Join(", ", ihlal.Take(10)));
    }

    [Fact]
    public void TokenOzgunRenkleri_PaletteKayitli_AcikTemadaDogruRoleCevrilir()
    {
        foreach (var rol in Enum.GetValues<TokenRol>())
        {
            var koyu = DesignTokens.Ozgun(rol);
            if (rol is TokenRol.TextOnAccent or TokenRol.AccentHover) continue;   // beyaz özel durum; AccentHover (129,140,248) eski tablodaki birincil metin rolünde
            Assert.NotEqual(PaletRol.Yok, Palet.Rol(koyu, true));
            var acik = Palet.Esle(koyu, true, koyu: false);
            if (rol.ToString().StartsWith("Surface") || rol.ToString().StartsWith("Text") || rol.ToString().StartsWith("Border")) Assert.NotEqual(koyu, acik);   // zemin/yazı/kenar açık temada çevrilir (vurgu/durum katı renkleri iki temada aynı kalabilir)
        }
        Assert.Equal(DesignTokens.Ozgun(TokenRol.StatusInfo), DesignTokens.Renk(TokenRol.StatusInfo, true));
    }

    [Fact]
    public void Saydam_AlfayiKorur_OzgunRenkSaydamdaPaletRolunuBulur()
    {
        var c = DesignTokens.Saydam(DesignTokens.Ozgun(TokenRol.StatusSuccess), 90);
        Assert.Equal(90, c.A); Assert.NotEqual(PaletRol.Yok, Palet.Rol(c, false)); Assert.Equal(90, Palet.Esle(c, false, koyu: false).A);
        Assert.Equal(0, DesignTokens.Saydam(Color.White, -5).A); Assert.Equal(255, DesignTokens.Saydam(Color.White, 999).A);
    }
}
