// SensorMantigi.cs (1000+ satır) sorumluluklarına göre bölündü: profil/depo, güvenlik doğrulaması, komut/argüman üretimi, ssh anahtar yönetimi, sunucu parmak izi, tarama yürütme, çıktı ayrıştırma,
// gömülü ajan kaynağı. Bu testler ayrımı ve her sorumluluğun tek başına (ağ/ssh olmadan) sınanabildiğini doğrular.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class SensorBolunmeTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void SensorMantigiDosyasiBolundu_HerSorumlulukAyriSinifVeDosyada_HicbiriBuyukDegil()
    {
        string? kok = Kok(); if (kok == null) return;
        Assert.False(File.Exists(Path.Combine(kok, "BulutSoft.Core", "SensorMantigi.cs")));
        var beklenen = new Dictionary<string, string>
        {
            ["SensorProfili.cs"] = "class SensorDeposu", ["SensorGuvenlik.cs"] = "class SensorGuvenlik", ["SensorKomutlari.cs"] = "class SensorKomutlari", ["SensorAnahtarYoneticisi.cs"] = "class SensorAnahtarYoneticisi",
            ["SensorSunucuParmakIzi.cs"] = "class SensorSunucuParmakIzi", ["SensorTaramaYurutucu.cs"] = "class SensorTaramaYurutucu", ["SensorCiktiAyristirici.cs"] = "class SensorCiktiAyristirici", ["SensorAjani.cs"] = "class SensorAjani"
        };
        foreach (var (dosya, sinif) in beklenen)
        {
            string yol = Path.Combine(kok, "BulutSoft.Core", "Sensor", dosya); Assert.True(File.Exists(yol), dosya);
            var satirlar = File.ReadAllLines(yol); Assert.Contains(sinif, string.Join("\n", satirlar), StringComparison.Ordinal); Assert.True(satirlar.Length < 130, $"{dosya} küçük kalmalı ({satirlar.Length} satır)");
        }
        Assert.False(File.ReadAllText(Path.Combine(kok, "BulutSoft.Core", "Sensor", "SensorAjani.cs")).Contains("import sys", StringComparison.Ordinal));   // Python kaynağı artık C# içinde değil
        Assert.DoesNotContain("SensorIstemci", string.Join("\n", Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(@"\obj\", StringComparison.Ordinal)).Select(f => File.ReadAllText(f))), StringComparison.Ordinal);
    }

    [Fact]
    public void GomuluAjanKaynagi_EskiHamDizeyleAyni_SurumUyumlu()
    {
        string k = SensorAjani.Kaynak;
        Assert.StartsWith("import sys, json, socket", k, StringComparison.Ordinal); Assert.EndsWith("sys.exit(main())", k, StringComparison.Ordinal); Assert.DoesNotContain('\r', k);
        Assert.Contains($"SURUM = \"{SensorAjani.Surum}\"", k, StringComparison.Ordinal);
        Assert.Equal(656, k.Split('\n').Length);                       // taşımadan önceki ham dizeyle aynı satır sayısı
        Assert.Same(k, SensorAjani.Kaynak);                            // bir kez yüklenir
    }

    [Fact]
    public void CiktiAyristirici_JsonSatiriniTemizCozer_KotuVeUzunuReddeder_HataMetniniSadelestirir()
    {
        var o = SensorCiktiAyristirici.Ayristir("{\"t\":\"open\",\"p\":22,\"b\":\"SSH-2.0\"}"); Assert.NotNull(o); Assert.Equal("open", o!.T); Assert.Equal(22, o.Kapi);
        Assert.Null(SensorCiktiAyristirici.Ayristir("düz metin")); Assert.Null(SensorCiktiAyristirici.Ayristir("{\"t\":\"bilinmeyen\"}")); Assert.Null(SensorCiktiAyristirici.Ayristir("{bozuk")); Assert.Null(SensorCiktiAyristirici.Ayristir("{" + new string('a', 5000)));
        Assert.Equal("a b", SensorCiktiAyristirici.Temizle("a\u0007b", 10)); Assert.Equal("abc", SensorCiktiAyristirici.Temizle("abcdef", 3));
        var s = SensorCiktiAyristirici.OnKontrolCoz(new[] { "BS-SENSOR-OK", "Linux 6.1", "kali", "PY-OK" }); Assert.True(s.Basarili && s.Python); Assert.False(SensorCiktiAyristirici.OnKontrolCoz(new[] { "bozuk" }).Basarili);
        Assert.Contains("PARMAK İZİ DEĞİŞMİŞ", SensorCiktiAyristirici.HataAciklama("Host key verification failed."), StringComparison.Ordinal); Assert.Equal("bilinmeyen sorun.", SensorCiktiAyristirici.HataAciklama(" "));
    }

    [Fact]
    public void Komutlar_OnayliVeDogrulanir_SshHerZamanKatiParmakIziKontrolluDur()
    {
        Assert.Equal("python3 - tcp 192.168.56.101 22,80", SensorKomutlari.TcpKomutu("192.168.56.101", "22,80"));
        Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("8.8.8.8", "22")); Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("192.168.56.101", "22; rm -rf /"));
        var p = new SensorProfili("kali", "192.168.56.101", 22, "ibrahim", @"C:\anahtar"); var a = SensorKomutlari.ArgumanOlustur(p, "python3 - hello");
        Assert.Contains("StrictHostKeyChecking=yes", a); Assert.Contains("BatchMode=yes", a); Assert.Contains("PasswordAuthentication=no", a); Assert.Contains("ibrahim@192.168.56.101", a); Assert.Equal("python3 - hello", a[^1]);
        Assert.Contains("BatchMode=no", SensorKomutlari.ArgumanOlustur(p, "x", parolali: true));
        Assert.Throws<ArgumentException>(() => SensorAnahtarYoneticisi.AnahtarEklemeKomutu("ssh-ed25519 AAAA'; rm -rf / #")); Assert.Contains("authorized_keys", SensorAnahtarYoneticisi.AnahtarEklemeKomutu("ssh-ed25519 AAAAC3Nza bulutsoft-sensor"));
    }

    [Fact]
    public void SunucuParmakIzi_BilinenSunucuTanima_KnownHostsDosyasindanOkunur()
    {
        string eski = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "sensbol_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(gecici);
        try
        {
            BulutKlasor.Kok = gecici; Assert.False(SensorSunucuParmakIzi.SunucuTanidikMi("192.168.56.101"));
            Directory.CreateDirectory(Path.GetDirectoryName(SensorSunucuParmakIzi.BilinenSunucular)!); File.WriteAllText(SensorSunucuParmakIzi.BilinenSunucular, "# yorum\n192.168.56.101 ssh-ed25519 AAAA\n[10.0.0.5]:2222 ssh-ed25519 BBBB\n");
            Assert.True(SensorSunucuParmakIzi.SunucuTanidikMi("192.168.56.101")); Assert.True(SensorSunucuParmakIzi.SunucuTanidikMi("10.0.0.5")); Assert.False(SensorSunucuParmakIzi.SunucuTanidikMi("192.168.56.1"));
        }
        finally { BulutKlasor.Kok = eski; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "SensorBolunmeTestleri.temizlik"); } }
    }
}
