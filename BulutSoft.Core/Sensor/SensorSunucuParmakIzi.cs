using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Sunucu PARMAK İZİ sabitleme (known_hosts + koruma hizmeti HKLM kanalı): ilk bağlantıda kullanıcı doğrulaması, değişimde bağlantı reddi.
internal static class SensorSunucuParmakIzi
{
    // Sensör klasörü ve known_hosts (sunucu parmak izlerinin sabitlendiği dosya).
    public static string BilinenSunucular => BulutKlasor.Dosya(Path.Combine("sensor", "known_hosts"));

    // Sunucu daha önce bağlanılıp parmak izi kaydedilmiş mi? Evet ise KATI doğrulama ("yes": parmak izi değişirse bağlantı reddedilir); hayırsa yalnızca İLK bağlantıda "accept-new".
    // (İlk bağlantıda ortadaki adam riski kalır; bunu tamamen kapatmak için parmak izini sensörde elle doğrulayıp known_hosts dosyasına önceden koymak gerekir.)
    internal static bool SunucuTanidikMi(string host)
    {
        try { return File.Exists(BilinenSunucular) && File.ReadLines(BilinenSunucular).Any(l => !l.StartsWith('#') && l.Split(' ', 2)[0].Split(',').Any(t => t.Equals(host, StringComparison.OrdinalIgnoreCase) || t.StartsWith("[" + host + "]:", StringComparison.OrdinalIgnoreCase))); }
        catch { return false; }
    }

