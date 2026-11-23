// Bu dosya Kali sensörünün DURUMUNU yönetir: program açılırken (ve periyodik olarak) arka planda sensöre bağlanıp anahtarı, Python'u ve ajanı sınar;
// sonucu "Aktif / Kapalı / Sorun / Tanımlı değil" olarak sade bir durumla verir. Sensör kapalıysa kullanıcı sessiz bırakılmaz: uyarı çıkar ve sanal makine onayla başlatılabilir.
// Ayrıca KaliKonsol: sensörde çalışan komutların (ssh, ajan, nmap) canlı çıktılarının halka belleği (Uzman Görünümü'ndeki Kali Konsolu).
// Bu dosya sensörde YALNIZCA mevcut onaylı "hello" ön kontrolünü çalıştırır; hiçbir tarama veya sistem değişikliği yapmaz.
using System.Collections.Concurrent;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Kali sensörünün durumu.
internal enum KaliDurumu { Bilinmiyor, Kontrol, Aktif, Kapali, Sorun, SensorYok }

// Bir kontrolün sonucu.
internal sealed record KaliKontrolSonucu(KaliDurumu Durum, string Kisa, string Ayrinti, string Sensor, DateTime Zaman);

// Kali konsolu: sensör komutlarının satır satır kaydı (son 600 satır).
internal static class KaliKonsol
{
    private static readonly ConcurrentQueue<string> Satirlar = new();
    private const int EnCok = 600;
    // Yeni satır gelince çağrılır (iş parçacığı belirsiz: arayüz BeginInvoke ile almalı).
    public static event Action<string>? SatirGeldi;

