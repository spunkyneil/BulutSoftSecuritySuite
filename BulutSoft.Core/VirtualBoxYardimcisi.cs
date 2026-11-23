// Bu dosya "Hedef Seç > Sanal Ağları Tara" listesini DOĞRU hâle getirir: VirtualBox'ın kendi aracıyla (VBoxManage) gerçek sanal makineleri,
// durumlarını (Çalışıyor / Kapalı) ve IP adreslerini öğrenir. Böylece (1) kapalı bir makine "çalışıyor" gibi görünmez, (2) VirtualBox'ın kendi
// DHCP sunucusu "sanal makine" sanılmaz, (3) kapalı bir makine seçilirse (onayınızla) başlatılıp IP'si beklenir.
// Yalnızca VBoxManage okuma komutları ve (onaylı) startvm kullanılır; VirtualBox kurulu değilse eski davranış sürer.
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Bir VirtualBox sanal makinesi.
internal sealed record VBoxVm(string Ad, string Uuid, string Durum, List<string> Macler)
{
    // Çalışıyor mu?
    public bool Calisiyor => Durum is "running" or "paused";
    // Türkçe durum yazısı.
    public string DurumYazi => Durum switch { "running" => "Çalışıyor", "paused" => "Duraklatıldı", "poweroff" => "Kapalı", "saved" => "Kayıtlı (uykuda)", "aborted" => "Kapalı (beklenmedik)", _ => Durum };
}

// Listede gösterilecek bir satır: bulunan cihaz ve varsa eşleşen sanal makine.
internal sealed record AgSatiri(AgCihazi Cihaz, VBoxVm? Vm);

// VirtualBox yardımcısı.
internal static class VirtualBoxYardimcisi
{
    // Test için başka bir VBoxManage yolu verilebilir.
    public static string? YolGecici { get; set; }

    // VBoxManage.exe yolu (kurulu değilse null).
    public static string? Yol()
    {
        // Test.
        if (YolGecici != null) return File.Exists(YolGecici) ? YolGecici : null;
        // Kayıt defterindeki kurulum klasörü.
        try { if (Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Oracle\VirtualBox")?.GetValue("InstallDir") is string d && File.Exists(Path.Combine(d, "VBoxManage.exe"))) return Path.Combine(d, "VBoxManage.exe"); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "VirtualBoxYardimcisi.Yol"); }
        // Varsayılan konum.
        string v = @"C:\Program Files\Oracle\VirtualBox\VBoxManage.exe"; return File.Exists(v) ? v : null;
    }

    // VBoxManage'i verilen argümanlarla çalıştırır (en çok 'sn' saniye); çıktıyı verir, hata olursa null.
    private static string? Calistir(string[] args, int sn = 15)
    {
        // Yol.
        var yol = Yol(); if (yol == null) return null;
        // Süreç.
        try
        {
            var psi = new ProcessStartInfo(yol) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!; var hata = p.StandardError.ReadToEndAsync(); string cikti = p.StandardOutput.ReadToEnd(); if (!p.WaitForExit(sn * 1000)) { try { p.Kill(true); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "VirtualBoxYardimcisi.Calistir"); } return null; }
            return cikti;
        }
        // Başlatılamadı.
        catch { return null; }
    }

    // ---- Ayrıştırıcılar (test edilebilir) ----
    // "list vms" çıktısı: "Ad" {uuid}
    public static List<(string Ad, string Uuid)> ListeCoz(string cikti) => Regex.Matches(cikti ?? "", @"^""(?<ad>.+)""\s+\{(?<id>[0-9a-fA-F\-]{36})\}\s*$", RegexOptions.Multiline).Select(m => (m.Groups["ad"].Value, m.Groups["id"].Value)).ToList();

    // "showvminfo --machinereadable" çıktısından durum ve MAC adresleri (NAT dahil; kapalı kart hariç).
    public static (string Durum, List<string> Macler) BilgiCoz(string cikti)
    {
        // Durum.
        string durum = Regex.Match(cikti ?? "", @"^VMState=""(?<d>[^""]+)""", RegexOptions.Multiline) is { Success: true } m ? m.Groups["d"].Value : "bilinmiyor";
        // Kartlar: nicN="none" olmayanların macaddressN değeri.
        var macler = new List<string>();
        for (int i = 1; i <= 8; i++)
        {
            // Kart türü ve MAC.
            var nic = Regex.Match(cikti ?? "", $@"^nic{i}=""(?<t>[^""]+)""", RegexOptions.Multiline); var mac = Regex.Match(cikti ?? "", $@"^macaddress{i}=""(?<m>[0-9A-Fa-f]{{12}})""", RegexOptions.Multiline);
            // Kart kullanımdaysa ekle (AA-BB-CC-DD-EE-FF biçimi: ARP ile aynı).
            if (nic.Success && nic.Groups["t"].Value != "none" && mac.Success) macler.Add(string.Join("-", Enumerable.Range(0, 6).Select(k => mac.Groups["m"].Value.Substring(k * 2, 2))).ToUpperInvariant());
        }
        // Sonuç.
        return (durum, macler);
    }

    // Konuk özelliklerinden IPv4 adreslerini çıkarır ("value: 192.168.56.101").
    public static List<string> IpCoz(string cikti) => Regex.Matches(cikti ?? "", @"value:\s*(?<ip>\d{1,3}(?:\.\d{1,3}){3})").Select(m => m.Groups["ip"].Value).Where(i => i != "0.0.0.0").Distinct().ToList();

    // ---- VirtualBox işlemleri ----
    // Kayıtlı sanal makineleri durumlarıyla listeler (VirtualBox kurulu değilse null).
    public static List<VBoxVm>? Bul()
    {
        // Liste.
        var l = Calistir(new[] { "list", "vms" }); if (l == null) return null;
        // Her makinenin bilgisi.
        var sonuc = new List<VBoxVm>();
        foreach (var (ad, id) in ListeCoz(l)) { var b = Calistir(new[] { "showvminfo", id, "--machinereadable" }); if (b == null) continue; var (durum, macler) = BilgiCoz(b); sonuc.Add(new VBoxVm(ad, id, durum, macler)); }
        // Sonuç.
        return sonuc;
    }

    // Çalışan makinenin konuk IP'lerini (Guest Additions varsa) verir.
    public static List<string> IpBul(string uuid)
    {
        // Güvenlik: yalnızca geçerli UUID.
        if (uuid == null || !Regex.IsMatch(uuid, "^[0-9a-fA-F\\-]{36}$")) return new();
        // Konuk özellikleri.
        var c = Calistir(new[] { "guestproperty", "enumerate", uuid, "--patterns", "/VirtualBox/GuestInfo/Net/*/V4/IP" }); return c == null ? new() : IpCoz(c);
    }

    // Makineyi başlatır (pencereli). Başarılı komut gönderildiyse true.
    public static bool Baslat(string uuid)
    {
        // Güvenlik: yalnızca geçerli UUID.
        if (uuid == null || !Regex.IsMatch(uuid, "^[0-9a-fA-F\\-]{36}$")) return false;
        // Başlat.
        var yol = Yol(); if (yol == null) return false;
        // Süreç: komut hemen döner, makine arka planda açılır.
        try { var psi = new ProcessStartInfo(yol) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var a in new[] { "startvm", uuid, "--type", "gui" }) psi.ArgumentList.Add(a); using var p = Process.Start(psi)!; p.StandardOutput.ReadToEndAsync(); p.StandardError.ReadToEndAsync(); return p.WaitForExit(60_000) && p.ExitCode == 0; } catch { return false; }
    }

