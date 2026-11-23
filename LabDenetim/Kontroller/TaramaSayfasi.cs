// Bu dosya "Yeni Tarama + Asistan" ekranının KENDİSİDİR (eskiden Form1.Designer.cs içinde kuruluyordu): hedef/profil/başlat şeridi, gelişmiş ayarlar, istatistik kartları, sayaç ve rapor şeridi,
// sonuç tablosu + Asistan paneli (bölücüyle), radar ve canlı günlük. Ekran yalnızca ARAYÜZÜ kurar ve denetimleri (internal alanlar) sunar; iş mantığı ve olay işleyicileri hâlâ Form1'dedir
// (Form1, denetimlere TaramaOlaylariniBagla ile bağlanır) — mantığın da buraya taşınması D4'ün sonraki dilimidir. Yeni ekran parçaları bu sınıfa, Form1'e değil, eklenir.

// Programımızın adı.
namespace LabDenetim;

// Tarama çalışma alanı denetimi.
internal sealed partial class TaramaSayfasi : UserControl, ITaramaBaglami
{
    private readonly System.ComponentModel.IContainer components = new System.ComponentModel.Container();
    // Fare açıklama balonları, sağ tık menüsü ve ekranın tüm denetimleri.
    internal ToolTip ipucu = null!;
    internal ContextMenuStrip ctxMenu = null!;
    internal ToolStripMenuItem miKopyala = null!;
    internal ToolStripMenuItem miMasaustu = null!;
    internal TableLayoutPanel tblAna = null!;
    internal TableLayoutPanel tblGel = null!;
    internal TableLayoutPanel pnlKartlar = null!;
    internal TableLayoutPanel tblDetay = null!;
    internal TableLayoutPanel tblLog = null!;
    internal SplitContainer splIcerik = null!;
    internal FlowLayoutPanel flwKontrol = null!;
    internal FlowLayoutPanel flwHedef = null!;
    internal FlowLayoutPanel flwSerit = null!;
    internal Label lblHedef = null!;
    internal Label lblGelIpucu = null!;
    internal Label lblPortlar = null!;
    internal Label lblUrl = null!;
    internal Label lblParametre = null!;
    internal Label lblAlan = null!;
    internal Label lblYesil = null!;
    internal Label lblSari = null!;
    internal Label lblKirmizi = null!;
    internal Label lblDetayBaslik = null!;
    internal TextBox txtHedef = null!;
    internal TextBox txtPortlar = null!;
    internal TextBox txtUrl = null!;
    internal TextBox txtParametre = null!;
    internal TextBox txtAlanAdi = null!;
    internal TextBox txtKomut = null!;
    internal RichTextBox rtbDetay = null!;
    internal Button btnKopyala = null!;
    internal Button btnOnar = null!;
    internal Button btnHedefSec = null!;
    internal ContextMenuStrip ctxHedef = null!;
    internal ToolStripMenuItem miHedefYerel = null!;
    internal ToolStripMenuItem miHedefAg = null!;
    internal Button btnModYerel = null!;
    internal Button btnModClaude = null!;
    internal Button btnApiAyar = null!;
    internal Button btnDerinAnaliz = null!;
    internal Button btnPromptKopyala = null!;
    internal CheckBox chkTumTarama = null!;
    internal Label lblManuelNot = null!;
    internal TableLayoutPanel tblMod = null!;
    internal TableLayoutPanel pnlClaude = null!;
    internal TableLayoutPanel pnlManuel = null!;
    internal Panel pnlAksiyon = null!;
    internal Label lblKomutBaslik = null!;
    internal Label lblTumu = null!;
    internal Panel pnlOrta = null!;
    internal RadarPaneli radar = null!;
    internal CheckBox chkPort = null!;
    internal CheckBox chkHttp = null!;
    internal CheckBox chkTls = null!;
    internal CheckBox chkDns = null!;
    internal CheckBox chkYapilandirma = null!;
    internal CheckBox chkGirdi = null!;
    internal Button btnBaslat = null!;
    internal Button btnIptal = null!;
    internal Button btnHtml = null!;
    internal Button btnPdf = null!;
    internal Button btnGelismis = null!;
    internal TextBox txtAra = null!;
    internal Panel pnlSolAlan = null!;
    internal Panel pnlAltin = null!;
    internal Label lblSerit = null!;
    internal Label lblAltinDurum = null!;
    internal Button btnKanit = null!;
    internal Button btnAltin = null!;
    internal DataGridView dgvTrafik = null!;
    internal FlowLayoutPanel flwProfil = null!;
    internal Button btnProfilHizli = null!;
    internal Button btnProfilStandart = null!;
    internal Button btnProfilKapsamli = null!;
    internal Label lblProfil = null!;
    internal Label lblProfilOzel = null!;
    internal DataGridView dgvBulgular = null!;
    internal ProgressBar progress = null!;

