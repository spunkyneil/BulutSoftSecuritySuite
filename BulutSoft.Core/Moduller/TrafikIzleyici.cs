// Bu dosya bilgisayardan dışarıya (internete / ağdaki başka cihazlara) yapılan CANLI bağlantıları okur:
// hangi program, hangi yerel porttan, hangi hedef adrese ve porta bağlı, durumu ne, güvenli mi?
// Not: .NET'in GetActiveTcpConnections() işlevi bağlantı sahibi programı (PID) vermez. Bu yüzden Windows'un GetExtendedTcpTable
// işlevi kullanılır (PID'yi verir) ve Process.GetProcessById ile program adı bulunur. Sistem çağrısı çalışmazsa GetActiveTcpConnections yedek olarak devreye girer.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// Canlı bir dış bağlantının bilgi kartı.
public record TrafikBaglantisi(int Pid, string Program, string Yol, IPAddress UzakIp, int YerelPort, int UzakPort, string Durum, bool DisAg, Onem Seviye, string Not);

// CANLI AĞ TRAFİĞİ MODÜLÜ.
public static class TrafikIzleyici
{
    // Adres aileleri: IPv4 = 2, IPv6 = 23. Tablo türü 5 = "PID'li tüm TCP bağlantıları".
    private const int AfInet = 2, AfInet6 = 23;
    // Program adlarını kısa süre hatırlayan önbellek (her seferinde yeniden sormamak için).
    private static readonly ConcurrentDictionary<int, (string Ad, string Yol, DateTime Zaman)> Onbellek = new();
    // Şifreli (güvenli) sayılan hedef portlar.
    private static readonly HashSet<int> Guvenli = new() { 443, 8443, 22, 993, 995, 465, 587, 853, 989, 990 };
    // Şifresiz hizmet portları (yalnızca DİKKAT).
    private static readonly HashSet<int> Sifresiz = new() { 80, 8080, 21, 23, 25, 110, 143, 8000 };
    // Kötü amaçlı yazılımların sık kullandığı bilinen portlar (UYARI).
    private static readonly HashSet<int> Supheli = new() { 4444, 1337, 31337, 6667, 12345, 5555, 6666, 9999 };

    // Sık kötüye kullanılan diğer portlar ve nedeni (kesin hüküm değil, dikkat çekme amaçlı).
    private static readonly Dictionary<int, string> OzelSupheli = new()
    {
        [6667] = "IRC sohbet portu: botnet'lerin kumanda kanalı olarak sık kullanılır.", [6668] = "IRC sohbet portu: botnet'lerin kumanda kanalı olarak sık kullanılır.", [6669] = "IRC sohbet portu: botnet'lerin kumanda kanalı olarak sık kullanılır.", [6697] = "IRC sohbet portu: botnet'lerin kumanda kanalı olarak sık kullanılır.",
        [9001] = "Tor ağı portu: trafik gizleniyor olabilir.", [9030] = "Tor ağı portu: trafik gizleniyor olabilir.", [9050] = "Tor yerel vekil portu: trafik gizleniyor olabilir.", [9150] = "Tor tarayıcı vekil portu: trafik gizleniyor olabilir.",
        [3333] = "Kripto para madenciliği (stratum) portu: izinsiz madenci olabilir.", [14444] = "Kripto para madenciliği portu: izinsiz madenci olabilir.", [45560] = "Kripto para madenciliği portu: izinsiz madenci olabilir.", [14433] = "Kripto para madenciliği portu: izinsiz madenci olabilir.",
        [2323] = "Telnet'in alternatif portu: IoT botnet'leri (Mirai türevleri) kullanır."
    };
    // Normalde program bulunmaması gereken klasörler (küçük harf parçaları).
    private static readonly string[] SupheliKlasor = { "\\temp\\", "\\users\\public\\", "\\downloads\\", "\\appdata\\local\\temp" };

    // Programın yolu şüpheli bir klasörde mi?
    public static bool SupheliYolMu(string yol) { var k = (yol ?? "").ToLowerInvariant(); return k.Length > 0 && SupheliKlasor.Any(k.Contains); }

