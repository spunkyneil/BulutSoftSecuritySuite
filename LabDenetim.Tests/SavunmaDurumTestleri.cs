// SavunmaDurumServisi testleri: durum tek kaynakta, değişiklik olayı yalnızca gerçek değişimde; Savunma Merkezi çubuğu, sistem tepsisi ve küre servise abone; eski görünmez menü düğmesi kalmadı.
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class SavunmaDurumServisiTestleri
{
    [Fact]
    public void Servis_VarsayilanKapali_DegisimdeOlayVerir_AyniDegerdeVermez_AboneHatasiniYutar()
    {
        var s = new SavunmaDurumServisi(); int olay = 0; s.Degisti += () => olay++;
        Assert.False(s.AcilAktif || s.AcilMesgul || s.NobetAktif || s.NobetMesgul);
        s.AcilAyarla(true); s.AcilAyarla(true); Assert.True(s.AcilAktif); Assert.Equal(1, olay);
        s.AcilMesgulAyarla(true); s.NobetAyarla(true); s.NobetMesgulAyarla(true); Assert.Equal(4, olay); Assert.True(s.AcilMesgul && s.NobetAktif && s.NobetMesgul);
        s.AcilAyarla(false); Assert.Equal(5, olay); Assert.False(s.AcilAktif);
        var bozuk = new SavunmaDurumServisi(); bozuk.Degisti += () => throw new InvalidOperationException("abone hatası"); bozuk.NobetAyarla(true); Assert.True(bozuk.NobetAktif);   // durum yine de yazılır
    }

    [Fact]
    public async Task Servis_CokIsParcacigindaTutarli_SonDurumDogru()
    {
        var s = new SavunmaDurumServisi(); int olay = 0; s.Degisti += () => Interlocked.Increment(ref olay);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => { for (int k = 0; k < 200; k++) s.NobetAyarla(((i + k) & 1) == 0); })));
        s.NobetAyarla(true); Assert.True(s.NobetAktif); Assert.True(olay > 0);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void EskiMenuDugmeleriKalmadi_DurumMetinRenktenOkunmaz_ServisKullanilir()
    {
        string? d = AppContext.BaseDirectory, kok = null; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) { kok = d; break; }
        if (kok == null) return;
        var rx = new Regex(@"\bbtnMenu\w*");
        var kalan = Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(@"\obj\", StringComparison.Ordinal) && !f.Contains(@"\bin\", StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((s, i) => (Dosya: Path.GetFileName(f), Satir: i + 1, Metin: s)).Where(x => !x.Metin.TrimStart().StartsWith("//", StringComparison.Ordinal) && rx.IsMatch(x.Metin))).Select(x => $"{x.Dosya}:{x.Satir}").ToList();
        Assert.True(kalan.Count == 0, "Eski btnMenu* öğesi kalmamalı: " + string.Join(", ", kalan));
        string cubuk = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kabuk", "SavunmaUstCubuk.cs"));
        Assert.DoesNotContain(".Text.Contains(", cubuk, StringComparison.Ordinal); Assert.Contains("_servis.Degisti += Degisti;", cubuk, StringComparison.Ordinal); Assert.Contains("_servis.Degisti -= Degisti;", cubuk, StringComparison.Ordinal);   // abone olur, atılınca ayrılır
    }
}

[Collection("Veri")]
public class SavunmaDurumArayuzTestleri
{
    [Fact]
    public void Servis_SavunmaCubugunuTepsiyiVeKureyiAyniAndaGunceller()
    {
        Exception? hata = null; string tepsi0 = "", tepsi1 = "", cubuk0 = "", cubuk1 = "";
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "sav_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); ArayuzModu.Ayarla(ModTuru.Uzman); Oturum.Aktif = new Kullanici { Ad = "t", Rol = Rol.Yonetici };
            try
            {
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.Show(); Application.DoEvents();
                T Al<T>(string ad) => (T)(typeof(Form1).GetField(ad, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(f) ?? typeof(TaramaSayfasi).GetField(ad, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.GetValue(f._tarama)!);
                var svc = Al<SavunmaDurumServisi>("_savunma"); var kabuk = Al<AppShell>("_kabuk"); var tepsi = f._nobet.bildirim;
                kabuk.GitAsync("savunma").GetAwaiter().GetResult(); var cubuk = kabuk.Controls.Cast<Control>().SelectMany(Tum).OfType<SavunmaUstCubuk>().Single();
                tepsi0 = tepsi.Text; cubuk0 = cubuk.NobetDurumu.Baslik;
                svc.NobetAyarla(true); Application.DoEvents(); tepsi1 = tepsi.Text; cubuk1 = cubuk.NobetDurumu.Baslik;
                Assert.Equal(KartTonu.Basari, cubuk.NobetDurumu.Ton);
                svc.AcilAyarla(true); Assert.Equal("İZOLASYON AÇIK", cubuk.AcilDurumu.Baslik); svc.AcilMesgulAyarla(true); Assert.False(cubuk.AcilDurumu.Etkin); svc.AcilMesgulAyarla(false); Assert.True(cubuk.AcilDurumu.Etkin); svc.NobetAyarla(false);   // açık nöbetçi kapatırken onay sorar
                f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { Oturum.Aktif = null; ArayuzModu.Sifirla(); BulutKlasor.Kok = eskiKok; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "SavunmaDurumArayuzTestleri.temizlik"); } }
        }); t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(90))) throw new TimeoutException(); if (hata != null) throw new Exception("Savunma durum sınaması: " + hata, hata);
        Assert.Equal("BulutSoft Nöbetçi Modu", tepsi0); Assert.Equal("Nöbetçi Modu: KAPALI", cubuk0); Assert.Equal("BulutSoft Nöbetçi Modu: AÇIK", tepsi1); Assert.Equal("Nöbetçi Modu: AÇIK", cubuk1);
    }
    private static IEnumerable<Control> Tum(Control c) { yield return c; foreach (Control k in c.Controls) foreach (var x in Tum(k)) yield return x; }
}
