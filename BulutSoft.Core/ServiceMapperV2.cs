// Bu dosya Hafta 4'ün hizmet tarafıdır: açık port → süreç (PID) → Windows hizmeti → hesap/imza zincirini kurar ve üç tür bulgu üretir:
// tırnaksız+boşluklu hizmet yolu, yazılabilir klasördeki hizmet dosyası, imzasız hizmet (ayrıcalıklı hesapla).
// Yanlış alarmı azaltma kuralları: tırnaklı yol bulgu üretmez; Microsoft imzalı hizmet "imzasız" sayılmaz; tırnaksız yolda ara klasörler YAZILAMIYORSA önem düşer.
using System.Text.RegularExpressions;

// V2 ad alanı.
namespace LabDenetim.V2;

// Bulgu üretirken ortak adımlar (uyumluluk etiketleri, doğrulama kimliği, skor).
internal static class BulguFabrikasi
{
    // Yerel kontrollerin KVKK/ISO/MITRE etiketleri (rehber niteliğinde).
    public static Finding Yap(string ruleId, string modul, string assetId, string anahtar, string baslik, string ne, string neden, string[] kanit, double etki, double varlikAgirligi, string[] iso, string[] mitre, string cozum, string kontrolKimligi, DateTime simdi)
        => new(Finding.KararliKimlik(ruleId, assetId, anahtar), ruleId, modul, assetId, baslik, new Explanation(ne, neden), kanit, etki, varlikAgirligi, Finding.HesaplaSkor(etki, varlikAgirligi),
            new ComplianceTags(new[] { "KVKK m.12/1" }, iso, mitre),
            new Remediation(RemediationType.Manual, cozum, null, false, false, null, kontrolKimligi, new VerificationPolicy(0, 1, "reopen")),
            new FalsePositiveTracking(FpState.None, ruleId, null, null, null, 0, 0), FindingState.Open, simdi, simdi);
}

// Hizmet eşleyici ve bulgu üreticisi.
internal static class ServiceMapper
{
    // Doğrulama kontrol kimliği (aynı kimliği tüm hizmet bulguları paylaşır: çoktan-bire).
    public const string KontrolKimligi = "CHK-SVC-LOCAL";

    // Hesap ayrıcalıklı mı (SYSTEM düzeyi)?
    public static bool SistemHesabiMi(string? hesap) { var h = (hesap ?? "").Trim().ToLowerInvariant(); return h is "localsystem" or "nt authority\\system" or "system"; }
    // Hizmet hesabı (LocalService/NetworkService dahil) yerleşik ayrıcalıklı hesap mı?
    public static bool YerlesikHesapMi(string? hesap) { var h = (hesap ?? "").Trim().ToLowerInvariant(); return SistemHesabiMi(h) || h.StartsWith("nt authority\\", StringComparison.Ordinal) || h == "localservice" || h == "networkservice"; }

    // Ham hizmet yolunu çözer: çalıştırılan dosya yolu ve yolun tırnaklı olup olmadığı. ".exe" olmayan (sürücü vb.) kayıtlar için null döner.
    public static (string Exe, bool Tirnakli)? YolCoz(string? hamYol)
    {
        if (string.IsNullOrWhiteSpace(hamYol)) return null;
        string y = Environment.ExpandEnvironmentVariables(hamYol.Trim());
        // Tırnaklıysa ikinci tırnağa kadar.
        if (y.StartsWith('"')) { int k = y.IndexOf('"', 1); if (k < 2) return null; string e = y[1..k]; return e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? (e, true) : null; }
        // Tırnaksız: ilk ".exe" bitişine kadar (parametreler yoksayılır).
        var m = Regex.Match(y, @"^(.+?\.exe)(?=\s|$)", RegexOptions.IgnoreCase); return m.Success ? (m.Groups[1].Value, false) : null;
    }

    // Tırnaksız yolun "sömürülebilir" aday dosyaları: her boşlukta yol kesilip ".exe" eklenir (örn. C:\Program Files\X\a.exe → C:\Program.exe).
    public static List<string> AdayYollar(string exe)
    {
        var l = new List<string>(); string ana = exe[..^4];
        for (int i = 0; i < ana.Length; i++) if (ana[i] == ' ') l.Add(ana[..i] + ".exe");
        return l;
    }

