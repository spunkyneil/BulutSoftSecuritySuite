// Bu dosya "Kalıcılık Denetçisi"nin beynidir (ekrandan bağımsız, test edilebilir).
// Zararlı yazılımlar bilgisayara kalıcı yerleşmek için başlangıç noktalarını kullanır: Kayıt Defteri Run anahtarları, Başlangıç klasörleri,
// Zamanlanmış Görevler ve otomatik başlayan hizmetler. Bu dosya hepsini OKUR, her öğenin dijital imzasını doğrular, riskini değerlendirir
// ve gerekirse GERİ ALINABİLİR biçimde devre dışı bırakacak komutları üretir (silme yok; kayıt defteri "StartupApproved" ve görev/hizmet devre dışı).
// Microsoft imzalı öğeler bu araçla devre dışı bırakılamaz (işletim sistemini bozmamak için).
using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

// Programımızın adı.
namespace LabDenetim;

// Bir başlangıç (kalıcılık) öğesi.
internal sealed class KaliciOge
{
    // Tür: "Run", "RunOnce", "Başlangıç Klasörü", "Zamanlanmış Görev", "Hizmet".
    public string Tur { get; set; } = ""; public string Ad { get; set; } = ""; public string Komut { get; set; } = ""; public string Yol { get; set; } = "";
    // Konum: kayıt defteri anahtarı, klasör, görev yolu veya hizmet adı. Alan: kayıt defteri için değer adı.
    public string Konum { get; set; } = ""; public bool Hklm { get; set; } public bool Wow64 { get; set; }
    // Etkin mi?
    public bool Etkin { get; set; } = true;
    // İmza: "İmzalı", "İmzasız", "Geçersiz", "Güvenilmeyen", "Doğrulanamadı", "Dosya yok"; yayıncı ve Microsoft mu.
    public string Imza { get; set; } = "?"; public string Yayinci { get; set; } = ""; public bool Microsoft { get; set; }
    // Risk ve nedeni.
    public string Risk { get; set; } = ""; public string Neden { get; set; } = "";
}

// Devre dışı bırakma yedeği (geri almak için).
internal sealed record KaliciYedek(string Id, DateTime Zaman, string Tur, string Ad, string Konum, bool Hklm, bool Wow64, string Aciklama);

// İmza denetimi (Windows PowerShell Get-AuthenticodeSignature: katalog imzalı sistem dosyalarını da doğru bilir).
internal static class ImzaDenetimi
{
    // Test için başka bir yürütücü verilebilir.
    public static string PowerShellYolu { get; set; } = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    // PowerShell'e verilen SABİT betik: yolları standart girişten JSON olarak okur (yollar KOD olarak çalıştırılmaz, yalnızca veri).
    private const string Betik = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; $p=[Console]::In.ReadToEnd() | ConvertFrom-Json; $r=@(); foreach($x in $p){ try { $s=Get-AuthenticodeSignature -LiteralPath $x -ErrorAction Stop; $c=''; if($s.SignerCertificate){$c=$s.SignerCertificate.Subject}; $r+=[pscustomobject]@{P=[string]$x;S=[string]$s.Status;C=[string]$c} } catch { $r+=[pscustomobject]@{P=[string]$x;S='Hata';C=''} } }; $r | ConvertTo-Json -Compress";

