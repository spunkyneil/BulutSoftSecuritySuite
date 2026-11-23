// Bu dosya küçük ekranlarda (1366x768 ve %150 ölçekli 1080p = 1280x720 mantıksal alan) pencerelerin ekrana sığdığını ve kesilen içeriğin KAYDIRMAYLA ulaşılabildiğini doğrular.
// Çalışma alanı (görev çubuğu hariç) olarak 1366x728 ve 1280x680 kullanılır. Pencereler gösterilmeden oluşturulur; gerçek ekrana dokunulmaz.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class EkranSigdirmaTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_sigdir_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    public EkranSigdirmaTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; }
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "EkranSigdirmaTestleri.temizlik"); } }

    public static IEnumerable<object[]> Alanlar() { yield return new object[] { 1366, 728 }; yield return new object[] { 1280, 680 }; }

    // Sayfa kabuğun çalışma alanına (pencere eksi sol menü ve üst çubuk) konunca içeriği ya sığar ya da sayfa kaydırılabilir olur (D5: Form değil UserControl sayfa).
    // Ana pencere (gerçek Form): ekrana sığdırma efekti uygulanınca boyut ekrandan büyük olmaz, kesilen içerik kaydırılabilir olur.
    private static void DogrulaPencere(Form f, int genislik, int yukseklik, bool ozgunKadarKaydir = true)
    {
        var ozgun = f.ClientSize; var alan = new Rectangle(0, 0, genislik, yukseklik); PencereEfekti.EkranaSigdir(f, alan);
        Assert.True(f.Width <= genislik && f.Height <= yukseklik, $"{f.GetType().Name} sığmadı: {f.Width}x{f.Height} > {genislik}x{yukseklik}");
        if (ozgun.Width > f.ClientSize.Width || ozgun.Height > f.ClientSize.Height) { Assert.True(f.AutoScroll, f.GetType().Name + " kaydırılamıyor"); Assert.True(!ozgunKadarKaydir || (f.AutoScrollMinSize.Width >= ozgun.Width && f.AutoScrollMinSize.Height >= ozgun.Height), f.GetType().Name + " kaydırma alanı özgün içerikten küçük"); }
        Assert.True(f.Left >= 0 && f.Top >= 0 && f.Right <= genislik && f.Bottom <= yukseklik, f.GetType().Name + " ekran dışında");
    }

    private static void Dogrula(SayfaDenetimi f, int genislik, int yukseklik, bool ozgunKadarKaydir = true)
    {
        var alan = new Size(genislik - 240, yukseklik - 113);
        using var host = SayfaTestKabi.Goster(f, alan.Width, alan.Height); host.PerformLayout();
        Assert.Equal(alan.Width, f.Width); Assert.Equal(alan.Height, f.Height);
        // Doğrudan çocukların kapladığı gerçek alan: dock'lu kapsayıcılar sayfayı doldurur; sabit boyutlular sığmıyorsa kaydırma açık olmalıdır.
        int sag = 0, alt = 0; foreach (Control c in f.Controls) { if (!c.Visible || c.Dock != DockStyle.None) continue; sag = Math.Max(sag, c.Right); alt = Math.Max(alt, c.Bottom); }
        Assert.True((sag <= alan.Width && alt <= alan.Height) || f.AutoScroll, f.GetType().Name + " sığmıyor ve kaydırılamıyor: " + sag + "x" + alt + " > " + alan.Width + "x" + alan.Height);
    }

    [Theory, MemberData(nameof(Alanlar))]
    public void TuzakFormu_Sigar(int g, int y) { using var f = new TuzakFormu(new HoneypotKapi(), new CanaryDosyalari(), new FidyeIzleyici(), _ => { }); Dogrula(f, g, y); }

    [Theory, MemberData(nameof(Alanlar))]
    public void MerkezFormu_Sigar(int g, int y) { using var f = new MerkezFormu(0, _ => { }, () => { }, () => { }); Dogrula(f, g, y); }

    [Theory, MemberData(nameof(Alanlar))]
    public void GozetimsizFormu_Sigar(int g, int y) { using var f = new GozetimsizFormu(_ => { }); Dogrula(f, g, y); }

    [Theory, MemberData(nameof(Alanlar))]
    public void SensorFormu_Sigar(int g, int y) { using var f = new SensorFormu(_ => { }); Dogrula(f, g, y); }

    [Theory, MemberData(nameof(Alanlar))]
    public void AnaPencere_Sigar_Ve_Icerik_Kaydirilabilir(int g, int y)
    {
        using var f = new Form1(); DogrulaPencere(f, g, y, false);
        // Kurumsal kabukta kaydırma pencere düzeyinde DEĞİL, en az alan isteyen eski sayfaların içindedir (BolumSayfasi.enAzIcerik); menü ve üst çubuk her boyutta görünür kalır.
        // (Bu testin Dogrula çağrısı EkranaSigdir'i elle çalıştırır ve o AutoScroll açar; Form1 üretimde ekrana sığdırmadan hariçtir, kendi ekranlarını kabuk yönetir.)
        Assert.True(f.MinimumSize.Width <= 1024); Assert.True(f.MinimumSize.Height <= 640);
    }

    [Fact]
    public void Ekrana_Zaten_Sigan_Pencereye_Dokunulmaz()
    {
        using var f = new Form { ClientSize = new Size(400, 300) }; int w = f.Width, h = f.Height; PencereEfekti.EkranaSigdir(f, new Rectangle(0, 0, 1366, 728)); Assert.Equal(w, f.Width); Assert.Equal(h, f.Height); Assert.False(f.AutoScroll);
    }
}
