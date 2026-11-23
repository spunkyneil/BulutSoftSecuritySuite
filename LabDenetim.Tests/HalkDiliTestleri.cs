// Kolay Mod arka planı testleri: mod tercihi, halk dili çevirici (tüm kural başlıkları kapsanır), trafik ışığı karnesi.
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class HalkDiliTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_halk_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    public HalkDiliTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; ArayuzModu.Sifirla(); }
    public void Dispose() { BulutKlasor.Kok = _eski; ArayuzModu.Sifirla(); try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "HalkDiliTestleri.temizlik"); } }

    // ---- Mod ----
    [Fact] public void Mod_VarsayilanKolay_TercihKalici()
    {
        Assert.Equal(ModTuru.Kolay, ArayuzModu.Mevcut);   // dosya yok: ilk açılış Kolay
        int olay = 0; void k() => olay++; ArayuzModu.Degisti += k;
        try { ArayuzModu.Ayarla(ModTuru.Uzman); ArayuzModu.Ayarla(ModTuru.Uzman); Assert.Equal(1, olay); }   // aynı değer olay vermez
        finally { ArayuzModu.Degisti -= k; }
        ArayuzModu.Sifirla(); Assert.Equal(ModTuru.Uzman, ArayuzModu.Mevcut);   // dosyadan geri okundu
        ArayuzModu.Ayarla(ModTuru.Kolay); ArayuzModu.Sifirla(); Assert.Equal(ModTuru.Kolay, ArayuzModu.Mevcut);
    }
    [Fact] public void Mod_BozukDosya_KolayaDoner() { File.WriteAllText(ArayuzModu.Yol, "{bozuk"); ArayuzModu.Sifirla(); Assert.Equal(ModTuru.Kolay, ArayuzModu.Mevcut); }

    // ---- Kullanıcının verdiği üç örnek ----
    [Fact] public void Ornekler_TamMetinler()
    {
        var h = HalkDiliCevirici.Cevir("HTTP", "HSTS (Strict-Transport-Security) başlığı yok"); Assert.Equal("Sitenin Güvenli Bağlantı Kilidi Eksik", h.Baslik); Assert.Contains("kilitli olmayan bir posta kutusu", h.Zarar); Assert.True(h.Ozel);
        var s = HalkDiliCevirici.Cevir("Port", "Açık port 445 (SMB)"); Assert.Equal("Dosya Paylaşım Kapısı Ardına Kadar Açık", s.Baslik); Assert.Contains("fidye yazılımı bulaştırabilir", s.Zarar);
        var t = HalkDiliCevirici.Cevir("TLS", "Sertifikanın süresi bitmiş"); Assert.Equal("Sitenin Resmi Kimlik Kartı Geçersiz", t.Baslik); Assert.Contains("Bu site güvenli değil", t.Zarar);
    }

    // ---- Kapsam: hiçbir kural/bulgu jargonla kalmamalı ----
    static IEnumerable<string> KuralBasliklari(string kaynak)
    {
        var k = PasifKurallar.Yukle(out _, null, kaynak, Path.Combine(Path.GetTempPath(), "yok_" + Guid.NewGuid().ToString("N") + ".json"));
        return k.Select(x => x.Title.Replace("{value}", "1").Replace("{item}", "oturum"));
    }
    [Fact] public void TumPasifVeAktifKuralBasliklari_OzelHalkDiliAciklamasiVar()
    {
        var eksik = new List<string>();
        foreach (var b in KuralBasliklari("pasif_kurallar.json").Select(t => ("HTTP", t)).Concat(KuralBasliklari("aktif_kurallar.json").Select(t => ("Aktif HTTP", t))))
        { var h = HalkDiliCevirici.Cevir(b.Item1, b.t); if (!h.Ozel) eksik.Add(b.t); }
        Assert.True(eksik.Count == 0, "Halk dili açıklaması olmayan kural başlıkları: " + string.Join(" | ", eksik));
    }
    [Theory]
    [InlineData("Hizmet", "Tırnaksız hizmet yolu: Foo")] [InlineData("Hizmet", "Hizmet dosyası yazılabilir klasörde: Foo")] [InlineData("Hizmet", "İmzasız hizmet yüksek yetkiyle çalışıyor: Foo")]
    [InlineData("Yama", "3 güvenlik güncellemesi eksik")] [InlineData("Yama", "Son güncelleme 120 gün önce kurulmuş")] [InlineData("Yama", "Yama kataloğu yüklenmedi")] [InlineData("Yama", "Katalogda bu Windows yapısı yok (26100)")] [InlineData("Yama", "Güncelleme kaydı okunamadı")]
    [InlineData("Yazılım", "Log4j 2.14.1: sömürüldüğü bilinen açık içeriyor")] [InlineData("Yazılım", "Foo 1.0: bilinen 3 açık içeriyor")] [InlineData("Yazılım", "Foo 1.0: bilinen 3 açık içeriyor (düşük güven)")] [InlineData("Yazılım", "Foo: ürün KEV listesinde yer alıyor (sürüm doğrulanamadı)")] [InlineData("Yazılım", "Zafiyet verisi yüklenmedi")]
    [InlineData("Yazılım", "DLL arama riski: app.exe yazılabilir klasörde")] [InlineData("Yazılım", "srv.exe: derleme korumaları eksik (ASLR (adres rastgeleleştirme))")]
    public void YeniModuller_BasliklariOzelAciklanir(string modul, string baslik) => Assert.True(HalkDiliCevirici.Cevir(modul, baslik).Ozel, baslik);

    [Theory]
    [InlineData("Açık port 3389 (RDP)", "Uzaktan Bilgisayarı Kullanma Kapısı Açık")] [InlineData("Açık port 23 (Telnet)", "Çok Eski ve Şifresiz Uzak Erişim Kapısı Açık")] [InlineData("Açık port 21 (FTP)", "Eski Tip Dosya Aktarma Kapısı Açık")]
    [InlineData("Açık port 3306 (MySQL)", "Veri Tabanı Kapısı Ağa Açık")] [InlineData("Açık port 5900 (VNC)", "Ekran Paylaşım Kapısı Açık")] [InlineData("Açık port 8080 (HTTP-Alt)", "Şifresiz Web Kapısı Açık")] [InlineData("Açık port 49999 (bilinmiyor)", "Bilgisayarınızda Bir Kapı Açık")]
    public void Portlar_KapiBenzetmesiyle(string baslik, string beklenen) => Assert.Equal(beklenen, HalkDiliCevirici.Cevir("Port", baslik).Baslik);

    [Fact] public void KEV_UrunAdiEslesmesi_AsiriIddiaEtmez()
    {
        var u = HalkDiliCevirici.Cevir("Yazılım", "Foo: ürün KEV listesinde yer alıyor (sürüm doğrulanamadı)"); Assert.Contains("doğrulanamadı", u.BuNeDemek); Assert.Contains("Bazı Sürümlerinde", u.Baslik);
        Assert.Contains("fiilen kullandığı", HalkDiliCevirici.Cevir("Yazılım", "Foo 1: sömürüldüğü bilinen açık içeriyor").BuNeDemek);
    }
    [Fact] public void Bilinmeyen_YedekMetin_OzelDegil_VeOnemeGoreZarar()
    {
        var h = HalkDiliCevirici.Cevir("Bilinmeyen Modül", "Tuhaf bir şey", "", Onem.Yuksek); Assert.False(h.Ozel); Assert.Contains("Ciddi", h.Zarar); Assert.Equal("Tuhaf bir şey", h.Baslik);
        Assert.Contains("Küçük", HalkDiliCevirici.Cevir("X", "y", "", Onem.Dusuk).Zarar); Assert.Equal("Bir eksik bulundu", HalkDiliCevirici.Cevir("X", "", "", Onem.Orta).Baslik);
        Assert.Contains("Windows hizmetlerinden", HalkDiliCevirici.Cevir("Hizmet", "Bilinmeyen hizmet sorunu").BuNeDemek, StringComparison.Ordinal);   // modül yedeği
    }
    [Fact] public void Finding_Ve_Bulgu_AyniSonucu_Verir()
    {
        var f = BulguFabrikasi.Yap("SVC-UNQUOTED-001", "Hizmet", "host:T", "a", "Tırnaksız hizmet yolu: Foo", "n", "w", new[] { "k" }, 9, 1.0, new[] { "A.8.9" }, Array.Empty<string>(), "c", "CHK", DateTime.UtcNow);
        Assert.Equal(HalkDiliCevirici.Cevir(f.ToBulgu()).Baslik, HalkDiliCevirici.Cevir(f).Baslik);
    }
    [Fact] public void Cevirici_KotuNiyetliUzunBaslik_Cokmez() => Assert.True(HalkDiliCevirici.Cevir("X", new string('a', 5000) + " port " + new string('9', 3000)).Baslik.Length <= 100);

    // ---- Karne ----
    static Bulgu B(Onem o, string modul = "Port", string baslik = "Açık port 445 (SMB)", string oneri = "Kapıyı kapatın. Ayrıntı: x") => new(modul, baslik, o, "d", oneri);

    [Fact] public void Karne_IyiDurum_YesilVeOrnekBaslik()
    {
        var k = KarneHesabi.Hesapla(new[] { B(Onem.Dusuk, "HTTP", "Referrer-Policy başlığı yok"), B(Onem.Dusuk, "HTTP", "Permissions-Policy başlığı yok (bilgi)"), B(Onem.Bilgi, "TLS", "Şifreleme") });
        Assert.Equal(94, k.Puan); Assert.Equal("94 / 100 — Genel Durum İyi, 2 Küçük Eksik Var", k.Baslik); Assert.Equal(0, k.Renk); Assert.Equal(2, k.SorunSayisi);
    }
    [Fact] public void Karne_SariVeKirmizi_Ve_YuksekVarsaSari()
    {
        Assert.Equal(1, KarneHesabi.Hesapla(new[] { B(Onem.Yuksek) }).Renk);   // puan 85 ama önemli eksik var → sarı
        var kirmizi = KarneHesabi.Hesapla(Enumerable.Range(0, 4).Select(i => B(Onem.Yuksek, "Port", "Açık port " + (3000 + i)))); Assert.Equal(40, kirmizi.Puan); Assert.Equal(2, kirmizi.Renk); Assert.Contains("Acil Müdahale", kirmizi.Baslik); Assert.Contains("4 Önemli", kirmizi.Baslik);
        var karma = KarneHesabi.Hesapla(new[] { B(Onem.Yuksek), B(Onem.Orta, "HTTP", "CORS her kaynağa açık"), B(Onem.Dusuk, "HTTP", "Referrer-Policy başlığı yok") }); Assert.Contains("1 Önemli, 2 Küçük Eksik Var", karma.Baslik);
    }
    [Fact] public void Karne_Temiz_VeBos_VeAtlananlarSayilmaz()
    {
        var k = KarneHesabi.Hesapla(Array.Empty<Bulgu>()); Assert.Equal(100, k.Puan); Assert.Equal(0, k.Renk); Assert.Contains("Hiç Eksik Bulunamadı", k.Baslik); Assert.Empty(k.EnOnemli3);
        Assert.Equal(100, KarneHesabi.Hesapla(new[] { new Bulgu("X", "Bu kontrol çalıştırılamadı", Onem.Yuksek, "", "") }).Puan);
    }
    [Fact] public void Karne_En3Adim_ÖnemSirali_TekrarsizVeSade()
    {
        var l = new[] { B(Onem.Dusuk, "HTTP", "Çerez 'Secure' bayrağı yok: a", "Secure ekleyin."), B(Onem.Dusuk, "HTTP", "Çerez 'Secure' bayrağı yok: b", "Secure ekleyin."), B(Onem.Yuksek, "Port", "Açık port 445 (SMB)", "Dosya paylaşımını kapatın. Sonra yeniden başlatın.\nÖrnek: netsh ..."), B(Onem.Orta, "HTTP", "CORS her kaynağa açık"), B(Onem.Orta, "TLS", "Sertifikanın süresi bitmiş"), B(Onem.Dusuk, "HTTP", "Referrer-Policy başlığı yok") };
        var k = KarneHesabi.Hesapla(l); Assert.Equal(3, k.EnOnemli3.Count);
        Assert.Equal("Dosya Paylaşım Kapısı Ardına Kadar Açık", k.EnOnemli3[0].Halk.Baslik); Assert.Equal("Dosya paylaşımını kapatın.", k.EnOnemli3[0].Yapilacak);   // ilk cümle, komut yok
        Assert.Equal(15, k.EnOnemli3[0].Kazanc); Assert.True(k.EnOnemli3[1].Onem == Onem.Orta && k.EnOnemli3[2].Onem == Onem.Orta);
        Assert.Equal(k.EnOnemli3.Count, k.EnOnemli3.Select(x => x.Halk.Baslik).Distinct().Count());
    }
    [Fact] public void Yapilacak_Metinleri() { Assert.Equal("Ayrıntı için Uzman Görünümü'ne bakın.", KarneHesabi.Yapilacak("")); Assert.Equal("Güncelleyin.", KarneHesabi.Yapilacak("Güncelleyin.")); Assert.DoesNotContain("netsh", KarneHesabi.Yapilacak("Örnek: netsh x\nKapatın."), StringComparison.Ordinal); Assert.True(KarneHesabi.Yapilacak(new string('a', 900)).Length <= 201); }
}
