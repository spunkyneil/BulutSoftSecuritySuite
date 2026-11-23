# Tasarım: Bulgu Şeması v2, Derin Tarama ve Yazılım Açık Denetimi Modülleri

Durum: **TASARIM (kod yazılmadı).** Mevcut tarama motoruna dokunulmaz; her şey ekleme (yan yana) olarak planlandı.

## 0. Kısa cevap ve önce netleşmesi gerekenler

**Entegre edilebilir mi? Evet.** Mevcut mimariyle (Kali sensör + Python ajan + Windows komuta merkezi + imzalı/ onaylı eylemler) uyumlu. Mevcut `Bulgu` kaydı ve tüm raporlar bozulmasın diye yeni şema **ayrı bir kayıt (`Finding`)** olarak eklenir; ikisi arasında bir bağdaştırıcı (adapter) çalışır.

**Verdiğim kararlar (itiraz varsa değiştirilir):**

| # | Konu | Karar |
|---|------|-------|
| 1 | "Hiçbir veri cihazdan çıkmaz" ile NVD/CISA KEV eşleştirmesi çelişir görünür | **KARAR (kullanıcı): onaylı "İndir" düğmesi olacak** (elle yükleme de kalır). Zafiyet verisi çevrimdışı saklanır; indirme yalnızca **veri çekme** yönündedir (cihazdan hiçbir veri gönderilmez), her seferinde onay ister, kaynak adresi sabit listededir (NVD ve CISA KEV), indirilen dosyanın SHA-256'sı ve tarihi kaydedilir, bozuk dosyada eski besleme korunur. |
| 2 | `remediation_type = auto` | **KARAR (kullanıcı): bazı sınıflar için gözetimsiz otomatik olacak.** Güvenlik çerçevesi: bkz. §2.10 "Gözetimsiz Otomatik Politikası" (sınıf bazlı, varsayılan KAPALI, yönetici bir kez açıkça etkinleştirir, yalnızca geri alınabilir ve yedekli sınıflar, süre sınırlı, denetim izli). Diğer tüm sınıflar "onaylı tek tık" kalır. |
| 3 | Kimlik doğrulamalı WMI (uzak makineler) | **KARAR (kullanıcı): uzak WMI gerekli.** Yerel makine ilk teslim; uzak makine ikinci teslim. Kimlik bilgisi **diske yazılmaz**, `SecureString`/bellekte oturum boyunca tutulur, bitince sıfırlanır. Uzak hedef yalnızca özel ağda olabilir (Kapsam kapısı), her bağlantı onay ister ve denetim izine yazılır. |
| 4 | Yazılım→CPE eşleştirme yanlış alarm üretir | Her eşleşmeye `confidence` (güven) konur; yalnızca **KEV'de olan + sürüm aralığı tam eşleşen** bulgular yüksek önem alır. Diğerleri "Bilgi/Düşük" ve yanlış alarm takibine açık. |
| 5 | `impact_weight × asset_weight × 10` | `impact_weight` 0-10, `asset_weight` 0-1 → skor 0-100. Eşikler: ≥70 Yüksek, ≥40 Orta, ≥15 Düşük, altı Bilgi (mevcut `Onem` ile uyumlu). |
| 6 | Mesajınızda iki yer bozuk geldi | **KARAR (kullanıcı): "Stajyer" satırı yok sayılır. HTTP modülü "pasif" değil AKTİF olacak** (bkz. §2.5). Dış ağ (Mod B) kuralı DEĞİŞMEDİ: internetteki hedeflerde yalnızca pasif gözlem kalır (az önce kodlanan Kapsam kilidi); aktif HTTP yalnızca iç ağ hedeflerinde (Mod A) çalışır. |

## 1. Veri modeli (C#)

Tüm alanlar JSON'da `snake_case` yazılır (`[JsonPropertyName]`). Bir `Finding`, `FindingSchema.Validate` geçmeden listeye giremez ("birebir şema").

