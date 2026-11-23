// D2 kendi güvenliğimiz testleri: Microsoft .NET kriptografi/DPAPI önerileri ve OWASP ASVS 5.0 (V11 Kriptografi, V12 İletişim, V13 Sır yönetimi, V16 Loglama) için denetlenebilir kurallar.
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public sealed class GuvenliKodlamaTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "guv_" + Guid.NewGuid().ToString("N"));
    public GuvenliKodlamaTestleri() { Directory.CreateDirectory(_t); ApiDeposu.KlasorGecersiz = _t; }
    public void Dispose() { ApiDeposu.KlasorGecersiz = null; try { Directory.Delete(_t, true); } catch (IOException hk) { HataKayit.Sessiz(hk, "GuvenliKodlamaTestleri.temizlik"); } }

    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact]
    public void ClaudeAnahtari_EntropiliSaklanir_EskiEntropisizKayitBirKezGocEder()
    {
        string yol = Path.Combine(_t, "api.dat");
        ApiDeposu.AnahtarKaydet("sk-ant-deneme-123");
        var sifreli = File.ReadAllBytes(yol);
        Assert.Throws<CryptographicException>(() => ProtectedData.Unprotect(sifreli, null, DataProtectionScope.CurrentUser));   // entropisiz çözülemez
        Assert.Equal("sk-ant-deneme-123", Encoding.UTF8.GetString(ProtectedData.Unprotect(sifreli, ApiDeposu.AnahtarEntropisi, DataProtectionScope.CurrentUser)));
        Assert.Equal("sk-ant-deneme-123", ApiDeposu.AnahtarOku());
        // Eski sürüm kaydı (entropisiz): okunur ve entropili biçime taşınır.
        File.WriteAllBytes(yol, ProtectedData.Protect(Encoding.UTF8.GetBytes("sk-ant-eski"), null, DataProtectionScope.CurrentUser));
        Assert.Equal("sk-ant-eski", ApiDeposu.AnahtarOku());
        Assert.Throws<CryptographicException>(() => ProtectedData.Unprotect(File.ReadAllBytes(yol), null, DataProtectionScope.CurrentUser));   // artık entropisiz değil
        Assert.Equal("sk-ant-eski", ApiDeposu.AnahtarOku());
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kaynak_ZayifVeTehlikeliKriptoAPIleriYok_SHA1YalnizcaPwnedpasswordsIcin()
    {
        string? kok = Kok(); if (kok == null) return; var ihlal = new List<string>();
        foreach (var proje in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
            {
                if (Regex.IsMatch(f, @"[\/](obj|bin)[\/]")) continue; int n = 0;
                foreach (var s in File.ReadLines(f))
                {
                    n++; if (s.TrimStart().StartsWith("//")) continue; string ad = Path.GetFileName(f);
                    if (Regex.IsMatch(s, @"\bBinaryFormatter\b|\bMD5\.(Create|HashData)|new\s+Random\(|TripleDES|RijndaelManaged|DESCryptoServiceProvider")) ihlal.Add($"{ad}:{n} zayıf/tehlikeli API");
                    if (Regex.IsMatch(s, @"\bSHA1\.(Create|HashData)") && ad != "AracMantigi.cs") ihlal.Add($"{ad}:{n} SHA1 (yalnızca pwnedpasswords k-anonimliği için izinli)");
                    if (Regex.IsMatch(s, @"ProtectedData\.(Protect|Unprotect)\([^;]*,\s*null\s*,") && ad != "ClaudeApi.cs") ihlal.Add($"{ad}:{n} DPAPI entropisiz");
                }
            }
        Assert.True(ihlal.Count == 0, string.Join(" | ", ihlal));
        // ClaudeApi'de entropisiz Protect yok (yalnızca eski kaydı çözen göç yolu Unprotect(null) kullanır).
        string claude = File.ReadAllText(Path.Combine(kok, "LabDenetim", "ClaudeApi.cs")); Assert.DoesNotContain("ProtectedData.Protect(Encoding.UTF8.GetBytes(anahtar.Trim()), null", claude, StringComparison.Ordinal);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void SertifikaDogrulamaBypassi_YalnizcaYerelHedefTarayicilarindaVeDisServisFabrikasindaYok()
    {
        // Sertifika doğrulamasını kapatan geri çağrılar yalnızca kullanıcının kendi (özel/yerel) hedeflerini tarayan istemcilerde olabilir (kendi imzalı sertifikalar yaygındır); dış servis çağrılarında asla.
        string? kok = Kok(); if (kok == null) return; var izinli = new HashSet<string> { "AktifHttp.cs", "SahiplikDogrulama.cs", "PasifToplayici.cs", "HttpDenetimi.cs", "GirdiDogrulamaTesti.cs" }; var ihlal = new List<string>();
        foreach (var proje in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
            {
                if (Regex.IsMatch(f, @"[\/](obj|bin)[\/]")) continue; string metin = File.ReadAllText(f);
                if (Regex.IsMatch(metin, @"(Remote|ServerCertificateCustom)\w*Callback\s*=\s*\(_, _, _, _\)\s*=>\s*true") && !izinli.Contains(Path.GetFileName(f))) ihlal.Add(Path.GetFileName(f));
            }
        Assert.True(ihlal.Count == 0, "Sertifika doğrulaması kapatılmış: " + string.Join(", ", ihlal));
        Assert.DoesNotContain("=> true", File.ReadAllText(Path.Combine(kok, "BulutSoft.Core", "AgIstemciFabrikasi.cs"), Encoding.UTF8), StringComparison.Ordinal);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Parola_PBKDF2_600binTur_SabitZamanliKarsilastirma_RastgeleTuz()
    {
        string? kok = Kok(); if (kok == null) return; string kod = File.ReadAllText(Path.Combine(kok, "BulutSoft.Core", "Kimlik.cs"));
        Assert.Contains("VarsayilanTekrar { get; set; } = 600_000;", kod, StringComparison.Ordinal);   // OWASP/ASVS V6: PBKDF2-HMAC-SHA256 için güncel öneri (testlerde geçici düşürülebildiği için kaynaktan denetlenir)
        Assert.Contains("FixedTimeEquals", kod, StringComparison.Ordinal); Assert.Contains("RandomNumberGenerator.GetBytes(16)", kod, StringComparison.Ordinal);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Gunlukler_SirVeAnahtarIcermez_ASVS_V16()
    {
        string? kok = Kok(); if (kok == null) return; var ihlal = new List<string>();
        foreach (var proje in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
            {
                if (Regex.IsMatch(f, @"[\/](obj|bin)[\/]")) continue; int n = 0;
                foreach (var s in File.ReadLines(f)) { n++; if (s.TrimStart().StartsWith("//")) continue; if (Regex.IsMatch(s, @"(Gunluk|OlayDeposu\.Ekle|OlayYazici\.Ekle|HataKayit\.Sessiz)\([^;]*\{(anahtar|parola|sifre|password|apiKey|token)\b", RegexOptions.IgnoreCase)) ihlal.Add($"{Path.GetFileName(f)}:{n}"); }
            }
        Assert.True(ihlal.Count == 0, "Günlüğe sır yazılıyor olabilir: " + string.Join(", ", ihlal));
    }
}
