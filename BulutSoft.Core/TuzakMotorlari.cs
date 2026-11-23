// Bu dosya "Tuzaklar (Erken Uyarı)" sistemini içerir. Tuzak, saldırganı veya zararlı yazılımı KENDİ İSTEMEDEN ele veren sahte bir yemdir:
//  1) Honeypot kapı: gerçek bir hizmet değil, sahte bir "SSH" kapısı dinler. Kim bağlanırsa kim olduğu ve ne yazdığı kaydedilir.
//  2) Canary (kanarya) dosyalar: "parolalar.txt" gibi cazip adlı SAHTE dosyalar. Biri bunları değiştirir, siler veya adını değiştirirse alarm çalar.
//  3) Fidye yazılımı alarmı: belgeler klasörlerinde kısa sürede çok sayıda dosya değişirse veya fidye uzantıları görülürse alarm çalar.
// Üçü de YALNIZCA izler ve uyarır; hiçbir şeyi engellemez, internete çıkmaz. Honeypot varsayılan olarak sadece bu bilgisayarın içinde (127.0.0.1) dinler.
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// ==================== 1) HONEYPOT KAPI ====================

// Sahte SSH kapısı: bağlananı kaydeder, hiçbir gerçek hizmet sunmaz.
internal sealed class HoneypotKapi : IDisposable
{
    // Açık dinleyiciler (her adres için bir tane).
    private readonly List<TcpListener> _dinleyiciler = new();
    // Dinlemeyi durduran şalter.
    private CancellationTokenSource? _iptal;
    // Aynı kaynaktan peş peşe gelen bağlantıların son zamanı (günlük taşmasın).
    private readonly Dictionary<string, DateTime> _son = new();
    // Dinleme açık mı?
    public bool Aktif => _dinleyiciler.Count > 0;
    // Dinlenen adresler (ekranda göstermek için).
    public string Dinlenen { get; private set; } = "";
    // Biri bağlandığında tetiklenir: kaynak adres, hedef kapı, ilk gönderdiği veri.
    public event Action<string, int, string>? Baglandi;
    // Sahte karşılama yazıları: gerçek bir Linux SSH sunucusu gibi görünür. Her başlatmada bunlardan biri rastgele seçilir (sabit parmak izi saldırgana "bu bir tuzak" demesin).
    private static readonly string[] Karsilamalar = { "SSH-2.0-OpenSSH_8.9p1 Ubuntu-3ubuntu0.1", "SSH-2.0-OpenSSH_8.2p1 Ubuntu-4ubuntu0.5", "SSH-2.0-OpenSSH_9.2p1 Debian-2+deb12u2", "SSH-2.0-OpenSSH_7.9p1 Debian-10+deb10u2", "SSH-2.0-OpenSSH_9.6p1 Ubuntu-3ubuntu13", "SSH-2.0-OpenSSH_8.4p1 Debian-5+deb11u3" };
    private byte[] _karsilama = Encoding.ASCII.GetBytes(Karsilamalar[0] + "\r\n");
    // Aynı anda en çok bu kadar bağlantı işlenir (soket seli belleği/iş parçacığını tüketmesin); fazlası hemen kapatılır.
    private const int EnFazlaEszamanli = 64; private readonly SemaphoreSlim _yer = new(EnFazlaEszamanli, EnFazlaEszamanli);
    // Kaynak sözlüğünün en çok tutacağı kayıt (eskiler süreyle, taşarsa tamamen temizlenir).
    private const int KaynakSiniri = 2000;

