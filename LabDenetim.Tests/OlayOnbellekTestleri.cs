// OlayDeposu artımlı okuma testleri (Faz 4): 10.000 satırlık sentetik günlükte son-30-dk özeti, değişmeyen dosyada yeniden ayrıştırma yapmaz,
// eklenen satır yalnızca kendisini ayrıştırır, arşiv rotasyonunda önbellek sıfırlanır, sayfalı tam tarama doğru sıralar; zincir doğrulama önbelleği güvenli çalışır.
// Süreler (ms) test çıktısına yazılır (örnek: dotnet test --logger "console;verbosity=detailed").
using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public sealed class OlayOnbellekTestleri : IDisposable
{
    private readonly ITestOutputHelper _cikti; private readonly string _t = Path.Combine(Path.GetTempPath(), "olayon_" + Guid.NewGuid().ToString("N")); private readonly string _eskiYol = OlayDeposu.Yol; private readonly bool _eskiAyna = OlayDeposu.AynaAktif;
    private readonly DateTime _simdi = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Local);

    public OlayOnbellekTestleri(ITestOutputHelper cikti)
    {
        _cikti = cikti; Directory.CreateDirectory(_t); OlayDeposu.Yol = Path.Combine(_t, "olaylar.jsonl"); OlayDeposu.AynaAktif = false; OlayDeposu.OnbellegiSifirla();
    }
    public void Dispose() { OlayDeposu.Yol = _eskiYol; OlayDeposu.AynaAktif = _eskiAyna; OlayDeposu.OnbellegiSifirla(); try { Directory.Delete(_t, true); } catch (IOException hk) { HataKayit.Sessiz(hk, "OlayOnbellekTestleri.temizlik"); } }

    // 10.000 satır: 10 saate yayılmış; her 20. satır Uyarı/Kritik; son 30 dakikada bilinen sayıda.
    private (int Kritik, int Uyari) Uret(int adet)
    {
        int kritik = 0, uyari = 0; var sb = new System.Text.StringBuilder();
        for (int i = 0; i < adet; i++)
        {
            var z = _simdi.AddSeconds(-(adet - i) * 3.6);   // 10.000 satır ≈ 10 saat
            string sev = i % 20 == 0 ? (i % 40 == 0 ? "Kritik" : "Uyarı") : "Bilgi";
            if (sev != "Bilgi" && _simdi - z <= TimeSpan.FromMinutes(30)) { if (sev == "Kritik") kritik++; else uyari++; }
            sb.Append(JsonSerializer.Serialize(new OlayKaydi(z, "Tarama", sev, "olay " + i))).Append('\n');
        }
        File.WriteAllText(OlayDeposu.Yol, sb.ToString()); return (kritik, uyari);
    }

    [Fact]
    public void Son30Dk_ArtimliOkuma_DegismeyenDosyadaYenidenAyristirmaz_VeSureleriYazar()
    {
        var (k, u) = Uret(10_000);
        var sw = Stopwatch.StartNew(); var tam = OlayDeposu.Oku(); sw.Stop(); long tamMs = sw.ElapsedMilliseconds;   // eski yöntem: tüm dosyayı ayrıştırır
        Assert.Equal(10_000, tam.Count);
        sw.Restart(); var o1 = KureIsiklari.Ozetle(OlayDeposu.SonUyariKritik(KureIsiklari.Pencere, _simdi), _simdi); sw.Stop(); long soguk = sw.ElapsedMilliseconds;
        Assert.NotNull(o1); Assert.Equal(k, o1!.Kritik); Assert.Equal(u, o1.Uyari);
        long ayr1 = OlayDeposu.AyrisanSatirSayisi;
        sw.Restart(); OlayDeposu.SonUyariKritik(KureIsiklari.Pencere, _simdi); sw.Stop(); double degismedi = sw.Elapsed.TotalMilliseconds;
        Assert.Equal(ayr1, OlayDeposu.AyrisanSatirSayisi);   // dosya değişmedi: tek satır bile ayrıştırılmadı
        // Bir satır eklenir: yalnızca o satır ayrıştırılır ve özete yansır.
        File.AppendAllText(OlayDeposu.Yol, JsonSerializer.Serialize(new OlayKaydi(_simdi.AddSeconds(-1), "Tuzak", "Kritik", "yeni")) + "\n");
        sw.Restart(); var o2 = KureIsiklari.Ozetle(OlayDeposu.SonUyariKritik(KureIsiklari.Pencere, _simdi), _simdi); sw.Stop(); double artim = sw.Elapsed.TotalMilliseconds;
        Assert.Equal(ayr1 + 1, OlayDeposu.AyrisanSatirSayisi); Assert.Equal(k + 1, o2!.Kritik); Assert.Equal("yeni", o2.SonMesaj);
        _cikti.WriteLine($"[ÖLÇÜM] 10.000 satır — tam Oku(): {tamMs} ms | artımlı ilk (soğuk) okuma: {soguk} ms | değişmeyen dosya: {degismedi:F3} ms | 1 satır eklenince: {artim:F3} ms");
    }

    [Fact]
    public void YarimSatir_TamamlanincaOkunur_Rotasyonda_OnbellekSifirlanir()
    {
        string yol = OlayDeposu.Yol; string s1 = JsonSerializer.Serialize(new OlayKaydi(_simdi.AddMinutes(-1), "A", "Uyarı", "bir")), s2 = JsonSerializer.Serialize(new OlayKaydi(_simdi.AddMinutes(-2), "A", "Kritik", "iki"));
        File.WriteAllText(yol, s1 + "\n" + s2.Substring(0, 10));   // ikinci satır yarım yazılmış
        Assert.Single(OlayDeposu.SonUyariKritik(KureIsiklari.Pencere, _simdi));
        File.AppendAllText(yol, s2.Substring(10) + "\n"); Assert.Equal(2, OlayDeposu.SonUyariKritik(KureIsiklari.Pencere, _simdi).Count);
        // Rotasyon: dosya arşive taşınır, yenisi (daha küçük/farklı oluşturma zamanı) başlar.
        File.Move(yol, Path.Combine(_t, "arsiv.jsonl")); File.WriteAllText(yol, JsonSerializer.Serialize(new OlayKaydi(_simdi.AddSeconds(-5), "Sistem", "Uyarı", "yeni dosya")) + "\n");
        var l = OlayDeposu.SonUyariKritik(KureIsiklari.Pencere, _simdi); Assert.Single(l); Assert.Equal("yeni dosya", l[0].Mesaj);
    }

    [Fact]
    public void SayfaliTamTarama_EnYeniUsttedir_ToplamDogru_SonOlaylarTutarli()
    {
        Uret(2_500);
        var sw = Stopwatch.StartNew(); var (s0, top) = OlayDeposu.OkuSayfali(0, 1000); sw.Stop();
        Assert.Equal(2_500, top); Assert.Equal(1000, s0.Count); Assert.Equal("olay 2499", s0[0].Mesaj); Assert.Equal("olay 1500", s0[^1].Mesaj);
        var (s2, _) = OlayDeposu.OkuSayfali(2, 1000); Assert.Equal(500, s2.Count); Assert.Equal("olay 499", s2[0].Mesaj); Assert.Equal("olay 0", s2[^1].Mesaj);
        Assert.Equal(new[] { "olay 2499", "olay 2498", "olay 2497" }, OlayDeposu.SonOlaylar(3).Select(o => o.Mesaj));
        _cikti.WriteLine($"[ÖLÇÜM] 2.500 satırda ilk sayfa (1000): {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void ZincirDogrulama_Onbellekli_DegismeyendeAyniSonuc_DegisinceYenidenHesaplar()
    {
        OlayDeposu.Ekle("Sistem", "Bilgi", "bir"); OlayDeposu.Ekle("Sistem", "Bilgi", "iki");
        var a = OlayDeposu.Dogrula(true); Assert.True(a.Saglam); Assert.Same(a, OlayDeposu.Dogrula(true));   // değişmedi: önbellekten
        OlayDeposu.Ekle("Sistem", "Bilgi", "uc"); var b = OlayDeposu.Dogrula(true); Assert.NotSame(a, b); Assert.Equal(a.Zincirli + 1, b.Zincirli);
        // Bozma: tam doğrulama her zaman gerçek taramadır.
        string[] satirlar = File.ReadAllLines(OlayDeposu.Yol); satirlar[1] = satirlar[1].Replace("iki", "ikX"); File.WriteAllLines(OlayDeposu.Yol, satirlar);
        Assert.False(OlayDeposu.Dogrula().Saglam);
    }
}
