// Bu dosya bulunan her web sitesi açığı için Apache ve Nginx'e ÖZEL, kopyala-yapıştır düzeltme önerileri üretir.
// Kural kimliği (ör. HDR-HSTS-MISSING) -> Apache ayarı + Nginx ayarı + kısa not. Ayrıca bulguların tümünden tek bir "düzeltme yapılandırma dosyası" üretir.
// Bu dosya hiçbir sunucuya dokunmaz: yalnızca metin üretir. Ayarları sitenin sahibi kendisi (yedek alarak) uygular.
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Hangi web sunucusu için öneri üretilecek.
internal enum SunucuTuru { Apache, Nginx }

// Öneri üretici.
internal static class SunucuOnerileri
{
    // Bir kuralın önerisi: Apache metni, Nginx metni, ortak not.
    private sealed record Oneri(string Apache, string Nginx, string Not);

    // Ortak not: başlık ayarları için.
    private const string BaslikNotu = "Apache için mod_headers açık olmalı (sudo a2enmod headers). Değişiklikten sonra: Apache için 'sudo apachectl configtest && sudo systemctl reload apache2'; Nginx için 'sudo nginx -t && sudo systemctl reload nginx'.";

    // Tablo: kural kimliği -> öneri. {HOST} gerçek alan adıyla değişir.
    private static readonly Dictionary<string, Oneri> Tablo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HDR-HSTS-MISSING"] = new("Header always set Strict-Transport-Security \"max-age=31536000; includeSubDomains\"", "add_header Strict-Transport-Security \"max-age=31536000; includeSubDomains\" always;", BaslikNotu + " Önce sitenizin TÜM alt alanlarının https çalıştığından emin olun."),
        ["HDR-HSTS-WEAK"] = new("Header always set Strict-Transport-Security \"max-age=31536000; includeSubDomains\"", "add_header Strict-Transport-Security \"max-age=31536000; includeSubDomains\" always;", BaslikNotu),
        ["HDR-CSP-MISSING"] = new("Header always set Content-Security-Policy \"default-src 'self'; img-src 'self' data:; frame-ancestors 'self'\"", "add_header Content-Security-Policy \"default-src 'self'; img-src 'self' data:; frame-ancestors 'self'\" always;", BaslikNotu + " İlk gün 'Content-Security-Policy-Report-Only' adıyla deneyin; sitenizin dış betik/yazı tipi kullandığı yerleri listeye ekleyin, sonra asıl başlığa geçin."),
        ["HDR-FRAME-MISSING"] = new("Header always set X-Frame-Options \"SAMEORIGIN\"", "add_header X-Frame-Options \"SAMEORIGIN\" always;", BaslikNotu),
        ["HDR-XCTO-MISSING"] = new("Header always set X-Content-Type-Options \"nosniff\"", "add_header X-Content-Type-Options \"nosniff\" always;", BaslikNotu),
        ["HDR-REFERRER-MISSING"] = new("Header always set Referrer-Policy \"strict-origin-when-cross-origin\"", "add_header Referrer-Policy \"strict-origin-when-cross-origin\" always;", BaslikNotu),
        ["HDR-PERMISSIONS-MISSING"] = new("Header always set Permissions-Policy \"geolocation=(), microphone=(), camera=()\"", "add_header Permissions-Policy \"geolocation=(), microphone=(), camera=()\" always;", BaslikNotu),
        ["LEAK-SERVER-VERSION"] = new("ServerTokens Prod\nServerSignature Off", "server_tokens off;", "Apache'de bu iki satır ana yapılandırmaya (apache2.conf / httpd.conf) yazılır, .htaccess'e yazılamaz. Nginx'te http { } bloğuna konur."),
        ["ACT-SERVER-VERSION"] = new("ServerTokens Prod\nServerSignature Off", "server_tokens off;", "Sürüm bilgisini gizlemek açığı kapatmaz ama saldırganın işini zorlaştırır; asıl çözüm yazılımı güncel tutmaktır."),
        ["ACT-ERR-VERSION"] = new("ServerTokens Prod\nServerSignature Off", "server_tokens off;", "Hata sayfalarındaki sürüm yazısı bu ayarlarla kalkar. Özel hata sayfaları (ErrorDocument / error_page) da ekleyin."),
        ["LEAK-X-POWERED-BY"] = new("Header always unset X-Powered-By\n# php.ini içinde: expose_php = Off", "fastcgi_hide_header X-Powered-By;\nproxy_hide_header X-Powered-By;\n# php.ini içinde: expose_php = Off", "PHP kullanıyorsanız php.ini'deki expose_php = Off ayarı da gerekir."),
        ["LEAK-ASPNET-VERSION"] = new("Header always unset X-AspNet-Version", "proxy_hide_header X-AspNet-Version;", "ASP.NET uygulamasının web.config dosyasında enableVersionHeader=\"false\" da ayarlanır."),
        ["REDIR-HTTP-TO-HTTPS"] = new("<VirtualHost *:80>\n    ServerName {HOST}\n    Redirect permanent / https://{HOST}/\n</VirtualHost>", "server {\n    listen 80;\n    server_name {HOST};\n    return 301 https://$host$request_uri;\n}", "http adresine gelenlerin kalıcı olarak https'e yönlendirilmesi içindir."),
        ["CORS-WILDCARD"] = new("# Header set Access-Control-Allow-Origin \"*\" satırını silin; gerekiyorsa:\nHeader always set Access-Control-Allow-Origin \"https://{HOST}\"\nHeader always append Vary Origin", "# add_header Access-Control-Allow-Origin * satırını silin; gerekiyorsa:\nadd_header Access-Control-Allow-Origin \"https://{HOST}\" always;\nadd_header Vary Origin always;", "Yalnızca gerçekten ihtiyaç duyan, güvendiğiniz adresleri tek tek yazın."),
        ["CORS-WILDCARD-CREDENTIALS"] = new("# Access-Control-Allow-Origin * ile Allow-Credentials true birlikte OLMAMALI.\nHeader always set Access-Control-Allow-Origin \"https://{HOST}\"", "add_header Access-Control-Allow-Origin \"https://{HOST}\" always;\n# add_header Access-Control-Allow-Credentials true; satırını yalnızca belirli adreslerle kullanın", "Kimlik bilgili isteklere her yerden izin vermek oturum çalınmasına yol açar."),
        ["ACT-CORS-REFLECT"] = new("# Uygulamanız gelen Origin'i aynen geri yazıyor; izinli adresleri sabit bir listeyle sınırlayın:\nSetEnvIf Origin \"^https://({HOST}|app\\.{HOST})$\" CORS=$0\nHeader always set Access-Control-Allow-Origin %{CORS}e env=CORS\nHeader always append Vary Origin", "# Uygulamanız gelen Origin'i aynen geri yazıyor; izinli adresleri sabit bir listeyle sınırlayın:\nmap $http_origin $cors_ok { default \"\"; \"https://{HOST}\" $http_origin; }\nadd_header Access-Control-Allow-Origin $cors_ok always;\nadd_header Vary Origin always;", "Asıl düzeltme çoğu zaman uygulama kodundadır (CORS kütüphanesinin ayarı)."),
        ["ACT-CORS-REFLECT-CRED"] = new("# Yansıtılan Origin + Allow-Credentials birlikte tehlikelidir: Allow-Credentials satırını kaldırın veya izinli adresleri sabit listeyle sınırlayın.\nSetEnvIf Origin \"^https://{HOST}$\" CORS=$0\nHeader always set Access-Control-Allow-Origin %{CORS}e env=CORS", "# Yansıtılan Origin + Allow-Credentials birlikte tehlikelidir: Allow-Credentials satırını kaldırın veya izinli adresleri sabit listeyle sınırlayın.\nmap $http_origin $cors_ok { default \"\"; \"https://{HOST}\" $http_origin; }\nadd_header Access-Control-Allow-Origin $cors_ok always;", "Bu bulgu yüksek önceliklidir: başka bir site, giriş yapmış kullanıcınız adına veri okuyabilir."),
        ["COOKIE-SECURE"] = new("Header edit Set-Cookie ^(.*)$ \"$1; Secure\"\n# PHP: session.cookie_secure = 1", "proxy_cookie_flags ~ secure;\n# PHP: session.cookie_secure = 1", "Çerez yalnızca https üzerinden gönderilsin. Nginx proxy_cookie_flags için 1.19.3 veya yenisi gerekir."),
        ["COOKIE-HTTPONLY"] = new("Header edit Set-Cookie ^(.*)$ \"$1; HttpOnly\"\n# PHP: session.cookie_httponly = 1", "proxy_cookie_flags ~ httponly;\n# PHP: session.cookie_httponly = 1", "JavaScript çereze erişemesin (XSS durumunda oturum çalınmasını zorlaştırır)."),
        ["COOKIE-SAMESITE"] = new("Header edit Set-Cookie ^(.*)$ \"$1; SameSite=Lax\"\n# PHP: session.cookie_samesite = Lax", "proxy_cookie_flags ~ samesite=lax;\n# PHP: session.cookie_samesite = Lax", "Başka sitelerden gelen isteklerde çerez gönderilmesini sınırlar. Aynı çereze birden çok bayrak ekleyecekseniz satırları tek tek değil birleştirerek yazın."),
        ["TLS-OLD-PROTOCOL"] = new("SSLProtocol -all +TLSv1.2 +TLSv1.3", "ssl_protocols TLSv1.2 TLSv1.3;", "Eski TLS 1.0/1.1 sürümleri kapatılır. Çok eski cihazlar (Windows XP, eski Android) bağlanamaz."),
        ["TLS-EXPIRED"] = new("sudo certbot renew --apache\nsudo systemctl reload apache2", "sudo certbot renew --nginx\nsudo systemctl reload nginx", "Let's Encrypt kullanıyorsanız yenileme komutu budur. Otomatik yenileme: 'sudo systemctl list-timers | grep certbot'."),
        ["TLS-EXPIRING"] = new("sudo certbot renew --apache\nsudo systemctl reload apache2", "sudo certbot renew --nginx\nsudo systemctl reload nginx", "Sertifika bitmeden yenileyin; otomatik yenilemeyi 'sudo certbot renew --dry-run' ile deneyin."),
        ["TLS-CHAIN"] = new("SSLCertificateFile /etc/letsencrypt/live/{HOST}/fullchain.pem\nSSLCertificateKeyFile /etc/letsencrypt/live/{HOST}/privkey.pem", "ssl_certificate /etc/letsencrypt/live/{HOST}/fullchain.pem;\nssl_certificate_key /etc/letsencrypt/live/{HOST}/privkey.pem;", "Sertifika zinciri eksik: 'cert.pem' yerine mutlaka 'fullchain.pem' dosyasını kullanın."),
        ["TLS-WEAK-KEY"] = new("# Yeni, en az 2048 bit RSA (veya ECDSA) anahtarlı sertifika alın:\nsudo certbot certonly --apache -d {HOST} --key-type ecdsa", "# Yeni, en az 2048 bit RSA (veya ECDSA) anahtarlı sertifika alın:\nsudo certbot certonly --nginx -d {HOST} --key-type ecdsa", "Sonra SSLCertificateFile/ssl_certificate yollarını yeni dosyalara çevirin ve sunucuyu yeniden yükleyin."),
        ["TLS-SHA1"] = new("# SHA-1 imzalı sertifika güvensizdir; yenisini alın:\nsudo certbot --apache -d {HOST}", "# SHA-1 imzalı sertifika güvensizdir; yenisini alın:\nsudo certbot --nginx -d {HOST}", "Sertifikayı veren kuruluştan SHA-256 (veya üstü) imzalı yeni sertifika isteyin."),
        ["TLS-NAME-MISMATCH"] = new("sudo certbot --apache -d {HOST}", "sudo certbot --nginx -d {HOST}", "Sertifika bu alan adı için düzenlenmemiş; doğru adlar için yeni sertifika alın."),
        ["TLS-SELF-SIGNED"] = new("sudo certbot --apache -d {HOST}", "sudo certbot --nginx -d {HOST}", "Kendi imzalı sertifika ziyaretçilerde uyarı çıkarır; ücretsiz Let's Encrypt sertifikası alın."),
        ["ACT-TRACE-ENABLED"] = new("TraceEnable off", "# Nginx TRACE'i varsayılan olarak reddeder; arkada başka sunucu varsa:\nif ($request_method = TRACE) { return 405; }", "Apache'de ana yapılandırmaya yazılır."),
        ["ACT-METHOD-PUT"] = new("<LimitExcept GET POST HEAD>\n    Require all denied\n</LimitExcept>", "limit_except GET POST HEAD { deny all; }", "Apache'de ilgili <Directory> veya <Location> içine; Nginx'te ilgili location { } içine yazılır. REST API'niz PUT kullanıyorsa yalnızca o adresi muaf tutun."),
        ["ACT-METHOD-DELETE"] = new("<LimitExcept GET POST HEAD>\n    Require all denied\n</LimitExcept>", "limit_except GET POST HEAD { deny all; }", "Apache'de ilgili <Directory> veya <Location> içine; Nginx'te ilgili location { } içine yazılır."),
        ["ACT-GIT-EXPOSED"] = new("RedirectMatch 404 /\\.git", "location ~ /\\.git { deny all; return 404; }", "ASIL ÇÖZÜM: .git klasörünü web dizininden kaldırın veya sitenizi 'git pull' yerine derlenmiş dosyalarla yayınlayın. Kaynak kodu ve şifreler sızmış olabilir: bulunan tüm şifre ve anahtarları DEĞİŞTİRİN."),
        ["ACT-ENV-EXPOSED"] = new("<FilesMatch \"^\\.env\">\n    Require all denied\n</FilesMatch>", "location ~ /\\.env { deny all; return 404; }", "ASIL ÇÖZÜM: .env dosyasını web dizininin DIŞINA taşıyın. İçindeki tüm şifre ve anahtarlar sızmış kabul edilip DEĞİŞTİRİLMELİDİR."),
        ["ACT-PHPINFO-EXPOSED"] = new("<Files \"phpinfo.php\">\n    Require all denied\n</Files>", "location = /phpinfo.php { deny all; return 404; }", "ASIL ÇÖZÜM: phpinfo.php dosyasını silin; sunucu ayrıntılarını ifşa eder."),
        ["ACT-SERVERSTATUS-EXPOSED"] = new("<Location \"/server-status\">\n    Require ip 127.0.0.1\n</Location>", "location /server-status { allow 127.0.0.1; deny all; }", "Durum sayfasını yalnızca sunucunun kendisi görebilsin."),
        ["ACT-DSSTORE-EXPOSED"] = new("<FilesMatch \"^\\.DS_Store$\">\n    Require all denied\n</FilesMatch>", "location ~ /\\.DS_Store { deny all; return 404; }", "ASIL ÇÖZÜM: .DS_Store dosyalarını sunucudan silin ve yayın sürecine dahil etmeyin."),
        ["ACT-ADMIN-OPEN"] = new("<Location \"/admin\">\n    # Yalnızca kendi IP adresinizi yazın:\n    Require ip 203.0.113.10\n</Location>", "location /admin {\n    # Yalnızca kendi IP adresinizi yazın:\n    allow 203.0.113.10;\n    deny all;\n}", "Yönetim paneli herkese açık görünüyor. IP kısıtına ek olarak güçlü şifre ve iki aşamalı doğrulama kullanın."),
        ["ACT-BACKUP-EXPOSED"] = new("<FilesMatch \"\\.(sql|bak|zip|tar\\.gz|old)$\">\n    Require all denied\n</FilesMatch>", "location ~* \\.(sql|bak|zip|tar\\.gz|old)$ { deny all; return 404; }", "ASIL ÇÖZÜM: yedek dosyalarını web dizininden silin. İçlerindeki veritabanı şifrelerini değiştirin."),
        ["ACT-DIR-LISTING"] = new("Options -Indexes", "autoindex off;", "Klasör içerikleri ziyaretçilere liste olarak gösterilmesin."),
        ["ACT-ERR-STACKTRACE"] = new("ErrorDocument 500 /hata.html\n# PHP: display_errors = Off", "fastcgi_intercept_errors on;\nerror_page 500 502 503 /hata.html;\n# PHP: display_errors = Off", "Uygulamanızın hata ayrıntılarını ziyaretçiye göstermeyin; üretim ortamında hata ayıklama (debug) kipini KAPATIN."),
        ["SECTXT-MISSING"] = new("# Dosyayı oluşturun: /var/www/html/.well-known/security.txt\nContact: mailto:guvenlik@{HOST}\nExpires: 2027-12-31T23:59:59Z\nPreferred-Languages: tr, en", "# Dosyayı oluşturun: /usr/share/nginx/html/.well-known/security.txt\nContact: mailto:guvenlik@{HOST}\nExpires: 2027-12-31T23:59:59Z\nPreferred-Languages: tr, en", "Güvenlik araştırmacıları size açık bildirebilsin. 'Expires' tarihini yılda bir yenileyin; yol sunucunuzun belge köküne göre değişir."),
        ["SECTXT-PROBLEMS"] = new("# security.txt içinde Contact (https veya mailto) ve geleceğe ait Expires alanı olmalı.", "# security.txt içinde Contact (https veya mailto) ve geleceğe ait Expires alanı olmalı.", "Dosya https üzerinden /.well-known/security.txt adresinde okunabilir olmalıdır."),
        ["DNS-SPF-MISSING"] = new("# DNS panelinizde TXT kaydı: {HOST}  TXT  \"v=spf1 mx -all\"", "# DNS panelinizde TXT kaydı: {HOST}  TXT  \"v=spf1 mx -all\"", "Bu bir web sunucusu değil, DNS ayarıdır (alan adı sağlayıcınızın panelinde). E-posta gönderen başka hizmetiniz varsa onları da ekleyin."),
        ["DNS-SPF-OPEN"] = new("# DNS: SPF kaydındaki '+all' veya '?all' ifadesini '-all' (veya '~all') yapın.", "# DNS: SPF kaydındaki '+all' veya '?all' ifadesini '-all' (veya '~all') yapın.", "Alan adı sağlayıcınızın DNS panelinde yapılır."),
        ["DNS-DMARC-MISSING"] = new("# DNS panelinizde TXT kaydı: _dmarc.{HOST}  TXT  \"v=DMARC1; p=none; rua=mailto:dmarc@{HOST}\"", "# DNS panelinizde TXT kaydı: _dmarc.{HOST}  TXT  \"v=DMARC1; p=none; rua=mailto:dmarc@{HOST}\"", "Önce p=none ile raporları izleyin, sonra p=quarantine ve p=reject'e geçin."),
        ["DNS-DMARC-NONE"] = new("# DNS: _dmarc kaydında p=none yerine önce p=quarantine, sorun çıkmazsa p=reject yazın.", "# DNS: _dmarc kaydında p=none yerine önce p=quarantine, sorun çıkmazsa p=reject yazın.", "Raporları (rua) en az birkaç hafta izledikten sonra sıkılaştırın."),
        ["DNS-CAA-MISSING"] = new("# DNS panelinizde CAA kaydı: {HOST}  CAA  0 issue \"letsencrypt.org\"", "# DNS panelinizde CAA kaydı: {HOST}  CAA  0 issue \"letsencrypt.org\"", "Yalnızca belirlediğiniz kuruluşun sizin adınıza sertifika vermesine izin verir (başka sertifika kurumu kullanıyorsanız onu yazın).")
    };

    // Bu kural için öneri var mı?
    public static bool Var(string ruleId) => Tablo.ContainsKey(ruleId);

    // Tablodaki kural sayısı (testler için).
    public static IEnumerable<string> Kimlikler => Tablo.Keys;

    // Tek kural için, iki sunucunun metnini yan yana veren çok satırlı öneri (bulgunun "Örnek" satırına eklenir). Yoksa null.
    public static string? Metin(string ruleId, string host)
    {
        // Tabloda yoksa öneri yok.
        if (!Tablo.TryGetValue(ruleId, out var o)) return null;
        // Metni oluştur.
        string h = host ?? "ornek.com";
        return $"--- Apache ---\n{o.Apache.Replace("{HOST}", h)}\n--- Nginx ---\n{o.Nginx.Replace("{HOST}", h)}\nNot: {o.Not}";
    }

    // Bulgu listesine sunucu önerilerini ekler (var olan 'Komut' korunur; önerisi olmayan bulgu aynen kalır).
    public static List<PasifBulgu> Zenginlestir(IEnumerable<PasifBulgu> bulgular, string host) =>
        bulgular.Select(b => { var m = Metin(b.RuleId, host); return m == null ? b : b with { Komut = string.IsNullOrWhiteSpace(b.Komut) ? m : b.Komut + "\n" + m }; }).ToList();

    // Tüm bulguların önerilerini tek bir yapılandırma dosyası metnine toplar (seçilen sunucu için). Aynı satırlar yinelenmez.
    public static string TopluDosya(IEnumerable<string> ruleIdler, string host, SunucuTuru tur)
    {
        // Başlık ve uyarılar.
        var sb = new StringBuilder(); string ad = tur == SunucuTuru.Apache ? "Apache" : "Nginx";
        sb.AppendLine($"# BulutSoft Security Suite - {host} için {ad} güvenlik düzeltme önerileri");
        sb.AppendLine($"# Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine("# DİKKAT: Bu dosya OTOMATİK UYGULANMAZ. Her bloğu okuyup anladıktan sonra, yedek alarak ilgili yapılandırma dosyasına kendiniz ekleyin.");
        sb.AppendLine(tur == SunucuTuru.Apache ? "# Sınama: sudo apachectl configtest   Uygulama: sudo systemctl reload apache2" : "# Sınama: sudo nginx -t   Uygulama: sudo systemctl reload nginx");
        sb.AppendLine("# '#' ile başlayan satırlar açıklamadır. Gerçek adreslerinizi (IP, alt alan) kendinize göre değiştirin.");
        sb.AppendLine();
        // Her kural bir kez, yinelenen metinler atlanır.
        var goruldu = new HashSet<string>(); var kimlikler = ruleIdler.Distinct(StringComparer.OrdinalIgnoreCase).Where(Var).ToList();
        foreach (var id in kimlikler)
        {
            // Öneri metni.
            var o = Tablo[id]; string govde = (tur == SunucuTuru.Apache ? o.Apache : o.Nginx).Replace("{HOST}", host); if (!goruldu.Add(govde)) continue;
            // Blok.
            sb.AppendLine($"# ---- {id} ----"); sb.AppendLine($"# {o.Not}"); sb.AppendLine(govde); sb.AppendLine();
        }
        // Hiç öneri yoksa.
        if (kimlikler.Count == 0) sb.AppendLine("# Bu denetimde sunucu ayarıyla düzeltilecek bulgu çıkmadı.");
        return sb.ToString();
    }
}
