// Tema geçişinde "kalan beyaz/yanlış renkli alan" denetimi: koyu temada tüm sayfalar gezilir, renkler kaydedilir; Açık'a ve tekrar Koyu'ya geçilince HER denetimin rengi ilk koyu değerine dönmelidir
// (ve tersi: Açık → Koyu → Açık). Dönmeyen denetim "temada takılı kalmış"tır (kullanıcı raporu: tema değişince beyaz ekranlar kalıyor, tekrar değiştirince düzeliyor).
using System.Reflection;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// NOT: her sınama tüm sayfaları birkaç kez gezer (2-3 dk); varsayılan birim koşusundan çıkarıldı (Category=Integration). Tema/miras kuralı (TemaYonetici.Yakala) değişince elle çalıştırın:
//   dotnet test LabDenetim.Tests --filter "FullyQualifiedName~TemaGidisDonus"
[Collection("Veri")]
public class TemaGidisDonusTestleri
{
    // Denetim ağacının (yol, arka plan, yazı rengi) dökümü.
    private static Dictionary<string, (Color Arka, Color Yazi)> Dok(Control kok)
    {
        var d = new Dictionary<string, (Color, Color)>();
        void Gez(Control c, string yol)
        {
            int i = 0; foreach (Control k in c.Controls) { string y = yol + "/" + k.GetType().Name + "[" + (i++) + "]"; if (k is not (DataGridView or ScrollBar or WebView2Kapsayici) && !(k is Label l && l.Text.StartsWith("Son 30 dk", StringComparison.Ordinal))) d[y] = (k.BackColor, k.ForeColor); Gez(k, y); }   // (küre ışık yazısının rengi olay verisine bağlıdır, tema değil)
        }
        Gez(kok, "Form1"); return d;
    }
    private sealed class WebView2Kapsayici : Control { }

