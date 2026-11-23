// Bu dosya Asistan'ın "yerel bilgi bankası"nı koddan çıkarıp bir JSON dosyasına taşır: %AppData%\BulutSoft\tehdit_veritabani.json
// Program açılırken dosyayı okur; dosya yoksa yerleşik içerikle OTOMATİK oluşturur.
// Böylece koda hiç dokunmadan yeni port/hizmet tanımları ve yeni "açık → çözüm" kuralları eklenebilir.
// Dosyayı düzenledikten sonra "Hakkımızda & Destek" penceresindeki "↻ Zafiyet Veritabanını Yenile" düğmesine basmak yeter.
using System.Text.Encodings.Web;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Bir hizmetin (portun) bilgisi: adı, sade açıklaması, hassas olup olmadığı ve önem derecesi.
internal sealed class HizmetKaydi
{
    // Kapı (port) numarası.
    public int Port { get; set; }
    // Kısa ad (örn. SMB).
    public string Ad { get; set; } = "";
    // Sade açıklama (örn. "Windows dosya paylaşımı (SMB)").
    public string Aciklama { get; set; } = "";
    // Açık olması tek başına dikkat gerektiriyor mu? (Akıllı korelasyon bunu kullanır.)
    public bool Hassas { get; set; }
    // Açık bulunduğunda tablodaki önem derecesi: Bilgi, Dusuk, Orta veya Yuksek.
    public string Onem { get; set; } = "Bilgi";
}

// Bir "bulgu → çözüm" kuralı.
internal sealed class KuralKaydi
{
    // Başlıkta aranacak ifadeler. Düz yazı = içinde geçsin; "^" ile başlarsa = bununla başlasın; "=" ile başlarsa = tam eşit olsun.
    public List<string> Eslesen { get; set; } = new();
    // Yalnızca bu kontrol modülünün bulgularına uygula (boşsa hepsi).
    public string? Modul { get; set; }
    // "Bu ne demek?" metni. {port}, {hizmet}, {etiket}, {deger} yer tutucuları kullanılabilir.
    public string NeDemek { get; set; } = "";
    // "Nasıl çözerim?" metni.
    public string NasilCozerim { get; set; } = "";
    // Hazır komut (güvenlik duvarı kuralı değilse).
    public string Komut { get; set; } = "";
    // Komut kutusunun başlığı.
    public string Etiket { get; set; } = "";
    // Doğruysa komut, "Şablonlar" bölümündeki güvenlik duvarı şablonundan üretilir (Windows: netsh, Linux: ufw).
    public bool GuvenlikDuvari { get; set; }
    // Kuralın varsayılan port numarası (bulguda port yazmıyorsa bu kullanılır).
    public int? Port { get; set; }
    // Bulguda port numarası ZORUNLU mu?
    public bool PortGerekli { get; set; }
    // Yalnızca Windows'ta komutun altına eklenecek ek komutlar.
    public string EkKomutWindows { get; set; } = "";
    // Yalnızca Linux'ta komutun altına eklenecek ek komutlar.
    public string EkKomutLinux { get; set; } = "";
    // Doğruysa başlıktaki koruma etiketi adı {etiket} ve önerilen değeri {deger} olarak hazırlanır.
    public bool EtiketCikar { get; set; }
}

// JSON dosyasının tamamı.
internal sealed class TehditVeritabaniDosyasi
{
    // Şema sürümü.
    public int Surum { get; set; } = 1;
    // Dosyayı düzenleyenler için kısa açıklama.
    public string Aciklama { get; set; } = "";
    // Komut şablonları ve başlıklar.
    public Dictionary<string, string> Sablonlar { get; set; } = new();
    // Koruma etiketi → önerilen değer.
    public Dictionary<string, string> EtiketDegerleri { get; set; } = new();
    // Hizmet (port) tanımları.
    public List<HizmetKaydi> Hizmetler { get; set; } = new();
    // Bulgu → çözüm kuralları (yukarıdan aşağıya; ilk uyan kazanır).
    public List<KuralKaydi> Kurallar { get; set; } = new();
}

