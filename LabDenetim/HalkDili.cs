// Bu dosya iki iş yapar:
//  1) ArayuzModu: programın "Kolay Görünüm" / "Uzman Görünümü" tercihini tutar (ilk açılışta KOLAY; tercih dosyada saklanır).
//  2) HalkDiliCevirici + Karne: teknik bulguları, hiç bilgisayar bilmeyen birinin anlayacağı dile ("Bu ne demek?" ve "Bana ne zararı olur?") çevirir
//     ve tarama sonunda gösterilen TRAFİK IŞIĞI KARNESİNİ (puan, durum cümlesi, en önemli 3 adım) hesaplar. Hiçbir şey sistemde değişiklik yapmaz.
using System.Text.Json;
using System.Text.RegularExpressions;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Görünüm modu.
internal enum ModTuru { Kolay, Uzman }

// Mod tercihi (kalıcı).
internal static class ArayuzModu
{
    // Tercih dosyası.
    public static string Yol => BulutKlasor.Dosya("arayuz_modu.json");
    private static ModTuru? _mod;
    // Mod değişince çağrılır.
    public static event Action? Degisti;

    // Şu anki mod: dosya yoksa/bozuksa KOLAY (varsayılan).
    public static ModTuru Mevcut { get => _mod ??= Oku(); private set => _mod = value; }
    private static ModTuru Oku() { try { if (File.Exists(Yol) && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Yol)) is { } d && d.TryGetValue("Mod", out var m) && m == "Uzman") return ModTuru.Uzman; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "HalkDili.Oku"); } return ModTuru.Kolay; }

    // Modu ayarlar ve kaydeder; yalnızca değişirse olay verir.
    public static void Ayarla(ModTuru m)
    {
        if (_mod == m) return; Mevcut = m;
        try { Depo.YazJson(Yol, new Dictionary<string, string> { ["Mod"] = m == ModTuru.Uzman ? "Uzman" : "Kolay" }); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "HalkDili.Ayarla", true); }
        Degisti?.Invoke();
    }

    // Testte bellekteki değeri sıfırlar (dosyadan yeniden okunsun).
    internal static void Sifirla() => _mod = null;
}

// Halk dilindeki açıklama.
internal sealed record HalkDili(string Baslik, string BuNeDemek, string Zarar, bool Ozel);

