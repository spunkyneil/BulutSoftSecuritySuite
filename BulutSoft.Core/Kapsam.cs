// Bu dosya "LabKapsam"ın evrimidir: hedefi İKİ KADEMELİ bir güvenlik kapısından geçirir.
//   Mod A  = İç Ağ (Aktif)           : hedef özel/yerel ağdaysa TÜM modüller (kapı tarama, girdi testi, zayıf ayarlar...) çalışır. (Eski davranış aynen korunur.)
//   Mod B  = Dış Ağ (Yalnızca Pasif) : hedef genel internetteyse program reddetmez; "Yasal ve Etik Pasif Gözlem" moduna geçer.
//                                       Bu modda port taraması, form/yük testi (fuzzing), kaba kuvvet ve sistem modülleri KOD SEVİYESİNDE kilitlidir;
//                                       yalnızca standart bir web tarayıcısı gibi GET/HEAD, TLS el sıkışması ve DNS sorgusu yapılır.
//   Reddedildi: karışık (özel + genel) adresler (DNS yeniden bağlama / SSRF riski), ayrılmış adresler, çözülemeyen veya biçimsiz hedefler.
// Ayrıca dış hedeflerde HIZ SINIRI vardır (toplu tarama aracına dönüşmesin): en az 15 sn arayla, saatte en çok 20 gözlem.
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Kapsam modu.
internal enum KapsamModu { IcAg, DisAgPasif, Reddedildi }

// Modül türleri (hangi modül hangi modda çalışabilir tablosu için).
internal enum ModulTuru { PortTarama, ZayifAyar, GirdiTesti, KabaKuvvet, SistemDenetimi, IcHttp, IcTls, IcDns, PasifHttp, PasifTls, PasifDns, SecurityTxt }

// Kilidi aşmaya çalışan çağrı için hata.
internal sealed class KapsamIhlaliException : InvalidOperationException { public KapsamIhlaliException(string m) : base(m) { } }

// Sınıflandırma sonucu.
internal sealed record KapsamKarari(KapsamModu Mod, string Host, IReadOnlyList<IPAddress> Adresler, string Neden);

// Kapsam kapısı.
internal static class Kapsam
{
    // Ad çözücü (testte sahte çözücü verilebilir).
    public static Func<string, CancellationToken, Task<IPAddress[]>> Cozucu { get; set; } = (h, ct) => Dns.GetHostAddressesAsync(h, ct);

