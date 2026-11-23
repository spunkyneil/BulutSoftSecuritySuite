// Bu dosya programın kritik mantığını otomatik denetleyen birim testlerini içerir.
// Çalıştırmak için: LabDenetim.Tests klasöründe "dotnet test" yazın. Testler gerçek ayarlarınıza dokunmaz (geçici klasör kullanır).
namespace LabDenetim.Tests;

// Link analizi testleri.
public class LinkTestleri
{
    // Normal site düşük risk olmalı.
    [Fact] public void NormalSite_DusukRisk() => Assert.True(LinkAnalizi.Analiz("https://www.google.com/search?q=a").Risk < 10);
    // Marka taklidi yüksek risk olmalı.
    [Fact] public void MarkaTaklidi_YuksekRisk() => Assert.True(LinkAnalizi.Analiz("http://paypal-secure-login.xyz/verify").Risk >= 60);
    // @ işareti uyarılmalı.
    [Fact] public void ArobaIsareti_Uyarir() => Assert.Contains(LinkAnalizi.Analiz("http://a.com@evil.com").Nedenler, n => n.Contains("@"));
    // Boş girdi çökmemeli.
    [Fact] public void Bos_Cokmez() => Assert.Equal("Boş", LinkAnalizi.Analiz("").Seviye);
}

// Kara liste testleri.
public class KaraListeTestleri
{
    // CIDR eşleşmesi.
    [Theory, InlineData("203.0.113.9", "203.0.113.0/24", true), InlineData("203.0.114.9", "203.0.113.0/24", false), InlineData("1.2.3.4", "1.2.3.4", true), InlineData("1.2.3.5", "1.2.3.4", false), InlineData("10.1.2.3", "10.0.0.0/8", true)]
    public void Cidr(string ip, string kayit, bool beklenen) => Assert.Equal(beklenen, KaraListe.Eslesir(System.Net.IPAddress.Parse(ip), kayit));
    // Geçersiz IP.
    [Fact] public void GecersizIp() => Assert.Contains("Geçerli", KaraListe.Kontrol("abc"));
}

// Parola sızıntı testleri (internete çıkmaz).
public class ParolaTestleri
{
    // Bilinen SHA-1 değeri.
    [Fact] public void Ozet_Dogru() => Assert.Equal("5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8", ParolaSizinti.Ozet("password"));
    // Cevap ayrıştırma.
    [Fact] public void Say_Bulur() => Assert.Equal(42, ParolaSizinti.Say("AAA:1\r\nBBB:42\r\n", "BBB"));
    // Olmayan sonek.
    [Fact] public void Say_Yoksa_Sifir() => Assert.Equal(0, ParolaSizinti.Say("AAA:1\r\n", "CCC"));
}

// Düzeltme planı ve puan testleri.
public class DurumTestleri
{
    // Plan en çok puan kazandıranı başa koymalı.
    [Fact] public void Plan_Sirali()
    {
        var l = new List<Bulgu> { new("HTTP", "küçük", Onem.Dusuk, "", ""), new("Port", "büyük", Onem.Yuksek, "", ""), new("TLS", "iyi", Onem.Bilgi, "", "") };
        var p = DurumAnalizi.Plan(l, 82);
        Assert.Equal(2, p.Count); Assert.Equal("büyük", p[0].B.Baslik); Assert.Equal(97, p[0].YeniPuan); Assert.Equal(100, p[1].YeniPuan);
    }
    // Harf notu sınırları.
    [Theory, InlineData(95, "A"), InlineData(75, "B"), InlineData(60, "C"), InlineData(40, "D"), InlineData(10, "F")]
    public void Not(int puan, string harf) => Assert.Equal(harf, DurumAnalizi.Not(puan).Harf);
    // MITRE eşleştirmesi.
    [Fact] public void Mitre_Rdp() => Assert.Equal("T1021.001", Mitre.Bul(new Bulgu("Port", "Açık kapı: 3389 (RDP)", Onem.Yuksek, "", ""))!.Kimlik);
    // CEF'te "|" kaçırılmalı.
    [Fact] public void Cef_KacirirBoruyu() => Assert.Contains(@"a\|b", SiemCikti.CefSatir(new Bulgu("Port", "a|b", Onem.Orta, "", ""), "h", DateTime.Now));
}

