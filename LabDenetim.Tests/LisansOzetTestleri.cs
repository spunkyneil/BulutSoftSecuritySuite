// Adım 3, madde 3: --tara özetinde lisans durumu (taramayı engellemez, hiçbir kayıt yazmaz) ve filo raporunda lisans görünümü.
using System.Text.Json;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class LisansOzetTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_lizoz_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiMakine = Lisans.MakineKlasoru, _eskiReg = Lisans.RegistryYolu;
    public LisansOzetTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = Path.Combine(_t, "veri"); Lisans.MakineKlasoru = Path.Combine(_t, "makine"); Lisans.RegistryYolu = @"Software\BulutSoft\LicTest_" + Guid.NewGuid().ToString("N"); }
    public void Dispose() { try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(Lisans.RegistryYolu, false); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "LisansOzetTestleri.temizlik"); } Lisans.RegistryYolu = _eskiReg; Lisans.MakineKlasoru = _eskiMakine; BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "LisansOzetTestleri.temizlik"); } }
    static readonly DateTime Z = new(2026, 11, 19, 10, 30, 5, DateTimeKind.Utc);

    [Fact]
    public void Tara_Ozeti_Lisans_Durumunu_Yazar_Ve_Hicbir_Lisans_Kaydi_Olusturmaz()
    {
        string k = Path.Combine(_t, "cikti") + "\\"; var (kod, yol) = TaramaCalistirici.Calistir(k, () => new(), Z); Assert.Equal(0, kod);
        var o = JsonSerializer.Deserialize<TaramaOzeti>(File.ReadAllText(yol), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal("Deneme", o.Lisans); Assert.Equal(14, o.LisansKalanGun);   // kayıt yok: deneme henüz başlamadı, 14 gün görünür
        Assert.False(File.Exists(Path.Combine(Lisans.MakineKlasoru, "deneme.dat"))); Assert.False(File.Exists(Path.Combine(BulutKlasor.Kok, "deneme.dat")));   // tarama deneme saatini BAŞLATMAZ
    }

    [Fact]
    public void Lisans_Okunamazsa_Tarama_Yine_Calisir()
    {
        var (kod, a) = LisansBilgisiAl(); Assert.NotNull(kod); Assert.True(a == null || a >= 0);
    }
    private static (string?, int?) LisansBilgisiAl() => TaramaCalistirici.LisansBilgisi(Z.ToLocalTime());

    private static TaramaOzeti Oz(string m, string? lisans, int? gun = null) => new(1, m, Z, "1.0.0", true, 90, "Genel Durum İyi", 0, 0, 0, 0, 0, new(), lisans, gun);

    [Fact]
    public void Filo_Raporu_Sorunlu_Lisansi_Uyarir_Ve_Raporda_Gosterir()
    {
        var u = new List<string>(); var s = FiloRaporu.Hesapla(new() { Oz("A", "Lisansli", 40), Oz("B", "DenemeBitti"), Oz("C", "LisansSuresiDoldu"), Oz("D", null) }, Z, 30, u);
        Assert.Contains(u, x => x.Contains("2 makinede lisans") && x.Contains("B") && x.Contains("C")); Assert.DoesNotContain(u, x => x.Contains(" A") && x.Contains("lisans"));
        string h = FiloRaporu.Html(s), c = FiloRaporu.Csv(s);
        Assert.Contains("<th>Lisans</th>", h); foreach (var m in new[] { "Lisanslı (40 gün)", "Deneme bitti", "Süresi doldu" }) Assert.Contains(System.Net.WebUtility.HtmlEncode(m), h);
        Assert.Contains(";Lisans", c.Split("\r\n")[0]); Assert.Contains("Deneme bitti", c);
    }

    [Fact]
    public void Eski_Surum_Ozeti_Lisans_Alani_Olmadan_Okunur_Uyari_Uretmez()
    {
        string json = "{\"Format\":1,\"Makine\":\"ESKI\",\"ZamanUtc\":\"2026-11-19T10:00:00Z\",\"Surum\":\"1.0.0\",\"Yonetici\":true,\"Puan\":80,\"Seviye\":\"x\",\"Yuksek\":0,\"Orta\":0,\"Dusuk\":0,\"Bilgi\":0,\"Atlanan\":0,\"Bulgular\":[]}";
        string d = Path.Combine(_t, "filo"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "ESKI.json"), json);
        var u = new List<string>(); var l = FiloRaporu.Oku(d, Z, u); Assert.Single(l); Assert.Null(l[0].Lisans);
        var s = FiloRaporu.Hesapla(l, Z, 30, u); Assert.DoesNotContain(u, x => x.Contains("lisans")); Assert.Contains(System.Net.WebUtility.HtmlEncode("—"), FiloRaporu.Html(s));
    }

    [Fact]
    public void Dusman_Dosyadaki_Lisans_Metni_Rapora_Giremez()
    {
        string json = "{\"Format\":1,\"Makine\":\"M\",\"ZamanUtc\":\"2026-11-19T10:00:00Z\",\"Surum\":\"1\",\"Yonetici\":true,\"Puan\":80,\"Seviye\":\"x\",\"Yuksek\":0,\"Orta\":0,\"Dusuk\":0,\"Bilgi\":0,\"Atlanan\":0,\"Bulgular\":[],\"Lisans\":\"<script>alert(1)</script>\",\"LisansKalanGun\":-5}";
        string d = Path.Combine(_t, "filo2"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "M.json"), json);
        var l = FiloRaporu.Oku(d, Z, new()); Assert.Null(l[0].Lisans); Assert.Equal(0, l[0].LisansKalanGun);
        string h = FiloRaporu.Html(FiloRaporu.Hesapla(l, Z, 30, new())); Assert.DoesNotContain("<script>alert", h);
    }
}
