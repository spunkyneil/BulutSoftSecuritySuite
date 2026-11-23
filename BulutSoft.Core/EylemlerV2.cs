// Bu dosya mevcut üç gerçek eylemi (IP engeli, port engeli, başlangıç öğesini devre dışı bırakma) onarım motoruna (IRemediationAction) bağlar.
// Eski kodun kendi güvenlik kontrolleri (korunan adresler, güvenli kural adı, Microsoft imzalılara dokunmama) AYNEN kullanılır.
// Sistemi değiştiren her iş tek bir arayüzden (ISistemCalistirici) geçer: gerçeği UAC ister, testlerde sahtesi kullanılır.
// Bu dosyadaki kod hiçbir yerden OTOMATİK çağrılmaz; çağıran taraf onay penceresini göstermek zorundadır (motor onay olmadan çalıştırmaz).
using System.Text.RegularExpressions;

// V2 ad alanı.
namespace LabDenetim.V2;

// Hiçbir değişiklik yapılmadan eylem başarısız olduysa fırlatılır (örn. kullanıcı UAC'yi reddetti): motor geri alma denemez.
public sealed class DegisiklikYapilmadiException : Exception { public DegisiklikYapilmadiException(string m) : base(m) { } }

// Sistemi değiştiren/okuyan işlerin tek kapısı.
internal interface ISistemCalistirici
{
    // Komut zincirini yönetici izniyle (UAC) çalıştırır; (başarılı mı, hata metni).
    Task<(bool Ok, string Hata)> YoneticiAsync(string zincir);
    // HKCU başlangıç onay değerini doğrudan yazar (yönetici gerekmez).
    void HkcuYaz(KaliciOge o, bool devreDisi);
    // Şu an var olan BulutSoft güvenlik duvarı kural adları.
    HashSet<string> KuralAdlari();
    // Devre dışı bırakılan öğeyi yedek dosyasına yazar.
    void YedekYaz(KaliciOge o);
}

// Gerçek uygulama: mevcut sınıfları çağırır (ÇAĞIRAN onaylatmıştır).
internal sealed class GercekSistemCalistirici : ISistemCalistirici
{
    public async Task<(bool Ok, string Hata)> YoneticiAsync(string zincir) { var (b, h) = await GuvenlikDuvari.YoneticiIleCalistirAsync(zincir); return (b, h); }
    public void HkcuYaz(KaliciOge o, bool devreDisi) => KaliciDenetci.HkcuYaz(o, devreDisi);
    public HashSet<string> KuralAdlari() => GuvenlikDuvari.Listele().Select(k => k.Ad).Where(GuvenlikDuvari.AdGuvenliMi).ToHashSet();
    public void YedekYaz(KaliciOge o) => KaliciDenetci.YedekEkle(o);
}

// Ortak yardımcı: yönetici komutu çalıştır, başarısızsa "değişiklik yapılmadı" istisnası at.
internal static class EylemYardim
{
    public static async Task Calistir(ISistemCalistirici s, string zincir)
    {
        var (ok, hata) = await s.YoneticiAsync(zincir);
        if (!ok) throw new DegisiklikYapilmadiException(hata.Length > 0 ? hata : "Komut başlatılamadı.");
    }
}

// 1) Tek bir IP'yi güvenlik duvarında engeller (BulutSoft-BF-<ip> kuralı).
internal sealed class IpEngelEylemi : IRemediationAction
{
    private readonly string _ip; private readonly ISet<string> _korunan; private readonly ISistemCalistirici _s;
    public IpEngelEylemi(string ip, ISet<string> korunan, ISistemCalistirici s) { _ip = ip; _korunan = korunan; _s = s; }
    public string Id => "fw_block_ip:" + _ip;
    // Yedek: kuralın önceden var olup olmadığı kaydedilir (geri almada önceki duruma dönmek için).
    public Task<BackupResult> BackupAsync(CancellationToken ct)
    {
        // Eski koddaki koruma kuralı: kendi/sensör adresi vb. engellenemez.
        var hata = BruteForce.EngelHatasi(_ip, _korunan); if (hata != null) return Task.FromResult(new BackupResult(false, "", "Engellenemez: " + hata));
        bool vardi = _s.KuralAdlari().Contains(BruteForce.KuralAdi(_ip));
        return Task.FromResult(new BackupResult(true, "onceden_vardi=" + (vardi ? "1" : "0"), "kural " + BruteForce.KuralAdi(_ip) + (vardi ? " zaten vardı" : " yoktu")));
    }
    // Uygula: eski koddaki zincir (önce aynı adlıyı siler, sonra engel kuralını ekler).
    public async Task ApplyAsync(CancellationToken ct)
    {
        string z = BruteForce.EngelZinciri(new[] { _ip }, _korunan); if (z.Length == 0) throw new DegisiklikYapilmadiException("Engellenecek geçerli adres yok.");
        await EylemYardim.Calistir(_s, z);
    }
    // Geri al: kural önceden yoktuysa siler; vardıysa dokunmaz.
    public async Task RollbackAsync(BackupResult y, CancellationToken ct)
    {
        if (y.Location == "onceden_vardi=1") return;
        string z = GuvenlikDuvari.SilZinciri(new[] { BruteForce.KuralAdi(_ip) }); if (z.Length > 0) await EylemYardim.Calistir(_s, z);
    }
}

