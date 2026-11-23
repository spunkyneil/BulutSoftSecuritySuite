using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Sensör ssh ANAHTARI yönetimi: üretim, DPAPI ile parola koruması, SSH_ASKPASS yardımcısı, ortak anahtarı sensöre ekleme komutu.
internal static class SensorAnahtarYoneticisi
{
    // Yeni bir ed25519 anahtar çifti üretir (parolasız; dosya yalnızca bu Windows hesabında durur). Var olan anahtarın ÜZERİNE YAZMAZ.
    public static async Task<string> AnahtarUretAsync(string yol)
    {
        // Var olanı koru.
        if (File.Exists(yol)) return File.ReadAllText(yol + ".pub").Trim();
        // Klasör.
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        // ssh-keygen. Anahtar RASTGELE PAROLAYLA şifreli üretilir; parola yalnızca bu Windows kullanıcısının DPAPI'siyle ("yol.dpapi") saklanır ve bağlantı anında SSH_ASKPASS ile verilir.
        // Böylece disk üzerinde düz (şifresiz) özel anahtar bulunmaz.
        string kg = File.Exists(@"C:\Windows\System32\OpenSSH\ssh-keygen.exe") ? @"C:\Windows\System32\OpenSSH\ssh-keygen.exe" : "ssh-keygen"; string anahtarParolasi = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        // Süreç.
        var psi = new ProcessStartInfo(kg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }; foreach (var a in new[] { "-t", "ed25519", "-f", yol, "-N", anahtarParolasi, "-C", "bulutsoft-sensor", "-q" }) psi.ArgumentList.Add(a);
        // Çalıştır.
        using var p = Process.Start(psi)!; string hata = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync();
        // Sonuç.
        if (p.ExitCode != 0 || !File.Exists(yol + ".pub")) throw new InvalidOperationException("Anahtar üretilemedi: " + hata);
        ParolaKoru(yol, anahtarParolasi);
        // Ortak anahtar.
        return File.ReadAllText(yol + ".pub").Trim();
    }

    // ---- Özel anahtar koruması (DPAPI) ----
    private static readonly byte[] ParolaEk = System.Text.Encoding.UTF8.GetBytes("bs-ssh-anahtar-v1");
    // Anahtar parolasını DPAPI (CurrentUser) ile "yol.dpapi" dosyasına yazar.
    private static void ParolaKoru(string yol, string parola) => Depo.YazBayt(yol + ".dpapi", System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(parola), ParolaEk, System.Security.Cryptography.DataProtectionScope.CurrentUser));
    // Anahtar parolası (yoksa/çözülemezse null).
    internal static string? ParolaOku(string yol) { try { return File.Exists(yol + ".dpapi") ? System.Text.Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(File.ReadAllBytes(yol + ".dpapi"), ParolaEk, System.Security.Cryptography.DataProtectionScope.CurrentUser)) : null; } catch { return null; } }
    private static string KeygenYolu => File.Exists(@"C:\Windows\System32\OpenSSH\ssh-keygen.exe") ? @"C:\Windows\System32\OpenSSH\ssh-keygen.exe" : "ssh-keygen";
    // ssh-keygen çalıştırır (çıkış kodu, çıktı).
    internal static (int, string) Keygen(params string[] args)
    {
        try { var psi = new ProcessStartInfo(KeygenYolu) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true }; foreach (var a in args) psi.ArgumentList.Add(a); using var pr = Process.Start(psi)!; pr.StandardInput.Close(); var h = pr.StandardError.ReadToEndAsync(); string o = pr.StandardOutput.ReadToEnd(); if (!pr.WaitForExit(15000)) { try { pr.Kill(true); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "SensorMantigi.Keygen"); } return (-1, ""); } return (pr.ExitCode, o + h.Result); }
        catch { return (-1, ""); }
    }
    // ESKİ düz (şifresiz) anahtarı yerinde parolalı hâle getirir ve parolayı DPAPI'ye yazar. Anahtar zaten parolalıysa (kullanıcı kendi koyduysa) dokunmaz. Başarıda true.
    internal static bool AnahtarKoru(string yol)
    {
        try
        {
            if (!File.Exists(yol) || File.Exists(yol + ".dpapi")) return File.Exists(yol + ".dpapi");
            if (Keygen("-y", "-P", "", "-f", yol).Item1 != 0) return false;      // parolasız açılamıyorsa zaten parolalı (kullanıcıya ait)
            string parola = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
            if (Keygen("-p", "-P", "", "-N", parola, "-f", yol, "-q").Item1 != 0) return false;
            ParolaKoru(yol, parola); return true;
        }
        catch { return false; }
    }
    // SSH_ASKPASS yardımcısı: parolayı ÇOCUK sürecin ortamından (BS_ANAHTAR_PAROLA) okuyup yazar; dosyada gizli bilgi yoktur.
    private static string AskpassYolu { get { string y = BulutKlasor.Dosya(Path.Combine("sensor", "askpass.cmd")); try { Directory.CreateDirectory(Path.GetDirectoryName(y)!); if (!File.Exists(y)) File.WriteAllText(y, "@echo %BS_ANAHTAR_PAROLA%\r\n", new UTF8Encoding(false)); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "SensorMantigi.AnahtarKoru"); } return y; } }
    // ssh sürecinin ortamına anahtar parolasını (yalnızca bellekte) ve askpass ayarlarını koyar. Parolalı anahtar kullanılıyorsa true.
    internal static bool AskpassHazirla(ProcessStartInfo psi, string anahtarYolu)
    {
        if (!AnahtarKoru(anahtarYolu)) return false; var parola = ParolaOku(anahtarYolu); if (parola == null) return false;
        psi.Environment["SSH_ASKPASS"] = AskpassYolu; psi.Environment["SSH_ASKPASS_REQUIRE"] = "force"; psi.Environment["DISPLAY"] = "bulutsoft"; psi.Environment["BS_ANAHTAR_PAROLA"] = parola; return true;
    }

    // Sensörde çalıştırılacak "ortak anahtarı ekle" komutunu üretir (tek tırnak içeren anahtar kabul edilmez).
    public static string AnahtarEklemeKomutu(string ortakAnahtar)
    {
        // Doğrula: tek satır, yalnızca beklenen karakterler.
        if (!Regex.IsMatch(ortakAnahtar ?? "", @"^ssh-ed25519 [A-Za-z0-9+/=]+( [\w\-.@]+)?$")) throw new ArgumentException("Ortak anahtar biçimi beklenmedik.");
        // Komut.
        return $"mkdir -p ~/.ssh && chmod 700 ~/.ssh && echo '{ortakAnahtar}' >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys";
    }
}
