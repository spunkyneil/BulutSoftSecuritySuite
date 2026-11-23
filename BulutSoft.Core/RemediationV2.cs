// Bu dosya Hafta 2'nin teslimidir: doğrulama servisi (çoktan-bire), onarım motoru, yanlış alarm takibi ve gözetimsiz otomatik politikası.
// ÖNEMLİ: Burada GERÇEK sistem değiştiren hiçbir eylem YOKTUR. Motor yalnızca IRemediationAction arayüzünü çağırır;
// gerçek eylemler (güvenlik duvarı, başlangıç öğesi) ayrı bağlanacak ve her zaman onay penceresinden geçecektir.
using System.Text.Json;

// V2 ad alanı.
namespace LabDenetim.V2;

// ---------- Eylem ve doğrulama arayüzleri ----------
// Yedek alma sonucu.
public sealed record BackupResult(bool Ok, string Location, string Detail);

// Sistemi değiştirebilen bir eylem: yedekle, uygula, geri al.
public interface IRemediationAction
{
    // Eylemin kimliği (denetim izinde görünür).
    string Id { get; }
    // Değişiklikten önce yedek alır.
    Task<BackupResult> BackupAsync(CancellationToken ct);
    // Değişikliği uygular.
    Task ApplyAsync(CancellationToken ct);
    // Yedekten geri alır.
    Task RollbackAsync(BackupResult yedek, CancellationToken ct);
}

// Bir doğrulama kontrolü: kendisine bağlı bulguları yeniden denetler, bulgu başına sonuç döndürür.
public interface IVerificationCheck
{
    // Kontrol kimliği (Remediation.VerificationCheckId ile eşleşir).
    string Id { get; }
    // Referans bulguları kontrol eder; anahtar = bulgu kimliği.
    Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct);
}

// ---------- Doğrulama servisi ----------
// Kontrolleri kayıtlı tutar ve çoktan-bire kuralıyla çalıştırır.
public sealed class VerificationService
{
    // Kayıtlı kontroller (kimliğe göre).
    private readonly Dictionary<string, IVerificationCheck> _kontroller = new(StringComparer.Ordinal);
    // Kontrol ekler.
    public void Kaydet(IVerificationCheck k) => _kontroller[k.Id] = k;

    // Kontrolü, kendisine bağlı TÜM bulgular için çalıştırır. Eksik sonuç veya hata "Error" sayılır (sessizce geçmez).
    public async Task<CheckRunResult> CalistirAsync(string checkId, IReadOnlyList<Finding> referans, CancellationToken ct, DateTime? zaman = null)
    {
        // Zaman.
        var t = zaman ?? DateTime.UtcNow;
        // Bilinmeyen kontrol: hepsi Error.
        if (!_kontroller.TryGetValue(checkId, out var k)) return Sonuc(checkId, t, referans, null);
        // Çalıştır; hata olursa hepsi Error.
        try { var s = await k.RunAsync(referans, ct); return Sonuc(checkId, t, referans, s); }
        catch (OperationCanceledException) { throw; }
        catch { return Sonuc(checkId, t, referans, null); }
    }

    // Sonuçları bulgu başına birleştirir (kontrolün bildirmediği bulgu = Error).
    private static CheckRunResult Sonuc(string id, DateTime t, IReadOnlyList<Finding> referans, IReadOnlyDictionary<string, VerificationOutcome>? s)
    {
        var d = new Dictionary<string, VerificationOutcome>();
        foreach (var f in referans) d[f.Id] = s != null && s.TryGetValue(f.Id, out var o) ? o : VerificationOutcome.Error;
        return new CheckRunResult(id, t, CheckRunResult.Birlestir(d.Values), d);
    }

