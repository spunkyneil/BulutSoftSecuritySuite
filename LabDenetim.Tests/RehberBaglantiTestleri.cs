// Kali Araç Rehberi ↔ komut şablonları bağlantısı: sınıflar, şablon haritası, arayüz alanları/onay/sonuç metinleri ve kalıcı izinli hedef listesi.
using Xunit;

namespace LabDenetim.Tests;

// Statik kayıtları (şablon deposu, izinli hedefler, BulutKlasor) paylaştığı için diğer "Veri" testleriyle sırayla çalışır.
[Collection("Veri")]
public class RehberBaglantiTestleri : IDisposable
{
    public RehberBaglantiTestleri() { KomutSablonuDeposu.Temizle(); HedefDogrulama.TestHedefleriniTemizle(); }
    public void Dispose() { KomutSablonuDeposu.Temizle(); HedefDogrulama.TestHedefleriniTemizle(); IzinliHedefler.Sifirla(); }

    // ================================================================ Sınıflar ve harita
    [Fact] public void Harita_HerSablonTamBirAractaVeAraclarMevcut()
    {
        var tum = KomutKaydi.Tanimlar.Select(t => t.Ad).ToList(); var haritadakiler = KaliRehberi.Harita.SelectMany(x => x.Value).ToList();
        Assert.Equal(tum.OrderBy(x => x).ToArray(), haritadakiler.OrderBy(x => x).ToArray());                     // gömülü tablodaki HER şablon bir rehber aracına bağlı, tekrar yok
        Assert.Equal(haritadakiler.Count, haritadakiler.Distinct().Count());
        foreach (var ad in KaliRehberi.Harita.Keys) Assert.Single(KaliRehberi.Tum, a => a.Ad == ad);              // haritadaki her araç rehberde var
    }

    [Fact] public void Siniflar_SablonluAraclarCalistirilir_KalanlarEgitim_KapaliDegismez()
    {
        var tum = KaliRehberi.Tum;
        Assert.Equal(20, tum.Count(a => a.Sablonlar != null)); Assert.All(tum.Where(a => a.Sablonlar != null), a => Assert.Equal(AracSinifi.Calistirilir, a.Sinif));
        // Henüz bağlanmamış (ek tasarım gereken) 5 araç "Egitim" kalır; başka hiçbir araç eğitimde kalmaz.
        Assert.Equal(new[] { "Autopsy", "Ghidra", "Kismet", "Mitmproxy", "OWASP ZAP" }, tum.Where(a => a.Sinif == AracSinifi.Egitim).Select(a => a.Ad).OrderBy(x => x).ToArray());
        // Kapalı araçlar (saldırı odaklı) AYNEN 19 ve hiçbirinin şablonu yok.
        var kapali = tum.Where(a => a.Sinif == AracSinifi.Kapali).ToList(); Assert.Equal(19, kapali.Count); Assert.All(kapali, a => { Assert.Null(a.Sablonlar); Assert.Null(a.Calistir); });
        Assert.DoesNotContain(KaliRehberi.Harita.Keys, ad => tum.Single(a => a.Ad == ad).Sinif == AracSinifi.Kapali);
        // Eski beş araç ve nmap yolu bozulmadı.
        Assert.All(new[] { "Whois", "Nikto", "Searchsploit", "sslscan", "ssh-audit" }, ad => { var a = tum.Single(x => x.Ad == ad); Assert.NotNull(a.Calistir); Assert.Null(a.Sablonlar); Assert.Equal(AracSinifi.Calistirilir, a.Sinif); });
        Assert.Equal(AracSinifi.Calistirilir, tum.Single(a => a.Ad == "Nmap").Sinif);
    }

    [Fact] public void Egitim_EtiketiArtikYalnizcaEgitimDemez()
    {
        string s = KaliRehberi.SinifAdi(AracSinifi.Egitim); Assert.DoesNotContain("Yalnızca eğitim", s); Assert.Contains("henüz bağlı değil", s);
    }

