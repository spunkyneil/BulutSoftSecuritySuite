// Küre animasyon duraklatma testleri (Faz 4): gerçek WebView2 içinde kure.html çalıştırılır; panel gizlenince / ana pencere küçültülünce requestAnimationFrame döngüsünün
// TAMAMEN durduğu (kare sayacı artmaz, rafAktif=false), görününce sürdüğü ölçülür. WebView2 çalışma zamanı yoksa test sessizce atlanır (kurulumda zorunlu değildir).
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class KureDuraklatmaTestleri
{
    private readonly ITestOutputHelper _cikti; public KureDuraklatmaTestleri(ITestOutputHelper cikti) => _cikti = cikti;

    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void KureHtml_DuraklatmaVeUyarlanabilirKareHizi_KaynakSozlesmesi()
    {
        string? kok = Kok(); if (kok == null) return; string html = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kure", "kure.html"));
        Assert.Contains("window.bsDuraklat", html, StringComparison.Ordinal); Assert.Contains("visibilitychange", html, StringComparison.Ordinal); Assert.Contains("cancelAnimationFrame", html, StringComparison.Ordinal);
        Assert.Contains("minKareMs", html, StringComparison.Ordinal); Assert.Contains("cizTaban", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(html, @"requestAnimationFrame\(").Count);   // yalnızca planla() ve kare atlama yeniden planlaması çağırır; döngü başka yerden canlandırılamaz
        string panel = File.ReadAllText(Path.Combine(kok, "LabDenetim", "KureWebPaneli.cs")); Assert.Contains("DuraklatBetigi", panel, StringComparison.Ordinal); Assert.Contains("WindowState: FormWindowState.Minimized", panel, StringComparison.Ordinal);
        Assert.Equal("window.bsDuraklat && window.bsDuraklat(true);", KureWebPaneli.DuraklatBetigi(true));
    }

    // Mesaj döngüsünü işleterek bir görevi bekler.
    private static T Bekle<T>(Task<T> g, int sn = 30) { var bitis = DateTime.UtcNow.AddSeconds(sn); while (!g.IsCompleted) { if (DateTime.UtcNow > bitis) throw new TimeoutException(); Application.DoEvents(); Thread.Sleep(8); } return g.GetAwaiter().GetResult(); }
    private static void Uyku(int ms) { var s = DateTime.UtcNow.AddMilliseconds(ms); while (DateTime.UtcNow < s) { Application.DoEvents(); Thread.Sleep(8); } }
    private static JsonElement Durum(KureWebPaneli p) { string? ham = Bekle(p.BetikCalistir("JSON.stringify(window.bsDurum ? window.bsDurum() : null)")!); string js = JsonSerializer.Deserialize<string>(ham!)!; return JsonDocument.Parse(js).RootElement.Clone(); }

    [Fact]
    public void GercekWebView2_PanelGizlenincePencereKucultulunce_AnimasyonDurur_GorununcDevamEder()
    {
        string? kok = Kok(); Exception? hata = null; var rapor = new List<string>(); bool atlandi = false;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok; BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "kure_" + Guid.NewGuid().ToString("N"));
            try
            {
                using var f = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), Size = new Size(420, 320), ShowInTaskbar = false, Opacity = 0.01 };   // sıfıra yakın saydam: kullanıcıyı rahatsız etmez, ama pencere "görünür" sayılır (rAF çalışır)
                var panel = new KureWebPaneli(); f.Controls.Add(panel); f.Show(); Application.DoEvents();
                var bitis = DateTime.UtcNow.AddSeconds(40); while (!panel.Hazir && panel.SonHata == null && DateTime.UtcNow < bitis) Uyku(50);
                if (!panel.Hazir) { atlandi = true; rapor.Add("WebView2 hazır olmadı: " + (panel.SonHata ?? "zaman aşımı")); return; }
                Uyku(1500);
                var d0 = Durum(panel); long k0 = d0.GetProperty("kare").GetInt64(); Uyku(1000); var d1 = Durum(panel); long k1 = d1.GetProperty("kare").GetInt64();
                if (k1 <= k0 + 3) { atlandi = true; rapor.Add($"Pencere arka planda: rAF ilerlemiyor (kare {k0}→{k1}); ölçüm yapılamadı."); return; }
                rapor.Add($"[ÖLÇÜM] görünürken 1 sn'de {k1 - k0} kare (rafAktif={d1.GetProperty("rafAktif").GetBoolean()}, maliyet≈{d1.GetProperty("maliyetMs").GetDouble()} ms)");
                Assert.False(d1.GetProperty("duraklatildi").GetBoolean()); Assert.True(d1.GetProperty("rafAktif").GetBoolean());

                // 1) Panel gizlenir (başka sekmeye geçmek gibi): döngü durmalı.
                panel.Visible = false; Uyku(300); var g0 = Durum(panel); long a = g0.GetProperty("kare").GetInt64(); Uyku(1500); var g1 = Durum(panel); long b = g1.GetProperty("kare").GetInt64();
                rapor.Add($"[KANIT] panel gizliyken 1,5 sn'de kare artışı: {b - a} (duraklatildi={g1.GetProperty("duraklatildi").GetBoolean()}, rafAktif={g1.GetProperty("rafAktif").GetBoolean()})");
                Assert.True(panel.Duraklatildi); Assert.True(g1.GetProperty("duraklatildi").GetBoolean()); Assert.False(g1.GetProperty("rafAktif").GetBoolean()); Assert.Equal(a, b);
                // 2) Görünür olunca devam eder.
                panel.Visible = true; Uyku(1200); long c = Durum(panel).GetProperty("kare").GetInt64(); rapor.Add($"[KANIT] yeniden görünür: kare {b} → {c}"); Assert.True(c > b + 3);
                // 3) Ana pencere simge durumuna küçültülür: durmalı (görünür olsa bile).
                f.WindowState = FormWindowState.Minimized; Uyku(400); long m0 = Durum(panel).GetProperty("kare").GetInt64(); Uyku(1500); var m1 = Durum(panel); long m2 = m1.GetProperty("kare").GetInt64();
                rapor.Add($"[KANIT] pencere simge durumundayken 1,5 sn'de kare artışı: {m2 - m0}"); Assert.Equal(m0, m2); Assert.False(m1.GetProperty("rafAktif").GetBoolean());
                f.WindowState = FormWindowState.Normal; Uyku(1200); long n = Durum(panel).GetProperty("kare").GetInt64(); rapor.Add($"[KANIT] pencere geri yüklendi: kare {m2} → {n}"); Assert.True(n > m2 + 3);
                // 4a) İmleç küre üzerine gelince dönüş durur: durağan karelerde taban önbellekten basılır (çizim hatası olursa döngü kesilir ve kare sayacı donardı).
                Bekle(panel.BetikCalistir("(function(){var c=document.querySelector('canvas');var r=c.getBoundingClientRect();c.dispatchEvent(new PointerEvent('pointermove',{clientX:r.left+r.width/2,clientY:r.top+r.height/2,bubbles:true}));return 1;})()")!); Uyku(1500);
                var h0 = Durum(panel); long hk0 = h0.GetProperty("kare").GetInt64(); Uyku(800); var h1 = Durum(panel); long hk1 = h1.GetProperty("kare").GetInt64();
                rapor.Add($"[ÖLÇÜM] imleç küre üzerinde: 0,8 sn'de {hk1 - hk0} kare; taban önbelleği çizim={h1.GetProperty("tabanCizim").GetInt64()} basım={h1.GetProperty("tabanBasim").GetInt64()}"); Assert.True(hk1 > hk0 + 3); Assert.True(h1.GetProperty("tabanBasim").GetInt64() > 10); Assert.True(h1.GetProperty("tabanCizim").GetInt64() <= 3);
                Bekle(panel.BetikCalistir("(function(){var c=document.querySelector('canvas');c.dispatchEvent(new PointerEvent('pointerleave',{bubbles:true}));return 1;})()")!);
                // 4b) Durağan karelerde taban önbelleği devrede (küre dönmüyorsa her karede yeniden çizilmez).
                var son = Durum(panel); rapor.Add($"[ÖLÇÜM] taban önbelleği: çizim={son.GetProperty("tabanCizim").GetInt64()} basım={son.GetProperty("tabanBasim").GetInt64()}, hedefKareMs={son.GetProperty("hedefKareMs").GetInt32()}, dpr sınırı={son.GetProperty("dpr").GetInt32()}");
                f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { try { Directory.Delete(BulutKlasor.Kok, true); } catch (IOException hk) { HataKayit.Sessiz(hk, "KureDuraklatmaTestleri.temizlik"); } BulutKlasor.Kok = eskiKok; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); Assert.True(t.Join(TimeSpan.FromSeconds(150)), "zaman aşımı");
        foreach (var s in rapor) _cikti.WriteLine(s);
        if (hata != null) throw new Exception("WebView2 sınaması hatası: " + hata, hata);
        if (atlandi) _cikti.WriteLine("(Ölçüm atlandı: " + string.Join(" | ", rapor) + ")");
    }
}
