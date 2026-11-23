// Bu dosya ANLIK BİLDİRİM MERKEZİ'dir: önemli bir olay olunca (Nöbetçi, USB kalkanı, Kali sensörünün kopması, kritik açık, IP sızıntısı...) kullanıcının KENDİ verdiği kanala
// (Telegram botu, Discord webhook'u, Microsoft Teams iş akışı webhook'u veya genel bir https webhook) kısa, biçimli bir mesaj gönderir.
// İLKELER: varsayılan KAPALI; kanal bilgisi (bot anahtarı / webhook adresi) yalnızca bu Windows hesabıyla çözülebilen DPAPI ile saklanır (düz yazı değil);
// yalnızca https ve izinli adres kalıpları; gönderilen metin KISA modda yerel IP adreslerini ve kullanıcı adı yollarını gizler; tekrar eden olaylar ve saatlik üst sınırla sel engellenir;
// gönderim hataları programı bozmaz. Mesajlar yalnızca KULLANICININ yapılandırdığı kanala gider.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Kanal türleri.
internal enum KanalTuru { Telegram, Discord, Teams, Genel }

// Tek kanalın ayarı. Hedef = Telegram'da bot anahtarı, diğerlerinde webhook adresi (gizli); Ek = Telegram'da sohbet kimliği.
internal sealed record KanalAyari(bool Etkin, string Hedef, string Ek);

// Tüm bildirim ayarları. EnDusukSeviye: "Kritik" veya "Uyarı". Ayrintili: false = KISA mod (yerel IP/yol gizlenir).
internal sealed record BildirimAyari(KanalAyari Telegram, KanalAyari Discord, KanalAyari Teams, KanalAyari Genel, string EnDusukSeviye, bool Ayrintili)
{
    public static BildirimAyari Bos() => new(new(false, "", ""), new(false, "", ""), new(false, "", ""), new(false, "", ""), "Kritik", false);
    public KanalAyari Al(KanalTuru t) => t switch { KanalTuru.Telegram => Telegram, KanalTuru.Discord => Discord, KanalTuru.Teams => Teams, _ => Genel };
    public BildirimAyari Ayarla(KanalTuru t, KanalAyari k) => t switch { KanalTuru.Telegram => this with { Telegram = k }, KanalTuru.Discord => this with { Discord = k }, KanalTuru.Teams => this with { Teams = k }, _ => this with { Genel = k } };
    public IEnumerable<KanalTuru> Etkinler => Enum.GetValues<KanalTuru>().Where(t => Al(t).Etkin && WebhookYoneticisi.Gecerli(t, Al(t)) == null);
}

// Bildirime konu olay.
internal sealed record BildirimOlayi(string Baslik, string Seviye, string Aciklama, string Tur, DateTime Zaman);

// Yönetici.
internal static class WebhookYoneticisi
{
    public static string Yol => BulutKlasor.Dosya("bildirim_kanallari.json");
    // Test kancası: gerçek ağ yerine bunu çağırır (url, json) → (başarılı, durum kodu).
    public static Func<string, string, CancellationToken, Task<(bool Ok, int Kod)>>? Gonderici { get; set; }
    public const int SaatlikUstSinir = 20; public static readonly TimeSpan TekrarAraligi = TimeSpan.FromMinutes(5);
    private static readonly object Kilit = new(); private static readonly Dictionary<string, DateTime> SonGonderilen = new(); private static readonly Queue<DateTime> Saatlik = new();
    // Son gönderim durumu (gösterge için).
    public static (DateTime Zaman, bool Ok, string Not)? SonDurum { get; private set; }
    public static event Action? Degisti;
    [ThreadStatic] private static bool _icerde;

    // ---------------- Doğrulama ----------------
    private static readonly string[] DiscordSunucular = { "discord.com", "discordapp.com", "ptb.discord.com", "canary.discord.com" };
    private static readonly string[] TeamsSonekleri = { ".webhook.office.com", ".logic.azure.com", ".powerplatform.com", ".azure.com" };

