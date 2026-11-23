// Bu dosya "Claude AI ile Derin Analiz" GÖNDERİM ONAY penceresidir (KVKK güvenli çıkış): dışarı çıkacak metnin AYNISINI (maskelenmiş) önizletir ve gönderimi kullanıcıya bırakır.
// Üstteki onay kutusu "özel IP son oktetlerini, kullanıcı adlarını ve yerel disk yollarını maskele" seçeneğidir (varsayılan AÇIK; tercih GorselAyarlar.ClaudeMaskele'de saklanır).
// Kutu kapatılırsa önizleme ham metni gösterir; bu durumda da gönderilen metin tam olarak ekrandakidir. Varsayılan düğme "Vazgeç"tir: hiçbir şey kullanıcı onayı olmadan gitmez.
// Renkler DesignTokens, yazı tipleri FontProvider'dan alınır (CLAUDE.md kural 3).

// Programımızın adı.
namespace LabDenetim;

// Gönderim onayı ve maskelenmiş önizleme.
internal sealed class ClaudeOnayFormu : TemaliForm
{
    private readonly string _ham; private readonly CheckBox _maske = new(); private readonly TextBox _onizleme = new(); private readonly Label _sayac = new();

    public ClaudeOnayFormu(string metin, MaskeSecenekleri? secenek = null)
    {
        _ham = metin ?? ""; _secenek = secenek ?? MaskeSecenekleri.Varsayilan();
        Text = "Gönderim Onayı — Claude AI"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(820, 640); MinimumSize = new Size(640, 480);
        BackColor = DesignTokens.SurfaceApp; ForeColor = DesignTokens.TextPrimary; Font = FontProvider.Al(FontRol.Body);

        var bilgi = new Label { Dock = DockStyle.Fill, AutoSize = false, UseMnemonic = false, ForeColor = DesignTokens.TextPrimary, Font = FontProvider.Al(FontRol.Body), Margin = new Padding(0, 0, 0, DesignTokens.SpaceSm),
            Text = "Şu bilgiler internet üzerinden Anthropic'in Claude servisine (api.anthropic.com) GÖNDERİLECEK: hedef adres ve tarama özeti, bulunan portlar ve bulgular (başlık, ayrıntı, öneri). API anahtarınız yalnızca kimlik doğrulama için gider.\r\n\r\nAşağıdaki kutu, dışarı çıkacak metnin AYNISIDIR. Göndermeden önce gözden geçirin." };
        _maske.Text = "Özel IP son oktetlerini, kullanıcı adlarını ve yerel disk yollarını maskele (önerilir)"; _maske.AutoSize = true; _maske.UseMnemonic = false; _maske.Checked = GorselAyarlar.ClaudeMaskele; _maske.ForeColor = DesignTokens.TextPrimary; _maske.Font = FontProvider.Al(FontRol.Subheading); _maske.Margin = new Padding(0, DesignTokens.SpaceXs, 0, DesignTokens.SpaceXs);
        _sayac.AutoSize = true; _sayac.UseMnemonic = false; _sayac.ForeColor = DesignTokens.TextSecondary; _sayac.Font = FontProvider.Al(FontRol.Caption); _sayac.Margin = new Padding(0, 0, 0, DesignTokens.SpaceSm);
        _onizleme.Multiline = true; _onizleme.ReadOnly = true; _onizleme.ScrollBars = ScrollBars.Both; _onizleme.WordWrap = false; _onizleme.Dock = DockStyle.Fill; _onizleme.Font = FontProvider.Al(FontRol.Mono); _onizleme.BackColor = DesignTokens.SurfaceInput; _onizleme.ForeColor = DesignTokens.TextPrimary; _onizleme.BorderStyle = BorderStyle.FixedSingle; _onizleme.AccessibleName = "Gönderilecek metin önizlemesi"; _onizleme.TabStop = true;
        var gonder = Dugme("Gönder", true); var vazgec = Dugme("Vazgeç", false);
        gonder.Click += (_, _) => { GorselAyarlar.ClaudeMaskele = _maske.Checked; DialogResult = DialogResult.OK; Close(); };
        vazgec.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(0, DesignTokens.SpaceMd, 0, 0) }; dugmeler.Controls.Add(vazgec); dugmeler.Controls.Add(gonder);
        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(DesignTokens.SpaceXl, DesignTokens.SpaceLg, DesignTokens.SpaceXl, DesignTokens.SpaceLg) };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 112)); tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        tbl.Controls.Add(bilgi, 0, 0); tbl.Controls.Add(_maske, 0, 1); tbl.Controls.Add(_sayac, 0, 2); tbl.Controls.Add(_onizleme, 0, 3); tbl.Controls.Add(dugmeler, 0, 4);
        Controls.Add(tbl); CancelButton = vazgec; ActiveControl = vazgec;   // varsayılan seçili düğme "Vazgeç": Enter ile kazara gönderilmez
        _maske.CheckedChanged += (_, _) => OnizlemeyiYenile(); OnizlemeyiYenile();
    }

    private readonly MaskeSecenekleri _secenek;

    // Ekrandaki önizleme metni (gönderilecek metinle aynıdır).
    public string OnizlemeMetni => _onizleme.Text;
    // Maskeleme kutusu işaretli mi?
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool MaskeleAcik { get => _maske.Checked; set => _maske.Checked = value; }
    // Onaylanırsa dışarı gidecek metin (önizlemeyle birebir aynı).
    public string GonderilecekMetin => _onizleme.Text;

    // Seçime göre maskelenmiş ya da ham metni gösterir ve gizlenen öğe sayısını yazar.
    private void OnizlemeyiYenile()
    {
        if (_maske.Checked) { var s = VeriMaskeleyici.Maskele(_ham, _secenek); _onizleme.Text = s.Metin; _sayac.Text = s.Toplam == 0 ? "Maskelenecek bir öğe bulunmadı." : $"Gizlendi: {s.IpSayisi} özel IP, {s.YolSayisi} yerel yol, {s.AdSayisi} kullanıcı/bilgisayar adı."; }
        else { _onizleme.Text = _ham; _sayac.Text = "UYARI: Maskeleme kapalı — metin olduğu gibi gönderilecek."; _sayac.ForeColor = DesignTokens.StatusWarning; return; }
        _sayac.ForeColor = DesignTokens.TextSecondary;
    }

    private static Button Dugme(string metin, bool birincil) => new()
    {
        Text = metin, Size = new Size(120, 36), Margin = new Padding(DesignTokens.SpaceSm, 0, 0, 0), UseMnemonic = false, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = FontProvider.Al(FontRol.Subheading),
        BackColor = birincil ? DesignTokens.AccentPrimary : DesignTokens.SurfaceElevated, ForeColor = birincil ? DesignTokens.TextOnAccent : DesignTokens.TextPrimary
    };

    // Onay penceresini gösterir: onaylanırsa gönderilecek (maskelenmiş ya da ham) metni, vazgeçilirse null döner.
    public static string? Sor(IWin32Window sahip, string metin)
    {
        using var f = new ClaudeOnayFormu(metin);
        return f.ShowDialog(sahip) == DialogResult.OK ? f.GonderilecekMetin : null;
    }
}
