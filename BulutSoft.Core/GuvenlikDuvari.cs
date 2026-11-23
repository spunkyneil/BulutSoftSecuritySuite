// Bu dosya Windows Güvenlik Duvarı ile konuşan yardımcı kodları içerir:
//  • BulutSoft ön ekli (BulutSoft-Blok-..., BulutSoft-Acil-...) kuralları listeler (okumak için yönetici izni gerekmez),
//  • kural ekleme/silme komutlarını TEK bir yönetici izni (UAC) penceresiyle çalıştırır.
// Program yalnızca kendi eklediği "BulutSoft-" ön ekli kurallara dokunur; başka kuralları asla silmez.
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Güvenlik duvarında bulunan bir BulutSoft kuralının bilgi kartı.
internal sealed record KalkanKurali(string Ad, string Tur, string Port, string Yon, bool Etkin, bool Engel);

// Güvenlik duvarı yardımcısı.
internal static class GuvenlikDuvari
{
    // Programın eklediği tüm kuralların ortak ön eki.
    public const string Onek = "BulutSoft-";

    // BulutSoft kurallarını Windows'tan okur (HNetCfg.FwPolicy2 bileşeni; yönetici izni gerekmez).
    public static List<KalkanKurali> Listele()
    {
        // Sonuç listesi.
        var liste = new List<KalkanKurali>();
        // Bileşen yoksa veya erişilemezse boş liste dön.
        try
        {
            // Güvenlik duvarı politika bileşenini bul.
            var tip = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            // Bulunamadıysa boş.
            if (tip == null) return liste;
            // Bileşenin bir örneğini oluştur (dinamik çağrılarla kullanılır).
            dynamic politika = Activator.CreateInstance(tip)!;
            // Tüm kuralları gez.
            foreach (dynamic r in politika.Rules)
            {
                // Kuralın adı.
                string ad = (string)(r.Name ?? "");
                // Yalnızca BulutSoft kurallarını al.
                if (!ad.StartsWith(Onek, StringComparison.Ordinal)) continue;
                // Etkin mi, eylem (0 = engelle), yön (1 = gelen, 2 = giden).
                bool etkin = (bool)r.Enabled; int eylem = (int)r.Action; int yon = (int)r.Direction;
                // Gelen kuralda yerel port, giden kuralda hedef adres gösterilir.
                string port = yon == 1 && !ad.StartsWith("BulutSoft-BF-", StringComparison.Ordinal) ? (string)(r.LocalPorts ?? "") : (string)(r.RemoteAddresses ?? "");
                // Kuralın türünü adından anla.
                string tur = ad.StartsWith("BulutSoft-Acil-", StringComparison.Ordinal) ? "Acil İzolasyon" : ad.StartsWith("BulutSoft-Blok-Cikis-", StringComparison.Ordinal) ? "Giden Engel" : ad.StartsWith("BulutSoft-Blok-", StringComparison.Ordinal) ? "Otomatik Onar" : ad.StartsWith("BulutSoft-BF-", StringComparison.Ordinal) ? "Saldırgan IP Engeli" : "Diğer";
                // Listeye ekle.
                liste.Add(new KalkanKurali(ad, tur, port, yon == 1 ? "Gelen" : "Giden", etkin, eylem == 0));
            }
        }
        // Okuma hatası olursa elimizdekini ver.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "GuvenlikDuvari.Listele"); }
        // Türe ve ada göre sırala.
        return liste.OrderBy(k => k.Tur).ThenBy(k => k.Ad, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Kural adı güvenli mi? (yalnızca BulutSoft- ile başlayan, harf/rakam/tire/nokta içeren adlar kabul edilir.)
    public static bool AdGuvenliMi(string ad) => Regex.IsMatch(ad, @"^BulutSoft-[A-Za-z0-9\-_.:]+\z");

    // Verilen portlar için "ad ön ekli" engelleme kuralları ekleyen tek bir komut zinciri üretir (önce eski aynı adlıyı siler).
    public static string EkleZinciri(IEnumerable<int> portlar, string adOnek) => string.Join(" & ", portlar.Select(p =>
        $"netsh advfirewall firewall delete rule name={adOnek}{p} >nul 2>&1 & netsh advfirewall firewall add rule name={adOnek}{p} dir=in action=block protocol=TCP localport={p}"));

    // Verilen adlı kuralları silen tek bir komut zinciri üretir (yalnızca güvenli adlar).
    public static string SilZinciri(IEnumerable<string> adlar) => string.Join(" & ", adlar.Where(AdGuvenliMi).Select(a => $"netsh advfirewall firewall delete rule name={a}"));

    // Güvenlik duvarı yedek klasörü: ProgramData altında, YALNIZCA yönetici yazabilir (kurulum klasörü korumalı oluşturur; yedek alınırken de korumalı kurulur).
    // Eskiden %AppData% altındaydı: aynı kullanıcının zararlısı dosyayı değiştirip kullanıcı "Geri Yükle"ye basınca yönetici izniyle uygulatabilirdi.
    public static string YedekKlasoru => Path.Combine(BrandConfig.OrtakKlasor, "fw_yedek");

    // Yedek alma zinciri (yönetici izniyle): klasörü yönetici sahipli ve yalnızca-yönetici-yazar kurar, sonra yedeği oraya yazar.
    public static string YedekAlZinciri(string dosya)
    {
        string k = Path.GetDirectoryName(dosya)!;
        return $"mkdir \"{k}\" 2>nul & icacls \"{k}\" /setowner *S-1-5-32-544 >nul 2>&1 & icacls \"{k}\" /inheritance:r /grant:r *S-1-5-32-544:(OI)(CI)F *S-1-5-18:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX >nul 2>&1 & netsh advfirewall export \"{dosya}\"";
    }

    // Klasörün sahibi Yöneticiler/SYSTEM mi ve Yönetici/SYSTEM dışında hiç kimseye yazma, silme ya da izin değiştirme hakkı verilmemiş mi?
    public static bool KlasorYalnizYoneticiYazar(string yol)
    {
        try
        {
            var di = new DirectoryInfo(yol); if (!di.Exists) return false; var acl = System.IO.FileSystemAclExtensions.GetAccessControl(di);
            static bool Guvenli(System.Security.Principal.SecurityIdentifier s) => s.IsWellKnown(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid) || s.IsWellKnown(System.Security.Principal.WellKnownSidType.LocalSystemSid) || s.Value.StartsWith("S-1-5-80-", StringComparison.Ordinal);
            if (acl.GetOwner(typeof(System.Security.Principal.SecurityIdentifier)) is not System.Security.Principal.SecurityIdentifier sahip || !Guvenli(sahip)) return false;
            var yazma = System.Security.AccessControl.FileSystemRights.WriteData | System.Security.AccessControl.FileSystemRights.AppendData | System.Security.AccessControl.FileSystemRights.Delete | System.Security.AccessControl.FileSystemRights.DeleteSubdirectoriesAndFiles | System.Security.AccessControl.FileSystemRights.ChangePermissions | System.Security.AccessControl.FileSystemRights.TakeOwnership;
            foreach (System.Security.AccessControl.FileSystemAccessRule r in acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)))
            {
                if (r.AccessControlType != System.Security.AccessControl.AccessControlType.Allow || (r.PropagationFlags & System.Security.AccessControl.PropagationFlags.InheritOnly) != 0) continue;
                if (!Guvenli((System.Security.Principal.SecurityIdentifier)r.IdentityReference) && (r.FileSystemRights & yazma) != 0) return false;
            }
            return true;
        }
        catch { return false; }
    }

