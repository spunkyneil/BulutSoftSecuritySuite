// Bu dosya pasif HTTP/TLS/DNS/security.txt gözlemlerini YORUMLAYAN kural motorudur.
// Kurallar kodun içinde DEĞİL, genişletilebilir bir JSON dosyasındadır (pasif_kurallar.json: programa gömülü varsayılanlar + isteğe bağlı
// %AppData%\BulutSoft\pasif_kurallar.json ile ekleme/değiştirme/devre dışı bırakma). Kurallar yalnızca VERİDİR (kod çalıştırmaz): bir "olgu" (fact),
// bir "işlem" (op) ve bir beklenen değer. Bu, ileride eklenti (plugin) mimarisine geçişin hazırlığıdır.
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Koşul (ek şart).
internal sealed class PasifKosul
{
    [JsonPropertyName("fact")] public string Fact { get; set; } = ""; [JsonPropertyName("op")] public string Op { get; set; } = ""; [JsonPropertyName("value")] public string? Value { get; set; }
}

// Tek bir kural.
internal sealed class PasifKural
{
    [JsonPropertyName("id")] public string Id { get; set; } = ""; [JsonPropertyName("module")] public string Module { get; set; } = "HTTP"; [JsonPropertyName("applies_to")] public string AppliesTo { get; set; } = "all";
    [JsonPropertyName("fact")] public string Fact { get; set; } = ""; [JsonPropertyName("op")] public string Op { get; set; } = ""; [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("requires")] public PasifKosul? Requires { get; set; } [JsonPropertyName("per_item")] public bool PerItem { get; set; } [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("impact")] public double Impact { get; set; } [JsonPropertyName("title")] public string Title { get; set; } = ""; [JsonPropertyName("what")] public string What { get; set; } = ""; [JsonPropertyName("why")] public string Why { get; set; } = "";
    [JsonPropertyName("fix")] public string Fix { get; set; } = ""; [JsonPropertyName("fix_command")] public string? FixCommand { get; set; }
    [JsonPropertyName("kvkk")] public string[] Kvkk { get; set; } = Array.Empty<string>(); [JsonPropertyName("iso")] public string[] Iso { get; set; } = Array.Empty<string>(); [JsonPropertyName("mitre")] public string[] Mitre { get; set; } = Array.Empty<string>();
    [JsonPropertyName("eta_minutes")] public int EtaMinutes { get; set; } = 15;
}

// Kuraldan doğan bulgu.
internal sealed record PasifBulgu(string RuleId, string Modul, string Baslik, string Ne, string Neden, string Cozum, string? Komut, string[] Kvkk, string[] Iso, string[] Mitre, double Etki, string Kanit, int TahminiDk, Onem Onem)
{
    // Mevcut tablo ve raporlar için Bulgu.
    public Bulgu ToBulgu() => new(Modul, Baslik, Onem, Ne + (Kanit.Length > 0 ? "\nKanıt: " + Kanit : ""), Cozum + (string.IsNullOrWhiteSpace(Komut) ? "" : "\nÖrnek: " + Komut));
}

// Kural motoru.
internal static class PasifKurallar
{
    // Kullanıcı kural dosyası.
    public static string KullaniciDosyasi => BulutKlasor.Dosya("pasif_kurallar.json");
    // JSON ayarı.
    private static readonly JsonSerializerOptions Js = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    // Etki puanından önem derecesi.
    public static Onem OnemOlarak(double etki) => etki >= 8 ? Onem.Yuksek : etki >= 4.5 ? Onem.Orta : etki >= 2 ? Onem.Dusuk : Onem.Bilgi;

    // Kuralı doğrular (geçersizse nedenini verir).
    public static string? Dogrula(PasifKural k)
    {
        // Zorunlu alanlar.
        if (!Regex.IsMatch(k.Id ?? "", @"^[A-Za-z0-9_\-]{3,60}$")) return "id geçersiz";
        if (string.IsNullOrWhiteSpace(k.Fact) || string.IsNullOrWhiteSpace(k.Title) || string.IsNullOrWhiteSpace(k.What)) return "fact/title/what zorunlu";
        if (k.Op is not ("missing" or "present" or "equals" or "not_equals" or "contains" or "not_contains" or "regex" or "lt" or "gt" or "lacks_flag")) return "op desteklenmiyor: " + k.Op;
        if (k.Impact is < 0 or > 10) return "impact 0-10 olmalı";
        if (k.AppliesTo is not ("all" or "http" or "https")) return "applies_to: all|http|https";
        if ((k.FixCommand?.Length ?? 0) > 1000 || k.Title.Length > 200 || k.What.Length > 600 || k.Why.Length > 800 || k.Fix.Length > 600) return "metin uzunluk sınırı aşıldı";
        if (k.Op == "regex") { try { _ = new Regex(k.Value ?? "", RegexOptions.None, TimeSpan.FromMilliseconds(100)); } catch { return "regex geçersiz"; } }
        // Sorun yok.
        return null;
    }

