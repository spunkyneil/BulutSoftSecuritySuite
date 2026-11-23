// Bu dosya nmap taramasını UZAK SENSÖRDE (ssh + gömülü ajan, sabit komut) çalıştırtır ve sonucu NormalizedScan'e çevirir.
// Sensörde serbest komut çalışmaz: ajan yalnızca sabit nmap bağımsız değişkenlerini kullanır; hedef özel ağ olmalıdır (burada ve ajanda iki kez denetlenir).
using System.Net;
using System.Text.Json;
using System.Xml;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Nmap tarama profili.
internal enum NmapProfili { Hizli, Standart }

// Sensörden nmap.
internal static class SensorNmap
{
    // Profilin ajandaki adı.
    public static string ProfilAdi(NmapProfili p) => p == NmapProfili.Hizli ? "hizli" : "standart";

    // Hedef geçerli mi? Yalnızca özel ağ IPv4 adresi veya /24-/32 aralığı. Hata varsa nedenini verir, yoksa null.
    public static string? HedefHatasi(string? hedef)
    {
        // Boşluk ve boş değer.
        hedef = (hedef ?? "").Trim(); if (hedef.Length == 0 || hedef.Length > 18) return "Hedef boş veya çok uzun.";
        // Aralık biçimi: ip/önek.
        string ipMetni = hedef; int onek = 32;
        int k = hedef.IndexOf('/');
        if (k >= 0) { ipMetni = hedef[..k]; if (!int.TryParse(hedef[(k + 1)..], out onek) || onek is < 24 or > 32) return "Aralık en çok /24 (256 adres) olabilir."; }
        // Adres geçerli ve özel ağda mı?
        if (!IPAddress.TryParse(ipMetni, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || ipMetni.Count(c => c == '.') != 3) return "Hedef geçerli bir IPv4 adresi olmalı (örn. 192.168.56.0/24).";
        var b = ip.GetAddressBytes(); if (b[0] is 127 or 0 || (b[0] == 169 && b[1] == 254)) return "Döngü (127.x) ve bağlantı-yerel adresler taranamaz.";
        if (!SensorGuvenlik.HedefGecerli(ip.ToString())) return "Hedef özel ağda olmalı; internet adreslerine nmap gönderilmez.";
        return null;
    }

    // Sensörde çalışacak nmap komutunun BİREBİR önizlemesi (ajandaki SABİT argüman listesinin aynısı). Serbest argüman girilemez: yalnızca profil ve zamanlama seçilir.
    public static string KomutOnizle(NmapProfili profil, string hedef, int zamanlama = 3)
        => "nmap -sV --version-light -T" + Math.Clamp(zamanlama, 2, 4) + " --max-retries 2 --host-timeout 60s -oX - " + (profil == NmapProfili.Hizli ? "-F" : "--top-ports 1000") + " " + (string.IsNullOrWhiteSpace(hedef) ? "<hedef>" : hedef.Trim());

    // Ajan cevabını (JSON) tarama sonucuna çevirir (ayrı, test edilebilsin).
    internal static NormalizedScan Coz(JsonElement k, DateTime simdiUtc)
    {
        // Sıkıştırılmış XML alanı.
        if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("xmlz", out var z) || z.ValueKind != JsonValueKind.String) throw new InvalidOperationException("Sensör nmap sonucu göndermedi.");
        // Aç ve çöz; bozuk veri anlaşılır hataya çevrilir.
        try { return NmapXml.Parse(NmapXml.Ac(z.GetString() ?? ""), simdiUtc); }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or FormatException) { throw new InvalidOperationException("Sensörden gelen nmap sonucu bozuk: " + ex.Message); }
    }

    // Sensörde nmap çalıştırır. Hedef doğrulanmazsa HİÇBİR ŞEY gönderilmez.
    public static async Task<NormalizedScan> TaraAsync(SensorProfili p, string hedef, NmapProfili profil, CancellationToken ct, int zamanlama = 3)
    {
        if (zamanlama is < 2 or > 4) throw new InvalidOperationException("Zamanlama 2 (yavaş/sessiz), 3 (normal) veya 4 (hızlı) olmalı.");
        // Hedef denetimi.
        var h = HedefHatasi(hedef); if (h != null) throw new InvalidOperationException(h);
        // Ajan.
        var d = await SensorAg.ToplaAsync(p, new { mod = "nmap", hedef = hedef.Trim(), profil = ProfilAdi(profil), zamanlama }, "nmap", ct);
        // Çöz.
        return Coz(d, DateTime.UtcNow);
    }
}