    // ---- Birleştirme ----
    // Ağ taramasının sonuçlarını sanal makine bilgisiyle birleştirir (saf mantık; test edilebilir).
    public static List<AgSatiri> Birlestir(List<AgCihazi> tarama, List<VBoxVm>? vmler)
    {
        // VirtualBox yoksa olduğu gibi.
        var sonuc = new List<AgSatiri>(); if (vmler == null) { sonuc.AddRange(tarama.Select(c => new AgSatiri(c, null))); return sonuc; }
        // MAC → makine.
        var macHarita = new Dictionary<string, VBoxVm>(); foreach (var v in vmler) foreach (var m in v.Macler) macHarita[m] = v;
        var bulunan = new HashSet<string>();
        // Taranan cihazlar.
        foreach (var c in tarama)
        {
            // Eşleşen makine.
            string mac = (c.Mac ?? "").ToUpperInvariant();
            if (macHarita.TryGetValue(mac, out var vm)) { bulunan.Add(vm.Uuid); sonuc.Add(new AgSatiri(c with { Tur = $"VirtualBox sanal makine ({vm.DurumYazi})", Ad = vm.Ad }, vm)); }
            // Kayıtlı hiçbir makineye ait değilse ve VirtualBox MAC'iyse: DHCP sunucusu / ağ aygıtı olabilir.
            else if (mac.StartsWith("08-00-27", StringComparison.Ordinal) && c.Tur != "Bu bilgisayar (ana makine)") sonuc.Add(new AgSatiri(c with { Tur = "VirtualBox ağ aygıtı (DHCP sunucusu olabilir; sanal makine DEĞİL)" }, null));
            // Diğerleri.
            else sonuc.Add(new AgSatiri(c, null));
        }
        // Ağda görünmeyen kayıtlı makineler: kapalıysa başlatılabilir olarak göster.
        foreach (var v in vmler.Where(v => !bulunan.Contains(v.Uuid)))
        {
            // Durum yazısı.
            string tur = v.Calisiyor ? "VirtualBox sanal makine (Çalışıyor, ağ taramasında görünmüyor)" : $"VirtualBox sanal makine ({v.DurumYazi} — seçerseniz başlatılır)";
            // Satır: IP yok.
            sonuc.Add(new AgSatiri(new AgCihazi("—", tur, v.Ad, v.Macler.Count > 0 ? string.Join(", ", v.Macler) : "—", "VirtualBox (kayıtlı makine)"), v));
        }
        // Sıra: sanal makineler önce.
        return sonuc.OrderBy(s => s.Vm != null ? 0 : 1).ThenBy(s => s.Cihaz.Ip == "—" ? 1 : 0).ToList();
    }
}
