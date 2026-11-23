// DesignTokens / FontProvider / küre token köprüsü testleri: her rol iki temada tanımlı, okunabilirlik (kontrast) eşikleri, ölçek değerleri, marka vurgusu ve kure.html'in token iletisini dinlemesi.
using System.Text.Json;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// BrandConfig.Aktif'i değiştiren testler aynı koleksiyonda sıralı çalışır.
[Collection("MarkaDurumu")]
public class DesignTokensTestleri
{
    private static double Parlaklik(Color c)
    {
        double K(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * K(c.R) + 0.7152 * K(c.G) + 0.0722 * K(c.B);
    }
    private static double Kontrast(Color a, Color b) { double x = Parlaklik(a), y = Parlaklik(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

    [Fact]
    public void HerRol_IkiTemadaGecerliHexVe_YuzeylerTemayaGoreFarklidir()
    {
        foreach (TokenRol r in Enum.GetValues<TokenRol>())
            foreach (bool koyu in new[] { true, false }) Assert.Matches("^#[0-9A-F]{6}$", DesignTokens.Hex(r, koyu));
        Assert.NotEqual(DesignTokens.Renk(TokenRol.SurfaceApp, true), DesignTokens.Renk(TokenRol.SurfaceApp, false));
        Assert.NotEqual(DesignTokens.Renk(TokenRol.TextPrimary, true), DesignTokens.Renk(TokenRol.TextPrimary, false));
        Assert.Equal(21, Enum.GetValues<TokenRol>().Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MetinRolleri_YuzeylerdeOkunabilir(bool koyu)
    {
        foreach (var yuzey in new[] { TokenRol.SurfaceApp, TokenRol.SurfaceCard, TokenRol.SurfaceSidebar })
            foreach (var metin in new[] { TokenRol.TextPrimary, TokenRol.TextSecondary, TokenRol.TextMuted })
                Assert.True(Kontrast(DesignTokens.Renk(metin, koyu), DesignTokens.Renk(yuzey, koyu)) >= 4.5, $"{metin} / {yuzey} koyu={koyu}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DurumRenkleri_ZeminlerindeVeKendiArkaPlanlarindaSecilebilir(bool koyu)
    {
        var cift = new[] { (TokenRol.StatusSuccess, TokenRol.StatusSuccessBg), (TokenRol.StatusWarning, TokenRol.StatusWarningBg), (TokenRol.StatusDanger, TokenRol.StatusDangerBg), (TokenRol.StatusInfo, TokenRol.StatusInfoBg) };
        foreach (var (on, arka) in cift)
        {
            Assert.True(Kontrast(DesignTokens.Renk(on, koyu), DesignTokens.Renk(TokenRol.SurfaceApp, koyu)) >= 3.0, $"{on} / SurfaceApp koyu={koyu}");
            Assert.True(Kontrast(DesignTokens.Renk(on, koyu), DesignTokens.Renk(arka, koyu)) >= 3.0, $"{on} / {arka} koyu={koyu}");
        }
        Assert.True(Kontrast(DesignTokens.Renk(TokenRol.TextOnAccent, koyu), DesignTokens.Renk(TokenRol.AccentPrimary, koyu)) >= 3.0);
    }

    [Fact]
    public void BosluklarVeYaricaplar_IstenenOlcekte()
    {
        Assert.Equal(new[] { 4, 8, 12, 16, 24 }, new[] { DesignTokens.SpaceXs, DesignTokens.SpaceSm, DesignTokens.SpaceMd, DesignTokens.SpaceLg, DesignTokens.SpaceXl });
        Assert.All(new[] { DesignTokens.SpaceXs, DesignTokens.SpaceSm, DesignTokens.SpaceMd, DesignTokens.SpaceLg, DesignTokens.SpaceXl }, v => Assert.Equal(0, v % 4));
        Assert.Equal(new[] { 6, 10, 14 }, new[] { DesignTokens.RadiusSm, DesignTokens.RadiusMd, DesignTokens.RadiusLg });
        Assert.Equal(12, DesignTokens.Px(8, 144)); Assert.Equal(8, DesignTokens.Px(8, 96));
    }

    [Fact]
    public void MarkaVurgusu_AccentPrimaryVeHoverRenginiDegistirir_DurumRenkleriDegismez()
    {
        var eski = BrandConfig.Aktif;
        try
        {
            var varsayilanDanger = DesignTokens.Hex(TokenRol.StatusDanger, true);
            BrandConfig.Aktif = new BrandConfig { VurguRengi = "#FF8800" };
            Assert.Equal("#FF8800", DesignTokens.Hex(TokenRol.AccentPrimary, true)); Assert.Equal("#FF8800", DesignTokens.Hex(TokenRol.AccentPrimary, false));
            Assert.NotEqual("#FF8800", DesignTokens.Hex(TokenRol.AccentHover, true)); Assert.Matches("^#[0-9A-F]{6}$", DesignTokens.Hex(TokenRol.AccentHover, false));
            Assert.Equal(varsayilanDanger, DesignTokens.Hex(TokenRol.StatusDanger, true));
        }
        finally { BrandConfig.Aktif = eski; }
        Assert.Equal("#6366F1", DesignTokens.Hex(TokenRol.AccentPrimary, true));
    }

    [Fact]
    public void FontProvider_AltiRol_OnbellekliVePaylasimli_BoyutSirasiDogru()
    {
        var roller = Enum.GetValues<FontRol>(); Assert.Equal(6, roller.Length);
        foreach (var r in roller) Assert.Same(FontProvider.Al(r), FontProvider.Al(r));
        float P(FontRol r) => FontProvider.Olcu(r).Punto;
        Assert.True(P(FontRol.Display) > P(FontRol.Heading) && P(FontRol.Heading) > P(FontRol.Subheading) && P(FontRol.Subheading) > P(FontRol.Body) && P(FontRol.Body) > P(FontRol.Caption));
        Assert.Contains(FontProvider.Al(FontRol.Mono).FontFamily.Name, new[] { "Consolas", "Cascadia Mono" });
        Assert.Equal("Segoe UI", FontProvider.Al(FontRol.Body).FontFamily.Name);
    }

    [Fact]
    public void KureJson_TumRolleriIcerir_VeGecerliJsondur()
    {
        foreach (bool koyu in new[] { true, false })
        {
            using var d = JsonDocument.Parse(DesignTokens.KureJson(koyu));
            Assert.Equal("tokenlar", d.RootElement.GetProperty("tip").GetString()); Assert.Equal(koyu, d.RootElement.GetProperty("koyu").GetBoolean());
            var renkler = d.RootElement.GetProperty("renkler"); Assert.Equal(21, renkler.EnumerateObject().Count());
            Assert.Equal(DesignTokens.Hex(TokenRol.SurfaceApp, koyu), renkler.GetProperty("surfaceApp").GetString());
            Assert.Equal(DesignTokens.Hex(TokenRol.StatusDangerBg, koyu), renkler.GetProperty("statusDangerBg").GetString());
        }
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void KureHtml_TokenIletisiniDinler_Ve_PanelTemaOlayinaBaglidir()
    {
        string? d = AppContext.BaseDirectory, kok = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) { kok = d; break; }
        if (kok == null) return;   // kaynak ağacı yoksa atla
        string html = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kure", "kure.html"));
        Assert.Contains("addEventListener('message'", html, StringComparison.Ordinal); Assert.Contains("tokenlariUygula", html, StringComparison.Ordinal); Assert.Contains("setProperty('--'", html, StringComparison.Ordinal);
        string panel = File.ReadAllText(Path.Combine(kok, "LabDenetim", "KureWebPaneli.cs"));
        Assert.Contains("PostWebMessageAsJson(DesignTokens.KureJson", panel, StringComparison.Ordinal); Assert.Contains("TemaYonetici.TemaDegisti += TemaDegistiOlayi", panel, StringComparison.Ordinal); Assert.Contains("TemaYonetici.TemaDegisti -= TemaDegistiOlayi", panel, StringComparison.Ordinal);
    }
}
