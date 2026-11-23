// Dış araca (Python, OpenSSL) bağlı entegrasyon testlerinin ortak yardımcıları.
// KURAL: araç bulunamazsa test sessizce "geçti" SAYILMAZ; xUnit'te açıkça ATLANDI (Skipped) görünür ve nedeni yazılır.
// Python sırasıyla BULUTSOFT_PYTHON_PATH ortam değişkeninden, yoksa sistem PATH'indeki python / python3 / py'den çözülür.
// OpenSSL sırasıyla BULUTSOFT_OPENSSL_PATH'ten, yoksa PATH'teki openssl'den, yoksa Git for Windows'un openssl'inden çözülür.
using System.Diagnostics;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Çalıştırılabilir araç çözücü (sürüm sorgusuyla DOĞRULAR: Windows'un "Mağaza" python.exe taslağı gibi sahte kısayollar elenir).
internal static class AracCozucu
{
    // Verilen yolun gerçekten çalışan bir araç olup olmadığına bakar (--version çıkış kodu 0 ve çıktı beklenen önekle başlar).
    internal static bool Calisir(string yol, string arguman, string beklenenOnek)
    {
        try
        {
            if (!File.Exists(yol)) return false;
            using var p = Process.Start(new ProcessStartInfo(yol, arguman) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p == null) return false;
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(8000)) { try { p.Kill(true); } catch (InvalidOperationException hkt1) { HataKayit.Sessiz(hkt1, "TestOrtamiGereksinimleri.temizlik"); } return false; }
            string cikti = (o.Result + e.Result).TrimStart();
            return p.ExitCode == 0 && cikti.StartsWith(beklenenOnek, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException) { return false; }
    }

    // PATH üzerinde adı (uzantılarıyla) arar ve ilk DOĞRULANAN yolu verir.
    internal static string? PathIcinde(string[] adlar, string arguman, string beklenenOnek)
    {
        var dizinler = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var ad in adlar)
            foreach (var dizin in dizinler)
            {
                string yol; try { yol = Path.Combine(dizin.Trim('"'), ad + ".exe"); } catch (ArgumentException) { continue; }
                if (Calisir(yol, arguman, beklenenOnek)) return yol;
            }
        return null;
    }
}

// Python çözücü.
internal static class TestPython
{
    // Çözülen python yolu (yoksa null). Bir kez hesaplanır.
    public static string? Yol { get; } = Coz();
    // Atlama nedeni.
    public static string EksiklikMesaji { get; private set; } = "Python bulunamadı: BULUTSOFT_PYTHON_PATH ortam değişkenini python.exe yoluna ayarlayın veya python/python3/py'yi PATH'e ekleyin (Python entegrasyon testi çalıştırılamadı).";

    private static string? Coz()
    {
        string? env = Environment.GetEnvironmentVariable("BULUTSOFT_PYTHON_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (AracCozucu.Calisir(env.Trim().Trim('"'), "--version", "Python")) return env.Trim().Trim('"');
            EksiklikMesaji = "BULUTSOFT_PYTHON_PATH geçerli bir Python göstermiyor: " + env + " (Python entegrasyon testi çalıştırılamadı).";
            return null;
        }
        return AracCozucu.PathIcinde(new[] { "python", "python3", "py" }, "--version", "Python");
    }
}

// OpenSSL çözücü.
internal static class TestOpenSsl
{
    public static string? Yol { get; } = Coz();
    public static string EksiklikMesaji { get; } = "OpenSSL bulunamadı: BULUTSOFT_OPENSSL_PATH ayarlayın veya openssl'i PATH'e ekleyin (Git for Windows'un usr/bin/openssl.exe'si de aranır); OpenSSL uyumluluk testi çalıştırılamadı.";

    private static string? Coz()
    {
        string? env = Environment.GetEnvironmentVariable("BULUTSOFT_OPENSSL_PATH");
        if (!string.IsNullOrWhiteSpace(env)) return AracCozucu.Calisir(env.Trim().Trim('"'), "version", "OpenSSL") ? env.Trim().Trim('"') : null;
        var p = AracCozucu.PathIcinde(new[] { "openssl" }, "version", "OpenSSL"); if (p != null) return p;
        string git = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "usr", "bin", "openssl.exe");
        return AracCozucu.Calisir(git, "version", "OpenSSL") ? git : null;
    }
}

// Python gerektiren test: Python yoksa (veya ek isteğe bağlı ortam değişkeni "1" değilse) açıkça ATLANIR; asla sessizce geçmez.
public sealed class PythonFactAttribute : FactAttribute
{
    public PythonFactAttribute(string? istegeBagliOrtamDegiskeni = null)
    {
        if (TestPython.Yol == null) Skip = TestPython.EksiklikMesaji;
        else if (istegeBagliOrtamDegiskeni != null && Environment.GetEnvironmentVariable(istegeBagliOrtamDegiskeni) != "1") Skip = $"Bu canlı test isteğe bağlıdır: {istegeBagliOrtamDegiskeni}=1 ayarlayın.";
    }
}

// Python gerektiren veri tabanlı test.
public sealed class PythonTheoryAttribute : TheoryAttribute
{
    public PythonTheoryAttribute() { if (TestPython.Yol == null) Skip = TestPython.EksiklikMesaji; }
}

// OpenSSL gerektiren test.
public sealed class OpenSslFactAttribute : FactAttribute
{
    public OpenSslFactAttribute() { if (TestOpenSsl.Yol == null) Skip = TestOpenSsl.EksiklikMesaji; }
}
