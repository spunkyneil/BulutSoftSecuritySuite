// Web sayfası için güvenli yazı dönüşümü araçlarını çağır.
using System.Net;
// Yazıları birleştirme araçlarını çağır.
using System.Text;
// PDF çizme araçlarını çağır (QuestPDF paketi).
using QuestPDF.Fluent;
// PDF renkleri ve yardımcıları.
using QuestPDF.Helpers;
// PDF lisans ayarları.
using QuestPDF.Infrastructure;

// Programımızın adı.
namespace LabDenetim;

// RAPOR MODÜLÜ: bulunan sonuçları BulutSoft markalı renkli web sayfası (HTML) veya PDF dosyası olarak kaydeder.
public static class RaporUretici
{
    // Raporun en altındaki imza cümlesi.
    public const string Imza = "Bu güvenlik denetim raporu yerel sistem analiz motoru tarafından oluşturulmuştur";

    // Yazıyı web sayfasında güvenle göstermek için özel karakterleri dönüştürür.
    static string E(string s) => WebUtility.HtmlEncode(s ?? "");

    // Bir sonucun rozet yazısını, web renk kodunu ve sıralama sırasını verir.
    static (string Metin, string Renk, int Sira) Rozet(Bulgu b)
    {
        // Kontrol yapılamadıysa gri ATLANDI (en sona).
        if (b.Atlandi) return ("ATLANDI", "#94a3b8", -1);
        // Önem derecesine göre seç.
        return b.Onem switch
        {
            // Kırmızı.
            Onem.Yuksek => ("KRİTİK", "#ef4444", 4),
            // Turuncu.
            Onem.Orta => ("UYARI", "#f59e0b", 3),
            // Sarı.
            Onem.Dusuk => ("DİKKAT", "#facc15", 2),
            // Yeşil.
            _ => ("GÜVENLİ", "#10b981", 1)
        };
    }

    // Puana göre halka rengini verir: 80+ yeşil, 50+ turuncu, altı kırmızı.
    static string PuanRengi(int puan) => puan >= 80 ? "#10b981" : puan >= 50 ? "#f59e0b" : "#ef4444";

    // Süreyi dakika:saniye olarak yazar.
    static string SureYaz(TimeSpan s) => $"{(int)s.TotalMinutes:00}:{s.Seconds:00}";

