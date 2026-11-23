using System.Text;

namespace LabDenetim;

// "Veri Akışı ve Gizlilik" belgesi: programın makineden dışarı yaptığı her bağlantıyı ve diske yazdığı verileri tek yerde sayar.
// Kaynak koddaki gerçek sabitlerden (adresler) üretilir; testler, kodda belgelenmemiş yeni bir dış adres eklenirse başarısız olur.
internal sealed record VeriAkisiKaydi(string Sunucu, string Amac, string Giden, string Tetik, string Kapatma);

internal static class VeriAkisi
{
    // Sunucu adı kaynak koddaki sabitlerle aynı olmalıdır (VeriAkisiTestleri kaynak dosyaları tarar).
    public static IReadOnlyList<VeriAkisiKaydi> Dis { get; } = new VeriAkisiKaydi[]
    {
        new(new Uri(LabDenetim.V2.KevIndirici.Adres).Host, "Bilinen istismar edilen açıklar listesi (CISA KEV) indirme", "Yalnızca GET isteği; makine veya tarama verisi gönderilmez", "Kullanıcı 'güncelle' dediğinde", "Güncelleme düğmesine basmayın; liste yerelde kalır"),
        new("api.pwnedpasswords.com", "Parola sızıntı kontrolü", "Parolanın SHA-1 özetinin yalnızca ilk 5 harfi (k-anonimlik); parolanın kendisi çıkmaz", "Kullanıcı Araç Kutusu'nda parola sızıntısı kontrolü yaptığında", "Aracı kullanmayın"),
        new("api.anthropic.com", "Yapay zekâ yorumu (Claude)", "Hedef IP, tarama özeti, bulgular ve kullanıcının kendi API anahtarı; bilgisayar ve kullanıcı adı gönderilmez", "Yalnızca kullanıcı onay penceresine 'Evet' derse", "Onay penceresine 'Hayır' deyin; varsayılan butondur"),
        new("api.ipify.org", "IP sızıntı testi: genel IP'yi öğrenme", "Boş GET isteği", "Kullanıcı IP Güvenlik Sızıntı Testi'ni başlattığında", "Testi çalıştırmayın"),
        new("api6.ipify.org", "IP sızıntı testi: IPv6 kontrolü", "Boş GET isteği", "Aynı test", "Testi çalıştırmayın"),
        new("www.cloudflare.com", "IP sızıntı testi: DNS/bağlantı kontrolü", "Boş GET isteği", "Aynı test", "Testi çalıştırmayın"),
        new("ipwho.is", "IP sızıntı testi: IP'nin ülke/ağ bilgisi", "Genel IP adresi (adrese göre sorgu)", "Aynı test", "Testi çalıştırmayın"),
        new("discord.com", "Uyarı bildirimi (Discord webhook)", "Uyarı metni (başlık, önem, makine adı)", "Yalnızca kullanıcı webhook adresini kendisi girerse", "Webhook ayarlamayın"),
        new("api.telegram.org", "Uyarı bildirimi (Telegram)", "Uyarı metni", "Yalnızca kullanıcı bot belirtecini kendisi girerse", "Telegram kanalı ayarlamayın"),
        new("freetsa.org", "Kanıt paketi için RFC 3161 zaman damgası", "Yalnızca paketin SHA-256 özeti (64 karakter); içerik, dosya adı, kullanıcı ve bilgisayar adı gönderilmez", "Yalnızca kullanıcı zaman damgası onay penceresine 'Evet' derse", "Onay penceresine 'Hayır' deyin; varsayılan butondur"),
        new("timestamp.digicert.com", "Zaman damgası (yedek sunucu)", "Aynı: yalnızca SHA-256 özeti", "freetsa.org yanıt vermezse ve kullanıcı onay verdiyse", "Onay penceresine 'Hayır' deyin"),
        new("logic.azure.com", "Uyarı bildirimi (Microsoft Teams / Power Automate)", "Uyarı metni", "Yalnızca kullanıcı adresi kendisi girerse", "Teams kanalı ayarlamayın"),
    };

    // Dış ağa çıkmayan ama koddaki "http" geçen yerler (testin yoksayacağı adresler): yerel, örnek ve ad alanı metinleri.
    public static readonly string[] AgDisiAdresler = { "127.0.0.1", "192.168.", "schemas.microsoft.com", "adaptivecards.io", "a.com", "bulutsoft-sonda.invalid", "...", "...." };

