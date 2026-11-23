// Bu dosya "Olay Zaman Çizgisi"ni çalıştırır: önemli olayları (USB, nöbetçi, izolasyon, tarama bitişi, zincirleme risk, kritik bulgular)
// bilgisayara kalıcı olarak yazar (%AppData%\BulutSoft\olaylar.jsonl). Program kapanıp açılsa da silinmez; bir pencerede aranır, süzülür ve .csv olarak dışa aktarılır.
using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Zaman çizgisindeki tek bir olay.
internal sealed record OlayKaydi(DateTime Zaman, string Tur, string Seviye, string Mesaj);

// Olayları dosyaya yazan ve okuyan yardımcı.
internal static partial class OlayDeposu
{
    // Dosyanın yolu (testte değiştirilebilir).
    public static string Yol { get; set; } = Path.Combine(BrandConfig.RoamingKlasor, "olaylar.jsonl");
    // Aynı anda iki yazma çakışmasın.
    private static readonly object Kilit = new();
    // En fazla bu kadar satır tutulur (fazlası kırpılır).
    private const int EnFazla = 5000;

    // Günlük satırı önemli bir olay mı? Önemliyse türünü ve seviyesini verir.
    public static (string Tur, string Seviye)? Siniflandir(string mesaj)
    {
        // Mesajın başına göre tür belirle.
        string? tur = mesaj switch
        {
            var m when m.StartsWith("USB KALKANI", StringComparison.Ordinal) => "USB",
            var m when m.StartsWith("NÖBETÇİ", StringComparison.Ordinal) => "Nöbetçi",
            var m when m.StartsWith("ACİL İZOLASYON", StringComparison.Ordinal) => "İzolasyon",
            var m when m.StartsWith("ZİNCİRLEME", StringComparison.Ordinal) => "Analiz",
            var m when m.StartsWith("ALTIN PROFİL", StringComparison.Ordinal) => "Referans",
            var m when m.StartsWith("KARŞILAŞTIRMA", StringComparison.Ordinal) || m.Contains("OPERASYON TAMAMLANDI", StringComparison.Ordinal) => "Tarama",
            var m when m.StartsWith("KRİTİK ->", StringComparison.Ordinal) => "Bulgu",
            var m when m.StartsWith("TUZAK", StringComparison.Ordinal) => "Tuzak",
            var m when m.StartsWith("OTURUM", StringComparison.Ordinal) => "Oturum",
            _ => null
        };
        // Önemli değilse yok.
        if (tur == null) return null;
        // Kritik göstergeleri.
        bool kritik = mesaj.Contains("KRİTİK", StringComparison.Ordinal) || mesaj.Contains("KLAVYE", StringComparison.Ordinal) || mesaj.Contains("YENİ KAPI", StringComparison.Ordinal) || mesaj.Contains("PUAN DÜŞTÜ", StringComparison.Ordinal) || mesaj.Contains("ALARM", StringComparison.Ordinal);
        // Uyarı göstergeleri.
        bool uyari = mesaj.Contains("UYARI", StringComparison.Ordinal) || mesaj.Contains("AKTİF", StringComparison.Ordinal) || mesaj.Contains("yeni sorun", StringComparison.Ordinal);
        // Sonuç.
        return (tur, kritik ? "Kritik" : uyari ? "Uyarı" : "Bilgi");
    }

