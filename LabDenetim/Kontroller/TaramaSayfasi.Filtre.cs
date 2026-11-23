// Bu dosya tablonun üstündeki YEŞİL / SARI-TURUNCU / KIRMIZI kutuları tıklanabilir filtre düğmelerine çevirir.
// Birine tıklayınca tabloda yalnızca o seviyedeki sonuçlar kalır; tekrar tıklayınca (veya "[ Tümünü Göster ]"e basınca) hepsi geri gelir.
namespace LabDenetim;

// Form1'in on dördüncü parçası: sonuç filtreleri.
internal sealed partial class TaramaSayfasi
{
    // Hangi filtre açık? (Yok = hepsi görünür)
    internal enum Filtre { Yok, Yesil, Sari, Kirmizi }
    // Şu anki filtre.
    internal Filtre _filtre = Filtre.Yok;

    // Bir sonucun verilen filtreye uyup uymadığını söyler. "Yok" filtresinde her şey uyar.
    internal static bool FiltreyeUyar(Filtre f, Bulgu b) => f switch
    {
        // Yeşil: normal sonuçlar (kontrol yapılamayanlar hariç).
        Filtre.Yesil => !b.Atlandi && b.Onem == Onem.Bilgi,
        // Sarı/turuncu: dikkat ve uyarı seviyesi.
        Filtre.Sari => !b.Atlandi && (b.Onem == Onem.Dusuk || b.Onem == Onem.Orta),
        // Kırmızı: kritik.
        Filtre.Kirmizi => !b.Atlandi && b.Onem == Onem.Yuksek,
        // Filtre yoksa hepsi.
        _ => true
    };

    // Filtre kutularına tıklamayı bağlar ve ilk görünümü kurar. Form1 açılırken bir kez çağrılır.
    internal void FiltreKur()
    {
        // Yeşil kutuya tıklanınca yeşil filtreyi aç/kapat.
        lblYesil.Click += (_, _) => FiltreSec(Filtre.Yesil);
        // Sarı/turuncu kutuya tıklanınca.
        lblSari.Click += (_, _) => FiltreSec(Filtre.Sari);
        // Kırmızı kutuya tıklanınca.
        lblKirmizi.Click += (_, _) => FiltreSec(Filtre.Kirmizi);
        // "[ Tümünü Göster ]"e tıklanınca filtreyi kaldır.
        lblTumu.Click += (_, _) => { _filtre = Filtre.Yok; FiltreyiUygula(); };
        // İlk görünümü kur.
        ChipYenile();
    }

    // Bir filtre kutusuna tıklandı: aynısı açıksa kapat, değilse onu aç.
    internal void FiltreSec(Filtre f)
    {
        // Açık olana tekrar tıklanırsa filtre kalkar.
        _filtre = _filtre == f ? Filtre.Yok : f;
        // Tabloya uygula.
        FiltreyiUygula();
    }

    // Filtreyi tablodaki tüm satırlara uygular (uymayanlar gizlenir).
    internal void FiltreyiUygula()
    {
        // Gizlenecek satır "geçerli hücre" olamaz; önce geçerli hücreyi kaldır.
        dgvBulgular.CurrentCell = null;
        // Her satırı gez.
        foreach (DataGridViewRow r in dgvBulgular.Rows)
            // Satırın içindeki sonuca göre göster/gizle.
            if (r.Tag is Bulgu b) r.Visible = _arama.SatirGorunur(b);
        // Filtre kutularının görünümünü güncelle.
        ChipYenile();
        // İlk görünen satırı bul.
        var ilk = dgvBulgular.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Visible);
        // Eski seçimi kaldır.
        dgvBulgular.ClearSelection();
        // Görünen satır varsa seç (asistan dolsun); yoksa asistanı sıfırla.
        if (ilk != null) ilk.Selected = true; else AsistanSifirla();
    }

    // Filtre kutularını açık/kapalı durumuna göre boyar: açık olanın çerçevesi kalınlaşır ve zemini parlar.
    internal void ChipYenile()
    {
        // Yeşil kutu.
        ChipDurum(lblYesil, DesignTokens.Ozgun(TokenRol.StatusSuccessBg), _filtre == Filtre.Yesil);
        // Sarı/turuncu kutu.
        ChipDurum(lblSari, DesignTokens.Ozgun(TokenRol.StatusWarningBg), _filtre == Filtre.Sari);
        // Kırmızı kutu.
        ChipDurum(lblKirmizi, DesignTokens.Ozgun(TokenRol.StatusDangerBg), _filtre == Filtre.Kirmizi);
        // "Tümünü Göster": filtre açıkken parlak mavi, kapalıyken silik.
        bool acik = _filtre != Filtre.Yok;
        // Yazı rengi.
        lblTumu.ForeColor = acik ? Elektrik : YaziSilik;
        // Çerçeve kalınlığı için işaret.
        lblTumu.Tag = acik;
        // Yeniden çiz.
        lblTumu.Invalidate();
    }

    // Tek bir filtre kutusunun görünümünü ayarlar.
    internal static void ChipDurum(Label l, Color zemin, bool aktif)
    {
        // Çerçeve kalınlığı bu işarete bakılarak çizilir.
        l.Tag = aktif;
        // Açıkken zemin biraz parlak olsun.
        l.BackColor = aktif ? ControlPaint.Light(zemin, 0.45f) : zemin;
        // Yeniden çiz.
        l.Invalidate();
    }
}
