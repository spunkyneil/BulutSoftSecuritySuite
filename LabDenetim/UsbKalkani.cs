// Bu dosya "USB ve Harici Donanım Kalkanı"nı (yetkisiz cihaz radarı) çalıştırır: program açıkken Windows'un WMI olay sistemi
// (ManagementEventWatcher) arka planda dinlenir. Bilgisayara yeni bir USB bellek/klavye/harici cihaz takıldığı anda
// Canlı Sistem Günlüğü'ne uyarı düşer ve ekranın sağ altında siber bir uyarı kutusu çıkar.
// Program yalnızca İZLER ve uyarır; cihaza dokunmaz, hiçbir şeyi engellemez veya değiştirmez.
using System.Management;
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// Ekranın sağ altında kısa süre görünen, odağı çalmayan siber uyarı kutusu.
internal sealed class UsbBildirimFormu : TemaliForm
{
    // Kutuyu kapatacak sayaç.
    private readonly System.Windows.Forms.Timer _kapat = new();

    // Odağı çalmadan göster: kullanıcı yazı yazarken bölünmesin.
    protected override bool ShowWithoutActivation => true;

    // Pencere ayarları: kenarlıksız, her zaman üstte, görev çubuğunda görünmeyen.
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00000080; return cp; } }

    // Kutuyu hazırlar. renkli: tehlike seviyesine göre şerit rengi; sure: kaç ms açık kalacağı.
    public UsbBildirimFormu(string baslik, string satir1, string satir2, Color renk, int sure = 9000)
    {
        // Kenarlıksız, boyutu sabit, görev çubuğunda yok.
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; Size = new Size(430, 128);
        // Koyu zemin.
        BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); DoubleBuffered = true;
        // Sağ alt köşeye yerleştir (çalışma alanının içinde).
        var alan = Screen.PrimaryScreen!.WorkingArea; Location = new Point(alan.Right - Width - 16, alan.Bottom - Height - 16);
        // Çizim: renkli sol şerit, başlık, iki satır metin.
        Paint += (_, e) =>
        {
            // Pürüzsüz yazı.
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // Renkli sol şerit.
            using (var f = new SolidBrush(renk)) e.Graphics.FillRectangle(f, 0, 0, 6, Height);
            // İnce çerçeve.
            using (var k = new Pen(DesignTokens.Ozgun(TokenRol.BorderStrong))) e.Graphics.DrawRectangle(k, 0, 0, Width - 1, Height - 1);
            // Başlık.
            TextRenderer.DrawText(e.Graphics, baslik, FontProvider.Al(FontRol.Subheading, FontStyle.Bold), new Rectangle(18, 10, Width - 30, 26), renk, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            // Cihaz adı satırı.
            TextRenderer.DrawText(e.Graphics, satir1, FontProvider.Al(FontRol.Body, FontStyle.Bold), new Rectangle(18, 40, Width - 30, 24), DesignTokens.Ozgun(TokenRol.TextPrimary), TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            // Açıklama satırı.
            TextRenderer.DrawText(e.Graphics, satir2, FontProvider.Al(FontRol.Caption), new Rectangle(18, 66, Width - 30, 56), DesignTokens.Ozgun(TokenRol.TextSecondary), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
        };
        // Tıklayınca hemen kapansın.
        Click += (_, _) => Close();
        // Süre dolunca kapansın.
        _kapat.Interval = sure; _kapat.Tick += (_, _) => { _kapat.Stop(); Close(); }; Shown += (_, _) => _kapat.Start();
        // Kapanırken zamanlayıcıyı temizle.
        FormClosed += (_, _) => _kapat.Dispose();
    }
}