    [Fact] public void SablonTanimlari_AraclaBirlikteGelir()
    {
        var tor = KaliRehberi.Tum.Single(a => a.Ad == "Tor"); Assert.Equal(new[] { "tor-baslat", "tor-durdur", "tor-durum" }, KaliRehberi.SablonTanimlari(tor).Select(t => t.Ad).OrderBy(x => x).ToArray());
        Assert.Empty(KaliRehberi.SablonTanimlari(KaliRehberi.Tum.Single(a => a.Ad == "Hydra")));
    }

    // ================================================================ Arayüz alanları
    private static KomutTanimi T(string ad) => KomutKaydi.Tanim(ad)!;
    // Zorunlu (varsayılansız) alana kullanıcının yazacağı örnek dosya adı (desen .pcap istiyorsa .pcap).
    private static string Dolu(TanimParametre p) => p.Desen.Contains("pcap") ? "ornek.pcap" : "ornek.bin";

    [Fact] public void Alanlar_HedefVeParametreleriSiralar_SecenekleriVarsayilanBasta()
    {
        var a = RehberCalistirma.Alanlar(T("theharvester-alan")); Assert.Equal(new[] { "hedef", "kaynak", "limit" }, a.Select(x => x.Ad).ToArray());
        Assert.True(a[0].Hedef); Assert.Contains("alan adı", a[0].Etiket); Assert.Equal("crtsh", a[1].Secenek![0]); Assert.Equal(6, a[1].Secenek!.Length);       // varsayılan başta
        Assert.Null(a[2].Secenek); Assert.Contains("(1-500)", a[2].Etiket); Assert.Contains("boşsa: 100", a[2].Etiket);
        Assert.Empty(RehberCalistirma.Alanlar(T("tor-baslat")));                                                                                    // hedefsiz ve parametresiz: soru yok
        Assert.Equal(new[] { "recon/domains-hosts/hackertarget", "recon/domains-hosts/certificate_transparency" }, RehberCalistirma.Alanlar(T("reconng-modul"))[1].Secenek);   // önekli seçenek
        Assert.Null(RehberCalistirma.Alanlar(T("volatility3-eklenti"))[0].Secenek); Assert.Contains("Dosya adı", RehberCalistirma.Alanlar(T("tshark-pcap-ozet"))[0].Etiket); Assert.Contains("bulutsoft_inceleme", RehberCalistirma.Alanlar(T("binwalk-tara"))[0].Etiket);
        Assert.Contains("Ağ arayüzü", RehberCalistirma.Alanlar(T("tcpdump-yakala"))[0].Etiket); Assert.Contains("özel ağ IP", RehberCalistirma.Alanlar(T("netdiscover-ag"))[0].Etiket); Assert.Contains("IP adresi veya alan adı", RehberCalistirma.Alanlar(T("netcat-baglanti"))[0].Etiket);
    }

    [Fact] public void Girdiler_BosDegerlerVarsayilanaBirakilir()
    {
        var t = T("theharvester-alan"); var (hedef, g) = RehberCalistirma.Girdiler(t, new[] { " musteri.example ", "otx", "" }); Assert.Equal("musteri.example", hedef); Assert.Equal(new Dictionary<string, string> { ["kaynak"] = "otx" }, g);
        var (h2, g2) = RehberCalistirma.Girdiler(T("tor-baslat"), Array.Empty<string>()); Assert.Equal("", h2); Assert.Empty(g2);
        var (h3, g3) = RehberCalistirma.Girdiler(t, new[] { "x.example" }); Assert.Equal("x.example", h3); Assert.Empty(g3);                           // eksik değerler yok sayılır
    }

