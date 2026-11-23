// DPI taşma testleri (Faz 3 Adım 6b): PerMonitorV2 ve yüksek ölçekte (%125/%150) yazı denetimlerinin içine sığıyor mu? Ölçüm tabanlı: gerçek monitör ölçeği yerine
// form ve yazı tipleri aynı oranda büyütülür, her etiket/düğmenin metni TextRenderer ile ölçülür. Gerçek çok monitörlü ortam yine elle denenmelidir (bkz. CLAUDE.md).
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class DpiTasmaTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Proje_PerMonitorV2_VeTemaliFormDpiOlceklemeliDir()
    {
        string? kok = Kok(); if (kok == null) return;
        Assert.Contains("<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>", File.ReadAllText(Path.Combine(kok, "LabDenetim", "LabDenetim.csproj")), StringComparison.Ordinal);
        string taban = File.ReadAllText(Path.Combine(kok, "LabDenetim", "TemaliForm.cs"));
        Assert.Contains("AutoScaleMode = AutoScaleMode.Dpi", taban, StringComparison.Ordinal);
    }

    // Parametresiz kurucusu olan, güvenle kurulabilen pencereler.
    private static readonly string[] Pencereler = { "HakkindaFormu", "ApiAyarFormu", "KullaniciFormu", "OlayFormu", "TurFormu", "DestekFormu", "KalkanFormu" };

    private static List<string> Tasanlar(Form f, float olcek, ref int sayac)
    {
        var sonuc = new List<string>();
        // Önce her denetimin MEVCUT (devralınmış dahil) yazı tipini kaydet, sonra hepsini aynı oranda büyüt (devralanlar ikinci kez büyütülmesin).
        var ozgun = Tum(f).Append(f).Select(c => (c, c.Font.FontFamily.Name, c.Font.Size, c.Font.Style)).ToList();
        f.Scale(new SizeF(olcek, olcek)); foreach (var (c, aile, boy, stil) in ozgun) c.Font = FontProvider.Gecici(aile, boy * olcek, stil); f.PerformLayout();
        foreach (var c in Tum(f))
        {
            if ((c is not Label && c is not Button) || string.IsNullOrWhiteSpace(c.Text) || c.Width < 20) continue;
            if (c is Label { AutoSize: true }) continue;   // kendi boyutunu metne göre alır
            if (c.Dock == DockStyle.Fill) continue;   // kalan alanı doldurur (çoğu kez gizli yer tutucu; boyutu yerleşimden gelir)
            sayac++;
            var oda = new Size(Math.Max(1, c.ClientSize.Width - c.Padding.Horizontal), int.MaxValue);
            var bayrak = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            var olcu = TextRenderer.MeasureText(c.Text, c.Font, oda, bayrak);
            int sigan = c.ClientSize.Height - c.Padding.Vertical - (c is Button ? 6 : 0);
            if (olcu.Height > sigan + 2) sonuc.Add($"{f.GetType().Name}/{c.Name}'{Kisalt(c.Text)}' {c.GetType().Name} metin {olcu.Height}px > alan {sigan}px (x{olcek})");
        }
        return sonuc;
    }

    private static string Kisalt(string s) => Regex.Replace(s, @"\s+", " ").Trim() is { Length: > 28 } t ? t[..28] + "…" : Regex.Replace(s, @"\s+", " ").Trim();
    private static IEnumerable<Control> Tum(Control k) { foreach (Control c in k.Controls) { yield return c; foreach (var a in Tum(c)) yield return a; } }

    // Sabit ölçüm: %100'de taşan yok; %125/%150'de %100'e göre YENİ taşma çıkmaz (yeni taşma = ölçekleme kaynaklı yazı kırpılması).
    [Fact]
    public void Pencereler_Yuzde125Ve150deYaziKirpilmaz()
    {
        var sorunlar = new List<string>(); Exception? hata = null; int olculenForm = 0, olculenDenetim = 0;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok; BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "dpi_" + Guid.NewGuid().ToString("N"));
            try
            {
                var asm = typeof(Form1).Assembly;
                foreach (var ad in Pencereler)
                {
                    var tur = asm.GetType("LabDenetim." + ad); var ctor = tur?.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null); if (ctor == null) continue;
                    var taban = new HashSet<string>(); olculenForm++;
                    foreach (float o in new[] { 1.0f, 1.25f, 1.5f })
                    {
                        var nesne = ctor.Invoke(null); using var f = nesne is SayfaDenetimi sayfa ? SayfaTestKabi.Goster(sayfa) : (Form)nesne; if (nesne is not SayfaDenetimi) { f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); _ = f.Handle; }
                        foreach (var s in Tasanlar(f, o, ref olculenDenetim)) { string anahtar = Regex.Replace(s, @" \(x[\d.,]+\)$", ""); if (o == 1.0f) { taban.Add(anahtar); } else if (!taban.Contains(anahtar)) sorunlar.Add(s); }
                    }
                }
            }
            catch (Exception ex) { hata = ex; }
            finally { try { Directory.Delete(BulutKlasor.Kok, true); } catch (Exception hk) { HataKayit.Sessiz(hk, "DpiTasmaTestleri.temizlik"); } BulutKlasor.Kok = eskiKok; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(120)), "zaman aşımı"); if (hata != null) throw new Exception("Pencere kurulamadı: " + hata, hata);
        Assert.True(olculenForm >= 4 && olculenDenetim >= 20, $"Ölçüm kapsamı yetersiz: {olculenForm} pencere, {olculenDenetim} denetim"); Assert.True(sorunlar.Count == 0, "Ölçeklemede yeni yazı taşması:\n" + string.Join("\n", sorunlar.Take(15)));
    }
}
