// Dosya adli inceleyici (olay müdahalesi için): şüpheli bir dosyayı ÇALIŞTIRMADAN inceler.
// Bakılanlar: gerçek tür (başlık baytları) ve uzantı uyumu, boyut ve zaman damgaları, dijital imza (Windows doğrulaması, internetsiz),
// internetten indirilme işareti, SHA-256, rastgelelik (paketlenme ipucu) ve okunabilir metin dizilerindeki şüpheli ipuçları.
// Dosyayı açmaz/çalıştırmaz/silmez/taşımaz; hiçbir şey internete gitmez. Sonuç kesin "virüs" hükmü değil, bir risk değerlendirmesidir.
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using LabDenetim.Moduller;

// Bu dosyanın ait olduğu ad alanı.
namespace LabDenetim;

// Dijital imza durumu.
public enum ImzaDurumu { Bilinmiyor, Yok, Gecerli, Bozuk, GuvenilmezKok, SuresiDolmus, Diger }

// Bir dosyanın inceleme raporu.
public sealed record DosyaRaporu(
    string Yol, string Ad, long Boyut, DateTime Olusma, DateTime Degisme, DateTime Erisim,
    string Tur, ImzaDurumu Imza, string Imzalayan, string Sha256, double Entropi,
    string? Kaynak, DateTime? DerlemeTarihi, int Puan, string Seviye, List<string> Nedenler, List<string> Ipuclari);

// Dosya inceleme mantığı.
public static class DosyaInceleyici
{
    // PE dosyasının Authenticode (WIN_CERTIFICATE → PKCS#7) imzasından imzalayan sertifikayı okur. X509Certificate.CreateFromSignedFile artık kullanılmıyor (SYSLIB0057) ve yerine
    // doğrudan karşılığı yok; bu yüzden imza bloğu PE sertifika tablosundan alınıp SignedCms ile çözülür. Okunamazsa null (imzalayan adı boş kalır; doğrulama ayrıca WinVerifyTrust ile yapılır).
    private static X509Certificate2? ImzaSertifikasi(string yol)
    {
        using var fs = File.OpenRead(yol); using var pe = new System.Reflection.PortableExecutable.PEReader(fs);
        var tablo = pe.PEHeaders.PEHeader?.CertificateTableDirectory; if (tablo == null || tablo.Value.Size < 8) return null;
        var b = new byte[tablo.Value.Size]; fs.Position = tablo.Value.RelativeVirtualAddress; fs.ReadExactly(b);   // sertifika tablosunun RVA'sı dosya konumudur
        int uzunluk = BitConverter.ToInt32(b, 0); if (uzunluk <= 8 || uzunluk > b.Length) return null;
        var cms = new System.Security.Cryptography.Pkcs.SignedCms(); cms.Decode(b.AsSpan(8, uzunluk - 8).ToArray());
        return cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
    }

    // Dizeler ve rastgelelik için okunacak en çok bayt (büyük dosyalar belleği doldurmasın).
    public const int OkumaSiniri = 8 * 1024 * 1024;

    // ---------- Gerçek tür (başlık baytları) ----------