    // ---- Sunucu parmak izi (known_hosts + koruma hizmeti HKLM kanalı) ----
    // Arayüz bunu atar: bilinmeyen sunucunun parmak izini kullanıcıya gösterip onaylatır. Atanmamışsa (örn. komut satırı) bilinmeyen sunucuya BAĞLANILMAZ.
    public static Func<string, string, bool>? SunucuOnayi { get; set; }
    private static readonly System.Text.RegularExpressions.Regex ParmakIziDeseni = new(@"SHA256:[A-Za-z0-9+/=]{20,64}", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static string SshAracYolu(string ad) => File.Exists(@"C:\Windows\System32\OpenSSH\" + ad) ? @"C:\Windows\System32\OpenSSH\" + ad : ad;

    // known_hosts içindeki sunucunun parmak izi (yoksa null).
    internal static string? KayitliParmakIzi(string host, int port)
    {
        if (!File.Exists(BilinenSunucular)) return null; string ad = port == 22 ? host : $"[{host}]:{port}"; var (k, o) = SensorAnahtarYoneticisi.Keygen("-l", "-f", BilinenSunucular, "-F", ad);
        if (k != 0) return null; var m = ParmakIziDeseni.Match(o); return m.Success ? m.Value : null;
    }

    // Sunucu bilinen parmak izi değiştiğinde (kayıtlı ≠ şimdiki) arayüzün atayacağı onay penceresi: (sensör adı, adres, KAYITLI parmak izi, ŞİMDİKİ parmak izi) → güncellensin mi? Atanmamışsa reddedilir.
    public static Func<string, string, string, string, bool>? SunucuDegistiOnayi { get; set; }

    // ssh-keyscan ile sunucunun şimdiki parmak izini ve known_hosts satırını alır (alınamazsa null).
    // Testte ağ yerine sahte sunucu parmak izi verilebilir (üretimde atanmaz).
    internal static Func<SensorProfili, CancellationToken, Task<(string Satir, string ParmakIzi)?>>? CanliTarayiciGecici { get; set; }
    private static async Task<(string Satir, string ParmakIzi)?> CanliParmakIziAsync(SensorProfili p, CancellationToken ct)
    {
        if (CanliTarayiciGecici != null) return await CanliTarayiciGecici(p, ct);
        string tarama = ""; try { var psi = new ProcessStartInfo(SshAracYolu("ssh-keyscan.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (var a in new[] { "-T", "6", "-t", "ed25519", "-p", p.Port.ToString(CultureInfo.InvariantCulture), p.Host }) psi.ArgumentList.Add(a); using var pr = Process.Start(psi)!; var h = pr.StandardError.ReadToEndAsync(ct); tarama = await pr.StandardOutput.ReadToEndAsync(ct); if (!pr.WaitForExit(12000)) { try { pr.Kill(true); } catch (Exception hk7) { HataKayit.Sessiz(hk7, "SensorMantigi.CanliParmakIziAsync"); } } }
        catch (Exception hk8) { HataKayit.Sessiz(hk8, "SensorMantigi.CanliParmakIziAsync"); }
        var satir = tarama.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0 && !x.StartsWith('#')); if (satir == null) return null;
        string gecici = Path.Combine(Path.GetTempPath(), "bs_hk_" + Guid.NewGuid().ToString("N") + ".txt");
        try { File.WriteAllText(gecici, satir + "\n"); var (k, o) = SensorAnahtarYoneticisi.Keygen("-l", "-f", gecici); var m = ParmakIziDeseni.Match(o); return k == 0 && m.Success ? (satir, m.Value) : null; } finally { try { File.Delete(gecici); } catch (Exception hk9) { HataKayit.Sessiz(hk9, "SensorMantigi.CanliParmakIziAsync"); } }
    }

    // known_hosts içindeki (varsa eski) satırı siler ve yenisini yazar.
    private static void KnownHostsYaz(SensorProfili p, string tarananSatir)
    {
        string ad = p.Port == 22 ? p.Host : $"[{p.Host}]:{p.Port}"; if (File.Exists(BilinenSunucular)) SensorAnahtarYoneticisi.Keygen("-R", ad, "-f", BilinenSunucular); string[] parca = tarananSatir.Split(' ', 3);
        File.AppendAllText(BilinenSunucular, $"{ad} {parca[^2]} {parca[^1]}\n");
    }

    // Hizmetteki (HKLM) sensör kaydı: "adres|parmakIzi" (sensör ADINA göre). Hizmet yoksa ya da kayıt yoksa null.
    private static (string Host, string Fp)? HizmetKaydi(SensorProfili p)
    {
        if (!ServisIstemci.Calisiyor) return null; var r = ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = p.Ad }); if (r?.Tamam != true || r.Veri == null || !r.Veri.TryGetValue("kayit", out var k)) return null;
        var d = k.Split('|'); return d.Length == 2 ? (d[0], d[1]) : null;
    }
    private static bool HizmetKaydet(SensorProfili p, string fp) => ServisIstemci.Calisiyor && ServisIstemci.Gonder("hostkey-yaz", new() { ["ad"] = p.Ad, ["host"] = p.Host, ["parmakIzi"] = fp })?.Tamam == true;

    // Bağlantıdan ÖNCE parmak izi denetimi. null = devam; metin = bağlantı REDDEDİLİR (kullanıcıya gösterilecek açıklama).
    //  * Hizmet kaydı (HKLM, sensör adına göre) known_hosts ile/şimdiki sunucuyla UYUŞUYORSA devam; UYUŞMUYORSA bağlantı reddedilir ve kullanıcıya "sensör kimliği DEĞİŞTİ" penceresi gösterilir
    //    (kayıtlı ve şimdiki parmak izi yan yana; onay varsayılan Hayır). Onaylanırsa known_hosts ve HKLM kaydı güncellenir.
    //  * Hiç kayıt yoksa (ilk bağlantı): parmak izi alınır, kullanıcıya doğrulatılır; onay yoksa bağlanılmaz. "accept-new" YOKTUR.
    internal static async Task<string?> ParmakIziDenetleAsync(SensorProfili p, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BilinenSunucular)!); string? yerel = KayitliParmakIzi(p.Host, p.Port); var kayit = HizmetKaydi(p);
        bool kayitAdres = kayit != null && string.Equals(kayit.Value.Host, p.Host, StringComparison.Ordinal);
        // 1) known_hosts'ta kayıt var ve hizmet kaydıyla tutarlı → devam.
        // known_hosts, hizmet kaydından FARKLI ise (dosya değiştirilmiş olabilir) sunucuya ulaşılamasa bile alarm yazılır.
        if (yerel != null && kayit != null && kayitAdres && yerel != kayit.Value.Fp) { try { OlayDeposu.Ekle("Sensör", "Kritik", $"SENSÖR ALARM -> {p.Ad} ({p.Host}): known_hosts parmak izi ({yerel}) koruma hizmeti kaydıyla ({kayit.Value.Fp}) UYUŞMUYOR."); } catch (Exception hk10) { HataKayit.Sessiz(hk10, "SensorMantigi.ParmakIziDenetleAsync"); } }
        if (yerel != null && kayit == null) { if (ServisIstemci.Calisiyor) HizmetKaydet(p, yerel); return null; }
        if (yerel != null && kayitAdres && yerel == kayit!.Value.Fp) return null;
        if (yerel != null && kayit == null) return null;
        // 2) Buraya gelindiyse: ya known_hosts yok, ya da hizmet kaydıyla UYUŞMUYOR. Sunucunun ŞİMDİKİ parmak izini al.
        var canli = await CanliParmakIziAsync(p, ct); if (canli == null) return yerel != null ? $"SUNUCU PARMAK İZİ DOĞRULANAMADI: {p.Ad} ({p.Host}) için kayıtlar uyuşmuyor ve sunucuya şu an ulaşılamıyor. Bağlantı REDDEDİLDİ." : "Sunucuya ulaşılamadı ya da parmak izi alınamadı (kapı kapalı ya da SSH yok).";
        // 2a) Hizmet kaydı var ve şimdiki sunucu kayıtla AYNI → known_hosts onarılır, devam (kayıt otoritedir).
        if (kayit != null && kayitAdres && canli.Value.ParmakIzi == kayit.Value.Fp) { KnownHostsYaz(p, canli.Value.Satir); return null; }
        // 2b) Kayıt var ama FARKLI (kimlik değişti) ya da known_hosts ile hizmet uyuşmuyor → kullanıcıya göster; onay yoksa REDDET.
        if (kayit != null || yerel != null)
        {
            string eski = kayit?.Fp ?? yerel!; try { OlayDeposu.Ekle("Sensör", "Kritik", $"SENSÖR ALARM -> {p.Ad} ({p.Host}) sunucu parmak izi DEĞİŞTİ (kayıtlı {eski}, şimdi {canli.Value.ParmakIzi}); bağlantı onay bekliyor."); } catch (Exception hk11) { HataKayit.Sessiz(hk11, "SensorMantigi.ParmakIziDenetleAsync"); }
            if (SunucuDegistiOnayi == null || !SunucuDegistiOnayi(p.Ad, p.Host, eski, canli.Value.ParmakIzi)) return $"SENSÖR KİMLİĞİ DEĞİŞTİ: {p.Ad} ({p.Host}) için kayıtlı parmak izi {eski}, şimdiki {canli.Value.ParmakIzi}. Sensör yeniden kurulmuş olabilir ya da ağda biri araya girmiş olabilir. Bağlantı REDDEDİLDİ; sensörde parmak izini doğrulayıp sensör ekranından onaylayın.";
            KnownHostsYaz(p, canli.Value.Satir); if (ServisIstemci.Calisiyor && !HizmetKaydet(p, canli.Value.ParmakIzi)) return "Parmak izi onaylandı ama koruma hizmeti kaydı güncelleyemedi; bağlantı yapılmadı."; try { OlayDeposu.Ekle("Sensör", "Uyarı", $"SENSÖR UYARI -> {p.Ad} yeni parmak izi kullanıcı tarafından onaylandı ve kaydedildi ({canli.Value.ParmakIzi})."); } catch (Exception hk12) { HataKayit.Sessiz(hk12, "SensorMantigi.ParmakIziDenetleAsync"); }
            return null;
        }
        // 3) İlk bağlantı: kullanıcı doğrular.
        if (SunucuOnayi == null || !SunucuOnayi(p.Host, canli.Value.ParmakIzi)) return $"Sunucu parmak izi ONAYLANMADI ({canli.Value.ParmakIzi}); güvenmediğiniz bir sunucuya bağlanılmaz. Sensör ekranından parmak izini sensörde doğrulayıp onaylayın.";
        KnownHostsYaz(p, canli.Value.Satir); HizmetKaydet(p, canli.Value.ParmakIzi); try { OlayDeposu.Ekle("Sensör", "Bilgi", $"SENSÖR BİLGİ -> {p.Ad} ({p.Host}) sunucu parmak izi kullanıcı tarafından onaylandı ve kaydedildi ({canli.Value.ParmakIzi})."); } catch (Exception hk13) { HataKayit.Sessiz(hk13, "SensorMantigi.ParmakIziDenetleAsync"); }
        return null;
    }
}
