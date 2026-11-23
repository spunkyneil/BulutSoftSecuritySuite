// Bu dosya BU BİLGİSAYARIN içini inceleyen üç YALNIZCA-OKUYAN kontrolü içerir:
//  1) Kalıcılık taraması: bilgisayar her açıldığında kendiliğinden çalışan programlar (saldırganların geri dönme yolu),
//  2) Windows güvenlik karnesi: virüs koruması, güvenlik duvarı, UAC, uzak masaüstü, eski SMB, yama yaşı,
//  3) Yerel hesap denetimi: Guest, parolasız hesaplar, yönetici grubundaki kişiler.
// Hiçbir ayarı değiştirmez, hiçbir şeyi silmez veya çalıştırmaz; internete de çıkmaz.
using System.Management;
using Microsoft.Win32;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// Sistem içi (yerel) denetimler.
public static class SistemDenetimi
{
    // Üç kontrolü art arda çalıştırıp tüm bulguları verir. Her kontrol kendi hatasını yutar (biri çökerse diğerleri sürer).
    public static List<Bulgu> Hepsi()
    {
        // Sonuç listesi.
        var liste = new List<Bulgu>();
        // Kalıcılık taraması.
        liste.AddRange(Guvenli("Kalıcılık", KaliciligiTara));
        // Güvenlik karnesi.
        liste.AddRange(Guvenli("Karne", GuvenlikKarnesi));
        // Wi-Fi güvenliği (bağlı değilse boş döner).
        liste.AddRange(Guvenli("Wi-Fi", WifiDenetle));
        // Hesap denetimi.
        liste.AddRange(Guvenli("Hesap", HesapDenetle));
        // Ağ paylaşımları (yalnızca okuma).
        liste.AddRange(Guvenli("Paylaşım", PaylasimDenetimi.Denetle));
        // Sonucu ver.
        return liste;
    }

    // Bir kontrolü hata yakalayarak çalıştırır; hata olursa "yapılamadı" notu döner.
    private static List<Bulgu> Guvenli(string modul, Func<List<Bulgu>> islem)
    {
        // Hata kontrolü.
        try { return islem(); }
        // Çalışamadıysa nedenini not olarak yaz.
        catch (Exception ex) { return new() { new Bulgu(modul, "Bu kontrol çalıştırılamadı", Onem.Bilgi, ex.Message, "Tekrar deneyin; sorun sürerse programı yönetici olarak çalıştırmayı deneyin.") }; }
    }

    // ==================== 1) KALICILIK TARAMASI ====================

    // Başlangıçta çalışan bir öğe: nereden geldiği, adı ve çalıştırdığı komut.
    private sealed record Oge(string Kaynak, string Ad, string Komut);

    // Çok tehlikeli sayılan komut parçaları (gizlenmiş/indirilen kod çalıştırma teknikleri).
    private static readonly string[] Tehlikeli = { "-enc ", "-encodedcommand", "frombase64string", "mshta", "wscript", "cscript", "bitsadmin", "certutil -urlcache", "downloadstring", "invoke-expression", "iex(", "iex (", "rundll32 javascript", "regsvr32 /s /n /u /i:http" };
    // Genelde sıradan programların yer almaması gereken klasörler.
    private static readonly string[] SupheliKlasor = { "\\temp\\", "\\users\\public\\", "\\downloads\\", "\\appdata\\local\\temp" };
    // Bazı meşru programların da kullandığı ama dikkat edilmesi gereken klasörler.
    private static readonly string[] DikkatKlasor = { "\\appdata\\roaming\\", "\\appdata\\local\\", "\\programdata\\" };