    // BulutSoft markalı, renkli web sayfası (HTML) raporu yazar.
    // aciklayici: her bulgu için "Bu ne demek / Nasıl çözerim / Komut" üreten isteğe bağlı fonksiyon.
    public static void HtmlYaz(string yol, RaporBilgisi bilgi, IEnumerable<Bulgu> bulgular, Func<Bulgu, (string NeDemek, string Nasil, string Komut)>? aciklayici = null)
    {
        // Sonuçları en tehlikeliden en güvenliye sırala (yapılamayanlar sonda).
        var liste = bulgular.OrderByDescending(b => Rozet(b).Sira).ToList();
        // Sayfa yazısını biriktireceğimiz araç.
        var sb = new StringBuilder();
        // Sayfanın başlangıcı, başlığı ve görünüm (CSS) ayarları.
        sb.Append("<!doctype html><html lang='tr'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>BulutSoft Güvenlik Denetim Raporu</title><style>")
          // Renk değişkenleri ve sayfa zemini.
          .Append(":root{--bg:#0b1220;--panel:#111a2e;--line:#1e293b;--text:#e2e8f0;--mut:#94a3b8;--blue:#38bdf8;--yellow:#ffd60a}")
          // Genel yazı tipi ve sayfa genişliği.
          .Append("*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:'Segoe UI',Arial,sans-serif;line-height:1.5}.wrap{max-width:1120px;margin:0 auto;padding:32px 24px}")
          // Üst başlık.
          .Append("header{border-bottom:3px solid var(--blue);padding-bottom:16px;margin-bottom:22px}h1{margin:0;font-size:30px;color:var(--yellow)}.sub{color:var(--blue);font-weight:600;margin-top:2px}.meta{color:var(--mut);font-size:13px;margin-top:8px}")
          // Özet alanı: halka ve kartlar.
          .Append(".ozet{display:flex;gap:22px;align-items:center;flex-wrap:wrap;margin:18px 0 26px}.halka{width:128px;height:128px;border-radius:50%;display:grid;place-items:center;flex:none}.halka div{width:98px;height:98px;border-radius:50%;background:var(--bg);display:flex;flex-direction:column;align-items:center;justify-content:center}.halka b{font-size:32px;line-height:1}.halka small{color:var(--mut)}")
          .Append(".kartlar{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:12px;flex:1}.kart{background:var(--panel);border-left:4px solid var(--blue);border-radius:6px;padding:12px 14px}.kart b{display:block;font-size:26px}.kart span{color:var(--mut);font-size:12px;text-transform:uppercase;letter-spacing:.5px}")
          // Tablo.
          .Append("h2{color:var(--blue);margin:8px 0 6px}table{width:100%;border-collapse:collapse}th{background:#1e293b;color:var(--blue);text-align:left;padding:10px;font-size:13px}td{padding:12px 10px;border-bottom:1px solid var(--line);vertical-align:top;font-size:13.5px}tr:nth-child(even) td{background:rgba(255,255,255,.02)}")
          // Rozet, bölüm başlıkları ve komut kutusu.
          .Append(".rozet{display:inline-block;padding:3px 13px;border-radius:999px;font-weight:700;font-size:12px;border:1px solid;white-space:nowrap}.baslik{font-weight:700;font-size:14.5px}.detay{color:var(--mut);font-size:12.5px;margin-top:2px}.alt{margin-top:8px;font-size:13px}.alt b{color:var(--blue)}")
          .Append("pre{background:#020617;color:#4ade80;border:1px solid var(--line);border-radius:6px;padding:10px;white-space:pre-wrap;font-family:Consolas,monospace;font-size:12px;margin:6px 0 0}")
          // Sayfa sonu imzası.
          .Append("footer{margin-top:40px;border-top:1px solid var(--line);padding-top:16px;text-align:center;color:var(--mut);font-size:13px}footer b{color:var(--yellow)}")
          // Yazdırma görünümü: beyaz zemin, siyah yazı.
          .Append("@media print{body{background:#fff;color:#000}:root{--panel:#f1f5f9;--line:#cbd5e1;--text:#000;--mut:#475569;--bg:#fff}th{background:#e2e8f0}pre{background:#f8fafc;color:#14532d}h1{color:#92400e}}")
          .Append("</style></head><body><div class='wrap'>");
        // Üst başlık: marka, rapor adı ve tarama bilgileri.
        sb.Append("<header><h1>⚡ BulutSoft Güvenlik Denetim Raporu</h1><div class='sub'>Siber Güvenlik ve Ağ Denetim Merkezi · BulutSoft Security Suite " + UrunBilgisi.SurumEtiketi + "</div>")
          .Append($"<div class='meta'>Hedef: <b>{E(bilgi.Hedef)}</b> &nbsp;·&nbsp; Tarih: {bilgi.Tarih:dd.MM.yyyy HH:mm} &nbsp;·&nbsp; Profil: {E(bilgi.Profil)} &nbsp;·&nbsp; Süre: {SureYaz(bilgi.Sure)}</div></header>");
        // Özet: puan halkası ve sayı kartları.
        string renk = PuanRengi(bilgi.Puan);
        // Halka: conic-gradient ile puan kadar dolu daire.
        sb.Append($"<div class='ozet'><div class='halka' style='background:conic-gradient({renk} {bilgi.Puan * 3.6:0}deg,#1e293b 0)'><div><b style='color:{renk}'>{bilgi.Puan}</b><small>/100</small></div></div><div class='kartlar'>")
          // Taranan kapı sayısı.
          .Append($"<div class='kart'><span>Taranan Kapı</span><b>{bilgi.KapiSayisi}</b></div>")
          // Kritik sayısı.
          .Append($"<div class='kart' style='border-color:#ef4444'><span>Kritik</span><b style='color:#ef4444'>{bilgi.Kirmizi}</b></div>")
          // Uyarı/dikkat sayısı.
          .Append($"<div class='kart' style='border-color:#f59e0b'><span>Uyarı / Dikkat</span><b style='color:#f59e0b'>{bilgi.Sari}</b></div>")
          // Güvenli sayısı.
          .Append($"<div class='kart' style='border-color:#10b981'><span>Güvenli</span><b style='color:#10b981'>{bilgi.Yesil}</b></div>")
          // Atlanan kontrol sayısı.
          .Append($"<div class='kart' style='border-color:#94a3b8'><span>Atlanan Kontrol</span><b style='color:#94a3b8'>{bilgi.Atlanan}</b></div></div></div>");
        // Bulgular tablosu başlığı.
        // Uyumluluk özeti: hangi KVKK maddesi / ISO 27001 kontrolünde kaç bulgu var.
        var uyum = Uyumluluk.Ozet(liste);
        if (uyum.Count > 0)
        {
            // Başlık ve tablo.
            sb.Append("<h2>KVKK ve ISO/IEC 27001 Uyumluluk Özeti</h2><table><tr><th style='width:140px'>Madde / Kontrol</th><th>Açıklama</th><th style='width:90px'>Bulgu Sayısı</th></tr>");
            // Her satır.
            foreach (var (kod, ad, adet) in uyum) sb.Append($"<tr><td><b>{E(kod)}</b></td><td>{E(ad)}</td><td>{adet}</td></tr>");
            // Kapat ve uyarı notu.
            sb.Append($"</table><p class='detay'>{E(Uyumluluk.Not)}</p>");
        }
        sb.Append("<h2>Bulgular</h2><table><tr><th style='width:110px'>Tehlike Seviyesi</th><th style='width:110px'>Kontrol</th><th>Ne Bulundu?</th><th>Ne Yapmalısın?</th><th style='width:210px'>Uyumluluk</th></tr>");
        // Her bulgu için bir satır yaz.
        foreach (var b in liste)
        {
            // Rozetin yazısı ve rengi.
            var (metin, rozetRenk, _) = Rozet(b);
            // Satırın ilk hücreleri: rozet, kontrol, başlık ve ayrıntı.
            sb.Append($"<tr><td><span class='rozet' style='color:{rozetRenk};border-color:{rozetRenk};background:{rozetRenk}22'>{metin}</span></td><td>{E(b.Modul)}</td><td><div class='baslik'>{E(b.Baslik)}</div><div class='detay'>{E(b.Detay).Replace("\n", "<br>")}</div></td><td>{E(b.Oneri)}");
            // Güvenli olmayan ve atlanmayan bulgular için sade açıklama ve hazır komut ekle.
            if (aciklayici != null && b.Onem != Onem.Bilgi && !b.Atlandi)
            {
                // Bilgi bankasından açıklamayı al.
                var (neDemek, nasil, komut) = aciklayici(b);
                // "Bu ne demek?" ve "Nasıl çözerim?" bölümleri.
                sb.Append($"<div class='alt'><b>📌 Bu ne demek?</b> {E(neDemek)}</div><div class='alt'><b>🛠️ Nasıl çözerim?</b> {E(nasil)}</div>");
                // Hazır komut varsa koyu kutuda göster.
                if (!string.IsNullOrWhiteSpace(komut)) sb.Append($"<pre>{E(komut)}</pre>");
            }
            // Uyumluluk hücresi (KVKK maddesi ve ISO kontrolleri; sorun olmayanlarda boş).
            sb.Append("</td><td class='detay'>" + string.Join("<br>", Uyumluluk.Uzun(b).Select(x => E(x))) + "</td></tr>");
        }
        // Tabloyu kapat ve sayfa sonu imzasını ekle.
        sb.Append($"</table><footer>⚡ <b>{Imza}.</b><br>{UrunBilgisi.Ad} {UrunBilgisi.SurumEtiketi} · {BrandConfig.Aktif.SirketAdi} · Rapor tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}</footer></div></body></html>");
        // Sayfayı dosyaya yaz.
        File.WriteAllText(yol, sb.ToString(), Encoding.UTF8);
    }