    // Mesaj pompalayarak bekler (WebView2 gibi arayüz iş parçacığına dönen devamlar eşzamanlı beklemede kilitlenmesin).
    private static void Bekle(Task g) { var son = DateTime.Now.AddSeconds(60); while (!g.IsCompleted && DateTime.Now < son) { Application.DoEvents(); Thread.Sleep(2); } g.GetAwaiter().GetResult(); }
    private static void Iz(string m) { try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "temagd_iz.txt"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + m + Environment.NewLine); } catch (IOException hki) { HataKayit.Sessiz(hki, "TemaGidisDonusTestleri.Iz", true); } }

    internal static List<string> Farklar(bool baslangicKoyu)
    {
        var sonuc = new List<string>(); Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; string gecici = Path.Combine(Path.GetTempPath(), "temagd_" + Guid.NewGuid().ToString("N")); var eskiBildirici = GuvenliCalistir.HataBildirici; GuvenliCalistir.HataBildirici = (_, _) => { };
            Oturum.Aktif = new Kullanici { Ad = "yonetici", Rol = Rol.Yonetici };
            try
            {
                BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
                if ((TemaYonetici.Mod == TemaModu.Koyu) != baslangicKoyu) TemaYonetici.Degistir(); Application.DoEvents();
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ClientSize = new Size(1500, 820); f.Show(); Application.DoEvents();
                var k = (AppShell)typeof(Form1).GetField("_kabuk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
                void Gez() { foreach (var mod in new[] { ModTuru.Uzman, ModTuru.Kolay }) { ArayuzModu.Ayarla(mod); Application.DoEvents(); foreach (var s in NavigasyonModeli.Gorunenler(mod)) { Iz("git " + s.Id); Bekle(k.GitAsync(s.Id, null)); Application.DoEvents(); foreach (var sk in NavigasyonModeli.GorunenSekmeler(s, mod)) { Iz("git " + s.Id + "/" + sk.Id); Bekle(k.GitAsync(s.Id, sk.Id)); Application.DoEvents(); } } } ArayuzModu.Ayarla(ModTuru.Uzman); Bekle(k.GitAsync("durum", null)); Application.DoEvents(); }
                Gez();                                         // tüm sayfalar kurulsun
                var once = Dok(f);
                for (int tur = 0; tur < 2; tur++)
                {
                    Iz("tema1"); TemaYonetici.Degistir(); Application.DoEvents(); Gez(); Iz("tema2"); TemaYonetici.Degistir(); Application.DoEvents(); Gez();
                    var sonra = Dok(f);
                    foreach (var (yol, (a0, y0)) in once) if (sonra.TryGetValue(yol, out var s) && (s.Arka.ToArgb() != a0.ToArgb() || s.Yazi.ToArgb() != y0.ToArgb())) sonuc.Add($"tur{tur + 1} {yol}: arka {a0.Name}→{s.Arka.Name}, yazı {y0.Name}→{s.Yazi.Name}");
                }
                f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { if ((TemaYonetici.Mod == TemaModu.Koyu) != true) TemaYonetici.Degistir(); Oturum.Aktif = null; ArayuzModu.Sifirla(); GuvenliCalistir.HataBildirici = eskiBildirici; BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "TemaGidisDonusTestleri.temizlik"); } }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(420))) throw new TimeoutException("Sayfalar gezilemedi");
        if (hata != null) throw new Exception("Tema gidiş-dönüş hatası: " + hata, hata);
        return sonuc;
    }

    [Fact, Trait("Category", "Integration")]
    public void KoyuAcikKoyu_HerDenetimIlkRengineDoner()
    {
        var fark = Farklar(true);
        Assert.True(fark.Count == 0, $"Temada takılı kalan {fark.Count} denetim:\n" + string.Join("\n", fark.Take(40)));
    }

    [Fact, Trait("Category", "Integration")]
    public void AcikKoyuAcik_HerDenetimIlkRengineDoner()
    {
        var fark = Farklar(false);
        Assert.True(fark.Count == 0, $"Temada takılı kalan {fark.Count} denetim:\n" + string.Join("\n", fark.Take(40)));
    }

    // Tek bir Form1 kurar; senaryo (tüm sayfaları gezen eylemi alır) bitince denetim dökümünü verir. Başlangıç teması koyu.
    private static Dictionary<string, (Color Arka, Color Yazi)> Topla(Action<Action> senaryo)
    {
        Dictionary<string, (Color, Color)>? sonuc = null; Exception? hata = null;
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; string gecici = Path.Combine(Path.GetTempPath(), "temagd2_" + Guid.NewGuid().ToString("N")); var eskiBildirici = GuvenliCalistir.HataBildirici; GuvenliCalistir.HataBildirici = (_, _) => { };
            Oturum.Aktif = new Kullanici { Ad = "yonetici", Rol = Rol.Yonetici };
            try
            {
                BulutKlasor.Kok = gecici; ArayuzModu.Sifirla(); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N"); if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir(); Application.DoEvents();
                using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ClientSize = new Size(1500, 820); f.Show(); Application.DoEvents();
                var k = (AppShell)typeof(Form1).GetField("_kabuk", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!;
                void Gez() { ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); foreach (var s in NavigasyonModeli.Gorunenler(ModTuru.Uzman)) { Bekle(k.GitAsync(s.Id, null)); Application.DoEvents(); foreach (var sk in NavigasyonModeli.GorunenSekmeler(s, ModTuru.Uzman)) { Bekle(k.GitAsync(s.Id, sk.Id)); Application.DoEvents(); } } Bekle(k.GitAsync("durum", null)); Application.DoEvents(); }
                senaryo(Gez); sonuc = Dok(f); f.Close();
            }
            catch (Exception ex) { hata = ex; }
            finally { if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir(); Oturum.Aktif = null; ArayuzModu.Sifirla(); GuvenliCalistir.HataBildirici = eskiBildirici; BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; try { Directory.Delete(gecici, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "TemaGidisDonusTestleri.temizlik2"); } }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(420))) throw new TimeoutException("Sayfalar gezilemedi");
        if (hata != null) throw new Exception("Tema senaryosu hatası: " + hata, hata);
        return sonuc!;
    }

    // Kullanıcı senaryosu: Açık'a geç, sayfaları O SIRADA ilk kez aç (açıkta oluşsunlar), tekrar Koyu'ya dön → doğrudan koyuda oluşan sayfalarla birebir aynı renklerde olmalı.
    [Fact, Trait("Category", "Integration")]
    public void SayfalarAciktaOlusturulupKoyuyaDonunce_KoyudaOlusanlarlaAyniRenktedir()
    {
        var referans = Topla(gez => gez());
        var deneme = Topla(gez => { TemaYonetici.Degistir(); Application.DoEvents(); gez(); TemaYonetici.Degistir(); Application.DoEvents(); gez(); });
        var fark = new List<string>(); foreach (var (yol, (a0, y0)) in referans) if (deneme.TryGetValue(yol, out var s) && (s.Arka.ToArgb() != a0.ToArgb() || s.Yazi.ToArgb() != y0.ToArgb())) fark.Add($"{yol}: arka {a0.Name}→{s.Arka.Name}, yazı {y0.Name}→{s.Yazi.Name}");
        Assert.True(fark.Count == 0, $"Açıkta oluşup koyuya dönünce farklı kalan {fark.Count} denetim:" + Environment.NewLine + string.Join(Environment.NewLine, fark.Take(40)));
    }
}
