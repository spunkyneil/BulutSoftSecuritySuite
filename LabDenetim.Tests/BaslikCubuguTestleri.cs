// Win32 başlık çubuğu (DWM) tema eşzamanı testi (Faz 3.5): TemaliForm tutamaç oluşunca ve tema değişince başlık çubuğunu koyu/açık yapar.
using System.Runtime.InteropServices;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class BaslikCubuguTestleri
{
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int ozellik, out int deger, int boyut);

    [Fact]
    public void TemaliForm_BaslikCubugu_TemaylaSenkronGecer()
    {
        Exception? hata = null; int ilk = -1, sonra = -1;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok; BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "dwm_" + Guid.NewGuid().ToString("N"));
            try
            {
                if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir();   // koyu başla
                using var f = new HakkindaFormu(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.Show(); Application.DoEvents();
                if (DwmGetWindowAttribute(f.Handle, 20, out ilk, 4) != 0) { ilk = -2; return; }   // eski Windows: özellik yok, sınama anlamsız
                TemaYonetici.Degistir(); Application.DoEvents();   // açık tema
                DwmGetWindowAttribute(f.Handle, 20, out sonra, 4);
                TemaYonetici.Degistir();                           // koyuya geri dön (kalıcı tercih bozulmasın)
            }
            catch (Exception ex) { hata = ex; }
            finally { try { Directory.Delete(BulutKlasor.Kok, true); } catch (Exception hk) { HataKayit.Sessiz(hk, "BaslikCubuguTestleri.temizlik"); } BulutKlasor.Kok = eskiKok; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(60)), "zaman aşımı"); if (hata != null) throw new Exception("Pencere hatası: " + hata, hata);
        if (ilk == -2) return;
        Assert.Equal(1, ilk); Assert.Equal(0, sonra);
    }
}
