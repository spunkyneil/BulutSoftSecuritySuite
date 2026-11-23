// Tarama ekranı ve kabuk iskeletinin ortak yerleşim yardımcıları (eskiden Form1'in Tema.cs partial dosyasındaydı).

// Programımızın adı.
namespace LabDenetim;

// Yerleşim yardımcıları.
internal static class YerlesimYardimcilari
{
    // Verilen sütun ve satır sayısıyla boş bir yerleşim ızgarası üretir (tüm ızgaralar aynı ayarla başlar).
    public static TableLayoutPanel Tablo(int kolon, int satir) => new TableLayoutPanel
    {
        // Bulunduğu hücreyi tamamen doldursun.
        Dock = DockStyle.Fill,
        // Sütun sayısı.
        ColumnCount = kolon,
        // Satır sayısı.
        RowCount = satir,
        // Dış ve iç boşlukları sıfırla.
        Margin = Padding.Empty, Padding = Padding.Empty,
        // Arka planı saydam bırak (alttaki renk görünsün).
        BackColor = Color.Transparent
    };

    // Verilen kutuyu ince çerçeveli (#334155) bir panelin içine koyup o paneli geri verir.
    public static Panel Cerceve(Control icerik)
    {
        // İçerik çerçevenin içini tamamen doldursun.
        icerik.Dock = DockStyle.Fill;
        // Dış boşluğu sıfırla.
        icerik.Margin = Padding.Empty;
        // Çerçeve paneli: zemin rengi çerçeve rengi; 1 piksel iç boşluk ince çizgi gibi görünür.
        var kap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = DesignTokens.Ozgun(TokenRol.BorderStrong), Margin = Padding.Empty };
        // İçeriği kaba ekle.
        kap.Controls.Add(icerik);
        // Kabı geri ver.
        return kap;
    }
}
