// Bu dosya "Bulgu Şeması v2" (Finding) modelini, şema doğrulayıcısını, denetim izini (AuditTrail) ve eski Bulgu ile köprüyü içerir.
// Eski Bulgu kaydı DEĞİŞMEZ; Finding yanında yaşar ve ToBulgu() ile eski ekranlara/raporlara çevrilir.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// V2 modelleri ayrı bir ad alanında durur, eski kodla çakışmaz.
namespace LabDenetim.V2;

// ---------- Sıralamalar ----------
// Düzeltme türü: tam otomatik / destekli (kullanıcı onaylı) / elle.
public enum RemediationType { Auto, Assisted, Manual }
// Bulgunun yaşam döngüsü durumu.
public enum FindingState { Open, Remediated, Verified, Closed, AcceptedRisk, FalsePositive }
// Doğrulama sonucu.
public enum VerificationOutcome { Pass, Fail, Error, NotRun }
// Yanlış alarm durumu.
public enum FpState { None, Suspected, Confirmed, Dismissed }

// ---------- Açıklama ----------
// Sade dilde: ne bulundu, neden önemli.
public sealed record Explanation(
    [property: JsonPropertyName("what")] string What,
    [property: JsonPropertyName("why_it_matters")] string WhyItMatters);

// ---------- Uyumluluk ve MITRE ----------
// KVKK / ISO 27001 / MITRE ATT&CK etiketleri.
public sealed record ComplianceTags(
    [property: JsonPropertyName("kvkk")] string[] Kvkk,
    [property: JsonPropertyName("iso27001")] string[] Iso27001,
    [property: JsonPropertyName("mitre_attack")] string[] MitreAttack);

// ---------- Yedek ve geri alma ----------
// Düzeltmeden önce neyin nasıl yedekleneceği.
public sealed record BackupPlan(
    [property: JsonPropertyName("what")] string What,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("verify_backup")] bool VerifyBackup);

// ---------- Doğrulama politikası ----------
// Düzeltmeden sonra yeniden kontrolün nasıl yapılacağı.
public sealed record VerificationPolicy(
    [property: JsonPropertyName("rerun_after_seconds")] int RerunAfterSeconds,
    [property: JsonPropertyName("max_retries")] int MaxRetries,
    [property: JsonPropertyName("on_fail")] string OnFail,
    [property: JsonPropertyName("close_when")] string CloseWhen = "all_referenced_findings_pass");

// ---------- Düzeltme ----------
// Bir bulgunun hazır çözümü ve güvenlik bilgileri.
public sealed record Remediation(
    [property: JsonPropertyName("remediation_type")] RemediationType Type,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("command")] string? Command,
    [property: JsonPropertyName("requires_uac")] bool RequiresUac,
    [property: JsonPropertyName("rollback_supported")] bool RollbackSupported,
    [property: JsonPropertyName("backup_plan")] BackupPlan? BackupPlan,
    [property: JsonPropertyName("verification_check_id")] string VerificationCheckId,
    [property: JsonPropertyName("verification_policy")] VerificationPolicy VerificationPolicy,
    [property: JsonPropertyName("unattended_class")] string? UnattendedClass = null);   // gözetimsiz sınıf adı (yoksa null)

// ---------- Yanlış alarm takibi ----------
// Bulgunun yanlış alarm olup olmadığı ve kuralın genel yanlış alarm oranı.
public sealed record FalsePositiveTracking(
    [property: JsonPropertyName("state")] FpState State,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("marked_by")] string? MarkedBy,
    [property: JsonPropertyName("marked_at")] DateTime? MarkedAt,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("rule_fp_count")] int RuleFpCount,
    [property: JsonPropertyName("rule_total_count")] int RuleTotalCount);

