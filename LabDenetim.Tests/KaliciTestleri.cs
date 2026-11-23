// Bu dosya Kalıcılık Denetçisi ve Süreç Avcısı testlerini içerir (sistemi DEĞİŞTİRMEZ; yalnızca okuma/ayrıştırma/plan mantığı).
namespace LabDenetim.Tests;

// Kalıcılık testleri.
[Collection("Veri")]
public class KaliciTestleri : IDisposable
{
    // Geçici klasör.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_kalici_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    // Kurulum.
    public KaliciTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; }
    // Temizlik.
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "KaliciTestleri.temizlik"); } }

    // Komuttan yol çıkarma.
    [Theory,
        InlineData("\"C:\\Program Files\\App\\app.exe\" --silent", "C:\\Program Files\\App\\app.exe"),
        InlineData("C:\\Tools\\run.exe /x", "C:\\Tools\\run.exe"),
        InlineData("C:\\Program Files\\App Dir\\app.exe -k net", "C:\\Program Files\\App Dir\\app.exe"),
        InlineData("", "")]
    public void KomuttanYol(string komut, string beklenen) => Assert.Equal(beklenen, KaliciDenetci.KomuttanYol(komut));

    // Öğe üretici.
    private static KaliciOge O(string imza, string yol, string komut = "", bool ms = false, string yay = "") => new() { Tur = "Run", Ad = "x", Imza = imza, Yol = yol, Komut = komut.Length > 0 ? komut : yol, Microsoft = ms, Yayinci = yay };

    // Risk değerlendirme.
    [Fact] public void Risk_Kurallari()
    {
        Assert.Equal("Yüksek", KaliciDenetci.RiskHesapla(O("İmzasız", @"C:\Users\a\AppData\Local\Temp\x.exe")).Risk);
        Assert.Equal("Yüksek", KaliciDenetci.RiskHesapla(O("İmzalı", @"C:\a.exe", "powershell -enc AAAA")).Risk);
        Assert.Equal("Yüksek", KaliciDenetci.RiskHesapla(O("Geçersiz", @"C:\Program Files\a.exe")).Risk);
        Assert.Equal("Orta", KaliciDenetci.RiskHesapla(O("İmzasız", @"C:\Users\a\AppData\Roaming\x.exe")).Risk);
        Assert.Equal("Orta", KaliciDenetci.RiskHesapla(O("Dosya yok", @"C:\yok.exe")).Risk);
        Assert.Equal("Düşük", KaliciDenetci.RiskHesapla(O("İmzasız", @"C:\Program Files\App\a.exe")).Risk);
        Assert.Equal("Güvenli", KaliciDenetci.RiskHesapla(O("İmzalı", @"C:\Windows\System32\a.exe", ms: true, yay: "Microsoft Corporation")).Risk);
        Assert.Equal("Düşük", KaliciDenetci.RiskHesapla(O("İmzalı", @"C:\Program Files\App\a.exe", yay: "Contoso")).Risk);
    }

    // Plan: Microsoft imzalı öğeye dokunulmaz.
    [Fact] public void Plan_MicrosoftReddedilir() => Assert.NotNull(KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Hizmet", Ad = "WinDefend", Konum = "WinDefend", Microsoft = true }).Ret);

    // Plan: tehlikeli karakterli ad reddedilir (komut enjeksiyonu engeli).
    [Theory, InlineData("evil & calc"), InlineData("a\"b"), InlineData("x|y"), InlineData("%PATH%"), InlineData("a^b")]
    public void Plan_TehlikeliAd(string ad) => Assert.NotNull(KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Hizmet", Ad = ad, Konum = "x" }).Ret);

    // Plan: türlere göre komutlar.
    [Fact] public void Plan_Turler()
    {
        var hkcu = KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Run", Ad = "App", Konum = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", Hklm = false }); Assert.True(hkcu.DogrudanHkcu); Assert.Null(hkcu.Komut); Assert.Null(hkcu.Ret);
        var hklm = KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Run", Ad = "App", Konum = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", Hklm = true }); Assert.Contains("StartupApproved\\Run", hklm.Komut); Assert.Contains("030000000000000000000000", hklm.Komut); Assert.True(hklm.Yonetici);
        var enable = KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Run", Ad = "App", Konum = "HKLM\\x", Hklm = true }, false); Assert.Contains("020000000000000000000000", enable.Komut);
        Assert.Equal("schtasks /change /tn \"\\MyTask\" /disable", KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Zamanlanmış Görev", Ad = "MyTask", Konum = "\\MyTask" }).Komut);
        Assert.Equal("sc config MySvc start= disabled", KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Hizmet", Ad = "MySvc", Konum = "MySvc" }).Komut);
        Assert.Equal("sc config MySvc start= auto", KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "Hizmet", Ad = "MySvc", Konum = "MySvc" }, false).Komut);
        Assert.NotNull(KaliciDenetci.DevreDisiPlani(new KaliciOge { Tur = "RunOnce", Ad = "x", Konum = "x" }).Ret);
    }

    // İmza JSON çıktısı çözümü (PowerShell 5.1 "value" sarmalayıcısı dahil).
    [Fact] public void Imza_Coz()
    {
        var d = ImzaDenetimi.Coz("[{\"P\":\"C:\\\\a.exe\",\"S\":\"Valid\",\"C\":\"CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond\"},{\"P\":\"C:\\\\b.exe\",\"S\":\"NotSigned\",\"C\":\"\"},{\"P\":\"C:\\\\c.exe\",\"S\":\"HashMismatch\",\"C\":\"CN=Contoso\"}]");
        Assert.True(d[@"C:\a.exe"].Microsoft); Assert.Equal("İmzasız", d[@"C:\b.exe"].Durum); Assert.Equal("Geçersiz", d[@"C:\c.exe"].Durum); Assert.False(d[@"C:\c.exe"].Microsoft);
        Assert.Single(ImzaDenetimi.Coz("{\"P\":\"C:\\\\x.exe\",\"S\":\"Valid\",\"C\":\"CN=Contoso, O=Contoso Ltd\"}")); Assert.Empty(ImzaDenetimi.Coz("bozuk")); Assert.Single(ImzaDenetimi.Coz("{\"value\":[{\"P\":\"C:\\\\y.exe\",\"S\":\"Valid\",\"C\":\"CN=X\"}],\"Count\":1}"));
    }

    // Gerçek imza denetimi (Windows PowerShell varsa): notepad.exe Microsoft imzalıdır; yoksa test atlanır.
    [Fact] public void Imza_Gercek_Notepad()
    {
        if (!File.Exists(ImzaDenetimi.PowerShellYolu)) return; string np = Path.Combine(Environment.SystemDirectory, "notepad.exe"); if (!File.Exists(np)) return;
        var d = ImzaDenetimi.Kontrol(new[] { np }); Assert.True(d.TryGetValue(np, out var r) && r.Durum == "İmzalı" && r.Microsoft);
    }

    // Yedek gidiş-dönüş.
    [Fact] public void Yedek_GidisDonus() { KaliciDenetci.YedekEkle(new KaliciOge { Tur = "Run", Ad = "A", Konum = "HKCU\\x", Komut = "c" }); KaliciDenetci.YedekEkle(new KaliciOge { Tur = "Run", Ad = "A", Konum = "HKCU\\x", Komut = "c2" }); var l = KaliciDenetci.YedekOku(); Assert.Single(l); Assert.Equal("c2", l[0].Aciklama); }

    // Gerçek (salt okuma) numaralandırma çökmez.
    [Fact] public void Tara_Cokmez() => Assert.NotNull(KaliciDenetci.Tara());
}

