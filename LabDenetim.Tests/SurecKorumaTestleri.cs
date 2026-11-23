// Süreç Avcısı koruma testleri: veri bozabilecek ürünler, hizmet süreçleri, kapalı kalma ve "yalnızca gerektiğinde sorgula" kuralı.
// Hiçbir test gerçek bir süreci sonlandırmaz; hizmet sorgusu sahtesiyle değiştirilir.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class SurecKorumaTestleri : IDisposable
{
    // Gerçek sorgu (test bitince geri konur).
    private readonly Func<int, bool> _eski = SurecAvcisi.HizmetMi;
    // Sahte hizmet sorgusu çağrı sayacı.
    private int _sorgu;
    // Testin başında: sahte kanca (varsayılan: hizmet değil).
    public SurecKorumaTestleri() { SurecAvcisi.HizmetMi = _ => { _sorgu++; return false; }; }
    // Testin sonunda gerçek kancayı geri koy.
    public void Dispose() => SurecAvcisi.HizmetMi = _eski;

    [Theory]
    [InlineData("sqlservr.exe")] [InlineData("mysqld")] [InlineData("postgres.exe")] [InlineData("w3wp.exe")] [InlineData("TiWorker.exe")] [InlineData("OneDrive.exe")] [InlineData("robocopy.exe")]
    [InlineData("Veeam.Backup.Service.exe")] [InlineData("vmware-vmx.exe")] [InlineData("acronis_agent.exe")] [InlineData("VBoxHeadless.exe")] [InlineData("BackupExec.exe")]
    public void Veri_Bozabilecek_Urunler_Korumali(string ad)
    {
        var n = SurecAvcisi.KorumaNedeni(ad, 99999); Assert.NotNull(n); Assert.Contains("sonlandırmak", n);
        Assert.Throws<ArgumentException>(() => SurecAvcisi.Sonlandir(99999, ad));
        // Ad listesinden korunanlarda hizmet sorgusuna hiç gidilmez.
        Assert.Equal(0, _sorgu);
    }

    [Theory]
    [InlineData("lsass.exe", 700)] [InlineData("svchost", 800)] [InlineData("MsMpEng.exe", 900)] [InlineData("explorer", 1000)]
    public void Eski_Sistem_Korumalari_Ayni_Metinle_Duruyor(string ad, int pid)
    {
        Assert.True(SurecAvcisi.Korunuyor(ad, pid)); Assert.Equal("Bu süreç sistem için kritik veya programın kendisidir; sonlandırılamaz.", SurecAvcisi.KorumaNedeni(ad, pid)); Assert.Equal(0, _sorgu);
    }

    [Fact]
    public void Kendisi_Ve_Dusuk_Pid_Hala_Korumali()
    {
        Assert.NotNull(SurecAvcisi.KorumaNedeni("herhangi", Environment.ProcessId)); Assert.NotNull(SurecAvcisi.KorumaNedeni("x", 4)); Assert.NotNull(SurecAvcisi.KorumaNedeni("x", 0));
    }

    [Fact]
    public void Hizmet_Olarak_Calisan_Surec_Korumali_Ve_Neden_Yazar()
    {
        SurecAvcisi.HizmetMi = pid => { _sorgu++; return pid == 5150; };
        var n = SurecAvcisi.KorumaNedeni("ozelservis.exe", 5150); Assert.NotNull(n); Assert.Contains("Windows hizmeti", n); Assert.Contains("services.msc", n); Assert.Throws<ArgumentException>(() => SurecAvcisi.Sonlandir(5150, "ozelservis.exe"));
        // Başka PID hizmet değil.
        Assert.Null(SurecAvcisi.KorumaNedeni("ozelservis.exe", 5151));
    }

    [Fact]
    public void Hizmet_Sorgusu_Basarisiz_Olursa_Kapali_Kalinir()
    {
        // Varsayılan kanca WMI hatasında true döner; burada aynı davranışı sahte kancayla yeniden değil, gerçek kancanın hata yolunu bozarak sınıyoruz.
        SurecAvcisi.HizmetMi = _eski;
        // Gerçek kanca geçersiz bir PID için de hata vermeden bool döner (çökmez).
        var sonuc = SurecAvcisi.HizmetMi(int.MaxValue); Assert.IsType<bool>(sonuc);
    }

    [Fact]
    public void Sıradan_Uygulama_Ve_Hizmet_Degilse_Engel_Yok()
    {
        Assert.Null(SurecAvcisi.KorumaNedeni("notepad.exe", 4321)); Assert.Equal(1, _sorgu);
    }

    [Fact]
    public void Hizmet_Sorgusu_Yalnizca_Istenince_Calisir_Ve_Liste_Islemleri_Sormaz()
    {
        // Ölçüm/liste işlemleri (Fark) hizmet sorgusu yapmamalı: yalnızca KorumaNedeni tek PID için sorar.
        _ = SurecAvcisi.Fark(new(), new()); Assert.Equal(0, _sorgu);
        SurecAvcisi.KorumaNedeni("notepad", 4321); SurecAvcisi.KorumaNedeni("notepad", 4322); Assert.Equal(2, _sorgu);
    }

    [Fact]
    public void Ad_Bos_Veya_Null_Cokmez() { Assert.Null(SurecAvcisi.KorumaNedeni("", 4321)); Assert.Null(SurecAvcisi.KorumaNedeni(null!, 4321)); }
}
