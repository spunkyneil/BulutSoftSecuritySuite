// Bu dosya "Uzak Sensörler" penceresini çizer: sensör ekleme, anahtar üretme, bağlantı sınama ve uzak port taraması (canlı JSON akışı).
// Sensör = ayrı bir sanal makine (Kali/Linux). Ağır işler orada çalışır; bu pencere yalnızca şifreli SSH ile komut gönderir ve sonuçları gösterir.
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Uzak Sensörler penceresi.
internal sealed class SensorFormu : SayfaDenetimi
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Sensör listesi (kayıtlı) ve ekrandaki alanlar.
    private List<SensorProfili> _liste = SensorDeposu.Yukle(); private readonly ListBox _lb = new(); private readonly TextBox _ad = new(), _host = new(), _port = new(), _kul = new(), _anahtar = new(), _hedef = new(), _kapilar = new(), _cikti = new();
    // Düğmeler.
    private readonly Button _tara = new EfektliButon(), _durdur = new EfektliButon();
    // Günlük kancası ve iptal şalteri.
    private readonly Action<string> _gunluk; private CancellationTokenSource? _iptal;
    // Ekrandaki sonuçlar: bulunan açık kapılar.
    private readonly List<(int Kapi, string Baslik)> _acik = new();

    // Pencereyi kurar.
    public SensorFormu(Action<string> gunluk)
    {
        // Günlük.
        _gunluk = gunluk;
        // Pencere.
        Text = "Uzak Sensörler (Sanal Makine)"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1080, 700); MinimumSize = new Size(940, 620); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Sol: sensör listesi.
        _lb.Dock = DockStyle.Left; _lb.Width = 220; _lb.BackColor = Kutu; _lb.ForeColor = Yazi; _lb.BorderStyle = BorderStyle.None; _lb.IntegralHeight = false; _lb.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular);
        // Ana alan.
        var alan = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 8) };
        // Açıklama.
        var acik = new Label { Dock = DockStyle.Top, Height = 62, ForeColor = Soluk, UseMnemonic = false, Text = "Sensör, ağır tarama işlerini ana bilgisayarınız yerine çalıştıran ayrı bir sanal makinedir (ör. VirtualBox'taki Kali). Bağlantı şifreli SSH ile ve YALNIZCA anahtarla yapılır (parola yok). Sensör ana bilgisayarınıza hiçbir kapı açmaz; bağlantıyı yalnızca bu program başlatır. Sensörde serbest komut çalışmaz; yalnızca onaylı ajan." };
        // Form alanları tablosu.
        var f = new TableLayoutPanel { Dock = DockStyle.Top, Height = 114, ColumnCount = 4, RowCount = 3 }; for (int r = 0; r < 3; r++) f.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); f.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); f.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Alan stilleri.
        foreach (var t in new[] { _ad, _host, _port, _kul, _anahtar, _hedef, _kapilar }) { t.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); t.ForeColor = Color.White; t.BorderStyle = BorderStyle.FixedSingle; t.Anchor = AnchorStyles.Left; t.Margin = new Padding(0, 3, 12, 3); }
        // Kutu genişlikleri içeriğe göre (gereksiz uzun kutular yok).
        _ad.Width = 240; _host.Width = 200; _port.Width = 80; _kul.Width = 170; _anahtar.Width = 560; _hedef.Width = 200; _kapilar.Width = 330;
        // İpuçları.
        _ad.PlaceholderText = "ör. Kali Sensörü"; _host.PlaceholderText = "ör. 192.168.56.101"; _port.Text = "22"; _kul.PlaceholderText = "ör. kali"; _anahtar.ReadOnly = false; _anahtar.PlaceholderText = "Anahtar Üret'e basın veya Gözat";
        // Satırlar.
        void Satir(int r, string a, Control x, string b, Control y) { f.Controls.Add(Et(a), 0, r); f.Controls.Add(x, 1, r); f.Controls.Add(Et(b), 2, r); f.Controls.Add(y, 3, r); }
        Satir(0, "Sensör adı:", _ad, "Adres (IP):", _host); Satir(1, "SSH kapısı:", _port, "Kullanıcı:", _kul); f.Controls.Add(Et("Anahtar dosyası:"), 0, 2); f.Controls.Add(_anahtar, 1, 2); f.SetColumnSpan(_anahtar, 3);
        // Düğme şeridi 1.
        var s1 = Serit(); var yeni = Dg("Yeni", 80); var kaydet = Dg("Kaydet", 90); var sil = Dg("Sil", 70); var uret = Dg("Anahtar Üret", 130); var goster = Dg("Ortak Anahtarı Göster", 190); var sina = Dg("Bağlantıyı Sına", 150); s1.Controls.AddRange(new Control[] { yeni, kaydet, sil, uret, goster, sina });
        // Uzak tarama alanı.
        var tbl = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 5 }; tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        _hedef.PlaceholderText = "IP veya aralık (ör. 192.168.56.0/24)"; _kapilar.Text = "21,22,23,25,53,80,110,135,139,143,443,445,3306,3389,5900,8080";
        tbl.Controls.Add(Et("Hedef (IP):"), 0, 0); tbl.Controls.Add(_hedef, 1, 0); tbl.Controls.Add(Et("Kapılar:"), 2, 0); tbl.Controls.Add(_kapilar, 3, 0);
        // Tarama düğmeleri.
        var s2 = Serit(); _tara.Text = "▶ Sensörden Port Taraması"; _tara.Size = new Size(240, 34); StilVer(_tara, DesignTokens.Ozgun(TokenRol.StatusSuccessBg)); _durdur.Text = "■ Durdur"; _durdur.Size = new Size(110, 34); StilVer(_durdur, DesignTokens.Ozgun(TokenRol.StatusDanger)); _durdur.Enabled = false; s2.Controls.AddRange(new Control[] { _tara, _durdur });
        // Çıktı kutusu.
        _cikti.Multiline = true; _cikti.ReadOnly = true; _cikti.Dock = DockStyle.Fill; _cikti.BackColor = Kutu; _cikti.ForeColor = Yazi; _cikti.BorderStyle = BorderStyle.None; _cikti.ScrollBars = ScrollBars.Vertical; _cikti.Font = FontProvider.Al(FontRol.Mono); _cikti.Text = "Sol listeden bir sensör seçin veya bilgileri yazıp \"Kaydet\"e basın.\r\n\r\nİlk kurulum (3 adım):\r\n 1) \"Anahtar Üret\" → 2) \"Ortak anahtarı göster\" ve çıkan komutu SENSÖRDE (Kali terminalinde) çalıştırın → 3) \"Bağlantıyı Sına\".\r\n\r\nİlk başarılı bağlantıda sensörün parmak izi sabitlenir; sonradan değişirse bağlantı reddedilir.";
        // Dock sırası: alttan yukarı.
        alan.Controls.Add(_cikti); alan.Controls.Add(s2); alan.Controls.Add(tbl); alan.Controls.Add(s1); alan.Controls.Add(f); alan.Controls.Add(acik);
        // Önce alan, sonra liste.
        Controls.Add(alan); Controls.Add(_lb);
        // Olaylar.
        _lb.SelectedIndexChanged += (_, _) => Goster(); yeni.Click += (_, _) => { _lb.SelectedIndex = -1; Temizle(); }; kaydet.Click += (_, _) => Kaydet(); sil.Click += (_, _) => Sil(); uret.Click += (_, _) => GuvenliCalistir.Baslat(() => UretAsync(), "SensorFormu"); goster.Click += (_, _) => AnahtarGoster(); sina.Click += (_, _) => GuvenliCalistir.Baslat(() => SinaAsync(), "SensorFormu"); _tara.Click += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "SensorFormu"); _durdur.Click += (_, _) => { if (_iptal == null || _iptal.IsCancellationRequested) return; _durdur.Enabled = false; Yaz("Durduruluyor..."); _iptal.Cancel(); };
        // Kapanırken tarama dursun.
        FormClosing += (_, _) => _iptal?.Cancel();
        // Listeyi doldur.
        Listele(); Temizle(); Shown += (_, _) => { _cikti.SelectionLength = 0; ActiveControl = null; };
    }

    // Etiket.
    private static Label Et(string t) => new() { Text = t, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Soluk, UseMnemonic = false };
    // Düğme şeridi.
    private static FlowLayoutPanel Serit() => new() { Dock = DockStyle.Top, Height = 44, WrapContents = false };
    // Düğme.
    private static Button Dg(string t, int g) { var b = new EfektliButon { Text = t, Size = new Size(g, 34), Margin = new Padding(0, 4, 8, 4) }; StilVer(b, DesignTokens.Ozgun(TokenRol.AccentPrimary)); return b; }
    // Düğme stili.
    private static void StilVer(Button b, Color renk) { b.FlatStyle = FlatStyle.Flat; b.BackColor = renk; b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 4, 8, 4); }
    // Bilgi.
    private static void Bilgi(string m) => TemaliMesaj.Goster(m, "Uzak Sensörler", MessageBoxButtons.OK, MessageBoxIcon.Information);
    // Çıktıya satır ekler.
    private void Yaz(string s) { if (!IsDisposed) _cikti.AppendText(s + "\r\n"); }

    // Listeyi yeniler.
    private void Listele() { _lb.Items.Clear(); foreach (var s in _liste) _lb.Items.Add(s.Ad + "  (" + s.Host + ")"); }
    // Alanları temizler.
    private void Temizle() { _ad.Clear(); _host.Clear(); _port.Text = "22"; _kul.Clear(); _anahtar.Text = BulutKlasor.Dosya(Path.Combine("sensor", "id_bulutsoft_ed25519")); }
    // Seçili sensörü alanlara yazar.
    private void Goster() { if (_lb.SelectedIndex < 0) return; var s = _liste[_lb.SelectedIndex]; _ad.Text = s.Ad; _host.Text = s.Host; _port.Text = s.Port.ToString(); _kul.Text = s.Kullanici; _anahtar.Text = s.AnahtarYolu; }
    // Alanlardan profil üretir (hatalıysa null ve uyarı).
    private SensorProfili? Profil(bool anahtarVarSart = true)
    {
        // Kapı sayısı.
        int.TryParse(_port.Text.Trim(), out int port); var p = new SensorProfili(_ad.Text.Trim(), _host.Text.Trim(), port, _kul.Text.Trim(), _anahtar.Text.Trim());
        // Doğrula (anahtar dosyası şartı isteğe bağlı: kaydederken anahtar henüz yoksa da kaydedilebilsin).
        var h = SensorGuvenlik.ProfilHatasi(p); if (h != null && (anahtarVarSart || !h.StartsWith("Anahtar"))) { Bilgi(h); return null; }
        // Geçerli.
        return p;
    }

    // Kaydeder (aynı ad varsa günceller).
    private void Kaydet()
    {
        // Profil.
        var p = Profil(false); if (p == null) return;
        // Ekle/güncelle.
        int i = _liste.FindIndex(x => x.Ad == p.Ad); if (i >= 0) _liste[i] = p; else _liste.Add(p); SensorDeposu.Kaydet(_liste); Listele(); _lb.SelectedIndex = _liste.FindIndex(x => x.Ad == p.Ad); Yaz($"Kaydedildi: {p.Ad}");
    }

    // Siler (onaylı).
    private void Sil()
    {
        // Seçili yoksa.
        if (_lb.SelectedIndex < 0) { Bilgi("Önce listeden bir sensör seçin."); return; }
        // Onay (varsayılan Hayır).
        var s = _liste[_lb.SelectedIndex]; if (TemaliMesaj.Goster($"\"{s.Ad}\" sensör kaydı silinsin mi?\r\n\r\n(Sensör makinesine ve anahtar dosyasına dokunulmaz.)", "Sensörü Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Sil.
        _liste.Remove(s); SensorDeposu.Kaydet(_liste); Listele(); Temizle();
    }

    // Anahtar üretir.
    private async Task UretAsync()
    {
        // Yol boşsa varsayılan.
        if (string.IsNullOrWhiteSpace(_anahtar.Text)) _anahtar.Text = BulutKlasor.Dosya(Path.Combine("sensor", "id_bulutsoft_ed25519"));
        // Var olan anahtar korunur.
        bool vardi = File.Exists(_anahtar.Text.Trim());
        // Üret.
        try { string pub = await SensorAnahtarYoneticisi.AnahtarUretAsync(_anahtar.Text.Trim()); Yaz(vardi ? "Bu yolda anahtar zaten var; üzerine YAZILMADI. Ortak anahtar:" : "Yeni anahtar üretildi (parolasız; dosya yalnızca bu Windows hesabında durur). Ortak anahtar:"); Yaz(pub); }
        // Hata.
        catch (Exception x) { Yaz("Hata: " + x.Message); }
    }

    // Ortak anahtarı ve sensörde çalıştırılacak komutu gösterir.
    private void AnahtarGoster()
    {
        // Dosya yoksa.
        string y = _anahtar.Text.Trim() + ".pub"; if (!File.Exists(y)) { Bilgi("Önce \"Anahtar Üret\"e basın."); return; }
        // Komut.
        try { string pub = File.ReadAllText(y).Trim(); Yaz("\r\nSENSÖRDE (Kali terminalinde) şu komutu bir kez çalıştırın:\r\n\r\n" + SensorAnahtarYoneticisi.AnahtarEklemeKomutu(pub) + "\r\n\r\nSonra \"Bağlantıyı Sına\"ya basın."); }
        // Hata.
        catch (Exception x) { Yaz("Hata: " + x.Message); }
    }

    // Bağlantıyı sınar.
    private async Task SinaAsync()
    {
        // Profil.
        var p = Profil(); if (p == null) return;
        // İptal şalteri.
        _iptal = new CancellationTokenSource(); Yaz($"\r\n{p.Ad} sensörüne bağlanılıyor...");
        // Sına.
        try { var s = await SensorTaramaYurutucu.SinaAsync(p, _iptal.Token); Yaz(s.Ozet); _gunluk(s.Basarili ? $"SENSÖR -> {p.Ad} bağlantısı başarılı." : $"SENSÖR UYARI -> {p.Ad} bağlantısı kurulamadı."); }
        // Hata.
        catch (Exception x) { Yaz("Hata: " + x.Message); }
    }

    // Sensörden port taraması yapar; sonuçlar JSON satırı olarak canlı gelir.
    private async Task TaraAsync()
    {
        // Profil ve girdiler.
        var p = Profil(); if (p == null) return; string hedef = _hedef.Text.Trim(), kapi = _kapilar.Text.Trim().Replace(" ", "");
        // Komut (girdiler doğrulanır).
        string komut; try { komut = SensorKomutlari.TcpKomutu(hedef, kapi); } catch (ArgumentException x) { Bilgi(x.Message); return; }
        // Onay: sensör hedefi tarayacak.
        if (TemaliMesaj.Goster($"\"{p.Ad}\" sensörü {hedef} adresinin {kapi.Split(',').Length} kapı grubunu yoklayacak.\r\n\r\nYalnızca KENDİNİZE ait/izinli cihazları tarayın. Devam edilsin mi?", "Uzak Tarama", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Hazırlık.
        _acik.Clear(); _tara.Enabled = false; _durdur.Enabled = true; _iptal = new CancellationTokenSource(); Yaz($"\r\n--- {p.Ad} → {hedef} ({kapi}) ---"); _gunluk($"SENSÖR -> {p.Ad} üzerinden {hedef} taranıyor.");
        // Çalıştır.
        try
        {
            // Satır geldikçe ekrana yaz (arayüz iş parçacığında).
            var (kod, hata) = await SensorTaramaYurutucu.CalistirAsync(p, komut, SensorAjani.Kaynak, satir => { var o = SensorCiktiAyristirici.Ayristir(satir); if (o != null) BeginInvoke(() => Olay(o)); }, _iptal.Token, 600);
            // Hata varsa söyle.
            if (_iptal.IsCancellationRequested) { Yaz("Tarama durduruldu."); Bildirim.Goster("Tarama durduruldu", $"{p.Ad} sensöründeki tarama yarıda kesildi.", BildirimTuru.Uyari); }
            else if (kod != 0) { Yaz("Sensör hatası: " + SensorCiktiAyristirici.HataAciklama(hata)); Bildirim.Goster("Sensör taraması başarısız", SensorCiktiAyristirici.HataAciklama(hata).Split('\n')[0].Trim(), BildirimTuru.Hata); }
        }
        // Beklenmeyen hata.
        catch (Exception x) { Yaz("Hata: " + x.Message); Bildirim.Goster("Sensör taraması başarısız", x.Message.Split('\n')[0].Trim(), BildirimTuru.Hata); }
        // Düğmeleri aç.
        finally { if (!IsDisposed) { _tara.Enabled = true; _durdur.Enabled = false; } }
    }

    // Sensörden gelen bir olayı ekrana işler.
    private void Olay(AjanOlayi o)
    {
        // Türüne göre.
        switch (o.T)
        {
            // Başladı.
            case "start": Yaz($"Başladı: {o.Ip}, {o.Toplam} deneme."); break;
            // Açık kapı (aralık taramasında hangi cihaz olduğu da yazılır).
            case "open": _acik.Add((o.Kapi, o.Baslik)); Yaz($"AÇIK  {(o.Ip.Length > 0 ? o.Ip + "  " : "")}{o.Kapi,5}   {(o.Baslik.Length > 0 ? "\"" + o.Baslik + "\"" : "")}"); break;
            // İlerleme.
            case "prog": Yaz($"... {o.Biten}/{o.Toplam}"); break;
            // Bitti.
            case "done": Yaz($"Bitti: {o.Acik} açık kapı, {o.Sure} sn."); _gunluk($"SENSÖR -> Uzak tarama bitti: {o.Acik} açık kapı."); Bildirim.Goster("Sensör taraması bitti", $"{o.Acik} açık kapı bulundu · {o.Sure} sn.", o.Acik > 0 ? BildirimTuru.Uyari : BildirimTuru.Basari); break;
            // Sensör hata bildirdi.
            case "error": Yaz("Sensör uyarısı: " + o.Mesaj); break;
        }
    }
}