    // Port → süreç → hizmet eşlemesi (yalnızca dinleyen kapılar).
    public static List<ServiceBinding> Esle(IYerelKaynak k, IReadOnlyList<WinHizmeti>? hizmetler = null, IReadOnlyList<DinleyenPort>? dinleyenler = null)
    {
        hizmetler ??= k.Hizmetler(); dinleyenler ??= k.Dinleyenler();
        // Aynı kapı + aynı süreç (IPv4/IPv6, birden çok adres) tek kayıt sayılır.
        dinleyenler = dinleyenler.GroupBy(d => (d.Port, d.Proto, d.Pid)).Select(g => g.First()).ToList();
        var sonuc = new List<ServiceBinding>();
        // Gerekli imzalar tek seferde sorulur.
        var surecler = dinleyenler.Select(d => d.Pid).Distinct().ToDictionary(p => p, p => k.Surec(p));
        // Süreç yolu okunamazsa (korumalı süreçler yönetici olmadan okunamaz) hizmetin kendi dosya yolu kullanılır.
        string? YolBul(int pid) { var s = surecler.GetValueOrDefault(pid); if (!string.IsNullOrEmpty(s?.Yol)) return s!.Yol; var hz = pid > 4 ? hizmetler.FirstOrDefault(x => x.Pid == pid) : null; return hz == null ? null : YolCoz(hz.HamYol)?.Exe; }
        var imza = k.Imzalar(dinleyenler.Select(d => YolBul(d.Pid)).Where(y => !string.IsNullOrEmpty(y)).Select(y => y!).Distinct(StringComparer.OrdinalIgnoreCase));
        foreach (var d in dinleyenler)
        {
            surecler.TryGetValue(d.Pid, out var s);
            // Aynı süreçte (örn. svchost) çalışan hizmetler: hepsinin adı birleştirilir.
            var h = hizmetler.Where(x => x.Pid == d.Pid && d.Pid > 4).ToList(); var ilk = h.FirstOrDefault();
            var cozum = ilk == null ? null : YolCoz(ilk.HamYol); string? yol = YolBul(d.Pid);
            string imzaDurumu = yol != null && imza.TryGetValue(yol, out var iz) ? iz.Durum : "Bilinmiyor";
            string ad = s?.Ad ?? (yol != null ? Path.GetFileName(yol) : "bilinmiyor");
            sonuc.Add(new ServiceBinding(d.Port, d.Proto, d.Pid, ad, yol, h.Count == 0 ? null : string.Join(",", h.Select(x => x.Ad)), ilk?.Hesap, ilk?.BaslatmaTuru, imzaDurumu, cozum?.Tirnakli ?? true));
        }
        return sonuc;
    }