    // Kullanılabilecek özel (lab) ağ adreslerini bulur: bu bilgisayarın özel IPv4 adresleri.
    public static List<IPAddress> LabAdresleri()
    {
        // Sonuç listesi.
        var l = new List<IPAddress>();
        // Ağ kartları okunamayabilir.
        try
        {
            // Açık kartların IPv4 adresleri.
            foreach (var k in NetworkInterface.GetAllNetworkInterfaces().Where(k => k.OperationalStatus == OperationalStatus.Up && k.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                foreach (var a in k.GetIPProperties().UnicastAddresses.Select(u => u.Address).Where(a => a.AddressFamily == AddressFamily.InterNetwork && LabKapsam.IpLabdaMi(a) && !IPAddress.IsLoopback(a) && !a.ToString().StartsWith("169.254", StringComparison.Ordinal)))
                    l.Add(a);
        }
        // Okunamazsa elimizdekini ver.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "TuzakMotorlari.LabAdresleri"); }
        // Sonuç.
        return l;
    }

    // Dinlemeyi başlatır. Hata varsa açıklama yazısı, sorun yoksa null döner.
    public string? Baslat(int port, bool labAgi)
    {
        // Zaten açıksa bir şey yapma.
        if (Aktif) return null;
        // Düşük numaralı (sistem) kapılar yasak: gerçek hizmetlerle karışmasın.
        if (port < 1024 || port > 65535) return "Kapı numarası 1024 ile 65535 arasında olmalı (düşük numaralar gerçek hizmetler içindir).";
        // Dinlenecek adresler: her zaman bu bilgisayarın içi; istenirse lab ağı adresleri de.
        var adresler = new List<IPAddress> { IPAddress.Loopback };
        // Lab ağı seçildiyse özel adresleri ekle.
        if (labAgi) adresler.AddRange(LabAdresleri());
        // Dinleyicileri aç.
        try
        {
            // Her adres için bir dinleyici.
            foreach (var a in adresler) { var l = new TcpListener(a, port); l.Start(); _dinleyiciler.Add(l); }
        }
        // Kapı başka bir program tarafından kullanılıyor olabilir.
        catch (Exception ex) { Durdur(); return $"Kapı {port} dinlenemedi (başka bir program kullanıyor olabilir): {ex.Message}"; }
        // Bu oturum için rastgele bir karşılama yazısı seç.
        _karsilama = Encoding.ASCII.GetBytes(Karsilamalar[System.Security.Cryptography.RandomNumberGenerator.GetInt32(Karsilamalar.Length)] + "\r\n");
        // Kabul döngülerini başlat.
        _iptal = new CancellationTokenSource(); var jeton = _iptal.Token;
        // Her dinleyici için arka plan görevi.
        foreach (var l in _dinleyiciler.ToList()) _ = Task.Run(() => KabulEt(l, jeton));
        // Ekranda gösterilecek yazı.
        Dinlenen = string.Join(", ", adresler.Select(a => $"{a}:{port}"));
        // Başarılı.
        return null;
    }

    // Gelen bağlantıları sürekli kabul eder.
    private async Task KabulEt(TcpListener l, CancellationToken ct)
    {
        // Durdurulana kadar dön.
        while (!ct.IsCancellationRequested)
        {
            // Hata olsa da döngü ölmesin.
            try
            {
                var c = await l.AcceptTcpClientAsync(ct);
                // Sınır doluysa bağlantıyı hiç işlemeden kapat (soket seli koruması).
                if (!_yer.Wait(0, ct)) { try { c.Dispose(); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "TuzakMotorlari.KabulEt"); } continue; }
                _ = Task.Run(async () => { try { await Isle(c, ct); } finally { try { _yer.Release(); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "TuzakMotorlari.KabulEt"); } } }, CancellationToken.None);   // iptal belirteci bilerek verilmez: görev hiç başlamazsa alınmış yuva (_yer) serbest bırakılamazdı
            }
            // Durduruldu.
            catch (OperationCanceledException) { break; }
            // Dinleyici kapandı.
            catch (ObjectDisposedException) { break; }
            // Başka bir sorun: kısa bekle, devam et.
            catch { try { await Task.Delay(200, ct); } catch { break; } }
        }
    }

