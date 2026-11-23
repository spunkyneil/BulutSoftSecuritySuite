// Olay tabanlı tema mimarisi testleri (CLAUDE.md kural 4): zamanlayıcıyla pencere taraması YOK; pencereler TemaliForm ile kaydolur,
// tutamaç oluşunca bir kez boyanır, tema değişince TEK SEFERDE bilgilendirilir, kapanınca kayıttan çıkar.
using System.Reflection;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Sınama penceresi ve kontrolü.
internal sealed class TemaTestFormu : TemaliForm { }
internal sealed class TemaSayacKontrolu : Control, IThemeAware { public int Sayi; public void TemaUygula() => Sayi++; }

// Tema kaydı ortak durumu paylaşır (BulutKlasor, TemaYonetici); sırayla çalışsın.
[Collection("Veri")]
public class TemaOlayTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_tema_" + Guid.NewGuid().ToString("N")); private readonly string _eskiKok;

    public TemaOlayTestleri()
    {
        _eskiKok = BulutKlasor.Kok; Directory.CreateDirectory(_t); BulutKlasor.Kok = _t;
        if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir();   // her sınama koyu temada başlasın
    }

    public void Dispose()
    {
        if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir();
        BulutKlasor.Kok = _eskiKok; try { Directory.Delete(_t, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "TemaOlayTestleri.temizlik"); }
    }

    [Fact]
    public void Her_Pencere_Sinifi_TemaliFormdan_Turer()
    {
        var kotu = typeof(TemaliForm).Assembly.GetTypes().Where(t => typeof(Form).IsAssignableFrom(t) && t != typeof(Form) && t != typeof(TemaliForm) && !typeof(TemaliForm).IsAssignableFrom(t)).Select(t => t.FullName).ToList();
        Assert.True(kotu.Count == 0, "TemaliForm'dan türemeyen pencereler (temaya kaydolmaz): " + string.Join(", ", kotu));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void TemaYonetici_ZamanlayiciVeAcikPencereTaramasiIcermez()
    {
        const BindingFlags hepsi = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var zamanlayicilar = typeof(TemaYonetici).GetFields(hepsi).Where(f => f.FieldType.Name.Contains("Timer", StringComparison.Ordinal)).Select(f => f.Name).ToList();
        Assert.Empty(zamanlayicilar);
        Assert.DoesNotContain(typeof(TemaYonetici).GetNestedTypes(hepsi), n => n.Name.Contains("Timer", StringComparison.Ordinal));
        // Kaynakta da geçmemeli (yorum satırları dahil değil: yalnızca kod).
        string? d = AppContext.BaseDirectory; string? yol = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "LabDenetim", "TemaYonetici.cs"); if (File.Exists(y)) { yol = y; break; } }
        if (yol == null) return;
        var kod = File.ReadAllLines(yol).Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)).Select(s => { int k = s.IndexOf("//", StringComparison.Ordinal); return k >= 0 ? s[..k] : s; });
        string metin = string.Join("\n", kod);
        Assert.DoesNotContain("Timer", metin, StringComparison.Ordinal); Assert.DoesNotContain("OpenForms", metin, StringComparison.Ordinal);
    }

    [Fact]
    public void KayitliPencere_TutamacOlusunca_Boyanir_TemaDegisinceTekSeferdeGuncellenir_GecEklenenlerDeBoyanir()
    {
        using var f = new TemaTestFormu(); var panel = new Panel { BackColor = Color.Black }; var sayac = new TemaSayacKontrolu(); panel.Controls.Add(sayac); f.Controls.Add(panel);
        int olay = 0, eski = 0, ingilizce = 0; Action a = () => olay++, b = () => eski++, c = () => ingilizce++;
        TemaYonetici.TemaDegisti += a; TemaYonetici.Degisti += b; TemaYonetici.ThemeChanged += c;
        try
        {
            _ = f.Handle;                                                     // tutamaç oluştu: tek seferlik boyama
            Assert.True(panel.BackColor.GetBrightness() < 0.2f, "koyu temada koyu zemin korunmalı");
            TemaYonetici.Degistir();                                           // → Açık
            Assert.Equal(TemaModu.Acik, TemaYonetici.Mod);
            Assert.True(panel.BackColor.GetBrightness() > 0.6f, "açık temada zemin açılmalı");
            Assert.Equal(1, sayac.Sayi); Assert.Equal(1, olay); Assert.Equal(1, eski); Assert.Equal(1, ingilizce);   // her aboneye TEK bildirim
            var gec = new Panel { BackColor = Color.Black }; f.Controls.Add(gec);   // sonradan eklenen: ControlAdded ile kendiliğinden boyanır
            Assert.True(gec.BackColor.GetBrightness() > 0.6f, "sonradan eklenen kontrol açık temaya göre boyanmalı");
            TemaYonetici.Degistir();                                           // → Koyu
            Assert.True(panel.BackColor.GetBrightness() < 0.2f); Assert.Equal(2, sayac.Sayi); Assert.Equal(2, olay);
        }
        finally { TemaYonetici.TemaDegisti -= a; TemaYonetici.Degisti -= b; TemaYonetici.ThemeChanged -= c; }
    }

    [Fact]
    public void Pencere_KapaninceVeAtilinca_KayittanCikar_AyniPencereIkiKezKaydedilmez()
    {
        int once = TemaYonetici.KayitliPencereSayisi;
        var f = new TemaTestFormu(); Assert.Equal(once + 1, TemaYonetici.KayitliPencereSayisi);
        TemaYonetici.KaydetVeUygula(f); Assert.Equal(once + 1, TemaYonetici.KayitliPencereSayisi);   // ikinci kayıt etkisiz
        _ = f.Handle; f.Dispose();
        Assert.Equal(once, TemaYonetici.KayitliPencereSayisi);
        // Hiç gösterilmeyen/tutamacı olmayan pencere de atılınca çıkar.
        var g = new TemaTestFormu(); Assert.Equal(once + 1, TemaYonetici.KayitliPencereSayisi); g.Dispose(); Assert.Equal(once, TemaYonetici.KayitliPencereSayisi);
    }

    [Fact]
    public void KaydedilmemisPencere_TemaDegisinceEtkilenmez_AtilmisPencereBildirimAlmaz()
    {
        var f = new TemaTestFormu(); var sayac = new TemaSayacKontrolu(); f.Controls.Add(sayac); _ = f.Handle; f.Dispose();
        TemaYonetici.Degistir(); Assert.Equal(0, sayac.Sayi);   // atılmış pencereye bildirim gitmez
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kaynakta_DogrudanFormTuremeVeKayitsizNewForm_Yok()
    {
        string? d = AppContext.BaseDirectory; string? kok = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (Directory.Exists(Path.Combine(d, "LabDenetim"))) { kok = Path.Combine(d, "LabDenetim"); break; }
        if (kok == null) return;
        var kotu = new List<string>(); var turet = new System.Text.RegularExpressions.Regex(@"class\s+\w+\s*:\s*(System\.Windows\.Forms\.)?Form");
        foreach (var f in Directory.EnumerateFiles(kok, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
            var satirlar = File.ReadAllLines(f);
            for (int i = 0; i < satirlar.Length; i++)
            {
                string s = satirlar[i]; if (s.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                if (turet.IsMatch(s) && !f.EndsWith("TemaliForm.cs", StringComparison.Ordinal)) kotu.Add(Path.GetFileName(f) + ":" + (i + 1) + " doğrudan Form'dan türüyor");
                // Elle üretilen pencere (new Form ...) hemen ardından temaya kaydedilmeli (en geç 3 satır içinde).
                if (System.Text.RegularExpressions.Regex.IsMatch(s, @"new\s+(System\.Windows\.Forms\.)?Form\s*(\{|\()") && !string.Join(" ", satirlar.Skip(i).Take(4)).Contains("KaydetVeUygula", StringComparison.Ordinal))
                    kotu.Add(Path.GetFileName(f) + ":" + (i + 1) + " new Form temaya kaydedilmiyor");
            }
        }
        Assert.True(kotu.Count == 0, string.Join("; ", kotu));
    }
}
