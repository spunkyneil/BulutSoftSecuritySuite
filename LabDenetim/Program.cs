// Programımızın adı.
namespace LabDenetim;

// Programın başlangıç noktası: .exe'ye çift tıklayınca ilk burası çalışır.
static class Program
{
    // Tek kopya kilidi (program kapanana kadar canlı kalmalı).
    private static Mutex? _kilit;

    // Son hata penceresinin zamanı (art arda gelen hatalar kullanıcıyı pencereye boğmasın).
    private static DateTime _sonHataPenceresi = DateTime.MinValue;

    // Beklenmeyen hatayı kaydeder: olay günlüğü (Kritik) + %AppData%\BulutSoft\cokme_*.txt (tam ayrıntı). Parola/anahtar içermez; yalnızca hata türü, iletisi ve yığın izi yazılır.
    internal static void BeklenmeyenHata(string kaynak, Exception? ex, bool goster)
    {
        try
        {
            string ozet = ex == null ? "bilinmeyen hata" : $"{ex.GetType().Name}: {ex.Message}"; string yer = ex?.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "";
            try { OlayDeposu.Ekle("Sistem", "Kritik", $"BEKLENMEYEN HATA ALARM -> {kaynak}: {ozet} {yer}"); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Program.BeklenmeyenHata"); }
            try { Directory.CreateDirectory(BulutKlasor.Kok); File.AppendAllText(BulutKlasor.Dosya("cokme_" + DateTime.Now.ToString("yyyyMMdd") + ".txt"), $"[{DateTime.Now:HH:mm:ss}] {kaynak}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}"); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Program.BeklenmeyenHata", true); }
            if (goster && (DateTime.Now - _sonHataPenceresi).TotalSeconds > 10)
            {
                _sonHataPenceresi = DateTime.Now;
                TemaliMesaj.Goster("Beklenmedik bir sorun oldu; program çalışmaya devam ediyor.\r\n\r\nAyrıntı kaydedildi (Olay Zaman Çizgisi ve %AppData%\\BulutSoft\\cokme_*.txt). Sorun sürerse Destek ve Yardım bölümündeki Tanılama Paketi'ni hazırlayıp gönderin.", "BulutSoft - Beklenmedik sorun", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "Program.BeklenmeyenHata"); }
    }

    // Windows'un tek bir pencere iş parçacığıyla (STA) çalışmasını sağlayan işaret.
    [STAThread]
    // Programın ilk çalışan fonksiyonu.
    static void Main(string[] args)
    {
        // Komut satırı: "--tara" / "--yardim" arayüz açmadan çalışır ve çıkış kodu verir (tek kopya kilidi ve arayüz atlanır; salt okunurdur).
        int cliKod = TaramaCalistirici.Giris(args); if (cliKod >= 0) { Environment.Exit(cliKod); return; }
        // Windows Forms için standart ayarları (yazı boyutu, ekran netliği) yap.
        ApplicationConfiguration.Initialize();
        // Beklenmeyen hatalar tek merkezden yakalanır: olay günlüğüne ve çökme dosyasına yazılır, kullanıcıya anlaşılır bir mesaj gösterilir (sessiz çökme ve ham .NET penceresi olmasın).
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => BeklenmeyenHata("arayüz", e.Exception, true);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => BeklenmeyenHata("arka plan", e.ExceptionObject as Exception, false);
        TaskScheduler.UnobservedTaskException += (_, e) => { BeklenmeyenHata("görev", e.Exception, false); e.SetObserved(); };
        // "Kullanıcı Değiştir" ile yeniden başlatıldıysa açılış ekranı atlanır (kullanıcı zaten az önce görmüştü).
        bool yenidenGiris = args.Contains("--oturum-degisti");
        // Tek kopya kilidi: program zaten açıksa ikinci kopya ortak dosyaları bozmasın. Yeniden başlatmada eski kopyanın kapanması için 15 sn beklenir.
        if (!TekKopya.Al(TekKopya.Ad(BulutKlasor.Kok), yenidenGiris ? 15000 : 0, out _kilit)) { TemaliMesaj.Goster("BulutSoft Security Suite zaten açık.\r\n\r\nGörev çubuğunu veya bildirim alanını kontrol edin. Aynı anda iki kopya çalıştırmak ayar ve kayıt dosyalarını bozabilir.", "Zaten Açık", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Koyu/Açık tema altyapısını başlat: açılış, giriş ve tüm pencereler seçili temaya göre boyanır.
        TemaYonetici.Baslat();
        // Koruma hizmeti çalışıyorsa olay günlüğünün TEK YAZARI odur: arayüz olayları hizmete iletir (ulaşılamazsa yerel dosyaya yazar, kayıp olmaz); okuma yerel + hizmet günlüğünü birleştirir.
        OlayDeposu.HizmetYazici = (tur, seviye, mesaj) => ServisIstemci.Calisiyor && ServisIstemci.Gonder("olay-yaz", new() { ["tur"] = tur, ["seviye"] = seviye, ["mesaj"] = mesaj }, 500)?.Tamam == true;
        OlayDeposu.HizmetOkumaYolu = () => ServisIstemci.Calisiyor ? Path.Combine(BrandConfig.OrtakKlasor, "Guard", "olaylar.jsonl") : null;
        // Açılış hazırlığı: normal açılışta 5 saniyelik açılış ekranıyla, yeniden girişte ekransız.
        AcilisBilgisi bilgi;
        // Açılış ekranı.
        if (yenidenGiris) bilgi = Acilis.Hazirla();
        else { using var acilis = new AcilisFormu(); acilis.ShowDialog(); if (acilis.Iptal) return; bilgi = acilis.Bilgi; }
        // Kimlik dosyasının durumu: yok = ilk kurulum, hazır = giriş, bozuk = güvenlik nedeniyle durdur.
        var durum = bilgi.Kimlik;
        // Bozuk dosya: sessizce yeni kurulum açmak güvenlik açığı olur; kullanıcıyı bilgilendirip çık.
        if (durum == KimlikDurumu.Bozuk)
        {
            // Açıklama ver.
            TemaliMesaj.Goster("Kullanıcı kayıt dosyası (kimlik.json) okunamıyor veya geçersiz.\r\n\r\nGüvenlik nedeniyle program açılmadı. Dosyanın yedeğini (kimlik.json ve kimlik.json.mac birlikte) geri koyun. Yedek yoksa ve bilgisayarın sahibi olduğunuzdan eminseniz kimlik.json, kimlik.json.mac ve varsa kimlik.json.tmp dosyalarının HEPSİNİ silerek ilk kuruluma dönebilirsiniz. Koruma hizmeti (BulutSoftGuard) kuruluysa ayrıca yönetici PowerShell'de şunu çalıştırın: reg delete HKLM\\SOFTWARE\\BulutSoft\\Guard /v KimlikMuhurHash /f\r\n" + KimlikDeposu.Yol, "BulutSoft Security Suite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            // Çık.
            return;
        }
        // Giriş (veya ilk kurulum) penceresi; iptal edilirse program kapanır.
        using (var giris = new GirisFormu(durum == KimlikDurumu.Yok)) { if (giris.ShowDialog() != DialogResult.OK || Oturum.Aktif == null) return; }
        // Ana pencereyi (Form1) aç; pencere kapanınca program biter.
        Application.Run(new Form1());
    }
}
