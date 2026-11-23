// Filo raporu testleri: okuma güvenliği (bozuk/büyük/sahte dosyalar), makine birleştirme ve trend, ortak sorunlar, uyarılar, HTML/CSV güvenliği.
using System.Text;
using System.Text.Json;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class FiloRaporuTestleri : IDisposable
{
    // Geçici özet klasörü.
    private readonly string _k = Path.Combine(Path.GetTempPath(), "bs_filo_" + Guid.NewGuid().ToString("N"));
    public FiloRaporuTestleri() => Directory.CreateDirectory(_k);
    public void Dispose() { try { Directory.Delete(_k, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "FiloRaporuTestleri.temizlik"); } }
    static readonly DateTime Simdi = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    static OzetBulgu OB(string modul, string baslik, string onem, string oneri = "Düzeltin. Ayrıntı.") => new(modul, baslik, onem, "ayrıntı", oneri);
    static TaramaOzeti Oz(string makine, int puan, DateTime? z = null, bool yonetici = true, int atlanan = 0, params OzetBulgu[] b) => new(1, makine, z ?? Simdi, "1.0.0", yonetici, puan, puan >= 80 ? "Genel Durum İyi" : puan >= 50 ? "Dikkat Gerekiyor" : "Acil Müdahale Gerekiyor", b.Count(x => x.Onem == "Yuksek"), b.Count(x => x.Onem == "Orta"), b.Count(x => x.Onem == "Dusuk"), b.Count(x => x.Onem == "Bilgi"), atlanan, b.ToList());
    void Yaz(string ad, TaramaOzeti o) => File.WriteAllText(Path.Combine(_k, ad), JsonSerializer.Serialize(o));

    // ---- Okuma güvenliği ----
    [Fact]
    public void Gecersiz_Dosyalar_Atlanir_Ve_Uyarilir_Gecerliler_Okunur()
    {
        Yaz("iyi.json", Oz("PC-1", 90)); File.WriteAllText(Path.Combine(_k, "bozuk.json"), "{ bozuk"); File.WriteAllText(Path.Combine(_k, "baska.json"), "{\"x\":1}"); File.WriteAllText(Path.Combine(_k, "bos.json"), "");
        File.WriteAllText(Path.Combine(_k, "dizi.json"), "[1,2,3]"); Yaz("yeni.json", Oz("PC-2", 50) with { Format = 99 }); File.WriteAllText(Path.Combine(_k, "not.txt"), "yoksay");
        var u = new List<string>(); var l = FiloRaporu.Oku(_k, Simdi, u);
        Assert.Single(l); Assert.Equal("PC-1", l[0].Makine); Assert.Contains(u, x => x.StartsWith("bozuk.json")); Assert.Contains(u, x => x.Contains("baska.json") && x.Contains("değil")); Assert.Contains(u, x => x.Contains("yeni.json") && x.Contains("daha yeni"));
        Assert.DoesNotContain(u, x => x.Contains("not.txt"));
    }

    [Fact]
    public void Cok_Buyuk_Dosya_Okunmadan_Atlanir()
    {
        using (var fs = new FileStream(Path.Combine(_k, "dev.json"), FileMode.Create)) fs.SetLength(FiloRaporu.EnBuyukDosya + 1);
        var u = new List<string>(); Assert.Empty(FiloRaporu.Oku(_k, Simdi, u)); Assert.Contains(u, x => x.Contains("çok büyük"));
    }

    [Fact]
    public void Klasor_Yoksa_Hata_Alt_Klasore_Inilmez()
    {
        Assert.Throws<DirectoryNotFoundException>(() => FiloRaporu.Oku(Path.Combine(_k, "yok"), Simdi, new()));
        Directory.CreateDirectory(Path.Combine(_k, "alt")); File.WriteAllText(Path.Combine(_k, "alt", "x.json"), JsonSerializer.Serialize(Oz("ALT", 80))); Assert.Empty(FiloRaporu.Oku(_k, Simdi, new()));
    }

    [Fact]
    public void Alanlar_Sinirlanir_Puan_Kirpilir_Gelecek_Tarih_Duzeltilir()
    {
        var kotu = Oz(new string('A', 5000), 500, Simdi.AddYears(3), true, -5, OB(new string('M', 900), new string('B', 900), "Yuksek", new string('x', 5000))) with { Yuksek = -9 };
        Yaz("k.json", kotu); var u = new List<string>(); var o = FiloRaporu.Oku(_k, Simdi, u).Single();
        Assert.True(o.Makine.Length <= 100); Assert.Equal(100, o.Puan); Assert.Equal(Simdi, o.ZamanUtc); Assert.Contains(u, x => x.Contains("gelecekte")); Assert.Equal(0, o.Yuksek); Assert.Equal(0, o.Atlanan); Assert.True(o.Bulgular[0].Baslik.Length <= 200 && o.Bulgular[0].Oneri.Length <= 601 && o.Bulgular[0].Modul.Length <= 100);
    }

    [Fact]
    public void Bulgu_Listesi_Null_Veya_Cok_Uzunsa_Cokmez()
    {
        File.WriteAllText(Path.Combine(_k, "n.json"), "{\"Format\":1,\"Makine\":\"N\",\"ZamanUtc\":\"2026-11-01T00:00:00Z\",\"Bulgular\":null}"); Assert.Empty(FiloRaporu.Oku(_k, Simdi, new()).Single().Bulgular);
        Yaz("u.json", Oz("U", 50, null, true, 0, Enumerable.Range(0, 5000).Select(i => OB("M", "b" + i, "Orta")).ToArray())); Assert.True(FiloRaporu.Oku(_k, Simdi, new()).Single(x => x.Makine == "U").Bulgular.Count <= 2000);
    }

    // ---- Hesaplama ----
    [Fact]
    public void Ayni_Makine_En_Yeni_Ve_Trend_Fark_Hesaplanir()
    {
        var l = new List<TaramaOzeti> { Oz("PC-1", 40, Simdi.AddDays(-10)), Oz("pc-1", 70, Simdi.AddDays(-1)), Oz("PC-2", 90), Oz("PC-3", 20, Simdi.AddDays(-3)) };
        var s = FiloRaporu.Hesapla(l, Simdi, 30, new());
        Assert.Equal(3, s.Makineler.Count); Assert.Equal("PC-3", s.Makineler[0].Son.Makine);   // en düşük puan önde
        var pc1 = s.Makineler.Single(m => m.Son.Makine.Equals("PC-1", StringComparison.OrdinalIgnoreCase)); Assert.Equal(70, pc1.Son.Puan); Assert.Equal(30, pc1.Fark); Assert.Null(s.Makineler.Single(m => m.Son.Makine == "PC-2").Fark);
        Assert.Equal(60.0, s.Ortalama); Assert.Equal(1, s.Iyi); Assert.Equal(1, s.Dikkat); Assert.Equal(1, s.Acil);
    }

    [Fact]
    public void Eski_Tarama_Ve_Yonetici_Degil_Ve_Atlanan_Uyarilari()
    {
        var u = new List<string>(); var s = FiloRaporu.Hesapla(new() { Oz("ESKI", 90, Simdi.AddDays(-45)), Oz("YP", 90, Simdi, false), Oz("ATL", 90, Simdi, true, 2) }, Simdi, 30, u);
        Assert.True(s.Makineler.Single(m => m.Son.Makine == "ESKI").Eski); Assert.Contains(u, x => x.Contains("yönetici olarak taranmamış")); Assert.Contains(u, x => x.Contains("çalıştırılamamış")); Assert.Contains(u, x => x.Contains("30 günden eski") && x.Contains("ESKI"));
    }

    [Fact]
    public void Ortak_Sorunlar_Makine_Sayisina_Gore_Ve_Degisken_Kisim_Birlesir()
    {
        var a = Oz("A", 70, null, true, 0, OB("Port", "Açık port 135 (RPC)", "Orta"), OB("Hizmet", "Tırnaksız hizmet yolu: AlfaServis", "Orta"), OB("X", "bilgi notu", "Bilgi"), OB("Y", "Bu kontrol çalıştırılamadı", "Bilgi"));
        var b = Oz("B", 70, null, true, 0, OB("Port", "Açık port 135 (RPC)", "Yuksek"), OB("Hizmet", "Tırnaksız hizmet yolu: BetaServis", "Orta")); var c = Oz("C", 90, null, true, 0, OB("Port", "Açık port 3389 (RDP)", "Orta"));
        var s = FiloRaporu.Hesapla(new() { a, b, c }, Simdi, 30, new());
        var rpc = s.EnSik.Single(g => g.Baslik == "Açık port 135 (RPC)"); Assert.Equal(2, rpc.Makineler.Count); Assert.Equal(Onem.Yuksek, rpc.EnYuksek);
        var hz = s.EnSik.Single(g => g.Baslik == "Tırnaksız hizmet yolu"); Assert.Equal(2, hz.Makineler.Count);
        Assert.DoesNotContain(s.EnSik, g => g.Baslik.Contains("bilgi notu") || g.Baslik.Contains("çalıştırılamadı")); Assert.Equal(3, s.EnSik.Count);
    }

    [Fact]
    public void Kvkk_Iso_Her_Makinede_Bir_Kez_Sayilir()
    {
        var a = Oz("A", 50, null, true, 0, OB("Port", "Açık port 445 (SMB)", "Orta"), OB("Port", "Açık port 139 (NetBIOS)", "Orta")); var b = Oz("B", 50, null, true, 0, OB("Port", "Açık port 445 (SMB)", "Orta"));
        var s = FiloRaporu.Hesapla(new() { a, b }, Simdi, 30, new()); Assert.True(s.Iso.Count > 0); Assert.All(s.Iso.Values, v => Assert.InRange(v, 1, 2)); Assert.Equal(2, s.Iso["A.8.20"]);
    }

    [Fact]
    public void Bos_Liste_Cokmez() { var s = FiloRaporu.Hesapla(new(), Simdi, 30, new()); Assert.Empty(s.Makineler); Assert.Equal(0, s.Ortalama); Assert.Contains("Filo Güvenlik Raporu", FiloRaporu.Html(s)); }

    // ---- HTML / CSV güvenliği ----
    [Fact]
    public void Html_Her_Metni_Kodlar_Betik_Enjeksiyonu_Olmaz()
    {
        string z = "<script>alert(1)</script>\"'&";
        var o = Oz(z, 50, null, true, 0, OB(z, z, "Yuksek", z)) with { Surum = z, Seviye = z };
        var h = FiloRaporu.Html(FiloRaporu.Hesapla(new() { o }, Simdi, 30, new()));
        Assert.DoesNotContain("<script>alert", h); Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", h); Assert.Contains("default-src 'none'", h); Assert.DoesNotContain("http://", h); Assert.DoesNotContain("https://", h); Assert.DoesNotContain("<script", h.Replace("&lt;script", ""));
    }

    [Fact]
    public void Csv_Formul_Enjeksiyonu_Ve_Ayrac_Kacisi()
    {
        var s = FiloRaporu.Hesapla(new() { Oz("=HYPERLINK(\"http://x\")", 50), Oz("a;b", 60), Oz("+cmd", 70), Oz("@x", 80) }, Simdi, 30, new()); var c = FiloRaporu.Csv(s);
        Assert.DoesNotContain("\n=HYPERLINK", c); Assert.Contains("'=HYPERLINK", c); Assert.Contains("\"a;b\"", c); Assert.Contains("'+cmd", c); Assert.Contains("'@x", c); Assert.StartsWith("Makine;Tarama (UTC);Puan", c);
    }

    // ---- Uçtan uca ----
    [Fact]
    public void Calistir_Html_Ve_Csv_Yazar_Ozetlere_Dokunmaz_Ikinci_Calistirma_Ezmez()
    {
        Yaz("a.json", Oz("PC-1", 90, null, true, 0, OB("Port", "Açık port 445 (SMB)", "Orta"))); Yaz("b.json", Oz("PC-2", 40)); string once = File.ReadAllText(Path.Combine(_k, "a.json"));
        var (kod, m) = FiloRaporu.Calistir(_k, null, Simdi); Assert.Equal(0, kod); var html = Directory.GetFiles(_k, "filo_raporu_*.html").Single(); Assert.True(File.Exists(Path.ChangeExtension(html, ".csv"))); Assert.Empty(Directory.GetFiles(_k, "*.tmp"));
        Assert.Equal(once, File.ReadAllText(Path.Combine(_k, "a.json"))); Assert.Contains("PC-2", File.ReadAllText(html));
        // Rapor dosyaları ".json" olmadığı için sonraki okumaya karışmaz; aynı saniyede ikinci çalıştırma aynı dosyanın üstüne yazar (rapor, özet değildir).
        Assert.Equal(0, FiloRaporu.Calistir(_k, null, Simdi.AddMinutes(1)).Kod); Assert.Equal(2, Directory.GetFiles(_k, "filo_raporu_*.html").Length);
        // CSV'de Excel için BOM var.
        Assert.Equal(0xEF, File.ReadAllBytes(Path.ChangeExtension(html, ".csv"))[0]);
    }

    [Fact]
    public void Calistir_Dosya_Yolu_Ve_Hatalar()
    {
        Yaz("a.json", Oz("PC-1", 90)); string hedef = Path.Combine(_k, "cikti", "rapor.html"); Assert.Equal(0, FiloRaporu.Calistir(_k, hedef, Simdi).Kod); Assert.True(File.Exists(hedef)); Assert.True(File.Exists(Path.Combine(_k, "cikti", "rapor.csv")));
        Assert.Equal(2, FiloRaporu.Calistir(Path.Combine(_k, "yok"), null, Simdi).Kod);
        string bos = Path.Combine(_k, "bos"); Directory.CreateDirectory(bos); var (k2, m2) = FiloRaporu.Calistir(bos, null, Simdi); Assert.Equal(2, k2); Assert.Contains("geçerli", m2);
    }

    [Fact]
    public void Tara_Ciktisi_Dogrudan_Filo_Raporuna_Girer()
    {
        // Gerçek tarama özetinin şeması (Ozetle) rapor tarafından okunabilmeli: iki taraf birlikte çalışıyor.
        var bulgular = new List<Bulgu> { new("Karne", "Güvenlik duvarı kapalı", Onem.Yuksek, "d", "Açın. Ek."), new("Port", "Açık port 445 (SMB)", Onem.Orta, "d", "Kapatın.") };
        var ozet = TaramaCalistirici.Ozetle(bulgular, "PC-X", Simdi, true, "1.0.0"); TaramaCalistirici.Yaz(Path.Combine(_k, "PC-X.json"), ozet);
        var s = FiloRaporu.Hesapla(FiloRaporu.Oku(_k, Simdi, new()), Simdi, 30, new()); Assert.Single(s.Makineler); Assert.Equal(ozet.Puan, s.Makineler[0].Son.Puan); Assert.Equal(2, s.Makineler[0].Son.Bulgular.Count);
    }

    // ---- Komut satırı ----
    [Fact]
    public void Filo_Argumani_Cozulur_Ve_Hatalar()
    {
        var i = TaramaCalistirici.ArgCoz(new[] { "--filo", @"\\s\ozet", "--cikti", "r.html" }); Assert.Equal(@"\\s\ozet", i.Filo); Assert.Equal("r.html", i.Cikti); Assert.Null(i.Hata); Assert.False(i.Tara);
        Assert.NotNull(TaramaCalistirici.ArgCoz(new[] { "--filo" }).Hata); Assert.NotNull(TaramaCalistirici.ArgCoz(new[] { "--filo", "x", "--tara" }).Hata); Assert.Contains("Bilinmeyen", TaramaCalistirici.ArgCoz(new[] { "--filo", "x", "--yanlis" }).Hata);
        Assert.NotNull(TaramaCalistirici.ArgCoz(new[] { "--cikti", "x" }).Hata); Assert.Null(TaramaCalistirici.ArgCoz(new[] { "--tara", "--cikti", "x" }).Filo);
        Assert.Equal(1, TaramaCalistirici.Giris(new[] { "--filo" }));
    }
}
