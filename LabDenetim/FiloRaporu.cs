// Filo raporu: bir klasördeki "--tara" özet dosyalarını (makine başına JSON) okuyup TEK bir toplu rapor üretir.
// Sunucu gerekmez; veri hiçbir yere gönderilmez. Rapor, BT sorumlusunun "50 bilgisayarın durumu nedir?" sorusuna cevap verir:
// makine başına puan ve değişim, en sık sorunlar, KVKK/ISO özeti, eski/eksik taramalar.
// GÜVENLİK: Klasördeki JSON dosyaları GÜVENİLMEZ girdi sayılır (paylaşılan klasördür): boyut/adet sınırı, şema kontrolü,
// gelecek tarih düzeltmesi ve raporda her metnin HTML'e çevrilmesi (betik enjeksiyonu olmasın) uygulanır. Hiçbir dosya silinmez/değiştirilmez.
using System.Net;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Raporda bir makinenin satırı: en son tarama, varsa önceki tarama ve karşılaştırma.
internal sealed record FiloMakine(TaramaOzeti Son, TaramaOzeti? Onceki, int Gun, bool Eski, int? Fark);

// Birden çok makinede görülen ortak sorun.
internal sealed record FiloGrup(string Baslik, string Modul, Onem EnYuksek, List<string> Makineler, string Oneri);

// Filo raporunun hesaplanmış verisi.
internal sealed record FiloSonucu(DateTime ZamanUtc, int EskiGun, List<FiloMakine> Makineler, List<FiloGrup> EnSik, Dictionary<string, int> Kvkk, Dictionary<string, int> Iso, List<string> Uyarilar, double Ortalama, int Iyi, int Dikkat, int Acil);

// Filo raporu üretici.
internal static class FiloRaporu
{
    // Tek özet dosyası için en büyük boyut ve bir klasörden okunacak en çok dosya sayısı.
    public const long EnBuyukDosya = 5 * 1024 * 1024; public const int EnCokDosya = 5000;
    // Bir özetten alınacak en çok bulgu ve metin uzunlukları (kötü niyetli/bozuk dosyalar raporu şişirmesin).
    private const int EnCokBulgu = 2000, EnUzunMetin = 600, EnUzunAd = 100;
    // JSON okuma ayarı.
    private static readonly JsonSerializerOptions Oku_ = new() { PropertyNameCaseInsensitive = true };

    // Metni güvenli uzunluğa getirir.
    private static string Kes(string? s, int n) { s ??= ""; return s.Length > n ? s[..n] : s; }

    // ---------- Okuma ----------