    // Hedef metninden ana bilgisayar adını/adresini çıkarır ("https://a.com/x" → "a.com"); geçersizse null.
    public static string? HostCikar(string? hedef)
    {
        // Boşsa yok.
        var s = (hedef ?? "").Trim(); if (s.Length == 0 || s.Length > 300) return null;
        // Adres biçimindeyse ana bilgisayarı al.
        if (s.Contains("://")) { if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https") || u.UserInfo.Length > 0) return null; s = u.Host; }
        // Köşeli parantezli IPv6 temizliği.
        s = s.Trim('[', ']');
        // IP ise olduğu gibi.
        if (IPAddress.TryParse(s, out _)) return s;
        // Alan adı biçimi.
        return s.Length <= 253 && Regex.IsMatch(s, @"^[A-Za-z0-9]([A-Za-z0-9.\-]{0,251}[A-Za-z0-9])?$") ? s.ToLowerInvariant() : null;
    }

    // Adres genel internetten (yönlendirilebilir, herkese açık) mi? Özel, yerel, paylaşımlı (CGNAT), belgeleme, çoklu yayın ve ayrılmış aralıklar DEĞİLDİR.
    public static bool GenelIpMi(IPAddress ip)
    {
        // IPv4'e eşlenmiş IPv6 adresini düz IPv4 gibi değerlendir.
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        // Döngü ve belirsiz adresler.
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None)) return false;
        // Bayt dizisi.
        var b = ip.GetAddressBytes();
        // IPv4 kuralları.
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // 0/8, 10/8, 127/8.
            if (b[0] is 0 or 10 or 127) return false;
            // Paylaşımlı adres alanı 100.64/10.
            if (b[0] == 100 && b[1] is >= 64 and <= 127) return false;
            // Bağlantı-yerel 169.254/16 (bulut meta veri adresi dahil).
            if (b[0] == 169 && b[1] == 254) return false;
            // 172.16/12.
            if (b[0] == 172 && b[1] is >= 16 and <= 31) return false;
            // 192.0.0/24, 192.0.2/24 (belgeleme), 192.168/16.
            if (b[0] == 192 && ((b[1] == 0 && b[2] is 0 or 2) || b[1] == 168)) return false;
            // 198.18/15 (ölçüm), 198.51.100/24 (belgeleme), 203.0.113/24 (belgeleme).
            if (b[0] == 198 && (b[1] is 18 or 19 || (b[1] == 51 && b[2] == 100))) return false; if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false;
            // Çoklu yayın ve ayrılmış (224/4, 240/4).
            return b[0] < 224;
        }
        // IPv6: bağlantı-yerel, site-yerel, çoklu yayın, benzersiz-yerel (fc00::/7), belgeleme (2001:db8::/32), Teredo.
        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (b[0] & 0xFE) == 0xFC) return false;
        if (b[0] == 0x20 && b[1] == 0x01 && ((b[2] == 0x0d && b[3] == 0xb8) || (b[2] == 0x00 && b[3] == 0x00))) return false;
        // Yalnızca küresel tekil adresler (2000::/3).
        return (b[0] & 0xE0) == 0x20;
    }

    // Hedefi sınıflandırır: İç Ağ / Dış Ağ (pasif) / Reddedildi.
    public static async Task<KapsamKarari> SiniflaAsync(string? hedef, CancellationToken ct)
    {
        // Ana bilgisayar.
        var host = HostCikar(hedef); if (host == null) return new(KapsamModu.Reddedildi, hedef ?? "", Array.Empty<IPAddress>(), "Hedef geçerli bir alan adı veya IP adresi değil.");
        // Adresler.
        IPAddress[] adr;
        try { adr = IPAddress.TryParse(host, out var ip) ? new[] { ip } : await Cozucu(host, ct); }
        // Çözülemediyse.
        catch (OperationCanceledException) { throw; } catch { return new(KapsamModu.Reddedildi, host, Array.Empty<IPAddress>(), "Hedef çözümlenemedi (alan adı yok veya DNS erişilemiyor)."); }
        // Hiç adres yoksa.
        if (adr.Length == 0) return new(KapsamModu.Reddedildi, host, adr, "Hedef çözümlenemedi.");
        // Hepsi iç ağ → Mod A (eski davranış).
        if (adr.All(LabKapsam.IpLabdaMi)) return new(KapsamModu.IcAg, host, adr, "Hedef yerel/özel ağda.");
        // Hepsi genel internet → Mod B (yalnızca pasif).
        if (adr.All(GenelIpMi)) return new(KapsamModu.DisAgPasif, host, adr, "Hedef genel internette: yalnızca yasal ve etik PASİF gözlem yapılır.");
        // Karışık veya ayrılmış: güvenlik nedeniyle reddet (DNS yeniden bağlama / iç ağa sızma riski).
        return new(KapsamModu.Reddedildi, host, adr, "Hedef hem özel hem genel adrese çözülüyor veya ayrılmış/yönlendirilemez bir adres kullanıyor; güvenlik için reddedildi.");
    }

    // Bağlantı anında YENİDEN doğrulama: yalnızca genel adresleri verir (gözlem sırasında DNS değişse bile iç ağa gidilemez).
    public static async Task<IPAddress[]> CozGenelAsync(string host, CancellationToken ct)
    {
        // Çöz.
        var adr = IPAddress.TryParse(host, out var ip) ? new[] { ip } : await Cozucu(host, ct);
        // Hepsi genel olmalı.
        if (adr.Length == 0 || !adr.All(GenelIpMi)) throw new KapsamIhlaliException($"\"{host}\" genel internet adresine çözülmüyor; pasif gözlem durduruldu.");
        // Sonuç.
        return adr;
    }

    // ---- Modül kilidi (KOD SEVİYESİNDE) ----
    // Dış ağda yalnızca bu modüller çalışabilir.
    private static readonly HashSet<ModulTuru> PasifIzinli = new() { ModulTuru.PasifHttp, ModulTuru.PasifTls, ModulTuru.PasifDns, ModulTuru.SecurityTxt };

    // Bu modda bu modül çalışabilir mi?
    public static bool Izinli(KapsamModu mod, ModulTuru t) => mod == KapsamModu.IcAg || (mod == KapsamModu.DisAgPasif && PasifIzinli.Contains(t));

    // Kilitliyse KapsamIhlaliException atar (modüller çalışmadan önce çağırır).
    public static void KilitliyseDur(KapsamModu mod, ModulTuru t) { if (!Izinli(mod, t)) throw new KapsamIhlaliException($"{t} modülü bu hedefte (mod: {mod}) KİLİTLİDİR: dış ağda yalnızca pasif gözlem yapılabilir."); }

    // ---- Dış hedef hız sınırı ----
    private static readonly List<DateTime> Gozlemler = new(); private static readonly object Kilit = new();
    // Test için sıfırlar.
    internal static void HizSifirla() { lock (Kilit) Gozlemler.Clear(); }

    // Yeni bir dış gözlem başlatma hakkı var mı? (en az 15 sn ara, saatte en çok 20)
    public static (bool Izinli, string Neden) GozlemHakki(DateTime simdi)
    {
        // Kilit.
        lock (Kilit)
        {
            // Bir saatten eskileri at.
            Gozlemler.RemoveAll(t => t < simdi.AddHours(-1));
            // Çok sık.
            if (Gozlemler.Count > 0 && (simdi - Gozlemler[^1]).TotalSeconds < 15) return (false, $"Dış hedeflere çok sık gözlem yapılamaz; {15 - (int)(simdi - Gozlemler[^1]).TotalSeconds} sn sonra tekrar deneyin.");
            // Saatlik sınır.
            if (Gozlemler.Count >= 20) return (false, "Saatlik dış gözlem sınırına (20) ulaşıldı; program toplu tarama aracı olarak kullanılamaz.");
            // Hak ver ve kaydet.
            Gozlemler.Add(simdi); return (true, "");
        }
    }

    // Devlet/askeri/kritik kurum alan adları için ek uyarı gerekir mi?
    public static bool HassasAlanMi(string host) => Regex.IsMatch(host ?? "", @"(\.|^)(gov|mil)(\.[a-z]{2})?$|\.(edu|k12)\.tr$|\.gov\.tr$|\.bel\.tr$|\.tsk\.tr$", RegexOptions.IgnoreCase);
}