// Teknik terim → günlük hayat çevirici.
internal static class HalkDiliCevirici
{
    // Tablo: (başlık deseni, halk dili başlık, "bu ne demek", "bana ne zararı olur"). İlk eşleşen kazanır: özel olanlar öne konur.
    private static readonly (string Desen, string Baslik, string Ne, string Zarar)[] Tablo =
    {
        // ---- Eski / desteği bitmiş yazılım (Ağ Envanteri) ----
        (@"^Tehlikeli sürüm:", "Bilinen Zararlı Bir Yazılım Sürümü Çalışıyor", "Ağınızdaki bir cihazda, geçmişte içine gizli arka kapı konduğu belgelenmiş bir yazılım sürümü çalışıyor.", "Arka kapı, yabancıların izinsiz içeri girmesine yarar. Cihazın şimdiden ele geçirilmiş olma ihtimali vardır; hemen yeniden kurulmalıdır."),
        (@"^Desteği bitmiş:", "Üreticisi Artık Yama Vermeyen Eski Yazılım Çalışıyor", "Ağınızdaki bir cihazda, üreticinin güvenlik yaması vermeyi bıraktığı bir yazılım sürümü çalışıyor.", "Yeni bulunan açıklar bu sürümde hiç kapatılmaz. Eski bir kilidin anahtarının her yerde dolaştığını düşünün: kapı kilitli görünür ama güvenli değildir."),
        (@"^Bir sürücünün tamamı ağa paylaşılmış|^Windows veya kullanıcı klasörleri ağa paylaşılmış", "Bilgisayarınızın İçi Ağa Açık", "Bilgisayarın bir sürücüsü ya da önemli bir klasörü ağdaki diğer cihazlara paylaşılmış.", "Ağa giren biri dosyalarınızı okuyabilir, kopyalayabilir veya değiştirebilir; fidye yazılımları da paylaşımlar üzerinden yayılır."),
        (@"^\d+ klasör ağa paylaşılmış|^Adı gizlenmiş \(\$ ile biten\) paylaşım var", "Klasörleriniz Ağdaki Diğerlerine Açık", "Bazı klasörleriniz ağdaki diğer bilgisayarlarla paylaşılıyor.", "İzinler gevşekse (Herkes erişebilir) başkaları bu dosyaları görebilir veya silebilir; gerekmeyen paylaşımları kapatmak daha güvenlidir."),
        (@"^Eski sürüm:","Çok Eski Bir Yazılım Sürümü Çalışıyor", "Ağınızdaki bir cihazda yazılımın uzun süre önceki bir sürümü çalışıyor.", "Eski sürümlerde bilinen açıkların bulunma ihtimali yüksektir; yazılım güncellenmeden bu açıklar kapanmaz."),
        // ---- Web sitesi başlıkları ve güvenli bağlantı ----
        (@"HSTS.*(yok|eksik)", "Sitenin Güvenli Bağlantı Kilidi Eksik", "Site, tarayıcılara \"beni hep şifreli aç\" demiyor.", "Ziyaretçiler siteye girerken araya giren bir dolandırıcı bağlantıyı şifresiz hale getirebilir. Tıpkı kilitli olmayan bir posta kutusu gibidir."),
        (@"HSTS süresi kısa", "Güvenli Bağlantı Kilidi Çabuk Açılıyor", "Güvenli bağlantı koruması çok kısa süreli tanımlanmış.", "Koruma sık sık sona erer; kilit zaman zaman açık kalmış bir kapı gibidir."),
        (@"İçerik Güvenlik Politikası|\bCSP\b", "Sitede Yabancı Kod Çalışmasına Karşı Bekçi Yok", "Sayfa, hangi kaynaklardan kod yükleneceğini tarayıcıya söylemiyor.", "Birisi sayfaya zararlı bir kod sokarsa tarayıcı bunu durduramaz; ziyaretçilerin bilgileri çalınabilir."),
        (@"gizlice gömülmesine karşı", "Site Başka Bir Sayfanın İçine Gizlice Konabilir", "Site, başka sitelerin kendini görünmez bir çerçeve içine almasını engellemiyor.", "Dolandırıcı, sizin sitenizi görünmez bir perdenin arkasına koyup ziyaretçileri farkında olmadan yanlış yere tıklatabilir."),
        (@"X-Content-Type-Options", "Tarayıcı Dosya Türünü Yanlış Tahmin Edebilir", "Sunucu dosya türlerini tarayıcıya kesin olarak söylemiyor.", "Zararlı bir dosya zararsız bir resim gibi gösterilip çalıştırılabilir."),
        (@"Referrer-Policy", "Ziyaretçilerin Nereden Geldiği Başkalarına Sızabilir", "Site, ziyaretçi başka bir siteye geçerken adres bilgisini nasıl paylaşacağını belirlememiş.", "Adreste özel bilgiler varsa başka sitelere kendiliğinden gönderilebilir."),
        (@"Permissions-Policy", "Tarayıcı Özellikleri İçin Kısıtlama Yok", "Site, kamera/konum gibi tarayıcı özelliklerinin kullanımını sınırlamamış.", "Önemi küçüktür; ileride gömülü bir içerik bu özellikleri istemeden isteyebilir."),
        (@"Sunucu yazılım sürümünü|Server başlığı|başlığı sürüm|sürüm bilgisi veriyor|X-Powered-By|X-AspNet-Version", "Site Hangi Yazılımı Kullandığını Açıkça Söylüyor", "Sunucu, kullandığı programın adını ve sürümünü herkese bildiriyor.", "Saldırgan o sürümün bilinen açıklarını hemen deneyebilir; tıpkı kapıya \"kilit markası ve modeli\" yazmak gibi."),
        (@"https'e yönlendirilmiyor", "Eski Şifresiz Adres Hâlâ Çalışıyor", "http:// ile yazan kişi güvenli adrese yönlendirilmiyor.", "Bu kişilerin bilgileri şifresiz gider; araya giren biri okuyabilir."),
        (@"CORS.*kimlik bilgisine|çerezli isteklerle", "Her Site, Giriş Yapmış Ziyaretçi Adına Veri Okuyabilir", "Sunucu, herhangi bir sitenin giriş yapmış kullanıcı adına istek yapmasına izin veriyor.", "Kötü niyetli bir site, ziyaretçinin hesabındaki bilgileri sessizce çekebilir."),
        (@"CORS", "Site Verilerini Başka Sitelere Fazla Açık Bırakıyor", "Sunucu, hangi sitelerin kendisinden veri okuyabileceği konusunda fazla gevşek.", "Başka siteler, ziyaretçilerin tarayıcısı üzerinden bu sitenin verilerine ulaşmaya çalışabilir."),
        (@"Çerez.*Secure|'Secure' bayrağı", "Giriş Çerezi Şifresiz Yolda Da Gönderilebilir", "Oturum çerezi her durumda şifreli bağlantıyla gönderilmiyor.", "Araya giren biri çerezi görüp sizin yerinize siteye girebilir."),
        (@"Çerez.*HttpOnly|'HttpOnly' bayrağı", "Giriş Çerezi Sayfadaki Kodlarca Okunabilir", "Oturum çerezi sayfadaki kodlara kapatılmamış.", "Sayfaya zararlı kod girerse oturum çerezini çalıp hesabı ele geçirebilir."),
        (@"Çerez.*SameSite|SameSite koruması", "Giriş Çerezi Başka Sitelerden Gelen İsteklerle de Kullanılabilir", "Çerezde \"yalnızca bu siteden gelen isteklerde kullan\" kuralı yok.", "Başka bir site sizi farkında olmadan işlem yaptırmaya (tıklama tuzağı) kandırabilir."),

        // ---- Sertifika (sitenin kimlik kartı) ----
        (@"Sertifikanın süresi bitmiş|süresi dolmuş", "Sitenin Resmi Kimlik Kartı Geçersiz", "Sitenin güvenlik sertifikasının süresi dolmuş.", "Tarayıcılar ziyaretçilere \"Bu site güvenli değil\" uyarısı verir, müşteriler korkup siteden çıkar."),
        (@"Sertifikanın süresi yakında", "Sitenin Kimlik Kartının Süresi Dolmak Üzere", "Güvenlik sertifikası çok yakında geçersiz olacak.", "Yenilenmezse ziyaretçiler \"güvenli değil\" uyarısı görmeye başlar."),
        (@"Sertifikaya güvenilemiyor", "Sitenin Kimlik Kartı Resmî Makamdan Onaylı Değil", "Sertifikayı veren kuruluş tarayıcılar tarafından tanınmıyor.", "Ziyaretçiler uyarı görür; sahte bir site gibi algılanabilirsiniz."),
        (@"bu alan adı için düzenlenmemiş", "Sitenin Kimlik Kartında Yanlış İsim Yazıyor", "Sertifika başka bir site adı için alınmış.", "Tarayıcılar güvenlik uyarısı verir; ziyaretçiler siteye girmekten çekinir."),
        (@"kendi kendine imzalı", "Sitenin Kimlik Kartını Siteyi Yapan Kendisi Vermiş", "Sertifika bağımsız bir kuruluş tarafından değil, sitenin kendisi tarafından imzalanmış.", "Tarayıcılar uyarı verir; araya giren birinin sahte sertifika koyup fark edilmemesi kolaylaşır."),
        (@"anahtarı zayıf|Sertifika anahtarı", "Sitenin Şifreleme Anahtarı Zayıf", "Şifreleme için kullanılan anahtar günümüzde yeterince güçlü değil.", "Güçlü bir saldırgan şifreyi kırıp haberleşmeyi okuyabilir."),
        (@"SHA-1|eski ve zayıf imza", "Sitenin Kimlik Kartı Eski ve Zayıf Yöntemle Mühürlenmiş", "Sertifika artık güvenli sayılmayan eski bir yöntemle imzalanmış.", "Sahte sertifika üretilmesi kolaylaşır; tarayıcılar uyarı verebilir."),
        (@"Eski şifreleme sürümü", "Site Eski ve Güvensiz Şifreleme Kullanabiliyor", "Sunucu, artık güvenli sayılmayan eski şifreleme sürümlerini kabul ediyor.", "Araya giren biri bağlantıyı zayıf şifrelemeye düşürüp içeriği okumaya çalışabilir."),

        // ---- E-posta kayıtları (DNS) ----
        (@"SPF.*(yok|herkese izin)", "Başkaları Sizin Adınıza E-posta Gönderebilir", "Alan adınızdan hangi sunucuların e-posta atabileceği tanımlanmamış (veya herkese izin verilmiş).", "Dolandırıcılar \"sizden geliyormuş gibi\" sahte e-posta atıp müşterilerinizi kandırabilir."),
        (@"DMARC.*(yok|izleme)", "Sahte E-postalara Karşı Alıcılara Talimat Verilmemiş", "Alıcı sunucular sahte e-postayı ne yapacağını bilmiyor (veya yalnızca izliyor).", "Sahte e-postalar müşterilerin gelen kutusuna ulaşmaya devam eder."),
        (@"CAA", "Sertifika Verecek Kuruluşlar Sınırlanmamış", "Alan adı için hangi kuruluşun sertifika verebileceği belirtilmemiş.", "Önemi küçüktür; bir hata sonucu istenmeyen bir sertifika verilme ihtimali biraz artar."),
        (@"security\.txt.*yok|security\.txt dosyası yok", "Güvenlik Sorunu Bildirenler İçin İletişim Adresi Yok", "Sitede, güvenlik açığı bulanların size ulaşacağı resmi bir adres yayınlanmamış.", "Birisi açığı fark etse bile size kolayca haber veremeyebilir."),
        (@"security\.txt", "Güvenlik İletişim Dosyasında Düzeltilecek Yerler Var", "İletişim dosyasında eksik veya hatalı bilgiler var.", "Bildirim yapmak isteyenler size ulaşamayabilir."),

        // ---- Aktif web denetimi ----
        (@"\.env", "Şifrelerin Durduğu Gizli Dosya Herkese Açık", "Sitenin gizli ayar dosyası (.env) internetten indirilebiliyor.", "İçindeki veritabanı şifreleri ve anahtarlar herkesin eline geçer; saldırgan doğrudan içeri girebilir."),
        (@"\.git klasörüne", "Sitenin Tüm Yazılım Kodu Dışarıdan İndirilebiliyor", "Yazılımın geçmiş kayıt klasörü (.git) sitede açıkta duruyor.", "Kaynak kodun tamamı ve içindeki şifreler başkalarına geçebilir."),
        (@"Yedek/veritabanı dosyası", "Yedek Dosyası Dışarıdan İndirilebiliyor", "Yedek veya veritabanı dökümü sitede açıkta duruyor.", "Müşteri bilgileri ve şifre özetleri indirilebilir."),
        (@"Dizin listeleme", "Klasör İçindeki Tüm Dosyalar Herkese Gösteriliyor", "Bir klasör açıldığında içindeki dosya listesi görünüyor.", "Saldırgan, bağlantısı verilmemiş yedek ve ayar dosyalarını listeden seçip indirebilir."),
        (@"phpinfo", "Sunucu Ayar Sayfası Herkese Açık", "Sunucunun ayrıntılı ayar sayfası (phpinfo) internete açık.", "Saldırgan sunucunuzun iç yapısını öğrenip hedefli saldırı hazırlayabilir."),
        (@"server-status|durum sayfası", "Sunucunun Canlı Durum Sayfası Herkese Açık", "Sunucunun anlık istek ve bağlantı bilgisi dışarıdan görülebiliyor.", "Ziyaretçi adresleri ve sitenin iç hareketleri izlenebilir."),
        (@"DS_Store", "Klasör Bilgi Dosyası Sitede Unutulmuş", "Bir Mac dosyası (.DS_Store) sitede duruyor.", "Klasördeki dosya adları sızar; gizli dosyaların bulunması kolaylaşır."),
        (@"Yönetim paneli", "Yönetim Girişi Herkese Açık", "Sitenin yönetim sayfası internetten erişilebilir.", "Saldırganlar şifre deneyerek yönetici hesabını ele geçirmeye çalışabilir."),
        (@"TRACE", "Sunucu Gereksiz Bir Hata Ayıklama Yöntemine Cevap Veriyor", "Sunucu, gelen isteği aynen geri yansıtan eski bir yöntemi (TRACE) açık tutuyor.", "Bazı saldırı türlerinde gizli başlık bilgileri (çerez gibi) bu yolla okunabilir."),
        (@"PUT yöntemi|DELETE yöntemi", "Sunucu Dosya Yükleme/Silme Yöntemlerini Duyuruyor", "Sunucu, dışarıdan dosya yazma veya silme yöntemlerini desteklediğini söylüyor.", "Gerçekten açıksa yetkisiz biri sitenize dosya koyabilir veya silebilir; durum ayrıca doğrulanmalıdır."),
        (@"stack trace|iç hata ayrıntısı", "Hata Sayfası Sitenin İç Yapısını Gösteriyor", "Bir hata olduğunda sayfa, programın iç kayıtlarını ve dosya yollarını yazıyor.", "Saldırgan bu bilgilerle sitenin zayıf noktalarını daha kolay bulur."),
        (@"Hata sayfası yazılım sürümünü", "Hata Sayfası Yazılım Sürümünü Söylüyor", "Hata sayfasında kullanılan programın sürümü yazıyor.", "Saldırgan o sürümün bilinen açıklarını denemeye başlayabilir."),

        // ---- Bilgisayar: hizmetler, güncellemeler, yazılımlar ----
        (@"Tırnaksız hizmet yolu", "Bir Program Yanlış Yazılmış Adresle Başlıyor", "Windows'un başlattığı bir hizmetin dosya adresi eksik yazılmış.", "Biri o adrese kendi zararlı dosyasını koyarsa Windows, onu en yüksek yetkiyle çalıştırabilir."),
        (@"Hizmet dosyası yazılabilir klasörde", "Bir Programın Klasörüne Herkes Dosya Koyabiliyor", "Windows hizmetinin bulunduğu klasörde sıradan kullanıcılar da dosya yazabiliyor.", "Zararlı biri programı kendi dosyasıyla değiştirip bilgisayarda tam yetki kazanabilir."),
        (@"İmzasız hizmet", "Kimin Yaptığı Belli Olmayan Bir Program Yüksek Yetkiyle Çalışıyor", "Bir hizmet, üreticisi dijital olarak doğrulanamayan bir programı çalıştırıyor.", "Tanımadığınız bir program bilgisayarınızda geniş yetkiyle çalışıyor olabilir."),
        (@"güvenlik güncellemesi eksik|güncellemesi eksik", "Windows'un Önemli Güvenlik Yamaları Eksik", "Bilgisayarınızda kurulması gereken bazı güvenlik güncellemeleri yok.", "Bilinen açıklar kapatılmamış olur; saldırganlar bunları hazır araçlarla kullanabilir."),
        (@"Son güncelleme \d+ gün önce", "Bilgisayar Uzun Zamandır Güncellenmemiş", "Son güvenlik güncellemesinin üzerinden uzun süre geçmiş.", "Zamanla bulunan yeni açıklar kapatılmamış kalır."),
        (@"Yama kataloğu yüklenmedi|Katalogda bu Windows|Güncelleme kaydı okunamadı", "Güncelleme Durumu Kontrol Edilemedi", "Eksik güncelleme listesi için gereken veri yok; bu \"sorun yok\" demek değildir.", "Eksik yamalar olsa bile program şu an bunu bilemiyor."),
        (@"ürün KEV listesinde", "Programın Bazı Sürümlerinde Dolandırıcıların Kullandığı Bir Açık Var", "Kurulu programın bazı sürümlerinde saldırganların fiilen kullandığı bir açık bulunuyor; sizin sürümünüzün etkilenip etkilenmediği henüz doğrulanamadı.", "Sürümünüz etkileniyorsa dolandırıcılar bu açığı şu anda kullanıyor olabilir; programı üreticisinin duyurusuna göre kontrol edin."),
        (@"sömürüldüğü bilinen açık", "Programda Dolandırıcıların Gerçekten Kullandığı Bir Açık Var", "Kurulu programın bu sürümünde, saldırganların fiilen kullandığı bilinen bir açık bulunuyor.", "Bu açık hâlihazırda sahada kullanılıyor; program güncellenmezse bilgisayar ele geçirilebilir."),
        (@"bilinen \d+ açık içeriyor|düşük güven", "Kurulu Bir Programın Eski Sürümünde Bilinen Açıklar Var", "Programın sürümü, herkese açıklanmış güvenlik açıklarından etkileniyor.", "Program güncellenmezse saldırganlar bu açıkları kullanabilir."),
        (@"Zafiyet verisi yüklenmedi", "Programların Güvenliği Kontrol Edilemedi", "Açık listesi yüklenmediği için yazılımlar kontrol edilemedi; bu \"sorun yok\" demek değildir.", "Eski ve açıklı programlar fark edilmeyebilir."),
        (@"DLL arama riski", "Bir Programın Klasörüne Herkes Zararlı Dosya Koyabilir", "Programın bulunduğu klasör herkese açık; program başlarken oradaki dosyaları da yükler.", "Zararlı biri sahte bir dosya koyarsa program onu kendi yetkisiyle çalıştırır."),
        (@"derleme korumaları eksik", "Programda Bellek Saldırılarına Karşı Koruma Kapalı", "Programın yapımında kullanılan standart savunma bayraklarından bazıları kapalı.", "Programdaki bir yazılım hatası, saldırganlar için çok daha kolay istismar edilebilir."),

        // ---- Açık kapılar (eski tarayıcı başlıkları: "Açık port 445 (SMB)") ----
        (@"[Pp]ort 445\b|\bSMB\b", "Dosya Paylaşım Kapısı Ardına Kadar Açık", "Bilgisayar, ağdaki diğerlerine dosya paylaşım kapısını (445) açık tutuyor.", "Ağdaki diğer bilgisayarlar bu kapıdan izinsiz dosya kopyalayabilir veya fidye yazılımı bulaştırabilir."),
        (@"[Pp]ort 3389\b|\bRDP\b|uzak masaüstü", "Uzaktan Bilgisayarı Kullanma Kapısı Açık", "Başka bir yerden bu bilgisayarın ekranına bağlanmaya izin veren kapı (3389) açık.", "Şifresi tahmin edilirse biri bilgisayarı kendi evinden yönetebilir; fidye saldırılarının en sevdiği kapıdır."),
        (@"[Pp]ort 23\b|[Tt]elnet", "Çok Eski ve Şifresiz Uzak Erişim Kapısı Açık", "Telnet adlı eski uzaktan bağlantı hizmeti açık; her şey şifresiz gider.", "Parolanız ve yazdıklarınız ağdaki herkes tarafından okunabilir."),
        (@"[Pp]ort 21\b|\bFTP\b", "Eski Tip Dosya Aktarma Kapısı Açık", "Şifresiz dosya aktarma hizmeti (FTP) açık.", "Parola ve dosyalar açık metin gider; araya giren okuyabilir."),
        (@"[Pp]ort 5900\b|\bVNC\b", "Ekran Paylaşım Kapısı Açık", "Ekranı uzaktan gösteren/kontrol eden hizmet (VNC) açık.", "Zayıf şifreyle biri ekranınıza girip bilgisayarı kullanabilir."),
        (@"[Pp]ort (135|139)\b", "Windows'un İç Haberleşme Kapısı Açık", "Windows'un kendi iç işleri için kullandığı kapı ağa açık.", "Eski ve açıklı sürümlerde saldırganlar bu kapıdan sızmayı dener."),
        (@"[Pp]ort (1433|3306|5432|1521|27017)\b", "Veri Tabanı Kapısı Ağa Açık", "Verilerinizi tutan veri tabanı programı ağa açık.", "Şifresi zayıfsa tüm müşteri/şirket verileri çalınabilir veya silinebilir."),
        (@"[Pp]ort 22\b|\bSSH\b", "Uzaktan Yönetim Kapısı Açık (SSH)", "Şifreli uzaktan yönetim hizmeti açık.", "Parolası zayıfsa saldırganlar deneme yaparak içeri girmeye çalışır; güçlü parola/anahtar şarttır."),
        (@"[Pp]ort (80|8080|8000)\b", "Şifresiz Web Kapısı Açık", "Bilgisayar, şifrelenmemiş web hizmeti sunuyor.", "Bu kapıdan geçen bilgiler araya giren biri tarafından okunabilir."),
        (@"[Pp]ort (25|110|143)\b", "E-posta Hizmeti Kapısı Açık", "Bilgisayar eski tip e-posta hizmeti sunuyor.", "Ayarlar yanlışsa spam göndermek için kullanılabilir veya yazışmalar okunabilir."),
        (@"[Aa]çık port (\d+)", "Bilgisayarınızda Bir Kapı Açık", "Ağdan gelen bağlantıları kabul eden bir kapı açık.", "Kapının arkasındaki program güncel ve güçlü şifreli değilse saldırganlar buradan girmeyi dener."),
        (@"[Pp]ort \d+", "Bilgisayarınızda Bir Kapı Açık", "Ağdan gelen bağlantıları kabul eden bir kapı açık.", "Kapının arkasındaki program güncel ve güçlü şifreli değilse saldırganlar buradan girmeyi dener."),

        // ---- Bilgisayar ayarları (karne) ----
        (@"[Dd]efender|[Aa]ntivir|virüs koruması", "Virüs Koruması Kapalı veya Eksik", "Bilgisayarı zararlı yazılımlardan koruyan program çalışmıyor.", "Virüs ve fidye yazılımları engellenmeden bilgisayara girebilir."),
        (@"[Gg]üvenlik duvarı|[Ff]irewall", "Güvenlik Duvarı Kapalı", "Ağdan gelen bağlantıları süzen koruma kapalı.", "Ağdaki herhangi bir bilgisayar sizin bilgisayarınıza doğrudan bağlanmayı deneyebilir."),
        (@"BitLocker|disk şifreleme", "Disk Şifrelenmemiş", "Bilgisayarın diski şifrelenmemiş.", "Bilgisayar çalınırsa içindeki tüm dosyalar başkasınca okunabilir."),
        (@"\bUAC\b|hesap denetimi", "Yetki Uyarısı Kapalı", "Programların yönetici izni isteme uyarısı kapalı.", "Zararlı bir program sizi uyarmadan yüksek yetki alabilir."),
        (@"[Ww]i-?Fi|kablosuz", "Kablosuz Ağınızda Zayıflık Var", "Wi-Fi ayarlarınız yeterince güvenli değil.", "Yakındaki biri ağınıza bağlanıp trafiği dinleyebilir."),
        (@"[Kk]alıcılık|[Bb]aşlangıç|Run anahtarı|[Zz]amanlanmış görev", "Bilgisayar Açılırken Kendiliğinden Çalışan Şüpheli Bir Öğe Var", "Bilgisayar her açıldığında otomatik başlayan bir program görünüyor.", "Zararlı yazılımlar kalıcı olmak için bu yolu kullanır."),
        (@"hosts", "İnternet Adresi Yönlendirmeleri Değiştirilmiş Olabilir", "Bilgisayarın adres defterinde olağan dışı kayıtlar var.", "Bankanızı açtığınızı sanırken sahte bir siteye gönderilebilirsiniz."),
        (@"[Hh]esap|[Pp]arola", "Hesap Güvenliğinde Eksik Var", "Kullanıcı hesaplarında veya parolalarda zayıflık görünüyor.", "Zayıf hesaplar ele geçirilerek bilgisayara girilebilir.")
    };