    // Sonuca göre bulgu durumlarını günceller: Pass → Verified, Fail → Open, diğerleri değişmez.
    public static List<Finding> DurumUygula(IReadOnlyList<Finding> bulgular, CheckRunResult r, DateTime simdi)
        => bulgular.Select(f => r.PerFinding.TryGetValue(f.Id, out var o)
            ? o == VerificationOutcome.Pass ? f with { State = FindingState.Verified, LastSeen = simdi }
            : o == VerificationOutcome.Fail ? f with { State = FindingState.Open, LastSeen = simdi } : f
            : f).ToList();
}

// ---------- Yanlış alarm takibi ----------
// Kural başına yanlış alarm oranını tutar; eşik aşılırsa kural "gözden geçir" olur (kapatılmaz).
public sealed class FalsePositiveTracker
{
    // Kural → (toplam, yanlış alarm).
    private readonly Dictionary<string, (int Toplam, int Fp)> _sayac = new();
    // Eşik oranı ve en az örnek sayısı (az örnekle karar verilmez).
    public double Esik { get; }
    public int EnAzOrnek { get; }
    // Oluşturur.
    public FalsePositiveTracker(double esik = 0.3, int enAzOrnek = 10) { Esik = esik; EnAzOrnek = enAzOrnek; }
    // Kalıcı depodan sayıları doğrudan ayarlar.
    public void Ayarla(string ruleId, int toplam, int fp) => _sayac[ruleId] = (toplam, fp);
    // Bulgu üretildi.
    public void Bulgu(string ruleId) { _sayac.TryGetValue(ruleId, out var s); _sayac[ruleId] = (s.Toplam + 1, s.Fp); }
    // Yanlış alarm işaretlendi.
    public void YanlisAlarm(string ruleId) { _sayac.TryGetValue(ruleId, out var s); _sayac[ruleId] = (Math.Max(s.Toplam, s.Fp + 1), s.Fp + 1); }
    // Sayılar.
    public (int Toplam, int Fp) Say(string ruleId) => _sayac.TryGetValue(ruleId, out var s) ? s : (0, 0);
    // Oran (0-1).
    public double Oran(string ruleId) { var (t, f) = Say(ruleId); return t == 0 ? 0 : (double)f / t; }
    // Kural gözden geçirilmeli mi?
    public bool GozdenGecir(string ruleId) { var (t, _) = Say(ruleId); return t >= EnAzOrnek && Oran(ruleId) >= Esik; }
}

// ---------- Gözetimsiz otomatik politikası ----------
// Gözetimsiz kararın sonucu.
public enum GozetimsizKarar { Kapali, KuruCalistirma, Izinli, HizSiniri, GozdenGecirmeGerekli, YardimciYok, BilinmeyenSinif }

// Tek sınıfın ayarı.
public sealed record GozetimsizSinif(bool Etkin, DateTime EtkinlestirmeZamani, int SaatteEnCok, int OmurSaat);

// Politika: hangi sınıflar, ne zaman, hangi sınırla gözetimsiz çalışabilir.
public sealed class UnattendedPolicy
{
    // İzin verilebilecek sınıfların SABİT listesi (başka ad etkinleştirilemez).
    public static readonly string[] IzinliSiniflar = { "fw_block_bruteforce", "reverify", "startup_disable_unsigned_highrisk" };
    // İlk günlerde yalnızca "ne yapacağını yaz" (kuru çalıştırma) süresi.
    public const int KuruGun = 7;
    // Sınıf ayarları.
    private readonly Dictionary<string, GozetimsizSinif> _sinif = new();
    // Sınıf başına eylem zamanları (hız sınırı için).
    private readonly Dictionary<string, List<DateTime>> _kullanim = new();
    // Yükseltilmiş yardımcı kayıtlı mı (UAC'li gözetimsiz işler için şart). BU SÜRÜMDE yardımcının kurulumu YOKTUR: değer yalnızca testlerde elle verilir, dosyaya yazılmaz.
    public bool YardimciKayitli { get; set; }
    // Yanlış alarm takipçisi (isteğe bağlı).
    private readonly FalsePositiveTracker? _fp;
    // Denetim izi (isteğe bağlı).
    private readonly AuditTrail? _iz;

