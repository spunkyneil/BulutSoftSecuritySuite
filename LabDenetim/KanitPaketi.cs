// Bu dosya "Siber Kara Kutu" — Adli Kanıt Paketi'ni üretir: o anki tarama sonuçları, canlı internet bağlantıları, açık portlar,
// hosts dosyası özeti, sistem bilgileri ve HTML raporu tek bir zaman damgalı .zip dosyasına paketlenir (masaüstüne).
// Paketin içine her dosyanın SHA-256 "parmak izi" de yazılır; böylece sonradan değiştirilip değiştirilmediği anlaşılır.
// Program yalnızca OKUR ve yazar; sisteme hiçbir ayar değişikliği yapmaz. Parola, anahtar vb. sırlar pakete konmaz.
using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Kanıt paketini üreten yardımcı (ekrana dokunmaz).
internal static class KanitPaketi
{
    // Paket dosya adı: BulutSoft_KanitPaketi_YYYYAAGG_SSDDss.zip
    public static string DosyaAdi(DateTime an) => $"BulutSoft_KanitPaketi_{an:yyyyMMdd_HHmmss}.zip";

    // Aktif TCP bağlantılarını okunaklı metne çevirir.
    public static string BaglantiMetni()
    {
        // Metin kutusu.
        var sb = new StringBuilder();
        // Başlık.
        sb.AppendLine("AKTİF İNTERNET / AĞ BAĞLANTILARI (anlık görüntü)").AppendLine(new string('=', 60));
        // Bağlantıları oku (PID'li liste).
        foreach (var b in TrafikIzleyici.Oku()) sb.AppendLine($"{b.Program} (PID {b.Pid}) | yerel :{b.YerelPort} -> {b.UzakIp}:{b.UzakPort} | {b.Durum} | {(b.DisAg ? "dış ağ" : "yerel ağ")} | {b.Seviye} | {b.Not} | {b.Yol}");
        // Metni ver.
        return sb.ToString();
    }

    // Bu bilgisayarda dinlenen (açık) TCP ve UDP kapılarını metne çevirir.
    public static string AcikPortMetni()
    {
        // Metin kutusu.
        var sb = new StringBuilder();
        // Başlık.
        sb.AppendLine("BU BİLGİSAYARDA DİNLENEN (AÇIK) KAPILAR (anlık görüntü)").AppendLine(new string('=', 60));
        // Windows'tan dinleyen kapıları sor.
        var ip = IPGlobalProperties.GetIPGlobalProperties();
        // TCP kapıları.
        foreach (var e in ip.GetActiveTcpListeners().OrderBy(x => x.Port)) sb.AppendLine($"TCP {e.Address}:{e.Port}");
        // UDP kapıları.
        foreach (var e in ip.GetActiveUdpListeners().OrderBy(x => x.Port)) sb.AppendLine($"UDP {e.Address}:{e.Port}");
        // Metni ver.
        return sb.ToString();
    }

