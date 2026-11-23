// Kalıcılık Denetçisi "kapat → geri al" akışı GERÇEK kayıt defteri koduyla denenir; ancak yol, gerçek StartupApproved yerine
// bu teste özel geçici bir HKCU anahtarına yönlendirilir (kullanıcının gerçek başlangıç ayarlarına dokunulmaz).
using LabDenetim.V2;
using Microsoft.Win32;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class KaliciGercekKodTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_kal_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiKok = KaliciDenetci.OnayliKok;
    private readonly string _anahtar = @"Software\BulutSoft\TestKalicilik_" + Guid.NewGuid().ToString("N");
    public KaliciGercekKodTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; KaliciDenetci.OnayliKok = _anahtar + @"\"; }
    public void Dispose() { KaliciDenetci.OnayliKok = _eskiKok; BulutKlasor.Kok = _eski; try { Registry.CurrentUser.DeleteSubKeyTree(_anahtar, false); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "KaliciGercekKodTestleri.temizlik"); } try { Directory.Delete(_t, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "KaliciGercekKodTestleri.temizlik"); } }

    private static KaliciOge Oge(bool ms = false, bool wow = false) => new() { Tur = "Run", Ad = "BulutSoftTestOgesi", Konum = @"HKCU\Software\Run", Hklm = false, Wow64 = wow, Microsoft = ms };
    private byte[]? Deger(string alt) { using var k = Registry.CurrentUser.OpenSubKey(_anahtar + @"\" + alt); return k?.GetValue("BulutSoftTestOgesi") as byte[]; }

    [Fact]
    public async Task Kapat_Yedekle_GeriAl_Gercek_Kayit_Defteri_Koduyla()
    {
        var s = new GercekSistemCalistirici(); var e = new BaslangicDevreDisiEylemi(Oge(), s);
        Assert.Null(Deger("Run")); Assert.False(KaliciDenetci.OnayliDevreDisi(false, "Run", "BulutSoftTestOgesi"));   // başlangıçta kayıt yok = etkin
        var y = await e.BackupAsync(default); Assert.True(y.Ok); Assert.Single(KaliciDenetci.YedekOku()); Assert.Equal("BulutSoftTestOgesi", KaliciDenetci.YedekOku()[0].Ad);
        await e.ApplyAsync(default);
        Assert.True(KaliciDenetci.OnayliDevreDisi(false, "Run", "BulutSoftTestOgesi")); Assert.Equal(3, Deger("Run")![0]); Assert.Equal(12, Deger("Run")!.Length);
        await e.RollbackAsync(y, default);
        Assert.False(KaliciDenetci.OnayliDevreDisi(false, "Run", "BulutSoftTestOgesi")); Assert.Equal(2, Deger("Run")![0]);   // 02 = etkin
    }

    [Fact]
    public async Task Yedekten_Geri_Alma_Kapat_Kapat_Cift_Uygulamada_Tutarli()
    {
        var s = new GercekSistemCalistirici(); var e = new BaslangicDevreDisiEylemi(Oge(), s); var y = await e.BackupAsync(default);
        await e.ApplyAsync(default); await e.ApplyAsync(default);   // iki kez kapatmak durumu bozmaz
        Assert.True(KaliciDenetci.OnayliDevreDisi(false, "Run", "BulutSoftTestOgesi"));
        await e.RollbackAsync(y, default); await e.RollbackAsync(y, default);
        Assert.False(KaliciDenetci.OnayliDevreDisi(false, "Run", "BulutSoftTestOgesi"));
    }

    [Fact]
    public async Task Wow64_Ogesi_Run32_Altina_Yazilir()
    {
        var e = new BaslangicDevreDisiEylemi(Oge(wow: true), new GercekSistemCalistirici()); await e.ApplyAsync(default);
        Assert.NotNull(Deger("Run32")); Assert.Null(Deger("Run")); Assert.True(KaliciDenetci.OnayliDevreDisi(false, "Run32", "BulutSoftTestOgesi"));
    }

    [Fact]
    public async Task Microsoft_Imzali_Oge_Gercek_Kodda_Da_Hic_Degismez()
    {
        var e = new BaslangicDevreDisiEylemi(Oge(ms: true), new GercekSistemCalistirici());
        Assert.False((await e.BackupAsync(default)).Ok); await Assert.ThrowsAsync<DegisiklikYapilmadiException>(() => e.ApplyAsync(default));
        Assert.Null(Deger("Run")); Assert.Empty(KaliciDenetci.YedekOku());
    }

    [Fact]
    public void Test_Yolu_Gercek_StartupApproved_Degil()
    {
        Assert.StartsWith(@"Software\BulutSoft\TestKalicilik_", KaliciDenetci.OnayliKok); Assert.DoesNotContain("StartupApproved", KaliciDenetci.OnayliKok);
    }
}
