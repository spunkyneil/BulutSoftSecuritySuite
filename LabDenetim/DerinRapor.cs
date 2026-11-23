// Bu dosya Derin Denetim Raporu'nu (HTML, JSON ve PDF) üretir; PDF çizimi arayüz projesindeki RaporUretici'yi kullanır.
// Bulguları eski biçime çeviren V2Kayit BulutSoft.Core içindedir (RaporlamaV2.cs).
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

// V2 ad alanı.
namespace LabDenetim.V2;

// Derin Denetim Raporu'nun girdisi.
internal sealed record DerinRaporGirdisi(string Makine, string Hazirlayan, DateTime Zaman, List<Finding> Bulgular, FpDeposu Fp, KbCatalog? Katalog, NvdIndeks? Nvd, VulnFeedStore? Kev, AuditTrail? Iz = null);

// Rapor üretici.
internal static class DerinRapor
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string OnemAdi(Onem o) => o switch { Onem.Yuksek => "Yüksek", Onem.Orta => "Orta", Onem.Dusuk => "Düşük", _ => "Bilgi" };
    private static string Renk(Onem o) => o switch { Onem.Yuksek => "#dc2626", Onem.Orta => "#f59e0b", Onem.Dusuk => "#0ea5e9", _ => "#94a3b8" };

    // Raporda sayılan (yanlış alarm olmayan) bulgular.
    public static List<Finding> Gecerli(IEnumerable<Finding> l) => l.Where(f => f.State != FindingState.FalsePositive).OrderByDescending(f => f.Score).ToList();

    // Uyumluluk özeti: ISO kontrolü → bulgu sayısı (yalnızca önemli bulgular: skor > 0).
    public static List<(string Kod, string Ad, int Adet)> IsoOzeti(IEnumerable<Finding> l)
        => l.Where(f => f.Score > 0).SelectMany(f => f.Compliance.Iso27001.Distinct()).GroupBy(c => c).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => (g.Key, Uyumluluk.Iso.TryGetValue(g.Key, out var v) ? v.Ad : "", g.Count())).ToList();

    // HTML rapor.
    public static string Html(DerinRaporGirdisi g)
    {
        var tum = g.Bulgular; var l = Gecerli(tum); var fpl = tum.Where(f => f.State == FindingState.FalsePositive).ToList(); var onemli = l.Where(f => f.Score > 0).ToList();
        int y = onemli.Count(f => f.EskiOnem == Onem.Yuksek), o = onemli.Count(f => f.EskiOnem == Onem.Orta), d = onemli.Count(f => f.EskiOnem == Onem.Dusuk), b = l.Count - onemli.Count + onemli.Count(f => f.EskiOnem == Onem.Bilgi);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Derin Denetim Raporu</title><style>body{font-family:'Segoe UI',Arial,sans-serif;max-width:900px;margin:28px auto;color:#0f172a;line-height:1.5;padding:0 16px;font-size:13.5px}h1{font-size:23px;border-bottom:3px solid #0284c7;padding-bottom:8px;margin-bottom:4px}h2{font-size:16px;margin-top:26px;color:#0c4a6e;border-bottom:1px solid #cbd5e1;padding-bottom:3px}.m{color:#64748b;font-size:12px}.k{display:flex;gap:10px;flex-wrap:wrap;margin:10px 0}.k div{flex:1;min-width:110px;border:1px solid #e2e8f0;border-radius:8px;padding:8px 12px;text-align:center}.k b{display:block;font-size:24px}table{width:100%;border-collapse:collapse;font-size:12px;margin:6px 0}td,th{border-bottom:1px solid #e2e8f0;padding:4px 6px;text-align:left;vertical-align:top}th{background:#f1f5f9}.b{border:1px solid #e2e8f0;border-left:5px solid #94a3b8;border-radius:6px;padding:9px 13px;margin:11px 0;page-break-inside:avoid}.uyari{background:#fffbeb;border-left:4px solid #f59e0b;padding:6px 12px;margin:8px 0}pre{background:#f1f5f9;padding:6px;border-radius:4px;white-space:pre-wrap;font-size:12px}@media print{body{margin:10mm}}</style></head><body>");
        sb.Append($"<h1>Derin Denetim Raporu</h1><p class=\"m\">Cihaz: {E(g.Makine)} · Tarih: {g.Zaman.ToLocalTime():dd.MM.yyyy HH:mm} · Hazırlayan: {E(g.Hazirlayan)} · Gizli — yönetim ve BT kullanımı</p>");
        // Özet.
        sb.Append($"<h2>Özet</h2><div class=\"k\"><div style=\"border-top:4px solid #dc2626\"><b>{y}</b>Yüksek</div><div style=\"border-top:4px solid #f59e0b\"><b>{o}</b>Orta</div><div style=\"border-top:4px solid #0ea5e9\"><b>{d}</b>Düşük</div><div style=\"border-top:4px solid #94a3b8\"><b>{b}</b>Bilgi</div></div>");
        sb.Append($"<p>{l.Count} bulgu listelendi" + (fpl.Count > 0 ? $"; {fpl.Count} bulgu kullanıcı tarafından <b>yanlış alarm</b> olarak işaretlendiği için sayıma katılmadı (aşağıda ayrıca listelenir)." : ".") + "</p>");
        var modul = l.GroupBy(f => f.Module).OrderByDescending(x => x.Count()); sb.Append("<table><tr><th>Modül</th><th>Bulgu</th><th>En yüksek skor</th></tr>" + string.Concat(modul.Select(m => $"<tr><td>{E(m.Key)}</td><td>{m.Count()}</td><td>{m.Max(f => f.Score)}</td></tr>")) + "</table>");
        // Veri kaynakları.
        sb.Append("<h2>Veri Kaynakları ve Sürümleri</h2><p class=\"m\">Bulguların dayandığı verilerin sürüm ve özet değerleri (sonuçların yeniden üretilebilmesi için).</p><table><tr><th>Kaynak</th><th>Durum</th></tr>");
        sb.Append($"<tr><td>Yama kataloğu (KB)</td><td>{(g.Katalog == null ? "<b>Yüklü değil</b> — eksik güncellemeler değerlendirilemedi" : $"Sürüm {E(g.Katalog.Surum)} · SHA-256 {g.Katalog.Ozet[..16]}…")}</td></tr>");
        sb.Append($"<tr><td>NVD zafiyet indeksi</td><td>{(g.Nvd == null ? "<b>Yüklü değil</b> — yazılım açıkları kesin değerlendirilemedi" : E(g.Nvd.Ozetle()) + $" · içe aktarma {E(g.Nvd.Zaman)}")}</td></tr>");
        sb.Append($"<tr><td>CISA KEV listesi</td><td>{(g.Kev == null ? "<b>Yüklü değil</b>" : E(g.Kev.Ozetle()) + (VulnFeedStore.Meta() is { } me ? $" · indirme {E(me.Zaman)}" : ""))}</td></tr></table>");
        // Uyumluluk.
        var iso = IsoOzeti(l); var kvkk = onemli.SelectMany(f => f.Compliance.Kvkk.Distinct()).GroupBy(x => x).Select(x => (x.Key, x.Count())).ToList(); var mitre = onemli.SelectMany(f => f.Compliance.MitreAttack.Distinct()).GroupBy(x => x).OrderByDescending(x => x.Count()).Take(8).ToList();
        sb.Append("<h2>KVKK, ISO/IEC 27001 ve MITRE ATT&amp;CK Görünümü</h2>");
        if (iso.Count == 0 && kvkk.Count == 0) sb.Append("<p class=\"m\">İlişkilendirilen bulgu yok.</p>"); else
        {
            sb.Append("<table><tr><th style=\"width:90px\">Kontrol</th><th>Ad</th><th style=\"width:60px\">Bulgu</th></tr>" + string.Concat(kvkk.Select(k => $"<tr><td><b>{E(k.Key)}</b></td><td>Veri güvenliği yükümlülükleri</td><td>{k.Item2}</td></tr>")) + string.Concat(iso.Take(12).Select(i => $"<tr><td><b>ISO {E(i.Kod)}</b></td><td>{E(i.Ad)}</td><td>{i.Adet}</td></tr>")) + "</table>");
            if (mitre.Count > 0) sb.Append("<p class=\"m\">MITRE ATT&amp;CK teknikleri (" + E(Mitre.Surum) + "): " + string.Join(" · ", mitre.Select(m => E(m.Key) + " (" + m.Count() + ")")) + "</p>");
        }
        sb.Append($"<div class=\"m\">{E(Uyumluluk.Not)}</div>");
        // Bulgular.
        sb.Append("<h2>Bulgular</h2>"); int n = 0;
        foreach (var f in l)
        {
            var onem = f.EskiOnem;
            sb.Append($"<div class=\"b\" style=\"border-left-color:{Renk(onem)}\"><b>{++n}. {E(f.Title)}</b> <span class=\"m\">· {OnemAdi(onem)} · skor {f.Score}/100 · {E(f.RuleId)} · varlık {E(f.AssetId)}</span><br><b>Ne bulundu:</b> {E(f.Explanation.What)}<br><b>Neden önemli:</b> {E(f.Explanation.WhyItMatters)}");
            if (f.Evidence.Length > 0) sb.Append("<br><b>Kanıt:</b><pre>" + E(string.Join("\n", f.Evidence.Take(8))) + "</pre>");
            sb.Append($"<b>Çözüm:</b> {E(f.Remediation.Summary)}");
            sb.Append($"<div class=\"m\">Düzeltme türü: {(f.Remediation.Type == RemediationType.Manual ? "elle" : f.Remediation.Type == RemediationType.Assisted ? "destekli" : "otomatik")} · Doğrulama kontrolü: {E(f.Remediation.VerificationCheckId)} · Durum: {DurumAdi(f.State)}");
            if (f.FalsePositiveTracking.State == FpState.Suspected) sb.Append(" · <b>⚠ Bu kuralın yanlış alarm oranı yüksek; puan düşürüldü, sonucu elle doğrulayın.</b>");
            sb.Append($"<br>Uyumluluk: {E(string.Join(", ", f.Compliance.Kvkk))} · ISO {E(string.Join(", ", f.Compliance.Iso27001))}{(f.Compliance.MitreAttack.Length > 0 ? " · MITRE " + E(string.Join(", ", f.Compliance.MitreAttack)) : "")}</div></div>");
        }
        if (l.Count == 0) sb.Append("<p>Listelenecek bulgu yok.</p>");
        // Yanlış alarm ve kural kalitesi.
        sb.Append("<h2>Yanlış Alarm ve Kural Kalitesi</h2>");
        if (fpl.Count > 0) sb.Append("<p>Yanlış alarm olarak işaretlenenler:</p><table><tr><th>Bulgu</th><th>İşaretleyen</th><th>Tarih</th><th>Neden</th></tr>" + string.Concat(fpl.Select(f => $"<tr><td>{E(f.Title)}</td><td>{E(f.FalsePositiveTracking.MarkedBy)}</td><td>{f.FalsePositiveTracking.MarkedAt?.ToLocalTime():dd.MM.yyyy}</td><td>{E(f.FalsePositiveTracking.Reason)}</td></tr>")) + "</table>");
        var kal = g.Fp.KuralKalitesi(); if (kal.Count == 0) sb.Append("<p class=\"m\">Henüz kural istatistiği yok.</p>"); else sb.Append("<table><tr><th>Kural</th><th>Bulgu</th><th>Yanlış alarm</th><th>Oran</th><th>Durum</th></tr>" + string.Concat(kal.Take(25).Select(k => $"<tr><td>{E(k.Kural)}</td><td>{k.Toplam}</td><td>{k.Fp}</td><td>%{k.Oran * 100:0}</td><td>{E(k.Durum)}</td></tr>")) + "</table><div class=\"m\">Oranı %30'u aşan (en az 10 örnek) kurallar \"gözden geçirilmeli\" olur; kural kapatılmaz, yalnızca puanı %25 düşürülür.</div>");
        // Denetim izi.
        if (g.Iz != null) { var (saglam, bozuk) = g.Iz.Dogrula(); var son = g.Iz.Kayitlar.LastOrDefault(); sb.Append($"<h2>Denetim İzi Bütünlüğü</h2><p>{g.Iz.Kayitlar.Count} kayıt · zincir: <b>{(saglam ? "sağlam" : "BOZUK (kayıt " + bozuk + ")")}</b>" + (son != null ? $" · son özet {son.Hash[..16]}…" : "") + "</p>"); }
        // Sınırlar.
        sb.Append("<h2>Kapsam ve Sınırlar</h2><ul><li>Denetim yalnızca <b>okur</b>; sistemde ayar değiştirmez. Düzeltmeler elle veya ayrıca onaylanarak yapılır.</li><li>Yama ve zafiyet bulguları, yüklediğiniz katalog/NVD/KEV verisinin kapsamıyla sınırlıdır; veri yoksa sonuç \"değerlendirilemedi\" olarak belirtilir, \"güvenli\" anlamına gelmez.</li><li>Yazılım-ürün eşlemesi tablo (yüksek güven) veya tahmine (düşük güven) dayanır; düşük güvenli eşleşmeler en çok \"Düşük/Orta\" önem alır.</li><li>Yazılabilir klasör kontrolü yalnızca Users, Everyone ve Authenticated Users gruplarına bakar.</li><li>Otomatik tarama; kapsamlı bir sızma testi veya bağımsız denetimin yerini tutmaz. Hukuki görüş değildir.</li></ul>");
        sb.Append("<p class=\"m\">Bu rapor BulutSoft Security Suite tarafından otomatik üretilmiştir.</p></body></html>");
        return sb.ToString();
    }
    private static string DurumAdi(FindingState s) => s switch { FindingState.Open => "açık", FindingState.Remediated => "düzeltildi (doğrulanmadı)", FindingState.Verified => "doğrulandı", FindingState.Closed => "kapatıldı", FindingState.AcceptedRisk => "risk kabul edildi", _ => "yanlış alarm" };

    // JSON çıktısı (makine tarafından okunabilir; alan adları snake_case).
    public static string Json(DerinRaporGirdisi g)
    {
        var yuk = new
        {
            rapor = new { cihaz = g.Makine, zaman = g.Zaman.ToUniversalTime().ToString("O"), hazirlayan = g.Hazirlayan, surum = "derin-denetim-1" },
            veri_kaynaklari = new { kb_katalog = g.Katalog == null ? null : new { surum = g.Katalog.Surum, ozet = g.Katalog.Ozet }, nvd = g.Nvd == null ? null : new { g.Nvd.CveSayisi, urun = g.Nvd.Urunler.Count, ozet = g.Nvd.KaynakOzeti }, kev = g.Kev == null ? null : new { surum = g.Kev.Surum, adet = g.Kev.Kayitlar.Count, ozet = g.Kev.Ozet } },
            ozet = new { toplam = g.Bulgular.Count, yanlis_alarm = g.Bulgular.Count(f => f.State == FindingState.FalsePositive), yuksek = Gecerli(g.Bulgular).Count(f => f.Score > 0 && f.EskiOnem == Onem.Yuksek) },
            bulgular = g.Bulgular
        };
        return JsonSerializer.Serialize(yuk, FindingSchema.SecenekJson);
    }

    // PDF çıktısı (mevcut rapor motoruyla; ayrıntılar HTML/JSON'da daha zengindir).
    public static void PdfYaz(string yol, DerinRaporGirdisi g)
    {
        var l = Gecerli(g.Bulgular); var liste = l.Select(V2Kayit.Donustur).ToList();
        int k = liste.Count(x => x.Onem == Onem.Yuksek), s = liste.Count(x => x.Onem is Onem.Orta or Onem.Dusuk), y = liste.Count(x => x.Onem == Onem.Bilgi);
        int puan = Math.Clamp(100 - liste.Sum(x => x.Onem switch { Onem.Yuksek => 15, Onem.Orta => 7, Onem.Dusuk => 3, _ => 0 }), 0, 100);
        RaporUretici.PdfYaz(yol, new RaporBilgisi(g.Makine, g.Zaman.ToLocalTime(), "Derin Denetim", 0, TimeSpan.Zero, puan, k, s, y, 0), liste);
    }
}
