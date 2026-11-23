// Modül 4 testleri: parola politikası ölçümü (zayıf kalıplar, leet, Türkçe, yasaklı sözcük, gizlilik) ve dosya özeti hesaplama/karşılaştırma (bilinen test vektörleri, biçim toleransı, iptal).
using System.Text;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Veri klasörünü paylaşan testler sırayla çalışsın.
[Collection("Veri")]
public class ParolaVeHashTestleri : IDisposable
{
    private readonly string _eskiKok = BulutKlasor.Kok; private readonly string _gecici = Path.Combine(Path.GetTempPath(), "ph_" + Guid.NewGuid().ToString("N"));
    public ParolaVeHashTestleri() { Directory.CreateDirectory(_gecici); BulutKlasor.Kok = Path.Combine(_gecici, "kok"); }
    public void Dispose() { BulutKlasor.Kok = _eskiKok; try { Directory.Delete(_gecici, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "ParolaVeHashTestleri.temizlik"); } }

    // ---------------- Parola ----------------
    [Theory]
    [InlineData("Password123!")] [InlineData("P@ssw0rd2024")] [InlineData("Qwerty123456!")] [InlineData("ŞifreŞifre1!")] [InlineData("Galatasaray1905!")] [InlineData("Istanbul34.Ankara06")] [InlineData("adminADMIN12!@")]
    public void Bilinen_Zayif_Kaliplar_Ihlal_Ve_Dusuk_Puan(string parola)
    {
        var s = ParolaPolitikasi.Degerlendir(parola); Assert.False(s.Uygun); Assert.True(s.Puan <= 15, $"{parola} puan {s.Puan}"); Assert.Contains(s.Ihlaller, x => x.Contains("tahmin edilebilir"));
    }

    [Fact]
    public void Guclu_Rastgele_Parola_Uygun_Ve_Yuksek_Puan()
    {
        var s = ParolaPolitikasi.Degerlendir("Kx9$mQ2#vL7!zP4w"); Assert.True(s.Uygun); Assert.Empty(s.Ihlaller); Assert.True(s.Puan >= 85); Assert.Equal("Güçlü", s.Seviye); Assert.True(s.EntropiBit > 80);
    }

    [Fact]
    public void Uzun_Parola_Cumlesi_Uygun_Sayilir()
    {
        var s = ParolaPolitikasi.Degerlendir("bulut-masa-kalem-yagmur-42"); Assert.True(s.Uygun, string.Join("|", s.Ihlaller)); Assert.True(s.Puan >= 70);
    }

    [Fact]
    public void Kisa_Ve_Cesitsiz_Parola_Ihlal_Verir()
    {
        var s = ParolaPolitikasi.Degerlendir("Ab1!"); Assert.False(s.Uygun); Assert.Contains(s.Ihlaller, x => x.Contains("Çok kısa")); Assert.True(s.Puan <= 45);
        var t = ParolaPolitikasi.Degerlendir("kkkkkkkkkkkkkkkk"); Assert.Contains(t.Ihlaller, x => x.Contains("Yeterince çeşitli")); Assert.Contains(t.Ihlaller, x => x.Contains("tekrar"));
    }

    [Theory]
    [InlineData("Zx9!abcdMk2$Lp", true)] [InlineData("Zx9!4321Mk2$Lp", true)] [InlineData("Zx9!qwerMk2$Lp", true)] [InlineData("Zx9!poiuMk2$Lp", true)] [InlineData("Zx9!aXbYMk2$Lp", false)]
    public void Ardisik_Ve_Klavye_Dizileri_Ileri_Ve_Geri_Yakalanir(string p, bool dizi) => Assert.Equal(dizi, ParolaPolitikasi.DiziVarMi(p));

    [Theory]
    [InlineData("aaaXk9$mQ2#vL7", true)] [InlineData("abcabcabcabc", true)] [InlineData("Xk9$mQ2#vL7!zP4w", false)]
    public void Tekrar_Yakalanir(string p, bool tekrar) => Assert.Equal(tekrar, ParolaPolitikasi.TekrarVarMi(p));

    [Fact]
    public void Yasakli_Sozcuk_Ve_Kullanici_Adi_Reddedilir()
    {
        var pol = new ParolaPolitika(12, 3, new() { "BulutSoft" });
        Assert.Contains(ParolaPolitikasi.Degerlendir("Xk9$BulutSoft#2z7Q", pol).Ihlaller, x => x.Contains("BulutSoft")); Assert.Contains(ParolaPolitikasi.Degerlendir("Xk9$bUlUtSoFt#2z7Q", pol).Ihlaller, x => x.Contains("adını"));
        Assert.Contains(ParolaPolitikasi.Degerlendir("Ahmet!Kx9$mQ2#vL", null, "ahmet").Ihlaller, x => x.Contains("ahmet")); Assert.DoesNotContain(ParolaPolitikasi.Degerlendir("Kx9$mQ2#vL7!zP4w", null, "ahmet").Ihlaller, x => x.Contains("ahmet"));
        // Çok kısa kullanıcı adı (2 harf) her parolada bulunabileceği için yok sayılır.
        Assert.True(ParolaPolitikasi.Degerlendir("Kx9$mQ2#vL7!zP4w", null, "Kx").Uygun);
    }

