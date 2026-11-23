// Bu dosya bu bilgisayardan SALT-OKUNUR bilgi toplayan "kaynak" katmanıdır: dinleyen portlar (PID ile), Windows hizmetleri (WMI), süreç yolları,
// dijital imzalar, klasör yazma izinleri (ACL) ve kurulu güncellemeler (KB). Hiçbir şeyi değiştirmez.
// Kaynak bir arayüzün arkasındadır: testlerde sahte veriyle çalışılır, gerçek makineye bakılmaz.
using System.Globalization;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

// V2 ad alanı.
namespace LabDenetim.V2;

// Dinleyen (LISTEN durumundaki) bir TCP kapısı.
internal sealed record DinleyenPort(int Port, string Proto, string Adres, int Pid);
// Bir Windows hizmetinin ham kaydı (WMI Win32_Service).
internal sealed record WinHizmeti(string Ad, string GorunenAd, int Pid, string HamYol, string Hesap, string BaslatmaTuru, string Durum);
// Süreç bilgisi.
internal sealed record SurecKaydi(string Ad, string? Yol);
// Kurulu güncelleme (Win32_QuickFixEngineering).
internal sealed record HotfixKaydi(string Kb, DateTime? Tarih);

// Yerel bilgi kaynağı arayüzü.
internal interface IYerelKaynak
{
    // Dinleyen TCP kapıları (PID ile).
    List<DinleyenPort> Dinleyenler();
    // Windows hizmetleri.
    List<WinHizmeti> Hizmetler();
    // PID'den süreç adı ve yolu (bulunamazsa null).
    SurecKaydi? Surec(int pid);
    // Yollardaki dosyaların dijital imza durumu: yol → (durum, yayıncı, Microsoft mu).
    Dictionary<string, (string Durum, string Yayinci, bool Microsoft)> Imzalar(IEnumerable<string> yollar);
    // Bu klasöre sıradan kullanıcılar (Users / Everyone / Authenticated Users) DOSYA YAZABİLİR mi?
    bool DizinYazilabilirMi(string dizin);
    // Kurulu güncellemeler.
    List<HotfixKaydi> Hotfixler();
    // İşletim sistemi sürümü, örn. "10.0.26100.1234".
    string OsSurumu();
    // Bilgisayar adı.
    string MakineAdi();
    // Kurulu yazılımlar (varsayılan: boş; gerçek kaynak kayıt defterinden okur).
    List<InstalledSoftware> Yazilimlar() => new();
    // Bir dosyanın PE başlık bilgisi (varsayılan: yok; PE değilse null).
    PeReport? PeOku(string yol) => null;
    // Dosyanın kendisine sıradan kullanıcılar yazabilir mi? (varsayılan: hayır)
    bool DosyaYazilabilirMi(string yol) => false;
}

// Gerçek makineden okuyan uygulama.
internal sealed class GercekYerelKaynak : IYerelKaynak
{
    // Adres aileleri ve tablo türü (5 = PID'li tüm TCP); LISTEN durumu = 2.
    private const int AfInet = 2, AfInet6 = 23, Listen = 2;

