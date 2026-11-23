// Bu dosya Hafta 4'ün yama tarafıdır: ÇEVRİMDIŞI KB kataloğu (elle yüklenir, bozuksa atomik reddedilir) ve yerel yama denetçisi.
// Katalog, işletim sistemi yapı numarası başına "olması gereken kümülatif güncellemeleri" listeler. Katalogda olmayan KB eksik sayılmaz.
// Kümülatif güncelleme kuralı: aynı yapıda kurulu, daha yeni (veya aynı tarihli) bir katalog güncellemesi eskisini karşılar (eski sayılmaz).
// Dürüstlük: katalog sizin yüklediğiniz veridir; doğruluğundan yükleyen sorumludur. Katalog yoksa program "değerlendirilemedi" der, "güvenli" demez.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// V2 ad alanı.
namespace LabDenetim.V2;

// Katalogdaki tek güncelleme.
internal sealed record KbOgesi(string Kb, DateTime Yayin, string Onem, string Baslik);

// Katalog dosyası bozuksa fırlatılır (eski katalog korunur).
internal sealed class KatalogHatasi : Exception { public KatalogHatasi(string m) : base(m) { } }

// Çevrimdışı KB kataloğu.
internal sealed class KbCatalog
{
    // Katalog sürümü (raporda görünür) ve içeriğin SHA-256 özeti.
    public string Surum { get; }
    public string Ozet { get; }
    // Yapı numarası → güncellemeler.
    public IReadOnlyDictionary<string, List<KbOgesi>> Yapilar { get; }

    private KbCatalog(string surum, string ozet, Dictionary<string, List<KbOgesi>> yapilar) { Surum = surum; Ozet = ozet; Yapilar = yapilar; }

    // Sınırlar (kötü niyetli/bozuk dosyaya karşı).
    public const int EnCokYapi = 50, EnCokOge = 5000, EnCokDosyaBayt = 5_000_000;
    private static readonly string[] Onemler = { "Critical", "Important", "Moderate", "Low" };

    // Dosya yolu (testte BulutKlasor.Kok değiştirilir).
    public static string Yol => BulutKlasor.Dosya("kb_katalog.json");

    // JSON'u TAMAMEN doğrulayarak çözer; herhangi bir hata varsa KatalogHatasi fırlatır (kısmi veri kabul edilmez).
    public static KbCatalog Coz(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > EnCokDosyaBayt) throw new KatalogHatasi("Katalog boş veya çok büyük.");
        JsonDocument d; try { d = JsonDocument.Parse(json); } catch (JsonException ex) { throw new KatalogHatasi("Katalog geçerli JSON değil: " + ex.Message); }
        using (d)
        {
            var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object) throw new KatalogHatasi("Katalog bir nesne olmalı.");
            string surum = k.TryGetProperty("catalog_version", out var sv) && sv.ValueKind == JsonValueKind.String ? sv.GetString()!.Trim() : "";
            if (surum.Length is 0 or > 40 || surum.Any(char.IsControl)) throw new KatalogHatasi("catalog_version zorunlu (en çok 40 karakter).");
            if (!k.TryGetProperty("builds", out var b) || b.ValueKind != JsonValueKind.Object) throw new KatalogHatasi("builds nesnesi zorunlu.");
            var yapilar = new Dictionary<string, List<KbOgesi>>(); int toplam = 0;
            foreach (var y in b.EnumerateObject())
            {
                if (!Regex.IsMatch(y.Name, @"^\d{4,6}$")) throw new KatalogHatasi("Geçersiz yapı numarası: " + y.Name);
                if (yapilar.Count >= EnCokYapi) throw new KatalogHatasi("En çok " + EnCokYapi + " yapı kabul edilir.");
                if (y.Value.ValueKind != JsonValueKind.Array) throw new KatalogHatasi($"{y.Name}: dizi olmalı.");
                var liste = new List<KbOgesi>();
                foreach (var o in y.Value.EnumerateArray())
                {
                    if (++toplam > EnCokOge) throw new KatalogHatasi("En çok " + EnCokOge + " güncelleme kabul edilir.");
                    string kb = Al(o, "kb").ToUpperInvariant(), rel = Al(o, "released"), onem = Al(o, "severity"), baslik = Al(o, "title");
                    if (!Regex.IsMatch(kb, @"^KB\d{6,8}$")) throw new KatalogHatasi($"{y.Name}: geçersiz KB numarası '{Kisalt(kb)}'.");
                    if (!DateTime.TryParseExact(rel, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var tarih)) throw new KatalogHatasi($"{kb}: released yyyy-MM-dd biçiminde olmalı.");
                    if (!Onemler.Contains(onem)) throw new KatalogHatasi($"{kb}: severity şunlardan biri olmalı: {string.Join(", ", Onemler)}.");
                    if (liste.Any(x => x.Kb == kb)) continue;   // tekrar yok sayılır
                    liste.Add(new KbOgesi(kb, tarih, onem, baslik.Length > 200 ? baslik[..200] : baslik));
                }
                yapilar[y.Name] = liste;
            }
            if (yapilar.Count == 0) throw new KatalogHatasi("Katalogda hiç yapı yok.");
            string ozet = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
            return new KbCatalog(surum, ozet, yapilar);
        }
    }
    private static string Al(JsonElement o, string ad) => o.ValueKind == JsonValueKind.Object && o.TryGetProperty(ad, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
    private static string Kisalt(string s) => new string(s.Where(c => !char.IsControl(c)).ToArray()) is { Length: > 20 } u ? u[..20] : s;

    // İçe aktarır: önce tamamen doğrular, sonra atomik yazar (geçici dosya + yer değiştirme). Hata olursa eski katalog DOKUNULMAZ.
    public static KbCatalog IceAktar(string json)
    {
        var k = Coz(json);
        Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, json);
        return k;
    }

    // Kayıtlı kataloğu yükler (yoksa/bozuksa null).
    public static KbCatalog? Yukle() { try { return File.Exists(Yol) ? Coz(File.ReadAllText(Yol)) : null; } catch { return null; } }
}

