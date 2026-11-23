// Bu dosya "Yönetim Kurulu Risk Özeti"ni üretir: teknik olmayan yöneticiler için TEK SAYFALIK (A4) bir rapor.
// İçerik: genel not, risk dağılımı, trend, en önemli 3 risk ve iş etkisi, KVKK/ISO 27001 özeti, 30/60/90 günlük eylem planı ve karar önerisi.
// Rapor HTML'dir; tarayıcıda açıp "Yazdır > PDF olarak kaydet" ile PDF yapılır. Bilgisayardan dışarı bir şey göndermez.
using System.Net;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Yönetim kurulu raporu üretici.
internal static class YonetimKuruluRaporu
{
    // HTML güvenli kodlama.
    private static string E(string s) => WebUtility.HtmlEncode(s ?? "");

    // Modülün iş diliyle etkisi (yönetici "port açık" yerine "ne olur" bilmek ister).
    public static string Etki(string modul) => modul switch
    {
        "Port" => "Dışarıdan erişilebilen hizmetler, saldırganların sisteme girdiği ilk kapıdır.",
        "HTTP" or "Girdi" => "Web uygulamasındaki zayıflıklar veri sızıntısına veya hizmet kesintisine yol açabilir.",
        "TLS" => "Şifreleme eksikleri, aktarılan verilerin (parola, kişisel veri) dinlenmesine imkân verir.",
        "Karne" => "Temel güvenlik önlemlerinin (antivirüs, güvenlik duvarı, güncelleme) eksikliği, saldırıyı kolaylaştırır.",
        "Kalıcılık" => "Sistemde gizli ve kalıcı bir yerleşim olabileceğini düşündürür.",
        "Hesap" => "Zayıf hesap yönetimi, yetkisiz erişim ve kimlik ele geçirme riskini artırır.",
        "Wi-Fi" => "Kablosuz ağ zayıflıkları, ağ trafiğinin dinlenmesine veya ağa sızılmasına yol açabilir.",
        "Hosts" or "DNS" => "İsim çözümleme sorunları, kullanıcıların sahte sitelere yönlendirilmesine yol açabilir.",
        "Zincir" => "Birden fazla zayıflığın birleşmesi, tek başına küçük görünen sorunları ciddi bir ihlale dönüştürebilir.",
        "Referans" => "Güvenli bilinen durumdan sapma, yetkisiz bir değişikliğe işaret edebilir.",
        "Hizmet" => "Hizmet yapılandırma zayıflıkları, bir saldırganın sistem yetkisi kazanmasına yol açabilir.",
        "Yama" => "Eksik güvenlik güncellemeleri, bilinen açıkların saldırganlarca kullanılmasına izin verir.",
        "Yazılım" => "Güncellenmemiş veya korumasız yazılımlardaki bilinen açıklar, saldırganlarca sahada kullanılır.",
        "Aktif HTTP" => "Web sunucusundaki yanlış yapılandırmalar veri sızıntısına veya yetkisiz erişime yol açabilir.",
        _ => "Bu bulgu, bilgi güvenliği tedbirlerinde bir eksikliğe işaret eder."
    };

    // Trend çizgisini (son 8 tarama) SVG olarak çizer.
    private static string TrendSvg(List<GecmisKayit> gecmis)
    {
        // Eskiden yeniye son 8 puan.
        var p = gecmis.Take(8).Reverse().Select(k => k.Puan).ToList(); if (p.Count < 2) return "<div class='sil'>Trend için en az 2 kayıtlı tarama gerekir.</div>";
        // Noktalar (300x70 alan).
        var ic = System.Globalization.CultureInfo.InvariantCulture; var nok = p.Select((v, i) => (10 + i * (280.0 / (p.Count - 1))).ToString("0.#", ic) + "," + (65 - v * 0.6).ToString("0.#", ic)).ToList();
        // SVG.
        return $"<svg viewBox='0 0 300 70' width='100%' height='70'><polyline fill='none' stroke='#0284c7' stroke-width='2.5' points='{string.Join(" ", nok)}'/>" + string.Concat(nok.Select(n => $"<circle cx='{n.Split(',')[0]}' cy='{n.Split(',')[1]}' r='3' fill='#0284c7'/>")) + $"</svg><div class='sil'>Son {p.Count} taramanın puanı: {string.Join(" → ", p)}</div>";
    }

