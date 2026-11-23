// Bu dosya Hafta 7'dir: çalıştırılabilir dosyaların (EXE) PE başlığındaki derleme korumalarını (ASLR, DEP, CFG, HighEntropyVA, SafeSEH) okur
// ve "DLL arama sırası" riskini değerlendirir (uygulama klasörü sıradan kullanıcılara yazılabiliyorsa saldırgan oraya sahte DLL koyabilir).
// Her şey SALT-OKUNURDUR: dosyalar yalnızca başlıkları için okunur (paylaşımlı, kilit almadan), hiçbir şey değiştirilmez.
// Yanlış alarm savunmaları: Microsoft imzalı dosyada eksik koruma yalnızca "bilgi" (önem ≤1); ağa açık/hizmet olmayan programlarda koruma eksikliği raporlanmaz;
// yönetici-yazılabilir klasörler (Program Files) risk üretmez; yönetilen (.NET) dosyalarda CFG/HighEntropy beklenmez; PE olmayan/bozuk dosya çökmeden atlanır.
using System.Reflection.PortableExecutable;

// V2 ad alanı.
namespace LabDenetim.V2;

// Bir dosyanın PE raporu (tasarım belgesindeki PeReport).
public sealed record PeReport(string Path, string Machine, bool IsManaged, bool Aslr, bool HighEntropyVa, bool Dep, bool Cfg, bool SafeSeh, string SignatureState, string? Publisher, bool InWritableDirectory, string[] WritableBy, bool DllSearchRisk, string[] RiskReasons);

// Analiz adayı: hangi dosya, neden inceleniyor.
internal sealed record PeAday(string Yol, bool Servis, bool Ayricalikli, bool Dinliyor, string Hesap);

// PE okuyucu.
internal static class PeAnalyzer
{
    // DllCharacteristics bit değerleri.
    private const int HighEntropyVa = 0x0020, DynamicBase = 0x0040, NxCompat = 0x0100, NoSeh = 0x0400, GuardCf = 0x4000;
    // Dosya adı uzantıları: yalnızca çalıştırılabilirler incelenir.
    public static bool PeUzantisi(string yol) => yol.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || yol.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    // PE başlığını okur; PE değilse/bozuksa/erişilemiyorsa null (çökmez). İmza ve ACL bilgisi sonradan eklenir.
    public static PeReport? Oku(string yol)
    {
        try
        {
            using var fs = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (fs.Length < 64) return null;
            using var pe = new PEReader(fs); var h = pe.PEHeaders; var ph = h.PEHeader; if (ph == null) return null;
            int dc = (int)ph.DllCharacteristics; bool x64 = h.CoffHeader.Machine is Machine.Amd64 or Machine.Arm64, yonetilen = h.CorHeader != null;
            string makine = h.CoffHeader.Machine switch { Machine.Amd64 => "x64", Machine.I386 => "x86", Machine.Arm64 => "arm64", Machine.Arm or Machine.ArmThumb2 => "arm", var m => m.ToString() };
            // SafeSEH: x64/ARM'da SEH tablosu zaten sabit (sorun değil); x86'da NO_SEH bayrağı SEH kullanmadığını gösterir (tam SafeSEH tablosu ayrıca okunmaz: bilinen sınır).
            bool safeSeh = x64 || (dc & NoSeh) != 0;
            return new PeReport(yol, makine, yonetilen, (dc & DynamicBase) != 0, (dc & HighEntropyVa) != 0, (dc & NxCompat) != 0, (dc & GuardCf) != 0, safeSeh, "Bilinmiyor", null, false, Array.Empty<string>(), false, Array.Empty<string>());
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException) { return null; }
    }
}

// Aday listesi ve bulgu üretimi.
internal static class PeBulgulari
{
    // Doğrulama kontrol kimliği.
    public const string KontrolKimligi = "CHK-PE-LOCAL";
    // En çok incelenecek dosya (süre ve bellek sınırı).
    public const int EnCokDosya = 400;

