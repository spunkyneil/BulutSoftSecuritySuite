// Uzman Görünüm harita küresindeki bildirim ışığı testleri: yalnızca Uyarı ve Kritik olaylar ışık yakar, Bilgi yakmaz; 30 dakikadan eski olay söner.
using System.Text.Json;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class KureIsiklariTestleri
{
    static readonly DateTime Simdi = new(2026, 11, 22, 12, 0, 0);
    static OlayKaydi O(int dakikaOnce, string seviye, string mesaj = "x") => new(Simdi.AddMinutes(-dakikaOnce), "Nöbetçi", seviye, mesaj);

    [Fact]
    public void Yalnizca_Bilgi_Olaylari_Isik_Yakmaz()
    {
        Assert.Null(KureIsiklari.Ozetle(new[] { O(1, "Bilgi"), O(2, "Bilgi"), O(3, "") }, Simdi));
        Assert.Null(KureIsiklari.Ozetle(Array.Empty<OlayKaydi>(), Simdi));
    }

    [Fact]
    public void Uyari_Sari_Kritik_Kirmizi_Ve_Sayilar_Dogru()
    {
        var yalnizUyari = KureIsiklari.Ozetle(new[] { O(5, "Uyarı", "a"), O(6, "Bilgi") }, Simdi)!; Assert.Equal("Uyarı", yalnizUyari.SonSeviye); Assert.Equal(0, yalnizUyari.Kritik); Assert.Equal(1, yalnizUyari.Uyari);
        var ikisi = KureIsiklari.Ozetle(new[] { O(5, "Uyarı", "eski uyarı"), O(2, "Kritik", "yeni kritik"), O(9, "Kritik", "daha eski"), O(1, "Bilgi", "yok say") }, Simdi)!;
        Assert.Equal("Kritik", ikisi.SonSeviye); Assert.Equal(2, ikisi.Kritik); Assert.Equal(1, ikisi.Uyari); Assert.Equal("yeni kritik", ikisi.SonMesaj);
    }

    [Fact]
    public void Otuz_Dakikadan_Eski_Olay_Isigi_Sondurur_Gelecektekiler_Sayilmaz()
    {
        Assert.NotNull(KureIsiklari.Ozetle(new[] { O(29, "Kritik") }, Simdi));
        Assert.Null(KureIsiklari.Ozetle(new[] { O(31, "Kritik"), O(500, "Uyarı") }, Simdi));
        Assert.Null(KureIsiklari.Ozetle(new[] { new OlayKaydi(Simdi.AddHours(3), "Nöbetçi", "Kritik", "saat kayması") }, Simdi));
    }

    [Fact]
    public void Mesaj_Temizlenir_Kisaltilir_Ve_Json_Guvenlidir()
    {
        string kirli = "satir1\r\nsatir2\u001b[31m" + new string('y', 400);
        var o = KureIsiklari.Ozetle(new[] { O(1, "Kritik", kirli) }, Simdi)!;
        Assert.DoesNotContain(o.SonMesaj, char.IsControl); Assert.True(o.SonMesaj.Length <= 140); Assert.EndsWith("…", o.SonMesaj);
        string json = KureIsiklari.Json(o); using var d = JsonDocument.Parse(json); Assert.Equal("Kritik", d.RootElement.GetProperty("seviye").GetString()); Assert.Equal(1, d.RootElement.GetProperty("kritik").GetInt32());
        Assert.Equal("null", KureIsiklari.Json(null));
        // Betik kapatma/etiket karakterleri sayfaya ham gitmemeli.
        string tehlikeli = KureIsiklari.Json(new IsikOzeti(1, 0, "Kritik", "</script><img src=x onerror=1>", Simdi)); Assert.DoesNotContain('<', tehlikeli); Assert.DoesNotContain('>', tehlikeli);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kure_Sayfasi_Isik_Koprusunu_Ve_Tiklamayi_Icerir()
    {
        string? d = AppContext.BaseDirectory; string html = ""; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "LabDenetim", "Kure", "kure.html"); if (File.Exists(y)) { html = File.ReadAllText(y); break; } }
        Assert.Contains("window.bsIsik", html); Assert.Contains("isik-tikla", html);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kure_Sayfasi_Ulke_Adlarini_Kure_Uzerine_Yazar_Ve_Imlec_Disinda_Doner()
    {
        string? d = AppContext.BaseDirectory; string html = ""; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) { var y = Path.Combine(d, "LabDenetim", "Kure", "kure.html"); if (File.Exists(y)) { html = File.ReadAllText(y); break; } }
        Assert.Contains("etiketSirasi", html); Assert.Contains("window.bsImlec", html); Assert.Contains("OTO_DONUS * dt", html);
        // Ülke adı artık imleç yanında açıklama olarak çıkmamalı.
        Assert.DoesNotContain("satirlar = [f.ad]", html);
        // Ülkeden ülkeye yayların sayısı arttırıldı (Güvende modunda en çok 70 yay, hub sayısı 70'ten fazla) ve titremeyi azaltan hız önlemleri yerinde.
        Assert.Contains("maxArcs: 70", html); Assert.True(System.Text.RegularExpressions.Regex.Matches(html, @"iso: '\d{3}' \}").Count >= 70);
        Assert.Contains("etiketAlfa", html); Assert.Contains("gorunurHublar", html);
        // Her karede pahalı gölge bulanıklığı etiketlerde ve yay uçlarında kullanılmamalı.
        Assert.DoesNotContain("ctx.shadowBlur = 3;", html); Assert.DoesNotContain("ctx.shadowBlur = isDark ? 10 : 6", html);
    }
}
