// Bu dosya "◉ Nöbetçi Modu (Sessiz İzleme)"ni çalıştırır: açıkken pencere küçültülünce program Windows Sistem Tepsisinde (saatin yanında)
// sessizce bekler ve 15 dakikada bir bu bilgisayarın hızlı kontrolünü yapar. Yeni bir kapı açılırsa veya güvenlik puanı düşerse
// sağ alttan "DİKKAT: Sisteminizde yeni bir kapı açıldı! İncelemek için tıklayın." balonu çıkar; tıklayınca sonuçlar ekrana gelir.
using System.Net;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Nöbetçi modu, sistem tepsisi ve koruma hizmeti izleme. Eskiden Form1'in Nobet.cs partial dosyasıydı; artık ayrı bir sınıf: ana pencereden yalnızca TemaliForm (pencere işleri),
// kaleler (bileşen kabı), tarama ekranı, savunma durumu servisi ve iki yenileme eylemi alır.
internal sealed class NobetciEylemi
{
    private readonly TemaliForm _f; private readonly System.ComponentModel.IContainer _kap; private readonly TaramaSayfasi _tarama; private readonly SavunmaDurumServisi _savunma; private readonly Action _kureYansit, _kolayYenile;
    // f: ana pencere; kap: bileşen kabı (tepsi simgesi onunla atılır); kureYansit/kolayYenile: küre ve Kolay göstergeleri yenileyen eylemler.
    public NobetciEylemi(TemaliForm f, System.ComponentModel.IContainer kap, TaramaSayfasi tarama, SavunmaDurumServisi savunma, Action kureYansit, Action kolayYenile) { _f = f; _kap = kap; _tarama = tarama; _savunma = savunma; _kureYansit = kureYansit; _kolayYenile = kolayYenile; }

    // Hızlı kontrolde bakılan kapılar (Standart profil + 135).
    internal const string NobetPortlari = "21,22,23,25,53,80,110,135,139,143,443,445,3306,3389,5432,5900,8080,8443";
    // Kontrol aralığı: 15 dakika.
    internal const int NobetAralikMs = 15 * 60 * 1000;