    [Fact]
    public void Bos_Cok_Uzun_Ve_Garip_Girdi_Cokmez_Ve_Hizli()
    {
        Assert.False(ParolaPolitikasi.Degerlendir("").Uygun); Assert.False(ParolaPolitikasi.Degerlendir(null).Uygun);
        var sw = System.Diagnostics.Stopwatch.StartNew(); var s = ParolaPolitikasi.Degerlendir(new string('a', 100000) + "Xk9$"); Assert.True(sw.ElapsedMilliseconds < 1500); Assert.False(s.Uygun);
        Assert.NotNull(ParolaPolitikasi.Degerlendir("😀😀😀😀😀😀😀😀😀😀😀😀😀😀")); Assert.NotNull(ParolaPolitikasi.Degerlendir("\0\r\n\t   ‮"));
    }

    [Fact]
    public void Politika_Degerleri_Guvenli_Araliga_Cekilir_Ve_Kaydedilir()
    {
        var d = ParolaPolitikasi.Duzelt(new ParolaPolitika(1, 9, new() { " ab ", "Sirket", "sirket", new string('x', 90), "  Kurum  " })); Assert.Equal(8, d.MinUzunluk); Assert.Equal(4, d.MinSinif); Assert.Equal(new[] { "Sirket", "Kurum" }, d.YasakliListe);
        Assert.Equal(64, ParolaPolitikasi.Duzelt(new ParolaPolitika(1000, 0)).MinUzunluk); Assert.Equal(1, ParolaPolitikasi.Duzelt(new ParolaPolitika(12, 0)).MinSinif);
        ParolaPolitikasi.Kaydet(new ParolaPolitika(16, 4, new() { "Firma" })); var y = ParolaPolitikasi.Yukle(); Assert.Equal(16, y.MinUzunluk); Assert.Equal(4, y.MinSinif); Assert.Equal("Firma", y.YasakliListe.Single());
        File.WriteAllText(ParolaPolitikasi.Yol, "{bozuk"); Assert.Equal(12, ParolaPolitikasi.Yukle().MinUzunluk);
    }

    [Fact]
    public void Politika_Siki_Olunca_Ayni_Parola_Reddedilir()
    {
        string p = "Kx9mQ2vL7zP4wR8t"; Assert.True(ParolaPolitikasi.Degerlendir(p, new ParolaPolitika(12, 2)).Uygun); Assert.False(ParolaPolitikasi.Degerlendir(p, new ParolaPolitika(20, 2)).Uygun); Assert.False(ParolaPolitikasi.Degerlendir(p, new ParolaPolitika(12, 4)).Uygun);
    }

    [Fact]
    public void Parola_Diske_Yazilmaz_Ve_Sonuca_Sizmaz()
    {
        // Parolanın kendisi hiçbir dosyada ve sonuç metinlerinde yer almamalı (yalnızca politika kaydedilir).
        string isaret = "Zq7#GizliParolaMarker9!"; var s = ParolaPolitikasi.Degerlendir(isaret); ParolaPolitikasi.Kaydet(new ParolaPolitika());
        Assert.DoesNotContain(isaret, string.Join("\n", s.Ihlaller.Concat(s.Oneriler)) + s.Seviye);
        foreach (var f in Directory.EnumerateFiles(BulutKlasor.Kok, "*", SearchOption.AllDirectories)) Assert.DoesNotContain(isaret, File.ReadAllText(f));
    }

    // ---------------- Hash ----------------
    string Dosya(string icerik) { var y = Path.Combine(_gecici, Guid.NewGuid().ToString("N") + ".bin"); File.WriteAllBytes(y, Encoding.ASCII.GetBytes(icerik)); return y; }
    static readonly HashTuru[] Hepsi = { HashTuru.MD5, HashTuru.SHA1, HashTuru.SHA256, HashTuru.SHA512 };