    public List<DinleyenPort> Dinleyenler()
    {
        var l = new List<DinleyenPort>();
        try { Oku(AfInet, l); Oku(AfInet6, l); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "YerelKaynakV2.Dinleyenler"); }
        // Aynı port/adres/PID tekrarsız.
        return l.DistinctBy(x => (x.Port, x.Adres, x.Pid)).OrderBy(x => x.Port).ToList();
    }

    // Bir adres ailesinin tablosunu okur ve yalnızca LISTEN satırlarını ekler.
    private static void Oku(int aile, List<DinleyenPort> hedef)
    {
        // Tablo Windows'tan okunur (iki çağrı arasında büyürse yeniden denenir; okunamazsa hata kaydedilir).
        TcpTablosu.Oku(aile, tablo =>
        {
            int adet = Marshal.ReadInt32(tablo), satirBoyu = aile == AfInet ? 24 : 56;
            for (int i = 0; i < adet; i++)
            {
                IntPtr s = tablo + 4 + i * satirBoyu; int durum, portHam, pid; var adres = new byte[aile == AfInet ? 4 : 16];
                // IPv4: durum(0) yerelAdres(4) yerelPort(8) uzakAdres(12) uzakPort(16) pid(20).
                if (aile == AfInet) { durum = Marshal.ReadInt32(s, 0); Marshal.Copy(s + 4, adres, 0, 4); portHam = Marshal.ReadInt32(s, 8); pid = Marshal.ReadInt32(s, 20); }
                // IPv6: yerelAdres(0,16) kapsam(16) yerelPort(20) uzakAdres(24) kapsam(40) uzakPort(44) durum(48) pid(52).
                else { Marshal.Copy(s, adres, 0, 16); portHam = Marshal.ReadInt32(s, 20); durum = Marshal.ReadInt32(s, 48); pid = Marshal.ReadInt32(s, 52); }
                if (durum != Listen) continue;
                int port = ((portHam & 0xFF) << 8) | ((portHam >> 8) & 0xFF);
                hedef.Add(new DinleyenPort(port, "tcp", new IPAddress(adres).ToString(), pid));
            }
        });
    }

    public List<WinHizmeti> Hizmetler()
    {
        var l = new List<WinHizmeti>();
        try
        {
            using var q = new ManagementObjectSearcher("SELECT Name, DisplayName, ProcessId, PathName, StartName, StartMode, State FROM Win32_Service");
            foreach (ManagementObject m in q.Get()) { using (m) l.Add(new WinHizmeti(S(m, "Name"), S(m, "DisplayName"), m["ProcessId"] is uint p ? (int)p : 0, S(m, "PathName"), S(m, "StartName"), S(m, "StartMode"), S(m, "State"))); }
        }
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "YerelKaynakV2.Hizmetler"); }
        return l;
    }
    private static string S(ManagementBaseObject m, string ad) => Convert.ToString(m[ad], CultureInfo.InvariantCulture) ?? "";

    public SurecKaydi? Surec(int pid)
    {
        if (pid <= 4) return pid == 4 ? new SurecKaydi("System", null) : null;
        try { using var p = Process.GetProcessById(pid); string? yol = null; try { yol = p.MainModule?.FileName; } catch (Exception hk3) { HataKayit.Sessiz(hk3, "YerelKaynakV2.Surec"); } return new SurecKaydi(p.ProcessName + ".exe", yol); }
        catch { return null; }
    }

    // İmza sonuçları oturum boyunca saklanır: aynı dosyayı (hizmet + PE analizi) iki kez PowerShell'e sormayalım.
    private readonly Dictionary<string, (string Durum, string Yayinci, bool Microsoft)> _imzaOnbellek = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, (string Durum, string Yayinci, bool Microsoft)> Imzalar(IEnumerable<string> yollar)
    {
        var liste = yollar.Where(y => !string.IsNullOrWhiteSpace(y)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var eksik = liste.Where(y => !_imzaOnbellek.ContainsKey(y)).ToList();
        if (eksik.Count > 0) foreach (var (y, v) in ImzaDenetimi.Kontrol(eksik)) _imzaOnbellek[y] = v;
        var sonuc = new Dictionary<string, (string Durum, string Yayinci, bool Microsoft)>(StringComparer.OrdinalIgnoreCase);
        foreach (var y in liste) if (_imzaOnbellek.TryGetValue(y, out var v)) sonuc[y] = v;
        return sonuc;
    }

    public PeReport? PeOku(string yol) => PeAnalyzer.Oku(yol);

    public bool DosyaYazilabilirMi(string yol)
    {
        try { if (!File.Exists(yol)) return false; return KurallarYazmaIzniVeriyor(new FileInfo(yol).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()); }
        catch { return false; }
    }

    // Kullanıcı grupları: Everyone, Authenticated Users, Users (yerleşik).
    private static readonly SecurityIdentifier[] GenisGruplar = { new(WellKnownSidType.WorldSid, null), new(WellKnownSidType.AuthenticatedUserSid, null), new(WellKnownSidType.BuiltinUsersSid, null) };

    public bool DizinYazilabilirMi(string dizin)
    {
        try
        {
            if (!Directory.Exists(dizin)) return false;
            var kurallar = new DirectoryInfo(dizin).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
            return KurallarYazmaIzniVeriyor(kurallar.Cast<FileSystemAccessRule>());
        }
        catch { return false; }
    }

    // Kurallar arasında, geniş bir gruba bu klasörde DOSYA OLUŞTURMA/YAZMA hakkı veren (ve "yalnız alt klasörler" olmayan) izin var mı? Reddetme kuralı aynı hakkı kapatıyorsa yazılamaz sayılır.
    internal static bool KurallarYazmaIzniVeriyor(IEnumerable<FileSystemAccessRule> kurallar)
    {
        // Dosya oluşturma/yazma biti (WriteData = CreateFiles). Modify ve FullControl bu biti zaten içerir; bunları ayrıca maskelemek HER hakkı "yazma" sayardı.
        const FileSystemRights Yazma = FileSystemRights.WriteData;
        bool izin = false;
        foreach (var k in kurallar)
        {
            // Yalnızca geniş gruplar ve bu klasörün KENDİSİNE uygulanan kurallar (InheritOnly = sadece alt öğelere).
            if (k.IdentityReference is not SecurityIdentifier sid || !GenisGruplar.Contains(sid) || k.PropagationFlags.HasFlag(PropagationFlags.InheritOnly)) continue;
            if ((k.FileSystemRights & Yazma) == 0) continue;
            if (k.AccessControlType == AccessControlType.Deny) return false;
            izin = true;
        }
        return izin;
    }

    public List<HotfixKaydi> Hotfixler()
    {
        var l = new List<HotfixKaydi>();
        try
        {
            using var q = new ManagementObjectSearcher("SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering");
            foreach (ManagementObject m in q.Get()) using (m) { string kb = S(m, "HotFixID").Trim().ToUpperInvariant(); if (kb.Length > 0) l.Add(new HotfixKaydi(kb, TarihOku(S(m, "InstalledOn")))); }
        }
        catch (Exception hk4) { HataKayit.Sessiz(hk4, "YerelKaynakV2.Hotfixler"); }
        return l;
    }

    // WMI tarih metnini (yerel biçim veya ay/gün/yıl) okur.
    internal static DateTime? TarihOku(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // WMI bu alanı genellikle ay/gün/yıl (ABD) biçiminde verir; Türkçe yerel biçim (gün/ay/yıl) önce denenirse 11/12/2026 yanlış okunur. Bu yüzden önce ABD biçimi.
        foreach (var kultur in new[] { new System.Globalization.CultureInfo("en-US"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CultureInfo.CurrentCulture })
            if (DateTime.TryParse(s, kultur, System.Globalization.DateTimeStyles.AssumeLocal, out var t)) return t;
        return null;
    }

    public string OsSurumu()
    {
        try
        {
            using var q = new ManagementObjectSearcher("SELECT Version FROM Win32_OperatingSystem"); string v = "";
            foreach (ManagementObject m in q.Get()) using (m) v = S(m, "Version");
            // Güncelleme revizyonu (UBR) kayıt defterinden.
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"); var ubr = k?.GetValue("UBR");
            return v.Length > 0 ? v + (ubr != null ? "." + ubr : "") : Environment.OSVersion.Version.ToString();
        }
        catch { return Environment.OSVersion.Version.ToString(); }
    }

    public string MakineAdi() => Environment.MachineName;
    public List<InstalledSoftware> Yazilimlar() => SoftwareInventoryCollector.Topla();
}