// Pencere yerleşimi testleri.
public class PencereTestleri
{
    // Ekrandan büyük pencere sığdırılmalı.
    [Fact] public void Sigdir_Kucultur() => Assert.Equal(new Rectangle(0, 0, 1920, 1040), PencereHesap.Sigdir(new Rectangle(100, 100, 2250, 1230), new Rectangle(0, 0, 1920, 1040)));
    // Ekran dışındaki pencere görünür sayılmamalı.
    [Fact] public void EkranDisi_Gorunmez() => Assert.False(PencereHesap.Gorunur(new Rectangle(-3000, 0, 1500, 800), new[] { new Rectangle(0, 0, 1920, 1040) }));
}

// Başarı rozeti testleri.
public class BasariTestleri
{
    // Üç gün üst üste seri.
    [Fact] public void Seri_UcGun()
    {
        var k = Enumerable.Range(0, 3).Select(g => new GecmisKayit("i" + g, DateTime.Now.Date.AddDays(-g).AddHours(9), "h", true, "S", 1, 1, 80, 0, 0, 0, 0, new List<Bulgu>())).ToList();
        Assert.Equal(3, Basarilar.Seri(k, DateTime.Now));
    }
}

// Dosya bütünlüğü ve mühür testleri (geçici klasörde).
[Collection("Veri")]
public class DosyaTestleri : IDisposable
{
    // Geçici klasör.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_test_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    // Kurulum.
    public DosyaTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = Path.Combine(_t, "veri"); }
    // Temizlik.
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "Testler.temizlik"); } }

    // Değişen/yeni/silinen dosyalar bulunmalı.
    [Fact] public void Fim_FarkBulur()
    {
        string d = Path.Combine(_t, "izle"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.txt"), "a"); File.WriteAllText(Path.Combine(d, "b.txt"), "b");
        Assert.Equal(2, DosyaButunlugu.TemelOlustur(d));
        File.WriteAllText(Path.Combine(d, "a.txt"), "AAA"); File.Delete(Path.Combine(d, "b.txt")); File.WriteAllText(Path.Combine(d, "c.txt"), "c");
        var f = DosyaButunlugu.Karsilastir(d)!; Assert.Equal(new[] { "a.txt" }, f.Degisen); Assert.Equal(new[] { "c.txt" }, f.Yeni); Assert.Equal(new[] { "b.txt" }, f.Silinen);
    }
    // Mühürlü dosya değişince fark edilmeli.
    [Fact] public void Muhur_DegisimiYakalar()
    {
        Directory.CreateDirectory(BulutKlasor.Kok); File.WriteAllText(BulutKlasor.Dosya("kabul_edilen.json"), "{}"); Muhur.Muhurle();
        Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "kabul_edilen.json" && x.Durum == "Sağlam");
        File.WriteAllText(BulutKlasor.Dosya("kabul_edilen.json"), "{\"x\":1}");
        Assert.Contains(Muhur.Dogrula(), x => x.Dosya == "kabul_edilen.json" && x.Durum == "DEĞİŞMİŞ");
    }
    // Webhook adres kuralları.
    [Theory, InlineData("https://hooks.slack.com/x", true), InlineData("http://x.com", false), InlineData("https://u:p@x.com", false), InlineData("https://localhost/x", false)]
    public void Webhook_Adres(string a, bool b) => Assert.Equal(b, Webhook.AdresGecerli(a));
}