    // Yola da bakan değerlendirme: internete çıkan program geçici/indirilenler klasöründense seviye en az Orta olur.
    public static (Onem Seviye, string Not) Degerlendir(int uzakPort, bool disAg, string yol)
    {
        // Port temelli sonuç.
        var (s, n) = Degerlendir(uzakPort, disAg);
        // Şüpheli klasörden internete çıkış.
        if (disAg && SupheliYolMu(yol)) { n += " Üstelik bu program geçici/indirilenler klasöründen çalışıyor."; if (s < Onem.Orta) s = Onem.Orta; }
        // Sonuç.
        return (s, n);
    }

    // Bağlantının güvenlik seviyesini ve sade notunu belirler.
    public static (Onem Seviye, string Not) Degerlendir(int uzakPort, bool disAg)
    {
        // Ek bilinen şüpheli portlar (özel açıklamalı).
        if (OzelSupheli.TryGetValue(uzakPort, out var ozel)) return (Onem.Orta, $"Şüpheli port ({uzakPort}): {ozel}");
        // Şüpheli bilinen portlar.
        if (Supheli.Contains(uzakPort)) return (Onem.Orta, $"Şüpheli port ({uzakPort}): bazı zararlı yazılımlar bunu kullanır.");
        // Şifreli (güvenli) hizmetler.
        if (Guvenli.Contains(uzakPort)) return (Onem.Bilgi, uzakPort is 443 or 8443 ? "Şifreli bağlantı (HTTPS)" : "Şifreli bağlantı");
        // İnternete çıkan dosya paylaşımı veya uzak masaüstü olağan değildir.
        if (uzakPort is 445 or 139 or 3389 or 5900) return disAg ? (Onem.Orta, "İnternetteki bir adrese dosya paylaşımı/uzak masaüstü bağlantısı: olağan dışı.") : (Onem.Dusuk, "Yerel ağdaki dosya paylaşımı/uzak masaüstü bağlantısı.");
        // Şifresiz hizmetler.
        if (Sifresiz.Contains(uzakPort)) return (Onem.Dusuk, uzakPort is 80 or 8080 ? "Şifresiz bağlantı (HTTP)" : "Şifresiz hizmet");
        // İsim sorgusu.
        if (uzakPort == 53) return (Onem.Bilgi, "İsim çözümleme (DNS)");
        // Diğer her şey alışılmadık sayılır.
        return (Onem.Dusuk, $"Alışılmadık port ({uzakPort})");
    }

