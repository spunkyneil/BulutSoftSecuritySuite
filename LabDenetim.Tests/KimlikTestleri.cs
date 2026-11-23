// Bu dosya kimlik doğrulama, roller ve yetki testlerini içerir (geçici klasörde; gerçek hesaplara dokunmaz).
namespace LabDenetim.Tests;

// Kimlik testleri.
[Collection("Veri")]
public class KimlikTestleri : IDisposable
{
    // Geçici klasör ve eski ayarlar.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_kimlik_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol; private readonly int _eskiTekrar = KimlikDeposu.VarsayilanTekrar;
    // Kurulum: hızlı testler için tur sayısını düşür.
    public KimlikTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); KimlikDeposu.VarsayilanTekrar = 1000; Oturum.Aktif = null; }
    // Temizlik.
    public void Dispose() { BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; KimlikDeposu.VarsayilanTekrar = _eskiTekrar; Oturum.Aktif = null; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "KimlikTestleri.temizlik"); } }

    // Hazır kurulum.
    private static void Kur() => Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);

    // Kurulum kuralları.
    [Fact] public void Kurulum_Kurallari()
    {
        Assert.Equal(KimlikDurumu.Yok, KimlikDeposu.Durum());
        Assert.False(KimlikDeposu.Kur("kisa", "ibrahim", "Yonetici1234").Basarili);
        Assert.False(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "KurulumSifre9").Basarili);
        Assert.False(KimlikDeposu.Kur("KurulumSifre9", "Ib", "Yonetici1234").Basarili);
        Kur(); Assert.Equal(KimlikDurumu.Hazir, KimlikDeposu.Durum()); Assert.False(KimlikDeposu.Kur("BaskaSifre12", "x1y2z3", "Baska12345").Basarili);
    }
    // Parolalar düz yazı saklanmamalı.
    [Fact] public void Parola_DuzYaziDegil() { Kur(); string d = File.ReadAllText(KimlikDeposu.Yol); Assert.DoesNotContain("Yonetici1234", d); Assert.DoesNotContain("KurulumSifre9", d); }
    // Doğru/yanlış giriş.
    [Fact] public void Giris()
    {
        Kur(); Assert.True(KimlikDeposu.GirisYap("ibrahim", "Yonetici1234").Basarili); Assert.True(KimlikDeposu.GirisYap("IBRAHIM", "Yonetici1234").Basarili);
        var r = KimlikDeposu.GirisYap("ibrahim", "yanlis"); Assert.False(r.Basarili); Assert.Equal(r.Mesaj, KimlikDeposu.GirisYap("yok", "x").Mesaj);
    }
    // 5 hatadan sonra kilit; doğru parola da kilitliyken reddedilir.
    [Fact] public void Kilit()
    {
        Kur(); for (int i = 0; i < 5; i++) KimlikDeposu.GirisYap("ibrahim", "yanlis");
        var r = KimlikDeposu.GirisYap("ibrahim", "Yonetici1234"); Assert.False(r.Basarili); Assert.Contains("saniye", r.Mesaj);
    }
    // Kilit süresi artar ve 15 dakikayı aşmaz.
    [Fact] public void KilitSuresi_Artar() { Assert.Equal(TimeSpan.Zero, KimlikDeposu.KilitSuresi(4)); Assert.Equal(30, KimlikDeposu.KilitSuresi(5).TotalSeconds); Assert.Equal(60, KimlikDeposu.KilitSuresi(6).TotalSeconds); Assert.Equal(900, KimlikDeposu.KilitSuresi(50).TotalSeconds); }
    // Kullanıcı ekleme kurulum şifresi ister.
    [Fact] public void Ekle_KurulumSifresiIster()
    {
        Kur(); Assert.False(KimlikDeposu.KullaniciEkle("ibrahim", "YanlisKurulum1", "ayse", "Analist12345", Rol.Analist).Basarili);
        Assert.True(KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Analist12345", Rol.Analist).Basarili);
        Assert.False(KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Analist12345", Rol.Analist).Basarili);
        Assert.True(KimlikDeposu.GirisYap("ayse", "Analist12345").Basarili);
    }
    // Yönetici olmayan yönetim işlemi yapamaz (rol dosyadan doğrulanır).
    [Fact] public void Analist_YonetemEzmez()
    {
        Kur(); KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Analist12345", Rol.Analist);
        Assert.False(KimlikDeposu.KullaniciEkle("ayse", "KurulumSifre9", "can", "Izleyici12345", Rol.Izleyici).Basarili);
    }
    // Son yönetici korunur.
    [Fact] public void SonYonetici_Korunur()
    {
        Kur(); Assert.False(KimlikDeposu.RolDegistir("ibrahim", "KurulumSifre9", "ibrahim", Rol.Analist).Basarili); Assert.False(KimlikDeposu.Sil("ibrahim", "KurulumSifre9", "ibrahim").Basarili); Assert.False(KimlikDeposu.DevreDegistir("ibrahim", "KurulumSifre9", "ibrahim", true).Basarili);
    }
    // Devre dışı hesap giremez.
    [Fact] public void DevreDisi_Giremez()
    {
        Kur(); KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Analist12345", Rol.Analist); Assert.True(KimlikDeposu.DevreDegistir("ibrahim", "KurulumSifre9", "ayse", true).Basarili);
        Assert.False(KimlikDeposu.GirisYap("ayse", "Analist12345").Basarili); Assert.True(KimlikDeposu.DevreDegistir("ibrahim", "KurulumSifre9", "ayse", false).Basarili); Assert.True(KimlikDeposu.GirisYap("ayse", "Analist12345").Basarili);
    }
    // Parola değiştirme ve sıfırlama.
    [Fact] public void ParolaDegistirVeSifirla()
    {
        Kur(); Assert.False(KimlikDeposu.SifreDegistir("ibrahim", "yanlis", "YeniParola123").Basarili); Assert.True(KimlikDeposu.SifreDegistir("ibrahim", "Yonetici1234", "YeniParola123").Basarili);
        Assert.False(KimlikDeposu.GirisYap("ibrahim", "Yonetici1234").Basarili); Assert.True(KimlikDeposu.GirisYap("ibrahim", "YeniParola123").Basarili);
        Assert.True(KimlikDeposu.SifreSifirla("ibrahim", "KurulumSifre9", "ibrahim", "SifirlaParola9").Basarili); Assert.True(KimlikDeposu.GirisYap("ibrahim", "SifirlaParola9").Basarili);
    }
    // Kurulum şifresi değişimi.
    [Fact] public void KurulumSifresiDegistir()
    {
        Kur(); Assert.False(KimlikDeposu.KurulumSifresiDegistir("ibrahim", "Yanlis99999", "YeniKurulum77").Basarili);
        Assert.False(KimlikDeposu.KurulumSifresiDegistir("ibrahim", "KurulumSifre9", "Yonetici1234").Basarili);
        Assert.True(KimlikDeposu.KurulumSifresiDegistir("ibrahim", "KurulumSifre9", "YeniKurulum77").Basarili);
        Assert.False(KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Analist12345", Rol.Analist).Basarili); Assert.True(KimlikDeposu.KullaniciEkle("ibrahim", "YeniKurulum77", "ayse", "Analist12345", Rol.Analist).Basarili);
    }
    // Bozuk dosya "yok" sayılmaz (sessiz yeniden kurulum engeli).
    [Fact] public void BozukDosya_Bozuk() { File.WriteAllText(KimlikDeposu.Yol, "{bozuk"); Assert.Equal(KimlikDurumu.Bozuk, KimlikDeposu.Durum()); }
    // Rol-yetki tablosu.
    [Theory, InlineData((int)Rol.Izleyici, (int)Eylem.Rapor, true), InlineData((int)Rol.Izleyici, (int)Eylem.Tarama, false), InlineData((int)Rol.Analist, (int)Eylem.Tarama, true), InlineData((int)Rol.Analist, (int)Eylem.SistemDegistir, false), InlineData((int)Rol.Analist, (int)Eylem.Yonetim, false), InlineData((int)Rol.Yonetici, (int)Eylem.Yonetim, true)]
    public void Yetki_Tablosu(int r, int e, bool b) => Assert.Equal(b, Yetki.Izinli((Rol)r, (Eylem)e));
    // Oturum yoksa hiçbir yetki yok.
    [Fact] public void OturumYok_YetkiYok() => Assert.False(Yetki.Izinli(null, Eylem.Gorme));
    // Parola politikası.
    [Theory, InlineData("kisa1", false), InlineData("sadeceHarflerr", false), InlineData("1234567890", false), InlineData("GucluParola42", true), InlineData("Parola12", true), InlineData("Parol12", false), InlineData("aaaaaaaaaa1", false)]
    public void Politika(string s, bool ok) => Assert.Equal(ok, SifrePolitikasi.Kontrol(s) == null);
    // Port grupları (sensör taraması).
    [Fact] public void PortGruplari()
    {
        Assert.Equal(new[] { "22,80,443" }, SensorTaramaYardimcisi.PortGruplari(new[] { 443, 22, 80, 22 }));
        Assert.Equal(new[] { "1-1024,3306" }, SensorTaramaYardimcisi.PortGruplari(Enumerable.Range(1, 1024).Append(3306).ToArray()));
        Assert.All(SensorTaramaYardimcisi.PortGruplari(Enumerable.Range(1, 400).Select(x => x * 100 % 65000 + 1).ToArray()), g => Assert.True(g.Length <= 100));
    }
}

// Şifremi unuttum (kurulum şifresiyle parola kurtarma) testleri.
[Collection("Veri")]
public class KurtarmaTestleri : IDisposable
{
    // Geçici klasör ve eski ayarlar.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_kurtar_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol; private readonly int _eskiTekrar = KimlikDeposu.VarsayilanTekrar;
    // Kurulum.
    public KurtarmaTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); KimlikDeposu.VarsayilanTekrar = 1000; KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234"); }
    // Temizlik.
    public void Dispose() { BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; KimlikDeposu.VarsayilanTekrar = _eskiTekrar; try { Directory.Delete(_t, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "KimlikTestleri.temizlik"); } }

    // Doğru kurulum şifresiyle sıfırlanır; eski parola geçersiz olur; kilit de açılır.
    [Fact] public void Kurtar_Basarili()
    {
        for (int i = 0; i < 5; i++) KimlikDeposu.GirisYap("ibrahim", "yanlis");
        Assert.True(KimlikDeposu.SifreKurtar("ibrahim", "KurulumSifre9", "YeniParola4567").Basarili);
        Assert.False(KimlikDeposu.GirisYap("ibrahim", "Yonetici1234").Basarili); Assert.True(KimlikDeposu.GirisYap("ibrahim", "YeniParola4567").Basarili);
    }
    // Yanlış kurulum şifresi reddedilir; parola değişmez.
    [Fact] public void Kurtar_YanlisKurulum() { Assert.False(KimlikDeposu.SifreKurtar("ibrahim", "Yanlis1234567", "YeniParola4567").Basarili); Assert.True(KimlikDeposu.GirisYap("ibrahim", "Yonetici1234").Basarili); }
    // Zayıf yeni parola reddedilir; olmayan hesap anlaşılır hata verir.
    [Fact] public void Kurtar_Kurallar() { Assert.False(KimlikDeposu.SifreKurtar("ibrahim", "KurulumSifre9", "kisa").Basarili); Assert.False(KimlikDeposu.SifreKurtar("yokkimse", "KurulumSifre9", "YeniParola4567").Basarili); }
    // Kurulum şifresi kaba kuvvetle denenemez: 5 yanlıştan sonra doğrusu da kilitlenir.
    [Fact] public void Kurtar_KurulumKilidi()
    {
        for (int i = 0; i < 5; i++) KimlikDeposu.SifreKurtar("ibrahim", "Yanlis1234567", "YeniParola4567");
        var r = KimlikDeposu.SifreKurtar("ibrahim", "KurulumSifre9", "YeniParola4567"); Assert.False(r.Basarili); Assert.Contains("saniye", r.Mesaj);
    }
    // Parola gücü göstergesi.
    [Theory, InlineData("abc", "Zayıf"), InlineData("GucluParola42!", "Güçlü")]
    public void Guc(string s, string beklenen) => Assert.Equal(beklenen, GirisFormu.Guc(s).Item1);
}