    // Tek bir bağlantıyı karşılar, ilk verisini okur, kaydeder ve kapatır.
    private async Task Isle(TcpClient c, CancellationToken ct)
    {
        // İşi bitince bağlantıyı kapat.
        using (c)
        {
            // Bilgileri al.
            var uzak = c.Client.RemoteEndPoint as IPEndPoint; int port = (c.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0; string veri = "";
            // Ağ hatası olabilir.
            try
            {
                // Akışı al ve sahte karşılamayı gönder.
                var ns = c.GetStream(); await ns.WriteAsync(_karsilama, ct);
                // En fazla 3 saniye cevap bekle.
                using var zaman = CancellationTokenSource.CreateLinkedTokenSource(ct); zaman.CancelAfter(3000);
                // İlk 256 baytı oku.
                var tampon = new byte[256]; int n = await ns.ReadAsync(tampon, zaman.Token);
                // Yazdırılabilir hale getir.
                veri = Temizle(tampon, n);
            }
            // Cevap vermediyse veya koptuysa boş bırak.
            catch (Exception hk4) { HataKayit.Sessiz(hk4, "TuzakMotorlari.Isle"); }
            // Kaynak adres.
            string kaynak = uzak?.Address.ToString() ?? "?";
            // Aynı kaynaktan 2 saniye içinde tekrar bildirme.
            lock (_son)
            {
                var simdi = DateTime.Now;
                // Sözlük sınırsız büyümesin: sınıra yaklaşınca 60 sn'den eski kayıtlar atılır; hâlâ doluysa tamamen sıfırlanır.
                if (_son.Count >= KaynakSiniri) { foreach (var k in _son.Where(x => (simdi - x.Value).TotalSeconds > 60).Select(x => x.Key).ToList()) _son.Remove(k); if (_son.Count >= KaynakSiniri) _son.Clear(); }
                if (_son.TryGetValue(kaynak, out var t) && (simdi - t).TotalSeconds < 2) return; _son[kaynak] = simdi;
            }
            // Olayı bildir.
            try { Baglandi?.Invoke($"{kaynak}:{uzak?.Port}", port, veri); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "TuzakMotorlari.Isle"); }
        }
    }

    // Gelen baytları yazdırılabilir (ASCII) metne çevirir; tehlikeli/okunamaz karakterler nokta olur, en fazla 80 karakter.
    public static string Temizle(byte[] b, int n) => new string(b.Take(Math.Min(n, 80)).Select(x => x >= 32 && x < 127 ? (char)x : '.').ToArray());

    // Dinlemeyi durdurur.
    public void Durdur()
    {
        // Şalteri kapat.
        try { _iptal?.Cancel(); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "TuzakMotorlari.Durdur"); }
        // Dinleyicileri kapat.
        foreach (var l in _dinleyiciler) { try { l.Stop(); } catch (Exception hk7) { HataKayit.Sessiz(hk7, "TuzakMotorlari.Durdur"); } }
        // Listeyi boşalt.
        _dinleyiciler.Clear(); Dinlenen = "";
    }

    // Kaynakları bırakır.
    public void Dispose() { Durdur(); _iptal?.Dispose(); }
}

// ==================== 2) CANARY (KANARYA) DOSYALAR ====================

