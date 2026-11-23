// BrandConfig testleri: varsayılanlar mevcut ürün değerleridir; brand.json yalnızca GEÇERLİ alanları değiştirir, geçersiz/bozuk/büyük dosya varsayılana döner.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class BrandConfigTestleri
{
    [Fact]
    public void Varsayilanlar_MevcutUrunDegerleridir()
    {
        var b = BrandConfig.Varsayilan;
        Assert.Equal("BulutSoft Security Suite", b.UrunAdi); Assert.Equal("BulutSoft Güvenlik Teknolojileri", b.SirketAdi); Assert.Equal("BulutSoft", b.KisaBaslik);
        Assert.Equal("bulutsoftsecurity@info.com", b.DestekEposta); Assert.Equal("BulutSoft", b.VeriKlasoruAdi); Assert.Equal("#6366F1", b.VurguRengi); Assert.Empty(b.Uyarilar);
    }

    [Fact]
    public void Oku_GecerliAlanlariUzerineYazar_DigerleriVarsayilanKalir()
    {
        var b = BrandConfig.Oku("{ \"urunAdi\": \" Acme Shield \", \"KISABASLIK\": \"Acme\", \"vurguRengi\": \"#ff8800\", \"destekEposta\": \"destek@acme.example\", \"veriKlasoruAdi\": \"AcmeShield\" }");
        Assert.Equal("Acme Shield", b.UrunAdi); Assert.Equal("Acme", b.KisaBaslik); Assert.Equal("#FF8800", b.VurguRengi); Assert.Equal("destek@acme.example", b.DestekEposta); Assert.Equal("AcmeShield", b.VeriKlasoruAdi);
        Assert.Equal(BrandConfig.VarsayilanSirketAdi, b.SirketAdi); Assert.Empty(b.Uyarilar);
    }

    [Theory]
    [InlineData("{\"vurguRengi\":\"kirmizi\"}", "VurguRengi")]
    [InlineData("{\"vurguRengi\":\"#12345\"}", "VurguRengi")]
    [InlineData("{\"destekEposta\":\"adres-degil\"}", "DestekEposta")]
    [InlineData("{\"veriKlasoruAdi\":\"..\\\\Windows\"}", "VeriKlasoruAdi")]
    [InlineData("{\"veriKlasoruAdi\":\"a/b\"}", "VeriKlasoruAdi")]
    [InlineData("{\"veriKlasoruAdi\":\"CON\"}", "VeriKlasoruAdi")]
    [InlineData("{\"veriKlasoruAdi\":\"nokta.\"}", "VeriKlasoruAdi")]
    [InlineData("{\"urunAdi\":\"   \"}", "UrunAdi")]
    [InlineData("{\"kisaBaslik\":\"bu kisa baslik otuz karakterden kesinlikle daha uzundur\"}", "KisaBaslik")]
    public void Oku_GecersizAlan_VarsayilanaDonerVeUyariVerir(string json, string alan)
    {
        var b = BrandConfig.Oku(json); var v = BrandConfig.Varsayilan;
        Assert.Contains(b.Uyarilar, u => u.StartsWith(alan, StringComparison.Ordinal));
        Assert.Equal(v.UrunAdi, b.UrunAdi); Assert.Equal(v.KisaBaslik, b.KisaBaslik); Assert.Equal(v.VeriKlasoruAdi, b.VeriKlasoruAdi); Assert.Equal(v.VurguRengi, b.VurguRengi); Assert.Equal(v.DestekEposta, b.DestekEposta);
    }

    [Fact]
    public void Oku_DenetimKarakteriIcerenMetin_Reddedilir()
    {
        var b = BrandConfig.Oku("{\"urunAdi\":\"Satir\\nSonu\"}");
        Assert.Equal(BrandConfig.VarsayilanUrunAdi, b.UrunAdi); Assert.Single(b.Uyarilar);
    }

    [Fact]
    public void Oku_BozukJson_VarsayilanDonerVeCokmez()
    {
        var b = BrandConfig.Oku("{ bu json degil");
        Assert.Equal(BrandConfig.VarsayilanUrunAdi, b.UrunAdi); Assert.NotEmpty(b.Uyarilar);
    }

    [Fact]
    public void DosyadanYukle_YokVeBuyukDosya_VarsayilanDoner_GecerliDosyaOkunur()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "brand_test_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(klasor);
        try
        {
            Assert.Equal(BrandConfig.VarsayilanUrunAdi, BrandConfig.DosyadanYukle(Path.Combine(klasor, "yok.json")).UrunAdi);
            string buyuk = Path.Combine(klasor, "buyuk.json"); File.WriteAllText(buyuk, "{\"urunAdi\":\"Buyuk\"}" + new string(' ', 17 * 1024));
            Assert.Equal(BrandConfig.VarsayilanUrunAdi, BrandConfig.DosyadanYukle(buyuk).UrunAdi);
            string iyi = Path.Combine(klasor, "brand.json"); File.WriteAllText(iyi, "{\"urunAdi\":\"Acme Shield\"}");
            Assert.Equal("Acme Shield", BrandConfig.DosyadanYukle(iyi).UrunAdi);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void VeriKlasorleri_MarkaAdindanTurer_VarsayilanMevcutYoldur()
    {
        string eski = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BulutSoft");
        Assert.Equal(eski, BrandConfig.Varsayilan.RoamingYolu);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BulutSoft"), BrandConfig.Varsayilan.OrtakYolu);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Acme"), new BrandConfig { VeriKlasoruAdi = "Acme" }.RoamingYolu);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void KaynakKodda_VeriKlasoruAdiSabitYazilmamis_Cekirdek_Arayuz_Hizmet()
    {
        string? d = AppContext.BaseDirectory, kok = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) { kok = d; break; }
        if (kok == null) return;   // kaynak ağacı yoksa atla
        var kotu = new List<string>();
        foreach (var p in new[] { "LabDenetim", "BulutSoft.Core", "BulutSoftGuardService" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, p), "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || Path.GetFileName(f) == "BrandConfig.cs") continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"SpecialFolder\.(Common|Local)?ApplicationData\)\s*,\s*""BulutSoft""")) kotu.Add(Path.GetFileName(f));
            }
        Assert.True(kotu.Count == 0, "Veri klasörü adı BrandConfig üzerinden alınmalı: " + string.Join(", ", kotu));
    }
}
