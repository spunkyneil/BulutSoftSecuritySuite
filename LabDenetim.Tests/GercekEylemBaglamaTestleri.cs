// Gerçek eylem bağdaştırıcılarının testleri: SAHTE sistem çalıştırıcıyla; gerçek komut/UAC/kayıt defteri/güvenlik duvarı KULLANILMAZ.
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

// Sahte sistem: çalıştırılacak komutları kaydeder, istenirse reddeder.
internal sealed class SahteSistem : ISistemCalistirici
{
    public List<string> Komutlar = new(); public List<string> Hkcu = new(); public List<string> Yedekler = new();
    public HashSet<string> Kurallar = new(); public bool UacRed;
    public Task<(bool Ok, string Hata)> YoneticiAsync(string z) { if (UacRed) return Task.FromResult((false, "Yönetici izni verilmedi")); Komutlar.Add(z); return Task.FromResult((true, "")); }
    public void HkcuYaz(KaliciOge o, bool d) => Hkcu.Add(o.Ad + ":" + d);
    public HashSet<string> KuralAdlari() => Kurallar;
    public void YedekYaz(KaliciOge o) => Yedekler.Add(o.Ad);
}

public class GercekEylemBaglamaTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    static readonly HashSet<string> Korunan = new() { "192.168.56.1", "192.168.56.101" };

    static Finding Bulgu(string chk = "CHK-IP", string onFail = "reopen_and_rollback")
        => new("f1", "BF-001", "Brute", "ip:203.0.113.9", "Saldırgan IP", new Explanation("a", "b"), new[] { "203.0.113.9" }, 8, 0.5, 40,
            new ComplianceTags(new[] { "KVKK m.12" }, new[] { "A.8.20" }, Array.Empty<string>()),
            new Remediation(RemediationType.Auto, "Engelle", "netsh ...", true, true, new BackupPlan("kural", "netsh", "x", true), chk, new VerificationPolicy(0, 1, onFail)),
            new FalsePositiveTracking(FpState.None, "BF-001", null, null, null, 0, 0), FindingState.Open, T0, T0);

    static (RemediationEngine m, AuditTrail iz, VerificationService v) Motor(SahteSistem s, string chk = "CHK-IP", string ip = "203.0.113.9")
    {
        var iz = new AuditTrail(); var v = new VerificationService(); v.Kaydet(DelegeKontrol.IpEngelVarMi(chk, s, _ => ip));
        return (new RemediationEngine(iz, v, null, (_, _) => Task.CompletedTask, () => T0), iz, v);
    }
    static Task<bool> Evet(string _) => Task.FromResult(true);

    // ---- IP engeli ----
    [Fact] public async Task IpEngel_Onayla_Uygular_VeDogrular()
    {
        var s = new SahteSistem(); var (m, iz, _) = Motor(s);
        // Sahte sistem komutu çalıştırınca kuralı "ekler".
        var e = new IpEngelEylemi("203.0.113.9", Korunan, new KuralEkleyen(s));
        var r = await m.YurutAsync(Bulgu(), new[] { Bulgu() }, e, Evet, "ali", default);
        Assert.Equal(OnarimSonucu.Dogrulandi, r.Sonuc);
        Assert.Single(s.Komutlar); Assert.Contains("remoteip=203.0.113.9", s.Komutlar[0]); Assert.Contains("name=BulutSoft-BF-203-0-113-9", s.Komutlar[0]);
        Assert.True(iz.Dogrula().Saglam);
    }
    [Fact] public async Task IpEngel_KorunanAdres_YedekteReddedilir_KomutCalismaz()
    {
        var s = new SahteSistem(); var (m, _, _) = Motor(s, ip: "192.168.56.101");
        var r = await m.YurutAsync(Bulgu(), new[] { Bulgu() }, new IpEngelEylemi("192.168.56.101", Korunan, s), Evet, "ali", default);
        Assert.Equal(OnarimSonucu.YedekHatasi, r.Sonuc); Assert.Empty(s.Komutlar);
    }
    [Fact] public async Task IpEngel_GecersizAdres_Reddedilir()
    {
        var s = new SahteSistem(); var (m, _, _) = Motor(s);
        var r = await m.YurutAsync(Bulgu(), new[] { Bulgu() }, new IpEngelEylemi("1.2.3.4; calc", Korunan, s), Evet, "ali", default);
        Assert.Equal(OnarimSonucu.YedekHatasi, r.Sonuc); Assert.Empty(s.Komutlar);
    }
    [Fact] public async Task IpEngel_UacReddedilirse_GeriAlmaDenenmez()
    {
        var s = new SahteSistem { UacRed = true }; var (m, iz, _) = Motor(s);
        var r = await m.YurutAsync(Bulgu(), new[] { Bulgu() }, new IpEngelEylemi("203.0.113.9", Korunan, s), Evet, "ali", default);
        Assert.Equal(OnarimSonucu.UygulamaHatasi, r.Sonuc); Assert.Empty(s.Komutlar); Assert.DoesNotContain(iz.Kayitlar, x => x.Action == "rolled_back");
    }
    [Fact] public async Task IpEngel_DogrulamaBasarisiz_KuralYoktuysaSilerek_GeriAlir()
    {
        // Sahte sistem komutu çalıştırır ama kural listesine eklemez → doğrulama Fail → reopen_and_rollback.
        var s = new SahteSistem(); var (m, _, _) = Motor(s);
        var r = await m.YurutAsync(Bulgu(), new[] { Bulgu() }, new IpEngelEylemi("203.0.113.9", Korunan, s), Evet, "ali", default);
        Assert.Equal(OnarimSonucu.DogrulamaBasarisizGeriAlindi, r.Sonuc);
        Assert.Equal(2, s.Komutlar.Count); Assert.Contains("delete rule name=BulutSoft-BF-203-0-113-9", s.Komutlar[1]); Assert.DoesNotContain("add rule", s.Komutlar[1]);
    }
    [Fact] public async Task IpEngel_KuralOncedenVarsa_GeriAlmadaDokunmaz()
    {
        var s = new SahteSistem(); s.Kurallar.Add("BulutSoft-BF-203-0-113-9"); var e = new IpEngelEylemi("203.0.113.9", Korunan, s);
        var y = await e.BackupAsync(default); await e.RollbackAsync(y, default);
        Assert.Empty(s.Komutlar);
    }

    // ---- Port engeli ----
    [Fact] public async Task PortEngel_Uygular_VeGeriAlmadaYalnizcaYeniyiSiler()
    {
        var s = new SahteSistem(); s.Kurallar.Add("BulutSoft-Port-23"); var e = new PortEngelEylemi(new[] { 23, 445 }, "BulutSoft-Port-", s);
        var y = await e.BackupAsync(default); Assert.True(y.Ok);
        await e.ApplyAsync(default); Assert.Contains("localport=445", s.Komutlar[0]);
        await e.RollbackAsync(y, default);
        Assert.Contains("name=BulutSoft-Port-445", s.Komutlar[1]); Assert.DoesNotContain("BulutSoft-Port-23", s.Komutlar[1]);
    }
    [Fact] public async Task PortEngel_GecersizPortVeAd_Reddedilir()
    {
        var s = new SahteSistem();
        Assert.False((await new PortEngelEylemi(new[] { 0 }, "BulutSoft-Port-", s).BackupAsync(default)).Ok);
        Assert.False((await new PortEngelEylemi(new[] { 70000 }, "BulutSoft-Port-", s).BackupAsync(default)).Ok);
        Assert.False((await new PortEngelEylemi(new[] { 80 }, "Baska-", s).BackupAsync(default)).Ok);
        Assert.False((await new PortEngelEylemi(Array.Empty<int>(), "BulutSoft-Port-", s).BackupAsync(default)).Ok);
        Assert.Empty(s.Komutlar);
    }

    // ---- Başlangıç öğesi ----
    static KaliciOge Oge(string tur = "Run", bool hklm = false, bool ms = false) => new() { Tur = tur, Ad = "Garip", Konum = hklm ? "HKLM\\Software\\Run" : "HKCU\\Software\\Run", Hklm = hklm, Microsoft = ms };

    [Fact] public async Task Baslangic_Hkcu_DogrudanYazar_YedekAlir_GeriAlir()
    {
        var s = new SahteSistem(); var e = new BaslangicDevreDisiEylemi(Oge(), s);
        var y = await e.BackupAsync(default); Assert.True(y.Ok); Assert.Equal(new[] { "Garip" }, s.Yedekler);
        await e.ApplyAsync(default); await e.RollbackAsync(y, default);
        Assert.Equal(new[] { "Garip:True", "Garip:False" }, s.Hkcu); Assert.Empty(s.Komutlar);
    }
    [Fact] public async Task Baslangic_Hklm_YoneticiKomutuKullanir()
    {
        var s = new SahteSistem(); var e = new BaslangicDevreDisiEylemi(Oge(hklm: true), s);
        await e.ApplyAsync(default); await e.RollbackAsync(new BackupResult(true, "", ""), default);
        Assert.Contains("030000000000000000000000", s.Komutlar[0]); Assert.Contains("020000000000000000000000", s.Komutlar[1]);
    }
    [Fact] public async Task Baslangic_MicrosoftImzali_HicDokunulmaz()
    {
        var s = new SahteSistem(); var e = new BaslangicDevreDisiEylemi(Oge(ms: true), s);
        var y = await e.BackupAsync(default); Assert.False(y.Ok); Assert.Contains("Microsoft", y.Detail);
        await Assert.ThrowsAsync<DegisiklikYapilmadiException>(() => e.ApplyAsync(default));
        Assert.Empty(s.Komutlar); Assert.Empty(s.Hkcu); Assert.Empty(s.Yedekler);
    }
    [Fact] public async Task Baslangic_RunOnceVeOzelKarakter_Reddedilir()
    {
        var s = new SahteSistem();
        Assert.False((await new BaslangicDevreDisiEylemi(Oge("RunOnce"), s).BackupAsync(default)).Ok);
        var kotu = Oge(); kotu.Ad = "x\" & calc & \"";
        Assert.False((await new BaslangicDevreDisiEylemi(kotu, s).BackupAsync(default)).Ok);
        Assert.Empty(s.Komutlar);
    }
    [Fact] public async Task Baslangic_Motorla_TamAkis_DogrulamaDelegeyle()
    {
        var s = new SahteSistem(); var iz = new AuditTrail(); var v = new VerificationService(); v.Kaydet(new DelegeKontrol("CHK-ST", (f, _) => Task.FromResult(s.Hkcu.Contains("Garip:True") ? VerificationOutcome.Pass : VerificationOutcome.Fail)));
        var m = new RemediationEngine(iz, v, null, (_, _) => Task.CompletedTask, () => T0);
        var b = Bulgu("CHK-ST");
        var r = await m.YurutAsync(b, new[] { b }, new BaslangicDevreDisiEylemi(Oge(), s), Evet, "ali", default);
        Assert.Equal(OnarimSonucu.Dogrulandi, r.Sonuc); Assert.Equal(new[] { "Garip:True" }, s.Hkcu);
    }
}

// Komutu çalıştırınca ilgili kuralı listeye de ekleyen sahte sistem (IP engeli doğrulaması için).
internal sealed class KuralEkleyen : ISistemCalistirici
{
    private readonly SahteSistem _s; public KuralEkleyen(SahteSistem s) { _s = s; }
    public async Task<(bool Ok, string Hata)> YoneticiAsync(string z)
    {
        var r = await _s.YoneticiAsync(z);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(z, @"add rule name=(\S+)")) _s.Kurallar.Add(m.Groups[1].Value);
        return r;
    }
    public void HkcuYaz(KaliciOge o, bool d) => _s.HkcuYaz(o, d);
    public HashSet<string> KuralAdlari() => _s.KuralAdlari();
    public void YedekYaz(KaliciOge o) => _s.YedekYaz(o);
}
