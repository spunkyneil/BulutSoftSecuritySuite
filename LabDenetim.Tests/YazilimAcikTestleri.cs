// Hafta 6 testleri: sürüm karşılaştırma, CPE eşleme (güven puanlı), NVD içe aktarma (atomik), açık eşleştirme, yanlış alarm savunması, doğrulama.
using System.Text;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class YazilimAcikTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    // ---- Sürüm karşılaştırma ----
    [Theory]
    [InlineData("2.4.49", "2.4.51", -1)] [InlineData("2.4.51", "2.4.49", 1)] [InlineData("2.4", "2.4.0", 0)] [InlineData("10.0", "9.9.9", 1)] [InlineData("23.01 (x64)", "23.1", 0)] [InlineData("v2.15.0-beta1", "2.15.0", 0)] [InlineData("1.10", "1.9", 1)]
    public void Surum_Kiyas(string a, string b, int beklenen) => Assert.Equal(beklenen, Math.Sign(SurumKiyasi.Kiyasla(a, b)!.Value));
    [Theory] [InlineData("")] [InlineData(null)] [InlineData("bilinmiyor")] [InlineData("abc.1")] [InlineData("99999999999999999999.1")]
    public void Surum_Cozulemez_Null(string? s) { Assert.Null(SurumKiyasi.Coz(s)); Assert.Null(SurumKiyasi.Kiyasla(s, "1.0")); }

    // ---- CPE eşleme ----
    [Fact] public void Cpe_Tablo_YuksekGuven()
    {
        var a = CpeMapper.Esle("7-Zip 23.01 (x64 edition)", "Igor Pavlov")!; Assert.Equal(("7-zip", "7-zip"), (a.Vendor, a.Product)); Assert.Equal("tablo", a.Yontem); Assert.True(a.Guven >= 0.8);
        Assert.Equal("chrome", CpeMapper.Esle("Google Chrome", "Google LLC")!.Product); Assert.Equal("vlc_media_player", CpeMapper.Esle("VLC media player", "VideoLAN")!.Product);
    }
    [Fact] public void Cpe_BenzerAdliAyriUrunler_YuksekGuvenAlmaz()
    {
        // Gerçek makinede görülen yanlış eşleşme riski: "OpenVPN Connect" farklı bir üründür; Python'un yardımcı bileşenleri çekirdek yorumlayıcı değildir.
        Assert.Equal("tahmin", CpeMapper.Esle("OpenVPN Connect", "OpenVPN Inc.")!.Yontem); Assert.Equal("openvpn", CpeMapper.Esle("OpenVPN 2.6.8", "OpenVPN Technologies")!.Product);
        foreach (var ad in new[] { "Python 3.14.2 Standard Library (64-bit)", "Python 3.14.2 Documentation (64-bit)", "Python Launcher", "Python 3.14.2 pip Bootstrap (64-bit)" }) Assert.NotEqual("tablo", CpeMapper.Esle(ad, "Python Software Foundation")!.Yontem);
        Assert.Equal("tablo", CpeMapper.Esle("Python 3.14.2 (64-bit)", "Python Software Foundation")!.Yontem);
    }
    [Fact] public void Cpe_Tahmin_DusukGuven_VeBosAd()
    {
        var t = CpeMapper.Esle("Süper Bilinmeyen Araç 4.2 (x64)", "Örnek Yazılım Ltd.")!; Assert.Equal("tahmin", t.Yontem); Assert.True(t.Guven < VulnMatcher.GuvenEsigi);
        Assert.Null(CpeMapper.Esle("", "x")); Assert.Null(CpeMapper.Esle(null, "x"));
    }
    [Fact] public void Cpe_KullaniciTablosu_OncelikliVeBozukSatirAtlanir()
    {
        string kul = "[{\"ad\":\"^7-Zip\",\"vendor\":\"ozel\",\"product\":\"yedi\",\"guven\":0.95},{\"ad\":\"(\",\"vendor\":\"x\",\"product\":\"y\"},{\"ad\":\"^Foo\",\"vendor\":\"BÜYÜK;rm\",\"product\":\"y\"}]";
        var a = CpeMapper.Esle("7-Zip 23", "x", kul)!; Assert.Equal("ozel", a.Vendor); Assert.Equal(0.95, a.Guven);
        Assert.Equal("tahmin", CpeMapper.Esle("Foo 1.0", "Bar", kul)!.Yontem);   // geçersiz vendor satırı atlandı
    }

    // ---- NVD içe aktarma ----
    static string Nvd(params string[] kayitlar) => "{\"resultsPerPage\":1,\"format\":\"NVD_CVE\",\"version\":\"2.0\",\"vulnerabilities\":[" + string.Join(",", kayitlar) + "]}";
    static string Cve(string id, double puan, string cpeMatchJson, string acik = "Test açığı") => $"{{\"cve\":{{\"id\":\"{id}\",\"descriptions\":[{{\"lang\":\"en\",\"value\":\"{acik}\"}}],\"metrics\":{{\"cvssMetricV31\":[{{\"cvssData\":{{\"baseScore\":{puan.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}}}]}},\"configurations\":[{{\"nodes\":[{{\"operator\":\"OR\",\"cpeMatch\":[{cpeMatchJson}]}}]}}]}}}}";
    static string Esl(string cpe, string? bd = null, string? bh = null, string? td = null, string? th = null, bool zafiyetli = true)
        => "{\"vulnerable\":" + (zafiyetli ? "true" : "false") + ",\"criteria\":\"" + cpe + "\"" + (bd != null ? $",\"versionStartIncluding\":\"{bd}\"" : "") + (bh != null ? $",\"versionStartExcluding\":\"{bh}\"" : "") + (td != null ? $",\"versionEndIncluding\":\"{td}\"" : "") + (th != null ? $",\"versionEndExcluding\":\"{th}\"" : "") + "}";

    [Fact] public void Nvd_Ekle_AralikTekSurumVeFiltreler()
    {
        var i = new NvdIndeks();
        int n = NvdIndeks.Ekle(i, Nvd(
            Cve("CVE-2021-44228", 10.0, Esl("cpe:2.3:a:apache:log4j:*:*:*:*:*:*:*:*", "2.0", null, null, "2.15.0")),
            Cve("CVE-2024-0001", 7.5, Esl("cpe:2.3:a:7-zip:7-zip:22.01:*:*:*:*:*:*:*")),
            Cve("CVE-2024-0002", 5.0, Esl("cpe:2.3:o:microsoft:windows_10:*:*:*:*:*:*:*:*")),                 // işletim sistemi: alınmaz
            Cve("CVE-2024-0003", 5.0, Esl("cpe:2.3:a:foo:bar:*:*:*:*:*:*:*:*", zafiyetli: false)),               // zafiyetli değil: alınmaz
            Cve("CVE-2024-0004", 6.0, Esl("cpe:2.3:a:notepad-plus-plus:notepad\\\\+\\\\+:*:*:*:*:*:*:*:*", null, null, null, "8.5.0"))));
        Assert.Equal(3, n); Assert.Equal(3, i.CveSayisi);
        var l4j = i.Urunler["apache:log4j"].Single(); Assert.Equal(("2.0", "2.15.0"), (l4j.BasDahil, l4j.BitHaric)); Assert.Equal(10.0, l4j.Cvss); Assert.Equal("Test açığı", l4j.Aciklama);
        var z = i.Urunler["7-zip:7-zip"].Single(); Assert.Equal(("22.01", "22.01"), (z.BasDahil, z.BitDahil));
        Assert.True(i.Urunler.ContainsKey("notepad-plus-plus:notepad++")); Assert.False(i.Urunler.ContainsKey("foo:bar")); Assert.False(i.Urunler.ContainsKey("microsoft:windows_10"));
    }

    [Theory]
    [InlineData("")] [InlineData("{bozuk")] [InlineData("[]")] [InlineData("{\"x\":1}")] [InlineData("{\"vulnerabilities\":[{\"cve\":{\"id\":\"HATA\"}}]}")] [InlineData("{\"vulnerabilities\":[{\"x\":1}]}")]
    public void Nvd_Bozuk_Reddedilir(string json) => Assert.Throws<KatalogHatasi>(() => NvdIndeks.Ekle(new NvdIndeks(), json));

    static string Gecici(string icerik) { string y = Path.Combine(Path.GetTempPath(), "nvd_" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(y, icerik, Encoding.UTF8); return y; }

    [Fact] public void Nvd_IceAktar_Atomik_BozukDosyaEskiIndeksiKorur_VeBirlestirir()
    {
        string eski = BulutKlasor.Kok, g = Path.Combine(Path.GetTempPath(), "nvdi_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = g; var dosyalar = new List<string>();
        try
        {
            string a = Gecici(Nvd(Cve("CVE-2021-44228", 10, Esl("cpe:2.3:a:apache:log4j:*:*:*:*:*:*:*:*", "2.0", null, null, "2.15.0")))), b = Gecici(Nvd(Cve("CVE-2024-0001", 7.5, Esl("cpe:2.3:a:7-zip:7-zip:22.01:*:*:*:*:*:*:*")))), kotu = Gecici("{bozuk"); dosyalar.AddRange(new[] { a, b, kotu });
            Assert.Null(NvdIndeks.Yukle());
            var i1 = NvdIndeks.IceAktar(new[] { a }, T0); Assert.Equal(1, i1.CveSayisi); Assert.Equal(64, i1.KaynakOzeti.Length);
            // İkinci içe aktarma BİRLEŞTİRİR.
            var i2 = NvdIndeks.IceAktar(new[] { b }, T0); Assert.Equal(2, i2.CveSayisi); Assert.Equal(2, i2.DosyaSayisi); Assert.Equal(2, NvdIndeks.Yukle()!.Urunler.Count);
            // Aynı dosya tekrar yüklenirse çoğalmaz.
            Assert.Equal(2, NvdIndeks.IceAktar(new[] { a }, T0).CveSayisi); Assert.Single(NvdIndeks.Yukle()!.Urunler["apache:log4j"]);
            // İyi + kötü birlikte: TÜMÜ reddedilir, indeks değişmez.
            string once = File.ReadAllText(NvdIndeks.Yol);
            Assert.Throws<KatalogHatasi>(() => NvdIndeks.IceAktar(new[] { b, kotu }, T0)); Assert.Equal(once, File.ReadAllText(NvdIndeks.Yol)); Assert.False(File.Exists(NvdIndeks.Yol + ".tmp"));
            Assert.Throws<KatalogHatasi>(() => NvdIndeks.IceAktar(new[] { Path.Combine(g, "yok.json") }, T0));
        }
        finally { BulutKlasor.Kok = eski; foreach (var d in dosyalar) try { File.Delete(d); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "YazilimAcikTestleri.temizlik"); } try { Directory.Delete(g, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "YazilimAcikTestleri.temizlik"); } }
    }

    // ---- Aralık eşleştirme ----
    [Fact] public void Aralikta_SinirDurumlari()
    {
        NvdAralik A(string? bd, string? bh, string? td, string? th) => new("CVE-2020-0001", 5, bd, bh, td, th, "");
        Assert.True(VulnMatcher.Aralikta("2.14.1", A("2.0", null, null, "2.15.0"))); Assert.False(VulnMatcher.Aralikta("2.15.0", A("2.0", null, null, "2.15.0"))); Assert.False(VulnMatcher.Aralikta("1.9", A("2.0", null, null, "2.15.0")));
        Assert.True(VulnMatcher.Aralikta("2.0", A("2.0", null, null, null))); Assert.False(VulnMatcher.Aralikta("2.0", A(null, "2.0", null, null))); Assert.True(VulnMatcher.Aralikta("2.15.0", A(null, null, "2.15.0", null)));
        Assert.True(VulnMatcher.Aralikta("9.9", A(null, null, null, null)));        // sınırsız: tüm sürümler
        Assert.False(VulnMatcher.Aralikta("bilinmiyor", A(null, null, null, null))); Assert.False(VulnMatcher.Aralikta("2.0", A("garip", null, null, null)));   // çözülemeyen sınır eşleşmez
    }

    // ---- Eşleştirici ----
    static InstalledSoftware Yaz(string ad, string? surum, string cpe = "apache:log4j", double guven = 0.9) => new(ad, surum, "Yayıncı", null, "HKLM\\x", cpe, guven);
    static NvdIndeks Indeks() { var i = new NvdIndeks(); NvdIndeks.Ekle(i, Nvd(Cve("CVE-2021-44228", 10.0, Esl("cpe:2.3:a:apache:log4j:*:*:*:*:*:*:*:*", "2.0", null, null, "2.15.0"), "Log4Shell"), Cve("CVE-2022-0001", 6.5, Esl("cpe:2.3:a:foo:bar:*:*:*:*:*:*:*:*", null, null, null, "3.0")))); i.KaynakOzeti = new string('a', 64); i.DosyaSayisi = 1; return i; }
    static VulnFeedStore Kev() => VulnFeedStore.Coz("{\"catalogVersion\":\"2026.11.17\",\"vulnerabilities\":[{\"cveID\":\"CVE-2021-44228\",\"vendorProject\":\"Apache\",\"product\":\"Log4j2\",\"vulnerabilityName\":\"Log4Shell\",\"dateAdded\":\"2021-12-10\"}]}");
    static VulnEslesmeSonucu Esle(IReadOnlyList<InstalledSoftware> l, NvdIndeks? n, VulnFeedStore? k, double agirlik = 0.5) => VulnMatcher.Esle(l, n, k, "host:T", agirlik, T0);

    [Fact] public void KevliSurum_Yuksek_YamaliSurum_BulguYok()
    {
        var s = Esle(new[] { Yaz("Log4j", "2.14.1") }, Indeks(), Kev(), 1.0); var f = Assert.Single(s.Bulgular);
        Assert.Equal("SW-KEV-001", f.RuleId); Assert.Equal(9, f.ImpactWeight); Assert.Equal(90, f.Score); Assert.Equal(Onem.Yuksek, f.EskiOnem); Assert.Empty(FindingSchema.Validate(f));
        Assert.Contains(f.Evidence, e => e.Contains("CVE-2021-44228") && e.Contains("KEV")); Assert.Contains(f.Evidence, e => e.Contains("2026.11.17")); Assert.Contains(f.Evidence, e => e.Contains("aaaaaaaaaaaa"));   // besleme sürümü ve özeti raporda
        Assert.Empty(Esle(new[] { Yaz("Log4j", "2.15.0") }, Indeks(), Kev()).Bulgular);   // yamalı sürüm
        Assert.Empty(Esle(new[] { Yaz("Log4j", "2.17.1") }, Indeks(), Kev()).Bulgular);
    }
    [Fact] public void KevsizAmaYuksekCvss_Orta_ve_DigerSeviyeler()
    {
        var s = Esle(new[] { Yaz("Log4j", "2.14.1") }, Indeks(), null).Bulgular.Single(); Assert.Equal("SW-VULN-001", s.RuleId); Assert.Equal(8, s.ImpactWeight);   // CVSS 10, KEV yok
        var d = Esle(new[] { Yaz("Bar", "2.0", "foo:bar") }, Indeks(), null).Bulgular.Single(); Assert.Equal(4, d.ImpactWeight);                                       // CVSS 6,5
    }
    [Fact] public void DusukGuven_AsistYuksekOlmaz_Basligi_DusukGuvenDer()
    {
        var f = Esle(new[] { Yaz("Log4j benzeri araç", "2.14.1", "apache:log4j", 0.4) }, Indeks(), Kev(), 1.0).Bulgular.Single();
        Assert.Equal("SW-LOWCONF-001", f.RuleId); Assert.True(f.ImpactWeight <= 4); Assert.True(f.Score <= 40); Assert.NotEqual(Onem.Yuksek, f.EskiOnem); Assert.Contains("düşük güven", f.Title); Assert.Contains("tahmine dayalı", f.Explanation.WhyItMatters);
    }
    [Fact] public void SurumCozulemezse_BulguYok_NotVar_VeCpeYoksaAtlanir()
    {
        var s = Esle(new[] { Yaz("A", null), Yaz("B", "bilinmiyor"), new InstalledSoftware("C", "1.0", null, null, "k", null, 0) }, Indeks(), Kev());
        Assert.Empty(s.Bulgular); Assert.Equal(3, s.Notlar.Count); Assert.Contains(s.Notlar, n => n.Contains("sürüm bilinmiyor")); Assert.Contains(s.Notlar, n => n.Contains("CPE"));
    }
    [Fact] public void HicVeriYoksa_NoFeed_Bilgi_SessizTemizDemez()
    {
        var f = Assert.Single(Esle(new[] { Yaz("Log4j", "2.14.1") }, null, null).Bulgular); Assert.Equal("SW-NOFEED-001", f.RuleId); Assert.Equal(0, f.Score); Assert.Empty(FindingSchema.Validate(f));
    }
    [Fact] public void SadeceKev_UrunAdiEslesmesi_DusukOnem_SurumDogrulanamadi()
    {
        var f = Esle(new[] { Yaz("Log4j 2.x", "2.14.1", "apache:log4j2") }, null, Kev(), 1.0).Bulgular.Single();
        Assert.Equal("SW-KEVPRODUCT-001", f.RuleId); Assert.Equal(3, f.ImpactWeight); Assert.Contains("doğrulanamadı", f.Title); Assert.Contains(f.Evidence, e => e.Contains("NVD verisi yüklenmeli"));
    }
    [Fact] public void NvdVarAmaUrunYok_KevAdiyleSessizBulguUretilmez()
    {
        // NVD indeksi VAR ve bu ürünü içeriyor → sürüm yamalıysa KEV ürün adı eşleşmesi ek bulgu üretmez.
        var s = Esle(new[] { Yaz("Log4j", "2.17.1", "apache:log4j") }, Indeks(), Kev()); Assert.Empty(s.Bulgular);
    }

    // ---- Doğrulama ----
    [Fact] public async Task Dogrulama_YazilimGuncellenince_Pass()
    {
        var ind = Indeks(); var kev = Kev(); var l = new List<InstalledSoftware> { Yaz("Log4j", "2.14.1") };
        var once = Esle(l, ind, kev).Bulgular; Assert.Single(once);
        var v = new VerificationService(); v.Kaydet(new YazilimDogrulamaKontrolu(() => Esle(l, ind, kev).Bulgular));
        Assert.Equal(VerificationOutcome.Fail, (await v.CalistirAsync(VulnMatcher.KontrolKimligi, once, default)).Aggregate);
        l[0] = Yaz("Log4j", "2.17.1");   // güncellendi
        var r = await v.CalistirAsync(VulnMatcher.KontrolKimligi, once, default); Assert.Equal(VerificationOutcome.Pass, r.Aggregate); Assert.Equal(FindingState.Verified, VerificationService.DurumUygula(once, r, T0)[0].State);
    }

    // ---- Yanlış alarm oranı (etiketli 200 örnekli envanter) ----
    [Fact] public void EtiketliEnvanter_YuksekBulgularda_YanlisAlarmOrani_YuzdeOnuAsmaz()
    {
        var ind = Indeks(); var kev = Kev(); var l = new List<InstalledSoftware>(); var gercekAcik = new HashSet<string>();
        for (int i = 0; i < 50; i++) { string ad = "Gercek-" + i; l.Add(new InstalledSoftware(ad, "2.14." + i, "x", null, "k" + i, "apache:log4j", 0.9)); gercekAcik.Add(ad); }          // 50 gerçekten açıklı, güvenilir eşleme
        for (int i = 0; i < 50; i++) l.Add(new InstalledSoftware("Yamali-" + i, "2.17." + i, "x", null, "y" + i, "apache:log4j", 0.9));                                         // 50 yamalı
        for (int i = 0; i < 50; i++) l.Add(new InstalledSoftware("YanlisEsleme-" + i, "2.14." + i, "x", null, "z" + i, "apache:log4j", 0.4));                                   // 50 tahmini eşleme: gerçekte başka ürün (hepsi yanlış alarm adayı)
        for (int i = 0; i < 50; i++) l.Add(new InstalledSoftware("SurumsuzArac-" + i, i % 2 == 0 ? null : "?", "x", null, "s" + i, "apache:log4j", 0.9));                       // sürümsüz
        var s = Esle(l, ind, kev, 1.0);
        var yuksek = s.Bulgular.Where(f => f.EskiOnem == Onem.Yuksek).ToList(); int yanlis = yuksek.Count(f => !gercekAcik.Contains(f.Title.Split(' ')[0]));
        Assert.Equal(50, yuksek.Count); Assert.True(yuksek.Count == 0 || yanlis / (double)yuksek.Count <= 0.10, $"Yüksek bulgularda yanlış alarm oranı: {yanlis}/{yuksek.Count}");
        Assert.Equal(200, l.Count); Assert.DoesNotContain(s.Bulgular.Where(f => f.Title.StartsWith("YanlisEsleme")), f => f.EskiOnem == Onem.Yuksek);
    }

    // ---- Akışa entegrasyon ----
    [Fact] public void YerelAkis_YazilimBulgusuVeHaritadaYazilimListesi()
    {
        var k = new SahteYerel(); k.Yaz.Add(Yaz("Log4j", "2.14.1")); var s = YerelDenetimAkisi.Calistir(k, null, null, T0, null, Indeks(), Kev());
        Assert.Contains(s.Bulgular, f => f.RuleId == "SW-KEV-001"); Assert.Equal(1, s.YazilimSayisi); Assert.Equal("Log4j", s.Harita.Assets.Single().Software.Single().Name); Assert.Empty(s.Reddedilen);
    }

    // ---- Gerçek envanter (salt-okunur; varsayılan kapalı) ----
    [Fact] public void Canli_GercekEnvanter_SaltOkunur()
    {
        if (Environment.GetEnvironmentVariable("BULUT_CANLI_TEST") != "1") return;
        var l = SoftwareInventoryCollector.Topla(); Console.WriteLine($"CANLI-ENVANTER: {l.Count} yazılım, CPE'li: {l.Count(x => x.CandidateCpe != null)}, tablo güvenli (≥0,6): {l.Count(x => x.CpeConfidence >= VulnMatcher.GuvenEsigi)}");
        foreach (var x in l.Where(x => x.CpeConfidence >= VulnMatcher.GuvenEsigi).Take(12)) Console.WriteLine($"  {x.Name} {x.Version} -> {x.CandidateCpe} ({x.CpeConfidence:0.0})");
        Assert.NotEmpty(l);
    }
}
