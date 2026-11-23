// Bu dosya "Geçmiş Operasyonlar" penceresini çizer: önceki taramaların listesi, karşılaştırma ve ekrana yükleme.
namespace LabDenetim;

// Geçmiş operasyonlar penceresi.
internal sealed class GecmisFormu : SayfaDenetimi
{
    // Kullanıcının "Ekrana Yükle" dediği kayıt (yoksa boş).
    public GecmisKayit? Secilen { get; private set; }
    // Kayıtların listesi (en yeni üstte).
    private List<GecmisKayit> _kayitlar = new();
    // Kayıt tablosu.
    private readonly DataGridView _tablo = new();
    // Seçili kaydın karşılaştırma ve özet yazısı.
    private readonly RichTextBox _detay;
    // Durum yazısı.
    private readonly Label _baslik;
    // Düğmeler.
    private readonly Button _yukle, _sil, _hepsiniSil, _kapat;

    // Pencereyi kurar.
    public GecmisFormu()
    {
        // Pencere ayarları.
        Text = "◷ Geçmiş Operasyonlar"; ClientSize = new Size(960, 600); MinimumSize = new Size(820, 520); StartPosition = FormStartPosition.CenterParent;
        // Koyu tema.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Üst açıklama.
        _baslik = new Label { Dock = DockStyle.Fill, UseMnemonic = false, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Margin = new Padding(0, 0, 0, 8), Text = "Önceki taramalarınız burada saklanır; programı kapatıp açsanız da silinmez. Bir satırı seçin: aşağıda önceki taramayla karşılaştırması görünür. \"Ekrana Yükle\" dediğinizde o taramanın sonuçları ana ekrana gelir." };
        // Tabloyu hazırla.
        TabloKur();
        // Tablonun kaydırma çubukları koyu olsun.
        KoyuTema.UygulaTablo(_tablo);
        // Karşılaştırma kutusu.
        _detay = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Body), TabStop = false, Margin = new Padding(0, 8, 0, 0) };
        // Karşılaştırma kutusunun kaydırma çubuğu koyu olsun.
        KoyuTema.Uygula(_detay);
        // Düğmeler.
        _yukle = Dugme("▸ Ekrana Yükle", 170, DesignTokens.Ozgun(TokenRol.AccentPrimary), Color.Black);
        // Sil düğmesi.
        _sil = Dugme("✖ Seçileni Sil", 150, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Tümünü sil düğmesi.
        _hepsiniSil = Dugme("Tümünü Sil", 120, DesignTokens.Ozgun(TokenRol.StatusDangerBg), DesignTokens.Ozgun(TokenRol.StatusDanger));
        // Kapat düğmesi.
        _kapat = Dugme("Kapat", 100, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Düğme tıklamaları.
        _yukle.Click += (_, _) => Yukle();
        // Seçileni sil.
        _sil.Click += (_, _) => SecileniSil();
        // Tümünü sil.
        _hepsiniSil.Click += (_, _) => HepsiniSil();
        // Kapat.
        _kapat.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        // Satır seçilince ayrıntıyı göster.
        _tablo.SelectionChanged += (_, _) => DetayGoster();
        // Çift tıklayınca yükle.
        _tablo.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Yukle(); };
        // Puan hücresini puana göre renklendir.
        _tablo.CellFormatting += (_, e) =>
        {
            // Yalnızca puan sütunu (3. sütun).
            if (e.ColumnIndex != 3 || e.RowIndex < 0 || e.Value is not string s || !int.TryParse(s.Split('/')[0], out int p)) return;
            // 80+ yeşil, 50+ turuncu, altı kırmızı.
            e.CellStyle!.ForeColor = p >= 80 ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : p >= 50 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusDanger);
            // Kalın yaz.
            e.CellStyle.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold);
        };
        // Düğmeleri sağa yaslı dizen panel.
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        // Düğmeleri ekle (sağdan sola).
        dugmeler.Controls.AddRange(new Control[] { _kapat, _hepsiniSil, _sil, _yukle });
        // Ana ızgara.
        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(16, 14, 16, 14) };
        // Tek sütun.
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Satırlar: açıklama, tablo, karşılaştırma, düğmeler.
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        // Parçaları yerleştir.
        tbl.Controls.Add(_baslik, 0, 0); tbl.Controls.Add(_tablo, 0, 1); tbl.Controls.Add(_detay, 0, 2); tbl.Controls.Add(dugmeler, 0, 3);
        // Izgarayı pencereye koy.
        Controls.Add(tbl);
        // Kayıtları yükle.
        Yenile();
    }

    // Koyu görünümlü düğme üretir.
    private static Button Dugme(string metin, int genislik, Color zemin, Color yazi) => new EfektliButon()
    {
        // Yazı, boyut ve boşluk.
        Text = metin, Size = new Size(genislik, 36), Margin = new Padding(8, 0, 0, 0), UseMnemonic = false,
        // Düz görünüm ve renkler.
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
        _tablo.EnableHeadersVisualStyles = false; _tablo.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; _tablo.ColumnHeadersHeight = 34;
        // Başlık renkleri.
        _tablo.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _tablo.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Satır renkleri.
        _tablo.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _tablo.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        // Seçili satır renkleri.
        _tablo.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.DefaultCellStyle.SelectionForeColor = Color.White;
        // Aralıklı satır rengi.
        _tablo.AlternatingRowsDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); _tablo.AlternatingRowsDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Sütunlar.
        _tablo.Columns.Add("Tarih", "Tarih"); _tablo.Columns.Add("Hedef", "Hedef"); _tablo.Columns.Add("Profil", "Profil"); _tablo.Columns.Add("Puan", "Puan");
        // Sayı sütunları.
        _tablo.Columns.Add("Kritik", "Kritik"); _tablo.Columns.Add("Uyari", "Uyarı/Dikkat"); _tablo.Columns.Add("Guvenli", "Güvenli"); _tablo.Columns.Add("Sure", "Süre");
        // Sütun ağırlıkları.
        _tablo.Columns[0].FillWeight = 120; _tablo.Columns[1].FillWeight = 110; _tablo.Columns[2].FillWeight = 80; _tablo.Columns[3].FillWeight = 70;
        // Kalan sütunlar.
        _tablo.Columns[4].FillWeight = 60; _tablo.Columns[5].FillWeight = 85; _tablo.Columns[6].FillWeight = 70; _tablo.Columns[7].FillWeight = 60;
    }

    // Kayıtları diskten okuyup tabloya yazar.
    private void Yenile()
    {
        // Eski satırları temizle.
        _tablo.Rows.Clear();
        // Kayıtları oku.
        _kayitlar = GecmisDeposu.Listele();
        // Her kaydı bir satır olarak ekle.
        foreach (var k in _kayitlar)
            // Satır: tarih, hedef, profil, puan, sayılar, süre.
            _tablo.Rows.Add(k.Tarih.ToString("dd.MM.yyyy HH:mm"), k.Hedef, k.Profil, $"{k.Puan}/100", k.Kirmizi, k.Sari, k.Yesil, $"{k.SureSn / 60:00}:{k.SureSn % 60:00}");
        // Hiç kayıt yoksa dostça bir not göster.
        if (_kayitlar.Count == 0) DetayYaz("Henüz kayıtlı operasyon yok.\n\nAna ekranda \"⚡ TARAMAYI BAŞLAT\" ile bir tarama yapın; tarama bitince otomatik olarak buraya kaydedilir.");
        // Düğmelerin durumunu güncelle.
        Dugmeler();
    }

    // Seçili satıra göre düğmeleri açar/kapatır.
    private void Dugmeler() { bool var = _tablo.SelectedRows.Count > 0; _yukle.Enabled = var; _sil.Enabled = var; _hepsiniSil.Enabled = _kayitlar.Count > 0; }

    // Seçili kaydı verir (yoksa boş).
    private GecmisKayit? SeciliKayit() => _tablo.SelectedRows.Count > 0 && _tablo.SelectedRows[0].Index < _kayitlar.Count ? _kayitlar[_tablo.SelectedRows[0].Index] : null;

    // Karşılaştırma kutusuna renkli yazı yazar (yalnızca düz metin).
    private void DetayYaz(string metin) { _detay.Clear(); _detay.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _detay.Text = metin; }

    // Seçili kaydın karşılaştırmasını ve özetini gösterir.
    private void DetayGoster()
    {
        // Düğmeleri güncelle.
        Dugmeler();
        // Seçili kayıt yoksa bir şey yapma.
        var k = SeciliKayit();
        // Kayıt yoksa çık.
        if (k == null) return;
        // Metni biriktir.
        var sb = new System.Text.StringBuilder();
        // Genel bilgi.
        sb.AppendLine($"{k.Tarih:dd.MM.yyyy HH:mm} · Hedef: {k.Hedef} · Profil: {k.Profil} · {k.KapiSayisi} kapı yoklandı · puan {k.Puan}/100");
        // Aynı hedefin bu kayıttan ÖNCEKİ taraması.
        var onceki = _kayitlar.Where(x => x.Tarih < k.Tarih && string.Equals(x.Hedef, k.Hedef, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Tarih).FirstOrDefault();
        // Önceki tarama varsa karşılaştır.
        if (onceki != null)
        {
            // Karşılaştır.
            var c = GecmisDeposu.Karsilastir(k, onceki);
            // Özet cümlesi.
            sb.AppendLine().AppendLine(c.Ozet);
            // Yeni çıkan sorunlar.
            if (c.Yeni.Count > 0) sb.AppendLine("▲ Yeni çıkanlar: " + string.Join(", ", c.Yeni.Take(8)) + (c.Yeni.Count > 8 ? " ..." : ""));
            // Kapanan sorunlar.
            if (c.Kapanan.Count > 0) sb.AppendLine("▼ Kapananlar: " + string.Join(", ", c.Kapanan.Take(8)) + (c.Kapanan.Count > 8 ? " ..." : ""));
            // Hiç değişiklik yoksa söyle.
            if (c.Yeni.Count == 0 && c.Kapanan.Count == 0) sb.AppendLine("Sorunlarda bir değişiklik yok.");
        }
        // İlk tarama ise söyle.
        else sb.AppendLine().AppendLine("Bu hedef için karşılaştırılacak daha eski bir tarama yok.");
        // Başlıca sorunları listele.
        var sorunlar = k.Bulgular.Where(b => b.Onem != Onem.Bilgi && !b.Atlandi).OrderByDescending(b => b.Onem).Take(6).ToList();
        // Sorun varsa yaz.
        if (sorunlar.Count > 0) { sb.AppendLine().AppendLine("Başlıca sorunlar:"); foreach (var b in sorunlar) sb.AppendLine($"  • [{(b.Onem == Onem.Yuksek ? "KRİTİK" : b.Onem == Onem.Orta ? "UYARI" : "DİKKAT")}] {b.Baslik}"); }
        // Sorun yoksa güzel haber ver.
        else sb.AppendLine().AppendLine("Bu taramada sorun bulunmadı.");
        // Metni kutuya yaz.
        DetayYaz(sb.ToString());
    }

    // "Ekrana Yükle": seçili kaydı döndürüp pencereyi kapatır.
    private void Yukle()
    {
        // Seçili kayıt yoksa çık.
        var k = SeciliKayit(); if (k == null) return;
        // Seçileni sakla.
        Secilen = k;
        // "Tamam" olarak kapat.
        DialogResult = DialogResult.OK; Close();
    }

    // Seçili kaydı (onaydan sonra) siler.
    private void SecileniSil()
    {
        // Seçili kayıt yoksa çık.
        var k = SeciliKayit(); if (k == null) return;
        // Onay iste (varsayılan: Hayır).
        if (TemaliMesaj.Goster($"{k.Tarih:dd.MM.yyyy HH:mm} tarihli kayıt kalıcı olarak silinecek. Geri alınamaz.\r\n\r\nDevam edilsin mi?", "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Kaydı sil ve listeyi yenile.
        GecmisDeposu.Sil(k.Id); Yenile();
    }

    // Tüm kayıtları (onaydan sonra) siler.
    private void HepsiniSil()
    {
        // Onay iste (varsayılan: Hayır).
        if (TemaliMesaj.Goster($"Tüm geçmiş kayıtlar ({_kayitlar.Count} adet) kalıcı olarak silinecek. Geri alınamaz.\r\n\r\nDevam edilsin mi?", "Tümünü Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Hepsini sil ve listeyi yenile.
        GecmisDeposu.HepsiniSil(); Yenile();
    }
}