```csharp
using System.Text.Json.Serialization;
namespace LabDenetim.V2;

// ---------- Sıralamalar ----------
public enum RemediationType { Auto, Assisted, Manual }          // otomatik / destekli / manuel
public enum FindingState { Open, Remediated, Verified, Closed, AcceptedRisk, FalsePositive }
public enum VerificationOutcome { Pass, Fail, Error, NotRun }
public enum FpState { None, Suspected, Confirmed, Dismissed }

// ---------- Açıklama ----------
public sealed record Explanation(
    [property: JsonPropertyName("what")] string What,                       // ne bulundu (sade dil)
    [property: JsonPropertyName("why_it_matters")] string WhyItMatters);    // neden önemli

// ---------- Uyumluluk ve MITRE ----------
public sealed record ComplianceTags(
    [property: JsonPropertyName("kvkk")] string[] Kvkk,                     // örn. ["KVKK m.12/1"]
    [property: JsonPropertyName("iso27001")] string[] Iso27001,             // örn. ["A.8.8","A.8.20"]
    [property: JsonPropertyName("mitre_attack")] string[] MitreAttack);     // örn. ["T1190"]

// ---------- Yedek ve geri alma ----------
public sealed record BackupPlan(
    [property: JsonPropertyName("what")] string What,                       // neyin yedeği (kayıt anahtarı, kural seti, dosya)
    [property: JsonPropertyName("method")] string Method,                   // "reg export", "netsh export", "dosya kopyası", "StartupApproved önceki değer"
    [property: JsonPropertyName("location")] string Location,               // yedeğin yolu
    [property: JsonPropertyName("verify_backup")] bool VerifyBackup);       // yedek alındıktan sonra okunabilirlik doğrulansın mı

// ---------- Doğrulama politikası ----------
public sealed record VerificationPolicy(
    [property: JsonPropertyName("rerun_after_seconds")] int RerunAfterSeconds,   // düzeltmeden kaç sn sonra yeniden çalıştır
    [property: JsonPropertyName("max_retries")] int MaxRetries,
    [property: JsonPropertyName("on_fail")] string OnFail,                       // "reopen" | "reopen_and_rollback"
    [property: JsonPropertyName("close_when")] string CloseWhen = "all_referenced_findings_pass"); // çoktan-bire kuralı (bkz. §2.9)

// ---------- Düzeltme ----------
public sealed record Remediation(
    [property: JsonPropertyName("remediation_type")] RemediationType Type,
    [property: JsonPropertyName("summary")] string Summary,                       // hazır çözüm (sade dil)
    [property: JsonPropertyName("command")] string? Command,                      // hazır komut (varsa)
    [property: JsonPropertyName("requires_uac")] bool RequiresUac,                // UAC gerekir mi (açıkça işaretli)
    [property: JsonPropertyName("rollback_supported")] bool RollbackSupported,
    [property: JsonPropertyName("backup_plan")] BackupPlan? BackupPlan,
    [property: JsonPropertyName("verification_check_id")] string VerificationCheckId,   // ÇOKTAN-BİRE: aynı kimliği birçok bulgu kullanabilir
    [property: JsonPropertyName("verification_policy")] VerificationPolicy VerificationPolicy);

// ---------- Yanlış alarm takibi ----------
public sealed record FalsePositiveTracking(
    [property: JsonPropertyName("state")] FpState State,
    [property: JsonPropertyName("rule_id")] string RuleId,                         // bu bulguyu üreten kural
    [property: JsonPropertyName("marked_by")] string? MarkedBy,
    [property: JsonPropertyName("marked_at")] DateTime? MarkedAt,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("rule_fp_count")] int RuleFpCount,                 // kuralın şimdiye kadarki yanlış alarm sayısı
    [property: JsonPropertyName("rule_total_count")] int RuleTotalCount);          // kuralın toplam bulgu sayısı (oran = fp/total)

// ---------- Bulgu ----------
public sealed record Finding(
    [property: JsonPropertyName("id")] string Id,                                  // kararlı kimlik: SHA-256(rule_id|asset_id|anahtar kanıt)
    [property: JsonPropertyName("rule_id")] string RuleId,                         // örn. "SW-KEV-001"
    [property: JsonPropertyName("module")] string Module,                          // mevcut Modul adıyla uyumlu ("Port","HTTP","Yazılım"...)
    [property: JsonPropertyName("asset_id")] string AssetId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("explanation")] Explanation Explanation,
    [property: JsonPropertyName("evidence")] string[] Evidence,                    // ham kanıt (başlık değeri, sürüm, KB listesi...)
    [property: JsonPropertyName("impact_weight")] double ImpactWeight,             // 0-10
    [property: JsonPropertyName("asset_weight")] double AssetWeight,               // 0-1
    [property: JsonPropertyName("score")] int Score,                               // = round(impact × asset × 10), 0-100
    [property: JsonPropertyName("compliance")] ComplianceTags Compliance,
    [property: JsonPropertyName("remediation")] Remediation Remediation,
    [property: JsonPropertyName("false_positive_tracking")] FalsePositiveTracking FalsePositiveTracking,
    [property: JsonPropertyName("state")] FindingState State,
    [property: JsonPropertyName("first_seen")] DateTime FirstSeen,
    [property: JsonPropertyName("last_seen")] DateTime LastSeen)
{
    // Skor formülü tek yerde.
    public static int HesaplaSkor(double impact, double asset) => (int)Math.Round(Math.Clamp(impact, 0, 10) * Math.Clamp(asset, 0, 1) * 10);
    // Mevcut Onem'e dönüşüm (eski raporlar/tablo bozulmasın).
    public Onem EskiOnem => Score >= 70 ? Onem.Yuksek : Score >= 40 ? Onem.Orta : Score >= 15 ? Onem.Dusuk : Onem.Bilgi;
    // Mevcut Bulgu'ya bağdaştırıcı.
    public Bulgu ToBulgu() => new(Module, Title, EskiOnem, string.Join("\n", Evidence), Remediation.Summary);
}

// ---------- Şema doğrulayıcı ("birebir uyum") ----------
public static class FindingSchema
{
    public static List<string> Validate(Finding f)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(f.Id) || string.IsNullOrWhiteSpace(f.RuleId)) e.Add("id/rule_id zorunlu");
        if (f.Score != Finding.HesaplaSkor(f.ImpactWeight, f.AssetWeight)) e.Add("score formülle uyuşmuyor");
        if (f.Explanation is null || string.IsNullOrWhiteSpace(f.Explanation.What) || string.IsNullOrWhiteSpace(f.Explanation.WhyItMatters)) e.Add("explanation.what/why_it_matters zorunlu");
        if (f.Compliance is null || f.Compliance.Kvkk.Length == 0 || f.Compliance.Iso27001.Length == 0) e.Add("KVKK ve ISO etiketi zorunlu"); // MITRE boş olabilir (her bulgu bir teknik değildir)
        var r = f.Remediation;
        if (r is null) { e.Add("remediation zorunlu"); return e; }
        if (string.IsNullOrWhiteSpace(r.VerificationCheckId)) e.Add("verification_check_id zorunlu");
        if (r.VerificationPolicy is null) e.Add("verification_policy zorunlu");
        if (r.Type == RemediationType.Auto && !(r.RollbackSupported && r.BackupPlan is not null)) e.Add("auto: rollback_supported=true ve backup_plan zorunlu");
        if (r.RollbackSupported && r.BackupPlan is null) e.Add("rollback_supported=true ise backup_plan zorunlu");
        if (r.Type != RemediationType.Manual && string.IsNullOrWhiteSpace(r.Command)) e.Add("auto/assisted: command zorunlu");
        if (r.RequiresUac && r.Type == RemediationType.Auto && r.VerificationPolicy?.OnFail is not ("reopen" or "reopen_and_rollback")) e.Add("UAC'li auto: on_fail tanımlı olmalı");
        if (f.FalsePositiveTracking is null) e.Add("false_positive_tracking zorunlu");
        return e;
    }
}

// ---------- Doğrulama kontrolü (çoktan-bire) ----------
public sealed record CheckRunResult(
    string CheckId, DateTime At, VerificationOutcome Aggregate,                          // Aggregate = Pass ancak TÜM referans bulgular geçtiyse
    IReadOnlyDictionary<string, VerificationOutcome> PerFinding);

// ---------- Denetim izi (ekleme-yalnızca, zincirleme özet) ----------
public sealed record AuditEntry(
    [property: JsonPropertyName("seq")] long Seq,
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("actor")] string Actor,                    // kullanıcı veya "system"
    [property: JsonPropertyName("action")] string Action,                  // "remediation_planned|approved|backup_taken|applied|verify_run|verified|closed|rolled_back|fp_marked"
    [property: JsonPropertyName("finding_id")] string? FindingId,
    [property: JsonPropertyName("check_id")] string? CheckId,
    [property: JsonPropertyName("requires_uac")] bool RequiresUac,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("prev_hash")] string PrevHash,
    [property: JsonPropertyName("hash")] string Hash);                     // SHA-256(prev_hash|alanlar): kurcalama belli olur

// ---------- Ağ haritası veri modeli ----------
public sealed record NetworkMap(
    string MapId, DateTime BuiltAt, List<Subnet> Subnets, List<Asset> Assets, List<Link> Links);
public sealed record Subnet(string Cidr, string Name, string SensorId);
public sealed record Asset(
    string AssetId,                         // MAC varsa MAC, yoksa "ip:x"
    string[] Addresses, string? Hostname, string? OsGuess, string Role,             // "server"|"workstation"|"network"|"guest"|"vm"
    double AssetWeight,                     // 0-1 (kritiklik)
    List<ServiceEndpoint> Services, List<ServiceBinding> Bindings,
    List<InstalledSoftware> Software, PatchState? Patch, string[] Tags, string Provenance); // Provenance: "nmap@sensor1","wmi@local"
public sealed record ServiceEndpoint(int Port, string Proto, string State, string Service, string? Product, string? Version, string? ExtraInfo, string[] Cpe);
public sealed record Link(string FromAssetId, string ToAssetId, string Kind, string? Port); // "reachable","observed_connection"

// ---------- Servis → Windows hizmeti uyumu ----------
public sealed record ServiceBinding(
    int Port, string Proto, int Pid, string ProcessName, string? ImagePath,
    string? WindowsServiceName, string? ServiceAccount, string? StartType, string SignatureState, bool PathQuoted);

// ---------- Yama durumu (WMI) ----------
public sealed record PatchState(string Source, DateTime CollectedAt, List<string> InstalledKbs, DateTime? LastInstallDate, string OsBuild, List<string> MissingKbs, string CatalogVersion);

// ---------- Yazılım envanteri ve zafiyet eşleşmesi ----------
public sealed record InstalledSoftware(string Name, string? Version, string? Publisher, string? InstallLocation, string UninstallKey, string? CandidateCpe, double CpeConfidence);
public sealed record VulnMatch(string Cve, double? Cvss, bool InKev, DateTime? KevDueDate, string MatchBasis /* exact|range|heuristic */, double Confidence, string FeedVersion);

// ---------- PE statik analiz ----------
public sealed record PeReport(
    string Path, string Machine, bool IsManaged,
    bool Aslr, bool HighEntropyVa, bool Dep, bool Cfg, bool SafeSeh,
    string SignatureState, string? Publisher,
    bool InWritableDirectory, string[] WritableBy, bool DllSearchRisk, string[] RiskReasons);
```