// Sahte cazip dosyalar: dokunulursa alarm verir.
internal sealed class CanaryDosyalari : IDisposable
{
    // Sahte dosyaların ilk satırındaki işaret: yalnızca bu işareti taşıyan dosyalar silinir (kullanıcının gerçek dosyasına asla dokunulmaz).
    public const string Isaret = "BULUTSOFT-TUZAK-DOSYASI";
    // Tuzak klasörü: Belgeler içinde cazip adlı bir klasör (testte değiştirilebilir).
    public static string Klasor { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eski_Parolalar_YEDEK");
    // Sahte dosya adları.
    private static readonly string[] Adlar = { "parolalar.txt", "banka_bilgileri.txt", "ozel_anahtar_yedek.txt" };
    // Klasörü izleyen sistem bileşeni.
    private FileSystemWatcher? _izleyici;
    // Aynı dosyadan peş peşe gelen olayları tek alarma indirmek için.
    private readonly Dictionary<string, DateTime> _son = new();
    // Oluşturma anı (kendi yazdığımız dosyalar alarm vermesin).
    private DateTime _olusturma = DateTime.MinValue;
    // İzleme açık mı?
    public bool Aktif => _izleyici != null;
    // Biri tuzağa dokununca tetiklenir: ne oldu, hangi dosya.
    public event Action<string, string>? Tetiklendi;

    // Sahte dosyaları oluşturur (yoksa) ve izlemeyi başlatır. Oluşturulan dosya adlarını verir.
    public List<string> Baslat()
    {
        // Klasörü oluştur.
        Directory.CreateDirectory(Klasor);
        // Oluşturma anını kaydet.
        _olusturma = DateTime.Now;
        // Her dosya için.
        foreach (var ad in Adlar)
        {
            // Dosya yolu.
            var yol = Path.Combine(Klasor, ad);
            // Zaten varsa dokunma.
            if (File.Exists(yol)) continue;
            // İçerik: açıkça sahte olduğunu söyleyen işaret + cazip görünen ama anlamsız satırlar.
            File.WriteAllText(yol, Isaret + " — Bu dosya BulutSoft tarafından konan SAHTE bir tuzaktır; gerçek bilgi içermez.\r\n\r\nsite: ornek.com\r\nkullanici: tuzak\r\nparola: TUZAK-123\r\n");
        }
        // İzleyiciyi kur.
        _izleyici = new FileSystemWatcher(Klasor) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.LastAccess | NotifyFilters.DirectoryName, EnableRaisingEvents = false };
        // Olayları bağla.
        _izleyici.Changed += (_, e) => Olay("değiştirildi/açıldı", e.Name); _izleyici.Deleted += (_, e) => Olay("silindi", e.Name); _izleyici.Created += (_, e) => Olay("yeni dosya eklendi", e.Name); _izleyici.Renamed += (_, e) => Olay($"adı değiştirildi ({e.OldName} → {e.Name})", e.Name);
        // İzlemeyi aç.
        _izleyici.EnableRaisingEvents = true;
        // Dosya adlarını ver.
        return Adlar.ToList();
    }

    // Bir olay geldi: kendi oluşturduklarımızı ve tekrarları ele, gerçekse bildir.
    private void Olay(string ne, string? dosya)
    {
        // Oluşturduktan sonraki 3 saniye bizim yazdıklarımızdır.
        if ((DateTime.Now - _olusturma).TotalSeconds < 3) return;
        // Aynı dosyadan 3 saniye içinde tekrar bildirme.
        lock (_son) { if (_son.TryGetValue(dosya ?? "", out var t) && (DateTime.Now - t).TotalSeconds < 3) return; _son[dosya ?? ""] = DateTime.Now; }
        // Bildir.
        try { Tetiklendi?.Invoke(ne, dosya ?? "?"); } catch (Exception hk8) { HataKayit.Sessiz(hk8, "TuzakMotorlari.Olay"); }
    }

    // İzlemeyi durdurur (dosyalar yerinde kalır).
    public void Durdur() { try { _izleyici?.Dispose(); } catch (Exception hk9) { HataKayit.Sessiz(hk9, "TuzakMotorlari.Durdur"); } _izleyici = null; }

    // İzlemeyi durdurur ve yalnızca BİZİM koyduğumuz (işaretli) sahte dosyaları siler. Silinen dosya sayısını verir.
    public int Kaldir()
    {
        // Önce izlemeyi kapat (silme alarm vermesin).
        Durdur();
        // Silinen sayısı.
        int n = 0;
        // Klasör yoksa yapılacak bir şey yok.
        if (!Directory.Exists(Klasor)) return 0;
        // Her dosyayı kontrol et.
        foreach (var yol in Directory.GetFiles(Klasor))
            // Okunamazsa geç.
            try
            {
                // İlk satırı oku; işaretimiz yoksa DOKUNMA.
                string ilk = ""; using (var r = new StreamReader(yol)) ilk = r.ReadLine() ?? "";
                // İşaretliyse sil.
                if (ilk.StartsWith(Isaret, StringComparison.Ordinal)) { File.Delete(yol); n++; }
            }
            catch (Exception hk10) { HataKayit.Sessiz(hk10, "TuzakMotorlari.Kaldir"); }
        // Klasör boş kaldıysa onu da sil.
        try { if (!Directory.EnumerateFileSystemEntries(Klasor).Any()) Directory.Delete(Klasor); } catch (Exception hk11) { HataKayit.Sessiz(hk11, "TuzakMotorlari.Kaldir"); }
        // Sayıyı ver.
        return n;
    }

