// Hafta 7 testleri: PE başlık okuma (sentetik ve gerçek dosyalar), DLL arama riski, koruma eksikliği bulguları, yanlış alarm kontrolleri, doğrulama.
using System.Text;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

public class PeAnalizTestleri
{
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    // Yalnızca başlık içeren en küçük geçerli PE (PE32+ veya PE32) üretir; DllCharacteristics ve makine seçilebilir.
    static byte[] Pe(ushort makine, ushort dllChar, bool pe32Plus = true)
    {
        var b = new byte[0x400]; b[0] = (byte)'M'; b[1] = (byte)'Z'; BitConverter.GetBytes(0x80).CopyTo(b, 0x3C);
        Encoding.ASCII.GetBytes("PE\0\0").CopyTo(b, 0x80);
        int c = 0x84; BitConverter.GetBytes(makine).CopyTo(b, c); BitConverter.GetBytes((ushort)0).CopyTo(b, c + 2);                       // makine, bölüm sayısı = 0
        ushort opt = (ushort)(pe32Plus ? 240 : 224); BitConverter.GetBytes(opt).CopyTo(b, c + 16); BitConverter.GetBytes((ushort)0x22).CopyTo(b, c + 18);
        int o = c + 20; BitConverter.GetBytes((ushort)(pe32Plus ? 0x20B : 0x10B)).CopyTo(b, o);
        // Ortak alanlar (ofsetler PE32+ için; PE32'de ImageBase 4 bayt olduğundan sonrası 4 bayt kayar).
        int sa = pe32Plus ? o + 32 : o + 32; BitConverter.GetBytes(0x1000).CopyTo(b, sa); BitConverter.GetBytes(0x200).CopyTo(b, sa + 4);   // SectionAlignment, FileAlignment
        BitConverter.GetBytes(0x2000).CopyTo(b, o + 56); BitConverter.GetBytes(0x200).CopyTo(b, o + 60);                                  // SizeOfImage, SizeOfHeaders
        BitConverter.GetBytes((ushort)3).CopyTo(b, o + 68); BitConverter.GetBytes(dllChar).CopyTo(b, o + 70);                              // Subsystem, DllCharacteristics
        int rva = pe32Plus ? o + 108 : o + 92; BitConverter.GetBytes(16).CopyTo(b, rva);                                                    // NumberOfRvaAndSizes
        return b;
    }
    static string Gecici(byte[] icerik, string ad = ".exe") { string y = Path.Combine(Path.GetTempPath(), "pe_" + Guid.NewGuid().ToString("N") + ad); File.WriteAllBytes(y, icerik); return y; }

