// Bu dosya "Kali Araç Rehberi"dir: eğitim rehberindeki araçları, YASAL kullanım sınırlarıyla ve SAVUNMA karşılıklarıyla birlikte listeler.
// Her araç üç sınıftan birine girer:
//   Calistirilir : bu uygulamadan, sensör üzerinden, sabit argümanlarla ve onayla çalıştırılabilir (komut şablonu: kali_araclari.json; eski beş araç: SensorArac; nmap kendi sekmesinde).
//   Egitim       : uygulamadan çalıştırma henüz bağlanmamış savunma/bilgi toplama araçları; Kali'de elle kullanılır (ürün mantığına uygundur, yalnızca bağlantı bekliyor).
//   Kapali       : saldırı odaklı araçlar (parola kırma, araya girme, exploit, kablosuz saldırı, brute-force). Uygulamada ÇALIŞTIRMA YOKTUR;
//                  yalnızca "ne olduğu, yasal sınırı ve nasıl savunulacağı" anlatılır (kopyalanıp çalıştırılabilir saldırı komutu içermez).
// Bu liste yalnızca bilgidir; hiçbir şeye dokunmaz.
namespace LabDenetim;

// Araç sınıfı.
internal enum AracSinifi { Calistirilir, Egitim, Kapali }

// Rehber kaydı. Calistir: AracTuru karşılığı (nmap için null; nmap kendi sekmesinden çalışır).
// Sablonlar: bu araç için kali_araclari.json'daki komut şablonlarının adları (yeni, tablo tabanlı yol; bkz. KaliRehberi.Sablonlar.cs). Calistir ise eski sabit beş aracın yoludur.
internal sealed record KaliAraci(string Ad, string Kategori, AracSinifi Sinif, string Ne, string Yasal, string Savunma, AracTuru? Calistir = null, string? UygulamaKarsiligi = null, string[]? Sablonlar = null);

// Rehber.
internal static partial class KaliRehberi
{
    // Ortak uyarı (rehberin başındaki yasal çerçeve).
    public const string Uyari = "Bu araçların çoğu ÇİFT KULLANIMLIDIR. Yalnızca kendi cihazlarınızda/ağınızda, yazılı izniniz olan sistemlerde veya yasal pratik platformlarında (TryHackMe, HackTheBox, PortSwigger Academy, kendi kurduğunuz DVWA/Metasploitable VM, scanme.nmap.org) kullanın. İzinsiz tarama veya saldırı Türkiye'de TCK 243-245 (bilişim suçları) kapsamında suçtur.";

    // Başka birinin sistemine karşı kullanılmasın uyarısı (kısa).
    private const string Izin = "Yalnızca kendi ortamınızda veya yazılı izinle.";
    private const string Saldiri = "Saldırı odaklıdır: yalnızca kendi yetkili test ortamınızda veya yazılı sözleşmeli sızma testinde yasaldır; başkasına karşı kullanmak suçtur.";

