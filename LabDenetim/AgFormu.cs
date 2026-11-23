// Bu dosya "Sanal Ağları Tara" penceresini çizer: ağdaki cihazları listeler ve seçileni hedef adrese yazdırır.
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Ağ tarama penceresi.
internal sealed class AgFormu : TemaliForm
{
    // Kullanıcının seçtiği IP adresi (seçim yapılmadıysa boş).
    public string? SecilenIp { get; private set; }
    // Bulunan cihazların listelendiği tablo.
    private readonly DataGridView _tablo = new();
    // Durum yazısı ve uyarı yazısı.
    private readonly Label _durum, _uyari;
    // İlerleme çubuğu.
    private readonly ProgressBar _cubuk;
    // Seç, yeniden tara ve kapat düğmeleri.
    private readonly Button _sec, _tara, _kapat;
    // Taramayı durdurmak için şalter.
    private CancellationTokenSource? _iptal;
    // Şu an tarama sürüyor mu?
    private bool _taramada;
    // Ana yerleşim ızgarası (uyarı satırının yüksekliğini sonradan değiştirmek için saklanır).
    private TableLayoutPanel? _tbl;

    // Uyarı kutusunun satır yüksekliğini ayarlar (0 = gizli, 104 = uyarı görünür).
    private void UyariYuksekligi(float yukseklik)
    {
        // Izgara henüz kurulmadıysa bir şey yapma.
        if (_tbl == null) return;
        // Satırı sabit yükseklikli yap.
        _tbl.RowStyles[4].SizeType = SizeType.Absolute;
        // Yüksekliği ver.
        _tbl.RowStyles[4].Height = yukseklik;
    }

