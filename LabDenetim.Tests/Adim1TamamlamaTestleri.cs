// Adım 1 tamamlama testleri: denetim izinin çökme toleransı, sensör/politika dosyalarının yedekten kurtarılması, günlük döndürme.
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class Adim1TamamlamaTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_a1_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    private readonly string _eskiOlay = OlayDeposu.Yol;
    public Adim1TamamlamaTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); }
    public void Dispose() { BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "Adim1TamamlamaTestleri.temizlik"); } }

    private string IzYolu => Path.Combine(_t, "iz.jsonl");
    private void UcKayit() { var iz = new AuditTrail(IzYolu); iz.Ekle("a", "approved", null, null, false, "bir"); iz.Ekle("a", "applied", null, null, false, "iki"); iz.Ekle("a", "closed", null, null, false, "uc"); }

    [Fact]
    public void Denetim_Izi_Yarim_Kalan_Son_Satir_Atilir_Ve_Dosya_Onarilir()
    {
        UcKayit(); File.AppendAllText(IzYolu, "{\"Seq\":4,\"Tim");   // çökmeyle yarım kalmış satır
        var iz = new AuditTrail(IzYolu);
        Assert.Equal(3, iz.Kayitlar.Count); Assert.True(iz.Dogrula().Saglam);
        iz.Ekle("a", "verified", null, null, false, "dort");   // yeni kayıt bozuk satıra yapışmamalı
        var yeni = new AuditTrail(IzYolu); Assert.Equal(4, yeni.Kayitlar.Count); Assert.True(yeni.Dogrula().Saglam);
    }

    [Fact]
    public void Denetim_Izi_Ortadaki_Bozuk_Satir_Kurcalama_Sayilir_Ve_Hata_Verir()
    {
        UcKayit(); var s = File.ReadAllLines(IzYolu).ToList(); s[1] = "{bozuk"; File.WriteAllLines(IzYolu, s);
        Assert.ThrowsAny<Exception>(() => new AuditTrail(IzYolu));
    }

    [Fact]
    public void Sensor_Listesi_Bozuk_Dosyada_Yedekten_Gelir()
    {
        var a = new SensorProfili("S1", "192.168.56.20", 22, "u", "k"); SensorDeposu.Kaydet(new List<SensorProfili> { a }); SensorDeposu.Kaydet(new List<SensorProfili> { a, new SensorProfili("S2", "192.168.56.21", 22, "u", "k") });
        File.WriteAllText(SensorDeposu.Yol, "[{yarim");
        var l = SensorDeposu.Yukle(); Assert.Single(l); Assert.Equal("S1", l[0].Ad);
    }

    [Fact]
    public void Parola_Politikasi_Bozuk_Dosyada_Yedekten_Gelir()
    {
        ParolaPolitikasi.Kaydet(new ParolaPolitika(20, 3)); ParolaPolitikasi.Kaydet(new ParolaPolitika(16, 3));
        File.WriteAllText(ParolaPolitikasi.Yol, "{"); Assert.Equal(20, ParolaPolitikasi.Yukle().MinUzunluk);
    }

    [Fact]
    public void Olay_Gunlugu_Doner_Ve_Son_Kayitlari_Tutar_Gecici_Dosya_Birakmaz()
    {
        for (int i = 0; i < 30; i++) OlayDeposu.Ekle("Test", "Bilgi", "olay " + i + " " + new string('x', 200));
        Assert.False(File.Exists(OlayDeposu.Yol + ".tmp")); Assert.Equal(30, OlayDeposu.Oku().Count);
    }
}
