// Bu dosya bilgisayarınızın bağlı olduğu yerel ağlardaki (VirtualBox / VMware / Wi-Fi / Ethernet) açık makineleri bulur.
// Yalnızca kendi ağ kartlarınızın özel (192.168..., 10..., 172.16...) alt ağlarına bakar; internete çıkmaz.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// Ağda bulunan bir cihazın bilgi kartı.
public record AgCihazi(string Ip, string Tur, string Ad, string Mac, string Ag);

// Bu bilgisayarın bir ağ kartı (adaptör) ve bağlı olduğu alt ağ.
public record AgAdaptoru(string Ad, string Tur, IPAddress Ip, int Onek);

// AĞ KEŞİF MODÜLÜ: ping + ARP ile aktif cihazları bulur.
public static class AgKesfi
{
    // Bilgisayarın açık (çalışan) ve özel IPv4 adresli ağ kartlarını listeler.
    public static List<AgAdaptoru> AdaptorleriBul()
    {
        // Sonuç listesi.
        var liste = new List<AgAdaptoru>();
        // Tüm ağ kartlarını gez.
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            // Çalışmayan kartları atla.
            if (n.OperationalStatus != OperationalStatus.Up) continue;
            // Kendi içine dönen (loopback) ve tünel kartlarını atla.
            if (n.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            // Kartın IP adreslerini gez.
            foreach (var u in n.GetIPProperties().UnicastAddresses)
            {
                // Yalnızca klasik (IPv4) adreslere bak.
                if (u.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                // Güvenlik: yalnızca laboratuvar (özel) adresleri.
                if (!LabKapsam.IpLabdaMi(u.Address)) continue;
                // Kartın adı ve açıklaması: türünü anlamak için.
                string metin = n.Name + " " + n.Description;
                // Kartın türünü belirle.
                string tur =
                    metin.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ? "VirtualBox Host-Only" :
                    metin.Contains("VMware", StringComparison.OrdinalIgnoreCase) ? "VMware (VMnet)" :
                    metin.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) || metin.Contains("vEthernet", StringComparison.OrdinalIgnoreCase) ? "Hyper-V" :
                    n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi (yerel ağ)" : "Ethernet (yerel ağ)";
                // Listeye ekle.
                liste.Add(new AgAdaptoru(n.Name, tur, u.Address, u.PrefixLength));
            }
        }
        // Listeyi geri ver.
        return liste;
    }

    // IP adresini tek bir büyük sayıya çevirir (hesap yapmak için).
    private static uint Say(IPAddress ip) { var b = ip.GetAddressBytes(); return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }

