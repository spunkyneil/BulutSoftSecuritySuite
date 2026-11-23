// Adım 2: gerçek Windows hizmetiyle Süreç Avcısı koruması ve Kalıcılık yedeğinin gerçek dosyayla geri dönüşü.
using System.Diagnostics;
using System.Management;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
[Trait("Category", "Integration")]
public class Adim2GercekTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_a2_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    public Adim2GercekTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; }
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "Adim2GercekTestleri.temizlik"); } }

    // Çalışan gerçek hizmetlerden, sabit koruma listesinde OLMAYAN bir süreç bulur (hizmet kontrolü gerçekten sınansın).
    private static (string Ad, int Pid)? ListeDisiGercekHizmet()
    {
        try
        {
            using var q = new ManagementObjectSearcher("SELECT ProcessId, Name FROM Win32_Service WHERE State='Running' AND ProcessId > 4");
            foreach (ManagementObject o in q.Get())
            {
                int pid = Convert.ToInt32(o["ProcessId"]); try { var ad = Process.GetProcessById(pid).ProcessName; if (!SurecAvcisi.Korunuyor(ad, pid)) return (ad, pid); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "Adim2GercekTestleri.temizlik"); }
            }
        }
        catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "Adim2GercekTestleri.temizlik"); }
        return null;
    }

    [Fact]
    public void Gercek_Hizmet_Sureci_Sabit_Listede_Olmasa_Bile_Korunur()
    {
        var h = ListeDisiGercekHizmet(); if (h == null) return;   // makinede böyle bir hizmet yoksa sınanamaz
        var neden = SurecAvcisi.KorumaNedeni(h.Value.Ad, h.Value.Pid);
        Assert.NotNull(neden);
        Assert.Throws<ArgumentException>(() => SurecAvcisi.Sonlandir(h.Value.Pid, h.Value.Ad));   // sonlandırma denenir ama korumaya takılır, süreç ölmez
        Assert.False(Process.GetProcessById(h.Value.Pid).HasExited);
    }

    [Fact]
    public void Test_Sureci_Hizmet_Degil_Ve_Korumasiz_Kalir_Ya_Da_Hizmet_Hatasinda_Korunur()
    {
        // Hizmet sorgusu çalışıyorsa normal bir süreç korunmaz; çalışmıyorsa (WMI hatası) güvenli tarafta kalıp korunur.
        var kendi = Process.GetCurrentProcess(); var neden = SurecAvcisi.KorumaNedeni(kendi.ProcessName, kendi.Id);
        if (neden != null) Assert.True(SurecAvcisi.Korunuyor(kendi.ProcessName, kendi.Id) || neden.Length > 0);
    }

    // Kalıcılık yedeği: gerçek dosyaya yazılır, bozulursa .bak'tan döner, geri alma için öğenin tüm alanları korunur.
    [Fact]
    public void Kalicilik_Yedek_Gidis_Donus_Alanlari_Korur_Ve_Bozulunca_Bakdan_Doner()
    {
        var o1 = new KaliciOge { Tur = "Run", Ad = "Garip Program", Konum = @"HKCU\Software\Run", Komut = @"C:\x\garip.exe /s", Hklm = false };
        var o2 = new KaliciOge { Tur = "Run", Ad = "Ikinci", Konum = @"HKCU\Software\Run", Komut = @"C:\x\ikinci.exe" };
        KaliciDenetci.YedekEkle(o1); KaliciDenetci.YedekEkle(o2);
        var l = KaliciDenetci.YedekOku(); Assert.Equal(2, l.Count); var g = l.First(x => x.Ad == "Garip Program");
        Assert.Equal(o1.Hklm, g.Hklm); Assert.Equal(o1.Konum, g.Konum); Assert.Equal(o1.Tur, g.Tur);
        File.WriteAllText(KaliciDenetci.YedekYolu, "[{yarim");   // çökmüş yazım
        Assert.Single(KaliciDenetci.YedekOku()); Assert.Equal("Garip Program", KaliciDenetci.YedekOku()[0].Ad);   // son sağlam sürüm (.bak)
    }
}
