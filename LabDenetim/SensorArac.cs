// Bu dosya Kali sensöründe çalıştırılabilecek YASAL, SAVUNMA/DENETİM amaçlı araçların kapısıdır (sslscan, ssh-audit, nikto, searchsploit, whois).
// Güvenlik ilkesi: sensörde serbest komut çalışmaz. Her aracın argüman listesi ajanda SABİTTİR; kullanıcı yalnızca HEDEFİ yazar ve hedef burada VE ajanda doğrulanır.
//   • sslscan / ssh-audit / nikto  : hedef yalnızca ÖZEL AĞ IPv4 adresi (isteğe bağlı :kapı). Nikto yalnızca zararsız sondalarla çalışır (-Tuning 123b: ilginç dosya, yanlış ayar, bilgi sızması, yazılım tanıma).
//   • searchsploit                 : yalnızca YEREL veri tabanında arama yapar; HİÇBİR exploit çalıştırılmaz ve indirilmez.
//   • whois                        : herkese açık alan adı kayıt sorgusu (internete çıkar; kullanıcı onayı ister).
// Parola kırma, exploit çalıştırma, araya girme (MITM), kablosuz saldırı ve yük bindirme araçları BİLEREK yoktur.
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Çalıştırılabilen araçlar.
internal enum AracTuru { Sslscan, SshAudit, Nikto, Searchsploit, Whois }

// Bir aracın sonucu.
internal sealed record AracSonucu(AracTuru Arac, string Hedef, string Komut, string Cikti);

// Araç kapısı.
internal static class SensorArac
{
    // Aracın ajandaki adı.
    public static string Ad(AracTuru t) => t switch { AracTuru.Sslscan => "sslscan", AracTuru.SshAudit => "ssh-audit", AracTuru.Nikto => "nikto", AracTuru.Searchsploit => "searchsploit", _ => "whois" };

    // Kolay anlatım: (başlık, ne yapar, hedef örneği).
    public static (string Baslik, string Aciklama, string Ornek) Bilgi(AracTuru t) => t switch
    {
        AracTuru.Sslscan => ("TLS/SSL Denetimi (sslscan)", "Bir sunucunun hangi şifreleme sürümlerini ve şifreleri desteklediğini, sertifikasını ve zayıf ayarları listeler. Yalnızca özel ağdaki sunucular.", "192.168.56.10:443"),
        AracTuru.SshAudit => ("SSH Ayar Denetimi (ssh-audit)", "SSH sunucusunun yapılandırmasını okur ve eski/zayıf algoritmaları gösterir. Şifre denemez, giriş yapmaz. Yalnızca özel ağ.", "192.168.56.10:22"),
        AracTuru.Nikto => ("Web Sunucusu Denetimi (nikto)", "Web sunucusundaki unutulmuş dosyaları, yanlış ayarları, bilgi sızıntılarını ve eski yazılım sürümlerini arar. Yalnızca zararsız sondalar (saldırı/yük testi yok), en çok 2,5 dakika. Yalnızca özel ağ.", "192.168.56.10:80"),
        AracTuru.Searchsploit => ("Bilinen Açık Arama (searchsploit)", "Bir yazılım adı ve sürümü için yerel veri tabanında bilinen güvenlik açığı kayıtlarını listeler. Hiçbir exploit çalıştırılmaz; yalnızca kayıt listelenir.", "apache 2.4.49"),
        _ => ("Alan Adı Kaydı (whois)", "Alan adının kime kayıtlı olduğunu, kayıt/bitiş tarihini ve DNS sunucularını gösterir. Herkese açık bir sorgudur (internete çıkar).", "example.com")
    };

