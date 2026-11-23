// Geri alma yedeğinin güvenli yazımı ve tek kopya kilidi testleri. Gerçek veri klasörüne dokunmaz (geçici klasöre yönlendirilir).
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class YedekVeTekKopyaTestleri : IDisposable
{
    // Geçici veri klasörü.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_yedek_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    public YedekVeTekKopyaTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; }
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "YedekVeTekKopyaTestleri.temizlik"); } }
    static KaliciOge O(string ad) => new() { Tur = "Run", Ad = ad, Konum = "HKCU\\x", Komut = "c" };

    [Fact]
    public void Yazim_Sonrasi_Gecici_Dosya_Kalmaz_Ve_Sağlam_Kopya_Tutulur()
    {
        KaliciDenetci.YedekEkle(O("A")); Assert.False(File.Exists(KaliciDenetci.YedekYolu + ".tmp")); Assert.False(File.Exists(KaliciDenetci.YedekYolu + ".bak"));   // ilk yazımda önceki dosya yok
        KaliciDenetci.YedekEkle(O("B")); Assert.True(File.Exists(KaliciDenetci.YedekYolu + ".bak")); Assert.Equal(2, KaliciDenetci.YedekOku().Count);
    }

    [Fact]
    public void Bozuk_Ana_Dosya_Son_Saglam_Kopyadan_Okunur()
    {
        KaliciDenetci.YedekEkle(O("A")); KaliciDenetci.YedekEkle(O("B"));
        // Yazma ortasında kesilmiş gibi bozuk ana dosya.
        File.WriteAllText(KaliciDenetci.YedekYolu, "[{\"Id\":\"x\",\"Zaman\":");
        var l = KaliciDenetci.YedekOku(); Assert.NotEmpty(l); Assert.Contains(l, x => x.Ad == "A");
    }

    [Fact]
    public void Bozuk_Dosya_Varken_Yeni_Ekleme_Eski_Kayitlari_Kaybettirmez()
    {
        KaliciDenetci.YedekEkle(O("A")); KaliciDenetci.YedekEkle(O("B")); var bakOnce = File.ReadAllText(KaliciDenetci.YedekYolu + ".bak");
        File.WriteAllText(KaliciDenetci.YedekYolu, "bozuk{{{");
        KaliciDenetci.YedekEkle(O("C"));
        // Eski kayıtlar korunur, yenisi eklenir; sağlam kopya bozuk dosyayla ezilmez.
        var ad = KaliciDenetci.YedekOku().Select(x => x.Ad).OrderBy(x => x).ToList(); Assert.Contains("A", ad); Assert.Contains("C", ad);
        Assert.Equal(bakOnce, File.ReadAllText(KaliciDenetci.YedekYolu + ".bak"));
    }

    [Fact]
    public void Artik_Gecici_Dosya_Yazimi_Engellemez_Ve_Dosya_Yoksa_Bos_Doner()
    {
        Assert.Empty(KaliciDenetci.YedekOku());
        File.WriteAllText(KaliciDenetci.YedekYolu + ".tmp", "yarım kalmış");
        KaliciDenetci.YedekEkle(O("A")); Assert.Single(KaliciDenetci.YedekOku()); Assert.False(File.Exists(KaliciDenetci.YedekYolu + ".tmp"));
    }

    [Fact]
    public void Eski_Davranis_Ayni_Ad_Ve_Konum_Tek_Kayit() { KaliciDenetci.YedekEkle(O("A")); KaliciDenetci.YedekEkle(new KaliciOge { Tur = "Run", Ad = "A", Konum = "HKCU\\x", Komut = "c2" }); var l = KaliciDenetci.YedekOku(); Assert.Single(l); Assert.Equal("c2", l[0].Aciklama); }

    // ---- Tek kopya kilidi ----
    // Kilidi ayrı bir iş parçacığında tutar (Mutex iş parçacığına bağlıdır); bırak.Set() ile bırakır.
    static (Thread, ManualResetEventSlim) Tut(string ad)
    {
        var alindi = new ManualResetEventSlim(); var birak = new ManualResetEventSlim();
        var th = new Thread(() => { if (TekKopya.Al(ad, 0, out var m)) { alindi.Set(); birak.Wait(5000); try { m!.ReleaseMutex(); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "YedekVeTekKopyaTestleri.temizlik"); } m!.Dispose(); } else alindi.Set(); }); th.Start(); Assert.True(alindi.Wait(3000)); return (th, birak);
    }
    static bool BaskaThreadteAl(string ad, int bekle) { bool r = false; Mutex? m = null; var th = new Thread(() => { r = TekKopya.Al(ad, bekle, out m); if (r) { try { m!.ReleaseMutex(); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "YedekVeTekKopyaTestleri.temizlik"); } m!.Dispose(); } }); th.Start(); th.Join(); return r; }

    [Fact]
    public void Ikinci_Kopya_Reddedilir_Ilki_Birakinca_Alinir()
    {
        string ad = TekKopya.Ad(_t); var (th, birak) = Tut(ad);
        Assert.False(BaskaThreadteAl(ad, 0)); Assert.False(BaskaThreadteAl(ad, 200));   // beklemeli deneme de başarısız (kilit bırakılmadı)
        birak.Set(); th.Join(); Assert.True(BaskaThreadteAl(ad, 0));
    }

    [Fact]
    public void Yeniden_Baslatmada_Bekleme_Eski_Kopya_Kapaninca_Kilidi_Alir()
    {
        string ad = TekKopya.Ad(_t + "_bekle"); var (th, birak) = Tut(ad);
        // 300 ms sonra eski kopya kapanıyor; 3 sn bekleyen yeni kopya kilidi almalı.
        _ = Task.Run(async () => { await Task.Delay(300); birak.Set(); });
        Assert.True(BaskaThreadteAl(ad, 3000)); th.Join();
    }

    [Fact]
    public void Kilit_Adi_Veri_Klasorune_Ozel_Ve_Kararli()
    {
        Assert.Equal(TekKopya.Ad(@"C:\A\B"), TekKopya.Ad(@"c:\a\b")); Assert.NotEqual(TekKopya.Ad(@"C:\A"), TekKopya.Ad(@"C:\B")); Assert.StartsWith("Local\\BulutSoft_", TekKopya.Ad("x"));
    }
}