    // ---- PE okuma ----
    [Fact] public void Pe_TumKorumalarAcik_x64()
    {
        string y = Gecici(Pe(0x8664, 0x0020 | 0x0040 | 0x0100 | 0x4000));
        try { var r = PeAnalyzer.Oku(y)!; Assert.NotNull(r); Assert.Equal("x64", r.Machine); Assert.True(r.Aslr && r.HighEntropyVa && r.Dep && r.Cfg && r.SafeSeh); Assert.False(r.IsManaged); }
        finally { File.Delete(y); }
    }
    [Fact] public void Pe_KorumalarKapali_x64()
    {
        string y = Gecici(Pe(0x8664, 0));
        try { var r = PeAnalyzer.Oku(y)!; Assert.False(r.Aslr || r.HighEntropyVa || r.Dep || r.Cfg); Assert.True(r.SafeSeh); /* x64'te SEH tablosu sabit */ }
        finally { File.Delete(y); }
    }
    [Fact] public void Pe_x86_SafeSeh_NoSehBayragina_Bakar()
    {
        string a = Gecici(Pe(0x014C, 0x0040 | 0x0100, false)), b = Gecici(Pe(0x014C, 0x0040 | 0x0100 | 0x0400, false));
        try { var ra = PeAnalyzer.Oku(a)!; var rb = PeAnalyzer.Oku(b)!; Assert.Equal("x86", ra.Machine); Assert.True(ra.Aslr && ra.Dep); Assert.False(ra.SafeSeh); Assert.True(rb.SafeSeh); }
        finally { File.Delete(a); File.Delete(b); }
    }
    [Fact] public void Pe_OlmayanVeBozukDosyalar_Cokmez_Null()
    {
        var yollar = new List<string> { Gecici(Encoding.UTF8.GetBytes("bu bir metin dosyasıdır, PE değil. " + new string('x', 200))), Gecici(Array.Empty<byte>()), Gecici(new byte[] { 0x4D, 0x5A, 1, 2, 3 }), Gecici(Pe(0x8664, 0).Take(100).ToArray()), Gecici(new byte[4096]) };
        try { foreach (var y in yollar) Assert.Null(PeAnalyzer.Oku(y)); Assert.Null(PeAnalyzer.Oku(@"C:\yok\boyle\dosya.exe")); Assert.Null(PeAnalyzer.Oku(Path.GetTempPath())); }
        finally { foreach (var y in yollar) File.Delete(y); }
    }
    [Fact] public void Pe_KilitliDosya_PaylasimliOkunur()
    {
        string y = Gecici(Pe(0x8664, 0x40 | 0x100));
        try { using var kilit = new FileStream(y, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); Assert.NotNull(PeAnalyzer.Oku(y)); }   // çalışan program gibi başka süreç tarafından açık dosya
        finally { File.Delete(y); }
    }
    [Fact] public void Pe_GercekYonetilenDosya_ManagedAlgilanir()
    {
        var r = PeAnalyzer.Oku(typeof(PeAnalyzer).Assembly.Location); Assert.NotNull(r); Assert.True(r!.IsManaged);   // .NET derlemesi
    }
    [Fact] public void Pe_GercekSistemDosyasi_SaltOkunur()
    {
        string n = Path.Combine(Environment.SystemDirectory, "notepad.exe"); if (!File.Exists(n)) n = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var r = PeAnalyzer.Oku(n); Assert.NotNull(r); Assert.True(r!.Aslr && r.Dep); Assert.False(r.IsManaged); Assert.Equal("x64", r.Machine);
    }
    [Theory] [InlineData(@"C:\a\b.exe", true)] [InlineData(@"C:\a\b.DLL", true)] [InlineData(@"C:\a\b.sys", false)] [InlineData(@"C:\a\b", false)]
    public void PeUzantisi(string y, bool beklenen) => Assert.Equal(beklenen, PeAnalyzer.PeUzantisi(y));

    // ---- DLL arama riski ----
    static PeReport Rapor(string yol, bool aslr = true, bool dep = true, bool cfg = true, bool yonetilen = false, string makine = "x64", bool hev = true, bool safeSeh = true)
        => new(yol, makine, yonetilen, aslr, hev, dep, cfg, safeSeh, "Bilinmiyor", null, false, Array.Empty<string>(), false, Array.Empty<string>());
    static List<Finding> Bul(SahteYerel k, params PeAday[] a) => PeBulgulari.Bulgular(k, a, "host:T", 0.5, T0);
    static PeAday Sup(string yol, bool servis = false, bool ayricalikli = false, bool dinliyor = true, string hesap = "") => new(yol, servis, ayricalikli, dinliyor, hesap);

