// Sayfa taşma testi (Faz 3.5): standart laptop genişliklerinde (1280/1366 px) kabuğun gömülü sayfalarında yatay kaydırma çıkmamalı, hiçbir düğme/etiket/kutu
// üst kapsayıcısının sağ sınırından taşmamalı (kırpılma). Dikey kaydırma serbesttir (kısa pencerede sayfa kendi içinde kayar).
using System.Reflection;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class SayfaTasmaTestleri
{
    private static IEnumerable<Control> Tum(Control k) { foreach (Control c in k.Controls) { yield return c; foreach (var a in Tum(c)) yield return a; } }

    private static string Yol(Control c, Control kok) { var l = new List<string>(); for (var x = c; x != null && x != kok; x = x.Parent) l.Add(string.IsNullOrEmpty(x.Name) ? x.GetType().Name : x.Name); l.Reverse(); return string.Join("/", l); }

    private static List<string> Tara(int w, int h, string? cikti)
    {
        var sorun = new List<string>(); Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; string gecici = Path.Combine(Path.GetTempPath(), "tasma_" + Guid.NewGuid().ToString("N"));
            Oturum.Aktif = new Kullanici { Ad = "yonetici", Rol = Rol.Yonetici };
            try
            {
                BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); ArayuzModu.Ayarla(ModTuru.Uzman); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.MinimumSize = new Size(900, 600); f.ClientSize = new Size(w, h); f.Show(); Application.DoEvents(); f.ClientSize = new Size(w, h); Application.DoEvents();   // Show sırasında ekrana sığdırma/DPI boyutu değiştirebilir: istenen boyutu yeniden uygula
                var k = (AppShell)typeof(Form1).GetField("_kabuk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
                if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir();
                void Tara((string, string?)[] sayfalar, string mod)
                {
                foreach (var (sayfa, sekme) in sayfalar)
                {
                    try { k.GitAsync(sayfa, sekme).GetAwaiter().GetResult(); } catch (Exception ex) { sorun.Add($"{sayfa}: açılamadı ({ex.GetType().Name})"); continue; }
                    for (int i = 0; i < 3; i++) { Application.DoEvents(); f.PerformLayout(); }
                    var sayfaKontrolu = k.Sayfa(sayfa) as Control; if (sayfaKontrolu == null) { sorun.Add($"{sayfa}: sayfa denetimi yok"); continue; }
                    string etiket = mod + ":" + sayfa + (sekme != null ? "/" + sekme : "") + $"@{f.ClientSize.Width}";
                    if (cikti != null) { Directory.CreateDirectory(cikti); using var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(bmp, new Rectangle(Point.Empty, f.ClientSize)); bmp.Save(Path.Combine(cikti, $"tasma_{mod}_{sayfa}_{sekme}_{w}.png"), System.Drawing.Imaging.ImageFormat.Png); }
                    if (sayfaKontrolu is ScrollableControl { HorizontalScroll.Visible: true }) sorun.Add($"{etiket}: sayfanın kendisinde yatay kaydırma çubuğu (AutoScrollMinSize={((ScrollableControl)sayfaKontrolu).AutoScrollMinSize})");
                    foreach (var c in Tum(sayfaKontrolu))
                    {
                        if (!c.Visible || c.Parent == null) continue;
                        if (c is ScrollableControl { HorizontalScroll.Visible: true } sc && c is not DataGridView) sorun.Add($"{etiket}: yatay kaydırma çubuğu: {Yol(c, sayfaKontrolu)}");
                        if (c is Form || c is Splitter) continue;
                        var p = c.Parent; if (p is ScrollableControl { AutoScroll: true }) continue;   // kendi içinde kayan kapsayıcı (dikey) — yatay denetim yukarıdaki kuraldadır
                        if (c is not (Button or Label or CheckBox or RadioButton or ComboBox or TextBox)) continue;
                        if (c.Right > p.ClientSize.Width + 1) sorun.Add($"{etiket}: sağdan taşıyor: {Yol(c, sayfaKontrolu)} '{(c.Text.Length > 30 ? c.Text[..30] : c.Text)}' sağ={c.Right} > {p.ClientSize.Width}");
                    }
                }
                }
                // Kolay mod: durum panosu (kart panosu); sonra Uzman mod sayfaları.
                ArayuzModu.Ayarla(ModTuru.Kolay); Application.DoEvents();
                Tara(new (string, string?)[] { ("durum", null) }, "kolay");
                ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents();
                Tara(new (string, string?)[] { ("tarama", "yeni"), ("durum", "radar"), ("savunma", "kalkan"), ("ag", "cihaz"), ("rapor", null), ("arac", null) }, "uzman");
                f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { Oturum.Aktif = null; ArayuzModu.Sifirla(); BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; try { Directory.Delete(gecici, true); } catch (IOException hkt) { HataKayit.Sessiz(hkt, "SayfaTasmaTestleri.temizlik"); } }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(120))) throw new TimeoutException("Form1 kurulamadı");
        if (hata != null) throw new Exception("Form1 harness hatası: " + hata, hata);
        return sorun;
    }

    [Theory]
    [InlineData(1280, 768)] [InlineData(1366, 768)]
    public void GomuluSayfalar_StandartLaptopGenisliginde_TasmazVeYatayKaydirmaCikmaz(int w, int h)
    {
        var sorun = Tara(w, h, Environment.GetEnvironmentVariable("BULUT_EKRAN_KLASORU"));
        Assert.True(sorun.Count == 0, "Taşma/kırpılma:\n" + string.Join("\n", sorun.Take(25)));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void TaramaSayfasi_SabitGenislikIstemez_YalnizDikeyKayar()
    {
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null; i++, kok = Path.GetDirectoryName(kok)) if (File.Exists(Path.Combine(kok, "Directory.Build.props"))) break;
        if (kok == null) return;
        string kod = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kabuk", "KabukKompozisyonu.cs"));
        Assert.DoesNotContain("enAzIcerik: new Size(1260", kod, StringComparison.Ordinal);   // sabit genişlik AutoScrollMinSize'ı kaldırıldı
    }
}
