// Bu dosya ekranın altındaki "Canlı Sistem Günlüğü" kutusunu (siyah komuta ekranı) kurar.
// Günlüğe yazılan her satır programın O ANDA gerçekten yaptığı bir işi anlatır; süs değildir.
using System.Globalization;

// Programımızın adı.
namespace LabDenetim;

// Form1'in beşinci parçası: canlı sistem günlüğü.
internal sealed partial class TaramaSayfasi
{
    // Günlük satırlarının yazıldığı siyah kutu.
    internal RichTextBox rtbGunluk = null!;
    // Günlüğün üstündeki küçük başlık yazısı.
    internal Label lblGunlukBaslik = null!;
    // Türkçe büyük harf kuralları (i → İ) için kültür bilgisi.
    internal static readonly CultureInfo Tr = new("tr-TR");

    // Günlük kutusunu yaratır ve günlük ızgarasına koyar. Form1 açılırken bir kez çağrılır.
    internal void GunlukKur()
    {
        // Günlüğün üstündeki başlık yazısını yarat.
        lblGunlukBaslik = new Label { Text = "⚡ CANLI SİSTEM GÜNLÜĞÜ", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Elektrik, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Margin = Padding.Empty };
        // Günlük kutusunu yarat: siyah zemin, eşit aralıklı yazı, sadece okunur.
        rtbGunluk = new RichTextBox
        {
            // Hücreyi tamamen doldursun.
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            // Çok koyu lacivert zemin (diğer panellerle uyumlu, saf siyah değil), parlak yeşil yazı, eşit aralıklı yazı tipi.
            BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar), ForeColor = RakamYesil, Font = FontProvider.Al(FontRol.Mono),
            // Kullanıcı yazı yazamasın, sadece okusun.
            ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical,
            // Satırlar kutuya sığmayınca alt satıra insin.
            WordWrap = true, DetectUrls = false, TabStop = false
        };
        // Başlığı günlük ızgarasının üst satırına koy.
        tblLog.Controls.Add(lblGunlukBaslik, 0, 0);
        // Siyah kutuyu alt satırına koy.
        tblLog.Controls.Add(rtbGunluk, 0, 1);
        // Günlüğün kaydırma çubuğu da koyu olsun.
        KoyuTema.Uygula(rtbGunluk);

        // İlk karşılama satırlarını yaz.
        Gunluk("⚡ DENETİM MOTORU HAZIR -> Güvenlik modülleri yüklendi.", RakamYesil);
        // Kullanıcıya ne yapacağını söyleyen ikinci satır.
        Gunluk("BEKLENİYOR -> Başlamak için \"⚡ TARAMAYI BAŞLAT\" butonuna basın.", Elektrik);
    }

    // Günlüğe saat damgalı yeni bir satır ekler ve en alta kaydırır.
    internal void Gunluk(string mesaj, Color renk)
    {
        // Günlük henüz kurulmadıysa veya pencere kapanıyorsa hiçbir şey yapma.
        if (rtbGunluk == null || rtbGunluk.IsDisposed) return;
        // Önemli bir olaysa (USB, nöbetçi, izolasyon, tarama bitişi...) kalıcı Olay Zaman Çizgisi'ne de yaz; eski kayıt yüklenirken yazma.
        if (!_gecmisYukleniyor && OlayDeposu.Siniflandir(mesaj) is { } olay) ServisBaglami.OlayYazici.Ekle(olay.Tur, olay.Seviye, mesaj);
        // Çok uzayınca eski satırları sil (bellek şişmesin): 500 satırı geçerse ilk 100'ü at.
        if (rtbGunluk.Lines.Length > 500)
        {
            // İlk 100 satırın bittiği yere kadar olan yazıyı seç.
            rtbGunluk.Select(0, rtbGunluk.GetFirstCharIndexFromLine(100));
            // Seçilen kısmı sil.
            rtbGunluk.SelectedText = "";
        }
        // İmleci en sona götür ki yazı sona eklensin.
        rtbGunluk.SelectionStart = rtbGunluk.TextLength;
        // Hiçbir şey seçili olmasın.
        rtbGunluk.SelectionLength = 0;
        // Saat damgasını soluk mavi-gri yaz.
        rtbGunluk.SelectionColor = TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextMuted));
        // Saat damgası: [14:22:05] biçiminde.
        rtbGunluk.AppendText($"[{DateTime.Now:HH:mm:ss}] ");
        // Mesajı verilen renkle yaz.
        rtbGunluk.SelectionColor = TemaYonetici.C(renk);
        // Mesajı ve satır sonunu ekle.
        rtbGunluk.AppendText(mesaj + "\n");
        // En son satır görünsün diye en alta kaydır.
        rtbGunluk.ScrollToCaret();
    }

    // Başka bir iş parçacığından (arka plandan) güvenle günlüğe yazmak için yardımcı.
    internal void GunlukGuvenli(string mesaj, Color renk)
    {
        // Pencere henüz yoksa veya kapandıysa yazma.
        if (!IsHandleCreated || IsDisposed) return;
        // Yazma işini ekranı çizen ana iş parçacığına devret.
        BeginInvoke(() => Gunluk(mesaj, renk));
    }

    // Metni Türkçe büyük harfe çevirir (komuta ekranı havası için).
    internal static string Buyuk(string metin) => metin.ToUpper(Tr);
}