// Veritabanını yükleyen, saklayan ve sorgulayan yardımcı.
internal static class TehditVeritabani
{
    // Dosyanın yolu.
    public static string Yol => Path.Combine(BrandConfig.RoamingKlasor, "tehdit_veritabani.json");
    // JSON'u okunaklı ve Türkçe karakterleri bozmadan yazmak için ayar.
    private static readonly JsonSerializerOptions Yaz = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    // JSON'u okurken: büyük/küçük harfe takılma, yorum ve sondaki virgüle izin ver.
    private static readonly JsonSerializerOptions Oku = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    // Şu an kullanılan veritabanı (başta yerleşik içerik).
    private static TehditVeritabaniDosyasi _db = Yerlesik();
    // Son yükleme durumu (kullanıcıya gösterilebilir).
    public static string SonDurum { get; private set; } = "Yerleşik bilgi bankası kullanılıyor.";

    // Kurallar listesi.
    public static IReadOnlyList<KuralKaydi> Kurallar => _db.Kurallar;
    // Hizmet sayısı.
    public static int HizmetSayisi => _db.Hizmetler.Count;

    // Program açılırken çağrılır: dosya yoksa oluşturur, varsa okur. Hata olursa program çalışmaya devam eder.
    public static void Yukle() => Yenile();

    // Dosyayı yeniden okur (yoksa oluşturur). Sonucu (başarılı mı, mesaj) olarak verir.
    public static (bool Basarili, string Mesaj) Yenile()
    {
        // Her türlü dosya sorununa karşı koruma.
        try
        {
            // Klasör yoksa oluştur.
            Directory.CreateDirectory(Path.GetDirectoryName(Yol)!);
            // Dosya yoksa yerleşik içerikle oluştur.
            if (!File.Exists(Yol))
            {
                // Yerleşik içeriği hazırla ve dosyaya yaz.
                _db = Yerlesik(); Depo.YazJson(Yol, _db, Yaz);
                // Durumu yaz.
                SonDurum = $"Dosya yoktu, yerleşik içerikle oluşturuldu: {_db.Hizmetler.Count} hizmet, {_db.Kurallar.Count} kural.";
                // Başarılı.
                return (true, SonDurum);
            }
            // Dosyayı oku.
            var okunan = JsonSerializer.Deserialize<TehditVeritabaniDosyasi>(File.ReadAllText(Yol), Oku);
            // Boş veya anlamsızsa kullanıcının dosyasına dokunmadan yerleşik içeriği kullan.
            if (okunan == null || okunan.Kurallar.Count == 0)
            {
                // Yerleşik içeriğe dön.
                _db = Yerlesik();
                // Durumu yaz.
                SonDurum = "Dosyada hiç kural bulunamadı; yerleşik bilgi bankası kullanılıyor (dosyanıza dokunulmadı).";
                // Başarısız.
                return (false, SonDurum);
            }
            // Şablonlar eksikse yerleşik şablonlarla tamamla.
            foreach (var s in Yerlesik().Sablonlar) okunan.Sablonlar.TryAdd(s.Key, s.Value);
            // Okunanı kullan.
            _db = okunan;
            // Durumu yaz.
            SonDurum = $"Yenilendi: {_db.Hizmetler.Count} hizmet, {_db.Kurallar.Count} kural.";
            // Başarılı.
            return (true, SonDurum);
        }
        // Dosya bozuksa (geçersiz JSON vb.) yerleşik içerikle devam et; dosyanın üzerine yazma.
        catch (Exception ex)
        {
            // Yerleşik içeriğe dön.
            _db = Yerlesik();
            // Durumu yaz.
            SonDurum = "Dosya okunamadı (" + ex.Message + "); yerleşik bilgi bankası kullanılıyor. Dosyanızı düzeltip tekrar yenileyin.";
            // Başarısız.
            return (false, SonDurum);
        }
    }