    [Fact] public void Alanlar_VarsayilanDegerlerVeSecenekler_SablonKurallarindanGecer()
    {
        // Arayüzün sunduğu HER seçenek ve varsayılan, şablonun kendi doğrulamasından geçmeli (açılır listede reddedilen seçenek olmasın).
        KomutKaydi.Hazirla(); int sinanan = 0;
        foreach (var t in KomutKaydi.Tanimlar)
        {
            var sab = KomutSablonuDeposu.Getir(t.Ad)!; var alanlar = RehberCalistirma.Alanlar(t);
            Assert.Equal((t.HedefTuru == "yok" ? 0 : 1) + (t.Parametreler?.Count ?? 0), alanlar.Count);
            foreach (var secenek in alanlar.Where(x => x.Secenek != null).SelectMany(x => x.Secenek!.Select(s => (x.Ad, s)))) { var g = new Dictionary<string, string> { [secenek.Ad] = secenek.s }; foreach (var p in t.Parametreler!.Where(p => p.Ad != secenek.Ad && string.IsNullOrEmpty(p.Varsayilan))) g[p.Ad] = Dolu(p); KomutSablonuDeposu.Uyarilastir(sab, g); sinanan++; }
            // Hiçbir şey yazılmazsa varsayılanlar (zorunlu dosya alanları hariç) geçerlidir.
            var bos = new Dictionary<string, string>(); foreach (var p in t.Parametreler ?? new()) if (string.IsNullOrEmpty(p.Varsayilan)) bos[p.Ad] = Dolu(p); KomutSablonuDeposu.Uyarilastir(sab, bos);
        }
        Assert.True(sinanan >= 8, "denenen açılır liste seçeneği sayısı: " + sinanan);   // şu an: theHarvester kaynakları (6) + recon-ng modülleri (2)
    }

    [Fact] public void DugmeMetni_MasaustuAraclarindaFarkli()
    {
        Assert.Equal("Kali Masaüstünde Başlat...", RehberCalistirma.DugmeMetni(new[] { T("maltego-ac") })); Assert.Equal("Sensörde Çalıştır...", RehberCalistirma.DugmeMetni(new[] { T("tor-durum"), T("tor-baslat") })); Assert.Equal("Sensörde Çalıştır...", RehberCalistirma.DugmeMetni(Array.Empty<KomutTanimi>()));
    }

    [Fact] public void OnayMetni_KomutuVeRiskleriGosterir()
    {
        string tcp = RehberCalistirma.OnayMetni(T("tcpdump-yakala"), "Kali Lab", "", "sudo -n timeout 120 tcpdump -nn");
        Assert.Contains("Kali Lab", tcp); Assert.Contains("sudo -n timeout 120 tcpdump -nn", tcp); Assert.Contains("sudo", tcp); Assert.DoesNotContain("yazılı izin", tcp); Assert.DoesNotContain("internete çıkar", tcp); Assert.EndsWith("Çalıştırılsın mı?", tcp);
        string wp = RehberCalistirma.OnayMetni(T("wpscan-url"), "Kali Lab", "musteri.example", "wpscan --url https://musteri.example"); Assert.Contains("internete çıkar", wp); Assert.Contains("yazılı izin", wp); Assert.Contains("musteri.example", wp); Assert.DoesNotContain("sudo) yetkisiyle", wp);
        string mal = RehberCalistirma.OnayMetni(T("maltego-ac"), "Kali Lab", "", "maltego"); Assert.Contains("masaüstünde açılır", mal); Assert.Contains("Lisans", mal); Assert.Contains("doğrulanmadı", mal);
    }

