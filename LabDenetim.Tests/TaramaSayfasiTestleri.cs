// D4 (1. dilim): "Yeni Tarama + Asistan" ekranı Form1.Designer.cs'ten ayrı bir UserControl'dür (Kontroller\TaramaSayfasi.cs). Form1 olmadan kurulabilir, denetimlerini sunar, yerleşimi esnektir.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class TaramaSayfasiTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact]
    public void TaramaSayfasi_Form1OlmadanKurulur_DenetimleriHazir_EsnekYerlesimli()
    {
        Exception? hata = null;
        var t = new Thread(() =>
        {
            try
            {
                using var s = new TaramaSayfasi(); s.Size = new Size(1000, 700); _ = s.Handle; s.PerformLayout();
                Assert.Equal(DockStyle.Fill, s.Dock);
                Assert.NotNull(s.txtHedef); Assert.NotNull(s.btnBaslat); Assert.NotNull(s.btnIptal); Assert.NotNull(s.dgvBulgular); Assert.NotNull(s.rtbDetay); Assert.NotNull(s.splIcerik); Assert.NotNull(s.flwHedef); Assert.NotNull(s.flwSerit);
                Assert.Equal(6, s.dgvBulgular.Columns.Count); Assert.Equal("127.0.0.1", s.txtHedef.Text); Assert.False(s.btnIptal.Enabled);
                Assert.Same(s.ctxMenu, s.dgvBulgular.ContextMenuStrip);
                // Yerleşim ızgarası tek parça ve sayfa kendi başına yatay kaydırma istemez.
                Assert.Single(s.Controls.OfType<TableLayoutPanel>()); Assert.False(s.HorizontalScroll.Visible);
                foreach (var ad in new[] { "btnBaslat", "btnIptal", "btnKanit", "btnAltin", "btnHtml", "btnPdf" }) Assert.NotNull(s.GetType().GetField(ad, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
            }
            catch (Exception ex) { hata = ex; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(60))); if (hata != null) throw new Exception("TaramaSayfasi sınaması: " + hata.Message, hata);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Form1Designer_TaramaEkraniniIcermez_KucukKalir()
    {
        string? kok = Kok(); if (kok == null) return; string d = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Form1.Designer.cs"));
        Assert.True(File.ReadAllLines(Path.Combine(kok, "LabDenetim", "Form1.Designer.cs")).Length <= 320, "Form1.Designer.cs şişmemeli");
        foreach (var yasak in new[] { "tblAna = ", "flwHedef = ", "dgvBulgular = new", "splIcerik = new", "btnBaslat = new", "txtHedef = new", "rtbDetay = new" }) Assert.DoesNotContain(yasak, d, StringComparison.Ordinal);
        string t = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kontroller", "TaramaSayfasi.cs"));
        Assert.Contains("class TaramaSayfasi : UserControl", t, StringComparison.Ordinal); Assert.Contains("flwHedef = new FlowLayoutPanel", t, StringComparison.Ordinal);
        Assert.DoesNotContain("Form1", string.Join("\n", File.ReadLines(Path.Combine(kok, "LabDenetim", "Kontroller", "TaramaSayfasi.cs")).Where(s => !s.TrimStart().StartsWith("//"))), StringComparison.Ordinal);   // denetim Form1'e bağımlı değil
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Dilim2_TaramaMantigiTaramaSayfasindadir_Form1KoprusuYok_Form1KucukKalir()
    {
        string? kok = Kok(); if (kok == null) return; string lab = Path.Combine(kok, "LabDenetim");
        // Mantık dosyaları TaramaSayfasi parçaları; Form1'de yalnızca kabuk, nöbetçi, kolay mod, kısayol, merkez, küre, menü/tur kaldı.
        foreach (var ad in new[] { "Asistan", "YapayZeka", "Trafik", "Kartlar", "Gorunum", "Filtre", "Profil", "Tablo", "Gunluk", "Gecmis", "DisAgAkis", "Tema", "Calistir" }) Assert.True(File.Exists(Path.Combine(lab, "Kontroller", $"TaramaSayfasi.{ad}.cs")), ad);
        string d = File.ReadAllText(Path.Combine(lab, "Form1.Designer.cs")); Assert.DoesNotContain("=> _tarama.", d, StringComparison.Ordinal);   // eski alan-adı yönlendirme köprüsü kalktı
        Assert.True(File.ReadAllLines(Path.Combine(lab, "Form1.cs")).Length <= 190, "Form1.cs küçük kalmalı");
        string ana = File.ReadAllText(Path.Combine(lab, "Kontroller", "TaramaSayfasi.cs")); Assert.Contains("UserControl, ITaramaBaglami", ana, StringComparison.Ordinal);
        Assert.DoesNotContain("class Form1 : TemaliForm, ITaramaBaglami", File.ReadAllText(Path.Combine(lab, "Form1.cs")), StringComparison.Ordinal);   // arayüzü artık tarama ekranı uygular
    }

    // Pencere olmadan sahte sahip: tarama ekranı Form1'e değil yalnızca ITaramaSahibi arayüzüne bağlıdır (D4 dilim 3).
    private sealed class SahteSahip : ITaramaSahibi
    {
        public readonly List<string> Durumlar = new(); public Color? Zemin, Yazi; public int Balon; public bool Hazir = true;
        public IWin32Window Pencere => null!; public bool PencereHazir => Hazir; public void UiIsle(Action is_) => is_(); public void TepsiBalonu(int ms, string baslik, string metin, bool uyari) => Balon++;
        public void KapanirkenCalistir(Action is_) { } public void DurumYaz(string metin) => Durumlar.Add(metin); public void DurumZemini(Color renk) => Zemin = renk; public void DurumYazisiRengi(Color renk) => Yazi = renk;
        public bool KabukHazir => false; public Task<bool> SayfayaGitAsync(string sayfa, string? sekme) => Task.FromResult(false); public SavunmaDurumServisi Savunma { get; } = new();
        public void KolayGostergeleriYenile() { } public void KureYansit() { } public void UzmanKureKur() { } public void UzmanKureGorunum(Gorunum g) { } public bool HaritaKureAcik => false;
    }

    [Fact]
    public void TaramaSayfasi_SahteSahiplePencereOlmadanCalisir_DurumVePencereCagrilariSahibeGider()
    {
        Exception? hata = null;
        var t = new Thread(() =>
        {
            try
            {
                var sahip = new SahteSahip(); using var s = new TaramaSayfasi { Sahibi = sahip };
                var b = (ITaramaBaglami)s;
                b.Durum("Merhaba", false); Assert.Contains("Merhaba", sahip.Durumlar);
                Assert.True(b.PencereHazir); sahip.Hazir = false; Assert.False(b.PencereHazir);
                b.TepsiBalonu(1, "a", "b", true); Assert.Equal(1, sahip.Balon);
                int calisti = 0; b.UiIsle(() => calisti++); Assert.Equal(1, calisti);
            }
            catch (Exception ex) { hata = ex; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(60))); if (hata != null) throw new Exception("Sahte sahip sınaması: " + hata.Message, hata);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void TaramaSayfasi_Form1ClassinaBaglanmaz_AnaYok_YardimEylemleriAyri()
    {
        string? kok = Kok(); if (kok == null) return; string lab = Path.Combine(kok, "LabDenetim");
        foreach (var f in Directory.EnumerateFiles(Path.Combine(lab, "Kontroller"), "TaramaSayfasi*.cs"))
        {
            string kod = string.Join("\n", File.ReadLines(f).Where(s => !s.TrimStart().StartsWith("//")));
            Assert.DoesNotContain("Form1", kod.Replace("\"Form1.", ""), StringComparison.Ordinal); Assert.DoesNotContain("Ana.", kod.Replace("tblAna.", ""), StringComparison.Ordinal);
        }
        Assert.False(File.Exists(Path.Combine(lab, "Menu.cs"))); Assert.False(File.Exists(Path.Combine(lab, "Bilgi.cs"))); Assert.True(File.Exists(Path.Combine(lab, "Kabuk", "YardimEylemleri.cs")));
    }
}
