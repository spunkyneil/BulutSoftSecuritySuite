// Bu dosya "Site Sahipliğini Doğrula" penceresini çizer: kullanıcıya gizli kodu ve iki yolu (dosya / DNS) gösterir, "Şimdi Doğrula" ile canlı kontrol eder.
// Pencere yalnızca KONTROL eder (siteye bir dosya GET'i ve bir DNS TXT sorgusu); kullanıcı doğrulayınca DialogResult.OK döner.
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Sahiplik doğrulama penceresi.
internal sealed class SahiplikFormu : TemaliForm
{
    private readonly string _host, _belirtec; private readonly TextBox _metin = new(); private readonly Label _durum = new(); private readonly Button _dogrula = new EfektliButon(), _kopyala = new EfektliButon(), _vazgec = new EfektliButon(); private CancellationTokenSource? _iptal;

    public SahiplikFormu(string host)
    {
        _host = host; _belirtec = SahiplikDogrulama.Belirtec(host);
        Text = "Site Sahipliğini Doğrula"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(720, 520); MinimumSize = new Size(620, 460); FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = false; ShowInTaskbar = false; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        // Başlık ve açıklama.
        var baslik = new Label { Text = $"{host} sitesinin sahibi olduğunuzu kanıtlayın", Dock = DockStyle.Top, Height = 44, Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), Padding = new Padding(16, 12, 16, 0), UseMnemonic = false };
        var acik = new Label { Text = "Derin denetim, sitenize ek nazik istekler gönderir (gizli dosya, açık yöntem, hata sayfası kontrolleri). Bunu yalnızca sitenin sahibi/yöneticisi yapabilmelidir; bu yüzden kısa bir kanıt istenir.", Dock = DockStyle.Top, Height = 64, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Padding = new Padding(16, 4, 16, 0), UseMnemonic = false };
        // Talimat kutusu.
        _metin.Multiline = true; _metin.ReadOnly = true; _metin.ScrollBars = ScrollBars.Vertical; _metin.Dock = DockStyle.Fill; _metin.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _metin.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _metin.BorderStyle = BorderStyle.None; _metin.Font = FontProvider.Al(FontRol.Mono); _metin.Text = SahiplikDogrulama.Talimat(host, _belirtec); _metin.TabStop = false;
        // Alt şerit.
        var alt = new Panel { Dock = DockStyle.Bottom, Height = 92, Padding = new Padding(16, 8, 16, 10) }; _durum.Dock = DockStyle.Top; _durum.Height = 36; _durum.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted); _durum.UseMnemonic = false; _durum.Text = "Hazır olduğunuzda \"Şimdi Doğrula\"ya basın.";
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        Stil(_vazgec, "Vazgeç", 120, DesignTokens.Ozgun(TokenRol.BorderStrong)); Stil(_dogrula, "Şimdi Doğrula", 170, DesignTokens.Ozgun(TokenRol.StatusSuccess)); Stil(_kopyala, "Gizli Kodu Kopyala", 190, DesignTokens.Ozgun(TokenRol.AccentPrimary));
        dugmeler.Controls.AddRange(new Control[] { _vazgec, _dogrula, _kopyala }); alt.Controls.Add(dugmeler); alt.Controls.Add(_durum);
        _vazgec.Click += (_, _) => { _iptal?.Cancel(); DialogResult = DialogResult.Cancel; Close(); };
        _kopyala.Click += (_, _) => { try { Clipboard.SetText(_belirtec); _durum.Text = "Gizli kod panoya kopyalandı."; } catch { _durum.Text = "Panoya kopyalanamadı; kodu elle seçip kopyalayın."; } };
        _dogrula.Click += (_, _) => GuvenliCalistir.Baslat(() => DogrulaAsync(), "SahiplikFormu");
        Controls.Add(_metin); Controls.Add(alt); Controls.Add(acik); Controls.Add(baslik); CancelButton = _vazgec;
        FormClosing += (_, _) => _iptal?.Cancel();
    }

    // Düğme stili.
    private static void Stil(Button b, string yazi, int g, Color renk) { b.Text = yazi; b.Size = new Size(g, 38); b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = renk; b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(8, 0, 0, 0); }

    // Canlı doğrulama (internete çıkar: yalnızca bu siteye GET ve bir DNS TXT sorgusu).
    private async Task DogrulaAsync()
    {
        _dogrula.Enabled = false; _durum.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted); _durum.Text = "Kontrol ediliyor..."; _iptal = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try
        {
            var s = await SahiplikDogrulama.DogrulaAsync(_host, _iptal.Token);
            if (IsDisposed) return; _durum.ForeColor = s.Dogrulandi ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusWarning); _durum.Text = s.Mesaj;
            if (s.Dogrulandi) { ServisBaglami.OlayYazici.Ekle("Site Sahipliği", "Bilgi", $"Site sahipliği doğrulandı ({s.Yontem}): {_host}"); await Task.Delay(900); DialogResult = DialogResult.OK; Close(); }
        }
        catch (OperationCanceledException) { if (!IsDisposed) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); _durum.Text = "Kontrol zaman aşımına uğradı; tekrar deneyin."; } }
        catch (Exception ex) { if (!IsDisposed) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Doğrulanamadı: " + ex.Message; } }
        finally { if (!IsDisposed) _dogrula.Enabled = true; }
    }
}
