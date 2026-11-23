// Faz 3.5'ten kalan iki görsel pürüzün testleri (Faz 4): kart açıklamaları kelime ortasından bölünmez; tema değişince başlık çubuğu çerçevesi anında yenilenir (SWP_FRAMECHANGED).
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class GorselPurusTestleri
{
    [Theory]
    [InlineData("Renkli, ayrıntılı tarama raporu. Her bulgu için \"bu ne demek, nasıl çözerim\" açıklaması içerir.", 160)]
    [InlineData("Teknik olmayan yöneticiler için tek sayfalık güvenlik karnesi (HTML) ya da ayrıntılı PDF.", 210)]
    [InlineData("Bulgular, olay günlüğü ve özetleri mühürlü tek pakette toplar; olay sonrası inceleme için.", 120)]
    public void KelimeSatirlari_KelimeyiOrtadanBolmez_HerSatirSigar(string metin, int genislik)
    {
        Func<string, int> olc = s => s.Length * 7;   // sabit genişlikli ölçü: sınır davranışını deterministik sınar
        var satirlar = Cizim.KelimeSatirlari(olc, metin, genislik);
        Assert.Equal(metin.Split(' ', StringSplitOptions.RemoveEmptyEntries), satirlar.SelectMany(s => s.Split(' ')).ToArray());   // kelimeler aynen ve aynı sırada: hiçbiri bölünmedi
        Assert.All(satirlar, s => Assert.True(olc(s) <= genislik || !s.Contains(' '), "satır sığmıyor: " + s));
    }

    [Fact]
    public void KelimeSatirlari_EnFazlaSatirdaUcNoktayla_Biter_UzunKelimeKendiSatirindaKalir()
    {
        Func<string, int> olc = s => s.Length * 7;
        var l = Cizim.KelimeSatirlari(olc, "bir iki üç dört beş altı yedi sekiz dokuz on", 70, 2); Assert.Equal(2, l.Count); Assert.EndsWith("…", l[1], StringComparison.Ordinal); Assert.True(olc(l[1]) <= 70);
        var u = Cizim.KelimeSatirlari(olc, "kısa Çokuzunbirkelimedir kısa", 70); Assert.Contains("Çokuzunbirkelimedir", u);   // sığmayan tek kelime bölünmeden kendi satırında
    }

    [Fact]
    public void KelimeSatirlari_GercekCaptionYaziTipiyle_KartGenisliginde_KelimeBolunmez()
    {
        Exception? hata = null;
        var t = new Thread(() =>
        {
            try
            {
                using var bmp = new Bitmap(10, 10); using var g = Graphics.FromImage(bmp); var f = FontProvider.Al(FontRol.Caption);
                Func<string, int> olc = s => TextRenderer.MeasureText(g, s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                string metin = "Renkli, ayrıntılı tarama raporu. Her bulgu için \"bu ne demek, nasıl çözerim\" açıklaması içerir.";
                foreach (int w in new[] { 150, 200, 230, 300 })
                {
                    var l = Cizim.KelimeSatirlari(olc, metin, w); Assert.Equal(metin.Split(' '), l.SelectMany(s => s.Split(' ')).ToArray()); Assert.All(l, s => Assert.True(olc(s) <= w || !s.Contains(' ')));
                }
            }
            catch (Exception ex) { hata = ex; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(30))); if (hata != null) throw new Exception(hata.Message, hata);
    }

    [Fact]
    public void TemaDegisince_BaslikCubuguCercevesiYenilenir_SetWindowPosBasarili()
    {
        Exception? hata = null; bool yenilendi = false;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok; BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "cerceve_" + Guid.NewGuid().ToString("N"));
            try
            {
                if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir();
                using var f = new HakkindaFormu(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.Show(); Application.DoEvents();
                var konum = f.Size; yenilendi = TemaYonetici.CerceveYenile(f.Handle);
                Assert.Equal(konum, f.Size);                       // çerçeve yenileme pencereyi boyutlandırmaz (açılış efekti konumu birkaç piksel oynatabilir)
                TemaYonetici.Degistir(); Application.DoEvents(); Assert.Equal(konum, f.Size); TemaYonetici.Degistir();   // tema geçişi (TemaUygula → KoyuBaslikAyarla → CerceveYenile) hatasız
            }
            catch (Exception ex) { hata = ex; }
            finally { try { Directory.Delete(BulutKlasor.Kok, true); } catch (IOException hk) { HataKayit.Sessiz(hk, "GorselPurusTestleri.temizlik"); } BulutKlasor.Kok = eskiKok; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(60))); if (hata != null) throw new Exception(hata.Message, hata);
        Assert.True(yenilendi); Assert.False(TemaYonetici.CerceveYenile(IntPtr.Zero));
    }
}
