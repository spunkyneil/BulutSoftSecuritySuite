// Bu dosya "Ağ Haritası" penceresini çizer: sensörde nmap sürüm taraması yaptırır, bulunan cihazları ve açık hizmetleri listeler, haritayı dosyada saklar.
// Tarama ağa istek gönderir: her seferinde onay istenir (varsayılan "Hayır"), hedef yalnızca özel ağ olabilir. Varlık ağırlığı (0-1) tablodan düzenlenebilir.
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Ağ Haritası penceresi.
internal sealed class AgHaritasiFormu : SayfaDenetimi
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Kontroller.
    private readonly DataGridView _t = new(); private readonly Label _durum = new(), _ayrinti = new(); private readonly Button _tara = new EfektliButon(), _iptal = new EfektliButon(), _yerel = new EfektliButon(), _katalog = new EfektliButon(), _web = new EfektliButon(), _kev = new EfektliButon(), _nvd = new EfektliButon();
    private readonly ComboBox _sensor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 }, _profil = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly TextBox _hedef = new() { Width = 150, Text = "192.168.56.0/24" };
    // Veri.
    private readonly Button _rapor = new EfektliButon(), _anaRapor = new EfektliButon(), _kalite = new EfektliButon(); private readonly Action<List<Bulgu>>? _anaListe; private readonly Action<string>? _pasifBaslat;
    private readonly List<SensorProfili> _sensorler = SensorDeposu.Yukle(); private NetworkMap? _harita = AssetStore.Load(); private readonly Action<string> _gunluk; private CancellationTokenSource? _cts; private bool _doluyor;

    // Pencereyi kurar.
    public AgHaritasiFormu(Action<string> gunluk, Action<List<Bulgu>>? anaListeyeEkle = null, Action<string>? pasifGozlemBaslat = null)
    {
        // Günlük ve ana pencerenin sonuç tablosuna ekleme kancası.
        _gunluk = gunluk; _anaListe = anaListeyeEkle; _pasifBaslat = pasifGozlemBaslat;
        // Pencere.
        Text = "Ağ Haritası"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1180, 660); MinimumSize = new Size(980, 520); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Açıklama.
        var acik = new Label { Dock = DockStyle.Top, Height = 56, Padding = new Padding(14, 10, 14, 0), ForeColor = Soluk, UseMnemonic = false, Text = "Sensör sanal makinesinde nmap ile cihazları ve açık hizmetleri (ürün, sürüm) bulur. Yalnızca özel ağ taranır. Her cihazın Önem Ağırlığı (0-1) risk puanını etkiler; tablodan değiştirebilirsiniz." };
        // Sensör ve profil seçenekleri.
        foreach (var s in _sensorler) _sensor.Items.Add(s.Ad); if (_sensor.Items.Count > 0) _sensor.SelectedIndex = 0;
        _profil.Items.AddRange(new object[] { "Hızlı (100 kapı)", "Standart (1000 kapı)" }); _profil.SelectedIndex = 0;
        // Giriş kutuları koyu zeminde okunur olsun (yazı rengi açıkça verilir).
        _hedef.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); _hedef.ForeColor = Yazi; _hedef.BorderStyle = BorderStyle.FixedSingle;
        foreach (var c in new[] { _sensor, _profil }) { c.FlatStyle = FlatStyle.Flat; c.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); c.ForeColor = Yazi; }
        // Düğmeler.
        _tara.Text = "▶ Sensörden Tara"; _tara.Size = new Size(160, 30); _iptal.Text = "Durdur"; _iptal.Size = new Size(80, 30); _iptal.Enabled = false;
        foreach (var b in new[] { _tara, _iptal }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); }
        // Şerit.
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 6, 0, 0), WrapContents = false };
        Label E(string t) => new() { Text = t, AutoSize = true, ForeColor = Soluk, Margin = new Padding(0, 7, 6, 0), UseMnemonic = false };
        _sensor.Margin = _profil.Margin = _hedef.Margin = new Padding(0, 3, 14, 0);
        serit.Controls.AddRange(new Control[] { E("Sensör"), _sensor, E("Hedef"), _hedef, E("Profil"), _profil, _tara, _iptal });
        // İkinci şerit: bu bilgisayarın yerel denetimi ve yama kataloğu.
        _yerel.Text = "▶ Bu Bilgisayarı Denetle"; _yerel.Size = new Size(190, 30); _katalog.Text = "KB Kataloğu Yükle..."; _katalog.Size = new Size(160, 30); _web.Text = "Aktif Web Denetimi..."; _web.Size = new Size(180, 30); _kev.Text = "KEV Listesini İndir..."; _kev.Size = new Size(170, 30); _nvd.Text = "NVD Verisi Yükle..."; _nvd.Size = new Size(165, 30);
        foreach (var b in new[] { _yerel, _katalog, _web, _kev, _nvd }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); }
        var serit2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 2, 0, 0), WrapContents = false };
        serit2.Controls.AddRange(new Control[] { _yerel, _katalog, _web, _kev, _nvd }); serit2.WrapContents = false; serit2.Height = 40;
        // Üçüncü şerit: raporlama ve kalibrasyon.
        _rapor.Text = "Derin Denetim Raporu..."; _rapor.Size = new Size(210, 30); _anaRapor.Text = "Ana Rapora Ekle"; _anaRapor.Size = new Size(170, 30); _kalite.Text = "Kural Kalitesi..."; _kalite.Size = new Size(160, 30);
        foreach (var b in new[] { _rapor, _anaRapor, _kalite }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); }
        var serit3 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 2, 0, 0), WrapContents = false }; serit3.Controls.AddRange(new Control[] { _rapor, _anaRapor, _kalite });
        _rapor.Click += (_, _) => RaporYaz(); _anaRapor.Click += (_, _) => AnaRaporaEkle(); _kalite.Click += (_, _) => { using var f = new KuralKalitesiFormu(FpDeposu.Yukle()); f.ShowDialog(this); };
        _yerel.Click += (_, _) => GuvenliCalistir.Baslat(() => YerelDenetleAsync(), "AgHaritasiFormu"); _katalog.Click += (_, _) => KatalogYukle(); _web.Click += (_, _) => GuvenliCalistir.Baslat(() => AktifWebAsync(), "AgHaritasiFormu"); _kev.Click += (_, _) => GuvenliCalistir.Baslat(() => KevIndirAsync(), "AgHaritasiFormu"); _nvd.Click += (_, _) => GuvenliCalistir.Baslat(() => NvdYukleAsync(), "AgHaritasiFormu");
        // Fare açıklamaları.
        var ipucu = new ToolTip { AutoPopDelay = 14000 };
        ipucu.SetToolTip(_yerel, "Bu bilgisayarı yalnızca OKUYARAK denetler: port-hizmet eşlemesi, hizmet yolları, imzalar, yama durumu. Sistemde hiçbir şey değiştirmez.");
        ipucu.SetToolTip(_web, "SADECE İÇ AĞDAKİ bir web sunucusunu kontrollü sondalarla denetler (GET/OPTIONS/TRACE; en çok 300 istek, ~5 istek/sn). Dış ağ adresleri kod seviyesinde engellidir. Her denetimde onay istenir.");
        ipucu.SetToolTip(_rapor, "Son denetim bulgularından HTML, PDF veya JSON rapor üretir: özet, veri kaynakları ve sürümleri, KVKK/ISO/MITRE görünümü, kanıtlar, çözümler, yanlış alarm istatistikleri.");
        ipucu.SetToolTip(_anaRapor, "Derin denetim bulgularını (yanlış alarm olmayanları) ana pencerenin sonuç tablosuna ekler; böylece mevcut HTML/PDF rapor, Durum Panosu ve Yönetim Kurulu özeti bunları da içerir.");
        ipucu.SetToolTip(_kalite, "Her kuralın yanlış alarm oranını gösterir. Oranı yüksek kuralların puanı otomatik düşürülür (kural kapatılmaz).");
        ipucu.SetToolTip(_nvd,"NVD API 2.0 biçimindeki JSON dosyalarını (kendi indirdiğiniz) çevrimdışı içe aktarır. Kurulu yazılım bilgisi NVD'ye GÖNDERİLMEZ; eşleştirme tamamen bu bilgisayarda yapılır. Bozuk dosyada mevcut indeks korunur.");
        ipucu.SetToolTip(_kev,"CISA'nın herkese açık KEV (sömürüldüğü bilinen açıklar) listesini indirir. Yalnızca veri çekilir; bilgisayarınızdan hiçbir şey gönderilmez. Her indirmede onay istenir.");
        // Alt: ayrıntı ve durum.
        _ayrinti.Dock = DockStyle.Bottom; _ayrinti.Height = 96; _ayrinti.Padding = new Padding(14, 8, 14, 0); _ayrinti.ForeColor = Yazi; _ayrinti.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _ayrinti.UseMnemonic = false; _ayrinti.Text = "Bir cihaz seçin: açık hizmetleri burada görünür.";
        _durum.Dock = DockStyle.Bottom; _durum.Height = 30; _durum.Padding = new Padding(14, 6, 14, 0); _durum.ForeColor = Soluk; _durum.UseMnemonic = false;
        // Tablo.
        _t.Dock = DockStyle.Fill; _t.AllowUserToAddRows = false; _t.AllowUserToDeleteRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = false; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = Kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false; _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; _t.DefaultCellStyle.BackColor = Kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.RowTemplate.Height = 30;
        // Sütunlar (yalnızca Ağırlık düzenlenebilir).
        foreach (var (ad, b, a) in new[] { ("Adr", "Adres", 14), ("Ad", "Ad", 16), ("Os", "İşletim Sistemi", 16), ("Rol", "Rol", 9), ("Agr", "Önem Ağırlığı (0-1)", 10), ("Port", "Açık Kapılar", 24), ("Kay", "Kaynak", 11) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; _t.Columns[ad]!.ReadOnly = ad != "Agr"; }
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(_t);
        // Dock sırası.
        Controls.Add(_t); Controls.Add(_ayrinti); Controls.Add(_durum); Controls.Add(serit3); Controls.Add(serit2); Controls.Add(serit); Controls.Add(acik);
        // Olaylar.
        _tara.Click += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "AgHaritasiFormu"); _iptal.Click += (_, _) => _cts?.Cancel(); _t.SelectionChanged += (_, _) => AyrintiYaz(); _t.CellEndEdit += (_, e) => AgirlikDegisti(e.RowIndex); FormClosing += (_, _) => _cts?.Cancel();
        // İlk doldurma.
        Doldur(); _durum.Text = _sensorler.Count == 0 ? "Önce \"Uzak Sensörler\" menüsünden bir sensör ekleyin." : "Hazır. Hedefi kontrol edip \"Sensörden Tara\"ya basın.";
    }

    // Tabloyu haritadan doldurur.
    private void Doldur()
    {
        _doluyor = true;
        try
        {
            _t.Rows.Clear();
            foreach (var a in _harita?.Assets.OrderBy(x => x.Addresses.FirstOrDefault(), StringComparer.Ordinal) ?? Enumerable.Empty<Asset>())
            {
                int acik = a.Services.Count(s => s.State == "open");
                string kapilar = string.Join(", ", a.Services.Where(s => s.State == "open").Take(8).Select(s => s.Port + (s.Service.Length > 0 ? "/" + s.Service : ""))) + (acik > 8 ? $" (+{acik - 8})" : "");
                int i = _t.Rows.Add(string.Join(", ", a.Addresses.Take(2)), a.Hostname ?? "", a.OsGuess ?? "", a.Role, a.AssetWeight.ToString("0.00"), kapilar, a.Provenance); _t.Rows[i].Tag = a.AssetId;
            }
        }
        finally { _doluyor = false; }
    }

    // Seçili cihazın hizmet ayrıntısı.
    private void AyrintiYaz()
    {
        if (_doluyor || _t.CurrentRow?.Tag is not string id) return;
        var a = _harita?.Assets.FirstOrDefault(x => x.AssetId == id); if (a == null) return;
        var s = a.Services.Where(x => x.State == "open").Take(12).Select(x => $"{x.Port}/{x.Proto} {x.Service} {x.Product} {x.Version}".Trim());
        _ayrinti.Text = $"Kimlik: {a.AssetId}\r\nAdresler: {string.Join(", ", a.Addresses)}\r\nHizmetler: {(a.Services.Any(x => x.State == "open") ? string.Join(" · ", s) : "açık kapı bulunamadı")}";
    }

    // Ağırlık hücresi düzenlendi: doğrula, kaydet.
    private void AgirlikDegisti(int satir)
    {
        if (_harita == null || satir < 0 || _t.Rows[satir].Tag is not string id) return;
        // Virgül/nokta ikisi de kabul edilir.
        string m = Convert.ToString(_t.Rows[satir].Cells["Agr"].Value)?.Replace(',', '.') ?? "";
        if (!double.TryParse(m, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) || v is < 0 or > 1) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Ağırlık 0 ile 1 arasında bir sayı olmalı (örn. 0,8)."; Doldur(); return; }
        _harita = NetworkMapBuilder.AgirlikAyarla(_harita, id, v);
        try { AssetStore.Save(_harita); _durum.ForeColor = Soluk; _durum.Text = "Ağırlık kaydedildi."; } catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Kaydedilemedi: " + ex.Message; }
        Doldur();
    }

    // Bu bilgisayarı denetler (salt-okunur): hizmet eşleme + yama. Sonuç haritaya ve bulgu deposuna yazılır, ayrı pencerede gösterilir.
    private async Task YerelDenetleAsync()
    {
        _yerel.Enabled = false; _durum.ForeColor = Soluk; _durum.Text = "Bu bilgisayar denetleniyor: hizmetler, dijital imzalar ve güncellemeler okunuyor (1-2 dakika sürebilir)...";
        try
        {
            var katalog = KbCatalog.Yukle();
            var fp = FpDeposu.Yukle(); var nvd = NvdIndeks.Yukle(); var kev = VulnFeedStore.Yukle();
            var sonuc = await Task.Run(() => YerelDenetimAkisi.Calistir(new GercekYerelKaynak(), katalog, _harita, DateTime.UtcNow, fp, nvd, kev));
            _harita = sonuc.Harita; AssetStore.Save(_harita); BulguDeposu.Degistir(YerelDenetimAkisi.VarlikKimligi(new GercekYerelKaynak()), sonuc.Bulgular); fp.Kaydet(); Doldur();
            int yuksek = sonuc.Bulgular.Count(b => b.Score >= 40 && b.State != FindingState.FalsePositive);
            _durum.ForeColor = yuksek > 0 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"Yerel denetim bitti: {sonuc.DinleyenSayisi} dinleyen kapı, {sonuc.HizmetSayisi} hizmet incelendi · {sonuc.Bulgular.Count} bulgu.";
            _gunluk($"Yerel Denetim: {sonuc.Bulgular.Count} bulgu ({yuksek} orta/yüksek), {sonuc.Yama.MissingKbs.Count} eksik güncelleme.");
            Bildirim.Goster("Yerel denetim bitti", $"{sonuc.Bulgular.Count} bulgu · {sonuc.DinleyenSayisi} dinleyen kapı incelendi.", yuksek > 0 ? BildirimTuru.Uyari : BildirimTuru.Basari);
            using var f = BulguSonucFormu.Yerel(sonuc, katalog, fp, ServisBaglami.Oturum.Aktif?.Ad ?? "bilinmiyor", nvd, kev); f.ShowDialog(this);
        }
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Yerel denetim başarısız: " + ex.Message.Split('\n')[0].Trim(); Bildirim.Goster("Yerel denetim başarısız", ex.Message.Split('\n')[0].Trim(), BildirimTuru.Hata); }
        finally { _yerel.Enabled = true; }
    }

    // Son denetim bulgularından rapor dosyası (HTML / PDF / JSON) üretir.
    private void RaporYaz()
    {
        var bulgular = BulguDeposu.Oku(); if (bulgular.Count == 0) { TemaliMesaj.Goster(this, "Raporlanacak bulgu yok. Önce \"Bu Bilgisayarı Denetle\" veya \"Aktif Web Denetimi\" çalıştırın.", "Derin Denetim Raporu", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var d = new SaveFileDialog { Title = "Derin Denetim Raporunu kaydet", Filter = "HTML rapor (*.html)|*.html|PDF rapor (*.pdf)|*.pdf|JSON (*.json)|*.json", FileName = $"DerinDenetim_{DateTime.Now:yyyyMMdd_HHmm}" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var fp = FpDeposu.Yukle(); var g = new DerinRaporGirdisi(Environment.MachineName, ServisBaglami.Oturum.Aktif?.Ad ?? "bilinmiyor", DateTime.UtcNow, fp.Uygula(bulgular), fp, KbCatalog.Yukle(), NvdIndeks.Yukle(), VulnFeedStore.Yukle());
            string uzanti = Path.GetExtension(d.FileName).ToLowerInvariant();
            if (uzanti == ".pdf") DerinRapor.PdfYaz(d.FileName, g); else if (uzanti == ".json") File.WriteAllText(d.FileName, DerinRapor.Json(g), new System.Text.UTF8Encoding(false)); else File.WriteAllText(d.FileName, DerinRapor.Html(g), new System.Text.UTF8Encoding(false));
            _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = "Rapor kaydedildi: " + d.FileName; _gunluk("Derin Denetim Raporu kaydedildi: " + Path.GetFileName(d.FileName)); Bildirim.Goster("Rapor kaydedildi", Path.GetFileName(d.FileName), BildirimTuru.Basari);
        }
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Rapor yazılamadı: " + ex.Message; TemaliMesaj.Goster(this, "Rapor yazılamadı: " + ex.Message, "Derin Denetim Raporu", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // Derin denetim bulgularını ana pencerenin sonuç tablosuna ekler (yanlış alarmlar ve skoru 0 olan bilgi satırları hariç).
    private void AnaRaporaEkle()
    {
        var l = FpDeposu.Yukle().Uygula(BulguDeposu.Oku()).Where(f => f.State != FindingState.FalsePositive && f.Score > 0).ToList();
        if (l.Count == 0) { TemaliMesaj.Goster(this, "Ana tabloya eklenecek bulgu yok.", "Ana Rapora Ekle", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (_anaListe == null) { TemaliMesaj.Goster(this, "Ana pencere bağlı değil.", "Ana Rapora Ekle", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _anaListe(l.Select(V2Kayit.Donustur).ToList());
        _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"{l.Count} bulgu ana tabloya eklendi; HTML/PDF rapor, Durum Panosu ve Yönetim Kurulu özeti artık bunları da içerir.";
    }

    // Aktif web denetimi: YALNIZCA iç ağ. Adres sorulur, kapsam kapısı denetler, kullanıcı onaylar (varsayılan Hayır), sonuç ortak pencerede gösterilir.
    private async Task AktifWebAsync()
    {
        var c = SoruFormu.Sor(this, "Aktif Web Denetimi", "İç ağdaki bir web sunucusunun adresini yazın (örn. http://192.168.56.20:8080/). Yalnızca KENDİNİZE ait veya yazılı izniniz olan sistemler denetlenmelidir.", "Devam", new Alan("Web adresi"));
        if (c == null) return; string url = c[0].Trim(); if (url.Length > 0 && !url.Contains("://")) url = "http://" + url;
        // Hızlı ön kontrol: iç ağ mı? (Asıl kilit denetçinin içindedir; burada kullanıcıya erken ve anlaşılır cevap verilir.)
        var host = Kapsam.HostCikar(url); var karar = host == null ? null : await Kapsam.SiniflaAsync(host, CancellationToken.None);
        if (karar == null || karar.Mod != KapsamModu.IcAg) { TemaliMesaj.Goster(this, "Aktif web denetimi yalnızca İÇ AĞ adreslerinde çalışır." + (karar != null ? "\r\n\r\n" + karar.Neden : "\r\n\r\nGeçerli bir adres yazın.") + "\r\n\r\nİnternetteki siteler için Yeni Tarama'daki PASİF gözlem kullanılır.", "Aktif web denetimi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (TemaliMesaj.Goster(this, $"{url} adresine kontrollü sondalar gönderilecek:\r\n\r\n• Yalnızca GET, OPTIONS ve TRACE yöntemleri (PUT/DELETE/POST gönderilmez)\r\n• En çok {AktifHttp.EnCokIstek} istek, yaklaşık saniyede 5\r\n• Bilinen hassas yollar (.git, .env, yedek dosyaları vb.) yalnızca VAR MI diye bakılır; içerik saklanmaz\r\n\r\nYalnızca sahibi olduğunuz veya yazılı izniniz bulunan sistemde yapın. Devam edilsin mi?", "Aktif denetimi onayla", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _web.Enabled = false; _iptal.Enabled = true; _cts = new(); _durum.ForeColor = Soluk; _durum.Text = "Aktif web denetimi başladı...";
        try
        {
            var g = await AktifHttp.GozlemAsync(url, null, m => { if (!IsDisposed) BeginInvoke(() => _durum.Text = m); }, AktifHttp.VarsayilanHizMs, _cts.Token);
            // Varlık ağırlığı: haritada bu adresi taşıyan cihaz varsa onunki, yoksa 0,5.
            double agirlik = _harita?.Assets.FirstOrDefault(a => a.Addresses.Contains(g.Host))?.AssetWeight ?? 0.5;
            var bulgular = AktifHttp.BulguUret(g, AktifHttp.Degerlendir(g), agirlik, DateTime.UtcNow); var fp = FpDeposu.Yukle(); fp.Gor(bulgular); bulgular = fp.Uygula(bulgular); fp.Kaydet();
            BulguDeposu.Degistir($"web:{g.Host}:{new Uri(g.Url).Port}", bulgular);
            int onemli = bulgular.Count(b => b.Score >= 40 && b.State != FindingState.FalsePositive);
            _durum.ForeColor = onemli > 0 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"Aktif web denetimi bitti: {g.IstekSayisi} istek, {bulgular.Count} bulgu.";
            _gunluk($"Aktif Web Denetimi: {g.Host} · {bulgular.Count} bulgu ({onemli} orta/yüksek), {g.IstekSayisi} istek.");
            Bildirim.Goster("Aktif web denetimi bitti", $"{bulgular.Count} bulgu · {g.IstekSayisi} istek gönderildi.", onemli > 0 ? BildirimTuru.Uyari : BildirimTuru.Basari);
            string ust = $"Hedef: {g.Url} · {g.IstekSayisi} istek (GET/OPTIONS/TRACE) · {bulgular.Count} bulgu\r\n" + (g.Notlar.Count > 0 ? string.Join(" | ", g.Notlar.Take(3)) : "Sunucu içeriği saklanmadı; yalnızca durum kodu ve imza bilgisi değerlendirildi.") + "\r\nBu pencere yalnızca bilgi verir; düzeltmeler elle yapılır.";
            using var f = new BulguSonucFormu("Aktif Web Denetimi Sonucu", ust, bulgular, fp, ServisBaglami.Oturum.Aktif?.Ad ?? "bilinmiyor"); f.ShowDialog(this);
        }
        catch (OperationCanceledException) { _durum.Text = "Aktif denetim durduruldu."; Bildirim.Goster("Denetim durduruldu", "Aktif web denetimi yarıda kesildi.", BildirimTuru.Uyari); }
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Aktif denetim başarısız: " + ex.Message.Split('\n')[0].Trim(); Bildirim.Goster("Aktif denetim başarısız", ex.Message.Split('\n')[0].Trim(), BildirimTuru.Hata); }
        finally { _web.Enabled = true; _iptal.Enabled = false; }
    }

    // NVD 2.0 JSON dosyalarını çevrimdışı içe aktarır (arka planda; büyük dosyalar uzun sürebilir). Hata olursa mevcut indeks korunur.
    private async Task NvdYukleAsync()
    {
        using var d = new OpenFileDialog { Title = "NVD 2.0 JSON dosyalarını seçin", Filter = "NVD JSON (*.json)|*.json|Tüm dosyalar (*.*)|*.*", Multiselect = true };
        if (d.ShowDialog(this) != DialogResult.OK || d.FileNames.Length == 0) return;
        _nvd.Enabled = false; _durum.ForeColor = Soluk; _durum.Text = $"{d.FileNames.Length} NVD dosyası içe aktarılıyor (büyük dosyalar birkaç dakika sürebilir)...";
        try
        {
            var idx = await Task.Run(() => NvdIndeks.IceAktar(d.FileNames, DateTime.UtcNow));
            _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = idx.Ozetle() + ".";
            _gunluk("NVD verisi içe aktarıldı: " + idx.Ozetle()); Bildirim.Goster("NVD verisi yüklendi", $"{idx.CveSayisi} CVE · {idx.Urunler.Count} ürün.", BildirimTuru.Basari);
        }
        catch (Exception ex) when (ex is KatalogHatasi or IOException or UnauthorizedAccessException or OutOfMemoryException)
        { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "NVD verisi yüklenemedi (mevcut indeks korundu): " + ex.Message; TemaliMesaj.Goster(this, ex.Message + "\r\n\r\nMevcut indeks (varsa) olduğu gibi duruyor.", "NVD dosyası reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { _nvd.Enabled = true; }
    }

    // CISA KEV listesini ONAYLA indirir (yalnızca veri çekme; hiçbir şey gönderilmez). Bozuk dosyada eski liste korunur.
    private async Task KevIndirAsync()
    {
        var eski = VulnFeedStore.Yukle(); string mevcut = eski == null ? "Şu an yüklü liste yok." : "Şu an: " + eski.Ozetle() + ".";
        if (TemaliMesaj.Goster(this, $"CISA'nın herkese açık \"Bilinen Sömürülen Açıklar\" (KEV) listesi indirilecek.\r\n\r\nAdres: {KevIndirici.Adres}\r\n\r\n• Yalnızca bu adrese TEK bir okuma (GET) isteği yapılır; bilgisayarınızdan hiçbir veri gönderilmez.\r\n• Dosya tamamen doğrulanır, özeti (SHA-256) ve indirme zamanı kaydedilir.\r\n• Bozuk veya şüpheli dosyada mevcut liste olduğu gibi korunur.\r\n\r\n{mevcut}\r\n\r\nİnternetten indirilsin mi?", "İnternetten veri indirme", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        _kev.Enabled = false; _durum.ForeColor = Soluk; _durum.Text = "KEV listesi indiriliyor...";
        try
        {
            var s = await KevIndirici.IndirAsync(null, DateTime.UtcNow, CancellationToken.None);
            _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = s.Ozetle() + ".";
            _gunluk("KEV listesi indirildi: " + s.Ozetle());
            Bildirim.Goster("KEV listesi indirildi", $"Sürüm {s.Surum} · {s.Kayitlar.Count} açık.", BildirimTuru.Basari);
        }
        catch (Exception ex) when (ex is KatalogHatasi or AgHatasi or HttpRequestException or TaskCanceledException or IOException)
        { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "KEV listesi indirilemedi (mevcut liste korundu): " + ex.Message.Split('\n')[0].Trim(); Bildirim.Goster("İndirme başarısız", ex.Message.Split('\n')[0].Trim(), BildirimTuru.Hata); }
        finally { _kev.Enabled = true; }
    }

    // KB kataloğu dosyasını seçtirir ve içe aktarır (önce tamamen doğrulanır; bozuksa eski katalog korunur).
    private void KatalogYukle()
    {
        using var d = new OpenFileDialog { Title = "KB kataloğu seçin", Filter = "Katalog (*.json)|*.json|Tüm dosyalar (*.*)|*.*" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            // Çok büyük dosya okunmadan reddedilir.
            if (new FileInfo(d.FileName).Length > KbCatalog.EnCokDosyaBayt) throw new KatalogHatasi("Dosya çok büyük (en çok 5 MB).");
            var k = KbCatalog.IceAktar(File.ReadAllText(d.FileName));
            int adet = k.Yapilar.Values.Sum(l => l.Count); _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"Katalog yüklendi: sürüm {k.Surum}, {k.Yapilar.Count} yapı, {adet} güncelleme (özet {k.Ozet[..12]}).";
            _gunluk($"KB kataloğu yüklendi: {k.Surum} ({adet} güncelleme).");
        }
        catch (Exception ex) when (ex is KatalogHatasi or IOException or UnauthorizedAccessException)
        { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Katalog yüklenemedi (eski katalog korundu): " + ex.Message; TemaliMesaj.Goster(this, ex.Message + "\r\n\r\nEski katalog (varsa) olduğu gibi duruyor.", "Katalog reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // Taramayı başlatır.
    private async Task TaraAsync()
    {
        // Sensör seçili mi?
        if (_sensor.SelectedIndex < 0) { TemaliMesaj.Goster(this, "Önce \"Uzak Sensörler\" menüsünden bir sensör ekleyin.", "Ağ Haritası", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var sensor = _sensorler[_sensor.SelectedIndex]; string hedef = _hedef.Text.Trim(); var profil = _profil.SelectedIndex == 1 ? NmapProfili.Standart : NmapProfili.Hizli;
        // Hedef denetimi.
        var h = SensorNmap.HedefHatasi(hedef);
        if (h != null)
        {
            // Hedef internetteki bir adres/alan adıysa nmap ÇALIŞMAZ; yerine yalnızca PASİF HTTP/TLS gözlemi önerilir (kod seviyesinde kilit: dış ağda port taraması yok).
            KapsamKarari? karar = null; try { using var zs = new CancellationTokenSource(TimeSpan.FromSeconds(8)); karar = await Kapsam.SiniflaAsync(hedef, zs.Token); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "AgHaritasiFormu.TaraAsync"); }
            if (karar is { Mod: KapsamModu.DisAgPasif } && _pasifBaslat != null)
            {
                if (TemaliMesaj.Goster(this, $"\"{karar.Host}\" internetteki bir adres.\r\n\r\nİnternetteki adreslere nmap (kapı taraması) GÖNDERİLMEZ.\r\n\r\nBunun yerine yalnızca PASİF gözlem (sıradan bir ziyaretçi gibi web sayfası, güvenlik sertifikası ve e-posta kayıtlarına bakma) yapılsın mı?", "Dış ağ: pasif gözleme geçilsin mi?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) { var host = karar.Host; Close(); _pasifBaslat(host); }
                return;
            }
            TemaliMesaj.Goster(this, h, "Hedef geçersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
        }
        // Onay (varsayılan Hayır): tarama ağa istek gönderir.
        if (TemaliMesaj.Goster(this, $"Sensör \"{sensor.Ad}\" şu hedefe nmap sürüm taraması gönderecek:\r\n\r\n{hedef}\r\n\r\nYalnızca sahibi olduğunuz veya yazılı izniniz bulunan ağda yapın. Devam edilsin mi?", "Taramayı onayla", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Çalıştır.
        _tara.Enabled = false; _iptal.Enabled = true; _cts = new(); _durum.ForeColor = Soluk; _durum.Text = "Sensörde nmap çalışıyor (birkaç dakika sürebilir)...";
        try
        {
            var tarama = await SensorNmap.TaraAsync(sensor, hedef, profil, _cts.Token);
            // Birleştir ve kaydet.
            _harita = NetworkMapBuilder.Merge(_harita, tarama, sensor.Ad, DateTime.UtcNow); AssetStore.Save(_harita); Doldur();
            // Eski / desteği bitmiş yazılım denetimi (yalnızca okuma): taranan her cihazın eski bulguları yenisiyle değiştirilir.
            var eol = DesteksizYazilim.TaramaBulgulari(tarama, ip => _harita.Assets.FirstOrDefault(a => a.Addresses.Contains(ip))?.AssetWeight ?? 0.5, DateTime.UtcNow);
            { var fp0 = FpDeposu.Yukle(); fp0.Gor(eol); eol = fp0.Uygula(eol); fp0.Kaydet(); var mevcut = BulguDeposu.Oku(); foreach (var h0 in tarama.Hosts) mevcut.RemoveAll(f => f.AssetId.StartsWith($"net:{h0.Address}:", StringComparison.Ordinal)); mevcut.AddRange(eol); BulguDeposu.Kaydet(mevcut); }
            int acik = tarama.Hosts.Sum(x => x.Services.Count(s => s.State == "open")); _durum.ForeColor = eol.Count > 0 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"{tarama.Hosts.Count} cihaz, {acik} açık hizmet bulundu. Harita kaydedildi ({_harita.Assets.Count} cihaz)." + (eol.Count > 0 ? $" DİKKAT: {eol.Count} eski/desteği bitmiş yazılım uyarısı (\"Ana Rapora Ekle\" ile rapora alın)." : "");
            if (eol.Count > 0) _gunluk($"Ağ Haritası: {eol.Count} eski/desteği bitmiş yazılım uyarısı: " + string.Join("; ", eol.Take(5).Select(f => f.Title)));
            _gunluk($"Ağ Haritası: {hedef} tarandı, {tarama.Hosts.Count} cihaz bulundu.");
            Bildirim.Goster("Ağ haritası tarandı", $"{tarama.Hosts.Count} cihaz, {acik} açık hizmet bulundu.", BildirimTuru.Basari);
        }
        catch (OperationCanceledException) { _durum.Text = "Tarama durduruldu."; Bildirim.Goster("Tarama durduruldu", "Ağ haritası taraması yarıda kesildi; harita değişmedi.", BildirimTuru.Uyari); }
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Tarama başarısız: " + ex.Message.Split('\n')[0].Trim(); Bildirim.Goster("Tarama başarısız", ex.Message.Split('\n')[0].Trim(), BildirimTuru.Hata); }
        finally { _tara.Enabled = true; _iptal.Enabled = false; }
    }
}

// Kural kalitesi (yanlış alarm istatistiği) penceresi.
internal sealed class KuralKalitesiFormu : TemaliForm
{
    public KuralKalitesiFormu(FpDeposu fp)
    {
        Color zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), soluk = DesignTokens.Ozgun(TokenRol.TextMuted);
        Text = "Kural Kalitesi"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(820, 480); MinimumSize = new Size(640, 360); BackColor = zemin; ForeColor = yazi; Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        var ust = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(14, 10, 14, 0), ForeColor = soluk, UseMnemonic = false, Text = "Her kuralın ürettiği benzersiz bulgu sayısı ve yanlış alarm olarak işaretlenenler. Oran %30'u aşarsa (en az 10 örnekle) kural \"gözden geçirilmeli\" olur: kapatılmaz, yalnızca puanı %25 düşürülür. Yanlış alarm işaretlemek için bulgu sonuç penceresini kullanın." };
        var t = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = kutu, GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false, RowTemplate = { Height = 28 } };
        t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); t.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); t.DefaultCellStyle.BackColor = kutu; t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        foreach (var (ad, b, a) in new[] { ("K", "Kural", 34), ("T", "Bulgu", 12), ("F", "Yanlış alarm", 14), ("O", "Oran", 12), ("D", "Durum", 28) }) { t.Columns.Add(ad, b); t.Columns[ad]!.FillWeight = a; }
        foreach (var k in fp.KuralKalitesi()) { int i = t.Rows.Add(k.Kural, k.Toplam, k.Fp, $"%{k.Oran * 100:0}", k.Durum); if (k.Durum == "Gözden geçirilmeli") t.Rows[i].DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); }
        if (t.Rows.Count == 0) ust.Text += "\r\n\r\nHenüz istatistik yok: bir denetim çalıştırın.";
        KoyuTema.UygulaTablo(t); Controls.Add(t); Controls.Add(ust);
    }
}