    // Bir olayı dosyaya ekler (hata olursa sessizce geçer; günlük asla programı bozmasın).
    public static void Ekle(string tur, string seviye, string mesaj)
    {
        // Yazma bozulabilir.
        try
        {
            // Çakışmayı önle.
            lock (Kilit)
            {
                // Klasör yoksa oluştur.
                Directory.CreateDirectory(Path.GetDirectoryName(Yol)!);
                // Dosya çok büyüdüyse ESKİSİ SİLİNMEZ/KIRPILMAZ: arşiv klasörüne taşınır, yeni dosya zincire arşivin son özetiyle devam eder.
                try { var fi = new FileInfo(Yol); if (fi.Exists && fi.Length > 2_000_000) Arsivle(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "OlayDeposu.Ekle"); }
                // Tek satırlık JSON ekle: satır sonuna önceki satırın özetine bağlı HMAC (zincir) konur; araya girme, silme ve sıra değiştirme bozulma olarak görünür.
                var kayit = new OlayKaydi(DateTime.Now, tur, seviye, mesaj);
                bool hizmeteGitti = false; try { hizmeteGitti = HizmetYazici?.Invoke(tur, seviye, mesaj) == true; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "OlayDeposu.Ekle"); }
                if (!hizmeteGitti) EkleZincirli(kayit);
                // Windows Olay Günlüğü'ne ayna (kaynak kayıtlıysa): yönetici olmayan kullanıcı oradan silemez/değiştiremez.
                if (!hizmeteGitti) Ayna(kayit);
            }
        }
        // Yazılamadıysa geç.
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "OlayDeposu.Ekle"); }
        // Kritik olay ve bildirim kanalı açıksa arka planda haber ver (kapalıysa hiçbir şey olmaz).
        if (seviye == "Kritik") { try { Webhook.KritikBildir(mesaj); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "OlayDeposu.Ekle"); } }
        // "Uyarı" seviyesi yalnızca kullanıcı bildirim eşiğini "Uyarı" yapmışsa gider (yönetici kendisi filtreler).
        else if (seviye == "Uyarı") { try { WebhookYoneticisi.Olay(tur, seviye, mesaj); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "OlayDeposu.Ekle"); } }
        // Dinleyenlere (örn. arayüzdeki bildirim zili) yeni olay eklendiğini haber ver; dinleyici hatası günlüğü bozmasın.
        try { Eklendi?.Invoke(seviye); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "OlayDeposu.Eklendi"); }
    }

    // Yeni bir olay eklendiğinde (herhangi bir iş parçacığında) tetiklenir; parametre olayın seviyesidir ("Bilgi", "Uyarı", "Kritik").
    public static event Action<string>? Eklendi;

    // ---------------- Bütünlük: zincirli HMAC, arşiv ve Windows Olay Günlüğü aynası ----------------
    // Her satırın sonuna "Z" alanı konur: HMAC-SHA256(anahtar, önceki satırın Z'si | satırın kendisi). Anahtar DPAPI ile (bu Windows kullanıcısına bağlı) saklanır.
    // DÜRÜST SINIR: aynı Windows kullanıcısı olarak çalışan zararlı anahtarı da okuyabilir; bu yüzden olaylar ayrıca Windows Olay Günlüğü'ne yansıtılır
    // (kaynak "BulutSoft" kurulumda yönetici tarafından kaydedilir; günlüğü yalnızca yönetici temizleyebilir ve temizlenmesi ayrıca kaydedilir).
    private static string? _zincirYol; private static string _sonZ = ""; private static int _zincirSayi; private static byte[]? _anahtar; private static string? _anahtarYol; private static bool _aynaYok;
    // TEK YAZAR: koruma hizmeti çalışıyorsa olaylar yerel dosyaya DEĞİL hizmete iletilir (eşzamanlı yazma/kilitleme sorunu ve çift kayıt olmasın). Kanca Program'da atanır;
    // atanmamışsa ya da hizmet yanıt vermezse olay yerel dosyaya yazılır (kayıp olmaz). Webhook bildirimleri yine yerelde (kullanıcının ayarlarıyla) gönderilir.
    public static Func<string, string, string, bool>? HizmetYazici { get; set; }
    // Hizmetin günlük dosyası (varsa); Oku() yerel dosyayla birlikte bunu da okur.
    public static Func<string?>? HizmetOkumaYolu { get; set; }
    // Windows Olay Günlüğü aynası açık mı? (testlerde kapatılır)
    public static bool AynaAktif { get; set; } = true;
    private static readonly byte[] AnahtarEk = Encoding.UTF8.GetBytes("bs-olay-v1");

    // Anahtarı okur; yoksa (olustur: true ise) üretir.
    private static byte[]? AnahtarAl(bool olustur)
    {
        if (_anahtarYol == Yol && _anahtar != null) return _anahtar;
        string ky = Yol + ".key"; byte[]? k = null;
        try { if (File.Exists(ky)) k = ProtectedData.Unprotect(File.ReadAllBytes(ky), AnahtarEk, DataProtectionScope.CurrentUser); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "OlayDeposu.AnahtarAl"); }
        if (k == null && olustur) { k = RandomNumberGenerator.GetBytes(32); try { Depo.YazBayt(ky, ProtectedData.Protect(k, AnahtarEk, DataProtectionScope.CurrentUser)); } catch (Exception hk7) { HataKayit.Sessiz(hk7, "OlayDeposu.AnahtarAl"); } }
        if (k != null) { _anahtar = k; _anahtarYol = Yol; }
        return k;
    }
    private static string Hmac(byte[] anahtar, string metin) => Convert.ToHexString(HMACSHA256.HashData(anahtar, Encoding.UTF8.GetBytes(metin)));
    private static bool Esit(string a, string b) => a.Length == b.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    // Satırdaki zincir değeri (yoksa null).
    private static string? ZOku(string satir) { int i = satir.LastIndexOf(",\"Z\":\"", StringComparison.Ordinal); if (i < 0) return null; int b = i + 6, e = satir.IndexOf('"', b); return e > b ? satir[b..e] : null; }

    // Zincirin son halkasını dosyadan bir kez yükler (yol değişince yeniden).
    private static void ZincirHazirla()
    {
        if (_zincirYol == Yol) return; _zincirYol = Yol; _sonZ = ""; _zincirSayi = 0;
        try { if (File.Exists(Yol)) foreach (var s in File.ReadLines(Yol)) { var z = ZOku(s); if (z != null) { _sonZ = z; _zincirSayi++; } } } catch (Exception hk8) { HataKayit.Sessiz(hk8, "OlayDeposu.ZincirHazirla"); }
    }

    // Satırı zincire ekleyerek yazar (kilit içinde çağrılır).
    private static void EkleZincirli(OlayKaydi kayit)
    {
        ZincirHazirla(); var anahtar = AnahtarAl(true)!; string govde = JsonSerializer.Serialize(kayit);
        string z = Hmac(anahtar, _sonZ + "|" + govde); string satir = govde[..^1] + ",\"Z\":\"" + z + "\"}";
        File.AppendAllText(Yol, satir + "\n", Encoding.UTF8); _sonZ = z; _zincirSayi++;
        // Baş mührü: (sayı | son halka | HMAC). Dosyanın sonunun kesilmesi bununla anlaşılır.
        try { string ic = _zincirSayi + "|" + _sonZ; Depo.YazBayt(Yol + ".head", Encoding.UTF8.GetBytes(ic + "|" + Hmac(anahtar, ic))); } catch (Exception hk9) { HataKayit.Sessiz(hk9, "OlayDeposu.EkleZincirli"); }
    }

    // Dosya büyüyünce eskisini SİLMEDEN arşiv klasörüne taşır; yeni dosyanın ilk satırı arşivin son özetini taşır (zincir kopmaz). En çok 200 arşiv tutulur.
    private static void Arsivle()
    {
        ZincirHazirla(); string dizin = Path.Combine(Path.GetDirectoryName(Yol)!, "olay_arsiv"); Directory.CreateDirectory(dizin);
        string hedef = Path.Combine(dizin, Path.GetFileNameWithoutExtension(Yol) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".jsonl"); string onceki = _sonZ;
        // Aynı milisaniyede ikinci arşiv oluşursa ad çakışmasın (aksi hâlde eski arşiv ezilirdi).
        if (File.Exists(hedef)) hedef = Path.Combine(dizin, Path.GetFileNameWithoutExtension(hedef) + "_" + Guid.NewGuid().ToString("N")[..6] + ".jsonl");
        File.Move(Yol, hedef, false); try { if (File.Exists(Yol + ".head")) File.Move(Yol + ".head", hedef + ".head", true); } catch (Exception hk10) { HataKayit.Sessiz(hk10, "OlayDeposu.Arsivle"); }
        _sonZ = ""; _zincirSayi = 0;
        EkleZincirli(new OlayKaydi(DateTime.Now, "Sistem", "Bilgi", $"ZİNCİR DEVAM: önceki kayıt dosyası arşive taşındı ({Path.GetFileName(hedef)}); arşivin son özeti {onceki}"));
        try { var eski = new DirectoryInfo(dizin).GetFiles("*.jsonl").OrderBy(f => f.CreationTimeUtc).ToList(); for (int i = 0; i < eski.Count - 200; i++) { try { File.Delete(eski[i].FullName); File.Delete(eski[i].FullName + ".head"); } catch (Exception hk11) { HataKayit.Sessiz(hk11, "OlayDeposu.Arsivle"); } } } catch (Exception hk12) { HataKayit.Sessiz(hk12, "OlayDeposu.Arsivle"); }
    }

    // Günlüğü şimdi arşive taşır (kırpma yok; zincir arşivin son özetiyle devam eder). Dosya yoksa bir şey yapmaz.
    public static bool ArsivleSimdi() { try { lock (Kilit) { if (!File.Exists(Yol)) return false; Arsivle(); return true; } } catch { return false; } }

    // Olayı Windows Olay Günlüğü'ne (Uygulama, kaynak "BulutSoft") yansıtır. Kaynak kayıtlı değilse sessizce vazgeçer (kayıt kurulumda yapılır).
    private static void Ayna(OlayKaydi k)
    {
        if (!AynaAktif || _aynaYok) return;
        try { var tip = k.Seviye == "Kritik" ? EventLogEntryType.Error : k.Seviye == "Uyarı" ? EventLogEntryType.Warning : EventLogEntryType.Information; string m = $"[{k.Tur}/{k.Seviye}] {k.Mesaj}"; if (m.Length > 2000) m = m[..2000]; EventLog.WriteEntry("BulutSoft", m, tip, k.Seviye == "Kritik" ? 4002 : k.Seviye == "Uyarı" ? 4001 : 4000); }
        catch { _aynaYok = true; }
    }

    // Zincir denetimi sonucu.
    internal sealed record ZincirSonucu(bool Saglam, int Satir, int Zincirli, int IlkBozukSatir, string Not);

    // Kayıt dosyasının zincirini ve baş mührünü doğrular. Zincirsiz ESKİ satırlar (bu özellikten önce yazılanlar) doğrulanamaz, ama hata sayılmaz; zincir başladıktan sonra zincirsiz satır bozulmadır.
    // onbellekli: durum yoklaması gibi SIK çağrılar için; dosya (boyut + son yazım) ve baş mührü değişmediyse önceki sonuç döner.
    // DÜRÜST SINIR: zaman damgası geri alınmış, aynı boyutlu bir değişiklik önbellekten görülemez; bu yüzden "bütünlük doğrula" eylemleri onbellekli:false (tam tarama) kullanır.
    public static ZincirSonucu Dogrula(bool onbellekli = false)
    {
        if (!onbellekli) return DogrulaTam();
        try
        {
            var fi = new FileInfo(Yol); var bi = new FileInfo(Yol + ".head");
            string anahtar = fi.Exists ? $"{fi.Length}|{fi.LastWriteTimeUtc.Ticks}|{(bi.Exists ? bi.Length + "|" + bi.LastWriteTimeUtc.Ticks : "-")}|{Yol}" : "yok|" + Yol;
            lock (DogrulaOnbellekKilit) { if (_dogrulaAnahtar == anahtar && _dogrulaSonuc != null) return _dogrulaSonuc; }
            var s = DogrulaTam();
            lock (DogrulaOnbellekKilit) { _dogrulaAnahtar = anahtar; _dogrulaSonuc = s; }
            return s;
        }
        catch (Exception hk) { HataKayit.Sessiz(hk, "OlayDeposu.Dogrula"); return DogrulaTam(); }
    }
    private static readonly object DogrulaOnbellekKilit = new(); private static string? _dogrulaAnahtar; private static ZincirSonucu? _dogrulaSonuc;

    // Tüm zinciri baştan doğrular (tam tarama).
    private static ZincirSonucu DogrulaTam()
    {
        try
        {
            lock (Kilit)
            {
                if (!File.Exists(Yol)) return new(true, 0, 0, -1, "");
                var anahtar = AnahtarAl(false); int n = 0, zin = 0, ilk = -1; string onceki = "";
                foreach (var s in File.ReadLines(Yol))
                {
                    n++; var z = ZOku(s);
                    if (z == null) { if (zin > 0 && ilk < 0) ilk = n; continue; }
                    if (anahtar == null) { if (ilk < 0) ilk = n; zin++; onceki = z; continue; }
                    string govde = s[..s.LastIndexOf(",\"Z\":\"", StringComparison.Ordinal)] + "}";
                    if (!Esit(Hmac(anahtar, onceki + "|" + govde), z) && ilk < 0) ilk = n;
                    onceki = z; zin++;
                }
                string not = ""; bool kuyruk = false;
                string basYol = Yol + ".head";
                if (anahtar != null && File.Exists(basYol))
                {
                    var p = File.ReadAllText(basYol).Split('|');
                    if (p.Length == 3 && Esit(Hmac(anahtar, p[0] + "|" + p[1]), p[2])) { if (int.TryParse(p[0], out int sayi) && (sayi > zin || (sayi == zin && !Esit(p[1], onceki)))) { kuyruk = true; not = "Kayıt dosyasının sonu silinmiş ya da değiştirilmiş."; } }
                    else { kuyruk = true; not = "Baş mührü bozuk."; }
                }
                else if (zin > 0) { kuyruk = anahtar != null; not = anahtar == null ? "Zincir anahtarı bulunamadı; satırlar doğrulanamıyor." : "Baş mührü silinmiş."; }
                if (ilk >= 0 && not.Length == 0) not = $"{ilk}. satırda zincir bozuk.";
                return new(ilk < 0 && !kuyruk, n, zin, ilk, not);
            }
        }
        catch (Exception ex) { return new(false, 0, 0, -1, "Doğrulanamadı: " + ex.Message); }
    }

    // TAM TARAMA: tüm olayları en yeniden eskiye okur (hepsini ayrıştırır). Sık çağrılar için SonOlaylar/SonUyariKritik (artımlı) kullanılır.
    public static List<OlayKaydi> Oku()
    {
        // Sonuç listesi.
        var l = new List<OlayKaydi>();
        // Okuma bozulabilir.
        try
        {
            // Çakışmayı önle ve satırları oku (yerel dosya yoksa boş; hizmetin günlüğü varsa onu da).
            string[] satirlar = Array.Empty<string>(); lock (Kilit) { if (File.Exists(Yol)) satirlar = File.ReadAllLines(Yol, Encoding.UTF8); }
            try { var hy = HizmetOkumaYolu?.Invoke(); if (!string.IsNullOrEmpty(hy) && File.Exists(hy) && !string.Equals(Path.GetFullPath(hy), Path.GetFullPath(Yol), StringComparison.OrdinalIgnoreCase)) { using var fs = new FileStream(hy, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var sr = new StreamReader(fs, Encoding.UTF8); var ek = new List<string>(); string? sat; while ((sat = sr.ReadLine()) != null) ek.Add(sat); satirlar = satirlar.Concat(ek).ToArray(); } } catch (Exception hk13) { HataKayit.Sessiz(hk13, "OlayDeposu.Oku"); }
            // Her satırı çöz.
            foreach (var s in satirlar) { try { var k = JsonSerializer.Deserialize<OlayKaydi>(s); if (k != null) l.Add(k); } catch (Exception hk14) { HataKayit.Sessiz(hk14, "OlayDeposu.Oku"); } }
        }
        // Okunamazsa elimizdekini ver.
        catch (Exception hk15) { HataKayit.Sessiz(hk15, "OlayDeposu.Oku"); }
        // En yeni üstte.
        return l.OrderByDescending(k => k.Zaman).ToList();
    }

    // Olayları .csv dosyasına yazar (Excel Türkçe karakterleri doğru açsın diye BOM'lu UTF-8, ayraç noktalı virgül).
    public static void CsvYaz(string yol, IEnumerable<OlayKaydi> olaylar)
    {
        // Metin kutusu.
        var sb = new StringBuilder("Zaman;Tür;Seviye;Mesaj\r\n");
        // Her olay (formül enjeksiyonuna karşı = + - @ ile başlayan metne ' eklenir).
        foreach (var o in olaylar) sb.Append(CultureInfo.InvariantCulture, $"{o.Zaman:dd.MM.yyyy HH:mm:ss};{o.Tur};{o.Seviye};\"{Guvenli(o.Mesaj)}\"\r\n");
        // Dosyaya yaz.
        File.WriteAllText(yol, sb.ToString(), new UTF8Encoding(true));
    }

    // CSV hücresini güvenli hale getirir.
    private static string Guvenli(string s) { s = s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " "); return s.Length > 0 && "=+-@".Contains(s[0]) ? "'" + s : s; }
}