    // Hizmetleri inceler ve bulgu üretir. assetId = bu bilgisayarın varlık kimliği.
    public static List<Finding> Bulgular(IYerelKaynak k, string assetId, double varlikAgirligi, DateTime simdi, IReadOnlyList<WinHizmeti>? hizmetler = null)
    {
        hizmetler ??= k.Hizmetler(); var bulgular = new List<Finding>();
        // Yalnızca .exe yolu olan ve devre dışı olmayan hizmetler.
        var adaylar = hizmetler.Select(h => (H: h, Yol: YolCoz(h.HamYol))).Where(x => x.Yol != null && !x.H.BaslatmaTuru.Equals("Disabled", StringComparison.OrdinalIgnoreCase)).ToList();
        var imza = k.Imzalar(adaylar.Select(x => x.Yol!.Value.Exe).Distinct(StringComparer.OrdinalIgnoreCase));
        foreach (var (h, yol) in adaylar)
        {
            var (exe, tirnakli) = yol!.Value; imza.TryGetValue(exe, out var iz); bool ms = iz.Microsoft; string imzaDurumu = iz.Durum ?? "Bilinmiyor"; bool sistem = SistemHesabiMi(h.Hesap);
            string hesapYazi = string.IsNullOrWhiteSpace(h.Hesap) ? "bilinmiyor" : h.Hesap;
            string[] ortakKanit = { $"Hizmet: {h.Ad} ({h.GorunenAd})", $"Yol: {h.HamYol}", $"Hesap: {hesapYazi}", $"İmza: {imzaDurumu}" };

            // 1) Tırnaksız + boşluklu yol (Microsoft imzalılar ve tırnaklılar atlanır).
            if (!tirnakli && exe.Contains(' ') && !ms)
            {
                // Aday dosyaların bulunacağı klasörlerden biri yazılabiliyorsa gerçekten sömürülebilir.
                var yazilabilir = AdayYollar(exe).Where(a => { var d = Path.GetDirectoryName(a); return !string.IsNullOrEmpty(d) && k.DizinYazilabilirMi(d); }).ToList();
                bool tehlikeli = yazilabilir.Count > 0;
                double etki = tehlikeli ? (sistem ? 9 : 7) : (sistem ? 4 : 3);
                string neden = tehlikeli ? "Yolun boşluklu bölümünde, sıradan kullanıcıların dosya yazabildiği bir klasör var; saldırgan oraya sahte bir program koyup hizmetin yüksek yetkiyle çalıştırmasını sağlayabilir." : "Yol tırnaksız, ancak ara klasörler sıradan kullanıcılara yazma izni vermiyor; şu an sömürülmesi için önce yönetici izni gerekir. Yine de tırnak içine alınması önerilir.";
                bulgular.Add(BulguFabrikasi.Yap("SVC-UNQUOTED-001", "Hizmet", assetId, h.Ad, $"Tırnaksız hizmet yolu: {h.Ad}", "Hizmetin dosya yolu tırnak içinde değil ve boşluk içeriyor.", neden,
                    ortakKanit.Concat(tehlikeli ? new[] { "Yazılabilir aday: " + string.Join("; ", yazilabilir) } : Array.Empty<string>()).ToArray(), etki, varlikAgirligi, new[] { "A.8.9", "A.8.2" }, new[] { "T1574.009" },
                    $"Kayıt defterinde HKLM\\SYSTEM\\CurrentControlSet\\Services\\{h.Ad} altındaki ImagePath değerini tırnak içine alın (\"{exe}\"). Sistemi değiştirdiği için yönetici olarak ve yedek alarak yapın.", KontrolKimligi, simdi));
            }

            // 2) Hizmet dosyasının klasörü sıradan kullanıcılar tarafından yazılabiliyor.
            string? dizin = Path.GetDirectoryName(exe);
            if (!string.IsNullOrEmpty(dizin) && k.DizinYazilabilirMi(dizin))
            {
                bulgular.Add(BulguFabrikasi.Yap("SVC-WRITABLE-001", "Hizmet", assetId, h.Ad, $"Hizmet dosyası yazılabilir klasörde: {h.Ad}", "Hizmetin çalıştırdığı programın bulunduğu klasöre sıradan kullanıcılar dosya yazabiliyor.",
                    "Saldırgan hizmet dosyasını kendi programıyla değiştirirse, hizmet yeniden başladığında bu program hizmetin yetkisiyle (çoğu zaman SYSTEM) çalışır.",
                    ortakKanit.Append("Yazılabilir klasör: " + dizin).ToArray(), sistem ? 9 : 7, varlikAgirligi, new[] { "A.8.2", "A.8.9" }, new[] { "T1574.010" },
                    $"\"{dizin}\" klasöründen Users / Everyone / Authenticated Users için yazma ve değiştirme izinlerini kaldırın (Özellikler > Güvenlik > Gelişmiş).", KontrolKimligi, simdi));
            }

            // 3) İmzasız (veya geçersiz imzalı) hizmet; ayrıcalıklı hesapla çalışıyorsa önem yüksek. "Doğrulanamadı" bulgu üretmez (yanlış alarm olmasın).
            if (!ms && imzaDurumu is "İmzasız" or "Geçersiz" or "Güvenilmeyen" && YerlesikHesapMi(h.Hesap))
            {
                bulgular.Add(BulguFabrikasi.Yap("SVC-UNSIGNED-001", "Hizmet", assetId, h.Ad, $"İmzasız hizmet yüksek yetkiyle çalışıyor: {h.Ad}", $"Hizmetin programı dijital olarak imzalı değil ({imzaDurumu}) ve {hesapYazi} hesabıyla çalışıyor.",
                    "İmzasız bir program kimin yazdığı doğrulanamayan koddur; yüksek yetkiyle çalışması riski artırır. Yazarı bilinen, güvendiğiniz bir yazılım olup olmadığını kontrol edin.",
                    ortakKanit, sistem ? 6 : 4, varlikAgirligi, new[] { "A.8.19", "A.8.2" }, new[] { "T1543.003" },
                    "Bu hizmetin hangi yazılıma ait olduğunu doğrulayın. Tanımıyorsanız hizmeti devre dışı bırakmadan önce yazılımın üreticisine veya BT sorumlusuna danışın.", KontrolKimligi, simdi));
            }
        }
        return bulgular;
    }
}

// Hizmet bulguları için doğrulama kontrolü: yeniden toplayıp bulgu hâlâ üretiliyor mu bakar (çoktan-bire).
internal sealed class HizmetDogrulamaKontrolu : IVerificationCheck
{
    private readonly Func<List<Finding>> _yenidenTopla;
    public HizmetDogrulamaKontrolu(Func<List<Finding>> yenidenTopla) { _yenidenTopla = yenidenTopla; }
    public string Id => ServiceMapper.KontrolKimligi;
    public Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct)
    {
        // Yeniden toplandığında hâlâ var olan bulguların kimlikleri.
        var simdiki = _yenidenTopla().Select(x => x.Id).ToHashSet();
        IReadOnlyDictionary<string, VerificationOutcome> sonuc = referans.ToDictionary(f => f.Id, f => simdiki.Contains(f.Id) ? VerificationOutcome.Fail : VerificationOutcome.Pass);
        return Task.FromResult(sonuc);
    }
}