// ---------- Bulgu ----------
// Şemaya birebir uyan tek bir bulgu.
public sealed record Finding(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("rule_id")] string RuleId,
    [property: JsonPropertyName("module")] string Module,
    [property: JsonPropertyName("asset_id")] string AssetId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("explanation")] Explanation Explanation,
    [property: JsonPropertyName("evidence")] string[] Evidence,
    [property: JsonPropertyName("impact_weight")] double ImpactWeight,
    [property: JsonPropertyName("asset_weight")] double AssetWeight,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("compliance")] ComplianceTags Compliance,
    [property: JsonPropertyName("remediation")] Remediation Remediation,
    [property: JsonPropertyName("false_positive_tracking")] FalsePositiveTracking FalsePositiveTracking,
    [property: JsonPropertyName("state")] FindingState State,
    [property: JsonPropertyName("first_seen")] DateTime FirstSeen,
    [property: JsonPropertyName("last_seen")] DateTime LastSeen)
{
    // Skor formülü tek yerde: etki (0-10) × varlık ağırlığı (0-1) × 10, 0-100 arası tam sayı.
    public static int HesaplaSkor(double impact, double asset) => (int)Math.Round(Math.Clamp(impact, 0, 10) * Math.Clamp(asset, 0, 1) * 10, MidpointRounding.AwayFromZero);

    // Skoru eski dört kademeli Önem'e çevirir (eski tablo ve raporlar bozulmasın).
    [JsonIgnore] public Onem EskiOnem => Score >= 70 ? Onem.Yuksek : Score >= 40 ? Onem.Orta : Score >= 15 ? Onem.Dusuk : Onem.Bilgi;

    // Kararlı kimlik: aynı kural + aynı varlık + aynı anahtar kanıt her taramada aynı kimliği verir.
    public static string KararliKimlik(string ruleId, string assetId, string anahtarKanit)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ruleId + "|" + assetId + "|" + anahtarKanit))).ToLowerInvariant();

    // Eski Bulgu'ya çevirir (mevcut ekran ve raporlar için).
    public Bulgu ToBulgu() => new(Module, Title, EskiOnem, string.Join("\n", Evidence), Remediation.Summary);

    // Eski Bulgu'dan Finding üretir. Eski kayıtta çözüm komutu yoktur, bu yüzden "Manual" olur (otomatik/destekli YAPILAMAZ).
    public static Finding FromBulgu(Bulgu b, string assetId, DateTime simdiUtc, double assetWeight = 0.5)
    {
        // Eski önemden etki puanı türetilir (Yüksek 9, Orta 6, Düşük 3, Bilgi 0).
        double impact = b.Onem switch { Onem.Yuksek => 9, Onem.Orta => 6, Onem.Dusuk => 3, _ => 0 };
        // Kural kimliği: modül + başlık (kararlı olması için).
        string ruleId = "LEGACY-" + b.Modul.ToUpperInvariant().Replace(' ', '_');
        // Uyumluluk etiketini mevcut eşleştirmeden al; yoksa genel erişim kontrolü etiketi kullan.
        var u = Uyumluluk.Bul(b); var m = Mitre.Bul(b);
        var comp = new ComplianceTags(
            new[] { u?.KvkkKisa ?? "KVKK m.12" },
            u is { IsoKodlari.Count: > 0 } ? u.IsoKodlari.ToArray() : new[] { "A.8.8" },
            m is null ? Array.Empty<string>() : new[] { m.Kimlik });
        // Doğrulama kimliği: aynı modül ve başlığı aynı kontrol yeniden çalıştırır (çoktan-bire).
        var rem = new Remediation(RemediationType.Manual, string.IsNullOrWhiteSpace(b.Oneri) ? "Ayrıntıyı inceleyin." : b.Oneri, null, false, false, null,
            "CHK-" + b.Modul.ToUpperInvariant().Replace(' ', '_'), new VerificationPolicy(0, 1, "reopen"));
        // Skor formülle hesaplanır, bulgu "açık" başlar.
        int skor = HesaplaSkor(impact, assetWeight);
        return new Finding(KararliKimlik(ruleId, assetId, b.Baslik), ruleId, b.Modul, assetId, b.Baslik,
            new Explanation(b.Baslik, string.IsNullOrWhiteSpace(b.Detay) ? "Ayrıntı yok." : b.Detay), string.IsNullOrWhiteSpace(b.Detay) ? Array.Empty<string>() : new[] { b.Detay },
            impact, assetWeight, skor, comp, rem, new FalsePositiveTracking(FpState.None, ruleId, null, null, null, 0, 0), FindingState.Open, simdiUtc, simdiUtc);
    }
}