    // Kaynakları bırakır (dosyalar silinmez).
    public void Dispose() => Durdur();
}

// ==================== 3) FİDYE YAZILIMI ALARMI ====================

// Belgeler klasörlerinde alışılmadık toplu değişimi yakalar.
internal sealed class FidyeIzleyici : IDisposable
{
    // Fidye yazılımlarının eklediği bilinen uzantılar.
    private static readonly string[] SupheliUzanti = { ".locked", ".encrypted", ".crypt", ".crypto", ".enc", ".wncry", ".wnry", ".ryk", ".lockbit", ".conti", ".hive", ".blackcat", ".djvu", ".cerber", ".zepto", ".petya", ".ransom", ".pay", ".pays", ".aes256" };
    // Görmezden gelinen geçici dosya uzantıları (normal programlar bunları çok üretir).
    private static readonly string[] Gurultu = { ".tmp", ".crdownload", ".part", ".partial", ".temp", ".lnk", ".log" };
    // Son 10 saniyedeki değişiklikler: zaman, dosya yolu, şüpheli uzantı mı.
    private readonly ConcurrentQueue<(DateTime Zaman, string Yol, bool Supheli)> _olaylar = new();
    // İzleyiciler (her klasör için bir tane).
    private readonly List<FileSystemWatcher> _izleyiciler = new();
    // Son alarmın zamanı (1 dakikada bir alarm).
    private DateTime _sonAlarm = DateTime.MinValue;
    // İzleme açık mı?
    public bool Aktif => _izleyiciler.Count > 0;
    // İzlenen klasörler (ekranda göstermek için).
    public List<string> Klasorler { get; } = new();
    // Eşik: 10 saniyede kaç FARKLI dosya değişirse alarm (testte değiştirilebilir).
    public int Esik { get; set; } = 40;
    // Şüpheli uzantı eşiği.
    public int UzantiEsik { get; set; } = 5;
    // Alarm tetiklenince çağrılır: neden ve sayı.
    public event Action<string, int>? Supheli;