    // Bir portun hizmet kaydını verir (yoksa boş).
    public static HizmetKaydi? Hizmet(int port) => _db.Hizmetler.FirstOrDefault(h => h.Port == port);

    // Bir portun sade açıklamasını verir (bilinmiyorsa "bir ağ hizmeti").
    public static string HizmetAciklama(int port) => Hizmet(port) is { Aciklama.Length: > 0 } h ? h.Aciklama : "bir ağ hizmeti";

    // Bir komut şablonunu veya başlığı verir (dosyada yoksa varsayılan).
    public static string Sablon(string ad, string varsayilan = "") => _db.Sablonlar.TryGetValue(ad, out var s) && !string.IsNullOrEmpty(s) ? s : varsayilan;

    // Bir koruma etiketinin önerilen değerini verir (yoksa "DENY").
    public static string EtiketDegeri(string etiket) => _db.EtiketDegerleri.TryGetValue(etiket, out var d) ? d : "DENY";

    // {anahtar} yer tutucularını değerlerle doldurur.
    public static string Doldur(string metin, Dictionary<string, string> degerler)
    {
        // Her yer tutucuyu değiştir.
        foreach (var d in degerler) metin = metin.Replace("{" + d.Key + "}", d.Value);
        // Doldurulmuş metni ver.
        return metin;
    }

    // Bir kuralın başlıkla eşleşip eşleşmediğini söyler.
    public static bool Uyar(KuralKaydi k, Bulgu b)
    {
        // Modül şartı varsa ona uymalı.
        if (k.Modul != null && b.Modul != k.Modul) return false;
        // Eşleşen ifade yoksa (yalnızca modül şartıyla) uyar.
        if (k.Eslesen.Count == 0) return k.Modul != null;
        // İfadelerden biri uysa yeter.
        foreach (var e in k.Eslesen)
        {
            // "=" ile başlıyorsa tam eşitlik.
            if (e.StartsWith('=')) { if (b.Baslik == e[1..]) return true; }
            // "^" ile başlıyorsa başlangıç eşleşmesi.
            else if (e.StartsWith('^')) { if (b.Baslik.StartsWith(e[1..], StringComparison.Ordinal)) return true; }
            // Düz yazıysa içinde geçsin.
            else if (b.Baslik.Contains(e, StringComparison.Ordinal)) return true;
        }
        // Hiçbiri uymadı.
        return false;
    }

