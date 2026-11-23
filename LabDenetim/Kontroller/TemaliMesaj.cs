// Bu dosya programın TEMA UYUMLU mesaj penceresini içerir. Windows'un kendi MessageBox'ı her zaman açık renkli ve tema yöneticisine kapalıdır;
// bu yüzden Koyu temada uygulamanın ortasında beyaz pencereler çıkıyordu. TemaliMesaj.Goster, MessageBox.Show ile AYNI parametreleri ve AYNI sonuçları (DialogResult) kullanır;
// pencere TemaYonetici tarafından Koyu/Açık temaya göre boyanır, düğmeler programın animasyonlu düğmesidir, uzun metin kaydırılır, ekran ölçeklemesine (DPI) uyar.
using System.Drawing.Drawing2D;

// Programımızın adı.
namespace LabDenetim;

internal static class TemaliMesaj
{
    // MessageBox.Show ile aynı biçimli aşırı yüklemeler.
    public static DialogResult Goster(string metin) => Goster(null, metin, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(string metin, string baslik) => Goster(null, metin, baslik, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(string metin, string baslik, MessageBoxButtons d) => Goster(null, metin, baslik, d, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(string metin, string baslik, MessageBoxButtons d, MessageBoxIcon i) => Goster(null, metin, baslik, d, i, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(string metin, string baslik, MessageBoxButtons d, MessageBoxIcon i, MessageBoxDefaultButton v) => Goster(null, metin, baslik, d, i, v);
    public static DialogResult Goster(IWin32Window? sahip, string metin) => Goster(sahip, metin, "", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(IWin32Window? sahip, string metin, string baslik) => Goster(sahip, metin, baslik, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(IWin32Window? sahip, string metin, string baslik, MessageBoxButtons d) => Goster(sahip, metin, baslik, d, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
    public static DialogResult Goster(IWin32Window? sahip, string metin, string baslik, MessageBoxButtons d, MessageBoxIcon i) => Goster(sahip, metin, baslik, d, i, MessageBoxDefaultButton.Button1);

    // Asıl iş. Arka plan iş parçacığından çağrılırsa arayüz iş parçacığına aktarılır.
    public static DialogResult Goster(IWin32Window? sahip, string metin, string baslik, MessageBoxButtons dugmeler, MessageBoxIcon simge, MessageBoxDefaultButton varsayilan)
    {
        var ana = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
        if (ana != null && ana.IsHandleCreated && ana.InvokeRequired) return (DialogResult)ana.Invoke(new Func<DialogResult>(() => Goster(sahip, metin, baslik, dugmeler, simge, varsayilan)));
        // Pencere kurulamazsa (çok nadir; örn. tema altyapısı henüz hazır değilken) Windows'un kendi penceresine düşülür; mesaj asla kaybolmaz.
        // Bu yedek, çözümdeki TEK doğrudan MessageBox.Show çağrısıdır (bir test başka hiçbir yerde olmamasını denetler).
        try
        {
            if (PencereHatasiZorla) throw new InvalidOperationException("Sınama: pencere kurulumu başarısız.");
            using var f = new Pencere(metin ?? "", baslik ?? "", dugmeler, simge, varsayilan, sahip is Form || sahip is Control); return sahip != null ? f.ShowDialog(sahip) : f.ShowDialog();
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "TemaliMesaj.Goster", true); return YedekPenceresi(sahip, metin ?? "", baslik ?? "", dugmeler, simge, varsayilan); }
    }

    // Acil durum yedeği: Windows'un kendi mesaj kutusu (testte değiştirilebilir).
    internal static Func<IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon, MessageBoxDefaultButton, DialogResult> YedekPenceresi { get; set; } =
        (sahip, metin, baslik, dugmeler, simge, varsayilan) => sahip != null ? MessageBox.Show(sahip, metin, baslik, dugmeler, simge, varsayilan) : MessageBox.Show(metin, baslik, dugmeler, simge, varsayilan);
    // Yalnızca sınama için: pencere kurulumu hatasını zorlar (yedek yolunu doğrulamak için).
    internal static bool PencereHatasiZorla { get; set; }

    // Pencereyi GÖSTERMEDEN kurar (sınama): erken başlangıçta (tema başlatılmadan) kurulabildiğini doğrulamak için.
    internal static Form PencereOlustur(string metin, string baslik, MessageBoxButtons dugmeler, MessageBoxIcon simge) => new Pencere(metin, baslik, dugmeler, simge, MessageBoxDefaultButton.Button1, false);

    // Düğme listesi (metin, sonuç).
    private static (string, DialogResult)[] Liste(MessageBoxButtons d) => d switch
    {
        MessageBoxButtons.OKCancel => new[] { ("Tamam", DialogResult.OK), ("İptal", DialogResult.Cancel) },
        MessageBoxButtons.YesNo => new[] { ("Evet", DialogResult.Yes), ("Hayır", DialogResult.No) },
        MessageBoxButtons.YesNoCancel => new[] { ("Evet", DialogResult.Yes), ("Hayır", DialogResult.No), ("İptal", DialogResult.Cancel) },
        MessageBoxButtons.RetryCancel => new[] { ("Yeniden Dene", DialogResult.Retry), ("İptal", DialogResult.Cancel) },
        MessageBoxButtons.AbortRetryIgnore => new[] { ("Durdur", DialogResult.Abort), ("Yeniden Dene", DialogResult.Retry), ("Yoksay", DialogResult.Ignore) },
        _ => new[] { ("Tamam", DialogResult.OK) }
    };

    // Pencerenin kendisi.
    private sealed class Pencere : TemaliForm
    {
        private readonly MessageBoxIcon _simge; private readonly Button[] _dugmeler; private readonly DialogResult _kacis;

        public Pencere(string metin, string baslik, MessageBoxButtons dugmeler, MessageBoxIcon simge, MessageBoxDefaultButton varsayilan, bool sahipli)
        {
            _simge = simge; float s = DeviceDpi / 96f; Func<int, int> P = x => (int)Math.Round(x * s);
            // Renkler Koyu temadaki özgün değerlerdir; TemaYonetici geçerli temaya çevirir.
            Text = string.IsNullOrEmpty(baslik) ? UrunBilgisi.Ad : baslik; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false; AutoScaleMode = AutoScaleMode.None;
            StartPosition = sahipli ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen; TopMost = !sahipli; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Body); KeyPreview = true;
            var liste = Liste(dugmeler); var calisma = Screen.FromPoint(Cursor.Position).WorkingArea;
            // Metin alanı: genişlik 460-620 px (ölçekli), yükseklik metne göre; ekranın %60'ını aşarsa kaydırılır.
            int metinGen = P(430), simgeGen = P(64), kenar = P(22), dugmeBoy = P(36), serit = dugmeBoy + P(34);
            var ol = TextRenderer.MeasureText(metin, Font, new Size(metinGen, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            if (ol.Height > P(330) && metin.Length > 600) metinGen = P(560);
            ol = TextRenderer.MeasureText(metin, Font, new Size(metinGen, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            int enYuk = (int)(calisma.Height * 0.6) - serit - kenar * 2, metinYuk = Math.Min(Math.Max(ol.Height + P(6), P(40)), Math.Max(P(60), enYuk)); bool kaydir = ol.Height + P(6) > metinYuk;
            int dugmeGen = P(104), dugmeAralik = P(10), dugmeToplam = liste.Length * dugmeGen + (liste.Length - 1) * dugmeAralik;
            int icGen = Math.Max(simgeGen + metinGen + kenar, dugmeToplam + kenar * 2); ClientSize = new Size(kenar + icGen + kenar - P(0), kenar + Math.Max(metinYuk, P(48)) + serit);
            // Simge (çizilir).
            var simgeKutu = new Panel { Location = new Point(kenar, kenar), Size = new Size(P(48), P(48)), BackColor = BackColor }; simgeKutu.Paint += SimgeBoya; Controls.Add(simgeKutu);
            // Metin (uzunsa kaydırılabilir kutuda).
            var pan = new Panel { Location = new Point(kenar + simgeGen, kenar), Size = new Size(metinGen + P(4), Math.Max(metinYuk, P(48))), AutoScroll = kaydir, BackColor = BackColor };
            var lb = new Label { Text = metin, AutoSize = false, Location = new Point(0, 0), Size = new Size(metinGen, kaydir ? ol.Height + P(6) : metinYuk), UseMnemonic = false, BackColor = BackColor, ForeColor = ForeColor, Font = Font };
            pan.Controls.Add(lb); Controls.Add(pan);
            // Düğmeler (sağa hizalı). Renkler Koyu temadaki özgün değerlerdir.
            _dugmeler = new Button[liste.Length]; int x = ClientSize.Width - kenar - dugmeToplam;
            for (int i = 0; i < liste.Length; i++)
            {
                var b = new EfektliButon { Text = liste[i].Item1, DialogResult = liste[i].Item2, Size = new Size(dugmeGen, dugmeBoy), Location = new Point(x + i * (dugmeGen + dugmeAralik), ClientSize.Height - serit + P(16)), FlatStyle = FlatStyle.Flat, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, TabIndex = i };
                bool birincil = i == (int)varsayilan / 256; b.BackColor = birincil ? ColorTranslator.FromHtml("#0C4A6E") : ColorTranslator.FromHtml("#1E293B"); b.ForeColor = Color.White; _dugmeler[i] = b; Controls.Add(b);
            }
            int vi = Math.Clamp((int)varsayilan / 256, 0, liste.Length - 1); AcceptButton = _dugmeler[vi]; ActiveControl = _dugmeler[vi];
            // Esc: İptal varsa İptal, yoksa Hayır, yoksa Tamam/Yoksay anlamına gelir (Windows'taki gibi çarpı olmayan YesNo'da güvenli seçenek: Hayır).
            _kacis = liste.Any(l => l.Item2 == DialogResult.Cancel) ? DialogResult.Cancel : liste.Any(l => l.Item2 == DialogResult.No) ? DialogResult.No : liste.Any(l => l.Item2 == DialogResult.Ignore) ? DialogResult.Ignore : liste[0].Item2;
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = _kacis; Close(); } };
            FormClosing += (_, e) => { if (DialogResult == DialogResult.None) DialogResult = _kacis; };
            // Ctrl+C: metni panoya kopyalar (Windows'un mesaj kutusu gibi).
            KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.C) { try { Clipboard.SetText(Text + Environment.NewLine + Environment.NewLine + metin); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "TemaliMesaj.Pencere"); } } };
        }

        // Simgeyi vektör olarak çizer (temaya uygun renk).
        private void SimgeBoya(object? gonderen, PaintEventArgs e)
        {
            if (_simge == MessageBoxIcon.None) return; var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; var p = (Control)gonderen!; var r = new RectangleF(2, 2, p.Width - 5, p.Height - 5);
            Color renk = _simge switch { MessageBoxIcon.Error => ColorTranslator.FromHtml("#EF4444"), MessageBoxIcon.Warning => ColorTranslator.FromHtml("#F59E0B"), MessageBoxIcon.Question => ColorTranslator.FromHtml("#6366F1"), _ => ColorTranslator.FromHtml("#38BDF8") };
            renk = TemaYonetici.C(renk); using (var b = new SolidBrush(DesignTokens.Saydam(renk, 38))) g.FillEllipse(b, r); using (var k = new Pen(renk, 2f)) g.DrawEllipse(k, r);
            string glif = _simge switch { MessageBoxIcon.Error => "✕", MessageBoxIcon.Warning => "!", MessageBoxIcon.Question => "?", _ => "i" };
            using var f = FontProvider.Gecici("Segoe UI", r.Height * 0.46f, FontStyle.Bold, GraphicsUnit.Pixel); TextRenderer.DrawText(g, glif, f, Rectangle.Round(r), renk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
