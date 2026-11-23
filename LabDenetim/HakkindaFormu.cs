// Bu dosya "ⓘ Hakkımızda & Destek" penceresini çizer: geliştirici, proje bilgisi, destek e-postası ve güvenlik ilkesi.
namespace LabDenetim;

// Hakkımızda penceresi.
internal sealed class HakkindaFormu : TemaliForm
{
    // Destek e-posta adresi.
    private static string Eposta => BrandConfig.Aktif.DestekEposta;
    // Kopyalandı bilgisini gösteren küçük yazı.
    private readonly Label _kopyalandi;

    // Pencereyi kurar.
    public HakkindaFormu()
    {
        // Pencere ayarları.
        Text = "ⓘ Hakkımızda & Destek"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
        // Boyut ve koyu tema.
        ClientSize = new Size(640, 670); BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Üstte marka bandı: şimşekli marka adı.
        var marka = new Label { Text = "⚡ " + UrunBilgisi.AdVeSurum, Location = new Point(28, 20), AutoSize = true, ForeColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), UseMnemonic = false };
        // Markanın altında elektrik mavisi ince çizgi.
        var cizgi = new Panel { Location = new Point(28, 62), Size = new Size(584, 3), BackColor = DesignTokens.Ozgun(TokenRol.StatusInfo) };
        // 1. bölüm başlığı.
        var b1 = Baslik("GELİŞTİRİCİ & MİMAR", 80);
        // Geliştirici adı.
        var ad = new Label { Text = "İbrahim Bulut", Location = new Point(28, 104), AutoSize = true, Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), UseMnemonic = false };
        // 2. bölüm başlığı.
        var b2 = Baslik("PROJE HAKKINDA", 144);
        // Proje açıklaması.
        var hakkinda = new Label { Location = new Point(28, 168), Size = new Size(584, 84), UseMnemonic = false, ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary), Font = FontProvider.Al(FontRol.Body), Text = UrunBilgisi.Ad + "; yerel ağ ve sistem güvenliği denetimlerini herkes için anlaşılır, hızlı ve erişilebilir kılmak amacıyla C# (.NET) ve özel GDI+ arayüz mimarisi kullanılarak İbrahim Bulut tarafından geliştirilmiştir." };
        // 3. bölüm başlığı.
        var b3 = Baslik("DESTEK VE İLETİŞİM", 268);
        // E-posta adresi (eşit aralıklı yazı tipiyle).
        var posta = new Label { Text = Eposta, Location = new Point(28, 294), AutoSize = true, ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess), Font = FontProvider.Al(FontRol.Mono, FontStyle.Bold), UseMnemonic = false };
        // E-postayı kopyalayan düğme.
        var kopyala = new EfektliButon { Text = "✉ E-Posta Adresini Kopyala", Location = new Point(28, 330), Size = new Size(240, 36), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false };
        // Düğmenin ince mavi kenarlığı.
        kopyala.FlatAppearance.BorderSize = 1; kopyala.FlatAppearance.BorderColor = DesignTokens.Ozgun(TokenRol.StatusInfo);
        // "Kopyalandı" bilgisi (başta boş).
        _kopyalandi = new Label { Location = new Point(282, 339), AutoSize = true, ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Text = "" };
        // Tıklanınca adresi panoya kopyala.
        kopyala.Click += (_, _) =>
        {
            // Pano bazen meşgul olabilir.
            try { Clipboard.SetText(Eposta); _kopyalandi.Text = "✓ Kopyalandı"; }
            // Olmazsa kibarca söyle.
            catch { _kopyalandi.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); _kopyalandi.Text = "Kopyalanamadı, adresi elle seçin."; }
        };
        // 4. bölüm başlığı.
        var b4 = Baslik("GÜVENLİK İLKESİ NOTU", 388);
        // Güvenlik ilkesi kutusu (amber, dikkat çekici).
        var ilke = new Label { Location = new Point(28, 412), Size = new Size(584, 70), Padding = new Padding(12, 10, 12, 10), UseMnemonic = false, BackColor = DesignTokens.Ozgun(TokenRol.StatusWarningBg), ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning), Font = FontProvider.Al(FontRol.Body), Text = "✦ Bu yazılım yalnızca kendi yerel ağınızdaki ve izole yetkili test ortamınızdaki sistemleri denetlemek üzere tasarlanmıştır." };
        // 5. bölüm başlığı: zafiyet veritabanı.
        var b5 = Baslik("ZAFİYET VERİTABANI", 500);
        // Veritabanını yenileyen düğme.
        var yenile = new EfektliButon { Text = "↻ Zafiyet Veritabanını Yenile", Location = new Point(28, 524), Size = new Size(240, 36), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false };
        // Düğmenin ince mavi kenarlığı.
        yenile.FlatAppearance.BorderSize = 1; yenile.FlatAppearance.BorderColor = DesignTokens.Ozgun(TokenRol.StatusInfo);
        // Sonucu gösteren küçük yazı (başlangıçta mevcut durum + dosya yolu).
        var vtDurum = new Label { Location = new Point(28, 566), Size = new Size(584, 44), UseMnemonic = false, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Font = FontProvider.Al(FontRol.Caption), Text = TehditVeritabani.SonDurum + "\r\nDosya: " + TehditVeritabani.Yol };
        // Tıklanınca dosyayı yeniden oku.
        yenile.Click += (_, _) =>
        {
            // Veritabanını yeniden yükle.
            var (basari, mesaj) = TehditVeritabani.Yenile();
            // Sonucu renkli göster: başarılıysa yeşil, değilse turuncu.
            vtDurum.ForeColor = basari ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusWarning);
            // Mesajı ve dosya yolunu yaz.
            vtDurum.Text = (basari ? "✓ " : "⚠ ") + mesaj + "\r\nDosya: " + TehditVeritabani.Yol;
        };
        // Kapat düğmesi.
        var kapat = new EfektliButon { Text = "Kapat", Location = new Point(512, 524), Size = new Size(100, 36), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand, DialogResult = DialogResult.Cancel };
        // Enter ve Esc pencereyi kapatsın.
        AcceptButton = kapat; CancelButton = kapat;
        // Tüm parçaları pencereye ekle.
        Controls.AddRange(new Control[] { marka, cizgi, b1, ad, b2, hakkinda, b3, posta, kopyala, _kopyalandi, b4, ilke, b5, yenile, vtDurum, kapat });
    }

    // Bölüm başlığı yazısı üretir (küçük, kalın, elektrik mavisi).
    private static Label Baslik(string metin, int y) => new() { Text = metin, Location = new Point(28, y), AutoSize = true, ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), UseMnemonic = false };
}