    // Sistem tepsisi simgesi ve sağ tık menüsü.
    internal NotifyIcon bildirim = null!;
    // Tepsi simgesinin sağ tık menüsü.
    internal ContextMenuStrip ctxTepsi = null!;
    // Periyodik kontrolü tetikleyen zamanlayıcı.
    internal readonly System.Windows.Forms.Timer _nobetZaman = new() { Interval = NobetAralikMs };
    // Nöbetçi modu açık mı?
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] internal bool _nobetAktif { get => _savunma.NobetAktif; set => _savunma.NobetAyarla(value); }   // durum Kalkan.cs'teki SavunmaDurumServisi'nde
    // Bir kontrol şu an sürüyor mu?
    internal bool _nobetCalisiyor;
    // Son kontrolde açık bulunan kapılar (karşılaştırma için "temel").
    internal HashSet<int> _nobetPortlar = new();
    // Son kontroldeki güvenlik puanı.
    internal int _nobetPuan = 100;
    // Son kontrolün tüm sonuçları ("incelemek için tıklayın" bunları ekrana getirir).
    internal List<Bulgu> _nobetBulgular = new();
    // Programın şimşek simgesi (pencere ve tepsi için).
    internal Icon? _uygulamaIkonu;

    // Tepsi simgesini, menüsünü ve zamanlayıcıyı hazırlar. Form1 açılırken bir kez çağrılır.
    internal void Kur()
    {
        // Şimşekli simgeyi çiz.
        _uygulamaIkonu = Logo.Ikon;
        // Pencerenin ve görev çubuğunun simgesi de bu olsun.
        _f.Icon = _uygulamaIkonu;
        // Tepsi simgesi (başta gizli).
        bildirim = new NotifyIcon(_kap) { Icon = _uygulamaIkonu, Text = "BulutSoft Nöbetçi Modu", Visible = false };
        // Sağ tık menüsü.
        ctxTepsi = new ContextMenuStrip(_kap) { BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard), ForeColor = TaramaSayfasi.Yazi };
        // Menü satırları.
        var ac = new ToolStripMenuItem("▣  Pencereyi Aç"); var simdi = new ToolStripMenuItem("↻  Şimdi Kontrol Et"); var kapat = new ToolStripMenuItem("◉  Nöbetçi Modunu Kapat"); var cik = new ToolStripMenuItem("✖  Çıkış");
        // Satırların işlevleri.
        ac.Click += (_, _) => PenceriyiGeriGetir(); simdi.Click += (_, _) => GuvenliCalistir.Baslat(() => NobetKontrolAsync(false), "Nobet"); kapat.Click += (_, _) => GuvenliCalistir.Baslat(async () => { if (_nobetAktif) await NobetDegistirAsync(); }, "Nobet"); cik.Click += (_, _) => _f.Close();
        // Satırları menüye ekle.
        ctxTepsi.Items.AddRange(new ToolStripItem[] { ac, simdi, new ToolStripSeparator(), kapat, cik });
        // Menüyü tepsi simgesine bağla.
        bildirim.ContextMenuStrip = ctxTepsi;
        // Simgeye çift tıklayınca pencereyi geri getir.
        bildirim.DoubleClick += (_, _) => PenceriyiGeriGetir();
        // Balona tıklayınca sonuçları ekrana getir.
        bildirim.BalloonTipClicked += (_, _) => NobetIncele();
        // Zamanlayıcı her 15 dakikada bir kontrol etsin.
        _nobetZaman.Tick += (_, _) => GuvenliCalistir.Baslat(() => NobetKontrolAsync(false), "Nobet");
        // Nöbetçi modu açıkken pencere küçültülünce tepsiye çekil.
        _f.Resize += (_, _) => { if (_nobetAktif && _f.WindowState == FormWindowState.Minimized) TepsiyeCekil(); };
        // Program kapanırken tepsi simgesi hayalet gibi kalmasın.
        _f.FormClosed += (_, _) => { _nobetZaman.Stop(); bildirim.Visible = false; bildirim.Dispose(); };
        // SSH sunucu parmak izi onayı: bilinmeyen bir sensörün parmak izi kullanıcıya gösterilir (varsayılan Hayır); onay olmadan bilinmeyen sunucuya BAĞLANILMAZ.
        SensorSunucuParmakIzi.SunucuOnayi = (host, parmakIzi) =>
        {
            Func<bool> sor = () => TemaliMesaj.Goster(string.Join(Environment.NewLine + Environment.NewLine, $"{host} adresindeki sensörü İLK KEZ görüyorum. SSH sunucu parmak izi:", parmakIzi, "Sensörde şu komutu çalıştırıp AYNI değeri gördüğünüzü doğrulayın:" + Environment.NewLine + "ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub", "Değerler aynıysa güvenin; farklıysa ağda biri araya girmiş olabilir. Bu parmak izi kaydedilir; sonradan değişirse bağlantı reddedilir.", "Bu sunucuya güveniyor musunuz?"), "Sensör parmak izi onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            return _f.IsHandleCreated && _f.InvokeRequired ? (bool)_f.Invoke(sor) : sor();
        };
        // SENSÖR KİMLİĞİ DEĞİŞTİ penceresi: kayıtlı ve şimdiki parmak izi yan yana gösterilir; varsayılan Hayır (bağlantı reddedilir). Evet derse kayıt güncellenir.
        SensorSunucuParmakIzi.SunucuDegistiOnayi = (ad, host, eski, yeni) =>
        {
            Func<bool> sor = () => TemaliMesaj.Goster(string.Join(Environment.NewLine + Environment.NewLine, $"SENSÖR KİMLİĞİ DEĞİŞTİ: \"{ad}\" ({host})", "Kayıtlı parmak izi:" + Environment.NewLine + eski, "Şimdiki parmak izi:" + Environment.NewLine + yeni, "Sensör yeniden kurulmuş olabilir; AMA ağda biri araya girmiş (ortadaki adam) de olabilir. Sensörde şunu çalıştırıp ŞİMDİKİ değerle karşılaştırın:" + Environment.NewLine + "ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub", "Emin değilseniz HAYIR deyin: bağlantı yapılmaz ve kayıt değişmez.", "Yeni parmak izi kabul edilip kayıt güncellensin mi?"), "Sensör kimliği değişti", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            return _f.IsHandleCreated && _f.InvokeRequired ? (bool)_f.Invoke(sor) : sor();
        };
        // Pencere açıldıktan sonra: önceki oturumda açık olan koruma bileşenlerini geri yükle ve koruma durumunu açıkça yaz.
        _f.Shown += (_, _) => GuvenliCalistir.Baslat(() => KorumaGeriYukleAsync(), "Nobet");
        // Kapatırken: Nöbetçi açıksa, çıkınca izlemenin DURACAĞI açıkça söylenir (kullanıcı korunduğunu sanmasın).
        _f.FormClosing += (_, e) => { if (_nobetAktif && e.CloseReason == CloseReason.UserClosing && TemaliMesaj.Goster(string.Join(Environment.NewLine + Environment.NewLine, "Nöbetçi Modu AÇIK.", "Programdan çıkarsanız 15 dakikalık otomatik kontrol, tuzaklar ve fidye izleme DURUR; bilgisayar izlenmez.", "Pencereyi kapatmak yerine küçültürseniz izleme sistem tepsisinde sürer.", "Yine de çıkılsın mı?"), "Nöbetçi Modu açık", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) e.Cancel = true; };
        // Tepsi ve küre ilk durumu yansıtsın; durum dışarıdan değişirse (servis olayı) yeniden yansır. Tepsi artık servise abone.
        _savunma.Degisti += () => { if (_f.IsHandleCreated && !_f.IsDisposed) { if (_f.InvokeRequired) _f.BeginInvoke(NobetDurumunuYansit); else NobetDurumunuYansit(); } };
        NobetDurumunuYansit();
    }

    // ---- Koruma hizmeti (BulutSoftGuard) izleme: durum, yoklama saatleri, yeni alarmlar ----
    // Durum: Calisiyor (yanıt veriyor), YanitYok (kurulu ama yanıt vermiyor), KuruluDegil.
    internal enum HizmetHali { Bilinmiyor, Calisiyor, YanitYok, KuruluDegil }
    internal readonly System.Windows.Forms.Timer _hizmetZaman = new() { Interval = 30000 }; private long _hizmetSon = DateTime.Now.AddDays(-1).Ticks; internal bool _hizmetCalisiyor, _hizmetNobet, _hizmetKoruma; private bool _hizmetYoklaniyor;
    internal HizmetHali _hizmetHali = HizmetHali.Bilinmiyor; private string _hizmetSonYoklama = "", _hizmetSonraki = "", _hizmetEtwNot = "";

    // Koruma göstergesi (Kolay Görünüm'de büyük durum): yazı ve renk (0 yeşil, 1 sarı, 2 kırmızı, 3 gri). "Gerçek zamanlı" iddiası YOKTUR.
    internal (string Yazi, int Renk) KorumaGostergesi()
    {
        string Saat(string iso) => DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToLocalTime().ToString("HH:mm") : "-";
        return _hizmetHali switch
        {
            HizmetHali.Calisiyor when _hizmetKoruma => ($"AKTİF (hizmet) · son yoklama {Saat(_hizmetSonYoklama)} · sonraki {Saat(_hizmetSonraki)}", 0),
            HizmetHali.Calisiyor => ("Hizmet çalışıyor ama nöbetçi/fidye izleme KAPALI (tıklayın)", 1),
            HizmetHali.YanitYok => ("Hizmet YANIT VERMİYOR — koruma doğrulanamıyor (tıklayın: başlat)", 2),
            HizmetHali.KuruluDegil => ("KORUMA KAPALI — hizmet kurulu değil (yalnızca program açıkken)", 2),
            _ => ("Kontrol ediliyor...", 3)
        };
    }

    // Göstergeye tıklama: hizmet çalışmıyorsa (onayla) yükseltilmiş başlatır; çalışıyorsa hemen yoklama ister; kurulu değilse nedenini söyler.
    internal void KorumaGostergesiTiklandi() => GuvenliCalistir.Baslat(() => KorumaGostergesiTiklandiAsync(), "Nobet.KorumaGostergesiTiklandi");
    internal async Task KorumaGostergesiTiklandiAsync()
    {
        string nl = Environment.NewLine + Environment.NewLine;
        if (PilotMod.SaltOkunur) { _tarama.Durum("Pilot (salt okunur) modda koruma hizmeti arayüzden yönetilemez; hizmet kendi başına çalışır."); return; }
        if (_hizmetHali == HizmetHali.KuruluDegil) { TemaliMesaj.Goster("Koruma hizmeti (BulutSoftGuard) bu bilgisayarda kurulu değil." + nl + "Koruma şu an YALNIZCA program açıkken çalışır; program kapanınca ya da oturum bitince izleme durur." + nl + "Kurmak için kurulum paketini yeniden çalıştırın (hizmeti kendisi kurar) ya da yönetici PowerShell'de HizmetKur.ps1 çalıştırın.", "Koruma hizmeti kurulu değil", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_hizmetHali != HizmetHali.Calisiyor)
        {
            if (TemaliMesaj.Goster("Koruma hizmeti kurulu ama yanıt vermiyor." + nl + "Yönetici izniyle başlatılsın mı? (Windows UAC penceresi çıkar.)", "Koruma hizmetini başlat", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var (ok, metin) = await HizmetYonetimi.BaslatAsync(); _tarama.Gunluk((ok ? "KORUMA HİZMETİ -> " : "KORUMA HİZMETİ UYARI -> ") + metin, ok ? TaramaSayfasi.RakamYesil : TaramaSayfasi.RakamKirmizi); if (!ok) TemaliMesaj.Goster(metin, "Hizmet başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            await HizmetYoklaAsync(); return;
        }
        var r = await Task.Run(() => ServisIstemci.Gonder("run-check-now")); _tarama.Gunluk(r?.Tamam == true ? "KORUMA HİZMETİ -> anlık yoklama istendi (sonuç birkaç saniye içinde günlüğe düşer)." : "KORUMA HİZMETİ UYARI -> yoklama istenemedi: " + (r?.Hata ?? "hizmet yanıt vermedi"), r?.Tamam == true ? TaramaSayfasi.RakamYesil : TaramaSayfasi.Simsek);
    }

    // Kalkan (küre) tıklaması: hizmet çalışıyorsa koruma (nöbetçi + fidye) hizmette AÇILIR/KAPATILIR; yoksa arayüzdeki Nöbetçi Modu. Pilot modda neden tepki vermediği açıklanır.
    internal void KalkanTiklandiAsync() => GuvenliCalistir.Baslat(() => KalkanTiklandiAsyncAsync(), "Nobet.KalkanTiklandiAsync");
    internal async Task KalkanTiklandiAsyncAsync()
    {
        if (PilotMod.SaltOkunur) { _tarama.Durum("Pilot (salt okunur) mod: koruma düğmesi pasif; bu kurulum makinede değişiklik yapmaz."); _tarama.Gunluk("NÖBETÇİ BİLGİ -> Pilot (salt okunur) modda kalkan düğmesi pasiftir.", TaramaSayfasi.Simsek); return; }
        if (_hizmetHali == HizmetHali.Calisiyor)
        {
            bool yeni = !_hizmetKoruma; var r = await Task.Run(() => ServisIstemci.Gonder("set-watchdog", new() { ["acik"] = yeni.ToString() }));
            _tarama.Gunluk(r?.Tamam == true ? $"KORUMA HİZMETİ -> nöbetçi ve fidye izleme {(yeni ? "AÇILDI" : "KAPATILDI")} (hizmet; kalıcı)." : "KORUMA HİZMETİ UYARI -> ayar değiştirilemedi: " + (r?.Hata ?? "hizmet yanıt vermedi"), r?.Tamam == true ? TaramaSayfasi.RakamYesil : TaramaSayfasi.Simsek); await HizmetYoklaAsync(); return;
        }
        NobetModunuDegistir(this, EventArgs.Empty);
    }

    // Hizmetin çalışıp çalışmadığına bakar (3 deneme, 5 sn arayla; durum değişince günlüğe yazar), hizmetin yazdığı yeni KRİTİK/UYARI olayları günlüğe ve balona taşır.
    internal async Task HizmetYoklaAsync()
    {
        if (_hizmetYoklaniyor) return; _hizmetYoklaniyor = true;
        try
        {
            var d = await Task.Run(() => { ServisIstemci.Sifirla(); return ServisIstemci.Gonder("get-status", null, 1200); });
            if (d == null && ServisIstemci.Kurulu) d = await ServisIstemci.GonderYenidenDeneAsync("get-status", null, 3, 5000);      // kısa kesinti: çökmeden 3 kez dene
            bool calisiyor = d?.Tamam == true; var onceki = _hizmetHali; _hizmetCalisiyor = calisiyor;
            _hizmetHali = calisiyor ? HizmetHali.Calisiyor : ServisIstemci.Kurulu ? HizmetHali.YanitYok : HizmetHali.KuruluDegil;
            var v = d?.Veri; _hizmetNobet = calisiyor && v != null && v.GetValueOrDefault("nobet") == "True"; _hizmetKoruma = calisiyor && v != null && v.GetValueOrDefault("korumaAktif") == "True";
            _hizmetSonYoklama = v?.GetValueOrDefault("sonYoklama") ?? ""; _hizmetSonraki = v?.GetValueOrDefault("sonrakiYoklama") ?? ""; _hizmetEtwNot = v?.GetValueOrDefault("etwNot") ?? "";
            if (_hizmetHali != onceki) _tarama.Gunluk(_hizmetHali switch { HizmetHali.Calisiyor => $"KORUMA HİZMETİ -> ÇALIŞIYOR (sürüm {v?.GetValueOrDefault("surum")}): arayüz kapalıyken de nöbetçi (15 dk), fidye izleme ve zincirli olay günlüğü sürer." + (_hizmetEtwNot.Length > 0 ? " Not: " + _hizmetEtwNot + "." : ""), HizmetHali.YanitYok => "NÖBETÇİ UYARI -> KORUMA KAPALI: koruma hizmeti kurulu ama YANIT VERMİYOR; koruma doğrulanamıyor.", _ => "NÖBETÇİ UYARI -> KORUMA KAPALI: koruma hizmeti kurulu değil; koruma yalnızca program açıkken çalışır." }, _hizmetHali == HizmetHali.Calisiyor ? TaramaSayfasi.RakamYesil : TaramaSayfasi.RakamKirmizi);
            if (calisiyor && v != null && v.TryGetValue("olayZinciriSaglam", out var zs) && zs != "True") _tarama.Gunluk("KORUMA HİZMETİ UYARI -> hizmetin olay günlüğü zinciri BOZUK: " + v.GetValueOrDefault("olayZinciriNot"), TaramaSayfasi.RakamKirmizi);
            if (calisiyor)
            {
                var o = await Task.Run(() => ServisIstemci.Gonder("olaylar", new() { ["sonrasi"] = _hizmetSon.ToString() }, 1500));
                if (o?.Tamam == true && o.Veri != null && o.Veri.TryGetValue("json", out var js))
                    foreach (var e in System.Text.Json.JsonSerializer.Deserialize<List<OlayKaydi>>(js) ?? new())
                    {
                        _hizmetSon = Math.Max(_hizmetSon, e.Zaman.Ticks);
                        if (e.Seviye == "Kritik") { _tarama.Gunluk("HİZMET ALARM -> " + e.Mesaj, TaramaSayfasi.RakamKirmizi); if (bildirim != null && bildirim.Visible) bildirim.ShowBalloonTip(10000, "BulutSoft Koruma Hizmeti", e.Mesaj.Length > 200 ? e.Mesaj[..200] : e.Mesaj, ToolTipIcon.Warning); }
                        else if (e.Seviye == "Uyarı") _tarama.Gunluk("HİZMET UYARI -> " + e.Mesaj, TaramaSayfasi.Simsek);
                    }
            }
            _kureYansit(); _kolayYenile();
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "Nobet.HizmetYoklaAsync"); }
        finally { _hizmetYoklaniyor = false; }
    }

    // Nöbetçi Modu'nun açık/kapalı durumu (program yeniden açılınca geri yüklenir).
    internal static string NobetDurumYolu => BulutKlasor.Dosya("nobet_durum.json");
    internal static void NobetDurumKaydet(bool aktif) { try { Depo.YazJson(NobetDurumYolu, new Dictionary<string, bool> { ["Aktif"] = aktif }); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Nobet.NobetDurumKaydet", true); } }
    internal static bool NobetDurumOku() { try { return Depo.Yukle<Dictionary<string, bool>>(NobetDurumYolu) is { } d && d.TryGetValue("Aktif", out var a) && a; } catch { return false; } }

    // Açılışta: önceki oturumdaki koruma durumunu (Nöbetçi, tuzaklar) geri yükler; ardından koruma durumunu günlüğe AÇIKÇA yazar (gizli "kapalı" durum kalmaz).
    internal async Task KorumaGeriYukleAsync()
    {
        try
        {
            _tarama._tuzak.GeriYukle(); await HizmetYoklaAsync(); _hizmetZaman.Tick += (_, _) => GuvenliCalistir.Baslat(() => HizmetYoklaAsync(), "Nobet"); _hizmetZaman.Start();
            bool izinli = Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Koruma) && !PilotMod.SaltOkunur;
            if (NobetDurumOku() && !_nobetAktif && izinli) { _tarama.Gunluk("NÖBETÇİ BİLGİ -> Önceki oturumda açık olan Nöbetçi Modu yeniden başlatılıyor.", TaramaSayfasi.Elektrik); await NobetDegistirAsync(); }
            string Dur(bool a) => a ? "AÇIK" : "KAPALI";
            // Koruma hizmeti çalışıyorsa asıl koruma ORADADIR (arayüz kapalıyken de sürer); arayüzdeki bileşenler yalnızca program açıkken çalışır. İkisi ayrı yazılır, "izlenmiyor" yanlışı çıkmaz.
            string hizmetMetni = _hizmetHali == HizmetHali.Calisiyor ? (_hizmetKoruma ? $"Koruma hizmeti ÇALIŞIYOR ve nöbetçi/fidye izleme AÇIK (arayüz kapalıyken de sürer)." : "Koruma hizmeti çalışıyor ama nöbetçi/fidye izleme KAPALI.") : _hizmetHali == HizmetHali.YanitYok ? "Koruma hizmeti kurulu ama YANIT VERMİYOR." : "Koruma hizmeti kurulu DEĞİL.";
            bool uiVar = _nobetAktif || _tarama._tuzak.Fidye.Aktif || _tarama._tuzak.Canary.Aktif || _tarama._tuzak.Honeypot.Aktif; bool izleniyor = _hizmetHali == HizmetHali.Calisiyor && _hizmetKoruma;
            _tarama.Gunluk($"NÖBETÇİ BİLGİ -> Koruma durumu: {hizmetMetni} Arayüzde: Nöbetçi Modu {Dur(_nobetAktif)}, fidye izleme {Dur(_tarama._tuzak.Fidye.Aktif)}, canary {Dur(_tarama._tuzak.Canary.Aktif)}, honeypot {Dur(_tarama._tuzak.Honeypot.Aktif)}." + (uiVar ? " Arayüzdekiler yalnızca program açıkken çalışır." : izleniyor ? "" : " Bu bilgisayar şu an otomatik izlenmiyor."), izleniyor || _nobetAktif ? TaramaSayfasi.RakamYesil : TaramaSayfasi.Simsek);
        }
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "Nobet.KorumaGeriYukleAsync"); }
    }

    // Nöbetçi durumunu tepsi simgesine ve Kolay görünümdeki küre/kalkana yansıtır (durum SavunmaDurumServisi'nde; menü düğmesi yok).
    internal void NobetDurumunuYansit()
    {
        if (bildirim != null) bildirim.Text = _nobetAktif ? "BulutSoft Nöbetçi Modu: AÇIK" : "BulutSoft Nöbetçi Modu";
        _kureYansit();
    }

    // Menü: "Nöbetçi Modu" düğmesine basılınca aç/kapat.
    internal void NobetModunuDegistir(object? sender, EventArgs e) => GuvenliCalistir.Baslat(() => NobetModunuDegistirAsync(sender, e), "Nobet.NobetModunuDegistir");
    internal async Task NobetModunuDegistirAsync(object? sender, EventArgs e)
    {
        // Yetki kontrolü.
        if (!KabukYetki.Kontrol(_f, Eylem.Koruma, "Nöbetçi Modu")) return;
        // Nöbetçi modunu aç/kapat.
        await NobetDegistirAsync();
    }

    // Nöbetçi modunu açar veya kapatır.
    internal async Task NobetDegistirAsync()
    {
        // Kapalıysa aç.
        if (!_nobetAktif)
        {
            // Açık say, tepsi simgesini göster.
            _nobetAktif = true; bildirim.Visible = true; KullaniciGunlugu.Yaz("Nöbetçi Modu'nu açtı");
            NobetDurumKaydet(true);
            // Düğmeyi güncelle.
            NobetDurumunuYansit();
            // Günlüğe ve durum çubuğuna yaz.
            _tarama.Gunluk("NÖBETÇİ MODU -> AÇIK: ilk durum kaydediliyor, sonra 15 dakikada bir kontrol edilecek.", TaramaSayfasi.RakamYesil);
            _tarama.Gunluk("NÖBETÇİ BİLGİ -> Bu bilgisayar 15 dakikada bir kontrol edilir; ANLIK (gerçek zamanlı) koruma değildir. Program kapanırsa ya da oturum biterse izleme durur.", TaramaSayfasi.RakamYesil);
            // İlk kontrolü sessizce yap: şimdiki durum "temel" olur.
            await NobetKontrolAsync(true);
            // Zamanlayıcıyı başlat.
            _nobetZaman.Start();
            // Kullanıcıyı bilgilendir.
            _tarama.Durum("Nöbetçi Modu açık. Pencereyi küçültürseniz sistem tepsisinde sessizce izlemeye devam ederim (15 dakikada bir).");
            // Kısa bir bilgi balonu.
            bildirim.ShowBalloonTip(4000, "Nöbetçi Modu açık", "Pencereyi küçültünce sistem tepsisinde sessizce izlemeye devam ederim.", ToolTipIcon.Info);
        }
        // Açıksa kapat.
        else
        {
            // Kapat, zamanlayıcıyı durdur, simgeyi gizle.
            _nobetAktif = false; _nobetZaman.Stop(); bildirim.Visible = false; KullaniciGunlugu.Yaz("Nöbetçi Modu'nu kapattı");
            NobetDurumKaydet(false);
            // Düğmeyi güncelle.
            NobetDurumunuYansit();
            // Günlüğe ve durum çubuğuna yaz.
            _tarama.Gunluk("NÖBETÇİ MODU -> KAPALI.", TaramaSayfasi.Simsek); _tarama.Durum("Nöbetçi Modu kapatıldı.", false);
            _tarama.Gunluk("NÖBETÇİ BİLGİ -> 15 dakikalık otomatik kontrol kullanıcı tarafından durduruldu; bu bilgisayar şu an otomatik izlenmiyor.", TaramaSayfasi.Simsek);
            // Pencere gizliyse geri getir (tepsi simgesi kalktı).
            if (!_f.Visible) PenceriyiGeriGetir();
        }
    }

    // Pencereyi küçültüp sistem tepsisine gizler.
    internal void TepsiyeCekil()
    {
        // Görev çubuğundan kaldır ve gizle.
        _f.ShowInTaskbar = false; _f.Hide();
        // Kullanıcıya nerede olduğumuzu söyle.
        bildirim.ShowBalloonTip(2500, "BulutSoft Nöbetçi", "Sistem tepsisinde izlemeye devam ediyorum.", ToolTipIcon.Info);
    }

    // Gizlenmiş pencereyi normal haline getirir.
    internal void PenceriyiGeriGetir()
    {
        // Görev çubuğuna geri koy ve göster.
        _f.ShowInTaskbar = true; _f.Show();
        // Normal boyuta getir ve öne al.
        _f.WindowState = FormWindowState.Normal; _f.Activate();
    }

    // Bu bilgisayarın hızlı kontrolünü yapar. ilk: true ise sessizce yalnızca "temel" durumu kaydeder.
    internal async Task NobetKontrolAsync(bool ilk)
    {
        // Üst üste kontrol başlamasın; elle tarama sürerken de karışma.
        if (_nobetCalisiyor || !_tarama.btnBaslat.Enabled) return;
        // Kontrol başladı.
        _nobetCalisiyor = true;
        // Hata olsa da nöbet bozulmasın.
        try
        {
            // Hedef: bu bilgisayar.
            var ip = IPAddress.Loopback;
            // Bakılacak kapılar.
            var portlar = PortTarayici.PortListesiOku(NobetPortlari);
            // Kapıları arka planda yokla.
            var acik = await Task.Run(() => PortTarayici.TaraAsync(ip, portlar, new Progress<int>(_ => { }), CancellationToken.None));
            // Açık kapılardan sonuçları üret.
            var bulgular = acik.Select(PortTarayici.BulguOlustur).ToList();
            // Zayıf ayar değerlendirmesini de ekle.
            bulgular.AddRange(await YapilandirmaDenetimi.DenetleAsync(ip, acik, CancellationToken.None));
            // Güvenlik puanı.
            int puan = TaramaSayfasi.PuanHesapla(bulgular);
            // Açık kapı numaraları.
            var simdi = acik.Select(p => p.Port).ToHashSet();
            // İlk kontrolse yalnızca temeli kaydet.
            if (!ilk)
            {
                // Yeni açılan kapılar.
                var yeni = simdi.Except(_nobetPortlar).OrderBy(p => p).ToList();
                // Yeni kapı varsa uyarı balonu.
                if (yeni.Count > 0)
                {
                    // Günlüğe yaz.
                    _tarama.Gunluk($"NÖBETÇİ -> YENİ KAPI AÇILDI: {string.Join(", ", yeni)}", TaramaSayfasi.RakamKirmizi);
                    // Balon: başlıkta kapı numaraları, metin istenen cümle.
                    bildirim.ShowBalloonTip(10000, "BulutSoft Nöbetçi · yeni kapı: " + string.Join(", ", yeni), "DİKKAT: Sisteminizde yeni bir kapı açıldı! İncelemek için tıklayın.", ToolTipIcon.Warning);
                }
                // Yeni kapı yok ama puan düştüyse uyarı balonu.
                else if (puan < _nobetPuan)
                {
                    // Günlüğe yaz.
                    _tarama.Gunluk($"NÖBETÇİ -> PUAN DÜŞTÜ: {_nobetPuan} → {puan}", DesignTokens.Ozgun(TokenRol.StatusWarning));
                    // Balon.
                    bildirim.ShowBalloonTip(10000, "BulutSoft Nöbetçi · güvenlik puanı düştü", $"DİKKAT: Güvenlik puanınız düştü ({_nobetPuan} → {puan})! İncelemek için tıklayın.", ToolTipIcon.Warning);
                }
                // Değişiklik yoksa günlüğe sakin bir satır yaz.
                else _tarama.Gunluk($"NÖBETÇİ -> Kontrol tamam: değişiklik yok (puan {puan}/100, {simdi.Count} açık kapı).", TaramaSayfasi.Elektrik);
            }
            // Ağ cihaz envanteri oluşturulmuşsa ağda yeni cihaz var mı da bak (ilk sessiz kontrolde yapma).
            if (!ilk) await NobetCihazKontrolcusu.KontrolEtAsync(_tarama, (baslik, metin) => bildirim.ShowBalloonTip(10000, baslik, metin, ToolTipIcon.Warning));
            // Temeli ve son sonuçları güncelle (aynı uyarı tekrar tekrar çıkmasın).
            _nobetPortlar = simdi; _nobetPuan = puan; _nobetBulgular = bulgular;
        }
        // Kontrol hatası sessizce geçilir; bir sonraki turda tekrar denenir.
        catch (Exception hk4) { HataKayit.Sessiz(hk4, "Nobet.NobetKontrolAsync"); }
        // Kontrol bitti.
        finally { _nobetCalisiyor = false; }
    }

    // Balona tıklanınca (veya menüden) son kontrolün sonuçlarını ekrana getirir.
    internal void NobetIncele()
    {
        // Pencere gizliyse geri getir.
        PenceriyiGeriGetir();
        // Elle tarama sürerken sonuçlar değiştirilmesin.
        if (!_tarama.btnBaslat.Enabled || _nobetBulgular.Count == 0) return;
        // Sayaçlar.
        int kirmizi = _nobetBulgular.Count(b => b.Onem == Onem.Yuksek && !b.Atlandi), yesil = _nobetBulgular.Count(b => b.Onem == Onem.Bilgi && !b.Atlandi), atlanan = _nobetBulgular.Count(TaramaSayfasi.Atlandi);
        // Kalan (sarı/turuncu).
        int sari = _nobetBulgular.Count - kirmizi - yesil - atlanan;
        // Sonuçları geçmiş kaydı biçiminde hazırla ve ekrana yükle.
        _tarama.YukleKayit(new GecmisKayit("nobet", DateTime.Now, "127.0.0.1", true, "Nöbetçi (hızlı kontrol)", PortTarayici.PortListesiOku(NobetPortlari).Length, 0, _nobetPuan, kirmizi, sari, yesil, atlanan, _nobetBulgular));
        // Tablo + asistan görünümüne geç.
        _tarama.GorunumSec(Gorunum.Tarama);
    }
}
