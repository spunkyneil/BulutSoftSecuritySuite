// Bu dosya SSH anahtar koruması (parolalı anahtar + DPAPI + SSH_ASKPASS) ve sunucu parmak izi sabitleme testlerini içerir.
// Gerçek ssh-keygen kullanılır (Windows'ta "OpenSSH İstemcisi"); ağa çıkılmaz, hiçbir sunucuya bağlanılmaz.
using System.Diagnostics;
using BulutSoftGuardService;
using Microsoft.Win32;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class SshKorumaTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_sshk_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol;
    private readonly RegistryKey _eskiKok = IkinciKanal.Kok; private readonly string _eskiAlt = IkinciKanal.AltAnahtar, _eskiPipe = ServisIstemci.PipeAdi, _anahtar = @"SOFTWARE\BulutSoftTest_" + Guid.NewGuid().ToString("N")[..8], _pipe = "TestSsh_" + Guid.NewGuid().ToString("N")[..8];
    private GuardCekirdek? _c; private PipeSunucu? _s; private readonly Func<string, string, bool>? _eskiOnay = SensorSunucuParmakIzi.SunucuOnayi;

    public SshKorumaTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); IkinciKanal.Kok = Registry.CurrentUser; IkinciKanal.AltAnahtar = _anahtar + @"\Muhur"; ServisIstemci.PipeAdi = _pipe; ServisIstemci.Sifirla(); SensorSunucuParmakIzi.SunucuOnayi = null; }
    public void Dispose()
    {
        try { _s?.StopAsync(CancellationToken.None).Wait(2000); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "SshKorumaTestleri.temizlik"); } try { _c?.Dispose(); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "SshKorumaTestleri.temizlik"); } try { Registry.CurrentUser.DeleteSubKeyTree(_anahtar, false); Registry.CurrentUser.DeleteSubKeyTree(_anahtar + "Gizli", false); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "SshKorumaTestleri.temizlik"); }
        IkinciKanal.Kok = _eskiKok; IkinciKanal.AltAnahtar = _eskiAlt; ServisIstemci.PipeAdi = _eskiPipe; ServisIstemci.Sifirla(); BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; SensorSunucuParmakIzi.SunucuOnayi = _eskiOnay; SensorSunucuParmakIzi.CanliTarayiciGecici = null; SensorSunucuParmakIzi.SunucuDegistiOnayi = null; try { Directory.Delete(_t, true); } catch (Exception hkt4) { HataKayit.Sessiz(hkt4, "SshKorumaTestleri.temizlik"); }
    }

    private static readonly string Keygen = File.Exists(@"C:\Windows\System32\OpenSSH\ssh-keygen.exe") ? @"C:\Windows\System32\OpenSSH\ssh-keygen.exe" : "ssh-keygen";
    private static (int, string) Calistir(IDictionary<string, string>? ortam, params string[] arg)
    {
        var psi = new ProcessStartInfo(Keygen) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true }; foreach (var a in arg) psi.ArgumentList.Add(a);
        if (ortam != null) foreach (var k in ortam) psi.Environment[k.Key] = k.Value; using var p = Process.Start(psi)!; p.StandardInput.Close(); var h = p.StandardError.ReadToEndAsync(); string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(15000); return (p.ExitCode, o + h.Result);
    }

    private async Task Servis()
    {
        var a = new GuardAyarlari { Gelistirme = true, PipeAdi = _pipe, Klasor = Path.Combine(_t, "guard"), IzinliUygulama = Environment.ProcessPath!, ProfilKoku = Path.Combine(_t, "profiller") };
        Directory.CreateDirectory(a.ProfilKoku); _c = new GuardCekirdek(a); _s = new PipeSunucu(_c, a); await _s.StartAsync(CancellationToken.None); OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); BulutKlasor.Kok = _t;
        for (int i = 0; i < 40 && !ServisIstemci.Calisiyor; i++) { await Task.Delay(100); ServisIstemci.Sifirla(); }
    }

    [Fact]
    public async Task Yeni_Anahtar_Parolali_Uretilir_Parola_DPAPIde_Duz_Anahtar_Diskte_Yok()
    {
        string k = Path.Combine(_t, "k"); await SensorAnahtarYoneticisi.AnahtarUretAsync(k); Assert.True(File.Exists(k + ".dpapi"));
        Assert.NotEqual(0, Calistir(null, "-y", "-P", "", "-f", k).Item1);                                 // parolasız AÇILAMAZ (şifreli anahtar)
        string? parola = SensorAnahtarYoneticisi.ParolaOku(k); Assert.False(string.IsNullOrEmpty(parola)); Assert.Equal(0, Calistir(null, "-y", "-P", parola!, "-f", k).Item1);
        Assert.DoesNotContain(parola!, File.ReadAllText(k + ".dpapi"));                                        // blob düz yazı içermez
    }

    [Fact]
    public void Eski_Duz_Anahtar_Yerinde_Parolali_Hale_Getirilir()
    {
        string k = Path.Combine(_t, "eski"); Assert.Equal(0, Calistir(null, "-t", "ed25519", "-f", k, "-N", "", "-q").Item1); Assert.Equal(0, Calistir(null, "-y", "-P", "", "-f", k).Item1);   // başta DÜZ
        Assert.True(SensorAnahtarYoneticisi.AnahtarKoru(k)); Assert.True(File.Exists(k + ".dpapi")); Assert.NotEqual(0, Calistir(null, "-y", "-P", "", "-f", k).Item1);                                // artık şifreli
        Assert.True(SensorAnahtarYoneticisi.AnahtarKoru(k));                                                              // ikinci çağrı sorunsuz (zaten korumalı)
    }

    [Fact]
    public async Task Askpass_Parolayi_Yalnizca_Cocuk_Surecin_Ortaminda_Verir_Ve_Anahtar_Acilir()
    {
        string k = Path.Combine(_t, "a"); await SensorAnahtarYoneticisi.AnahtarUretAsync(k); var psi = new ProcessStartInfo(Keygen) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        Assert.True(SensorAnahtarYoneticisi.AskpassHazirla(psi, k)); Assert.Contains("BS_ANAHTAR_PAROLA", psi.Environment.Keys); Assert.Equal("force", psi.Environment["SSH_ASKPASS_REQUIRE"]);
        foreach (var a in new[] { "-y", "-f", k }) psi.ArgumentList.Add(a); using var p = Process.Start(psi)!; p.StandardInput.Close(); string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(15000);
        Assert.Equal(0, p.ExitCode); Assert.StartsWith("ssh-ed25519", o);                                       // parola askpass ile verildi, anahtar açıldı
        string askpass = Path.Combine(_t, "sensor", "askpass.cmd"); Assert.True(File.Exists(askpass)); Assert.DoesNotContain(SensorAnahtarYoneticisi.ParolaOku(k)!, File.ReadAllText(askpass));   // betikte parola YOK
        Assert.Null(Environment.GetEnvironmentVariable("BS_ANAHTAR_PAROLA"));                                    // bu sürecin ortamına sızmadı
    }

    [Fact]
    public void ArgumanOlustur_Parolali_Anahtarda_BatchMode_Kapali_Digerlerinde_Ayni()
    {
        var p = new SensorProfili("x", "192.168.56.1", 22, "kali", "k"); Assert.Contains("BatchMode=yes", SensorKomutlari.ArgumanOlustur(p, "cmd")); Assert.Contains("BatchMode=no", SensorKomutlari.ArgumanOlustur(p, "cmd", true));
        Assert.Contains("PreferredAuthentications=publickey", SensorKomutlari.ArgumanOlustur(p, "cmd", true)); Assert.Contains("PasswordAuthentication=no", SensorKomutlari.ArgumanOlustur(p, "cmd", true));
    }

    // Gerçek bir ed25519 sunucu anahtarı üretir: (known_hosts satırı, parmak izi).
    private (string Satir, string Fp) SunucuAnahtari(string host)
    {
        string k = Path.Combine(_t, "h" + Guid.NewGuid().ToString("N")[..4]); Calistir(null, "-t", "ed25519", "-f", k, "-N", "", "-q"); string[] pub = File.ReadAllText(k + ".pub").Trim().Split(' ');
        var (_, o) = Calistir(null, "-l", "-f", k + ".pub"); var m = System.Text.RegularExpressions.Regex.Match(o, @"SHA256:[A-Za-z0-9+/=]+"); return ($"{host} {pub[0]} {pub[1]}", m.Value);
    }
    private void KnownHostsYaz(string satir) { Directory.CreateDirectory(Path.GetDirectoryName(SensorSunucuParmakIzi.BilinenSunucular)!); File.WriteAllText(SensorSunucuParmakIzi.BilinenSunucular, satir + "\n"); }
    private static void Canli((string, string)? sonuc) => SensorSunucuParmakIzi.CanliTarayiciGecici = (_, _) => Task.FromResult<(string Satir, string ParmakIzi)?>(sonuc);

    [Fact]
    public async Task Parmak_Izi_Hizmet_Kaydi_Yoksa_Mevcut_Kaydedilir_Kayit_Varken_Uyusmazsa_Reddedilir()
    {
        await Servis(); var (satir, fp) = SunucuAnahtari("192.168.56.77"); KnownHostsYaz(satir); var p = new SensorProfili("Kali Bir", "192.168.56.77", 22, "kali", "k");
        // Hizmette kayıt yok: mevcut parmak izi kaydedilir (sensör ADINA göre), devam.
        Assert.Null(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default)); Assert.Equal("192.168.56.77|" + fp, ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = p.Ad })!.Veri!["kayit"]);
        // Saldırgan known_hosts'a kendi anahtarını koydu; sunucuya ulaşılamıyor: uyuşmazlık alarmı + REDDET.
        Canli(null); var (satir2, _) = SunucuAnahtari("192.168.56.77"); KnownHostsYaz(satir2); var red = await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default); Assert.NotNull(red); Assert.Contains("REDDEDİLDİ", red); Assert.Contains(OlayDeposu.Oku(), o => o.Mesaj.Contains("SENSÖR ALARM"));
    }

    [Fact]
    public async Task Sensor_Kimligi_Degisti_Onay_Yoksa_Reddedilir_Onaylanirsa_Kayit_Ve_Known_Hosts_Guncellenir()
    {
        await Servis(); var (satir, fp) = SunucuAnahtari("192.168.56.88"); KnownHostsYaz(satir); var p = new SensorProfili("Kali Iki", "192.168.56.88", 22, "kali", "k"); Assert.Null(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default));   // kayıt oluştu
        var (yeniSatir, yeniFp) = SunucuAnahtari("192.168.56.88"); Canli((yeniSatir, yeniFp)); KnownHostsYaz(yeniSatir);                                                                 // sunucu anahtarı DEĞİŞTİ
        string? sorulan = null; SensorSunucuParmakIzi.SunucuDegistiOnayi = (ad, host, eski, yeni) => { sorulan = $"{ad}|{host}|{eski}|{yeni}"; return false; };
        var red = await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default); Assert.NotNull(red); Assert.Contains("KİMLİĞİ DEĞİŞTİ", red); Assert.Equal($"Kali Iki|192.168.56.88|{fp}|{yeniFp}", sorulan);               // yeni parmak izi kullanıcıya gösterildi
        Assert.Equal("192.168.56.88|" + fp, ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = p.Ad })!.Veri!["kayit"]);                                                               // kayıt DEĞİŞMEDİ
        // Kullanıcı onaylarsa kayıt güncellenir.
        SensorSunucuParmakIzi.SunucuDegistiOnayi = (_, _, _, _) => true; Assert.Null(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default)); Assert.Equal("192.168.56.88|" + yeniFp, ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = p.Ad })!.Veri!["kayit"]);
        Assert.Equal(yeniFp, SensorSunucuParmakIzi.KayitliParmakIzi("192.168.56.88", 22)); SensorSunucuParmakIzi.SunucuDegistiOnayi = null;
    }

    [Fact]
    public async Task Hizmet_Kaydi_Sunucuyla_Ayniysa_Silinen_Known_Hosts_Sessizce_Onarilir()
    {
        await Servis(); var (satir, fp) = SunucuAnahtari("192.168.56.99"); KnownHostsYaz(satir); var p = new SensorProfili("Kali Uc", "192.168.56.99", 22, "kali", "k"); Assert.Null(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default));
        File.Delete(SensorSunucuParmakIzi.BilinenSunucular); Canli((satir, fp)); Assert.Null(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default)); Assert.Equal(fp, SensorSunucuParmakIzi.KayitliParmakIzi("192.168.56.99", 22));
    }

    [Fact]
    public async Task Ilk_Baglanti_Kullanici_Onayi_Olmadan_Yapilmaz_Onayla_Known_Hosts_Ve_Hizmete_Yazilir()
    {
        await Servis(); var (satir, fp) = SunucuAnahtari("192.168.56.55"); var p = new SensorProfili("Kali Dort", "192.168.56.55", 22, "kali", "k"); Canli((satir, fp));
        SensorSunucuParmakIzi.SunucuOnayi = null; var red = await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default); Assert.NotNull(red); Assert.Contains("ONAYLANMADI", red); Assert.False(File.Exists(SensorSunucuParmakIzi.BilinenSunucular));
        string? gosterilen = null; SensorSunucuParmakIzi.SunucuOnayi = (host, parmak) => { gosterilen = parmak; return false; }; Assert.NotNull(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default)); Assert.Equal(fp, gosterilen); Assert.False(File.Exists(SensorSunucuParmakIzi.BilinenSunucular));
        SensorSunucuParmakIzi.SunucuOnayi = (_, _) => true; Assert.Null(await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default)); Assert.Equal(fp, SensorSunucuParmakIzi.KayitliParmakIzi("192.168.56.55", 22)); Assert.Equal("192.168.56.55|" + fp, ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = p.Ad })!.Veri!["kayit"]);
        SensorSunucuParmakIzi.SunucuOnayi = null;
    }

    [Fact]
    public async Task Bilinmeyen_Sunucuya_Ulasilamazsa_Baglanilmaz()
    {
        SensorSunucuParmakIzi.CanliTarayiciGecici = null; var p = new SensorProfili("x", "127.0.0.1", 1, "kali", "k");   // kapalı kapı: parmak izi alınamaz
        var r = await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, default); Assert.NotNull(r); Assert.Contains("ulaşılamadı", r);
        Assert.False(File.Exists(SensorSunucuParmakIzi.BilinenSunucular) && File.ReadAllText(SensorSunucuParmakIzi.BilinenSunucular).Contains("127.0.0.1"));   // known_hosts'a hiçbir şey yazılmadı
    }
}
