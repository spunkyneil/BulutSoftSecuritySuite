// Gözetimsiz (arayüzsüz) tarama: "BulutSoftSecuritySuite.exe --tara --cikti <klasör veya dosya.json>"
// Bu bilgisayarın mevcut SALT-OKUNUR yerel denetimini çalıştırır ve sonucu tek bir JSON özet dosyasına yazar.
// GÜVENLİK: Hiçbir ayarı değiştirmez, hiçbir şeyi kapatmaz/silmez/engellemez; internete çıkmaz. Kullanıcı arayüzünün ortak veri
// dosyalarına (bulgu deposu, varlık haritası, yanlış alarm listesi) YAZMAZ; yalnızca istenen çıktı dosyasını yazar.
// Amaç: BT sorumlusunun 50 bilgisayardan paylaşılan bir klasöre makine başına birer özet toplaması (sonraki adım: toplu rapor).
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LabDenetim.Moduller;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Özet dosyasındaki tek bulgu (kısaltılmış, makineler arası taşınabilir).
internal sealed record OzetBulgu(string Modul, string Baslik, string Onem, string Detay, string Oneri);

// Bir bilgisayarın tarama özeti. Format: dosya şeması sürümü (toplu rapor eski dosyaları tanıyabilsin diye).
// Atlanan: çalıştırılamayan kontrol sayısı (taramanın ne kadar tam olduğunu gösterir; sayılara ve puana girmez).
internal sealed record TaramaOzeti(int Format, string Makine, DateTime ZamanUtc, string Surum, bool Yonetici, int Puan, string Seviye, int Yuksek, int Orta, int Dusuk, int Bilgi, int Atlanan, List<OzetBulgu> Bulgular, string? Lisans = null, int? LisansKalanGun = null);

// Komut satırı isteği.
// Filo: "--filo <klasör>" ile verilen özet klasörü (toplu rapor isteği).
internal sealed record CliIstek(bool Tara, bool Yardim, string? Cikti, string? Hata, string? Filo = null);

// Gözetimsiz tarama mantığı.
internal static class TaramaCalistirici
{
    // Çıkış kodları: 0 = başarılı, 1 = hatalı kullanım, 2 = tarama/yazma hatası.
    public const int Tamam = 0, KullanimHatasi = 1, Hata = 2;
    // Dosya şeması sürümü.
    public const int FormatSurumu = 1;
    // Bir bulgunun özete giren en uzun ayrıntı uzunluğu (dosya şişmesin).
    private const int EnUzunDetay = 600;
    // JSON ayarı: okunabilir, Türkçe karakterler olduğu gibi kalsın.
    private static readonly JsonSerializerOptions Js = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Kısa yardım metni.
    public const string YardimMetni = "BulutSoft Security Suite - gözetimsiz tarama\r\n\r\nKullanım:\r\n  BulutSoftSecuritySuite.exe --tara [--cikti <klasör veya dosya.json>]\r\n\r\n  --tara    Bu bilgisayarın salt-okunur denetimini arayüz açmadan çalıştırır.\r\n  --cikti   Klasör verilirse dosya adı MAKINE_yyyyMMdd_HHmmss.json olur (üzerine yazılmaz).\r\n            \".json\" ile biten yol verilirse o dosya yazılır. Verilmezse veri klasöründeki \"ozetler\" kullanılır.\r\n\r\nToplu rapor:\r\n  BulutSoftSecuritySuite.exe --filo <özet klasörü> [--cikti <rapor.html veya klasör>]\r\n  Klasördeki .json özetlerinden tek HTML raporu ve CSV üretir (özet dosyalarına dokunulmaz).\r\n\r\nGizlilik:\r\n  BulutSoftSecuritySuite.exe --gizlilik\r\n  Programın dışarı giden tüm bağlantılarını ve sakladığı verileri listeler.\r\n\r\nÇıkış kodu: 0 = başarılı, 1 = hatalı kullanım, 2 = tarama veya yazma hatası.\r\nNot: Hiçbir ayar değiştirilmez; internete çıkılmaz. Tam sonuç için yönetici olarak çalıştırın.\r\n";

