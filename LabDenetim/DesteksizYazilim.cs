// Bu dosya "ESKİ / DESTEĞİ BİTMİŞ YAZILIM DENETÇİSİ"dir (Ağ Envanteri modülünün yeni parçası).
// Ağdaki cihazlardan okunan hizmet tabelalarını (banner) ve nmap sürüm bilgisini ("Apache 2.2.15", "OpenSSH 7.4", "PHP 5.3.3" ...) bilinen ömür sonu (EOL) tablosuyla karşılaştırır
// ve sistem yöneticisine "bu yazılımın güvenlik yaması artık yayınlanmıyor, güncelleyin" der.
// Bu dosya hiçbir şeye BAĞLANMAZ ve hiçbir şeyi değiştirmez: yalnızca verilen metni okuyup değerlendirir (savunma/envanter amaçlı, saf hesaplama).
// Önemli not (yanlış alarm savunması): Linux dağıtımları güvenlik yamalarını sürüm numarasını değiştirmeden uygulayabilir (backport). Bu yüzden "Eski" sınıfı
// kesin hüküm değil, "doğrulayın" uyarısıdır; "Desteği bitmiş" ise üreticinin resmî ömür sonu tarihine dayanır.
using System.Text.RegularExpressions;
using LabDenetim.Moduller;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Değerlendirme sonucu.
internal sealed record EolSonucu(string Kimlik, string Urun, string Surum, string Durum, string Baslik, string Ne, string Neden, string Cozum, double Etki, Onem Onem);

// Değerlendirici.
internal static class DesteksizYazilim
{
    // Doğrulama kontrol kimliği (aynı kimliği tüm bu bulgular paylaşır).
    public const string KontrolKimligi = "CHK-EOL-NET";

    // Ömür sonu serisi: sürüm bu önekle başlıyorsa (veya önek '.' ile bitiyorsa o ana sürümün tamamı) bu tarihte desteği bitmiştir.
    private sealed record Seri(string Onek, DateTime Bitis);

    // Ürün kuralı: ad, tanıma kalıbı (v grubu sürümü yakalar), seri tablosu (yoksa sürüm eşiği).
    private sealed record Kural(string Kimlik, string Urun, Regex Kalip, Seri[] Seriler, Version? EskiEsigi = null, string? TehlikeliSurum = null, string? TehlikeliNot = null);