    // Klasördeki özet dosyalarını okur. Geçersiz/okunamayan/fazla büyük dosyalar atlanır ve nedeni uyarılara yazılır.
    public static List<TaramaOzeti> Oku(string klasor, DateTime simdiUtc, List<string> uyarilar)
    {
        // Sonuç.
        var l = new List<TaramaOzeti>();
        // Klasör yoksa hata.
        if (!Directory.Exists(klasor)) throw new DirectoryNotFoundException("Klasör bulunamadı: " + klasor);
        // Dosyalar (alt klasörlere inilmez), adete sınır.
        var dosyalar = Directory.EnumerateFiles(klasor, "*.json", SearchOption.TopDirectoryOnly).Take(EnCokDosya + 1).ToList();
        if (dosyalar.Count > EnCokDosya) { uyarilar.Add($"Klasörde {EnCokDosya}'den fazla .json dosyası var; yalnızca ilk {EnCokDosya} tanesi okundu."); dosyalar = dosyalar.Take(EnCokDosya).ToList(); }
        // Her dosya.
        foreach (var d in dosyalar)
        {
            // Hata kontrolü: tek dosyanın bozukluğu raporu durdurmaz.
            try
            {
                // Çok büyük dosya.
                var fi = new FileInfo(d); string ad = fi.Name; if (fi.Length > EnBuyukDosya) { uyarilar.Add($"{ad}: dosya çok büyük, atlandı."); continue; }
                // Çöz.
                var o = JsonSerializer.Deserialize<TaramaOzeti>(File.ReadAllBytes(d), Oku_);
                // Tarama özeti değil.
                if (o == null || string.IsNullOrWhiteSpace(o.Makine) || o.Format <= 0) { uyarilar.Add($"{ad}: BulutSoft tarama özeti değil, atlandı."); continue; }
                // Daha yeni şema.
                if (o.Format > TaramaCalistirici.FormatSurumu) { uyarilar.Add($"{ad}: daha yeni bir program sürümünün dosyası (şema {o.Format}); bu sürüm okuyamıyor, atlandı."); continue; }
                // Alanları güvenli hale getir.
                var bulgular = (o.Bulgular ?? new()).Take(EnCokBulgu).Select(b => new OzetBulgu(Kes(b.Modul, EnUzunAd), Kes(b.Baslik, 200), Kes(b.Onem, 20), Kes(b.Detay, EnUzunMetin + 1), Kes(b.Oneri, EnUzunMetin + 1))).ToList();
                // Gelecekteki tarih: sahte/yanlış saat; "şimdi"ye çekilir ve uyarılır.
                var zaman = o.ZamanUtc; if (zaman > simdiUtc.AddDays(1)) { uyarilar.Add($"{ad}: tarama tarihi gelecekte görünüyor ({o.ZamanUtc:yyyy-MM-dd}); bilgisayar saati yanlış olabilir."); zaman = simdiUtc; }
                // Düzeltilmiş kayıt.
                l.Add(o with { Makine = Kes(o.Makine.Trim(), EnUzunAd), Surum = Kes(o.Surum, 20), Seviye = Kes(o.Seviye, 40), Puan = Math.Clamp(o.Puan, 0, 100), ZamanUtc = zaman, Bulgular = bulgular, Yuksek = Math.Max(0, o.Yuksek), Orta = Math.Max(0, o.Orta), Dusuk = Math.Max(0, o.Dusuk), Bilgi = Math.Max(0, o.Bilgi), Atlanan = Math.Max(0, o.Atlanan), Lisans = LisansKodu(o.Lisans), LisansKalanGun = o.LisansKalanGun is int g ? Math.Clamp(g, 0, 36500) : null });
            }
            // Okunamadı.
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { uyarilar.Add($"{Path.GetFileName(d)}: okunamadı ({ex.GetType().Name}), atlandı."); }
        }
        // Sonuç.
        return l;
    }

    // Güvenilmeyen dosyadaki lisans kodu yalnızca bilinen değerlerden biri olabilir (başka metin raporlara girmez).
    private static readonly string[] LisansKodlari = { "Sahip", "Lisansli", "Deneme", "DenemeBitti", "LisansSuresiDoldu", "Gecersiz" };
    public static string? LisansKodu(string? k) => LisansKodlari.FirstOrDefault(x => string.Equals(x, k, StringComparison.Ordinal));
    // Raporda görünen Türkçe lisans metni (bilinmiyorsa "—": eski sürümün özeti).
    public static string LisansMetni(TaramaOzeti o) => o.Lisans switch
    {
        "Sahip" => "Sahip", "Lisansli" => o.LisansKalanGun is int a ? $"Lisanslı ({a} gün)" : "Lisanslı", "Deneme" => o.LisansKalanGun is int d ? $"Deneme ({d} gün)" : "Deneme",
        "DenemeBitti" => "Deneme bitti", "LisansSuresiDoldu" => "Süresi doldu", "Gecersiz" => "Geçersiz", _ => "—"
    };
    // Lisansı sorunlu mu? (deneme bitti, süre doldu, geçersiz)
    public static bool LisansSorunlu(TaramaOzeti o) => o.Lisans is "DenemeBitti" or "LisansSuresiDoldu" or "Gecersiz";

    // ---------- Hesaplama (saf mantık) ----------