    // Lisans durumunu YAZMADAN okur (taramayı asla engellemez; hata olursa lisans alanı boş kalır). Kalan gün: deneme ve süreli lisansta dolu.
    public static (string? Kod, int? KalanGun) LisansBilgisi(DateTime simdi)
    {
        // Hata olsa da tarama sürsün.
        try { var s = Lisans.Durum(simdi, yaz: false); return (s.Durum.ToString(), s.KalanGun >= 0 ? s.KalanGun : null); } catch { return (null, null); }
    }

    // ---------- Komut satırı ----------

    // Argümanları çözer. --tara yoksa ve --yardim yoksa Tara=false (normal arayüz açılır).
    public static CliIstek ArgCoz(string[] args)
    {
        // Değişkenler.
        bool tara = false, yardim = false; string? cikti = null, filo = null;
        // Her argümanı gez.
        for (int i = 0; i < args.Length; i++)
        {
            // Küçük harfe çevrilmiş argüman.
            string a = args[i].ToLowerInvariant();
            // Toplu rapor: --filo <özet klasörü>.
            if (a == "--filo") { if (i + 1 >= args.Length || args[i + 1].StartsWith("--")) return new(tara, yardim, cikti, "--filo için özet dosyalarının bulunduğu klasör verilmelidir.", null); filo = args[++i].Trim('"'); }
            // Kısa tanımlar.
            else if (a == "--tara") tara = true;
            else if (a == "--yardim" || a == "--help" || a == "-h" || a == "/?") yardim = true;
            // Çıktı yolu: sonraki argüman değer olmalı.
            else if (a == "--cikti") { if (i + 1 >= args.Length || args[i + 1].StartsWith("--")) return new(tara, yardim, null, "--cikti için bir klasör veya dosya yolu verilmelidir."); cikti = args[++i].Trim('"'); }
            // Arayüzün kendi bayrağı ("Kullanıcı Değiştir") tarama komutunu bozmaz.
            else if (a == "--oturum-degisti") { }
            // Tarama isteniyorsa bilinmeyen bayrak hatadır (yazım hatası sessizce yutulmasın).
            else if (a.StartsWith("--")) { if (tara || filo != null || args.Any(x => x.Equals("--tara", StringComparison.OrdinalIgnoreCase) || x.Equals("--filo", StringComparison.OrdinalIgnoreCase))) return new(tara, yardim, cikti, $"Bilinmeyen seçenek: {args[i]}", filo); }
        }
        // Tarama ve toplu rapor aynı anda istenemez.
        if (tara && filo != null) return new(tara, yardim, cikti, "--tara ve --filo birlikte kullanılamaz.", filo);
        // --cikti tek başına anlamsızdır.
        if (cikti != null && !tara && filo == null) return new(false, yardim, cikti, "--cikti yalnızca --tara veya --filo ile kullanılır.", null);
        // Sonuç.
        return new(tara, yardim, cikti, null, filo);
    }

    // ---------- Özet üretimi (saf mantık; test edilebilir) ----------

    // Bulgulardan özet üretir. "Bu kontrol çalıştırılamadı" bulguları listede KALIR (kapsam eksiği bilinsin) ama sayılara ve puana girmez; ayrıca sayılır.
    public static TaramaOzeti Ozetle(IEnumerable<Bulgu> bulgular, string makine, DateTime zamanUtc, bool yonetici, string surum, string? lisans = null, int? lisansKalanGun = null)
    {
        // Önemden aza, sonra modül adına göre sırala.
        var hepsi = bulgular.OrderByDescending(b => b.Onem).ThenBy(b => b.Modul, StringComparer.Ordinal).ToList();
        // Gerçek bulgular (çalışamayan kontroller hariç).
        var liste = hepsi.Where(b => !b.Atlandi).ToList();
        // Özet kayıtları (ayrıntı kısaltılır).
        var ozet = hepsi.Select(b => new OzetBulgu(b.Modul, b.Baslik, b.Onem.ToString(), Kisalt(b.Detay), Kisalt(b.Oneri))).ToList();
        // Mevcut karne hesabı (arayüzdekiyle aynı puan).
        int puan = KarneHesabi.Puan(liste); string seviye = puan >= 80 ? "Genel Durum İyi" : puan >= 50 ? "Dikkat Gerekiyor" : "Acil Müdahale Gerekiyor";
        // Sonuç.
        return new TaramaOzeti(FormatSurumu, makine, zamanUtc, surum, yonetici, puan, seviye, liste.Count(b => b.Onem == Onem.Yuksek), liste.Count(b => b.Onem == Onem.Orta), liste.Count(b => b.Onem == Onem.Dusuk), liste.Count(b => b.Onem == Onem.Bilgi), hepsi.Count - liste.Count, ozet, lisans, lisansKalanGun);
    }