    // Bir programın adını ve dosya yolunu bulur (PID'den).
    private static (string Ad, string Yol) Program(int pid)
    {
        // Sistem süreçleri.
        if (pid == 0) return ("Sistem (boşta)", "");
        // PID 4 = Windows çekirdeği.
        if (pid == 4) return ("System", "");
        // Önbellekte 60 saniyeden yeni bilgi varsa onu kullan.
        if (Onbellek.TryGetValue(pid, out var e) && (DateTime.UtcNow - e.Zaman).TotalSeconds < 60) return (e.Ad, e.Yol);
        // Bilgi alınamazsa PID numarası gösterilir.
        string ad = $"PID {pid}", yol = "";
        // Süreç kapanmış veya erişilemez olabilir.
        try
        {
            // Süreci bul.
            using var p = Process.GetProcessById(pid);
            // Program adı.
            ad = p.ProcessName + ".exe";
            // Dosya yolu (yetki yoksa okunamaz).
            try { yol = p.MainModule?.FileName ?? ""; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "TrafikIzleyici.Program"); }
        }
        // Süreç bulunamadı.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "TrafikIzleyici.Program"); }
        // Önbelleğe yaz.
        Onbellek[pid] = (ad, yol, DateTime.UtcNow);
        // Sonucu ver.
        return (ad, yol);
    }

    // Windows'tan PID'li bağlantı tablosunu okur ve ham listeye ekler.
    private static void TabloOku(int aile, List<(int Pid, IPAddress Uzak, int YerelPort, int UzakPort, int Durum)> ham)
    {
        // Tablo Windows'tan okunur (iki çağrı arasında büyürse yeniden denenir; okunamazsa hata kaydedilir).
        TcpTablosu.Oku(aile, tablo =>
        {
            // Satır sayısı ilk 4 bayttadır.
            int adet = Marshal.ReadInt32(tablo);
            // IPv4 satırı 24 bayt, IPv6 satırı 56 bayttır.
            int satirBoyu = aile == AfInet ? 24 : 56;
            // Her satırı oku.
            for (int i = 0; i < adet; i++)
            {
                // Satırın başlangıcı.
                IntPtr s = tablo + 4 + i * satirBoyu;
                // Alanları adres ailesine göre oku.
                int durum, yerelHam, uzakHam, pid; byte[] adres = new byte[aile == AfInet ? 4 : 16];
                // IPv4: durum, yerel adres, yerel port, uzak adres, uzak port, PID.
                if (aile == AfInet) { durum = Marshal.ReadInt32(s, 0); yerelHam = Marshal.ReadInt32(s, 8); Marshal.Copy(s + 12, adres, 0, 4); uzakHam = Marshal.ReadInt32(s, 16); pid = Marshal.ReadInt32(s, 20); }
                // IPv6: yerel adres(16) kapsam(4) port(4) uzak adres(16) kapsam(4) port(4) durum(4) PID(4).
                else { yerelHam = Marshal.ReadInt32(s, 20); Marshal.Copy(s + 24, adres, 0, 16); uzakHam = Marshal.ReadInt32(s, 44); durum = Marshal.ReadInt32(s, 48); pid = Marshal.ReadInt32(s, 52); }
                // Portlar "ağ sırasıyla" saklanır; baytları çevir.
                int yerelPort = ((yerelHam & 0xFF) << 8) | ((yerelHam >> 8) & 0xFF), uzakPort = ((uzakHam & 0xFF) << 8) | ((uzakHam >> 8) & 0xFF);
                // Ham listeye ekle.
                ham.Add((pid, new IPAddress(adres), yerelPort, uzakPort, durum));
            }
        });
    }

    // Canlı dış bağlantıları okur ve değerlendirir. Hata olursa boş liste verir (program bozulmaz).
    public static List<TrafikBaglantisi> Oku()
    {
        // Ham bağlantılar.
        var ham = new List<(int Pid, IPAddress Uzak, int YerelPort, int UzakPort, int Durum)>();
        // Önce PID'li sistem çağrısını dene.
        try { TabloOku(AfInet, ham); TabloOku(AfInet6, ham); } catch { ham.Clear(); }
        // Sonuç listesi.
        var sonuc = new List<TrafikBaglantisi>();
        // Sistem çağrısı bir şey vermediyse .NET'in kendi işlevine geç (PID olmadan).
        if (ham.Count == 0)
        {
            // Yedek yol.
            try
            {
                // Tüm aktif TCP bağlantıları.
                foreach (var c in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections())
                    // PID bilinmediği için 0 yazılır.
                    ham.Add((0, c.RemoteEndPoint.Address, c.LocalEndPoint.Port, c.RemoteEndPoint.Port, c.State == TcpState.Established ? 5 : c.State == TcpState.SynSent ? 3 : 0));
            }
            // Bu da olmazsa boş dön.
            catch { return sonuc; }
        }
        // Her ham bağlantıyı süz ve zenginleştir.
        foreach (var h in ham)
        {
            // Yalnızca canlı bağlantılar: 5 = bağlı, 3 = bağlanıyor.
            if (h.Durum != 5 && h.Durum != 3) continue;
            // Kendi içine (loopback) ve belirsiz adreslere olan bağlantıları atla.
            if (IPAddress.IsLoopback(h.Uzak) || h.Uzak.Equals(IPAddress.Any) || h.Uzak.Equals(IPAddress.IPv6Any) || h.UzakPort == 0) continue;
            // Program adı ve yolu.
            var (ad, yol) = Program(h.Pid);
            // Hedef halka açık internette mi (özel/yerel ağ değilse)?
            bool disAg = !LabKapsam.IpLabdaMi(h.Uzak);
            // Güvenlik seviyesi ve not.
            var (seviye, not) = Degerlendir(h.UzakPort, disAg, yol);
            // Listeye ekle.
            sonuc.Add(new TrafikBaglantisi(h.Pid, ad, yol, h.Uzak, h.YerelPort, h.UzakPort, h.Durum == 5 ? "Bağlı" : "Bağlanıyor", disAg, seviye, not));
        }
        // Programa göre, sonra hedefe göre sırala.
        return sonuc.OrderBy(x => x.Program, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UzakIp.ToString()).ThenBy(x => x.UzakPort).ToList();
    }
}