    public TaramaSayfasi()
    {
        // Fare üstüne gelince çıkan sarı açıklama balonlarını hazırla.
        ipucu = new ToolTip(components) { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 100 };
        // Sağ tık menüsünü yarat.
        ctxMenu = new ContextMenuStrip(components);
        // Sağ tık menüsündeki "Sonucu Kopyala" satırını yarat.
        miKopyala = new ToolStripMenuItem("Sonucu Kopyala");
        // Sağ tık menüsündeki "Tüm Raporu Masaüstüne Kaydet" satırını yarat.
        miMasaustu = new ToolStripMenuItem("Tüm Raporu Masaüstüne Kaydet");
        // "Sonucu Kopyala" tıklanınca çalışacak kodu bağla.
        // "Masaüstüne Kaydet" tıklanınca çalışacak kodu bağla.
        // İki satırı menüye ekle.
        ctxMenu.Items.AddRange(new ToolStripItem[] { miKopyala, miMasaustu });
        // Tablo hazırlanırken çizimi durdur.
        dgvBulgular = new DataGridView();
        // Tablonun hazırlığını başlat.
        ((System.ComponentModel.ISupportInitialize)dgvBulgular).BeginInit();
        // Pencerenin çizimini geçici olarak durdur (hızlı olsun diye).
        SuspendLayout();

        // ================= SAĞ ÇALIŞMA ALANI =================
        // Sağ çalışma alanının zemin paneli (bu denetimin kendisi): kenarlardan 14 piksel boşluk bırakır.
        Dock = DockStyle.Fill; Padding = new Padding(14, 12, 14, 8); Margin = Padding.Empty;
        // Sağ alanı 6 satıra bölen ızgara (hedef çubuğu, gelişmiş panel, kartlar, şerit, içerik, günlük).
        tblAna = YerlesimYardimcilari.Tablo(1, 7);
        // Tek sütun: tüm genişliği kaplasın (bu satır olmazsa içerik genişleyip pencereden taşar).
        tblAna.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 1. satır: hedef çubuğu, 56 piksel.
        tblAna.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // 1b satırı: ince ilerleme çubuğu, 10 piksel.
        tblAna.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
        // 2. satır: gelişmiş ayarlar paneli, içeriğine göre (gizliyken 0).
        tblAna.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // 3. satır: istatistik kartları, 92 piksel.
        tblAna.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        // 4. satır: trafik lambası sayaçları ve rapor butonları, 40 piksel.
        tblAna.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // 5. satır: tablo ve detay paneli, kalan tüm yükseklik.
        tblAna.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // 6. satır: canlı günlük, 150 piksel.
        tblAna.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

        // ---- 1. satır: hedef çubuğu ----
        // Hedef şeridi: genişlik yetmezse alt satıra SARILIR (hiçbir düğme kesilmez); satır yüksekliği içeriğe göre büyür.
        flwHedef = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        // "Hedef adres" yazısı.
        lblHedef = new Label { Text = "Hedef adres:", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        // Adres kutusu.
        txtHedef = new TextBox { Width = 180, MinimumSize = new Size(120, 0), PlaceholderText = "Örnek: 127.0.0.1", Text = "127.0.0.1", Margin = new Padding(0, 8, 12, 0) };
        // Adres kutusu fare açıklaması.
        ipucu.SetToolTip(txtHedef, "Denetlenecek bilgisayarın adresi (evin sokak numarası gibi).\r\nKendi bilgisayarınız için 127.0.0.1 yazın.\r\nSanal Kali bilgisayarı için onun 192.168... ile başlayan adresini yazın.");
        // "Gelişmiş" düğmesi.
        btnGelismis = new EfektliButon { Text = "Gelişmiş ▾", Size = new Size(110, 38), Margin = new Padding(0, 0, 12, 0) };
        // "Gelişmiş" tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnGelismis, "Kapı numaraları, web adresi ve hangi kontrollerin çalışacağı gibi ayrıntılı ayarları açar/kapatır.\r\nBilmiyorsanız açmanıza gerek yok; hazır değerler çoğu durumda yeterlidir.");
        // "1. Adım" düğmesi.
        btnBaslat = new EfektliButon { Text = "⚡ TARAMAYI BAŞLAT", Size = new Size(190, 38), Margin = new Padding(0, 0, 10, 0) };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnBaslat, "Seçili kontrolleri sırayla başlatır.\r\nBittiğinde sonuçlar aşağıdaki tabloda görünür.");
        // "Durdur" düğmesi.
        btnIptal = new EfektliButon { Text = "Durdur", Size = new Size(84, 38), Enabled = false };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnIptal, "Devam eden taramayı hemen durdurur. O ana kadar bulunanlar tabloda kalır.");
        // İlerleme çubuğu.
        progress = new ProgressBar { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
        // Fare açıklaması.
        ipucu.SetToolTip(progress, "Çubuk hareket ediyorsa program çalışıyor demektir. Bitene kadar bekleyin.");
        // Altı parçayı hedef çubuğuna sırayla koy.
        // "Profil:" yazısı.
        lblProfil = new Label { Text = "Profil:", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        // Profil seçici: üç düğme yan yana (her zaman yalnızca biri seçili görünür).
        flwProfil = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 12, 0) };
        // "Hızlı" profil düğmesi.
        btnProfilHizli = new EfektliButon { Text = "Hızlı", Size = new Size(60, 36), Margin = new Padding(0, 0, 2, 0) };
        // "Standart" profil düğmesi.
        btnProfilStandart = new EfektliButon { Text = "Standart", Size = new Size(80, 36), Margin = new Padding(0, 0, 2, 0) };
        // "Kapsamlı" profil düğmesi.
        btnProfilKapsamli = new EfektliButon { Text = "Kapsamlı", Size = new Size(84, 36), Margin = new Padding(0, 0, 6, 0) };
        // Elle ayar yapılınca görünen küçük "Özel" yazısı (başta gizli).
        lblProfilOzel = new Label { Text = "", AutoSize = false, Size = new Size(52, 20), Margin = new Padding(0, 8, 0, 0) };
        // Dört parçayı profil seçiciye koy.
        flwProfil.Controls.AddRange(new Control[] { btnProfilHizli, btnProfilStandart, btnProfilKapsamli, lblProfilOzel });
        // Her düğmenin fare açıklaması.
        ipucu.SetToolTip(btnProfilHizli, "HIZLI: en yaygın 8 kapıya bakar, saniyeler sürer.");
        // Standart açıklaması.
        ipucu.SetToolTip(btnProfilStandart, "STANDART (önerilen): 17 yaygın kapı ve web/isim kontrolleri. Dengeli.");
        // Kapsamlı açıklaması.
        ipucu.SetToolTip(btnProfilKapsamli, "KAPSAMLI: 1-1024 arası tüm kapılar ve daha fazlası. Daha uzun sürer.");
        // Profil seçicinin boşluklarına gelinirse genel açıklama çıksın.
        ipucu.SetToolTip(flwProfil, "Taramanın ne kadar ayrıntılı olacağını seçer; kapı listesini ve kontrolleri sizin yerinize doldurur.\r\nHızlı: az kapı, birkaç dakika değil birkaç saniye.\r\nStandart: önerilen, dengeli.\r\nKapsamlı: çok sayıda kapı, daha uzun sürer.\r\nElle ayar yaparsanız burası otomatik \"Özel\" olur.");
        // Hedef yazısını (yanında ⓘ ile) ve adres kutusunu çubuğa koy.
        // "Hedef Seç / İç Ağ Keşfi" açılır menü düğmesi.
        btnHedefSec = new EfektliButon { Text = "◈ Hedef Seç / İç Ağ Keşfi ▾", Size = new Size(235, 38), Margin = new Padding(0, 0, 10, 0) };
        // Tıklanınca menüyü açacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnHedefSec, "Hedefi listeden seçmenizi sağlar:\r\n1) Yerel Bilgisayarım (127.0.0.1)\r\n2) Sanal Ağları Tara: VirtualBox / VMware / yerel ağdaki açık makineleri otomatik bulur; Kali'nin IP'sini elle aramanız gerekmez.");
        // Hedef seçim menüsü (düğmenin altında açılır).
        ctxHedef = new ContextMenuStrip(components);
        // Menü satırı 1: bu bilgisayar.
        miHedefYerel = new ToolStripMenuItem("▣  1. Yerel Bilgisayarım (127.0.0.1)");
        // Menü satırı 2: ağ taraması.
        miHedefAg = new ToolStripMenuItem("◉  2. Sanal Ağları Tara (VirtualBox / VMware / Yerel Ağdaki Açık Makineleri Bul)...");
        // Tıklanınca çalışacak kodlar.
        // İki satırı menüye ekle.
        ctxHedef.Items.AddRange(new ToolStripItem[] { miHedefYerel, miHedefAg });
        // Hedef yazısını (yanında ⓘ ile), adres kutusunu ve hedef seç düğmesini çubuğa koy.
        // Hedef yazısı ve adres kutusu.
        // Sıra: hedef, birincil eylemler (Başlat/Durdur), sonra profil ve gelişmiş.
        flwHedef.Controls.Add(EtiketBilgi(lblHedef, "Hedef adres", "Denetlenecek bilgisayarın adresi (evin sokak numarası gibi).\r\n\r\nKendi bilgisayarınız için 127.0.0.1 yazın. Sanal Kali bilgisayarı için onun 192.168... ile başlayan adresini yazın.\r\n\r\nGüvenlik için yalnızca kendi ağınızdaki adresler kabul edilir.")); flwHedef.Controls.Add(txtHedef); flwHedef.Controls.Add(btnHedefSec); flwHedef.Controls.Add(btnBaslat); flwHedef.Controls.Add(btnIptal);
        // Profil yazısı (yanında ⓘ ile) ve seçim kutusu.
        // Profil + profil düğmeleri + Gelişmiş tek BÜTÜN olarak taşınır (grup ortadan bölünüp alt satıra düşmez).
        var grpProfil = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        grpProfil.Controls.Add(EtiketBilgi(lblProfil, "Tarama profili", "Taramanın ne kadar ayrıntılı olacağını seçer.\r\n\r\nHızlı: en yaygın 8 kapıya bakar, saniyeler sürer.\r\nStandart (önerilen): 17 yaygın kapı ve web/isim kontrolleri.\r\nKapsamlı: 1-1024 arası tüm kapılar ve daha fazlası; daha uzun sürer.\r\n\r\nGelişmiş ayarlardan elle değişiklik yaparsanız profil kendiliğinden \"Özel\" olur.")); grpProfil.Controls.Add(flwProfil); grpProfil.Controls.Add(btnGelismis); flwHedef.Controls.Add(grpProfil);
        // Kalan üç parça.

