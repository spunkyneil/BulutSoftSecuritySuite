// Kullanıcı etkinlik günlüğü: eylemler "KULLANICI -> ad: ..." olarak yazılır; kayıt yalnızca o kullanıcıya aittir (başka kullanıcının adı geçse bile karışmaz); oturum olayları da kullanıcıya eşlenir;
// Olay Zaman Çizgisi bu iki türü gizler. Oturum yoksa hiçbir şey yazılmaz.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class KullaniciGunluguTestleri : IDisposable
{
    public void Dispose() => ServisBaglami.Sifirla();

    private sealed class SahteOturum : IOturumBaglami { public Kullanici? Aktif { get; set; } public Rol? Rol => Aktif?.Rol; }
    private sealed class SahteYazici : IOlayYazici { public readonly List<(string Tur, string Seviye, string Mesaj)> Yazilan = new(); public void Ekle(string tur, string seviye, string mesaj) => Yazilan.Add((tur, seviye, mesaj)); }

    [Fact]
    public void Yaz_OturumVarsa_KullaniciAdiyla_BilgiSeviyesindeYazar_OturumYoksaYazmaz()
    {
        var y = new SahteYazici(); var o = new SahteOturum(); ServisBaglami.OlayYazici = y; ServisBaglami.Oturum = o;
        KullaniciGunlugu.Yaz("Tarama başlattı"); Assert.Empty(y.Yazilan);   // oturum yok
        o.Aktif = new Kullanici { Ad = "admin", Rol = Rol.Yonetici }; KullaniciGunlugu.Yaz("Nöbetçi Modu'nu açtı"); KullaniciGunlugu.Yaz("  ");
        var k = Assert.Single(y.Yazilan); Assert.Equal(("Kullanıcı", "Bilgi", "KULLANICI -> admin: Nöbetçi Modu'nu açtı"), k);
    }

    [Fact]
    public void KullaniciyaAitMi_YalnizcaKendiKayitlari_OturumOlaylariAdaGoreEslenir()
    {
        var t = DateTime.Now;
        Assert.True(KullaniciGunlugu.KullaniciyaAitMi(new OlayKaydi(t, "Kullanıcı", "Bilgi", "KULLANICI -> admin: Tarama başlattı"), "admin"));
        Assert.False(KullaniciGunlugu.KullaniciyaAitMi(new OlayKaydi(t, "Kullanıcı", "Bilgi", "KULLANICI -> deneme: Tarama başlattı"), "admin"));
        Assert.False(KullaniciGunlugu.KullaniciyaAitMi(new OlayKaydi(t, "Kullanıcı", "Bilgi", "KULLANICI -> deneme: admin sayfasını açtı"), "admin"));   // eylem metninde ad geçmesi aidiyeti değiştirmez
        Assert.True(KullaniciGunlugu.KullaniciyaAitMi(new OlayKaydi(t, "Oturum", "Bilgi", "OTURUM -> Giriş yapıldı: admin (Yönetici)"), "admin"));
        Assert.False(KullaniciGunlugu.KullaniciyaAitMi(new OlayKaydi(t, "Oturum", "Bilgi", "OTURUM -> Giriş yapıldı: administrator (Yönetici)"), "admin"));   // ad başka bir adın parçası değil
        Assert.False(KullaniciGunlugu.KullaniciyaAitMi(new OlayKaydi(t, "USB", "Bilgi", "USB KALKANI -> admin"), "admin"));   // başka türler kullanıcı günlüğüne girmez
    }

    [Fact]
    public void Metin_OnekleriAtar_ZamanCizgisiOturumVeKullaniciTurunuGizler()
    {
        var t = DateTime.Now; Assert.Equal("Tarama başlattı", KullaniciGunlugu.Metin(new OlayKaydi(t, "Kullanıcı", "Bilgi", "KULLANICI -> admin: Tarama başlattı"), "admin"));
        Assert.Equal("Oturum: Oturum kapatıldı: admin", KullaniciGunlugu.Metin(new OlayKaydi(t, "Oturum", "Bilgi", "OTURUM -> Oturum kapatıldı: admin"), "admin"));
        Assert.True(KullaniciGunlugu.ZamanCizgisindeGizli("Oturum")); Assert.True(KullaniciGunlugu.ZamanCizgisindeGizli("Kullanıcı")); Assert.False(KullaniciGunlugu.ZamanCizgisindeGizli("Nöbetçi")); Assert.False(KullaniciGunlugu.ZamanCizgisindeGizli("USB"));
    }

    [Fact]
    public void KullaniciMenusu_HerOgeTamBirKez_ProgramdanCikisEnSondaAyriGrupta_DemoYaylarYok()
    {
        // Davranış testi (D6): menü içeriği veri olarak okunur; yeni bir KullaniciMenuOgesi menüye eklenmeden bırakılamaz.
        var tumu = GlobalHeader.MenuGruplari.SelectMany(g => g).ToList();
        foreach (var oge in Enum.GetValues<KullaniciMenuOgesi>()) Assert.Single(tumu, x => x.Oge == oge);
        var son = GlobalHeader.MenuGruplari[^1]; Assert.Single(son); Assert.Equal(("Programdan Çıkış", KullaniciMenuOgesi.Cikis), son[0]);
        Assert.False(GorselAyarlar.DemoYaylar);   // örnek yaylar hiçbir koşulda açılmaz
    }
}