    // Modüle göre genel yedek açıklamalar.
    private static readonly Dictionary<string, (string Ne, string Zarar)> ModulYedek = new()
    {
        ["Port"] = ("Ağdan gelen bağlantıları kabul eden bir kapı açık.", "Kapının arkasındaki program güncel değilse saldırganlar buradan girmeyi dener."),
        ["HTTP"] = ("Web sitesi ayarlarında bir eksik var.", "Ziyaretçilerin güvenliği veya sitenin itibarı zarar görebilir."),
        ["TLS"] = ("Sitenin güvenli bağlantı (şifreleme) ayarında bir eksik var.", "Ziyaretçiler uyarı görebilir veya bağlantı dinlenebilir."),
        ["DNS"] = ("Alan adı kayıtlarında bir eksik var.", "Sahte e-posta veya yönlendirme riskini artırır."),
        ["Hizmet"] = ("Windows hizmetlerinden birinin ayarında zayıflık var.", "Saldırgan bunu bilgisayarda yetki kazanmak için kullanabilir."),
        ["Yama"] = ("Windows güncellemeleriyle ilgili bir eksik var.", "Kapatılmamış açıklar saldırganlarca kullanılabilir."),
        ["Yazılım"] = ("Kurulu yazılımlardan biriyle ilgili bir eksik var.", "Güncel olmayan veya korumasız programlar saldırıya açıktır."),
        ["Aktif HTTP"] = ("Web sunucusunda yanlış bir ayar bulundu.", "Gizli bilgiler sızabilir veya yetkisiz erişim olabilir.")
    };