    // hosts dosyası özeti: yorum olmayan satırlar, boyut, değişiklik tarihi ve SHA-256, ayrıca DNS sunucuları.
    public static string HostsMetni()
    {
        // Metin kutusu.
        var sb = new StringBuilder();
        // Başlık.
        sb.AppendLine("HOSTS DOSYASI ÖZETİ").AppendLine(new string('=', 60));
        // Dosya yolu.
        string yol = DnsDenetimi.HostsYolu; sb.AppendLine("Yol: " + yol);
        // Dosya okunabilir mi?
        try
        {
            // Dosya bilgisi.
            var fi = new FileInfo(yol);
            // Dosya yoksa söyle.
            if (!fi.Exists) sb.AppendLine("Dosya bulunamadı.");
            else
            {
                // Boyut ve tarih.
                sb.AppendLine($"Boyut: {fi.Length} bayt · Son değişiklik: {fi.LastWriteTime:dd.MM.yyyy HH:mm:ss}");
                // Parmak izi.
                sb.AppendLine("SHA-256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(yol))));
                // Yorum olmayan satırlar.
                var aktif = File.ReadAllLines(yol).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')).ToList();
                // Aktif satır sayısı.
                sb.AppendLine($"Etkin (yorum olmayan) satır sayısı: {aktif.Count}");
                // En fazla 200 satırı yaz.
                foreach (var s in aktif.Take(200)) sb.AppendLine("  " + s);
                // Fazlası varsa belirt.
                if (aktif.Count > 200) sb.AppendLine($"  … ve {aktif.Count - 200} satır daha");
            }
        }
        // Okunamazsa nedenini yaz.
        catch (Exception ex) { sb.AppendLine("Okunamadı: " + ex.Message); }
        // DNS sunucuları.
        sb.AppendLine().AppendLine("AĞ KARTLARI DNS SUNUCULARI").AppendLine(new string('-', 60));
        // Her açık kart için DNS adresleri.
        try { foreach (var k in NetworkInterface.GetAllNetworkInterfaces().Where(k => k.OperationalStatus == OperationalStatus.Up && k.NetworkInterfaceType != NetworkInterfaceType.Loopback)) sb.AppendLine($"{k.Name}: {string.Join(", ", k.GetIPProperties().DnsAddresses)}"); }
        // Okunamazsa geç.
        catch (Exception ex) { sb.AppendLine("Okunamadı: " + ex.Message); }
        // Metni ver.
        return sb.ToString();
    }

    // Sistem bilgileri: işletim sistemi, makine, bellek, açık kalma süresi, ağ kartları. (Parola/anahtar içermez.)
    public static string SistemMetni()
    {
        // Metin kutusu.
        var sb = new StringBuilder();
        // Başlık.
        sb.AppendLine("SİSTEM BİLGİLERİ").AppendLine(new string('=', 60));
        // Genel bilgiler.
        sb.AppendLine($"Bilgisayar adı: {Environment.MachineName}").AppendLine($"Kullanıcı: {Environment.UserName}").AppendLine($"İşletim sistemi: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        // Mimari ve çekirdek sayısı.
        sb.AppendLine($"Mimari: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} · Mantıksal işlemci: {Environment.ProcessorCount}").AppendLine($".NET: {Environment.Version}");
        // Açık kalma süresi.
        sb.AppendLine($"Açık kalma süresi: {TimeSpan.FromMilliseconds(Environment.TickCount64):d\\.hh\\:mm\\:ss}").AppendLine($"Paket zamanı: {DateTime.Now:dd.MM.yyyy HH:mm:ss} (yerel) / {DateTime.UtcNow:O} (UTC)");
        // Ağ kartları.
        sb.AppendLine().AppendLine("AĞ KARTLARI").AppendLine(new string('-', 60));
        // Kartları listele.
        try
        {
            // Her kart.
            foreach (var k in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Kart adı ve durumu.
                sb.AppendLine($"{k.Name} · {k.NetworkInterfaceType} · {k.OperationalStatus}");
                // Adresler.
                foreach (var a in k.GetIPProperties().UnicastAddresses) sb.AppendLine("    " + a.Address);
            }
        }
        // Okunamazsa geç.
        catch (Exception ex) { sb.AppendLine("Okunamadı: " + ex.Message); }
        // Metni ver.
        return sb.ToString();
    }

    // Tarama sonuçlarını okunaklı metne çevirir.
    public static string SonucMetni(RaporBilgisi bilgi, IEnumerable<Bulgu> bulgular)
    {
        // Metin kutusu.
        var sb = new StringBuilder();
        // Başlık bilgileri.
        sb.AppendLine("TARAMA SONUÇLARI").AppendLine(new string('=', 60)).AppendLine($"Hedef: {bilgi.Hedef} · Tarih: {bilgi.Tarih:dd.MM.yyyy HH:mm:ss} · Profil: {bilgi.Profil}").AppendLine($"Puan: {bilgi.Puan}/100 · Kırmızı {bilgi.Kirmizi} · Sarı {bilgi.Sari} · Yeşil {bilgi.Yesil} · Atlanan {bilgi.Atlanan} · Taranan kapı {bilgi.KapiSayisi}").AppendLine();
        // Her bulgu.
        foreach (var b in bulgular) sb.AppendLine($"[{b.Onem}] ({b.Modul}) {b.Baslik}").AppendLine("    Ayrıntı: " + b.Detay.Replace("\n", "\n             ")).AppendLine("    Öneri: " + b.Oneri).AppendLine();
        // Metni ver.
        return sb.ToString();
    }

    // Tüm parçaları tek zip dosyasında toplar. htmlYaz: HTML raporunu verilen yola yazan işlev.
    // operatorAd: paketi hazırlayan oturumun kullanıcısı (sorumluluk zinciri için manifeste yazılır).
    public static string Olustur(string klasor, DateTime an, RaporBilgisi bilgi, IReadOnlyCollection<Bulgu> bulgular, Action<string> htmlYaz, string? operatorAd = null)
    {
        // Paketin son yolu.
        string yol = Path.Combine(klasor, DosyaAdi(an));
        // Zip'e girecek dosyalar: ad -> içerik.
        var dosyalar = new Dictionary<string, byte[]>();
        // UTF-8 (BOM'lu: Not Defteri Türkçe karakterleri doğru açsın).
        var utf8 = new UTF8Encoding(true);
        // Yazılı parçalar.
        dosyalar["01_tarama_sonuclari.txt"] = utf8.GetBytes(SonucMetni(bilgi, bulgular));
        // Makine tarafından okunur JSON sürümü.
        dosyalar["01_tarama_sonuclari.json"] = utf8.GetBytes(JsonSerializer.Serialize(new { Bilgi = bilgi, Bulgular = bulgular }, new JsonSerializerOptions { WriteIndented = true }));
        // Bağlantılar.
        dosyalar["02_aktif_baglantilar.txt"] = utf8.GetBytes(BaglantiMetni());
        // Açık kapılar.
        dosyalar["03_acik_portlar.txt"] = utf8.GetBytes(AcikPortMetni());
        // hosts özeti.
        dosyalar["04_hosts_ozeti.txt"] = utf8.GetBytes(HostsMetni());
        // Sistem bilgisi.
        dosyalar["05_sistem_bilgisi.txt"] = utf8.GetBytes(SistemMetni());
        // KVKK veri ihlali müdahale planı şablonu (72 saat kuralı) ve ATT&CK sürümü bilgisi.
        dosyalar["07_KVKK_ihlal_mudahale_plani.txt"] = utf8.GetBytes(KvkkIhlalPlani.Metin(an) + "\r\n\r\nEtiket sürümü: " + Mitre.Surum + "\r\nUyumluluk kaynağı: " + Uyumluluk.Kaynak + "\r\n");
        // HTML raporu: geçici dosyaya yazıp oku.
        string gecici = Path.Combine(Path.GetTempPath(), "bulutsoft_kanit_" + Guid.NewGuid().ToString("N") + ".html");
        // Hata olsa da geçici dosya silinsin.
        try { htmlYaz(gecici); dosyalar["06_rapor.html"] = File.ReadAllBytes(gecici); }
        // Geçici dosyayı sil.
        finally { try { File.Delete(gecici); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KanitPaketi.Olustur", true); } }
        // KAPSAM.txt: paketin neyi kapsadığı ve kapsamadığı (okuyanın anlayacağı düz metin).
        var kap = new StringBuilder(); kap.AppendLine("KAPSAM — BULUTSOFT ADLİ KANIT PAKETİ").AppendLine(new string('=', 60)).AppendLine($"Üretim zamanı: {an:dd.MM.yyyy HH:mm:ss} (yerel) / {DateTime.UtcNow:O} (UTC)").AppendLine($"Operatör: {operatorAd ?? "bilinmiyor"} · Bilgisayar: {Environment.MachineName} · Program sürümü: {typeof(KanitPaketi).Assembly.GetName().Version?.ToString(3)}").AppendLine($"Tarama hedefi: {bilgi.Hedef} · {bulgular.Count} bulgu · puan {bilgi.Puan}/100").AppendLine().AppendLine("DAHİL OLANLAR:");
        foreach (var d in dosyalar.OrderBy(x => x.Key)) kap.AppendLine($"  {d.Key} ({d.Value.Length / 1024.0:0.0} KB)");
        kap.AppendLine().AppendLine("DAHİL OLMAYANLAR: kullanıcı dosyalarının içeriği, parolalar, API anahtarları, ekran görüntüleri, olay günlüğü (olaylar.jsonl; ayrıca zincirli saklanır), güvenlik duvarı yedekleri. Paket yalnızca paketlendiği andaki anlık durumu gösterir; önceki durumları kanıtlamaz.").AppendLine("Kişisel veri notu (KVKK): bilgisayar adı, kullanıcı adı, IP adresleri, açık bağlantılar ve hosts dosyası içerir; paylaşırken gerekli olandan fazlasını vermeyin.");
        dosyalar["KAPSAM.txt"] = utf8.GetBytes(kap.ToString());
        // Bütünlük listesi: her dosyanın SHA-256 değeri.
        var man = new StringBuilder();
        // Başlık ve açıklama.
        man.AppendLine("BULUTSOFT SİBER KARA KUTU — ADLİ KANIT PAKETİ").AppendLine(new string('=', 60)).AppendLine($"Oluşturulma: {an:dd.MM.yyyy HH:mm:ss}").AppendLine($"Hedef: {bilgi.Hedef}").AppendLine($"Hazırlayan (operatör): {operatorAd ?? "bilinmiyor (oturum yok)"} · Bilgisayar: {Environment.MachineName}").AppendLine($"Program: BulutSoft Security Suite {typeof(KanitPaketi).Assembly.GetName().Version?.ToString(3)} · Bilgisayar saati (UTC): {DateTime.UtcNow:O}").AppendLine().AppendLine("Bu pakette ne var?").AppendLine("  01 tarama sonuçları · 02 aktif bağlantılar · 03 açık portlar · 04 hosts özeti · 05 sistem bilgisi · 06 HTML rapor").AppendLine().AppendLine("Bütünlük (SHA-256) — dosyalar sonradan değiştirilirse bu değerler tutmaz:");
        // DÜRÜST NOT: bu liste paketin İÇİNDEDİR; paketi değiştiren kişi listeyi de yeniden üretebilir. Dış güvenceler ayrı dosyalardır ve neyi kanıtladıkları aşağıda açıkça yazılıdır.
        man.AppendLine("(Bu liste paketin içinde olduğundan TEK BAŞINA yeterli kanıt değildir: paketi değiştiren kişi listeyi de yeniden üretebilir.)");
        man.AppendLine("Dış güvenceler (zip'in yanında, ayrı dosyalar):");
        man.AppendLine("  .sha256 : zip'in tamamının özeti. Paket sonradan değişirse tutmaz; ama bu dosyayı da değiştiren, yenisini yazabilir. Ayrı bir yerde saklanmalıdır.");
        man.AppendLine("  .hmac   : paketin özeti ve künyesi, koruma hizmetindeki GİZLİ anahtarla imzalanır. Yalnızca AYNI bilgisayardaki hizmet doğrulayabilir; anahtara (SYSTEM/Yönetici) sahip biri yeniden üretebilir. Hizmet yoksa üretilmez.");
        man.AppendLine("  .tsr    : bağımsız bir zaman damgası sunucusunun (RFC 3161) imzası: özetin o tarihte var olduğunu gösterir. Yalnızca kullanıcı onayıyla alınır; alınmadıysa bu dosya yoktur.");
        // Her dosyanın parmak izi.
        foreach (var d in dosyalar.OrderBy(x => x.Key)) man.AppendLine($"  {Convert.ToHexString(SHA256.HashData(d.Value))}  {d.Key}");
        // Manifest dosyası.
        dosyalar["00_OKUBENI_butunluk.txt"] = utf8.GetBytes(man.ToString());
        // Klasör yoksa oluştur.
        Directory.CreateDirectory(klasor);
        // Aynı ad varsa üzerine yaz.
        if (File.Exists(yol)) File.Delete(yol);
        // Zip'i yaz.
        using (var zip = ZipFile.Open(yol, ZipArchiveMode.Create))
            // Her dosyayı arşive ekle.
            foreach (var d in dosyalar.OrderBy(x => x.Key))
            {
                // Zip girdisi (zaman damgası paket anı).
                var giris = zip.CreateEntry(d.Key, CompressionLevel.Optimal); giris.LastWriteTime = an;
                // İçeriği yaz.
                using var s = giris.Open(); s.Write(d.Value, 0, d.Value.Length);
            }
        // Dış özet: zip dosyasının tamamının SHA-256 değeri yanına ayrı dosya olarak yazılır (sha256sum biçimi). Paketin sonradan değiştirilip değiştirilmediği bununla ve onu saklayan ayrı kayıtla anlaşılır.
        try { string ozet = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(yol))); File.WriteAllText(yol + ".sha256", ozet + "  " + Path.GetFileName(yol) + Environment.NewLine, new UTF8Encoding(false)); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "KanitPaketi.Olustur", true); }
        // Dosya yolunu geri ver.
        return yol;
    }
}