    // Kanal ayarını doğrular; hata varsa sade nedenini, yoksa null verir.
    public static string? Gecerli(KanalTuru t, KanalAyari k)
    {
        string h = (k.Hedef ?? "").Trim();
        switch (t)
        {
            case KanalTuru.Telegram:
                if (!Regex.IsMatch(h, @"^\d{6,12}:[A-Za-z0-9_\-]{30,50}$")) return "Bot anahtarı 123456789:ABC... biçiminde olmalı (BotFather'dan alınır).";
                if (!Regex.IsMatch((k.Ek ?? "").Trim(), @"^(-?\d{5,20}|@[A-Za-z0-9_]{5,32})$")) return "Sohbet kimliği (Chat ID) sayı veya @kanaladı olmalı.";
                return null;
            case KanalTuru.Discord:
                if (!Uri.TryCreate(h, UriKind.Absolute, out var d) || d.Scheme != Uri.UriSchemeHttps || d.UserInfo.Length > 0 || !DiscordSunucular.Contains(d.Host, StringComparer.OrdinalIgnoreCase) || !d.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal)) return "Discord webhook adresi https://discord.com/api/webhooks/... biçiminde olmalı.";
                return null;
            case KanalTuru.Teams:
                if (!Uri.TryCreate(h, UriKind.Absolute, out var tm) || tm.Scheme != Uri.UriSchemeHttps || tm.UserInfo.Length > 0 || !TeamsSonekleri.Any(s => tm.Host.EndsWith(s, StringComparison.OrdinalIgnoreCase)) || tm.AbsolutePath.Length < 2) return "Teams adresi https ile başlamalı ve webhook.office.com, logic.azure.com veya powerplatform.com sunucusunda olmalı (Teams'te \"Workflows\" > \"Webhook isteği alındığında\").";
                return null;
            default:
                if (!Webhook.AdresGecerli(h)) return "Adres yalnızca https:// ile başlamalı, kullanıcı adı/parola içermemeli ve yerel olmamalı.";
                return null;
        }
    }

    // Gizli değeri günlüklerde/önizlemede maskeler.
    public static string Maske(KanalTuru t, KanalAyari k)
    {
        string h = k.Hedef ?? ""; if (h.Length == 0) return "";
        if (t == KanalTuru.Telegram) { int i = h.IndexOf(':'); return i > 0 ? h[..i] + ":***" : "***"; }
        return Uri.TryCreate(h, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Host}/…/***" : "***";
    }

    // ---------------- Saklama (DPAPI) ----------------
    private static readonly byte[] Entropi = Encoding.UTF8.GetBytes("BulutSoft-Bildirim-1");
    private static string Sifrele(string s) => s.Length == 0 ? "" : "dp:" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), Entropi, DataProtectionScope.CurrentUser));
    private static string Coz(string s) { if (!s.StartsWith("dp:", StringComparison.Ordinal)) return ""; try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s[3..]), Entropi, DataProtectionScope.CurrentUser)); } catch { return ""; } }
    private sealed class Dosya { public string EnDusukSeviye { get; set; } = "Kritik"; public bool Ayrintili { get; set; } public Dictionary<string, KanalAyari> Kanallar { get; set; } = new(); }
    private static readonly JsonSerializerOptions Sec = new() { WriteIndented = true };

    // Yükler. Yeni dosya yoksa ve eski tek adresli webhook (Araç Kutusu) açıksa "Genel" kanal olarak içe alınır.
    public static BildirimAyari Yukle()
    {
        var a = BildirimAyari.Bos();
        try
        {
            if (File.Exists(Yol))
            {
                var d = JsonSerializer.Deserialize<Dosya>(File.ReadAllText(Yol), Sec) ?? new();
                a = a with { EnDusukSeviye = d.EnDusukSeviye == "Uyarı" ? "Uyarı" : "Kritik", Ayrintili = d.Ayrintili };
                foreach (var t in Enum.GetValues<KanalTuru>()) if (d.Kanallar.TryGetValue(t.ToString(), out var k)) a = a.Ayarla(t, new KanalAyari(k.Etkin, Coz(k.Hedef ?? ""), k.Ek ?? ""));
            }
            else { var eski = Webhook.Yukle(); if (eski.Aktif && Webhook.AdresGecerli(eski.Adres)) a = a.Ayarla(KanalTuru.Genel, new KanalAyari(true, eski.Adres, "")); }
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "WebhookYoneticisi.Yukle"); }
        return a;
    }

    // Kaydeder (gizli değerler DPAPI ile şifrelenir). Geçersiz etkin kanal varsa kaydetmez ve nedenini söyler.
    public static string? Kaydet(BildirimAyari a)
    {
        foreach (var t in Enum.GetValues<KanalTuru>()) { var k = a.Al(t); if (k.Etkin) { var h = Gecerli(t, k); if (h != null) return $"{t}: {h}"; } }
        var d = new Dosya { EnDusukSeviye = a.EnDusukSeviye, Ayrintili = a.Ayrintili };
        foreach (var t in Enum.GetValues<KanalTuru>()) { var k = a.Al(t); d.Kanallar[t.ToString()] = new KanalAyari(k.Etkin, Sifrele((k.Hedef ?? "").Trim()), (k.Ek ?? "").Trim()); }
        Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, JsonSerializer.Serialize(d, Sec));
        // Ayar kaydedildi: dinleyenler (Kolay Görünümdeki "Bildirim Kanalı" göstergesi) anında yenilenir.
        try { Degisti?.Invoke(); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "WebhookYoneticisi.Kaydet"); }
        return null;
    }

    // ---------------- Mesaj biçimleri ----------------
    // Yerel IP adreslerini ve kullanıcı yollarını gizler (KISA mod).
    public static string Gizle(string s)
    {
        s = Regex.Replace(s ?? "", @"\b(10(\.\d{1,3}){3}|192\.168(\.\d{1,3}){2}|172\.(1[6-9]|2\d|3[01])(\.\d{1,3}){2}|127(\.\d{1,3}){3})\b", "[yerel IP]");
        s = Regex.Replace(s, @"(?i)([A-Z]:\\Users\\)[^\\\s]+", "$1[kullanıcı]"); return Regex.Replace(s, @"(?i)/home/[^/\s]+", "/home/[kullanıcı]");
    }
    private static string Kes(string s, int en) { s = new string(s.Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim(); return s.Length > en ? s[..en] + "…" : s; }
    private static string SeviyeYazi(string s) => s switch { "Kritik" => "KRİTİK", "Uyarı" => "UYARI", _ => "BİLGİ" };

    // Olaydan başlık/açıklama üretir ("KRİTİK -> metin" → başlık = metin).
    public static BildirimOlayi OlayUret(string tur, string seviye, string mesaj, DateTime zaman)
    {
        string m = Regex.Replace(mesaj ?? "", @"^\s*(KRİTİK|UYARI|BİLGİ)\s*:?\s*->?\s*", "", RegexOptions.IgnoreCase).Trim(); string baslik = m.Length > 90 ? m[..90] + "…" : m; if (baslik.Length == 0) baslik = "Olay";
        return new BildirimOlayi(baslik, seviye, m, tur, zaman);
    }

    // Kanala göre JSON yükü. Test ve önizleme için herkese açık (gizli değer içermez; Telegram'da adres ayrı verilir).
    public static string Yuk(KanalTuru t, KanalAyari k, BildirimOlayi o, bool ayrintili)
    {
        string baslik = Kes(ayrintili ? o.Baslik : Gizle(o.Baslik), 150), acik = Kes(ayrintili ? o.Aciklama : Gizle(o.Aciklama), ayrintili ? 800 : 300), zaman = o.Zaman.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture), risk = SeviyeYazi(o.Seviye), metin = $"[{risk}] {baslik}\n{acik}\n{o.Tur} · {zaman} · BulutSoft Security Suite";
        object yuk = t switch
        {
            KanalTuru.Telegram => new { chat_id = k.Ek, text = metin, disable_web_page_preview = true },
            KanalTuru.Discord => new { username = "BulutSoft", embeds = new[] { new { title = $"[{risk}] {baslik}", description = acik, color = o.Seviye == "Kritik" ? 14423100 : o.Seviye == "Uyarı" ? 16096779 : 953086, timestamp = o.Zaman.ToUniversalTime().ToString("o"), footer = new { text = "BulutSoft Security Suite" }, fields = new[] { new { name = "Risk", value = risk, inline = true }, new { name = "Kaynak", value = o.Tur, inline = true } } } } },
            KanalTuru.Teams => new { type = "message", attachments = new[] { new { contentType = "application/vnd.microsoft.card.adaptive", contentUrl = (string?)null, content = new Dictionary<string, object> { ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json", ["type"] = "AdaptiveCard", ["version"] = "1.4", ["body"] = new object[] { new { type = "TextBlock", size = "Medium", weight = "Bolder", text = $"[{risk}] {baslik}", wrap = true }, new { type = "TextBlock", text = acik, wrap = true }, new { type = "FactSet", facts = new[] { new { title = "Risk", value = risk }, new { title = "Kaynak", value = o.Tur }, new { title = "Zaman", value = zaman } } } } } } } },
            _ => new { text = metin, content = metin }
        };
        return JsonSerializer.Serialize(yuk, JsonAyarlari.GevsekKacis);
    }

    // Gönderilecek adres.
    private static string Adres(KanalTuru t, KanalAyari k) => t == KanalTuru.Telegram ? $"https://api.telegram.org/bot{k.Hedef.Trim()}/sendMessage" : k.Hedef.Trim();

    // ---------------- Gönderim ----------------
    private static async Task<(bool Ok, int Kod)> Yolla(string url, string json, CancellationToken ct)
    {
        if (Gonderici != null) return await Gonderici(url, json, ct);
        try { using var r = await AgIstemciFabrikasi.YollaAsync(AgProfili.Webhook, url, new StringContent(json, Encoding.UTF8, "application/json"), ct); return (r.IsSuccessStatusCode, (int)r.StatusCode); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; } catch (Exception) { return (false, 0); }   // ağ hatası: durum "gönderilemedi" olarak saklanır (hata günlüğe yazılmaz: günlük webhook'u tetikleyip döngü olmasın)
    }

    // Tek kanala gönderir (1 yeniden deneme). Sonuç durumu saklanır.
    private static async Task<bool> KanalaGonder(KanalTuru t, KanalAyari k, BildirimOlayi o, bool ayrintili, CancellationToken ct)
    {
        string url = Adres(t, k), json = Yuk(t, k, o, ayrintili); var (ok, kod) = await Yolla(url, json, ct);
        if (!ok && kod != 400 && kod != 401 && kod != 403 && kod != 404) { await Task.Delay(Gonderici != null ? 1 : 2500, ct); (ok, kod) = await Yolla(url, json, ct); }   // geçici hata: bir kez daha dene (yetki/biçim hatasında deneme)
        SonDurum = (DateTime.Now, ok, ok ? $"{t} kanalına gönderildi." : $"{t} kanalına gönderilemedi (HTTP {kod})."); try { Degisti?.Invoke(); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "WebhookYoneticisi.KanalaGonder"); } return ok;
    }

    // Test mesajı: yalnızca seçilen kanala (ayar henüz kaydedilmemiş olsa bile, verilen ayarla). Sonuç: (başarılı, açıklama).
    public static async Task<(bool Ok, string Mesaj)> TestGonderAsync(KanalTuru t, KanalAyari k, bool ayrintili = false, CancellationToken ct = default)
    {
        var h = Gecerli(t, k); if (h != null) return (false, h);
        var o = new BildirimOlayi("Test mesajı: bildirim kanalı çalışıyor", "Bilgi", "Bu bir deneme bildirimidir. Gerçek bir olay değildir.", "Bildirim Merkezi", DateTime.Now);
        bool ok = await KanalaGonder(t, k, o, ayrintili, ct); return (ok, ok ? "Test mesajı gönderildi. Kanalınızı kontrol edin." : "Test mesajı gönderilemedi. Adresi/anahtarı ve internet bağlantısını kontrol edin.");
    }

    // Sıralı seviye değeri.
    private static int Sira(string s) => s == "Kritik" ? 2 : s == "Uyarı" ? 1 : 0;

    // Olay bildirimi (OlayDeposu'ndan çağrılır): ayarlara, seviyeye, tekrar ve saatlik sınıra göre arka planda gönderir. Asla istisna fırlatmaz, beklemez.
    public static void Olay(string tur, string seviye, string mesaj)
    {
        if (_icerde) return; try { _ = Task.Run(() => OlayAsync(tur, seviye, mesaj, DateTime.Now)); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "WebhookYoneticisi.Olay"); }
    }

    // Çekirdek (test edilebilir): gönderilen kanal sayısını döndürür.
    public static async Task<int> OlayAsync(string tur, string seviye, string mesaj, DateTime simdi, CancellationToken ct = default)
    {
        try
        {
            _icerde = true; var a = Yukle(); var kanallar = a.Etkinler.ToList(); if (kanallar.Count == 0 || Sira(seviye) < Sira(a.EnDusukSeviye) || Sira(seviye) < 1) return 0;
            var o = OlayUret(tur, seviye, mesaj, simdi); string anahtar = tur + "|" + o.Baslik;
            lock (Kilit)
            {
                if (SonGonderilen.TryGetValue(anahtar, out var son) && simdi - son < TekrarAraligi) return 0;           // aynı olay 5 dk içinde tekrar gitmez
                while (Saatlik.Count > 0 && simdi - Saatlik.Peek() > TimeSpan.FromHours(1)) Saatlik.Dequeue();
                if (Saatlik.Count >= SaatlikUstSinir) return 0;                                                           // saatte en çok 20 bildirim
                SonGonderilen[anahtar] = simdi; Saatlik.Enqueue(simdi); if (SonGonderilen.Count > 500) foreach (var k in SonGonderilen.Where(x => simdi - x.Value > TekrarAraligi).Select(x => x.Key).ToList()) SonGonderilen.Remove(k);
            }
            int ok = 0; foreach (var t in kanallar) { try { if (await KanalaGonder(t, a.Al(t), o, a.Ayrintili, ct)) ok++; } catch (OperationCanceledException) { throw; } catch (Exception hk4) { HataKayit.Sessiz(hk4, "WebhookYoneticisi.OlayAsync"); } }
            return ok;
        }
        finally { _icerde = false; }
    }

    // Göstergede görünecek (yazı, renk 0=yeşil 1=sarı 2=kırmızı 3=gri).
    public static (string Yazi, int Renk) Gosterge()
    {
        var a = Yukle(); var e = a.Etkinler.ToList(); if (e.Count == 0) return ("Kapalı (tıklayıp kurun)", 3);
        string adlar = string.Join(", ", e.Select(x => x.ToString())); if (SonDurum is { Ok: false }) return ($"Bağlı ama son gönderim başarısız ({adlar})", 1);
        return ($"Bağlı ({adlar})", 0);
    }

    internal static void Sifirla() { lock (Kilit) { SonGonderilen.Clear(); Saatlik.Clear(); } SonDurum = null; Gonderici = null; }
}