    // Aday dosyalar: hizmetlerin çalıştırdığı dosyalar + dinleyen süreçlerin dosyaları (tekrarsız).
    public static List<PeAday> Adaylar(IReadOnlyList<WinHizmeti> hizmetler, IReadOnlyList<ServiceBinding> baglar)
    {
        var d = new Dictionary<string, PeAday>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in hizmetler)
        {
            if (h.BaslatmaTuru.Equals("Disabled", StringComparison.OrdinalIgnoreCase)) continue;
            var y = ServiceMapper.YolCoz(h.HamYol); if (y == null || !PeAnalyzer.PeUzantisi(y.Value.Exe)) continue;
            bool dinliyor = baglar.Any(b => string.Equals(b.ImagePath, y.Value.Exe, StringComparison.OrdinalIgnoreCase));
            d[y.Value.Exe] = new PeAday(y.Value.Exe, true, ServiceMapper.YerlesikHesapMi(h.Hesap), dinliyor, h.Hesap);
        }
        foreach (var b in baglar) if (!string.IsNullOrEmpty(b.ImagePath) && PeAnalyzer.PeUzantisi(b.ImagePath) && !d.ContainsKey(b.ImagePath)) d[b.ImagePath] = new PeAday(b.ImagePath, false, false, true, b.ServiceAccount ?? "");
        return d.Values.Take(EnCokDosya).ToList();
    }

    // DLL arama sırası riski: uygulama klasörü sıradan kullanıcılara yazılabiliyorsa (sahte DLL yerleştirilebilir). Nedenleri döndürür.
    public static (bool Risk, List<string> Nedenler) DllAramaRiski(bool dizinYazilabilir, bool dosyaYazilabilir, PeAday a)
    {
        var n = new List<string>();
        if (dizinYazilabilir) n.Add("Uygulama klasörüne sıradan kullanıcılar dosya yazabiliyor: aynı klasöre konan sahte bir DLL, program başlarken yüklenebilir.");
        if (dosyaYazilabilir) n.Add("Programın kendisi sıradan kullanıcılar tarafından değiştirilebiliyor.");
        if ((dizinYazilabilir || dosyaYazilabilir) && a.Ayricalikli) n.Add($"Program ayrıcalıklı hesapla çalışıyor ({a.Hesap}): saldırgan yetki yükseltebilir.");
        return (n.Count > 0, n);
    }

    // Adayları inceler ve bulgu üretir. Kaynaktan PE bilgisi, imza ve ACL alınır; kaynak null PE verirse dosya atlanır.
    public static List<Finding> Bulgular(IYerelKaynak k, IReadOnlyList<PeAday> adaylar, string assetId, double varlikAgirligi, DateTime simdi, ISet<string>? haricYollar = null)
    {
        var l = new List<Finding>();
        var imza = k.Imzalar(adaylar.Select(a => a.Yol).Distinct(StringComparer.OrdinalIgnoreCase));
        foreach (var a in adaylar)
        {
            var r = k.PeOku(a.Yol); if (r == null) continue;
            imza.TryGetValue(a.Yol, out var iz); bool ms = iz.Microsoft; string imzaDurumu = iz.Durum ?? "Bilinmiyor";
            string[] kanitTaban = { $"Dosya: {a.Yol}", $"Mimari: {r.Machine}{(r.IsManaged ? " (yönetilen .NET)" : "")}", $"İmza: {imzaDurumu}", a.Servis ? $"Hizmet olarak çalışıyor ({(string.IsNullOrWhiteSpace(a.Hesap) ? "hesap bilinmiyor" : a.Hesap)})" : "Ağ dinleyen süreç" };

            // 1) DLL arama sırası / yazılabilir klasör. Hizmetlerde klasör yazılabilirliği zaten SVC-WRITABLE-001 ile raporlanır (çift bulgu olmasın).
            string? dizin = Path.GetDirectoryName(a.Yol); bool dizinY = !string.IsNullOrEmpty(dizin) && k.DizinYazilabilirMi(dizin), dosyaY = k.DosyaYazilabilirMi(a.Yol);
            var (risk, nedenler) = DllAramaRiski(dizinY, dosyaY, a);
            if (risk && !(a.Servis && haricYollar != null && haricYollar.Contains(a.Yol)))
            {
                double etki = a.Ayricalikli ? 8 : a.Dinliyor ? 6 : 3;
                l.Add(BulguFabrikasi.Yap("PE-DLLRISK-001", "Yazılım", assetId, a.Yol, $"DLL arama riski: {Path.GetFileName(a.Yol)} yazılabilir klasörde", "Programın bulunduğu klasör (veya dosyanın kendisi) sıradan kullanıcılara yazılabiliyor.",
                    "Windows, bir program başlarken önce kendi klasöründe DLL arar. Saldırgan oraya sahte bir DLL koyarsa, program onu kendi yetkisiyle çalıştırır.",
                    kanitTaban.Concat(nedenler).Append("Yazılabilir klasör: " + dizin).ToArray(), etki, varlikAgirligi, new[] { "A.8.9", "A.8.2" }, new[] { "T1574.001", "T1574.002" },
                    $"\"{dizin}\" klasöründen Users / Everyone / Authenticated Users için yazma ve değiştirme izinlerini kaldırın; programı Program Files gibi yönetici-yazılabilir bir konuma taşıyın.", KontrolKimligi, simdi));
            }

            // 2) Derleme korumaları: yalnızca ağa açık veya hizmet olan, yönetilmeyen (native) dosyalar. Microsoft imzalı dosyada yalnızca bilgi.
            if (r.IsManaged || !(a.Dinliyor || a.Servis)) continue;
            var eksik = new List<(string Ad, double Etki)>();
            if (!r.Aslr) eksik.Add(("ASLR (adres rastgeleleştirme)", 5)); if (!r.Dep) eksik.Add(("DEP (veri alanında kod çalıştırma engeli)", 5));
            if (!r.Cfg) eksik.Add(("CFG (akış bütünlüğü)", 2)); if (r.Machine == "x64" && r.Aslr && !r.HighEntropyVa) eksik.Add(("HighEntropyVA (64 bit rastgeleleştirme)", 1)); if (r.Machine == "x86" && !r.SafeSeh) eksik.Add(("SafeSEH/NO_SEH (istisna işleyici koruması)", 2));
            // Yalnızca ASLR veya DEP eksikse bulgu verilir: CFG/HighEntropyVA/SafeSEH tek başına eksikse çok yaygın ve düşük değerlidir (gürültü olmasın); ASLR/DEP eksikse ayrıntı olarak yanlarında listelenir.
            if (r.Aslr && r.Dep) continue;
            double e = eksik.Max(x => x.Etki); if (ms) e = Math.Min(e, 1); else if (a.Ayricalikli && e >= 5) e = 6;
            string ad = Path.GetFileName(a.Yol); var liste = string.Join(", ", eksik.Select(x => x.Ad));
            l.Add(BulguFabrikasi.Yap("PE-MITIGATION-001", "Yazılım", assetId, a.Yol, $"{ad}: derleme korumaları eksik ({liste})", "Programın başlığında bellek bozulması saldırılarını zorlaştıran koruma bayraklarından bazıları kapalı.",
                ms ? "Microsoft imzalı bir sistem dosyası; eksik bayrak bilgi amaçlıdır, işletim sistemi başka katmanlarla korur." : "Bu korumalar olmadan, programdaki bir bellek hatasının sömürülmesi saldırgan için çok daha kolaydır.",
                kanitTaban.Append("Eksik: " + liste).ToArray(), e, varlikAgirligi, new[] { "A.8.28", "A.8.9" }, new[] { "T1203" },
                ms ? "Yapılacak bir şey yok." : $"Yazılımın üreticisinden korumalı (ASLR/DEP/CFG açık derlenmiş) sürümünü isteyin veya güncel sürüme geçin; kaynak kodu sizdeyse derleyici bayraklarını açın (/DYNAMICBASE /NXCOMPAT /guard:cf).", KontrolKimligi, simdi));
        }
        return l.OrderByDescending(f => f.Score).ToList();
    }
}

// PE bulguları için doğrulama kontrolü: izinler/dosya düzeltilince bulgu artık üretilmez.
internal sealed class PeDogrulamaKontrolu : IVerificationCheck
{
    private readonly Func<List<Finding>> _yenidenTopla;
    public PeDogrulamaKontrolu(Func<List<Finding>> yenidenTopla) { _yenidenTopla = yenidenTopla; }
    public string Id => PeBulgulari.KontrolKimligi;
    public Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct)
    {
        var simdiki = _yenidenTopla().Select(x => x.Id).ToHashSet();
        IReadOnlyDictionary<string, VerificationOutcome> sonuc = referans.ToDictionary(f => f.Id, f => simdiki.Contains(f.Id) ? VerificationOutcome.Fail : VerificationOutcome.Pass);
        return Task.FromResult(sonuc);
    }
}
