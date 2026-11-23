// Faz 3 hijyen testleri: tüm çözümde 0 boş catch ve (TemaliMesaj acil yedeği hariç) 0 doğrudan MessageBox.Show; TemaliMesaj'ın tema başlatılmadan kurulabilmesi ve kurulamazsa yedeğe düşmesi;
// brand.json'un tek ortak konumdan (arayüz + SYSTEM hizmeti) okunması ve veri klasörü adının sıkı doğrulanması (path traversal vb.).
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public class HijyenTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    private static IEnumerable<string> Kaynaklar(string kok, params string[] projeler)
    {
        foreach (var p in projeler)
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, p), "*.cs", SearchOption.AllDirectories))
            { string s = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, b = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar; if (!f.Contains(s, StringComparison.Ordinal) && !f.Contains(b, StringComparison.Ordinal)) yield return f; }
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void TumCozum_BosCatchBulunmaz()
    {
        string? kok = Kok(); if (kok == null) return;
        var rx = new Regex(@"catch\s*(\(\s*[\w\.]+(?:\s+\w+)?\s*\))?\s*\{\s*(?:/\*.*?\*/\s*)?\}", RegexOptions.Singleline);
        var kotu = Kaynaklar(kok, "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService", "LabDenetim.Tests").Where(f => rx.IsMatch(File.ReadAllText(f))).Select(f => Path.GetRelativePath(kok, f)).ToList();
        Assert.True(kotu.Count == 0, "Boş catch (HataKayit.Sessiz kullanın): " + string.Join(", ", kotu));
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void TumCozum_DogrudanMessageBoxShowYok_YalnizTemaliMesajAcilYedegi()
    {
        string? kok = Kok(); if (kok == null) return;
        var rx = new Regex(@"(?<![""\w])MessageBox\.Show\(");
        var bulunan = Kaynaklar(kok, "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService", "LabDenetim.Tests")
            .Select(f => (Dosya: Path.GetRelativePath(kok, f), Say: File.ReadAllLines(f).Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)).Sum(s => rx.Matches(s).Count))).Where(x => x.Say > 0).ToList();
        // Tek istisna: TemaliMesaj'ın kendi pencere kurulumu başarısız olursa Windows kutusuna düşen acil durum yedeği (iki aşırı yükleme tek temsilcide).
        Assert.Equal(new[] { Path.Combine("LabDenetim", "Kontroller", "TemaliMesaj.cs") }, bulunan.Select(x => x.Dosya).ToArray());
        Assert.Equal(2, bulunan.Single().Say);   // sahipli + sahipsiz çağrı aynı yedek temsilcisinde
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void TemaliMesaj_TemaBaslatilmadanKurulur_ProgramErkenHatalariGosterebilir()
    {
        Exception? hata = null; string baslik = ""; bool kapali = false;
        var t = new Thread(() =>
        {
            try { using var f = TemaliMesaj.PencereOlustur("Erken hata metni", "Zaten Açık", MessageBoxButtons.OK, MessageBoxIcon.Information); _ = f.Handle; baslik = f.Text; kapali = f.IsHandleCreated; }   // TemaYonetici.Baslat() çağrılmadan
            catch (Exception ex) { hata = ex; }
        }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
        Assert.Null(hata); Assert.Equal("Zaten Açık", baslik); Assert.True(kapali);
        string? kok = Kok(); if (kok == null) return;
        string program = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Program.cs"));
        Assert.DoesNotContain("MessageBox.Show(", program, StringComparison.Ordinal); Assert.True(Regex.Matches(program, @"TemaliMesaj\.Goster\(").Count >= 3);
    }

    [Fact]
    public void TemaliMesaj_PencereKurulamazsa_AcilYedegeDuser_MesajKaybolmaz()
    {
        var eskiYedek = TemaliMesaj.YedekPenceresi; string? metin = null, baslik = null; var sonuc = DialogResult.None;
        var t = new Thread(() =>
        {
            TemaliMesaj.PencereHatasiZorla = true; TemaliMesaj.YedekPenceresi = (_, m, b, _, _, _) => { metin = m; baslik = b; return DialogResult.Yes; };
            try { sonuc = TemaliMesaj.Goster("Beklenmedik sorun", "Başlık", MessageBoxButtons.YesNo, MessageBoxIcon.Warning); }
            finally { TemaliMesaj.PencereHatasiZorla = false; TemaliMesaj.YedekPenceresi = eskiYedek; }
        }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
        Assert.Equal(DialogResult.Yes, sonuc); Assert.Equal("Beklenmedik sorun", metin); Assert.Equal("Başlık", baslik);
    }

    // ---------- Adım 2: async void ve zamanlayıcılar ----------
    [Fact, Trait("Tur", "KaynakKurali")]
    public void TumCozum_AsyncVoidVeAsyncOlayLambdasiYok_GuvenliCalistirKullanilir()
    {
        string? kok = Kok(); if (kok == null) return;
        var rx = new Regex(@"\basync\s+void\b|\+=\s*async\s*\(");
        var kotu = Kaynaklar(kok, "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService")
            .Where(f => File.ReadAllLines(f).Any(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal) && rx.IsMatch(s))).Select(f => Path.GetRelativePath(kok, f)).ToList();
        Assert.True(kotu.Count == 0, "async void / async olay lambdası (GuvenliCalistir.Baslat kullanın): " + string.Join(", ", kotu));
    }

    [Fact]
    public async Task GuvenliCalistir_HatayiYakalar_Bildirir_IptaliSessizGecer_BasariyiCalistirir()
    {
        var eski = GuvenliCalistir.HataBildirici; var bildirilen = new List<(string, string)>();
        GuvenliCalistir.HataBildirici = (b, ex) => bildirilen.Add((b, ex.Message));
        try
        {
            int ok = 0; await GuvenliCalistir.CalistirAsync(async () => { await Task.Yield(); ok++; }, "test.basari"); Assert.Equal(1, ok); Assert.Empty(bildirilen);
            int once = HataKayit.Sayi("test.hata"); await GuvenliCalistir.CalistirAsync(async () => { await Task.Yield(); throw new InvalidOperationException("patladı"); }, "test.hata");   // sızmaz
            Assert.Equal(new[] { ("test.hata", "patladı") }, bildirilen); Assert.Equal(once + 1, HataKayit.Sayi("test.hata"));
            await GuvenliCalistir.CalistirAsync(() => throw new OperationCanceledException(), "test.iptal"); Assert.Single(bildirilen);   // iptal kullanıcıya hata olarak bildirilmez
            Assert.True(HataKayit.Sayi("test.iptal.iptal") >= 1);
            GuvenliCalistir.Baslat(() => throw new InvalidOperationException("senkron fırlatma"), "test.senkron"); await Task.Delay(50); Assert.Equal(2, bildirilen.Count);   // iş yöntemi senkron fırlatsa bile
        }
        finally { GuvenliCalistir.HataBildirici = eski; }
    }

    [Theory]
    [InlineData(true, true, false, true, true, true, false, true)]
    [InlineData(false, true, false, true, true, true, false, false)]   // istenmemiş
    [InlineData(true, true, false, true, false, true, false, false)]   // görünmez
    [InlineData(true, true, false, true, true, false, false, false)]   // pencereye bağlı değil
    [InlineData(true, true, false, true, true, true, true, false)]     // simge durumunda
    [InlineData(true, true, true, true, true, true, false, false)]     // atılmış
    [InlineData(true, true, false, false, true, true, false, false)]   // tutamaç yok
    [InlineData(true, false, false, true, true, true, false, false)]   // sahip yok
    public void GorunurZamanlayici_KararKurali(bool istenen, bool sahipVar, bool atildi, bool handle, bool gorunur, bool pencereVar, bool simge, bool beklenen) =>
        Assert.Equal(beklenen, GorunurZamanlayici.CalismaliMi(istenen, sahipVar, atildi, handle, gorunur, pencereVar, simge));

    [Fact]
    public void GorunurZamanlayici_GizlenincePencereSimgeDurumunaGecinceDurur_GeriGelinceSurer()
    {
        Exception? hata = null;
        var t = new Thread(() =>
        {
            try
            {
                using var f = new TemaTestFormu { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Size = new Size(400, 300), ShowInTaskbar = false }; var panel = new Panel { Dock = DockStyle.Fill }; f.Controls.Add(panel);
                using var z = new GorunurZamanlayici { Interval = 50, Sahip = panel }; z.Start(); Assert.True(z.Enabled); Assert.False(z.Calisiyor);   // pencere henüz gösterilmedi
                f.Show(); Application.DoEvents(); Assert.True(z.Calisiyor);
                panel.Visible = false; Application.DoEvents(); Assert.True(z.Enabled); Assert.False(z.Calisiyor);   // başka sayfa/sekme
                panel.Visible = true; Application.DoEvents(); Assert.True(z.Calisiyor);
                f.WindowState = FormWindowState.Minimized; Application.DoEvents(); Assert.False(z.Calisiyor);       // simge durumu
                f.WindowState = FormWindowState.Normal; Application.DoEvents(); Assert.True(z.Calisiyor);
                z.Stop(); Assert.False(z.Calisiyor); Assert.False(z.Enabled);
                z.Start(); f.Controls.Remove(panel); Application.DoEvents(); Assert.False(z.Calisiyor);              // sayfadan koparıldı (üst pencere yok)
            }
            catch (Exception ex) { hata = ex; }
        }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); if (hata != null) throw new Exception("Zamanlayıcı sınaması: " + hata, hata);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Zamanlayicilar_YalnizGorselOlanlarDuraklar_GuvenlikZamanlayicilariKesintisizCalisir()
    {
        string? kok = Kok(); if (kok == null) return;
        string Oku(string p) => File.ReadAllText(Path.Combine(kok, p));
        // Görsel: radar, canlı trafik tablosu, küre imleç/ışık, kart sayaçları duraklatılabilir.
        foreach (var g in new[] { "RadarPaneli.cs", @"Kontroller\TaramaSayfasi.Trafik.cs", "KureWebPaneli.cs", @"Servisler\UzmanKureYoneticisi.cs", @"Kontroller\TaramaSayfasi.Kartlar.cs" }) Assert.Contains("GorunurZamanlayici", Oku(@"LabDenetim\" + g), StringComparison.Ordinal);
        // Güvenlik/koruma: Nöbetçi Modu + koruma hizmeti yoklaması, zamanlanmış tarama, Kali sağlık denetimi, Brute-Force otomatik yenileme, bildirim sayacı, USB kalkanı — ASLA duraklatılmaz.
        foreach (var g in new[] { @"Servisler\NobetciEylemi.cs", @"Servisler\KisayolEylemleri.cs", "Kisayol.cs", "MerkezFormu.cs", "BruteForceFormu.cs", "UsbKalkani.cs", @"Kabuk\BildirimKaynagi.cs", "SavunmaDurumServisi.cs" })
        { string yol = Path.Combine(kok, "LabDenetim", g); if (File.Exists(yol)) Assert.DoesNotContain("GorunurZamanlayici", File.ReadAllText(yol), StringComparison.Ordinal); }
        Assert.DoesNotContain("GorunurZamanlayici", Oku(@"LabDenetim\Kisayol.cs"), StringComparison.Ordinal);
    }

    // ---------- brand.json: ortak konum ve sıkı doğrulama ----------
    [Fact, Trait("Tur", "KaynakKurali")]
    public void BrandJson_TekOrtakKonumdan_ArayuzVeHizmetAyniDosyayiOkur()
    {
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BulutSoft", "brand.json"), BrandConfig.DosyaYolu);
        string? kok = Kok(); if (kok == null) return;
        // brand.json yalnızca BrandConfig'te anılır; arayüz ve koruma hizmeti markayı BrandConfig.Aktif üzerinden alır (ayrı okuma yolu yok).
        var anan = Kaynaklar(kok, "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService").Where(f => File.ReadAllLines(f).Any(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal) && s.Contains("brand.json", StringComparison.OrdinalIgnoreCase))).Select(Path.GetFileName).ToList();   // yorum satırları sayılmaz
        Assert.Equal(new[] { "BrandConfig.cs" }, anan);
        Assert.Contains("BrandConfig.OrtakKlasor", File.ReadAllText(Path.Combine(kok, "BulutSoftGuardService", "GuardAyarlari.cs")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("..")] [InlineData(".")] [InlineData("..\\\\..\\\\Windows")] [InlineData("../etc")] [InlineData("C:\\\\Windows")] [InlineData("C:")] [InlineData("a:b")] [InlineData("\\\\\\\\sunucu\\\\paylasim")]
    [InlineData("son.")] [InlineData("com1")] [InlineData("NUL.txt")] [InlineData("Aux")] [InlineData("a\\u202eb")] [InlineData("a\\u0000b")]
    [InlineData("yirmi-bes-karakterden-cok-uzun-bir-klasor-adi-kirk-bir")]
    public void VeriKlasoruAdi_TehlikeliDegerler_GuvenliVarsayilanaDuser(string klasor)
    {
        var b = BrandConfig.Oku("{\"veriKlasoruAdi\":\"" + klasor + "\"}");
        Assert.Equal("BulutSoft", b.VeriKlasoruAdi); Assert.NotEmpty(b.Uyarilar);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), b.OrtakYolu, StringComparison.OrdinalIgnoreCase);   // hiçbir zaman kök dışına çıkmaz
    }

    [Theory]
    [InlineData("AcmeShield")] [InlineData("Acme Shield 2")] [InlineData("acme_shield-1.0")]
    public void VeriKlasoruAdi_GecerliDegerler_KabulEdilir(string klasor) => Assert.Equal(klasor, BrandConfig.Oku("{\"veriKlasoruAdi\":\"" + klasor + "\"}").VeriKlasoruAdi);
}