## 2. Modül tasarımı

Her modül **tek sorumlu** ve arayüzle konuşur. Yerinde bir `FindingPipeline` hepsini ortak bulgu biçimine çevirir.

### 2.1 `NmapNormalizer` (sensör tarafı)
- **Sorumlu:** Sensörde `nmap -sV -oX -` çalıştırmak (yalnızca onaylı sabit komut, hedef özel ağ olmalı) ve XML çıktısını `NormalizedScan` modeline çevirmek (port, servis, ürün, sürüm, CPE).
- **Arayüz:** `interface INmapRunner { Task<NormalizedScan> RunAsync(SensorProfili s, string target, NmapProfile p, CancellationToken ct); }` · `static class NmapXml { NormalizedScan Parse(string xml); }`
- **Girdi:** Mevcut `SensorIstemci` + ajan "nmap" modu. **Çıktı:** `NormalizedScan` → `NetworkMapBuilder`.

### 2.2 `NetworkMapBuilder`
- **Sorumlu:** Nmap, ARP, yerel numaralandırma, WMI ve VirtualBox bilgisini tek `NetworkMap`'te birleştirmek; her kayıtta `Provenance` tutmak; varlık ağırlığı (`asset_weight`) atamak (kullanıcı değiştirebilir).
- **Arayüz:** `interface INetworkMapBuilder { NetworkMap Merge(NetworkMap? onceki, IEnumerable<IAssetObservation> gozlemler); }` · `interface IAssetStore { NetworkMap? Load(); void Save(NetworkMap m); }`
- **Mevcutla ilişki:** `CihazDeposu` (envanter) ve `VirtualBoxYardimcisi` gözlem kaynağı olur.

