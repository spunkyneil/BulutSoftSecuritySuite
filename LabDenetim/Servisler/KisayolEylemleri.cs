// Kısayollarla ve Komut Paleti'yle ulaşılan modal araçlar: Komut Paleti, Zamanlanmış tarama, Başarılar ve Eğitim modu. Eskiden Form1'in Kisayol.cs partial dosyasındaydı;
// artık ayrı sınıf: ana pencereden yalnızca TemaliForm (pencere sahibi) ve kabukta sayfaya gitme eylemi alır.

// Programımızın adı.
namespace LabDenetim;

// Komut Paleti, zamanlanmış tarama, başarılar ve eğitim modu eylemleri.
internal sealed class KisayolEylemleri
{
    // Zamanlama sayacı (dakikada bir bakar).
    internal readonly System.Windows.Forms.Timer _zamanSayaci = new() { Interval = 60_000 };
    private readonly TemaliForm _f; private readonly TaramaSayfasi _tarama; private readonly Action<string, string?, bool> _kabukGit; private readonly YardimEylemleri _yardim; private readonly OturumEylemleri _oturum; private readonly NobetciEylemi _nobet;

    // kabukGit: kabukta sayfaya (sekmeye) gitme eylemi (sayfa, sekme, zorla).
    public KisayolEylemleri(TemaliForm f, TaramaSayfasi tarama, Action<string, string?, bool> kabukGit, YardimEylemleri yardim, OturumEylemleri oturum, NobetciEylemi nobet) { _f = f; _tarama = tarama; _kabukGit = kabukGit; _yardim = yardim; _oturum = oturum; _nobet = nobet; }
    private void KabukGit0(string s, string? k = null, bool z = false) => _kabukGit(s, k, z);

    // Zamanlama sayacını başlatır (her dakika zamanlanmış taramaya bakar).
    internal void SayaciBaslat() { _zamanSayaci.Tick += (_, _) => ZamanlamaKontrol(); _zamanSayaci.Start(); }

    // Komut Paleti komutları: ad + çalıştırılacak iş (test edilebilsin diye ayrı yöntem).
    internal List<(string Ad, Action Is)> Komutlar()
    {
        // Komutlar: menü düğmeleri + ek komutlar.
        return new List<(string Ad, Action Is)>
        {
            ("Taramayı başlat (F5)", _tarama.TaramaKisayolu),
            ("Sonuçlarda ara (Ctrl+F)", _tarama.SonucAraKisayolu),
            ("Durum Panosu: puan, plan, özet, SIEM (Ctrl+D)", () => KabukGit0("durum", "pano")),
            ("Kullanıcı Değiştir (oturumu kapat)", () => _oturum.OturumDegistir()),
            ("Hesabım ve Kullanıcılar (parola, roller)", () => _oturum.HesapAc()),
            ("Lisans: durum, makine kimliği, lisans yükleme/üretme", () => _oturum.LisansAc()),
            ("Kalıcılık Denetçisi: başlangıç öğeleri, imza ve devre dışı bırakma", () => KabukGit0("kalici")),
            ("Ağ Haritası: sensörde nmap ile cihaz ve hizmet keşfi", () => KabukGit0("ag", "harita")),
            ("Gözetimsiz Politika: kendiliğinden yapılabilecek işleri yönet", () => KabukGit0("savunma", "nobet")),
            ("Sensör ve Bildirim Merkezi: Kali durumu, IP sızıntı testi, bildirimler", () => KabukGit0("lab", "merkez")),
            ("Giriş Saldırı Radarı: brute-force tespiti ve IP engelleme", () => KabukGit0("savunma", "brute")),
            ("Uzak Sensörler: sanal makineden tarama (SSH)", () => KabukGit0("lab", "sensor")),
            ("Güvenlik Araç Kutusu: link analizi, parola sızıntısı, FIM, vaka defteri...", () => KabukGit0("arac")),
            ("Geçmiş Operasyonlar", () => KabukGit0("tarama", "gecmis")),
            ("Profesyonel Rapor", () => KabukGit0("rapor")),
            ("Olay Zaman Çizgisi (Ctrl+E)", () => KabukGit0("olay", null, true)),
            ("Ağ Cihaz Envanteri", () => KabukGit0("ag", "cihaz")),
            ("Aktif Kalkanlarım", () => KabukGit0("savunma", "kalkan")),
            ("Tuzaklar (Erken Uyarı)", () => KabukGit0("savunma", "tuzak")),
            ("Nöbetçi Modu", () => _nobet.NobetModunuDegistir(_f, EventArgs.Empty)),
            ("Acil İzolasyon (onay sorar)", () => _tarama._acil.Degistir()),
            ("Canlı Ağ Trafiği", () => KabukGit0("trafik")),
            ("Yardım (F1)", () => _yardim.Yardim()),
            ("Tanıtım Turu", () => _yardim.TurAc()),
            ("Zamanlanmış tarama ayarları", ZamanlamaAc),
            ("Başarılar ve rozetler", BasarilarAc),
            ("Eğitim modu: örnek sonuçları yükle (gerçek değil)", EgitimYukle)
        };
    }

    // Komut Paleti'ni açar ve seçilen işi çalıştırır.
    internal void KomutPaletiAc()
    {
        var k = Komutlar();
        // Paleti göster.
        using var p = new KomutPaleti(k); p.ShowDialog(_f);
        // Seçilen iş pencere kapandıktan sonra çalışsın.
        p.Secilen?.Invoke();
    }

