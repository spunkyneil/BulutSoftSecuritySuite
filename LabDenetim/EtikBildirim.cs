// Bu dosya üçüncü rapor türünü üretir: "Etik Açık Bildirim Raporu" (Responsible Disclosure).
// Amaç: dış ağdaki bir sitenin sahibine, YAPICI ve NEZAKETLİ bir dille, pasif gözlemde gördüğümüz eksikleri ve nasıl düzelteceğini göndermek.
// Üç çıktı: e-postaya yapıştırılabilir Markdown metni, tarayıcıda yazdırılabilir HTML ve kısa PDF özeti (mevcut RaporUretici ile).
// GÜVENLİK: Rapora giren her metin uzak sunucudan gelmiş olabilir (başlıklar, çerezler, security.txt). Bu yüzden HTML'de kodlanır, Markdown'da temizlenir;
// çerez DEĞERLERİ hiçbir zaman rapora girmez (PasifKurallar.CerezMaskele).
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Etik bildirim raporu üretici.
internal static class EtikBildirim
{
    // HTML kodlama.
    private static string E(string s) => WebUtility.HtmlEncode(s ?? "");
    // Markdown için metin temizliği (satır sonu, tablo/kod/HTML karakterleri).
    internal static string Md(string s) => Regex.Replace((s ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Replace("`", "'").Replace("<", "‹").Replace(">", "›"), @"\s{2,}", " ").Trim();
    // Önem adı.
    private static string OnemAdi(Onem o) => o switch { Onem.Yuksek => "Yüksek", Onem.Orta => "Orta", Onem.Dusuk => "Düşük", _ => "Bilgi" };

    // Toplam tahmini düzeltme süresi (dakika): aynı kök neden için tekrarı önlemek amacıyla kural başına bir kez sayılır.
    public static int ToplamDakika(IEnumerable<PasifBulgu> b) => b.GroupBy(x => x.RuleId).Sum(g => g.First().TahminiDk);
    // Süreyi okunur yazar.
    public static string SureYazi(int dk) => dk < 60 ? $"~{dk} dakika" : dk % 60 == 0 ? $"~{dk / 60} saat" : $"~{dk / 60} saat {dk % 60} dakika";

    // Doğru yapılandırılmış olanlar (yapıcı ton için): gözlemden çıkarılır.
    public static List<string> IyiUygulamalar(PasifGozlem g, List<PasifBulgu> b)
    {
        // Bulgusu olmayan kontroller.
        var kimlikler = b.Select(x => x.RuleId).ToHashSet(); var l = new List<string>();
        // HTTPS ve başlıklar.
        if (g.Http != null)
        {
            if (!kimlikler.Contains("HDR-HSTS-MISSING") && g.Http.Basliklar.ContainsKey("strict-transport-security")) l.Add("HSTS (Strict-Transport-Security) etkin.");
            if (!kimlikler.Contains("HDR-CSP-MISSING") && g.Http.Basliklar.ContainsKey("content-security-policy")) l.Add("İçerik Güvenliği Politikası (CSP) tanımlı.");
            if (g.Http.HttpYonlendirme == "evet") l.Add("http:// adresi https:// adresine yönlendiriliyor.");
        }
        // TLS.
        if (g.Tls != null && !b.Any(x => x.Modul == "TLS")) l.Add($"TLS sertifikası geçerli ve güvenilir ({g.Tls.Surum}).");
        // DNS.
        if (g.Dns != null) { if (g.Dns.Spf != null && !kimlikler.Contains("DNS-SPF-MISSING")) l.Add("SPF kaydı mevcut."); if (g.Dns.Dmarc != null && !kimlikler.Contains("DNS-DMARC-MISSING")) l.Add("DMARC kaydı mevcut."); }
        // security.txt.
        if (g.SecTxt.Bulundu) l.Add("security.txt yayınlanmış (güvenlik açığı bildirim kanalı var).");
        // Sonuç.
        return l;
    }

    // Bildirimin gönderileceği adres(ler): security.txt Contact alanından (yoksa boş).
    public static List<string> Alicilar(PasifGozlem g) => SecurityTxt.Iletisimler(g.SecTxt);

    // Konu satırı.
    public static string Konu(PasifGozlem g) => $"Güvenlik Bildirimi: {Md(g.Host)} için dışarıdan gözlemlenen yapılandırma önerileri";

    // Önem sırası.
    private static IEnumerable<PasifBulgu> Sirali(List<PasifBulgu> b) => b.OrderByDescending(x => x.Onem).ThenByDescending(x => x.Etki);

    // ---------------- Markdown (e-posta metni) ----------------
    public static string Markdown(PasifGozlem g, List<PasifBulgu> bulgular, string gonderenAd, string gonderenIletisim)
    {
        // Yazıcı.
        var sb = new StringBuilder(); int dk = ToplamDakika(bulgular); var alicilar = Alicilar(g);
        // Başlık ve selam.
        sb.AppendLine($"# {Konu(g)}").AppendLine();
        sb.AppendLine($"**Alıcı:** {(alicilar.Count > 0 ? string.Join(", ", alicilar.Select(Md)) : "security.txt bulunamadı — kurumun resmi iletişim kanalını kullanın")}  ").AppendLine($"**Tarih:** {g.Zaman.ToLocalTime():dd.MM.yyyy HH:mm}  ").AppendLine($"**Gözlenen adres:** {Md(g.Host)}").AppendLine();
        sb.AppendLine("Sayın Güvenlik / Bilgi İşlem Sorumlusu,").AppendLine();
        sb.AppendLine("İyi niyetle ve iyileştirme amacıyla yazıyorum. Alan adınızın **herkese açık** yüzünü, standart bir web tarayıcısının yapacağı kadarıyla inceleyerek aşağıdaki yapılandırma önerilerini derledim. İnceleme sırasında hiçbir sisteme girilmemiş, şifre denenmemiş, port taraması veya form/yük testi yapılmamış, hiçbir veriye erişilmemiştir.").AppendLine();
        // Yöntem.
        sb.AppendLine("## Ne yaptık, ne yapmadık").AppendLine();
        sb.AppendLine("- Yapılan: ana sayfaya birkaç standart GET/HEAD isteği, bir TLS el sıkışması (sertifika okuma), DNS sorguları (SPF/DMARC/CAA) ve `security.txt` araması.").AppendLine("- Yapılmayan: port taraması, açık sömürme, kaba kuvvet, form doldurma, hizmeti yoracak yoğun istek.").AppendLine();
        // İyi uygulamalar.
        var iyi = IyiUygulamalar(g, bulgular); if (iyi.Count > 0) { sb.AppendLine("## Doğru yaptıklarınız").AppendLine(); foreach (var x in iyi) sb.AppendLine("- " + Md(x)); sb.AppendLine(); }
        // Özet.
        sb.AppendLine("## Özet").AppendLine();
        if (bulgular.Count == 0) sb.AppendLine("Pasif gözlemde düzeltilmesi gereken bir nokta görmedik. Emeğinize teşekkürler.").AppendLine();
        else { sb.AppendLine($"{bulgular.Count} öneri: Yüksek {bulgular.Count(x => x.Onem == Onem.Yuksek)}, Orta {bulgular.Count(x => x.Onem == Onem.Orta)}, Düşük {bulgular.Count(x => x.Onem == Onem.Dusuk)}, Bilgi {bulgular.Count(x => x.Onem == Onem.Bilgi)}. **Tahmini toplam düzeltme süresi: {SureYazi(dk)}** (deneyimli bir sistem yöneticisi için; test dahil değildir).").AppendLine(); }
        // Ayrıntılar.
        int n = 0; foreach (var b in Sirali(bulgular))
        {
            // Bulgu.
            sb.AppendLine($"### {++n}. {Md(b.Baslik)}  _(Önem: {OnemAdi(b.Onem)} · ~{b.TahminiDk} dk)_").AppendLine();
            sb.AppendLine($"- **Ne gördük:** {Md(b.Ne)}").AppendLine($"- **Neden önemli:** {Md(b.Neden)}").AppendLine($"- **Nasıl düzeltilir:** {Md(b.Cozum)}");
            if (!string.IsNullOrWhiteSpace(b.Komut)) sb.AppendLine("- **Örnek yapılandırma:**").AppendLine().AppendLine("  ```").AppendLine("  " + (b.Komut ?? "").Replace("\r", "").Replace("\n", "\n  ").Replace("```", "'''")).AppendLine("  ```");
            if (b.Kvkk.Length + b.Iso.Length > 0) sb.AppendLine($"- **Mevzuat/standart ilişkisi:** {string.Join(" · ", b.Kvkk.Select(x => "KVKK " + Md(x)).Concat(b.Iso.Select(x => "ISO 27001 " + Md(x))))}");
            if (b.Kanit.Length > 0) sb.AppendLine($"- **Gözlenen kanıt:** `{Md(b.Kanit)}`");
            sb.AppendLine();
        }
        // Kapanış.
        sb.AppendLine("## Gizlilik ve devam").AppendLine();
        sb.AppendLine("Bu bildirimi yalnızca size ilettim; üçüncü kişilerle veya kamuyla paylaşmayacağım. Düzeltmeleri tamamladığınızda isterseniz aynı pasif gözlemi tekrarlayıp sonucu paylaşabilirim. Bu öneriler dışarıdan görülebilen bilgilere dayanır; yanlış anlaşılan veya sizin tarafınızda zaten çözülmüş noktalar olabilir, bunun için şimdiden özür dilerim.").AppendLine();
        sb.AppendLine("Saygılarımla,  ").AppendLine(Md(string.IsNullOrWhiteSpace(gonderenAd) ? "[Adınız]" : gonderenAd) + "  ").AppendLine(Md(string.IsNullOrWhiteSpace(gonderenIletisim) ? "[İletişim bilgisi]" : gonderenIletisim)).AppendLine();
        sb.AppendLine("---").AppendLine("_Bu rapor BulutSoft Security Suite ile pasif gözlem sonucu hazırlanmıştır; otomatik üretilmiştir, göndermeden önce gözden geçirin._");
        // Sonuç.
        return sb.ToString();
    }

    // ---------------- HTML (yazdırılabilir) ----------------
    public static string Html(PasifGozlem g, List<PasifBulgu> bulgular, string gonderenAd, string gonderenIletisim)
    {
        // Yazıcı ve değerler.
        var sb = new StringBuilder(); int dk = ToplamDakika(bulgular); var alicilar = Alicilar(g); var iyi = IyiUygulamalar(g, bulgular);
        // Biçim.
        sb.Append("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Etik Açık Bildirim Raporu</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:820px;margin:32px auto;color:#0f172a;line-height:1.55;padding:0 16px}h1{font-size:22px;border-bottom:3px solid #0ea5e9;padding-bottom:8px}h2{font-size:17px;margin-top:26px;color:#0c4a6e}.b{border:1px solid #e2e8f0;border-left:5px solid #94a3b8;border-radius:6px;padding:10px 14px;margin:12px 0;page-break-inside:avoid}.Yuksek{border-left-color:#dc2626}.Orta{border-left-color:#f59e0b}.Dusuk{border-left-color:#0ea5e9}.m{color:#64748b;font-size:13px}pre{background:#f1f5f9;padding:8px;border-radius:4px;white-space:pre-wrap;font-size:12.5px}.ok{background:#f0fdf4;border-left:4px solid #16a34a;padding:6px 12px}@media print{body{margin:10mm}}</style></head><body>");
        // Başlık.
        sb.Append($"<h1>{E(Konu(g))}</h1><p class=\"m\">Alıcı: {(alicilar.Count > 0 ? E(string.Join(", ", alicilar)) : "security.txt bulunamadı — kurumun resmi iletişim kanalını kullanın")}<br>Tarih: {g.Zaman.ToLocalTime():dd.MM.yyyy HH:mm} · Gözlenen adres: {E(g.Host)}</p>");
        sb.Append("<p>Sayın Güvenlik / Bilgi İşlem Sorumlusu,</p><p>İyi niyetle ve iyileştirme amacıyla yazıyorum. Alan adınızın <b>herkese açık</b> yüzünü, standart bir web tarayıcısının yapacağı kadarıyla inceleyerek aşağıdaki yapılandırma önerilerini derledim. İnceleme sırasında hiçbir sisteme girilmemiş, şifre denenmemiş, port taraması veya form/yük testi yapılmamış, hiçbir veriye erişilmemiştir.</p>");
        // Yöntem.
        sb.Append("<h2>Ne yaptık, ne yapmadık</h2><ul><li><b>Yapılan:</b> ana sayfaya birkaç standart GET/HEAD isteği, bir TLS el sıkışması, DNS sorguları (SPF/DMARC/CAA) ve security.txt araması.</li><li><b>Yapılmayan:</b> port taraması, açık sömürme, kaba kuvvet, form doldurma, hizmeti yoracak yoğun istek.</li></ul>");
        // İyi uygulamalar.
        if (iyi.Count > 0) { sb.Append("<h2>Doğru yaptıklarınız</h2>"); foreach (var x in iyi) sb.Append($"<div class=\"ok\">{E(x)}</div>"); }
        // Özet.
        sb.Append("<h2>Özet</h2>"); sb.Append(bulgular.Count == 0 ? "<p>Pasif gözlemde düzeltilmesi gereken bir nokta görmedik. Emeğinize teşekkürler.</p>" : $"<p>{bulgular.Count} öneri: Yüksek {bulgular.Count(x => x.Onem == Onem.Yuksek)}, Orta {bulgular.Count(x => x.Onem == Onem.Orta)}, Düşük {bulgular.Count(x => x.Onem == Onem.Dusuk)}, Bilgi {bulgular.Count(x => x.Onem == Onem.Bilgi)}. <b>Tahmini toplam düzeltme süresi: {E(SureYazi(dk))}</b> <span class=\"m\">(deneyimli bir sistem yöneticisi için; test dahil değildir)</span></p>");
        // Bulgular.
        int n = 0; foreach (var b in Sirali(bulgular))
        {
            sb.Append($"<div class=\"b {OnemAdi(b.Onem)}\"><b>{++n}. {E(b.Baslik)}</b> <span class=\"m\">· Önem: {OnemAdi(b.Onem)} · ~{b.TahminiDk} dk</span><br><b>Ne gördük:</b> {E(b.Ne)}<br><b>Neden önemli:</b> {E(b.Neden)}<br><b>Nasıl düzeltilir:</b> {E(b.Cozum)}");
            if (!string.IsNullOrWhiteSpace(b.Komut)) sb.Append($"<pre>{E(b.Komut)}</pre>");
            if (b.Kvkk.Length + b.Iso.Length > 0) sb.Append($"<div class=\"m\">Mevzuat/standart: {E(string.Join(" · ", b.Kvkk.Select(x => "KVKK " + x).Concat(b.Iso.Select(x => "ISO 27001 " + x))))}</div>");
            if (b.Kanit.Length > 0) sb.Append($"<div class=\"m\">Gözlenen kanıt: {E(b.Kanit)}</div>");
            sb.Append("</div>");
        }
        // Kapanış.
        sb.Append("<h2>Gizlilik ve devam</h2><p>Bu bildirimi yalnızca size ilettim; üçüncü kişilerle veya kamuyla paylaşmayacağım. Düzeltmeleri tamamladığınızda isterseniz aynı pasif gözlemi tekrarlayıp sonucu paylaşabilirim. Bu öneriler dışarıdan görülebilen bilgilere dayanır; yanlış anlaşılan veya sizin tarafınızda zaten çözülmüş noktalar olabilir, bunun için şimdiden özür dilerim.</p>");
        sb.Append($"<p>Saygılarımla,<br>{E(string.IsNullOrWhiteSpace(gonderenAd) ? "[Adınız]" : gonderenAd)}<br>{E(string.IsNullOrWhiteSpace(gonderenIletisim) ? "[İletişim bilgisi]" : gonderenIletisim)}</p><p class=\"m\">Bu rapor BulutSoft Security Suite ile pasif gözlem sonucu hazırlanmıştır; otomatik üretilmiştir, göndermeden önce gözden geçirin.</p></body></html>");
        // Sonuç.
        return sb.ToString();
    }

    // ---------------- PDF özeti (mevcut rapor motoruyla) ----------------
    public static void PdfYaz(string yol, PasifGozlem g, List<PasifBulgu> bulgular)
    {
        // Mevcut PDF üreticisi Bulgu listesi ister; pasif bulguları dönüştür.
        var liste = bulgular.Select(b => b.ToBulgu()).ToList(); int k = liste.Count(x => x.Onem == Onem.Yuksek), s = liste.Count(x => x.Onem is Onem.Orta or Onem.Dusuk), y = liste.Count(x => x.Onem == Onem.Bilgi);
        // Puan (mevcut hesapla aynı mantık için basit): her yüksek -15, orta -7, düşük -3.
        int puan = Math.Clamp(100 - liste.Sum(x => x.Onem switch { Onem.Yuksek => 15, Onem.Orta => 7, Onem.Dusuk => 3, _ => 0 }), 0, 100);
        // Yaz.
        RaporUretici.PdfYaz(yol, new RaporBilgisi(g.Host, g.Zaman.ToLocalTime(), "Etik Açık Bildirim (pasif gözlem)", 0, TimeSpan.Zero, puan, k, s, y, 0), liste);
    }
}
