// Bu dosya "Sensör ve Bildirim Merkezi" penceresini çizer: Kali sensör durumu (yeniden dene / sanal makineyi başlat), IP sızıntı testi, bildirim kanalları (Telegram / Discord / Teams),
// ve yalnızca UZMAN görünümünde: Kali konsolu (canlı komut çıktıları) ile nmap komut önizlemesi.
// Pencere sistemde hiçbir şeyi DEĞİŞTİRMEZ; tek istisna kullanıcının onayladığı "sanal makineyi başlat" ve ağa çıkan testlerdir (hepsi onay ister, varsayılan "Hayır").
using System.Text;
using System.Text.Json;
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Merkez penceresi.
internal sealed class MerkezFormu : SayfaDenetimi
{
    public const int SekmeKali = 0, SekmeSizinti = 1, SekmeBildirim = 2, SekmeKonsol = 3, SekmeNmap = 4, SekmeRehber = 5;
    internal static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Giris = DesignTokens.Ozgun(TokenRol.SurfaceInput);
    internal readonly Panel[] _paneller = new Panel[6]; private readonly Button[] _dugmeler = new EfektliButon[6]; private readonly Action _sensorlerAc, _haritaAc; private readonly Action<string> _olay; private bool _uzman;
    // Kali sekmesi.
    internal readonly Label _kaliDurum = new(), _kaliAyrinti = new(), _kaliParam = new(); private readonly Button _tekrar = new EfektliButon(), _uyandir = new EfektliButon(), _sensorBtn = new EfektliButon();
    // IP sızıntı sekmesi.
    internal readonly CheckBox _kaliTest = new() { Text = "Kali sensörünü de test et", AutoSize = true }, _tunelBekle = new() { Text = "Kali'nin ayrı bir tünelden (Tailscale/VPN) çıkması bekleniyor", AutoSize = true }; private readonly TextBox _sizSonuc = new(); private readonly Button _sizBaslat = new EfektliButon(); private readonly Label _sizDurum = new();
    // Bildirim sekmesi.
    internal readonly TextBox _tgToken = new(), _tgChat = new(), _dcUrl = new(), _tmUrl = new(), _gnUrl = new(), _onizleme = new(); private readonly CheckBox _tgEtkin = new() { Text = "Telegram etkin", AutoSize = true }, _dcEtkin = new() { Text = "Discord etkin", AutoSize = true }, _tmEtkin = new() { Text = "Teams etkin", AutoSize = true }, _gnEtkin = new() { Text = "Genel webhook etkin", AutoSize = true }, _ayrintili = new() { Text = "Ayrıntılı mesaj (yerel IP ve kullanıcı yollarını gizleme)", AutoSize = true }, _goster = new() { Text = "Gizli alanları göster", AutoSize = true };
    internal readonly ComboBox _esik = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 }, _onizKanal = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 }; private readonly Label _bilDurum = new();
    // Konsol ve nmap.
    internal readonly TextBox _konsol = new(); private readonly TextBox _nHedef = new() { Text = "192.168.56.0/24", Width = 220 }; private readonly ComboBox _nProfil = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 }, _nZam = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 }; private readonly Label _nOniz = new(), _nUyari = new();
    internal readonly Action<string> _konsolDinle; private readonly Action _kaliDinle;

    public MerkezFormu(int sekme, Action<string> olay, Action sensorlerAc, Action haritaAc)
    {
        _olay = olay; _sensorlerAc = sensorlerAc; _haritaAc = haritaAc; _uzman = ArayuzModu.Mevcut == ModTuru.Uzman;
        Text = "Sensör ve Bildirim Merkezi"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1120, 700); MinimumSize = new Size(900, 560); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Sol gezinme.
        var sol = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 230, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 14, 10, 0), BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar) };
        string[] ad = { "Kali Sensör Durumu", "IP Sızıntı Testi", "Bildirim Kanalları", "Kali Konsol (Uzman)", "Nmap Komutu (Uzman)", "Kali Araç Rehberi (Uzman)" };
        for (int i = 0; i < 6; i++)
        {
            int ix = i; var b = new EfektliButon { Text = ad[i], Size = new Size(206, 44), FlatStyle = FlatStyle.Flat, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 0, 0, 6), Visible = i < 3 || _uzman };
            b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => Goster(ix); _dugmeler[i] = b; sol.Controls.Add(b);
            _paneller[i] = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(18, 14, 18, 10) };
        }
        KaliKur(_paneller[0]); SizintiKur(_paneller[1]); BildirimKur(_paneller[2]); KonsolKur(_paneller[3]); NmapKur(_paneller[4]); RehberKur(_paneller[5]);
        foreach (var p in _paneller) Controls.Add(p); Controls.Add(sol);
        // Canlı dinleyiciler (pencere kapanınca bırakılır).
        _konsolDinle = s => { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => { _konsol.AppendText(s + "\r\n"); }); }; KaliKonsol.SatirGeldi += _konsolDinle;
        _kaliDinle = () => { if (IsHandleCreated && !IsDisposed) BeginInvoke(KaliYenile); }; KaliDurumYoneticisi.Degisti += _kaliDinle;
        FormClosed += (_, _) => { KaliKonsol.SatirGeldi -= _konsolDinle; KaliDurumYoneticisi.Degisti -= _kaliDinle; };
        Goster(!_uzman && sekme > 2 ? 0 : sekme); KaliYenile(); BildirimYukle(); NmapOnizle();
    }

    // Etiket/stil yardımcıları.
    internal static Label Baslik(string t) => new() { Text = t, Dock = DockStyle.Top, Height = 40, Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), UseMnemonic = false };
    internal static Label Aciklama(string t, int yuk = 70) => new() { Text = t, Dock = DockStyle.Top, Height = yuk, ForeColor = Soluk, UseMnemonic = false };
    internal static void Stil(Button b, int g, Color? renk = null) { b.Size = new Size(g, 36); b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = renk ?? DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); }
    internal static void Kutuyu(TextBox t, int genislik = 460) { t.BackColor = Giris; t.ForeColor = Yazi; t.BorderStyle = BorderStyle.FixedSingle; t.Width = genislik; }
    internal static Label Etiket(string t) => new() { Text = t, AutoSize = true, ForeColor = Soluk, Margin = new Padding(0, 8, 0, 2), UseMnemonic = false };

    internal void Goster(int i)
    {
        for (int k = 0; k < 6; k++) { _paneller[k].Visible = k == i; _dugmeler[k].BackColor = k == i ? DesignTokens.Ozgun(TokenRol.SurfaceElevated) : DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _dugmeler[k].ForeColor = k == i ? Color.White : Soluk; }
        if (i == SekmeKonsol && _konsol.TextLength == 0) _konsol.Text = string.Join("\r\n", KaliKonsol.Hepsi()) + "\r\n";
    }

    // =================== Kali sekmesi ===================
    internal void KaliKur(Panel p)
    {
        var ic = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        _kaliDurum.AutoSize = false; _kaliDurum.Size = new Size(900, 46); _kaliDurum.Font = FontProvider.Al(FontRol.Display, FontStyle.Bold); _kaliAyrinti.AutoSize = false; _kaliAyrinti.Size = new Size(900, 90); _kaliAyrinti.ForeColor = Soluk; _kaliParam.AutoSize = false; _kaliParam.Size = new Size(900, 110); _kaliParam.ForeColor = Yazi; _kaliParam.Font = FontProvider.Al(FontRol.Mono);
        var serit = new FlowLayoutPanel { Height = 46, Width = 900, WrapContents = false }; _tekrar.Text = "Yeniden Dene"; _uyandir.Text = "Sanal Makineyi Başlat..."; _sensorBtn.Text = "Sensör Ayarları (SSH)..."; Stil(_tekrar, 160); Stil(_uyandir, 220, DesignTokens.Ozgun(TokenRol.StatusSuccess)); Stil(_sensorBtn, 220);
        serit.Controls.AddRange(new Control[] { _tekrar, _uyandir, _sensorBtn }); _tekrar.Click += (_, _) => GuvenliCalistir.Baslat(() => TekrarDene(), "MerkezFormu"); _uyandir.Click += (_, _) => GuvenliCalistir.Baslat(() => SanalMakineBaslat(), "MerkezFormu"); _sensorBtn.Click += (_, _) => _sensorlerAc();
        ic.Controls.AddRange(new Control[] { _kaliDurum, _kaliAyrinti, serit, _kaliParam }); p.Controls.Add(ic); p.Controls.Add(Aciklama("Program her açıldığında Kali sanal makinenize SSH ile bağlanıp anahtarı, Python'u ve ajanı denetler. Kali kapalıysa burada görürsünüz; sanal makineyi tek tuşla (onayınızla) başlatabilirsiniz.", 52)); p.Controls.Add(Baslik("Kali Sensör Durumu"));
    }
    internal void KaliYenile()
    {
        var s = KaliDurumYoneticisi.Son; var (yazi, renk) = KaliDurumYoneticisi.Gosterge(s); _kaliDurum.Text = yazi; _kaliDurum.ForeColor = renk switch { 0 => DesignTokens.Ozgun(TokenRol.StatusSuccess), 1 => DesignTokens.Ozgun(TokenRol.StatusWarning), 2 => DesignTokens.Ozgun(TokenRol.StatusDanger), _ => Soluk };
        _kaliAyrinti.Text = s.Durum == KaliDurumu.Bilinmiyor ? "Henüz kontrol edilmedi. \"Yeniden Dene\" ile hemen kontrol edebilirsiniz." : $"{s.Ayrinti}\r\nSon kontrol: {(s.Zaman == DateTime.MinValue ? "—" : s.Zaman.ToString("dd.MM.yyyy HH:mm:ss"))}";
        _tekrar.Enabled = s.Durum != KaliDurumu.Kontrol;
        var p = KaliDurumYoneticisi.SensorSec(SensorDeposu.Yukle()); _kaliParam.Visible = _uzman;
        if (_uzman) _kaliParam.Text = p == null ? "SSH parametreleri: sensör tanımlı değil." : $"SSH bağlantı parametreleri (salt okunur; değiştirmek için \"Sensör Ayarları\"):\r\n  Ad        : {p.Ad}\r\n  Adres     : {p.Host}:{p.Port}\r\n  Kullanıcı : {p.Kullanici}\r\n  Anahtar   : {p.AnahtarYolu}\r\n  Ajan      : sürüm {SensorAjani.Surum} (her bağlantıda stdin ile gönderilir, sensörde dosya bırakmaz)";
    }
    internal async Task TekrarDene() { _tekrar.Enabled = false; try { await KaliDurumYoneticisi.KontrolEtAsync(); } finally { if (!IsDisposed) KaliYenile(); } }

    // Sanal makineyi (VirtualBox) ONAYLA başlatır ve sensörün açılmasını bekler.
    internal async Task SanalMakineBaslat()
    {
        var vmler = await Task.Run(() => VirtualBoxYardimcisi.Bul()); if (vmler == null) { TemaliMesaj.Goster(this, "VirtualBox (VBoxManage) bulunamadı veya sanal makine listesi okunamadı.\r\n\r\nSanal makineyi VirtualBox'tan elle başlatın.", "Sanal makine başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var aday = KaliDurumYoneticisi.AdayMakineler(vmler); if (aday.Count == 0) { TemaliMesaj.Goster(this, "Kayıtlı sanal makine bulunamadı.", "Sanal makine yok", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var c = SoruFormu.Sor(this, "Sanal Makineyi Başlat", "Başlatılacak sanal makineyi seçin (adında \"kali\" geçen başta gelir).", "Devam", new Alan("Sanal makine", false, aday.Select(v => $"{v.Ad}  [{v.Durum}]").ToArray())); if (c == null) return; var vm = aday.First(v => $"{v.Ad}  [{v.Durum}]" == c[0]);
        if (vm.Durum.Contains("running", StringComparison.OrdinalIgnoreCase)) { TemaliMesaj.Goster(this, $"\"{vm.Ad}\" zaten çalışıyor görünüyor. Bağlantı sorunu başka bir nedenden olabilir (anahtar, adres, ağ). \"Yeniden Dene\" ile tekrar deneyin.", "Zaten çalışıyor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (TemaliMesaj.Goster(this, $"\"{vm.Ad}\" sanal makinesi arka planda (pencere açmadan) başlatılacak.\r\n\r\nBu bilgisayarın belleğini ve işlemcisini kullanır. Başlatılsın mı?", "Sanal makine başlatma onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _uyandir.Enabled = false; _tekrar.Enabled = false; _olay($"KALİ -> \"{vm.Ad}\" sanal makinesi başlatılıyor (kullanıcı onayıyla).");
        try
        {
            bool ok = await Task.Run(() => VirtualBoxYardimcisi.Baslat(vm.Uuid)); if (!ok) { TemaliMesaj.Goster(this, "Sanal makine başlatılamadı. VirtualBox'tan elle başlatmayı deneyin.", "Başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            for (int n = 1; n <= 12 && !IsDisposed; n++) { _kaliDurum.Text = $"Sanal makine başlatıldı, sensör bekleniyor ({n}/12)..."; _kaliDurum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); await Task.Delay(7000); await KaliDurumYoneticisi.KontrolEtAsync(); if (IsDisposed) return; if (KaliDurumYoneticisi.Son.Durum == KaliDurumu.Aktif) break; }
        }
        finally { if (!IsDisposed) { _uyandir.Enabled = true; KaliYenile(); } }
    }

    // =================== IP sızıntı sekmesi ===================
    internal void SizintiKur(Panel p)
    {
        var ust = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 118, FlowDirection = FlowDirection.TopDown, WrapContents = false }; _tunelBekle.ForeColor = Yazi; _kaliTest.ForeColor = Yazi; _sizBaslat.Text = "Testi Başlat..."; Stil(_sizBaslat, 170, DesignTokens.Ozgun(TokenRol.StatusSuccess)); _sizBaslat.Click += (_, _) => GuvenliCalistir.Baslat(() => SizintiCalistir(), "MerkezFormu");
        ust.Controls.AddRange(new Control[] { _kaliTest, _tunelBekle, new Panel { Height = 6, Width = 10 }, _sizBaslat });
        _sizDurum.Dock = DockStyle.Top; _sizDurum.Height = 40; _sizDurum.Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold); _sizDurum.Text = "Henüz test edilmedi";
        _sizSonuc.Multiline = true; _sizSonuc.ReadOnly = true; _sizSonuc.ScrollBars = ScrollBars.Vertical; _sizSonuc.Dock = DockStyle.Fill; _sizSonuc.BackColor = Kutu; _sizSonuc.ForeColor = Yazi; _sizSonuc.BorderStyle = BorderStyle.None; _sizSonuc.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular); _sizSonuc.TabStop = false;
        p.Controls.Add(_sizSonuc); p.Controls.Add(_sizDurum); p.Controls.Add(ust);
        p.Controls.Add(Aciklama("Bu test, bilgisayarınızın (isterseniz Kali sensörünün de) internete çıkarken dışarıdan nasıl göründüğünü ölçer: dış IP adresi, ülke/servis sağlayıcı, DNS isteklerinizi kimin gördüğü, IPv6 ve VPN/tünel işaretleri. Amaç, VPN veya tünel kullanırken gerçek kimliğinizin sızıp sızmadığını anlamaktır.\r\nGİZLİLİK: Test, halka açık servislere bağlanır ve servisler dış IP adresinizi görür. Her test onayınızla çalışır.", 96)); p.Controls.Add(Baslik("IP Sızıntı Testi"));
    }
    internal async Task SizintiCalistir()
    {
        if (TemaliMesaj.Goster(this, "Test şu halka açık servislere bağlanacak:\r\n\r\n  • " + string.Join("\r\n  • ", IpSizintiTesti.Hizmetler) + "\r\n\r\nBu servisler dış IP adresinizi görür. Başka hiçbir kişisel bilgi gönderilmez." + (_kaliTest.Checked ? "\r\n\r\nKali sensörü de aynı servislere KENDİ ağından bağlanacak." : "") + "\r\n\r\nTest çalıştırılsın mı?", "İnternete çıkan test: onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _sizBaslat.Enabled = false; _sizDurum.ForeColor = Soluk; _sizDurum.Text = "Test çalışıyor..."; _sizSonuc.Text = "";
        try
        {
            var win = await IpSizintiTesti.WindowsTestiAsync(new IpSizintiTesti.Kaynaklar(), CancellationToken.None); IpSizintiSonucu? kali = null; string kaliNot = "";
            if (_kaliTest.Checked)
            {
                var sensor = KaliDurumYoneticisi.SensorSec(SensorDeposu.Yukle());
                if (sensor == null) kaliNot = "Kali sensörü tanımlı değil; Kali tarafı atlandı.";
                else if (KaliDurumYoneticisi.Son.Durum != KaliDurumu.Aktif) kaliNot = "Kali sensörü şu an aktif değil; Kali tarafı atlandı (önce \"Kali Sensör Durumu\"ndan bağlantıyı düzeltin).";
                else { _sizDurum.Text = "Kali tarafı test ediliyor (1-2 dk sürebilir)..."; try { kali = await IpSizintiTesti.KaliTestiAsync(sensor, CancellationToken.None); } catch (Exception ex) { kaliNot = "Kali testi başarısız: " + ex.Message.Split('\n')[0]; } }
            }
            var d = IpSizintiTesti.Degerlendir(win, kali, _tunelBekle.Checked); IpSizintiTesti.Kaydet(win, kali, d);
            _sizDurum.Text = d.Kisa; _sizDurum.ForeColor = d.Durum switch { SizintiDurumu.Temiz => DesignTokens.Ozgun(TokenRol.StatusSuccess), SizintiDurumu.Sizinti => DesignTokens.Ozgun(TokenRol.StatusDanger), SizintiDurumu.Dikkat => DesignTokens.Ozgun(TokenRol.StatusWarning), _ => Soluk };
            _sizSonuc.Text = string.Join("\r\n\r\n", d.Maddeler.Select(x => "• " + x).Concat(kaliNot.Length > 0 ? new[] { "• " + kaliNot } : Array.Empty<string>())) + $"\r\n\r\nTest zamanı: {win.Zaman:dd.MM.yyyy HH:mm:ss}";
            _olay($"IP SIZINTI TESTİ -> {d.Kisa}");
        }
        catch (Exception ex) { _sizDurum.Text = "Test başarısız"; _sizDurum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _sizSonuc.Text = ex.Message; }
        finally { if (!IsDisposed) _sizBaslat.Enabled = true; }
    }

    // =================== Bildirim sekmesi ===================
    internal void BildirimKur(Panel p)
    {
        var ic = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        foreach (var t in new[] { _tgToken, _dcUrl, _tmUrl, _gnUrl }) { Kutuyu(t); t.UseSystemPasswordChar = true; } Kutuyu(_tgChat, 240); _tgToken.PlaceholderText = "123456789:ABC... (BotFather'dan)"; _tgChat.PlaceholderText = "-1001234567890 veya @kanaladi"; _dcUrl.PlaceholderText = "https://discord.com/api/webhooks/..."; _tmUrl.PlaceholderText = "https://....logic.azure.com/... (Teams Workflows)"; _gnUrl.PlaceholderText = "https://... (Slack/diğer)";
        foreach (var c in new[] { _tgEtkin, _dcEtkin, _tmEtkin, _gnEtkin, _ayrintili, _goster }) { c.ForeColor = Yazi; c.Margin = new Padding(0, 10, 0, 0); }
        _goster.CheckedChanged += (_, _) => { foreach (var t in new[] { _tgToken, _dcUrl, _tmUrl, _gnUrl }) t.UseSystemPasswordChar = !_goster.Checked; };
        Button TestDugmesi(KanalTuru k) { var b = new EfektliButon { Text = "Test Mesajı Gönder" }; Stil(b, 180, DesignTokens.Ozgun(TokenRol.SurfaceElevated)); b.Margin = new Padding(0, 6, 0, 0); b.Click += (_, _) => GuvenliCalistir.Baslat(() => TestGonder(k, b), "MerkezFormu"); return b; }
        _esik.Items.AddRange(new object[] { "Yalnızca KRİTİK olaylar", "Uyarı ve KRİTİK olaylar" }); _esik.SelectedIndex = 0;
        var kaydet = new EfektliButon { Text = "Ayarları Kaydet" }; Stil(kaydet, 180, DesignTokens.Ozgun(TokenRol.StatusSuccess)); kaydet.Margin = new Padding(0, 14, 0, 0); kaydet.Click += (_, _) => BildirimKaydet();
        _bilDurum.AutoSize = true; _bilDurum.ForeColor = Soluk; _bilDurum.Margin = new Padding(0, 8, 0, 0); _bilDurum.UseMnemonic = false;
        ic.Controls.AddRange(new Control[] { _tgEtkin, Etiket("Bot anahtarı (Token)"), _tgToken, Etiket("Sohbet kimliği (Chat ID)"), _tgChat, TestDugmesi(KanalTuru.Telegram), _dcEtkin, Etiket("Discord webhook adresi"), _dcUrl, TestDugmesi(KanalTuru.Discord), _tmEtkin, Etiket("Teams webhook adresi"), _tmUrl, TestDugmesi(KanalTuru.Teams), _gnEtkin, Etiket("Genel (Slack vb.) webhook adresi"), _gnUrl, TestDugmesi(KanalTuru.Genel), Etiket("Hangi olaylar gönderilsin?"), _esik, _ayrintili, _goster, kaydet, _bilDurum });
        // Uzman: webhook yükü önizlemesi.
        if (_uzman)
        {
            _onizKanal.Items.AddRange(new object[] { "Telegram", "Discord", "Teams", "Genel" }); _onizKanal.SelectedIndex = 0; _onizKanal.SelectedIndexChanged += (_, _) => Onizle();
            _onizleme.Multiline = true; _onizleme.ReadOnly = true; _onizleme.ScrollBars = ScrollBars.Both; _onizleme.WordWrap = false; _onizleme.Width = 640; _onizleme.Height = 230; _onizleme.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _onizleme.ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary); _onizleme.Font = FontProvider.Al(FontRol.Mono); _onizleme.BorderStyle = BorderStyle.FixedSingle;
            ic.Controls.Add(Etiket("Webhook JSON yükü önizlemesi (örnek olay; gizli alanlar maskelenir) — kanal:")); ic.Controls.Add(_onizKanal); ic.Controls.Add(_onizleme);
            foreach (var c in new Control[] { _tgChat, _dcUrl, _tmUrl, _gnUrl, _ayrintili }) { if (c is TextBox t) t.TextChanged += (_, _) => Onizle(); else if (c is CheckBox cb) cb.CheckedChanged += (_, _) => Onizle(); }
        }
        p.Controls.Add(ic); p.Controls.Add(Aciklama("Kritik bir olay olunca (şüpheli giriş, Kali sensörünün kopması, kritik açık, USB uyarısı, IP sızıntısı...) kendi kanalınıza kısa bir mesaj gider. Anahtar ve adresler yalnızca bu Windows hesabıyla çözülebilen biçimde saklanır. Aynı olay 5 dakikada bir, toplamda saatte en çok 20 kez gönderilir.", 66)); p.Controls.Add(Baslik("Bildirim Kanalları"));
    }
    internal void BildirimYukle()
    {
        var a = WebhookYoneticisi.Yukle(); _tgEtkin.Checked = a.Telegram.Etkin; _tgToken.Text = a.Telegram.Hedef; _tgChat.Text = a.Telegram.Ek; _dcEtkin.Checked = a.Discord.Etkin; _dcUrl.Text = a.Discord.Hedef; _tmEtkin.Checked = a.Teams.Etkin; _tmUrl.Text = a.Teams.Hedef; _gnEtkin.Checked = a.Genel.Etkin; _gnUrl.Text = a.Genel.Hedef; _esik.SelectedIndex = a.EnDusukSeviye == "Uyarı" ? 1 : 0; _ayrintili.Checked = a.Ayrintili; if (_uzman) Onizle();
        var (yazi, _) = WebhookYoneticisi.Gosterge(); _bilDurum.Text = "Şu an: " + yazi;
    }
    // Ekrandaki alanlardan ayar nesnesi.
    internal BildirimAyari Ayar() => new(new(_tgEtkin.Checked, _tgToken.Text.Trim(), _tgChat.Text.Trim()), new(_dcEtkin.Checked, _dcUrl.Text.Trim(), ""), new(_tmEtkin.Checked, _tmUrl.Text.Trim(), ""), new(_gnEtkin.Checked, _gnUrl.Text.Trim(), ""), _esik.SelectedIndex == 1 ? "Uyarı" : "Kritik", _ayrintili.Checked);
    internal void BildirimKaydet()
    {
        var hata = WebhookYoneticisi.Kaydet(Ayar()); if (hata != null) { TemaliMesaj.Goster(this, hata, "Kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var (yazi, _) = WebhookYoneticisi.Gosterge(); _bilDurum.Text = "Kaydedildi. Şu an: " + yazi; _olay("BİLDİRİM -> Bildirim kanalı ayarları kaydedildi."); Bildirim.Goster("Bildirim ayarları kaydedildi", yazi, BildirimTuru.Basari);
    }
    // Test mesajı: ONAY ister (mesaj kullanıcının kendi kanalına gider ama internete çıkar).
    internal async Task TestGonder(KanalTuru k, Button b)
    {
        var a = Ayar().Al(k); if (!a.Etkin) a = a with { Etkin = true };
        var hata = WebhookYoneticisi.Gecerli(k, a); if (hata != null) { TemaliMesaj.Goster(this, hata, "Ayar eksik veya hatalı", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (TemaliMesaj.Goster(this, $"{k} kanalınıza bir TEST mesajı gönderilecek (internete çıkar). Gönderilsin mi?", "Test mesajı onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        b.Enabled = false; try { var r = await WebhookYoneticisi.TestGonderAsync(k, a, _ayrintili.Checked); _bilDurum.Text = r.Mesaj; _bilDurum.ForeColor = r.Ok ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusDanger); } finally { if (!IsDisposed) b.Enabled = true; }
    }
    // Yük önizlemesi (gizli değerler maskeli).
    internal void Onizle()
    {
        if (!_uzman) return; var a = Ayar(); var k = (KanalTuru)Math.Max(0, _onizKanal.SelectedIndex); var o = new BildirimOlayi("Giriş saldırısı şüphesi", "Kritik", "Şüpheli giriş: 192.168.1.20 adresinden 5 hatalı deneme.", "Nöbetçi", DateTime.Now); var kanal = a.Al(k);
        string adres = k == KanalTuru.Telegram ? $"POST https://api.telegram.org/bot{(kanal.Hedef.Contains(':') ? kanal.Hedef[..kanal.Hedef.IndexOf(':')] : "<token>")}:***/sendMessage" : "POST " + (kanal.Hedef.Length > 0 ? WebhookYoneticisi.Maske(k, kanal) : "<webhook adresi>");
        string json = WebhookYoneticisi.Yuk(k, kanal, o, a.Ayrintili); try { json = JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "MerkezFormu.Onizle"); }
        _onizleme.Text = adres + "\r\nContent-Type: application/json\r\n\r\n" + json.Replace("\n", "\r\n");
    }

    // =================== Kali konsol sekmesi ===================
    internal void KonsolKur(Panel p)
    {
        _konsol.Multiline = true; _konsol.ReadOnly = true; _konsol.ScrollBars = ScrollBars.Both; _konsol.WordWrap = false; _konsol.Dock = DockStyle.Fill; _konsol.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _konsol.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _konsol.Font = FontProvider.Al(FontRol.Mono); _konsol.BorderStyle = BorderStyle.None; _konsol.TabStop = false;
        var alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(0, 8, 0, 0) }; var tem = new EfektliButon { Text = "Temizle" }; var kop = new EfektliButon { Text = "Kopyala" }; Stil(tem, 120); Stil(kop, 120); alt.Controls.AddRange(new Control[] { tem, kop });
        tem.Click += (_, _) => { KaliKonsol.Temizle(); _konsol.Clear(); }; kop.Click += (_, _) => { try { Clipboard.SetText(_konsol.Text); Bildirim.Goster("Kopyalandı", "Konsol çıktısı panoya kopyalandı.", BildirimTuru.Basari); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "MerkezFormu.KonsolKur"); } };
        p.Controls.Add(_konsol); p.Controls.Add(alt); p.Controls.Add(Aciklama("Sensörde çalışan komutların (ssh, python ajanı, nmap) anlık çıktıları. Sensörden gelen veriler güvenilmez kabul edilir: denetim karakterleri temizlenir, uzun satırlar kısaltılır. Parametre yükleri (şifreli) gösterilmez.", 52)); p.Controls.Add(Baslik("Kali Konsol"));
    }

    // =================== Kali Araç Rehberi sekmesi ===================
    internal readonly ListBox _rListe = new(); private readonly TextBox _rAra = new(), _rDetay = new(), _rCikti = new(); private readonly Button _rCalistir = new EfektliButon(), _rDurdur = new EfektliButon(); private KaliAraci? _rSecili; private CancellationTokenSource? _rCts;

    internal void RehberKur(Panel p)
    {
        // Sol: arama + liste.
        var sol = new Panel { Dock = DockStyle.Left, Width = 300, Padding = new Padding(0, 0, 10, 0) };
        _rAra.PlaceholderText = "Araç ara (ad veya konu)..."; Kutuyu(_rAra, 290); _rAra.Dock = DockStyle.Top; _rAra.TextChanged += (_, _) => RehberListele();
        _rListe.Dock = DockStyle.Fill; _rListe.BackColor = Kutu; _rListe.ForeColor = Yazi; _rListe.BorderStyle = BorderStyle.None; _rListe.Font = FontProvider.Al(FontRol.Body); _rListe.IntegralHeight = false; _rListe.FormattingEnabled = true;
        _rListe.Format += (_, e) => { if (e.ListItem is KaliAraci a) e.Value = (a.Sinif == AracSinifi.Calistirilir ? "▶  " : a.Sinif == AracSinifi.Kapali ? "✕  " : "•  ") + a.Ad; };
        _rListe.SelectedIndexChanged += (_, _) => RehberSec(_rListe.SelectedItem as KaliAraci);
        sol.Controls.Add(_rListe); sol.Controls.Add(_rAra);
        // Sağ: ayrıntı, düğme, çıktı.
        _rDetay.Multiline = true; _rDetay.ReadOnly = true; _rDetay.ScrollBars = ScrollBars.Vertical; _rDetay.Dock = DockStyle.Top; _rDetay.Height = 250; _rDetay.BackColor = Kutu; _rDetay.ForeColor = Yazi; _rDetay.BorderStyle = BorderStyle.None; _rDetay.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular); _rDetay.TabStop = false;
        Stil(_rCalistir, 260); _rCalistir.Visible = false; _rCalistir.Click += (_, _) => GuvenliCalistir.Baslat(() => RehberCalistir(), "MerkezFormu");
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 8, 0, 0) }; serit.Controls.Add(_rCalistir); Stil(_rDurdur, 110, DesignTokens.Ozgun(TokenRol.StatusDanger)); _rDurdur.Text = "Durdur"; _rDurdur.Visible = false; _rDurdur.Click += (_, _) => _rCts?.Cancel(); serit.Controls.Add(_rDurdur);
        _rCikti.Multiline = true; _rCikti.ReadOnly = true; _rCikti.ScrollBars = ScrollBars.Both; _rCikti.WordWrap = false; _rCikti.Dock = DockStyle.Fill; _rCikti.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _rCikti.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _rCikti.Font = FontProvider.Al(FontRol.Mono); _rCikti.BorderStyle = BorderStyle.None; _rCikti.TabStop = false;
        var sag = new Panel { Dock = DockStyle.Fill }; sag.Controls.Add(_rCikti); sag.Controls.Add(serit); sag.Controls.Add(_rDetay);
        p.Controls.Add(sag); p.Controls.Add(sol);
        p.Controls.Add(Aciklama(KaliRehberi.Uyari + "\r\n▶ uygulamadan çalıştırılabilir (hedef doğrulama + onay ile)   • Kali'de elle kullanılır (çalıştırma henüz bağlı değil)   ✕ saldırı odaklı: çalıştırılmaz, yalnızca anlatılır ve savunması gösterilir.", 96)); p.Controls.Add(Baslik("Kali Araç Rehberi"));
        RehberListele();
    }

    // Aramaya göre listeyi doldurur.
    internal void RehberListele()
    {
        _rListe.BeginUpdate(); _rListe.Items.Clear(); foreach (var a in KaliRehberi.Ara(_rAra.Text)) _rListe.Items.Add(a); _rListe.EndUpdate(); if (_rListe.Items.Count > 0) _rListe.SelectedIndex = 0; else RehberSec(null);
    }

    // Seçili aracın ayrıntısını gösterir.
    internal void RehberSec(KaliAraci? a)
    {
        _rSecili = a; _rCikti.Clear(); _rCalistir.Visible = false;
        if (a == null) { _rDetay.Text = "Eşleşen araç yok."; return; }
        _rDetay.Text = $"{a.Ad}   —   {a.Kategori}\r\n{KaliRehberi.SinifAdi(a.Sinif)}\r\n\r\nNE İŞE YARAR\r\n{a.Ne}\r\n\r\nYASAL SINIR\r\n{a.Yasal}\r\n\r\nSAVUNMA (kendinizi nasıl korursunuz)\r\n{a.Savunma}" + (a.UygulamaKarsiligi != null ? $"\r\n\r\nBU UYGULAMADAKİ KARŞILIĞI\r\n{a.UygulamaKarsiligi}" : "");
        if (a.Sablonlar != null) { string bolum = "\r\n\r\nBU UYGULAMADAN ÇALIŞTIRMA\r\n" + string.Join("\r\n", KaliRehberi.SablonTanimlari(a).Select(t => "• " + RehberCalistirma.SablonBasligi(t))); int i = _rDetay.Text.IndexOf("\r\n\r\nNE İŞE YARAR", StringComparison.Ordinal); _rDetay.Text = i >= 0 ? _rDetay.Text.Insert(i, bolum) : _rDetay.Text + bolum; }
        if (a.Sinif == AracSinifi.Calistirilir) { _rCalistir.Visible = true; _rCalistir.Text = a.Sablonlar != null ? RehberCalistirma.DugmeMetni(KaliRehberi.SablonTanimlari(a)) : a.Calistir == null ? "Nmap Sekmesini Aç" : "Sensörde Çalıştır..."; }
    }

    // Seçili aracı sensörde çalıştırır (hedef doğrulama + komut önizleme + onay; varsayılan "Hayır").
    internal async Task RehberCalistir()
    {
        var a = _rSecili; if (a == null || a.Sinif != AracSinifi.Calistirilir) return;
        // Yetki: araç çalıştırmak için Analist veya Yönetici gerekir (İzleyici yalnızca görür).
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Arac)) { TemaliMesaj.Goster(this, "Araç çalıştırma için yetkiniz yok.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Arac), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Komut şablonu olan araçlar yeni yoldan çalışır (kali_araclari.json).
        if (a.Sablonlar != null) { await RehberSablonCalistir(a); return; }
        // Nmap kendi sekmesinde çalışır.
        if (a.Calistir == null) { Goster(SekmeNmap); return; }
        var t = a.Calistir.Value; var bilgi = SensorArac.Bilgi(t);
        // Sensör hazır mı?
        var sensor = KaliDurumYoneticisi.SensorSec(SensorDeposu.Yukle()); if (sensor == null || KaliDurumYoneticisi.Son.Durum != KaliDurumu.Aktif) { TemaliMesaj.Goster(this, "Bu araç Kali sensöründe çalışır, ama sensör şu an aktif görünmüyor.\r\n\r\n\"Kali Sensör Durumu\" sekmesinden bağlantıyı düzeltin.", "Sensör hazır değil", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Hedef.
        var c = SoruFormu.Sor(this, bilgi.Baslik, bilgi.Aciklama + "\r\n\r\nÖrnek: " + bilgi.Ornek, "Devam", new Alan(t == AracTuru.Searchsploit ? "Yazılım adı ve sürümü" : t == AracTuru.Whois ? "Alan adı" : "Hedef (özel ağ IP adresi)")); if (c == null) return; string hedef = c[0].Trim();
        var hata = SensorArac.HedefHatasi(t, hedef); if (hata != null) { TemaliMesaj.Goster(this, hata, "Hedef kabul edilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Onay.
        string onay = $"Sensörde şu komut çalıştırılacak:\r\n\r\n  {SensorArac.KomutOnizle(t, hedef)}\r\n\r\n" + (SensorArac.InternetKullanir(t) ? "Bu sorgu internete çıkar (herkese açık kayıt sunucuları).\r\n\r\n" : t == AracTuru.Searchsploit ? "Yalnızca yerel veri tabanında arama yapılır; hiçbir şey çalıştırılmaz.\r\n\r\n" : "Hedefe yalnızca denetim amaçlı, zararsız sorgular gönderilir; saldırı veya yük testi yapılmaz. Bu hedefi denetleme yetkiniz var mı?\r\n\r\n") + "Çalıştırılsın mı?";
        if (TemaliMesaj.Goster(this, onay, "Araç çalıştırma onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Çalıştır.
        _rCalistir.Enabled = false; _rCikti.Text = "Çalışıyor (en çok 3 dakika)..."; _olay($"KALİ ARAÇ -> {SensorArac.KomutOnizle(t, hedef)} (kullanıcı onayıyla)");
        try
        {
            var s = await SensorArac.CalistirAsync(sensor, t, hedef, CancellationToken.None); if (IsDisposed) return;
            _rCikti.Text = "$ " + s.Komut + "\r\n\r\n" + s.Cikti.Replace("\r\n", "\n").Replace("\n", "\r\n"); KaliKonsol.Yaz("arac", s.Komut);
        }
        catch (Exception ex) { if (!IsDisposed) _rCikti.Text = "Çalıştırılamadı: " + ex.Message; }
        finally { if (!IsDisposed) _rCalistir.Enabled = true; }
    }

    // Komut şablonu olan aracı sensörde çalıştırır: işlem seç → hedef/parametreleri sor → hedefi doğrula (izinli listede değilse yetki onayıyla eklenebilir) → ajandan TAM komutu önizle
    // → onay (varsayılan "Hayır") → çalıştır (kurulu değilse kurulum ayrıca onay ister) → sonucu göster ve olay günlüğüne yaz.
    internal async Task RehberSablonCalistir(KaliAraci a)
    {
        var tanimlar = KaliRehberi.SablonTanimlari(a);
        if (tanimlar.Count == 0) { TemaliMesaj.Goster(this, "Bu araç için tanımlı komut şablonu bulunamadı.", "Şablon yok", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Sensör hazır mı?
        var sensor = KaliDurumYoneticisi.SensorSec(SensorDeposu.Yukle()); if (sensor == null || KaliDurumYoneticisi.Son.Durum != KaliDurumu.Aktif) { TemaliMesaj.Goster(this, "Bu araç Kali sensöründe çalışır, ama sensör şu an aktif görünmüyor.\r\n\r\n\"Kali Sensör Durumu\" sekmesinden bağlantıyı düzeltin.", "Sensör hazır değil", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // 1) İşlem seçimi (araçta birden fazla şablon varsa).
        var t = tanimlar[0];
        if (tanimlar.Count > 1)
        {
            var sec = SoruFormu.Sor(this, a.Ad, "Ne yapmak istiyorsunuz?\r\n\r\n" + string.Join("\r\n", tanimlar.Select(x => "• " + RehberCalistirma.SablonBasligi(x))), "Devam", new Alan("İşlem", false, tanimlar.Select(x => x.Ad).ToArray())); if (sec == null) return;
            t = tanimlar.FirstOrDefault(x => x.Ad == sec[0]) ?? tanimlar[0];
        }
        // 2) Hedef ve parametreler.
        string hedef = ""; var girdiler = new Dictionary<string, string>(); var alanlar = RehberCalistirma.Alanlar(t);
        if (alanlar.Count > 0) { var c = SoruFormu.Sor(this, a.Ad + " — " + t.Ad, t.Aciklama ?? "", "Devam", alanlar.Select(x => new Alan(x.Etiket, false, x.Secenek)).ToArray()); if (c == null) return; (hedef, girdiler) = RehberCalistirma.Girdiler(t, c); }
        // 3) Hedef doğrulama: izinli listede değilse, kullanıcı "yazılı yetkim var" derse listeye eklenir (kalıcı, olay günlüğüne yazılır).
        IzinliHedefler.Hazirla(); try { KomutKaydi.Hazirla(); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { _rCikti.Text = "Araç tablosu yüklenemedi: " + ex.Message; return; }
        var sab = KomutSablonuDeposu.Getir(t.Ad); if (sab == null) { _rCikti.Text = "Şablon kayıtlı değil: " + t.Ad; return; }
        var he = KomutCalistirici.HedefHatasiBul(sab, hedef);
        if (he != null && he.Contains("izinli hedef listesinde değil", StringComparison.Ordinal))
        {
            if (TemaliMesaj.Goster(this, $"\"{hedef}\" izinli hedef listenizde değil.\r\n\r\nBu hedefin sahibi siz misiniz ya da bu hedefi denetlemek için YAZILI yetkiniz var mı?\r\n\r\nEvet derseniz hedef listeye eklenir ve olay günlüğüne yazılır:\r\n{IzinliHedefler.Yol}", "İzinli hedef", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try { IzinliHedefler.Ekle(hedef); } catch (ArgumentException ex) { TemaliMesaj.Goster(this, ex.Message, "Hedef eklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            he = KomutCalistirici.HedefHatasiBul(sab, hedef);
        }
        if (he != null) { TemaliMesaj.Goster(this, he, "Hedef kabul edilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // 4) Sensörde gerçekten çalışacak komutu ajandan al (parametreler de burada doğrulanır).
        string komut; _rCalistir.Enabled = false;
        try { _rCikti.Text = "Komut doğrulanıyor..."; komut = await KomutCalistirici.OnizleAsync(sensor, t.Ad, hedef, girdiler, CancellationToken.None); }
        catch (Exception ex) { if (!IsDisposed) { _rCikti.Text = "Çalıştırılamadı: " + ex.Message; _rCalistir.Enabled = true; } return; }
        if (IsDisposed) return; _rCalistir.Enabled = true;
        // 5) Onay (varsayılan "Hayır").
        if (TemaliMesaj.Goster(this, RehberCalistirma.OnayMetni(t, sensor.Ad, hedef, komut), "Araç çalıştırma onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) { _rCikti.Clear(); return; }
        // 6) Çalıştır.
        _rCalistir.Enabled = false; _rDurdur.Visible = true; _rCts = new CancellationTokenSource(); var jeton = _rCts.Token; _rCikti.Text = $"Çalışıyor (en çok {t.ZamanAsimi} sn)...\r\n$ {komut}";
        _olay($"KALİ ARAÇ -> {t.Ad} hedef={(hedef.Length > 0 ? hedef : "-")} (kullanıcı onayıyla)");
        try
        {
            var r = await KomutCalistirici.CalistirAsync(sensor, t.Ad, hedef, girdiler, q => (bool)Invoke(new Func<bool>(() => TemaliMesaj.Goster(this, q, "Araç kurulumu", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)), jeton); if (IsDisposed) return;
            _rCikti.Text = RehberCalistirma.SonucMetni(r); KaliKonsol.Yaz("arac", r.Komut); _olay($"KALİ ARAÇ BİTTİ -> {t.Ad}: {RehberCalistirma.DurumSatiri(r)}");
        }
        catch (OperationCanceledException) { if (!IsDisposed) _rCikti.Text = "Durduruldu."; _olay($"KALİ ARAÇ DURDURULDU -> {t.Ad}"); }
        catch (Exception ex) { if (!IsDisposed) _rCikti.Text = "Çalıştırılamadı: " + ex.Message; }
        finally { if (!IsDisposed) { _rCalistir.Enabled = true; _rDurdur.Visible = false; } _rCts?.Dispose(); _rCts = null; }
    }

    // =================== Nmap sekmesi ===================
    internal void NmapKur(Panel p)
    {
        _nProfil.Items.AddRange(new object[] { "Hızlı (100 kapı)", "Standart (1000 kapı)" }); _nProfil.SelectedIndex = 0; _nZam.Items.AddRange(new object[] { "2 — Yavaş / sessiz", "3 — Normal", "4 — Hızlı" }); _nZam.SelectedIndex = 1; Kutuyu(_nHedef, 220);
        foreach (var c in new Control[] { _nHedef, _nProfil, _nZam }) { if (c is ComboBox cb) { cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Giris; cb.ForeColor = Yazi; } }
        _nHedef.TextChanged += (_, _) => NmapOnizle(); _nProfil.SelectedIndexChanged += (_, _) => NmapOnizle(); _nZam.SelectedIndexChanged += (_, _) => NmapOnizle();
        _nOniz.AutoSize = false; _nOniz.Size = new Size(900, 70); _nOniz.Font = FontProvider.Al(FontRol.Mono); _nOniz.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _nOniz.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _nOniz.Padding = new Padding(10, 8, 10, 0); _nOniz.UseMnemonic = false; _nUyari.AutoSize = false; _nUyari.Size = new Size(900, 60); _nUyari.UseMnemonic = false;
        var ac = new EfektliButon { Text = "Ağ Haritası'nda Tara..." }; Stil(ac, 220); ac.Click += (_, _) => _haritaAc();
        var ic = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        ic.Controls.AddRange(new Control[] { Etiket("Hedef (yalnızca özel ağ IPv4 adresi veya en çok /24 aralığı)"), _nHedef, Etiket("Profil"), _nProfil, Etiket("Zamanlama (-T)"), _nZam, Etiket("Sensörde çalışacak komut (BİREBİR; ajandaki sabit argüman listesi)"), _nOniz, _nUyari, new Panel { Height = 8, Width = 10 }, ac });
        p.Controls.Add(ic); p.Controls.Add(Aciklama("Komut, Windows'tan sensöre SSH ile (stdin üzerinden gönderilen ajan aracılığıyla) iletilir; sonuç JSON olarak geri akar. GÜVENLİK: serbest nmap argümanı girilemez; yalnızca profil ve zamanlama seçilir, böylece sensörde keyfi komut çalıştırılamaz. Dış ağ (internet) adreslerine nmap gönderilmez; internet hedeflerinde otomatik olarak pasif HTTP/TLS gözlemine geçilir.", 82)); p.Controls.Add(Baslik("Nmap Komut Önizlemesi"));
    }
    internal void NmapOnizle()
    {
        var prof = _nProfil.SelectedIndex == 1 ? NmapProfili.Standart : NmapProfili.Hizli; int zam = _nZam.SelectedIndex + 2; _nOniz.Text = SensorNmap.KomutOnizle(prof, _nHedef.Text, zam); var h = SensorNmap.HedefHatasi(_nHedef.Text);
        _nUyari.Text = h == null ? "Hedef geçerli: kapsam kuralına uyuyor (özel ağ)." : "Bu hedefle nmap çalıştırılamaz: " + h; _nUyari.ForeColor = h == null ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusWarning);
    }
}
