// Mimari kural koruması (CLAUDE.md, kural 1): BulutSoft.Core ve koruma hizmeti arayüz bileşenlerine BAĞLANAMAZ.
// Bu testler derlenmiş başvuru listesine bakar; yanlışlıkla bir arayüz paketi eklenirse derleme yeşil olsa bile burada kırılır.
using System.Reflection;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class CekirdekIzolasyonTestleri
{
    // Çekirdek ve hizmetin başvuramayacağı derleme adı önekleri (arayüz, çizim, web görünümü).
    private static readonly string[] Yasak = { "System.Windows.Forms", "System.Drawing", "Microsoft.Web.WebView2", "WindowsBase", "PresentationFramework", "PresentationCore", "BulutSoftSecuritySuite", "QuestPDF" };

    private static IEnumerable<string> Basvurular(Assembly a) => a.GetReferencedAssemblies().Select(x => x.Name ?? "");

    [Fact]
    public void Cekirdek_ArayuzDerlemelerineBasvurmaz()
    {
        var cekirdek = typeof(BulutKlasor).Assembly;
        Assert.Equal("BulutSoft.Core", cekirdek.GetName().Name);
        var kotu = Basvurular(cekirdek).Where(n => Yasak.Any(y => n.StartsWith(y, StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.True(kotu.Count == 0, "BulutSoft.Core arayüz derlemesine başvuruyor: " + string.Join(", ", kotu));
    }

    [Fact]
    public void KorumaHizmeti_ArayuzProjesineBagliDegil_YalnizcaCekirdegeBaglidir()
    {
        var hizmet = Assembly.Load("BulutSoftGuardService");
        var basvurular = Basvurular(hizmet).ToList();
        Assert.Contains("BulutSoft.Core", basvurular);
        var kotu = basvurular.Where(n => Yasak.Any(y => n.StartsWith(y, StringComparison.OrdinalIgnoreCase))).ToList();
        Assert.True(kotu.Count == 0, "Koruma hizmeti arayüz derlemesine başvuruyor: " + string.Join(", ", kotu));
    }

    [Fact]
    public void HizmetProjeDosyasi_ArayuzProjesineProjeReferansiVermez()
    {
        string? d = AppContext.BaseDirectory; string? yol = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "BulutSoftGuardService", "BulutSoftGuardService.csproj"); if (File.Exists(y)) { yol = y; break; } }
        if (yol == null) return;   // kaynak ağacı yoksa atla
        string icerik = File.ReadAllText(yol);
        Assert.DoesNotContain("LabDenetim.csproj", icerik, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BulutSoft.Core.csproj", icerik, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CekirdekProjeDosyasi_ArayuzPaketiVeWindowsFormsIcermez()
    {
        string? d = AppContext.BaseDirectory; string? yol = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "BulutSoft.Core", "BulutSoft.Core.csproj"); if (File.Exists(y)) { yol = y; break; } }
        if (yol == null) return;
        // Yorum satırları sayılmasın diye XML olarak çözülür.
        var xml = System.Xml.Linq.XDocument.Load(yol);
        Assert.Empty(xml.Descendants("UseWindowsForms"));
        Assert.Empty(xml.Descendants("UseWPF"));
        var paketler = xml.Descendants("PackageReference").Select(x => (string?)x.Attribute("Include") ?? "").ToList();
        Assert.DoesNotContain(paketler, p => p.Contains("WebView2", StringComparison.OrdinalIgnoreCase) || p.Contains("QuestPDF", StringComparison.OrdinalIgnoreCase) || p.Contains("Windows.Forms", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("net10.0", xml.Descendants("TargetFramework").Single().Value);
        Assert.Equal("latest-recommended", xml.Descendants("AnalysisLevel").Single().Value);
    }
}