        // ---- 2. satır: gelişmiş ayarlar paneli (başta gizli) ----
        // Gelişmiş panel: 4 sütun (yazı, kutu, yazı, kutu), 5 satır.
        tblGel = YerlesimYardimcilari.Tablo(4, 5);
        // Panel içeriğine göre büyüsün.
        tblGel.AutoSize = true;
        // Hem büyüyüp hem küçülebilsin.
        tblGel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        // Dolgu: içeriği kenarlardan uzak tut.
        tblGel.Padding = new Padding(12, 10, 12, 10);
        // Başlangıçta gizli: çoğu kullanıcının buna ihtiyacı yok.
        tblGel.Visible = false;
        // Sütun 1: yazılar (yazı kadar yer).
        tblGel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        // Sütun 2: kutular (yarım genişlik).
        tblGel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Sütun 3: yazılar.
        tblGel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        // Sütun 4: kutular.
        tblGel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Beş satırın hepsi içeriği kadar yüksek olsun.
        for (int i = 0; i < 5; i++) tblGel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // En üstteki sade açıklama yazısı.
        lblGelIpucu = new Label { Text = "Bu bölümü değiştirmek zorunda değilsiniz; hazır değerler çoğu durumda yeterlidir. Kutuların üstüne fareyi getirirseniz ne işe yaradıklarını okursunuz.", AutoSize = false, Dock = DockStyle.Fill, Height = 40, Margin = new Padding(0, 0, 0, 6) };
        // Kapı numaraları yazısı.
        lblPortlar = new Label { Text = "Kapı numaraları:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
        // Kapı numaraları kutusu.
        txtPortlar = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "Örnek: 22,80,443 veya 8000-8100", Text = "21,22,23,25,53,80,110,139,143,443,445,3306,3389,5432,5900,8080,8443", Margin = new Padding(0, 6, 0, 6) };
        // Fare açıklaması.
        ipucu.SetToolTip(txtPortlar, "Bilgisayarların \"kapıları\" numaralıdır (port). Her hizmet kendi kapısını kullanır:\r\n22 = uzaktan bağlantı, 80 = web sitesi, 21 = dosya aktarımı.\r\nVirgülle ayırın. Aralık için 8000-8100 yazabilirsiniz. Bilmiyorsanız olduğu gibi bırakın.");
        // Web adresi yazısı.
        lblUrl = new Label { Text = "Web sitesi adresi:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
        // Web adresi kutusu.
        txtUrl = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "Örnek: http://127.0.0.1:8080/", Text = "http://127.0.0.1:8080/", Margin = new Padding(0, 6, 16, 6) };
        // Fare açıklaması.
        ipucu.SetToolTip(txtUrl, "Denetlenecek web sitesinin tam adresi (tarayıcının adres çubuğuna yazdığınız şey).\r\nÖrnek: http://127.0.0.1:8080/  veya  https://192.168.56.101/");
        // Kutucuk adı yazısı.
        lblParametre = new Label { Text = "Test edilecek kutucuk adı:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
        // Kutucuk adı kutusu.
        txtParametre = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "Örnek: q", Text = "q", Margin = new Padding(0, 6, 0, 6) };
        // Fare açıklaması.
        ipucu.SetToolTip(txtParametre, "Siteniz bir arama kutusu veya form içeriyorsa, o kutucuğun adını yazın (genelde q, ara, search, id gibi).\r\nProgram oraya zararsız deneme yazıları göndererek sitenin kötü girdilere karşı korunup korunmadığına bakar.\r\nBilmiyorsanız 'Form kutusu testi'ni işaretlemeyin.");
        // Alan adı yazısı.
        lblAlan = new Label { Text = "Alan adı (isim kaydı):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
        // Alan adı kutusu.
        txtAlanAdi = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "Örnek: localhost", Text = "localhost", Margin = new Padding(0, 6, 16, 6) };
        // Fare açıklaması.
        ipucu.SetToolTip(txtAlanAdi, "Alan adı, internet sitelerinin adıdır (örn. ornek.local). Telefon rehberindeki isim gibi düşünün:\r\nisimden numaraya çevrilir. Kendi yetkili test ortamınızda bir isim kullanmıyorsanız 'localhost' bırakın.");
        // Kontrol işaret kutularını yarat.
        chkPort = new CheckBox(); chkHttp = new CheckBox(); chkTls = new CheckBox();
        // Kalan işaret kutuları.
        chkDns = new CheckBox(); chkYapilandirma = new CheckBox(); chkGirdi = new CheckBox();
        // Her kutucuk için: değişken, yazı ve fare açıklaması üçlüsü.
        var kutular = new (CheckBox Kutu, string Metin, string Aciklama)[]
        {
            // Kapı yoklama.
            (chkPort, "Kapıları yokla", "Hedefte hangi kapıların (portların) açık olduğuna bakar ve servisin kendini tanıttığı yanıtı (servis kimliği) okur.\r\nEvin hangi kapılarının açık kaldığını görmek gibi."),
            // Web sitesi korumaları.
            (chkHttp, "Web sitesi korumaları", "Web sitesinin gönderdiği koruma etiketlerine bakar.\r\nBinanın girişinde kamera, alarm, kilit olup olmadığını kontrol etmek gibi."),
            // Şifreli bağlantı.
            (chkTls, "Şifreli bağlantı (https)", "Sitenin gizli (şifreli) konuşma sertifikasına bakar.\r\nSertifika, sitenin kimlik kartı gibidir: süresi dolmuş mu, sahte mi, eski mi?\r\nSadece adresiniz https ile başlıyorsa işaretleyin."),
            // İsim kayıtları.
            (chkDns, "İsim kayıtları", "Alan adına bağlı gizli kalmış alt isimleri arar (örn. admin.lab.local).\r\nTelefon rehberinde unutulmuş bir numarayı aramak gibi."),
            // Zayıf ayarlar.
            (chkYapilandirma, "Zayıf ayarlar", "Açık kapılara bakıp tehlikeli ayarları bulur (şifresiz giriş, eski ve güvensiz hizmetler gibi).\r\nKapıda anahtarın üstünde unutulduğunu fark etmek gibi."),
            // Form kutusu testi.
            (chkGirdi, "Form kutusu testi", "Sitenizdeki bir kutucuğa zararsız deneme yazıları gönderip siteniz bunları kontrolsüz kabul ediyor mu bakar.\r\nSadece KENDİ test sitenizde kullanın. Yukarıdaki 'kutucuk adı' dolu olmalı.")
        };
        // Kutucukları yan yana dizen panel.
        flwKontrol = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0), WrapContents = true, Dock = DockStyle.Fill };
        // Her kutucuk için aynı işlemi tekrarla.
        foreach (var (kutu, metin, aciklama) in kutular)
        {
            // Kutucuğun yazısını koy.
            kutu.Text = metin;
            // Başlangıçta işaretli gelsin.
            kutu.Checked = true;
            // Yazıya göre boyutunu ayarla.
            kutu.AutoSize = true;
            // Kutucuklar arasında boşluk bırak.
            kutu.Margin = new Padding(0, 0, 22, 0);
            // Fareyle üstüne gelince açıklama çıksın.
            ipucu.SetToolTip(kutu, aciklama);
            // Paneline ekle.
            flwKontrol.Controls.Add(kutu);
        }
        // Şifreli bağlantı ilk başta kapalı: her sitenin https'i yoktur.
        chkTls.Checked = false;
        // Form testi ilk başta kapalı: önce siz bilerek açın.
        chkGirdi.Checked = false;
        // Satır 0: açıklama yazısı (4 sütunu kaplar).
        tblGel.Controls.Add(lblGelIpucu, 0, 0); tblGel.SetColumnSpan(lblGelIpucu, 4);
        // Satır 1: kapı numaraları (kutu 3 sütunu kaplar).
        tblGel.Controls.Add(EtiketBilgi(lblPortlar, "Kapı numaraları", "Bilgisayarların \"kapıları\" numaralıdır (port). Her hizmet kendi kapısını kullanır: 22 = uzaktan bağlantı, 80 = web sitesi, 21 = dosya aktarımı.\r\n\r\nVirgülle ayırın; aralık için 8000-8100 yazabilirsiniz.\r\nBu kutu seçtiğiniz profile göre otomatik dolar; elle değiştirirseniz profil \"Özel\" olur."), 0, 1); tblGel.Controls.Add(txtPortlar, 1, 1); tblGel.SetColumnSpan(txtPortlar, 3);
        // Satır 2: web adresi ve kutucuk adı.
        tblGel.Controls.Add(EtiketBilgi(lblUrl, "Web sitesi adresi", "Denetlenecek web sitesinin tam adresi (tarayıcının adres çubuğuna yazdığınız şey).\r\n\r\nÖrnek: http://127.0.0.1:8080/ veya https://192.168.56.101/\r\nSiteniz yoksa web ile ilgili kontrolleri işaretlemeyin."), 0, 2); tblGel.Controls.Add(txtUrl, 1, 2); tblGel.Controls.Add(EtiketBilgi(lblParametre, "Test edilecek kutucuk adı", "Siteniz bir arama kutusu veya form içeriyorsa, o kutucuğun adını yazın (genelde q, ara, search, id gibi).\r\n\r\nProgram oraya zararsız deneme yazıları göndererek sitenin kötü girdilere karşı korunup korunmadığına bakar. Bilmiyorsanız \"Form kutusu testi\"ni işaretlemeyin."), 2, 2); tblGel.Controls.Add(txtParametre, 3, 2);
        // Satır 3: alan adı.
        tblGel.Controls.Add(EtiketBilgi(lblAlan, "Alan adı (isim kaydı)", "Alan adı, internet sitelerinin adıdır (örn. ornek.local). Telefon rehberindeki isim gibi düşünün: isimden numaraya çevrilir.\r\n\r\nKendi yetkili test ortamınızda bir isim kullanmıyorsanız \"localhost\" kalsın."), 0, 3); tblGel.Controls.Add(txtAlanAdi, 1, 3);
        // Satır 4: kontrol işaret kutuları (4 sütunu kaplar).
        tblGel.Controls.Add(flwKontrol, 0, 4); tblGel.SetColumnSpan(flwKontrol, 4);

        // ---- 3. satır: istatistik kartları (içi KartlariKur'da doldurulur) ----
        // 4 eşit sütunlu kart ızgarası.
        pnlKartlar = YerlesimYardimcilari.Tablo(4, 1);
        // Her sütun eşit genişlikte.
        for (int i = 0; i < 4; i++) pnlKartlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        // Tek satır.
        pnlKartlar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ---- 4. satır: trafik lambası sayaçları (solda) ve rapor butonları (sağda) ----
        // Sayaç ve rapor düğmeleri TEK sarmalanan akışta: dar pencerede ikinci satıra iner, hiçbir düğme kesilmez.
        flwSerit = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        // Yeşil sayaç kutusu.
        lblYesil = new Label { Text = "YEŞİL (Normal): 0", Size = new Size(170, 28), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 4, 8, 0) };
        // Fare açıklaması.
        ipucu.SetToolTip(lblYesil, "YEŞİL: Sorun değil, sadece bilgi notu veya normal durum.\r\nTıklayınca tabloda yalnızca yeşil sonuçlar kalır; tekrar tıklayınca hepsi geri gelir.");
        // Sarı/turuncu sayaç kutusu.
        lblSari = new Label { Text = "SARI/TURUNCU (Dikkat): 0", Size = new Size(200, 28), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 4, 8, 0) };
        // Fare açıklaması.
        ipucu.SetToolTip(lblSari, "SARI/TURUNCU: Dikkat edilmesi gereken yerler. Sıra gelince düzeltin.\r\nTıklayınca tabloda yalnızca bu seviyedekiler kalır; tekrar tıklayınca hepsi geri gelir.");
        // Kırmızı sayaç kutusu.
        lblKirmizi = new Label { Text = "KIRMIZI (Tehlike): 0", Size = new Size(170, 28), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 4, 0, 0) };
        // Fare açıklaması.
        ipucu.SetToolTip(lblKirmizi, "KIRMIZI: Ciddi güvenlik açığı. Önce bunları düzeltin.\r\nTıklayınca tabloda yalnızca kritik sonuçlar kalır; tekrar tıklayınca hepsi geri gelir.");
        // Üç sayacı panele koy.
        // "[ Tümünü Göster ]" düğmesi: açık filtreyi kaldırır.
        lblTumu = new Label { Text = "[ Tümünü Göster ]", Size = new Size(140, 28), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(8, 4, 0, 0) };
        // Fare açıklaması.
        ipucu.SetToolTip(lblTumu, "Açık filtreyi kaldırır ve tüm sonuçları yeniden gösterir.");
        // Dört kutuyu panele koy.
        // Web raporu butonu.
        btnHtml = new EfektliButon { Text = "▤ HTML Rapor İndir", Size = new Size(160, 32), Margin = new Padding(16, 4, 8, 0) };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnHtml, "Sonuçları, tarayıcıda açabileceğiniz düzenli bir sayfaya (.html) kaydeder.");
        // PDF raporu butonu.
        btnPdf = new EfektliButon { Text = "▦ PDF Rapor İndir", Size = new Size(160, 32), Margin = new Padding(0, 4, 8, 0) };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnPdf, "Sonuçları yazdırılabilir bir PDF dosyası olarak kaydeder.");
        // İki butonu panele koy.
        btnKanit = new EfektliButon { Text = "❖ Adli Kanıt Paketi (.ZIP)", Size = new Size(220, 32), Margin = new Padding(0, 4, 0, 0) };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnKanit, "Siber Kara Kutu: tarama sonuçlarını, aktif internet bağlantılarını, açık kapıları, hosts özetini, sistem bilgilerini ve HTML raporu tek bir tarih damgalı .zip dosyasına paketleyip masaüstüne koyar. Her dosyanın SHA-256 parmak izi pakete yazılır.");
        // Üç butonu panele koy.
        flwSerit.Controls.AddRange(new Control[] { lblYesil, lblSari, lblKirmizi, lblTumu, btnHtml, btnPdf, btnKanit });
        // Şerit hücrelerine yerleştir.

        // ---- 5. satır: sonuç tablosu (solda) + detay paneli (sağda) ----
        // Sol tablo ile sağ Asistan paneli arasında kullanıcının sürükleyebildiği bölücü (varsayılan %65 / %35); sağ panel okunur genişliğin altına inmez.
        splIcerik = new SplitContainer { Dock = DockStyle.Fill, Margin = Padding.Empty, FixedPanel = FixedPanel.None, SplitterWidth = 8, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp) };
        splIcerik.Panel1.BackColor = splIcerik.Panel2.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp);
        splIcerik.Panel2.AutoScroll = true;
        bool splKullanici = false;
        splIcerik.MouseUp += (_, _) => splKullanici = true;   // kullanıcı bölücüyü elle ayarladıysa oran artık zorlanmaz
        // En küçük panel genişlikleri, pencere yeterince genişleyince (ilk yerleşimden sonra) uygulanır; kurucuda küçük başlangıç genişliği hata verir.
        splIcerik.SizeChanged += (_, _) =>
        {
            if (splIcerik.Width < 380 + 330 + splIcerik.SplitterWidth + 4) return;
            if (splIcerik.Panel1MinSize != 380) { splIcerik.Panel1MinSize = 380; splIcerik.Panel2MinSize = 330; }
            if (splKullanici) return;
            splIcerik.SplitterDistance = Math.Clamp((int)(splIcerik.Width * 0.65), splIcerik.Panel1MinSize, splIcerik.Width - splIcerik.Panel2MinSize - splIcerik.SplitterWidth);
        };
        // Tablonun yeri: hücreyi tamamen doldur, sağ tarafa 10 piksel boşluk bırak.
        dgvBulgular.Dock = DockStyle.Fill;
        // Tablonun dış boşluğu.
        dgvBulgular.Margin = Padding.Empty;
        // Tablo ile canlı trafik tablosunu aynı hücrede üst üste tutan kap (sağda 10 piksel boşluk).
        pnlSolAlan = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty };
        // Canlı ağ trafiği tablosu (içi Trafik.cs'te hazırlanır); başta gizli.
        dgvTrafik = new DataGridView { Dock = DockStyle.Fill, Visible = false };
        // Kullanıcı tablodaki yazıları yanlışlıkla değiştirmesin.
        dgvBulgular.ReadOnly = true;
        // Tabloya boş satır eklenmesin.
        dgvBulgular.AllowUserToAddRows = false;
        // Soldaki satır başlık sütununu gizle.
        dgvBulgular.RowHeadersVisible = false;
        // Sütunlar tablonun genişliğini paylaşsın.
        dgvBulgular.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Tıklayınca tüm satır seçilsin.
        dgvBulgular.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        // Tek satır seçilsin.
        dgvBulgular.MultiSelect = false;
        // Uzun yazılar alt satıra insin.
        dgvBulgular.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        // Satır yüksekliği yazıya göre ayarlansın.
        dgvBulgular.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        // Sütun: trafik lambası.
        dgvBulgular.Columns.Add("Isik", "Tehlike Seviyesi");
        // Sütun: hangi kontrol buldu.
        dgvBulgular.Columns.Add("Modul", "Kontrol");
        // Sütun: bulgunun adı.
        dgvBulgular.Columns.Add("Baslik", "Ne bulundu?");
        // Sütun: ayrıntı.
        dgvBulgular.Columns.Add("Detay", "Ayrıntı");
        // Sütun: ne yapmalı.
        dgvBulgular.Columns.Add("Oneri", "Ne Yapmalısın?");
        // Sütun: ilgili KVKK maddesi ve ISO 27001 kontrolleri.
        dgvBulgular.Columns.Add("Uyum", "KVKK / ISO 27001");
        // Satır seçilince açıklama kutusu güncellensin.
        // Sağ tıklanan satır da seçilsin.
        // İlk sütundaki rozetleri (KRİTİK, UYARI, GÜVENLİ) kendimiz çizelim.
        // Tabloya sağ tık menüsünü bağla.
        dgvBulgular.ContextMenuStrip = ctxMenu;
        // Fareyle üstüne gelince açıklama çıksın.
        ipucu.SetToolTip(dgvBulgular, "Bulunan her şey burada bir satırdır. Satıra tıklayın: sağda ne anlama geldiğini okursunuz. Sağ tıklayın: kopyalama ve kaydetme menüsü açılır.");
        // Siber İlk Yardım Asistanı paneli: 6 satır (başlık, açıklama, komut başlığı, komut kutusu, 2 düğme).
        tblDetay = YerlesimYardimcilari.Tablo(1, 8);
        // Kenarlardan boşluk bırak.
        tblDetay.Padding = new Padding(12, 10, 12, 12);
        // Tek sütun: tüm genişliği kaplasın.
        tblDetay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Başlık satırı 28 piksel.
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        tblDetay.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        lblDetayBaslik = new Label { Text = "⚡ SİBER İLK YARDIM ve AI ÇÖZÜM ASİSTANI", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        // Detay metin kutusu.
        rtbDetay = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, TabStop = false, Margin = new Padding(0, 0, 0, 14) };
        // "ÇÖZÜM KOMUTU" başlık yazısı.
        lblKomutBaslik = new Label { Text = "ÇÖZÜM KOMUTU", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(0, 0, 0, 3) };
        // Çözüm komutunun gösterileceği kutu (kopyalanabilir, değiştirilemez).
        txtKomut = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, WordWrap = true, Margin = Padding.Empty };
        // Komutu panoya kopyalayan düğme.
        btnKopyala = new EfektliButon { Text = "❐ Çözüm Komutunu Kopyala", Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), Enabled = false };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnKopyala, "Yukarıdaki komutu panoya kopyalar. Sonra yönetici olarak açtığınız PowerShell'e (veya hedef Linux makinedeki terminale) yapıştırıp çalıştırabilirsiniz.");
        // Güvenlik duvarı kuralını otomatik ekleyen düğme.
        btnOnar = new EfektliButon { Text = "✚ Otomatik Onar (Güvenlik Duvarı Kuralı Ekle)", Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), Enabled = false };
        // Tıklanınca çalışacak kod.
        // (Başlangıç yazısı AsistanSifirla() içinde yazılır.)
        // Başlığı ve metni detay paneline koy.
        // ---- Mod seçici: Yerel & Manuel (ücretsiz) veya Claude API ----
        // İki eşit düğmelik şerit.
        tblMod = YerlesimYardimcilari.Tablo(2, 1);
        // Sol düğme (uzun yazılı) biraz daha geniş.
        tblMod.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Sağ düğme.
        tblMod.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Tek satır.
        tblMod.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Yerel & Manuel (ücretsiz) mod düğmesi.
        btnModYerel = new EfektliButon { Text = "◉ Yerel & Manuel Mod\r\n(Ücretsiz)", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 2, 4) };
        // Claude API modu düğmesi.
        btnModClaude = new EfektliButon { Text = "⚡ Claude API Modu", Dock = DockStyle.Fill, Margin = new Padding(2, 4, 0, 4) };
        // Düğmelere basılınca ilgili modu seç.
        // Fare açıklamaları.
        ipucu.SetToolTip(btnModYerel, "Tamamen ücretsiz ve internetsiz çalışır. Programın kendi Türkçe bilgi bankası açıklama ve komut üretir; isterseniz verileri kopyalayıp ücretsiz bir yapay zeka sohbetine yapıştırabilirsiniz.");
        // Claude modu açıklaması.
        ipucu.SetToolTip(btnModClaude, "Anthropic'in Claude yapay zekasıyla derin analiz yapar (kendi API anahtarınız gerekir). Anahtar yoksa veya kredi biterse program kendiliğinden Yerel moda döner; hiçbir şey kaybolmaz.");
        // İki düğmeyi şeride koy.
        tblMod.Controls.Add(btnModYerel, 0, 0); tblMod.Controls.Add(btnModClaude, 1, 0);
        // ---- Moda özel eylem alanı ----
        // İki alt paneli üst üste tutan kap (aynı anda yalnızca biri görünür).
        pnlAksiyon = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        // Claude modu paneli: üstte API Ayarı + onay kutusu, altta Derin Analiz düğmesi.
        pnlClaude = YerlesimYardimcilari.Tablo(2, 2);
        // Sol sütun 124 piksel.
        pnlClaude.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        // Sağ sütun kalan genişlik.
        pnlClaude.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Üst satır 32 piksel.
        pnlClaude.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        // Alt satır kalan yükseklik.
        pnlClaude.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // API anahtarı ayar düğmesi.
        btnApiAyar = new EfektliButon { Text = "⚙ API Ayarı", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 4) };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnApiAyar, "Anthropic API anahtarınızı girip kaydedersiniz. Anahtar bu bilgisayarda, yalnızca sizin Windows hesabınızın açabileceği şekilde şifrelenerek saklanır.");
        // "Tüm tarama özeti" onay kutusu.
        chkTumTarama = new CheckBox { Text = "Tüm Tarama Özetini Analiz Et", Dock = DockStyle.Fill, Margin = Padding.Empty, CheckAlign = ContentAlignment.MiddleLeft };
        // Fare açıklaması.
        ipucu.SetToolTip(chkTumTarama, "İşaretliyse yalnızca seçili satırı değil, tüm tarama sonuçlarını birlikte analiz eder. İşaretsizse tablodan seçtiğiniz satırı analiz eder.");
        // Derin analiz düğmesi.
        btnDerinAnaliz = new EfektliButon { Text = "◆ Claude AI ile Derin Analiz Et", Dock = DockStyle.Fill, Margin = Padding.Empty };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnDerinAnaliz, "Seçili bulguyu (veya tüm tarama özetini) Claude'a gönderir; sade açıklamayı ve savunma/kapatma komutlarını canlı olarak aşağıdaki kutuya yazar. Göndermeden önce onayınız istenir.");
        // Üç parçayı Claude paneline koy.
        pnlClaude.Controls.Add(btnApiAyar, 0, 0); pnlClaude.Controls.Add(chkTumTarama, 1, 0); pnlClaude.Controls.Add(btnDerinAnaliz, 0, 1); pnlClaude.SetColumnSpan(btnDerinAnaliz, 2);
        // Başlangıçta Claude paneli gizli (Yerel mod ile açılır).
        pnlClaude.Visible = false;
        // Manuel mod paneli: üstte kısa not, altta prompt kopyalama düğmesi.
        pnlManuel = YerlesimYardimcilari.Tablo(1, 2);
        // Tek sütun.
        pnlManuel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Not satırı 34 piksel.
        pnlManuel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        // Düğme satırı kalan yükseklik.
        pnlManuel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Kısa not yazısı.
        lblManuelNot = new Label { Text = "Abonelik veya API gerekmez. Metni kopyalayıp ücretsiz bir yapay zeka sohbet ekranına yapıştırın.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        // Prompt hazırlama ve kopyalama düğmesi.
        btnPromptKopyala = new EfektliButon { Text = "❐ AI Danışman Metnini (Prompt) Hazırla ve Kopyala", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4) };
        // Tıklanınca çalışacak kod.
        // Fare açıklaması.
        ipucu.SetToolTip(btnPromptKopyala, "Bulunan tüm açıkları, portları ve sistem durumunu ayrıntılı bir Türkçe soru metnine çevirip panoya kopyalar. Ücretsiz herhangi bir yapay zeka sohbetine Ctrl+V ile yapıştırabilirsiniz.");
        // Notu ve düğmeyi Manuel paneline koy.
        pnlManuel.Controls.Add(lblManuelNot, 0, 0); pnlManuel.Controls.Add(btnPromptKopyala, 0, 1);
        // İki paneli eylem alanına koy.
        pnlAksiyon.Controls.AddRange(new Control[] { pnlClaude, pnlManuel });
        // Başlığı, mod seçiciyi, açıklama kutusunu ve komut başlığını asistan paneline koy.
        tblDetay.Controls.Add(lblDetayBaslik, 0, 0); tblDetay.Controls.Add(tblMod, 0, 1); tblDetay.Controls.Add(rtbDetay, 0, 2); tblDetay.Controls.Add(lblKomutBaslik, 0, 3);
        // Komut kutusu (ince çerçeveli) ve iki düğme.
        tblDetay.Controls.Add(YerlesimYardimcilari.Cerceve(txtKomut), 0, 4); tblDetay.Controls.Add(pnlAksiyon, 0, 5); tblDetay.Controls.Add(btnOnar, 0, 6); tblDetay.Controls.Add(btnKopyala, 0, 7);
        // Tabloyu ve detay panelini içerik ızgarasına koy.
        pnlSolAlan.Controls.Add(dgvBulgular); pnlSolAlan.Controls.Add(dgvTrafik);
        // Tablonun üstündeki zincirleme risk / altın profil uyarı şeridi (başta gizli; boyunu SeritYenile ayarlar).
        lblSerit = new Label { Dock = DockStyle.Top, Visible = false, Height = 40, UseMnemonic = false, Padding = new Padding(10, 6, 10, 6), Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger), BackColor = DesignTokens.Ozgun(TokenRol.StatusDangerBg) };
        // Fare açıklaması.
        ipucu.SetToolTip(lblSerit, "Zincirleme Risk Analizi: tek tek küçük görünen açıkların birleşince saldırgana nasıl yol açabileceğini gösterir.");
        // Altın profil çubuğu: sol alt köşede düğme + durum yazısı.
        pnlAltin = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 6, 0, 0) };
        // Düğme.
        btnAltin = new EfektliButon { Text = "⚿ Bu Durumu 'Güvenli Referans (Altın Profil)' Olarak Kilitle", Dock = DockStyle.Left, Width = 420 };
        // Durum yazısı.
        lblAltinDurum = new Label { Dock = DockStyle.Fill, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), AutoEllipsis = true };
        // Tıklanınca çalışacak kod.
        // Fare açıklamaları.
        ipucu.SetToolTip(btnAltin, "Sistemini temizleyip temiz bir tarama sonucu aldığında bu düğmeyle o durumu 'referans' olarak kilitlersin. Sonraki taramalar bu temiz referansla kıyaslanır; yeni açılan kapı olursa uyarır.");
        // Durum yazısının açıklaması.
        ipucu.SetToolTip(lblAltinDurum, "Kilitli Altın Profil bilgisi (altin_profil.json).");
        // Düğmeyi ve yazıyı çubuğa koy (Fill önce eklenir).
        pnlAltin.Controls.Add(lblAltinDurum); pnlAltin.Controls.Add(btnAltin);
        // Şeridi ve çubuğu sol kaba ekle (Fill olanlardan sonra eklenmeleri gerekir).
        pnlSolAlan.Controls.Add(lblSerit); pnlSolAlan.Controls.Add(pnlAltin);
        // Tablonun en üstündeki arama kutusu.
        txtAra = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Sonuçlarda ara: örn. 445, SMB, hosts, USB ...", Font = FontProvider.Al(FontRol.Body), BorderStyle = BorderStyle.FixedSingle, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary) };
        // Fare açıklaması.
        ipucu.SetToolTip(txtAra, "Yazdıkça tabloda yalnızca bu kelimeyi içeren sonuçlar kalır. Renkli filtre kutularıyla birlikte çalışır.");
        // Sol kabın en üstüne koy (en son eklenen en üstte durur).
        pnlSolAlan.Controls.Add(txtAra);
        // Sol kabı ve detay panelini içerik ızgarasına koy.
        splIcerik.Panel1.Controls.Add(pnlSolAlan); var detayKap = YerlesimYardimcilari.Cerceve(tblDetay); detayKap.Dock = DockStyle.Fill; detayKap.MinimumSize = new Size(0, 520); splIcerik.Panel2.Controls.Add(detayKap);   // dar/alçak pencerede Asistan paneli kendi içinde kayar
        // Orta alan kabı: içinde hem radar hem tablo+asistan durur; aynı anda yalnızca biri görünür.
        pnlOrta = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        // Canlı Ağ ve Port Radarı (GDI+ ile çizilir).
        radar = new RadarPaneli { Dock = DockStyle.Fill };
        // Veri kaynağı bilgisi (fareyle üzerine gelince): kapı düğümleri gerçek tarama verisi, küre üzerindeki yaylar örnek görseldir.
        ipucu.SetToolTip(radar, "Kapı düğümleri son taramanın gerçek sonucudur. Küre üzerindeki hareketli yaylar yalnızca \"Demo yayları\" açıkken görünür ve ÖRNEK GÖRSELDİR (gerçek trafik değildir).");
        // Tablo+asistan ızgarasını da hücreyi dolduracak şekilde ayarla.
        splIcerik.Dock = DockStyle.Fill;
        // İkisini de orta alan kabına koy.
        pnlOrta.Controls.Add(splIcerik); pnlOrta.Controls.Add(radar);
        // ---- 6. satır: canlı günlük (içi GunlukKur'da doldurulur) ----
        // 2 satırlı günlük ızgarası: başlık + siyah kutu.
        tblLog = YerlesimYardimcilari.Tablo(1, 2);
        // Tek sütun: tüm genişliği kaplasın.
        tblLog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Başlık satırı 22 piksel.
        tblLog.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        // Günlük kutusu kalan yükseklik.
        tblLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Altı bölümü sağ alan ızgarasına sırayla koy.
        tblAna.Controls.Add(flwHedef, 0, 0); tblAna.Controls.Add(progress, 0, 1); tblAna.Controls.Add(tblGel, 0, 2); tblAna.Controls.Add(pnlKartlar, 0, 3);
        // Kalan üç bölüm.
        tblAna.Controls.Add(flwSerit, 0, 4); tblAna.Controls.Add(pnlOrta, 0, 5); tblAna.Controls.Add(tblLog, 0, 6);
        // Izgarayı sağ alan paneline koy.
        Controls.Add(tblAna);

        // Tablonun hazırlığını bitir.
        ((System.ComponentModel.ISupportInitialize)dgvBulgular).EndInit();
        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) components.Dispose();
        base.Dispose(disposing);
    }

    // Bir yazının (Label) yanına ⓘ simgesi koyup ikisini yan yana tek parça olarak geri verir.
    // Simgenin üstüne fareyi getirince açıklama çıkar; tıklayınca açıklama pencere olarak açılır.
    private Control EtiketBilgi(Label etiket, string baslik, string metin)
    {
        // Yazının dış boşluğunu kendi kabına devret (yan yana düzgün dursun).
        var disBosluk = etiket.Margin;
        // Yazının kendi boşluğunu sıfırla.
        etiket.Margin = new Padding(0, 3, 0, 0);
        // ⓘ simgesini yarat: elektrik mavisi, kalın, el imleçli.
        var ikon = new Label { Text = "ⓘ", AutoSize = true, ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4, 0, 0, 0) };
        // Fareyi üstüne getirince açıklama çıksın.
        ipucu.SetToolTip(ikon, metin + "\r\n\r\n(Daha rahat okumak için simgeye tıklayın.)");
        // Tıklayınca aynı açıklama büyük bir pencerede açılsın.
        ikon.Click += (_, _) => TemaliMesaj.Goster(metin, baslik, MessageBoxButtons.OK, MessageBoxIcon.Information);
        // Yazı ve simgeyi yan yana koyan kap.
        var kap = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = disBosluk, Anchor = AnchorStyles.Left };
        // Yazıyı ve simgeyi kaba ekle.
        kap.Controls.Add(etiket); kap.Controls.Add(ikon);
        // Kabı geri ver.
        return kap;
    }
}
