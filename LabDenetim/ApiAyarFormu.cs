// Bu dosya "⚙ API Ayarı" penceresini çizer: kullanıcı Anthropic API anahtarını girip kaydeder.
// Anahtar yalnızca kullanıcının kendi klavyesinden girilir; program hiçbir yere göndermez (analiz isteği hariç).
namespace LabDenetim;

// API ayar penceresi.
internal sealed class ApiAyarFormu : TemaliForm
{
    // Anahtar ve model kutuları.
    private readonly TextBox _anahtar, _model;
    // Kayıtlı anahtar durumunu gösteren yazı.
    private readonly Label _durum;

    // Pencereyi kurar.
    public ApiAyarFormu()
    {
        // Pencere ayarları.
        Text = "⚙ API Ayarı"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        // Konum ve boyut.
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(600, 430);
        // Koyu tema.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Açıklama yazısı.
        var bilgi = new Label { Location = new Point(20, 16), Size = new Size(560, 150), UseMnemonic = false, ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary), Text =
            "Claude AI modunu kullanmak için kendi Anthropic API anahtarınızı girin (console.anthropic.com adresinden alınır).\r\n\r\n" +
            "• Anahtar bu bilgisayarda, yalnızca sizin Windows hesabınızın açabileceği şekilde ŞİFRELİ saklanır.\r\n" +
            "• Anahtar yalnızca siz \"Derin Analiz\" istediğinizde, Anthropic'in sunucusuna (api.anthropic.com) gönderilir.\r\n" +
            "• Not: claude.ai sohbet aboneliği API erişimi sağlamaz; API için ayrı kredi gerekir. Anahtarınız yoksa veya krediniz biterse program hata vermez, Yerel & Manuel Mod'da çalışmaya devam eder." };
        // Anahtar etiketi.
        var e1 = new Label { Text = "API anahtarı:", Location = new Point(20, 178), AutoSize = true, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold) };
        // Anahtar kutusu: yazılanlar nokta olarak görünür.
        _anahtar = new TextBox { Location = new Point(20, 202), Size = new Size(560, 26), UseSystemPasswordChar = true, PlaceholderText = "sk-ant-... (yeni anahtar girmezseniz kayıtlı anahtar korunur)", BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), BorderStyle = BorderStyle.FixedSingle };
        // Model etiketi.
        var e2 = new Label { Text = "Model adı:", Location = new Point(20, 242), AutoSize = true, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold) };
        // Model kutusu.
        _model = new TextBox { Location = new Point(20, 266), Size = new Size(560, 26), Text = ApiDeposu.AyarOku().Model, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), BorderStyle = BorderStyle.FixedSingle };
        // Kayıtlı anahtar durumu.
        _durum = new Label { Location = new Point(20, 304), Size = new Size(560, 22), ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold) };
        // Düğmeler.
        var kaydet = Dugme("Kaydet", new Point(20, 350), DesignTokens.Ozgun(TokenRol.AccentPrimary), Color.Black);
        // Anahtarı sil düğmesi.
        var sil = Dugme("Anahtarı Sil", new Point(160, 350), DesignTokens.Ozgun(TokenRol.StatusDangerBg), DesignTokens.Ozgun(TokenRol.StatusDanger));
        // İptal düğmesi.
        var iptal = Dugme("İptal", new Point(480, 350), DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Kaydet'e basılınca.
        kaydet.Click += (_, _) =>
        {
            // Hata olursa pencere çökmesin.
            try
            {
                // Yeni anahtar yazıldıysa şifreleyip kaydet.
                if (!string.IsNullOrWhiteSpace(_anahtar.Text)) ApiDeposu.AnahtarKaydet(_anahtar.Text);
                // Model adı boşsa varsayılanı kullan.
                string model = string.IsNullOrWhiteSpace(_model.Text) ? ApiDeposu.VarsayilanModel : _model.Text.Trim();
                // Ayarı kaydet (modu koru).
                ApiDeposu.AyarKaydet(ApiDeposu.AyarOku() with { Model = model });
                // Pencereyi kapat.
                DialogResult = DialogResult.OK; Close();
            }
            // Kaydedilemezse kullanıcıya söyle.
            catch (Exception ex) { TemaliMesaj.Goster("Kaydedilemedi: " + ex.Message, "Sorun oldu"); }
        };
        // Sil'e basılınca.
        sil.Click += (_, _) => { ApiDeposu.AnahtarSil(); _anahtar.Clear(); Guncelle(); };
        // İptal'e basılınca.
        iptal.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        // Enter = Kaydet, Esc = İptal.
        AcceptButton = kaydet; CancelButton = iptal;
        // Parçaları pencereye ekle.
        Controls.AddRange(new Control[] { bilgi, e1, _anahtar, e2, _model, _durum, kaydet, sil, iptal });
        // Kayıtlı anahtar durumunu yaz.
        Guncelle();
    }

    // Kayıtlı anahtar var mı yok mu yazar (anahtarın kendisini asla göstermez).
    private void Guncelle() => _durum.Text = ApiDeposu.AnahtarVarMi() ? "✓ Kayıtlı bir API anahtarı var (güvenlik için gösterilmez)." : "Kayıtlı API anahtarı yok.";

    // Koyu görünümlü düğme üretir.
    private static Button Dugme(string metin, Point yer, Color zemin, Color yazi) => new EfektliButon()
    {
        // Yazı, yer ve boyut.
        Text = metin, Location = yer, Size = new Size(120, 36),
        // Düz görünüm, renkler.
        FlatStyle = FlatStyle.Flat, BackColor = zemin, ForeColor = yazi, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand
    };
}