### 2.3 `ServiceMapper`
- **Sorumlu:** Açık port → süreç (PID) → Windows hizmeti → çalışan hesap/imza zinciri. Yerelde `GetExtendedTcpTable` ve `Win32_Service`; uzakta kimlik bilgili WMI.
- **Arayüz:** `interface IServiceMapper { Task<IReadOnlyList<ServiceBinding>> MapAsync(AssetRef asset, int[] ports, ICredentialSession? cred, CancellationToken ct); }`
- **Üretilen bulgular:** Çalıştırılan dosyası yazılabilir klasörde olan hizmet, tırnaksız yol, SYSTEM yetkisiyle çalışan imzasız hizmet.

### 2.4 `PatchAuditor` (WMI)
- **Sorumlu:** `Win32_QuickFixEngineering`, OS yapı numarası ve son yama tarihi ile **çevrimdışı KB kataloğunu** karşılaştırmak (katalog dosyası elle yüklenir; sürüm numarası raporda görünür).
- **Arayüz:** `interface IPatchAuditor { Task<PatchState> AuditAsync(AssetRef asset, ICredentialSession? cred, KbCatalog catalog, CancellationToken ct); }` · `interface ICredentialSession : IDisposable` (bellekte, saklanmaz).

### 2.5 `HttpActiveAuditor` (AKTİF HTTP/HTTPS, yalnızca iç ağ)
- **Karar:** Kullanıcı isteği ile "pasif" değil **aktif**. Yalnızca `KapsamModu.IcAg` hedeflerinde çalışır; `Kapsam.Izinli(DisAgPasif, ModulTuru.IcHttp)` zaten false olduğundan dış ağda KOD SEVİYESİNDE kilitlidir (testle kanıtlı). Dış ağda yalnızca mevcut pasif gözlem (PasifToplayici) kalır.
- **Sorumlu (pasif kontrollere EK olarak etkin sondalar):** `OPTIONS`/`TRACE`/`PUT`/`DELETE` yöntem yoklaması (yalnızca izin başlığı ve durum kodu; gövde gönderilmez, kaynak değiştirilmez: `PUT/DELETE` için ZARARSIZ tek bayt sahte yol ve yalnızca `OPTIONS` Allow okunur), CORS ön uç sondası (sahte `Origin` yansıtılıyor mu), bilinen hassas yol listesi (`/.git/HEAD`, `/.env`, `/admin`, `/server-status`, `/phpinfo.php`, yedek uzantıları; yalnızca varlık/durum kodu, içerik rapora kopyalanmaz), hata sayfası sürüm sızıntısı (var olmayan yol → 404 gövdesi), dizin listeleme tespiti, çerez/oturum bayrakları, kimlik doğrulama gerektiren yolda `WWW-Authenticate` türü, açık yönlendirme (yalnızca iç ağ içinde), önbellek başlıkları, mevcut `GirdiDogrulamaTesti` ile birleşik "Aktif Web Denetimi" raporu.
- **Sınırlar:** istek hızı sınırı (örn. 5 istek/sn), toplam istek tavanı (örn. 300), yalnızca GET/HEAD/OPTIONS (zarar verebilecek yöntemler SONDALANMAZ, yalnızca OPTIONS cevabında "izinli" yazıyorsa raporlanır), iç ağ dışına yönlendirme olursa durur, her istek denetim izine sayılır, kullanıcı onayı (iç ağ hedefi bile olsa "Aktif Web Denetimi başlasın mı?").
- **Arayüz:** `interface IHttpAuditor { Task<HttpObservation> ObserveAsync(Uri url, HttpProbeProfile profil, ExecutionPlace place, CancellationToken ct); }` · `static class HttpRules { IEnumerable<Finding> Evaluate(HttpObservation o, Asset a); }`. Kurallar `pasif_kurallar.json` motorunu (JSON kural şeması) YENİDEN KULLANIR; aktif sondalar yeni `facts` üretir (`probe:trace_enabled`, `probe:git_exposed`, `probe:cors_reflects_origin`...), böylece kural eklemek kod değil JSON işidir.
- **Dış ağdaki karşılığı:** değişmedi (Mod B = pasif).

