// Bu dosya bulguları listeleyen ortak sonuç penceresidir (yerel denetim ve aktif web denetimi kullanır): liste, seçilenin ayrıntısı ve "Yanlış Alarm" işaretleme.
// Pencere yalnızca bilgi verir; sistemde hiçbir şey değiştirmez. Yanlış alarm işareti kalıcı depoya (fp_takip.json) yazılır.
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Bulgu sonuç penceresi.
internal sealed class BulguSonucFormu : TemaliForm
{
    // Kontroller ve veri.
    private readonly DataGridView _t = new(); private readonly Label _alt = new(); private readonly TextBox _neden = new(); private readonly Button _isaret = new EfektliButon();
    private readonly List<Finding> _ham; private readonly FpDeposu _fp; private readonly string _kullanici; private List<Finding> _liste;

    public BulguSonucFormu(string baslik, string ustMetin, List<Finding> bulgular, FpDeposu fp, string kullanici)
    {
        _ham = bulgular; _fp = fp; _kullanici = kullanici; _liste = fp.Uygula(bulgular);
        Color zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), soluk = DesignTokens.Ozgun(TokenRol.TextMuted);
        Text = baslik; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1080, 640); MinimumSize = new Size(860, 500); BackColor = zemin; ForeColor = yazi; Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        var ust = new Label { Dock = DockStyle.Top, Height = 98, Padding = new Padding(14, 10, 14, 0), ForeColor = soluk, UseMnemonic = false, Text = ustMetin };
        _alt.Dock = DockStyle.Bottom; _alt.Height = 140; _alt.Padding = new Padding(14, 8, 14, 0); _alt.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _alt.ForeColor = yazi; _alt.UseMnemonic = false; _alt.Text = "Bir bulgu seçin: ne bulunduğu, neden önemli olduğu, kanıt ve çözüm burada görünür.";
        // Alt çubuk: yanlış alarm işaretleme.
        _neden.Width = 420; _neden.PlaceholderText = "Neden yanlış alarm? (isteğe bağlı)"; _neden.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); _neden.ForeColor = yazi; _neden.BorderStyle = BorderStyle.FixedSingle;
        _isaret.Text = "Yanlış Alarm İşaretle / Geri Al"; _isaret.Size = new Size(240, 30); _isaret.FlatStyle = FlatStyle.Flat; _isaret.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); _isaret.ForeColor = Color.White; _isaret.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); _isaret.Cursor = Cursors.Hand; _isaret.Margin = new Padding(8, 0, 0, 0);
        var cubuk = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(10, 6, 0, 0), WrapContents = false }; _neden.Margin = new Padding(0, 3, 0, 0); cubuk.Controls.AddRange(new Control[] { _neden, _isaret });
        _t.Dock = DockStyle.Fill; _t.ReadOnly = true; _t.AllowUserToAddRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = false; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false; _t.RowTemplate.Height = 28;
        _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _t.DefaultCellStyle.BackColor = kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        foreach (var (ad, b, a) in new[] { ("On", "Önem", 8), ("Sk", "Skor", 5), ("Ku", "Kural", 16), ("Ba", "Başlık", 56), ("Du", "Durum", 15) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; }
        KoyuTema.UygulaTablo(_t);
        Controls.Add(_t); Controls.Add(_alt); Controls.Add(cubuk); Controls.Add(ust);
        Doldur();
        _t.SelectionChanged += (_, _) => Ayrinti(); _isaret.Click += (_, _) => Isaretle();
    }

    // Tabloyu doldurur (yanlış alarmlar soluk gösterilir, en sona alınır).
    private void Doldur()
    {
        string? secili = (_t.CurrentRow?.Tag as Finding)?.Id; _t.Rows.Clear();
        foreach (var f in _liste.OrderBy(x => x.State == FindingState.FalsePositive).ThenByDescending(x => x.Score))
        {
            string durum = f.State == FindingState.FalsePositive ? "Yanlış alarm" : f.FalsePositiveTracking.State == FpState.Suspected ? "⚠ Kural gözden geçirilmeli" : "";
            int i = _t.Rows.Add(f.EskiOnem switch { Onem.Yuksek => "Yüksek", Onem.Orta => "Orta", Onem.Dusuk => "Düşük", _ => "Bilgi" }, f.Score, f.RuleId, f.Title, durum); _t.Rows[i].Tag = f;
            if (f.State == FindingState.FalsePositive) _t.Rows[i].DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted);
            if (f.Id == secili) _t.CurrentCell = _t.Rows[i].Cells[0];
        }
    }

    // Seçilenin ayrıntısı.
    private void Ayrinti()
    {
        if (_t.CurrentRow?.Tag is not Finding f) return;
        var fp = f.FalsePositiveTracking; string oran = fp.RuleTotalCount > 0 ? $" · bu kuralın yanlış alarm oranı: {fp.RuleFpCount}/{fp.RuleTotalCount}" : "";
        _alt.Text = $"{f.Explanation.What}\r\nNeden önemli: {f.Explanation.WhyItMatters}\r\nKanıt: {string.Join(" | ", f.Evidence.Take(6))}\r\nÇözüm: {f.Remediation.Summary}\r\nUyumluluk: {string.Join(", ", f.Compliance.Kvkk)} · ISO {string.Join(", ", f.Compliance.Iso27001)}{(f.Compliance.MitreAttack.Length > 0 ? " · MITRE " + string.Join(", ", f.Compliance.MitreAttack) : "")}{oran}";
    }

    // Seçili bulguyu yanlış alarm olarak işaretler veya işareti geri alır; depo hemen yazılır.
    private void Isaretle()
    {
        if (_t.CurrentRow?.Tag is not Finding f) { TemaliMesaj.Goster(this, "Önce listeden bir bulgu seçin.", "Yanlış alarm", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try
        {
            if (_fp.Isaretli(f.Id)) _fp.Geri(f.Id);
            else if (TemaliMesaj.Goster(this, $"\"{f.Title}\" yanlış alarm olarak işaretlensin mi?\r\n\r\nBu bulgu listede soluk görünür ve kuralın yanlış alarm oranına eklenir. Her zaman geri alabilirsiniz.", "Yanlış alarm", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes) _fp.Isaretle(f, _kullanici, _neden.Text.Trim(), DateTime.UtcNow);
            else return;
            _fp.Kaydet(); _liste = _fp.Uygula(_ham); Doldur(); Ayrinti();
        }
        catch (Exception ex) { TemaliMesaj.Goster(this, "Kaydedilemedi: " + ex.Message, "Yanlış alarm", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // Yerel denetim sonucu için pencere.
    public static BulguSonucFormu Yerel(YerelDenetimSonucu s, KbCatalog? katalog, FpDeposu fp, string kullanici, NvdIndeks? nvd = null, VulnFeedStore? kev = null)
    {
        string kat = katalog == null ? "Yama kataloğu YÜKLÜ DEĞİL: eksik güncellemeler değerlendirilemedi." : $"Katalog sürümü: {katalog.Surum} (özet {katalog.Ozet[..12]})";
        string veri = $"Yazılım: {s.YazilimSayisi} kurulu" + (s.YazilimNotlari is { Count: > 0 } ? $" ({s.YazilimNotlari.Count} atlandı: ürün kimliği veya sürüm yok)" : "") + " · " + (nvd == null ? "NVD verisi YOK" : nvd.Ozetle()) + " · " + (kev == null ? "KEV listesi YOK" : kev.Ozetle());
        string ust = $"İşletim sistemi: {s.Yama.OsBuild} · Kurulu güncelleme kaydı: {s.Yama.InstalledKbs.Count} · Eksik (katalogdan): {s.Yama.MissingKbs.Count}\r\n{kat}\r\n{veri}\r\n{s.Bulgular.Count} bulgu listelendi" + (s.Reddedilen.Count > 0 ? $" · {s.Reddedilen.Count} bulgu şemaya uymadığı için reddedildi." : ".") + " Bu pencere yalnızca bilgi verir; düzeltmeler elle yapılır.";
        return new BulguSonucFormu("Yerel Denetim Sonucu", ust, s.Bulgular, fp, kullanici);
    }
}