    // Büyük sayıyı yeniden IP adresine çevirir.
    private static IPAddress Adres(uint v) => new(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

    // Verilen ağ kartının alt ağındaki tüm olası cihaz adreslerini verir (en fazla 254; büyük ağlar /24'e daraltılır).
    private static List<IPAddress> Hostlar(IPAddress ip, int onek, out uint ag)
    {
        // Çok büyük ağları (örn. /16) taramak uzun sürer ve gereksizdir; /24'e daralt.
        if (onek < 24) onek = 24;
        // Bu bilgisayarın adresi.
        uint a = Say(ip);
        // Alt ağ maskesi.
        uint maske = 0xFFFFFFFFu << (32 - onek);
        // Ağ adresi (alt ağın ilk adresi).
        ag = a & maske;
        // Yayın (broadcast) adresi (alt ağın son adresi).
        uint yayin = ag | ~maske;
        // Sonuç listesi.
        var liste = new List<IPAddress>();
        // /31 ve /32 gibi çok küçük ağlarda cihaz yoktur.
        if (onek >= 31) return liste;
        // Ağ ve yayın adresleri dışındaki her adresi ekle (kendimiz hariç).
        for (uint h = ag + 1; h < yayin; h++) if (h != a) liste.Add(Adres(h));
        // Listeyi geri ver.
        return liste;
    }

    // MAC adresinin ilk üç baytından cihazın türünü tahmin eder (hangi firmanın kartı?).
    public static string MacTur(string mac)
    {
        // MAC yoksa bilinmiyor.
        if (string.IsNullOrEmpty(mac)) return "Cihaz (MAC okunamadı)";
        // İlk üç baytı al (örn. 08-00-27).
        string on = mac.Length >= 8 ? mac[..8] : mac;
        // Bilinen sanallaştırma üreticilerini tanı.
        return on switch
        {
            // VirtualBox sanal makine (Kali burada genelde 08-00-27 ile başlar).
            "08-00-27" => "VirtualBox sanal makine",
            // VirtualBox ana makine adaptörü.
            "0A-00-27" => "VirtualBox Host-Only adaptörü",
            // VMware sanal makine.
            "00-0C-29" or "00-50-56" or "00-05-69" or "00-1C-14" => "VMware sanal makine",
            // Hyper-V sanal makine.
            "00-15-5D" => "Hyper-V sanal makine",
            // QEMU / KVM sanal makine.
            "52-54-00" => "QEMU/KVM sanal makine",
            // Parallels sanal makine.
            "00-1C-42" => "Parallels sanal makine",
            // Diğer her şey.
            _ => "Ağ cihazı"
        };
    }

    // Ping'e cevap vermeyen bir adresin gerçekten canlı olup olmadığını yaygın kapılara kısa bağlantı denemesiyle anlar.
    // Bağlantı kurulursa VEYA "reddedildi" (RST) cevabı gelirse cihaz canlıdır; hiç cevap yoksa eski bir ARP kaydı sayılır.
    internal static async Task<bool> TcpYasiyor(string ip, CancellationToken ct)
    {
        // Yaygın kapılar.
        var gorevler = new[] { 445, 135, 139, 22, 80, 443, 3389, 5900, 8080 }.Select(async port =>
        {
            // Bağlantı denemesi.
            try
            {
                // İstemci ve bağlanma.
                using var c = new TcpClient(); var t = c.ConnectAsync(ip, port, ct).AsTask();
                // 300 ms içinde sonuç gelmezse yok say.
                if (await Task.WhenAny(t, Task.Delay(300, ct)) != t) return false;
                // Bağlandıysa canlı.
                if (t.IsCompletedSuccessfully) return true;
                // Reddedildiyse (RST) yine canlı.
                return t.Exception?.GetBaseException() is SocketException se && se.SocketErrorCode == SocketError.ConnectionRefused;
            }
            // Diğer hatalar: cevap yok.
            catch { return false; }
        });
        // Herhangi biri canlıysa canlı.
        return (await Task.WhenAll(gorevler)).Any(x => x);
    }

    // Windows'un "ARP tablosunu" (kimin MAC adresi ne) okur. ARP, ping'e cevap vermeyen cihazları bile yakalar.
    private static Dictionary<string, string> ArpOku()
    {
        // Sonuç: IP -> MAC.
        var sonuc = new Dictionary<string, string>();
        // Okuma başarısız olursa boş döneriz.
        try
        {
            // "arp -a" komutunu görünmez pencerede çalıştır (yönetici izni gerekmez).
            using var p = Process.Start(new ProcessStartInfo("arp", "-a") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            // Başlatılamadıysa boş dön.
            if (p == null) return sonuc;
            // Çıktıyı oku.
            string cikti = p.StandardOutput.ReadToEnd();
            // Bitmesini en fazla 3 saniye bekle.
            p.WaitForExit(3000);
            // Her satırdan IP, MAC adresini yakala.
            foreach (Match m in Regex.Matches(cikti, @"(\d{1,3}(?:\.\d{1,3}){3})\s+((?:[0-9a-fA-F]{2}-){5}[0-9a-fA-F]{2})\s+\w+"))
            {
                // MAC adresini büyük harfe çevir.
                string mac = m.Groups[2].Value.ToUpperInvariant();
                // Yayın ve çoklu yayın adreslerini atla.
                if (mac == "FF-FF-FF-FF-FF-FF" || mac.StartsWith("01-00-5E", StringComparison.Ordinal)) continue;
                // Sözlüğe yaz.
                sonuc[m.Groups[1].Value] = mac;
            }
        }
        // Hata olursa sessizce geç.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "AgKesfi.ArpOku"); }
        // Sonucu geri ver.
        return sonuc;
    }

    // IP adresinden bilgisayar adını öğrenmeye çalışır (en fazla 1 saniye bekler).
    private static async Task<string> AdCoz(string ip)
    {
        // Sorgu başarısız olabilir.
        try
        {
            // Ters isim sorgusunu başlat.
            var gorev = Dns.GetHostEntryAsync(ip);
            // 1 saniyede cevap gelirse adı al.
            if (await Task.WhenAny(gorev, Task.Delay(1000)) == gorev)
            {
                // Bulunan ad.
                string ad = (await gorev).HostName;
                // Ad IP'nin kendisi değilse ver.
                if (!string.IsNullOrWhiteSpace(ad) && ad != ip) return ad;
            }
        }
        // Hata olursa ad yok say.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "AgKesfi.AdCoz"); }
        // Ad bulunamadı.
        return "—";
    }

    // Tüm yerel ağları tarar ve bulunan cihazları verir.
    public static async Task<List<AgCihazi>> TaraAsync(IProgress<string> durum, IProgress<int> yuzde, CancellationToken ct)
    {
        // Ağ kartlarını bul.
        var adaptorler = AdaptorleriBul();
        // Bu bilgisayarın kendi adresleri.
        var kendi = adaptorler.Select(a => a.Ip.ToString()).ToHashSet();
        // Aynı alt ağı iki kez taramamak için tekilleştir (sanal kartlar öne alınır).
        var aglar = new List<(AgAdaptoru Kart, uint Ag, int Onek, List<IPAddress> Hostlar)>();
        // Kartları sırala: sanal olanlar önce.
        foreach (var a in adaptorler.OrderBy(x => x.Tur.StartsWith("Wi-Fi", StringComparison.Ordinal) || x.Tur.StartsWith("Ethernet", StringComparison.Ordinal) ? 1 : 0))
        {
            // Alt ağın adreslerini hesapla.
            var hostlar = Hostlar(a.Ip, a.Onek, out uint ag);
            // Aynı ağ zaten listedeyse atla.
            if (aglar.Any(x => x.Ag == ag && x.Onek == Math.Max(24, a.Onek))) continue;
            // Yeni ağı ekle.
            aglar.Add((a, ag, Math.Max(24, a.Onek), hostlar));
        }
        // Taranacak toplam adres sayısı (yüzde hesabı için).
        int toplam = Math.Max(1, aglar.Sum(x => x.Hostlar.Count));
        // Şimdiye kadar yoklanan adres sayısı.
        int bitti = 0;
        // Ping cevap veren adresler.
        var yanit = new ConcurrentBag<string>();
        // Her ağı sırayla tara.
        foreach (var g in aglar)
        {
            // Kullanıcı durdurduysa çık.
            ct.ThrowIfCancellationRequested();
            // Durum yazısı.
            durum.Report($"{g.Kart.Tur} ağı taranıyor: {Adres(g.Ag)}/{g.Onek} ({g.Hostlar.Count} adres)...");
            // Aynı anda en fazla 96 ping gönder.
            await Parallel.ForEachAsync(g.Hostlar, new ParallelOptions { MaxDegreeOfParallelism = 96, CancellationToken = ct }, async (h, t) =>
            {
                // Ping başarısız olabilir.
                try
                {
                    // Ping aracı.
                    using var ping = new Ping();
                    // 450 milisaniye içinde cevap bekle.
                    var r = await ping.SendPingAsync(h, 450);
                    // Cevap geldiyse listeye ekle.
                    if (r.Status == IPStatus.Success) yanit.Add(h.ToString());
                }
                // Cevap yok: sorun değil.
                catch (Exception hk3) { HataKayit.Sessiz(hk3, "AgKesfi.TaraAsync"); }
                // İlerlemeyi bildir.
                yuzde.Report(Interlocked.Increment(ref bitti) * 100 / toplam);
            });
        }
        // ARP tablosunu oku (ping'e cevap vermeyen ama ağda olan cihazları yakalar).
        durum.Report("Cihaz adresleri (ARP) okunuyor...");
        // ARP sözlüğü.
        var arp = ArpOku();
        // Bulunan cihazlar.
        var cihazlar = new List<AgCihazi>();
        // Cihaz bilgilerini toplamak için geçici liste.
        var adaylar = new List<(string Ip, string Mac, AgAdaptoru Kart)>();
        // Her ağ için adayları belirle.
        foreach (var g in aglar)
        {
            // Bu ağın adres kümesi.
            var adresKumesi = g.Hostlar.Select(h => h.ToString()).ToHashSet();
            // Ping yanıtı verenler kesin canlıdır.
            foreach (var ip in adresKumesi.Where(ip => yanit.Contains(ip)))
                // Aday olarak ekle.
                adaylar.Add((ip, arp.GetValueOrDefault(ip, ""), g.Kart));
            // Yalnızca ARP'de görünenler ESKİ kayıt (kapanmış makinenin hayaleti) olabilir: gerçekten canlı mı diye kısa bir kapı yoklamasıyla doğrula.
            var yalnizArp = adresKumesi.Where(ip => !yanit.Contains(ip) && arp.ContainsKey(ip)).ToList();
            var canli = await Task.WhenAll(yalnizArp.Select(ip => TcpYasiyor(ip, ct)));
            // Canlı çıkanları ekle; çıkmayanlar listede gösterilmez.
            for (int k = 0; k < yalnizArp.Count; k++) if (canli[k]) adaylar.Add((yalnizArp[k], arp[yalnizArp[k]], g.Kart));
            // Bu bilgisayarın kendi adresini de ekle (sanal ağdaki ana makine adresi).
            adaylar.Add((g.Kart.Ip.ToString(), "", g.Kart));
        }
        // Adları öğren (hepsi aynı anda).
        durum.Report($"{adaylar.Count} cihazın adı öğreniliyor...");
        // Ad sorgularını başlat.
        var adGorevleri = adaylar.Select(a => AdCoz(a.Ip)).ToList();
        // Hepsinin bitmesini bekle.
        var adlar = await Task.WhenAll(adGorevleri);
        // Sonuç kartlarını oluştur.
        for (int i = 0; i < adaylar.Count; i++)
        {
            // Aday bilgisi.
            var a = adaylar[i];
            // Bu bilgisayarın kendisi mi?
            bool bu = kendi.Contains(a.Ip);
            // Cihaz türü.
            string tur = bu ? "Bu bilgisayar (ana makine)" : MacTur(a.Mac);
            // Kartı ekle.
            cihazlar.Add(new AgCihazi(a.Ip, tur, adlar[i], string.IsNullOrEmpty(a.Mac) ? "—" : a.Mac, $"{a.Kart.Tur} · {a.Kart.Ad}"));
        }
        // Sırala: önce sanal makineler, sonra IP sırasına göre.
        return cihazlar.OrderBy(c => c.Tur.Contains("sanal makine") ? 0 : 1).ThenBy(c => Say(IPAddress.Parse(c.Ip))).ToList();
    }
}
