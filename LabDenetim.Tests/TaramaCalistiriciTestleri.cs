// Gözetimsiz tarama (--tara) testleri: argümanlar, özet üretimi, çıktı yolu, yazım güvenliği ve gerçek (salt-okunur) tarama.
using System.Text.Json;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class TaramaCalistiriciTestleri : IDisposable
{
    // Geçici veri/çıktı klasörü (gerçek verilere dokunulmaz).
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_tara_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    public TaramaCalistiriciTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = Path.Combine(_t, "veri"); }
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "TaramaCalistiriciTestleri.temizlik"); } }
    static readonly DateTime Z = new(2026, 11, 19, 10, 30, 5, DateTimeKind.Utc);
    static Bulgu B(string m, string b, Onem o, string d = "ayrıntı", string oneri = "öneri") => new(m, b, o, d, oneri);

    // ---- Argümanlar ----
    [Fact]
    public void Arguman_Tara_Ve_Cikti_Cozulur()
    {
        var i = TaramaCalistirici.ArgCoz(new[] { "--tara", "--cikti", @"\\sunucu\ozet\" }); Assert.True(i.Tara); Assert.Equal(@"\\sunucu\ozet\", i.Cikti); Assert.Null(i.Hata);
        Assert.Equal("a b.json", TaramaCalistirici.ArgCoz(new[] { "--cikti", "\"a b.json\"", "--tara" }).Cikti); Assert.True(TaramaCalistirici.ArgCoz(new[] { "--TARA" }).Tara);
    }

    [Fact]
    public void Arguman_Normal_Acilis_Ve_Yardim_Ve_Hatalar()
    {
        Assert.False(TaramaCalistirici.ArgCoz(Array.Empty<string>()).Tara); Assert.False(TaramaCalistirici.ArgCoz(new[] { "--oturum-degisti" }).Tara); Assert.Null(TaramaCalistirici.ArgCoz(new[] { "--oturum-degisti" }).Hata);
        Assert.True(TaramaCalistirici.ArgCoz(new[] { "--yardim" }).Yardim); Assert.True(TaramaCalistirici.ArgCoz(new[] { "-h" }).Yardim);
        Assert.NotNull(TaramaCalistirici.ArgCoz(new[] { "--tara", "--cikti" }).Hata); Assert.NotNull(TaramaCalistirici.ArgCoz(new[] { "--tara", "--cikti", "--x" }).Hata);
        Assert.NotNull(TaramaCalistirici.ArgCoz(new[] { "--cikti", "x" }).Hata); Assert.Contains("Bilinmeyen", TaramaCalistirici.ArgCoz(new[] { "--tara", "--tarma" }).Hata);
        // Tarama istenmeyen bilinmeyen bayrak arayüzü engellemez.
        Assert.Null(TaramaCalistirici.ArgCoz(new[] { "--baska" }).Hata);
    }

    [Fact]
    public void Giris_Arayuz_Icin_Eksi_Bir_Doner() => Assert.Equal(-1, TaramaCalistirici.Giris(new[] { "--oturum-degisti" }));

    // ---- Özet ----
    [Fact]
    public void Ozet_Sayar_Siralar_Ve_Calismayan_Kontrolu_Ayri_Sayar()
    {
        // "Bu kontrol çalıştırılamadı" bulgusu listede kalır ama puana/sayılara girmez, Atlanan olarak sayılır.
        var l = new List<Bulgu> { B("A", "bilgi", Onem.Bilgi), B("B", "yüksek", Onem.Yuksek), B("C", "orta", Onem.Orta), B("D", "düşük", Onem.Dusuk), B("E", "Bu kontrol çalıştırılamadı", Onem.Bilgi) };
        var o = TaramaCalistirici.Ozetle(l, "PC-01", Z, true, "1.0.0");
        Assert.Equal(1, o.Yuksek); Assert.Equal(1, o.Orta); Assert.Equal(1, o.Dusuk); Assert.Equal(1, o.Bilgi); Assert.Equal(1, o.Atlanan); Assert.Equal(5, o.Bulgular.Count); Assert.Equal("yüksek", o.Bulgular[0].Baslik); Assert.Equal("Yuksek", o.Bulgular[0].Onem);
        Assert.Equal(100 - 15 - 7 - 3, o.Puan); Assert.Equal(TaramaCalistirici.FormatSurumu, o.Format); Assert.Equal("PC-01", o.Makine); Assert.True(o.Yonetici);
    }

    [Fact]
    public void Ozet_Puan_Arayuzdeki_Karneyle_Ayni_Ve_Seviye_Dogru()
    {
        var l = Enumerable.Range(0, 3).Select(i => B("M", "x" + i, Onem.Yuksek)).ToList(); var o = TaramaCalistirici.Ozetle(l, "PC", Z, false, "1");
        Assert.Equal(KarneHesabi.Puan(l), o.Puan); Assert.Equal(55, o.Puan); Assert.Equal("Dikkat Gerekiyor", o.Seviye); Assert.Equal("Acil Müdahale Gerekiyor", TaramaCalistirici.Ozetle(Enumerable.Range(0, 5).Select(i => B("M", "y" + i, Onem.Yuksek)), "PC", Z, false, "1").Seviye); Assert.Equal("Genel Durum İyi", TaramaCalistirici.Ozetle(Array.Empty<Bulgu>(), "PC", Z, false, "1").Seviye);
    }

    [Fact]
    public void Uzun_Ayrinti_Kisaltilir_Ve_Turkce_Karakter_Okunur_Kalir()
    {
        var o = TaramaCalistirici.Ozetle(new[] { B("M", "Şişli ğüöçı", Onem.Orta, new string('x', 5000)) }, "PC", Z, false, "1"); Assert.True(o.Bulgular[0].Detay.Length <= 601);
        string yol = Path.Combine(_t, "o.json"); TaramaCalistirici.Yaz(yol, o); var metin = File.ReadAllText(yol); Assert.Contains("Şişli ğüöçı", metin); Assert.DoesNotContain("\\u", metin);
    }

    [Fact]
    public void Ozet_Kisisel_Kullanici_Alani_Icermez()
    {
        var json = JsonSerializer.Serialize(TaramaCalistirici.Ozetle(new[] { B("M", "x", Onem.Bilgi) }, "PC", Z, false, "1")); Assert.DoesNotContain("Kullanici", json); Assert.DoesNotContain(Environment.UserName + "\"", json);
    }

    // ---- Çıktı yolu ----
    [Fact]
    public void Klasor_Modu_Zaman_Damgali_Ve_Uzerine_Yazmaz()
    {
        string k = Path.Combine(_t, "paylasim"); Directory.CreateDirectory(k);
        string a = TaramaCalistirici.CiktiYolu(k, "PC 01/x", Z); Assert.Equal(Path.Combine(k, "PC-01-x_20261119_103005.json"), a); File.WriteAllText(a, "{}");
        string b = TaramaCalistirici.CiktiYolu(k, "PC 01/x", Z); Assert.EndsWith("_103005_2.json", b); File.WriteAllText(b, "{}"); Assert.EndsWith("_103005_3.json", TaramaCalistirici.CiktiYolu(k, "PC 01/x", Z));
        // Henüz olmayan klasör, sonu ayraçla bitiyorsa klasör sayılır.
        Assert.EndsWith(".json", TaramaCalistirici.CiktiYolu(Path.Combine(_t, "yeni") + Path.DirectorySeparatorChar, "PC", Z)); Assert.Contains(Path.Combine(_t, "yeni"), TaramaCalistirici.CiktiYolu(Path.Combine(_t, "yeni") + "\\", "PC", Z));
    }

    [Fact]
    public void Dosya_Modu_Tam_Yol_Ve_Varsayilan_Veri_Klasoru()
    {
        Assert.Equal(Path.Combine(_t, "tek.json"), TaramaCalistirici.CiktiYolu(Path.Combine(_t, "tek.json"), "PC", Z));
        Assert.StartsWith(Path.Combine(BulutKlasor.Kok, "ozetler"), TaramaCalistirici.CiktiYolu(null, "PC", Z)); Assert.Equal("MAKINE", TaramaCalistirici.DosyaAdi("///"));
        // Uzantısız yol (örn. tırnak içinde "C:\x\" yazılınca son ters bölünün kaybolması) klasör sayılır, uzantısız dosya yazılmaz.
        var k = TaramaCalistirici.CiktiYolu(Path.Combine(_t, "paylasim_yeni"), "PC", Z); Assert.StartsWith(Path.Combine(_t, "paylasim_yeni") + Path.DirectorySeparatorChar, k); Assert.EndsWith(".json", k);
        Assert.Equal(Path.Combine(_t, "BÜYÜK.JSON"), TaramaCalistirici.CiktiYolu(Path.Combine(_t, "BÜYÜK.JSON"), "PC", Z));
    }

    // ---- Çalıştırma ----
    [Fact]
    public void Calistir_Dosya_Yazar_Gecerli_Json_Ve_Gecici_Dosya_Kalmaz()
    {
        string k = Path.Combine(_t, "cikti"); var (kod, yol) = TaramaCalistirici.Calistir(k + "\\", () => new() { B("Karne", "Güvenlik duvarı kapalı", Onem.Yuksek), B("Hesap", "tamam", Onem.Bilgi) }, Z);
        Assert.Equal(0, kod); Assert.True(File.Exists(yol)); Assert.False(File.Exists(yol + ".tmp"));
        var o = JsonSerializer.Deserialize<TaramaOzeti>(File.ReadAllText(yol))!; Assert.Equal(2, o.Bulgular.Count); Assert.Equal(1, o.Yuksek); Assert.Equal(Environment.MachineName, o.Makine); Assert.Equal(Z, o.ZamanUtc);
    }

    [Fact]
    public void Ikinci_Calistirma_Oncekini_Silmez()
    {
        string k = Path.Combine(_t, "c2") + "\\"; var a = TaramaCalistirici.Calistir(k, () => new(), Z); var b = TaramaCalistirici.Calistir(k, () => new(), Z);
        Assert.Equal(0, a.Kod); Assert.Equal(0, b.Kod); Assert.NotEqual(a.Mesaj, b.Mesaj); Assert.True(File.Exists(a.Mesaj)); Assert.True(File.Exists(b.Mesaj));
    }

    [Fact]
    public void Toplama_Hata_Verirse_Kod_Iki_Ve_Dosya_Olusmaz()
    {
        string k = Path.Combine(_t, "c3"); var (kod, m) = TaramaCalistirici.Calistir(k + "\\", () => throw new InvalidOperationException("patladı"), Z); Assert.Equal(2, kod); Assert.Contains("patladı", m); Assert.False(Directory.Exists(k) && Directory.GetFiles(k).Length > 0);
    }

    [Fact]
    public void Yazilamayan_Konum_Kod_Iki_Doner()
    {
        // Dosya olan bir yerin "altına" klasör açılamaz.
        string dosya = Path.Combine(_t, "dosya.txt"); File.WriteAllText(dosya, "x"); var (kod, _) = TaramaCalistirici.Calistir(Path.Combine(dosya, "alt", "o.json"), () => new(), Z); Assert.Equal(2, kod);
    }

    [Fact]
    public void Gercek_Tarama_Salt_Okunur_Calisir_Ve_Ortak_Depolara_Yazmaz()
    {
        // Gerçek yerel denetim (yalnızca okuma): veri klasörü geçiciye yönlendirildiği için gerçek verilere dokunulmaz.
        string k = Path.Combine(_t, "gercek") + "\\"; var (kod, yol) = TaramaCalistirici.Calistir(k);
        Assert.Equal(0, kod); var o = JsonSerializer.Deserialize<TaramaOzeti>(File.ReadAllText(yol))!; Assert.Equal(1, o.Format); Assert.InRange(o.Puan, 0, 100); Assert.NotNull(o.Bulgular);
        // Arayüzün ortak bulgu/varlık/yanlış alarm dosyaları OLUŞTURULMAMALI.
        foreach (var ad in new[] { "bulgular_v2.json", "ag_haritasi.json", "fp_takip.json", "kimlik.json", "lisans.bslic" }) Assert.False(File.Exists(Path.Combine(BulutKlasor.Kok, ad)), ad);
        Assert.Empty(Directory.GetFiles(_t, "*.tmp", SearchOption.AllDirectories));
    }
}