// Uzak sensör güvenlik kuralları ve ajan çıktısı testleri (gerçek sensöre bağlanmaz).
[Collection("Veri")]
public class SensorTestleri : IDisposable
{
    // Geçici klasör.
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_sensor_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok;
    // Kurulum.
    public SensorTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; }
    // Temizlik.
    public void Dispose() { BulutKlasor.Kok = _eski; try { Directory.Delete(_t, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "Testler.temizlik"); } }

    // Sensör adresi yalnızca özel ağ IPv4 olabilir.
    [Theory, InlineData("192.168.56.101", true), InlineData("10.0.0.5", true), InlineData("8.8.8.8", false), InlineData("evil.com", false), InlineData("192.168.1.1; rm -rf /", false), InlineData("-oProxyCommand=x", false)]
    public void Host(string h, bool b) => Assert.Equal(b, SensorGuvenlik.HostGecerli(h));
    // Kullanıcı adı: ssh seçeneği veya kabuk karakteri olamaz.
    [Theory, InlineData("kali", true), InlineData("-oProxyCommand=x", false), InlineData("a b", false), InlineData("root;id", false), InlineData("Kali", false)]
    public void Kullanici(string k, bool b) => Assert.Equal(b, SensorGuvenlik.KullaniciGecerli(k));
    // Kapı listesi biçimi ve sınırları.
    [Theory, InlineData("22,80,443", true), InlineData("1-1024", true), InlineData("0", false), InlineData("70000", false), InlineData("80;id", false), InlineData("1-65535", false), InlineData("5-3", false), InlineData("", false)]
    public void Kapilar(string k, bool b) => Assert.Equal(b, SensorGuvenlik.KapilarGecerli(k));
    // Hedef internet adresi reddedilmeli.
    [Theory, InlineData("192.168.56.1", true), InlineData("1.1.1.1", false), InlineData("192.168.56.1 && id", false)]
    public void Hedef(string h, bool b) => Assert.Equal(b, SensorGuvenlik.HedefGecerli(h));

    // Geçersiz girdiyle uzak komut ÜRETİLMEMELİ (enjeksiyon engeli).
    [Fact] public void Komut_Enjeksiyonu_Reddedilir() { Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("192.168.56.1", "80;id")); Assert.Throws<ArgumentException>(() => SensorKomutlari.TcpKomutu("8.8.8.8", "80")); }
    // Geçerli girdiyle beklenen komut.
    [Fact] public void Komut_Uretir() => Assert.Equal("python3 - tcp 192.168.56.1 22,80", SensorKomutlari.TcpKomutu("192.168.56.1", "22,80"));
    // Ajan çıktısını çözme; kötü satırlar yok sayılır.
    [Fact] public void Ajan_Ayristirma()
    {
        Assert.Equal(22, SensorCiktiAyristirici.Ayristir("{\"t\":\"open\",\"p\":22,\"b\":\"SSH-2.0\"}")!.Kapi);
        Assert.Null(SensorCiktiAyristirici.Ayristir("not json")); Assert.Null(SensorCiktiAyristirici.Ayristir("{\"t\":\"rm\"}")); Assert.Null(SensorCiktiAyristirici.Ayristir("{bozuk"));
        Assert.DoesNotContain('\n', SensorCiktiAyristirici.Ayristir(@"{""t"":""error"",""m"":""a\nb\u001b[31m""}")!.Mesaj);
    }
    // Ön kontrol çıktısı.
    [Fact] public void OnKontrol_Cozer() { var s = SensorCiktiAyristirici.OnKontrolCoz(new[] { "BS-SENSOR-OK", "Linux 6.8", "kali", "PY-OK" }); Assert.True(s.Basarili && s.Python); Assert.False(SensorCiktiAyristirici.OnKontrolCoz(new[] { "baska" }).Basarili); }
    // ssh argümanları: parola kapalı, parmak izi dosyası var, hedef tek argüman.
    [Fact] public void Ssh_Argumanlari()
    {
        var a = SensorKomutlari.ArgumanOlustur(new SensorProfili("x", "192.168.56.1", 22, "kali", "k"), "cmd");
        Assert.Contains("BatchMode=yes", a); Assert.Contains("PasswordAuthentication=no", a); Assert.Contains("kali@192.168.56.1", a); Assert.Equal("cmd", a[^1]);
    }
    // Ortak anahtar komutu: şüpheli biçim reddedilir.
    [Fact] public void AnahtarKomutu() { Assert.Throws<ArgumentException>(() => SensorAnahtarYoneticisi.AnahtarEklemeKomutu("ssh-ed25519 AAAA'; rm -rf / #")); Assert.Contains("authorized_keys", SensorAnahtarYoneticisi.AnahtarEklemeKomutu("ssh-ed25519 AAAAC3Nz bulutsoft-sensor")); }
    // Profil kaydı gidiş-dönüş.
    [Fact] public void Depo_GidisDonus() { SensorDeposu.Kaydet(new() { new SensorProfili("Kali", "192.168.56.101", 22, "kali", "k") }); Assert.Equal("Kali", SensorDeposu.Yukle()[0].Ad); }
    // Gerçek ssh ile ulaşılamayan yerel kapıya bağlanma: çökmeden anlaşılır hata (ağ dışına çıkmaz).
    [Fact] public async Task Gercek_Ssh_BaglantiReddedilir()
    {
        string anahtar = Path.Combine(_t, "k"); await SensorAnahtarYoneticisi.AnahtarUretAsync(anahtar); Assert.True(File.Exists(anahtar + ".pub"));
        var s = await SensorTaramaYurutucu.SinaAsync(new SensorProfili("t", "127.0.0.1", 1, "kali", anahtar), CancellationToken.None);
        Assert.False(s.Basarili); Assert.StartsWith("Bağlanılamadı", s.Ozet);
    }
}