    // Önem metnini çözer; bilinmiyorsa Bilgi.
    private static Onem OnemCoz(string s) => Enum.TryParse<Onem>(s, true, out var o) ? o : Onem.Bilgi;
    // Ortak sorun anahtarı: "Tırnaksız hizmet yolu: X" ve "...: Y" aynı sorun sayılsın diye ":" öncesi alınır.
    public static string Anahtar(string baslik) { int i = baslik.IndexOf(':'); return (i > 8 ? baslik[..i] : baslik).Trim(); }

    // Okunan özetlerden rapor verisini hesaplar.
    public static FiloSonucu Hesapla(List<TaramaOzeti> ozetler, DateTime simdiUtc, int eskiGun, List<string> uyarilar)
    {
        // Makine başına en yeni ve önceki tarama (ad büyük/küçük harf duyarsız).
        var makineler = ozetler.GroupBy(o => o.Makine, StringComparer.OrdinalIgnoreCase).Select(g => { var s = g.OrderByDescending(x => x.ZamanUtc).ToList(); var son = s[0]; var once = s.Count > 1 ? s[1] : null; int gun = (int)Math.Max(0, (simdiUtc - son.ZamanUtc).TotalDays); return new FiloMakine(son, once, gun, gun > eskiGun, once == null ? null : son.Puan - once.Puan); }).OrderBy(m => m.Son.Puan).ThenBy(m => m.Son.Makine, StringComparer.OrdinalIgnoreCase).ToList();
        // Uyarılar: kapsam ve güncellik.
        int yoneticiDegil = makineler.Count(m => !m.Son.Yonetici), atlananli = makineler.Count(m => m.Son.Atlanan > 0), eski = makineler.Count(m => m.Eski);
        if (yoneticiDegil > 0) uyarilar.Add($"{yoneticiDegil} makine yönetici olarak taranmamış; bazı kontroller eksik kalmış olabilir ve puanlar gerçekte daha düşük olabilir.");
        int lisansSorunlu = makineler.Count(m => LisansSorunlu(m.Son)); if (lisansSorunlu > 0) uyarilar.Add($"{lisansSorunlu} makinede lisans yok, geçersiz ya da süresi dolmuş: {string.Join(", ", makineler.Where(m => LisansSorunlu(m.Son)).Select(m => m.Son.Makine).Take(10))}{(lisansSorunlu > 10 ? " …" : "")}");
        if (atlananli > 0) uyarilar.Add($"{atlananli} makinede bazı kontroller çalıştırılamamış (ayrıntı makine bölümünde).");
        if (eski > 0) uyarilar.Add($"{eski} makinenin son taraması {eskiGun} günden eski: {string.Join(", ", makineler.Where(m => m.Eski).Select(m => m.Son.Makine).Take(10))}{(eski > 10 ? " …" : "")}");
        // Ortak sorunlar: Bilgi ve çalıştırılamayan kontroller hariç; makine sayısına göre.
        var grup = new Dictionary<string, (string Modul, Onem En, string Oneri, HashSet<string> M)>();
        foreach (var m in makineler) foreach (var b in m.Son.Bulgular)
        {
            var on = OnemCoz(b.Onem); if (on == Onem.Bilgi || b.Baslik == "Bu kontrol çalıştırılamadı") continue;
            string k = Anahtar(b.Baslik); if (!grup.TryGetValue(k, out var g)) g = (b.Modul, on, b.Oneri, new(StringComparer.OrdinalIgnoreCase)); if (on > g.En) g = (g.Modul, on, g.Oneri, g.M); g.M.Add(m.Son.Makine); grup[k] = g;
        }
        var ensik = grup.Select(p => new FiloGrup(p.Key, p.Value.Modul, p.Value.En, p.Value.M.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), p.Value.Oneri)).OrderByDescending(g => g.Makineler.Count).ThenByDescending(g => g.EnYuksek).ThenBy(g => g.Baslik, StringComparer.Ordinal).Take(15).ToList();
        // Uyumluluk: her madde kaç makinede (makine başına bir kez sayılır).
        var kvkk = new Dictionary<string, int>(); var iso = new Dictionary<string, int>();
        foreach (var m in makineler)
        {
            var k = new HashSet<string>(); var i = new HashSet<string>();
            foreach (var b in m.Son.Bulgular) { var e = Uyumluluk.Bul(new Bulgu(b.Modul, b.Baslik, OnemCoz(b.Onem), b.Detay, b.Oneri)); if (e == null) continue; k.Add(e.KvkkKisa); foreach (var c in e.IsoKodlari) i.Add(c); }
            foreach (var x in k) kvkk[x] = kvkk.GetValueOrDefault(x) + 1; foreach (var x in i) iso[x] = iso.GetValueOrDefault(x) + 1;
        }
        // Dağılım ve ortalama (arayüzdeki seviye eşikleriyle aynı).
        int iyi = makineler.Count(m => m.Son.Puan >= 80), dikkat = makineler.Count(m => m.Son.Puan is >= 50 and < 80), acil = makineler.Count(m => m.Son.Puan < 50);
        double ort = makineler.Count == 0 ? 0 : Math.Round(makineler.Average(m => (double)m.Son.Puan), 1);
        // Sonuç.
        return new FiloSonucu(simdiUtc, eskiGun, makineler, ensik, kvkk, iso, uyarilar, ort, iyi, dikkat, acil);
    }

    // ---------- Çıktılar ----------

    // HTML'e güvenli metin.
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    // Puan rengi.
    private static string Renk(int p) => p >= 80 ? "#15803d" : p >= 50 ? "#b45309" : "#b91c1c";

    // Tek dosyalık HTML rapor (yazdırılınca PDF yapılabilir). Dış kaynak, betik veya bağlantı içermez.
    public static string Html(FiloSonucu s)
    {
        // Metin kurucu.
        var h = new StringBuilder();
        h.Append("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\"><title>Filo Güvenlik Raporu</title><style>");
        h.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#0f172a;background:#fff}h1{margin:0 0 4px}h2{margin-top:28px;border-bottom:2px solid #0369a1;padding-bottom:4px;color:#0369a1}");
        h.Append("table{border-collapse:collapse;width:100%;font-size:13px}th,td{border:1px solid #cbd5e1;padding:6px 8px;text-align:left;vertical-align:top}th{background:#e0f2fe}");
        h.Append(".kart{display:inline-block;min-width:150px;margin:0 12px 12px 0;padding:10px 14px;border:1px solid #cbd5e1;border-radius:8px}.kart b{display:block;font-size:24px}.uyari{background:#fef3c7;border:1px solid #f59e0b;padding:6px 10px;margin:4px 0;border-radius:6px}.soluk{color:#64748b;font-size:12px}");
        h.Append("details{margin:6px 0}summary{cursor:pointer;font-weight:600}@media print{details{display:block}}</style></head><body>");
        h.Append($"<h1>Filo Güvenlik Raporu</h1><div class=\"soluk\">Oluşturulma: {s.ZamanUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {s.Makineler.Count} makine</div>");
        // Özet kartları.
        h.Append("<h2>Genel Özet</h2>");
        h.Append($"<div class=\"kart\">Ortalama puan<b style=\"color:{Renk((int)s.Ortalama)}\">{s.Ortalama:0.#} / 100</b></div><div class=\"kart\">İyi (80+)<b style=\"color:#15803d\">{s.Iyi}</b></div><div class=\"kart\">Dikkat (50-79)<b style=\"color:#b45309\">{s.Dikkat}</b></div><div class=\"kart\">Acil (&lt;50)<b style=\"color:#b91c1c\">{s.Acil}</b></div>");
        // Uyarılar.
        foreach (var u in s.Uyarilar) h.Append("<div class=\"uyari\">" + E(u) + "</div>");
        // Makine tablosu.
        h.Append("<h2>Makineler (en düşük puandan)</h2><table><tr><th>Makine</th><th>Tarama</th><th>Puan</th><th>Değişim</th><th>Yüksek</th><th>Orta</th><th>Düşük</th><th>Atlanan</th><th>Yönetici</th><th>Lisans</th></tr>");
        foreach (var m in s.Makineler)
        {
            var o = m.Son; string fark = m.Fark == null ? "—" : m.Fark > 0 ? "▲ +" + m.Fark : m.Fark < 0 ? "▼ " + m.Fark : "=";
            h.Append($"<tr><td>{E(o.Makine)}</td><td>{o.ZamanUtc.ToLocalTime():yyyy-MM-dd}{(m.Eski ? " <b>(eski)</b>" : "")}</td><td style=\"color:{Renk(o.Puan)};font-weight:700\">{o.Puan}</td><td>{fark}</td><td>{o.Yuksek}</td><td>{o.Orta}</td><td>{o.Dusuk}</td><td>{o.Atlanan}</td><td>{(o.Yonetici ? "evet" : "hayır")}</td><td>{E(LisansMetni(o))}</td></tr>");
        }
        h.Append("</table>");
        // Ortak sorunlar.
        h.Append("<h2>En Sık Görülen Sorunlar</h2>");
        if (s.EnSik.Count == 0) h.Append("<p>Hiçbir makinede dikkat çeken sorun bulunmadı.</p>");
        else
        {
            h.Append("<table><tr><th>Sorun</th><th>Önem</th><th>Makine sayısı</th><th>Makineler</th><th>Ne yapmalı?</th></tr>");
            foreach (var g in s.EnSik) h.Append($"<tr><td>{E(g.Baslik)}<div class=\"soluk\">{E(g.Modul)}</div></td><td>{E(g.EnYuksek.ToString())}</td><td>{g.Makineler.Count}</td><td>{E(string.Join(", ", g.Makineler.Take(8)))}{(g.Makineler.Count > 8 ? " …" : "")}</td><td>{E(Ozet(g.Oneri))}</td></tr>");
            h.Append("</table>");
        }
        // Uyumluluk.
        h.Append("<h2>KVKK ve ISO/IEC 27001 Özeti</h2>");
        if (s.Kvkk.Count + s.Iso.Count == 0) h.Append("<p>Eşleşen madde yok.</p>");
        else
        {
            h.Append("<table><tr><th>Madde / Kontrol</th><th>Etkilenen makine</th></tr>");
            foreach (var p in s.Kvkk.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)) h.Append($"<tr><td>{E(p.Key)}</td><td>{p.Value}</td></tr>");
            foreach (var p in s.Iso.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)) h.Append($"<tr><td>ISO/IEC 27001: {E(p.Key)}{(Uyumluluk.Iso.TryGetValue(p.Key, out var iso) ? " — " + E(iso.Ad) : "")}</td><td>{p.Value}</td></tr>");
            h.Append("</table>");
        }
        h.Append("<p class=\"soluk\">" + E(Uyumluluk.Not) + "</p>");
        // Makine ayrıntıları.
        h.Append("<h2>Makine Ayrıntıları</h2>");
        foreach (var m in s.Makineler)
        {
            var o = m.Son; var onemli = o.Bulgular.Where(b => OnemCoz(b.Onem) >= Onem.Orta && b.Baslik != "Bu kontrol çalıştırılamadı").OrderByDescending(b => OnemCoz(b.Onem)).Take(25).ToList();
            h.Append($"<details><summary>{E(o.Makine)} — {o.Puan}/100 ({E(o.Seviye)}) · sürüm {E(o.Surum)}</summary>");
            if (onemli.Count == 0) h.Append("<p>Orta veya yüksek önemli sorun yok.</p>"); else { h.Append("<table><tr><th>Önem</th><th>Sorun</th><th>Öneri</th></tr>"); foreach (var b in onemli) h.Append($"<tr><td>{E(b.Onem)}</td><td>{E(b.Baslik)}</td><td>{E(Ozet(b.Oneri))}</td></tr>"); h.Append("</table>"); }
            var atl = o.Bulgular.Count(b => b.Baslik == "Bu kontrol çalıştırılamadı"); if (atl > 0) h.Append($"<p class=\"soluk\">{atl} kontrol çalıştırılamadı.</p>");
            h.Append("</details>");
        }
        h.Append("<p class=\"soluk\">Bu rapor yerel tarama özetlerinden üretilmiştir; veri hiçbir yere gönderilmemiştir. Puanlar rehber niteliğindedir.</p></body></html>");
        // Sonuç.
        return h.ToString();
    }

    // Öneri metninin ilk cümlesi (komut satırı örnekleri çıkarılır).
    private static string Ozet(string? oneri) => KarneHesabi.Yapilacak(oneri);

    // CSV hücresi: noktalı virgül/tırnak kaçışı + Excel formül enjeksiyonuna karşı başına ' konur.
    private static string Hucre(string s) { s ??= ""; if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s; return s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }

    // Makine başına özet CSV (Excel Türkçe: BOM'lu UTF-8, ayraç noktalı virgül — BOM yazarken eklenir).
    public static string Csv(FiloSonucu s)
    {
        // Başlık satırı.
        var sb = new StringBuilder("Makine;Tarama (UTC);Puan;Seviye;Değişim;Yüksek;Orta;Düşük;Bilgi;Atlanan;Yönetici;Eski tarama;Sürüm;Lisans\r\n");
        // Her makine.
        foreach (var m in s.Makineler) { var o = m.Son; sb.Append(string.Join(';', new[] { Hucre(o.Makine), o.ZamanUtc.ToString("yyyy-MM-dd HH:mm"), o.Puan.ToString(), Hucre(o.Seviye), m.Fark?.ToString() ?? "", o.Yuksek.ToString(), o.Orta.ToString(), o.Dusuk.ToString(), o.Bilgi.ToString(), o.Atlanan.ToString(), o.Yonetici ? "evet" : "hayır", m.Eski ? "evet" : "hayır", Hucre(o.Surum), Hucre(LisansMetni(o)) })).Append("\r\n"); }
        // Sonuç.
        return sb.ToString();
    }

    // ---------- Giriş ----------

    // Klasörü okuyup rapor dosyalarını (HTML + CSV) yazar. cikti: .html dosya yolu ya da klasör (verilmezse özet klasörünün içi).
    // Dönüş: (çıkış kodu, mesaj). Yalnızca rapor dosyaları yazılır; özet dosyalarına dokunulmaz.
    public static (int Kod, string Mesaj) Calistir(string klasor, string? cikti, DateTime? simdiUtc = null, int eskiGun = 30)
    {
        // Hata kontrolü.
        try
        {
            // Zaman, okuma, hesap.
            var simdi = simdiUtc ?? DateTime.UtcNow; var uyari = new List<string>(); var ozetler = Oku(klasor, simdi, uyari);
            // Hiç geçerli özet yoksa anlamlı rapor olmaz.
            if (ozetler.Count == 0) return (TaramaCalistirici.Hata, "Klasörde geçerli bir tarama özeti bulunamadı." + (uyari.Count > 0 ? " " + uyari[0] : ""));
            // Hesapla.
            var sonuc = Hesapla(ozetler, simdi, eskiGun, uyari);
            // Dosya yolları.
            string html = cikti != null && cikti.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(cikti) : Path.Combine(Path.GetFullPath(string.IsNullOrWhiteSpace(cikti) ? klasor : cikti), "filo_raporu_" + simdi.ToString("yyyyMMdd_HHmmss") + ".html");
            string csv = Path.ChangeExtension(html, ".csv");
            // Yaz (önce geçici dosya).
            Directory.CreateDirectory(Path.GetDirectoryName(html)!);
            foreach (var (yol, icerik, bom) in new[] { (html, Html(sonuc), false), (csv, Csv(sonuc), true) }) { string g = yol + ".tmp"; File.WriteAllText(g, icerik, new UTF8Encoding(bom)); File.Move(g, yol, true); }
            // Başarı.
            return (TaramaCalistirici.Tamam, $"{html}  ({sonuc.Makineler.Count} makine, ortalama {sonuc.Ortalama:0.#}; {uyari.Count} uyarı)");
        }
        catch (Exception ex) { return (TaramaCalistirici.Hata, "Rapor üretilemedi: " + ex.Message); }
    }
}
