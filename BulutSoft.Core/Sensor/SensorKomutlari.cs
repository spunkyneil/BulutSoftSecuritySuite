using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Sensörde çalıştırılacak ONAYLI komutların ve ssh argümanlarının üretimi (serbest komut yok; her argüman ayrı).
internal static class SensorKomutlari
{
    // Sensörde çalışan ÖN KONTROL komutu: sabit metin, kullanıcı girdisi içermez.
    public const string OnKontrolKomutu = "echo BS-SENSOR-OK; uname -sr; id -un; if command -v python3 >/dev/null 2>&1; then echo PY-OK; else echo PY-YOK; fi";

    // Test sırasında ssh.exe yerine başka bir program kullanmak için kanca.
    public static string? SshYoluGecici { get; set; }

    // ssh.exe yolu: Windows'un kendi OpenSSH'ı (Git'in kopyasıyla karışmasın).
    public static string SshYolu => SshYoluGecici ?? (File.Exists(@"C:\Windows\System32\OpenSSH\ssh.exe") ? @"C:\Windows\System32\OpenSSH\ssh.exe" : "ssh");

    // ssh argüman listesini üretir (kabuk metni birleştirmesi yok; her argüman ayrı).
    // parolali: anahtar dosyası parolalı ve parola SSH_ASKPASS ile (yalnızca çocuk sürecin belleğinde) verilecekse true; BatchMode bu durumda kapatılır (askpass çalışsın).
    public static List<string> ArgumanOlustur(SensorProfili p, string uzakKomut, bool parolali = false) => new()
    {
        "-i", p.AnahtarYolu, "-p", p.Port.ToString(CultureInfo.InvariantCulture),
        // Parola sorma, yalnızca anahtar; yalnızca bu anahtar denensin.
        "-o", "BatchMode=" + (parolali ? "no" : "yes"), "-o", "PasswordAuthentication=no", "-o", "PreferredAuthentications=publickey", "-o", "IdentitiesOnly=yes",
        // Parmak izi HER ZAMAN katı ("yes"): "accept-new" kaldırıldı. Bilinmeyen sunucunun parmak izi bağlantıdan önce kullanıcıya onaylatılıp known_hosts'a yazılır (ParmakIziDenetleAsync).
        "-o", "StrictHostKeyChecking=yes", "-o", "UserKnownHostsFile=" + SensorSunucuParmakIzi.BilinenSunucular,
        // Zaman aşımları, terminal yok, temiz çıktı.
        "-o", "ConnectTimeout=8", "-o", "ServerAliveInterval=10", "-o", "ServerAliveCountMax=3", "-o", "LogLevel=ERROR", "-T",
        p.Kullanici + "@" + p.Host, uzakKomut
    };

    // Ajanı "tcp" kipinde çalıştıracak uzak komutu üretir (girdiler doğrulanır).
    public static string TcpKomutu(string hedefIp, string kapilar)
    {
        // Doğrulama: geçersizse hiç komut üretilmez.
        if (!SensorGuvenlik.HedefAraligiGecerli(hedefIp)) throw new ArgumentException("Hedef özel ağdan bir IPv4 adresi veya en çok /24 aralığı olmalı (örn. 192.168.56.101 veya 192.168.56.0/24).");
        if (!SensorGuvenlik.KapilarGecerli(kapilar)) throw new ArgumentException("Kapı listesi geçersiz (örn. 22,80,443 veya 1-1024).");
        // Güvenli: yalnızca rakam, nokta, virgül ve tire içerir.
        return $"python3 - tcp {hedefIp} {kapilar}";
    }

    // Ajanı "hello" kipinde çalıştıracak komut.
    public const string HelloKomutu = "python3 - hello";
}
