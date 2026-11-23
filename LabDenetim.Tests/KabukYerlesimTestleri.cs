// Kabuk yerleşim testi (eski sol menü sığma testinin yerine): ana pencere hangi boyutta olursa olsun kabuğun ana parçaları (sol menü, üst çubuk, sayfa alanı, durum çubuğu)
// pencereye sığmalı, üst çubuk 52 px (DPI'a göre), sol menü 236 px olmalı; Uzman'da on sayfanın tamamı kaydırmadan görünmeli ve sayfa alanı pencerenin kalanını doldurmalı.
// Form ekran dışı konumda gösterilir (sayfaların açılması için); pencere hafızası/ekrana sığdırma gerçek ekranı aşan boyutları kırpabilir, bu yüzden sınamalar
// istenen boyuta değil pencerenin GERÇEK istemci boyutuna göre yapılır. Dosya ve kayıt defteri yolları geçici klasöre yönlendirilir.
using System.Reflection;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class KabukYerlesimTestleri
{
    static (int KenarGen, int UstYuk, int KenarYuk, int Gerekli, int AlanGen, int AlanYuk, int PencereGen, int PencereYuk, int Oge) Olc(int w, int h)
    {
        (int, int, int, int, int, int, int, int, int) sonuc = default; Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; string gecici = Path.Combine(Path.GetTempPath(), "yer_" + Guid.NewGuid().ToString("N"));
            try
            {
                BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); ArayuzModu.Ayarla(ModTuru.Uzman); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ClientSize = new Size(w, h); f.Show(); Application.DoEvents(); f.PerformLayout(); Application.DoEvents();
                var k = (AppShell)typeof(Form1).GetField("_kabuk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
                var alan = k.Controls.OfType<TableLayoutPanel>().Single();
                sonuc = (k.Kenar.Width, k.Ust.Height, k.Kenar.Height, k.Kenar.GerekenYukseklik, alan.Width, alan.Height, f.ClientSize.Width, f.ClientSize.Height, k.Kenar.OgeSayisi);
                f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { ArayuzModu.Sifirla(); BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "KabukYerlesimTestleri.temizlik"); } }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("Form1 kurulamadı");
        if (hata != null) throw new Exception("Form1 harness hatası: " + hata, hata);
        return sonuc;
    }

    // Gerçek Form1'i kabuğuyla çizer (Kolay + Uzman sayfaları); BULUT_EKRAN_KLASORU verilirse PNG olarak kaydeder (gözle inceleme için). Sol menü/üst çubuk token renginde olmalı.
    [Fact] public void Form1_GercekCizim_KabukSayfalari_TokenRenkleriylePiksellenir()
    {
        string? cikti = Environment.GetEnvironmentVariable("BULUT_EKRAN_KLASORU"); Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; string gecici = Path.Combine(Path.GetTempPath(), "cizim_" + Guid.NewGuid().ToString("N"));
            Oturum.Aktif = new Kullanici { Ad = "yonetici", Rol = Rol.Yonetici };
            try
            {
                BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ClientSize = new Size(1500, 820); f.Show(); Application.DoEvents();
                var k = (AppShell)typeof(Form1).GetField("_kabuk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
                void Ciz(string ad, bool koyu)
                {
                    Application.DoEvents(); f.Refresh(); using var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.ClientSize));
                    if (cikti != null) { Directory.CreateDirectory(cikti); bmp.Save(Path.Combine(cikti, ad + ".png"), System.Drawing.Imaging.ImageFormat.Png); }
                    Assert.Equal(DesignTokens.Renk(TokenRol.SurfaceSidebar, koyu).ToArgb(), bmp.GetPixel(24, bmp.Height - 120).ToArgb());   // sol menü zemini (pencere çerçevesinin içinde, öğelerin altında)
                }
                if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir();   // her sınama koyu temada başlasın (geçici klasöre kaydedilir)
                Ciz("form1_kolay_durum", true);
                ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents();
                var sayfalar = new[] { ("durum", "radar", "form1_uzman_durum_radar"), ("tarama", "yeni", "form1_uzman_tarama"), ("savunma", "kalkan", "form1_uzman_savunma"), ("rapor", (string?)null, "form1_uzman_rapor"), ("ag", "cihaz", "form1_uzman_ag") };
                foreach (var (sayfa, sekme, ad) in sayfalar) { k.GitAsync(sayfa, sekme).GetAwaiter().GetResult(); Ciz(ad, true); }
                // Açık tema: kabuk ve gömülü eski sayfalar tema olayıyla yeniden boyanır.
                TemaYonetici.Degistir(); Application.DoEvents();
                foreach (var (sayfa, sekme, ad) in sayfalar) { k.GitAsync(sayfa, sekme).GetAwaiter().GetResult(); Ciz(ad + "_acik", false); }
                TemaYonetici.Degistir();
                f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { Oturum.Aktif = null; ArayuzModu.Sifirla(); BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; try { Directory.Delete(gecici, true); } catch (IOException hkt2) { HataKayit.Sessiz(hkt2, "KabukYerlesimTestleri.temizlik"); } }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(90))) throw new TimeoutException("Form1 çizilemedi");
        if (hata != null) throw new Exception("Form1 çizim hatası: " + hata, hata);
    }

    [Theory]
    [InlineData(1500, 820)] [InlineData(1500, 950)] [InlineData(1920, 1030)] [InlineData(2560, 1400)]
    public void Kabuk_HerBoyutta_SolMenuUstCubukVeSayfaAlaniSigar(int w, int h)
    {
        var (kenarGen, ustYuk, kenarYuk, gerekli, alanGen, alanYuk, pencereGen, pencereYuk, oge) = Olc(w, h);
        Assert.Equal(10, oge);
        Assert.Equal(236, kenarGen);                       // sol menü sabit 236 px (96 DPI)
        Assert.Equal(52, ustYuk);                          // üst çubuk 52 px (96 DPI)
        if (pencereYuk >= 820) Assert.True(kenarYuk >= gerekli, $"Sol menü kaydırmadan sığmalı: yükseklik {kenarYuk}, gereken {gerekli}");   // daha kısa pencerede menü kaydırılır (fare tekerleği)
        // Kabuk pencerenin tamamını doldurur: sol menü + sayfa alanı = pencere genişliği; sayfa alanı + 28 px durum çubuğu = pencere yüksekliği (eski sayfalar gerekirse kendi içinde kaydırılır).
        Assert.Equal(pencereGen - kenarGen, alanGen);
        Assert.Equal(pencereYuk - 28, alanYuk);
    }
}
