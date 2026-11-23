// Bu dosya 3 adımlık "Tanıtım Turu" penceresini çizer.
namespace LabDenetim;

// Tanıtım turu penceresi: Geri / İleri düğmeleriyle 3 kısa adımı gösterir.
internal sealed class TurFormu : TemaliForm
{
    // Şu an gösterilen adımın sırası (0, 1, 2).
    private int _adim;
    // Başlık, açıklama ve nokta göstergesi yazıları.
    private readonly Label _baslik, _metin, _noktalar;
    // Geri, İleri ve Kapat düğmeleri.
    private readonly Button _geri, _ileri, _kapat;

    // Üç adımın başlığı ve açıklaması (sıfır bilgiye göre, sade dille).
    private static readonly (string Baslik, string Metin)[] Adimlar =
    {
        // 1. adım.
        ("1 / 3   Hedefi yazın",
         "Sol üstteki \"Hedef adres\" kutusuna kontrol edilecek bilgisayarın adresini yazın.\r\n\r\nHazır gelen 127.0.0.1, sizin kendi bilgisayarınızdır; ilk deneme için en güvenlisidir. Sanal Kali makinenizi denetlemek için \"◈ Hedef Seç / İç Ağ Keşfi\" düğmesine basın; program ağdaki makineleri bulur, siz seçersiniz.\r\n\r\nYanındaki \"Profil\" düğmelerinden taramanın ne kadar ayrıntılı olacağını seçebilirsiniz: Hızlı (az kapı, çabuk), Standart (önerilen), Kapsamlı (çok kapı, daha uzun sürer)."),
        // 2. adım.
        ("2 / 3   Taramayı başlatın",
         "Sarı \"⚡ TARAMAYI BAŞLAT\" butonuna basın.\r\n\r\nÜstteki kartlar canlı olarak değişir; alttaki siyah günlük programın o an ne yaptığını satır satır yazar.\r\n\r\nİsterseniz \"Durdur\" butonuyla işlemi istediğiniz an kesebilirsiniz."),
        // 3. adım.
        ("3 / 3   Sonuçları okuyun",
         "Sonuçlar renkli rozetlerle gösterilir:\r\nGÜVENLİ = normal, UYARI/DİKKAT = dikkat, KRİTİK = tehlike.\r\n\r\nBir satıra tıklayın: sağdaki Siber İlk Yardım Asistanı ne demek olduğunu, nasıl çözeceğinizi ve hazır çözüm komutunu gösterir.\r\n\r\nSonra \"Rapor İndir\" butonlarıyla raporu kaydedin.")
    };

    // Pencereyi kurar.
    public TurFormu()
    {
        // Pencere başlığı.
        Text = "⚡ Tanıtım Turu";
        // Sabit boyutlu, yeniden boyutlandırılamayan pencere.
        FormBorderStyle = FormBorderStyle.FixedDialog;
        // Küçültme ve büyütme düğmelerini kaldır.
        MaximizeBox = false; MinimizeBox = false;
        // Ana pencerenin ortasında aç.
        StartPosition = FormStartPosition.CenterParent;
        // Pencere boyutu.
        ClientSize = new Size(620, 340);
        // Koyu zemin ve açık yazı.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Adım başlığı: parlak sarı, büyük.
        _baslik = new Label { Location = new Point(24, 20), Size = new Size(570, 36), ForeColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold) };
        // Adım açıklaması.
        _metin = new Label { Location = new Point(24, 70), Size = new Size(570, 190), Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular) };
        // Nokta göstergesi (● ○ ○): hangi adımda olduğumuzu gösterir.
        _noktalar = new Label { Location = new Point(24, 285), Size = new Size(120, 28), ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold) };
        // Turu kapat düğmesi.
        _kapat = new EfektliButon { Text = "Turu Kapat", Location = new Point(250, 284), Size = new Size(110, 34), DialogResult = DialogResult.Cancel };
        // Geri düğmesi.
        _geri = new EfektliButon { Text = "◀ Geri", Location = new Point(380, 284), Size = new Size(100, 34) };
        // İleri düğmesi.
        _ileri = new EfektliButon { Text = "İleri ▶", Location = new Point(490, 284), Size = new Size(110, 34) };
        // Üç düğmeyi aynı düz görünüme boya.
        foreach (var b in new[] { _kapat, _geri, _ileri }) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.BackColor = DesignTokens.Ozgun(TokenRol.BorderStrong); b.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); b.Cursor = Cursors.Hand; }
        // İleri düğmesi şimşek sarısı olsun (ana eylem).
        _ileri.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); _ileri.ForeColor = Color.Black;
        // Geri düğmesine basılınca bir önceki adıma dön.
        _geri.Click += (_, _) => { _adim = Math.Max(0, _adim - 1); Goster(); };
        // İleri düğmesine basılınca sonraki adıma geç; son adımdaysa turu bitir.
        _ileri.Click += (_, _) => { if (_adim >= Adimlar.Length - 1) Close(); else { _adim++; Goster(); } };
        // Esc tuşu turu kapatsın.
        CancelButton = _kapat;
        // Enter tuşu "İleri" düğmesine basmış gibi olsun.
        AcceptButton = _ileri;
        // Tüm parçaları pencereye ekle.
        Controls.AddRange(new Control[] { _baslik, _metin, _noktalar, _kapat, _geri, _ileri });
        // İlk adımı göster.
        Goster();
    }

    // Şu anki adımın yazılarını ekrana yazar.
    private void Goster()
    {
        // Başlığı yaz.
        _baslik.Text = Adimlar[_adim].Baslik;
        // Açıklamayı yaz.
        _metin.Text = Adimlar[_adim].Metin;
        // Nokta göstergesini oluştur: şu anki adım dolu (●), diğerleri boş (○).
        _noktalar.Text = string.Join(" ", Enumerable.Range(0, Adimlar.Length).Select(i => i == _adim ? "●" : "○"));
        // İlk adımda "Geri" kapalı olsun.
        _geri.Enabled = _adim > 0;
        // Son adımda "İleri" yazısı "Başla" olsun.
        _ileri.Text = _adim == Adimlar.Length - 1 ? "Başla ⚡" : "İleri ▶";
    }
}