    // Varsayılan (gömülü) ve kullanıcı kurallarını yükler; kullanıcı kuralı aynı id'yi geçersiz kılar, "enabled": false devre dışı bırakır.
    // kaynakAdi: gömülü kural dosyası (varsayılan pasif; aktif denetim "aktif_kurallar.json" verir); kullaniciYolu: kullanıcı geçersiz kılma dosyası.
    public static List<PasifKural> Yukle(out List<string> uyarilar, string? kullaniciMetni = null, string kaynakAdi = "pasif_kurallar.json", string? kullaniciYolu = null)
    {
        string kYol = kullaniciYolu ?? KullaniciDosyasi;
        // Uyarılar.
        uyarilar = new List<string>(); var sozluk = new Dictionary<string, PasifKural>(StringComparer.OrdinalIgnoreCase);
        // Gömülü varsayılanlar.
        try
        {
            using var s = typeof(PasifKurallar).Assembly.GetManifestResourceStream(kaynakAdi); if (s == null) uyarilar.Add("Gömülü kural dosyası bulunamadı."); else foreach (var k in JsonSerializer.Deserialize<List<PasifKural>>(s, Js) ?? new()) { var h = Dogrula(k); if (h != null) uyarilar.Add($"Gömülü kural {k.Id}: {h}"); else sozluk[k.Id] = k; }
        }
        catch (Exception ex) { uyarilar.Add("Gömülü kurallar okunamadı: " + ex.Message); }
        // Kullanıcı kuralları.
        try
        {
            string? m = kullaniciMetni ?? (File.Exists(kYol) && new FileInfo(kYol).Length < 500_000 ? File.ReadAllText(kYol) : null);
            if (m != null) foreach (var oge in JsonSerializer.Deserialize<List<JsonElement>>(m, Js) ?? new())
            {
                // Kısa biçim: {"id": "...", "enabled": false} (başka alan yok) → o kuralı devre dışı bırakır.
                if (oge.ValueKind == JsonValueKind.Object && !oge.TryGetProperty("fact", out _) && oge.TryGetProperty("id", out var idv) && idv.ValueKind == JsonValueKind.String && oge.TryGetProperty("enabled", out var ev) && ev.ValueKind == JsonValueKind.False) { sozluk.Remove(idv.GetString() ?? ""); continue; }
                // Tam kural.
                PasifKural k; try { k = oge.Deserialize<PasifKural>(Js) ?? new(); } catch (Exception ex) { uyarilar.Add("Kullanıcı kuralı okunamadı: " + ex.Message + " (atlandı)"); continue; }
                var h = Dogrula(k); if (h != null) uyarilar.Add($"Kullanıcı kuralı {k.Id}: {h} (atlandı)"); else sozluk[k.Id] = k;
            }
        }
        catch (Exception ex) { uyarilar.Add("Kullanıcı kuralları okunamadı (yok sayıldı): " + ex.Message); }
        // Etkin kurallar.
        return sozluk.Values.Where(k => k.Enabled).ToList();
    }

