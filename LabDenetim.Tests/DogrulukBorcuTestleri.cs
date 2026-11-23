// D1 doğruluk borcu testleri: MITRE ATT&CK v20 taksonomisi, KVKK dayanağı, ihlal müdahale planı, CIS lisans sınırı.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class DogrulukBorcuTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Mitre_V20Taksonomisi_EskiDefenseEvasionVeEskiTeknikKimlikleriYok()
    {
        Assert.Contains("v20", Mitre.Surum, StringComparison.Ordinal);
        var firewall = Mitre.Bul(new Bulgu("Karne", "Güvenlik duvarı kapalı", Onem.Yuksek, "Windows Güvenlik Duvarı (firewall) kapalı", "Açın")); Assert.NotNull(firewall);
        Assert.Equal("T1686.003", firewall!.Kimlik); Assert.Equal("Defense Impairment", firewall.Taktik);
        var defender = Mitre.Bul(new Bulgu("Karne", "Defender kapalı", Onem.Yuksek, "antivirüs devre dışı", "Açın")); Assert.Equal("T1685", defender!.Kimlik); Assert.Equal("Defense Impairment", defender.Taktik);
        Assert.Equal("T1685", Mitre.Bul(new Bulgu("Karne", "Başka karne maddesi", Onem.Orta, "x", "y"))!.Kimlik);
        string? kok = Kok(); if (kok == null) return; string kod = string.Join(Environment.NewLine, File.ReadAllLines(Path.Combine(kok, "BulutSoft.Core", "Mitre.cs")).Where(s => !s.TrimStart().StartsWith("//")));   // açıklama satırları hariç
        Assert.DoesNotContain("\"Defense Evasion\"", kod, StringComparison.Ordinal);   // v20'de taktik adı değişti
        // Hiçbir kural dosyası "Defense Evasion" taktiği ya da kaldırılan T1562.001/.004 kimliğini kullanmaz.
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, "BulutSoft.Core"), "*.json")) { string j = File.ReadAllText(f); Assert.DoesNotContain("Defense Evasion", j, StringComparison.Ordinal); Assert.DoesNotContain("T1562.001", j, StringComparison.Ordinal); Assert.DoesNotContain("T1562.004", j, StringComparison.Ordinal); }
    }

    [Fact]
    public void Uyumluluk_KvkkRehberDayanagiRaporSatirlarinda_VeKaynakBelirtilir()
    {
        Assert.Contains("Nisan 2025", Uyumluluk.Kaynak, StringComparison.Ordinal);
        var port = new Bulgu("Port", "Açık port 445 (SMB)", Onem.Yuksek, "d", "o"); Assert.Equal("Ağ Güvenliği", Uyumluluk.RehberTedbiri(port));
        Assert.Contains(Uyumluluk.Uzun(port), s => s.StartsWith("KVKK Rehberi (2025) teknik tedbir: Ağ Güvenliği", StringComparison.Ordinal));
        Assert.Equal("Şifreleme", Uyumluluk.RehberTedbiri(new Bulgu("TLS", "x", Onem.Orta, "d", "o")));
        Assert.Null(Uyumluluk.RehberTedbiri(new Bulgu("Bilinmeyen", "x", Onem.Orta, "d", "o")));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void IhlalPlani_72SaatiVeKaynagiIcerir_KanitPaketineGirer()
    {
        string m = KvkkIhlalPlani.Metin(new DateTime(2026, 11, 22, 10, 0, 0));
        Assert.Contains("72 SAAT", m, StringComparison.Ordinal); Assert.Contains("2019/10", m, StringComparison.Ordinal); Assert.Contains("hukuki görüş değildir", m, StringComparison.OrdinalIgnoreCase); Assert.Contains("22.11.2026 10:00", m, StringComparison.Ordinal);
        string? kok = Kok(); if (kok == null) return; Assert.Contains("07_KVKK_ihlal_mudahale_plani.txt", File.ReadAllText(Path.Combine(kok, "LabDenetim", "KanitPaketi.cs")), StringComparison.Ordinal);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void CisBenchmark_LisansSiniri_KodVeArayuzdeCisUyumIddiasiYok()
    {
        // CIS Benchmarks içeriğini ticari üründe kullanmak SecureSuite Product Vendor üyeliği gerektirir; üye olunmadan "CIS uyumlu" iddiası ve içerik kopyası yasaktır (cisecurity.org terms-of-use).
        string? kok = Kok(); if (kok == null) return; var ihlal = new List<string>();
        foreach (var proje in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.*", SearchOption.AllDirectories).Where(x => x.EndsWith(".cs") || x.EndsWith(".json")))
            {
                if (Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]")) continue; int n = 0;
                foreach (var s in File.ReadLines(f)) { n++; if (s.TrimStart().StartsWith("//")) continue; if (Regex.IsMatch(s, @"\bCIS\s+(Benchmark|uyumlu|Controls|Level)", RegexOptions.IgnoreCase)) ihlal.Add($"{Path.GetFileName(f)}:{n}"); }
            }
        Assert.True(ihlal.Count == 0, "CIS iddiası/içeriği: " + string.Join(", ", ihlal));
    }
}