// ---------- Şema doğrulayıcı ("birebir uyum") ----------
// Bir Finding listeye girmeden önce bu denetimden geçmek zorundadır; hata listesi boşsa geçerlidir.
public static class FindingSchema
{
    // Geçerli on_fail değerleri.
    private static readonly string[] OnFailDegerleri = { "reopen", "reopen_and_rollback" };

    // Kuralları denetler, bulduğu her hatayı Türkçe yazar.
    public static List<string> Validate(Finding f, bool yardimciKayitli = false)
    {
        // Hata listesi.
        var e = new List<string>();
        // Boş nesne gelirse hemen dön.
        if (f is null) { e.Add("bulgu boş"); return e; }
        // Kimlikler.
        if (string.IsNullOrWhiteSpace(f.Id) || string.IsNullOrWhiteSpace(f.RuleId)) e.Add("id/rule_id zorunlu");
        if (string.IsNullOrWhiteSpace(f.AssetId)) e.Add("asset_id zorunlu");
        if (string.IsNullOrWhiteSpace(f.Title)) e.Add("title zorunlu");
        // Ağırlıklar aralığı ve skor formülü.
        if (f.ImpactWeight is < 0 or > 10) e.Add("impact_weight 0-10 olmalı");
        if (f.AssetWeight is < 0 or > 1) e.Add("asset_weight 0-1 olmalı");
        if (f.Score != Finding.HesaplaSkor(f.ImpactWeight, f.AssetWeight)) e.Add("score formülle uyuşmuyor");
        // Açıklama.
        if (f.Explanation is null || string.IsNullOrWhiteSpace(f.Explanation.What) || string.IsNullOrWhiteSpace(f.Explanation.WhyItMatters)) e.Add("explanation.what/why_it_matters zorunlu");
        // Kanıt listesi boş olamaz (null olmamalı).
        if (f.Evidence is null) e.Add("evidence null olamaz");
        // KVKK ve ISO etiketi zorunlu, MITRE boş olabilir.
        if (f.Compliance is null || f.Compliance.Kvkk is null || f.Compliance.Iso27001 is null || f.Compliance.Kvkk.Length == 0 || f.Compliance.Iso27001.Length == 0) e.Add("KVKK ve ISO etiketi zorunlu");
        // Düzeltme bölümü.
        var r = f.Remediation;
        if (r is null) { e.Add("remediation zorunlu"); }
        else
        {
            if (string.IsNullOrWhiteSpace(r.Summary)) e.Add("remediation.summary zorunlu");
            if (string.IsNullOrWhiteSpace(r.VerificationCheckId)) e.Add("verification_check_id zorunlu");
            if (r.VerificationPolicy is null) e.Add("verification_policy zorunlu");
            else if (!OnFailDegerleri.Contains(r.VerificationPolicy.OnFail)) e.Add("verification_policy.on_fail 'reopen' veya 'reopen_and_rollback' olmalı");
            // Otomatik düzeltme yalnızca geri alınabiliyor ve yedeği varsa olur.
            if (r.Type == RemediationType.Auto && !(r.RollbackSupported && r.BackupPlan is not null)) e.Add("auto: rollback_supported=true ve backup_plan zorunlu");
            if (r.RollbackSupported && r.BackupPlan is null) e.Add("rollback_supported=true ise backup_plan zorunlu");
            if (r.Type != RemediationType.Manual && string.IsNullOrWhiteSpace(r.Command)) e.Add("auto/assisted: command zorunlu");
            // Başarısız doğrulamada geri alma istenirse geri alma desteklenmeli.
            if (r.VerificationPolicy?.OnFail == "reopen_and_rollback" && !r.RollbackSupported) e.Add("on_fail=reopen_and_rollback için rollback_supported=true gerekir");
            // Gözetimsiz sınıf: yalnızca geri alınabilir, yedekli, otomatik eylemler; UAC'li ise yükseltilmiş yardımcı kayıtlı olmalı.
            if (r.UnattendedClass != null)
            {
                if (r.Type != RemediationType.Auto) e.Add("unattended_class için type=auto gerekir");
                if (!r.RollbackSupported || r.BackupPlan is null) e.Add("unattended_class için rollback_supported ve backup_plan gerekir");
                if (r.RequiresUac && !yardimciKayitli) e.Add("unattended_class + requires_uac için yükseltilmiş yardımcı kayıtlı olmalı");
            }
        }
        // Yanlış alarm izleme.
        if (f.FalsePositiveTracking is null) e.Add("false_positive_tracking zorunlu");
        else if (f.FalsePositiveTracking.RuleFpCount > f.FalsePositiveTracking.RuleTotalCount) e.Add("rule_fp_count rule_total_count'tan büyük olamaz");
        // Zaman damgaları.
        if (f.LastSeen < f.FirstSeen) e.Add("last_seen first_seen'den önce olamaz");
        // Sonuç.
        return e;
    }

