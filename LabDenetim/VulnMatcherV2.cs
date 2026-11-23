// Bu dosya yazılım envanterini NVD indeksi ve KEV listesiyle eşleştirir ve GÜVEN PUANLI bulgular üretir.
// Yanlış alarm savunmaları: (1) sürüm çözülemezse bulgu YOK ("sürüm bilinmiyor" notu); (2) CPE güveni 0,6'nın altındaysa önem en çok 4 (asla Yüksek);
// (3) yamalı/etkilenmeyen sürüm bulgu üretmez; (4) NVD indeksi yoksa yalnızca KEV ürün adı eşleşmesi, düşük önemle ve "sürüm doğrulanamadı" notuyla verilir;
// (5) hiçbir veri yoksa "değerlendirilemedi" der, "güvenli" demez.
using System.Text.RegularExpressions;

// V2 ad alanı.
namespace LabDenetim.V2;

// Eşleştirme sonucu.
internal sealed record VulnEslesmeSonucu(List<Finding> Bulgular, List<string> Notlar, int Incelenen, int Eslesen);

// Eşleştirici.
internal static class VulnMatcher
{
    // Doğrulama kontrol kimliği.
    public const string KontrolKimligi = "CHK-SW-VULN";
    // Bu güvenin altındaki eşleşmeler Yüksek önem almaz.
    public const double GuvenEsigi = 0.6;

    // Sürüm bir aralıkta mı? (sınır sürümler çözülemezse eşleşme sayılmaz: yanlış alarm olmasın)
    public static bool Aralikta(string surum, NvdAralik a)
    {
        if (SurumKiyasi.Coz(surum) == null) return false;
        if (a.BasDahil == null && a.BasHaric == null && a.BitDahil == null && a.BitHaric == null) return true;   // tüm sürümler
        if (a.BasDahil != null) { var k = SurumKiyasi.Kiyasla(surum, a.BasDahil); if (k == null || k < 0) return false; }
        if (a.BasHaric != null) { var k = SurumKiyasi.Kiyasla(surum, a.BasHaric); if (k == null || k <= 0) return false; }
        if (a.BitDahil != null) { var k = SurumKiyasi.Kiyasla(surum, a.BitDahil); if (k == null || k > 0) return false; }
        if (a.BitHaric != null) { var k = SurumKiyasi.Kiyasla(surum, a.BitHaric); if (k == null || k >= 0) return false; }
        return true;
    }

