// Site sahipliği doğrulama, doğrulanmış dış sitede nazik aktif denetim kapısı ve Apache/Nginx öneri testleri. Gerçek internete HİÇ çıkılmaz (sahte çözücü, sahte dosya/DNS, sahte sunucu).
using System.Net;
using LabDenetim.V2;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Statik durum (Kapsam.Cozucu, doğrulama oturumu, veri klasörü) paylaştığı için seri çalışır.
[Collection("Veri")]
public class SiteSahipligiTestleri : IDisposable
{
    // Eski ayarlar saklanır, test sonunda geri konur.
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _eskiCozucu = Kapsam.Cozucu; private readonly string _eskiKok = BulutKlasor.Kok;
    private readonly Func<string, CancellationToken, Task<string?>> _eskiDosya = SahiplikDogrulama.DosyaOku; private readonly Func<string, CancellationToken, Task<List<string>>> _eskiTxt = SahiplikDogrulama.TxtOku;
    public SiteSahipligiTestleri()
    {
        // Geçici veri klasörü ve genel IP'ye çözülen sahte DNS.
        BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "sh_" + Guid.NewGuid().ToString("N")); Kapsam.Cozucu = (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }); SahiplikDogrulama.OturumuTemizle();
        SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>(null); SahiplikDogrulama.TxtOku = (_, _) => Task.FromResult(new List<string>());
    }
    public void Dispose() { Kapsam.Cozucu = _eskiCozucu; SahiplikDogrulama.DosyaOku = _eskiDosya; SahiplikDogrulama.TxtOku = _eskiTxt; SahiplikDogrulama.OturumuTemizle(); try { Directory.Delete(BulutKlasor.Kok, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "SiteSahipligiTestleri.temizlik"); } BulutKlasor.Kok = _eskiKok; }

    [Fact]
    public void Belirtec_Rastgele_Kalici_Ve_Alan_Adina_Ozel()
    {
        // Aynı alan adı aynı kodu, farklı alan adı farklı kodu almalı; kod 128 bit rastgele.
        var a = SahiplikDogrulama.Belirtec("ornek.com"); Assert.Equal(a, SahiplikDogrulama.Belirtec("ORNEK.com"));
        Assert.NotEqual(a, SahiplikDogrulama.Belirtec("baska.com")); Assert.Matches("^bulutsoft-dogrulama=[0-9a-f]{32}$", a);
    }

    [Fact]
    public async Task Dosya_Yontemi_Dogru_Kodla_Dogrular_Ve_Oturum_Acar()
    {
        // Gizli kod https dosyasında varsa doğrulanır; oturum açılır.
        var b = SahiplikDogrulama.Belirtec("ornek.com"); string? sorulan = null;
        SahiplikDogrulama.DosyaOku = (u, _) => { sorulan = u; return Task.FromResult<string?>(u.StartsWith("https") ? b + "\n" : null); };
        Assert.False(SahiplikDogrulama.OturumDogrulu("ornek.com"));
        var s = await SahiplikDogrulama.DogrulaAsync("ornek.com", default);
        Assert.True(s.Dogrulandi); Assert.Equal("dosya", s.Yontem); Assert.Equal("https://ornek.com/.well-known/bulutsoft-dogrulama.txt", sorulan); Assert.True(SahiplikDogrulama.OturumDogrulu("ornek.com"));
    }

    [Fact]
    public async Task Yanlis_Kod_Dogrulamaz()
    {
        // Başkasının/yanlış kod geçersiz; oturum açılmaz.
        SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>("bulutsoft-dogrulama=00000000000000000000000000000000");
        SahiplikDogrulama.TxtOku = (_, _) => Task.FromResult(new List<string> { "v=spf1 -all" });
        var s = await SahiplikDogrulama.DogrulaAsync("ornek.com", default); Assert.False(s.Dogrulandi); Assert.False(SahiplikDogrulama.OturumDogrulu("ornek.com"));
    }

    [Fact]
    public async Task Dns_Yontemi_Dogrular_Ve_Dogru_Adi_Sorar()
    {
        // DNS TXT'de kod varsa doğrulanır; sorgulanan ad _bulutsoft.<alanadı> olmalı.
        var b = SahiplikDogrulama.Belirtec("ornek.com"); string? ad = null;
        SahiplikDogrulama.TxtOku = (a, _) => { ad = a; return Task.FromResult(new List<string> { "baska", b }); };
        var s = await SahiplikDogrulama.DogrulaAsync("ornek.com", default); Assert.True(s.Dogrulandi); Assert.Equal("dns", s.Yontem); Assert.Equal("_bulutsoft.ornek.com", ad);
    }

    [Fact]
    public async Task Alt_Alan_Ayri_Dogrulanir()
    {
        // ornek.com doğrulanınca alt.ornek.com doğrulanmış SAYILMAZ.
        var b = SahiplikDogrulama.Belirtec("ornek.com"); SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>(b);
        Assert.True((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi); Assert.False(SahiplikDogrulama.OturumDogrulu("alt.ornek.com"));
    }

    [Fact]
    public async Task Oturum_60_Dakikada_Sona_Erer()
    {
        // Süre dolunca kapı yeniden kapanmalı.
        SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>(SahiplikDogrulama.Belirtec("ornek.com"));
        Assert.True((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
        Assert.True(SahiplikDogrulama.OturumDogrulu("ornek.com", DateTime.UtcNow.AddMinutes(59))); Assert.False(SahiplikDogrulama.OturumDogrulu("ornek.com", DateTime.UtcNow.AddMinutes(61)));
    }

    [Fact]
    public async Task Ic_Ag_Ve_Ozel_Adres_Icin_Dogrulama_Istenmez_Ve_Oturum_Acilmaz()
    {
        // İç ağ adresinde doğrulama gerekmez; hiçbir oturum açılmaz.
        var s = await SahiplikDogrulama.DogrulaAsync("192.168.1.10", default); Assert.False(s.Dogrulandi); Assert.Contains("kendi ağınızda", s.Mesaj); Assert.False(SahiplikDogrulama.OturumDogrulu("192.168.1.10"));
    }

    [Fact]
    public async Task Karisik_Adres_Reddedilir()
    {
        // Hem özel hem genel IP'ye çözülen ad (DNS yeniden bağlama) asla doğrulanmaz.
        Kapsam.Cozucu = (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.5") });
        SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>(SahiplikDogrulama.Belirtec("ornek.com"));
        Assert.False((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
    }

    [Fact]
    public async Task Meta_Etiketi_Yontemi_Dogrular()
    {
        // Ana sayfanın <head> içindeki meta etiketinde kod varsa doğrulanır.
        var b = SahiplikDogrulama.Belirtec("ornek.com");
        SahiplikDogrulama.DosyaOku = (u, _) => Task.FromResult<string?>(u.EndsWith("/") ? $"<html><head><title>x</title><meta name=\"bulutsoft-dogrulama\" content=\"{b}\"></head></html>" : null);
        var s = await SahiplikDogrulama.DogrulaAsync("ornek.com", default); Assert.True(s.Dogrulandi); Assert.Equal("meta", s.Yontem);
    }

    [Fact]
    public async Task Kodun_Yalnizca_Sayfa_Yazisinda_Gecmesi_Meta_Sayilmaz()
    {
        // Kod etiket dışında (düz yazıda/yorumda) geçiyorsa doğrulama olmaz.
        var b = SahiplikDogrulama.Belirtec("ornek.com");
        SahiplikDogrulama.DosyaOku = (u, _) => Task.FromResult<string?>(u.EndsWith("/") ? $"<html><body><p>{b}</p><!-- {b} --></body></html>" : null);
        Assert.False((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
    }

    [Fact]
    public async Task Kok_Dizindeki_Dosya_Yontemi_Calisir()
    {
        // .well-known engelliyse kök dizindeki dosya yeter.
        var b = SahiplikDogrulama.Belirtec("ornek.com"); SahiplikDogrulama.DosyaOku = (u, _) => Task.FromResult<string?>(u == "https://ornek.com/bulutsoft-dogrulama.txt" ? b : null);
        Assert.True((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
    }

    [Fact]
    public async Task Dns_Alan_Adinin_Kendisinde_De_Aranir()
    {
        // _ ile başlayan ad kabul edilmeyen DNS panelleri için alan adının kendi TXT kaydı da geçerli.
        var b = SahiplikDogrulama.Belirtec("ornek.com"); SahiplikDogrulama.TxtOku = (a, _) => Task.FromResult(a == "ornek.com" ? new List<string> { b } : new List<string>());
        var s = await SahiplikDogrulama.DogrulaAsync("ornek.com", default); Assert.True(s.Dogrulandi); Assert.Equal("dns", s.Yontem);
    }

    [Fact]
    public async Task Basarisizlikta_Denenen_Yollar_Listelenir()
    {
        // Kullanıcı neyin neden olmadığını görebilmeli.
        var s = await SahiplikDogrulama.DogrulaAsync("ornek.com", default); Assert.False(s.Dogrulandi);
        Assert.Contains("bulutsoft-dogrulama.txt", s.Mesaj); Assert.Contains("ana sayfa", s.Mesaj); Assert.Contains("DNS TXT _bulutsoft.ornek.com", s.Mesaj);
    }

    [Fact]
    public void Not_Defteri_Unicode_Kaydi_Ve_Bom_Okunur()
    {
        // UTF-16 (Not Defteri "Unicode") ve UTF-8 BOM'lu dosyalar doğru metne çevrilmeli.
        var kod = "bulutsoft-dogrulama=0123456789abcdef0123456789abcdef";
        var u16 = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(kod + "\r\n")).ToArray(); var u8 = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes(kod)).ToArray();
        Assert.Contains(kod, SahiplikDogrulama.MetneCevir(u16, u16.Length)); Assert.Equal(kod, SahiplikDogrulama.MetneCevir(u8, u8.Length));
    }

    [Fact]
    public async Task Yalnizca_Hex_Kismi_Yazilsa_Da_Kabul_Edilir()
    {
        // Kullanıcı 'bulutsoft-dogrulama=' önekini yazmayı unutursa da kod doğru sayılır; yanlış hex sayılmaz.
        var b = SahiplikDogrulama.Belirtec("ornek.com"); var hex = b[(b.IndexOf('=') + 1)..];
        SahiplikDogrulama.DosyaOku = (u, _) => Task.FromResult<string?>(u.Contains("well-known") ? hex + "\n" : null);
        Assert.True((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
        SahiplikDogrulama.OturumuTemizle(); SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>("00000000000000000000000000000000");
        Assert.False((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
    }

    [Fact]
    public void Www_Yonlendirmesi_Ayni_Aile_Baska_Alan_Degil()
    {
        // ornek.com <-> www.ornek.com aynı aile; başka alan adı değil.
        Assert.True(SahiplikDogrulama.AyniAile("www.ornek.com", "ornek.com")); Assert.True(SahiplikDogrulama.AyniAile("ornek.com", "WWW.ornek.com")); Assert.False(SahiplikDogrulama.AyniAile("evil.com", "ornek.com")); Assert.False(SahiplikDogrulama.AyniAile("ornek.com.evil.com", "ornek.com"));
    }

    // ---- Aktif denetim kapısı ----
    [Fact]
    public async Task Dogrulanmamis_Dis_Site_Hala_Kilitli_Ve_Hic_Istek_Gitmez()
    {
        // Doğrulama yoksa dış ağ hedefine hiç istek gönderilmeden durulmalı.
        var s = new SahteSunucu(); await Assert.ThrowsAsync<KapsamIhlaliException>(() => AktifHttp.GozlemAsync("https://ornek.com/", s, null, 0)); Assert.Empty(s.Kayit);
    }

    [Fact]
    public async Task Dogrulanmis_Dis_Site_Nazik_Aktif_Denetimden_Gecer_Ve_Yalniz_Izinli_Yontemler_Gider()
    {
        // Doğrulanınca denetim çalışır; yalnızca GET/OPTIONS/TRACE gönderilir; açıklar bulunur.
        SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>(SahiplikDogrulama.Belirtec("ornek.com")); Assert.True((await SahiplikDogrulama.DogrulaAsync("ornek.com", default)).Dogrulandi);
        var s = new SahteSunucu(); s.Yollar["/"] = (200, "<html>Ana</html>", new() { ["Server"] = "nginx/1.18.0" }); s.Yollar["/.env"] = (200, "DB_PASSWORD=x\n", null);
        var g = await AktifHttp.GozlemAsync("https://ornek.com/", s, null, 0);
        Assert.All(s.Kayit, k => Assert.Contains(k.Yontem, new[] { "GET", "OPTIONS", "TRACE" })); Assert.Equal("found", g.Olgular["probe:path:/.env"]);
        var b = AktifHttp.Degerlendir(g); Assert.Contains(b, x => x.RuleId == "ACT-ENV-EXPOSED");
    }

    [Fact]
    public async Task Dogrulanmis_Site_Icin_Baska_Alan_Adi_Yine_Kilitli()
    {
        // ornek.com doğrulandı ama baska.com için kapı kapalı kalmalı.
        SahiplikDogrulama.DosyaOku = (_, _) => Task.FromResult<string?>(SahiplikDogrulama.Belirtec("ornek.com")); await SahiplikDogrulama.DogrulaAsync("ornek.com", default);
        await Assert.ThrowsAsync<KapsamIhlaliException>(() => AktifHttp.GozlemAsync("https://baska.com/", new SahteSunucu(), null, 0));
    }

    // ---- Apache / Nginx önerileri ----
    [Fact]
    public void Her_Aktif_Ve_Pasif_Kural_Icin_Oneri_Var_Ve_Iki_Sunucuyu_Kapsar()
    {
        // Tüm kural kimlikleri için öneri bulunmalı (yeni kural eklenince unutulmasın).
        var idler = new List<string>(); foreach (var k in PasifKurallar.Yukle(out _)) idler.Add(k.Id); foreach (var k in PasifKurallar.Yukle(out _, null, "aktif_kurallar.json", BulutKlasor.Dosya("aktif_kurallar.json"))) idler.Add(k.Id);
        Assert.NotEmpty(idler); var eksik = idler.Where(i => !SunucuOnerileri.Var(i)).ToList(); Assert.True(eksik.Count == 0, "Önerisi olmayan kurallar: " + string.Join(", ", eksik));
        foreach (var i in idler) { var m = SunucuOnerileri.Metin(i, "ornek.com")!; Assert.Contains("--- Apache ---", m); Assert.Contains("--- Nginx ---", m); Assert.DoesNotContain("{HOST}", m); }
    }

    [Fact]
    public void Onemli_Oneriler_Dogru_Direktifleri_Icerir()
    {
        // Bilinen doğru direktifler.
        Assert.Contains("TraceEnable off", SunucuOnerileri.Metin("ACT-TRACE-ENABLED", "a.com")); Assert.Contains("server_tokens off;", SunucuOnerileri.Metin("LEAK-SERVER-VERSION", "a.com")); Assert.Contains("ServerTokens Prod", SunucuOnerileri.Metin("LEAK-SERVER-VERSION", "a.com"));
        Assert.Contains("Options -Indexes", SunucuOnerileri.Metin("ACT-DIR-LISTING", "a.com")); Assert.Contains("autoindex off;", SunucuOnerileri.Metin("ACT-DIR-LISTING", "a.com"));
        Assert.Contains("return 301 https://$host$request_uri;", SunucuOnerileri.Metin("REDIR-HTTP-TO-HTTPS", "a.com")); Assert.Contains("ssl_protocols TLSv1.2 TLSv1.3;", SunucuOnerileri.Metin("TLS-OLD-PROTOCOL", "a.com"));
        Assert.Null(SunucuOnerileri.Metin("YOK-BOYLE-KURAL", "a.com"));
    }

    [Fact]
    public void Zenginlestir_Komutu_Korur_Ve_Eklemeyi_Yapar()
    {
        // Var olan örnek korunmalı, öneri eklenmeli; önerisiz bulgu aynen kalmalı.
        var b1 = new PasifBulgu("HDR-HSTS-MISSING", "Pasif", "t", "n", "z", "c", "mevcut komut", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 5, "k", 5, Onem.Orta);
        var b2 = b1 with { RuleId = "BILINMEYEN" }; var s = SunucuOnerileri.Zenginlestir(new[] { b1, b2 }, "a.com");
        Assert.StartsWith("mevcut komut\n--- Apache ---", s[0].Komut); Assert.Equal("mevcut komut", s[1].Komut);
    }

    [Fact]
    public void Toplu_Dosya_Secilen_Sunucuya_Ozel_Ve_Yinelenmez()
    {
        // Apache dosyasında Nginx direktifi olmamalı ve tersi; aynı metin iki kez yazılmamalı; uyarı başlığı bulunmalı.
        var idler = new[] { "LEAK-SERVER-VERSION", "ACT-SERVER-VERSION", "ACT-DIR-LISTING", "HDR-XCTO-MISSING" };
        var ap = SunucuOnerileri.TopluDosya(idler, "a.com", SunucuTuru.Apache); var ng = SunucuOnerileri.TopluDosya(idler, "a.com", SunucuTuru.Nginx);
        Assert.Contains("Options -Indexes", ap); Assert.DoesNotContain("autoindex", ap); Assert.Contains("autoindex off;", ng); Assert.DoesNotContain("ServerTokens", ng);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(ng, "server_tokens off;")); Assert.Contains("OTOMATİK UYGULANMAZ", ap);
        Assert.Contains("çıkmadı", SunucuOnerileri.TopluDosya(Array.Empty<string>(), "a.com", SunucuTuru.Nginx));
    }
}