    // BulutSoft markalı PDF raporu yazar.
    public static void PdfYaz(string yol, RaporBilgisi bilgi, IEnumerable<Bulgu> bulgular)
    {
        // QuestPDF'in ücretsiz (topluluk) lisansını kullandığımızı belirt.
        QuestPDF.Settings.License = LicenseType.Community;
        // Sonuçları en tehlikeliden en güvenliye sırala.
        var liste = bulgular.OrderByDescending(b => Rozet(b).Sira).ToList();
        // PDF belgesini oluştur.
        Document.Create(d => d.Page(p =>
        {
            // Sayfa kenar boşluğu.
            p.Margin(30);
            // Varsayılan yazı boyutu. (Yazı tipi: QuestPDF'in kendi içindeki Lato kullanılır; Türkçe karakterleri destekler.
            // Yeni QuestPDF sürümleri Windows yazı tiplerini kendiliğinden kullanmadığı için "Segoe UI" belirtmiyoruz.)
            p.DefaultTextStyle(t => t.FontSize(9));
            // Sayfa üst kısmı: başlık, marka ve tarama bilgileri.
            p.Header().Column(c =>
            {
                // Büyük başlık.
                c.Item().Text("BulutSoft Güvenlik Denetim Raporu").FontSize(18).Bold().FontColor("#92400e");
                // Marka satırı.
                c.Item().Text("Siber Güvenlik ve Ağ Denetim Merkezi · BulutSoft Security Suite " + UrunBilgisi.SurumEtiketi).FontColor("#0369a1").SemiBold();
                // Hedef, tarih, profil ve süre.
                c.Item().Text($"Hedef: {bilgi.Hedef}   Tarih: {bilgi.Tarih:dd.MM.yyyy HH:mm}   Profil: {bilgi.Profil}   Süre: {SureYaz(bilgi.Sure)}");
                // Özet sayılar.
                c.Item().PaddingBottom(6).Text($"Güvenlik puanı: {bilgi.Puan}/100   |   Kritik: {bilgi.Kirmizi}   Uyarı/Dikkat: {bilgi.Sari}   Güvenli: {bilgi.Yesil}   Atlanan: {bilgi.Atlanan}").Bold();
            });
            // Sayfa ortası: sonuç tablosu.
            p.Content().PaddingVertical(10).Column(ana =>
            {
            // Uyumluluk özeti: hangi KVKK maddesi / ISO kontrolünde kaç bulgu var (varsa).
            var uyumOzet = Uyumluluk.Ozet(liste);
            if (uyumOzet.Count > 0)
            {
                // Başlık.
                ana.Item().PaddingBottom(3).Text("KVKK ve ISO/IEC 27001 Uyumluluk Özeti").FontSize(11).Bold().FontColor("#0369a1");
                // Her satır: kod, açıklama ve adet.
                foreach (var (kod, ad, adet) in uyumOzet) ana.Item().Text($"{kod}  —  {ad}  ({adet} bulgu)").FontSize(8);
                // Uyarı notu.
                ana.Item().PaddingTop(3).PaddingBottom(8).Text(Uyumluluk.Not).FontSize(7).Italic().FontColor("#64748b");
            }
            ana.Item().Table(t =>
            {
                // Sütun genişlikleri.
                t.ColumnsDefinition(c => { c.ConstantColumn(56); c.ConstantColumn(58); c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(3); c.RelativeColumn(2); });
                // Tablonun başlık satırı.
                t.Header(h =>
                {
                    // Her başlık için gri kutu çiz.
                    foreach (var s in new[] { "Seviye", "Kontrol", "Ne bulundu?", "Ayrıntı", "Ne Yapmalısın?", "Uyumluluk" })
                        // Başlık hücresi.
                        h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text(s).Bold();
                });
                // Her sonuç için bir satır yaz.
                foreach (var b in liste)
                {
                    // Rozetin yazısı ve rengi.
                    var (metin, renk, _) = Rozet(b);
                    // Seviye hücresi, seviye rengiyle boyalı.
                    t.Cell().Background(renk).Padding(3).Text(metin).FontColor(Colors.White).Bold();
                    // Kontrol adı hücresi.
                    t.Cell().BorderBottom(0.5f).Padding(3).Text(b.Modul);
                    // Bulgu başlığı hücresi.
                    t.Cell().BorderBottom(0.5f).Padding(3).Text(b.Baslik);
                    // Ayrıntı hücresi.
                    t.Cell().BorderBottom(0.5f).Padding(3).Text(b.Detay.Replace("\n", " "));
                    // Öneri hücresi.
                    t.Cell().BorderBottom(0.5f).Padding(3).Text(b.Oneri);
                    // Uyumluluk hücresi (KVKK maddesi ve ISO kodları).
                    t.Cell().BorderBottom(0.5f).Padding(3).Text(Uyumluluk.Yazi(b).Replace("\r\n", "\n")).FontSize(7.5f);
                }
            });
            });
            // Sayfa altı: imza ve sayfa numarası.
            p.Footer().Column(c =>
            {
                // İmza cümlesi.
                c.Item().AlignCenter().Text(Imza + ".").FontColor("#475569").SemiBold();
                // Sayfa numarası.
                c.Item().AlignCenter().Text(x => { x.Span("Sayfa "); x.CurrentPageNumber(); });
            });
        // PDF dosyasını belirtilen yola kaydet.
        })).GeneratePdf(yol);
    }
}
