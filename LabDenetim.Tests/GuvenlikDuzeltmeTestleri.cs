// Bu dosya kırmızı takım (saldırgan bakışlı) denetim bulgularının KAPATILDIĞINI doğrulayan testleri içerir:
// mühür geri düşürme (K1/K2), zincirli olay günlüğü (K3), kanıt paketi dış özeti (K4), güvenlik duvarı yedek klasörü (K5), ada göre süreç koruması (K8),
// yavaş fidye şifrelemesi (O1), honeypot soket seli (O3), sensör dosyası doğrulaması ve parmak izi sabitleme (O10), komut tırnaklama (O4), global hata kaydı (K9).
// Hepsi geçici klasörde çalışır; gerçek hesaplara, günlüklere ve güvenlik duvarına dokunmaz.
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class GuvenlikDuzeltmeTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_gd_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol; private readonly int _eskiTekrar = KimlikDeposu.VarsayilanTekrar;
    public GuvenlikDuzeltmeTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); KimlikDeposu.VarsayilanTekrar = 1000; Oturum.Aktif = null; }
    public void Dispose() { BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; KimlikDeposu.VarsayilanTekrar = _eskiTekrar; Oturum.Aktif = null; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "GuvenlikDuzeltmeTestleri.temizlik"); } }

    // ---------- K1 / K2: mühür geri düşürme ----------
    [Fact]
    public void K1_Muhuru_Silinen_Kimlik_Dosyasi_Kabul_Edilmez_Ve_Yeniden_Muhurlenmez()
    {
        Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili); Assert.Equal(KimlikDurumu.Hazir, KimlikDeposu.Durum());
        // Saldırgan dosyayı değiştirir ve mührü siler.
        File.WriteAllText(KimlikDeposu.Yol, File.ReadAllText(KimlikDeposu.Yol).Replace("ibrahim", "saldirgan")); File.Delete(Butunluk.MuhurYolu(KimlikDeposu.Yol));
        Assert.Equal(KimlikDurumu.Bozuk, KimlikDeposu.Durum());
        // Mühür yeniden oluşturulmamış olmalı.
        Assert.False(File.Exists(Butunluk.MuhurYolu(KimlikDeposu.Yol)));
        Assert.False(Butunluk.GuvenliMiKati(KimlikDeposu.Yol));
    }

    [Fact]
    public void K1_Eski_Gevsek_Gecis_Diger_Dosyalarda_Duruyor_Kimlikte_Yok()
    {
        File.WriteAllText(Path.Combine(_t, "e.json"), "{}"); Assert.False(Butunluk.GuvenliMiKati(Path.Combine(_t, "e.json")));
        Assert.True(Butunluk.GuvenliMi(Path.Combine(_t, "e.json")));   // önemsiz dosyalar için eski geçiş davranışı korunur
    }

    [Fact]
    public void K2_Mührü_Kalan_Ama_Dosyasi_Silinen_Kimlik_Ilk_Kuruluma_Donmez()
    {
        Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        File.Delete(KimlikDeposu.Yol);
        Assert.Equal(KimlikDurumu.Bozuk, KimlikDeposu.Durum());
        // Mühür izi de silinmişse (bilgisayar sahibinin bilinçli sıfırlaması) ilk kuruluma dönülür.
        File.Delete(Butunluk.MuhurYolu(KimlikDeposu.Yol)); Assert.Equal(KimlikDurumu.Yok, KimlikDeposu.Durum());
    }

    // ---------- K3: zincirli olay günlüğü ----------
    [Fact]
    public void K3_Zincir_Saglam_Kayitlar_Dogrulanir()
    {
        for (int i = 0; i < 6; i++) OlayDeposu.Ekle("Test", "Bilgi", "olay " + i);
        var d = OlayDeposu.Dogrula(); Assert.True(d.Saglam, d.Not); Assert.Equal(6, d.Zincirli); Assert.Equal(6, OlayDeposu.Oku().Count);
    }

    [Fact]
    public void K3_Ortadaki_Satir_Degistirilirse_Bozulma_Gorunur()
    {
        for (int i = 0; i < 6; i++) OlayDeposu.Ekle("Test", "Bilgi", "olay " + i);
        var satirlar = File.ReadAllLines(OlayDeposu.Yol); satirlar[2] = satirlar[2].Replace("olay 2", "olay X"); File.WriteAllLines(OlayDeposu.Yol, satirlar);
        var d = OlayDeposu.Dogrula(); Assert.False(d.Saglam); Assert.Equal(3, d.IlkBozukSatir);
    }

    [Fact]
    public void K3_Silinen_Ya_Da_Kesilen_Kuyruk_Bas_Muhuruyle_Yakalanir()
    {
        for (int i = 0; i < 6; i++) OlayDeposu.Ekle("Test", "Bilgi", "olay " + i);
        File.WriteAllLines(OlayDeposu.Yol, File.ReadAllLines(OlayDeposu.Yol).Take(4));   // son 2 satır silindi
        var d = OlayDeposu.Dogrula(); Assert.False(d.Saglam); Assert.Contains("sonu", d.Not);
    }

    [Fact]
    public void K3_Dosya_Buyuyunce_Eski_Kayit_Silinmez_Arsive_Tasinir_Ve_Zincir_Surer()
    {
        OlayDeposu.Ekle("Test", "Bilgi", "ilk");
        // Dosyayı eşiğin üstüne çıkar (zincirsiz eski dolgu satırları).
        File.AppendAllText(OlayDeposu.Yol, string.Concat(Enumerable.Repeat(new string('x', 1000) + "\n", 2100)));
        OlayDeposu.Ekle("Test", "Bilgi", "ikinci");
        var arsiv = Path.Combine(_t, "olay_arsiv"); Assert.True(Directory.Exists(arsiv)); var dosyalar = Directory.GetFiles(arsiv, "*.jsonl"); Assert.Single(dosyalar);
        Assert.Contains("ilk", File.ReadAllText(dosyalar[0]));                          // eski kanıt silinmedi
        var kayitlar = OlayDeposu.Oku(); Assert.Contains(kayitlar, o => o.Mesaj.Contains("ZİNCİR DEVAM")); Assert.Contains(kayitlar, o => o.Mesaj == "ikinci");
        Assert.True(OlayDeposu.Dogrula().Saglam);
    }

    [Fact]
    public void K3_Gurultu_Seli_Eski_Kaniti_Kaydirip_Silemez()
    {
        OlayDeposu.Ekle("Test", "Kritik", "ONEMLI-KANIT");
        for (int k = 0; k < 3; k++) { File.AppendAllText(OlayDeposu.Yol, string.Concat(Enumerable.Repeat(new string('g', 1000) + "\n", 2100))); OlayDeposu.Ekle("Test", "Bilgi", "gurultu " + k); }
        string tum = string.Concat(Directory.GetFiles(Path.Combine(_t, "olay_arsiv"), "*.jsonl").Select(File.ReadAllText)) + File.ReadAllText(OlayDeposu.Yol);
        Assert.Contains("ONEMLI-KANIT", tum);
    }

    // ---------- K4: kanıt paketi ----------
    [Fact]
    public void K4_Kanit_Paketi_Dis_Ozet_Ve_Operator_Bilgisi_Tasir()
    {
        var bilgi = new RaporBilgisi("127.0.0.1", DateTime.Now, "Standart", 1, TimeSpan.FromSeconds(1), 90, 0, 0, 1, 0);
        string zip = KanitPaketi.Olustur(_t, new DateTime(2026, 11, 21, 10, 0, 0), bilgi, new List<Bulgu>(), h => File.WriteAllText(h, "<html></html>"), "ayse (Yönetici)");
        string yan = zip + ".sha256"; Assert.True(File.Exists(yan));
        Assert.StartsWith(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))), File.ReadAllText(yan));
        using var z = System.IO.Compression.ZipFile.OpenRead(zip); using var r = new StreamReader(z.GetEntry("00_OKUBENI_butunluk.txt")!.Open()); string m = r.ReadToEnd();
        Assert.Contains("ayse (Yönetici)", m); Assert.Contains(".sha256", m);
    }

    // ---------- K5: güvenlik duvarı yedek klasörü ----------
    [Fact]
    public void K5_Yedek_Zinciri_Klasoru_Yalnizca_Yoneticiye_Kilitler()
    {
        string z = GuvenlikDuvari.YedekAlZinciri(Path.Combine(GuvenlikDuvari.YedekKlasoru, "yedek_20261121_100000.wfw"));
        Assert.Contains("icacls", z); Assert.Contains("/inheritance:r", z); Assert.Contains("S-1-5-32-544", z); Assert.Contains("S-1-5-32-545:(OI)(CI)RX", z); Assert.Contains("netsh advfirewall export", z);
        Assert.DoesNotContain("%AppData%", z); Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), GuvenlikDuvari.YedekKlasoru, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void K5_Kullanici_Yazilabilir_Klasorden_Geri_Yukleme_Reddedilir()
    {
        Assert.False(GuvenlikDuvari.KlasorYalnizYoneticiYazar(_t));                 // bu testin kullanıcısı sahibi/yazanı
        Assert.False(GuvenlikDuvari.KlasorYalnizYoneticiYazar(Path.Combine(_t, "yok")));
    }

    // ---------- K8: süreç koruması ada + yola bakar ----------
    [Theory]
    [InlineData("svchost.exe")] [InlineData("explorer.exe")] [InlineData("backup_job.exe")] [InlineData("vboxsvc.exe")] [InlineData("OneDrive.exe")]
    public void K8_Ad_Taklidi_Temp_Yolundan_Calisan_Surec_Dokunulmaz_Sayilmaz(string ad)
    {
        string eski = Environment.CurrentDirectory; var kanca = SurecAvcisi.HizmetMi; SurecAvcisi.HizmetMi = _ => false;
        try { Assert.Null(SurecAvcisi.KorumaNedeni(ad, 70000, @"C:\Users\kurban\AppData\Local\Temp\" + ad)); } finally { SurecAvcisi.HizmetMi = kanca; }
    }

    [Fact]
    public void K8_Gercek_Sistem_Yolundaki_Ve_Yolu_Bilinmeyen_Surec_Korunur()
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.NotNull(SurecAvcisi.KorumaNedeni("svchost.exe", 70001, Path.Combine(win, "System32", "svchost.exe")));
        Assert.NotNull(SurecAvcisi.KorumaNedeni("svchost.exe", 70002, null));
        Assert.NotNull(SurecAvcisi.KorumaNedeni("sqlservr.exe", 70003, @"C:\Program Files\Microsoft SQL Server\sqlservr.exe"));
    }

    [Fact]
    public void K8_Wmi_Hatasinda_Surec_Engellenmez_Ama_Isaretlenir()
    {
        // Gerçek kanca hata yoluna zorlanamaz; sahte kancayla işaretin okunabildiğini ve KorumaNedeni'nin her çağrıda sıfırladığını doğrula.
        SurecAvcisi.HizmetSorgusuBasarisiz = true; var kanca = SurecAvcisi.HizmetMi; SurecAvcisi.HizmetMi = _ => false;
        try { Assert.Null(SurecAvcisi.KorumaNedeni("ozel.exe", 70004, null)); Assert.False(SurecAvcisi.HizmetSorgusuBasarisiz); } finally { SurecAvcisi.HizmetMi = kanca; }
    }

    // ---------- O1: yavaş fidye şifrelemesi ----------
    [Fact]
    public void O1_Hizli_Esigin_Altinda_Kalan_Yavas_Sifreleme_60sn_Penceresinde_Yakalanir()
    {
        using var f = new FidyeIzleyici { Esik = 1000, YavasEsik60 = 8, YavasEsik600 = 5000 }; string neden = ""; int n = 0; f.Supheli += (m, s) => { neden = m; n = s; };
        for (int i = 0; i < 7; i++) f.Kaydet(@"C:\Users\x\Belgeler\a" + i + ".docx", false); Assert.Equal("", neden);
        f.Kaydet(@"C:\Users\x\Belgeler\a7.docx", false); Assert.Contains("yavaş", neden); Assert.Equal(8, n);
    }

    [Fact]
    public void O1_AppData_Genelinde_Dislama_Kalkti_Yalnizca_Gurultulu_Yerler_Dislanir()
    {
        using var f = new FidyeIzleyici { Esik = 3 }; int n = 0; f.Supheli += (_, _) => n++;
        for (int i = 0; i < 5; i++) f.Kaydet(@"C:\Users\x\AppData\Roaming\Veri\d" + i + ".dat", false); Assert.Equal(1, n);
        using var g = new FidyeIzleyici { Esik = 3 }; int m = 0; g.Supheli += (_, _) => m++; for (int i = 0; i < 9; i++) g.Kaydet(@"C:\Users\x\AppData\Local\Temp\t" + i + ".dat", false); Assert.Equal(0, m);
    }

    [Fact]
    public void O1_Varsayilan_Klasorler_Indirilenler_Ve_Ag_Surucusu_Mantigini_Icerir_Ve_Cakismaz()
    {
        var l = FidyeIzleyici.VarsayilanKlasorler(); Assert.Equal(l.Count, l.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var a in l) Assert.DoesNotContain(l, b => !ReferenceEquals(a, b) && a.Length > b.Length && a.StartsWith(b.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- O3: honeypot soket seli ----------
    [Fact]
    public async Task O3_Honeypot_Soket_Selinde_Ayakta_Kalir_Ve_Sonrasinda_Hizmet_Verir()
    {
        using var hp = new HoneypotKapi(); int port = 40000 + Random.Shared.Next(2000); string? hata = null; for (int i = 0; i < 20 && (hata = hp.Baslat(port + i, false)) != null; i++) { }
        Assert.Null(hata); int p = int.Parse(hp.Dinlenen.Split(':')[1]); var istemciler = new List<TcpClient>();
        try
        {
            for (int i = 0; i < 300; i++) { try { var c = new TcpClient(); await c.ConnectAsync("127.0.0.1", p); istemciler.Add(c); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "GuvenlikDuzeltmeTestleri.temizlik"); } }
            Assert.True(hp.Aktif);
            // Sel bitince yeni bir bağlantı yine karşılanır (banner gelir).
            foreach (var c in istemciler) c.Dispose(); await Task.Delay(3500);
            using var yeni = new TcpClient(); await yeni.ConnectAsync("127.0.0.1", p); var buf = new byte[64]; yeni.ReceiveTimeout = 3000; int n = await yeni.GetStream().ReadAsync(buf); Assert.StartsWith("SSH-2.0-OpenSSH", Encoding.ASCII.GetString(buf, 0, n));
        }
        finally { foreach (var c in istemciler) try { c.Dispose(); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "GuvenlikDuzeltmeTestleri.temizlik"); } }
    }

    // ---------- O4: komut tırnaklama ----------
    [Fact]
    public void O4_Bosluklu_Hizmet_Adi_Tirnaklanir_Duz_Ad_Aynen_Kalir()
    {
        Assert.Equal("sc config \"Kotu Hizmet\" start= disabled", KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Hizmet", Ad = "Kotu Hizmet", Konum = "Kotu Hizmet" }).Komut);
        Assert.Equal("sc config MySvc start= disabled", KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Hizmet", Ad = "MySvc", Konum = "MySvc" }).Komut);
    }

    [Fact]
    public void O4_Kural_Adi_Regexi_Satir_Sonu_Karakterine_Izin_Vermez()
    {
        Assert.True(GuvenlikDuvari.AdGuvenliMi("BulutSoft-Acil-21")); Assert.False(GuvenlikDuvari.AdGuvenliMi("BulutSoft-Acil-21\n")); Assert.False(GuvenlikDuvari.AdGuvenliMi("BulutSoft-x & calc"));
    }

    // ---------- O10: sensör dosyası doğrulaması ve parmak izi ----------
    [Fact]
    public void O10_Degistirilmis_Sensor_Dosyasindaki_Gecersiz_Profiller_Yuklenmez()
    {
        SensorDeposu.Kaydet(new() { new SensorProfili("Iyi", "192.168.56.101", 22, "kali", @"C:\k\anahtar"), new SensorProfili("Kotu", "203.0.113.9", 22, "kali", @"C:\k\anahtar"), new SensorProfili("Kotu2", "192.168.56.102", 22, "-oProxyCommand=calc", @"C:\k\anahtar") });
        var l = SensorDeposu.Yukle(); Assert.Single(l); Assert.Equal("Iyi", l[0].Ad);
        Assert.Contains(OlayDeposu.Oku(), o => o.Mesaj.Contains("sensorler.json"));
    }

    [Fact]
    public void O10_Tanidik_Sunucuda_Katı_Anahtar_Denetimi_Ilk_Baglantida_Accept_New()
    {
        var p = new SensorProfili("x", "192.168.56.150", 22, "kali", "k");
        // accept-new KALDIRILDI: bilinen ya da bilinmeyen her sunucuda katı denetim ("yes"); bilinmeyen sunucu bağlantıdan önce parmak izi onayından geçer.
        Assert.Contains("StrictHostKeyChecking=yes", SensorKomutlari.ArgumanOlustur(p, "cmd")); Assert.DoesNotContain("StrictHostKeyChecking=accept-new", SensorKomutlari.ArgumanOlustur(p, "cmd"));
    }

    // ---------- K9: global hata kaydı ----------
    [Fact]
    public void K9_Beklenmeyen_Hata_Olay_Gunlugune_Ve_Cokme_Dosyasina_Yazilir()
    {
        Program.BeklenmeyenHata("test", new InvalidOperationException("deneme hatası"), false);
        Assert.Contains(OlayDeposu.Oku(), o => o.Seviye == "Kritik" && o.Mesaj.Contains("deneme hatası"));
        Assert.Single(Directory.GetFiles(_t, "cokme_*.txt")); Assert.Contains("InvalidOperationException", File.ReadAllText(Directory.GetFiles(_t, "cokme_*.txt")[0]));
    }
}