// 2) Verilen portları gelen yönde engeller (adOnek + port adlı kurallar).
internal sealed class PortEngelEylemi : IRemediationAction
{
    private readonly int[] _portlar; private readonly string _onek; private readonly ISistemCalistirici _s;
    public PortEngelEylemi(IEnumerable<int> portlar, string adOnek, ISistemCalistirici s) { _portlar = portlar.Distinct().ToArray(); _onek = adOnek; _s = s; }
    public string Id => "fw_block_port:" + string.Join(",", _portlar);
    public Task<BackupResult> BackupAsync(CancellationToken ct)
    {
        // Geçerlilik: port aralığı ve güvenli kural adı.
        if (_portlar.Length == 0 || _portlar.Any(p => p is < 1 or > 65535)) return Task.FromResult(new BackupResult(false, "", "Geçersiz port listesi."));
        if (_portlar.Any(p => !GuvenlikDuvari.AdGuvenliMi(_onek + p))) return Task.FromResult(new BackupResult(false, "", "Kural adı güvenli değil (BulutSoft- ile başlamalı)."));
        // Önceden var olan kurallar (geri almada dokunulmaz).
        var var_ = _s.KuralAdlari(); var once = _portlar.Where(p => var_.Contains(_onek + p)).ToArray();
        return Task.FromResult(new BackupResult(true, "onceden=" + string.Join(",", once), "mevcut kural sayısı: " + once.Length));
    }
    public async Task ApplyAsync(CancellationToken ct) => await EylemYardim.Calistir(_s, GuvenlikDuvari.EkleZinciri(_portlar, _onek));
    // Geri al: yalnızca bu eylemin YENİ eklediği kurallar silinir.
    public async Task RollbackAsync(BackupResult y, CancellationToken ct)
    {
        var once = (y.Location.StartsWith("onceden=", StringComparison.Ordinal) ? y.Location[8..] : "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
        string z = GuvenlikDuvari.SilZinciri(_portlar.Where(p => !once.Contains(p)).Select(p => _onek + p)); if (z.Length > 0) await EylemYardim.Calistir(_s, z);
    }
}

// 3) Başlangıç/kalıcılık öğesini devre dışı bırakır (silmez; geri alınabilir). Microsoft imzalılara dokunulmaz.
internal sealed class BaslangicDevreDisiEylemi : IRemediationAction
{
    private readonly KaliciOge _o; private readonly ISistemCalistirici _s;
    public BaslangicDevreDisiEylemi(KaliciOge o, ISistemCalistirici s) { _o = o; _s = s; }
    public string Id => "startup_disable:" + _o.Tur + ":" + _o.Ad;
    // Yedek: önce eski koddaki güvenlik planı denetlenir (ret varsa yedek de alınmaz), sonra öğe yedek dosyasına yazılır.
    public Task<BackupResult> BackupAsync(CancellationToken ct)
    {
        var plan = KaliciDenetci.DevreDisiPlani(_o, true); if (plan.Ret != null) return Task.FromResult(new BackupResult(false, "", plan.Ret));
        try { _s.YedekYaz(_o); return Task.FromResult(new BackupResult(true, KaliciDenetci.YedekYolu, "öğe yedeklendi")); }
        catch (Exception ex) { return Task.FromResult(new BackupResult(false, "", "Yedek yazılamadı: " + ex.Message)); }
    }
    public Task ApplyAsync(CancellationToken ct) => Uygula(true);
    public Task RollbackAsync(BackupResult y, CancellationToken ct) => Uygula(false);
    // Aynı yol iki yönde de kullanılır (devre dışı bırak / yeniden etkinleştir).
    private async Task Uygula(bool devreDisi)
    {
        var p = KaliciDenetci.DevreDisiPlani(_o, devreDisi); if (p.Ret != null) throw new DegisiklikYapilmadiException(p.Ret);
        if (p.DogrudanHkcu) _s.HkcuYaz(_o, devreDisi);
        else if (p.Komut != null) await EylemYardim.Calistir(_s, p.Komut);
        else throw new DegisiklikYapilmadiException("Uygulanacak komut yok.");
    }
}

// Genel amaçlı doğrulama kontrolü: bulgu başına bir işlev çalıştırır.
internal sealed class DelegeKontrol : IVerificationCheck
{
    private readonly Func<Finding, CancellationToken, Task<VerificationOutcome>> _f;
    public DelegeKontrol(string id, Func<Finding, CancellationToken, Task<VerificationOutcome>> f) { Id = id; _f = f; }
    public string Id { get; }
    public async Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct)
    {
        var d = new Dictionary<string, VerificationOutcome>();
        foreach (var f in referans) { try { d[f.Id] = await _f(f, ct); } catch (OperationCanceledException) { throw; } catch { d[f.Id] = VerificationOutcome.Error; } }
        return d;
    }
    // IP engel kuralı hâlâ duruyor mu? (bulgudan IP almak için verilen işlev kullanılır; çözemezse Error)
    public static DelegeKontrol IpEngelVarMi(string id, ISistemCalistirici s, Func<Finding, string?> ipBul)
        => new(id, (f, _) => { var ip = ipBul(f); return Task.FromResult(ip == null ? VerificationOutcome.Error : s.KuralAdlari().Contains(BruteForce.KuralAdi(ip)) ? VerificationOutcome.Pass : VerificationOutcome.Fail); });
}