    // Satır ekler (denetim karakterleri temizlenir, çok uzunsa kısaltılır).
    public static void Yaz(string kaynak, string metin)
    {
        string t = new string((metin ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim(); if (t.Length > 400) t = t[..400] + $"… (+{t.Length - 400} karakter)";
        string satir = $"{DateTime.Now:HH:mm:ss} [{kaynak}] {t}"; Satirlar.Enqueue(satir); while (Satirlar.Count > EnCok && Satirlar.TryDequeue(out _)) { }
        try { SatirGeldi?.Invoke(satir); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KaliDurum.Yaz"); }
    }
    public static List<string> Hepsi() => Satirlar.ToList();
    public static void Temizle() { while (Satirlar.TryDequeue(out _)) { } }

    // Komutun ekranda gösterilecek güvenli etiketi: base64 yükü gösterilmez, yalnızca ajan kipi.
    public static string KomutEtiketi(string uzakKomut) => uzakKomut.StartsWith("python3 - run ", StringComparison.Ordinal) ? "python3 - run <şifreli parametre>" : uzakKomut.Length > 80 ? uzakKomut[..80] + "…" : uzakKomut;
}

// Durum yöneticisi.
internal static class KaliDurumYoneticisi
{
    // Test için sınama işlevi değiştirilebilir.
    public static Func<SensorProfili, CancellationToken, Task<SensorSonuc>> Sina { get; set; } = SensorTaramaYurutucu.SinaAsync;
    // Test için sensör listesi kaynağı.
    public static Func<List<SensorProfili>> Sensorler { get; set; } = SensorDeposu.Yukle;
    // Son sonuç ve değişim olayı.
    public static KaliKontrolSonucu Son { get; private set; } = new(KaliDurumu.Bilinmiyor, "Henüz kontrol edilmedi", "", "", DateTime.MinValue);
    public static event Action? Degisti;
    private static int _calisiyor;
    // Bir kez "Aktif" görüldü mü? (kopma bildirimi yalnızca o zaman verilir; açılışta kapalı olması "kopma" değildir)
    private static bool _aktifGorulmus;

    // Kali sensörünü seçer: adında "kali" geçen, yoksa ilk kayıt.
    public static SensorProfili? SensorSec(List<SensorProfili> l) => l.FirstOrDefault(s => s.Ad.Contains("kali", StringComparison.OrdinalIgnoreCase)) ?? l.FirstOrDefault();

    // Sınama çıktısını sade duruma çevirir (saf: test edilebilir).
    public static KaliKontrolSonucu Sinifla(SensorSonuc s, string sensorAdi, DateTime zaman)
    {
        string o = s.Ozet ?? "";
        if (s.Basarili) return new(KaliDurumu.Aktif, "Aktif", $"Bağlantı, anahtar, Python ve ajan tamam. {o.Replace("\r\n", " ")}", sensorAdi, zaman);
        if (o.Contains("Anahtar reddedildi", StringComparison.OrdinalIgnoreCase) || o.Contains("anahtar", StringComparison.OrdinalIgnoreCase) && o.Contains("reddedildi", StringComparison.OrdinalIgnoreCase)) return new(KaliDurumu.Sorun, "Anahtar reddedildi", "Sensör bu bilgisayarın anahtarını kabul etmiyor. \"Uzak Sensörler\" penceresinde \"Ortak anahtarı göster\" ile komutu sensörde çalıştırın. " + o, sensorAdi, zaman);
        if (o.Contains("parmak izi", StringComparison.OrdinalIgnoreCase) || o.Contains("host key", StringComparison.OrdinalIgnoreCase) || o.Contains("REMOTE HOST IDENTIFICATION", StringComparison.OrdinalIgnoreCase)) return new(KaliDurumu.Sorun, "Sensör kimliği değişmiş", "Sensörün parmak izi değişmiş görünüyor (başka bir makine olabilir). Doğruluğunu teyit etmeden bağlanmayın. " + o, sensorAdi, zaman);
        if (o.Contains("Python3: YOK", StringComparison.OrdinalIgnoreCase)) return new(KaliDurumu.Sorun, "Sensörde Python yok", "Bağlantı var ama sensörde python3 kurulu değil: ajan çalışamaz. " + o.Replace("\r\n", " "), sensorAdi, zaman);
        if (o.Contains("Ajan çalışmadı", StringComparison.OrdinalIgnoreCase)) return new(KaliDurumu.Sorun, "Ajan çalışmadı", o.Replace("\r\n", " "), sensorAdi, zaman);
        if (o.Contains("Bağlanılamadı", StringComparison.OrdinalIgnoreCase) || o.Contains("zaman aşımı", StringComparison.OrdinalIgnoreCase) || o.Contains("ulaşılamıyor", StringComparison.OrdinalIgnoreCase) || o.Contains("reddetti", StringComparison.OrdinalIgnoreCase)) return new(KaliDurumu.Kapali, "Kali Sensör Kapalı / Bağlantı Bekleniyor", "Sensöre ulaşılamıyor: sanal makine kapalı olabilir. " + o.Replace("\r\n", " "), sensorAdi, zaman);
        return new(KaliDurumu.Sorun, "Sensör beklenmedik cevap verdi", o.Replace("\r\n", " "), sensorAdi, zaman);
    }

    // Göstergede görünecek (kısa yazı, renk 0=yeşil 1=sarı 2=kırmızı 3=gri).
    public static (string Yazi, int Renk) Gosterge(KaliKontrolSonucu s) => s.Durum switch
    {
        KaliDurumu.Aktif => ("Aktif", 0), KaliDurumu.Kontrol => ("Kontrol ediliyor...", 3), KaliDurumu.Kapali => ("Kapalı / Bağlantı Bekleniyor", 2),
        KaliDurumu.Sorun => (s.Kisa, 1), KaliDurumu.SensorYok => (UzmanPaketi.PasifYazi, 3), _ => ("Henüz kontrol edilmedi", 3)
    };

    // Kontrolü çalıştırır (aynı anda tek kontrol). ct: iptal. Hata olursa Sorun olarak döner; asla istisna fırlatmaz.
    public static async Task<KaliKontrolSonucu> KontrolEtAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _calisiyor, 1) == 1) return Son;
        try
        {
            var p = SensorSec(Sensorler()); var onceki = Son.Durum;
            if (p == null) return Ayarla(new(KaliDurumu.SensorYok, UzmanPaketi.PasifYazi, UzmanPaketi.PasifAciklama, "", DateTime.Now));
            Ayarla(new(KaliDurumu.Kontrol, "Kontrol ediliyor...", "", p.Ad, DateTime.Now)); KaliKonsol.Yaz("durum", $"Kali sensör denetimi başladı: {p.Ad} ({p.Host})");
            // Anahtar dosyası var mı? (yoksa bağlanmaya çalışmaya gerek yok)
            if (string.IsNullOrWhiteSpace(p.AnahtarYolu) || !File.Exists(p.AnahtarYolu)) return Bitir(new(KaliDurumu.Sorun, "Anahtar dosyası yok", "SSH anahtar dosyası bulunamadı. \"Uzak Sensörler\" > \"Anahtar Üret\" ile oluşturun.", p.Ad, DateTime.Now), onceki);
            SensorSonuc s; try { using var zs = CancellationTokenSource.CreateLinkedTokenSource(ct); zs.CancelAfter(TimeSpan.FromSeconds(45)); s = await Sina(p, zs.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { s = new(false, "", "", false, "Bağlanılamadı: zaman aşımı."); }
            catch (Exception ex) when (ex is not OperationCanceledException) { s = new(false, "", "", false, "Bağlanılamadı: " + ex.Message); }
            return Bitir(Sinifla(s, p.Ad, DateTime.Now), onceki);
        }
        finally { Interlocked.Exchange(ref _calisiyor, 0); }
    }

    // Sonucu saklar, konsola yazar ve (aktifken kopma varsa) olay günlüğüne KRİTİK düşer.
    private static KaliKontrolSonucu Bitir(KaliKontrolSonucu r, KaliDurumu onceki)
    {
        KaliKonsol.Yaz("durum", $"{r.Kisa} — {r.Ayrinti}"); Ayarla(r);
        if (r.Durum == KaliDurumu.Aktif) _aktifGorulmus = true;
        else if (_aktifGorulmus && onceki == KaliDurumu.Aktif && r.Durum is KaliDurumu.Kapali or KaliDurumu.Sorun) { try { OlayDeposu.Ekle("Sensör", "Kritik", $"SENSÖR -> Kali sensör bağlantısı koptu: {r.Kisa}"); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "KaliDurum.Bitir"); } }
        return r;
    }
    private static KaliKontrolSonucu Ayarla(KaliKontrolSonucu r) { Son = r; try { Degisti?.Invoke(); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "KaliDurum.Ayarla"); } return r; }

    // Arka planda başlatır (açılış ekranından: beklemez).
    public static void ArkaPlandaBaslat() { _ = Task.Run(async () => { try { await KontrolEtAsync(); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "KaliDurum.ArkaPlandaBaslat"); } }); }

    // Test için durumu sıfırlar.
    internal static void Sifirla() { Son = new(KaliDurumu.Bilinmiyor, "Henüz kontrol edilmedi", "", "", DateTime.MinValue); _aktifGorulmus = false; Interlocked.Exchange(ref _calisiyor, 0); }

    // ---- Sanal makineyi uyandırma (VirtualBox) ----
    // Kali'ye benzeyen kayıtlı sanal makineleri verir (kapalı olanlar başa). Arayüz kullanıcıya sorar; bu işlev kendisi hiçbir şey başlatmaz.
    public static List<VBoxVm> AdayMakineler(List<VBoxVm>? vmler) => (vmler ?? new()).OrderByDescending(v => v.Ad.Contains("kali", StringComparison.OrdinalIgnoreCase)).ThenBy(v => v.Durum.Contains("running", StringComparison.OrdinalIgnoreCase)).ThenBy(v => v.Ad).ToList();
}
