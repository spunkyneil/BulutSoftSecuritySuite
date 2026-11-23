// Hafta 4 testleri: port→süreç→hizmet eşleme, hizmet bulguları (yanlış alarm kontrolleriyle), ACL kuralı, KB kataloğu, yama denetçisi, doğrulama.
// Tüm veriler SAHTE kaynaktan gelir; gerçek makineye bakılmaz (yalnızca iki salt-okunur kontrol hariç).
using System.Security.AccessControl;
using System.Security.Principal;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

// Sahte yerel kaynak.
internal sealed class SahteYerel : IYerelKaynak
{
    public List<DinleyenPort> Dinleyen = new(); public List<WinHizmeti> Hizmet = new(); public Dictionary<int, SurecKaydi> Surecler = new();
    public Dictionary<string, (string, string, bool)> Imza = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> Yazilabilir = new(StringComparer.OrdinalIgnoreCase);
    public List<HotfixKaydi> Hf = new(); public string Os = "10.0.26100.1234"; public List<InstalledSoftware> Yaz = new();
    public List<InstalledSoftware> Yazilimlar() => Yaz;
    public Dictionary<string, PeReport> Pe = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> YazilabilirDosya = new(StringComparer.OrdinalIgnoreCase);
    public PeReport? PeOku(string yol) => Pe.GetValueOrDefault(yol); public bool DosyaYazilabilirMi(string yol) => YazilabilirDosya.Contains(yol);
    public List<DinleyenPort> Dinleyenler() => Dinleyen; public List<WinHizmeti> Hizmetler() => Hizmet; public SurecKaydi? Surec(int pid) => Surecler.GetValueOrDefault(pid);
    public Dictionary<string, (string Durum, string Yayinci, bool Microsoft)> Imzalar(IEnumerable<string> y) { var d = new Dictionary<string, (string, string, bool)>(StringComparer.OrdinalIgnoreCase); foreach (var x in y) if (Imza.TryGetValue(x, out var v)) d[x] = v; return d; }
    public bool DizinYazilabilirMi(string d) => Yazilabilir.Contains(d.TrimEnd('\\')) || Yazilabilir.Contains(d);
    public List<HotfixKaydi> Hotfixler() => Hf; public string OsSurumu() => Os; public string MakineAdi() => "TESTPC";
}

