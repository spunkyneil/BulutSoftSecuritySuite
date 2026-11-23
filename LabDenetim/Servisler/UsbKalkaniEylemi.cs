// USB Kalkanı: yeni takılan/çıkarılan USB cihazları WMI ile canlı izler, günlüğe yazar ve uyarı kutusu gösterir (BadUSB şüphesi için klavye benzeri cihaz uyarısı).
// Eskiden Form1'in UsbKalkani.cs partial dosyasındaydı; USB uyarı penceresi (UsbBildirimFormu) aynı dosyada kalır.
using System.Management;

// Programımızın adı.
namespace LabDenetim;

// USB Kalkanı eylemi.
internal sealed class UsbKalkaniEylemi
{
    private readonly ITaramaBaglami _b; private readonly UyariKutusu _kutu;
    // "Yeni USB cihaz" olayını dinleyen WMI izleyicisi.
    private ManagementEventWatcher? _usbTak;
    // "USB cihaz çıkarıldı" olayını dinleyen WMI izleyicisi.
    private ManagementEventWatcher? _usbCik;
    // Aynı fiş takılınca arka arkaya gelen çok sayıda olayı tek uyarıya indirmek için son uyarı zamanı.
    private DateTime _usbSonUyari = DateTime.MinValue;
    // Klavye benzeri (BadUSB şüphesi) uyarısının son zamanı.
    private DateTime _usbKlavyeUyari = DateTime.MinValue;

    public UsbKalkaniEylemi(ITaramaBaglami baglam, UyariKutusu kutu) { _b = baglam; _kutu = kutu; }

    // WMI dinleyicilerini başlatır. Form1 açılırken bir kez çağrılır; başlatılamazsa program yine çalışır.
    public void Kur()
    {
        // WMI erişimi engellenmiş olabilir.
        try
        {
            // Takılma: Win32_PnPEntity yeni oluştuğunda (USB aygıt kimlikleri USB\ veya USBSTOR\ ile başlar). İki saniyede bir yoklar.
            _usbTak = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' AND (TargetInstance.DeviceID LIKE 'USB\\\\%' OR TargetInstance.DeviceID LIKE 'USBSTOR\\\\%')"));
            // Olay gelince ayrıntıyı oku ve ekran iş parçacığına aktar.
            _usbTak.EventArrived += (_, e) => OlayIsle(e.NewEvent, true);
            // Dinlemeyi başlat.
            _usbTak.Start();
            // Çıkarılma: aynı sorgunun silinme sürümü.
            _usbCik = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' AND (TargetInstance.DeviceID LIKE 'USB\\\\%' OR TargetInstance.DeviceID LIKE 'USBSTOR\\\\%')"));
            // Olay gelince işle.
            _usbCik.EventArrived += (_, e) => OlayIsle(e.NewEvent, false);
            // Dinlemeyi başlat.
            _usbCik.Start();
            // Günlüğe bilgi yaz.
            _b.Gunluk("USB KALKANI -> Aktif: yeni takılan USB/harici cihazlar canlı izleniyor.", TaramaRenkleri.Elektrik);
        }
        // Başlatılamazsa günlüğe nedenini yaz.
        catch (Exception ex) { _b.Gunluk("USB KALKANI -> Başlatılamadı (" + ex.Message + "). Diğer özellikler çalışmaya devam eder.", TaramaRenkleri.Simsek); }
        // Program kapanırken dinleyicileri durdur.
        _b.KapanirkenCalistir(() => { try { _usbTak?.Stop(); _usbTak?.Dispose(); _usbCik?.Stop(); _usbCik?.Dispose(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "UsbKalkani.UsbKur"); } });
    }

    // WMI olayından cihaz bilgisini okur (arka plan iş parçacığında çalışır) ve ekrandaki işleyiciye gönderir.
    private void OlayIsle(ManagementBaseObject olay, bool takildi)
    {
        // Okuma hatası olursa yut.
        try
        {
            // Olayın içindeki cihaz nesnesi.
            var cihaz = (ManagementBaseObject)olay["TargetInstance"];
            // Gerekli alanlar.
            string ad = cihaz["Name"]?.ToString() ?? "Bilinmeyen USB cihaz", sinif = cihaz["PNPClass"]?.ToString() ?? "", kimlik = cihaz["DeviceID"]?.ToString() ?? "", uretici = cihaz["Manufacturer"]?.ToString() ?? "";
            // Pencere yoksa veya kapanıyorsa bırak.
            if (!_b.PencereHazir) return;
            // Ekranı çizen iş parçacığında işle.
            _b.UiIsle(() => CihazBildir(ad, sinif, kimlik, uretici, takildi));
        }
        // Okunamayan olayı atla.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "UsbKalkani.UsbOlayIsle"); }
    }