    // Pencereyi kurar.
    public AgFormu()
    {
        // Pencere başlığı.
        Text = "◈ İç Ağ Keşfi — Ağdaki Cihazlar";
        // Boyut ve konum.
        ClientSize = new Size(880, 560); StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(760, 480);
        // Koyu tema.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // En üstteki açıklama yazısı.
        var aciklama = new Label { Text = "Bu liste, bilgisayarınızın bağlı olduğu ağlardaki (VirtualBox, VMware, Wi-Fi/Ethernet) açık cihazları gösterir. Sanal Kali makinenizi seçip \"Seç\" derseniz IP adresi otomatik olarak Hedef adres kutusuna yazılır.\r\nYalnızca KENDİ ağınızdaki ve kendi cihazlarınızdaki makineleri seçin; başkalarına ait cihazları izinsiz taramayın.", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8), ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted) };
        // Durum yazısı.
        _durum = new Label { Text = "Hazırlanıyor...", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Margin = Padding.Empty };
        // İlerleme çubuğu.
        _cubuk = new ProgressBar { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
        // Uyarı yazısı (başta gizli): sanal makine bulunamazsa gösterilir.
        _uyari = new Label { Dock = DockStyle.Fill, AutoSize = false, UseMnemonic = false, Visible = false, Padding = new Padding(10, 6, 10, 6), BackColor = DesignTokens.Ozgun(TokenRol.StatusWarningBg), ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning), Margin = new Padding(0, 8, 0, 0) };
        // Tabloyu hazırla.
        TabloKur();
        // Tablonun kaydırma çubukları koyu olsun.
        KoyuTema.UygulaTablo(_tablo);
        // Düğmeler.
        _sec = Dugme("✓ Seç ve Hedef Adrese Yaz", DesignTokens.Ozgun(TokenRol.AccentPrimary), Color.Black);
        // Yeniden tara düğmesi.
        _tara = Dugme("↻ Yeniden Tara", DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Kapat düğmesi.
        _kapat = Dugme("Kapat", DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Başta seç düğmesi kapalı.
        _sec.Enabled = false;
        // Düğme tıklamaları.
        _sec.Click += (_, _) => Sec();
        // Yeniden tara.
        _tara.Click += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "AgFormu");
        // Kapat.
        _kapat.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        // Tabloda satır seçilince "Seç" açılsın.
        _tablo.SelectionChanged += (_, _) => _sec.Enabled = !_taramada && _tablo.SelectedRows.Count > 0;
        // Çift tıklayınca da seçilsin.
        _tablo.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Sec(); };
        // Düğmeleri sağa yaslı dizen panel.
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        // Düğmeleri ekle (sağdan sola).
        dugmeler.Controls.AddRange(new Control[] { _kapat, _tara, _sec });
        // Ana ızgara: açıklama, durum, çubuk, tablo, uyarı, düğmeler.
        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(16, 14, 16, 14) };
        // Tek sütun.
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Satır yükseklikleri.
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        // Tablo satırı kalan yer, uyarı satırı içeriğe göre, düğmeler 52.
        tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 0)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        // Parçaları yerleştir.
        tbl.Controls.Add(aciklama, 0, 0); tbl.Controls.Add(_durum, 0, 1); tbl.Controls.Add(_cubuk, 0, 2); tbl.Controls.Add(_tablo, 0, 3); tbl.Controls.Add(_uyari, 0, 4); tbl.Controls.Add(dugmeler, 0, 5);
        // Izgarayı sonradan kullanmak için sakla.
        _tbl = tbl;
        // Ana ızgarayı pencereye koy.
        Controls.Add(tbl);
        // Pencere açılınca taramayı kendiliğinden başlat.
        Shown += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "AgFormu");
        // Pencere kapanırken taramayı durdur.
        FormClosing += (_, _) => _iptal?.Cancel();
    }

    // Tek bir düğmeyi koyu görünümle üretir.
    private static Button Dugme(string metin, Color zemin, Color yazi) => new EfektliButon()
    {
        // Yazı ve boyut.
        Text = metin, Size = new Size(metin.Length > 18 ? 230 : 140, 36), Margin = new Padding(8, 0, 0, 0),
        // Düz görünüm, renkler.
        FlatStyle = FlatStyle.Flat, BackColor = zemin, ForeColor = yazi, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand
    };

    // Tabloyu koyu temayla hazırlar.
    private void TabloKur()
    {
        // Genel ayarlar.
        _tablo.Dock = DockStyle.Fill; _tablo.ReadOnly = true; _tablo.AllowUserToAddRows = false; _tablo.RowHeadersVisible = false; _tablo.MultiSelect = false;
        // Satır seçimi ve sütun dağılımı.
        _tablo.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _tablo.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Koyu renkler.
        _tablo.BackgroundColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.BorderStyle = BorderStyle.None; _tablo.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        // Başlık satırı.
        _tablo.EnableHeadersVisualStyles = false; _tablo.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; _tablo.ColumnHeadersHeight = 32;
        // Başlık renkleri.
        _tablo.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _tablo.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Satır renkleri.
        _tablo.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Seçili satır renkleri.
        _tablo.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.DefaultCellStyle.SelectionForeColor = Color.White; _tablo.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        // Aralıklı satır rengi.
        _tablo.AlternatingRowsDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); _tablo.AlternatingRowsDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Sütunlar.
        _tablo.Columns.Add("Ip", "IP Adresi"); _tablo.Columns.Add("Tur", "Cihaz Türü"); _tablo.Columns.Add("Ad", "Ad"); _tablo.Columns.Add("Mac", "MAC Adresi"); _tablo.Columns.Add("Ag", "Hangi Ağda?");
        // Sütun ağırlıkları.
        _tablo.Columns[0].FillWeight = 70; _tablo.Columns[1].FillWeight = 110; _tablo.Columns[2].FillWeight = 80; _tablo.Columns[3].FillWeight = 85; _tablo.Columns[4].FillWeight = 120;
    }

    // Ağı tarar ve sonuçları tabloya yazar.
    private async Task TaraAsync()
    {
        // Zaten taranıyorsa bir şey yapma.
        if (_taramada) return;
        // Tarama başladı.
        _taramada = true; _iptal = new CancellationTokenSource();
        // Ekranı hazırla.
        _tablo.Rows.Clear(); _uyari.Visible = false; UyariYuksekligi(0); _sec.Enabled = false; _tara.Enabled = false;
        // Çubuğu hareketli yap.
        _cubuk.Style = ProgressBarStyle.Marquee; _cubuk.MarqueeAnimationSpeed = 30; _cubuk.Visible = true;
        // Hata olursa pencere çökmesin.
        try
        {
            // Durum yazısını güncelleyen yardımcı.
            var durum = new Progress<string>(s => _durum.Text = s);
            // Yüzde ilerlemesi: çubuk gerçek ilerlemeyi göstersin.
            var yuzde = new Progress<int>(v => { if (_cubuk.Style != ProgressBarStyle.Continuous) _cubuk.Style = ProgressBarStyle.Continuous; _cubuk.Value = Math.Clamp(v, 0, 100); });
            // Taramayı çalıştır.
            var liste = await AgKesfi.TaraAsync(durum, yuzde, _iptal.Token);
            // VirtualBox kuruluysa gerçek makineleri ve durumlarını öğren (kapalı makine "çalışıyor" gibi görünmesin).
            _durum.Text = "VirtualBox makineleri okunuyor..."; var vmler = await Task.Run(() => VirtualBoxYardimcisi.Bul()); var satirlar = VirtualBoxYardimcisi.Birlestir(liste, vmler);
            // Bulunanları tabloya ekle (satırın etiketine makine bilgisi konur).
            foreach (var s in satirlar) { int ri = _tablo.Rows.Add(s.Cihaz.Ip, s.Cihaz.Tur, s.Cihaz.Ad, s.Cihaz.Mac, s.Cihaz.Ag); _tablo.Rows[ri].Tag = s; if (s.Vm != null && !s.Vm.Calisiyor) _tablo.Rows[ri].DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted); }
            // Gerçekten ulaşılabilen sanal makine sayısı (kapalı olanlar sayılmaz).
            int sanal = satirlar.Count(s => s.Vm != null && s.Vm.Calisiyor && s.Cihaz.Ip != "—") + (vmler == null ? liste.Count(c => c.Tur.Contains("sanal makine")) : 0); int kapali = satirlar.Count(s => s.Vm != null && !s.Vm.Calisiyor);
            // Sonuç yazısı.
            _durum.Text = $"Tarama bitti: {liste.Count} cihaz bulundu ({sanal} çalışan sanal makine)" + (kapali > 0 ? $"; {kapali} kayıtlı makine kapalı (gri satırlar; seçerseniz başlatılır)." : ".");
            // Köşe bildirimi.
            Bildirim.Goster("Ağ taraması bitti", $"{liste.Count} cihaz bulundu ({sanal} çalışan sanal makine).", liste.Count > 0 ? BildirimTuru.Basari : BildirimTuru.Uyari);
            // Sanal makine yoksa kullanıcıya sade dille ne yapacağını söyle (kapalı makine varsa uyarı gerekmez).
            if (sanal == 0 && kapali == 0)
            {
                // Uyarı metni.
                _uyari.Text = "⚠ Sanal makinenize ulaşılamadı.\r\nVirtualBox'ta Kali'ye sağ tıklayıp Ayarlar → Ağ bölümünden \"Bağlı olduğu\" kutusunu \"Yalnızca Ana Makine (Host-Only)\" veya \"Köprü Bağdaştırıcısı (Bridged)\" yapın.\r\n(Sanal makine tamamen açılmış olmalı. \"NAT\" modunda ana bilgisayar sanal makineyi göremez. Değişiklikten sonra makineyi yeniden başlatıp \"Yeniden Tara\"ya basın.)";
                // Uyarı için yer aç ve uyarıyı göster.
                UyariYuksekligi(104); _uyari.Visible = true;
            }
            // Listede bir şey seçili olsun.
            if (_tablo.Rows.Count > 0) { _tablo.ClearSelection(); _tablo.Rows[0].Selected = true; }
        }
        // Kullanıcı pencereyi kapattı veya durdurdu.
        catch (OperationCanceledException) { _durum.Text = "Tarama durduruldu."; }
        // Başka bir sorun oldu.
        catch (Exception ex) { _durum.Text = "Tarama sırasında bir sorun oldu: " + ex.Message; }
        // Her durumda ekranı toparla.
        finally
        {
            // Tarama bitti.
            _taramada = false; _tara.Enabled = true; _cubuk.Visible = false;
            // Seç düğmesi satır seçiliyse açık olsun.
            _sec.Enabled = _tablo.SelectedRows.Count > 0;
        }
    }

    // Seçili satırın IP adresini alıp pencereyi kapatır. Kapalı bir VirtualBox makinesi seçildiyse (onayla) başlatıp IP'sini bekler.
    private void Sec() => GuvenliCalistir.Baslat(() => SecAsync(), "AgFormu.Sec");
    private async Task SecAsync()
    {
        // Seçili satır yoksa bir şey yapma.
        if (_tablo.SelectedRows.Count == 0) return;
        // Kapalı/IP'si olmayan makine satırı mı?
        if (_tablo.SelectedRows[0].Tag is AgSatiri { Vm: { } vm, Cihaz.Ip: "—" })
        {
            // Zaten çalışıyorsa IP bilinmiyor: yeniden taramayı öner.
            if (vm.Calisiyor) { TemaliMesaj.Goster($"\"{vm.Ad}\" çalışıyor ama ağda görünmüyor (NAT ağı olabilir). Makineye bir Host-Only veya Köprü ağ kartı ekleyin; sonra \"Yeniden Tara\".", "Ağda Görünmüyor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            // Onay (varsayılan Hayır): makineyi başlatmak bilgisayarın kaynaklarını kullanır.
            if (TemaliMesaj.Goster($"\"{vm.Ad}\" sanal makinesi şu an {vm.DurumYazi.ToLowerInvariant()}.\r\n\r\nVirtualBox ile BAŞLATILSIN mı? Açılınca IP adresi otomatik bulunup hedef adrese yazılır (1-2 dakika sürebilir).", "Sanal Makineyi Başlat", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            // Başlat ve IP bekle.
            _sec.Enabled = false; _tara.Enabled = false; _durum.Text = $"\"{vm.Ad}\" başlatılıyor..."; _cubuk.Style = ProgressBarStyle.Marquee; _cubuk.Visible = true;
            try
            {
                // Başlatma komutu.
                if (!await Task.Run(() => VirtualBoxYardimcisi.Baslat(vm.Uuid))) { _durum.Text = "Makine başlatılamadı. VirtualBox'ı açıp elle başlatmayı deneyin."; return; }
                // En çok 120 sn: konuk IP'si (Guest Additions) veya ARP'de makinenin MAC'i görünene kadar bekle.
                string? ip = null; for (int i = 0; i < 60 && ip == null && !IsDisposed; i++) { _durum.Text = $"\"{vm.Ad}\" açılıyor, IP bekleniyor... ({i * 2} sn)"; await Task.Delay(2000); ip = (await Task.Run(() => VirtualBoxYardimcisi.IpBul(vm.Uuid))).FirstOrDefault(x => LabKapsam.IpLabdaMi(System.Net.IPAddress.Parse(x)) && !x.StartsWith("10.0.2.")); }
                // Bulunamadıysa bilgi ver.
                if (ip == null) { _durum.Text = "Makine başladı ama IP henüz bulunamadı. Birkaç saniye bekleyip \"Yeniden Tara\"ya basın."; return; }
                // Bulundu: hedefe yaz.
                SecilenIp = ip; DialogResult = DialogResult.OK; Close();
            }
            // Düğmeleri toparla.
            finally { if (!IsDisposed) { _tara.Enabled = true; _cubuk.Visible = false; _sec.Enabled = _tablo.SelectedRows.Count > 0; } }
            return;
        }
        // IP adresini al.
        SecilenIp = _tablo.SelectedRows[0].Cells[0].Value?.ToString();
        // "Tamam" olarak kapat.
        DialogResult = DialogResult.OK; Close();
    }
}
