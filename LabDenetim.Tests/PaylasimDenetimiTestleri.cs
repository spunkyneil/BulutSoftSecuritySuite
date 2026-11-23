// Ağ paylaşımı denetimi testleri: örnek kayıtlarla mantık, halk dili karşılığı ve gerçek okumanın çökmediği.
using LabDenetim.Moduller;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class PaylasimDenetimiTestleri
{
    // Kısa kayıt üretici.
    static PaylasimKaydi K(string ad, string yol, uint tur = 0, string ac = "") => new(ad, yol, tur, ac);

    [Fact]
    public void Sadece_Yonetimsel_Paylasim_Varsa_Bilgi_Notu()
    {
        // C$, ADMIN$ ve IPC$ normaldir; yazıcı paylaşımı da sayılmaz.
        var s = PaylasimDenetimi.Degerlendir(new[] { K("C$", "C:\\", 2147483648), K("ADMIN$", "C:\\Windows", 2147483648), K("IPC$", "", 3), K("Yazici", "", 1) });
        Assert.Single(s); Assert.Equal(Onem.Bilgi, s[0].Onem);
    }

    [Fact]
    public void Surucu_Koku_Paylasimi_Yuksek()
    {
        var s = PaylasimDenetimi.Degerlendir(new[] { K("Disk", "D:\\"), K("Hepsi", "C:") });
        var b = s.Single(x => x.Baslik.Contains("sürücünün tamamı")); Assert.Equal(Onem.Yuksek, b.Onem); Assert.Contains("Disk", b.Detay); Assert.Contains("Hepsi", b.Detay);
    }

    [Theory]
    [InlineData("C:\\Windows")] [InlineData("C:\\Users")] [InlineData("C:\\Users\\Ali\\Masaüstü")] [InlineData("D:\\Program Files\\X")] [InlineData("c:\\WINDOWS\\")]
    public void Sistem_Klasoru_Paylasimi_Yuksek(string yol)
    {
        var s = PaylasimDenetimi.Degerlendir(new[] { K("Paylas", yol) }); Assert.Contains(s, x => x.Onem == Onem.Yuksek && x.Baslik.Contains("Windows veya kullanıcı"));
    }

    [Fact]
    public void Sıradan_Klasor_Orta_Ve_Izin_Onerisi_Var()
    {
        var s = PaylasimDenetimi.Degerlendir(new[] { K("Belgeler", "D:\\Ortak", 0, "Ortak belgeler") }); var b = Assert.Single(s); Assert.Equal(Onem.Orta, b.Onem); Assert.Contains("Okuma", b.Oneri); Assert.Contains("Ortak belgeler", b.Detay);
        // "Users2" gibi benzer adlar hassas sayılmaz.
        Assert.DoesNotContain(PaylasimDenetimi.Degerlendir(new[] { K("U", "C:\\Users2") }), x => x.Onem == Onem.Yuksek);
    }

    [Fact]
    public void Gizli_Paylasim_Ayri_Uyarilir()
    {
        var s = PaylasimDenetimi.Degerlendir(new[] { K("Yedek$", "D:\\Yedek") }); Assert.Contains(s, x => x.Baslik.Contains("gizlenmiş"));
    }

    [Fact]
    public void Cok_Paylasim_Listesi_Kisaltilir()
    {
        var s = PaylasimDenetimi.Degerlendir(Enumerable.Range(1, 15).Select(i => K("P" + i, "D:\\K" + i))); var b = Assert.Single(s); Assert.Contains("5 paylaşım daha", b.Detay); Assert.Contains("15 klasör", b.Baslik);
    }

    [Fact]
    public void Halk_Dili_Ozel_Karsilik_Var()
    {
        foreach (var bas in new[] { "Bir sürücünün tamamı ağa paylaşılmış", "Windows veya kullanıcı klasörleri ağa paylaşılmış", "3 klasör ağa paylaşılmış", "Adı gizlenmiş ($ ile biten) paylaşım var" })
        { var h = HalkDiliCevirici.Cevir("Paylaşım", bas, "ayrıntı", Onem.Orta); Assert.True(h.Ozel, bas); Assert.NotEmpty(h.Zarar); }
    }

    [Fact]
    public void Gercek_Okuma_Cokmez()
    {
        // Bu bilgisayarın paylaşım listesi yalnızca okunur; hata atmamalı (WMI yoksa Hepsi'deki koruma yakalar).
        try { Assert.NotNull(PaylasimDenetimi.Denetle()); } catch (System.Management.ManagementException hkt1) { HataKayit.Sessiz(hkt1, "PaylasimDenetimiTestleri.temizlik"); }
    }
}
