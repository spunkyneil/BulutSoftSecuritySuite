// HataKayit (CLAUDE.md kural 5) testleri: bastırılan hatalar sayılır ve saklanır, hiçbir zaman istisna fırlatmaz, kritik akışta günlüğe sınırlı yazar;
// ayrıca BulutSoft.Core kaynağında boş catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "HataKayitTestleri.temizlik"); } kalmadığı denetlenir.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// OlayDeposu.Yol'u değiştiren testlerle çakışmasın.
[Collection("Veri")]
public class HataKayitTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_hk_" + Guid.NewGuid().ToString("N")); private readonly string _eskiYol; private readonly bool _eskiAyna;

    public HataKayitTestleri()
    {
        _eskiYol = OlayDeposu.Yol; _eskiAyna = OlayDeposu.AynaAktif; Directory.CreateDirectory(_t); OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); OlayDeposu.AynaAktif = false; HataKayit.Sifirla();
    }

    public void Dispose() { OlayDeposu.Yol = _eskiYol; OlayDeposu.AynaAktif = _eskiAyna; try { Directory.Delete(_t, true); } catch (IOException ex) { HataKayit.Sessiz(ex, "HataKayitTestleri.Dispose"); } }

    [Fact]
    public void Sessiz_BaglamBasinaSayarVeSonHatalariTutar()
    {
        HataKayit.Sessiz(new InvalidOperationException("bir"), "ctx.A"); HataKayit.Sessiz(new IOException("iki"), "ctx.A"); HataKayit.Sessiz(new TimeoutException("uc"), "ctx.B");
        Assert.Equal(2, HataKayit.Sayi("ctx.A")); Assert.Equal(1, HataKayit.Sayi("ctx.B")); Assert.Equal(0, HataKayit.Sayi("yok")); Assert.Equal(3, HataKayit.Toplam);
        var son = HataKayit.SonHatalar(); Assert.Equal(3, son.Count); Assert.Equal("ctx.A", son[0].Baglam); Assert.Equal("InvalidOperationException", son[0].Tur); Assert.Equal("uc", son[^1].Mesaj);
        Assert.Equal(2, HataKayit.Ozet()["ctx.A"]);
    }

    [Fact]
    public void Sessiz_BosVeyaNullGirdiyleDeFirlatmaz_VeBellekSinirlidir()
    {
        HataKayit.Sessiz(null!, ""); HataKayit.Sessiz(new Exception(), null!); Assert.Equal(2, HataKayit.Sayi("bilinmeyen"));
        for (int i = 0; i < 1000; i++) HataKayit.Sessiz(new Exception("x" + i), "dongu");
        Assert.Equal(1000, HataKayit.Sayi("dongu")); Assert.True(HataKayit.SonHatalar().Count <= 200);   // halka tampon
    }

    [Fact]
    public async Task Sessiz_CokIsParcacigindenGuvenlidir()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(k => Task.Run(() => { for (int i = 0; i < 500; i++) HataKayit.Sessiz(new Exception("e"), "es.zamanli"); })));
        Assert.Equal(4000, HataKayit.Sayi("es.zamanli"));
    }

    [Fact]
    public void Kritik_OlayGunlugunuSinirliSayidaYazar_VeYenidenGirisYapmaz()
    {
        for (int i = 0; i < 10; i++) HataKayit.Sessiz(new InvalidOperationException("patladı"), "kritik.akis", true);
        int yazilan = OlayDeposu.Oku().Count(o => o.Mesaj.StartsWith("SESSİZ HATA (kritik.akis)", StringComparison.Ordinal));
        Assert.Equal(3, yazilan);                                  // ilk 3 hata yazılır; sonrası (50'de bir) yazılmaz
        Assert.Equal(10, HataKayit.Sayi("kritik.akis"));
        HataKayit.Sessiz(new Exception("kritik olmayan"), "sakin"); Assert.DoesNotContain(OlayDeposu.Oku(), o => o.Mesaj.Contains("(sakin)", StringComparison.Ordinal));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void UretimKaynagi_Cekirdek_Arayuz_Hizmet_BosCatchIcermez()
    {
        string? d = AppContext.BaseDirectory; string? kok = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (Directory.Exists(Path.Combine(d, "BulutSoft.Core"))) { kok = d; break; }
        if (kok == null) return;   // kaynak ağacı yoksa atla
        var bos = new Regex(@"catch(\s*\(\s*[\w\.]+(?:\s+\w+)?\s*\))?\s*\{\s*(?:/\*.*?\*/\s*)?\}", RegexOptions.Singleline); var kotu = new List<string>();
        foreach (var proje in new[] { "BulutSoft.Core", "LabDenetim", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                int n = bos.Matches(File.ReadAllText(f)).Count; if (n > 0) kotu.Add(proje + "/" + Path.GetFileName(f) + ":" + n);
            }
        Assert.True(kotu.Count == 0, "Boş catch kalmış (HataKayit.Sessiz kullanın): " + string.Join(", ", kotu));
    }
}
