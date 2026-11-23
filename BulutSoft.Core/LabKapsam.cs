// Ağ adreslerini tanımak için gerekli araçları çağır.
using System.Net;
// Adres türünü (IPv4/IPv6) anlamak için gerekli araçları çağır.
using System.Net.Sockets;

// Programımızın adı.
namespace LabDenetim;

// BU DOSYA GÜVENLİK KİLİDİDİR:
// Program yalnızca kendi evinizdeki (laboratuvarınızdaki) adreslere bakabilir.
// İnternetteki başkalarının bilgisayarlarına yanlışlıkla bakılmasını engeller.
public static class LabKapsam
{
    // Verilen adres laboratuvara (kendi ağınıza) ait mi? Evet/Hayır cevabı verir.
    public static bool IpLabdaMi(IPAddress ip)
    {
        // "Kendi bilgisayarım" adresiyse (127.0.0.1) kabul et.
        if (IPAddress.IsLoopback(ip)) return true;
        // Adres yeni tip (IPv6) ise ayrı kurallarla bak.
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            // Yalnızca yerel ağ türü IPv6 adreslerini kabul et.
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        // Adresi 4 sayıya ayır (örn. 192.168.56.101 → 192, 168, 56, 101).
        var b = ip.GetAddressBytes();
        // 10... ile başlıyorsa, 172.16-31 ile başlıyorsa veya 192.168... ile başlıyorsa özel (ev/lab) ağıdır.
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    // Yazılan adresi kontrol eder; laboratuvara aitse sayısal adresini verir, değilse hata verip durdurur.
    public static async Task<IPAddress> DogrulaVeCozAsync(string hedef, CancellationToken ct)
    {
        // Kutu boşsa kullanıcıya hata göster.
        if (string.IsNullOrWhiteSpace(hedef)) throw new ArgumentException("Hedef boş olamaz.");
        // Yazılan zaten sayısal adres ise onu kullan, isim ise rehberden (DNS) numarasını öğren.
        var adresler = IPAddress.TryParse(hedef.Trim(), out var ip)
            ? new[] { ip }
            : await Dns.GetHostAddressesAsync(hedef.Trim(), ct);
        // Hiç adres bulunamadıysa hata ver.
        if (adresler.Length == 0) throw new InvalidOperationException("Hedef çözümlenemedi.");
        // Adreslerden biri bile laboratuvar dışındaysa güvenlik için dur.
        if (!adresler.All(IpLabdaMi))
            throw new InvalidOperationException("Hedef yetkili kapsam dışında (yalnızca 127.0.0.0/8, 10/8, 172.16/12, 192.168/16).");
        // Tercihen klasik (IPv4) adresi ver; yoksa ilk adresi ver.
        return adresler.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? adresler[0];
    }
}