    // İlk baytlara göre dosya türünü söyler (uzantıya bakmaz); bilinmiyorsa "Bilinmeyen".
    public static string TurTahmin(byte[] b)
    {
        // Bayt dizisinin başı belirli bir kalıpla mı başlıyor?
        static bool Bas(byte[] x, params byte[] k) { if (x.Length < k.Length) return false; for (int i = 0; i < k.Length; i++) if (x[i] != k[i]) return false; return true; }
        // Windows programı.
        if (Bas(b, 0x4D, 0x5A)) return "Windows programı (EXE/DLL)";
        // Linux programı.
        if (Bas(b, 0x7F, 0x45, 0x4C, 0x46)) return "Linux programı (ELF)";
        // PDF.
        if (Bas(b, 0x25, 0x50, 0x44, 0x46)) return "PDF belgesi";
        // Zip tabanlı (Office, jar, apk dahil).
        if (Bas(b, 0x50, 0x4B, 0x03, 0x04)) return "ZIP tabanlı arşiv (Office belgesi, jar, apk olabilir)";
        // Eski Office (OLE).
        if (Bas(b, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)) return "Eski Office belgesi (makro içerebilir)";
        // RAR.
        if (Bas(b, 0x52, 0x61, 0x72, 0x21)) return "RAR arşivi";
        // 7-Zip.
        if (Bas(b, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) return "7-Zip arşivi";
        // PNG.
        if (Bas(b, 0x89, 0x50, 0x4E, 0x47)) return "PNG resmi";
        // JPEG.
        if (Bas(b, 0xFF, 0xD8, 0xFF)) return "JPEG resmi";
        // GIF.
        if (Bas(b, 0x47, 0x49, 0x46, 0x38)) return "GIF resmi";
        // Kısayol (.lnk).
        if (Bas(b, 0x4C, 0x00, 0x00, 0x00, 0x01, 0x14, 0x02, 0x00)) return "Windows kısayolu (LNK)";
        // Betik (#!).
        if (Bas(b, 0x23, 0x21)) return "Betik (#! ile başlıyor)";
        // Düz metin gibi mi? (ilk 512 baytta kontrol karakteri yok)
        int n = Math.Min(b.Length, 512); if (n > 0) { bool metin = true; for (int i = 0; i < n; i++) { byte c = b[i]; if (c < 9 || (c > 13 && c < 32) || c == 127) { metin = false; break; } } if (metin) return "Düz metin / betik olabilir"; }
        // Tanınmadı.
        return "Bilinmeyen";
    }

    // Uzantıdan beklenen tür grubu: "belge/resim" (zararsız görünen) mi, "program" mı?
    private static readonly string[] ZararsizUzanti = { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".mp3", ".mp4", ".zip" };
    // Çalıştırılabilir sayılan uzantılar.
    private static readonly string[] CalistirilabilirUzanti = { ".exe", ".dll", ".scr", ".com", ".bat", ".cmd", ".js", ".vbs", ".ps1", ".lnk", ".msi", ".hta", ".jar" };

    // Gerçek tür ile uzantı çelişiyor mu? Çelişkiyi açıklayan metni (yoksa null) verir.
    public static string? UzantiUyumsuzlugu(string ad, string tur)
    {
        // Uzantı.
        string u = Path.GetExtension(ad).ToLowerInvariant();
        // Program ama zararsız uzantılı: gizlenmiş program.
        bool program = tur.StartsWith("Windows programı") || tur.StartsWith("Linux programı");
        if (program && ZararsizUzanti.Contains(u)) return $"Dosya \"{u}\" gibi görünüyor ama gerçekte bir PROGRAM. Zararsız bir belge/resim gibi gösterilmiş olabilir.";
        // Kısayol ama zararsız uzantılı.
        if (tur.StartsWith("Windows kısayolu") && u != ".lnk") return "Dosya gerçekte bir Windows kısayolu; kısayollar komut çalıştırabilir.";
        // Uzantı program ama içerik program değil (örn. .exe adlı ama düz metin): ilginç ama kesin tehlike değil.
        if ((u is ".exe" or ".dll" or ".scr") && !program) return $"Uzantı \"{u}\" ama içerik bir Windows programı gibi görünmüyor (bozuk, boş ya da sahte olabilir).";
        // Uyumlu.
        return null;
    }

    // Çift uzantı ya da sağdan-sola yönlendirme karakteriyle ad gizleme var mı? Açıklama metni verir.
    public static string? AdHilesi(string ad)
    {
        // Sağdan sola geçersiz kılma karakteri (U+202E) dosya adını ters gösterir.
        if (ad.Contains('‮')) return "Dosya adında gizli bir yön değiştirme karakteri (U+202E) var: uzantı gerçekte olduğundan farklı görünür.";
        // "fatura.pdf.exe" gibi çift uzantı.
        if (Regex.IsMatch(ad, @"\.(pdf|docx?|xlsx?|pptx?|jpe?g|png|gif|txt|zip|mp3|mp4)\s*\.(exe|scr|bat|cmd|js|vbs|ps1|lnk|com|hta|jar)$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200))) return "Çift uzantı: ad zararsız bir belge gibi başlıyor ama sonunda çalıştırılabilir uzantı var.";
        // Çok sayıda boşlukla uzantıyı saklama.
        if (Regex.IsMatch(ad, @"\s{5,}\.[a-z0-9]{2,4}$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200))) return "Adın sonunda uzantıyı saklamak için çok sayıda boşluk var.";
        // Sorun yok.
        return null;
    }

    // ---------- PE başlığı ----------

    // Windows programının derleme tarihini okur (sahte/sıfır/gelecek tarihler ipucudur); okunamazsa null.
    public static DateTime? DerlemeTarihi(byte[] b)
    {
        // Başlık yeterince uzun ve MZ ile başlıyor mu?
        if (b.Length < 0x40 || b[0] != 0x4D || b[1] != 0x5A) return null;
        // PE başlığının yeri.
        int pe = BitConverter.ToInt32(b, 0x3C); if (pe < 0 || pe + 12 > b.Length) return null;
        // "PE\0\0" imzası.
        if (b[pe] != 0x50 || b[pe + 1] != 0x45 || b[pe + 2] != 0 || b[pe + 3] != 0) return null;
        // Zaman damgası (1970'ten beri saniye).
        uint ts = BitConverter.ToUInt32(b, pe + 8); if (ts == 0) return null;
        // Tarihe çevir.
        return DateTime.UnixEpoch.AddSeconds(ts);
    }

    // ---------- Rastgelelik (entropi) ----------

    // Baytların Shannon entropisi (0–8). Sıkıştırılmış/şifrelenmiş/paketlenmiş içerik 7'nin üstünde olur.
    public static double Entropi(byte[] b, int uzunluk = -1)
    {
        // Ölçülecek bayt sayısı.
        int n = uzunluk < 0 ? b.Length : Math.Min(uzunluk, b.Length); if (n == 0) return 0;
        // Bayt sayacı.
        var say = new long[256]; for (int i = 0; i < n; i++) say[b[i]]++;
        // Shannon toplamı.
        double e = 0; foreach (var s in say) if (s > 0) { double p = (double)s / n; e -= p * Math.Log2(p); }
        // Sonuç.
        return e;
    }

    // ---------- Okunabilir metin dizileri ----------

    // Baytlardan en az 6 karakterlik okunabilir ASCII ve UTF-16 dizeleri çıkarır (en çok sinir adet).
    public static List<string> Dizeler(byte[] b, int sinir = 20000)
    {
        // Sonuç listesi.
        var l = new List<string>(); var sb = new StringBuilder();
        // Yazdırılabilir ASCII mi?
        static bool Y(byte c) => c >= 0x20 && c <= 0x7E;
        // 1) ASCII dizeleri.
        for (int i = 0; i <= b.Length && l.Count < sinir; i++)
        {
            // Uzun bir dizeyi 400 karakterde kes (düzenli ifadeler yavaşlamasın).
            if (i < b.Length && Y(b[i])) { if (sb.Length < 400) sb.Append((char)b[i]); }
            else { if (sb.Length >= 6) l.Add(sb.ToString()); sb.Clear(); }
        }
        // 2) UTF-16 (iki baytta bir sıfır) dizeleri.
        for (int i = 0; i + 1 <= b.Length && l.Count < sinir; i += 1)
        {
            // Başlangıç: yazdırılabilir bayt + sıfır.
            if (i + 1 < b.Length && Y(b[i]) && b[i + 1] == 0)
            {
                // Dizeyi ilerlet.
                int j = i; sb.Clear(); while (j + 1 < b.Length && Y(b[j]) && b[j + 1] == 0 && sb.Length < 400) { sb.Append((char)b[j]); j += 2; }
                // Yeterince uzunsa ekle.
                if (sb.Length >= 6) l.Add(sb.ToString()); i = Math.Max(i, j - 1);
            }
        }
        // Sonuç.
        return l;
    }

    // Şüpheli ipucu kalıpları: kategori, düzenli ifade, puan. Hepsi yalnızca METİN eşleştirmesidir, bir şey çalıştırılmaz.
    private static readonly (string Kategori, Regex Kalip, int Puan)[] Kaliplar =
    {
        ("Gizlenmiş PowerShell komutu", new(@"powershell.{0,40}(-enc|-encodedcommand|-nop|-w\s+hidden)|frombase64string", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 15),
        ("İnternetten kod indirip çalıştırma", new(@"downloadstring|downloadfile|urldownloadtofile|invoke-expression|\biex\b|certutil.{0,20}urlcache|bitsadmin.{0,20}/transfer", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 15),
        ("Başka işleme kod enjekte etme", new(@"virtualallocex|writeprocessmemory|createremotethread|ntunmapviewofsection|queueuserapc", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 20),
        ("Tuş kaydı / ekran yakalama", new(@"getasynckeystate|setwindowshookex|getkeyboardstate|bitblt.{0,30}getdc", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 15),
        ("Kalıcılık (açılışta çalışma)", new(@"currentversion\\run|schtasks\s+/create|winlogon\\(shell|userinit)|startup\\", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 10),
        ("Yedekleri/kurtarmayı silme", new(@"vssadmin.{0,20}delete\s+shadows|wbadmin\s+delete|bcdedit.{0,40}(recoveryenabled|ignoreallfailures)|wmic.{0,20}shadowcopy.{0,10}delete", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 30),
        ("Parola/kimlik bilgisi çalma izi", new(@"mimikatz|sekurlsa|lsass\.exe|logonpasswords|credential\s*dump", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 25),
        ("Analizden kaçınma (hata ayıklayıcı/sanal makine denetimi)", new(@"isdebuggerpresent|checkremotedebuggerpresent|vboxservice|vmware\s*tools|sbiedll", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 8),
        ("Tor / .onion adresi", new(@"[a-z2-7]{16,56}\.onion", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 20),
        ("Fidye notu benzeri ifade", new(@"your files (have been|are) encrypted|dosyalarınız şifrelen|decrypt.{0,30}bitcoin|bitcoin.{0,40}(wallet|address)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)), 30),
    };
    // URL ve IP ipuçları (bilgi amaçlı; puan vermez).
    private static readonly Regex UrlKalibi = new(@"https?://[^\s""'<>]{4,120}", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
    private static readonly Regex IpKalibi = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.None, TimeSpan.FromMilliseconds(200));

    // Dizelerde şüpheli ipuçlarını arar: (kategori → örnekler) ve toplam puanı verir. Her kategori bir kez puan alır.
    public static (List<string> Ipuclari, int Puan) SupheliDizeler(IEnumerable<string> dizeler)
    {
        // Kategori başına örnekler, URL/IP kümeleri.
        var bulunan = new Dictionary<string, List<string>>(); var urller = new HashSet<string>(); var ipler = new HashSet<string>();
        // Her dizeyi tara.
        foreach (var d in dizeler)
        {
            // Çok uzun dizeyi kısalt.
            string s = d.Length > 400 ? d[..400] : d;
            // Kategoriler.
            foreach (var (kat, kalip, _) in Kaliplar)
                // Eşleşme zaman aşımı olursa atla.
                try { if (kalip.IsMatch(s)) { if (!bulunan.TryGetValue(kat, out var o)) bulunan[kat] = o = new(); if (o.Count < 2) o.Add(s.Length > 80 ? s[..80] + "…" : s); } } catch (RegexMatchTimeoutException hk1) { HataKayit.Sessiz(hk1, "DosyaInceleyici.SupheliDizeler"); }
            // URL ve IP topla.
            try { foreach (Match m in UrlKalibi.Matches(s)) if (urller.Count < 12) urller.Add(m.Value); foreach (Match m in IpKalibi.Matches(s)) if (ipler.Count < 12 && IpGecerli(m.Value)) ipler.Add(m.Value); } catch (RegexMatchTimeoutException hk2) { HataKayit.Sessiz(hk2, "DosyaInceleyici.SupheliDizeler"); }
        }
        // Çıktı satırları ve puan.
        var ipuc = new List<string>(); int puan = 0;
        // Kategori sonuçları.
        foreach (var (kat, _, p) in Kaliplar) if (bulunan.TryGetValue(kat, out var ornek)) { puan += p; ipuc.Add($"{kat}: " + string.Join(" | ", ornek)); }
        // URL ve IP bilgileri.
        if (urller.Count > 0) ipuc.Add("İçinde geçen web adresleri: " + string.Join(", ", urller.Take(6)) + (urller.Count > 6 ? " …" : ""));
        if (ipler.Count > 0) ipuc.Add("İçinde geçen IP adresleri: " + string.Join(", ", ipler.Take(6)) + (ipler.Count > 6 ? " …" : ""));
        // Sonuç.
        return (ipuc, Math.Min(puan, 60));
    }

    // IP'nin her parçası 0–255 mi (sürüm numarası gibi 1.2.3.4 yanlış alarmı bilgi düzeyindedir)?
    private static bool IpGecerli(string ip) => ip.Split('.').All(p => int.TryParse(p, out int n) && n <= 255) && ip != "0.0.0.0";

    // ---------- İnternetten indirilme işareti ----------

    // "Zone.Identifier" içeriğinden kaynağı özetler; işaret yoksa null. ZoneId 3 = İnternet.
    public static string? KaynakOzeti(string? zoneMetni)
    {
        // İşaret yok.
        if (string.IsNullOrWhiteSpace(zoneMetni)) return null;
        // Alanları oku.
        string? Al(string anahtar) { foreach (var satir in zoneMetni.Split('\n')) { var t = satir.Trim(); if (t.StartsWith(anahtar + "=", StringComparison.OrdinalIgnoreCase)) return t[(anahtar.Length + 1)..].Trim(); } return null; }
        // Bölge numarası.
        string bolge = Al("ZoneId") switch { "0" => "Bu bilgisayar", "1" => "Yerel ağ", "2" => "Güvenilen site", "3" => "İnternet", "4" => "Kısıtlı site", _ => "Bilinmeyen bölge" };
        // Nereden indirildi (varsa).
        string? host = Al("HostUrl") ?? Al("ReferrerUrl");
        // Özet.
        return $"{bolge}'nden geldi" + (string.IsNullOrEmpty(host) || host == "about:internet" ? "" : $" ({(host.Length > 100 ? host[..100] + "…" : host)})");
    }

    // Dosyanın "Zone.Identifier" akışını okur (yoksa null).
    private static string? ZoneOku(string yol) { try { return File.ReadAllText(yol + ":Zone.Identifier"); } catch { return null; } }

    // ---------- Dijital imza (Windows WinVerifyTrust, internetsiz) ----------

    // WinVerifyTrust için yapılar.
    [StructLayout(LayoutKind.Sequential)] private struct WtFile { public uint Boyut; public IntPtr Yol; public IntPtr Dosya; public IntPtr Konu; }
    [StructLayout(LayoutKind.Sequential)] private struct WtData { public uint Boyut; public IntPtr Politika; public IntPtr Istemci; public uint Arayuz; public uint Iptal; public uint Birlesim; public IntPtr Dosya; public uint Eylem; public IntPtr Durum; public IntPtr Url; public uint Bayrak; public uint Baglam; public IntPtr Ayar; }
    // Windows'un imza doğrulama işlevi.
    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)] private static extern int WinVerifyTrust(IntPtr pencere, [MarshalAs(UnmanagedType.LPStruct)] Guid eylem, ref WtData veri);

    // Windows sonuç kodunu sade duruma çevirir (saf; test edilebilir).
    public static ImzaDurumu ImzaKodu(int kod) => (uint)kod switch
    {
        0x00000000 => ImzaDurumu.Gecerli,
        0x800B0100 => ImzaDurumu.Yok,
        // Biçim tanınmadı (örn. bozuk/sahte program başlığı): imza okunamaz, "yok" sayılır.
        0x800B0003 => ImzaDurumu.Yok,
        0x80096010 => ImzaDurumu.Bozuk,
        0x80096011 => ImzaDurumu.Bozuk,
        0x800B0109 => ImzaDurumu.GuvenilmezKok,
        0x800B010A => ImzaDurumu.GuvenilmezKok,
        0x800B0101 => ImzaDurumu.SuresiDolmus,
        0x800B0004 => ImzaDurumu.Diger,
        _ => ImzaDurumu.Diger
    };

    // Dosyanın gömülü dijital imzasını doğrular (iptal listesi/ağ sorgusu YAPMAZ). Dosyayı çalıştırmaz.
    public static (ImzaDurumu Durum, string Imzalayan) ImzaKontrol(string yol)
    {
        // Yalnızca Windows'ta.
        if (!OperatingSystem.IsWindows()) return (ImzaDurumu.Bilinmiyor, "");
        // Yol bellek işaretçisi olarak hazırlanır.
        IntPtr yolPtr = Marshal.StringToHGlobalUni(yol); IntPtr dosyaPtr = IntPtr.Zero;
        try
        {
            // Dosya bilgisi yapısı.
            var wf = new WtFile { Yol = yolPtr }; wf.Boyut = (uint)Marshal.SizeOf<WtFile>(); dosyaPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WtFile>()); Marshal.StructureToPtr(wf, dosyaPtr, false);
            // Doğrulama verisi: arayüz yok (2), iptal denetimi yok (0), dosya birleşimi (1), yalnızca önbellekten URL (0x1000) + iptal yok (0x10).
            var wd = new WtData { Arayuz = 2, Iptal = 0, Birlesim = 1, Dosya = dosyaPtr, Eylem = 0, Bayrak = 0x10 | 0x1000 }; wd.Boyut = (uint)Marshal.SizeOf<WtData>();
            // Genel doğrulama eylemi kimliği.
            int kod = WinVerifyTrust(new IntPtr(-1), new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"), ref wd);
            // Durum.
            var durum = ImzaKodu(kod);
            // İmzalayanı oku (yalnızca imza varsa).
            string imzalayan = ""; if (durum != ImzaDurumu.Yok) { try { using var c = ImzaSertifikasi(yol) ?? throw new InvalidDataException("İmza sertifikası okunamadı."); imzalayan =c.GetNameInfo(X509NameType.SimpleName, false) ?? ""; var o = Regex.Match(c.Subject, @"(?:^|,\s*)O=([^,]+)"); if (o.Success && !imzalayan.Contains(o.Groups[1].Value.Trim(), StringComparison.OrdinalIgnoreCase)) imzalayan += $" ({o.Groups[1].Value.Trim()})"; } catch (Exception hk3) { HataKayit.Sessiz(hk3, "DosyaInceleyici.ImzaKontrol"); } }
            // Sonuç.
            return (durum, imzalayan);
        }
        // Beklenmeyen hata: bilinmiyor.
        catch { return (ImzaDurumu.Bilinmiyor, ""); }
        // Bellek temizliği.
        finally { Marshal.FreeHGlobal(yolPtr); if (dosyaPtr != IntPtr.Zero) Marshal.FreeHGlobal(dosyaPtr); }
    }

    // ---------- Değerlendirme ----------

    // Toplanan ipuçlarından puan, seviye ve nedenler üretir (saf mantık; test edilebilir).
    public static (int Puan, string Seviye, List<string> Nedenler) Degerlendir(string ad, string tur, ImzaDurumu imza, double entropi, string? kaynak, DateTime? derleme, DateTime degisme, int dizePuani, DateTime simdi)
    {
        // Nedenler ve puan.
        var n = new List<string>(); int p = 0;
        // Program mı?
        bool program = tur.StartsWith("Windows programı");
        // Uzantı çelişkisi.
        var u = UzantiUyumsuzlugu(ad, tur); if (u != null) { n.Add(u); p += tur.StartsWith("Windows programı") || tur.StartsWith("Linux programı") || tur.StartsWith("Windows kısayolu") ? 35 : 5; }
        // Ad hilesi.
        var h = AdHilesi(ad); if (h != null) { n.Add(h); p += 30; }
        // İmza.
        if (program)
            switch (imza)
            {
                case ImzaDurumu.Bozuk: n.Add("Dijital imza BOZUK: dosya imzalandıktan sonra değiştirilmiş olabilir."); p += 50; break;
                case ImzaDurumu.GuvenilmezKok: n.Add("İmza var ama güvenilen bir kök makamdan gelmiyor (kendi kendine imzalı olabilir)."); p += 20; break;
                case ImzaDurumu.SuresiDolmus: n.Add("İmza sertifikasının süresi dolmuş (eski dosyalarda normal olabilir)."); p += 5; break;
                case ImzaDurumu.Yok: n.Add("Dijital imza yok (Windows'un kendi dosyaları katalogla imzalı olduğu için burada \"yok\" görünebilir)."); p += 10; break;
                case ImzaDurumu.Gecerli: n.Add("Dijital imza geçerli."); p -= 10; break;
            }
        // Paketlenmiş/şifreli içerik ipucu.
        if (program && entropi >= 7.2) { n.Add($"İçerik çok rastgele (entropi {entropi:0.0}/8): program paketlenmiş veya şifrelenmiş olabilir; kötü amaçlı yazılımlar sık yapar, bazı meşru koruyucular da yapar."); p += 15; }
        // İnternetten indirilmiş.
        if (kaynak != null && kaynak.StartsWith("İnternet")) { n.Add("İnternetten indirilmiş: " + kaynak); p += program ? 5 : 0; }
        // Derleme tarihi garipliği.
        if (derleme is DateTime d) { if (d > simdi.AddDays(1)) { n.Add($"Programın derleme tarihi gelecekte ({d:yyyy-MM-dd}): tarih sahte olabilir."); p += 10; } else if (d.Year < 1995) { n.Add($"Programın derleme tarihi anlamsız ({d:yyyy-MM-dd}): sahte olabilir."); p += 5; } }
        // Değişme tarihi gelecekte.
        if (degisme > simdi.AddDays(1)) { n.Add("Dosyanın değişme tarihi gelecekte: zaman damgası oynanmış olabilir."); p += 10; }
        // Dize ipuçları.
        if (dizePuani > 0) { n.Add("İçindeki metinlerde şüpheli ifadeler bulundu (aşağıdaki ipuçlarına bakın)."); p += dizePuani; }
        // Sınırla.
        p = Math.Clamp(p, 0, 100);
        // Seviye.
        string seviye = p >= 60 ? "Yüksek şüphe" : p >= 30 ? "Orta şüphe" : p >= 10 ? "Düşük şüphe" : "Belirgin şüphe yok";
        // Sonuç.
        return (p, seviye, n);
    }

    // ---------- Ana işlem ----------

    // Bir dosyayı inceler. Dosyayı yalnızca OKUR; çalıştırmaz, değiştirmez.
    public static async Task<DosyaRaporu> InceleAsync(string yol, CancellationToken ct = default)
    {
        // Dosya bilgisi.
        var fi = new FileInfo(yol); if (!fi.Exists) throw new FileNotFoundException("Dosya bulunamadı.", yol);
        // Baştan en çok OkumaSiniri bayt oku (diğer program açık tutuyorsa da okunur).
        byte[] veri; using (var fs = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true)) { int n = (int)Math.Min(fs.Length, OkumaSiniri); veri = new byte[n]; int oku = 0; while (oku < n) { int r = await fs.ReadAsync(veri.AsMemory(oku, n - oku), ct); if (r == 0) break; oku += r; } if (oku < n) Array.Resize(ref veri, oku); }
        // Gerçek tür, entropi, dizeler.
        string tur = TurTahmin(veri); double ent = Entropi(veri); var (ipucu, dizePuani) = SupheliDizeler(Dizeler(veri));
        // Özet (SHA-256).
        var oz = await HashAraci.DosyaOzetleriAsync(yol, new[] { HashTuru.SHA256 }, null, ct); string sha = oz[HashTuru.SHA256];
        // Dijital imza yalnızca programlarda anlamlı.
        var (imza, imzalayan) = tur.StartsWith("Windows programı") ? ImzaKontrol(yol) : (ImzaDurumu.Bilinmiyor, "");
        // Kaynak ve derleme tarihi.
        string? kaynak = KaynakOzeti(ZoneOku(yol)); DateTime? derleme = DerlemeTarihi(veri);
        // Değerlendir.
        var (puan, seviye, nedenler) = Degerlendir(fi.Name, tur, imza, ent, kaynak, derleme, fi.LastWriteTime, dizePuani, DateTime.Now);
        // Rapor.
        return new DosyaRaporu(fi.FullName, fi.Name, fi.Length, fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime, tur, imza, imzalayan, sha, ent, kaynak, derleme, puan, seviye, nedenler, ipucu);
    }

    // İmza durumunun sade Türkçesi.
    public static string ImzaMetni(ImzaDurumu d) => d switch { ImzaDurumu.Gecerli => "Geçerli", ImzaDurumu.Yok => "Yok", ImzaDurumu.Bozuk => "BOZUK", ImzaDurumu.GuvenilmezKok => "Güvenilmeyen makam", ImzaDurumu.SuresiDolmus => "Süresi dolmuş", ImzaDurumu.Diger => "Doğrulanamadı", _ => "Uygulanmaz" };

    // Raporu okunabilir metne çevirir (ekranda gösterilir, dosyaya kaydedilebilir).
    public static string Metin(DosyaRaporu r)
    {
        // Metin kurucu.
        var sb = new StringBuilder();
        // Başlık.
        sb.AppendLine($"DOSYA İNCELEME RAPORU  ({DateTime.Now:yyyy-MM-dd HH:mm})");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine($"SONUÇ: {r.Seviye}  (puan {r.Puan}/100)");
        sb.AppendLine("Not: Bu bir kesin virüs hükmü değil, risk değerlendirmesidir. Dosya çalıştırılmadı.");
        sb.AppendLine();
        // Temel bilgiler.
        sb.AppendLine($"Ad: {r.Ad}"); sb.AppendLine($"Yol: {r.Yol}"); sb.AppendLine($"Boyut: {r.Boyut:N0} bayt");
        sb.AppendLine($"Gerçek tür: {r.Tur}");
        sb.AppendLine($"SHA-256: {r.Sha256}");
        sb.AppendLine($"Oluşturulma: {r.Olusma:yyyy-MM-dd HH:mm:ss}   Değişme: {r.Degisme:yyyy-MM-dd HH:mm:ss}   Son erişim: {r.Erisim:yyyy-MM-dd HH:mm:ss}");
        if (r.DerlemeTarihi != null) sb.AppendLine($"Programın derleme tarihi: {r.DerlemeTarihi:yyyy-MM-dd HH:mm:ss} (UTC)");
        sb.AppendLine($"Dijital imza: {ImzaMetni(r.Imza)}" + (string.IsNullOrEmpty(r.Imzalayan) ? "" : $" — {r.Imzalayan}"));
        sb.AppendLine($"Rastgelelik (entropi): {r.Entropi:0.00} / 8");
        sb.AppendLine($"Kaynak: {r.Kaynak ?? "İnternetten indirildiğine dair işaret yok"}");
        // Nedenler.
        sb.AppendLine(); sb.AppendLine("Değerlendirme nedenleri:");
        if (r.Nedenler.Count == 0) sb.AppendLine("  • Dikkat çeken bir neden bulunmadı.");
        foreach (var n in r.Nedenler) sb.AppendLine("  • " + n);
        // İpuçları.
        sb.AppendLine(); sb.AppendLine("İçerik ipuçları (okunabilir metinlerden):");
        if (r.Ipuclari.Count == 0) sb.AppendLine("  • Şüpheli ipucu bulunmadı.");
        foreach (var i in r.Ipuclari) sb.AppendLine("  • " + i);
        // Önerilen adımlar.
        sb.AppendLine(); sb.AppendLine("Önerilen adımlar:");
        if (r.Puan >= 30) { sb.AppendLine("  1. Dosyayı AÇMAYIN/ÇALIŞTIRMAYIN; ağdan ayrılmış bir yere taşıyıp kanıt olarak saklayın."); sb.AppendLine("  2. SHA-256 değerini kurumunuzun güvenlik ekibiyle veya bir kötü amaçlı yazılım veritabanında (kendiniz karar vererek) sorgulatın."); sb.AppendLine("  3. Dosya çalıştırıldıysa bilgisayarı ağdan ayırıp Sistem ve Kalıcılık denetimlerini çalıştırın; parolaları temiz bir cihazdan değiştirin."); }
        else sb.AppendLine("  • Belirgin bir şüphe yok; yine de kaynağını tanımıyorsanız çalıştırmayın ve resmî kaynakla SHA-256 değerini karşılaştırın.");
        // Sonuç.
        return sb.ToString();
    }
}