    // Gözlemden "olgular" (düz anahtar-değer) çıkarır.
    public static Dictionary<string, string?> Olgular(PasifGozlem g, DateTime simdiUtc)
    {
        // Sözlük.
        var o = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        // HTTP.
        bool https = g.Http != null && g.Http.Url.StartsWith("https", StringComparison.OrdinalIgnoreCase); o["https"] = https ? "true" : "false";
        if (g.Http != null)
        {
            // Başlıklar.
            foreach (var (k, v) in g.Http.Basliklar) o["header:" + k.ToLowerInvariant()] = v; o["http_redirect"] = g.Http.HttpYonlendirme;
            // HSTS süresi.
            if (g.Http.Basliklar.TryGetValue("strict-transport-security", out var hs) && Regex.Match(hs, @"max-age\s*=\s*(\d+)", RegexOptions.IgnoreCase) is { Success: true } mm) o["hsts_max_age"] = mm.Groups[1].Value;
            // Çerçeve koruması.
            g.Http.Basliklar.TryGetValue("content-security-policy", out var csp); o["frame_protection"] = g.Http.Basliklar.ContainsKey("x-frame-options") || (csp?.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase) ?? false) ? "var" : "yok";
            // CORS.
            g.Http.Basliklar.TryGetValue("access-control-allow-origin", out var acao); g.Http.Basliklar.TryGetValue("access-control-allow-credentials", out var acac); o["cors_wildcard"] = acao?.Trim() == "*" ? "true" : "false"; o["cors_wildcard_credentials"] = acao?.Trim() == "*" && string.Equals(acac?.Trim(), "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
        }
        // TLS.
        if (g.Tls is { } t)
        {
            // Kalan gün.
            if (t.Bitis is { } b) o["tls:days_left"] = Math.Floor((b - simdiUtc).TotalDays).ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Zincir hataları (isim uyuşmazlığı ayrı bir olgu).
            var zh = t.ZincirHatalari.Where(x => x is not ("NoError" or "RemoteCertificateNameMismatch")).Distinct().ToList(); o["tls:chain_errors"] = zh.Count > 0 ? string.Join(", ", zh) : null; o["tls:name_mismatch"] = !t.IsimUyumu ? "true" : "false";
            // Kendi imzalı, anahtar, imza, protokol.
            o["tls:self_signed"] = t.Subject.Length > 0 && t.Subject == t.Issuer ? "true" : "false"; if (t.AnahtarTur.Contains("rsa", StringComparison.OrdinalIgnoreCase) && t.AnahtarBit > 0) o["tls:rsa_bits"] = t.AnahtarBit.ToString(CultureInfo.InvariantCulture); o["tls:sig_alg"] = t.ImzaAlg; o["tls:protocol"] = t.Surum;
        }
        // DNS.
        if (g.Dns is { OrgAlan.Length: > 0 } d)
        {
            // SPF, DMARC, sayılar.
            o["dns:spf"] = d.Spf; if (d.Spf != null && Regex.Match(d.Spf, @"([+\-~?])all\b") is { Success: true } sm) o["dns:spf_all"] = sm.Groups[1].Value + "all"; o["dns:dmarc"] = d.Dmarc; if (d.Dmarc != null && Regex.Match(d.Dmarc, @"\bp\s*=\s*(\w+)", RegexOptions.IgnoreCase) is { Success: true } dm) o["dns:dmarc_policy"] = dm.Groups[1].Value.ToLowerInvariant();
            o["dns:caa_count"] = d.Caa.Count.ToString(CultureInfo.InvariantCulture); o["dns:mx_count"] = d.Mx.Count.ToString(CultureInfo.InvariantCulture);
        }
        // security.txt.
        o["sectxt:found"] = g.SecTxt.Bulundu ? "true" : "false"; o["sectxt:problems"] = g.SecTxt.Bulundu && g.SecTxt.Sorunlar.Count > 0 ? string.Join("; ", g.SecTxt.Sorunlar) : null;
        // Sonuç.
        return o;
    }