    // Raporun HTML'ini üretir.
    public static string Html(string hedef, DateTime tarih, int puan, List<Bulgu> bulgular, List<GecmisKayit> gecmis)
    {
        // Notlar ve sayılar.
        var (harf, yorum) = DurumAnalizi.Not(puan); var gercek = bulgular.Where(b => !b.Atlandi).ToList();
        int kir = gercek.Count(b => b.Onem == Onem.Yuksek), orta = gercek.Count(b => b.Onem == Onem.Orta), dus = gercek.Count(b => b.Onem == Onem.Dusuk), guv = gercek.Count(b => b.Onem == Onem.Bilgi), top = Math.Max(1, kir + orta + dus + guv);
        // Önceki tarama (aynı hedef).
        var onceki = gecmis.Where(k => string.Equals(k.Hedef, hedef, StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        string trend = onceki == null ? "Karşılaştırılacak önceki tarama yok." : puan > onceki.Puan ? $"Önceki taramaya göre <b style='color:#15803d'>+{puan - onceki.Puan} puan iyileşme</b>." : puan < onceki.Puan ? $"Önceki taramaya göre <b style='color:#b91c1c'>{puan - onceki.Puan} puan kötüleşme</b>." : "Önceki taramayla aynı seviyede.";
        // Plan (puan kazancına göre).
        var plan = DurumAnalizi.Plan(bulgular, puan); string renk = puan >= 75 ? "#15803d" : puan >= 50 ? "#b45309" : "#b91c1c";
        // Sayfa.
        var sb = new StringBuilder("<!doctype html><html lang='tr'><head><meta charset='utf-8'><title>Yönetim Kurulu Siber Risk Özeti</title><style>");
        // Yazdırma ve ekran stilleri.
        sb.Append("@page{size:A4;margin:12mm}*{box-sizing:border-box}body{font-family:'Segoe UI',Arial,sans-serif;color:#0f172a;margin:0;background:#e2e8f0}.sayfa{width:210mm;min-height:297mm;margin:10px auto;background:#fff;padding:12mm 13mm;box-shadow:0 2px 12px #0003}@media print{body{background:#fff}.sayfa{margin:0;box-shadow:none;width:auto;min-height:0;padding:0}}");
        sb.Append("h1{font-size:19px;margin:0}h2{font-size:12.5px;margin:12px 0 5px;color:#0369a1;text-transform:uppercase;letter-spacing:.4px;border-bottom:1px solid #cbd5e1;padding-bottom:3px}.ust{display:flex;justify-content:space-between;border-bottom:3px solid #0284c7;padding-bottom:7px}.sil{color:#64748b;font-size:10px}.kart{display:flex;gap:14px;align-items:center}.not{width:78px;height:78px;border-radius:50%;border:6px solid " + renk + ";display:flex;align-items:center;justify-content:center;font-size:36px;font-weight:700;color:" + renk + "}");
        sb.Append(".iki{display:flex;gap:16px}.iki>div{flex:1}table{width:100%;border-collapse:collapse;font-size:10.5px}td,th{border-bottom:1px solid #e2e8f0;padding:3px 5px;text-align:left;vertical-align:top}th{background:#f1f5f9}.bar{height:11px;border-radius:3px;display:inline-block;vertical-align:middle}.risk{border-left:4px solid #b91c1c;background:#fef2f2;padding:5px 8px;margin:4px 0;font-size:10.5px}.imza{display:flex;gap:30px;margin-top:14px}.imza div{flex:1;border-top:1px solid #64748b;padding-top:3px;font-size:10px;color:#475569}</style></head><body><div class='sayfa'>");
        // Üst başlık.
        sb.Append($"<div class='ust'><div><h1>Yönetim Kurulu Siber Risk Özeti</h1><div class='sil'>Hedef: {E(hedef)} · Tarama tarihi: {tarih:dd.MM.yyyy HH:mm} · Rapor tarihi: {DateTime.Now:dd.MM.yyyy}</div></div><div class='sil'>GİZLİ · Yönetim kullanımı</div></div>");
        // Genel durum.
        sb.Append($"<h2>Genel Durum</h2><div class='kart'><div class='not'>{harf}</div><div><div style='font-size:15px'><b>{puan}/100</b> — {E(yorum)}</div><div style='font-size:11px'>{trend}</div><div class='sil'>Not skalası: A ≥90 · B ≥75 · C ≥60 · D ≥40 · F &lt;40</div></div></div>");
        // Risk dağılımı + trend.
        sb.Append("<div class='iki'><div><h2>Risk Dağılımı</h2><table>");
        foreach (var (ad, n, r) in new[] { ("Kritik", kir, "#b91c1c"), ("Orta", orta, "#d97706"), ("Düşük", dus, "#ca8a04"), ("Güvenli", guv, "#15803d") }) sb.Append($"<tr><td style='width:60px'>{ad}</td><td><span class='bar' style='width:{Math.Max(2, 100.0 * n / top):0}%;background:{r}'></span> <b>{n}</b></td></tr>");
        sb.Append("</table></div><div><h2>Trend</h2>" + TrendSvg(gecmis) + "</div></div>");
        // En önemli 3 risk.
        sb.Append("<h2>En Önemli 3 Risk ve İş Etkisi</h2>");
        var ilk = plan.Take(3).ToList(); if (ilk.Count == 0) sb.Append("<div class='sil'>Düzeltilmesi gereken bir risk görünmüyor.</div>");
        foreach (var m in ilk) sb.Append($"<div class='risk'><b>{m.Sira}. {E(m.B.Baslik)}</b> <span class='sil'>({E(m.B.Modul)} · düzelirse +{m.Kazanc} puan)</span><br><i>İş etkisi:</i> {E(Etki(m.B.Modul))}<br><i>Önerilen aksiyon:</i> {E(m.B.Oneri)}</div>");
        // Uyumluluk.
        var uyum = Uyumluluk.Ozet(bulgular); sb.Append("<h2>KVKK ve ISO/IEC 27001 Uyumluluk Görünümü</h2>");
        if (uyum.Count == 0) sb.Append("<div class='sil'>Uyumluluk açısından işaretlenmiş bulgu yok.</div>"); else { sb.Append("<table><tr><th style='width:95px'>Madde / Kontrol</th><th>Açıklama</th><th style='width:60px'>Bulgu</th></tr>"); foreach (var (kod, ad, adet) in uyum.Take(6)) sb.Append($"<tr><td><b>{E(kod)}</b></td><td>{E(ad)}</td><td>{adet}</td></tr>"); sb.Append("</table>"); }
        // Eylem planı.
        sb.Append("<h2>30 / 60 / 90 Günlük Eylem Planı</h2>");
        if (plan.Count == 0) sb.Append("<div class='sil'>Plan gerektiren madde yok.</div>"); else { sb.Append("<table><tr><th style='width:70px'>Dönem</th><th>Madde</th><th style='width:55px'>Kazanç</th></tr>"); foreach (var m in plan.Take(10)) sb.Append($"<tr><td>{(m.Sira <= 3 ? "0-30 gün" : m.Sira <= 6 ? "31-60 gün" : "61-90 gün")}</td><td>{E(m.B.Baslik)} <span class='sil'>({E(m.B.Modul)})</span></td><td>+{m.Kazanc}</td></tr>"); sb.Append("</table>" + (plan.Count > 10 ? $"<div class='sil'>+ {plan.Count - 10} madde daha (teknik rapora bakın).</div>" : "")); }
        // Karar önerisi.
        sb.Append("<h2>Yönetimden Beklenen Karar</h2><ul style='margin:3px 0;padding-left:18px;font-size:11px'>");
        if (kir > 0) sb.Append($"<li><b>{kir} kritik riskin 30 gün içinde kapatılması</b> için BT ekibine kaynak ve yetki verilmesi.</li>"); if (orta > 0) sb.Append($"<li>{orta} orta riskli maddenin 60-90 gün içinde planlanması.</li>"); sb.Append("<li>Taramaların düzenli (en az ayda bir) yapılması ve sonuçların bu formatla yönetime sunulması.</li>"); if (kir == 0 && orta == 0) sb.Append("<li>Mevcut güvenlik düzeyinin korunması.</li>");
        sb.Append("</ul>");
        // Uyarı ve imza.
        sb.Append($"<div class='sil' style='margin-top:8px'>{E(Uyumluluk.Not)} Bu rapor otomatik tarama sonuçlarına dayanır; kapsamlı bir sızma testi veya bağımsız denetimin yerini tutmaz.</div><div class='imza'><div>Hazırlayan (BT / Bilgi Güvenliği)</div><div>Onaylayan (Yönetim)</div><div>Tarih</div></div></div></body></html>");
        // Sonuç.
        return sb.ToString();
    }
}
