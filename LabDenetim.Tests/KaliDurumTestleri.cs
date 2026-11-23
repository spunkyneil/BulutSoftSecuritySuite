// Bu dosya Kali durum sınıflandırması, konsol halka belleği ve nmap komut önizlemesini sahte verilerle sınar (gerçek sensör/ağ kullanılmaz).
using LabDenetim;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Veri klasörünü paylaşan testler sırayla çalışsın.
[Collection("Veri")]
public class KaliDurumTestleri
{
    // Yardımcı: sahte sınama sonucu.
    private static SensorSonuc S(bool ok, string ozet) => new(ok, "Linux", "kali", ok, ozet);

    [Fact]
    public void Basarili_Aktif_Ve_Yesil()
    {
        // Başarılı sonuç Aktif olmalı.
        var r = KaliDurumYoneticisi.Sinifla(S(true, "ok"), "kali1", DateTime.Now);
        Assert.Equal(KaliDurumu.Aktif, r.Durum);
        Assert.Equal(("Aktif", 0), KaliDurumYoneticisi.Gosterge(r));
    }

    [Theory]
    [InlineData("Bağlanılamadı: zaman aşımı.", "Kapali")]
    [InlineData("Anahtar reddedildi (Permission denied)", "Sorun")]
    [InlineData("Python3: YOK", "Sorun")]
    [InlineData("Ajan çalışmadı: hata", "Sorun")]
    [InlineData("REMOTE HOST IDENTIFICATION HAS CHANGED", "Sorun")]
    [InlineData("tuhaf bir şey", "Sorun")]
    public void Hata_Metinleri_Dogru_Siniflanir(string ozet, string beklenen)
    {
        // Her hata metni doğru duruma düşmeli.
        Assert.Equal(beklenen, KaliDurumYoneticisi.Sinifla(S(false, ozet), "k", DateTime.Now).Durum.ToString());
    }

    [Fact]
    public void Kapali_Gostergesi_Kirmizi_Ve_Uyari_Metni_Var()
    {
        // Kapalı durum kırmızı olmalı ve yönlendirici kısa metin taşımalı.
        var r = KaliDurumYoneticisi.Sinifla(S(false, "Bağlanılamadı: ulaşılamıyor"), "k", DateTime.Now);
        Assert.Equal(2, KaliDurumYoneticisi.Gosterge(r).Renk);
        Assert.Contains("Bağlantı Bekleniyor", r.Kisa);
    }

    [Fact]
    public void Sensor_Secimi_Kali_Adini_Tercih_Eder()
    {
        // Adında kali geçen sensör öncelikli, liste boşsa null.
        var a = new SensorProfili("Diger", "1.1.1.1", 22, "u", "k"); var b = new SensorProfili("Kali-Lab", "2.2.2.2", 22, "u", "k");
        Assert.Same(b, KaliDurumYoneticisi.SensorSec(new() { a, b }));
        Assert.Null(KaliDurumYoneticisi.SensorSec(new()));
    }

    [Fact]
    public void Konsol_Satiri_Temizler_Kisaltir_Ve_Halka_Sinirini_Korur()
    {
        // Denetim karakterleri boşluğa dönmeli, uzun satır kısalmalı, 600 satırı geçmemeli.
        KaliKonsol.Temizle(); KaliKonsol.Yaz("t", "a\u001b[31mb\nc"); Assert.DoesNotContain(KaliKonsol.Hepsi(), x => x.Any(char.IsControl));
        KaliKonsol.Yaz("t", new string('x', 1000)); Assert.Contains(KaliKonsol.Hepsi(), x => x.Contains("(+600 karakter)"));
        for (int i = 0; i < 700; i++) KaliKonsol.Yaz("t", "satir" + i);
        Assert.True(KaliKonsol.Hepsi().Count <= 600);
        KaliKonsol.Temizle(); Assert.Empty(KaliKonsol.Hepsi());
    }

    [Fact]
    public void Konsol_Parametre_Yukunu_Gostermez()
    {
        // Şifreli ajan parametresi etikette yer almamalı.
        var e = KaliKonsol.KomutEtiketi("python3 - run QWxhZGRpbjpvcGVuIHNlc2FtZQ==");
        Assert.DoesNotContain("QWxh", e); Assert.Contains("şifreli parametre", e);
    }

    [Fact]
    public void Nmap_Onizleme_Sabit_Argumanlar_Ve_Zamanlama()
    {
        // Önizleme zamanlamayı yansıtmalı ve serbest argüman içermemeli.
        var o = SensorNmap.KomutOnizle(NmapProfili.Hizli, "192.168.56.10", 2);
        Assert.Contains("-T2", o); Assert.Contains("192.168.56.10", o); Assert.StartsWith("nmap", o);
        Assert.Contains("-T4", SensorNmap.KomutOnizle(NmapProfili.Standart, "192.168.56.10", 4));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("example.com")]
    [InlineData("10.0.0.1; rm -rf /")]
    [InlineData("")]
    public void Nmap_Dis_Ag_Ve_Zararli_Hedefi_Reddeder(string hedef)
    {
        // Dış ağ, alan adı veya komut enjeksiyonu denemesi hata vermeli.
        Assert.NotNull(SensorNmap.HedefHatasi(hedef));
    }

    [Theory]
    [InlineData("192.168.56.10")]
    [InlineData("10.0.0.0/24")]
    public void Nmap_Ozel_Ag_Hedefini_Kabul_Eder(string hedef) => Assert.Null(SensorNmap.HedefHatasi(hedef));
}