    // Yolların imzalarını denetler (en çok 120 saniye). Sonuç: yol → (durum, yayıncı, Microsoft mu).
    public static Dictionary<string, (string Durum, string Yayinci, bool Microsoft)> Kontrol(IEnumerable<string> yollar)
    {
        // Yalnızca var olan dosyalar; tekrarsız.
        var liste = yollar.Where(y => !string.IsNullOrWhiteSpace(y)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); var sonuc = new Dictionary<string, (string, string, bool)>(StringComparer.OrdinalIgnoreCase);
        // Boşsa çık.
        if (liste.Count == 0 || !File.Exists(PowerShellYolu)) return sonuc;
        // Süreç.
        try
        {
            // PowerShell: profil yok, etkileşim yok.
            var psi = new ProcessStartInfo(PowerShellYolu) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 }; psi.Environment.Remove("PSModulePath"); Directory.CreateDirectory(BulutKlasor.Kok); string betikYolu = BulutKlasor.Dosya("imza_betik.ps1"); File.WriteAllText(betikYolu, Betik, new UTF8Encoding(true)); foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", betikYolu }) psi.ArgumentList.Add(a);
            // Başlat ve yolları ver.
            using var p = Process.Start(psi)!; p.StandardInput.Write(JsonSerializer.Serialize(liste)); p.StandardInput.Close();
            // Çıktıyı oku (hata akışı kilitlenmesin diye eşzamanlı).
            var hata = p.StandardError.ReadToEndAsync(); string cikti = p.StandardOutput.ReadToEnd(); if (!p.WaitForExit(120_000)) { try { p.Kill(true); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KaliciDenetci.Kontrol"); } return sonuc; }
            // Çöz.
            sonuc = Coz(cikti);
        }
        // Başlatılamazsa boş sonuç (öğeler "Doğrulanamadı" kalır).
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "KaliciDenetci.Kontrol"); }
        // Sonuç.
        return sonuc;
    }

    // PowerShell JSON çıktısını çözer.
    public static Dictionary<string, (string Durum, string Yayinci, bool Microsoft)> Coz(string json)
    {
        // Sonuç.
        var d = new Dictionary<string, (string, string, bool)>(StringComparer.OrdinalIgnoreCase);
        // Bozuk çıktı güvenle atlanır.
        try
        {
            // JSON dizisi.
            using var doc = JsonDocument.Parse(json.Trim()); var kok = doc.RootElement; if (kok.ValueKind == JsonValueKind.Object && kok.TryGetProperty("value", out var sar) && sar.ValueKind == JsonValueKind.Array) kok = sar; var ogeler = kok.ValueKind == JsonValueKind.Array ? kok.EnumerateArray().ToList() : new List<JsonElement> { kok };
            foreach (var o in ogeler)
            {
                // Alanlar.
                string yol = o.TryGetProperty("P", out var p) ? p.GetString() ?? "" : "", s = o.TryGetProperty("S", out var st) ? st.GetString() ?? "" : "", c = o.TryGetProperty("C", out var cc) ? cc.GetString() ?? "" : "";
                // Durum çevirisi.
                string durum = s switch { "Valid" => "İmzalı", "NotSigned" => "İmzasız", "HashMismatch" => "Geçersiz", "NotTrusted" => "Güvenilmeyen", _ => "Doğrulanamadı" };
                // Yayıncı: sertifika konusunda O= veya CN=.
                string yay = Alan(c, "O") is { Length: > 0 } a ? a : Alan(c, "CN"); bool ms = c.Contains("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase) && durum == "İmzalı";
                // Ekle.
                if (yol.Length > 0) d[yol] = (durum, yay, ms);
            }
        }
        // Sessiz geç.
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "KaliciDenetci.Coz"); }
        // Sonuç.
        return d;
    }

    // "CN=..., O=..." konusundan bir alanı alır.
    private static string Alan(string konu, string ad) { var m = Regex.Match(konu ?? "", @"(?:^|,\s*)" + ad + @"=(?:""([^""]*)""|([^,]*))"); return m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim() : ""; }
}

// Kalıcılık numaralandırma, risk ve devre dışı bırakma.
internal static class KaliciDenetci
{
    // Tehlikeli komut parçaları ve klasör ipuçları (SistemDenetimi'ndekilerle aynı mantık).
    private static readonly string[] Tehlikeli = { "-enc ", "-encodedcommand", "frombase64string", "mshta", "wscript", "cscript", "bitsadmin", "certutil -urlcache", "regsvr32 /s /u /i:http", "rundll32 javascript", "powershell -w hidden", "-windowstyle hidden", "invoke-expression", "downloadstring" };
    private static readonly string[] SupheliKlasor = { "\\temp\\", "\\users\\public\\", "\\downloads\\", "\\appdata\\local\\temp", "\\$recycle.bin\\" };
    private static readonly string[] DikkatKlasor = { "\\appdata\\roaming\\", "\\appdata\\local\\", "\\programdata\\" };

    // Güvenli ad kuralı: komut satırına koyulacağı için kabuk özel karakterleri (& | < > ^ % " ! ') içermemeli.
    public static bool AdGuvenli(string ad) => Regex.IsMatch(ad ?? "", @"^[\p{L}\p{N} _.\-\\{}()+,;=@#\[\]~]{1,200}$");