    // Uzun metni kısaltır.
    private static string Kisalt(string? s) { s ??= ""; return s.Length > EnUzunDetay ? s[..EnUzunDetay] + "…" : s; }

    // Makine adından dosya adında sakıncalı karakterleri temizler.
    public static string DosyaAdi(string makine) { var s = new string((makine ?? "").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-'); return s.Length == 0 ? "MAKINE" : s; }

    // Çıktı yolunu belirler: klasörse zaman damgalı ve ÜZERİNE YAZMAYAN ad; dosya yoluysa tam o yol.
    public static string CiktiYolu(string? cikti, string makine, DateTime zamanUtc)
    {
        // Verilmediyse veri klasöründeki "ozetler".
        string yol = string.IsNullOrWhiteSpace(cikti) ? Path.Combine(BulutKlasor.Kok, "ozetler") : cikti;
        // Klasör modu: var olan klasör, sonu ayraçla biten yol ya da ".json" ile BİTMEYEN yol (Windows'ta "C:\x\" tırnak içinde yazılınca
        // son ters bölü kaybolabilir; uzantısız yol dosya değil klasör sayılır, böylece yanlışlıkla uzantısız dosya yazılmaz).
        bool klasor = Directory.Exists(yol) || yol.EndsWith('\\') || yol.EndsWith('/') || string.IsNullOrWhiteSpace(cikti) || !yol.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        // Dosya modu: tam yol.
        if (!klasor) return Path.GetFullPath(yol);
        // Zaman damgalı ad; aynı saniyede ikinci çalıştırmada _2, _3 ... eklenir.
        string tabani = Path.Combine(Path.GetFullPath(yol), DosyaAdi(makine) + "_" + zamanUtc.ToString("yyyyMMdd_HHmmss")), aday = tabani + ".json"; int n = 2;
        while (File.Exists(aday)) aday = tabani + "_" + n++ + ".json";
        return aday;
    }

    // Özeti dosyaya yazar: önce geçici dosya, sonra yerine taşıma (yarım dosya oluşmaz; toplayıcı yarım dosya görmez).
    public static void Yaz(string yol, TaramaOzeti ozet)
    {
        // Klasörü oluştur.
        Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
        // Atomik yaz (BOM'suz UTF-8, benzersiz geçici dosya + yer değiştirme).
        Depo.Yaz(yol, JsonSerializer.Serialize(ozet, Js));
    }

    // ---------- Toplama ve çalıştırma ----------

    // Bir adımı hata yakalayarak çalıştırır; hata olursa özet yine de üretilsin diye bilgi bulgusu ekler.
    private static void Adim(List<Bulgu> tum, string ad, Func<IEnumerable<Bulgu>> is_)
    {
        // Hata kontrolü.
        try { tum.AddRange(is_()); }
        // Çalışamadıysa nedenini not et.
        catch (Exception ex) { tum.Add(new Bulgu("Tarama", $"{ad} adımı çalıştırılamadı", Onem.Bilgi, ex.Message, "Programı yönetici olarak çalıştırmayı deneyin.")); }
    }

    // Standart salt-okunur toplama: arayüzdeki "Bilgisayarımı ve Şirketimi Kontrol Et" ile aynı üç adım; ortak veri dosyalarına YAZMAZ.
    public static List<Bulgu> StandartTopla()
    {
        // Sonuç ve yerel kaynak.
        var tum = new List<Bulgu>(); var kaynak = new GercekYerelKaynak();
        // 1) Güvenlik ayarları, hesaplar, başlangıç öğeleri, paylaşımlar.
        Adim(tum, "Sistem denetimi", () => SistemDenetimi.Hepsi());
        // 2) Ağdan erişilebilen riskli kapılar.
        Adim(tum, "Açık kapı denetimi", () => KolayModEylemleri.KolayKapiBulgulari(kaynak.Dinleyenler()));
        // 3) Hizmetler, güncellemeler, yazılımlar (yalnızca okuma; sonuçlar kaydedilmez).
        Adim(tum, "Yerel denetim", () =>
        {
            // Yanlış alarm listesi yalnızca OKUNUR (kaydedilmez).
            var fp = FpDeposu.Yukle(); var s = YerelDenetimAkisi.Calistir(kaynak, KbCatalog.Yukle(), null, DateTime.UtcNow, fp, NvdIndeks.Yukle(), VulnFeedStore.Yukle());
            // Yanlış alarm işaretlileri ayıkla ve ana tablo biçimine çevir.
            return s.Bulgular.Where(f => f.State != FindingState.FalsePositive).Select(V2Kayit.Donustur).ToList();
        });
        // Sonuç.
        return tum;
    }

    // Taramayı çalıştırıp özeti yazar. Dönüş: (çıkış kodu, yazılan dosya yolu veya hata mesajı). topla/simdi testte değiştirilir.
    public static (int Kod, string Mesaj) Calistir(string? cikti, Func<List<Bulgu>>? topla = null, DateTime? simdiUtc = null)
    {
        // Hata kontrolü: tarama veya yazma bozulursa program çökmesin, kod 2 dönsün.
        try
        {
            // Zaman ve makine.
            var zaman = simdiUtc ?? DateTime.UtcNow; string makine = Environment.MachineName;
            // Topla.
            var bulgular = (topla ?? StandartTopla)();
            // Özet ve yazım.
            var (lisKod, lisGun) = LisansBilgisi(zaman.ToLocalTime()); var ozet = Ozetle(bulgular, makine, zaman, YoneticiMi(), Surum(), lisKod, lisGun); string yol = CiktiYolu(cikti, makine, zaman); Yaz(yol, ozet);
            // Başarı.
            return (Tamam, yol);
        }
        catch (Exception ex) { return (Hata, "Tarama tamamlanamadı: " + ex.Message); }
    }

    // Program sürümü.
    public static string Surum() => UrunBilgisi.Surum;
    // Yönetici olarak mı çalışıyor? (özet, taramanın tam mı kısmi mi olduğunu bilsin)
    private static bool YoneticiMi() { try { using var k = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(k).IsInRole(WindowsBuiltInRole.Administrator); } catch { return false; } }

    // ---------- Program girişi ----------

    // Üst işlemin konsoluna bağlanır (arayüz uygulaması konsolsuz başlar; PowerShell/cmd'de yazı görünsün).
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);

    // Komut satırı tarama girişi. Tarama/yardım isteği değilse -1 döner (normal arayüz açılır); aksi halde çıkış kodunu verir.
    public static int Giris(string[] args)
    {
        // Veri akışı ve gizlilik belgesi (yalnızca ekrana yazar; hiçbir şey değiştirmez).
        if (args.Length == 1 && args[0].Equals("--gizlilik", StringComparison.OrdinalIgnoreCase))
        {
            try { AttachConsole(-1); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "TaramaCalistirici.Giris"); }
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "TaramaCalistirici.Giris"); }
            Console.WriteLine(VeriAkisi.Metin(PilotMod.SaltOkunur));
            return Tamam;
        }
        // İstek.
        var istek = ArgCoz(args);
        // Hiçbiri istenmediyse arayüz.
        if (!istek.Tara && !istek.Yardim && istek.Hata == null && istek.Filo == null) return -1;
        // Konsola bağlan (üst işlem yoksa sessizce geçer).
        try { AttachConsole(-1); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "TaramaCalistirici.Giris"); }
        // Hatalı kullanım.
        if (istek.Hata != null) { Console.Error.WriteLine(istek.Hata); Console.Error.WriteLine(); Console.Error.WriteLine(YardimMetni); return KullanimHatasi; }
        // Toplu rapor (yalnızca okur; rapor dosyalarını yazar).
        if (istek.Filo != null) { var (fk, fm) = FiloRaporu.Calistir(istek.Filo, istek.Cikti); if (fk == Tamam) Console.WriteLine("Filo raporu yazıldı: " + fm); else Console.Error.WriteLine(fm); return fk; }
        // Yardım.
        if (!istek.Tara) { Console.WriteLine(YardimMetni); return Tamam; }
        // Tarama.
        Console.WriteLine("Tarama başlıyor (salt okunur)...");
        var (kod, mesaj) = Calistir(istek.Cikti);
        // Sonuç.
        if (kod == Tamam) Console.WriteLine("Özet yazıldı: " + mesaj); else Console.Error.WriteLine(mesaj);
        return kod;
    }
}
