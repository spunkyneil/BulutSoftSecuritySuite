// Bu dosya "✦ Aktif Kalkanlarım" penceresini çizer: programın Windows Güvenlik Duvarı'na eklediği (BulutSoft- ön ekli) tüm
// engelleme kurallarını listeler; her kuralın yanındaki "Kilidi Aç (Kuralı Kaldır)" düğmesi (yönetici izniyle) onu tek tıkla geri alır.
namespace LabDenetim;

// Aktif kalkanlar penceresi.
internal sealed class KalkanFormu : SayfaDenetimi
{
    // Kuralların listelendiği tablo.
    private readonly DataGridView _tablo = new();
    // Üstteki açıklama / durum yazısı ve boş liste mesajı.
    private readonly Label _baslik, _bos;

    // Pencereyi kurar.
    public KalkanFormu()
    {
        // Pencere ayarları.
        Text = "✦ Aktif Kalkanlarım"; ClientSize = new Size(900, 520); MinimumSize = new Size(780, 420); StartPosition = FormStartPosition.CenterParent;
        // Koyu tema.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Üst açıklama.
        _baslik = new Label { Dock = DockStyle.Fill, UseMnemonic = false, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Text = "Burada, bu program aracılığıyla Windows Güvenlik Duvarı'na eklenen tüm engelleme kuralları (\"kalkanlar\") görünür: Otomatik Onar ile kapattığınız kapılar ve Acil İzolasyon kuralları. \"Kilidi Aç\" düğmesi o kuralı kaldırır ve kapıyı eski haline döndürür (Windows yönetici izni ister)." };
        // Boş liste mesajı.
        _bos = new Label { Dock = DockStyle.Fill, UseMnemonic = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Visible = false, Text = "Henüz aktif bir kalkanınız yok.\r\nBir sonuçta \"✚ Otomatik Onar\" kullandığınızda veya \"⚡ Acil İzolasyon\"u açtığınızda kurallar burada listelenir." };
        // Tabloyu hazırla.
        TabloKur();
        // Kaydırma çubukları koyu olsun.
        KoyuTema.UygulaTablo(_tablo);
        // Düğmeler.
        var yenile = Dugme("↻ Yenile", 120, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Kapat düğmesi.
        var kapat = Dugme("Kapat", 100, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Düğme tıklamaları.
        yenile.Click += (_, _) => Yenile();
        // Kapat.
        kapat.Click += (_, _) => Close();
        // Tablodaki düğme sütununa tıklanınca ilgili kuralı kaldır.
        _tablo.CellContentClick += (_, e) => GuvenliCalistir.Baslat(() => HucreTikla(e), "KalkanFormu");
        // Düğmeleri sağa yaslı dizen panel.
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        // Düğmeleri ekle (sağdan sola).
        dugmeler.Controls.AddRange(new Control[] { kapat, yenile });
        // Ana ızgara: açıklama, tablo (boş mesajıyla üst üste), düğmeler.
        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 14, 16, 14) };
        // Tek sütun.
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Satırlar: açıklama, tablo, düğmeler.
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        // Tablo ve boş mesajı aynı kapta durur.
        var kap = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        // İkisini kaba koy.
        kap.Controls.Add(_tablo); kap.Controls.Add(_bos);
        // Parçaları yerleştir.
        tbl.Controls.Add(_baslik, 0, 0); tbl.Controls.Add(kap, 0, 1); tbl.Controls.Add(dugmeler, 0, 2);
        // Izgarayı pencereye koy.
        Controls.Add(tbl);
        // Pencere açılınca listeyi doldur.
        Shown += (_, _) => Yenile();
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
        _tablo.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _tablo.DefaultCellStyle.Padding = new Padding(6, 6, 6, 6);
        // Seçili satır renkleri.
        _tablo.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.DefaultCellStyle.SelectionForeColor = Color.White;
        // Aralıklı satır rengi.
        _tablo.AlternatingRowsDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); _tablo.AlternatingRowsDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Satır yüksekliği.
        _tablo.RowTemplate.Height = 40;
        // Sütunlar.
        _tablo.Columns.Add("Ad", "Kural Adı"); _tablo.Columns.Add("Tur", "Kalkanın Türü"); _tablo.Columns.Add("Port", "Port / Hedef"); _tablo.Columns.Add("Yon", "Yön"); _tablo.Columns.Add("Durum", "Durum");
        // Kilidi aç düğme sütunu.
        var dugme = new DataGridViewButtonColumn { Name = "Kilit", HeaderText = "İşlem", Text = "✓ Kilidi Aç (Kuralı Kaldır)", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat, MinimumWidth = 200 };
        // Düğme hücre renkleri.
        dugme.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.StatusWarningBg); dugme.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); dugme.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.StatusWarningBg); dugme.DefaultCellStyle.SelectionForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning);
        // Sütunu ekle.
        _tablo.Columns.Add(dugme);
        // Sütun ağırlıkları.
        _tablo.Columns[0].FillWeight = 130; _tablo.Columns[1].FillWeight = 100; _tablo.Columns[2].FillWeight = 70; _tablo.Columns[3].FillWeight = 50; _tablo.Columns[4].FillWeight = 70; _tablo.Columns[5].FillWeight = 120;
    }

    // Kuralları Windows'tan okuyup tabloya yazar.
    private void Yenile()
    {
        // Eski satırları temizle.
        _tablo.Rows.Clear();
        // Kuralları oku.
        var liste = GuvenlikDuvari.Listele();
        // Her kuralı bir satır olarak ekle.
        foreach (var k in liste)
        {
            // Satırı ekle.
            int i = _tablo.Rows.Add(k.Ad, k.Tur, k.Port, k.Yon, (k.Etkin ? "Etkin" : "Pasif") + (k.Engel ? " · Engelle" : " · İzin"));
            // Kuralı satırın içine sakla.
            _tablo.Rows[i].Tag = k;
        }
        // Liste boşsa dostça mesaj göster.
        _bos.Visible = liste.Count == 0; _tablo.Visible = liste.Count > 0;
        // Mesaj kutusu tablonun üstünde kalsın.
        if (liste.Count == 0) _bos.BringToFront();
    }

    // Tablodaki bir düğmeye tıklandıysa ilgili kuralı (onaydan sonra, yönetici izniyle) kaldırır.
    private async Task HucreTikla(DataGridViewCellEventArgs e)
    {
        // Başlık satırı veya başka bir sütunsa bir şey yapma.
        if (e.RowIndex < 0 || _tablo.Columns[e.ColumnIndex].Name != "Kilit") return;
        // Satırdaki kural bilgisi.
        if (_tablo.Rows[e.RowIndex].Tag is not KalkanKurali k) return;
        // Kuralı kaldırmak güvenlik duvarını değiştirir: yalnızca Yönetici (Analist listeyi görebilir).
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)) { TemaliMesaj.Goster("\"Kilidi Aç (Kuralı Kaldır)\" özelliği için yetkiniz yok.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Güvenlik: yalnızca güvenli adlı BulutSoft kuralları kaldırılır.
        if (!GuvenlikDuvari.AdGuvenliMi(k.Ad)) { TemaliMesaj.Goster("Bu kuralın adı güvenli biçimde değil; program bu kurala dokunmaz.", "Kaldırılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Ne yapılacağını anlatıp onay iste (varsayılan: Hayır).
        var onay = TemaliMesaj.Goster($"\"{k.Ad}\" kuralı Windows Güvenlik Duvarı'ndan KALDIRILACAK.\r\n\r\nSonuç: {k.Port} {(k.Yon == "Gelen" ? "numaralı kapıya gelen bağlantılar" : "hedefine giden bağlantılar")} artık engellenmeyecek (kapı eski haline döner).\r\nWindows sizden yönetici izni (UAC penceresi) isteyecek.\r\n\r\nDevam edilsin mi?", "Kilidi Aç - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        // Onaylamadıysa hiçbir şey yapma.
        if (onay != DialogResult.Yes) return;
        // Kuralı yönetici izniyle sil.
        var (tamam, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(GuvenlikDuvari.SilZinciri(new[] { k.Ad }));
        // İzin verilmediyse veya başlatılamadıysa söyle.
        if (!tamam) { TemaliMesaj.Goster(hata, "Kilit açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Gerçekten silindi mi diye Windows'a yeniden sor.
        bool hala = GuvenlikDuvari.Listele().Any(x => x.Ad == k.Ad);
        // Sonucu bildir.
        TemaliMesaj.Goster(hala ? "Kural hâlâ görünüyor; kaldırma tamamlanmamış olabilir. Listeyi yenileyip tekrar deneyin." : $"Kilit açıldı: \"{k.Ad}\" kaldırıldı.", hala ? "Doğrulanamadı" : "Tamam", MessageBoxButtons.OK, hala ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        // Listeyi tazele.
        Yenile();
        // Kural kaldırıldı: dışarıdaki dinleyiciler (Savunma Merkezi'ndeki Acil İzolasyon çubuğu) anında haberdar olur.
        if (!hala) KuralKaldirildi?.Invoke();
    }

    // Bir kalkan kuralı başarıyla kaldırılınca tetiklenir (kabuk Acil İzolasyon durumunu tazeler).
    public event Action? KuralKaldirildi;
}