    // Eşleşen desenleri bir kez derler.
    private static readonly List<(Regex Re, string Baslik, string Ne, string Zarar)> Derli = Tablo.Select(t => (new Regex(t.Desen, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), t.Baslik, t.Ne, t.Zarar)).ToList();

    // Bir bulguyu çevirir. Özel bir açıklama yoksa modüle/önem derecesine göre genel bir cümle verir (Ozel=false).
    public static HalkDili Cevir(string modul, string baslik, string detay = "", Onem onem = Onem.Orta)
    {
        string aranan = (baslik ?? "") + " " + (modul == "Port" ? detay ?? "" : "");
        foreach (var (re, b, n, z) in Derli) { try { if (re.IsMatch(baslik ?? "") || (modul == "Port" && re.IsMatch(aranan))) return new HalkDili(b, n, z, true); } catch (RegexMatchTimeoutException hk3) { HataKayit.Sessiz(hk3, "HalkDili.Cevir"); } }
        if (ModulYedek.TryGetValue(modul ?? "", out var y)) return new HalkDili(Temiz(baslik), y.Ne, y.Zarar, false);
        string zarar = onem switch { Onem.Yuksek => "Ciddi bir güvenlik riski: bir saldırgan bunu kullanarak zarar verebilir.", Onem.Orta => "Orta düzeyde bir risk: tek başına büyük zarar vermez ama saldırganların işini kolaylaştırır.", _ => "Küçük bir risk: şimdilik acil değil, fırsat bulunca düzeltin." };
        return new HalkDili(Temiz(baslik), "Güvenlik denetiminde bir eksik veya risk bulundu.", zarar, false);
    }
    public static HalkDili Cevir(Bulgu b) => Cevir(b.Modul, b.Baslik, b.Detay, b.Onem);
    public static HalkDili Cevir(Finding f) => Cevir(f.Module, f.Title, string.Join(" ", f.Evidence), f.EskiOnem);