    // JSON'a yazar (snake_case alan adları, sıralamalar yazı olarak).
    public static string Json(Finding f) => JsonSerializer.Serialize(f, SecenekJson);
    // JSON'dan okur.
    public static Finding? Oku(string json) => JsonSerializer.Deserialize<Finding>(json, SecenekJson);
    // Ortak seçenekler.
    internal static readonly JsonSerializerOptions SecenekJson = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } };
}

// ---------- Doğrulama kontrolü (çoktan-bire) ----------
// Aggregate yalnızca TÜM referans bulgular geçtiyse Pass olur.
public sealed record CheckRunResult(string CheckId, DateTime At, VerificationOutcome Aggregate, IReadOnlyDictionary<string, VerificationOutcome> PerFinding)
{
    // Bulgu sonuçlarından toplam sonucu hesaplar: biri Fail ise Fail, biri Error ise Error, hepsi Pass ise Pass.
    public static VerificationOutcome Birlestir(IEnumerable<VerificationOutcome> sonuclar)
    {
        var l = sonuclar.ToList();
        if (l.Count == 0) return VerificationOutcome.NotRun;
        if (l.Contains(VerificationOutcome.Fail)) return VerificationOutcome.Fail;
        if (l.Contains(VerificationOutcome.Error)) return VerificationOutcome.Error;
        if (l.Contains(VerificationOutcome.NotRun)) return VerificationOutcome.NotRun;
        return VerificationOutcome.Pass;
    }
}

// ---------- Denetim izi ----------
// Tek bir kayıt: kim, ne zaman, ne yaptı; önceki kaydın özetiyle zincirlenir (değiştirilirse zincir bozulur).
public sealed record AuditEntry(
    [property: JsonPropertyName("seq")] long Seq,
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("actor")] string Actor,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("finding_id")] string? FindingId,
    [property: JsonPropertyName("check_id")] string? CheckId,
    [property: JsonPropertyName("requires_uac")] bool RequiresUac,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("prev_hash")] string PrevHash,
    [property: JsonPropertyName("hash")] string Hash);

// Ekleme-yalnızca, zincirleme özetli denetim izi. İsteğe bağlı dosyaya (JSON satırları) yazar.
public sealed class AuditTrail
{
    // Geçerli eylem adları (bilinmeyen eylem reddedilir).
    public static readonly string[] Eylemler = { "remediation_planned", "approved", "backup_taken", "applied", "verify_run", "verified", "closed", "rolled_back", "fp_marked", "dry_run", "rejected", "blocked", "failed", "policy_changed" };
    // İlk kaydın "önceki özeti".
    public const string Baslangic = "GENESIS";
    // Kayıtlar (yalnızca okunur görünüm dışarı verilir).
    private readonly List<AuditEntry> _kayitlar = new();
    // Dosya yolu (null ise yalnızca bellekte).
    private readonly string? _yol;
    // Aynı anda iki iş parçacığı yazmasın.
    private readonly object _kilit = new();

    // Oluşturur; dosya verilmişse varsa okur ve zinciri doğrular.
    public AuditTrail(string? dosyaYolu = null)
    {
        _yol = dosyaYolu;
        if (_yol != null && File.Exists(_yol))
            YukleDosya();
    }

