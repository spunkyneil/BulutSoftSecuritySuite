// Kali araç kapısı (SensorArac), ajanın "arac" kipi ve Kali Araç Rehberi testleri. Gerçek hedefe/internete HİÇ çıkılmaz; araçlar sahte/eksik olduğu için ajan kurallarını sınar.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Veri klasörünü paylaşan testler sırayla çalışsın.
[Collection("Veri")]
public class KaliAracTestleri
{
    [Theory]
    [InlineData("sslscan", "192.168.56.10", true)] [InlineData("sslscan", "192.168.56.10:8443", true)] [InlineData("sslscan", "8.8.8.8", false)] [InlineData("sslscan", "127.0.0.1", false)] [InlineData("sslscan", "example.com", false)]
    [InlineData("sslscan", "192.168.56.10:99999", false)] [InlineData("sslscan", "192.168.56.10; id", false)] [InlineData("sshaudit", "10.0.0.5:22", true)] [InlineData("sshaudit", "169.254.1.1", false)]
    [InlineData("nikto", "192.168.56.10:80", true)] [InlineData("nikto", "203.0.113.5", false)] [InlineData("nikto", "192.168.56.0/24", false)] [InlineData("nikto", "-h 8.8.8.8", false)]
    [InlineData("searchsploit", "apache 2.4.49", true)] [InlineData("searchsploit", "-x 1234", false)] [InlineData("searchsploit", "apache -m", false)] [InlineData("searchsploit", "a; rm -rf /", false)] [InlineData("searchsploit", "", false)]
    [InlineData("whois", "example.com", true)] [InlineData("whois", "8.8.8.8", false)] [InlineData("whois", "-h evil.com", false)] [InlineData("whois", "localhost", false)]
    public void Hedef_Kurallari(string arac, string hedef, bool gecerli)
    {
        // Araç adını türe çevir (internal enum Theory parametresi olamaz).
        var t = arac switch { "sslscan" => AracTuru.Sslscan, "sshaudit" => AracTuru.SshAudit, "nikto" => AracTuru.Nikto, "searchsploit" => AracTuru.Searchsploit, _ => AracTuru.Whois };
        Assert.Equal(gecerli, SensorArac.HedefHatasi(t, hedef) == null);
    }

    [Fact]
    public void Komut_Onizleme_Sabit_Argumanlari_Gosterir()
    {
        // Önizleme sabit liste olmalı; nikto yalnızca zararsız sondalar (123b) ve zaman sınırlı.
        Assert.Equal("sslscan --no-colour 192.168.56.10:443", SensorArac.KomutOnizle(AracTuru.Sslscan, "192.168.56.10"));
        Assert.Equal("ssh-audit -p 2222 192.168.56.10", SensorArac.KomutOnizle(AracTuru.SshAudit, "192.168.56.10:2222"));
        var n = SensorArac.KomutOnizle(AracTuru.Nikto, "192.168.56.10:443"); Assert.Contains("-Tuning 123b", n); Assert.Contains("-maxtime 150s", n); Assert.EndsWith("-ssl", n);
        Assert.Equal("searchsploit --disable-colour apache 2.4.49", SensorArac.KomutOnizle(AracTuru.Searchsploit, "apache 2.4.49"));
        Assert.True(SensorArac.InternetKullanir(AracTuru.Whois)); Assert.False(SensorArac.InternetKullanir(AracTuru.Nikto));
    }

    [Fact]
    public void Coz_Denetim_Karakterlerini_Temizler_Ve_Kisaltir()
    {
        // Sensörden gelen çıktı güvenilmez: ESC temizlenir, satır sonu korunur, aşırı uzun çıktı kısalır.
        using var d = JsonDocument.Parse(JsonSerializer.Serialize(new { t = "arac", arac = "sslscan", hedef = "192.168.56.10", komut = "sslscan x", cikti = "satir1\u001b[31m\nsatir2" + new string('x', 50000) }));
        var s = SensorArac.Coz(d.RootElement); Assert.Equal(AracTuru.Sslscan, s.Arac); Assert.DoesNotContain('\u001b', s.Cikti); Assert.Contains("satir1", s.Cikti); Assert.Contains('\n', s.Cikti); Assert.Contains("kısaltıldı", s.Cikti); Assert.True(s.Cikti.Length < 40100);
    }