    [Fact]
    public async Task Bilinen_Test_Vektorleri_Dogru()
    {
        // RFC/NIST test vektörleri: "abc" ve boş girdi.
        var abc = await HashAraci.DosyaOzetleriAsync(Dosya("abc"), Hepsi, null, default);
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", abc[HashTuru.MD5]); Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", abc[HashTuru.SHA1]); Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", abc[HashTuru.SHA256]);
        Assert.Equal("ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f", abc[HashTuru.SHA512]);
        var bos = await HashAraci.DosyaOzetleriAsync(Dosya(""), new[] { HashTuru.MD5, HashTuru.SHA256 }, null, default);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", bos[HashTuru.MD5]); Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", bos[HashTuru.SHA256]);
    }

    [Fact]
    public async Task Buyuk_Dosya_Akisla_Okunur_Ilerleme_Bildirilir_Ve_Iptal_Edilir()
    {
        // 6 MB dosya: sonuç tek seferde okumakla aynı; ilerleme yüzde 100'e ulaşır; önceden iptal edilmiş belirteç işlemi durdurur.
        var y = Path.Combine(_gecici, "buyuk.bin"); var veri = new byte[6 * 1024 * 1024]; new Random(7).NextBytes(veri); File.WriteAllBytes(y, veri);
        var yuzdeler = new List<int>(); var ilr = new SenkronIlerleme(yuzdeler.Add); var r = await HashAraci.DosyaOzetleriAsync(y, new[] { HashTuru.SHA256 }, ilr, default);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(veri)).ToLowerInvariant(), r[HashTuru.SHA256]); Assert.Equal(100, yuzdeler.Max());
        using var iptal = new CancellationTokenSource(); iptal.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HashAraci.DosyaOzetleriAsync(y, new[] { HashTuru.SHA256 }, null, iptal.Token));
    }

    // Eşzamanlı ilerleme (Progress<T> arka planda çağırır; testte sıralı toplamak için).
    sealed class SenkronIlerleme : IProgress<int> { private readonly Action<int> _a; public SenkronIlerleme(Action<int> a) { _a = a; } public void Report(int v) => _a(v); }

    [Fact]
    public async Task Kilitli_Dosya_Okunur_Eksik_Dosya_Ve_Bos_Tur_Hata_Verir()
    {
        // Başka bir program yazmak için açıkken de okunabilmeli (paylaşımlı okuma).
        var y = Dosya("abc"); using (new FileStream(y, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", (await HashAraci.DosyaOzetleriAsync(y, new[] { HashTuru.SHA1 }, null, default))[HashTuru.SHA1]);
        await Assert.ThrowsAsync<FileNotFoundException>(() => HashAraci.DosyaOzetleriAsync(Path.Combine(_gecici, "yok.bin"), Hepsi, null, default)); await Assert.ThrowsAsync<ArgumentException>(() => HashAraci.DosyaOzetleriAsync(y, Array.Empty<HashTuru>(), null, default));
        await Assert.ThrowsAsync<FileNotFoundException>(() => HashAraci.DosyaOzetleriAsync(_gecici, Hepsi, null, default));   // klasör dosya değildir
    }

    [Theory]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    [InlineData("sha256: ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("SHA256=ba7816bf 8f01cfea 414140de 5dae2223 b00361a3 96177a9c b410ff61 f20015ad")]
    [InlineData("ba:78:16:bf:8f:01:cf:ea:41:41:40:de:5d:ae:22:23:b0:03:61:a3:96:17:7a:9c:b4:10:ff:61:f2:00:15:ad")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  indirilen dosya.iso")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad *kurulum.iso\r\n")]
    [InlineData("  ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  ")]
    public void Ozet_Yapistirma_Bicimleri_Tolere_Edilir(string ham) => Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", HashAraci.Normalize(ham));

    [Theory]
    [InlineData(32, "MD5")] [InlineData(40, "SHA1")] [InlineData(64, "SHA256")] [InlineData(128, "SHA512")] [InlineData(63, "")] [InlineData(0, "")]
    public void Tur_Uzunluktan_Tahmin_Edilir(int uzunluk, string beklenen) => Assert.Equal(beklenen, HashAraci.TurTahmin(new string('a', uzunluk))?.ToString() ?? "");

    [Fact]
    public async Task Karsilastirma_Eslesme_Uyusmazlik_Ve_Hatali_Girdi()
    {
        var hesap = await HashAraci.DosyaOzetleriAsync(Dosya("abc"), new[] { HashTuru.SHA256 }, null, default); const string dogru = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        var e = HashAraci.Karsilastir(hesap, dogru); Assert.True(e.Eslesti); Assert.Equal(HashTuru.SHA256, e.Tur); Assert.StartsWith("EŞLEŞTİ", e.Mesaj);
        var u = HashAraci.Karsilastir(hesap, dogru[..63] + "0"); Assert.False(u.Eslesti); Assert.StartsWith("UYUŞMUYOR", u.Mesaj); Assert.Contains("kullanmayın", u.Mesaj);
        Assert.Null(HashAraci.Karsilastir(hesap, "").Eslesti); Assert.Null(HashAraci.Karsilastir(hesap, "abc123").Eslesti); Assert.Contains("tanınmıyor", HashAraci.Karsilastir(hesap, "abc123").Mesaj);
        // Yazılan özet MD5 uzunluğunda ama MD5 hesaplanmadıysa: yönlendirme.
        var md5 = HashAraci.Karsilastir(hesap, "900150983cd24fb0d6963f7d28e17f72"); Assert.Null(md5.Eslesti); Assert.Contains("önce MD5", md5.Mesaj);
    }

    [Fact]
    public void Zayif_Ozetler_Icin_Uyari_Guclu_Icin_Degil_Ve_Esit_Uzunluk_Farki_Reddeder()
    {
        Assert.Contains("çarpışma", HashAraci.Not(HashTuru.MD5)); Assert.Contains("çarpışma", HashAraci.Not(HashTuru.SHA1)); Assert.Contains("güvenli", HashAraci.Not(HashTuru.SHA256));
        Assert.True(HashAraci.Esit("abcd", "abcd")); Assert.False(HashAraci.Esit("abcd", "abce")); Assert.False(HashAraci.Esit("abcd", "abc"));
    }
}