    // Tek koşulu değerlendirir.
    public static bool Kosul(string? deger, string op, string? beklenen)
    {
        // Sayısal karşılaştırma yardımcısı.
        bool Say(Func<double, double, bool> f) => double.TryParse(deger, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a) && double.TryParse(beklenen, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b) && f(a, b);
        // İşleme göre.
        return op switch
        {
            "missing" => string.IsNullOrWhiteSpace(deger),
            "present" => !string.IsNullOrWhiteSpace(deger),
            "equals" => string.Equals(deger ?? "", beklenen ?? "", StringComparison.OrdinalIgnoreCase),
            "not_equals" => !string.Equals(deger ?? "", beklenen ?? "", StringComparison.OrdinalIgnoreCase),
            "contains" => deger != null && deger.Contains(beklenen ?? "", StringComparison.OrdinalIgnoreCase),
            "not_contains" => deger == null || !deger.Contains(beklenen ?? "", StringComparison.OrdinalIgnoreCase),
            "regex" => deger != null && Regex.IsMatch(deger, beklenen ?? "", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)),
            "lt" => Say((a, b) => a < b),
            "gt" => Say((a, b) => a > b),
            _ => false
        };
    }

    // Çerezden değeri çıkarıp yalnızca "ad + öznitelikler" gösterir (oturum değeri raporlara/günlüğe SIZMASIN).
    public static string CerezMaskele(string c) { var p = c.Split(';'); var ad = p[0].Split('=')[0].Trim(); return ad + "=<gizlendi>" + (p.Length > 1 ? ";" + string.Join(";", p.Skip(1).Select(x => x.Trim())) : ""); }

    // Tüm kuralları gözleme uygular.
    public static List<PasifBulgu> Degerlendir(PasifGozlem g, List<PasifKural>? kurallar = null, DateTime? simdiUtc = null)
    {
        // Kurallar ve olgular.
        kurallar ??= Yukle(out _); var simdi = simdiUtc ?? DateTime.UtcNow; var o = Olgular(g, simdi); bool https = o.GetValueOrDefault("https") == "true"; var sonuc = new List<PasifBulgu>();
        // Her kural.
        foreach (var k in kurallar)
        {
            // Uygulanabilirlik.
            if (k.AppliesTo == "https" && !https) continue; if (k.AppliesTo == "http" && https) continue;
            // Ek koşul.
            if (k.Requires != null && !Kosul(o.GetValueOrDefault(k.Requires.Fact), k.Requires.Op, k.Requires.Value)) continue;
            // Çerez kuralları (her çerez için).
            if (k.Fact == "cookie" && k.Op == "lacks_flag")
            {
                // Çerezler.
                foreach (var c in g.Http?.Cerezler ?? new())
                {
                    // Öznitelikler (ad=değer'den sonrası).
                    var oz = string.Join(";", c.Split(';').Skip(1)); if (oz.Contains(k.Value ?? "", StringComparison.OrdinalIgnoreCase)) continue;
                    // Bulgu.
                    string ad = c.Split(';')[0].Split('=')[0].Trim(); sonuc.Add(Uret(k, ad, null, CerezMaskele(c)));
                }
                continue;
            }
            // Normal kural.
            var deger = o.GetValueOrDefault(k.Fact);
            if (Kosul(deger, k.Op, k.Value)) sonuc.Add(Uret(k, null, deger, deger == null ? "(yok)" : Kes(deger)));
        }
        // En önemliden aza.
        return sonuc.OrderByDescending(x => x.Etki).ThenBy(x => x.RuleId).ToList();
    }

    // Hazır olgu sözlüğüne kuralları uygular (aktif denetim kendi olgularını üretir: "probe:*"). Çerez kuralları burada yoktur.
    public static List<PasifBulgu> DegerlendirOlgular(Dictionary<string, string?> o, List<PasifKural> kurallar)
    {
        var sonuc = new List<PasifBulgu>();
        foreach (var k in kurallar)
        {
            // Ek koşul.
            if (k.Requires != null && !Kosul(o.GetValueOrDefault(k.Requires.Fact), k.Requires.Op, k.Requires.Value)) continue;
            // Normal kural.
            var deger = o.GetValueOrDefault(k.Fact); if (Kosul(deger, k.Op, k.Value)) sonuc.Add(Uret(k, null, deger, deger == null ? "(yok)" : Kes(deger)));
        }
        return sonuc.OrderByDescending(x => x.Etki).ThenBy(x => x.RuleId).ToList();
    }

    // Uzun kanıtları keser.
    private static string Kes(string s) => s.Length > 200 ? s[..200] + "…" : s;

    // Kuraldan bulgu üretir (yer tutucuları doldurur).
    private static PasifBulgu Uret(PasifKural k, string? oge, string? deger, string kanit)
    {
        // Yer tutucu doldurucu.
        string D(string s) => s.Replace("{item}", oge ?? "").Replace("{value}", deger != null ? Kes(deger) : "");
        // Bulgu.
        return new PasifBulgu(k.Id, k.Module, D(k.Title), D(k.What), D(k.Why), D(k.Fix), k.FixCommand, k.Kvkk, k.Iso, k.Mitre, k.Impact, kanit, k.EtaMinutes, OnemOlarak(k.Impact));
    }
}