    public static IReadOnlyList<(string Ne, string Nerede, string Not)> Yerel { get; } = new[]
    {
        ("Tarama sonuçları, bulgular, günlük, kalıcılık yedeği, ayarlar, kullanıcı hesapları", "%AppData%\\BulutSoft (her Windows kullanıcısına ayrı)", "Parolalar PBKDF2 ile özetlenmiş saklanır; düz metin parola yoktur"),
        ("Tarama özeti (--tara)", "%AppData%\\BulutSoft\\ozetler veya --cikti ile verilen klasör", "Makine adı, bulgular ve puan içerir; kurumun paylaşım klasörüne yazılırsa orada erişim denetimi BT'nindir"),
        ("Filo raporu (--filo)", "Verilen klasörde HTML ve CSV", "Özet dosyalarına dokunmaz, yalnızca okur"),
        ("Koruma hizmeti (BulutSoftGuard) kayıtları: zincirli olay günlüğü, ayarlar, nöbetçi tabanı", "%ProgramData%\\BulutSoft\\Guard (yalnızca SYSTEM/Yönetici yazar, kullanıcılar okur)", "Olaylar Windows Olay Günlüğü'ne (Uygulama, kaynak BulutSoft) de yansıtılır"),
        ("Mühür özetleri ve SSH sunucu parmak izleri", "HKLM\\SOFTWARE\\BulutSoft (kullanıcılar okur, yalnızca hizmet yazar)", "Dosya içeriği değil yalnızca SHA-256 özeti ve SHA256 parmak izi tutulur"),
        ("Güvenlik duvarı kuralları", "Windows Güvenlik Duvarı ('BulutSoft-' ile başlayan adlar)", "Yalnızca kullanıcı onayıyla; geri alınabilir; pilot modda hiç yazılmaz"),
    };

    public static string Metin(bool saltOkunur)
    {
        var s = new StringBuilder();
        s.AppendLine("# BulutSoft Security Suite — Veri Akışı ve Gizlilik");
        s.AppendLine();
        s.AppendLine("Bu belge programın kaynak kodundaki gerçek bağlantı adreslerinden üretilir. Kodda belgelenmemiş bir dış adres olursa yazılım testi başarısız olur.");
        s.AppendLine();
        s.AppendLine("## Özet");
        s.AppendLine("- Program bir bulut hizmetine bağlı değildir; BulutSoft'a veri göndermez, lisans veya kullanım izi sunucusu yoktur.");
        s.AppendLine("- Tarama (`--tara`) ve filo raporu (`--filo`) dış ağa bağlanmaz. Tarama yalnızca bu bilgisayarın adını yerel çözücüde sorgular.");
        s.AppendLine("- Aşağıdaki dış bağlantılar ilgili özellik kullanıcı tarafından başlatılınca ya da ayarlanınca yapılır. Zamanlayıcıyla otomatik çalışan bir özellik ayarlandıysa o da bu listedeki adreslerle sınırlıdır.");
        s.AppendLine($"- Şu an bu makinede pilot (salt okunur) mod: **{(saltOkunur ? "AÇIK — hiçbir değişiklik yapılmaz" : "kapalı")}**.");
        s.AppendLine();
        s.AppendLine("## Dışarı giden bağlantılar");
        s.AppendLine();
        s.AppendLine("| Sunucu | Amaç | Giden veri | Ne zaman | Nasıl kapatılır |");
        s.AppendLine("|---|---|---|---|---|");
        foreach (var k in Dis) s.AppendLine($"| {k.Sunucu} | {k.Amac} | {k.Giden} | {k.Tetik} | {k.Kapatma} |");
        s.AppendLine();
        s.AppendLine("Ek olarak: kullanıcı kendi webhook adresini girerse bildirimler yalnızca o adrese gider (https zorunlu, yönlendirmeler izlenmez).");
        s.AppendLine();
        s.AppendLine("## Makinede saklanan veriler");
        s.AppendLine();
        s.AppendLine("| Veri | Yer | Not |");
        s.AppendLine("|---|---|---|");
        foreach (var (ne, nerede, not) in Yerel) s.AppendLine($"| {ne} | {nerede} | {not} |");
        s.AppendLine();
        s.AppendLine("## Pilot (salt okunur) kurulum");
        s.AppendLine($"BT yöneticisi `{PilotMod.OrtamDegiskeni}=1` ortam değişkenini ya da `%ProgramData%\\BulutSoft\\{PilotMod.BayrakAdi}` dosyasını koyarsa program bu makinede güvenlik duvarı kuralı eklemez, süreç sonlandırmaz, başlangıç öğesi kapatmaz. Tarama, rapor ve görüntüleme çalışır. Normal kullanıcı ProgramData altına yazamadığı için modu kapatamaz.");
        s.AppendLine();
        s.AppendLine("## Bu belgenin sınırı");
        s.AppendLine("Bu belge teknik bir dökümdür, hukuki metin değildir. KVKK aydınlatma metni ve veri işleme sözleşmesi bir hukukçu tarafından hazırlanmalıdır.");
        return s.ToString();
    }
}