// Süreç avcısı testleri.
public class SurecAvciTestleri
{
    // Korunan süreçler.
    [Theory, InlineData("lsass", 500, true), InlineData("csrss.exe", 600, true), InlineData("svchost", 700, true), InlineData("EXPLORER", 800, true), InlineData("evil", 4, true), InlineData("notepad", 9999, false)]
    public void Korunuyor(string ad, int pid, bool beklenen) => Assert.Equal(beklenen, SurecAvcisi.Korunuyor(ad, pid));

    // Programın kendisi korunur.
    [Fact] public void Kendisi_Korunur() => Assert.True(SurecAvcisi.Korunuyor("herhangi", Environment.ProcessId));

    // Fark hesabı: aynı süreç, yeni başlayan ve PID yeniden kullanımı.
    [Fact] public void Fark_Hesabi()
    {
        var t = new DateTime(2026, 1, 1, 10, 0, 0); var once = new Dictionary<int, (long, long, string, DateTime, string)> { [10] = (1000, 5, "a", t, "pa"), [11] = (5000, 9, "b", t, "pb"), [12] = (0, 0, "c", t, "pc") };
        var sonra = new Dictionary<int, (long, long, string, DateTime, string)> { [10] = (9000, 15, "a", t, "pa"), [11] = (5000, 9, "b", t, "pb"), [12] = (500, 1, "c", t.AddHours(1), "pc"), [13] = (100, 1, "d", t, "pd") };
        var f = SurecAvcisi.Fark(once, sonra); Assert.Single(f); Assert.Equal(10, f[0].Pid); Assert.Equal(8000, f[0].YazilanBayt); Assert.Equal(10, f[0].YazmaIslemi);
    }

    // Gerçek ölçüm çökmez.
    [Fact] public void Olc_Cokmez() => Assert.NotNull(SurecAvcisi.Olc(2));
}