    // Zamanlama ayar penceresini açar.
    internal void ZamanlamaAc()
    {
        // Yetki kontrolü.
        if (!KabukYetki.Kontrol(_f, Eylem.Koruma, "Zamanlanmış Tarama")) return;
        // Pencere.
        using var f = new ZamanlamaFormu(_tarama.txtHedef.Text.Trim(), ZamanlamaDeposu.Yukle());
        // Tamam denirse kaydet.
        if (f.ShowDialog(_f) == DialogResult.OK && f.Sonuc != null) { ZamanlamaDeposu.Kaydet(f.Sonuc); _tarama.Gunluk(f.Sonuc.Aktif ? $"ZAMANLANMIŞ TARAMA -> Açık: {f.Sonuc.SaatAraligi} saatte bir (program açıkken)." : "ZAMANLANMIŞ TARAMA -> Kapatıldı.", TaramaSayfasi.Elektrik); }
    }

    // Zamanı geldiyse (ve program boştaysa) taramayı başlatır.
    internal void ZamanlamaKontrol()
    {
        // Ayar.
        var a = ZamanlamaDeposu.Yukle();
        // Kapalıysa veya meşgulse geç.
        if (!a.Aktif || !_tarama.btnBaslat.Enabled) return;
        // Lisans yoksa/bittiyse zamanlanmış tarama sessizce atlanır (ekranda açılan bir kutu gözetimsiz çalışmayı kilitlemesin).
        if (!Lisans.YeniTaramaIzni(Lisans.Durum()).Izinli) return;
        // Süre dolmadıysa geç.
        if ((DateTime.Now - a.Son).TotalHours < a.SaatAraligi) return;
        // Son çalışmayı şimdi yaz ve başlat.
        ZamanlamaDeposu.Kaydet(a with { Son = DateTime.Now }); _tarama.Gunluk("ZAMANLANMIŞ TARAMA -> Zamanı geldi, tarama başlatılıyor.", TaramaSayfasi.Elektrik); _tarama.btnBaslat_Click(_tarama.btnBaslat, EventArgs.Empty);
    }

    // Başarılar penceresini açar.
    internal void BasarilarAc()
    {
        // Pencere.
        using var f = new BasarilarFormu(GecmisDeposu.Listele()); f.ShowDialog(_f);
    }

    // Eğitim modu: gerçek olmayan örnek sonuçları ekrana koyar.
    internal void EgitimYukle()
    {
        // Tarama sürerken olmaz.
        if (!_tarama.btnBaslat.Enabled) { TemaliMesaj.Goster("Şu an bir tarama sürüyor. Bitmesini bekleyin.", "Eğitim modu", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay (varsayılan Hayır).
        if (TemaliMesaj.Goster("Eğitim modu, ekrandaki sonuçları UYDURMA örnek sonuçlarla değiştirir; programı risksiz öğrenmeniz içindir.\r\n\r\n• Hiçbir sistem ayarı değişmez, hiçbir şey taranmaz.\r\n• Örnekler Geçmiş'e kaydedilmez; başlıkları [EĞİTİM] ile başlar.\r\n• Otomatik Onar düğmesi bu örneklerde kapalıdır.\r\n\r\nDevam edilsin mi?", "Eğitim Modu", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Ekranı temizle.
        _tarama._bulgular.Clear(); _tarama.dgvBulgular.Rows.Clear(); _tarama._yesil = _tarama._sari = _tarama._kirmizi = 0;
        // Günlük kalabalık etmesin.
        _tarama._gecmisYukleniyor = true;
        // Örnekler.
        try
        {
            // Beş uydurma sonuç.
            foreach (var b in new[]
            {
                new Bulgu("Port", "[EĞİTİM] Açık kapı: 3389 (RDP)", Onem.Yuksek, "Örnek: Uzak Masaüstü kapısı dışarıya açık görünüyor.", "Gerekmiyorsa Uzak Masaüstü'nü kapatın; gerekiyorsa güvenlik duvarında yalnızca güvendiğiniz adreslere izin verin."),
                new Bulgu("Karne", "[EĞİTİM] Güvenlik duvarı özel ağda kapalı", Onem.Orta, "Örnek: Windows Güvenlik Duvarı kapalı görünüyor.", "Güvenlik duvarını açın."),
                new Bulgu("HTTP", "[EĞİTİM] Güvenlik başlığı eksik", Onem.Dusuk, "Örnek: Web yanıtında X-Frame-Options başlığı yok.", "Web sunucusu ayarına başlığı ekleyin."),
                new Bulgu("TLS", "[EĞİTİM] Sertifika geçerli", Onem.Bilgi, "Örnek: Sertifika 90 gün daha geçerli.", "İşlem gerekmiyor."),
                new Bulgu("Kalıcılık", "[EĞİTİM] Başlangıçta çalışan bilinmeyen program", Onem.Orta, "Örnek: Run anahtarında tanınmayan bir kayıt var.", "Programı tanımıyorsanız kaydı silin ve virüs taraması yapın.")
            }) _tarama.Ekle(b);
        }
        // Bayrağı kapat.
        finally { _tarama._gecmisYukleniyor = false; }
        // Bu sonuçlar gerçek makineye ait değil: onarım düğmeleri kapalı kalsın.
        _tarama._sonHedefYerel = false;
        // Ekranı güncelle.
        _tarama.SayaclariYaz(); _tarama.GorunumSec(Gorunum.Tarama); if (_tarama.dgvBulgular.Rows.Count > 0) { _tarama.dgvBulgular.ClearSelection(); _tarama.dgvBulgular.Rows[0].Selected = true; }
        // Bilgi ver.
        _tarama.Durum("EĞİTİM MODU: Bunlar uydurma örneklerdir. Satırlara tıklayıp asistanın açıklamasını okuyun; gerçek tarama için \"⚡ TARAMAYI BAŞLAT\"a basın.");
    }
}
