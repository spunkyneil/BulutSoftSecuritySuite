// Faz 5: iki kurulum profili (Hafif / Cevrimdisi) betik ve çıktı denetimleri. Kurulum gerçek makinede ÇALIŞTIRILMAZ; betik yapısı, yayın hattı, güncelleme betiği ve (varsa) üretilmiş paketlerin boyut/özet tutarlılığı sınanır.
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class KurulumProfilTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }
    private static string Oku(string ad) { string? k = Kok(); return k == null ? "" : File.ReadAllText(Path.Combine(k, ad)); }

    [Fact]
    public void InnoBetigi_IkiProfil_CevrimdisiYalnizcaGomulu_HafifImzaliIndirir()
    {
        string iss = Oku("BulutSoft.iss"); if (iss.Length == 0) return;
        Assert.Contains("#define Profil \"Cevrimdisi\"", iss, StringComparison.Ordinal);                       // varsayılan = eski davranış
        Assert.Contains("OutputBaseFilename=BulutSoft_Setup_v{#Surum}_{#Profil}", iss, StringComparison.Ordinal);
        // Gömülü WebView2 kurucusu ve çalıştırma satırı yalnızca Cevrimdisi bloklarında.
        foreach (var anahtar in new[] { "Source: \"Kurucular\\MicrosoftEdgeWebView2RuntimeInstallerX64.exe\"", "Filename: \"{tmp}\\MicrosoftEdgeWebView2RuntimeInstallerX64.exe\"" })
        {
            int i = iss.IndexOf(anahtar, StringComparison.Ordinal); Assert.True(i > 0, anahtar);
            int bas = iss.LastIndexOf("#if Profil", i, StringComparison.Ordinal); int son = iss.IndexOf("#endif", i, StringComparison.Ordinal);
            Assert.True(bas >= 0 && son > i && iss.Substring(bas, i - bas).StartsWith("#if Profil == \"Cevrimdisi\"", StringComparison.Ordinal), anahtar + " yalnızca Cevrimdisi profilinde olmalı");
        }
        // Hafif profil: indirme + Microsoft imzası doğrulaması + hata sessiz geçilir.
        int h = iss.IndexOf("#if Profil == \"Hafif\"", StringComparison.Ordinal); Assert.True(h > 0);
        string hafif = iss.Substring(h, iss.IndexOf("#endif", h, StringComparison.Ordinal) - h);
        Assert.Contains("DownloadTemporaryFile", hafif, StringComparison.Ordinal); Assert.Contains("Get-AuthenticodeSignature", hafif, StringComparison.Ordinal); Assert.Contains("O=Microsoft Corporation", hafif, StringComparison.Ordinal);
        Assert.Contains("except", hafif, StringComparison.Ordinal); Assert.Contains("https://go.microsoft.com/", hafif, StringComparison.Ordinal); Assert.Contains("if not WebView2Gerekli then Exit;", hafif, StringComparison.Ordinal);
        // Kullanıcı verisi koruması değişmedi: silme bölümü yok.
        Assert.DoesNotContain("\n[InstallDelete]", iss, StringComparison.Ordinal); Assert.DoesNotContain("\n[UninstallDelete]", iss, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(iss, @"^\[Code\]\s*$", RegexOptions.Multiline));   // [Code] bölümü tek (yorumdaki geçiş sayılmaz)
    }

    [Fact]
    public void YayinlaVeGuncelle_IkiProfiliTanir_EskiAdlandirmaBozulmaz()
    {
        string y = Oku("Yayinla.ps1"); if (y.Length == 0) return;
        Assert.Contains("ValidateSet('Hepsi', 'Hafif', 'Cevrimdisi')", y, StringComparison.Ordinal); Assert.Contains("\"/DProfil=$p\"", y, StringComparison.Ordinal); Assert.Contains("BulutSoft_Setup_v${Surum}_$p.exe", y, StringComparison.Ordinal);
        Assert.Contains("if ($profiller -contains 'Cevrimdisi')", y, StringComparison.Ordinal);
        Assert.Contains("package --vulnerable --include-transitive", y, StringComparison.Ordinal);   // yayın öncesi bağımlılık zafiyet taraması (ASVS V15)   // WebView2 kurucusu zorunluluğu yalnızca çevrimdışı profilde
        // Güncelleme betiğinin dosya adı deseni: yeni adlar ve eski (profilsiz) ad kabul edilir.
        string g = Oku("Guncelle.ps1"); var m = Regex.Match(g, @"-match '(\^BulutSoft_Setup_v[^']+)'"); Assert.True(m.Success);
        var re = new Regex(m.Groups[1].Value);
        Assert.Matches(re, "BulutSoft_Setup_v1.5.3_Hafif.exe"); Assert.Matches(re, "BulutSoft_Setup_v1.5.3_Cevrimdisi.exe"); Assert.Matches(re, "BulutSoft_Setup_v1.2.1.exe"); Assert.DoesNotMatch(re, "BulutSoft_Setup_v1.5.3_Baska.exe");
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void UretilmisPaketler_VarsaOzetleriUyarVeHafifPaketCokKucuktur()
    {
        string? k = Kok(); if (k == null) return; string cikti = Path.Combine(k, "Output"); string sum = Path.Combine(cikti, "SHA256SUMS.txt");
        if (!Directory.Exists(cikti)) return;   // Output klasörü yoksa (CI/temiz kopya) atla
        var hafif = Directory.EnumerateFiles(cikti, "BulutSoft_Setup_v*_Hafif.exe").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault(); var cevrimdisi = Directory.EnumerateFiles(cikti, "BulutSoft_Setup_v*_Cevrimdisi.exe").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (hafif == null || cevrimdisi == null || !File.Exists(sum)) return;   // paketler henüz üretilmediyse (temiz kopya) atla
        var satirlar = File.ReadAllLines(sum).Select(s => s.Split("  ", 2)).ToDictionary(p => p[1], p => p[0]);
        foreach (var f in new[] { hafif, cevrimdisi }) { Assert.True(satirlar.ContainsKey(Path.GetFileName(f)), Path.GetFileName(f) + " özet satırı yok"); Assert.Equal(satirlar[Path.GetFileName(f)], Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant()); }
        long fark = new FileInfo(cevrimdisi).Length - new FileInfo(hafif).Length;
        Assert.True(fark > 200L * 1024 * 1024, $"Çevrimdışı paket, WebView2 kurucusu (~280 MB) kadar büyük olmalı; fark {fark / 1048576} MB");   // WebView2 yalnızca çevrimdışıda gömülü
        Assert.True(new FileInfo(hafif).Length < 120L * 1024 * 1024, "Hafif paket 120 MB altında olmalı");
    }
}