    // Hedef bu araç için geçerli mi? Hata varsa nedenini verir, yoksa null.
    public static string? HedefHatasi(AracTuru t, string? hedef)
    {
        // Boşluk ve uzunluk.
        hedef = (hedef ?? "").Trim(); if (hedef.Length == 0 || hedef.Length > 80) return "Hedef boş veya çok uzun.";
        // Aramaya özel: yalnızca harf-rakam-boşluk-nokta-tire-alt çizgi; ilk karakter harf/rakam; hiçbir sözcük '-' ile başlamaz (bayrak sızdırma engeli).
        if (t == AracTuru.Searchsploit) return Regex.IsMatch(hedef, @"^[A-Za-z0-9][A-Za-z0-9 ._-]{1,59}$") && !hedef.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(x => x.StartsWith('-')) ? null : "Arama metni yalnızca harf, rakam, boşluk, nokta ve tire içerebilir (örn. apache 2.4.49).";
        // Alan adı: nokta içerir, IP değildir, '-' ile başlamaz.
        if (t == AracTuru.Whois) return SensorAg.AdGecerli(hedef) && hedef.Contains('.') && !hedef.StartsWith('-') && !Regex.IsMatch(hedef, @"^[0-9.]+$") ? null : "Geçerli bir alan adı yazın (örn. example.com).";
        // Diğerleri: özel ağ IPv4 + isteğe bağlı kapı.
        string ip = hedef; int kapi = 0; int k = hedef.IndexOf(':');
        if (k >= 0) { ip = hedef[..k]; if (!int.TryParse(hedef[(k + 1)..], out kapi) || kapi is < 1 or > 65535) return "Kapı numarası 1-65535 arasında olmalı."; }
        if (!System.Net.IPAddress.TryParse(ip, out var a) || a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || ip.Count(c => c == '.') != 3) return "Hedef bir IPv4 adresi olmalı (örn. 192.168.56.10 veya 192.168.56.10:443).";
        var b = a.GetAddressBytes(); if (b[0] is 127 or 0 || (b[0] == 169 && b[1] == 254)) return "Döngü (127.x) ve bağlantı-yerel adresler denetlenemez.";
        return SensorGuvenlik.HedefGecerli(a.ToString()) ? null : "Hedef özel ağda olmalı; internet adreslerine bu araç gönderilmez.";
    }

    // Internete çıkan araç mı? (onay metni için)
    public static bool InternetKullanir(AracTuru t) => t == AracTuru.Whois;

    // Sensörde çalışacak komutun BİREBİR önizlemesi (ajandaki sabit argüman listesinin aynısı).
    public static string KomutOnizle(AracTuru t, string hedef)
    {
        // Hedef parçaları.
        string h = (hedef ?? "").Trim(); if (h.Length == 0) h = "<hedef>"; string ip = h.Split(':')[0]; string? kp = h.Contains(':') ? h.Split(':')[1] : null;
        return t switch
        {
            AracTuru.Searchsploit => "searchsploit --disable-colour " + h,
            AracTuru.Whois => "whois " + h,
            AracTuru.Sslscan => "sslscan --no-colour " + ip + ":" + (kp ?? "443"),
            AracTuru.SshAudit => "ssh-audit -p " + (kp ?? "22") + " " + ip,
            _ => "nikto -h " + ip + " -p " + (kp ?? "80") + " -Tuning 123b -maxtime 150s -nointeractive -ask no" + (kp is "443" or "8443" ? " -ssl" : "")
        };
    }

    // Ajan cevabını çözer (ayrı, test edilebilsin). Çıktı güvenilmez kabul edilir: denetim karakterleri temizlenir, uzunluk sınırlanır.
    internal static AracSonucu Coz(JsonElement k)
    {
        // Alan okuyucu.
        string S(string ad, int en) => k.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? Temizle(v.GetString() ?? "", en) : "";
        if (k.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Sensör araç sonucu göndermedi.");
        string ad = S("arac", 20); var t = Enum.GetValues<AracTuru>().FirstOrDefault(x => Ad(x) == ad, AracTuru.Whois);
        return new AracSonucu(t, S("hedef", 80), S("komut", 300), S("cikti", 40000));
    }

    // Satır sonları korunur, diğer denetim karakterleri (ESC dahil) boşluğa çevrilir.
    internal static string Temizle(string s, int en) { var c = s.Select(x => x is '\n' or '\r' or '\t' || !char.IsControl(x) ? x : ' ').ToArray(); var r = new string(c); return r.Length > en ? r[..en] + "\n… (kısaltıldı)" : r; }

    // Aracı sensörde çalıştırır. Hedef doğrulanmazsa HİÇBİR ŞEY gönderilmez.
    public static async Task<AracSonucu> CalistirAsync(SensorProfili p, AracTuru t, string hedef, CancellationToken ct)
    {
        // Doğrulama.
        var h = HedefHatasi(t, hedef); if (h != null) throw new InvalidOperationException(h);
        // Ajan.
        var d = await SensorAg.ToplaAsync(p, new { mod = "arac", arac = Ad(t), hedef = hedef.Trim() }, "arac", ct);
        return Coz(d);
    }
}