    // Oluşturur.
    public UnattendedPolicy(FalsePositiveTracker? fp = null, AuditTrail? iz = null) { _fp = fp; _iz = iz; }

    // ---- Kalıcılık (arayüz için) ----
    // Açıksa her değişiklik dosyaya yazılır (Yukle ile açılır).
    public bool Kalici { get; private set; }
    // Dosya okunurken sorun çıktıysa (mühür bozuk vb.) nedeni; politika bu durumda GÜVENLİ varsayılana (hepsi kapalı) döner.
    public string? YukleUyarisi { get; private set; }
    public static string Yol => BulutKlasor.Dosya("gozetimsiz_politika.json");
    private sealed class KayitDosyasi { public Dictionary<string, GozetimsizSinif> Siniflar { get; set; } = new(); public Dictionary<string, List<DateTime>> Kullanim { get; set; } = new(); }
    private static readonly System.Text.Json.JsonSerializerOptions Sec = new() { WriteIndented = true };

    // Kalıcı politikayı yükler. Dosya yoksa boş (hepsi kapalı); mühür doğrulanamazsa HEPSİ KAPALI başlar ve uyarı verilir (dosyayla oynanmış olabilir).
    public static UnattendedPolicy Yukle(FalsePositiveTracker? fp = null, AuditTrail? iz = null)
    {
        var p = new UnattendedPolicy(fp, iz) { Kalici = true };
        try
        {
            if (!File.Exists(Yol)) return p;
            if (!Butunluk.GuvenliMi(Yol)) { p.YukleUyarisi = "Politika dosyasının mührü doğrulanamadı (dosya değiştirilmiş olabilir). Güvenlik için tüm gözetimsiz sınıflar KAPALI başlatıldı."; return p; }
            var k = System.Text.Json.JsonSerializer.Deserialize<KayitDosyasi>(File.ReadAllText(Yol), Sec) ?? new();
            foreach (var (ad, s) in k.Siniflar) if (IzinliSiniflar.Contains(ad) && s.SaatteEnCok >= 1 && s.OmurSaat >= 1) p._sinif[ad] = s;
            foreach (var (ad, l) in k.Kullanim) if (IzinliSiniflar.Contains(ad)) p._kullanim[ad] = l.TakeLast(1000).ToList();
        }
        catch (Exception ex) { p._sinif.Clear(); p._kullanim.Clear(); p.YukleUyarisi = "Politika dosyası okunamadı; tüm sınıflar KAPALI başlatıldı: " + ex.Message; }
        return p;
    }