    [Fact] public void DurumSatiri_VeSonucMetni()
    {
        KomutSonucu S(int kod, string dosya = "", List<Bulgu>? b = null) => new("ornek", "10.0.0.1", "ornek 10.0.0.1", DateTime.Now, TimeSpan.FromSeconds(2.5), kod, dosya, b ?? new());
        Assert.StartsWith("BAŞARILI", RehberCalistirma.DurumSatiri(S(0))); Assert.StartsWith("ZAMAN AŞIMI", RehberCalistirma.DurumSatiri(S(124))); Assert.StartsWith("ATLANDI", RehberCalistirma.DurumSatiri(S(-2))); Assert.StartsWith("ÇIKIŞ KODU 3", RehberCalistirma.DurumSatiri(S(3)));
        // Atlandı: ham dosya yok, açıklama ve öneri gösterilir.
        string atl = RehberCalistirma.SonucMetni(S(-2, "", new() { KomutCalistirici.AtlandiBulgusu("ornek", "araç kurulu değil", "apt install ornek") })); Assert.Contains("ATLANDI", atl); Assert.Contains("araç kurulu değil", atl); Assert.Contains("apt install ornek", atl); Assert.DoesNotContain("Ham çıktı dosyası", atl);
        // Ham çıktı dosyası: içerik gösterilir ve sınırda kısaltılır.
        string d = Path.Combine(Path.GetTempPath(), "rehber_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(d, "satir1\nsatir2\n" + new string('x', 5000)); string m = RehberCalistirma.SonucMetni(S(0, d, new() { new Bulgu("o", "b", Onem.Bilgi, "d", "") }), 100);
            Assert.Contains("$ ornek 10.0.0.1", m); Assert.Contains("1 bulgu", m); Assert.Contains(d, m); Assert.Contains("satir1\r\nsatir2", m); Assert.Contains("kısaltıldı", m); Assert.True(m.Length < 600);
            Assert.Contains("okunamadı", RehberCalistirma.SonucMetni(S(0, d + ".yok")));
        }
        finally { try { File.Delete(d); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "RehberBaglantiTestleri.temizlik"); } }
    }

    // ================================================================ Kalıcı izinli hedefler
    [Fact] public void IzinliHedefler_EklenirKalicidirYenidenYuklenir()
    {
        string eskiKok = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "izinli_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = gecici;
        try
        {
            IzinliHedefler.Sifirla(); Assert.NotNull(HedefDogrulama.HedefHatasi("web", "Musteri.Example"));
            IzinliHedefler.Ekle("Musteri.Example"); Assert.Null(HedefDogrulama.HedefHatasi("web", "musteri.example")); IzinliHedefler.Ekle("musteri.example"); IzinliHedefler.Ekle("203.0.113.7");   // yinelenen eklenmez
            Assert.Equal(new[] { "musteri.example", "203.0.113.7" }, File.ReadAllLines(IzinliHedefler.Yol).Where(x => x.Length > 0).ToArray()); Assert.Equal(2, HedefDogrulama.TestHedefleri().Count);
            Assert.Throws<ArgumentException>(() => IzinliHedefler.Ekle("localhost")); Assert.Throws<ArgumentException>(() => IzinliHedefler.Ekle("10.0.0.0/8")); Assert.Throws<ArgumentException>(() => IzinliHedefler.Ekle(""));   // geçersiz girdi eklenmez
            // Yeniden başlatma benzetimi: bellek boşalır, dosyadan geri yüklenir.
            HedefDogrulama.TestHedefleriniTemizle(); IzinliHedefler.Sifirla(); Assert.NotNull(HedefDogrulama.HedefHatasi("web", "musteri.example")); IzinliHedefler.Hazirla(); Assert.Null(HedefDogrulama.HedefHatasi("web", "musteri.example")); Assert.Null(HedefDogrulama.HedefHatasi("web", "203.0.113.7"));
            // Elle bozulmuş dosya: geçersiz satırlar yüklenmez, geçerliler yüklenir.
            File.WriteAllText(IzinliHedefler.Yol, "# yorum\nsaglam.example\n0.0.0.0/0\nbozuk satir;id\n"); HedefDogrulama.TestHedefleriniTemizle(); IzinliHedefler.Sifirla(); IzinliHedefler.Hazirla();
            Assert.Null(HedefDogrulama.HedefHatasi("web", "saglam.example")); Assert.Single(HedefDogrulama.TestHedefleri());
        }
        finally { BulutKlasor.Kok = eskiKok; try { Directory.Delete(gecici, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "RehberBaglantiTestleri.temizlik"); } }
    }
}