### 2.6 `SoftwareInventoryCollector` + `CpeMapper`
- **Sorumlu:** `Uninstall` kayıt defteri anahtarlarından (HKLM 64/32, HKCU) yazılım envanteri; ad/yayıncı/sürüm → aday CPE (çevrimdışı sözlük + sürüm normalizasyonu), `cpe_confidence` ile.
- **Arayüz:** `interface ISoftwareInventory { IReadOnlyList<InstalledSoftware> Collect(); }` · `interface ICpeMapper { (string? Cpe, double Confidence) Map(InstalledSoftware s); }`

### 2.7 `VulnFeedStore` + `VulnMatcher`
- **Sorumlu:** NVD ve CISA KEV JSON'larını **çevrimdışı içe aktarmak**, yerel dizinde (SQLite veya sıkıştırılmış JSON) saklamak; CPE + sürüm aralığı eşleştirmesi; KEV varsa önem yükseltme (`impact_weight` +).
- **Arayüz:** `interface IVulnFeedStore { FeedInfo Info { get; } void Import(string nvdPath, string kevPath); IEnumerable<VulnMatch> Query(string cpe, string version); }` · `interface IVulnMatcher { IEnumerable<VulnMatch> Match(InstalledSoftware s); }`
- **Güvenlik:** İçe aktarılan dosyaların SHA-256'sı kaydedilir; kaynak ve tarih rapora yazılır.

### 2.8 `PeAnalyzer` + `DllSearchRiskAnalyzer`
- **Sorumlu:** PE başlığından `DllCharacteristics` (ASLR=DYNAMIC_BASE, HighEntropyVA, DEP=NX_COMPAT, CFG=GUARD_CF), x86'da SafeSEH; imza durumu (mevcut `ImzaDenetimi`/WinVerifyTrust); dosyanın bulunduğu klasörün ACL'i (Users/Everyone yazabiliyor mu), DLL arama sırası riski (uygulama klasöründe yazılabilir + bilinen `KnownDLLs` dışı), tırnaksız hizmet yolları.
- **Arayüz:** `interface IPeAnalyzer { PeReport Analyze(string path); }` · `interface IAclInspector { string[] WritableBy(string directory); }`

### 2.9 `FindingPipeline`, `VerificationService`, `RemediationEngine`, `AuditTrail`, `FalsePositiveTracker`
- **`FindingPipeline`:** ham gözlemi `Finding`'e çevirir: skor, KVKK/ISO/MITRE (mevcut `Uyumluluk` ve `Mitre`), `FindingSchema.Validate`; geçmeyen bulgu **kabul edilmez** ve denetim izine yazılır.
- **`VerificationService`:** `verification_check_id` **çoktan-bire** çalışır: `IVerificationCheck { string Id; Task<CheckRunResult> RunAsync(IReadOnlyList<Finding> referans, ...); }`. **Kural:** kontrol, kendisine bağlı **tüm** bulgular doğrulanmadıkça `Aggregate=Fail` döner (kısmi başarıda bile). Her bulgunun kendi sonucu `PerFinding`'de tutulur; kendi sonucu `Pass` olan bulgu `Verified` olur, `Fail` olanlar `Open`'a döner. Böylece "bir kontrol anında çok bulguyu doğrular" ve "tüm referanslar doğrulanmadıysa kontrol başarısız" birlikte sağlanır.
- **`RemediationEngine`:** akış: *planla → onay → yedek al → uygula → doğrula → kapat/geri al*. `IRemediationAction { Task<BackupResult> BackupAsync(); Task ApplyAsync(); Task RollbackAsync(BackupResult b); }`. Her adım `AuditTrail`'e yazılır; `requires_uac=true` olanlar tek UAC penceresiyle çalışır. Doğrulama başarısızsa `on_fail`'e göre yeniden aç veya geri al.
- **`AuditTrail`:** `audit_trail.jsonl`, zincirleme özet; bozulursa Olay Zaman Çizgisi'ne KRİTİK yazılır. `Verify()` zinciri doğrular.
- **`FalsePositiveTracker`:** kural başına `fp/total` oranı; oran eşiği aşarsa kural otomatik **"gözden geçir"** etiketi alır (kuralı kapatmaz, yalnızca puanı düşürür/uyarır).

### 2.10 Gözetimsiz Otomatik Politikası (`UnattendedPolicy`) — kullanıcı kararı
Kullanıcı bazı sınıflar için gözetimsiz otomatik onarım istedi. Bu, "sistemi değiştiren eylem her seferinde onaylı" ilkesini **bilinçli olarak gevşetir**; bu yüzden onay her olayda değil, **politika düzeyinde bir kez** alınır ve aşağıdaki çerçeveyle sınırlanır:

1. **Sınıf bazlı, varsayılan KAPALI.** Her sınıf ayrı anahtar: `unattended_classes = { "fw_block_bruteforce": {enabled, ttl, max_per_hour}, ... }`. Etkinleştirme yalnızca **Yönetici** rolüyle, kurulum şifresi + açık onay penceresi (varsayılan "Hayır") + denetim izi kaydıyla yapılır; her an tek tıkla kapatılır.
2. **Yalnızca geri alınabilir + yedekli + kapsamı dar sınıflar** uygundur (şema zaten `auto` için `rollback_supported=true` ve `backup_plan` şart koşar). Önerilen aday sınıflar:
   - `fw_block_bruteforce`: Radar'ın "Yüksek" saydığı genel IP'yi Windows Güvenlik Duvarı'nda **süreli** engelle (örn. 24 saat sonra kural kendiliğinden silinir); kendi/yerel/sensör adresleri ve özel ağ asla engellenmez (mevcut `EngelHatasi` kuralı), saatte en çok N yeni kural.
   - `reverify`: düzeltme sonrası otomatik yeniden doğrulama (sistemi değiştirmez).
   - `startup_disable_unsigned_highrisk`: AÇIK/İSTEĞE BAĞLI, varsayılan kapalı; yalnızca imzasız + yüksek riskli + yeni eklenen başlangıç öğesini devre dışı bırak (silme yok, `StartupApproved` önceki değer yedeklenir), Microsoft imzalılara dokunmaz.
   - **Gözetimsiz OLMAYACAKLAR (her zaman onaylı):** Acil İzolasyon, ağ kartı kapatma, süreç sonlandırma, hizmet durdurma, kayıt defteri güvenlik ayarı değişiklikleri, hosts/DNS değişikliği, dosya silme/karantina.
3. **UAC gerçeği:** Gözetimsiz değişiklik yönetici hakkı ister. UAC penceresi gözetimsiz çıkmaz; bu yüzden gözetimsiz sınıflar için **ayrı bir "Yükseltilmiş Yardımcı" servisi/zamanlanmış görev** gerekir (yalnızca bu sabit eylem listesini çalıştırır, serbest komut almaz, imzalı/özetli). Bu, saldırı yüzeyini büyüten bir parçadır: kurulum sırasında bir kez UAC ile kaydedilir, eylem listesi kod içinde sabittir, girdileri sıkı doğrulanır.
4. **Kontrol kutuları:** hız sınırı, toplam tavan, "kuru çalıştırma" (dry-run, yalnızca ne yapacağını yazar) varsayılan ilk 7 gün, yanlış alarm oranı eşiği aşan kural kendiliğinden gözetimsizlikten çıkar (`FalsePositiveTracker`), her eylem `AuditTrail`'e ve Olay Zaman Çizgisi'ne yazılır, başarısız doğrulamada otomatik geri alma.
5. **Test ilkesi (önceki kuralla uyumlu):** Geliştirme sırasında bu eylemler **kullanıcının makinesinde ASLA çalıştırılmaz**; yalnızca sahte (fake) eylem sınıfı ve örnek veriyle test edilir. Gerçek denemeyi kullanıcı kendi ortamında "kuru çalıştırma"dan başlayarak yapar.
6. Şema eki: `Remediation` kaydına `unattended_class` (string?) alanı eklenir; `FindingSchema.Validate` kuralı: `unattended_class != null` ⇒ `type=Auto`, `rollback_supported=true`, `backup_plan` var, `requires_uac` ise yükseltilmiş yardımcı kayıtlı olmalı.

### Mevcut kodla bağlantı noktaları
| Yeni | Mevcut |
|---|---|
| `Finding.ToBulgu()` | `Form1.Ekle`, `RaporUretici`, `Uyumluluk`, `Mitre` (aynen çalışır) |
| `RemediationEngine` | `KaliciDenetci.DevreDisiPlani`, `BruteForce.EngelZinciri`, `GuvenlikDuvari` (ilk üç `IRemediationAction`) |
| `AuditTrail` | `OlayDeposu` (özet satırlar), `Muhur` |
| `HttpPassiveAuditor` | `HttpDenetimi`, `TlsDenetimi`, `SensorAg` |
| `NmapNormalizer` | `SensorIstemci`, `SensorAjani` (yeni mod: `nmap`) |

## 3. Uygulama sırası (haftalık)