    // Atomik yazar ve mühürler.
    public void Kaydet()
    {
        Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, System.Text.Json.JsonSerializer.Serialize(new KayitDosyasi { Siniflar = _sinif.ToDictionary(x => x.Key, x => x.Value), Kullanim = _kullanim.ToDictionary(x => x.Key, x => x.Value.TakeLast(1000).ToList()) }, Sec)); Butunluk.Muhurla(Yol);
    }
    private void KalicıysaYaz() { if (Kalici) Kaydet(); }

    // Arayüz yardımcıları.
    public IReadOnlyDictionary<string, GozetimsizSinif> Tum => _sinif;
    // Kuru çalıştırma döneminden kaç gün kaldı (dönem bittiyse 0).
    public int KuruKalanGun(string sinif, DateTime simdi) => _sinif.TryGetValue(sinif, out var a) ? Math.Max(0, (int)Math.Ceiling((a.EtkinlestirmeZamani.AddDays(KuruGun) - simdi).TotalDays)) : 0;
    // Son 1 saatteki gözetimsiz eylem sayısı.
    public int SonSaatKullanim(string sinif, DateTime simdi) => _kullanim.TryGetValue(sinif, out var l) ? l.Count(x => x > simdi.AddHours(-1)) : 0;
    // Sınıf UAC (yönetici) gerektiren eylem içerir mi? (yükseltilmiş yardımcı gerekir)
    public static bool UacGerekir(string sinif) => sinif != "reverify";
    // Sınıfın sade açıklaması (arayüz).
    public static (string Ad, string Aciklama) Bilgi(string sinif) => sinif switch
    {
        "fw_block_bruteforce" => ("Saldırgan IP'yi süreli engelle", "Giriş Saldırı Radarı'nın \"Yüksek\" saydığı GENEL bir IP'yi güvenlik duvarında süreli engeller. Kendi/sensör adresleri ve özel ağ asla engellenmez."),
        "reverify" => ("Düzeltme sonrası otomatik yeniden doğrulama", "Bir düzeltme yapıldıktan sonra bulgunun gerçekten kapandığını kendiliğinden yeniden kontrol eder. Sistemi DEĞİŞTİRMEZ."),
        "startup_disable_unsigned_highrisk" => ("İmzasız yüksek riskli başlangıç öğesini devre dışı bırak", "Yalnızca imzasız + yüksek riskli + yeni eklenen başlangıç öğesini devre dışı bırakır (silmez, geri alınır). Microsoft imzalılara dokunmaz. VARSAYILAN KAPALI, isteğe bağlı."),
        _ => (sinif, "")
    };

    // Sınıfı etkinleştirir. Yalnızca Yönetici; bilinmeyen sınıf reddedilir. Her değişiklik denetim izine yazılır.
    public void Etkinlestir(string sinif, string actor, bool yoneticiMi, DateTime simdi, int saatteEnCok = 5, int omurSaat = 24)
    {
        if (!yoneticiMi) throw new UnauthorizedAccessException("Gözetimsiz sınıfı yalnızca Yönetici etkinleştirebilir.");
        if (!IzinliSiniflar.Contains(sinif)) throw new ArgumentException("Bu sınıf gözetimsiz çalışamaz: " + sinif);
        if (saatteEnCok < 1 || omurSaat < 1) throw new ArgumentException("Sınırlar en az 1 olmalı.");
        _sinif[sinif] = new GozetimsizSinif(true, simdi, saatteEnCok, omurSaat);
        _iz?.Ekle(actor, "policy_changed", null, null, false, "etkinleştirildi: " + sinif + $" (saatte {saatteEnCok}, ömür {omurSaat} sa)", simdi); KalicıysaYaz();
    }

    // Sınıfı kapatır (her an, herkes tarafından yapılabilir: güvenli yön).
    public void Kapat(string sinif, string actor, DateTime simdi)
    {
        if (_sinif.Remove(sinif)) { _iz?.Ekle(actor, "policy_changed", null, null, false, "kapatıldı: " + sinif, simdi); KalicıysaYaz(); }
    }

    // Ayar (yoksa null).
    public GozetimsizSinif? Ayar(string sinif) => _sinif.TryGetValue(sinif, out var a) ? a : null;

    // Bu sınıf şimdi gözetimsiz çalışabilir mi? (Hız sayacını ARTIRMAZ.)
    public GozetimsizKarar Karar(string sinif, string ruleId, bool requiresUac, DateTime simdi)
    {
        if (!IzinliSiniflar.Contains(sinif)) return GozetimsizKarar.BilinmeyenSinif;
        if (!_sinif.TryGetValue(sinif, out var a) || !a.Etkin) return GozetimsizKarar.Kapali;
        if (requiresUac && !YardimciKayitli) return GozetimsizKarar.YardimciYok;
        if (_fp != null && _fp.GozdenGecir(ruleId)) return GozetimsizKarar.GozdenGecirmeGerekli;
        if (simdi < a.EtkinlestirmeZamani.AddDays(KuruGun)) return GozetimsizKarar.KuruCalistirma;
        int son = _kullanim.TryGetValue(sinif, out var l) ? l.Count(x => x > simdi.AddHours(-1)) : 0;
        return son >= a.SaatteEnCok ? GozetimsizKarar.HizSiniri : GozetimsizKarar.Izinli;
    }

    // Gerçek bir gözetimsiz eylem yapıldığında sayacı artırır.
    public void KullanimKaydet(string sinif, DateTime simdi) { if (!_kullanim.TryGetValue(sinif, out var l)) _kullanim[sinif] = l = new(); l.Add(simdi); KalicıysaYaz(); }
}

