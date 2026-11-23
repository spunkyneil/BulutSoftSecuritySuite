// Bildirim merkezi testleri: doğrulama, DPAPI saklama, kanal biçimleri, gizlilik (KISA mod), seviye/tekrar/saatlik sınır, yeniden deneme, eski webhook'tan geçiş. Gerçek ağ KULLANILMAZ.
using System.Text.Json;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class WebhookYoneticisiTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_wh_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol;
    public WebhookYoneticisiTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); WebhookYoneticisi.Sifirla(); }
    public void Dispose() { WebhookYoneticisi.Sifirla(); BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "WebhookYoneticisiTestleri.temizlik"); } }
    static readonly DateTime T0 = new(2026, 11, 19, 12, 0, 0);
    const string Token = "123456789:AAH-abcdefghijklmnopqrstuvwxyz012345", Discord = "https://discord.com/api/webhooks/123456789012345678/AbCdEfGhIjKlMnOpQrStUvWxYz_0123456789", Teams = "https://prod-12.westeurope.logic.azure.com:443/workflows/abc123/triggers/manual/paths/invoke?sig=SECRET";

    // ---- Doğrulama ----
    [Theory]
    [InlineData(KanalTuru.Telegram, Token, "-1001234567890", true)] [InlineData(KanalTuru.Telegram, Token, "@kanalim", true)] [InlineData(KanalTuru.Telegram, "bozuk", "123456", false)] [InlineData(KanalTuru.Telegram, Token, "abc", false)] [InlineData(KanalTuru.Telegram, "", "", false)]
    [InlineData(KanalTuru.Discord, Discord, "", true)] [InlineData(KanalTuru.Discord, "http://discord.com/api/webhooks/1/x", "", false)] [InlineData(KanalTuru.Discord, "https://evil.example/api/webhooks/1/x", "", false)] [InlineData(KanalTuru.Discord, "https://discord.com.evil.example/api/webhooks/1/x", "", false)] [InlineData(KanalTuru.Discord, "https://user:pw@discord.com/api/webhooks/1/x", "", false)] [InlineData(KanalTuru.Discord, "https://discord.com/baska/yol", "", false)]
    [InlineData(KanalTuru.Teams, Teams, "", true)] [InlineData(KanalTuru.Teams, "https://contoso.webhook.office.com/webhookb2/abc", "", true)] [InlineData(KanalTuru.Teams, "https://evil.example/logic.azure.com", "", false)] [InlineData(KanalTuru.Teams, "http://x.logic.azure.com/a", "", false)]
    [InlineData(KanalTuru.Genel, "https://hooks.slack.com/services/T/B/x", "", true)] [InlineData(KanalTuru.Genel, "http://hooks.example/x", "", false)] [InlineData(KanalTuru.Genel, "https://localhost/x", "", false)] [InlineData(KanalTuru.Genel, "https://127.0.0.1/x", "", false)]
    public void Dogrulama(object t, string hedef, string ek, bool gecerli) => Assert.Equal(gecerli, WebhookYoneticisi.Gecerli((KanalTuru)t, new KanalAyari(true, hedef, ek)) == null);

    // ---- Saklama ----
    [Fact] public void Kaydet_Yukle_GizliDegerlerDuzYaziDegil()
    {
        var a = BildirimAyari.Bos().Ayarla(KanalTuru.Telegram, new(true, Token, "-1001234567890")).Ayarla(KanalTuru.Discord, new(true, Discord, "")) with { EnDusukSeviye = "Uyarı", Ayrintili = true };
        Assert.Null(WebhookYoneticisi.Kaydet(a)); string dosya = File.ReadAllText(WebhookYoneticisi.Yol);
        Assert.DoesNotContain("AAH-abcdef", dosya); Assert.DoesNotContain("AbCdEfGhIjKl", dosya); Assert.DoesNotContain("discord.com", dosya); Assert.Contains("dp:", dosya);   // DPAPI ile şifreli
        var y = WebhookYoneticisi.Yukle(); Assert.Equal(Token, y.Telegram.Hedef); Assert.Equal("-1001234567890", y.Telegram.Ek); Assert.Equal(Discord, y.Discord.Hedef); Assert.True(y.Telegram.Etkin); Assert.False(y.Teams.Etkin); Assert.Equal("Uyarı", y.EnDusukSeviye); Assert.True(y.Ayrintili);
        Assert.Equal(new[] { KanalTuru.Telegram, KanalTuru.Discord }, y.Etkinler);
    }
    [Fact] public void Kaydet_GecersizEtkinKanal_Reddedilir_DosyaDegismez()
    {
        Assert.Null(WebhookYoneticisi.Kaydet(BildirimAyari.Bos())); string once = File.ReadAllText(WebhookYoneticisi.Yol);
        Assert.Contains("Discord", WebhookYoneticisi.Kaydet(BildirimAyari.Bos().Ayarla(KanalTuru.Discord, new(true, "http://x", "")))!); Assert.Equal(once, File.ReadAllText(WebhookYoneticisi.Yol));
        Assert.Null(WebhookYoneticisi.Kaydet(BildirimAyari.Bos().Ayarla(KanalTuru.Discord, new(false, "gecersiz-ama-kapali", ""))));   // kapalı kanal doğrulanmaz
    }
    [Fact] public void BozukVeyaBaskaHesabinSifreliDegeri_BosGelir()
    {
        File.WriteAllText(WebhookYoneticisi.Yol, "{\"EnDusukSeviye\":\"Kritik\",\"Kanallar\":{\"Discord\":{\"Etkin\":true,\"Hedef\":\"dp:AAAA\",\"Ek\":\"\"}}}"); var y = WebhookYoneticisi.Yukle(); Assert.Equal("", y.Discord.Hedef); Assert.Empty(y.Etkinler);   // çözülemeyen kanal etkin sayılmaz
        File.WriteAllText(WebhookYoneticisi.Yol, "{bozuk"); Assert.Empty(WebhookYoneticisi.Yukle().Etkinler);
    }
    [Fact] public void EskiWebhookAyari_GenelKanalOlarakIceAlinir()
    {
        Webhook.Kaydet(new WebhookAyari(true, "https://hooks.slack.com/services/T/B/x")); var y = WebhookYoneticisi.Yukle(); Assert.True(y.Genel.Etkin); Assert.Equal("https://hooks.slack.com/services/T/B/x", y.Genel.Hedef);
        Webhook.Kaydet(new WebhookAyari(false, "")); Assert.Empty(WebhookYoneticisi.Yukle().Etkinler);
        Webhook.Kaydet(new WebhookAyari(true, "http://yerel")); Assert.Empty(WebhookYoneticisi.Yukle().Etkinler);   // geçersiz eski adres alınmaz
    }
    [Fact] public void Maske() { Assert.Equal("123456789:***", WebhookYoneticisi.Maske(KanalTuru.Telegram, new(true, Token, "1"))); Assert.DoesNotContain("AbCdEf", WebhookYoneticisi.Maske(KanalTuru.Discord, new(true, Discord, ""))); Assert.Equal("", WebhookYoneticisi.Maske(KanalTuru.Teams, new(true, "", ""))); }

    // ---- Yük biçimleri ----
    static BildirimOlayi Olay(string aciklama = "Şüpheli giriş: 192.168.1.20 adresinden 5 hatalı deneme", string seviye = "Kritik", string baslik = "Giriş saldırısı şüphesi") => new(baslik, seviye, aciklama, "Nöbetçi", T0);
    [Fact] public void Yuk_Telegram_Discord_Teams_Genel_GecerliJson()
    {
        var o = Olay(); var tg = JsonDocument.Parse(WebhookYoneticisi.Yuk(KanalTuru.Telegram, new(true, Token, "-1001"), o, false)).RootElement; Assert.Equal("-1001", tg.GetProperty("chat_id").GetString()); Assert.Contains("[KRİTİK] Giriş saldırısı şüphesi", tg.GetProperty("text").GetString()); Assert.True(tg.GetProperty("disable_web_page_preview").GetBoolean()); Assert.DoesNotContain("AAH-", tg.GetRawText());   // bot anahtarı gövdede YOK
        var dc = JsonDocument.Parse(WebhookYoneticisi.Yuk(KanalTuru.Discord, new(true, Discord, ""), o, false)).RootElement.GetProperty("embeds")[0]; Assert.Contains("KRİTİK", dc.GetProperty("title").GetString()); Assert.Equal(14423100, dc.GetProperty("color").GetInt32()); Assert.Equal("Risk", dc.GetProperty("fields")[0].GetProperty("name").GetString());
        var tm = JsonDocument.Parse(WebhookYoneticisi.Yuk(KanalTuru.Teams, new(true, Teams, ""), o, false)).RootElement; Assert.Equal("message", tm.GetProperty("type").GetString()); var ic = tm.GetProperty("attachments")[0].GetProperty("content"); Assert.Equal("AdaptiveCard", ic.GetProperty("type").GetString()); Assert.Equal("1.4", ic.GetProperty("version").GetString()); Assert.Equal("FactSet", ic.GetProperty("body")[2].GetProperty("type").GetString());
        var gn = JsonDocument.Parse(WebhookYoneticisi.Yuk(KanalTuru.Genel, new(true, "https://x.example/h", ""), o, false)).RootElement; Assert.Equal(gn.GetProperty("text").GetString(), gn.GetProperty("content").GetString());
        Assert.NotEqual(14423100, JsonDocument.Parse(WebhookYoneticisi.Yuk(KanalTuru.Discord, new(true, Discord, ""), Olay(seviye: "Bilgi"), false)).RootElement.GetProperty("embeds")[0].GetProperty("color").GetInt32());   // seviyeye göre renk
    }
    [Fact] public void KisaMod_YerelIpVeKullaniciYoluGizlenir_AyrintiliModdaKalir()
    {
        var o = Olay(@"192.168.1.20, 10.0.0.5, 172.16.4.1 ve 127.0.0.1 ile C:\Users\ibrahim\Belgeler\x.txt ve /home/ali/dosya; dış adres 203.0.113.9");
        string kisa = WebhookYoneticisi.Yuk(KanalTuru.Genel, new(true, "https://x.example/h", ""), o, false), uzun = WebhookYoneticisi.Yuk(KanalTuru.Genel, new(true, "https://x.example/h", ""), o, true);
        foreach (var gizli in new[] { "192.168.1.20", "10.0.0.5", "172.16.4.1", "127.0.0.1", "ibrahim", "/home/ali" }) Assert.DoesNotContain(gizli, kisa); Assert.Contains("[yerel IP]", kisa); Assert.Contains("203.0.113.9", kisa);   // dış adres kalır
        Assert.Contains("192.168.1.20", uzun); Assert.Contains("ibrahim", uzun);
    }
    [Fact] public void Yuk_UzunMetinKesilir_KontrolKarakteriVeKacisSaglam()
    {
        var j = WebhookYoneticisi.Yuk(KanalTuru.Telegram, new(true, Token, "-1001"), Olay(new string('a', 5000) + "\u0001\"tırnak\" <b>"), false); using var d = JsonDocument.Parse(j); string t = d.RootElement.GetProperty("text").GetString()!; Assert.True(t.Length < 700); Assert.DoesNotContain('\u0001', t);
    }
    [Fact] public void OlayUret_OnekleriTemizler() { var o = WebhookYoneticisi.OlayUret("Nöbetçi", "Kritik", "KRİTİK -> Yeni kapı açıldı: 3389", T0); Assert.Equal("Yeni kapı açıldı: 3389", o.Baslik); Assert.Equal("Nöbetçi", o.Tur); Assert.Equal("Olay", WebhookYoneticisi.OlayUret("x", "Kritik", "", T0).Baslik); }

    // ---- Gönderim: seviye, tekrar, saatlik sınır, yeniden deneme ----
    static void Kur(List<(string Url, string Json)> gonderilen, Func<string, int, (bool, int)>? sonuc = null, string seviye = "Kritik")
    {
        WebhookYoneticisi.Kaydet(BildirimAyari.Bos().Ayarla(KanalTuru.Telegram, new(true, Token, "-1001234567")).Ayarla(KanalTuru.Discord, new(true, Discord, "")) with { EnDusukSeviye = seviye }); int n = 0;
        WebhookYoneticisi.Gonderici = (url, json, ct) => { lock (gonderilen) { gonderilen.Add((url, json)); n++; } var (ok, kod) = sonuc?.Invoke(url, n) ?? (true, 200); return Task.FromResult((ok, kod)); };
    }
    [Fact] public async Task Olay_EtkinKanallaraGider_TelegramAdresiTokenIcerir_VeriGovdedeDegil()
    {
        var g = new List<(string, string)>(); Kur(g); int n = await WebhookYoneticisi.OlayAsync("Nöbetçi", "Kritik", "KRİTİK -> Şüpheli giriş", T0);
        Assert.Equal(2, n); Assert.Equal($"https://api.telegram.org/bot{Token}/sendMessage", g[0].Item1); Assert.Equal(Discord, g[1].Item1); Assert.DoesNotContain(Token, g[0].Item2);   // anahtar yalnızca adreste
        Assert.True(WebhookYoneticisi.SonDurum!.Value.Ok); Assert.Equal(("Bağlı (Telegram, Discord)", 0), WebhookYoneticisi.Gosterge());
    }
    [Fact] public async Task Olay_KapaliyaVeEsikAltinaGitmez_Bilgi_HicGitmez()
    {
        var g = new List<(string, string)>(); WebhookYoneticisi.Gonderici = (u, j, c) => { g.Add((u, j)); return Task.FromResult((true, 200)); };
        Assert.Equal(0, await WebhookYoneticisi.OlayAsync("x", "Kritik", "a", T0)); Assert.Empty(g);   // hiçbir kanal etkin değil
        Kur(g); Assert.Equal(0, await WebhookYoneticisi.OlayAsync("x", "Uyarı", "u", T0)); Assert.Equal(0, await WebhookYoneticisi.OlayAsync("x", "Bilgi", "b", T0)); Assert.Empty(g);
        Kur(g, null, "Uyarı"); Assert.Equal(2, await WebhookYoneticisi.OlayAsync("x", "Uyarı", "uyari olayi", T0)); Assert.Equal(0, await WebhookYoneticisi.OlayAsync("x", "Bilgi", "yine bilgi", T0.AddMinutes(9)));   // eşik Uyarı: Bilgi yine gitmez
    }
    [Fact] public async Task Olay_AyniOlay5DakikaIcindeTekrarGitmez()
    {
        var g = new List<(string, string)>(); Kur(g); Assert.Equal(2, await WebhookYoneticisi.OlayAsync("T", "Kritik", "KRİTİK -> aynı olay", T0)); Assert.Equal(0, await WebhookYoneticisi.OlayAsync("T", "Kritik", "KRİTİK -> aynı olay", T0.AddMinutes(4)));
        Assert.Equal(2, await WebhookYoneticisi.OlayAsync("T", "Kritik", "KRİTİK -> aynı olay", T0.AddMinutes(6))); Assert.Equal(2, await WebhookYoneticisi.OlayAsync("T", "Kritik", "KRİTİK -> başka olay", T0.AddMinutes(6)));
    }
    [Fact] public async Task Olay_SaatteEnCok20Bildirim()
    {
        var g = new List<(string, string)>(); Kur(g); int gonderilen = 0; for (int i = 0; i < 30; i++) gonderilen += await WebhookYoneticisi.OlayAsync("T", "Kritik", "olay " + i, T0.AddSeconds(i)) > 0 ? 1 : 0;
        Assert.Equal(WebhookYoneticisi.SaatlikUstSinir, gonderilen); Assert.Equal(1, await WebhookYoneticisi.OlayAsync("T", "Kritik", "yeni saat", T0.AddHours(2)) > 0 ? 1 : 0);   // bir saat sonra sınır sıfırlanır
    }
    [Fact] public async Task Gonderim_GeciciHatadaBirKezDener_YetkiHatasindaDenemez()
    {
        var g = new List<(string, string)>(); Kur(g, (u, n) => n == 1 ? (false, 500) : (true, 200)); Assert.Equal(2, await WebhookYoneticisi.OlayAsync("A", "Kritik", "gecici", T0)); Assert.Equal(3, g.Count);   // ilk kanal 1 kez yeniden denendi
        WebhookYoneticisi.Sifirla(); var h = new List<(string, string)>(); Kur(h, (u, n) => (false, 401)); Assert.Equal(0, await WebhookYoneticisi.OlayAsync("B", "Kritik", "yetkisiz", T0)); Assert.Equal(2, h.Count);   // 401: yeniden deneme yok
        Assert.False(WebhookYoneticisi.SonDurum!.Value.Ok); Assert.Contains("HTTP 401", WebhookYoneticisi.SonDurum.Value.Not); Assert.StartsWith("Bağlı ama son gönderim başarısız", WebhookYoneticisi.Gosterge().Yazi); Assert.Equal(1, WebhookYoneticisi.Gosterge().Renk);
    }
    [Fact] public async Task Gonderici_Istisna_ProgramiBozmaz()
    {
        var g = new List<(string, string)>(); Kur(g); WebhookYoneticisi.Gonderici = (u, j, c) => throw new InvalidOperationException("ağ patladı"); Assert.Equal(0, await WebhookYoneticisi.OlayAsync("C", "Kritik", "x", T0));
    }
    [Fact] public async Task OlayDeposu_KritikOlayi_BildirimeUlasir()
    {
        var g = new List<(string, string)>(); Kur(g); var bitti = new TaskCompletionSource(); var onceki = WebhookYoneticisi.Gonderici; WebhookYoneticisi.Gonderici = async (u, j, c) => { var r = await onceki!(u, j, c); bitti.TrySetResult(); return r; };
        OlayDeposu.Ekle("Bulgu", "Kritik", "KRİTİK -> deneme olayı"); for (int i = 0; i < 100 && !g.ToArray().Any(x => x.Item2.Contains("deneme olay")); i++) await Task.Delay(50);   // başka testlerin arka plan olaylarına takılmadan, bizim mesajımızı bekle
        Assert.Contains(g.ToArray(), x => x.Item2.Contains("deneme olay"));
    }

    // ---- Test mesajı ----
    [Fact] public async Task TestMesaji_YalnizSecilenKanala_GecersizseGonderilmez()
    {
        var g = new List<(string, string)>(); WebhookYoneticisi.Gonderici = (u, j, c) => { g.Add((u, j)); return Task.FromResult((true, 200)); };
        var r = await WebhookYoneticisi.TestGonderAsync(KanalTuru.Discord, new(true, "http://x", "")); Assert.False(r.Ok); Assert.Empty(g); Assert.Contains("Discord", r.Mesaj);
        var ok = await WebhookYoneticisi.TestGonderAsync(KanalTuru.Teams, new(true, Teams, ""), false); Assert.True(ok.Ok); Assert.Single(g); Assert.Equal(Teams, g[0].Item1); Assert.Contains("Test mesajı", g[0].Item2);
        WebhookYoneticisi.Gonderici = (u, j, c) => Task.FromResult((false, 0)); var kotu = await WebhookYoneticisi.TestGonderAsync(KanalTuru.Genel, new(true, "https://x.example/h", "")); Assert.False(kotu.Ok); Assert.Contains("gönderilemedi", kotu.Mesaj);
    }
    [Fact] public void Gosterge_Kapali_Gri() => Assert.Equal(("Kapalı (tıklayıp kurun)", 3), WebhookYoneticisi.Gosterge());
}