| Hafta | Teslim | Çıkış ölçütü |
|---|---|---|
| 1 | **[TAMAM 2026-11-18, FindingV2.cs, 24 test]** `Finding` şeması + `FindingSchema` + `AuditTrail` + skor + `ToBulgu` bağdaştırıcısı | Mevcut tarama sonuçları şemaya sorunsuz dönüşür; eski ekran/rapor aynen çalışır |
| 2 | **[TAMAM 2026-11-18, RemediationV2.cs + EylemlerV2.cs, 39 test: motor, doğrulama, FP takipçisi, UnattendedPolicy kuru çalıştırma; IP engeli/port engeli/başlangıç öğesi IRemediationAction olarak bağlandı (sahte sistemle test; arayüz bağlantısı yok)]** `VerificationService` (çoktan-bire) + `RemediationEngine` iskeleti; 3 mevcut eylem (başlangıç öğesi, IP engeli, güvenlik duvarı kuralı) yeniden bağlanır | Geri alma ve doğrulama akışı uçtan uca; denetim izi zinciri doğrulanır |
| 3 | **[TAMAM 2026-11-18, NetworkMapV2.cs + SensorNmap.cs + AgHaritasiFormu.cs, 28 test; gerçek Kali'de nmap denenmedi, sahte nmap ile ajan doğrulandı]** Ajan `nmap` modu + `NmapXml.Parse` + `NetworkMap` modeli + minimal "Ağ Haritası" listesi | Kali'den nmap sonucu haritaya düşer |
| 4 | **[TAMAM 2026-11-18: YerelKaynakV2.cs, ServiceMapperV2.cs, YamaDenetimiV2.cs, YerelDenetimAkisi.cs + Ağ Haritası penceresinde "Bu Bilgisayarı Denetle" ve "KB Kataloğu Yükle"; 469 test; gerçek makinede salt-okunur denendi. Katalog dosyası elle hazırlanır: {"catalog_version","builds":{"<yapı>":[{"kb","released":"yyyy-MM-dd","severity":"Critical|Important|Moderate|Low","title"}]}}; gerçek bir KB kataloğu AKTARILMADI]** `ServiceMapper` (yerel) + `PatchAuditor` (yerel WMI) + KB kataloğu içe aktarma | Port→hizmet zinciri ve eksik KB bulguları |
| 5 | **[TAMAM 2026-11-19: AktifHttp.cs + aktif_kurallar.json (16 kural), YanlisAlarmDeposu.cs (kalıcı FP + UnattendedPolicy bağlantısı), VulnFeedV2.cs (KEV onaylı indirme/doğrulama), BulguSonucFormu.cs; 515 test; aktif denetim gerçek HTTP yığınıyla yerel Python sunucusuna denendi. NVD İNDİRİLMİYOR (yalnızca KEV); gerçek CISA indirmesi canlı denenmedi (sahte sunucu testi)]** `HttpActiveAuditor` (iç ağ, aktif sondalar + JSON kurallar) + `FalsePositiveTracker` + onaylı NVD/KEV "İndir" | Kural başına yanlış alarm oranı raporda; dış ağda aktif HTTP kilitli (test) |
| 5b | Uzak WMI (`ICredentialSession`, bellekte kimlik) + `UnattendedPolicy` iskeleti (yalnızca kuru çalıştırma) | Uzak makinede KB/hizmet okunur; gözetimsiz sınıflar kapalı ve kuru çalıştırmada |
| 6 | **[TAMAM 2026-11-19: YazilimEnvanteriV2.cs (envanter+CpeMapper+SurumKiyasi), NvdVeriV2.cs (NVD 2.0 JSON çevrimdışı içe aktarma, atomik, birleştirir), VulnMatcherV2.cs (güven puanlı), cpe_esleme.json (27 satırlık BAŞLANGIÇ tablosu, kullanıcı dosyasıyla genişler); 551 test; gerçek envanter salt-okunur denendi (112 yazılım, 7'si yüksek güvenli eşleme). NVD verisi UYGULAMA TARAFINDAN İNDİRİLMEZ: kullanıcı kendi dosyasını yükler (envanter NVD'ye gönderilmesin diye); gerçek NVD dosyasıyla denenmedi]** `SoftwareInventoryCollector` + `CpeMapper` + `VulnFeedStore` (çevrimdışı NVD/KEV) + `VulnMatcher` | KEV'li ve sürüm aralığı eşleşen yazılım bulguları, güven puanlı |
| 7 | **[TAMAM 2026-11-19: PeAnalyzerV2.cs (PE başlık okuma, DLL arama riski, PE-DLLRISK-001, PE-MITIGATION-001), IYerelKaynak.PeOku/DosyaYazilabilirMi, imza önbelleği, yerel denetim akışına bağlandı; 575 test (sentetik PE dahil); gerçek makinede salt-okunur denendi: 88 aday, 3 bulgu. Bilinen sınırlar: tam SafeSEH tablosu okunmaz, EXE manifestindeki "yönetici ister" bilgisi okunmaz, yalnızca hizmet ve dinleyen süreç dosyaları incelenir (başlangıç öğeleri değil)]** `PeAnalyzer` + ACL + DLL arama riski + tırnaksız yol | EXE bayrak raporu ve yazılabilir dizin bulguları |
| 8 | **[TAMAM 2026-11-19: RaporlamaV2.cs (V2Kayit köprüsü: Uyumluluk/Mitre/YonetimKuruluRaporu yeni bulguları tanır; DerinRapor HTML/JSON/PDF), FpDeposu kalibrasyonu (oranı yüksek kuralda puan %25 düşer, idempotent) + KuralKalitesi, Ağ Haritası'nda "Derin Denetim Raporu", "Ana Rapora Ekle", "Kural Kalitesi"; Pilot_Rehberi.md; 590 test. PİLOT YAPILMADI (yalnızca rehber hazır)]** Raporlara entegrasyon (Yönetim Kurulu özeti, KVKK/ISO), arayüz cilası, pilot ve yanlış alarm kalibrasyonu | Pilotta yanlış alarm oranı hedefin altında |

Her hafta sonunda: derleme 0 hata, tüm testler geçmeli; **mevcut tarama motoru testleri bozulmamalı**.

## 4. Test senaryoları (modül başına en az 5)

**Şema ve denetim izi**
1. `auto` bulguda `backup_plan` yok → `Validate` hata verir, bulgu reddedilir ve denetim izine yazılır.
2. Skor formülü: `impact=8, asset=0.6` → 48 (Orta); `asset=1` → 80 (Yüksek).
3. Denetim izi zincirinde bir satır elle değiştirilir → `Verify()` bozulmayı yakalar.
4. `requires_uac=true` ve `auto` bulgu, `on_fail` olmadan reddedilir.
5. `ToBulgu()` ile üretilen eski kayıt, mevcut rapor ve tabloda aynı görünür.

**`NmapNormalizer`**
1. Örnek XML (kapalı/filtreli/açık portlar) doğru normalize edilir.
2. CPE'siz servis `Cpe=[]` ile gelir, çökme olmaz.
3. Bozuk/kesik XML → anlaşılır hata, kısmi veri atılır.
4. İnternet adresi hedefi ajan ve Windows'ta reddedilir.
5. **Yanlış alarm:** `tcpwrapped`/filtreli port "açık servis bulgusu" üretmez.

**`ServiceMapper` / `PatchAuditor`**
1. Dinleyen yerel port doğru PID ve hizmete bağlanır.
2. Tırnaksız ve boşluklu hizmet yolu bulgu üretir; tırnaklı yol üretmez (**yanlış alarm kontrolü**).
3. Microsoft imzalı hizmet "imzasız hizmet" bulgusu üretmez.
4. Katalogda olmayan KB eksik sayılmaz; katalog sürümü raporda görünür.
5. **Doğrulama:** KB kurulduktan sonra yeniden çalıştırma bulguyu `Verified` yapar.

**`HttpPassiveAuditor`**
1. HSTS'siz https site Orta bulgu; HSTS'li site temiz.
2. `Access-Control-Allow-Origin: *` + çerezli yanıt bulgu üretir; herkese açık statik içerikte düşük puan (**yanlış alarm azaltma**).
3. Yönlendirme özel ağ dışına çıkarsa durur.
4. Yalnızca GET/HEAD gönderildiği (istek günlüğü) doğrulanır.
5. **Doğrulama:** başlık eklendikten sonra yeniden çalıştırma çoktan-bire kuralıyla ilgili tüm bulguları kapatır.

**`Yazılım envanteri → CPE → NVD/KEV`**
1. Bilinen KEV'li sürüm → Yüksek; aynı ürünün yamalı sürümü → bulgu yok.
2. **Yanlış alarm oranı:** 200 örnek kayıtlı envanterde (etiketli), `confidence<0.6` eşleşmeler Yüksek önem **almaz**; hedef: Yüksek bulgularda yanlış alarm ≤ %10.
3. Sürüm ayrıştırılamıyorsa bulgu üretilmez ("sürüm bilinmiyor" bilgi satırı).
4. Beslemeyi içe aktarma bozuk dosyada atomik başarısız olur (eski besleme korunur).
5. Besleme sürümü ve özet değeri rapora yazılır.

**`PeAnalyzer` / DLL arama riski**
1. ASLR/DEP/CFG bayrakları bilinen örnek ikililerde doğru okunur.
2. Yönetici-yazılabilir `Program Files` altındaki uygulama risk üretmez; `Users` yazılabilir uygulama klasörü üretir.
3. Microsoft imzalı sistem dosyası bayrak eksikliği için "Düşük/bilgi" olur (**yanlış alarm**).
4. Bozuk/PE olmayan dosya çökme yapmaz.
5. **Doğrulama:** ACL düzeltildikten sonra bulgu `Verified` olur.

**`RemediationEngine` (geri alma bütünlüğü)**
1. Başlangıç öğesi devre dışı → doğrulama geçer → kapatma; sonra geri alma önceki duruma **bayt bayt** döner.
2. Doğrulama başarısız → `reopen_and_rollback` sistemi eski hâline döndürür.
3. Yedek alınamazsa (disk dolu/yetki) uygulama **başlamaz**.
4. UAC reddedilirse hiçbir değişiklik yapılmaz, denetim izi bunu kaydeder.
5. **Çoktan-bire:** 5 bulgu aynı `verification_check_id`'yi kullanır; 4'ü düzelir, 1'i düzelmez → kontrol `Aggregate=Fail`, 4 bulgu `Verified`, 1 bulgu `Open` kalır.

## 5. Açık sorular — CEVAPLANDI (2026-11-18)
1. NVD/KEV: **onaylı "İndir" düğmesi olacak** (elle yükleme de kalır).
2. `auto`: **bazı sınıflar gözetimsiz otomatik** (çerçeve §2.10; aday sınıflar yönetici tarafından tek tek açılır).
3. Uzak WMI: **gerekli** (kimlik bilgisi bellekte, diske yazılmaz).
4. "Stajyer": **yok sayıldı.** HTTP modülü **aktif** olacak (yalnızca iç ağ; dış ağ pasif kalır).

### Yeni, kısa netleştirme gereken noktalar (varsayılanlarla ilerlenir, itiraz edilirse değişir)
- §2.10'daki aday gözetimsiz sınıflar: varsayılan olarak yalnızca `fw_block_bruteforce` (süreli) ve `reverify` önerildi; başlangıç öğesi devre dışı bırakma isteğe bağlı/kapalı. Başka sınıf isterseniz ekleriz.
- Gözetimsiz eylemler için "Yükseltilmiş Yardımcı" (kurulumda bir kez UAC) gereklidir; bunu kabul etmezseniz gözetimsiz sınıflar yalnızca **UAC gerektirmeyen** işlerle sınırlanır (örn. yeniden doğrulama, kullanıcı hakları içinde kalan değişiklikler).