// ---------- Onarım motoru ----------
// Bir onarımın sonucu.
public enum OnarimSonucu { GecersizBulgu, ElleDuzeltme, Reddedildi, Engellendi, KuruCalistirma, YedekHatasi, UygulamaHatasi, Dogrulandi, DogrulamaBasarisizAcildi, DogrulamaBasarisizGeriAlindi }

// Sonuç ayrıntısı: yeni bulgu durumları (referans bulgular dahil) ve açıklama.
public sealed record OnarimRapor(OnarimSonucu Sonuc, List<Finding> Bulgular, string Aciklama);

// Akış: planla → onay → yedek al → uygula → doğrula → kapat / yeniden aç / geri al. Her adım denetim izine yazılır.
public sealed class RemediationEngine
{
    // Bağımlılıklar.
    private readonly AuditTrail _iz; private readonly VerificationService _dogrulama; private readonly UnattendedPolicy? _politika;
    // Bekleme (testte anında dönen sahte ile değiştirilir).
    private readonly Func<TimeSpan, CancellationToken, Task> _bekle;
    // Saat (testte sabitlenir).
    private readonly Func<DateTime> _simdi;

    // Oluşturur.
    public RemediationEngine(AuditTrail iz, VerificationService dogrulama, UnattendedPolicy? politika = null, Func<TimeSpan, CancellationToken, Task>? bekle = null, Func<DateTime>? simdi = null)
    { _iz = iz; _dogrulama = dogrulama; _politika = politika; _bekle = bekle ?? Task.Delay; _simdi = simdi ?? (() => DateTime.UtcNow); }

