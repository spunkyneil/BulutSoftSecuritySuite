// Atomik yazma testleri (Faz 4): kalıcı durum dosyaları ortak ".tmp" adıyla elle yer değiştirilmez; Depo.Yaz eşzamanlı yazarlarda dosyayı asla yarım bırakmaz.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class AtomikYazmaTestleri
{
    [Fact]
    public void Depo_Yaz_ParalelYazarlarda_DosyaHepSaglamJsonKalir_GeciciDosyaArtmaz()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "atomik_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(klasor); string yol = Path.Combine(klasor, "durum.json");
        try
        {
            var hatalar = new List<string>();
            Parallel.For(0, 200, i =>
            {
                try { Depo.YazJson(yol, new { sira = i, dolgu = new string('x', 2000 + i) }); string oku = File.ReadAllText(yol); if (!Depo.JsonMu(oku)) lock (hatalar) hatalar.Add("yarım/bozuk: " + oku.Length); }
                catch (IOException ex) { lock (hatalar) hatalar.Add(ex.Message); }
            });
            Assert.True(hatalar.Count == 0, string.Join(" | ", hatalar.Take(3)));
            Assert.Empty(Directory.GetFiles(klasor, "*.tmp"));
        }
        finally { try { Directory.Delete(klasor, true); } catch (IOException hk) { HataKayit.Sessiz(hk, "AtomikYazmaTestleri.temizlik"); } }
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void KalıcıDurumYazanlar_OrtakTmpAdiylaElleYerDegistirmez_DepoYazKullanir()
    {
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null; i++, kok = Path.GetDirectoryName(kok)) if (File.Exists(Path.Combine(kok, "Directory.Build.props"))) break;
        if (kok == null) return;
        var izinli = new HashSet<string> { "Depo.cs", "Butunluk.cs", "FiloRaporu.cs" };   // Depo: asıl uygulama; Butunluk (DPAPI mühür) ve FiloRaporu (kullanıcı çıktısı) kendi doğrulamalı yazımıyla
        var ihlal = new List<string>();
        foreach (var proje in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, proje), "*.cs", SearchOption.AllDirectories))
            {
                if (Regex.IsMatch(f, @"[\/](obj|bin)[\/]") || izinli.Contains(Path.GetFileName(f))) continue;
                int n = 0; foreach (var s in File.ReadLines(f)) { n++; if (!s.TrimStart().StartsWith("//") && Regex.IsMatch(s, @"=\s*\w+\s*\+\s*""\.tmp""")) ihlal.Add($"{Path.GetFileName(f)}:{n}"); }
            }
        Assert.True(ihlal.Count == 0, "Elle .tmp yer değiştirme (Depo.Yaz kullan): " + string.Join(", ", ihlal));
    }
}
