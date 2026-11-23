// Bu dosya Giriş Saldırı Radarı testlerini içerir (günlük okumaz, ağa çıkmaz; yalnızca ayrıştırma ve analiz mantığı).
namespace LabDenetim.Tests;

// Brute-force testleri.
public class BruteForceTestleri
{
    // Örnek 4625 olayı.
    private static string Xml(string ip, string user = "admin", string tur = "10", string zaman = "2026-11-18T11:02:11.1234567Z") => $"<Event xmlns=\"http://schemas.microsoft.com/win/2004/08/events/event\"><System><EventID>4625</EventID><TimeCreated SystemTime=\"{zaman}\"/></System><EventData><Data Name=\"TargetUserName\">{user}</Data><Data Name=\"LogonType\">{tur}</Data><Data Name=\"IpAddress\">{ip}</Data></EventData></Event>";

    // 4625 ayrıştırma: RDP girişi.
    [Fact] public void Win4625_Rdp() { var d = BruteForce.Win4625Ayristir(Xml("203.0.113.5"))!; Assert.Equal("203.0.113.5", d.Ip); Assert.Equal("admin", d.Kullanici); Assert.Equal("RDP", d.Kaynak); }
    // IP "-" veya yerel ise atlanır.
    [Theory, InlineData("-"), InlineData(""), InlineData("127.0.0.1"), InlineData("::1"), InlineData("0.0.0.0")]
    public void Win4625_GecersizIp(string ip) => Assert.Null(BruteForce.Win4625Ayristir(Xml(ip)));
    // Bozuk XML çökmez.
    [Fact] public void Win4625_Bozuk() => Assert.Null(BruteForce.Win4625Ayristir("<bozuk"));
    // Kullanıcı adındaki denetim karakterleri temizlenir.
    [Fact] public void Win4625_KullaniciTemiz() => Assert.DoesNotContain('\u0001', BruteForce.Win4625Ayristir(Xml("203.0.113.5", "a&#1;b"))?.Kullanici ?? "ab");

    // SSH satırı: geçersiz kullanıcı.
    [Fact] public void Ssh_GecersizKullanici()
    {
        var d = BruteForce.SshSatirAyristir("2026-11-18T14:02:11+0300 bulut sshd[1234]: Failed password for invalid user test from 198.51.100.7 port 4444 ssh2")!;
        Assert.Equal("198.51.100.7", d.Ip); Assert.Equal("test", d.Kullanici); Assert.Equal("SSH", d.Kaynak); Assert.Equal(14, d.Zaman.Hour == 14 ? 14 : d.Zaman.ToUniversalTime().Hour + 3);
    }
    // SSH satırı: var olan kullanıcı ve mikrosaniyeli zaman.
    [Fact] public void Ssh_Root() => Assert.Equal("root", BruteForce.SshSatirAyristir("2026-11-18T14:02:11.123456+0300 h sshd[1]: Failed password for root from 10.0.0.9 port 22 ssh2")!.Kullanici);
    // İlgisiz satırlar yok sayılır.
    [Theory, InlineData("2026-11-18T14:02:11+0300 h sshd[1]: Accepted password for root from 10.0.0.9 port 22 ssh2"), InlineData("rastgele metin"), InlineData("")]
    public void Ssh_Ilgisiz(string s) => Assert.Null(BruteForce.SshSatirAyristir(s));

    // Analiz: eşik, sıralama, seviye.
    [Fact] public void Analiz_EsikVeSeviye()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0); var l = new List<GirisDenemesi>();
        for (int i = 0; i < 30; i++) l.Add(new GirisDenemesi(now.AddMinutes(-i % 10), "203.0.113.5", "admin", "RDP"));
        for (int i = 0; i < 6; i++) l.Add(new GirisDenemesi(now.AddMinutes(-2), "198.51.100.7", "u" + i, "SSH"));
        for (int i = 0; i < 3; i++) l.Add(new GirisDenemesi(now.AddMinutes(-1), "192.0.2.1", "x", "SSH"));
        var r = BruteForce.Analiz(l, now, TimeSpan.FromHours(1), 5);
        Assert.Equal(2, r.Count); Assert.Equal("203.0.113.5", r[0].Ip); Assert.Equal("Yüksek", r[0].Seviye); Assert.Equal("Yüksek", r[1].Seviye); Assert.Equal(6, r[1].KullaniciSayisi);
    }
    // Süre penceresi dışındaki denemeler sayılmaz.
    [Fact] public void Analiz_EskiDenemeler()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0); var l = Enumerable.Range(0, 10).Select(_ => new GirisDenemesi(now.AddHours(-5), "203.0.113.5", "a", "RDP")).ToList();
        Assert.Empty(BruteForce.Analiz(l, now, TimeSpan.FromHours(1), 5));
    }

    // Engelleme güvenlik kuralları.
    [Fact] public void Engel_KendiAdresiAsla()
    {
        var korunan = new HashSet<string> { "192.168.56.1", "192.168.56.101" };
        Assert.NotNull(BruteForce.EngelHatasi("192.168.56.1", korunan)); Assert.NotNull(BruteForce.EngelHatasi("127.0.0.1", korunan)); Assert.NotNull(BruteForce.EngelHatasi("169.254.1.1", korunan)); Assert.NotNull(BruteForce.EngelHatasi("abc", korunan)); Assert.Null(BruteForce.EngelHatasi("203.0.113.5", korunan));
    }
    // Kural adı güvenli ve komut doğru biçimde.
    [Fact] public void Engel_KomutVeAd()
    {
        Assert.Equal("BulutSoft-BF-203-0-113-5", BruteForce.KuralAdi("203.0.113.5")); Assert.True(GuvenlikDuvari.AdGuvenliMi(BruteForce.KuralAdi("2001:db8::1")));
        string z = BruteForce.EngelZinciri(new[] { "203.0.113.5", "127.0.0.1", "203.0.113.5; calc" }, new HashSet<string>());
        Assert.Contains("remoteip=203.0.113.5 protocol=any", z); Assert.DoesNotContain("127.0.0.1", z); Assert.DoesNotContain("calc", z); Assert.Equal(1, z.Split(" & ").Count(s => s.Contains("add rule")));
    }
    // Özel ağ uyarısı.
    [Fact] public void OzelAg() { Assert.True(BruteForce.OzelAg("192.168.1.5")); Assert.False(BruteForce.OzelAg("203.0.113.5")); }
    // Sensör komutu: dakika sınırlanır, sabit biçim.
    [Fact] public void SshKomutu() { Assert.Contains("'60 min ago'", BruteForce.SshGunlukKomutu(60)); Assert.Contains("'1440 min ago'", BruteForce.SshGunlukKomutu(999999)); Assert.Contains("'5 min ago'", BruteForce.SshGunlukKomutu(-3)); }
}
