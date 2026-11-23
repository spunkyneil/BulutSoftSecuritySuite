// Bu dosya sonuç tablosunun ilk sütunundaki renkli yuvarlak köşeli rozetleri (KRİTİK, UYARI, GÜVENLİ) çizer.
namespace LabDenetim;

// Form1'in dokuzuncu parçası: tablo rozetleri.
internal sealed partial class TaramaSayfasi
{
    // "Bu kontrol çalıştırılamadı" gibi, kontrolün hiç yapılamadığı sonuçları tanır.
    // Bunlar "güvenli" sayılmaz; sadece "bakılamadı" demektir.
    internal static bool Atlandi(Bulgu b) => b.Baslik == "Bu kontrol çalıştırılamadı";

    // Bir sonuç için rozetin yazısını ve neon rengini verir.
    internal static (string Metin, Color Renk) Rozet(Bulgu b)
    {
        // Kontrol yapılamadıysa gri "ATLANDI" rozeti.
        if (Atlandi(b)) return ("ATLANDI", DesignTokens.Ozgun(TokenRol.TextMuted));
        // Önem derecesine göre metin ve renk seç.
        return b.Onem switch
        {
            // Yüksek: parlak kırmızı KRİTİK.
            Onem.Yuksek => ("KRİTİK", DesignTokens.Ozgun(TokenRol.StatusDanger)),
            // Orta: parlak turuncu UYARI.
            Onem.Orta => ("UYARI", DesignTokens.Ozgun(TokenRol.StatusWarning)),
            // Düşük: sarı DİKKAT.
            Onem.Dusuk => ("DİKKAT", DesignTokens.Ozgun(TokenRol.StatusWarning)),
            // Bilgi: parlak yeşil GÜVENLİ.
            _ => ("GÜVENLİ", DesignTokens.Ozgun(TokenRol.StatusSuccess))
        };
    }

    // Tablonun her hücresi çizilirken çalışır; yalnızca ilk sütunda kendi rozetimizi çizeriz.
    internal void dgvBulgular_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        // İlk sütun ve gerçek bir satır değilse (başlık vb.) Windows'un kendi çizimi kalsın.
        if (e.ColumnIndex != 0 || e.RowIndex < 0) return;
        // Bu olay hangi tablodan geldi? (sonuç tablosu veya trafik tablosu)
        if (sender is not DataGridView tablo) return;
        // Satırın içindeki sonucu al; yoksa kendi çizimimizi yapma.
        if (tablo.Rows[e.RowIndex].Tag is not Bulgu b) return;
        // Önce hücrenin arka planını (satır rengi, seçiliyse seçim rengi) normal şekilde çiz.
        e.PaintBackground(e.ClipBounds, true);
        // Rozetin yazısını ve rengini öğren.
        var (metin, renk) = Rozet(b);
        // Rozet yazısının yazı tipi.
        using var font = FontProvider.Gecici("Segoe UI", 8.5f, FontStyle.Bold);
        // Yazının ne kadar yer kaplayacağını ölç.
        var yaziBoyu = TextRenderer.MeasureText(e.Graphics!, metin, font, Size.Empty, TextFormatFlags.NoPadding);
        // Rozetin boyu: yazı genişliği + iki yandan boşluk; yükseklik 22.
        int genislik = yaziBoyu.Width + 22, yukseklik = 22;
        // Rozetin yeri: hücrenin solundan 10 piksel içeride, dikeyde ortada.
        var kutu = new Rectangle(e.CellBounds.X + 10, e.CellBounds.Y + (e.CellBounds.Height - yukseklik) / 2, genislik, yukseklik);
        // Kenarları pürüzsüz çizmek için yumuşatmayı aç.
        e.Graphics!.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // Yuvarlak köşeli (hap şeklinde) yolu hazırla.
        using var yol = new System.Drawing.Drawing2D.GraphicsPath();
        // Köşe çapı yüksekliğe eşit: uçlar tam yuvarlak olsun.
        int cap = yukseklik;
        // Sol üst yay.
        yol.AddArc(kutu.X, kutu.Y, cap, cap, 180, 90);
        // Sağ üst yay.
        yol.AddArc(kutu.Right - cap, kutu.Y, cap, cap, 270, 90);
        // Sağ alt yay.
        yol.AddArc(kutu.Right - cap, kutu.Bottom - cap, cap, cap, 0, 90);
        // Sol alt yay.
        yol.AddArc(kutu.X, kutu.Bottom - cap, cap, cap, 90, 90);
        // Yolu kapat.
        yol.CloseFigure();
        // Rozetin içi: neon rengin saydam ve koyu hali (yarı saydam zemin etkisi).
        using (var dolgu = new SolidBrush(DesignTokens.Saydam(renk, 50))) e.Graphics.FillPath(dolgu, yol);
        // Rozetin ince parlak çerçevesi.
        using (var kalem = new Pen(renk, 1.2f)) e.Graphics.DrawPath(kalem, yol);
        // Rozet yazısı: neon renkte, kutunun ortasında.
        TextRenderer.DrawText(e.Graphics, metin, font, kutu, renk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        // "Bu hücreyi biz çizdik, Windows tekrar çizmesin" de.
        e.Handled = true;
    }
}