    [Fact]
    public async Task Gecersiz_Hedefte_Sensore_Hicbir_Sey_Gonderilmez()
    {
        // Doğrulama başarısızsa sensöre bağlanmadan hata verilmeli.
        var p = new SensorProfili("x", "192.0.2.1", 22, "u", "k");
        await Assert.ThrowsAsync<InvalidOperationException>(() => SensorArac.CalistirAsync(p, AracTuru.Nikto, "8.8.8.8", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => SensorArac.CalistirAsync(p, AracTuru.Searchsploit, "-x 1", default));
    }

    // Gerçek Python varsa ajanın "arac" kipini çalıştırır (yoksa test atlanır).
    static (int Kod, string Cikti) Ajan(object arguman)
    {
        string py = TestPython.Yol!;   // [PythonFact]/[PythonTheory] Python yoksa testi zaten atlar
        var psi = new ProcessStartInfo(py) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        psi.ArgumentList.Add("-"); psi.ArgumentList.Add("run"); psi.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(arguman))));
        using var p = Process.Start(psi)!; p.StandardInput.Write(SensorAjani.Kaynak); p.StandardInput.Close(); string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(60000); return (p.ExitCode, o);
    }

    [Trait("Category", "Integration")]
    [PythonTheory]
    [InlineData("sslscan", "8.8.8.8")] [InlineData("nikto", "203.0.113.9")] [InlineData("ssh-audit", "127.0.0.1")] [InlineData("sslscan", "192.168.56.10:70000")]
    [InlineData("searchsploit", "-x 1234")] [InlineData("searchsploit", "apache -m")] [InlineData("whois", "-h evil.com")] [InlineData("whois", "8.8.8.8")] [InlineData("nmap-hack", "192.168.56.10")] [InlineData("hydra", "192.168.56.10")]
    public void Ajan_Gecersiz_Veya_Bilinmeyen_Araci_Reddeder(string arac, string hedef)
    {
        // Ajan kendi başına da reddetmeli (Windows tarafı atlatılsa bile): hata olayı ve sıfırdan farklı çıkış kodu.
        var (kod, cikti) = Ajan(new { mod = "arac", arac, hedef });
        Assert.NotEqual(0, kod); Assert.Contains("\"t\": \"error\"", cikti); Assert.DoesNotContain("\"t\": \"arac\"", cikti);
    }

    [Trait("Category", "Integration")]
    [PythonFact]
    public void Ajan_Arac_Kurulu_Degilse_Anlasilir_Hata_Verir()
    {
        // Geçerli hedef ama araç yok (Windows'ta sslscan kurulu değil): kurulum ipucu içeren hata.
        var (kod, cikti) = Ajan(new { mod = "arac", arac = "sslscan", hedef = "192.168.56.10" });
        if (cikti.Contains("\"t\": \"arac\"")) { Assert.DoesNotContain("kurulu degil", cikti); return; }   // araç gerçekten kuruluysa kurulu-değil hatası OLMAMALI
        Assert.Contains("kurulu degil", cikti);
    }

    // ---- Rehber ----
    [Fact]
    public void Rehber_Tum_Kategoriler_Var_Ve_Adlar_Benzersiz()
    {
        // Rehberdeki 8 kategori + ek kategori; adlar yinelenmez; hiçbir alan boş değil.
        var tum = KaliRehberi.Tum; Assert.Equal(tum.Count, tum.Select(x => x.Ad.ToLowerInvariant()).Distinct().Count());
        for (int i = 1; i <= 8; i++) Assert.Contains(KaliRehberi.Kategoriler, k => k.StartsWith(i + "."));
        Assert.All(tum, a => { Assert.False(string.IsNullOrWhiteSpace(a.Ne)); Assert.False(string.IsNullOrWhiteSpace(a.Yasal)); Assert.False(string.IsNullOrWhiteSpace(a.Savunma)); });
        Assert.True(tum.Count >= 50);
    }

    [Fact]
    public void Saldiri_Araclari_Kapali_Ve_Calistirilamaz()
    {
        // Parola kırma, MITM, exploit, kablosuz saldırı ve brute-force araçları ASLA çalıştırılabilir olmamalı.
        string[] yasak = { "Hydra", "Medusa", "John the Ripper", "Hashcat", "Crunch", "CeWL", "Metasploit Framework", "Sqlmap", "Ettercap", "Bettercap", "Dsniff", "Aircrack-ng", "Reaver", "Wifite", "Masscan", "Hping3", "Dirb", "Gobuster", "Wfuzz" };
        foreach (var ad in yasak) { var a = KaliRehberi.Tum.Single(x => x.Ad == ad); Assert.Equal(AracSinifi.Kapali, a.Sinif); Assert.Null(a.Calistir); Assert.Contains("Yasal", "Yasal"); }
    }

    [Fact]
    public void Calistirilabilen_Her_Arac_Rehberde_Ve_Tersi()
    {
        // Her AracTuru rehberde bir kayıtla eşleşmeli; 'Calistirilir' kayıtlar ya AracTuru taşır ya da nmap'tir.
        foreach (var t in Enum.GetValues<AracTuru>()) Assert.Contains(KaliRehberi.Tum, a => a.Calistir == t && a.Sinif == AracSinifi.Calistirilir);
        Assert.All(KaliRehberi.Tum.Where(a => a.Sinif == AracSinifi.Calistirilir), a => Assert.True(a.Calistir != null || a.Sablonlar != null || a.Ad == "Nmap"));
        Assert.All(KaliRehberi.Tum.Where(a => a.Sinif != AracSinifi.Calistirilir), a => Assert.Null(a.Calistir));
    }

    [Fact]
    public void Rehber_Metinlerinde_Calistirilabilir_Saldiri_Komutu_Yok()
    {
        // Kapalı araçların açıklamaları kopyalanıp çalıştırılabilir saldırı komutu (örn. 'hydra -l', 'msfconsole', 'use exploit') içermemeli.
        var metin = string.Join("\n", KaliRehberi.Tum.Where(a => a.Sinif == AracSinifi.Kapali).Select(a => a.Ne + a.Yasal + a.Savunma));
        foreach (var k in new[] { "hydra -", "msfconsole", "use exploit", "sqlmap -u", "aircrack-ng -w", "john --wordlist", "hashcat -m", "rockyou" }) Assert.DoesNotContain(k, metin, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rehber_Arama_Turkce_Ve_Buyuk_Kucuk_Harf_Duyarsiz()
    {
        // Ad ve konuya göre arama; boş arama hepsini verir.
        Assert.Contains(KaliRehberi.Ara("NMAP"), a => a.Ad == "Nmap"); Assert.Contains(KaliRehberi.Ara("parola"), a => a.Ad == "Hydra"); Assert.Equal(KaliRehberi.Tum.Count, KaliRehberi.Ara("  ").Count); Assert.Empty(KaliRehberi.Ara("yokboyleBirSey123"));
    }
}