    [Fact] public void DllRisk_UsersYazilabilirKlasor_Bulgu_ProgramFilesYoksa_Degil()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Tools"); k.Pe[@"C:\Tools\app.exe"] = Rapor(@"C:\Tools\app.exe"); k.Pe[@"C:\Program Files\App\app.exe"] = Rapor(@"C:\Program Files\App\app.exe");
        var l = Bul(k, Sup(@"C:\Tools\app.exe"), Sup(@"C:\Program Files\App\app.exe"));
        var f = Assert.Single(l); Assert.Equal("PE-DLLRISK-001", f.RuleId); Assert.Equal(6, f.ImpactWeight); Assert.Contains(f.Evidence, e => e.Contains(@"C:\Tools")); Assert.Empty(FindingSchema.Validate(f));
    }
    [Fact] public void DllRisk_AyricalikliOlunca_Yuksek_YazilabilirDosya_DaTek()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Srv"); k.Pe[@"C:\Srv\s.exe"] = Rapor(@"C:\Srv\s.exe"); var f = Assert.Single(Bul(k, Sup(@"C:\Srv\s.exe", false, true, true, "LocalSystem")));
        Assert.Equal(8, f.ImpactWeight); Assert.Equal(40, f.Score); Assert.Contains(f.Evidence, e => e.Contains("ayrıcalıklı"));
        var k2 = new SahteYerel(); k2.YazilabilirDosya.Add(@"C:\Prog\p.exe"); k2.Pe[@"C:\Prog\p.exe"] = Rapor(@"C:\Prog\p.exe"); Assert.Equal("PE-DLLRISK-001", Assert.Single(Bul(k2, Sup(@"C:\Prog\p.exe"))).RuleId);   // dosya yazılabilir
    }
    [Fact] public void DllRisk_Hizmet_SvcWritableIleCiftBulgu_Uretmez()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Srv"); k.Pe[@"C:\Srv\s.exe"] = Rapor(@"C:\Srv\s.exe");
        var a = Sup(@"C:\Srv\s.exe", true, true, false, "LocalSystem"); var haric = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { a.Yol };
        Assert.Empty(PeBulgulari.Bulgular(k, new[] { a }, "host:T", 0.5, T0, haric)); Assert.Single(PeBulgulari.Bulgular(k, new[] { a }, "host:T", 0.5, T0));   // dışlama yoksa üretir
    }
    [Fact] public void DllAramaRiski_Nedenler()
    {
        Assert.False(PeBulgulari.DllAramaRiski(false, false, Sup("x", ayricalikli: true)).Risk);
        var (r, n) = PeBulgulari.DllAramaRiski(true, false, Sup("x", ayricalikli: true, hesap: "LocalSystem")); Assert.True(r); Assert.Equal(2, n.Count);
    }

    // ---- Koruma eksikliği ----
    [Fact] public void KorumaEksik_AgaAcikNativeDosya_Bulgu_EksikleriListeler()
    {
        var k = new SahteYerel(); k.Pe[@"C:\Apps\srv.exe"] = Rapor(@"C:\Apps\srv.exe", aslr: false, dep: false, cfg: false); var f = Assert.Single(Bul(k, Sup(@"C:\Apps\srv.exe")));
        Assert.Equal("PE-MITIGATION-001", f.RuleId); Assert.Equal(5, f.ImpactWeight); Assert.Contains("ASLR", f.Title); Assert.Contains("DEP", f.Title); Assert.Contains("CFG", f.Title); Assert.Empty(FindingSchema.Validate(f));
        Assert.Equal(6, Assert.Single(Bul(k, Sup(@"C:\Apps\srv.exe", true, true, true, "LocalSystem"))).ImpactWeight);   // hizmet + ayrıcalıklı → 6
    }
    [Fact] public void KorumaEksik_MicrosoftImzali_YalnizcaBilgi()
    {
        var k = new SahteYerel(); k.Pe[@"C:\Windows\eski.exe"] = Rapor(@"C:\Windows\eski.exe", aslr: false, dep: false, cfg: false); k.Imza[@"C:\Windows\eski.exe"] = ("İmzalı", "Microsoft Corporation", true);
        var f = Assert.Single(Bul(k, Sup(@"C:\Windows\eski.exe"))); Assert.True(f.ImpactWeight <= 1); Assert.Equal(Onem.Bilgi, f.EskiOnem); Assert.Contains("bilgi amaçlıdır", f.Explanation.WhyItMatters); Assert.Equal("Yapılacak bir şey yok.", f.Remediation.Summary);
    }
    [Fact] public void KorumaEksik_AgaAcikOlmayan_YonetilenVeTamKorumali_Yok()
    {
        var k = new SahteYerel();
        k.Pe[@"C:\a\yerel.exe"] = Rapor(@"C:\a\yerel.exe", aslr: false, dep: false);                  // ağa açık değil, hizmet değil → raporlanmaz
        k.Pe[@"C:\a\net.exe"] = Rapor(@"C:\a\net.exe", aslr: true, dep: true, cfg: false, yonetilen: true, hev: false);   // yönetilen: CFG/HighEntropy beklenmez
        k.Pe[@"C:\a\iyi.exe"] = Rapor(@"C:\a\iyi.exe");                                              // her şey açık
        Assert.Empty(Bul(k, Sup(@"C:\a\yerel.exe", dinliyor: false), Sup(@"C:\a\net.exe"), Sup(@"C:\a\iyi.exe")));
    }
    [Fact] public void KorumaEksik_YalnizCfgHighEntropySafeSeh_GurultuUretmez_AmaAslrVarsaYanindaListelenir()
    {
        var k = new SahteYerel(); k.Pe[@"C:\a\x.exe"] = Rapor(@"C:\a\x.exe", cfg: false, hev: false); k.Pe[@"C:\a\y.exe"] = Rapor(@"C:\a\y.exe", cfg: false, makine: "x86", safeSeh: false);
        Assert.Empty(Bul(k, Sup(@"C:\a\x.exe"), Sup(@"C:\a\y.exe")));   // ASLR+DEP açık: CFG/HEV/SafeSEH tek başına bulgu değil
        k.Pe[@"C:\a\z.exe"] = Rapor(@"C:\a\z.exe", aslr: false, cfg: false); var f = Assert.Single(Bul(k, Sup(@"C:\a\z.exe"))); Assert.Contains("ASLR", f.Title); Assert.Contains("CFG", f.Title);
    }
    [Fact] public void PeOkunamayanDosya_AtlanirBulguYok() { var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Tools"); Assert.Empty(Bul(k, Sup(@"C:\Tools\yok.exe"))); }

    // ---- Adaylar ----
    [Fact] public void Adaylar_HizmetVeDinleyenSurecler_TekrarsizVeDevreDisiAtlanir()
    {
        var hz = new List<WinHizmeti> { new("A", "A", 5, "\"C:\\x\\a.exe\" -s", "LocalSystem", "Auto", "Running"), new("B", "B", 0, @"C:\x\b.exe", "LocalService", "Disabled", "Stopped"), new("Sur", "Sur", 0, @"\SystemRoot\drv.sys", "", "Auto", "Running") };
        var bg = new List<ServiceBinding> { new(80, "tcp", 5, "a.exe", @"C:\x\a.exe", "A", "LocalSystem", "Auto", "İmzalı", true), new(9000, "tcp", 9, "z.exe", @"C:\y\z.exe", null, null, null, "Bilinmiyor", true), new(9001, "tcp", 9, "z.exe", @"C:\y\z.exe", null, null, null, "Bilinmiyor", true) };
        var a = PeBulgulari.Adaylar(hz, bg); Assert.Equal(2, a.Count);
        var servis = a.Single(x => x.Servis); Assert.True(servis.Ayricalikli); Assert.True(servis.Dinliyor); Assert.Equal(@"C:\x\a.exe", servis.Yol);
        var sur = a.Single(x => !x.Servis); Assert.Equal(@"C:\y\z.exe", sur.Yol); Assert.True(sur.Dinliyor); Assert.False(sur.Ayricalikli);
    }

    // ---- Doğrulama: ACL düzeltilince ----
    [Fact] public async Task Dogrulama_KlasorKilitlenince_Pass()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Tools"); k.Pe[@"C:\Tools\app.exe"] = Rapor(@"C:\Tools\app.exe"); var once = Bul(k, Sup(@"C:\Tools\app.exe")); Assert.Single(once);
        var v = new VerificationService(); v.Kaydet(new PeDogrulamaKontrolu(() => Bul(k, Sup(@"C:\Tools\app.exe"))));
        Assert.Equal(VerificationOutcome.Fail, (await v.CalistirAsync(PeBulgulari.KontrolKimligi, once, default)).Aggregate);
        k.Yazilabilir.Clear();   // izin kaldırıldı
        var r = await v.CalistirAsync(PeBulgulari.KontrolKimligi, once, default); Assert.Equal(VerificationOutcome.Pass, r.Aggregate); Assert.Equal(FindingState.Verified, VerificationService.DurumUygula(once, r, T0)[0].State);
    }

    // ---- Akış entegrasyonu ----
    [Fact] public void YerelAkis_PeBulgusu_Uretir_ServisIcinCiftBulguYok()
    {
        var k = new SahteYerel(); k.Yazilabilir.Add(@"C:\Srv"); k.Hizmet.Add(new WinHizmeti("S", "S", 7, @"C:\Srv\s.exe", "LocalSystem", "Auto", "Running")); k.Pe[@"C:\Srv\s.exe"] = Rapor(@"C:\Srv\s.exe", aslr: false);
        k.Dinleyen.Add(new DinleyenPort(8080, "tcp", "0.0.0.0", 9)); k.Surecler[9] = new SurecKaydi("z.exe", @"C:\Tools\z.exe"); k.Yazilabilir.Add(@"C:\Tools"); k.Pe[@"C:\Tools\z.exe"] = Rapor(@"C:\Tools\z.exe");
        var s = YerelDenetimAkisi.Calistir(k, null, null, T0);
        Assert.Equal(1, s.Bulgular.Count(f => f.RuleId == "SVC-WRITABLE-001")); Assert.Equal(1, s.Bulgular.Count(f => f.RuleId == "PE-DLLRISK-001" && f.Title.Contains("z.exe")));   // hizmet için yalnızca SVC-WRITABLE
        Assert.DoesNotContain(s.Bulgular, f => f.RuleId == "PE-DLLRISK-001" && f.Title.Contains("s.exe")); Assert.Contains(s.Bulgular, f => f.RuleId == "PE-MITIGATION-001"); Assert.Empty(s.Reddedilen);
    }

    // ---- Gerçek makine (salt-okunur; varsayılan kapalı) ----
    [Fact] public void Canli_GercekMakine_PeAnalizi_SaltOkunur()
    {
        if (Environment.GetEnvironmentVariable("BULUT_CANLI_TEST") != "1") return;
        string eski = BulutKlasor.Kok, g = Path.Combine(Path.GetTempPath(), "canli_pe_" + Guid.NewGuid().ToString("N")); BulutKlasor.Kok = g;
        try
        {
            var k = new GercekYerelKaynak(); var hz = k.Hizmetler(); var bg = ServiceMapper.Esle(k, hz, k.Dinleyenler()); var ad = PeBulgulari.Adaylar(hz, bg); var sw = System.Diagnostics.Stopwatch.StartNew();
            var l = PeBulgulari.Bulgular(k, ad, "host:T", 0.5, DateTime.UtcNow, ad.Where(a => a.Servis).Select(a => a.Yol).ToHashSet(StringComparer.OrdinalIgnoreCase));
            Console.WriteLine($"CANLI-PE: aday={ad.Count} okunabilen={ad.Count(a => k.PeOku(a.Yol) != null)} bulgu={l.Count} sure={sw.Elapsed.TotalSeconds:0.0}sn");
            foreach (var f in l.Take(12)) Console.WriteLine($"  [{f.Score}] {f.RuleId} {f.Title}");
            Assert.All(l, f => Assert.Empty(FindingSchema.Validate(f)));
        }
        finally { BulutKlasor.Kok = eski; try { Directory.Delete(g, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "PeAnalizTestleri.temizlik"); } }
    }
}