    // Ürün adını KEV kaydıyla karşılaştırmak için sadeleştirir.
    private static string Sade(string s) => Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9]", "");

    // Envanteri değerlendirir.
    public static VulnEslesmeSonucu Esle(IReadOnlyList<InstalledSoftware> yazilimlar, NvdIndeks? nvd, VulnFeedStore? kev, string assetId, double varlikAgirligi, DateTime simdi)
    {
        var bulgular = new List<Finding>(); var notlar = new List<string>(); int eslesen = 0;
        // Hiç veri yoksa açıkça söyle.
        if (nvd == null && kev == null)
        {
            bulgular.Add(BulguFabrikasi.Yap("SW-NOFEED-001", "Yazılım", assetId, "veri", "Zafiyet verisi yüklenmedi", "Kurulu yazılımların bilinen açıklara karşı kontrolü yapılamadı.", "Zafiyet verisi olmadan hangi yazılımın açık içerdiği bilinemez; bu bir 'sorun yok' sonucu DEĞİLDİR.", new[] { $"Kurulu yazılım sayısı: {yazilimlar.Count}" }, 0, varlikAgirligi, new[] { "A.8.8" }, Array.Empty<string>(), "\"KEV Listesini İndir\" (onaylı) ve \"NVD Verisi Yükle\" ile veri ekleyin.", KontrolKimligi, simdi));
            return new VulnEslesmeSonucu(bulgular, notlar, yazilimlar.Count, 0);
        }
        // Sürümü ve CPE'si olmayanlar atlanır, nedeni notlara yazılır.
        foreach (var y in yazilimlar)
        {
            if (y.CandidateCpe == null) { notlar.Add($"{y.Name}: ürün kimliği (CPE) bulunamadı, atlandı."); continue; }
            if (SurumKiyasi.Coz(y.Version) == null) { notlar.Add($"{y.Name}: sürüm bilinmiyor/çözülemedi ({y.Version ?? "boş"}); bulgu üretilmedi."); continue; }
            var p = y.CandidateCpe.Split(':'); string anahtar = NvdIndeks.Anahtar(p[0], p.Length > 1 ? p[1] : "");
            // NVD eşleşmeleri (aynı CVE tek sayılır).
            var cveler = new Dictionary<string, NvdAralik>();
            if (nvd != null && nvd.Urunler.TryGetValue(anahtar, out var aralik)) foreach (var a in aralik) if (Aralikta(y.Version!, a)) cveler.TryAdd(a.Cve, a);
            bool kevli = kev != null && cveler.Keys.Any(kev.KevMi);
            // KEV ürün-adı eşleşmesi (yalnızca NVD verisi bu ürün için yoksa; sürüm doğrulanamaz).
            KevKaydi? kevUrun = null;
            if (cveler.Count == 0 && (nvd == null || !nvd.Urunler.ContainsKey(anahtar)) && kev != null) kevUrun = kev.Kayitlar.Values.FirstOrDefault(k => Sade(k.Product) == Sade(p.Length > 1 ? p[1] : "") && (Sade(k.Vendor).Contains(Sade(p[0])) || Sade(p[0]).Contains(Sade(k.Vendor))));
            double guven = y.CpeConfidence; bool yuksekGuven = guven >= GuvenEsigi;
            if (cveler.Count > 0)
            {
                eslesen++; double enCvss = cveler.Values.Max(a => a.Cvss);
                // Etki: KEV'li ve güvenilir eşleşme en yüksek; güven düşükse üst sınır 4.
                double etki = kevli ? 9 : enCvss >= 9 ? 8 : enCvss >= 7 ? 6 : enCvss >= 4 ? 4 : 2; if (!yuksekGuven) etki = Math.Min(etki, 4);
                string kural = kevli && yuksekGuven ? "SW-KEV-001" : yuksekGuven ? "SW-VULN-001" : "SW-LOWCONF-001";
                var ilk = cveler.Values.OrderByDescending(a => kev != null && kev.KevMi(a.Cve)).ThenByDescending(a => a.Cvss).Take(5).ToList();
                var kanit = new List<string> { $"Yazılım: {y.Name} {y.Version}", $"Ürün kimliği: {y.CandidateCpe} (eşleme güveni {guven:0.00})", $"Eşleşen açık sayısı: {cveler.Count}, en yüksek CVSS: {enCvss:0.0}" };
                kanit.AddRange(ilk.Select(a => $"{a.Cve} (CVSS {a.Cvss:0.0}){(kev != null && kev.KevMi(a.Cve) ? " — KEV: sömürüldüğü biliniyor" : "")}: {a.Aciklama}"));
                if (nvd != null) kanit.Add($"Veri: {nvd.Ozetle()}"); if (kev != null) kanit.Add($"Veri: {kev.Ozetle()}");
                string baslik = (kevli ? $"{y.Name} {y.Version}: sömürüldüğü bilinen açık içeriyor" : $"{y.Name} {y.Version}: bilinen {cveler.Count} açık içeriyor") + (yuksekGuven ? "" : " (düşük güven)");
                bulgular.Add(BulguFabrikasi.Yap(kural, "Yazılım", assetId, y.Name, baslik, $"Kurulu sürüm, NVD'deki {cveler.Count} açığın etkilediği sürüm aralığında." + (kevli ? " Bunlardan en az biri CISA KEV listesinde (sahada sömürüldüğü bilinir)." : ""),
                    "Güncellenmemiş yazılımdaki bilinen açıklar saldırganlarca kullanılabilir." + (yuksekGuven ? "" : " NOT: ürün eşlemesi tahmine dayalı (düşük güven); önce yazılımın gerçekten bu ürün olup olmadığını doğrulayın."),
                    kanit.ToArray(), etki, varlikAgirligi, new[] { "A.8.8", "A.8.19" }, new[] { "T1190", "T1203" }, $"\"{y.Name}\" yazılımını üreticisinin en son güvenli sürümüne güncelleyin; kullanılmıyorsa kaldırın.", KontrolKimligi, simdi));
            }
            else if (kevUrun != null)
            {
                eslesen++;
                bulgular.Add(BulguFabrikasi.Yap("SW-KEVPRODUCT-001", "Yazılım", assetId, y.Name, $"{y.Name}: ürün KEV listesinde yer alıyor (sürüm doğrulanamadı)", "Bu ürünün bazı sürümlerinde sahada sömürülen açık bulunuyor; sizin sürümünüzün etkilenip etkilenmediği KEV verisinden anlaşılamaz.", "Sürümünüz etkileniyorsa saldırganlar açığı hâlihazırda kullanıyor olabilir.",
                    new[] { $"Yazılım: {y.Name} {y.Version}", $"KEV kaydı: {kevUrun.Cve} — {kevUrun.Vendor} {kevUrun.Product}: {kevUrun.Ad}", "Sürüm aralığı bilgisi için NVD verisi yüklenmeli.", $"Veri: {kev!.Ozetle()}" }, 3, varlikAgirligi, new[] { "A.8.8" }, Array.Empty<string>(), $"\"{y.Name}\" yazılımının sürümünün {kevUrun.Cve} açığından etkilenip etkilenmediğini üreticinin duyurusundan kontrol edin; NVD verisi yüklerseniz bu kontrol otomatikleşir.", KontrolKimligi, simdi));
            }
        }
        return new VulnEslesmeSonucu(bulgular.OrderByDescending(b => b.Score).ToList(), notlar, yazilimlar.Count, eslesen);
    }
}

// Yazılım açığı bulguları için doğrulama kontrolü: yazılım güncellenince bulgu artık üretilmez.
internal sealed class YazilimDogrulamaKontrolu : IVerificationCheck
{
    private readonly Func<List<Finding>> _yenidenTopla;
    public YazilimDogrulamaKontrolu(Func<List<Finding>> yenidenTopla) { _yenidenTopla = yenidenTopla; }
    public string Id => VulnMatcher.KontrolKimligi;
    public Task<IReadOnlyDictionary<string, VerificationOutcome>> RunAsync(IReadOnlyList<Finding> referans, CancellationToken ct)
    {
        var simdiki = _yenidenTopla().Select(x => x.Id).ToHashSet();
        IReadOnlyDictionary<string, VerificationOutcome> sonuc = referans.ToDictionary(f => f.Id, f => simdiki.Contains(f.Id) ? VerificationOutcome.Fail : VerificationOutcome.Pass);
        return Task.FromResult(sonuc);
    }
}