public class YerelDenetimTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    static WinHizmeti H(string ad, string yol, string hesap = "LocalSystem", int pid = 0, string mod = "Auto") => new(ad, ad + " Hizmeti", pid, yol, hesap, mod, "Running");
    static (string, string, bool) Ms => ("İmzalı", "Microsoft Corporation", true);
    static (string, string, bool) Imzasiz => ("İmzasız", "", false);
    static List<Finding> Bul(SahteYerel k, double agirlik = 0.5) => ServiceMapper.Bulgular(k, "host:TESTPC", agirlik, T0);

    // ---- Yol çözümü ----
    [Fact] public void YolCoz_Durumlari()
    {
        Assert.Equal((@"C:\Program Files\X\a.exe", true), ServiceMapper.YolCoz("\"C:\\Program Files\\X\\a.exe\" -k run"));
        Assert.Equal((@"C:\Program Files\X\a.exe", false), ServiceMapper.YolCoz(@"C:\Program Files\X\a.exe -k run"));
        Assert.Equal((@"C:\Windows\System32\svchost.exe", false), ServiceMapper.YolCoz(@"C:\Windows\System32\svchost.exe -k netsvcs -p"));
        Assert.Null(ServiceMapper.YolCoz(@"\SystemRoot\System32\drivers\x.sys")); Assert.Null(ServiceMapper.YolCoz("")); Assert.Null(ServiceMapper.YolCoz(null)); Assert.Null(ServiceMapper.YolCoz("\"bozuk"));
        Assert.Equal((Environment.GetEnvironmentVariable("SystemRoot") + @"\x.exe", false), ServiceMapper.YolCoz(@"%SystemRoot%\x.exe"));
    }
    [Fact] public void AdayYollar_BoslugaGoreKesilir() => Assert.Equal(new[] { @"C:\Program.exe" }, ServiceMapper.AdayYollar(@"C:\Program Files\X\a.exe"));
    [Fact] public void AdayYollar_CokBosluk() => Assert.Equal(new[] { @"C:\Program.exe", @"C:\Program Files\My.exe" }, ServiceMapper.AdayYollar(@"C:\Program Files\My App\a.exe"));

    // ---- Port → süreç → hizmet ----
    [Fact] public void Esle_PortuPidyeVeHizmeteBaglar()
    {
        var k = new SahteYerel(); k.Dinleyen.Add(new DinleyenPort(8080, "tcp", "0.0.0.0", 100)); k.Surecler[100] = new SurecKaydi("foo.exe", @"C:\Foo\foo.exe"); k.Hizmet.Add(H("FooSvc", "\"C:\\Foo\\foo.exe\"", "NT AUTHORITY\\NetworkService", 100)); k.Imza[@"C:\Foo\foo.exe"] = ("İmzalı", "Foo Inc", false);
        var b = Assert.Single(ServiceMapper.Esle(k));
        Assert.Equal(8080, b.Port); Assert.Equal(100, b.Pid); Assert.Equal("foo.exe", b.ProcessName); Assert.Equal("FooSvc", b.WindowsServiceName); Assert.Equal("NT AUTHORITY\\NetworkService", b.ServiceAccount); Assert.Equal("İmzalı", b.SignatureState); Assert.True(b.PathQuoted);
    }
    [Fact] public void Esle_OrtakSurec_HizmetAdlariBirlesir_VeHizmetsizSurecDeListelenir()
    {
        var k = new SahteYerel(); k.Dinleyen.Add(new DinleyenPort(135, "tcp", "0.0.0.0", 7)); k.Dinleyen.Add(new DinleyenPort(9999, "tcp", "127.0.0.1", 55));
        k.Surecler[7] = new SurecKaydi("svchost.exe", @"C:\Windows\System32\svchost.exe"); k.Hizmet.Add(H("RpcSs", @"C:\Windows\System32\svchost.exe -k rpc", "NT AUTHORITY\\NetworkService", 7)); k.Hizmet.Add(H("RpcEptMapper", @"C:\Windows\System32\svchost.exe -k rpc", "NT AUTHORITY\\NetworkService", 7));
        var l = ServiceMapper.Esle(k);
        Assert.Equal("RpcSs,RpcEptMapper", l.Single(x => x.Port == 135).WindowsServiceName); Assert.Null(l.Single(x => x.Port == 9999).WindowsServiceName); Assert.Equal("bilinmiyor", l.Single(x => x.Port == 9999).ProcessName);
    }
    [Fact] public void Esle_SurecYoluOkunamazsa_HizmetYolundanImzaBulunur_VeIpv4Ipv6TekKayit()
    {
        var k = new SahteYerel(); k.Dinleyen.Add(new DinleyenPort(135, "tcp", "0.0.0.0", 7)); k.Dinleyen.Add(new DinleyenPort(135, "tcp", "::", 7));
        k.Hizmet.Add(H("RpcSs", @"C:\Windows\System32\svchost.exe -k rpc", "NT AUTHORITY\\NetworkService", 7)); k.Imza[@"C:\Windows\System32\svchost.exe"] = Ms;   // k.Surecler boş: süreç bilgisi alınamadı
        var b = Assert.Single(ServiceMapper.Esle(k)); Assert.Equal("İmzalı", b.SignatureState); Assert.Equal("svchost.exe", b.ProcessName);
    }
    [Fact] public void Hotfix_TarihiAbdBicimindenOkunur() => Assert.Equal(new DateTime(2026, 11, 12), GercekYerelKaynak.TarihOku("11/12/2026")!.Value.Date);   // WMI: ay/gün/yıl
    [Fact] public void Esle_Pid0_HizmetEslesmez()
    {
        var k = new SahteYerel(); k.Dinleyen.Add(new DinleyenPort(80, "tcp", "::", 0)); k.Hizmet.Add(H("Durmus", @"C:\x.exe", pid: 0));
        Assert.Null(Assert.Single(ServiceMapper.Esle(k)).WindowsServiceName);   // durdurulmuş (PID 0) hizmetler yanlış eşleşmesin
    }

    // ---- Tırnaksız yol ----
    [Fact] public void Tirnaksiz_YazilabilirAraKlasor_Yuksek_ve_SkorFormuluDogru()
    {
        var k = new SahteYerel(); k.Hizmet.Add(H("Zayif", @"C:\Program Files\App\svc.exe")); k.Yazilabilir.Add(@"C:");
        var b = Assert.Single(Bul(k)); Assert.Equal("SVC-UNQUOTED-001", b.RuleId); Assert.Equal(9, b.ImpactWeight); Assert.Equal(45, b.Score);
        Assert.Contains(b.Evidence, x => x.Contains(@"C:\Program.exe")); Assert.Empty(FindingSchema.Validate(b));
        Assert.Equal(63, Bul(k, 0.7).Single().Score);   // varlık ağırlığı skoru etkiler
    }
    [Fact] public void Tirnaksiz_ArakKlasorlerYazilamazsa_OnemDuser_YanlisAlarmAzalir()
    {
        var k = new SahteYerel(); k.Hizmet.Add(H("Sakin", @"C:\Program Files\App\svc.exe"));
        var b = Assert.Single(Bul(k)); Assert.Equal(4, b.ImpactWeight); Assert.Equal(Onem.Dusuk, b.EskiOnem);
    }
    [Fact] public void Tirnakli_BoslukSuzVeMicrosoft_BulguUretmez()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:");
        k.Hizmet.Add(H("Tirnakli", "\"C:\\Program Files\\App\\svc.exe\""));                      // tırnaklı → yanlış alarm olmamalı
        k.Hizmet.Add(H("BoslukYok", @"C:\Apps\svc.exe -k x"));                                    // yolda boşluk yok (parametre sayılmaz)
        k.Hizmet.Add(H("MsHizmet", @"C:\Program Files\Microsoft X\ms.exe")); k.Imza[@"C:\Program Files\Microsoft X\ms.exe"] = Ms;   // Microsoft imzalı
        k.Hizmet.Add(H("Surucu", @"\SystemRoot\System32\drivers\x.sys"));                         // sürücü
        Assert.Empty(Bul(k));
    }
    [Fact] public void Devredisi_Hizmetler_Atlanir()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:"); k.Hizmet.Add(H("Kapali", @"C:\Program Files\App\svc.exe", mod: "Disabled"));
        Assert.Empty(Bul(k));
    }

    // ---- Yazılabilir klasör ----
    [Fact] public void YazilabilirKlasor_Bulgu_SYSTEM_Yuksek_NormalHesapDahaDusuk()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Apps"); k.Hizmet.Add(H("A", @"C:\Apps\a.exe")); k.Hizmet.Add(H("B", @"C:\Apps\b.exe", @"NT AUTHORITY\LocalService"));
        var l = Bul(k).Where(x => x.RuleId == "SVC-WRITABLE-001").ToList(); Assert.Equal(2, l.Count);
        Assert.Equal(9, l.Single(x => x.Title.Contains(": A")).ImpactWeight); Assert.Equal(7, l.Single(x => x.Title.Contains(": B")).ImpactWeight);
        Assert.All(l, f => Assert.Empty(FindingSchema.Validate(f)));
    }
    [Fact] public void YazilamazKlasor_Bulgu_Yok() { var k = new SahteYerel(); k.Hizmet.Add(H("A", @"C:\Windows\a.exe")); Assert.Empty(Bul(k)); }

    // ---- İmzasız hizmet ----
    [Fact] public void Imzasiz_SYSTEM_Bulgu_AmaMicrosoftVeDogrulanamadiVeNormalHesapDegil()
    {
        var k = new SahteYerel();
        k.Hizmet.Add(H("Bilinmeyen", @"C:\Tools\x.exe")); k.Imza[@"C:\Tools\x.exe"] = Imzasiz;                                       // bulgu
        k.Hizmet.Add(H("MsOlan", @"C:\Tools\ms.exe")); k.Imza[@"C:\Tools\ms.exe"] = Ms;                                              // Microsoft: yok
        k.Hizmet.Add(H("Belirsiz", @"C:\Tools\b.exe")); k.Imza[@"C:\Tools\b.exe"] = ("Doğrulanamadı", "", false);                     // doğrulanamadı: yanlış alarm yok
        k.Hizmet.Add(H("Kullanici", @"C:\Tools\u.exe", @".\ali")); k.Imza[@"C:\Tools\u.exe"] = Imzasiz;                              // ayrıcalıksız hesap: yok
        k.Hizmet.Add(H("ImzaliDiger", @"C:\Tools\d.exe")); k.Imza[@"C:\Tools\d.exe"] = ("İmzalı", "Foo Inc", false);                  // imzalı başka yayıncı: yok
        var b = Assert.Single(Bul(k)); Assert.Equal("SVC-UNSIGNED-001", b.RuleId); Assert.Equal(6, b.ImpactWeight); Assert.Empty(FindingSchema.Validate(b));
    }

    [Fact] public void HepsiGecerliFinding_VeKararliKimlik()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:"); k.Yazilabilir.Add(@"C:\Program Files\App"); k.Hizmet.Add(H("Hepsi", @"C:\Program Files\App\svc.exe")); k.Imza[@"C:\Program Files\App\svc.exe"] = Imzasiz;
        var a = Bul(k); var b = Bul(k); Assert.Equal(3, a.Count);
        Assert.All(a, f => Assert.Empty(FindingSchema.Validate(f))); Assert.Equal(a.Select(x => x.Id), b.Select(x => x.Id)); Assert.Equal(3, a.Select(x => x.Id).Distinct().Count());
        Assert.All(a, f => Assert.Equal(ServiceMapper.KontrolKimligi, f.Remediation.VerificationCheckId));
    }

    // ---- Doğrulama (düzeltmeden sonra yeniden çalıştırma) ----
    [Fact] public async Task Dogrulama_HizmetYoluDuzelince_Pass_DuzelmeyenOpenKalir()
    {
        var k = new SahteYerel(); k.Hizmet.Add(H("A", @"C:\Program Files\A\a.exe")); k.Hizmet.Add(H("B", @"C:\Program Files\B\b.exe")); k.Yazilabilir.Add(@"C:");
        var once = Bul(k); Assert.Equal(2, once.Count);
        // A'nın yolu tırnak içine alınır (düzeltme); B aynı kalır.
        k.Hizmet[0] = H("A", "\"C:\\Program Files\\A\\a.exe\"");
        var v = new VerificationService(); v.Kaydet(new HizmetDogrulamaKontrolu(() => Bul(k)));
        var r = await v.CalistirAsync(ServiceMapper.KontrolKimligi, once, default);
        Assert.Equal(VerificationOutcome.Fail, r.Aggregate);   // çoktan-bire: biri hâlâ açık
        var d = VerificationService.DurumUygula(once, r, T0);
        Assert.Equal(FindingState.Verified, d.Single(x => x.Title.Contains(": A")).State); Assert.Equal(FindingState.Open, d.Single(x => x.Title.Contains(": B")).State);
    }

    // ---- ACL kuralı ----
    static FileSystemAccessRule Kural(WellKnownSidType sid, FileSystemRights hak, AccessControlType tur = AccessControlType.Allow, PropagationFlags yayilma = PropagationFlags.None)
        => new(new SecurityIdentifier(sid, null), hak, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, yayilma, tur);
    [Fact] public void Acl_KullanicilaraYazmaVerilirse_Yazilabilir() => Assert.True(GercekYerelKaynak.KurallarYazmaIzniVeriyor(new[] { Kural(WellKnownSidType.BuiltinUsersSid, FileSystemRights.Modify) }));
    [Fact] public void Acl_YalnizAltOgelereUygulananKural_Sayilmaz() => Assert.False(GercekYerelKaynak.KurallarYazmaIzniVeriyor(new[] { Kural(WellKnownSidType.AuthenticatedUserSid, FileSystemRights.CreateFiles, yayilma: PropagationFlags.InheritOnly) }));
    [Fact] public void Acl_SadeceOkumaVeYoneticiler_Yazilamaz() => Assert.False(GercekYerelKaynak.KurallarYazmaIzniVeriyor(new[] { Kural(WellKnownSidType.BuiltinUsersSid, FileSystemRights.ReadAndExecute), Kural(WellKnownSidType.BuiltinAdministratorsSid, FileSystemRights.FullControl) }));
    [Fact] public void Acl_ReddetmeKurali_YazmayiKapatir() => Assert.False(GercekYerelKaynak.KurallarYazmaIzniVeriyor(new[] { Kural(WellKnownSidType.WorldSid, FileSystemRights.FullControl), Kural(WellKnownSidType.BuiltinUsersSid, FileSystemRights.WriteData, AccessControlType.Deny) }));
    [Fact] public void Acl_KlasorOlusturmaHakki_DosyaYazmaSayilmaz() => Assert.False(GercekYerelKaynak.KurallarYazmaIzniVeriyor(new[] { Kural(WellKnownSidType.AuthenticatedUserSid, FileSystemRights.AppendData) }));   // C:\ varsayılanı: yalnızca alt klasör
    [Fact] public void Acl_GercekSistemKlasoru_Yazilamaz_SalitOkuma() { Assert.False(new GercekYerelKaynak().DizinYazilabilirMi(Environment.SystemDirectory)); Assert.False(new GercekYerelKaynak().DizinYazilabilirMi(@"C:\yok_boyle_klasor_123")); }

    // ---- KB kataloğu ----
    const string Katalog = """
{ "catalog_version": "2026-11-01-test", "builds": { "26100": [
 {"kb":"KB1000001","released":"2026-01-01","severity":"Important","title":"Ocak"},
 {"kb":"KB1000002","released":"2026-02-01","severity":"Critical","title":"Şubat"},
 {"kb":"KB1000003","released":"2026-03-01","severity":"Moderate","title":"Mart"} ] } }
""";
    static KbCatalog Kat => KbCatalog.Coz(Katalog);

    [Fact] public void Katalog_Cozulur_SurumVeOzetVar() { var k = Kat; Assert.Equal("2026-11-01-test", k.Surum); Assert.Equal(64, k.Ozet.Length); Assert.Equal(3, k.Yapilar["26100"].Count); }

    [Theory]
    [InlineData("")] [InlineData("{bozuk")] [InlineData("[]")] [InlineData("{\"catalog_version\":\"x\"}")] [InlineData("{\"catalog_version\":\"\",\"builds\":{\"26100\":[]}}")]
    [InlineData("{\"catalog_version\":\"x\",\"builds\":{\"abc\":[]}}")] [InlineData("{\"catalog_version\":\"x\",\"builds\":{}}")]
    [InlineData("{\"catalog_version\":\"x\",\"builds\":{\"26100\":[{\"kb\":\"KB12\",\"released\":\"2026-01-01\",\"severity\":\"Low\"}]}}")]
    [InlineData("{\"catalog_version\":\"x\",\"builds\":{\"26100\":[{\"kb\":\"KB1000001\",\"released\":\"01.01.2026\",\"severity\":\"Low\"}]}}")]
    [InlineData("{\"catalog_version\":\"x\",\"builds\":{\"26100\":[{\"kb\":\"KB1000001\",\"released\":\"2026-01-01\",\"severity\":\"Kritik\"}]}}")]
    public void Katalog_BozukGirdi_Reddedilir(string json) => Assert.Throws<KatalogHatasi>(() => KbCatalog.Coz(json));

    [Fact] public void Katalog_Sinirlar_VeTekrarYoksayilir()
    {
        var cok = "{\"catalog_version\":\"x\",\"builds\":{\"26100\":[" + string.Join(",", Enumerable.Range(0, KbCatalog.EnCokOge + 1).Select(i => $"{{\"kb\":\"KB{1000000 + i}\",\"released\":\"2026-01-01\",\"severity\":\"Low\"}}")) + "]}}";
        Assert.Throws<KatalogHatasi>(() => KbCatalog.Coz(cok));
        var tekrar = KbCatalog.Coz("{\"catalog_version\":\"x\",\"builds\":{\"26100\":[{\"kb\":\"KB1000001\",\"released\":\"2026-01-01\",\"severity\":\"Low\"},{\"kb\":\"kb1000001\",\"released\":\"2026-01-01\",\"severity\":\"Low\"}]}}");
        Assert.Single(tekrar.Yapilar["26100"]);
    }

    [Collection("Veri")]
    public class Depo
    {
        [Fact] public void IceAktar_Atomik_BozukDosyaEskiKatalogu_Korur()
        {
            string eski = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "kb_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = gecici;
            try
            {
                Assert.Null(KbCatalog.Yukle());
                KbCatalog.IceAktar(Katalog); Assert.Equal("2026-11-01-test", KbCatalog.Yukle()!.Surum);
                Assert.Throws<KatalogHatasi>(() => KbCatalog.IceAktar("{\"catalog_version\":\"yeni\",\"builds\":{\"26100\":[{\"kb\":\"HATALI\",\"released\":\"2026-01-01\",\"severity\":\"Low\"}]}}"));
                Assert.Equal("2026-11-01-test", KbCatalog.Yukle()!.Surum);   // eski katalog hâlâ geçerli
                Assert.False(File.Exists(KbCatalog.Yol + ".tmp"));
            }
            finally { BulutKlasor.Kok = eski; try { Directory.Delete(gecici, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "YerelDenetimTestleri.temizlik"); } }
        }
    }

    // ---- Eksik KB hesabı (kümülatif kural) ----
    [Fact] public void Eksikler_KumulatifKural()
    {
        var l = Kat.Yapilar["26100"];
        Assert.Equal(new[] { "KB1000001", "KB1000002", "KB1000003" }, PatchAuditor.Eksikler(Array.Empty<string>(), l).Select(x => x.Kb));
        Assert.Equal(new[] { "KB1000003" }, PatchAuditor.Eksikler(new[] { "KB1000002" }, l).Select(x => x.Kb));   // Şubat kuruluysa Ocak karşılanmış sayılır
        Assert.Empty(PatchAuditor.Eksikler(new[] { "kb1000003" }, l));
        Assert.Equal(new[] { "KB1000001", "KB1000002", "KB1000003" }, PatchAuditor.Eksikler(new[] { "KB9999999" }, l).Select(x => x.Kb));   // katalogda olmayan KB bir şeyi karşılamaz ama eksik de saymaz
    }

    // ---- Yama bulguları ----
    static SahteYerel YamaKaynak(params (string kb, int gunOnce)[] kurulu) { var k = new SahteYerel(); foreach (var (kb, g) in kurulu) k.Hf.Add(new HotfixKaydi(kb, T0.AddDays(-g))); return k; }
    static List<Finding> Yama(SahteYerel k, KbCatalog? kat) => PatchAuditor.Bulgular(k, kat, "host:TESTPC", 0.5, T0);

    [Fact] public void Yama_KatalogYok_AcikcaSoyler_SessizTemizDemez()
    {
        var l = Yama(YamaKaynak(("KB1", 5)), null); var b = Assert.Single(l); Assert.Equal("PATCH-NOCATALOG-001", b.RuleId); Assert.Equal(0, b.Score); Assert.Contains("değerlendirilemedi", b.Explanation.What); Assert.Empty(FindingSchema.Validate(b));
    }
    [Fact] public void Yama_EksikKb_SurumRaporda_OnemEnYuksekten()
    {
        var k = YamaKaynak(("KB1000001", 20)); var l = Yama(k, Kat); var b = l.Single(x => x.RuleId == "PATCH-MISSING-001");
        Assert.Equal(9, b.ImpactWeight);   // eksikler arasında Critical (Şubat) var
        Assert.Contains(b.Evidence, e => e.Contains("2026-11-01-test")); Assert.Contains(b.Evidence, e => e.Contains("KB1000002")); Assert.Contains("2", b.Title);
        Assert.All(l, f => Assert.Empty(FindingSchema.Validate(f)));
        var st = PatchAuditor.Topla(k, Kat, T0); Assert.Equal("2026-11-01-test", st.CatalogVersion); Assert.Equal(new[] { "KB1000002", "KB1000003" }, st.MissingKbs); Assert.Equal("wmi@local", st.Source);
    }
    [Fact] public void Yama_Guncel_Temiz() => Assert.Empty(Yama(YamaKaynak(("KB1000003", 10)), Kat));
    [Fact] public void Yama_Bayat_90GunVe180Gun()
    {
        Assert.Equal(5, Yama(YamaKaynak(("KB1000003", 100)), Kat).Single(x => x.RuleId == "PATCH-STALE-001").ImpactWeight);
        Assert.Equal(7, Yama(YamaKaynak(("KB1000003", 200)), Kat).Single(x => x.RuleId == "PATCH-STALE-001").ImpactWeight);
        Assert.DoesNotContain(Yama(YamaKaynak(("KB1000003", 89)), Kat), x => x.RuleId == "PATCH-STALE-001");
    }
    [Fact] public void Yama_YapiKatalogdaYok_VeVeriYok()
    {
        var k = YamaKaynak(("KB1", 5)); k.Os = "10.0.19045.5000";
        Assert.Equal("PATCH-BUILD-001", Yama(k, Kat).Single().RuleId);
        // Hiç güncelleme kaydı yoksa bunu açıkça söyler (ve katalogdaki her şey eksik görünür).
        var bos = Yama(new SahteYerel(), Kat); Assert.Contains(bos, x => x.RuleId == "PATCH-NODATA-001"); Assert.Contains(bos, x => x.RuleId == "PATCH-MISSING-001");
    }
    [Fact] public async Task Yama_Dogrulama_KbKurulunca_Pass()
    {
        var k = YamaKaynak(("KB1000001", 20)); var once = Yama(k, Kat); Assert.Contains(once, x => x.RuleId == "PATCH-MISSING-001");
        var v = new VerificationService(); v.Kaydet(new YamaDogrulamaKontrolu(() => Yama(k, Kat)));
        // Hâlâ eksik: Fail.
        Assert.Equal(VerificationOutcome.Fail, (await v.CalistirAsync(PatchAuditor.KontrolKimligi, once.Where(x => x.RuleId == "PATCH-MISSING-001").ToList(), default)).Aggregate);
        // KB'ler kurulur → bulgu yeniden üretilmez → Pass → Verified.
        k.Hf.Add(new HotfixKaydi("KB1000003", T0.AddDays(-1)));
        var hedef = once.Where(x => x.RuleId == "PATCH-MISSING-001").ToList(); var r = await v.CalistirAsync(PatchAuditor.KontrolKimligi, hedef, default);
        Assert.Equal(VerificationOutcome.Pass, r.Aggregate); Assert.Equal(FindingState.Verified, VerificationService.DurumUygula(hedef, r, T0)[0].State);
    }

    // ---- Akış, harita ve depo ----
    [Fact] public void Akis_BulgulariDogrular_HaritayaYazar_AgirligiKorur()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Apps"); k.Hizmet.Add(H("A", @"C:\Apps\a.exe", pid: 5)); k.Dinleyen.Add(new DinleyenPort(8080, "tcp", "0.0.0.0", 5)); k.Surecler[5] = new SurecKaydi("a.exe", @"C:\Apps\a.exe"); k.Hf.Add(new HotfixKaydi("KB1000003", T0.AddDays(-5)));
        var s1 = YerelDenetimAkisi.Calistir(k, Kat, null, T0);
        Assert.Contains(s1.Bulgular, f => f.RuleId == "SVC-WRITABLE-001"); Assert.Empty(s1.Reddedilen); Assert.Equal(s1.Bulgular.OrderByDescending(f => f.Score).Select(f => f.Id), s1.Bulgular.Select(f => f.Id));
        var a = Assert.Single(s1.Harita.Assets); Assert.Equal("host:TESTPC", a.AssetId); Assert.Equal("wmi@local", a.Provenance); Assert.Equal(8080, Assert.Single(a.Bindings).Port); Assert.Equal("2026-11-01-test", a.Patch!.CatalogVersion);
        // Kullanıcı ağırlığı değiştirir; yeni denetimde korunur ve skora yansır.
        var m = NetworkMapBuilder.AgirlikAyarla(s1.Harita, "host:TESTPC", 1.0); var s2 = YerelDenetimAkisi.Calistir(k, Kat, m, T0);
        Assert.Equal(1.0, s2.Harita.Assets.Single().AssetWeight); Assert.Equal(90, s2.Bulgular.Single(f => f.RuleId == "SVC-WRITABLE-001").Score);
    }

    [Fact] public void Akis_SemayaUymayanBulgu_Reddedilir_Degil_ListelenmezAmaSayilir()
    {
        // Geçersiz varlık ağırlığı (>1) olan bulgu şemaya uymaz; sahte kaynakta ağırlık 0..1 sınırlandığı için doğrudan şemayı sınıyoruz.
        var f = BulguFabrikasi.Yap("X-1", "Test", "host:T", "a", "t", "n", "w", new[] { "k" }, 5, 0.5, new[] { "A.8.8" }, Array.Empty<string>(), "çöz", "CHK", T0) with { Score = 1 };
        Assert.NotEmpty(FindingSchema.Validate(f));
    }

    [Collection("Veri")]
    public class DepoTesti
    {
        [Fact] public void BulguDeposu_YazOku()
        {
            string eski = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "bd_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = gecici;
            try
            {
                var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Apps"); k.Hizmet.Add(H("A", @"C:\Apps\a.exe")); var s = YerelDenetimAkisi.Calistir(k, null, null, T0);
                BulguDeposu.Kaydet(s.Bulgular); var g = BulguDeposu.Oku(); Assert.Equal(s.Bulgular.Count, g.Count); Assert.Equal(s.Bulgular.Select(x => x.Id), g.Select(x => x.Id)); Assert.All(g, f => Assert.Empty(FindingSchema.Validate(f)));
                File.WriteAllText(BulguDeposu.Yol, "{bozuk"); Assert.Empty(BulguDeposu.Oku());
            }
            finally { BulutKlasor.Kok = eski; try { Directory.Delete(gecici, true); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "YerelDenetimTestleri.temizlik"); } }
        }

        // Gerçek makinede SALT-OKUNUR deneme (varsayılan kapalı): BULUT_CANLI_TEST=1 ile çalışır; hiçbir şey değiştirmez.
        [Fact] public void Canli_BuBilgisayar_SaltOkunur()
        {
            if (Environment.GetEnvironmentVariable("BULUT_CANLI_TEST") != "1") return;
            string eski = BulutKlasor.Kok, gecici = Path.Combine(Path.GetTempPath(), "canli_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = gecici;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew(); var s = YerelDenetimAkisi.Calistir(new GercekYerelKaynak(), null, null, DateTime.UtcNow);
                Console.WriteLine($"CANLI: {sw.Elapsed.TotalSeconds:0.0} sn · dinleyen={s.DinleyenSayisi} hizmet={s.HizmetSayisi} bulgu={s.Bulgular.Count} reddedilen={s.Reddedilen.Count} os={s.Yama.OsBuild} kb={s.Yama.InstalledKbs.Count} sonYama={s.Yama.LastInstallDate:yyyy-MM-dd}");
                foreach (var b in s.Harita.Assets.Single().Bindings.Take(12)) Console.WriteLine($"  PORT {b.Port,5} pid={b.Pid,6} {b.ProcessName,-22} hizmet={b.WindowsServiceName} hesap={b.ServiceAccount} imza={b.SignatureState}");
                foreach (var f in s.Bulgular.Take(15)) Console.WriteLine($"  BULGU [{f.Score}] {f.RuleId} {f.Title}");
                Assert.Empty(s.Reddedilen);
            }
            finally { BulutKlasor.Kok = eski; try { Directory.Delete(gecici, true); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "YerelDenetimTestleri.temizlik"); } }
        }
    }

    [Fact] public void YapiNo_VeTarihOkuma()
    {
        Assert.Equal("26100", PatchAuditor.YapiNo("10.0.26100.1234")); Assert.Equal("", PatchAuditor.YapiNo("bozuk")); Assert.Equal("", PatchAuditor.YapiNo(""));
        Assert.Equal(new DateTime(2026, 11, 18), GercekYerelKaynak.TarihOku("11/18/2026")!.Value.Date); Assert.Equal(new DateTime(2026, 11, 18), GercekYerelKaynak.TarihOku("2026-11-18")!.Value.Date); Assert.Null(GercekYerelKaynak.TarihOku("")); Assert.Null(GercekYerelKaynak.TarihOku("abc"));
    }
}
