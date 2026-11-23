// Pilot (salt okunur) mod ve veri akışı belgesi testleri. Gerçek ProgramData klasörüne dokunmaz.
using System.Text.RegularExpressions;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class PilotVeVeriAkisiTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_pilot_" + Guid.NewGuid().ToString("N")); private readonly string _eski = PilotMod.MakineKlasoru; private readonly string? _env = Environment.GetEnvironmentVariable(PilotMod.OrtamDegiskeni);
    public PilotVeVeriAkisiTestleri() { Directory.CreateDirectory(_t); PilotMod.MakineKlasoru = _t; Environment.SetEnvironmentVariable(PilotMod.OrtamDegiskeni, null); }
    public void Dispose() { PilotMod.MakineKlasoru = _eski; Environment.SetEnvironmentVariable(PilotMod.OrtamDegiskeni, _env); try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "PilotVeVeriAkisiTestleri.temizlik"); } }

    [Fact]
    public void Varsayilan_Kapali_Ve_Yonetici_Sistem_Degistirebilir()
    {
        Assert.False(PilotMod.SaltOkunur);
        Assert.True(Yetki.Izinli(Rol.Yonetici, Eylem.SistemDegistir));
    }

    // En az yetki ilkesi: yeni hesap varsayılan olarak İzleyici, oturumsuz kullanıcı hiçbir şey yapamaz.
    [Fact]
    public void Varsayilan_Rol_En_Az_Yetkili_Ve_Oturumsuz_Hicbir_Sey_Yapamaz()
    {
        Assert.Equal(Rol.Izleyici, new Kullanici().Rol);
        foreach (Eylem e in Enum.GetValues<Eylem>()) Assert.False(Yetki.Izinli(null, e));
        Assert.False(Yetki.Izinli(Rol.Izleyici, Eylem.Tarama)); Assert.False(Yetki.Izinli(Rol.Analist, Eylem.SistemDegistir));
    }

    [Fact]
    public void Yardim_Metni_Gizlilik_Komutunu_Anlatir() => Assert.Contains("--gizlilik", TaramaCalistirici.YardimMetni);

    [Fact]
    public void Bayrak_Dosyasi_Modu_Acar_Ve_Yonetici_Bile_Sistem_Degistiremez()
    {
        File.WriteAllText(Path.Combine(_t, PilotMod.BayrakAdi), "");
        Assert.True(PilotMod.SaltOkunur);
        Assert.False(Yetki.Izinli(Rol.Yonetici, Eylem.SistemDegistir));
        Assert.Contains("SALT OKUNUR", Yetki.RedMesaji(Rol.Yonetici, Eylem.SistemDegistir));
        // Görme, rapor, tarama ve yönetim (kullanıcı hesapları) etkilenmez.
        Assert.True(Yetki.Izinli(Rol.Yonetici, Eylem.Tarama)); Assert.True(Yetki.Izinli(Rol.Yonetici, Eylem.Rapor)); Assert.True(Yetki.Izinli(Rol.Yonetici, Eylem.Yonetim));
    }

    [Theory]
    [InlineData("1")] [InlineData("true")] [InlineData("TRUE")]
    public void Ortam_Degiskeni_Modu_Acar(string deger)
    {
        Environment.SetEnvironmentVariable(PilotMod.OrtamDegiskeni, deger); Assert.True(PilotMod.SaltOkunur);
    }

    [Fact]
    public void Ortam_Degiskeni_Baska_Deger_Modu_Acmaz()
    {
        Environment.SetEnvironmentVariable(PilotMod.OrtamDegiskeni, "0"); Assert.False(PilotMod.SaltOkunur);
    }

    [Fact]
    public async Task Yonetici_Komutu_Calismaz_Pilot_Modda()
    {
        File.WriteAllText(Path.Combine(_t, PilotMod.BayrakAdi), "");
        var (ok, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync("echo test");   // mod kapalı olsaydı UAC penceresi açılırdı
        Assert.False(ok); Assert.Contains("SALT OKUNUR", hata);
    }

    [Fact]
    public void Surec_Sonlandirma_Pilot_Modda_Reddedilir()
    {
        File.WriteAllText(Path.Combine(_t, PilotMod.BayrakAdi), "");
        var e = Assert.Throws<InvalidOperationException>(() => SurecAvcisi.Sonlandir(Environment.ProcessId, "testhost"));
        Assert.Contains("SALT OKUNUR", e.Message);
    }

    [Fact]
    public void Belge_Her_Dis_Sunucuyu_Ve_Mod_Durumunu_Yazar()
    {
        var m = VeriAkisi.Metin(true);
        foreach (var k in VeriAkisi.Dis) Assert.Contains(k.Sunucu, m);
        Assert.Contains("AÇIK", m); Assert.Contains("kapalı", VeriAkisi.Metin(false));
        Assert.Contains(PilotMod.OrtamDegiskeni, m); Assert.Contains("hukuki metin değildir", m);
        Assert.Equal(new Uri(LabDenetim.V2.KevIndirici.Adres).Host, VeriAkisi.Dis[0].Sunucu);
    }

    // Kaynak kodda geçen her dış adres belgede olmalı: yeni bir dış bağlantı eklenirse bu test belgeyi güncellemeye zorlar.
    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kodda_Belgelenmemis_Dis_Adres_Yok()
    {
        string? dizin = AppContext.BaseDirectory; string? kaynak = null;
        for (int i = 0; i < 8 && dizin != null; i++, dizin = Path.GetDirectoryName(dizin)) if (File.Exists(Path.Combine(dizin, "LabDenetim", "LabDenetim.csproj"))) { kaynak = Path.Combine(dizin, "LabDenetim"); break; }
        if (kaynak == null) return;   // kaynak ağacı yoksa (yalnızca derlenmiş çıktı) atla
        var belgeli = VeriAkisi.Dis.Select(k => k.Sunucu).ToList(); var eksik = new List<string>();
        foreach (var f in new[] { kaynak, Path.Combine(Path.GetDirectoryName(kaynak)!, "BulutSoft.Core") }.Where(Directory.Exists).SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories)))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"https?://([A-Za-z0-9.\-]+)"))
            {
                var host = m.Groups[1].Value;
                if (VeriAkisi.AgDisiAdresler.Any(a => host.StartsWith(a, StringComparison.OrdinalIgnoreCase) || host == a.TrimEnd('.'))) continue;
                if (belgeli.Any(b => host.Equals(b, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + b, StringComparison.OrdinalIgnoreCase))) continue;
                eksik.Add(Path.GetFileName(f) + ": " + host);
            }
        }
        Assert.True(eksik.Count == 0, "Belgelenmemiş dış adresler: " + string.Join(", ", eksik.Distinct()));
    }
}
