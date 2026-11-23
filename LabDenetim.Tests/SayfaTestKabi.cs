// Test yardımcısı: kabuğa konan eski ekran sayfalarını (SayfaDenetimi) ekran dışı bir pencereye gömer, gösterir ve Shown olayını tetikler (D5).
// Program içinde bu işi DenetimSayfasi yapar; birim testlerinde kabuk kurmadan tek sayfayı sınamak için kullanılır.

// Test ad alanı.
namespace LabDenetim.Tests;

// Sayfa barındırma yardımcısı.
internal static class SayfaTestKabi
{
    // Sayfayı verilen boyutta (varsayılan 1100x700) ekran dışı pencereye Dock=Fill koyar. Dönen pencere using ile atılır.
    public static Form Goster(SayfaDenetimi s, int genislik = 1100, int yukseklik = 700)
    {
        var f = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ClientSize = new Size(genislik, yukseklik), ShowInTaskbar = false };
        s.Dock = DockStyle.Fill; f.Controls.Add(s); f.Show(); Application.DoEvents(); s.SayfaAcildi(); Application.DoEvents();
        return f;
    }
}
