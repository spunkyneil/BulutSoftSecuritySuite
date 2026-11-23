// Bu dosya koruma hizmeti (BulutSoftGuard) ile arayüz arasındaki uçtan uca davranışı doğrular: hizmet bu test sürecinin İÇİNDE, geliştirme kipinde
// (HKCU test anahtarı, geçici klasör, benzersiz boru adı) çalıştırılır; gerçek hizmete, HKLM'ye ve ProgramData'ya dokunulmaz.
// Doğrulananlar: ping/durum, yetkili ve yetkisiz mühür yazımı, K1/K2 ikinci kanal (mühür silme, dosya silme, sahte yerel mühür), hizmet kapalıyken kritik değişikliğin reddi,
// paket imzası, SSH parmak izi kaydı ve süreç bazlı yazma sayacı.
using BulutSoftGuardService;
using Microsoft.Win32;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class GuardHizmetTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_guard_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol;
    private readonly int _eskiTekrar = KimlikDeposu.VarsayilanTekrar; private readonly RegistryKey _eskiKok = IkinciKanal.Kok; private readonly string _eskiAlt = IkinciKanal.AltAnahtar, _eskiPipe = ServisIstemci.PipeAdi, _anahtar = @"SOFTWARE\BulutSoftTest_" + Guid.NewGuid().ToString("N")[..8];
    private readonly string _pipe = "TestGuard_" + Guid.NewGuid().ToString("N")[..8]; private GuardCekirdek? _c; private PipeSunucu? _s;

    public GuardHizmetTestleri()
    {
        Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); KimlikDeposu.VarsayilanTekrar = 1000; Oturum.Aktif = null;
        IkinciKanal.Kok = Registry.CurrentUser; IkinciKanal.AltAnahtar = _anahtar + @"\Muhur"; ServisIstemci.PipeAdi = _pipe; ServisIstemci.Sifirla();
    }
    public void Dispose()
    {
        try { _s?.StopAsync(CancellationToken.None).Wait(2000); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "GuardHizmetTestleri.temizlik"); } try { _c?.Dispose(); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "GuardHizmetTestleri.temizlik"); }
        try { Registry.CurrentUser.DeleteSubKeyTree(_anahtar, false); Registry.CurrentUser.DeleteSubKeyTree(_anahtar + "Gizli", false); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "GuardHizmetTestleri.temizlik"); }
        IkinciKanal.Kok = _eskiKok; IkinciKanal.AltAnahtar = _eskiAlt; ServisIstemci.PipeAdi = _eskiPipe; ServisIstemci.Sifirla(); BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; KimlikDeposu.VarsayilanTekrar = _eskiTekrar; Oturum.Aktif = null;
        try { Directory.Delete(_t, true); } catch (Exception hkt4) { HataKayit.Sessiz(hkt4, "GuardHizmetTestleri.temizlik"); }
    }

    // Hizmeti bu süreçte başlatır. izinli: pipe'a yazma yetkisi verilen uygulama yolu (null = bu test süreci).
    private async Task Baslat(string? izinli = null)
    {
        var a = new GuardAyarlari { Gelistirme = true, PipeAdi = _pipe, Klasor = Path.Combine(_t, "guard"), IzinliUygulama = izinli ?? Environment.ProcessPath!, ProfilKoku = Path.Combine(_t, "profiller") };
        Directory.CreateDirectory(a.ProfilKoku); _c = new GuardCekirdek(a); _s = new PipeSunucu(_c, a); await _s.StartAsync(CancellationToken.None);
        // Hizmet çekirdeği statik günlük yolunu kendi klasörüne taşır; istemci tarafı (bu test) kendi günlüğüne geri döner.
        OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); BulutKlasor.Kok = _t;
        for (int i = 0; i < 40 && !ServisIstemci.Calisiyor; i++) { await Task.Delay(100); ServisIstemci.Sifirla(); }
    }

    private static readonly string Hash64 = new('A', 64);

    [Fact]
    public async Task Ping_Ve_Durum_Cevap_Verir()
    {
        await Baslat(); Assert.True(ServisIstemci.Calisiyor);
        var d = ServisIstemci.Gonder("durum"); Assert.True(d!.Tamam); Assert.Equal("True", d.Veri!["nobet"]); Assert.Equal("True", d.Veri["fidye"]); Assert.Equal("False", d.Veri["canary"]);
    }

    [Fact]
    public async Task Yetkili_Uygulama_Muhur_Yazabilir_Yetkisiz_Istemci_Yazamaz()
    {
        await Baslat(); Assert.True(ServisIstemci.MuhurYaz("kimlik", Hash64)); Assert.Equal(Hash64, IkinciKanal.Oku("kimlik"));
        // Geçersiz özet/ad reddedilir.
        Assert.False(ServisIstemci.MuhurYaz("kimlik", "xyz")); Assert.False(ServisIstemci.MuhurYaz("../kotu", Hash64));
        // Yetkisiz: izinli uygulama yolu başka bir exe ise bu süreç değiştiremez.
        await _s!.StopAsync(CancellationToken.None); _c!.Dispose(); await Task.Delay(300); ServisIstemci.Sifirla(); ServisIstemci.PipeAdi = _pipe + "x";
        var a = new GuardAyarlari { Gelistirme = true, PipeAdi = _pipe + "x", Klasor = Path.Combine(_t, "guard2"), IzinliUygulama = @"C:\Windows\System32\notepad.exe", ProfilKoku = Path.Combine(_t, "profiller") };
        _c = new GuardCekirdek(a); _s = new PipeSunucu(_c, a); await _s.StartAsync(CancellationToken.None); OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); BulutKlasor.Kok = _t; await Task.Delay(300);
        var r = ServisIstemci.Gonder("muhur-yaz", new() { ["ad"] = "kimlik", ["ozet"] = new string('B', 64) }); Assert.False(r!.Tamam); Assert.Contains("yetkisiz", r.Hata);
        Assert.Equal(Hash64, IkinciKanal.Oku("kimlik"));                                   // kayıt DEĞİŞMEDİ
        Assert.True(ServisIstemci.Gonder("ping")!.Tamam);                                   // okuma komutları herkese açık
    }

    [Fact]
    public async Task K1_Yerel_Muhuru_Silinen_Ama_Hizmetle_Eslesen_Kimlik_Dosyasi_Onarilarak_Kabul_Edilir()
    {
        await Baslat(); Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili); Assert.NotNull(IkinciKanal.Oku("kimlik"));
        File.Delete(Butunluk.MuhurYolu(KimlikDeposu.Yol));                                  // yerel mühür silindi, dosya aynı
        Assert.Equal(KimlikDurumu.Hazir, KimlikDeposu.Durum()); Assert.True(File.Exists(Butunluk.MuhurYolu(KimlikDeposu.Yol)));   // hizmet teyit etti → yerel mühür onarıldı
    }

    [Fact]
    public async Task K1_Dosya_Degistirilip_Sahte_Yerel_Muhur_Uretilse_Bile_Hizmet_Reddeder()
    {
        await Baslat(); Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        // Saldırgan dosyadaki kullanıcı adını değiştirir ve yerel mührü KENDİSİ yeniden üretir (aynı kullanıcı bunu yapabilir).
        File.WriteAllText(KimlikDeposu.Yol, File.ReadAllText(KimlikDeposu.Yol).Replace("ibrahim", "saldirgan")); Butunluk.Muhurla(KimlikDeposu.Yol);
        Assert.True(Butunluk.GuvenliMiKati(KimlikDeposu.Yol));                              // yerel mühür geçerli görünüyor...
        Assert.Equal(KimlikDurumu.Bozuk, KimlikDeposu.Durum());                             // ...ama hizmetteki özet uyuşmuyor
    }

    [Fact]
    public async Task K2_Dosyasi_Silinen_Kimlik_Hizmet_Kaydi_Varken_Ilk_Kuruluma_Donmez()
    {
        await Baslat(); Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        File.Delete(KimlikDeposu.Yol); File.Delete(Butunluk.MuhurYolu(KimlikDeposu.Yol));   // saldırgan dosyayı VE yerel mührü sildi
        Assert.Equal(KimlikDurumu.Bozuk, KimlikDeposu.Durum());                             // hizmet kaydı hâlâ var
    }

    [Fact]
    public async Task Hizmet_Kapaliyken_Kritik_Hesap_Degisikligi_Reddedilir_Giris_Calismaya_Devam_Eder()
    {
        await Baslat(); Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        await _s!.StopAsync(CancellationToken.None); ServisIstemci.Sifirla(); await Task.Delay(400);                       // hizmet durdu (HKCU kaydı kalır)
        Assert.True(KimlikDeposu.GirisYap("ibrahim", "Yonetici1234").Basarili);              // sayaç/kilit yazımı hizmet gerektirmez
        var r = KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Parola12345", Rol.Analist); Assert.False(r.Basarili); Assert.Contains("Koruma hizmeti", r.Mesaj);
    }

    [Fact]
    public async Task K4_Paket_Imzasi_Hizmetten_Alinir_Ve_Dogrulanir_Degistirilen_Meta_Reddedilir()
    {
        await Baslat(); string ozet = new('C', 64); var i = ServisIstemci.Gonder("paket-imzala", new() { ["ozet"] = ozet, ["meta"] = "op=ayse" }); Assert.True(i!.Tamam); string imza = i.Veri!["imza"]; Assert.Equal(64, imza.Length);
        Assert.True(ServisIstemci.Gonder("paket-dogrula", new() { ["ozet"] = ozet, ["meta"] = "op=ayse", ["imza"] = imza })!.Tamam);
        Assert.False(ServisIstemci.Gonder("paket-dogrula", new() { ["ozet"] = ozet, ["meta"] = "op=baska", ["imza"] = imza })!.Tamam);
        Assert.False(ServisIstemci.Gonder("paket-dogrula", new() { ["ozet"] = new string('D', 64), ["meta"] = "op=ayse", ["imza"] = imza })!.Tamam);
    }

    [Fact]
    public async Task O10_Ssh_Parmak_Izi_Sensor_Adina_Gore_Kaydedilir_Ve_Okunur_Gecersiz_Bicim_Reddedilir()
    {
        await Baslat(); Assert.True(ServisIstemci.Gonder("hostkey-yaz", new() { ["ad"] = "Kali Ana", ["host"] = "192.168.56.101", ["parmakIzi"] = "SHA256:abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG" })!.Tamam);
        Assert.Equal("192.168.56.101|SHA256:abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG", ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = "Kali Ana" })!.Veri!["kayit"]);
        Assert.False(ServisIstemci.Gonder("hostkey-yaz", new() { ["ad"] = "Kali Ana", ["host"] = "kotu host", ["parmakIzi"] = "x" })!.Tamam); Assert.False(ServisIstemci.Gonder("hostkey-yaz", new() { ["ad"] = "../x", ["host"] = "10.0.0.1", ["parmakIzi"] = "SHA256:abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG" })!.Tamam);
        Assert.False(ServisIstemci.Gonder("hostkey-oku", new() { ["ad"] = "Yok" })!.Tamam);
    }

    // ---------- Şartname: IPC komutları (GET_STATUS, GET_SEAL_STATE, VERIFY_KIMLIK, RUN_CHECK_NOW, SET_WATCHDOG, ARCHIVE_LOG, olay-yaz) ----------
    [Fact]
    public async Task Ipc_Komutlari_Buyuk_Kucuk_Ve_Alt_Cizgi_Farkindan_Bagimsiz_Ve_Surumlu()
    {
        await Baslat(); var d = ServisIstemci.Gonder("GET_STATUS"); Assert.True(d!.Tamam); Assert.Equal("1", d.V); Assert.Equal("True", d.Veri!["hizmetAyakta"]); Assert.Equal("True", d.Veri["korumaAktif"]); Assert.True(d.Veri.ContainsKey("sonAlarm")); Assert.True(d.Veri.ContainsKey("surum")); Assert.True(d.Veri.ContainsKey("etwNot"));
        Assert.True(ServisIstemci.Gonder("get-status")!.Tamam); Assert.False(ServisIstemci.Gonder("YOK_KOMUT")!.Tamam);
    }

    [Fact]
    public async Task Ipc_Eski_Protokol_Surumu_Reddedilir()
    {
        await Baslat(); using var c = new System.IO.Pipes.NamedPipeClientStream(".", _pipe, System.IO.Pipes.PipeDirection.InOut); c.Connect(2000); using var w = new StreamWriter(c, new System.Text.UTF8Encoding(false), 4096, true) { AutoFlush = true }; using var r = new StreamReader(c, System.Text.Encoding.UTF8, false, 4096, true);
        w.WriteLine("{\"Komut\":\"ping\",\"V\":\"9\"}"); var cevap = System.Text.Json.JsonSerializer.Deserialize<ServisCevap>(r.ReadLine()!); Assert.False(cevap!.Tamam); Assert.Contains("sürüm", cevap.Hata);
    }

    [Fact]
    public async Task Ipc_Muhur_Durumu_Ve_Kimlik_Dogrulama_Dort_Durumu_Ayirir()
    {
        await Baslat(); string yol = KimlikDeposu.Yol;
        // Kimlik yok, kayıt yok → ilk kurulum.
        var r = ServisIstemci.Gonder("VERIFY_KIMLIK", new() { ["yol"] = yol }); Assert.False(r!.Tamam); Assert.Equal("ilk-kurulum", r.Veri!["durum"]);
        Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        // Sağlam.
        r = ServisIstemci.Gonder("VERIFY_KIMLIK", new() { ["yol"] = yol }); Assert.True(r!.Tamam); Assert.Equal("gecerli", r.Veri!["durum"]);
        var s = ServisIstemci.Gonder("GET_SEAL_STATE", new() { ["yol"] = yol }); Assert.True(s!.Tamam); Assert.Equal("True", s.Veri!["kimlikVar"]); Assert.Equal("True", s.Veri["muhurVar"]); Assert.Equal(s.Veri["kimlikHash"], s.Veri["muhurHash"]); Assert.Equal("1", s.Veri["kanalKuruldu"]);
        // İhlal: dosya değişti.
        File.WriteAllText(yol, File.ReadAllText(yol).Replace("ibrahim", "saldirgan")); r = ServisIstemci.Gonder("VERIFY_KIMLIK", new() { ["yol"] = yol }); Assert.False(r!.Tamam); Assert.Equal("ihlal", r.Veri!["durum"]);
        // Kurtarma: dosya silindi, kayıt var.
        File.Delete(yol); r = ServisIstemci.Gonder("VERIFY_KIMLIK", new() { ["yol"] = yol }); Assert.False(r!.Tamam); Assert.Equal("kurtarma", r.Veri!["durum"]);
        // Boş yol reddedilir (üretimde ayrıca yalnızca bir kullanıcının BulutSoft klasöründeki kimlik.json kabul edilir).
        Assert.False(ServisIstemci.Gonder("VERIFY_KIMLIK", new() { ["yol"] = "" })!.Tamam);
    }

    [Fact]
    public async Task Ipc_Kimlik_Kanal_Damgalari_Ilk_Kurulumda_Bir_Kez_Yazilir()
    {
        await Baslat(); Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        Assert.NotNull(IkinciKanal.OkuHam("KimlikMuhurZamani")); Assert.NotNull(IkinciKanal.OkuHam("KimlikIlkKurulum")); Assert.Equal("1", IkinciKanal.OkuHam("KanalKuruldu")); Assert.Equal(1, (int)IkinciKanal.OkuHam("AnahtarSurum")!);
        string ilk = (string)IkinciKanal.OkuHam("KimlikIlkKurulum")!; Assert.NotNull(IkinciKanal.Oku("kimlik")); Assert.Equal(IkinciKanal.Oku("kimlik"), (string)IkinciKanal.OkuHam("KimlikMuhurHash")!);
        Assert.True(KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Parola12345", Rol.Analist).Basarili); Assert.Equal(ilk, (string)IkinciKanal.OkuHam("KimlikIlkKurulum")!);   // ilk kurulum damgası DEĞİŞMEZ
    }

    [Fact]
    public async Task Ipc_Watchdog_Aracligiyla_Koruma_Kapatilir_Acilir_Ve_Arsiv_Alinir()
    {
        await Baslat(); Assert.True(ServisIstemci.Gonder("SET_WATCHDOG", new() { ["acik"] = "false" })!.Tamam); var d = ServisIstemci.Gonder("GET_STATUS")!; Assert.Equal("False", d.Veri!["korumaAktif"]); Assert.Equal(0, (int)IkinciKanal.OkuHam("WatchdogAcik")!);
        Assert.True(ServisIstemci.Gonder("SET_WATCHDOG", new() { ["acik"] = "true" })!.Tamam); Assert.Equal("True", ServisIstemci.Gonder("GET_STATUS")!.Veri!["korumaAktif"]); Assert.False(ServisIstemci.Gonder("SET_WATCHDOG", new() { ["acik"] = "belki" })!.Tamam);
        Assert.True(ServisIstemci.Gonder("RUN_CHECK_NOW")!.Tamam);
    }

    [Fact]
    public async Task Olay_Yaz_Komutu_Hizmet_Gunlugune_Yazar_Zincirli_Ve_Dogrulanir()
    {
        await Baslat(); OlayDeposu.Yol = Path.Combine(_t, "guard", "olaylar.jsonl");   // (süreç içi test: hizmet ve arayüz aynı statik günlüğü paylaşır; gerçekte ayrı süreçlerdir)
        Assert.True(ServisIstemci.Gonder("olay-yaz", new() { ["tur"] = "Test", ["seviye"] = "Kritik", ["mesaj"] = "arayuzden gelen olay" })!.Tamam);
        Assert.Contains(OlayDeposu.Oku(), o => o.Mesaj == "arayuzden gelen olay" && o.Seviye == "Kritik"); var z = ServisIstemci.Gonder("olay-dogrula")!; Assert.True(z.Tamam, z.Hata); Assert.Equal("1", z.Veri!["zincirli"]);
        OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl");
    }

    // Tek yazar kancası (arayüz tarafı): kanca true derse yerel dosyaya YAZILMAZ; false/yoksa yerel yazılır (kayıp olmaz); okuma hizmet günlüğünü de birleştirir.
    [Fact]
    public void Tek_Yazar_Kancasi_Hizmete_Gidince_Yerel_Yazmaz_Gitmezse_Yerel_Yazar_Okuma_Birlestirir()
    {
        string hizmetGunlugu = Path.Combine(_t, "hizmet_olaylar.jsonl"); File.WriteAllText(hizmetGunlugu, System.Text.Json.JsonSerializer.Serialize(new OlayKaydi(DateTime.Now, "Sistem", "Bilgi", "hizmetin kendi olayi")) + "\n");
        var gelenler = new List<string>(); bool hizmetVar = true; OlayDeposu.HizmetYazici = (t, s, m) => { if (!hizmetVar) return false; gelenler.Add(m); return true; }; OlayDeposu.HizmetOkumaYolu = () => hizmetGunlugu;
        try
        {
            OlayDeposu.Ekle("Test", "Bilgi", "hizmete giden"); Assert.Contains("hizmete giden", gelenler); Assert.False(File.Exists(OlayDeposu.Yol) && File.ReadAllText(OlayDeposu.Yol).Contains("hizmete giden"));
            hizmetVar = false; OlayDeposu.Ekle("Test", "Bilgi", "yerel yazilan"); Assert.Contains("yerel yazilan", File.ReadAllText(OlayDeposu.Yol));
            var oku = OlayDeposu.Oku(); Assert.Contains(oku, o => o.Mesaj == "hizmetin kendi olayi"); Assert.Contains(oku, o => o.Mesaj == "yerel yazilan");
        }
        finally { OlayDeposu.HizmetYazici = null; OlayDeposu.HizmetOkumaYolu = null; }
    }

    [Fact]
    public async Task Olay_Yaz_Komutu_Gecersiz_Seviye_Ve_Kontrol_Karakterini_Reddeder()
    {
        await Baslat(); Assert.False(ServisIstemci.Gonder("olay-yaz", new() { ["tur"] = "T", ["seviye"] = "Felaket", ["mesaj"] = "x" })!.Tamam); Assert.False(ServisIstemci.Gonder("olay-yaz", new() { ["tur"] = "T" + (char)7, ["seviye"] = "Bilgi", ["mesaj"] = "x" })!.Tamam); Assert.False(ServisIstemci.Gonder("olay-yaz", new() { ["tur"] = "T", ["seviye"] = "Bilgi", ["mesaj"] = new string('a', 3000) })!.Tamam);
        Assert.True(ServisIstemci.Gonder("olay-yaz", new() { ["tur"] = "T", ["seviye"] = "Bilgi", ["mesaj"] = "tamam" })!.Tamam);
    }

    [Fact]
    public async Task Ipc_Arsiv_Komutu_Gunlugu_Arsive_Tasir()
    {
        await Baslat(); OlayDeposu.Yol = Path.Combine(_t, "guard", "olaylar.jsonl"); OlayDeposu.Ekle("Test", "Bilgi", "arsivlenecek"); var r = ServisIstemci.Gonder("ARCHIVE_LOG"); Assert.True(r!.Tamam, r.Hata); Assert.True(Directory.Exists(Path.Combine(_t, "guard", "olay_arsiv")));
        OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl");
    }

    [Fact]
    public void Hizmet_Kapaliysa_Istemci_Sessizce_Null_Doner()
    {
        ServisIstemci.Sifirla(); Assert.Null(ServisIstemci.Gonder("ping", null, 300)); Assert.False(ServisIstemci.Calisiyor);
    }

    // ---------- K4: paket HMAC (.hmac) ----------
    [Fact]
    public async Task K4_Paket_Hmac_Yazilir_Dogrulanir_Degisen_Paket_Ya_Da_Kunye_Reddedilir()
    {
        await Baslat(); string zip = Path.Combine(_t, "paket.zip"); File.WriteAllBytes(zip, new byte[] { 1, 2, 3, 4, 5 });
        string? yol = KanitImzasi.HmacYaz(zip, "ayse (Yönetici)"); Assert.NotNull(yol); Assert.True(File.Exists(zip + ".hmac"));
        Assert.True(KanitImzasi.HmacDogrula(zip).Gecerli, KanitImzasi.HmacDogrula(zip).Aciklama);
        // Paket değişirse.
        File.WriteAllBytes(zip, new byte[] { 9, 9, 9 }); var d = KanitImzasi.HmacDogrula(zip); Assert.False(d.Gecerli); Assert.Contains("DEĞİŞTİRİLMİŞ", d.Aciklama);
        // Künye (operatör adı) değiştirilirse imza tutmaz.
        File.WriteAllBytes(zip, new byte[] { 1, 2, 3, 4, 5 }); File.WriteAllText(zip + ".hmac", File.ReadAllText(zip + ".hmac").Replace("ayse", "mehmet")); Assert.False(KanitImzasi.HmacDogrula(zip).Gecerli);
    }

    [Fact]
    public void K4_Hizmet_Yoksa_Hmac_Uretilmez_Ve_Dogrulama_Net_Mesaj_Verir()
    {
        string zip = Path.Combine(_t, "p2.zip"); File.WriteAllBytes(zip, new byte[] { 7 }); ServisIstemci.Sifirla(); Assert.Null(KanitImzasi.HmacYaz(zip, "x")); Assert.False(File.Exists(zip + ".hmac"));
        Assert.False(KanitImzasi.HmacDogrula(zip).Gecerli);
    }

    // ---------- D6: tek mühür mekaniği (Butunluk .mac + HKLM ikinci kanal) ----------
    [Fact]
    public async Task D6_Ayar_Dosyalari_Tek_Mekanikle_Muhurlenir_Eski_Hmac_Dosyalari_Uretilmez()
    {
        await Baslat(); File.WriteAllText(BulutKlasor.Dosya("kabul_edilen.json"), "{}"); Assert.Equal(1, Muhur.Muhurle());
        Assert.False(File.Exists(BulutKlasor.Dosya("muhur.json"))); Assert.False(File.Exists(BulutKlasor.Dosya("muhur.key"))); Assert.True(File.Exists(Butunluk.MuhurYolu(BulutKlasor.Dosya("kabul_edilen.json"))));
        Assert.NotNull(IkinciKanal.Oku("dosya-kabul_edilen.json")); Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "kabul_edilen.json" && x.Durum == "Sağlam");
        // Dosya değiştirilip YEREL mühür de yeniden üretilirse (aynı kullanıcı yapabilir) hizmet kaydı yakalar.
        File.WriteAllText(BulutKlasor.Dosya("kabul_edilen.json"), "{\"a\":1}"); Butunluk.Muhurla(BulutKlasor.Dosya("kabul_edilen.json"));
        Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "kabul_edilen.json" && x.Durum == "DEĞİŞMİŞ");
        // Dosya silinirse.
        File.Delete(BulutKlasor.Dosya("kabul_edilen.json")); Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "kabul_edilen.json" && x.Durum == "SİLİNMİŞ");
    }

    [Fact]
    public void D6_Hizmet_Yokken_Yerel_Muhur_Calisir()
    {
        File.WriteAllText(BulutKlasor.Dosya("zamanlama.json"), "{}"); Muhur.Muhurle(); Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "zamanlama.json" && x.Durum == "Sağlam");
        File.WriteAllText(BulutKlasor.Dosya("zamanlama.json"), "{\"x\":1}"); Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "zamanlama.json" && x.Durum == "DEĞİŞMİŞ");
        File.WriteAllText(BulutKlasor.Dosya("webhook.json"), "{}"); Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "webhook.json" && x.Durum == "Mühürsüz (yeni)");
    }

    // ---------- ETW yanlış pozitif: Microsoft imzalı sistem süreci (CompatTelRunner vb.) alarm üretmez; taklitçi üretir ----------
    [Fact]
    public void Etw_Alarmi_Microsoft_Imzali_Sistem_Surecini_Elerken_Taklitciyi_Elemez()
    {
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows), pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.True(SurecAvcisi.GuvenilirSistemSureciMi(Path.Combine(win, "System32", "compattelrunner.exe"), true));         // gerçek CompatTelRunner: imzalı + Windows klasöründe
        Assert.True(SurecAvcisi.GuvenilirSistemSureciMi(Path.Combine(pf, "Windows Defender", "MpCmdRun.exe"), true));
        Assert.False(SurecAvcisi.GuvenilirSistemSureciMi(@"C:\Users\kurban\AppData\Local\Temp\compattelrunner.exe", true));    // taklitçi: Temp'ten, imza iddia etse bile
        Assert.False(SurecAvcisi.GuvenilirSistemSureciMi(Path.Combine(win, "System32", "compattelrunner.exe"), false));        // imzasız/başkası imzalı: elenmez
        Assert.False(SurecAvcisi.GuvenilirSistemSureciMi(null, true)); Assert.False(SurecAvcisi.GuvenilirSistemSureciMi(@"D:\tools\x.exe", true));
    }

    // ---------- O1: süreç bazlı yazma sayacı (ETW'den bağımsız mantık) ----------
    [Fact]
    public void O1_Tek_Surec_Pencerede_Esigi_Asarsa_Alarm_Verir_Dagitik_Yazma_Vermez()
    {
        var s = new SurecYazmaSayaci { Esik = 5, PencereSn = 10, BeklemeSn = 60 }; var t0 = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc); var alarm = new List<(int, int)>(); s.Asildi += (p, n) => alarm.Add((p, n));
        // 4 farklı süreç her biri 4 dosya: hiçbiri eşiği aşmaz (toplam 16 ama süreç başına 4).
        for (int p = 1; p <= 4; p++) for (int f = 0; f < 4; f++) s.Yazma(1000 + p, p * 100 + f, t0.AddMilliseconds(f * 100)); Assert.Empty(alarm);
        // Tek süreç 5 farklı dosya: alarm.
        for (int f = 0; f < 5; f++) s.Yazma(2000, f, t0.AddMilliseconds(f * 100)); Assert.Equal(new[] { (2000, 5) }, alarm);
        // Aynı dosyaya tekrar tekrar yazmak farklı dosya sayısını artırmaz.
        var s2 = new SurecYazmaSayaci { Esik = 5 }; int n2 = 0; s2.Asildi += (_, _) => n2++; for (int i = 0; i < 100; i++) s2.Yazma(3000, 42, t0.AddMilliseconds(i)); Assert.Equal(0, n2);
    }

    [Fact]
    public void O1_Alarm_Bekleme_Suresi_Ve_Pencere_Disi_Yazmalar_Sayilmaz_En_Cok_Yazan_Listelenir()
    {
        var s = new SurecYazmaSayaci { Esik = 3, PencereSn = 10, BeklemeSn = 60 }; var t0 = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc); int n = 0; s.Asildi += (_, _) => n++;
        for (int f = 0; f < 3; f++) s.Yazma(7, f, t0); Assert.Equal(1, n); for (int f = 10; f < 20; f++) s.Yazma(7, f, t0.AddSeconds(5)); Assert.Equal(1, n);   // bekleme süresinde tekrar alarm yok
        var yavas = new SurecYazmaSayaci { Esik = 3, PencereSn = 10 }; int m = 0; yavas.Asildi += (_, _) => m++; for (int f = 0; f < 6; f++) yavas.Yazma(8, f, t0.AddSeconds(f * 6)); Assert.Equal(0, m);    // 6 sn arayla: pencerede hep en çok 2
        Assert.Equal(7, s.EnCok(1, t0.AddSeconds(6))[0].Pid);
    }
}
