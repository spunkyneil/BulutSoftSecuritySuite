// Uzak sensör GİRDİ DOĞRULAMASI: profil, hedef aralığı ve kapı listesi kuralları (saf mantık; ağa/sürece dokunmaz).
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Girişleri doğrulayan kurallar (komut enjeksiyonunu ve yanlış hedefi önler).
internal static class SensorGuvenlik
{
    // Sensör adı: harf, rakam, boşluk, tire, alt çizgi.
    public static bool AdGecerli(string s) => Regex.IsMatch(s ?? "", @"^[\p{L}0-9 _\-]{1,40}$");

    // Sensörün kendi adresi: yalnızca özel ağ/yerel IPv4 (örn. VirtualBox 192.168.56.x). Ad (DNS) kabul edilmez; yanlış yere bağlanma riskini keser.
    public static bool HostGecerli(string s) => Ipv4Mi(s, out var ip) && LabKapsam.IpLabdaMi(ip);

    // Kullanıcı adı: küçük harfle başlayan Linux kullanıcı adı ("-" ile başlayamaz; ssh seçeneği gibi yorumlanmasın).
    public static bool KullaniciGecerli(string s) => Regex.IsMatch(s ?? "", @"^[a-z_][a-z0-9_\-]{0,31}$");

    // Kapı numarası.
    public static bool PortGecerli(int p) => p is >= 1 and <= 65535;

    // Hedef IP: yalnızca özel ağ IPv4 (internetteki başkalarını taramayı engeller).
    public static bool HedefGecerli(string s) => Ipv4Mi(s, out var ip) && LabKapsam.IpLabdaMi(ip);

    // Tarama hedefi: tek özel ağ IPv4 adresi VEYA "ip/önek" aralığı (önek 24-32; en çok 256 adres).
    public static bool HedefAraligiGecerli(string s)
    {
        // Boş değer.
        if (string.IsNullOrEmpty(s)) return false;
        // Aralık değilse tek adres kuralı.
        int k = s.IndexOf('/'); if (k < 0) return HedefGecerli(s);
        // Önek 24-32 arasında bir sayı olmalı; adres özel ağda olmalı.
        string onek = s[(k + 1)..]; return Regex.IsMatch(onek, @"^\d{2}$") && int.Parse(onek, CultureInfo.InvariantCulture) is >= 24 and <= 32 && HedefGecerli(s[..k]);
    }

    // Kapı listesi: "80", "1-1024", "22,80,443,8000-8100" gibi; en fazla 20000 kapı.
    public static bool KapilarGecerli(string s)
    {
        // Biçim.
        if (string.IsNullOrEmpty(s) || s.Length > 120 || !Regex.IsMatch(s, @"^\d{1,5}(-\d{1,5})?(,\d{1,5}(-\d{1,5})?)*$")) return false;
        // Toplam kapı sayısı ve sınırlar.
        long toplam = 0;
        // Her parça.
        foreach (var parca in s.Split(','))
        {
            // Alt ve üst.
            var p = parca.Split('-'); int a = int.Parse(p[0], CultureInfo.InvariantCulture), b = p.Length > 1 ? int.Parse(p[1], CultureInfo.InvariantCulture) : a;
            // Sınır kontrolü.
            if (a < 1 || b > 65535 || a > b) return false; toplam += b - a + 1;
        }
        // Üst sınır.
        return toplam <= 20000;
    }

    // Tam olarak "sayı.sayı.sayı.sayı" biçiminde IPv4 mü?
    private static bool Ipv4Mi(string s, out IPAddress ip)
    {
        // Başlangıç.
        ip = IPAddress.None;
        // Biçim ve ayrıştırma.
        return s != null && Regex.IsMatch(s, @"^\d{1,3}(\.\d{1,3}){3}$") && IPAddress.TryParse(s, out ip!);
    }

    // Tüm profil kuralları sağlanıyor mu? Sağlanmıyorsa nedenini verir.
    public static string? ProfilHatasi(SensorProfili p)
    {
        // Sırayla kontrol et.
        if (!AdGecerli(p.Ad)) return "Sensör adı 1-40 karakter olmalı (harf, rakam, boşluk, - veya _).";
        if (!HostGecerli(p.Host)) return "Sensör adresi özel ağdan bir IPv4 olmalı (örn. 192.168.56.101).";
        if (!PortGecerli(p.Port)) return "SSH kapısı 1-65535 arasında olmalı (genelde 22).";
        if (!KullaniciGecerli(p.Kullanici)) return "Kullanıcı adı küçük harf/rakam/_/- içermeli (örn. kali).";
        if (string.IsNullOrWhiteSpace(p.AnahtarYolu) || !File.Exists(p.AnahtarYolu)) return "Anahtar dosyası bulunamadı. \"Anahtar Üret\" düğmesini kullanın.";
        // Sorun yok.
        return null;
    }
}

// Sensörden gelen tek bir JSON satırının çözülmüş hâli.
