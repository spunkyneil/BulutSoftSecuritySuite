// Bu dosya programın "kıyafetini" (renkler, yazı tipleri) giydirir.
// Programın nasıl çalıştığını değiştirmez; sadece görünümünü değiştirir.
namespace LabDenetim;

// Form1'in üçüncü parçası: tema ve marka (BulutSoft & Şimşek).
internal sealed partial class TaramaSayfasi
{
    // Marka renkleri: tek yerde tanımlı, istersen buradan değiştirirsin.
    // Çok koyu lacivert/siyah zemin (komuta merkezi hissi).
    internal static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp);
    // Biraz daha açık koyu zemin (kutuların içi).
    internal static readonly Color ZeminKutu = DesignTokens.Ozgun(TokenRol.SurfaceCard);
    // Menü ve durum çubuğu için en koyu zemin.
    internal static readonly Color ZeminKoyu = DesignTokens.Ozgun(TokenRol.SurfaceSidebar);
    // Parlak şimşek sarısı.
    internal static readonly Color Simsek = DesignTokens.Ozgun(TokenRol.AccentPrimary);
    // Elektrik mavisi.
    internal static readonly Color Elektrik = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Açık gri yazı rengi (koyu zeminde okunur).
    internal static readonly Color Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary);
    // Silik gri yazı rengi (küçük açıklamalar için).
    internal static readonly Color YaziSilik = DesignTokens.Ozgun(TokenRol.TextMuted);

    // Programın resmi adı.
    internal static string UrunAdi => UrunBilgisi.AdVeSurum;
    // Pencerenin en üstünde görünen başlık.
    internal const string PencereBasligi = "Siber Güvenlik ve Ağ Denetim Merkezi";

    // Pencerenin başlangıç tema düzenini kurar (renk/yazı tipi atamaları). Kurucudan (Form1 açılırken) bir kez çağrılır; tema DEĞİŞİMİ ayrıca TemaYonetici üzerinden yürür.
    internal void TemaDuzeniniKur()
    {
        // Pencerenin üst başlığını yaz.
        Text = PencereBasligi;
        // Pencerenin zemin rengini koyu yap.
        BackColor = Zemin;
        // Pencerenin varsayılan yazı rengini açık yap.
        ForeColor = Yazi;

        // (Sol menü artık yeni kabuğun SidebarNav bileşenidir; eski menü düğmelerinden yalnızca Nöbetçi/Acil durum taşıyıcıları kaldı.)

        // ---- Üst hedef çubuğu ----
        // "Hedef adres" yazısı açık ve kalın olsun.
        lblHedef.ForeColor = Yazi; lblHedef.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold);
        // "Profil:" yazısı da açık ve kalın olsun.
        lblProfil.ForeColor = Yazi; lblProfil.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold);
        // İlerleme çubuğu boştayken beyaz şerit gibi durmasın; yalnızca tarama sürerken görünsün.
        progress.Visible = false;
        // "Özel" göstergesi elektrik mavisi ve kalın olsun.
        lblProfilOzel.ForeColor = Elektrik; lblProfilOzel.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
        // Üç profil düğmesini düz ve koyu boya (seçili olan sonra sarıya döner).
        foreach (var b in new[] { btnProfilHizli, btnProfilStandart, btnProfilKapsamli }) ButonBoya(b, DesignTokens.Ozgun(TokenRol.BorderStrong), Yazi);
        // Adres kutusunu boya ve büyüt.
        KutuBoya(txtHedef); txtHedef.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular);
        // "Gelişmiş" düğmesi: koyu gri zemin, açık yazı.
        ButonBoya(btnGelismis, DesignTokens.Ozgun(TokenRol.BorderStrong), Yazi);
        // Başlat butonu: şimşek sarısı zemin, siyah yazı (en dikkat çeken buton).
        ButonBoya(btnBaslat, Simsek, Color.Black);
        // Durdur butonu: koyu kırmızı zemin, beyaz yazı.
        ButonBoya(btnIptal, DesignTokens.Ozgun(TokenRol.StatusDanger), Color.White);

        // ---- Gelişmiş panel ----
        // Panel zemini koyu gri olsun.
        tblGel.BackColor = ZeminKutu;
        // Yazıların rengi.
        foreach (var l in new[] { lblPortlar, lblUrl, lblParametre, lblAlan }) l.ForeColor = Yazi;
        // Açıklama yazısı silik gri.
        lblGelIpucu.ForeColor = YaziSilik;
        // Yazı girilen kutuları boya.
        foreach (var t in new[] { txtPortlar, txtUrl, txtParametre, txtAlanAdi }) KutuBoya(t);
        // İşaret kutularının yazısı açık renk olsun.
        foreach (var c in new[] { chkPort, chkHttp, chkTls, chkDns, chkYapilandirma, chkGirdi }) c.ForeColor = Yazi;

        // ---- Trafik lambası sayaçları (koyu neon) ve rapor butonları (koyu kurumsal) ----
        // Yeşil sayaç: koyu yeşil zemin, parlak yeşil yazı ve ince parlak çerçeve.
        ChipBoya(lblYesil, DesignTokens.Ozgun(TokenRol.StatusSuccessBg), DesignTokens.Ozgun(TokenRol.StatusSuccess));
        // Sarı/turuncu sayaç: koyu kehribar zemin, parlak turuncu yazı ve çerçeve.
        ChipBoya(lblSari, DesignTokens.Ozgun(TokenRol.StatusWarningBg), DesignTokens.Ozgun(TokenRol.StatusWarning));
        // Kırmızı sayaç: koyu bordo zemin, parlak kırmızı yazı ve çerçeve.
        ChipBoya(lblKirmizi, DesignTokens.Ozgun(TokenRol.StatusDangerBg), DesignTokens.Ozgun(TokenRol.StatusDanger));
        // "[ Tümünü Göster ]" düğmesi: koyu zemin, açık mavi çerçeve.
        ChipBoya(lblTumu, ZeminKutu, DesignTokens.Ozgun(TokenRol.StatusInfo));
        // Web rapor butonu: koyu kurumsal zemin, ince açık mavi kenarlık.
        KurumsalButon(btnHtml, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.StatusInfo), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // PDF rapor butonu: aynı koyu kurumsal görünüm.
        KurumsalButon(btnPdf, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.StatusInfo), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Adli kanıt paketi butonu: koyu zemin, mor-mavi vurgu (rapor butonlarından ayırt edilsin).
        KurumsalButon(btnKanit, DesignTokens.Ozgun(TokenRol.AccentPrimary), DesignTokens.Ozgun(TokenRol.AccentHover), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Altın profil butonu: koyu altın zemin ve sarı kenarlık.
        KurumsalButon(btnAltin, DesignTokens.Ozgun(TokenRol.StatusWarningBg), DesignTokens.Ozgun(TokenRol.StatusWarning), DesignTokens.Ozgun(TokenRol.StatusWarning));
        // Tablonun altındaki çubuğun ve sol kabın zemini ana zeminle aynı olsun.
        pnlAltin.BackColor = pnlSolAlan.BackColor = ZeminKoyu;

        // ---- Siber İlk Yardım Asistanı paneli ----
        // Panelin zemini koyu lacivert olsun.
        tblDetay.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar);
        // Asistan başlığı elektrik mavisi ve kalın.
        lblDetayBaslik.ForeColor = Elektrik; lblDetayBaslik.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold);
        // Açıklama kutusu: panelle aynı koyu zemin, açık yazı, çerçevesiz.
        rtbDetay.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); rtbDetay.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); rtbDetay.Font = FontProvider.Al(FontRol.Body);
        // Komut başlığı silik gri ve kalın.
        lblKomutBaslik.ForeColor = YaziSilik; lblKomutBaslik.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
        // Komut kutusu: en koyu zemin, parlak yeşil yazı, eşit aralıklı yazı tipi.
        txtKomut.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); txtKomut.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); txtKomut.Font = FontProvider.Al(FontRol.Mono);
        // Kopyala düğmesi: koyu kurumsal görünüm.
        KurumsalButon(btnKopyala, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.StatusInfo), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Otomatik Onar düğmesi: dikkat çekmesi için koyu kehribar zemin ve turuncu kenarlık (sistemi değiştirir).
        KurumsalButon(btnOnar, DesignTokens.Ozgun(TokenRol.StatusWarningBg), DesignTokens.Ozgun(TokenRol.StatusWarning), DesignTokens.Ozgun(TokenRol.StatusWarning));
        // "Hedef Seç / İç Ağ Keşfi" düğmesi: koyu mavi zemin, parlak açık mavi kenarlık.
        KurumsalButon(btnHedefSec, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.StatusInfo), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Hedef menüsünü koyu temaya boya (menü zemini ve yazısı).
        foreach (var m in new[] { ctxHedef, ctxMenu }) { m.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); m.ForeColor = Yazi; }
        // API Ayarı düğmesi: koyu kurumsal görünüm.
        KurumsalButon(btnApiAyar, DesignTokens.Ozgun(TokenRol.SurfaceElevated), DesignTokens.Ozgun(TokenRol.TextMuted), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Derin Analiz düğmesi: koyu indigo zemin, parlak indigo kenarlık (yapay zeka rengi).
        KurumsalButon(btnDerinAnaliz, DesignTokens.Ozgun(TokenRol.AccentPrimary), DesignTokens.Ozgun(TokenRol.AccentHover), DesignTokens.Ozgun(TokenRol.TextPrimary));
        // Prompt kopyalama düğmesi: koyu yeşil zemin, parlak yeşil kenarlık (ücretsiz mod rengi).
        KurumsalButon(btnPromptKopyala, DesignTokens.Ozgun(TokenRol.StatusSuccessBg), DesignTokens.Ozgun(TokenRol.StatusSuccess), DesignTokens.Ozgun(TokenRol.StatusSuccessBg));
        // "Tüm tarama özeti" onay kutusunun yazısı açık renk olsun.
        chkTumTarama.ForeColor = Yazi; chkTumTarama.Font = FontProvider.Al(FontRol.Caption);
        // Manuel mod notu silik ve küçük olsun.
        lblManuelNot.ForeColor = YaziSilik; lblManuelNot.Font = FontProvider.Al(FontRol.Caption);

        // ---- Alt durum çubuğu ----
        // Çubuğun zemini en koyu renk olsun.
        Sahibi.DurumZemini(ZeminKoyu);
        // Durum yazısı elektrik mavisi (canlı görünsün).
        Sahibi.DurumYazisiRengi(Elektrik);
        // İmza şimşek sarısı ve kalın olsun.

        // ---- Sonuç tablosu ----
        // Titreme ve silik yazı kalıntılarını önlemek için tabloyu çift tamponlu (pürüzsüz) çizdir.
        typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(dgvBulgular, true);
        // Tablonun boş kısım rengi koyu siber lacivert olsun.
        dgvBulgular.BackgroundColor = DesignTokens.Ozgun(TokenRol.SurfaceCard);
        // Yatay çizgi rengi koyu ve ince olsun.
        dgvBulgular.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Sadece satırlar arası yatay çizgi görünsün (dikey çizgiler olmasın).
        dgvBulgular.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        // Tablonun dış çerçevesini kaldır.
        dgvBulgular.BorderStyle = BorderStyle.None;
        // Başlık satırı kendi rengimizle boyansın (Windows'unkiyle değil).
        dgvBulgular.EnableHeadersVisualStyles = false;
        // Başlık satırının çerçevesini kaldır.
        dgvBulgular.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        // Başlık satırı yüksekliği sabit olsun.
        dgvBulgular.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        // Başlık satırının yüksekliği.
        dgvBulgular.ColumnHeadersHeight = 36;
        // Başlık satırı koyu kurumsal zeminli olsun.
        dgvBulgular.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Başlık yazıları açık mavi olsun.
        dgvBulgular.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo);
        // Başlık yazıları kalın olsun.
        dgvBulgular.ColumnHeadersDefaultCellStyle.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
        // Başlık yazıları alt satıra inmesin (tek satırda kalsın).
        dgvBulgular.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        // Başlıkta iç boşluk bırak.
        dgvBulgular.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        // Seçili başlık rengi değişmesin.
        dgvBulgular.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Satırların arka planı koyu siber lacivert (#0F172A).
        dgvBulgular.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard);
        // Hücre yazısı açık gri-mavi olsun.
        dgvBulgular.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Yazı tipi.
        dgvBulgular.DefaultCellStyle.Font = FontProvider.Al(FontRol.Body);
        // Hücre içi boşluk: yazılar kenarlara yapışmasın.
        dgvBulgular.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
        // Seçili satırın arka planı göz yormayan koyu siber mavi (#1E3A5F).
        dgvBulgular.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Seçili satırın yazısı beyaz olsun.
        dgvBulgular.DefaultCellStyle.SelectionForeColor = Color.White;
        // Aralıklı satırların arka planı biraz daha açık antrasit (#162032).
        dgvBulgular.AlternatingRowsDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput);
        // Aralıklı satırlarda da seçili renk aynı olsun.
        dgvBulgular.AlternatingRowsDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Aralıklı satırlarda seçili yazı beyaz olsun.
        dgvBulgular.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
        // Satırlar en az bu kadar yüksek olsun (rozet rahat sığsın).
        dgvBulgular.RowTemplate.MinimumHeight = 40;
        // Yeni satırların varsayılan yüksekliği.
        dgvBulgular.RowTemplate.Height = 40;
        // Yazılar sütuna sığmayınca alt satıra insin ve satır yüksekliği buna göre büyüsün (kelime ortadan bölünmesin).
        dgvBulgular.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        // Satır yüksekliğini içeriğe göre otomatik ayarla.
        dgvBulgular.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        // Sütunlar tablonun genişliğini paylaşsın (ilk sütun hariç, aşağıda sabitleniyor).
        dgvBulgular.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // İlk sütun (rozet): sabit genişlik.
        dgvBulgular.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        // Rozet sütununun genişliği.
        dgvBulgular.Columns[0].Width = 118;
        // Rozet sütunu yeniden boyutlandırılamasın.
        dgvBulgular.Columns[0].Resizable = DataGridViewTriState.False;
        // Kontrol sütunu: dar, ama kelime sığacak kadar.
        dgvBulgular.Columns[1].FillWeight = 60; dgvBulgular.Columns[1].MinimumWidth = 80;
        // "Ne bulundu?" sütunu: en geniş.
        dgvBulgular.Columns[2].FillWeight = 130; dgvBulgular.Columns[2].MinimumWidth = 120;
        // "Ayrıntı" sütunu.
        dgvBulgular.Columns[3].FillWeight = 110; dgvBulgular.Columns[3].MinimumWidth = 100;
        // "Ne Yapmalısın?" sütunu.
        dgvBulgular.Columns[4].FillWeight = 120; dgvBulgular.Columns[4].MinimumWidth = 110;
        // "Uyumluluk" sütunu: kısa KVKK/ISO kodları.
        dgvBulgular.Columns[5].FillWeight = 70; dgvBulgular.Columns[5].MinimumWidth = 80;
        // Beyaz Windows kaydırma çubukları yerine koyu olanlar: tablo, asistan açıklama kutusu ve komut kutusu.
        KoyuTema.UygulaTablo(dgvBulgular); KoyuTema.Uygula(rtbDetay); KoyuTema.Uygula(txtKomut);
    }

    // Windows, yazıdaki "&" işaretini "kısayol harfi" sanıp siler (örn. "Yerel & Manuel" → "Yerel  Manuel" olur).
    // Bu fonksiyon tüm yazı, buton ve işaret kutularında bu davranışı kapatır ki "&" aynen görünsün.
    internal static void MnemonicKapat(Control kok)
    {
        // Bu öğenin türüne göre ayarı kapat.
        switch (kok)
        {
            // Yazı alanı.
            case Label l: l.UseMnemonic = false; break;
            // Düğme.
            case Button b: b.UseMnemonic = false; break;
            // İşaret kutusu.
            case CheckBox c: c.UseMnemonic = false; break;
        }
        // İçindeki tüm öğeler için de aynısını yap.
        foreach (Control alt in kok.Controls) MnemonicKapat(alt);
    }

    // Özet kutusunu (sayaç) koyu neon görünüme boyar: koyu zemin, parlak yazı, ince parlak çerçeve.
    internal static void ChipBoya(Label l, Color zemin, Color neon)
    {
        // Koyu zemin.
        l.BackColor = zemin;
        // Parlak neon yazı.
        l.ForeColor = neon;
        // Kalın yazı.
        l.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
        // Çerçeveyi kendimiz çizelim: kutunun kenarına 1 piksellik neon çizgi.
        // Filtre açıkken (Tag = true) çerçeve 3 piksel kalınlaşır.
        l.Paint += (_, e) => { bool aktif = l.Tag is true; using var kalem = new Pen(neon, aktif ? 3f : 1f); e.Graphics.DrawRectangle(kalem, aktif ? 1 : 0, aktif ? 1 : 0, l.Width - (aktif ? 3 : 1), l.Height - (aktif ? 3 : 1)); };
        // Tıklanabilir olduğunu belli etmek için el imleci.
        l.Cursor = Cursors.Hand;
    }

    // Bir butonu koyu kurumsal görünüme boyar: koyu zemin, ince renkli kenarlık, parlak yazı.
    internal static void KurumsalButon(Button b, Color zemin, Color kenar, Color yazi)
    {
        // Düz görünüm.
        b.FlatStyle = FlatStyle.Flat;
        // İnce (1 piksel) kenarlık.
        b.FlatAppearance.BorderSize = 1;
        // Kenarlık rengi.
        b.FlatAppearance.BorderColor = kenar;
        // Fare üstüne gelince biraz aydınlansın.
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(zemin, 0.15f);
        // Tıklarken biraz daha aydınlansın.
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Light(zemin, 0.3f);
        // Zemin rengi.
        b.BackColor = zemin;
        // Yazı rengi.
        b.ForeColor = yazi;
        // Yazı tipi: emoji de gösterebilen, kalın.
        b.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
        // Fare üstüne gelince el işareti göster.
        b.Cursor = Cursors.Hand;
    }

    // Bir menü düğmesini boyar; "secili" ise sarı vurgulu görünür.
    internal static void MenuBoya(Button b, bool secili)
    {
        // Düz görünüm.
        b.FlatStyle = FlatStyle.Flat;
        // Çerçeveyi kaldır.
        b.FlatAppearance.BorderSize = 0;
        // Fare üstüne gelince hafif aydınlansın.
        b.FlatAppearance.MouseOverBackColor = DesignTokens.Ozgun(TokenRol.BorderSubtle);
        // Seçiliyse hafif aydınlık zemin, değilse menü zemini.
        b.BackColor = secili ? DesignTokens.Ozgun(TokenRol.SurfaceCard) : ZeminKoyu;
        // Seçiliyse sarı yazı, değilse açık gri.
        b.ForeColor = secili ? Simsek : Yazi;
        // Yazıyı sola yasla.
        b.TextAlign = ContentAlignment.MiddleLeft;
        // Sol tarafta boşluk bırak.
        b.Padding = new Padding(14, 0, 0, 0);
        // Düğmeler arasında boşluk olmasın.
        b.Margin = Padding.Empty;
        // Yazı tipi burada SABİT verilmez: menü düğmelerinin yazı boyu pencere boyuna göre Kisayol.cs içindeki MenuSigdir ile ayarlanır.
        // Fare üstüne gelince el işareti göster.
        b.Cursor = Cursors.Hand;
    }

    // Yazı girilen bir kutuyu koyu temaya boyar.
    internal static void KutuBoya(TextBox t)
    {
        // Kutunun içi koyu.
        t.BackColor = ZeminKutu;
        // Yazılan yazı açık.
        t.ForeColor = Yazi;
        // Düz (modern) çerçeve.
        t.BorderStyle = BorderStyle.FixedSingle;
    }

    // Bir butonu düz (modern) görünüme ve verilen renklere boyar.
    internal static void ButonBoya(Button b, Color zemin, Color yazi)
    {
        // Düz görünüm: Windows'un gri 3 boyutlu butonu yerine modern düz buton.
        b.FlatStyle = FlatStyle.Flat;
        // Çerçeve kalınlığını sıfırla.
        b.FlatAppearance.BorderSize = 0;
        // Buton zemin rengi.
        b.BackColor = zemin;
        // Buton yazı rengi.
        b.ForeColor = yazi;
        // Yazıyı kalın yap.
        b.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
        // Fare üstüne gelince el işareti göster.
        b.Cursor = Cursors.Hand;
    }
}