    // Başlıktaki yer tutucuları ve çok uzun metni sadeleştirir.
    private static string Temiz(string? s) { s = Regex.Replace(s ?? "", @"\s+", " ").Trim(); return s.Length > 90 ? s[..90] + "…" : s.Length == 0 ? "Bir eksik bulundu" : s; }
}

// Karnedeki tek madde.
internal sealed record KarneMaddesi(HalkDili Halk, string Yapilacak, Onem Onem, int Kazanc);
// Trafik ışığı karnesi. Renk: 0 = yeşil, 1 = sarı, 2 = kırmızı.
internal sealed record Karne(int Puan, string Baslik, string Ozet, int Renk, List<KarneMaddesi> EnOnemli3, int SorunSayisi, int OnemliSayisi);

// Karne hesabı (ekrandan bağımsız, test edilebilir).
internal static class KarneHesabi
{
    // Puan: mevcut hesapla aynı ağırlıklar (yüksek 15, orta 7, düşük 3), 0-100.
    public static int Puan(IEnumerable<Bulgu> l) => Math.Clamp(100 - l.Where(b => !b.Atlandi).Sum(b => DurumAnalizi.Kazanc(b.Onem)), 0, 100);

    // Karneyi üretir. "Yapılacak": bulgunun önerisinin ilk cümlesi (teknik komut satırları çıkarılır).
    public static Karne Hesapla(IEnumerable<Bulgu> bulgular)
    {
        var liste = bulgular.Where(b => !b.Atlandi).ToList(); int puan = Puan(liste);
        var sorunlar = liste.Where(b => b.Onem != Onem.Bilgi).ToList(); int onemli = sorunlar.Count(b => b.Onem == Onem.Yuksek), kucuk = sorunlar.Count - onemli;
        string durum = puan >= 80 ? "Genel Durum İyi" : puan >= 50 ? "Dikkat Gerekiyor" : "Acil Müdahale Gerekiyor";
        string eksik = sorunlar.Count == 0 ? "Hiç Eksik Bulunamadı" : onemli > 0 ? $"{onemli} Önemli" + (kucuk > 0 ? $", {kucuk} Küçük" : "") + " Eksik Var" : $"{kucuk} Küçük Eksik Var";
        int renk = puan < 50 ? 2 : (puan < 80 || onemli > 0) ? 1 : 0;
        // En önemli 3 adım: önem sırasına göre, aynı halk dili başlığı tekrar etmez.
        var uc = sorunlar.OrderByDescending(b => DurumAnalizi.Kazanc(b.Onem)).ThenBy(b => b.Modul).Select(b => (b, h: HalkDiliCevirici.Cevir(b))).GroupBy(x => x.h.Baslik).Select(g => g.First()).Take(3)
            .Select(x => new KarneMaddesi(x.h, Yapilacak(x.b.Oneri), x.b.Onem, DurumAnalizi.Kazanc(x.b.Onem))).ToList();
        string ozet = sorunlar.Count == 0 ? "Bu denetimde dikkat çeken bir sorun görünmüyor. Düzenli aralıklarla tekrar kontrol edin." : $"{sorunlar.Count} madde bulundu. Önce aşağıdaki en önemli adımlara bakın; her biri düzelince puanınız yükselir.";
        return new Karne(puan, $"{puan} / 100 — {durum}, {eksik}", ozet, renk, uc, sorunlar.Count, onemli);
    }

    // Öneri metninden ilk cümleyi alır; komut/örnek satırlarını atar.
    public static string Yapilacak(string? oneri)
    {
        var satirlar = (oneri ?? "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith("Örnek:", StringComparison.OrdinalIgnoreCase)).ToList();
        string ilk = satirlar.FirstOrDefault() ?? "Ayrıntı için Uzman Görünümü'ne bakın."; int nokta = ilk.IndexOf(". ", StringComparison.Ordinal);
        if (nokta > 20) ilk = ilk[..(nokta + 1)]; return ilk.Length > 200 ? ilk[..200] + "…" : ilk;
    }
}
