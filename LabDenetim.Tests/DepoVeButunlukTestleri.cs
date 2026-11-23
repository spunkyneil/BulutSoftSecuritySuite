// Atomik veri deposu ve çökmeye dayanıklı mühürlü yazma testleri. Geçici klasörde çalışır, gerçek veriye dokunmaz.
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace LabDenetim.Tests;

public class DepoVeButunlukTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_depo_" + Guid.NewGuid().ToString("N"));
    public DepoVeButunlukTestleri() => Directory.CreateDirectory(_t);
    public void Dispose() { try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "DepoVeButunlukTestleri.temizlik"); } }
    private string Y(string ad) => Path.Combine(_t, ad);

    [Fact]
    public void Yaz_Gecici_Dosya_Birakmaz_Ve_Ikinci_Yazimda_Saglam_Yedek_Tutar()
    {
        Depo.Yaz(Y("a.json"), "{\"s\":1}"); Assert.False(File.Exists(Y("a.json.tmp"))); Assert.False(File.Exists(Y("a.json.bak")));
        Depo.Yaz(Y("a.json"), "{\"s\":2}"); Assert.Equal("{\"s\":1}", File.ReadAllText(Y("a.json.bak"))); Assert.Equal("{\"s\":2}", File.ReadAllText(Y("a.json")));
    }

    [Fact]
    public void Bozuk_Ana_Dosya_Yedekten_Okunur_Ve_Bozuk_Dosya_Yedegi_Ezmez()
    {
        Depo.YazJson(Y("v.json"), new List<int> { 1 }); Depo.YazJson(Y("v.json"), new List<int> { 1, 2 });
        File.WriteAllText(Y("v.json"), "{yarim");   // çökmeyle yarım kalmış dosya
        Assert.Equal(new List<int> { 1 }, Depo.Yukle<List<int>>(Y("v.json")));
        Depo.YazJson(Y("v.json"), new List<int> { 9 });   // bozuk dosya yeni yazımda yedeğin yerine geçmez
        Assert.Equal(new List<int> { 1 }, JsonSerializerOku(Y("v.json.bak")));
    }
    private static List<int>? JsonSerializerOku(string y) => System.Text.Json.JsonSerializer.Deserialize<List<int>>(File.ReadAllText(y));

    [Fact]
    public void Ana_Ve_Yedek_Bozuksa_Varsayilan_Doner()
    {
        File.WriteAllText(Y("x.json"), "{"); File.WriteAllText(Y("x.json.bak"), "[");
        Assert.Null(Depo.Yukle<List<int>>(Y("x.json"))); Assert.Null(Depo.Yukle<List<int>>(Y("yok.json")));
    }

    [Fact]
    public void Muhurlu_Yazim_Gecerli_Birakir_Ve_Gecici_Dosya_Kalmaz()
    {
        Butunluk.YazGuvenli(Y("k.json"), "{\"a\":1}"); Butunluk.YazGuvenli(Y("k.json"), "{\"a\":2}");
        Assert.Equal(ButunlukDurumu.Gecerli, Butunluk.Kontrol(Y("k.json"))); Assert.False(File.Exists(Y("k.json.tmp"))); Assert.False(File.Exists(Y("k.json.mac.tmp")));
    }

    // Çökme 1: iki özetli mühür yazıldı ama dosya henüz değişmedi → eski dosya geçerli.
    [Fact]
    public void Cokme_Dosya_Degismeden_Once_Eski_Dosya_Gecerli()
    {
        Butunluk.YazGuvenli(Y("k.json"), "{\"a\":1}"); var eski = SHA256.HashData(File.ReadAllBytes(Y("k.json"))); var yeni = SHA256.HashData(Encoding.UTF8.GetBytes("{\"a\":2}"));
        Butunluk.MuhurYaz(Y("k.json"), yeni, eski);
        Assert.Equal(ButunlukDurumu.Gecerli, Butunluk.Kontrol(Y("k.json")));
    }

    // Çökme 2: dosya yeni sürüme geçti ama mühür henüz sadeleşmedi → yeni dosya geçerli (eski kodda burada "bozuk" görünürdü).
    [Fact]
    public void Cokme_Dosya_Degistikten_Sonra_Yeni_Dosya_Gecerli()
    {
        Butunluk.YazGuvenli(Y("k.json"), "{\"a\":1}"); var eski = SHA256.HashData(File.ReadAllBytes(Y("k.json"))); var yeniMetin = "{\"a\":2}"; var yeni = SHA256.HashData(Encoding.UTF8.GetBytes(yeniMetin));
        Butunluk.MuhurYaz(Y("k.json"), yeni, eski); File.WriteAllText(Y("k.json"), yeniMetin);
        Assert.Equal(ButunlukDurumu.Gecerli, Butunluk.Kontrol(Y("k.json")));
    }

    [Fact]
    public void Elle_Degistirilmis_Dosya_Hala_Bozuk_Sayilir()
    {
        Butunluk.YazGuvenli(Y("k.json"), "{\"a\":1}"); File.WriteAllText(Y("k.json"), "{\"a\":999}");
        Assert.Equal(ButunlukDurumu.Bozuk, Butunluk.Kontrol(Y("k.json")));
    }

    [Fact]
    public void Oynanmis_Eski_Dosyanin_Ozeti_Yeni_Muhre_Girmez()
    {
        Butunluk.YazGuvenli(Y("k.json"), "{\"a\":1}"); File.WriteAllText(Y("k.json"), "{\"yetki\":\"admin\"}");   // oynanmış
        Butunluk.YazGuvenli(Y("k.json"), "{\"a\":2}");   // meşru yeni yazım
        File.WriteAllText(Y("k.json"), "{\"yetki\":\"admin\"}");   // oynanmış sürüm geri konursa
        Assert.Equal(ButunlukDurumu.Bozuk, Butunluk.Kontrol(Y("k.json")));
    }

    [Fact]
    public void Eski_Tek_Ozetli_Muhur_Hala_Kabul_Edilir()
    {
        File.WriteAllText(Y("e.json"), "{}"); Butunluk.Muhurla(Y("e.json")); Assert.Equal(ButunlukDurumu.Gecerli, Butunluk.Kontrol(Y("e.json")));
    }
}
