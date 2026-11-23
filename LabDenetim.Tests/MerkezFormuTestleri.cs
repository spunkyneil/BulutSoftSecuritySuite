// Merkez penceresi duman testi: Kolay'da yalnızca 3 sekme, Uzman'da 5 sekme görünür ve form hatasız kurulur (ağa çıkılmaz, düğmeye basılmaz).
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Veri klasörünü paylaşan testler sırayla çalışsın.
[Collection("Veri")]
public class MerkezFormuTestleri
{
    // Pencereyi geçici klasörde kurar ve görünen sekme düğmesi sayısını döndürür.
    static int GorunenSekme(ModTuru mod, int sekme)
    {
        int sonuc = 0; Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "mf_" + Guid.NewGuid().ToString("N")); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
            try
            {
                ArayuzModu.Sifirla(); ArayuzModu.Ayarla(mod);
                using var f = new MerkezFormu(sekme, _ => { }, () => { }, () => { }); using var host = SayfaTestKabi.Goster(f); Application.DoEvents();
                sonuc = f.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<Button>()).Count(b => b.Visible); f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { ArayuzModu.Sifirla(); try { Directory.Delete(BulutKlasor.Kok, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "MerkezFormuTestleri.temizlik"); } BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException(); if (hata != null) throw new Exception("Merkez hatası: " + hata, hata); return sonuc;
    }

    [Fact] public void Kolay_Modda_Uc_Sekme() => Assert.Equal(3, GorunenSekme(ModTuru.Kolay, 0));
    [Fact] public void Kolay_Modda_Uzman_Sekmesi_Istenirse_Kali_Acilir() => Assert.Equal(3, GorunenSekme(ModTuru.Kolay, 4));
    [Fact] public void Uzman_Modda_Alti_Sekme() => Assert.Equal(6, GorunenSekme(ModTuru.Uzman, 3));
}
