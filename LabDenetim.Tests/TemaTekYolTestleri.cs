// D3 (tek tema yolu): açık temada program içindeki HİÇBİR renk "yedek ters çevirme" (TemaYonetici.Cevir, HSL) yoluna düşmemeli; her renk Palet tablosunda bir role sahip olmalı.
// Bu test tüm kabuk sayfalarını (Kolay + Uzman, her sekme) ve ana iletişim pencerelerini AÇIK temada kurar; Palet.Bilinmeyen boş kalmalıdır.
using System.Reflection;
using LabDenetim.V2;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class TemaTekYolTestleri
{
    // Açık temada tüm sayfa ve pencereleri gezip yedek yönteme düşen renkleri toplar.
    internal static List<string> BilinmeyenRenkleriTopla()
    {
        var sonuc = new List<string>(); Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; string gecici = Path.Combine(Path.GetTempPath(), "temayol_" + Guid.NewGuid().ToString("N")); var eskiBildirici = GuvenliCalistir.HataBildirici; GuvenliCalistir.HataBildirici = (_, _) => { };
            Oturum.Aktif = new Kullanici { Ad = "yonetici", Rol = Rol.Yonetici };
            try
            {
                BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
                if (TemaYonetici.Mod == TemaModu.Koyu) TemaYonetici.Degistir(); Application.DoEvents(); Palet.Bilinmeyen.Clear();
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ClientSize = new Size(1500, 820); f.Show(); Application.DoEvents();
                var k = (AppShell)typeof(Form1).GetField("_kabuk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
                foreach (var mod in new[] { ModTuru.Kolay, ModTuru.Uzman })
                {
                    ArayuzModu.Ayarla(mod); Application.DoEvents();
                    foreach (var s in NavigasyonModeli.Gorunenler(mod)) { k.GitAsync(s.Id, null).GetAwaiter().GetResult(); Application.DoEvents(); foreach (var sk in NavigasyonModeli.GorunenSekmeler(s, mod)) { k.GitAsync(s.Id, sk.Id).GetAwaiter().GetResult(); Application.DoEvents(); } }
                }
                // Ana iletişim pencereleri.
                foreach (var ad in new[] { "HakkindaFormu", "ApiAyarFormu", "TurFormu", "DestekFormu", "KullaniciFormu", "LisansFormu" })
                {
                    var ctor = typeof(Form1).Assembly.GetType("LabDenetim." + ad)?.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null); if (ctor == null) continue;
                    using var d = (Form)ctor.Invoke(null); d.StartPosition = FormStartPosition.Manual; d.Location = new Point(-5000, -5000); d.Show(); Application.DoEvents();
                }
                using (var d = new SurecAvciFormu(_ => { })) { d.StartPosition = FormStartPosition.Manual; d.Location = new Point(-5000, -5000); d.Show(); Application.DoEvents(); }
                using (var d = new SahiplikFormu("ornek.com")) { d.StartPosition = FormStartPosition.Manual; d.Location = new Point(-5000, -5000); d.Show(); Application.DoEvents(); }
                // Diğer iletişim kutuları (kurucusu basit olanlar): komut paleti, zamanlama, başarılar, Claude onayı, giriş, kural kalitesi.
                void Goster(Form d) { using (d) { d.StartPosition = FormStartPosition.Manual; d.Location = new Point(-5000, -5000); d.Show(); Application.DoEvents(); } }
                Goster(new KomutPaleti(new List<(string, Action)> { ("Sınama komutu", () => { }) })); Goster(new ZamanlamaFormu("ornek.com", ZamanlamaDeposu.Yukle())); Goster(new BasarilarFormu(new List<GecmisKayit>()));
                Goster(new ClaudeOnayFormu("Örnek metin: bu yalnızca bir sınamadır.")); Goster(new GirisFormu(false)); Goster(new GirisFormu(true)); Goster(new KuralKalitesiFormu(FpDeposu.Yukle()));
                f.Close();
                sonuc = Palet.Bilinmeyen.Select(p => p.Key + " x" + p.Value).OrderBy(x => x).ToList();
            }
            catch (Exception ex) { hata = ex; }
            finally { if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir(); Oturum.Aktif = null; ArayuzModu.Sifirla(); GuvenliCalistir.HataBildirici = eskiBildirici; BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "TemaTekYolTestleri.temizlik"); } }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(150))) throw new TimeoutException("Sayfalar gezilemedi");
        if (hata != null) throw new Exception("Tema gezintisi hatası: " + hata, hata);
        return sonuc;
    }

    [Fact]
    public void AcikTema_TumSayfaVePencereler_HicbirRenkYedekTersCevirmeyeDusmez()
    {
        var bilinmeyen = BilinmeyenRenkleriTopla();
        Assert.True(bilinmeyen.Count == 0, "Palet tablosunda karşılığı olmayan renkler (Palet.Tablo'ya ekleyin):\n" + string.Join("\n", bilinmeyen));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Palet_OrtakRoller_DesignTokensTekKaynaktan_IkiTemadaAyniDeger()
    {
        var eslesme = new (PaletRol, TokenRol)[] { (PaletRol.Sayfa, TokenRol.SurfaceApp), (PaletRol.Kart, TokenRol.SurfaceCard), (PaletRol.Girdi, TokenRol.SurfaceInput), (PaletRol.Izgara, TokenRol.BorderSubtle), (PaletRol.KenarUstunde, TokenRol.BorderStrong), (PaletRol.Metin, TokenRol.TextPrimary),
            (PaletRol.BasariMetin, TokenRol.StatusSuccess), (PaletRol.BasariArka, TokenRol.StatusSuccessBg), (PaletRol.UyariMetin, TokenRol.StatusWarning), (PaletRol.UyariArka, TokenRol.StatusWarningBg), (PaletRol.TehlikeMetin, TokenRol.StatusDanger), (PaletRol.TehlikeArka, TokenRol.StatusDangerBg), (PaletRol.BilgiMetin, TokenRol.StatusInfo), (PaletRol.BilgiArka, TokenRol.StatusInfoBg) };
        foreach (var (p, tk) in eslesme) foreach (bool koyu in new[] { true, false }) Assert.Equal(DesignTokens.Renk(tk, koyu).ToArgb(), Palet.Renk(p, koyu).ToArgb());
        // Palet.cs bu rollerin değerini tekrar yazmaz.
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null && !File.Exists(Path.Combine(kok, "Directory.Build.props")); i++) kok = Path.GetDirectoryName(kok); if (kok == null) return;
        string kaynak = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Palet.cs"));
        foreach (var (p, _) in eslesme) Assert.DoesNotContain($"[PaletRol.{p}] = H(", kaynak, StringComparison.Ordinal);
        // HSL ters-çevirme yedeği kaldırıldı.
        Assert.DoesNotContain("TemaYonetici.Cevir(", kaynak, StringComparison.Ordinal); Assert.DoesNotContain("static Color Cevir(Color", File.ReadAllText(Path.Combine(kok, "LabDenetim", "TemaYonetici.cs")), StringComparison.Ordinal);
    }
}