    // Ham kayıtlar (sınıfları ve şablon bağlantısı olmadan). Dışarıya "Tum" verilir (KaliRehberi.Sablonlar.cs): şablonu olan araçlar orada "Calistirilir" sınıfına geçer.
    private static readonly IReadOnlyList<KaliAraci> Ham = new List<KaliAraci>
    {
        // ---- 1. Bilgi toplama ----
        new("Nmap", "1. Bilgi Toplama", AracSinifi.Calistirilir, "Ağdaki cihazları, açık kapıları ve çalışan servisleri tarar.", Izin + " scanme.nmap.org taramaya açıktır.", "Gereksiz açık kapıları kapatın, güvenlik duvarında yalnızca gerekenlere izin verin.", null, "Ağ Haritası ve Sensör nmap (yalnızca özel ağ)"),
        new("Masscan", "1. Bilgi Toplama", AracSinifi.Kapali, "Çok yüksek hızlı kapı tarayıcı (çok sayıda adresi saniyeler içinde tarar).", Saldiri + " Yüksek hız ağı bozabilir.", "Dışarıya açık kapıları düzenli kontrol edin; tarama denemelerini güvenlik duvarı günlüklerinden izleyin."),
        new("Dnsenum", "1. Bilgi Toplama", AracSinifi.Egitim, "Alan adının DNS kayıtlarını ve alt alan adlarını keşfeder.", "Yalnızca kendi alan adınızda. example.com test amaçlı ayrılmıştır.", "Gereksiz alt alanları kapatın; DNS bölge aktarımını (AXFR) yalnızca yetkili sunuculara açın.", null, "Pasif Gözlem DNS kontrolleri (SPF/DMARC/CAA)"),
        new("TheHarvester", "1. Bilgi Toplama", AracSinifi.Egitim, "Arama motorlarından bir alan adına ait e-posta ve alt alan bilgisi toplar (OSINT).", "Herkese açık kaynakları okur; yine de yalnızca kendi kuruluşunuz için kullanın.", "Çalışan e-postalarının internette ne kadar göründüğünü kontrol edin; oltalama farkındalığı verin."),
        new("Whois", "1. Bilgi Toplama", AracSinifi.Calistirilir, "Alan adının kime kayıtlı olduğunu, kayıt tarihini ve DNS sunucularını gösterir.", "Herkese açık, yasal bir sorgudur.", "Kayıt bilgilerinizin güncel ve gizlilik korumalı olduğundan emin olun.", AracTuru.Whois),
        new("Netdiscover", "1. Bilgi Toplama", AracSinifi.Egitim, "Yerel ağdaki aktif cihazları ARP ile bulur.", "Yalnızca kendi ağınızda.", "Ağınızdaki bilinmeyen cihazları envanterle karşılaştırın.", null, "Ağ Haritası (cihaz keşfi)"),
        new("Recon-ng", "1. Bilgi Toplama", AracSinifi.Egitim, "Komut satırı tabanlı modüler OSINT çerçevesi.", "Herkese açık kaynaklar; yalnızca kendi kuruluşunuz için.", "Kuruluşunuzun internetteki dijital ayak izini kendiniz izleyin."),
        new("SpiderFoot", "1. Bilgi Toplama", AracSinifi.Egitim, "Bir alan adı/IP/e-posta için onlarca kaynaktan otomatik OSINT toplar, web arayüzü sunar.", "Yalnızca kendi varlıklarınız için.", "Çıkan açık bilgileri (eski alt alanlar, sızmış e-postalar) kapatın."),
        new("Maltego", "1. Bilgi Toplama", AracSinifi.Egitim, "OSINT verilerini ilişki grafiği olarak gösterir.", "Yalnızca kendi kuruluşunuz için.", "Dışarıdan görünen bilgilerin birbiriyle nasıl ilişkilendirilebildiğini görüp azaltın."),

        // ---- 2. Ağ analizi ----
        new("Wireshark", "2. Ağ Analizi", AracSinifi.Egitim, "Ağ trafiğini canlı yakalayıp paket paket inceleyen standart analiz aracı.", "Yalnızca kendi ağınızın trafiği; başkalarının trafiğini dinlemek suçtur.", "Şifrelenmemiş protokolleri (HTTP, FTP, Telnet) kaldırın; trafiği TLS ile koruyun."),
        new("Tcpdump", "2. Ağ Analizi", AracSinifi.Egitim, "Wireshark'ın komut satırı karşılığı: trafiği yakalar ve .pcap dosyasına yazar.", "Yalnızca kendi ağınız/arayüzünüz.", "Yakalanan dosyalarda şifre ve kişisel veri olabilir: güvenli saklayın, işi bitince silin."),
        new("Ettercap", "2. Ağ Analizi", AracSinifi.Kapali, "ARP zehirleme ile araya girme (MITM) saldırılarını benzetir.", Saldiri, "Anahtarlarda dinamik ARP denetimi (DAI) ve port güvenliği açın; trafiği TLS ile koruyun; ağ içi ARP anormalliklerini izleyin."),
        new("Bettercap", "2. Ağ Analizi", AracSinifi.Kapali, "Ağ keşfi, MITM ve Wi-Fi analizi yapan çok amaçlı araç.", Saldiri, "Ağ bölümleme, DAI, sertifika sabitleme (pinning) ve her yerde TLS kullanın."),
        new("Macchanger", "2. Ağ Analizi", AracSinifi.Egitim, "Ağ kartının MAC adresini geçici olarak değiştirir (gizlilik/test).", "Kendi cihazınızda yasaldır; başkasının cihazını taklit etmek suçtur.", "MAC adresine dayalı güvenlik filtrelerine tek başına güvenmeyin."),
        new("Dsniff", "2. Ağ Analizi", AracSinifi.Kapali, "Ağ trafiğinden şifrelenmemiş parolaları yakalayan eski araç seti.", Saldiri, "Şifresiz protokolleri kapatın; tüm hizmetlerde TLS/SSH kullanın."),
        new("Mitmproxy", "2. Ağ Analizi", AracSinifi.Egitim, "HTTP/HTTPS trafiğini proxy olarak izlemeyi/değiştirmeyi sağlar (kendi uygulamanızı incelemek için).", "Yalnızca kendi uygulamanız/cihazınız.", "Uygulamalarınızda sertifika sabitleme (pinning) kullanın."),
        new("Hping3", "2. Ağ Analizi", AracSinifi.Kapali, "Özel hazırlanmış TCP/IP paketleri gönderir (güvenlik duvarı testi).", Saldiri + " Yanlış kullanım hizmet aksatır.", "Güvenlik duvarı ve hız sınırlama kurallarınızı kendi test ortamınızda sınayın."),
        new("Netcat", "2. Ağ Analizi", AracSinifi.Egitim, "Basit TCP/UDP bağlantıları kurar, port dinler, dosya aktarır.", "Kendi iki makineniz arasında.", "Beklenmeyen dinleyen kapıları ve dışarı çıkan bağlantıları izleyin.", null, "Trafik İzleyici / dinleyen kapılar"),
        new("Proxychains4", "2. Ağ Analizi", AracSinifi.Egitim, "Programların trafiğini proxy/Tor zinciri üzerinden geçirir.", "Anonimlik yasal bir araçtır; suç işlemek için kullanımı suçtur.", "Kuruluşunuzda Tor/anonimleştirici çıkış trafiğini politikayla yönetin."),
        new("Tor", "2. Ağ Analizi", AracSinifi.Egitim, "Trafiği çok katmanlı şifreleyip yönlendirerek IP adresini gizler.", "Yasal bir gizlilik aracıdır.", "Kurumsal ağda Tor kullanımını politikayla belirleyin.", null, "IP Sızıntı Testi (tünel/VPN denetimi)"),

        // ---- 3. Web güvenliği ----
        new("Burp Suite", "3. Web Güvenliği", AracSinifi.Egitim, "Tarayıcı ile sunucu arasındaki trafiği yakalayan, web güvenlik testi standardı proxy.", "Yalnızca kendi uygulamanız veya PortSwigger Academy gibi eğitim platformları.", "Girdi doğrulama, parametreli sorgu, oturum ve yetki kontrollerini uygulama kodunda sağlayın."),
        new("Sqlmap", "3. Web Güvenliği", AracSinifi.Kapali, "SQL injection açıklarını otomatik bulup veri çekmeyi dener.", Saldiri, "Parametreli sorgu/ORM kullanın, veritabanı hesabına en az yetki verin, web uygulama güvenlik duvarı (WAF) ekleyin."),
        new("Nikto", "3. Web Güvenliği", AracSinifi.Calistirilir, "Web sunucusundaki bilinen zayıflıkları, eski sürümleri ve yanlış yapılandırmaları tarar.", Izin + " Uygulama yalnızca zararsız sondalarla (123b) çalıştırır.", "Çıkan unutulmuş dosyaları silin, sürümleri güncelleyin, gereksiz modülleri kapatın.", AracTuru.Nikto, "Aktif Web Denetimi"),
        new("WPScan", "3. Web Güvenliği", AracSinifi.Egitim, "WordPress eklenti/tema/kullanıcı zayıflıklarını tarar.", "Yalnızca kendi WordPress sitenizde.", "Çekirdek, tema ve eklentileri güncel tutun; kullanılmayanları silin; kullanıcı adı sızıntısını kapatın."),
        new("Dirb", "3. Web Güvenliği", AracSinifi.Kapali, "Kelime listesiyle gizli dizin/dosya arar.", Saldiri, "Web kökünde yedek/yönetim dosyası bırakmayın; dizin listelemeyi kapatın. (Uygulamanın Aktif Web Denetimi bilinen hassas yolları zaten nazikçe kontrol eder.)", null, "Aktif Web Denetimi (hassas yol kontrolü)"),
        new("Gobuster", "3. Web Güvenliği", AracSinifi.Kapali, "Dirb'e benzer ama daha hızlı: dizin, dosya ve alt alan adı tarar.", Saldiri, "Beklenmeyen yolları kapatın, kaba tarama davranışını hız sınırlamayla yavaşlatın."),
        new("Wfuzz", "3. Web Güvenliği", AracSinifi.Kapali, "Web parametrelerini/dizinlerini liste ile deneyerek fuzzing yapar.", Saldiri, "Sunucu tarafı girdi doğrulaması ve hız sınırlama uygulayın."),
        new("OWASP ZAP", "3. Web Güvenliği", AracSinifi.Egitim, "Ücretsiz, açık kaynak web güvenlik tarayıcısı.", "Yalnızca kendi sitelerinizde (sahiplik doğrulamalı).", "Bulunan başlık/çerez/girdi sorunlarını giderin.", null, "Kendi siteniz için: Derin Denetim (sahiplik doğrulamalı)"),

        // ---- 4. Şifre kırma ----
        new("John the Ripper", "4. Şifre Kırma", AracSinifi.Kapali, "Parola özetlerini (hash) kırmayı dener.", "Yalnızca yetkili sızma testinde veya kendi hash'lerinizi sınamak için yasaldır.", "Uzun, benzersiz parolalar; bcrypt/argon2 gibi yavaş özetleme; iki aşamalı doğrulama."),
        new("Hydra", "4. Şifre Kırma", AracSinifi.Kapali, "SSH, FTP, web formu gibi hizmetlere deneme-yanılma parola saldırısı yapar.", Saldiri, "Hesap kilitleme/hız sınırlama, güçlü parola, iki aşamalı doğrulama, SSH'ta anahtarla giriş ve parolayı kapatma. (Uygulamanın Giriş Saldırı Radarı bu denemeleri tespit eder.)", null, "Giriş Saldırı Radarı (brute-force algılama)"),
        new("Hashcat", "4. Şifre Kırma", AracSinifi.Kapali, "GPU hızlandırmalı parola özeti kırıcı.", "Yalnızca yetkili sızma testi veya kendi hash'leriniz.", "Parolaları yavaş ve tuzlu özetle saklayın; uzun parola cümleleri kullanın."),
        new("Crunch", "4. Şifre Kırma", AracSinifi.Kapali, "Kurallara göre özel parola listesi üretir.", "Liste üretmek yasaldır; izinsiz giriş denemesinde kullanmak suçtur.", "Tahmin edilebilir parola desenlerini yasaklayın."),
        new("CeWL", "4. Şifre Kırma", AracSinifi.Kapali, "Bir sitedeki kelimelerden hedefe özel parola listesi çıkarır.", Saldiri, "Kurumsal terimlerden oluşan parolaları yasaklayın."),
        new("Medusa", "4. Şifre Kırma", AracSinifi.Kapali, "Hydra gibi paralel çalışan parola deneme aracı.", Saldiri, "Hesap kilitleme, hız sınırlama ve iki aşamalı doğrulama."),

        // ---- 5. Exploit ----
        new("Metasploit Framework", "5. Exploit Çerçevesi", AracSinifi.Kapali, "Bilinen açıkları test edip sisteme sızma denemesi yapan çerçeve.", "Yalnızca kendi yetkili test ortamınızda (Metasploitable VM) veya yazılı sözleşmeli sızma testinde.", "Yamaları zamanında uygulayın, gereksiz hizmetleri kapatın, ağ bölümlemesi ve EDR kullanın."),
        new("Searchsploit", "5. Exploit Çerçevesi", AracSinifi.Calistirilir, "Bilinen açık (CVE) ve exploit kayıtlarının yerel veri tabanında arama yapar.", "Arama yapmak yasaldır. Uygulama yalnızca KAYIT LİSTELER; exploit çalıştırmaz veya indirmez.", "Bulduğunuz sürümler için üretici yamalarını uygulayın.", AracTuru.Searchsploit, "CVE/KEV eşleştirme (Yazılım Envanteri)"),

        // ---- 6. Kablosuz ----
        new("Aircrack-ng", "6. Kablosuz Ağ", AracSinifi.Kapali, "Wi-Fi şifrelerini (WEP/WPA) kırmayı dener.", "Yalnızca KENDİ Wi-Fi ağınızda. Başkasının ağı suçtur.", "WPA3 veya WPA2 + uzun rastgele parola; WPS'i kapatın."),
        new("Reaver", "6. Kablosuz Ağ", AracSinifi.Kapali, "WPS açığı olan yönlendiricilere PIN saldırısı yapar.", "Yalnızca kendi yönlendiriciniz.", "Yönlendiricide WPS'i kapatın, yazılımını güncelleyin."),
        new("Wifite", "6. Kablosuz Ağ", AracSinifi.Kapali, "Wi-Fi saldırı araçlarını otomatikleştirir.", "Yalnızca kendi Wi-Fi ağınız.", "WPA3, güçlü parola, WPS kapalı."),
        new("Kismet", "6. Kablosuz Ağ", AracSinifi.Egitim, "Kablosuz ağları/cihazları pasif olarak algılayan dedektör (IDS).", "Pasif dinleme; yalnızca kendi ortamınızda ve yasal sınırlar içinde.", "Yetkisiz erişim noktalarını (rogue AP) bulup kaldırın."),

        // ---- 7. Adli bilişim ----
        new("Autopsy", "7. Adli Bilişim", AracSinifi.Egitim, "Disk imajlarını analiz eden grafik arayüzlü adli bilişim aracı.", "Yalnızca sahibi olduğunuz veya yetkili olduğunuz imajlar.", "Olay müdahale planınıza adli imaj alma adımlarını ekleyin."),
        new("Binwalk", "7. Adli Bilişim", AracSinifi.Egitim, "Firmware içindeki gömülü dosya ve dosya sistemlerini çıkarır (IoT analizi).", "Kendi cihazınızın firmware'i.", "Cihaz firmware'inde sabit parola/anahtar bırakmayın."),
        new("Foremost", "7. Adli Bilişim", AracSinifi.Egitim, "Disk imajından silinmiş dosyaları imzalarına göre kurtarır.", "Yalnızca kendi imajlarınız.", "Hassas verili diskleri atmadan önce güvenli silin."),
        new("Sleuth Kit", "7. Adli Bilişim", AracSinifi.Egitim, "Disk imajlarını komut satırından inceleyen araç seti.", "Yalnızca yetkili olduğunuz imajlar.", "Diskleri şifreleyin; imaj alma zincirini belgeleyin."),
        new("Volatility3", "7. Adli Bilişim", AracSinifi.Egitim, "RAM imajlarını analiz eder: işlemler, ağ bağlantıları, zararlı yazılım izleri.", "Yalnızca yetkili olduğunuz bellek imajları.", "Olay anında bellek imajı almayı prosedüre ekleyin."),

        // ---- 8. Ters mühendislik ----
        new("Ghidra", "8. Ters Mühendislik", AracSinifi.Egitim, "Derlenmiş programları okunabilir sözde koda çeviren ters mühendislik aracı.", "Kendi programlarınız, CTF dosyaları ve izinli örnekler; lisans şartlarına dikkat.", "Zararlı yazılım analizi tespit kurallarınızı geliştirmenize yardım eder."),
        new("Radare2", "8. Ters Mühendislik", AracSinifi.Egitim, "Komut satırı tabanlı ikili dosya analizi ve hata ayıklama aracı.", "Kendi programlarınız ve izinli örnekler.", "Uygulamalarınızı derlerken güvenlik bayraklarını (ASLR, DEP, stack canary) açın."),
        new("GDB", "8. Ters Mühendislik", AracSinifi.Egitim, "Çalışan programı adım adım inceleyen hata ayıklayıcı.", "Kendi programlarınız.", "Bellek güvenliği açıklarını kodlama aşamasında yakalayın."),

        // ---- Ek: savunma denetimi (rehberde yok; Kali'de yaygın, yalnızca savunma amaçlı) ----
        new("sslscan", "Ek: Savunma Denetimi", AracSinifi.Calistirilir, "Sunucunun desteklediği TLS sürümlerini/şifreleri ve sertifikasını listeler.", Izin, "Eski TLS/zayıf şifreleri kapatın, sertifikayı yenileyin.", AracTuru.Sslscan, "Pasif/Aktif TLS kontrolleri"),
        new("ssh-audit", "Ek: Savunma Denetimi", AracSinifi.Calistirilir, "SSH sunucusunun yapılandırmasını okuyup zayıf algoritmaları gösterir; parola denemez.", Izin, "Zayıf algoritmaları kapatın, parolayı kapatıp anahtarla girişe geçin.", AracTuru.SshAudit)
    };

    // Kategorilerin sıralı listesi.
    public static IEnumerable<string> Kategoriler => Tum.Select(x => x.Kategori).Distinct();

    // Adı veya açıklaması aranan metni içerenler (boşsa hepsi).
    public static List<KaliAraci> Ara(string? metin)
    {
        // Boşsa hepsi.
        var m = (metin ?? "").Trim(); if (m.Length == 0) return Tum.ToList();
        // Büyük/küçük harf ve Türkçe karakter duyarsız eşleşme.
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        return Tum.Where(x => tr.CompareInfo.IndexOf(x.Ad + " " + x.Ne + " " + x.Kategori, m, System.Globalization.CompareOptions.IgnoreCase) >= 0).ToList();
    }

    // Sınıfın kısa Türkçe adı.
    public static string SinifAdi(AracSinifi s) => s switch { AracSinifi.Calistirilir => "Uygulamadan çalıştırılabilir", AracSinifi.Egitim => "Kali'de elle kullanılır (uygulamadan çalıştırma henüz bağlı değil)", _ => "Saldırı odaklı: uygulamada çalıştırılmaz" };
}
