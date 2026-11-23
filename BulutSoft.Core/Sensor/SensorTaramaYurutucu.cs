using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

internal sealed record SensorSonuc(bool Basarili, string Sistem, string Kullanici, bool Python, string Ozet);

// Sensörde komut/tarama YÜRÜTME: ssh sürecini başlatır, çıktıyı canlı akıtır, zaman aşımı/iptal/denetim günlüğü uygular.
internal static class SensorTaramaYurutucu
{
    // Denetim günlüğüne yazar: hangi sensörde ne çalıştırıldı (komutun etiketi; sır içermez).
    public static void Denetle(SensorProfili p, string islem)
    {
        // Yazma bozulabilir.
        try { Directory.CreateDirectory(BulutKlasor.Kok); File.AppendAllText(BulutKlasor.Dosya("sensor_denetim.log"), $"{DateTime.Now:o}\t{p.Ad}\t{p.Kullanici}@{p.Host}:{p.Port}\t{islem}\r\n", new UTF8Encoding(false)); }
        // Sessiz geç.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "SensorMantigi.Denetle"); }
    }

    // Uzak komutu çalıştırır; stdout satırlarını canlı olarak "satir" kancasına verir. Dönüş: (çıkış kodu, hata metni).
    public static async Task<(int Cikis, string Hata)> CalistirAsync(SensorProfili p, string uzakKomut, string? girdi, Action<string> satir, CancellationToken ct, int zamanAsimiSn = 300, int enUzunSatir = 4096)
    {
        // Profil kurallara uymuyorsa hiç çalıştırma.
        var h = SensorGuvenlik.ProfilHatasi(p); if (h != null) return (-1, h);
        // Klasörü hazırla (known_hosts için) ve sunucu parmak izini denetle (değişmişse ya da onaylanmamışsa bağlanılmaz).
        Directory.CreateDirectory(Path.GetDirectoryName(SensorSunucuParmakIzi.BilinenSunucular)!);
        // (Test için verilen sahte ssh yolu varken parmak izi ağ denetimi atlanır; üretimde SensorKomutlari.SshYoluGecici HİÇ atanmaz.)
        if (SensorKomutlari.SshYoluGecici == null) { var pi = await SensorSunucuParmakIzi.ParmakIziDenetleAsync(p, ct); if (pi != null) return (-1, pi); }
        // Süreç ayarları.
        var psi = new ProcessStartInfo(SensorKomutlari.SshYolu) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        // Argümanlar.
        bool parolali = SensorAnahtarYoneticisi.AskpassHazirla(psi, p.AnahtarYolu);
        foreach (var a in SensorKomutlari.ArgumanOlustur(p, uzakKomut, parolali)) psi.ArgumentList.Add(a);
        // Başlat.
        Process pr;
        // ssh bulunamazsa anlaşılır hata.
        try { pr = Process.Start(psi)!; } catch (Win32Exception) { return (-1, "ssh programı bulunamadı. Windows Ayarları > Uygulamalar > İsteğe bağlı özellikler > \"OpenSSH İstemcisi\" kurulu olmalı."); }
        // Bitirmek için iptal ve zaman aşımı.
        using var oldur = CancellationTokenSource.CreateLinkedTokenSource(ct); oldur.CancelAfter(TimeSpan.FromSeconds(zamanAsimiSn)); using var kayit = oldur.Token.Register(() => { try { pr.Kill(true); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "SensorMantigi.CalistirAsync"); } });
        // Denetim kaydı.
        Denetle(p, uzakKomut.StartsWith("python3 -", StringComparison.Ordinal) ? uzakKomut : "on-kontrol"); KaliKonsol.Yaz("ssh", $"$ {p.Kullanici}@{p.Host}:{p.Port} {(uzakKomut.StartsWith("python3 -", StringComparison.Ordinal) ? KaliKonsol.KomutEtiketi(uzakKomut) : "on-kontrol")}");
        // Süreç sonlandırılırken bile kaynaklar bırakılsın.
        using (pr)
        {
            // Hata akışını eşzamanlı oku (dolup kilitlenmesin).
            var hataGorevi = pr.StandardError.ReadToEndAsync();
            // Ajan kaynağını (varsa) standart girişten ver ve kapat.
            try { if (girdi != null) await pr.StandardInput.WriteAsync(girdi); pr.StandardInput.Close(); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "SensorMantigi.CalistirAsync"); }
            // Satırları canlı oku.
            string? l; while ((l = await pr.StandardOutput.ReadLineAsync()) != null) { if (l.Length <= enUzunSatir) satir(l); KaliKonsol.Yaz("ajan", l); }
            // Bitmesini bekle.
            await pr.WaitForExitAsync(CancellationToken.None);
            // Sonuç.
            string hata = (await hataGorevi).Trim(); if (oldur.IsCancellationRequested && string.IsNullOrEmpty(hata)) hata = ct.IsCancellationRequested ? "İptal edildi." : "Zaman aşımı.";
            KaliKonsol.Yaz(pr.ExitCode == 0 ? "ssh" : "hata", $"çıkış kodu {pr.ExitCode}" + (hata.Length > 0 ? ": " + hata : "")); return (pr.ExitCode, hata);
        }
    }

    // Bağlantıyı sınar: ön kontrol + (Python varsa) ajan "hello".
    public static async Task<SensorSonuc> SinaAsync(SensorProfili p, CancellationToken ct)
    {
        // Ön kontrol.
        var satirlar = new List<string>(); var (kod, hata) = await CalistirAsync(p, SensorKomutlari.OnKontrolKomutu, null, satirlar.Add, ct, 30);
        // Bağlantı kurulamadıysa nedenini anlaşılır yaz.
        if (kod != 0 && !satirlar.Contains("BS-SENSOR-OK")) return new(false, "", "", false, "Bağlanılamadı: " + SensorCiktiAyristirici.HataAciklama(hata));
        // Çöz.
        var s = SensorCiktiAyristirici.OnKontrolCoz(satirlar); if (!s.Basarili || !s.Python) return s;
        // Ajan testi.
        AjanOlayi? hello = null; var (k2, h2) = await CalistirAsync(p, SensorKomutlari.HelloKomutu, SensorAjani.Kaynak, x => { var o = SensorCiktiAyristirici.Ayristir(x); if (o is { T: "hello" }) hello = o; }, ct, 30);
        // Sonuç.
        return hello != null ? s with { Ozet = s.Ozet + $"\r\nAjan çalıştı: sürüm {hello.Surum}, Python {hello.Python}, sunucu adı: {hello.Sunucu}." } : s with { Basarili = false, Ozet = s.Ozet + "\r\nAjan çalışmadı: " + SensorCiktiAyristirici.HataAciklama(h2) };
    }
}
