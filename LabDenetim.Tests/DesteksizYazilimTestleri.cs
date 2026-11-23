// Eski / desteği bitmiş yazılım denetçisi testleri: ömür sonu tarihleri, eşikler, yanlış alarm savunmaları, nmap ve port taraması entegrasyonu, halk dili karşılığı.
using LabDenetim.Moduller;
using LabDenetim.V2;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class DesteksizYazilimTestleri
{
    // Sabit "şimdi": testler tarihe bağlı bozulmasın.
    static readonly DateTime Simdi = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
    static List<EolSonucu> D(string m, DateTime? t = null) => DesteksizYazilim.Degerlendir(m, t ?? Simdi);

    [Theory]
    [InlineData("Server: Apache/2.2.15 (CentOS)", "EOL-APACHE")] [InlineData("Apache httpd 2.0.52", "EOL-APACHE")]
    [InlineData("X-Powered-By: PHP/5.3.3", "EOL-PHP")] [InlineData("PHP/7.4.33", "EOL-PHP")] [InlineData("PHP/8.1.2", "EOL-PHP")]
    [InlineData("OpenSSL/1.0.2k-fips", "EOL-OPENSSL")] [InlineData("OpenSSL/1.1.1w", "EOL-OPENSSL")]
    [InlineData("Microsoft-IIS/7.5", "EOL-IIS")] [InlineData("Microsoft-IIS/6.0", "EOL-IIS")] [InlineData("Microsoft-IIS/8.5", "EOL-IIS")]
    [InlineData("Apache Tomcat/7.0.100", "EOL-TOMCAT")] [InlineData("Apache Tomcat/8.5.99", "EOL-TOMCAT")]
    [InlineData("MySQL 5.7.33", "EOL-MYSQL")] [InlineData("MySQL 8.0.30", "EOL-MYSQL")]
    [InlineData("PostgreSQL DB 9.6.3", "EOL-POSTGRESQL")] [InlineData("PostgreSQL DB 13.2", "EOL-POSTGRESQL")]
    public void Desteksiz_Surumler_Yuksek_Onemle_Bulunur(string metin, string kimlik)
    {
        var s = D(metin); Assert.Single(s); Assert.Equal(kimlik, s[0].Kimlik); Assert.Equal("Desteği bitmiş", s[0].Durum); Assert.Equal(Onem.Yuksek, s[0].Onem); Assert.Contains("doğrulay", s[0].Cozum);
    }

    [Theory]
    [InlineData("Apache/2.4.58 (Ubuntu)")] [InlineData("PHP/8.3.1")] [InlineData("PHP/8.2.10")] [InlineData("OpenSSL/3.0.2")] [InlineData("Microsoft-IIS/10.0")] [InlineData("Apache Tomcat/9.0.80")]
    [InlineData("MySQL 8.4.0")] [InlineData("PostgreSQL DB 16.1")] [InlineData("SSH-2.0-OpenSSH_9.6")] [InlineData("SSH-2.0-OpenSSH_8.0")] [InlineData("nginx/1.25.3")] [InlineData("vsFTPd 3.0.5")]
    [InlineData("Apache Coyote/1.1")] [InlineData("HTTP/1.0 200 OK")] [InlineData("")] [InlineData("   ")]
    public void Guncel_Veya_Ilgisiz_Metinde_Uyari_Yok(string metin) => Assert.Empty(D(metin));

    [Fact]
    public void Ssh_Ve_Nginx_Esik_Eski_Sinifi_Daha_Dusuk_Onemde()
    {
        // Resmî EOL tarihi olmayan ürünlerde "Eski sürüm" ve Orta önem; kesin hüküm değil.
        var s = D("SSH-2.0-OpenSSH_7.4p1 Debian"); Assert.Single(s); Assert.Equal("Eski sürüm", s[0].Durum); Assert.Equal(Onem.Orta, s[0].Onem);
        var n = D("nginx/1.14.0"); Assert.Single(n); Assert.Equal("Eski sürüm", n[0].Durum);
    }

    [Theory]
    [InlineData("220 (vsFTPd 2.3.4)", "EOL-VSFTPD")] [InlineData("220 ProFTPD 1.3.3c Server", "EOL-PROFTPD")]
    public void Belgelenmis_Arka_Kapili_Surumler_Tehlikeli(string metin, string kimlik)
    {
        var s = D(metin); Assert.Single(s); Assert.Equal(kimlik, s[0].Kimlik); Assert.Equal("Tehlikeli sürüm", s[0].Durum); Assert.Equal(9, s[0].Etki); Assert.Contains("yeniden kurun", s[0].Cozum);
    }

    [Fact]
    public void Tarih_Siniri_Dogru_Calisir()
    {
        // PHP 8.1: güvenlik desteği 31.12.2025'te biter. Öncesinde uyarı yok, sonrasında var.
        Assert.Empty(D("PHP/8.1.2", new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc))); Assert.Single(D("PHP/8.1.2", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        // PHP 8.2 henüz bitmedi (2026-12-31).
        Assert.Empty(D("PHP/8.2.4", new DateTime(2026, 12, 30, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void MariaDB_MySQL_Sanilmaz()
    {
        // MariaDB'nin "5.5.5-10.x" sürüm yazısı MySQL 5.5 gibi okunmamalı (yanlış alarm).
        Assert.Empty(D("MySQL 5.5.5-10.11.6-MariaDB")); Assert.Empty(D("MariaDB 10.5.12"));
    }

    [Fact]
    public void Kotu_Niyetli_Uzun_Metin_Takilmaz_Ve_Cokmez()
    {
        // Çok uzun / tekrarlı metin: kısaltılır, düzenli ifade zaman aşımına güvenli.
        var sw = System.Diagnostics.Stopwatch.StartNew(); Assert.Empty(D(new string('A', 200000) + " " + new string('1', 50000))); Assert.True(sw.ElapsedMilliseconds < 2000);
        Assert.Single(D("Apache/2.2.3 " + new string('x', 100000)));
    }

    [Fact]
    public void Port_Taramasindan_Ana_Tablo_Satiri_Uretilir()
    {
        // SSH ve MySQL tabelaları: tabloya ayrı "Eski Yazılım" satırı eklenir; güncel tabela satır eklemez.
        var l = DesteksizYazilim.PortBulgulari(new PortSonucu(3306, "MySQL", "J5.7.33-0ubuntu0.18.04"), Simdi); Assert.Single(l); Assert.Equal("Eski Yazılım", l[0].Modul); Assert.Equal(Onem.Yuksek, l[0].Onem); Assert.Contains("port 3306", l[0].Baslik);
        Assert.Empty(DesteksizYazilim.PortBulgulari(new PortSonucu(22, "SSH", "SSH-2.0-OpenSSH_9.6"), Simdi)); Assert.Single(DesteksizYazilim.PortBulgulari(new PortSonucu(22, "SSH", "SSH-2.0-OpenSSH_7.4"), Simdi));
    }

    [Fact]
    public void Nmap_Hizmetinden_V2_Bulgu_Uretilir_Ve_Kapali_Port_Yok_Sayilir()
    {
        // nmap ürün+sürüm ayrı alanlarda gelir: birleştirilip değerlendirilmeli; kapalı port atlanmalı.
        var acik = new ServiceEndpoint(80, "tcp", "open", "http", "Apache httpd", "2.2.15", "(CentOS)", Array.Empty<string>()); var kapali = acik with { State = "closed" };
        var f = DesteksizYazilim.BulguUret("192.168.56.10", acik, 0.5, Simdi); Assert.Single(f); Assert.Equal("EOL-APACHE", f[0].RuleId); Assert.Equal("net:192.168.56.10:80", f[0].AssetId); Assert.True(f[0].Score > 0);
        Assert.Empty(DesteksizYazilim.BulguUret("192.168.56.10", kapali, 0.5, Simdi));
        var iis = new ServiceEndpoint(80, "tcp", "open", "http", "Microsoft IIS httpd", "7.5", null, Array.Empty<string>()); Assert.Equal("EOL-IIS", DesteksizYazilim.BulguUret("192.168.56.11", iis, 0.5, Simdi).Single().RuleId);
    }

    [Fact]
    public void Tarama_Birden_Cok_Cihaz_Ve_Ayni_Kimlik_Kararli()
    {
        // Aynı tarama iki kez değerlendirilince aynı bulgu kimlikleri çıkmalı (yinelenen kayıt oluşmasın).
        var tarama = new NormalizedScan("192.168.56.0/24", Simdi, new() { new NmapHost("192.168.56.10", null, null, null, null, new() { new ServiceEndpoint(22, "tcp", "open", "ssh", "OpenSSH", "7.4", null, Array.Empty<string>()), new ServiceEndpoint(80, "tcp", "open", "http", "nginx", "1.25.3", null, Array.Empty<string>()) }) });
        var a = DesteksizYazilim.TaramaBulgulari(tarama, _ => 0.5, Simdi); var b = DesteksizYazilim.TaramaBulgulari(tarama, _ => 0.5, Simdi);
        Assert.Single(a); Assert.Equal(a[0].Id, b[0].Id);
    }

    [Fact]
    public void Halk_Dili_Uc_Durum_Icin_Ozel_Karsilik_Var()
    {
        // Üç durumun başlığı halk diline özel çevrilmeli (genel yedek cümleye düşmemeli).
        foreach (var d in new[] { "Desteği bitmiş", "Eski sürüm", "Tehlikeli sürüm" })
        {
            var h = HalkDiliCevirici.Cevir("Eski Yazılım", $"{d}: Apache HTTP Server 2.2.15 (port 80)", "ayrıntı", Onem.Yuksek); Assert.True(h.Ozel, d); Assert.NotEmpty(h.BuNeDemek); Assert.NotEmpty(h.Zarar);
        }
    }
}
