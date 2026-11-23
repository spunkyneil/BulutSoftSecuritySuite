// Bu dosya "Süreç Avcısı"nı içerir: fidye yazılımı alarmından sonra "dosyaları kim yazıyor?" sorusuna cevap arar.
// Çalışan her işlemin diske yazdığı veri miktarını kısa bir süre ölçer; çok yazanları (şüpheli) listeler ve gerekirse (onayla) sonlandırır.
// Sonlandırma sistemi etkiler: onay + Yönetici rolü gerekir; sistem süreçlerine, Microsoft imzalı işlemlere ve programın kendisine dokunulmaz.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// Ölçüm sonucu: bir sürecin ölçüm süresince yazdığı veri.
internal sealed record SurecYazma(int Pid, string Ad, string Yol, long YazilanBayt, long YazmaIslemi);

// Süreç avcısı mantığı.
internal static class SurecAvcisi
{
    // Windows'un işlem G/Ç sayaçları.
    [StructLayout(LayoutKind.Sequential)] private struct IoSayac { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessIoCounters(IntPtr hProcess, out IoSayac io);

    // Asla sonlandırılmayacak sistem süreçleri.
    private static readonly HashSet<string> Korunan = new(StringComparer.OrdinalIgnoreCase) { "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "dwm", "fontdrvhost", "memory compression", "secure system", "explorer", "sihost", "taskhostw", "ctfmon", "runtimebroker", "searchhost", "startmenuexperiencehost", "msmpeng", "securityhealthservice" };

    // Bu süreç korumalı mı? (adı sistem listesinde veya programın kendisi)
    public static bool Korunuyor(string ad, int pid) => Korunan.Contains((ad ?? "").Replace(".exe", "", StringComparison.OrdinalIgnoreCase)) || pid == Environment.ProcessId || pid <= 4;

    // Sonlandırılması veri bozabilecek tam adlı süreçler: veritabanı, web sunucusu, güncelleme ve eşitleme hizmetleri.
    private static readonly HashSet<string> EkKorunan = new(StringComparer.OrdinalIgnoreCase) { "sqlservr", "sqlwriter", "sqlagent", "mysqld", "mariadbd", "postgres", "oracle", "mongod", "w3wp", "inetinfo", "tiworker", "trustedinstaller", "msdtc", "vmms", "vmwp", "onedrive", "robocopy" };
    // Ada göre öne bakılan korumalar: yedekleme ve sanallaştırma ürünleri (ör. "veeam.backup.service", "vmware-vmx", "acronis_xyz").
    private static readonly string[] EkKorunanOnek = { "veeam", "acronis", "vmware", "vbox", "virtualbox", "backup", "bacula", "cohesity", "commvault" };

    // Sürecin Windows hizmeti olup olmadığını SADECE İSTENDİĞİNDE (tek süreç için, tıklama anında) sorar; liste yüklenirken çağrılmaz.
    // Sorgu çalışmazsa (WMI bozuk ya da kapalı) sonlandırma ENGELLENMEZ ama "HizmetSorgusuBasarisiz" işaretlenir ve arayüz kullanıcıya AÇIKÇA sorar:
    // saldırgan WMI'yi bozarak süreci "dokunulmaz" gösteremesin. Testte sahtesi verilir.
    internal static volatile bool HizmetSorgusuBasarisiz;
    internal static Func<int, bool> HizmetMi = pid =>
    {
        // WMI sorgusu: bu PID'ye ait bir hizmet var mı?
        try { using var ara = new System.Management.ManagementObjectSearcher($"SELECT Name FROM Win32_Service WHERE ProcessId={pid}"); foreach (var _ in ara.Get()) return true; return false; }
        // Sorulamadıysa: işaretle (kullanıcıya sorulacak), engelleme.
        catch { HizmetSorgusuBasarisiz = true; return false; }
    };

    // Yol, kullanıcının yazabildiği bir yerde mi? (Temp, İndirilenler, AppData, Ortak Kullanıcı klasörü...) Sistem dosyaları buralarda durmaz.
    internal static bool KullaniciYazilabilirYol(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol)) return false;
        return yol.Contains("\\AppData\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\Temp\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\Users\\Public\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\$Recycle.Bin\\", StringComparison.OrdinalIgnoreCase) || yol.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase);
    }

    // ETW/fidye alarmı için: Microsoft imzalı VE Windows/Program Files altından çalışan süreç (örn. CompatTelRunner, TiWorker, Defender taraması) kendi işini yapıyordur;
    // alarm üretmez. Ad değil İMZA + YOL kararı verir (adı taklit eden Temp'teki dosya imzasız/yanlış yolda olduğu için bu istisnadan yararlanamaz).
    internal static bool GuvenilirSistemSureciMi(string? yol, bool microsoftImzali)
    {
        if (!microsoftImzali || string.IsNullOrWhiteSpace(yol)) return false; if (KullaniciYazilabilirYol(yol) && !SistemYolu(yol)) return false;
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return SistemYolu(yol) || (pf.Length > 0 && yol.StartsWith(pf + "\\", StringComparison.OrdinalIgnoreCase)) || (pf86.Length > 0 && yol.StartsWith(pf86 + "\\", StringComparison.OrdinalIgnoreCase));
    }

    // Yol Windows klasörü ya da Defender klasörlerinden birinde mi? (sistem süreçlerinin gerçek konumu)
    internal static bool SistemYolu(string yol)
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows); string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return (win.Length > 0 && yol.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase)) || yol.StartsWith(Path.Combine(pd, "Microsoft", "Windows Defender") + "\\", StringComparison.OrdinalIgnoreCase) || yol.StartsWith(Path.Combine(pf, "Windows Defender") + "\\", StringComparison.OrdinalIgnoreCase) || yol.StartsWith(Path.Combine(pf86, "Windows Defender") + "\\", StringComparison.OrdinalIgnoreCase);
    }

    // Süreç neden sonlandırılamaz? Nedeni (kullanıcıya gösterilecek metin) verir; sorun yoksa null. Hizmet sorgusu yalnızca öncekiler geçince, tek kez çalışır.
    // yol: sürecin gerçek dosya yolu (biliniyorsa). Koruma yalnızca ada değil ADA + YOLA bakar: adı "svchost", "backup_job" vb. olup Temp/İndirilenler/AppData'dan çalışan süreç,
    // ad taklidi (T1036) olabileceği için "dokunulmaz" sayılmaz (kullanıcı yine açık onay verir ve Microsoft imzalı dosyalar ayrıca engellenir). Yol bilinmiyorsa ad koruması aynen geçerlidir.
    public static string? KorumaNedeni(string ad, int pid, string? yol = null)
    {
        HizmetSorgusuBasarisiz = false;
        // 0) Programın kendisi ve çok düşük PID'ler her zaman korunur.
        if (pid == Environment.ProcessId || pid <= 4) return "Bu süreç sistem için kritik veya programın kendisidir; sonlandırılamaz.";
        // 1) Mevcut sistem koruması (eski metin aynen korunur). Yol biliniyor ve sistem klasörlerinin DIŞINDA, kullanıcı-yazılabilir bir yerdeyse ad taklidi sayılır.
        if (Korunuyor(ad, pid) && !(KullaniciYazilabilirYol(yol) && !SistemYolu(yol!))) return "Bu süreç sistem için kritik veya programın kendisidir; sonlandırılamaz.";
        // Ad (".exe" olmadan).
        string a = (ad ?? "").Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        // 2) Veri bozabilecek ürünler (ad listesi veya ön ek). Kullanıcı-yazılabilir yoldan çalışanlar (ad taklidi) bu korumadan çıkar.
        if ((EkKorunan.Contains(a) || EkKorunanOnek.Any(o => a.StartsWith(o, StringComparison.OrdinalIgnoreCase))) && !KullaniciYazilabilirYol(yol)) return $"\"{a}\" bir veritabanı, yedekleme, sanallaştırma veya sistem hizmeti ürününe benziyor; yazma ortasında sonlandırmak veriyi bozabilir. Gerekiyorsa ürünün kendi aracından durdurun.";
        // 3) Windows hizmeti olarak çalışıyor mu? (tek PID için, tıklama anında)
        if (HizmetMi(pid)) return $"\"{a}\" bir Windows hizmeti olarak çalışıyor (veya hizmet durumu doğrulanamadı); doğrudan sonlandırmak yerine Hizmetler (services.msc) üzerinden durdurun.";
        // Engel yok.
        return null;
    }

    // Süreçlerin yazma sayaçlarının anlık görüntüsü: pid → (yazılan bayt, yazma işlemi, ad, başlangıç zamanı).
    private static Dictionary<int, (long Bayt, long Islem, string Ad, DateTime Basla, string Yol)> Anlik()
    {
        // Sonuç.
        var d = new Dictionary<int, (long, long, string, DateTime, string)>();
        // Her süreç; erişilemeyenler atlanır.
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                // Sayaçlar (aynı kullanıcının süreçleri için yönetici izni gerekmez).
                if (!GetProcessIoCounters(p.Handle, out var io)) continue;
                // Başlangıç zamanı: aynı PID'nin yeniden kullanımını ayırt etmek için.
                string yol = ""; try { yol = p.MainModule?.FileName ?? ""; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SurecAvcisi.Anlik"); }
                d[p.Id] = ((long)io.WriteBytes, (long)io.WriteOps, p.ProcessName, p.StartTime, yol);
            }
            catch (Exception hk2) { HataKayit.Sessiz(hk2, "SurecAvcisi.Anlik"); }
            finally { p.Dispose(); }
        }
        // Sonuç.
        return d;
    }

    // İki anlık görüntü arasındaki farkı hesaplar (yeni başlayan/aynı süreç ayrımı dahil). Test edilebilir.
    internal static List<SurecYazma> Fark(Dictionary<int, (long Bayt, long Islem, string Ad, DateTime Basla, string Yol)> once, Dictionary<int, (long Bayt, long Islem, string Ad, DateTime Basla, string Yol)> sonra)
    {
        // Sonuç.
        var l = new List<SurecYazma>();
        // Sonradaki her süreç.
        foreach (var (pid, s) in sonra)
        {
            // Aynı süreç mi? (PID tekrar kullanılmış olabilir)
            if (!once.TryGetValue(pid, out var o) || o.Basla != s.Basla) continue;
            // Fark.
            long bayt = s.Bayt - o.Bayt, islem = s.Islem - o.Islem; if (bayt <= 0) continue;
            // Ekle.
            l.Add(new SurecYazma(pid, s.Ad, s.Yol, bayt, islem));
        }
        // En çok yazandan aza.
        return l.OrderByDescending(x => x.YazilanBayt).ToList();
    }

    // Verilen süre boyunca ölçer (arka planda çağrılmalı).
    public static List<SurecYazma> Olc(int saniye)
    {
        // İki ölçüm arası bekle.
        var once = Anlik(); Thread.Sleep(Math.Clamp(saniye, 2, 30) * 1000); var sonra = Anlik();
        // Fark.
        return Fark(once, sonra);
    }

    // Bir süreci sonlandırır (korumalıysa ArgumentException, başarısızsa Win32Exception).
    public static void Sonlandir(int pid, string ad)
    {
        // Pilot (salt okunur) modda süreç sonlandırılmaz.
        if (PilotMod.SaltOkunur) throw new InvalidOperationException(PilotMod.Mesaj);
        // Sürecin gerçek yolu (okunabiliyorsa): koruma ada ve yola birlikte bakar.
        string? yol = null; try { using var q = Process.GetProcessById(pid); yol = q.MainModule?.FileName; } catch (Exception hk3) { HataKayit.Sessiz(hk3, "SurecAvcisi.Sonlandir"); }
        // Güvenlik: korumalı süreç.
        if (KorumaNedeni(ad, pid, yol) is { } neden) throw new ArgumentException(neden);
        // Süreci bul ve adı hâlâ aynı mı doğrula (PID değişmiş olabilir).
        using var p = Process.GetProcessById(pid); if (!string.Equals(p.ProcessName, ad, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Süreç değişmiş; ölçümü yenileyin.");
        // Ağaçla birlikte sonlandır.
        p.Kill(true);
    }
}
