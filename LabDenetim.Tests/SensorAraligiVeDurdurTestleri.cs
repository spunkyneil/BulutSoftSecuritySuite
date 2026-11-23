// Sensör port taraması: aralık (/24) desteği, Durdur'un gerçekten süreci sonlandırması ve köşe bildirimi güvenliği.
using System.Diagnostics;
using System.Text;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class SensorAraligiVeDurdurTestleri
{
    [Theory]
    [InlineData("192.168.56.0/24", true)] [InlineData("192.168.56.101", true)] [InlineData("10.1.2.0/25", true)] [InlineData("192.168.56.5/32", true)]
    [InlineData("192.168.0.0/16", false)] [InlineData("10.0.0.0/8", false)] [InlineData("8.8.8.0/24", false)] [InlineData("192.168.56.0/2", false)]
    [InlineData("192.168.56.0/24; id", false)] [InlineData("192.168.56.0/", false)] [InlineData("", false)] [InlineData("192.168.56.0/240", false)]
    public void HedefAraligi_Kurallari(string h, bool beklenen) => Assert.Equal(beklenen, SensorGuvenlik.HedefAraligiGecerli(h));

    [Fact] public void TcpKomutu_AraligiKabulEder_VeEnjeksiyonuReddeder()
    {
        Assert.Equal("python3 - tcp 192.168.56.0/24 22,80", SensorKomutlari.TcpKomutu("192.168.56.0/24", "22,80"));
        Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("8.8.8.0/24", "80"));
        Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("10.0.0.0/8", "80"));
        Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("192.168.56.0/24 && id", "80"));
    }

    // Gerçek Python varsa ajanın aralık kipini çalıştırır (yoksa test atlanır).
    static (int Kod, List<string> Satirlar) Ajan(string hedef, string kapilar)
    {
        string py = TestPython.Yol!;   // [PythonFact] Python yoksa testi zaten atlar
        var psi = new ProcessStartInfo(py) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        psi.ArgumentList.Add("-"); psi.ArgumentList.Add("tcp"); psi.ArgumentList.Add(hedef); psi.ArgumentList.Add(kapilar);
        using var p = Process.Start(psi)!; p.StandardInput.Write(SensorAjani.Kaynak); p.StandardInput.Close();
        var l = new List<string>(); string? s; while ((s = p.StandardOutput.ReadLine()) != null) l.Add(s); p.WaitForExit(60000); return (p.ExitCode, l);
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_AralikTarar_ToplamDenemeSayisiDogru()
    {
        var (kod, l) = Ajan("127.0.0.0/30", "1");
        Assert.Equal(0, kod);
        var basla = SensorCiktiAyristirici.Ayristir(l.First(x => x.Contains("\"start\""))); Assert.Equal(2, basla!.Toplam);   // /30 → 2 kullanılabilir adres × 1 kapı
        Assert.Contains(l, x => x.Contains("\"done\""));
    }

    [Trait("Category", "Integration")]
    [PythonFact] public void Ajan_GenisVeGenelAraliklariReddeder()
    {
        var (k1, l1) = Ajan("8.8.8.0/24", "80");
        Assert.NotEqual(0, k1); Assert.Contains(l1, x => x.Contains("error")); Assert.DoesNotContain(l1, x => x.Contains("\"start\""));
        var (k2, l2) = Ajan("10.0.0.0/8", "80"); Assert.NotEqual(0, k2); Assert.DoesNotContain(l2, x => x.Contains("\"start\""));
        var (k3, _) = Ajan("10.0.0.5; id", "80"); Assert.NotEqual(0, k3);
    }

    // Durdur: iptal edilince uzun süren ssh süreci GERÇEKTEN sonlandırılmalı ve çağrı hızlıca dönmeli.
    [Fact] public async Task Iptal_UzunSurenSshSureciniOldurur()
    {
        string eskiKok = BulutKlasor.Kok; string? eskiSsh = SensorKomutlari.SshYoluGecici; string gecici = Path.Combine(Path.GetTempPath(), "durdur_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(gecici);
        try
        {
            BulutKlasor.Kok = gecici;
            // Sahte ssh: ~60 sn uyur.
            string sahte = Path.Combine(gecici, "ssh.cmd"); File.WriteAllText(sahte, "@echo off\r\nping -n 60 127.0.0.1 >nul\r\n"); SensorKomutlari.SshYoluGecici = sahte;
            string anahtar = Path.Combine(gecici, "anahtar"); File.WriteAllText(anahtar, "x");
            var p = new SensorProfili("Test", "192.168.56.101", 22, "kali", anahtar);
            using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
            var is_ = SensorTaramaYurutucu.CalistirAsync(p, "python3 - tcp 192.168.56.0/24 22", SensorAjani.Kaynak, _ => { }, cts.Token, 600);
            await Task.Delay(800); cts.Cancel();
            var (kod, hata) = await is_.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(15), "Durdur çok geç sonlandırdı"); Assert.NotEqual(0, kod); Assert.Contains("İptal", hata);
        }
        finally { SensorKomutlari.SshYoluGecici = eskiSsh; BulutKlasor.Kok = eskiKok; try { Directory.Delete(gecici, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "SensorAraligiVeDurdurTestleri.temizlik"); } }
    }

    // Bildirim: pencere yokken (test/komut satırı) hiçbir şey yapmadan sessizce dönmeli.
    [Fact] public void Bildirim_PencereYokken_SessizGecer() { Bildirim.Goster("Başlık", "Mesaj", BildirimTuru.Basari); Bildirim.Goster("", "", BildirimTuru.Hata); }
}