    // ---- Komuttan dosya yolu ----
    // Bir komut satırından çalıştırılan dosyanın yolunu çıkarır (ortam değişkenleri açılır).
    public static string KomuttanYol(string komut)
    {
        // Boşsa.
        if (string.IsNullOrWhiteSpace(komut)) return "";
        // Ortam değişkenlerini aç.
        string k = Environment.ExpandEnvironmentVariables(komut.Trim());
        // Tırnaklıysa tırnağın içi.
        var m = Regex.Match(k, "^\"([^\"]+)\""); if (m.Success) return m.Groups[1].Value.Trim();
        // Uzantılı ilk parça (boşluklu yollar dahil: Program Files gibi).
        m = Regex.Match(k, @"^(.+?\.(?:exe|com|bat|cmd|ps1|vbs|vbe|js|jse|wsf|scr|msi|lnk|dll))(?=\s|$|,)", RegexOptions.IgnoreCase); if (m.Success) return m.Groups[1].Value.Trim();
        // Son çare: ilk sözcük.
        return k.Split(' ', 2)[0].Trim('"');
    }

    // ---- Numaralandırma ----
    // Tüm başlangıç öğelerini okur (imza henüz denetlenmez).
    public static List<KaliciOge> Tara()
    {
        // Sonuç.
        var l = new List<KaliciOge>();
        // Kayıt defteri Run / RunOnce anahtarları.
        foreach (var (hklm, wow, yol, tur) in new[] { (true, false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Run"), (true, true, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run"), (true, false, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce"), (false, false, @"Software\Microsoft\Windows\CurrentVersion\Run", "Run"), (false, false, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "RunOnce") })
        {
            // Okunamayan anahtar atlanır.
            try
            {
                // Anahtarı aç.
                using var k = (hklm ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(yol); if (k == null) continue;
                // Her değer.
                foreach (var ad in k.GetValueNames()) { string komut = Convert.ToString(k.GetValue(ad), CultureInfo.InvariantCulture) ?? ""; l.Add(new KaliciOge { Tur = tur, Ad = ad, Komut = komut, Yol = KomuttanYol(komut), Konum = (hklm ? "HKLM\\" : "HKCU\\") + yol, Hklm = hklm, Wow64 = wow, Etkin = tur == "Run" ? !OnayliDevreDisi(hklm, wow ? "Run32" : "Run", ad) : true }); }
            }
            catch (Exception hk4) { HataKayit.Sessiz(hk4, "KaliciDenetci.Tara"); }
        }
        // Başlangıç klasörleri.
        foreach (var (hklm, klasor) in new[] { (false, Environment.GetFolderPath(Environment.SpecialFolder.Startup)), (true, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)) })
        {
            // Klasör yoksa atla.
            try
            {
                if (!Directory.Exists(klasor)) continue;
                // Her dosya (desktop.ini hariç).
                foreach (var f in Directory.GetFiles(klasor)) { string ad = Path.GetFileName(f); if (ad.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue; string hedef = f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? LnkHedef(f) : f; l.Add(new KaliciOge { Tur = "Başlangıç Klasörü", Ad = ad, Komut = hedef.Length > 0 ? hedef : f, Yol = hedef.Length > 0 ? hedef : f, Konum = klasor, Hklm = hklm, Etkin = !OnayliDevreDisi(hklm, "StartupFolder", ad) }); }
            }
            catch (Exception hk5) { HataKayit.Sessiz(hk5, "KaliciDenetci.Tara"); }
        }
        // Zamanlanmış görevler (oturum açma/açılış tetikleyicili olanlar).
        try { l.AddRange(GorevleriOku()); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "KaliciDenetci.Tara"); }
        // Otomatik başlayan hizmetler.
        try { l.AddRange(HizmetleriOku()); } catch (Exception hk7) { HataKayit.Sessiz(hk7, "KaliciDenetci.Tara"); }
        // Sonuç.
        return l;
    }

    // .lnk kısayolunun hedefini okur (WScript.Shell).
    private static string LnkHedef(string lnk) { try { dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic k = sh.CreateShortcut(lnk); return (string)(k.TargetPath ?? ""); } catch { return ""; } }

    // StartupApproved kayıt yolu (yalnızca testte, gerçek sistem ayarlarına dokunmadan denemek için değiştirilebilir; sonunda ters eğik çizgi olmalı).
    internal static string OnayliKok { get; set; } = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    // StartupApproved kaydına göre devre dışı mı? (ilk baytın en düşük biti 1 ise devre dışı)
    public static bool OnayliDevreDisi(bool hklm, string alt, string ad)
    {
        // Okuma bozulabilir.
        try { using var k = (hklm ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(OnayliKok + alt); return k?.GetValue(ad) is byte[] b && b.Length > 0 && (b[0] & 1) == 1; } catch { return false; }
    }

    // Zamanlanmış görevleri (Microsoft klasörü hariç) okur.
    private static List<KaliciOge> GorevleriOku()
    {
        // Sonuç.
        var l = new List<KaliciOge>(); var tip = Type.GetTypeFromProgID("Schedule.Service"); if (tip == null) return l;
        // Servis.
        dynamic svc = Activator.CreateInstance(tip)!; svc.Connect();
        // Klasörleri gez.
        void Gez(dynamic klasor)
        {
            // Bu klasördeki görevler (gizliler dahil).
            foreach (dynamic t in klasor.GetTasks(1))
            {
                // Her görev tek tek korunur.
                try
                {
                    // Microsoft'un kendi görevleri çok fazla: yolu \Microsoft\ ile başlayanlar atlanır.
                    string yol = (string)t.Path; if (yol.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase)) continue;
                    // Tetikleyici: 8 = açılışta, 9 = oturum açılışında.
                    bool baslangic = false; foreach (dynamic tr in t.Definition.Triggers) { int ty = (int)tr.Type; if (ty is 8 or 9) baslangic = true; } if (!baslangic) continue;
                    // İlk çalıştırma eylemi.
                    string komut = ""; foreach (dynamic a in t.Definition.Actions) { if ((int)a.Type == 0) { komut = ((string)(a.Path ?? "") + " " + (string)(a.Arguments ?? "")).Trim(); break; } }
                    // Ekle.
                    l.Add(new KaliciOge { Tur = "Zamanlanmış Görev", Ad = (string)t.Name, Komut = komut, Yol = KomuttanYol(komut), Konum = yol, Hklm = true, Etkin = (bool)t.Enabled });
                }
                catch (Exception hk8) { HataKayit.Sessiz(hk8, "KaliciDenetci.GorevleriOku"); }
            }
            // Alt klasörler.
            foreach (dynamic alt in klasor.GetFolders(0)) { try { Gez(alt); } catch (Exception hk9) { HataKayit.Sessiz(hk9, "KaliciDenetci.GorevleriOku"); } }
        }
        // Kökten başla.
        Gez(svc.GetFolder("\\"));
        // Sonuç.
        return l;
    }

    // Otomatik başlayan Win32 hizmetlerini okur (Start=2).
    private static List<KaliciOge> HizmetleriOku()
    {
        // Sonuç.
        var l = new List<KaliciOge>();
        // Anahtar.
        using var kok = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"); if (kok == null) return l;
        // Her hizmet.
        foreach (var ad in kok.GetSubKeyNames())
        {
            // Okunamayan atlanır.
            try
            {
                using var k = kok.OpenSubKey(ad); if (k == null) continue;
                // Yalnızca otomatik başlayan (2) Win32 hizmetleri (tür 16 veya 32).
                if (k.GetValue("Start") is not int start || start != 2 || k.GetValue("Type") is not int tur || (tur & 0x30) == 0) continue;
                // Komut.
                string komut = Convert.ToString(k.GetValue("ImagePath"), CultureInfo.InvariantCulture) ?? ""; if (komut.Length == 0) continue;
                // Ekle.
                l.Add(new KaliciOge { Tur = "Hizmet", Ad = ad, Komut = komut, Yol = KomuttanYol(komut), Konum = ad, Hklm = true, Etkin = true });
            }
            catch (Exception hk10) { HataKayit.Sessiz(hk10, "KaliciDenetci.HizmetleriOku"); }
        }
        // Sonuç.
        return l;
    }

    // ---- İmza ve risk ----
    // İmzaları denetleyip risk değerlendirmesini yapar (öğeleri yerinde günceller).
    public static void Degerlendir(List<KaliciOge> ogeler, Dictionary<string, (string Durum, string Yayinci, bool Microsoft)>? imzalar = null)
    {
        // İmzalar (verilmediyse denetle).
        imzalar ??= ImzaDenetimi.Kontrol(ogeler.Select(o => o.Yol).Where(y => y.Length > 0 && File.Exists(y)));
        // Her öğe.
        foreach (var o in ogeler)
        {
            // İmza bilgisi.
            if (o.Yol.Length > 0 && File.Exists(o.Yol)) { if (imzalar.TryGetValue(o.Yol, out var s)) { o.Imza = s.Durum; o.Yayinci = s.Yayinci; o.Microsoft = s.Microsoft; } else o.Imza = "Doğrulanamadı"; } else o.Imza = o.Yol.Length > 0 ? "Dosya yok" : "-";
            // Risk.
            (o.Risk, o.Neden) = RiskHesapla(o);
        }
    }

    // Tek öğenin riski ve nedeni.
    public static (string Risk, string Neden) RiskHesapla(KaliciOge o)
    {
        // Küçük harfli komut ve yol.
        string k = (o.Komut ?? "").ToLowerInvariant(), y = (o.Yol ?? "").ToLowerInvariant();
        // Tehlikeli teknik.
        if (Tehlikeli.Any(t => k.Contains(t))) return ("Yüksek", "Şüpheli komut tekniği (gizli/kodlanmış çalıştırma veya indirme).");
        // Geçersiz imza: dosya imzalandıktan sonra değiştirilmiş.
        if (o.Imza == "Geçersiz") return ("Yüksek", "Dijital imza geçersiz: dosya imzalandıktan sonra değiştirilmiş olabilir.");
        // Dosya yok: kırık kayıt.
        if (o.Imza == "Dosya yok") return ("Orta", "Çalıştırılan dosya bulunamıyor (kırık veya silinmiş kalıntı kayıt).");
        // Microsoft imzalı: güvenli.
        if (o.Microsoft) return ("Güvenli", "Microsoft imzalı.");
        // İmzasız.
        if (o.Imza == "İmzasız") return SupheliKlasor.Any(t => y.Contains(t)) ? ("Yüksek", "İmzasız ve şüpheli bir klasörde (Temp/İndirilenler vb.).") : DikkatKlasor.Any(t => y.Contains(t)) ? ("Orta", "İmzasız ve kullanıcı yazılabilir bir klasörde.") : ("Düşük", "Dijital imzası yok (birçok meşru program da imzasızdır).");
        // Güvenilmeyen / doğrulanamayan imza.
        if (o.Imza is "Güvenilmeyen" or "Doğrulanamadı") return ("Orta", "İmza doğrulanamadı.");
        // İmzalı ama şüpheli klasörde.
        if (SupheliKlasor.Any(t => y.Contains(t))) return ("Orta", "İmzalı ama şüpheli bir klasörden çalışıyor.");
        // İmzalı, diğer yayıncı.
        return ("Düşük", "İmzalı (" + (o.Yayinci.Length > 0 ? o.Yayinci : "yayıncı bilinmiyor") + ").");
    }

    // ---- Devre dışı bırakma / geri alma ----
    // Devre dışı bırakma planı: komut (HKCU için boş = doğrudan yazılır), yönetici gerekir mi, ret nedeni.
    public sealed record Plan(string? Komut, bool Yonetici, bool DogrudanHkcu, string? Ret, string GeriAl);

    // Öğeyi devre dışı bırakma planı (güvenlik kontrolleri burada).
    public static Plan DevreDisiPlani(KaliciOge o, bool devreDisi = true)
    {
        // Microsoft imzalı öğelere dokunulmaz.
        if (o.Microsoft) return new(null, false, false, "Microsoft imzalı öğeler işletim sistemini bozmamak için bu araçla değiştirilemez.", "");
        // Ad güvenli mi? (komut satırına konacak)
        if (!AdGuvenli(o.Ad) || !AdGuvenli(o.Konum.Replace("HKLM\\", "").Replace("HKCU\\", "").Replace(":", ""))) return new(null, false, false, "Adı güvenli biçimde değil (özel karakter içeriyor); Windows araçlarıyla elle devre dışı bırakın.", "");
        // Tür.
        switch (o.Tur)
        {
            // Run ve Başlangıç klasörü: StartupApproved.
            case "Run": case "Başlangıç Klasörü":
                {
                    // Alt anahtar.
                    string alt = o.Tur == "Run" ? (o.Wow64 ? "Run32" : "Run") : "StartupFolder"; string deger = devreDisi ? "030000000000000000000000" : "020000000000000000000000";
                    // HKCU: yönetici gerekmez, doğrudan yazılır.
                    if (!o.Hklm) return new(null, false, true, null, "");
                    // HKLM: yönetici komutu.
                    return new($"reg add \"HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\{alt}\" /v \"{o.Ad}\" /t REG_BINARY /d {deger} /f", true, false, null, "");
                }
            // RunOnce: tek seferlik; StartupApproved desteklemez.
            case "RunOnce": return new(null, false, false, "RunOnce kayıtları yalnızca bir kez çalışır ve bu araçla devre dışı bırakılamaz; kaydı Kayıt Defteri'nden elle inceleyin.", "");
            // Zamanlanmış görev.
            case "Zamanlanmış Görev": return new($"schtasks /change /tn \"{o.Konum}\" /{(devreDisi ? "disable" : "enable")}", true, false, null, "");
            // Hizmet: otomatik → devre dışı (geri: otomatik).
            case "Hizmet": { string hAd = o.Ad.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.') ? o.Ad : "\"" + o.Ad + "\""; return new($"sc config {hAd} start= {(devreDisi ? "disabled" : "auto")}", true, false, null, ""); }
        }
        // Tanınmayan tür.
        return new(null, false, false, "Bu öğe türü desteklenmiyor.", "");
    }

    // HKCU StartupApproved değerini doğrudan yazar (yönetici gerekmez).
    public static void HkcuYaz(KaliciOge o, bool devreDisi)
    {
        // Alt anahtar ve değer.
        string alt = o.Tur == "Run" ? (o.Wow64 ? "Run32" : "Run") : "StartupFolder"; var b = new byte[12]; b[0] = (byte)(devreDisi ? 3 : 2);
        // Yaz.
        using var k = Registry.CurrentUser.CreateSubKey(OnayliKok + alt); k.SetValue(o.Ad, b, RegistryValueKind.Binary);
    }

    // ---- Yedek (geri almak için) ----
    // Yedek dosyası.
    public static string YedekYolu => BulutKlasor.Dosya("kalicilik_yedek.json");

    // Yedekleri okur.
    // Önce ana dosyayı, okunamazsa son sağlam kopyayı (.bak) dener; ikisi de yoksa/bozuksa boş liste verir.
    public static List<KaliciYedek> YedekOku() => YedekCoz(YedekYolu) ?? YedekCoz(YedekYolu + ".bak") ?? new();

    // Bir yedek dosyasını çözer; dosya yok veya bozuksa null.
    private static List<KaliciYedek>? YedekCoz(string yol) { try { return File.Exists(yol) ? JsonSerializer.Deserialize<List<KaliciYedek>>(File.ReadAllText(yol)) : null; } catch { return null; } }

    // Yedekleri yazar: önce geçici dosyaya yazılır, sonra yerine taşınır (yazma sırasında çökme eski dosyayı bozmaz).
    // Yerine koymadan önce, ana dosya sağlamsa son sağlam kopya olarak .bak'a alınır (bozuk dosya sağlam kopyanın üstüne yazılmaz).
    public static void YedekYaz(List<KaliciYedek> l)
    {
        // Klasör.
        Directory.CreateDirectory(BulutKlasor.Kok);
        string json = JsonSerializer.Serialize(l, JsonAyarlari.Girintili);
        // Mevcut ana dosya okunabiliyorsa son sağlam kopya olarak sakla.
        try { if (YedekCoz(YedekYolu) != null) File.Copy(YedekYolu, YedekYolu + ".bak", true); } catch (Exception hk11) { HataKayit.Sessiz(hk11, "KaliciDenetci.YedekYaz"); }
        // Atomik yaz (benzersiz geçici dosya + yer değiştirme).
        Depo.Yaz(YedekYolu, json);
    }

    // Devre dışı bırakılan öğeyi yedeğe ekler.
    public static void YedekEkle(KaliciOge o) { var l = YedekOku(); l.RemoveAll(x => x.Tur == o.Tur && x.Ad == o.Ad && x.Konum == o.Konum); l.Add(new KaliciYedek(Guid.NewGuid().ToString("N"), DateTime.Now, o.Tur, o.Ad, o.Konum, o.Hklm, o.Wow64, o.Komut)); YedekYaz(l); }

    // Yedekten bir öğe oluşturur (geri alma için).
    public static KaliciOge YedektenOge(KaliciYedek y) => new() { Tur = y.Tur, Ad = y.Ad, Konum = y.Konum, Hklm = y.Hklm, Wow64 = y.Wow64, Komut = y.Aciklama };
}