    // Yerleşik (varsayılan) içeriği üretir. Dosya yoksa bu içerik dosyaya yazılır.
    private static TehditVeritabaniDosyasi Yerlesik()
    {
        // Yeni veritabanı nesnesi.
        var db = new TehditVeritabaniDosyasi
        {
            // Düzenleme rehberi.
            Aciklama = "BulutSoft Security Suite bilgi bankası. Kurallar yukarıdan aşağıya denenir, ilk uyan kazanır. Eslesen: düz yazı=içinde geçsin, ^=ile başlasın, ==tam eşit. Yer tutucular: {port} {hizmet} {etiket} {deger}. Değişiklikten sonra Hakkımızda & Destek > Zafiyet Veritabanını Yenile.",
            // Komut şablonları ve komut kutusu başlıkları.
            Sablonlar = new()
            {
                // Windows güvenlik duvarı komutu.
                ["GuvenlikDuvariWindows"] = "netsh advfirewall firewall add rule name=\"BulutSoft-Blok-{port}\" dir=in action=block protocol=TCP localport={port}\r\n\r\n# Geri almak için:\r\n# netsh advfirewall firewall delete rule name=\"BulutSoft-Blok-{port}\"",
                // Linux güvenlik duvarı komutu.
                ["GuvenlikDuvariLinux"] = "sudo ufw deny {port}/tcp\r\n\r\n# Geri almak için:\r\n# sudo ufw delete deny {port}/tcp",
                // Windows başlığı.
                ["EtiketWindows"] = "ÇÖZÜM KOMUTU · Windows (Yönetici olarak açılmış PowerShell)",
                // Linux başlığı.
                ["EtiketLinux"] = "ÇÖZÜM KOMUTU · Linux (hedef bilgisayardaki terminal)"
            },
            // Koruma etiketlerinin önerilen değerleri.
            EtiketDegerleri = new()
            {
                // HSTS: hep şifreli bağlan.
                ["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains",
                // CSP: yalnızca kendi içeriğine izin ver.
                ["Content-Security-Policy"] = "default-src 'self'",
                // Dosya türü tahminini engelle.
                ["X-Content-Type-Options"] = "nosniff",
                // Referans bilgisini gönderme.
                ["Referrer-Policy"] = "no-referrer",
                // Gömülmeyi engelle.
                ["X-Frame-Options"] = "DENY"
            },
            // Hizmet (port) tanımları.
            Hizmetler = new()
            {
                // FTP.
                new() { Port = 21, Ad = "FTP", Aciklama = "dosya aktarımı (FTP)", Hassas = true, Onem = "Dusuk" },
                // SSH.
                new() { Port = 22, Ad = "SSH", Aciklama = "şifreli uzaktan bağlantı (SSH)" },
                // Telnet.
                new() { Port = 23, Ad = "Telnet", Aciklama = "eski ve şifresiz uzaktan bağlantı (Telnet)", Hassas = true, Onem = "Orta" },
                // SMTP.
                new() { Port = 25, Ad = "SMTP", Aciklama = "e-posta gönderme (SMTP)" },
                // DNS.
                new() { Port = 53, Ad = "DNS", Aciklama = "isim çözümleme (DNS)" },
                // HTTP.
                new() { Port = 80, Ad = "HTTP", Aciklama = "şifresiz web sitesi (HTTP)" },
                // POP3.
                new() { Port = 110, Ad = "POP3", Aciklama = "e-posta alma (POP3)" },
                // RPC.
                new() { Port = 135, Ad = "RPC", Aciklama = "Windows uzaktan yordam çağrısı (RPC)", Hassas = true, Onem = "Dusuk" },
                // NetBIOS.
                new() { Port = 139, Ad = "NetBIOS", Aciklama = "Windows dosya ve yazıcı paylaşımı (NetBIOS)", Hassas = true, Onem = "Dusuk" },
                // IMAP.
                new() { Port = 143, Ad = "IMAP", Aciklama = "e-posta alma (IMAP)" },
                // HTTPS.
                new() { Port = 443, Ad = "HTTPS", Aciklama = "şifreli web sitesi (HTTPS)" },
                // SMB.
                new() { Port = 445, Ad = "SMB", Aciklama = "Windows dosya paylaşımı (SMB)", Hassas = true, Onem = "Orta" },
                // MSSQL.
                new() { Port = 1433, Ad = "MSSQL", Aciklama = "Microsoft SQL Server veritabanı" },
                // MySQL.
                new() { Port = 3306, Ad = "MySQL", Aciklama = "MySQL veritabanı", Hassas = true, Onem = "Dusuk" },
                // RDP.
                new() { Port = 3389, Ad = "RDP", Aciklama = "Windows uzak masaüstü (RDP)", Hassas = true, Onem = "Orta" },
                // PostgreSQL.
                new() { Port = 5432, Ad = "PostgreSQL", Aciklama = "PostgreSQL veritabanı" },
                // WinRM.
                new() { Port = 5985, Ad = "WinRM", Aciklama = "Windows uzaktan yönetim (WinRM)" },
                // VNC.
                new() { Port = 5900, Ad = "VNC", Aciklama = "ekran paylaşımı (VNC)", Hassas = true, Onem = "Dusuk" },
                // Alternatif web.
                new() { Port = 8080, Ad = "HTTP-Alt", Aciklama = "alternatif web sitesi (HTTP)" },
                // Alternatif şifreli web.
                new() { Port = 8443, Ad = "HTTPS-Alt", Aciklama = "alternatif şifreli web sitesi (HTTPS)" }
            }
        };
        // Bulgu → çözüm kuralları (sıra önemlidir).
        db.Kurallar.AddRange(new KuralKaydi[]
        {
            // SMB (Windows dosya paylaşımı).
            new() { Eslesen = { "SMB" }, GuvenlikDuvari = true, Port = 445,
                NeDemek = "Bilgisayarınızın dosya paylaşımı hizmeti (SMB) ağdan erişime açık. Eski sürümü (SMBv1), fidye yazılımlarının girdiği bilinen bir kapıdır. Dosya paylaşmıyorsanız bu kapı boşuna açık duruyor.",
                NasilCozerim = "Dosya paylaşımına ihtiyacınız yoksa güvenlik duvarıyla bu kapıyı engelleyin ve eski SMBv1'i kapatın. Alttaki komut bunu yapar.",
                EkKomutWindows = "# Eski SMBv1'i kapatmak için (isteğe bağlı):\r\nSet-SmbServerConfiguration -EnableSMB1Protocol $false -Force" },
            // Telnet.
            new() { Eslesen = { "Telnet" }, GuvenlikDuvari = true, Port = 23,
                NeDemek = "Telnet bir uzaktan bağlanma yöntemidir ama yazdığınız her şeyi, parolanız dahil, şifresiz gönderir. Ağı dinleyen biri hepsini okuyabilir (postaya kartpostalla parola yazmak gibi).",
                NasilCozerim = "Telnet'i kapatın, uzaktan bağlantı için şifreli SSH kullanın. Alttaki komut Telnet kapısını engeller." },
            // FTP kimliksiz giriş.
            new() { Eslesen = { "FTP kimliksiz" }, Etiket = "ÇÖZÜM KOMUTU · Linux (vsftpd)",
                NeDemek = "Dosya sunucusu \"anonymous\" adıyla, parolasız girişe izin veriyor. Yani kapıda kimlik sorulmuyor; herkes içeri girip dosyalara bakabilir.",
                NasilCozerim = "Sunucunun ayarında anonim girişi kapatın ve servisi yeniden başlatın.",
                Komut = "sudo sed -i 's/^anonymous_enable=YES/anonymous_enable=NO/' /etc/vsftpd.conf\r\nsudo systemctl restart vsftpd\r\n\r\n# (vsftpd kullanılıyorsa. Başka FTP yazılımında aynı ayarı o yazılımın ayar dosyasında kapatın.)" },
            // FTP açık.
            new() { Eslesen = { "FTP" }, GuvenlikDuvari = true, Port = 21,
                NeDemek = "FTP, dosya aktarma yöntemidir ama parolaları ve dosyaları şifresiz gönderir; arada dinleyen biri görebilir.",
                NasilCozerim = "Şifreli SFTP veya FTPS'e geçin. FTP'ye ihtiyacınız yoksa alttaki komutla kapısını engelleyin." },
            // Eski SSH sürümü.
            new() { Eslesen = { "SSH sürümü" }, Etiket = "ÇÖZÜM KOMUTU · Linux (hedef bilgisayardaki terminal)",
                NeDemek = "Çok eski SSH sürümü (SSH-1) kullanılıyor; bu sürümün güvenlik zayıflıkları bilinir ve artık güvenli sayılmaz.",
                NasilCozerim = "Sadece yeni sürüm (SSH-2) kabul edilsin.",
                Komut = "sudo sed -i 's/^#\\?Protocol.*/Protocol 2/' /etc/ssh/sshd_config\r\nsudo systemctl restart ssh" },
            // SSH açık.
            new() { Eslesen = { "=SSH açık" }, Etiket = "ÇÖZÜM KOMUTU · Linux (hedef bilgisayardaki terminal)",
                NeDemek = "SSH, bilgisayara şifreli uzaktan bağlanma yöntemidir ve genelde güvenlidir. Asıl risk ayarlardadır: parolayla giriş ve doğrudan \"root\" girişi açıksa, saldırganlar parola tahmin edebilir.",
                NasilCozerim = "SSH açık kalsın ama root girişini kapatın, mümkünse parola yerine anahtarla giriş kullanın.",
                Komut = "sudo sed -i 's/^#\\?PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config\r\nsudo systemctl restart ssh" },
            // Ekran paylaşımı (RDP/VNC).
            new() { Eslesen = { "ekran paylaşımı" }, GuvenlikDuvari = true, Port = 3389,
                NeDemek = "Bilgisayarınızın ekranına uzaktan bağlanma hizmeti ağdan erişime açık. Parolası zayıfsa biri bilgisayarınızı kendi bilgisayarında açılmış gibi kullanabilir.",
                NasilCozerim = "Bu hizmeti kullanmıyorsanız kapıyı engelleyin. Kullanıyorsanız yalnızca güvendiğiniz adreslere açın ve güçlü parola kullanın." },
            // Veritabanı.
            new() { Eslesen = { "Veritabanı" }, GuvenlikDuvari = true, Port = 3306,
                NeDemek = "Veritabanınız ağdan erişilebilir durumda. Veritabanları genelde yalnızca aynı bilgisayardaki programlar tarafından kullanılmalıdır; dışarıya açık olması parola tahmin saldırılarını davet eder.",
                NasilCozerim = "Veritabanını yalnızca bu bilgisayardan erişilir yapın veya kapısını güvenlik duvarıyla engelleyin." },
            // Genel açık port bulgusu.
            new() { Eslesen = { "^Açık port" }, GuvenlikDuvari = true, PortGerekli = true,
                NeDemek = "Bu bilgisayarın {port} numaralı kapısı (port) dışarıdan gelen bağlantılara açık. Bu kapı normalde {hizmet} için kullanılır. Açık olması tek başına kötü değildir; ama kullanmadığınız bir hizmetin kapısı açık kalmışsa saldırgana boşuna bir giriş yolu bırakır (kapısı açık unutulmuş bir oda gibi).",
                NasilCozerim = "Bu hizmete ihtiyacınız yoksa hizmeti durdurun veya kapıyı güvenlik duvarıyla engelleyin. İhtiyacınız varsa yalnızca güvendiğiniz bilgisayarların erişmesine izin verin." },
            // Şifresiz web.
            new() { Eslesen = { "Şifresiz web", "şifresiz konuşuyor" }, Etiket = "ÖRNEK AYAR · Web sunucusu (nginx)",
                NeDemek = "Web siteniz şifresiz (http) konuşuyor. Aradaki biri, sitede yazılan her şeyi (parola dahil) okuyabilir.",
                NasilCozerim = "Siteye geçerli bir sertifika kurup https'e geçin ve http'yi https'e yönlendirin.",
                Komut = "# nginx örneği: http isteklerini https'e yönlendir\r\nserver {\r\n    listen 80;\r\n    return 301 https://$host$request_uri;\r\n}" },
            // Eksik web koruma etiketi.
            new() { Eslesen = { "^Eksik koruma etiketi", "gizlice gömülmesine" }, EtiketCikar = true, Etiket = "ÖRNEK AYAR · Web sunucusu (nginx)",
                NeDemek = "Web siteniz \"{etiket}\" adlı koruma etiketini göndermiyor. Bu etiketler tarayıcıya \"şu saldırı türlerine karşı önlem al\" der; yoksa tarayıcı ek korumayı devreye sokmaz (binanın girişinde alarm olmaması gibi).",
                NasilCozerim = "Web sunucusunun ayarına bu etiketi ekleyin. Alttaki örnek nginx içindir; IIS veya Apache'de de aynı etiket ve değer kullanılır.",
                Komut = "add_header {etiket} \"{deger}\" always;" },
            // Çerez bayrakları.
            new() { Eslesen = { "Çerez" }, Etiket = "ÖRNEK AYAR · Web sunucusu",
                NeDemek = "Çerez, sitenin tarayıcınızda sakladığı küçük nottur (oturum bilgisi gibi). Koruma bayrakları eksikse çerez başkalarınca okunabilir veya çalınabilir.",
                NasilCozerim = "Çerezi oluştururken Secure, HttpOnly ve SameSite bayraklarını ekleyin.",
                Komut = "Set-Cookie: oturum=DEGER; Secure; HttpOnly; SameSite=Lax" },
            // Sürüm sızıntısı.
            new() { Eslesen = { "kendi sürümünü" }, Etiket = "ÖRNEK AYAR · Web sunucusu (nginx)",
                NeDemek = "Sunucu, cevaplarında kendi adını ve sürümünü söylüyor. Saldırgan bu sürümdeki bilinen açıkları arayıp ona göre hareket edebilir.",
                NasilCozerim = "Sürüm bilgisini cevaplardan kaldırın.", Komut = "server_tokens off;" },
            // Eski şifreleme sürümü.
            new() { Eslesen = { "Eski şifreleme sürümü" }, Etiket = "ÖRNEK AYAR · Web sunucusu (nginx)",
                NeDemek = "Sunucu eski ve güvensiz şifreleme sürümlerini (TLS 1.0/1.1) hâlâ kabul ediyor. Bu sürümlerdeki zayıflıklar bilinir.",
                NasilCozerim = "Yalnızca TLS 1.2 ve üstüne izin verin.", Komut = "ssl_protocols TLSv1.2 TLSv1.3;" },
            // Sertifika sorunları.
            new() { Eslesen = { "Sertifika", "sertifika", "imza yöntemi", "anahtarı" }, Etiket = "ÖRNEK KOMUT · Deneme sertifikası (OpenSSL)",
                NeDemek = "Sertifika, sitenin \"gerçekten ben\" diyen kimlik kartıdır. Süresi dolmuş, kendi kendine verilmiş veya zayıfsa tarayıcılar uyarı verir ve araya girmek kolaylaşır.",
                NasilCozerim = "Güvenilir bir kuruluştan geçerli, yeni bir sertifika alın. Test ortamında deneme için kendi sertifikanızı üretebilirsiniz (alttaki örnek).",
                Komut = "openssl req -x509 -newkey rsa:3072 -nodes -keyout lab.key -out lab.crt -days 365 -subj \"/CN=lab.local\"" },
            // Alt isim bulgusu.
            new() { Eslesen = { "Alt isim" },
                NeDemek = "Alan adınıza bağlı bir alt isim bulundu. \"admin\", \"dev\", \"test\", \"backup\" gibi isimler genelde yönetim veya deneme sistemleridir; dışarıdan görünür olmaları saldırganlara ipucu verir.",
                NasilCozerim = "Dışarıya açık olması gerekmiyorsa bu isim kaydını silin veya erişimi yalnızca güvendiğiniz adreslerle sınırlayın." },
            // Form kutusu testi bulguları.
            new() { Modul = "Girdi",
                NeDemek = "Sitenizdeki bir kutucuğa yazılan yazılar kontrolsüz işleniyor olabilir. Kötü niyetli biri bunu kullanarak sayfaya zararlı kod sokabilir veya veritabanından bilgi sızdırabilir.",
                NasilCozerim = "Kullanıcıdan gelen her yazıyı doğrulayın; sayfaya basarken güvenli biçime (HTML encode) çevirin; veritabanı sorgularında hazır (parametreli) sorgu kullanın; hata mesajlarını kullanıcıya göstermeyin." }
        });
        // Hazır veritabanını ver.
        return db;
    }
}
