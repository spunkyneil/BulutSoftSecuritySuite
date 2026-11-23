// Bu dosya ekranın üstündeki 4 bilgi kartını (özet panosu) çizer ve canlı günceller.
namespace LabDenetim;

// Form1'in dördüncü parçası: istatistik kartları.
internal sealed partial class TaramaSayfasi
{
    // Şimdiye kadar yoklanan kapı sayısı (kartta canlı görünür).
    internal int _taranan;
    // Toplam yoklanacak kapı sayısı.
    internal int _toplamKapi;
    // Geçen süreyi ölçen kronometre.
    internal readonly System.Diagnostics.Stopwatch _saat = new();
    // Kartı her 0,3 saniyede bir yenileyen zamanlayıcı (süre aksın diye).
    internal GorunurZamanlayici _zamanlayici = null!;
    // Güvenlik puanı kartındaki dairesel kadran (gauge).
    internal PuanHalkasi halka = null!;
    // Dört kartın büyük rakam yazıları.
    internal Label lblKartKapi = null!, lblKartAcik = null!, lblKartPuan = null!, lblKartSure = null!;

    // Parlak yeşil: "iyi" rakamlar için.
    internal static readonly Color RakamYesil = TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusSuccess));
    // Parlak kırmızı: "kötü" rakamlar için.
    internal static readonly Color RakamKirmizi = TemaYonetici.C(DesignTokens.Ozgun(TokenRol.StatusDanger));

    // Kartları yaratır ve kart ızgarasına koyar. Form1 açılırken bir kez çağrılır.
    internal void KartlariKur()
    {
        // Kart 1: taranan kapı sayısı.
        lblKartKapi = KartYap(0, "TARANAN KAPI SAYISI", "Şimdiye kadar yoklanan kapı sayısı / toplam kapı sayısı.", Elektrik);
        // Kart 2: bulunan açıklar.
        lblKartAcik = KartYap(1, "BULUNAN AÇIKLAR", "Sarı, turuncu ve kırmızı sonuçların toplamı (yeşiller açık sayılmaz).", Simsek);
        // Kart 3: güvenlik puanı.
        lblKartPuan = KartYap(2, "GÜVENLİK PUANI", "100'den başlar. Her kırmızı sonuç 15, turuncu 7, sarı 3 puan düşürür. 80 ve üstü iyi, 50 altı kötüdür.", RakamYesil);
        // Puan kartının sağ iç boşluğuna dairesel kadranı koy.
        halka = new PuanHalkasi { Dock = DockStyle.Right, Width = 96 };
        // Kadranı puan kartının içine ekle.
        lblKartPuan.Parent!.Controls.Add(halka);
        // Kadranın üstüne gelince de aynı açıklama görünsün (daha önce yalnızca kartın boş yerlerinde görünüyordu; kadran kartın üstünde ayrı bir kutu olduğu için açıklama onun üzerinde çıkmıyordu).
        ipucu.SetToolTip(halka, "100'den başlar. Her kırmızı sonuç 15, turuncu 7, sarı 3 puan düşürür. 80 ve üstü iyi, 50 altı kötüdür.");
        // Büyük puan yazısı kadrana taşmasın diye sağa uzanmasın.
        lblKartPuan.Anchor = AnchorStyles.Top | AnchorStyles.Left; lblKartPuan.Width = 100;
        // Kart 4: geçen süre.
        lblKartSure = KartYap(3, "GEÇEN SÜRE", "Taramanın başlamasından bu yana geçen süre (dakika:saniye).", Elektrik);

        // Zamanlayıcıyı hazırla.
        _zamanlayici = new GorunurZamanlayici { Interval = 300, Sahip = this };   // yalnızca görsel sayaç; süre ölçümü kronometrededir
        // Her tıkta kartları yenile (süre akıcı görünsün).
        _zamanlayici.Tick += (_, _) => KartlariGuncelle();
        // İlk değerleri yaz.
        KartlariGuncelle();
    }

    // Tek bir kart çizer: koyu kutu, sol renkli şerit, küçük başlık, büyük rakam. Rakam yazısını geri verir.
    internal Label KartYap(int sira, string baslik, string aciklama, Color seritRengi)
    {
        // Kart kutusu: hücreyi doldurur; son kart hariç sağında 12 piksel boşluk kalır.
        var kart = new Panel { Dock = DockStyle.Fill, BackColor = ZeminKutu, Margin = new Padding(0, 0, sira < 3 ? 12 : 0, 0) };
        // Sol kenardaki renkli şerit.
        var serit = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = seritRengi };
        // Küçük gri başlık yazısı: kart genişleyince o da genişler.
        var ust = new Label { Text = baslik, AutoSize = false, Location = new Point(16, 10), Size = new Size(150, 18), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = YaziSilik, Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold) };
        // Büyük rakam yazısı.
        var deger = new Label { Text = "--", AutoSize = false, Location = new Point(16, 34), Size = new Size(150, 46), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ForeColor = seritRengi, Font = FontProvider.Al(FontRol.Mono, FontStyle.Bold) };
        // Parçaları karta koy.
        kart.Controls.AddRange(new Control[] { ust, deger, serit });
        // Fare üstüne gelince ne anlama geldiğini söyle (kartın her yerinde).
        foreach (Control c in new Control[] { kart, ust, deger, serit }) ipucu.SetToolTip(c, aciklama);
        // Kartı kart ızgarasında kendi sütununa koy.
        pnlKartlar.Controls.Add(kart, sira, 0);
        // Rakam yazısını geri ver ki sonra güncelleyebilelim.
        return deger;
    }

    // Güvenlik puanını hesaplar (100 üzerinden).
    internal int PuanHesapla() => PuanHesapla(_bulgular);

    // Verilen sonuç listesi için güvenlik puanını hesaplar (Nöbetçi Modu gibi ekran dışı kontroller de kullanır).
    internal static int PuanHesapla(IEnumerable<Bulgu> liste)
    {
        // 100 puandan başla.
        int puan = 100;
        // Her sonuç için önem derecesine göre puan düş.
        foreach (var b in liste)
            // Kırmızı 15, turuncu 7, sarı 3 puan düşürür; yeşil düşürmez.
            puan -= b.Onem switch { Onem.Yuksek => 15, Onem.Orta => 7, Onem.Dusuk => 3, _ => 0 };
        // Puan sıfırın altına inmesin.
        return Math.Max(0, puan);
    }

    // Dört kartın rakamlarını günceller.
    internal void KartlariGuncelle()
    {
        // Kart 1: kapı yoklaması yapılmadıysa çizgi, yapıldıysa "yoklanan/toplam".
        lblKartKapi.Text = _toplamKapi == 0 ? "--" : $"{_taranan}/{_toplamKapi}";
        // Kart 2: sarı + turuncu + kırmızı toplamı.
        int acik = _sari + _kirmizi;
        // Rakamı yaz.
        lblKartAcik.Text = acik.ToString();
        // Açık yoksa yeşil, kırmızı varsa kırmızı, sadece sarı/turuncu varsa sarı renk.
        lblKartAcik.ForeColor = _kirmizi > 0 ? RakamKirmizi : acik > 0 ? Simsek : RakamYesil;
        // Kart 3: henüz hiç sonuç yoksa çizgi göster.
        if (_bulgular.Count == 0)
        {
            // Çizgi yaz.
            lblKartPuan.Text = "--";
            // Kadran boş görünsün.
            halka.Puan = null;
            // Başlangıç rengi.
            lblKartPuan.ForeColor = YaziSilik;
        }
        // Sonuç varsa puanı hesapla.
        else
        {
            // Puanı hesapla.
            int puan = PuanHesapla();
            // Puanı yaz.
            lblKartPuan.Text = puan.ToString();
            // Kadran puan kadar dolsun.
            halka.Puan = puan;
            // 80 ve üstü yeşil, 50 ve üstü sarı, altı kırmızı.
            lblKartPuan.ForeColor = puan >= 80 ? RakamYesil : puan >= 50 ? Simsek : RakamKirmizi;
        }
        // Kart 4: geçen süreyi dakika:saniye olarak yaz.
        lblKartSure.Text = (_sureOverride ?? _saat.Elapsed).ToString(@"mm\:ss");
    }
}