    // Varsayılan izlenen klasörler: Belgeler, Masaüstü, Resimler.
    public static List<string> VarsayilanKlasorler()
    {
        // Kullanıcı verisi: Belgeler, Masaüstü, Resimler, Videolar, Müzik, İndirilenler ve OneDrive kökleri.
        var l = new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.MyMusic }.Select(Environment.GetFolderPath).ToList();
        string profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); if (profil.Length > 0) l.Add(Path.Combine(profil, "Downloads"));
        foreach (var ev in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" }) { var v = Environment.GetEnvironmentVariable(ev); if (!string.IsNullOrWhiteSpace(v)) l.Add(v); }
        // Sistem sürücüsü dışındaki sabit diskler ve eşlenmiş AĞ PAYLAŞIMLARI (fidye yazılımı bunları da şifreler). Yalnızca hazır sürücüler.
        try { string sistem = Path.GetPathRoot(Environment.SystemDirectory) ?? ""; foreach (var d in DriveInfo.GetDrives()) if (d.IsReady && (d.DriveType == DriveType.Network || (d.DriveType == DriveType.Fixed && !string.Equals(d.RootDirectory.FullName, sistem, StringComparison.OrdinalIgnoreCase)))) l.Add(d.RootDirectory.FullName); } catch (Exception hk12) { HataKayit.Sessiz(hk12, "TuzakMotorlari.VarsayilanKlasorler"); }
        var var_ = l.Where(x => !string.IsNullOrEmpty(x) && Directory.Exists(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Başka bir izlenen klasörün içinde kalanlar ayrıca izlenmez (aynı olay iki kez gelmesin).
        return var_.Where(x => !var_.Any(y => !ReferenceEquals(x, y) && x.Length > y.Length && x.StartsWith(y.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))).ToList();
    }

    // İzlemeyi başlatır.
    public void Baslat(IEnumerable<string>? klasorler = null)
    {
        // Zaten açıksa dur.
        if (Aktif) return;
        // Klasörler.
        foreach (var k in klasorler ?? VarsayilanKlasorler())
            // Klasör izlenemezse atla.
            try
            {
                // Alt klasörleri de izle; büyük değişimlerde taşmaya karşı tamponu büyüt.
                var w = new FileSystemWatcher(k) { IncludeSubdirectories = true, InternalBufferSize = 65536, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
                // Olaylar.
                w.Changed += (_, e) => Kaydet(e.FullPath, false); w.Created += (_, e) => Kaydet(e.FullPath, false); w.Renamed += (_, e) => Kaydet(e.FullPath, SupheliUzanti.Contains(Path.GetExtension(e.FullPath).ToLowerInvariant()));
                // Tampon taşarsa (çok fazla olay) bu da şüphelidir; yine de say.
                w.Error += (_, _) => Kaydet("TAMPON-TASTI-" + Guid.NewGuid(), false);
                // Aç.
                w.EnableRaisingEvents = true; _izleyiciler.Add(w); Klasorler.Add(k);
            }
            catch (Exception hk13) { HataKayit.Sessiz(hk13, "TuzakMotorlari.Baslat"); }
    }

    // Bir değişikliği kaydeder ve eşiği kontrol eder. (Testte doğrudan çağrılabilir.)
    public void Kaydet(string yol, bool supheliUzanti)
    {
        // Gürültü dosyalarını ve canary klasörünü say.
        string uz = Path.GetExtension(yol).ToLowerInvariant(); string ad = Path.GetFileName(yol);
        // Geçici/gürültü dosyası.
        // (Eskiden tüm "\AppData\" yolları dışlanıyordu; bu, OneDrive/Belgeler dışına yönlendirilmiş ya da AppData altına yazan fidye yazılımına kör nokta bırakıyordu. Yalnızca gerçekten gürültülü yerler dışlanır.)
        if (Gurultu.Contains(uz) || ad.StartsWith("~$", StringComparison.Ordinal) || yol.Contains("\\AppData\\Local\\Temp\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\AppData\\Local\\Microsoft\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\AppData\\Local\\Packages\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\.git\\", StringComparison.OrdinalIgnoreCase) || yol.Contains("\\node_modules\\", StringComparison.OrdinalIgnoreCase)) return;
        // Yeni uzantı şüpheliyse işaretle (oluşturma olaylarında da).
        bool sup = supheliUzanti || SupheliUzanti.Contains(uz);
        // Aynı dosyanın art arda gelen olayları (bir günlük dosyasına sürekli yazılması gibi) kuyruğu şişirmesin.
        var simdi = DateTime.Now; if (!sup && yol == _sonYol && (simdi - _sonYolZamani).TotalMilliseconds < 1000) return; _sonYol = yol; _sonYolZamani = simdi;
        // Kuyruğa ekle.
        _olaylar.Enqueue((simdi, yol, sup));
        // En uzun pencere (10 dakika) dışındakileri at.
        while (_olaylar.TryPeek(out var eski) && (simdi - eski.Zaman).TotalSeconds > 600) _olaylar.TryDequeue(out _);
        // Alarm bekleme süresindeyse çık.
        if ((simdi - _sonAlarm).TotalSeconds < 60) return;
        // Hızlı pencere (10 sn): hem şüpheli uzantı hem toplu değişim eşiği.
        var liste = _olaylar.ToArray(); var son10 = liste.Where(x => (simdi - x.Zaman).TotalSeconds <= 10).ToArray(); int farkli = son10.Select(x => x.Yol).Distinct().Count(); int uzantili = liste.Where(x => x.Supheli && (simdi - x.Zaman).TotalSeconds <= 10).Select(x => x.Yol).Distinct().Count();
        // Şüpheli uzantı eşiği aşıldı.
        if (uzantili >= UzantiEsik) { _sonAlarm = simdi; try { Supheli?.Invoke("bilinen fidye yazılımı uzantılarına dönüştürülen dosyalar", uzantili); } catch (Exception hk14) { HataKayit.Sessiz(hk14, "TuzakMotorlari.Kaydet"); } return; }
        // Toplu değişim eşiği aşıldı.
        if (farkli >= Esik) { _sonAlarm = simdi; try { Supheli?.Invoke("10 saniyede çok sayıda dosya toplu olarak değişti", farkli); } catch (Exception hk15) { HataKayit.Sessiz(hk15, "TuzakMotorlari.Kaydet"); } return; }
        // YAVAŞ şifreleme: saniyede 4 dosyanın altında kalarak hızlı eşiği atlatan davranış 60 sn ve 10 dk pencerelerinde yakalanır (yalnızca kuyruk yeterince uzunsa hesaplanır).
        if (liste.Length >= YavasEsik60)
        {
            int d60 = liste.Where(x => (simdi - x.Zaman).TotalSeconds <= 60).Select(x => x.Yol).Distinct().Count();
            if (d60 >= YavasEsik60) { _sonAlarm = simdi; try { Supheli?.Invoke("60 saniyede sürekli ve yavaş toplu dosya değişimi", d60); } catch (Exception hk16) { HataKayit.Sessiz(hk16, "TuzakMotorlari.Kaydet"); } return; }
            if (liste.Length >= YavasEsik600) { int d600 = liste.Select(x => x.Yol).Distinct().Count(); if (d600 >= YavasEsik600) { _sonAlarm = simdi; try { Supheli?.Invoke("10 dakikada çok sayıda dosyanın yavaş yavaş değişmesi", d600); } catch (Exception hk17) { HataKayit.Sessiz(hk17, "TuzakMotorlari.Kaydet"); } } }
        }
    }
    // Yavaş şifreleme pencereleri: 60 sn'de bu kadar, 10 dk'da bu kadar FARKLI dosya (testte değiştirilebilir).
    public int YavasEsik60 { get; set; } = 150; public int YavasEsik600 { get; set; } = 600;
    private string _sonYol = ""; private DateTime _sonYolZamani = DateTime.MinValue;

    // İzlemeyi durdurur.
    public void Durdur() { foreach (var w in _izleyiciler) { try { w.Dispose(); } catch (Exception hk18) { HataKayit.Sessiz(hk18, "TuzakMotorlari.Durdur"); } } _izleyiciler.Clear(); Klasorler.Clear(); while (_olaylar.TryDequeue(out _)) { } }

    // Kaynakları bırakır.
    public void Dispose() => Durdur();
}

// Tuzakların açık/kapalı durumu (program yeniden açılınca önceki durum geri yüklensin diye). Kimlik/parola içermez.
internal sealed record TuzakDurumuVeri(bool Honeypot, int Port, bool Lab, bool Canary, bool Fidye);

internal static class TuzakDurumu
{
    public static string Yol => BulutKlasor.Dosya("tuzak_durum.json");
    public static void Kaydet(bool honeypot, int port, bool lab, bool canary, bool fidye) { try { Depo.YazJson(Yol, new TuzakDurumuVeri(honeypot, port, lab, canary, fidye)); } catch (Exception hk19) { HataKayit.Sessiz(hk19, "TuzakMotorlari.Kaydet"); } }
    public static TuzakDurumuVeri? Yukle() { try { return Depo.Yukle<TuzakDurumuVeri>(Yol); } catch { return null; } }
}
