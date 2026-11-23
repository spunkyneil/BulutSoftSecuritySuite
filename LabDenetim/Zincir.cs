// Bu dosya "Saldırgan Gözüyle Zincirleme Risk Analizi"ni yapar: tarama bitince tek tek bulunan sorunları birbirine bağlar.
// Çünkü saldırganlar tek bir açığı değil, birkaç küçük açığı art arda kullanarak (zincirleyerek) içeri girer.
// Sonuç hem tabloya satır olarak hem de tablonun üstündeki kırmızı "uyarı şeridine" yazılır. Program hiçbir şeyi değiştirmez.
namespace LabDenetim;

// Bulgu listesinden zincirleme riskleri çıkaran yardımcı (saf hesap; ekrana dokunmaz, bu yüzden kolay test edilir).
internal static class ZincirAnalizi
{
    // Zincirleme risk bulgularının ortak başlığı (tabloda ve raporda bu adla görünür).
    public const string Baslik = "⛓ Zincirleme Risk Uyarısı";

    // Bir kapının (port) açık bulunduğunu söyler.
    private static bool KapiAcik(IEnumerable<Bulgu> l, int port) => l.Any(b => !b.Atlandi && b.Modul == "Port" && b.Baslik.StartsWith($"Açık port {port}", StringComparison.Ordinal));

    // Hassas bir alt alan adı (admin, dev, test, backup...) bulunmuş mu?
    private static bool HassasAltAlan(IEnumerable<Bulgu> l) => l.Any(b => !b.Atlandi && b.Modul == "DNS" && b.Baslik.StartsWith("Alt isim bulundu:", StringComparison.Ordinal) && b.Onem != Onem.Bilgi);

    // Şifresiz (http) web hizmeti bulunmuş mu?
    private static bool SifresizHttp(IEnumerable<Bulgu> l) => l.Any(b => !b.Atlandi && b.Onem != Onem.Bilgi && (b.Baslik.StartsWith("Şifresiz web sitesi hizmeti", StringComparison.Ordinal) || (b.Modul == "HTTP" && b.Baslik.Contains("şifresiz", StringComparison.OrdinalIgnoreCase))));

    // Hosts dosyasında şüpheli yönlendirme var mı?
    private static bool HostsSupheli(IEnumerable<Bulgu> l) => l.Any(b => !b.Atlandi && b.Modul == "Hosts" && b.Baslik.StartsWith("hosts dosyasında", StringComparison.Ordinal) && b.Onem >= Onem.Orta);

    // Tüm zincir kurallarını dener ve uyanlar için bulgu üretir.
    public static List<Bulgu> Analiz(IReadOnlyCollection<Bulgu> bulgular)
    {
        // Sonuç listesi.
        var sonuc = new List<Bulgu>();
        // Daha önce eklenmiş zincir bulguları varsa tekrar üretme (aynı tarama iki kez analiz edilmesin).
        var l = bulgular.Where(b => b.Modul != "Zincir").ToList();
        // Kapı durumları.
        bool smb = KapiAcik(l, 445) || KapiAcik(l, 139), rdp = KapiAcik(l, 3389), telnet = KapiAcik(l, 23), ftp = KapiAcik(l, 21), vnc = KapiAcik(l, 5900);
        // Veritabanı kapısı ve web kapısı.
        bool db = KapiAcik(l, 3306) || KapiAcik(l, 5432), web = KapiAcik(l, 80) || KapiAcik(l, 8080) || KapiAcik(l, 443) || KapiAcik(l, 8443);
        // Diğer işaretler.
        bool alt = HassasAltAlan(l), http = SifresizHttp(l), hosts = HostsSupheli(l);

        // Kural 1 (ana senaryo): bilgi toplama + dosya paylaşımı kapısı.
        if (smb && (alt || http))
            sonuc.Add(new Bulgu("Zincir", Baslik, Onem.Yuksek,
                "Açık bulunan kapılar ve zayıf ayarlar bir saldırganın önce bilgi toplayıp ardından dosya paylaşım kapısından içeri sızmasına olanak tanıyabilir.\n" +
                $"Zincirin halkaları: 1) {(alt ? "hassas bir alt alan adı (admin/dev gibi) saldırgana yol gösteriyor" : "şifresiz web hizmeti (http) bilgi sızdırıyor")}  →  2) dosya paylaşım kapısı (445/SMB) açık  →  3) içeri sızma.",
                "Zincirin en kolay koparılacak halkası dosya paylaşım kapısıdır: kullanmıyorsanız 445'i güvenlik duvarıyla kapatın (Otomatik Onar). Ardından şifresiz web hizmetini https yapın veya hassas alt alan adını silin."));
        // Kural 2: iki uzaktan erişim kapısı birlikte açık, biri de şifresiz.
        if ((rdp || vnc) && (telnet || ftp))
            sonuc.Add(new Bulgu("Zincir", Baslik, Onem.Yuksek,
                $"Şifresiz bir kapı ({(telnet ? "Telnet" : "FTP")}) ile uzaktan ekran erişimi kapısı ({(rdp ? "Uzak Masaüstü" : "VNC")}) birlikte açık. Saldırgan şifresiz kapıyı dinleyerek parola çalıp ardından bilgisayarı uzaktan kullanabilir.",
                "Önce şifresiz kapıyı (Telnet/FTP) kapatın; uzaktan erişimi yalnızca güvenlik duvarıyla sınırlı tutun ve güçlü parola kullanın."));
        // Kural 3: veritabanı ile web sitesi birlikte açık.
        if (db && web)
            sonuc.Add(new Bulgu("Zincir", Baslik, Onem.Orta,
                "Hem web sitesi hem veritabanı kapısı ağdan erişilebilir. Web sitesindeki bir hata bulunursa saldırgan doğrudan veritabanını da deneyebilir; tüm kayıtlı bilgiler tek adımda tehlikeye girer.",
                "Veritabanı kapısını (3306/5432) yalnızca aynı bilgisayardan erişilir yapın veya güvenlik duvarıyla kapatın."));
        // Kural 4: hosts şüpheli ve şifresiz web.
        if (hosts && http)
            sonuc.Add(new Bulgu("Zincir", Baslik, Onem.Yuksek,
                "hosts dosyasında şüpheli yönlendirme var ve web hizmeti şifresiz. Saldırgan sizi sahte bir siteye yönlendirip yazdıklarınızı (parola dahil) şifresiz yoldan okuyabilir.",
                "hosts dosyasındaki bilmediğiniz satırları silin ve web hizmetini https yapın."));
        // Kural 5: virüs koruması kapalı ve başlangıçta şüpheli öğe var.
        bool defKapali = l.Any(b => b.Modul == "Karne" && b.Onem >= Onem.Yuksek && b.Baslik.Contains("Virüs koruması", StringComparison.Ordinal));
        // Şüpheli kalıcılık öğesi.
        bool kalici = l.Any(b => b.Modul == "Kalıcılık" && b.Onem >= Onem.Orta);
        // İkisi birlikteyse zincir.
        if (defKapali && kalici)
            sonuc.Add(new Bulgu("Zincir", Baslik, Onem.Yuksek, "Virüs koruması kapalıyken bilgisayar açılışında şüpheli bir program çalışıyor. Zararlı bir yazılım hiç engellenmeden yerleşmiş ve her açılışta geri geliyor olabilir.", "Önce virüs korumasını açıp tam tarama yapın, sonra Başlangıç öğelerindeki tanımadığınız programı kaldırın."));
        // Sonucu ver.
        return sonuc;
    }
}
