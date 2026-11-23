// D7 kararı (grafik/ikon kütüphanesi): skor halkası, trend çizgisi ve ikon seti GDI+ ile kendi kodumuzda kalır; ScottPlot/LiveCharts2/SkiaSharp/SVG ikon kütüphanesi EKLENMEZ.
// Gerekçe ve ölçümler: KARAR_D7_GORSEL_KUTUPHANE.md. Bu test kararı korur: arayüz projesine yeni bir grafik/ikon paketi bilinçli (belge + bu liste güncellenerek) eklenir, sessizce değil.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class GorselKutuphaneKarariTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void ArayuzProjesi_PaketleriOnayliListedeVeGrafikIkonKutuphanesiIceremez()
    {
        string? kok = Kok(); if (kok == null) return;
        var paketler = Regex.Matches(File.ReadAllText(Path.Combine(kok, "LabDenetim", "LabDenetim.csproj")), @"<PackageReference\s+Include=""([^""]+)""").Select(m => m.Groups[1].Value).ToList();
        var onayli = new[] { "Microsoft.Web.WebView2", "QuestPDF", "System.Management" };
        Assert.True(paketler.All(onayli.Contains), "Onaysız paket: " + string.Join(", ", paketler.Except(onayli)) + " — grafik/ikon kütüphanesi eklemeden önce KARAR_D7_GORSEL_KUTUPHANE.md'yi güncelleyin.");
        foreach (var yasak in new[] { "ScottPlot", "LiveCharts", "OxyPlot", "SkiaSharp", "Svg", "FontAwesome", "Lucide" }) Assert.DoesNotContain(paketler, p => p.Contains(yasak, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IkonSeti_HerAdinGercektenCizilir_VeKucukBoyutlardaOkunur()
    {
        Exception? hata = null;
        var t = new Thread(() =>
        {
            try
            {
                foreach (var ad in Enum.GetValues<IkonAdi>()) foreach (int boyut in new[] { 16, 24, 32, 48 })
                {
                    using var bmp = new Bitmap(boyut, boyut); using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.Black); Ikonlar.Ciz(g, ad, new RectangleF(0, 0, boyut, boyut), Color.White); }
                    int boyali = 0; for (int y = 0; y < boyut; y++) for (int x = 0; x < boyut; x++) if (bmp.GetPixel(x, y).R > 40) boyali++;
                    Assert.True(boyali >= boyut / 2, $"{ad} {boyut}px: yeterince çizilmedi ({boyali} piksel)");
                    Assert.True(boyali < boyut * boyut * 0.6, $"{ad} {boyut}px: dolu blok gibi çizildi ({boyali} piksel)");
                }
            }
            catch (Exception ex) { hata = ex; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(60))); if (hata != null) throw new Exception("İkon sınaması: " + hata.Message, hata);
    }
}