// Yerel yama denetçisi.
internal static class PatchAuditor
{
    // Doğrulama kontrol kimliği.
    public const string KontrolKimligi = "CHK-PATCH-LOCAL";
    // Son kurulumdan bu yana kaç gün geçerse "bayat" sayılır (sarı / kırmızı eşiği).
    public const int BayatGun = 90, CokBayatGun = 180;

    // Yapı numarası: "10.0.26100.1234" → "26100".
    public static string YapiNo(string osSurumu) { var p = (osSurumu ?? "").Split('.'); return p.Length >= 3 && Regex.IsMatch(p[2], @"^\d{4,6}$") ? p[2] : ""; }

    // Katalogdaki, kurulu OLMAYAN ve daha yeni bir güncelleme tarafından KARŞILANMAYAN güncellemeler.
    public static List<KbOgesi> Eksikler(IEnumerable<string> kurulu, List<KbOgesi> katalog)
    {
        var kur = kurulu.Select(x => x.ToUpperInvariant()).ToHashSet();
        // Kurulu olan katalog güncellemelerinin en yeni yayın tarihi: kümülatif kuralı.
        DateTime? enYeniKurulu = katalog.Where(o => kur.Contains(o.Kb)).Select(o => (DateTime?)o.Yayin).Max();
        return katalog.Where(o => !kur.Contains(o.Kb) && !(enYeniKurulu != null && enYeniKurulu >= o.Yayin)).OrderBy(o => o.Yayin).ToList();
    }

    // Yama durumunu toplar.
    public static PatchState Topla(IYerelKaynak k, KbCatalog? katalog, DateTime simdi)
    {
        var hf = k.Hotfixler(); string os = k.OsSurumu(); string yapi = YapiNo(os);
        var kurulu = hf.Select(h => h.Kb).Distinct().ToList(); DateTime? son = hf.Where(h => h.Tarih != null).Select(h => h.Tarih).Max();
        var eksik = katalog != null && yapi.Length > 0 && katalog.Yapilar.TryGetValue(yapi, out var l) ? Eksikler(kurulu, l).Select(x => x.Kb).ToList() : new List<string>();
        return new PatchState("wmi@local", simdi, kurulu, son, os, eksik, katalog?.Surum ?? "");
    }

