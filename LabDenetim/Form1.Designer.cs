// Bu dosya programın EKRANININ İSKELETİNİ (sol menü, üst çubuk, tablo, günlük) kurar.
// Artık öğeleri tek tek piksel piksel yerleştirmiyoruz: "satır ve sütun ızgaraları" kullanıyoruz.
// Pencere büyüyüp küçülünce parçalar kendiliğinden yer değiştirir ve üst üste binmez.
namespace LabDenetim;

// Form1'in ekran tarafını anlatan parça (aynı sınıfın ikinci yarısı).
partial class Form1
{
    // Ekrandaki görünmez yardımcı parçaları tutan kutu.
    internal System.ComponentModel.IContainer components = null;

    // Pencere kapanınca kullanılan belleği temizler (ev boşaltılırken çöpü atmak gibi).
    protected override void Dispose(bool disposing)
    {
        // Temizlenecek bir şey varsa temizle.
        if (disposing && (components != null)) components.Dispose();
        // Windows'un kendi temizliğini de yap.
        base.Dispose(disposing);
    }

    // Tasarımcı bu bölgeyi tanır.
    #region Windows Form Designer generated code

    // Ekrandaki her şeyi yaratır ve yerine koyar.
    internal void InitializeComponent()
    {
        // Yardımcı parçalar için kutuyu hazırla.
        components = new System.ComponentModel.Container();
        // Tarama ekranı (hedef şeridi, kartlar, tablo + Asistan, günlük) ayrı bir denetimdir: Kontroller\TaramaSayfasi.cs.
        _tarama = new TaramaSayfasi { Sahibi = this };
        SuspendLayout();

        // ================= ANA İSKELET: sol menü + sağ çalışma alanı + alt durum çubuğu =================
        // 2 sütun (sol menü, sağ alan) ve 2 satır (üst gövde, alt durum çubuğu) olan büyük ızgara.
        tblKok = YerlesimYardimcilari.Tablo(2, 2);
        // Sol sütun: menü, 210 piksel sabit genişlik.
        tblKok.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        // Sağ sütun: kalan tüm genişlik.
        tblKok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Üst satır: kalan tüm yükseklik.
        tblKok.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Alt satır: durum çubuğu, 28 piksel.
        tblKok.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        // ================= SOL MENÜ =================
        // Sol menü, üst çubuk ve sayfalar kurumsal kabukta (Kabuk\AppShell) kurulur; burada yalnızca iskelet vardır.

        // ================= ALT DURUM ÇUBUĞU =================
        // Durum çubuğu: 2 sütun (durum yazısı solda, imza sağda).
        tblDurum = YerlesimYardimcilari.Tablo(1, 1);
        // Sol sütun: kalan genişlik.
        tblDurum.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Tek satır.
        tblDurum.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Durum yazısı.
        lblDurum = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(14, 0, 0, 0), Margin = Padding.Empty, Text = "Hazır. Hedefi kontrol edip \"⚡ TARAMAYI BAŞLAT\" butonuna basın." };
        // Durum yazısını durum çubuğuna koy.
        tblDurum.Controls.Add(lblDurum, 0, 0);

        // ================= HEPSİNİ ANA İSKELETE YERLEŞTİR =================
        // Durum çubuğunu alt satıra koy ve iki sütunu da kaplasın.
        tblKok.Controls.Add(tblDurum, 0, 1); tblKok.SetColumnSpan(tblDurum, 2);

        // ================= PENCERENİN KENDİSİ =================
        // Ekran çözünürlüğüne (DPI) göre ölçeklensin.
        AutoScaleDimensions = new SizeF(96F, 96F);
        // Ölçekleme türü.
        AutoScaleMode = AutoScaleMode.Dpi;
        // Pencerenin başlangıç boyutu: 1080p ekranda rahat sığar.
        ClientSize = new Size(1620, 960);
        // Pencere bundan daha fazla küçültülemesin (parçalar sıkışmasın).
        MinimumSize = new Size(1024, 600);
        // (Eski ekranlar için pencere düzeyinde kaydırma kalktı: kurumsal kabuk her boyuta uyar; en az alan isteyen eski sayfalar kendi içinde kaydırılır — BolumSayfasi.)
        // Pencere ekranın ortasında açılsın.
        StartPosition = FormStartPosition.CenterScreen;
        // Büyük ızgarayı pencereye ekle.
        Controls.Add(tblKok);
        // Pencerenin çizimini yeniden başlat.
        ResumeLayout(false);
        // Yerleşimi hesapla.
        PerformLayout();
    }

    #endregion

    // Tarama ekranı (UserControl); pnlAna kabuğa verilen alan olarak onun kendisidir.
    internal TaramaSayfasi _tarama = null!;
    internal Control pnlAna => _tarama;
    internal TableLayoutPanel tblKok, tblDurum; internal Label lblDurum;
}