    // Dosyadaki kayıtları okur. Çökmeyle yarım kalmış SON satır atılır ve dosya onarılır; ORTADAKİ bozuk satır ise kurcalama belirtisidir ve hata verir.
    private void YukleDosya()
    {
        // Boş olmayan satırlar.
        var satirlar = File.ReadAllLines(_yol!, Encoding.UTF8).Where(s => s.Trim().Length > 0).ToList(); bool yarimKuyruk = false;
        // Her satır.
        for (int i = 0; i < satirlar.Count; i++)
        {
            // Çöz; yalnızca SON satır bozuksa yarım yazılmış sayılır.
            try { var k = JsonSerializer.Deserialize<AuditEntry>(satirlar[i]); if (k != null) _kayitlar.Add(k); }
            catch (JsonException) when (i == satirlar.Count - 1) { yarimKuyruk = true; }
        }
        // Yarım satırı dosyadan da kaldır (sonraki ekleme bozuk satıra yapışmasın).
        if (yarimKuyruk) Depo.Yaz(_yol!, string.Join("\n", satirlar.Take(satirlar.Count - 1)) + (satirlar.Count > 1 ? "\n" : ""));
    }

    // Kayıtların kopyası.
    public IReadOnlyList<AuditEntry> Kayitlar { get { lock (_kilit) return _kayitlar.ToList(); } }

    // Bir kaydın özetini hesaplar: önceki özet + tüm alanlar.
    public static string OzetHesapla(AuditEntry k)
    {
        // Alanlar ayırıcıyla birleştirilir (ayırıcı kaymasıyla çakışma olmasın diye uzunluk da yazılır).
        string[] alanlar = { k.Seq.ToString(CultureInfo.InvariantCulture), k.Time.ToUniversalTime().ToString("O"), k.Actor, k.Action, k.FindingId ?? "", k.CheckId ?? "", k.RequiresUac ? "1" : "0", k.Detail, k.PrevHash };
        string metin = string.Join("\u001f", alanlar.Select(a => a.Length + ":" + a));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metin))).ToLowerInvariant();
    }

    // Yeni kayıt ekler ve döndürür.
    public AuditEntry Ekle(string actor, string action, string? findingId, string? checkId, bool requiresUac, string detail, DateTime? zaman = null)
    {
        // Eylem bilinen listede olmalı.
        if (!Eylemler.Contains(action)) throw new ArgumentException("Bilinmeyen denetim eylemi: " + action);
        lock (_kilit)
        {
            // Sıra ve önceki özet.
            long seq = _kayitlar.Count + 1; string onceki = _kayitlar.Count == 0 ? Baslangic : _kayitlar[^1].Hash;
            // Özetsiz kayıt, sonra özetli kayıt.
            var taslak = new AuditEntry(seq, zaman ?? DateTime.UtcNow, string.IsNullOrWhiteSpace(actor) ? "system" : actor, action, findingId, checkId, requiresUac, detail ?? "", onceki, "");
            var k = taslak with { Hash = OzetHesapla(taslak) };
            // Dosyaya önce yaz, sonra belleğe ekle (yazma başarısızsa bellek ile dosya ayrışmaz).
            if (_yol != null) { var d = Path.GetDirectoryName(_yol); if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d); File.AppendAllText(_yol, JsonSerializer.Serialize(k) + Environment.NewLine, Encoding.UTF8); }
            _kayitlar.Add(k);
            return k;
        }
    }

    // Zincir bütün mü? Bozuk ilk kaydın sırasını (yoksa null) döndürür.
    public static (bool Saglam, long? BozukSira) Dogrula(IReadOnlyList<AuditEntry> kayitlar)
    {
        // Her kayıt: sıra doğru, önceki özet doğru, kendi özeti doğru olmalı.
        string onceki = Baslangic;
        for (int i = 0; i < kayitlar.Count; i++)
        {
            var k = kayitlar[i];
            if (k.Seq != i + 1 || k.PrevHash != onceki || k.Hash != OzetHesapla(k)) return (false, k.Seq);
            onceki = k.Hash;
        }
        return (true, null);
    }

    // Bu izin kendi zincirini doğrular.
    public (bool Saglam, long? BozukSira) Dogrula() => Dogrula(Kayitlar);
}