    // Başlangıç öğelerini toplar ve riskli olanları işaretler.
    public static List<Bulgu> KaliciligiTara()
    {
        // Tüm öğeler.
        var ogeler = new List<Oge>();
        // Kayıt defteri Run/RunOnce anahtarları.
        foreach (var (kok, ad) in new[] { (Registry.CurrentUser, "HKCU"), (Registry.LocalMachine, "HKLM") })
            foreach (var yol in new[] { @"Software\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run" })
                // Anahtar yoksa veya okunamazsa geç.
                try { using var k = kok.OpenSubKey(yol); if (k != null) foreach (var d in k.GetValueNames()) ogeler.Add(new Oge($"Kayıt defteri Run ({ad})", d, k.GetValue(d)?.ToString() ?? "")); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SistemDenetimi.KaliciligiTara"); }
        // Başlangıç klasörleri (kullanıcı ve herkes).
        foreach (var kl in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
            // Klasör okunamazsa geç.
            try { if (Directory.Exists(kl)) foreach (var f in Directory.GetFiles(kl).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase))) ogeler.Add(new Oge("Başlangıç klasörü", Path.GetFileName(f), f)); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "SistemDenetimi.KaliciligiTara"); }
        // Zamanlanmış görevler (Microsoft'un kendi klasörü hariç).
        try { ogeler.AddRange(GorevleriOku()); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "SistemDenetimi.KaliciligiTara"); }
        // Otomatik başlayan hizmetler.
        try { ogeler.AddRange(HizmetleriOku()); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "SistemDenetimi.KaliciligiTara"); }

        // Risk gruplarına ayır.
        var yuksek = new List<string>(); var orta = new List<string>(); var dikkat = new List<string>(); int temiz = 0;
        // Her öğeyi değerlendir.
        foreach (var o in ogeler)
        {
            // Küçük harfe çevrilmiş, ortam değişkenleri açılmış komut.
            string k = Environment.ExpandEnvironmentVariables(o.Komut).ToLowerInvariant();
            // Gösterim satırı.
            string satir = $"[{o.Kaynak}] {o.Ad} → {Kisalt(o.Komut)}";
            // Tehlikeli komut tekniği.
            if (Tehlikeli.Any(t => k.Contains(t))) yuksek.Add(satir);
            // Şüpheli klasörden çalışıyor.
            else if (SupheliKlasor.Any(t => k.Contains(t))) orta.Add(satir);
            // Kullanıcı verisi klasörlerinden çalışan (çoğu meşru: Teams, Discord…) ama dikkat edilmeli; Microsoft/Windows yolları hariç.
            else if (DikkatKlasor.Any(t => k.Contains(t)) && !k.Contains("\\microsoft\\") ) dikkat.Add(satir);
            // Sorunsuz.
            else temiz++;
        }
        // Sonuçlar.
        var sonuc = new List<Bulgu>();
        // En fazla 8 satır göster.
        static string Ozet(List<string> l) => string.Join("\n", l.Take(8).Select(x => "  • " + x)) + (l.Count > 8 ? $"\n  … ve {l.Count - 8} öğe daha" : "");
        // Yüksek risk.
        if (yuksek.Count > 0) sonuc.Add(new Bulgu("Kalıcılık", "Başlangıçta çalışan gizlenmiş/indirilen kod şüphesi", Onem.Yuksek, "Bilgisayar açılırken şu öğeler gizlenmiş veya internetten indirilmiş kod çalıştırma teknikleri içeriyor:\n" + Ozet(yuksek), "Bu öğeleri tanımıyorsanız hemen devre dışı bırakın ve virüs taraması yapın; parolalarınızı değiştirmeyi düşünün."));
        // Orta risk.
        if (orta.Count > 0) sonuc.Add(new Bulgu("Kalıcılık", "Başlangıçta geçici/ortak klasörden çalışan program", Onem.Orta, "Şu öğeler normalde program bulunmaması gereken klasörlerden (Temp, Downloads, Public) başlıyor:\n" + Ozet(orta), "Tanımıyorsanız kaldırın. Meşru programlar genellikle Program Files'ta durur."));
        // Dikkat.
        if (dikkat.Count > 0) sonuc.Add(new Bulgu("Kalıcılık", "Başlangıçta kullanıcı veri klasöründen çalışan program", Onem.Dusuk, "Şu öğeler AppData/ProgramData içinden başlıyor (Teams, Discord, Spotify gibi bilinen uygulamalar için normaldir):\n" + Ozet(dikkat), "Hepsini tanıyorsanız sorun yok; tanımadığınız varsa araştırın."));
        // Hiç şüpheli yoksa bilgi notu.
        if (yuksek.Count + orta.Count + dikkat.Count == 0) sonuc.Add(new Bulgu("Kalıcılık", "Başlangıç öğeleri temiz", Onem.Bilgi, $"{ogeler.Count} başlangıç öğesi incelendi (Run anahtarları, başlangıç klasörü, görevler, hizmetler); şüpheli bulunmadı.", "Yapılacak bir şey yok."));
        // Sonucu ver.
        return sonuc;
    }

    // Komutu 90 karaktere kısaltır.
    private static string Kisalt(string s) => s.Length > 90 ? s[..90] + "…" : s;

    // Zamanlanmış görevleri Windows Görev Zamanlayıcısı'ndan okur (yalnızca okuma).
    private static List<Oge> GorevleriOku()
    {
        // Sonuç listesi.
        var l = new List<Oge>();
        // Bileşeni bul.
        var tip = Type.GetTypeFromProgID("Schedule.Service"); if (tip == null) return l;
        // Bağlan.
        dynamic svc = Activator.CreateInstance(tip)!; svc.Connect();
        // Klasörleri gez (özyinelemeli).
        void Gez(dynamic klasor)
        {
            // Görevler.
            foreach (dynamic g in klasor.GetTasks(0))
            {
                // Pasif görevleri atla.
                if (!(bool)g.Enabled) continue;
                // Görevin eylemleri.
                foreach (dynamic a in g.Definition.Actions)
                    // Yalnızca program çalıştıran eylemler (Type 0).
                    if ((int)a.Type == 0) l.Add(new Oge("Zamanlanmış görev", (string)g.Path, $"{a.Path} {a.Arguments}".Trim()));
            }
            // Alt klasörler (Microsoft'unkini atla).
            foreach (dynamic alt in klasor.GetFolders(0)) { if (!((string)alt.Path).StartsWith("\\Microsoft", StringComparison.OrdinalIgnoreCase)) Gez(alt); }
        }
        // Kökten başla.
        Gez(svc.GetFolder("\\"));
        // Listeyi ver.
        return l;
    }

    // Otomatik başlayan, Windows klasörü dışından çalışan hizmetleri okur.
    private static List<Oge> HizmetleriOku()
    {
        // Sonuç listesi.
        var l = new List<Oge>();
        // Hizmetlerin kayıt defteri anahtarı.
        using var kok = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"); if (kok == null) return l;
        // Windows klasörü ve Program Files (tanıdık yerler).
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant();
        // Her hizmet.
        foreach (var ad in kok.GetSubKeyNames())
            // Okunamayanı geç.
            try
            {
                // Hizmet anahtarı.
                using var h = kok.OpenSubKey(ad); if (h == null) continue;
                // Otomatik başlıyor mu (Start=2) ve gerçek bir hizmet mi (Type 16/32)?
                if (h.GetValue("Start") is not int s || s != 2 || h.GetValue("Type") is not int t || (t != 16 && t != 32)) continue;
                // Çalıştırdığı dosya.
                var yol = h.GetValue("ImagePath")?.ToString() ?? ""; if (yol.Length == 0) continue;
                // Windows klasöründeki hizmetler (svchost vb.) normaldir; atla.
                if (Environment.ExpandEnvironmentVariables(yol).Contains(win, StringComparison.OrdinalIgnoreCase)) continue;
                // Listeye ekle.
                l.Add(new Oge("Hizmet", ad, yol));
            }
            catch (Exception hk5) { HataKayit.Sessiz(hk5, "SistemDenetimi.HizmetleriOku"); }
        // Listeyi ver.
        return l;
    }

    // ==================== 2) WINDOWS GÜVENLİK KARNESİ ====================

    // Kayıt defterinden bir sayı okur (yoksa null).
    private static int? Sayi(RegistryKey kok, string yol, string ad) { try { using var k = kok.OpenSubKey(yol); return k?.GetValue(ad) is int i ? i : null; } catch { return null; } }

    // Temel Windows korumalarının durumunu denetler.
    public static List<Bulgu> GuvenlikKarnesi()
    {
        // Sonuç listesi.
        var l = new List<Bulgu>(); int kontrol = 0;
        // Defender ve üçüncü parti virüs koruması.
        kontrol++;
        try
        {
            // Defender durumu.
            using var ara = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus");
            // İlk (tek) kayıt.
            var d = ara.Get().Cast<ManagementObject>().FirstOrDefault();
            // Başka virüs koruması var mı?
            string baska = "";
            // Güvenlik Merkezi'nden ürünler.
            try { using var s2 = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT displayName FROM AntiVirusProduct"); baska = string.Join(", ", s2.Get().Cast<ManagementObject>().Select(x => x["displayName"]?.ToString() ?? "").Where(x => x.Length > 0 && !x.Contains("Defender", StringComparison.OrdinalIgnoreCase))); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "SistemDenetimi.GuvenlikKarnesi"); }
            // Defender okunamadıysa.
            if (d == null) l.Add(new Bulgu("Karne", "Virüs koruması durumu okunamadı", Onem.Bilgi, "Windows Defender bilgisi alınamadı.", "Windows Güvenliği uygulamasından virüs korumasının açık olduğunu kontrol edin."));
            // Gerçek zamanlı koruma kapalı.
            else if (!(bool)d["RealTimeProtectionEnabled"]) l.Add(baska.Length > 0
                ? new Bulgu("Karne", "Defender kapalı (başka virüs koruması var)", Onem.Bilgi, "Başka bir koruma yazılımı görünüyor: " + baska, "Yapılacak bir şey yok; o yazılımın açık ve güncel olduğundan emin olun.")
                : new Bulgu("Karne", "Virüs koruması (Defender) gerçek zamanlı kapalı", Onem.Yuksek, "Hiçbir virüs koruması anlık çalışmıyor.", "Windows Güvenliği > Virüs ve tehdit koruması > Gerçek zamanlı korumayı açın."));
            // İmzalar eski.
            else if (d["AntivirusSignatureAge"] is uint yas && yas > 7) l.Add(new Bulgu("Karne", $"Virüs tanımları {yas} gün eski", Onem.Dusuk, "Koruma açık ama tanım dosyaları güncel değil.", "Windows Güncelleme'den güncellemeleri denetleyin."));
            // Sorun yok.
            else l.Add(new Bulgu("Karne", "Virüs koruması açık ve güncel", Onem.Bilgi, "Defender gerçek zamanlı koruma açık.", "Yapılacak bir şey yok."));
        }
        // WMI erişilemediyse atla.
        catch (Exception hk7) { HataKayit.Sessiz(hk7, "SistemDenetimi.GuvenlikKarnesi"); }
        // Güvenlik duvarı profilleri.
        foreach (var (ad, yol) in new[] { ("Etki alanı", "DomainProfile"), ("Özel ağ", "StandardProfile"), ("Genel ağ", "PublicProfile") })
        {
            // Değer.
            kontrol++; var v = Sayi(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\" + yol, "EnableFirewall");
            // Kapalıysa uyar.
            if (v == 0) l.Add(new Bulgu("Karne", $"Güvenlik duvarı kapalı ({ad} profili)", ad == "Genel ağ" ? Onem.Yuksek : Onem.Orta, $"Windows Güvenlik Duvarı {ad} profilinde kapalı; gelen bağlantılar süzülmüyor.", "Windows Güvenliği > Güvenlik duvarı ve ağ koruması'ndan ilgili profili açın."));
        }
        // UAC.
        kontrol++; if (Sayi(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA") == 0) l.Add(new Bulgu("Karne", "Kullanıcı Hesabı Denetimi (UAC) kapalı", Onem.Yuksek, "Programlar sizi uyarmadan yönetici yetkisi kullanabilir.", "UAC ayarlarını açın (Denetim Masası > Kullanıcı Hesabı Denetimi)."));
        // Uzak masaüstü.
        kontrol++; if (Sayi(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections") == 0) l.Add(new Bulgu("Karne", "Uzak Masaüstü (RDP) açık", Onem.Orta, "Bu bilgisayara ağdan uzaktan ekran bağlantısı kabul ediliyor.", "Kullanmıyorsanız Ayarlar > Sistem > Uzak Masaüstü'nden kapatın."));
        // Eski SMBv1.
        kontrol++; if (Sayi(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", "SMB1") == 1) l.Add(new Bulgu("Karne", "Eski dosya paylaşım protokolü SMBv1 açık", Onem.Yuksek, "SMBv1, fidye yazılımlarının kullandığı bilinen eski ve güvensiz protokoldür.", "Yönetici PowerShell: Set-SmbServerConfiguration -EnableSMB1Protocol $false"));
        // Yama yaşı.
        kontrol++;
        try
        {
            // Son kurulan güncellemelerin tarihleri.
            using var q = new ManagementObjectSearcher("SELECT InstalledOn FROM Win32_QuickFixEngineering");
            // Tarihleri çöz.
            var tarihler = q.Get().Cast<ManagementObject>().Select(x => DateTime.TryParse(x["InstalledOn"]?.ToString(), out var t) ? t : (DateTime?)null).Where(t => t != null).Select(t => t!.Value).ToList();
            // En yenisi 90 günden eskiyse uyar.
            if (tarihler.Count > 0 && (DateTime.Now - tarihler.Max()).TotalDays > 90) l.Add(new Bulgu("Karne", $"Son Windows güncellemesi {(int)(DateTime.Now - tarihler.Max()).TotalDays} gün önce", Onem.Dusuk, $"En son güncelleme: {tarihler.Max():dd.MM.yyyy}.", "Windows Güncelleme'den bekleyen güncellemeleri kurun."));
        }
        // Okunamazsa atla.
        catch (Exception hk8) { HataKayit.Sessiz(hk8, "SistemDenetimi.GuvenlikKarnesi"); }
        // BitLocker (yönetici izni gerekir; olmazsa sessizce geç).
        kontrol++;
        try
        {
            // C: sürücüsünün şifreleme durumu.
            using var b = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption", "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'");
            // Kayıt.
            var v = b.Get().Cast<ManagementObject>().FirstOrDefault();
            // Korumasızsa bilgi ver (çoğu ev bilgisayarında kapalıdır; bu yüzden düşük).
            if (v != null && v["ProtectionStatus"] is uint ps && ps == 0) l.Add(new Bulgu("Karne", "Disk şifreleme (BitLocker) kapalı", Onem.Dusuk, "Bilgisayar çalınırsa diskteki dosyalar okunabilir.", "Dizüstü bilgisayarlarda BitLocker/Cihaz şifrelemeyi açmanız önerilir."));
        }
        // Yönetici izni yoksa okunamaz; geç.
        catch (Exception hk9) { HataKayit.Sessiz(hk9, "SistemDenetimi.GuvenlikKarnesi"); }
        // Hiç sorun çıkmadıysa özet bilgi.
        if (!l.Any(x => x.Onem != Onem.Bilgi)) l.Add(new Bulgu("Karne", "Windows güvenlik karnesi temiz", Onem.Bilgi, $"{kontrol} temel koruma kontrolü yapıldı; sorun bulunmadı.", "Yapılacak bir şey yok."));
        // Sonucu ver.
        return l;
    }

    // ==================== Wi-Fi GÜVENLİK KONTROLÜ ====================

    // netsh komutunu çalıştırıp çıktısını verir (yalnızca okuma; yönetici izni gerekmez). Çıktı 4 saniyede gelmezse boş verir.
    private static string Netsh(string argumanlar)
    {
        // Çalıştırma başarısız olabilir.
        try
        {
            // Çıktıyı yakalayarak başlat.
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("netsh", argumanlar) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = System.Text.Encoding.UTF8 });
            // Başlatılamadıysa boş.
            if (p == null) return "";
            // Çıktıyı arka planda oku, en fazla 4 saniye bekle.
            var okuma = p.StandardOutput.ReadToEndAsync(); if (!okuma.Wait(4000)) { try { p.Kill(); } catch (Exception hk10) { HataKayit.Sessiz(hk10, "SistemDenetimi.Netsh"); } return ""; }
            // Çıktı.
            return okuma.Result;
        }
        // Hata olursa boş.
        catch { return ""; }
    }

    // Şifreleme/kimlik doğrulama metnini sınıflandırır: 3 = WPA3, 2 = WPA2, 1 = WPA (eski), 0 = WEP, -1 = açık (şifresiz), -2 = anlaşılamadı.
    public static int WifiSinif(string kimlik)
    {
        // Küçük harfe çevir.
        string k = kimlik.ToLowerInvariant();
        // WPA3 / SAE.
        if (k.Contains("wpa3") || k.Contains("sae")) return 3;
        // WPA2.
        if (k.Contains("wpa2")) return 2;
        // WPA (eski).
        if (k.Contains("wpa")) return 1;
        // WEP.
        if (k.Contains("wep")) return 0;
        // Açık ağ (İngilizce "Open", Türkçe "Açık").
        if (k.Contains("open") || k.Contains("açık") || k.Contains("acik")) return -1;
        // Anlaşılamadı.
        return -2;
    }

    // Bağlı olunan Wi-Fi ağının güvenliğini ve "ikiz ağ" şüphesini denetler. Wi-Fi yoksa/bağlı değilse boş liste verir.
    public static List<Bulgu> WifiDenetle() => WifiDegerlendir(Netsh("wlan show interfaces"), () => Netsh("wlan show networks mode=bssid"));

    // Verilen netsh çıktılarını değerlendirir (ayrı yazıldı ki örnek metinle denenebilsin). aglar: çevredeki ağ listesini isteğe bağlı getiren işlev.
    public static List<Bulgu> WifiDegerlendir(string a, Func<string> aglar)
    {
        // Sonuç listesi.
        var l = new List<Bulgu>();
        // Bağlı arayüz bilgisi.

        // Bağlı SSID.
        var ssidM = System.Text.RegularExpressions.Regex.Match(a, @"^\s*SSID\s*:\s*(.+?)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Wi-Fi yoksa veya bağlı değilse bir şey yapma.
        if (!ssidM.Success || ssidM.Groups[1].Value.Length == 0) return l;
        // Bağlı ağın adı.
        string ssid = ssidM.Groups[1].Value;
        // Kimlik doğrulama ve şifre (dil bağımsız yakalama).
        var kimlikM = System.Text.RegularExpressions.Regex.Match(a, @"^\s*(?:Authentication|Kimlik do[ğg]rulama\w*)\s*:\s*(.+?)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Şifreleme.
        var sifreM = System.Text.RegularExpressions.Regex.Match(a, @"^\s*(?:Cipher|[ŞSsş]ifre)\s*:\s*(.+?)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Değerler.
        string kimlik = kimlikM.Success ? kimlikM.Groups[1].Value : "", sifre = sifreM.Success ? sifreM.Groups[1].Value : "";
        // Sınıf.
        int s = WifiSinif(kimlik);
        // Açık ağ.
        if (s == -1) l.Add(new Bulgu("Wi-Fi", $"Açık (şifresiz) Wi-Fi ağına bağlısınız: {ssid}", Onem.Yuksek, $"Kimlik doğrulama: {kimlik}. Aynı ağdaki herkes trafiğinizi dinleyebilir.", "Mümkünse bu ağdan çıkın; zorunluysa yalnızca https siteleri kullanın ve VPN açın."));
        // WEP.
        else if (s == 0) l.Add(new Bulgu("Wi-Fi", $"Çok eski ve kırılmış Wi-Fi şifrelemesi (WEP): {ssid}", Onem.Yuksek, $"Kimlik doğrulama: {kimlik}. WEP dakikalar içinde kırılabilir.", "Modemin Wi-Fi ayarından WPA2 veya WPA3'e geçin."));
        // Eski WPA.
        else if (s == 1) l.Add(new Bulgu("Wi-Fi", $"Eski Wi-Fi şifrelemesi (WPA/TKIP): {ssid}", Onem.Orta, $"Kimlik doğrulama: {kimlik}; şifreleme: {sifre}. Eski sürüm zayıf kabul edilir.", "Modemin Wi-Fi ayarından WPA2 (AES) veya WPA3'e geçin."));
        // WPA2 ama TKIP.
        else if (s == 2 && sifre.Contains("TKIP", StringComparison.OrdinalIgnoreCase)) l.Add(new Bulgu("Wi-Fi", $"Wi-Fi şifrelemesi zayıf (TKIP): {ssid}", Onem.Dusuk, $"Kimlik doğrulama: {kimlik}; şifreleme: {sifre}.", "Modemin ayarında şifreleme türünü AES (CCMP) yapın."));
        // İyi.
        else if (s >= 2) l.Add(new Bulgu("Wi-Fi", $"Wi-Fi güvenliği iyi: {ssid}", Onem.Bilgi, $"Kimlik doğrulama: {kimlik}; şifreleme: {sifre}.", "Yapılacak bir şey yok."));
        // Anlaşılamadıysa bilgi.
        else l.Add(new Bulgu("Wi-Fi", $"Wi-Fi güvenlik türü okunamadı: {ssid}", Onem.Bilgi, $"Kimlik doğrulama: {kimlik}.", "Modem ayarlarından şifreleme türünü kontrol edin."));
        // İkiz ağ şüphesi: aynı adlı başka bir ağ ŞİFRESİZ yayın yapıyorsa (bağlı ağımız şifreliyken).
        if (s >= 0)
        {
            // Çevredeki ağların listesi.
            string c = aglar();
            // SSID bloklarını parçala.
            var bloklar = System.Text.RegularExpressions.Regex.Split(c, @"(?m)^\s*SSID\s+\d+\s*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Skip(1);
            // Her blok.
            foreach (var b in bloklar)
            {
                // Bloğun ilk satırı ağ adıdır.
                string ad = b.Split('\n')[0].Trim(); if (!string.Equals(ad, ssid, StringComparison.OrdinalIgnoreCase)) continue;
                // Bu adlı ağın kimlik doğrulaması.
                var km = System.Text.RegularExpressions.Regex.Match(b, @"^\s*(?:Authentication|Kimlik do[ğg]rulama\w*)\s*:\s*(.+?)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // Aynı adlı ama şifresizse ikiz ağ şüphesi.
                if (km.Success && WifiSinif(km.Groups[1].Value) == -1) { l.Add(new Bulgu("Wi-Fi", $"Aynı adlı ŞİFRESİZ ikinci ağ görünüyor: {ssid}", Onem.Orta, $"Bağlı olduğunuz \"{ssid}\" şifreli; ama çevrede aynı adı taşıyan şifresiz bir ağ da yayın yapıyor. Bu, sahte \"ikiz ağ\" (evil twin) tuzağı olabilir.", "Cihazınızın bu ağa otomatik bağlanmasını kapatın; bağlanırken ağ adının yanında kilit olduğundan emin olun.")); break; }
            }
        }
        // Sonuç.
        return l;
    }

    // ==================== 3) YEREL HESAP DENETİMİ ====================

    // Yerel kullanıcı hesaplarını ve yönetici grubunu denetler.
    public static List<Bulgu> HesapDenetle()
    {
        // Sonuç listesi.
        var l = new List<Bulgu>();
        // Yerel hesaplar.
        using (var ara = new ManagementObjectSearcher("SELECT Name, SID, Disabled, PasswordRequired FROM Win32_UserAccount WHERE LocalAccount=True"))
            // Her hesap.
            foreach (ManagementObject h in ara.Get())
            {
                // Bilgiler.
                string ad = h["Name"]?.ToString() ?? "", sid = h["SID"]?.ToString() ?? ""; bool pasif = h["Disabled"] is bool d && d, parolaGerek = h["PasswordRequired"] is not bool p || p;
                // Pasif hesaplar sorun değildir.
                if (pasif) continue;
                // Guest hesabı açık.
                if (sid.EndsWith("-501", StringComparison.Ordinal)) l.Add(new Bulgu("Hesap", "Misafir (Guest) hesabı açık", Onem.Orta, $"'{ad}' hesabı etkin; kimliksiz kullanıma açıktır.", "Hesabı devre dışı bırakın: net user " + ad + " /active:no"));
                // Yerleşik Administrator açık.
                else if (sid.EndsWith("-500", StringComparison.Ordinal)) l.Add(new Bulgu("Hesap", "Yerleşik Administrator hesabı açık", Onem.Dusuk, $"'{ad}' hesabı etkin; saldırganların ilk denediği adlardandır.", "Kullanmıyorsanız devre dışı bırakın: net user " + ad + " /active:no"));
                // Parolasız hesap.
                if (!parolaGerek) l.Add(new Bulgu("Hesap", $"Parola gerektirmeyen hesap: {ad}", Onem.Orta, $"'{ad}' hesabına parolasız girilebilir.", "Hesaba bir parola verin."));
            }
        // Yönetici grubu üyeleri (grup adı dile göre değişir; SID ile bulunur).
        using (var g = new ManagementObjectSearcher("SELECT Name, Domain FROM Win32_Group WHERE SID='S-1-5-32-544'"))
        {
            // Grup kaydı.
            var grup = g.Get().Cast<ManagementObject>().FirstOrDefault();
            // Grup bulunduysa üyeleri listele.
            if (grup != null)
            {
                // Üyeleri sorgula.
                using var u = new ManagementObjectSearcher($"SELECT PartComponent FROM Win32_GroupUser WHERE GroupComponent=\"Win32_Group.Domain='{grup["Domain"]}',Name='{grup["Name"]}'\"");
                // Üye adları (PartComponent içinden Name='...').
                var uyeler = u.Get().Cast<ManagementObject>().Select(x => System.Text.RegularExpressions.Regex.Match(x["PartComponent"]?.ToString() ?? "", "Name=\"([^\"]+)\"").Groups[1].Value).Where(x => x.Length > 0).ToList();
                // Üçten fazla yönetici varsa uyar.
                if (uyeler.Count > 3) l.Add(new Bulgu("Hesap", $"Yönetici grubunda {uyeler.Count} hesap var", Onem.Dusuk, "Yöneticiler: " + string.Join(", ", uyeler), "Gündelik kullanım için standart hesap kullanın; yönetici grubunu gerekenlerle sınırlayın."));
                // Sorun yoksa bilgi.
                else l.Add(new Bulgu("Hesap", $"Yönetici grubu makul ({uyeler.Count} hesap)", Onem.Bilgi, "Yöneticiler: " + string.Join(", ", uyeler), "Yapılacak bir şey yok."));
            }
        }
        // Hiç sorun çıkmadıysa bilgi notu.
        if (!l.Any(x => x.Onem != Onem.Bilgi)) l.Add(new Bulgu("Hesap", "Yerel hesaplar temiz", Onem.Bilgi, "Guest kapalı, parolasız etkin hesap yok.", "Yapılacak bir şey yok."));
        // Sonucu ver.
        return l;
    }
}
