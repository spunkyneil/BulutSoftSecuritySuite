// Kurumsal sınır testleri (CLAUDE.md kural 6): dahili satıcı/lisans üretim araçları standart (müşteri) derlemesinde BULUNMAZ.
using System.Reflection;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class KurumsalSinirTestleri
{
    private const BindingFlags Hepsi = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void LisansFormu_StandartDerlemede_SaticiAraclariniIcermez()
    {
        var t = typeof(LisansFormu);
        foreach (var ad in new[] { "OtoriteKurAsync", "SahipLisansiAsync", "MusteriLisansiAsync", "Sensor", "Yaz" })
            Assert.Null(t.GetMethod(ad, Hepsi));
        foreach (var ad in new[] { "_sensor", "_cikti", "_sensorler" })
            Assert.Null(t.GetField(ad, Hepsi));
        // Müşteri sürümü: yalnızca durum, makine kimliği ve lisans yükleme (Yukle) vardır.
        Assert.NotNull(t.GetMethod("Yukle", Hepsi));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void LisansFormu_KaynagiSaticiBolumunuKosulluDerlemeIcineAlir()
    {
        string? d = AppContext.BaseDirectory; string? yol = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "LabDenetim", "LisansFormu.cs"); if (File.Exists(y)) { yol = y; break; } }
        if (yol == null) return;
        var satirlar = File.ReadAllLines(yol); bool icinde = false; var disarida = new List<string>();
        foreach (var s in satirlar)
        {
            string k = s.Trim();
            if (k.StartsWith("#if INTERNAL_VENDOR_TOOLS", StringComparison.Ordinal)) { icinde = true; continue; }
            if (k.StartsWith("#else", StringComparison.Ordinal) || k.StartsWith("#endif", StringComparison.Ordinal)) { icinde = false; continue; }
            if (!icinde) disarida.Add(s);
        }
        string kod = string.Join("\n", disarida);
        foreach (var yasak in new[] { "Lisans Otoritesini Kur", "Sahip Lisansı Üret", "Müşteri Lisansı Üret", "LisansOtoritesi.", "OtoriteKurAsync(" })
            Assert.DoesNotContain(yasak, kod, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjeDosyasi_SaticiSabiti_YalnizcaVendorToolsOzelligiyleTanimlanir()
    {
        string? d = AppContext.BaseDirectory; string? yol = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "LabDenetim", "LabDenetim.csproj"); if (File.Exists(y)) { yol = y; break; } }
        if (yol == null) return;
        var xml = System.Xml.Linq.XDocument.Load(yol);
        var sabitler = xml.Descendants("DefineConstants").ToList();
        Assert.All(sabitler, e => { Assert.Contains("VendorTools", (string?)e.Attribute("Condition") ?? "", StringComparison.Ordinal); });
        Assert.DoesNotContain(sabitler, e => !((string?)e.Attribute("Condition") ?? "").Contains("VendorTools", StringComparison.Ordinal) && e.Value.Contains("INTERNAL_VENDOR_TOOLS", StringComparison.Ordinal));
    }
}
