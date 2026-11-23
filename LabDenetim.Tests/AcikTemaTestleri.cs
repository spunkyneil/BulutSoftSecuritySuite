// Açık tema renk dönüşümü (D3: tek yol = Palet tablosu): koyu "özgün" renkler Palet rollerine, rollerin açık temadaki değerlerine gider.
// Göz yormayan yumuşak zemin, saf siyah/beyaz yok, marka sarısı mat hardal değil derin kehribar. Tabloda olmayan renk DEĞİŞMEDEN kalır ve Palet.Bilinmeyen'e yazılır.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class AcikTemaTestleri
{
    // Açık temada arka plan olarak çevrilmiş renk.
    private static Color Acik(Color koyu, bool arka = true) => Palet.Esle(koyu, arka, false);

    [Theory]
    [InlineData(8, 13, 24)] [InlineData(15, 23, 42)] [InlineData(10, 17, 32)] [InlineData(22, 27, 34)] [InlineData(30, 41, 59)]
    public void KoyuZeminler_YumusakGriMaviyeGider_BeyazaYaklasmaz(int r, int g, int b)
    {
        var c = Acik(Color.FromArgb(r, g, b)); Assert.NotEqual(Color.Black.ToArgb(), c.ToArgb());
        // Yumuşak açık zemin: Kart rolü (beyaz) dışında saf beyaz olmamalı ve çok parlak değil (göz yormaz).
        Assert.True(c.GetBrightness() is >= 0.88f and <= 1.0f, $"{c} {c.GetBrightness()}");
    }

    [Fact] public void Katmanlar_ArasiFark_Korunur()
    {
        // Sayfa zemini ile kart zemini açık temada da ayırt edilebilir olmalı.
        var sayfa = Acik(Color.FromArgb(20, 27, 46)); var kart = Acik(Color.FromArgb(28, 36, 56));
        Assert.NotEqual(sayfa.ToArgb(), kart.ToArgb()); Assert.True(kart.GetBrightness() - sayfa.GetBrightness() >= 0.02, $"{sayfa} {kart}");
    }

    [Fact] public void AcikYazi_SafSiyahOlmaz_ama_OkunurKalir()
    {
        var y = Acik(Color.FromArgb(224, 242, 254), false); float l = y.GetBrightness();
        Assert.InRange(l, 0.05, 0.30); Assert.NotEqual(Color.Black.ToArgb(), y.ToArgb());
        // Yazı ile zemin arasında güçlü kontrast.
        Assert.True(Acik(Color.FromArgb(8, 13, 24)).GetBrightness() - l > 0.5);
    }

    [Fact] public void MarkaSarisi_DerinKehribarOlur()
    {
        var c = Acik(Color.FromArgb(250, 204, 21), false);   // parlak sarı yazı
        Assert.InRange(c.GetHue(), 25, 40); Assert.True(c.GetBrightness() < 0.42f); Assert.True(c.GetSaturation() > 0.7f);
    }

    [Fact] public void SistemRenkleri_PaletRollerineDoner()
    {
        Assert.Equal(Palet.Renk(PaletRol.Girdi, false).ToArgb(), Acik(SystemColors.Window).ToArgb());
        Assert.Equal(Palet.Renk(PaletRol.Sayfa, false).ToArgb(), Acik(SystemColors.Control).ToArgb());
        Assert.Equal(Palet.Renk(PaletRol.Metin, false).ToArgb(), Acik(SystemColors.WindowText, false).ToArgb());
    }

    [Fact] public void Saydam_DegismezVeVurguRenkleriKoyulasir()
    {
        Assert.Equal(Color.Transparent, Acik(Color.Transparent));
        var mavi = Acik(Color.FromArgb(56, 189, 248)); Assert.True(mavi.GetBrightness() <= 0.43f);
    }

    [Fact] public void TablodaOlmayanRenk_AcikTemadaDegismezVeKaydedilir()
    {
        Palet.Bilinmeyen.Clear(); var garip = Color.FromArgb(123, 45, 67);
        Assert.Equal(garip.ToArgb(), Acik(garip).ToArgb()); Assert.True(Palet.Bilinmeyen.Count >= 1);
        Palet.Bilinmeyen.Clear(); Assert.Equal(garip.ToArgb(), Palet.Esle(garip, true, true).ToArgb()); Assert.Empty(Palet.Bilinmeyen);   // koyu temada özgün renk zaten doğru: kayıt yok
    }
}