    // Yama bulguları üretir. Katalog yoksa veya yapı katalogda yoksa bunu AÇIKÇA söyler (sessizce "temiz" demez).
    public static List<Finding> Bulgular(IYerelKaynak k, KbCatalog? katalog, string assetId, double varlikAgirligi, DateTime simdi, PatchState? durum = null)
    {
        durum ??= Topla(k, katalog, simdi); var l = new List<Finding>(); string yapi = YapiNo(durum.OsBuild);
        string[] iso = { "A.8.8" }, mitre = { "T1190" };
        // Katalog yok.
        if (katalog == null) l.Add(BulguFabrikasi.Yap("PATCH-NOCATALOG-001", "Yama", assetId, "katalog", "Yama kataloğu yüklenmedi", "Eksik güncellemeler değerlendirilemedi.", "Katalog olmadan hangi güvenlik güncellemelerinin eksik olduğu bilinemez; bu bir 'sorun yok' sonucu DEĞİLDİR.", new[] { $"İşletim sistemi: {durum.OsBuild}", $"Kurulu güncelleme sayısı: {durum.InstalledKbs.Count}" }, 0, varlikAgirligi, iso, Array.Empty<string>(), "\"KB Kataloğu Yükle\" ile kendi hazırladığınız katalog dosyasını yükleyin (biçim belgede).", KontrolKimligi, simdi));
        // Katalog var ama bu yapıyı içermiyor.
        else if (!katalog.Yapilar.ContainsKey(yapi)) l.Add(BulguFabrikasi.Yap("PATCH-BUILD-001", "Yama", assetId, "yapi", $"Katalogda bu Windows yapısı yok ({(yapi.Length > 0 ? yapi : "bilinmiyor")})", "Yüklenen katalog bu işletim sistemi yapısı için güncelleme listesi içermiyor.", "Eksik güncellemeler değerlendirilemedi.", new[] { $"İşletim sistemi: {durum.OsBuild}", $"Katalog sürümü: {katalog.Surum}" }, 0, varlikAgirligi, iso, Array.Empty<string>(), "Bu yapıyı içeren güncel bir katalog yükleyin.", KontrolKimligi, simdi));
        // Eksik güncelleme var.
        else if (durum.MissingKbs.Count > 0)
        {
            var ogeler = katalog.Yapilar[yapi].Where(o => durum.MissingKbs.Contains(o.Kb)).ToList();
            double etki = ogeler.Select(o => o.Onem switch { "Critical" => 9.0, "Important" => 7.0, "Moderate" => 5.0, _ => 3.0 }).DefaultIfEmpty(3).Max();
            l.Add(BulguFabrikasi.Yap("PATCH-MISSING-001", "Yama", assetId, "eksik", $"{durum.MissingKbs.Count} güvenlik güncellemesi eksik", "Katalogda olması gereken güncellemelerden bazıları bu bilgisayarda kurulu görünmüyor.", "Eksik güvenlik güncellemeleri, bilinen açıkların saldırganlarca kullanılmasına izin verebilir.",
                ogeler.Select(o => $"{o.Kb} ({o.Onem}, {o.Yayin:yyyy-MM-dd}) {o.Baslik}").Append($"Katalog sürümü: {katalog.Surum}").Append($"Katalog özeti (SHA-256): {katalog.Ozet[..16]}…").ToArray(), etki, varlikAgirligi, iso, mitre,
                "Windows Update'i çalıştırıp listedeki güncellemeleri kurun, gerekirse yeniden başlatın. Kurumsal ortamda yama yönetim aracınızı kullanın.", KontrolKimligi, simdi));
        }
        // Son kurulum tarihi bayat mı? (Katalogdan bağımsız, basit ve açıklanabilir.)
        if (durum.LastInstallDate is DateTime son)
        {
            int gun = (int)(simdi - son).TotalDays;
            if (gun >= BayatGun) l.Add(BulguFabrikasi.Yap("PATCH-STALE-001", "Yama", assetId, "bayat", $"Son güncelleme {gun} gün önce kurulmuş", "Bilgisayarda uzun süredir güvenlik güncellemesi kurulmamış.", "Düzenli güncellenmeyen sistemler bilinen açıklara karşı savunmasız kalır.",
                new[] { $"Son kurulum: {son:yyyy-MM-dd}", $"Geçen gün: {gun}", "Not: Windows'un güncelleme kaydı (QuickFix) her güncellemeyi göstermeyebilir; kesin sonuç için Windows Update geçmişine de bakın." }, gun >= CokBayatGun ? 7 : 5, varlikAgirligi, iso, mitre, "Windows Update'ten güncellemeleri kontrol edin ve kurun.", KontrolKimligi, simdi));
        }
        // Hiç güncelleme kaydı okunamadıysa bunu söyle.
        else if (durum.InstalledKbs.Count == 0) l.Add(BulguFabrikasi.Yap("PATCH-NODATA-001", "Yama", assetId, "veri", "Güncelleme kaydı okunamadı", "Kurulu güncelleme listesi alınamadı (yetki veya WMI sorunu olabilir).", "Yama durumu hakkında bir şey söylenemez; bu bir 'sorun yok' sonucu değildir.", new[] { $"İşletim sistemi: {durum.OsBuild}" }, 0, varlikAgirligi, iso, Array.Empty<string>(), "Programı yönetici olarak çalıştırıp yeniden deneyin.", KontrolKimligi, simdi));
        return l;
    }
}

// Yama bulguları için doğrulama kontrolü: yeniden toplayıp aynı bulgu hâlâ üretiliyor mu bakar.
internal sealed class YamaDogrulamaKontrolu : IVerificationCheck
{
    private readonly Func<List<Finding>> _yenidenTopla;
    public YamaDogrulamaKontrolu(Func<List<Finding>> yenidenTopla) { _yenidenTopla = yenidenTopla; }
    public string Id => PatchAuditor.KontrolKimligi;
    public Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct)
    {
        // Yeniden toplanınca hâlâ üretilen bulgu kimlikleri: kimlik kararlıdır, kısmen düzelmiş (hâlâ eksik KB var) bulgu da "geçmedi" sayılır.
        var simdiki = _yenidenTopla().Select(x => x.Id).ToHashSet();
        IReadOnlyDictionary<string, VerificationOutcome> sonuc = referans.ToDictionary(f => f.Id, f => simdiki.Contains(f.Id) ? VerificationOutcome.Fail : VerificationOutcome.Pass);
        return Task.FromResult(sonuc);
    }
}