    // Onarımı yürütür. onay = kullanıcıya gösterilen plan metnini onaylatır (gözetimsizde çağrılmaz).
    // referans = aynı doğrulama kontrolüne bağlı TÜM bulgular (hedef bulgu da içinde olmalı).
    public async Task<OnarimRapor> YurutAsync(Finding hedef, IReadOnlyList<Finding> referans, IRemediationAction eylem, Func<string, Task<bool>>? onay, string actor, CancellationToken ct, bool kuruCalistirma = false, bool gozetimsiz = false)
    {
        // Hedef referans listesinde yoksa eklenir.
        var refl = referans.Any(x => x.Id == hedef.Id) ? referans.ToList() : referans.Append(hedef).ToList();
        // Şema geçerli mi? (UAC'li gözetimsizde yardımcı kaydı politikadan gelir.)
        var hata = FindingSchema.Validate(hedef, _politika?.YardimciKayitli ?? false);
        if (hata.Count > 0) { _iz.Ekle(actor, "blocked", hedef.Id, null, false, "şema geçersiz: " + string.Join("; ", hata), _simdi()); return new(OnarimSonucu.GecersizBulgu, refl, string.Join("; ", hata)); }
        var r = hedef.Remediation; string chk = r.VerificationCheckId;
        // Elle düzeltmeler motorla yürütülmez.
        if (r.Type == RemediationType.Manual) { _iz.Ekle(actor, "blocked", hedef.Id, chk, false, "manuel düzeltme motorla yürütülmez", _simdi()); return new(OnarimSonucu.ElleDuzeltme, refl, "Bu düzeltme elle yapılır."); }
        // Planı denetim izine yaz.
        string plan = $"{r.Summary} | komut: {r.Command} | UAC: {(r.RequiresUac ? "evet" : "hayır")} | geri alma: {(r.RollbackSupported ? "var" : "yok")} | eylem: {eylem.Id}";
        _iz.Ekle(actor, "remediation_planned", hedef.Id, chk, r.RequiresUac, plan, _simdi());
        // Kuru çalıştırma: hiçbir şey yapılmaz.
        if (kuruCalistirma) { _iz.Ekle(actor, "dry_run", hedef.Id, chk, r.RequiresUac, "yalnızca plan yazıldı", _simdi()); return new(OnarimSonucu.KuruCalistirma, refl, plan); }
        // Onay: gözetimsizde politika, değilse kullanıcı.
        if (gozetimsiz)
        {
            // Sınıf yoksa gözetimsiz çalışamaz.
            if (r.UnattendedClass == null || _politika == null) { _iz.Ekle(actor, "blocked", hedef.Id, chk, r.RequiresUac, "gözetimsiz sınıf yok", _simdi()); return new(OnarimSonucu.Engellendi, refl, "Bu bulgu gözetimsiz çalışamaz."); }
            var k = _politika.Karar(r.UnattendedClass, hedef.RuleId, r.RequiresUac, _simdi());
            if (k == GozetimsizKarar.KuruCalistirma) { _iz.Ekle(actor, "dry_run", hedef.Id, chk, r.RequiresUac, "gözetimsiz kuru çalıştırma dönemi", _simdi()); return new(OnarimSonucu.KuruCalistirma, refl, plan); }
            if (k != GozetimsizKarar.Izinli) { _iz.Ekle(actor, "blocked", hedef.Id, chk, r.RequiresUac, "politika: " + k, _simdi()); return new(OnarimSonucu.Engellendi, refl, "Politika izin vermedi: " + k); }
            _politika.KullanimKaydet(r.UnattendedClass, _simdi());
            _iz.Ekle("policy:" + r.UnattendedClass, "approved", hedef.Id, chk, r.RequiresUac, "gözetimsiz politika onayı", _simdi());
        }
        else
        {
            // Onay yöntemi yoksa veya "Hayır" derse hiçbir şey yapılmaz.
            bool evet = onay != null && await onay(plan);
            if (!evet) { _iz.Ekle(actor, "rejected", hedef.Id, chk, r.RequiresUac, "kullanıcı onaylamadı", _simdi()); return new(OnarimSonucu.Reddedildi, refl, "Onaylanmadı."); }
            _iz.Ekle(actor, "approved", hedef.Id, chk, r.RequiresUac, "kullanıcı onayı", _simdi());
        }
        // Yedek: başarısızsa HİÇBİR değişiklik yapılmaz.
        BackupResult yedek;
        try { yedek = await eylem.BackupAsync(ct); } catch (OperationCanceledException) { throw; } catch (Exception ex) { yedek = new(false, "", ex.Message); }
        if (!yedek.Ok) { _iz.Ekle(actor, "failed", hedef.Id, chk, r.RequiresUac, "yedek alınamadı: " + yedek.Detail, _simdi()); return new(OnarimSonucu.YedekHatasi, refl, "Yedek alınamadı, değişiklik yapılmadı."); }
        _iz.Ekle(actor, "backup_taken", hedef.Id, chk, r.RequiresUac, yedek.Location, _simdi());
        // Uygula: hata olursa geri alınır.
        try { await eylem.ApplyAsync(ct); }
        // Hiçbir şey değişmediyse (örn. UAC reddedildi) geri alma denenmez.
        catch (DegisiklikYapilmadiException ex)
        {
            _iz.Ekle(actor, "failed", hedef.Id, chk, r.RequiresUac, "değişiklik yapılmadı: " + ex.Message, _simdi());
            return new(OnarimSonucu.UygulamaHatasi, refl, "Değişiklik yapılmadı: " + ex.Message);
        }
        catch (OperationCanceledException) { await GeriAl(eylem, yedek, hedef, chk, actor, "iptal"); throw; }
        catch (Exception ex)
        {
            _iz.Ekle(actor, "failed", hedef.Id, chk, r.RequiresUac, "uygulama hatası: " + ex.Message, _simdi());
            await GeriAl(eylem, yedek, hedef, chk, actor, "uygulama hatasından sonra");
            return new(OnarimSonucu.UygulamaHatasi, refl, "Uygulama hatası, geri alındı.");
        }
        _iz.Ekle(actor, "applied", hedef.Id, chk, r.RequiresUac, r.Command ?? "", _simdi());
        // Durumu "Düzeltildi" yap.
        var guncel = refl.Select(f => f.Id == hedef.Id ? f with { State = FindingState.Remediated, LastSeen = _simdi() } : f).ToList();
        // Doğrulama: beklemeden sonra, en çok MaxRetries (en az 1) deneme.
        var vp = r.VerificationPolicy; int deneme = Math.Max(1, vp.MaxRetries); CheckRunResult? sonuc = null;
        for (int i = 0; i < deneme; i++)
        {
            if (vp.RerunAfterSeconds > 0) await _bekle(TimeSpan.FromSeconds(vp.RerunAfterSeconds), ct);
            sonuc = await _dogrulama.CalistirAsync(chk, guncel, ct, _simdi());
            _iz.Ekle(actor, "verify_run", hedef.Id, chk, false, $"deneme {i + 1}/{deneme}: {sonuc.Aggregate}", _simdi());
            if (sonuc.Aggregate == VerificationOutcome.Pass) break;
        }
        // Hedef bulgu geçtiyse kapatılır (diğer referans bulgular kendi sonucuna göre güncellenir).
        guncel = VerificationService.DurumUygula(guncel, sonuc!, _simdi());
        bool hedefGecti = sonuc!.PerFinding.TryGetValue(hedef.Id, out var ho) && ho == VerificationOutcome.Pass;
        if (hedefGecti)
        {
            _iz.Ekle(actor, "verified", hedef.Id, chk, false, "doğrulandı", _simdi());
            guncel = guncel.Select(f => f.Id == hedef.Id ? f with { State = FindingState.Closed } : f).ToList();
            _iz.Ekle(actor, "closed", hedef.Id, chk, false, "kapatıldı", _simdi());
            return new(OnarimSonucu.Dogrulandi, guncel, "Doğrulandı ve kapatıldı.");
        }
        // Geçmediyse bulgu yeniden açılır (Error/NotRun'da da açık kalır).
        guncel = guncel.Select(f => f.Id == hedef.Id ? f with { State = FindingState.Open } : f).ToList();
        if (vp.OnFail == "reopen_and_rollback")
        {
            await GeriAl(eylem, yedek, hedef, chk, actor, "doğrulama başarısız");
            return new(OnarimSonucu.DogrulamaBasarisizGeriAlindi, guncel, "Doğrulama başarısız: geri alındı ve yeniden açıldı.");
        }
        return new(OnarimSonucu.DogrulamaBasarisizAcildi, guncel, "Doğrulama başarısız: bulgu yeniden açıldı.");
    }

    // Geri alır ve izine yazar; geri alma da başarısız olursa bunu açıkça kaydeder.
    private async Task GeriAl(IRemediationAction eylem, BackupResult yedek, Finding f, string chk, string actor, string neden)
    {
        try { await eylem.RollbackAsync(yedek, CancellationToken.None); _iz.Ekle(actor, "rolled_back", f.Id, chk, f.Remediation.RequiresUac, "geri alındı (" + neden + ")", _simdi()); }
        catch (Exception ex) { _iz.Ekle(actor, "failed", f.Id, chk, f.Remediation.RequiresUac, "GERİ ALMA BAŞARISIZ (elle müdahale gerekir): " + ex.Message, _simdi()); }
    }
}