    // Kalıp üretici (tek tanım yeri; zaman aşımı kötü niyetli uzun metinlere karşı).
    private static Regex R(string k) => new(k, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    // Kısa tarih yazımı.
    private static DateTime T(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    // Bilinen ömür sonu tablosu (üretici duyurularına dayanır; tarihler "güvenlik yaması bu tarihe kadar" anlamındadır).
    private static readonly Kural[] Kurallar =
    {
        new("EOL-APACHE", "Apache HTTP Server", R(@"\bApache(?:\s+httpd)?[/ ]+(?<v>\d+\.\d+(?:\.\d+)?)"), new[] { new Seri("1.3", T(2010, 2, 3)), new Seri("2.0", T(2013, 7, 10)), new Seri("2.2", T(2017, 12, 31)) }),
        new("EOL-PHP", "PHP", R(@"\bPHP[/ ]+(?<v>\d+\.\d+(?:\.\d+)?)"), new[] { new Seri("4.", T(2008, 8, 8)), new Seri("5.", T(2018, 12, 31)), new Seri("7.0", T(2018, 12, 3)), new Seri("7.1", T(2019, 12, 1)), new Seri("7.2", T(2020, 11, 30)), new Seri("7.3", T(2021, 12, 6)), new Seri("7.4", T(2022, 11, 28)), new Seri("8.0", T(2023, 11, 26)), new Seri("8.1", T(2025, 12, 31)), new Seri("8.2", T(2026, 12, 31)) }),
        new("EOL-OPENSSL", "OpenSSL", R(@"\bOpenSSL[/ ]+(?<v>\d+\.\d+\.\d+)"), new[] { new Seri("0.9.", T(2015, 12, 31)), new Seri("1.0.0", T(2015, 12, 31)), new Seri("1.0.1", T(2016, 12, 31)), new Seri("1.0.2", T(2019, 12, 31)), new Seri("1.1.0", T(2019, 9, 11)), new Seri("1.1.1", T(2023, 9, 11)) }),
        new("EOL-IIS", "Microsoft IIS", R(@"\bMicrosoft-IIS[/ ]+(?<v>\d+\.\d+)"), new[] { new Seri("5.0", T(2010, 7, 13)), new Seri("5.1", T(2014, 4, 8)), new Seri("6.0", T(2015, 7, 14)), new Seri("7.0", T(2020, 1, 14)), new Seri("7.5", T(2020, 1, 14)), new Seri("8.0", T(2023, 10, 10)), new Seri("8.5", T(2023, 10, 10)) }),
        new("EOL-TOMCAT", "Apache Tomcat", R(@"\bTomcat[/ ]+(?<v>\d+\.\d+(?:\.\d+)?)"), new[] { new Seri("5.", T(2012, 12, 31)), new Seri("6.", T(2016, 12, 31)), new Seri("7.", T(2021, 3, 31)), new Seri("8.0", T(2018, 6, 30)), new Seri("8.5", T(2024, 3, 31)) }),
        new("EOL-MYSQL", "MySQL", R(@"(?<!Maria)MySQL\D{0,25}(?<v>\d+\.\d+\.\d+)"), new[] { new Seri("5.0", T(2012, 1, 9)), new Seri("5.1", T(2013, 12, 31)), new Seri("5.5", T(2018, 12, 3)), new Seri("5.6", T(2021, 2, 5)), new Seri("5.7", T(2023, 10, 31)), new Seri("8.0", T(2026, 4, 30)) }),
        new("EOL-POSTGRESQL", "PostgreSQL", R(@"\bPostgreSQL(?:\s+DB)?[/ ]+(?<v>\d+(?:\.\d+)?)"), new[] { new Seri("9.", T(2021, 11, 11)), new Seri("10", T(2022, 11, 10)), new Seri("11", T(2023, 11, 9)), new Seri("12", T(2024, 11, 14)), new Seri("13", T(2025, 11, 13)), new Seri("14", T(2026, 11, 12)) }),
        // Sürüm eşiğiyle "eski" sayılanlar (resmî EOL tarihi yok; üretici yalnızca güncel sürümlere yama verir).
        new("EOL-OPENSSH", "OpenSSH", R(@"\bOpenSSH[_ /]+(?<v>\d+\.\d+)"), Array.Empty<Seri>(), new Version(8, 0)),
        new("EOL-NGINX", "nginx", R(@"\bnginx[/ ]+(?<v>\d+\.\d+(?:\.\d+)?)"), Array.Empty<Seri>(), new Version(1, 18)),
        // Bilinen arka kapılı sürümler (tarihî, belgelenmiş olaylar: bu sürümler gerçek kurulumlarda bulunmamalıdır).
        new("EOL-VSFTPD", "vsftpd", R(@"\bvsFTPd[ /]+(?<v>\d+\.\d+\.\d+)"), Array.Empty<Seri>(), null, "2.3.4", "vsftpd 2.3.4 sürümünün 2011'de arka kapılı (kötü amaçlı kodlu) dağıtıldığı bilinmektedir."),
        new("EOL-PROFTPD", "ProFTPD", R(@"\bProFTPD[ /]+(?<v>\d+\.\d+\.\d+[a-z]?)"), Array.Empty<Seri>(), null, "1.3.3c", "ProFTPD 1.3.3c sürümünün 2010'da arka kapılı dağıtıldığı bilinmektedir.")
    };

    // Dağıtım yamaları hakkında ortak not.
    private const string YamaNotu = "Not: Linux dağıtımları güvenlik yamalarını sürüm numarasını değiştirmeden uygulayabilir; paket yöneticinizle (apt/yum/dnf) gerçek yama düzeyini doğrulayın.";

    // Sürüm serisine uyuyor mu?
    private static bool SeriUyar(string v, string onek) => v == onek || v.StartsWith(onek + ".", StringComparison.Ordinal) || (onek.EndsWith('.') && v.StartsWith(onek, StringComparison.Ordinal)) || (onek.Length <= 2 && !onek.EndsWith('.') && v.Split('.')[0] == onek);

    // Sürüm metnini Version'a çevirir (yalnızca ana.ikincil).
    private static Version? Surum(string v) { var p = v.Split('.'); return p.Length >= 2 && int.TryParse(p[0], out var a) && int.TryParse(p[1], out var b) ? new Version(a, b) : null; }

    // Tek bir metni (tabela/ürün+sürüm) değerlendirir; sorun yoksa boş liste. simdi: ömür sonu karşılaştırma tarihi (testte sabitlenir).
    public static List<EolSonucu> Degerlendir(string? metin, DateTime? simdi = null)
    {
        var sonuc = new List<EolSonucu>(); if (string.IsNullOrWhiteSpace(metin)) return sonuc; var bugun = simdi ?? DateTime.UtcNow; string m = metin.Length > 600 ? metin[..600] : metin;
        foreach (var k in Kurallar)
        {
            // MariaDB, MySQL ile aynı protokolü konuşur ve sürümü "5.5.5-10.x" biçiminde yazar; MySQL sürümüyle karıştırılmaz.
            if (k.Kimlik == "EOL-MYSQL" && m.Contains("MariaDB", StringComparison.OrdinalIgnoreCase)) continue;
            Match e; try { e = k.Kalip.Match(m); } catch (RegexMatchTimeoutException) { continue; } if (!e.Success) continue; string v = e.Groups["v"].Value;
            // 1) Belgelenmiş tehlikeli sürüm.
            if (k.TehlikeliSurum != null && string.Equals(v, k.TehlikeliSurum, StringComparison.OrdinalIgnoreCase))
            { sonuc.Add(Uret(k, v, "Tehlikeli sürüm", $"{k.Urun} {v} bilinen tehlikeli bir sürümdür.", k.TehlikeliNot ?? "", $"{k.Urun} hizmetini resmî kaynaktan alınmış güncel ve doğrulanmış sürümle (imza/özet kontrolüyle) yeniden kurun; bu sunucunun ele geçirilmiş olma ihtimalini de inceleyin.", 9, Onem.Yuksek)); continue; }
            // 2) Resmî ömür sonu serisi.
            var seri = k.Seriler.Where(s => SeriUyar(v, s.Onek)).OrderByDescending(s => s.Onek.Length).FirstOrDefault();
            if (seri != null && bugun >= seri.Bitis)
            { sonuc.Add(Uret(k, v, "Desteği bitmiş", $"{k.Urun} {v} sürüm serisinin güvenlik desteği {seri.Bitis:MM.yyyy} tarihinde sona ermiştir.", "Üretici bu seri için artık güvenlik yaması yayınlamıyor; bu sürümdeki yeni bulunan açıklar kapatılmaz.", $"{k.Urun} yazılımını desteklenen güncel bir sürüme yükseltin; yükseltme hemen mümkün değilse hizmeti yalnızca güvenilen ağlara açın ve bir sonraki bakım penceresine planlayın. " + YamaNotu, 7, Onem.Yuksek)); continue; }
            // 3) Sürüm eşiği: üretici yalnızca güncel sürümleri destekler.
            if (k.EskiEsigi != null && Surum(v) is { } sv && sv < k.EskiEsigi)
                sonuc.Add(Uret(k, v, "Eski sürüm", $"{k.Urun} {v}, {k.EskiEsigi} sürümünden daha eski.", $"{k.Urun} projesi yalnızca güncel sürümlere güvenlik yaması verir; bu kadar eski bir sürümde bilinen açıklar bulunma ihtimali yüksektir.", $"{k.Urun} yazılımını güncelleyin. " + YamaNotu, 4, Onem.Orta));
        }
        return sonuc;
    }

    // Sonuç kaydı üretici.
    private static EolSonucu Uret(Kural k, string v, string durum, string ne, string neden, string cozum, double etki, Onem onem) => new(k.Kimlik, k.Urun, v, durum, $"{durum}: {k.Urun} {v}", ne, neden, cozum, etki, onem);

    // ---- Ana tablo (Bulgu) çıktıları ----
    // Port taramasından: servis adı + banner birlikte değerlendirilir (MySQL gibi bannerı yalnızca sürüm taşıyan hizmetler için servis adı öne eklenir).
    public static List<Bulgu> PortBulgulari(PortSonucu p, DateTime? simdi = null)
        => Degerlendir($"{p.Servis} {p.Banner}", simdi).Select(e => new Bulgu("Eski Yazılım", $"{e.Baslik} (port {p.Port})", e.Onem, $"Port {p.Port} ({p.Servis}) tabelası: {(string.IsNullOrEmpty(p.Banner) ? "—" : p.Banner)}\n{e.Ne}\n{e.Neden}".TrimEnd(), e.Cozum)).ToList();

    // ---- Web sitesi (pasif/aktif gözlem) çıktıları ----
    // Sitenin "Server" ve "X-Powered-By" başlıklarındaki sürüm yazısından eski yazılım bulgusu üretir. Başlık yoksa/sürüm vermiyorsa boş döner.
    // Not: bazı Linux dağıtımları yamaları eski sürüm numarasına geri taşır (backport); bu yüzden çözüm metninde doğrulama notu vardır.
    internal static List<PasifBulgu> WebBulgulari(HttpGozlem? http, DateTime? simdi = null)
    {
        // Sonuç ve tekrar engelleyici.
        var l = new List<PasifBulgu>(); if (http?.Basliklar == null) return l; var goruldu = new HashSet<string>();
        // İncelenecek başlıklar.
        foreach (var ad in new[] { "server", "x-powered-by" })
        {
            // Başlık değeri yoksa geç.
            if (!http.Basliklar.TryGetValue(ad, out var deger) || string.IsNullOrWhiteSpace(deger)) continue;
            // Her eski sürüm sonucu için bulgu.
            foreach (var e in Degerlendir(deger, simdi))
                // Aynı ürün+sürüm iki başlıkta geçerse bir kez yaz.
                if (goruldu.Add(e.Kimlik + "|" + e.Surum))
                    l.Add(new PasifBulgu(e.Kimlik, "Eski Yazılım", e.Baslik + " (web sunucusu)", e.Ne + " Sitenin " + ad + " başlığı bunu herkese söylüyor.", e.Neden, e.Cozum + " Sürüm yazısını gizlemek (ServerTokens Prod / server_tokens off) saldırganın işini zorlaştırır ama asıl çözüm güncellemedir.", null, Array.Empty<string>(), new[] { "A.8.8", "A.8.9" }, new[] { "T1190" }, e.Etki, $"{ad}: {(deger.Length > 80 ? deger[..80] + "…" : deger)}", 60, e.Onem));
        }
        // Sonuç.
        return l;
    }

    // ---- Ağ haritası (V2 Finding) çıktıları ----
    // nmap hizmet kaydından (ürün + sürüm + ek bilgi) bulgu üretir; varlık kimliği "net:adres:port".
    public static List<Finding> BulguUret(string adres, ServiceEndpoint s, double varlikAgirligi, DateTime simdi)
    {
        // Yalnızca açık kapılar.
        if (!string.Equals(s.State, "open", StringComparison.OrdinalIgnoreCase)) return new();
        string metin = $"{s.Product} {s.Version} {s.ExtraInfo}".Trim(); if (metin.Length == 0) metin = s.Service ?? "";
        // nmap ürün adını kalıbın bulabileceği biçime getir ("Apache httpd 2.2.15" zaten uyar; "OpenSSH 7.4" uyar; "Microsoft IIS httpd 7.5" -> Microsoft-IIS).
        metin = metin.Replace("Microsoft IIS httpd", "Microsoft-IIS", StringComparison.OrdinalIgnoreCase);
        // MySQL: nmap ürün="MySQL", sürüm="5.7.33".
        return Degerlendir(metin, simdi).Select(e => BulguFabrikasi.Yap(e.Kimlik, "Ağ Envanteri", $"net:{adres}:{s.Port}", e.Kimlik + "|" + e.Surum, e.Baslik, e.Ne, e.Neden, new[] { $"Adres: {adres}:{s.Port}/{s.Proto}", "Okunan: " + metin }, e.Etki, varlikAgirligi, new[] { "A.8.8", "A.8.9" }, new[] { "T1190" }, e.Cozum, KontrolKimligi, simdi)).ToList();
    }

    // Bir taramadaki tüm cihazlar için bulgular.
    public static List<Finding> TaramaBulgulari(NormalizedScan tarama, Func<string, double> varlikAgirligi, DateTime simdi)
        => tarama.Hosts.SelectMany(h => h.Services.SelectMany(s => BulguUret(h.Address, s, varlikAgirligi(h.Address), simdi))).ToList();
}