    // Cihaz bilgisini günlüğe yazar ve uyarı kutusunu gösterir. Test için de doğrudan çağrılabilir.
    internal void CihazBildir(string ad, string sinif, string kimlik, string uretici, bool takildi, int kutuSuresiMs = 9000)
    {
        // Çıkarılma: yalnızca günlüğe sakin bir satır.
        if (!takildi) { _b.Gunluk($"USB KALKANI -> Cihaz çıkarıldı: {ad}", TaramaRenkleri.Elektrik); return; }
        // USB kök denetleyicileri ve hub'lar gürültüdür; atla.
        if (kimlik.Contains("ROOT_HUB", StringComparison.OrdinalIgnoreCase) || ad.Contains("Root Hub", StringComparison.OrdinalIgnoreCase)) return;
        // Klavye/HID benzeri mi? (Zararlı USB'ler çoğunlukla bellek gibi görünüp klavye gibi davranır: BadUSB.)
        bool klavye = sinif.Equals("Keyboard", StringComparison.OrdinalIgnoreCase) || ad.Contains("keyboard", StringComparison.OrdinalIgnoreCase) || ad.Contains("klavye", StringComparison.OrdinalIgnoreCase);
        // Depolama (bellek/disk) mi?
        bool depo = kimlik.StartsWith("USBSTOR", StringComparison.OrdinalIgnoreCase) || sinif.Equals("DiskDrive", StringComparison.OrdinalIgnoreCase) || ad.Contains("Mass Storage", StringComparison.OrdinalIgnoreCase) || ad.Contains("Yığın", StringComparison.OrdinalIgnoreCase);
        // Aynı fişten gelen arka arkaya olaylar: 6 saniye içindeki ek parçalar yalnızca günlüğe yazılır.
        bool ekParca = (DateTime.Now - _usbSonUyari).TotalSeconds < 6;
        // Klavye uyarısı daha önce verilmişse tekrar verme (10 sn).
        bool klavyeTekrar = klavye && (DateTime.Now - _usbKlavyeUyari).TotalSeconds < 10;
        // Günlük satırı.
        _b.Gunluk($"USB KALKANI -> {(klavye ? "KLAVYE BENZERİ CİHAZ" : depo ? "USB DEPOLAMA" : "YENİ USB CİHAZ")}: {ad}{(uretici.Length > 0 ? " (" + uretici + ")" : "")} · durum: takıldı ve algılandı", klavye ? TaramaRenkleri.Kirmizi : TaramaRenkleri.Simsek);
        // Ek parça ve klavye değilse ya da klavye uyarısı zaten verildiyse kutu açma.
        if ((ekParca && !klavye) || klavyeTekrar) return;
        // Zamanları güncelle.
        if (!ekParca) _usbSonUyari = DateTime.Now; if (klavye) _usbKlavyeUyari = DateTime.Now;
        // Kutunun metinleri.
        string baslik = klavye ? "⚠ KLAVYE BENZERİ CİHAZ TAKILDI" : depo ? "◈ YENİ USB BELLEK / DİSK ALGILANDI" : "◈ YENİ HARİCİ CİHAZ ALGILANDI";
        // Açıklama satırı.
        string aciklama = klavye
            ? "Bu cihaz kendini klavye olarak tanıttı. Siz bir klavye takmadıysanız tanımadığınız bir USB'yi hemen çıkarın; bellek görünümlü zararlı cihazlar (BadUSB) kendi kendine yazı yazıp komut çalıştırabilir."
            : depo ? "Tanımadığınız bir USB belleği takmayın ve içindeki dosyaları açmayın. Kendi cihazınızsa sorun yok. Durum: takıldı, kullanıma hazır."
            : "Cihazı siz taktıysanız sorun yok. Tanımıyorsanız çıkarın. Durum: takıldı, sürücüsü yüklendi.";
        // Eski kutu açıksa kapat.
        _kutu.Goster(baslik, ad.Length > 0 ? ad : "Bilinmeyen cihaz", aciklama, klavye ? TaramaRenkleri.Kirmizi : TaramaRenkleri.Simsek, kutuSuresiMs);
        // Nöbetçi modunda sistem tepsisindeysek balonla da haber ver.
        _b.TepsiBalonu(8000, klavye ? "USB: klavye benzeri cihaz!" : "Yeni USB cihaz takıldı", ad, klavye);
    }
}