    // Son yönetici komutunun çıkış kodu (cmd zincirinde SON komutun kodu; -1 = okunamadı). Çağıranlar sonucu doğrularken ek bilgi olarak kullanır.
    public static volatile int SonCikisKodu = -1;

    // Komut zincirini Windows yönetici izniyle (UAC penceresi) çalıştırır. Kullanıcı reddederse hiçbir şey yapılmaz.
    public static async Task<(bool Baslatildi, string Hata)> YoneticiIleCalistirAsync(string zincir)
    {
        // Çalıştırılacak bir şey yoksa çık.
        if (string.IsNullOrWhiteSpace(zincir)) return (false, "Çalıştırılacak komut yok.");
        // Pilot (salt okunur) modda hiçbir yönetici komutu çalıştırılmaz (son savunma hattı).
        if (PilotMod.SaltOkunur) return (false, PilotMod.Mesaj);
        // Komut başlatılamayabilir.
        try
        {
            // cmd.exe ile zinciri yönetici izniyle (runas) görünmez pencerede çalıştır.
            var psi = new ProcessStartInfo("cmd.exe", "/c " + zincir) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            // Arka planda çalıştır ve bitmesini bekle (ekran donmasın).
            SonCikisKodu = -1; await Task.Run(() => { using var p = Process.Start(psi); p?.WaitForExit(); try { if (p != null) SonCikisKodu = p.ExitCode; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "GuvenlikDuvari.YoneticiIleCalistirAsync"); } });
            // Kuralların Windows'ta görünür olması için kısa bir an bekle.
            await Task.Delay(500);
            // Başlatıldı.
            return (true, "");
        }
        // Kullanıcı yönetici izni penceresini (UAC) reddetti.
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return (false, "Yönetici izni verilmedi; hiçbir değişiklik yapılmadı."); }
        // Başka bir sorun.
        catch (Exception ex) { return (false, ex.Message); }
    }
}
