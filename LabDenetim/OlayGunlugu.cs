// Bu dosya "Olay Zaman Çizgisi" penceresini ve menü düğmesini içerir; olay deposunun kendisi BulutSoft.Core içindedir (OlayDeposu.cs).
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// "Olay Zaman Çizgisi" penceresi.
internal sealed class OlayFormu : SayfaDenetimi
{
    // Tablo, arama kutusu ve özet yazısı.
    private readonly DataGridView _tablo = new(); private readonly TextBox _ara = new(); private readonly Label _ozet = new();
    // Okunan tüm olaylar.
    private List<OlayKaydi> _tum = new(); private int _sayfa, _toplam; private const int SayfaBoyutu = 1000;   // sayfalı tam tarama: açılışta en yeni 1000 olay, "Daha eski" ile sonraki sayfalar

    // Pencereyi kurar.
    public OlayFormu()
    {
        // Pencere ayarları.
        Text = "▥ Olay Zaman Çizgisi"; ClientSize = new Size(1000, 560); MinimumSize = new Size(780, 420); StartPosition = FormStartPosition.CenterParent; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Tablo ayarları.
        _tablo.Dock = DockStyle.Fill; _tablo.ReadOnly = true; _tablo.AllowUserToAddRows = false; _tablo.RowHeadersVisible = false; _tablo.MultiSelect = false; _tablo.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _tablo.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Koyu renkler.
        _tablo.BackgroundColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.BorderStyle = BorderStyle.None; _tablo.EnableHeadersVisualStyles = false; _tablo.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo);
        // Satır renkleri.
        _tablo.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _tablo.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.DefaultCellStyle.WrapMode = DataGridViewTriState.True; _tablo.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        // Sütunlar.
        _tablo.Columns.Add("Zaman", "Zaman"); _tablo.Columns.Add("Tur", "Tür"); _tablo.Columns.Add("Seviye", "Seviye"); _tablo.Columns.Add("Mesaj", "Olay");
        // Sütun ağırlıkları.
        _tablo.Columns[0].FillWeight = 18; _tablo.Columns[1].FillWeight = 12; _tablo.Columns[2].FillWeight = 10; _tablo.Columns[3].FillWeight = 100;
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(_tablo);
        // Arama kutusu.
        _ara.Dock = DockStyle.Fill; _ara.PlaceholderText = "Ara: örn. USB, KRİTİK, 445 ..."; _ara.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _ara.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _ara.BorderStyle = BorderStyle.FixedSingle; _ara.Font = FontProvider.Al(FontRol.Body);
        // Yazdıkça süz.
        _ara.TextChanged += (_, _) => Doldur();
        // Düğmeler.
        var csv = Dugme("▤ CSV Olarak Dışa Aktar", 200); var yenile = Dugme("↻ Yenile", 110); var kapat = Dugme("Kapat", 90); var eski = Dugme("▼ Daha Eski Olaylar", 180);
        // Tıklamalar.
        csv.Click += (_, _) => Aktar(); yenile.Click += (_, _) => Yukle(); eski.Click += (_, _) => DahaEski(); kapat.Click += (_, _) => Close();
        // Alt düğme paneli.
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; dugmeler.Controls.AddRange(new Control[] { kapat, yenile, eski, csv });
        // Özet yazısı.
        _ozet.Dock = DockStyle.Fill; _ozet.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted); _ozet.UseMnemonic = false;
        // Ana ızgara: arama, özet, tablo, düğmeler.
        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14) };
        // Satır boyları.
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        // Yerleştir.
        tbl.Controls.Add(_ara, 0, 0); tbl.Controls.Add(_ozet, 0, 1); tbl.Controls.Add(_tablo, 0, 2); tbl.Controls.Add(dugmeler, 0, 3); Controls.Add(tbl);
        // Açılınca yükle.
        Shown += (_, _) => Yukle();
    }

    // Koyu düğme üretir.
    private static Button Dugme(string metin, int g) => new EfektliButon() { Text = metin, Size = new Size(g, 34), Margin = new Padding(8, 4, 0, 0), UseMnemonic = false, FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand };

    // Olayları dosyadan okuyup tabloyu doldurur.
    private void Yukle() { _sayfa = 0; var (s, top) = ServisBaglami.OlayOkuyucu.OkuSayfali(0, SayfaBoyutu); _tum = s.Where(o => !KullaniciGunlugu.ZamanCizgisindeGizli(o.Tur)).ToList(); _toplam = top; Doldur(); }

    // Bir sonraki (daha eski) sayfayı listeye ekler.
    private void DahaEski() { if ((_sayfa + 1) * SayfaBoyutu >= _toplam) return; _sayfa++; var (s, top) = ServisBaglami.OlayOkuyucu.OkuSayfali(_sayfa, SayfaBoyutu); _tum = _tum.Concat(s.Where(o => !KullaniciGunlugu.ZamanCizgisindeGizli(o.Tur))).ToList(); _toplam = top; Doldur(); }

    // Arama kutusuna göre tabloyu doldurur.
    private void Doldur()
    {
        // Aranan metin.
        string a = _ara.Text.Trim();
        // Süzülmüş liste.
        var l = _tum.Where(o => a.Length == 0 || o.Mesaj.Contains(a, StringComparison.OrdinalIgnoreCase) || o.Tur.Contains(a, StringComparison.OrdinalIgnoreCase) || o.Seviye.Contains(a, StringComparison.OrdinalIgnoreCase)).ToList();
        // Tabloyu boşalt.
        _tablo.Rows.Clear();
        // Satırları ekle ve seviyeye göre boya.
        foreach (var o in l) { int i = _tablo.Rows.Add(o.Zaman.ToString("dd.MM.yyyy HH:mm:ss"), o.Tur, o.Seviye, o.Mesaj); _tablo.Rows[i].Tag = o; _tablo.Rows[i].Cells[2].Style.ForeColor = o.Seviye == "Kritik" ? DesignTokens.Ozgun(TokenRol.StatusDanger) : o.Seviye == "Uyarı" ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess); }
        // Özet yazısı.
        _ozet.Text = _tum.Count == 0 ? "Henüz kayıtlı olay yok. USB takılması, nöbetçi uyarıları, izolasyon ve tarama sonuçları burada birikir." : $"{l.Count} / {_tum.Count} olay gösteriliyor (toplam kayıt: {_toplam}{(_tum.Count < _toplam ? "; daha eskisi için \"Daha Eski Olaylar\"" : "")}) · kritik: {_tum.Count(o => o.Seviye == "Kritik")} · dosya: {OlayDeposu.Yol}";
    }

    // Görünen olayları .csv olarak masaüstüne kaydeder.
    private void Aktar()
    {
        // Kaydetmeyi dene.
        try
        {
            // Hedef yol.
            string yol = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"BulutSoft_Olaylar_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            // Görünen satırları yaz.
            OlayDeposu.CsvYaz(yol, _tablo.Rows.Cast<DataGridViewRow>().Select(r => (OlayKaydi)r.Tag!));
            // Bildir.
            TemaliMesaj.Goster("Masaüstüne kaydedildi:\r\n" + Path.GetFileName(yol), "Dışa aktarıldı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        // Hata olursa göster.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
