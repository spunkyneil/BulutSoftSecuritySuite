// Bu dosya Savunma Merkezi'nin ÜST ÇUBUĞUDUR: solda belirgin kırmızı "Acil İzolasyon" eylem kartı, sağda "Nöbetçi Modu" durum kartı.
// Kartlar, SavunmaDurumServisi'nin (Acil İzolasyon + Nöbetçi durumu) Degisti olayına abone olur ve anında güncellenir (eski görünmez menü düğmeleri kalktı); böylece Kalkan.cs ve Nobet.cs'in
// düğme metnini güncelleyen mevcut kodu bozulmadan, görünür yeni çubuk anında güncel kalır. Tıklama, eski işleyicilere (yetki + onay akışı aynen) yönlendirilir.

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Kartın görsel tonu.
internal enum KartTonu { Notr, Basari, Tehlike, Kilitli }

// Başlık + açıklama + sağda eylem hapı olan durum/eylem kartı.
internal sealed class DurumKarti : KabukDugmesi
{
    private string _baslik = "", _aciklama = "", _eylem = ""; private KartTonu _ton = KartTonu.Notr; private IkonAdi _ikon = IkonAdi.Kalkan; private bool _etkin = true;
    public string Baslik { get => _baslik; set { _baslik = value; AccessibleName = value; Invalidate(); } }
    public string Aciklama { get => _aciklama; set { _aciklama = value; Invalidate(); } }
    public string EylemYazisi { get => _eylem; set { _eylem = value; Invalidate(); } }
    public KartTonu Ton { get => _ton; set { _ton = value; Invalidate(); } }
    public IkonAdi KartIkonu { get => _ikon; set { _ikon = value; Invalidate(); } }
    public bool KartEtkin { get => _etkin; set { _etkin = value; Cursor = value ? Cursors.Hand : Cursors.Default; Invalidate(); } }
    // Kilitli karta tıklandı (kabuk yetki mesajını gösterir).
    public event Action? KilitliTiklandi;
    public override Size GetPreferredSize(Size proposedSize) => new(Math.Max(Px(260), proposedSize.Width), Px(68));

    private (Color Zemin, Color Cizgi, Color Vurgu) Renkler() => _ton switch
    {
        KartTonu.Tehlike => (DesignTokens.StatusDangerBg, DesignTokens.StatusDanger, DesignTokens.StatusDanger),
        KartTonu.Basari => (DesignTokens.StatusSuccessBg, DesignTokens.StatusSuccess, DesignTokens.StatusSuccess),
        KartTonu.Kilitli => (DesignTokens.SurfaceCard, DesignTokens.BorderSubtle, DesignTokens.TextMuted),
        _ => (DesignTokens.SurfaceCard, DesignTokens.BorderSubtle, DesignTokens.AccentPrimary)
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; Cizim.Kalite(g); var (zemin, cizgi, vurgu) = Renkler();
        var r = new Rectangle(0, Px(DesignTokens.SpaceXs), Width - Px(DesignTokens.SpaceSm), Height - 2 * Px(DesignTokens.SpaceXs));
        Cizim.Doldur(g, r, DesignTokens.RadiusMd, zemin);
        if (Uzerinde && _etkin && _ton != KartTonu.Kilitli) Cizim.Doldur(g, r, DesignTokens.RadiusMd, DesignTokens.Saydam(vurgu, 36));
        Cizim.Cerceve(g, r, DesignTokens.RadiusMd, cizgi, _ton == KartTonu.Tehlike ? 1.5f : 1f);
        int ik = Px(24), x = r.X + Px(DesignTokens.SpaceLg);
        Ikonlar.Ciz(g, _ton == KartTonu.Kilitli ? IkonAdi.Kilit : _ikon, new RectangleF(x, r.Y + (r.Height - ik) / 2f, ik, ik), vurgu, 1.8f); x += ik + Px(DesignTokens.SpaceMd);
        int pillW = _eylem.Length == 0 ? 0 : Cizim.Genislik(_eylem, FontRol.Body) + Px(DesignTokens.SpaceXl) + Px(DesignTokens.SpaceSm), pillH = Px(32);
        int tw = r.Right - x - pillW - Px(DesignTokens.SpaceLg) - (pillW > 0 ? Px(DesignTokens.SpaceMd) : 0);
        Color yazi = _etkin ? DesignTokens.TextPrimary : DesignTokens.TextMuted;
        Cizim.Metin(g, _baslik, FontRol.Subheading, new Rectangle(x, r.Y + Px(DesignTokens.SpaceSm), tw, r.Height / 2 - Px(DesignTokens.SpaceSm) + Px(4)), yazi, TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        Cizim.Metin(g, _aciklama, FontRol.Caption, new Rectangle(x, r.Y + r.Height / 2 + Px(4), tw, r.Height / 2 - Px(DesignTokens.SpaceSm)), DesignTokens.TextSecondary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (pillW > 0)
        {
            var p = new Rectangle(r.Right - Px(DesignTokens.SpaceLg) - pillW, r.Y + (r.Height - pillH) / 2, pillW, pillH);
            if (_ton == KartTonu.Kilitli || !_etkin) { Cizim.Cerceve(g, p, pillH / 2f, DesignTokens.BorderStrong); Cizim.Metin(g, _eylem, FontRol.Body, p, DesignTokens.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding); }
            else { Cizim.Doldur(g, p, pillH / 2f, vurgu); Cizim.Metin(g, _eylem, FontRol.Body, p, DesignTokens.TextOnAccent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding, FontStyle.Bold); }
        }
        OdakHalkasi(g, r);
    }

    protected override void OnClick(EventArgs e)
    {
        if (_ton == KartTonu.Kilitli) { KilitliTiklandi?.Invoke(); return; }
        if (!_etkin) return;
        base.OnClick(e);
    }
}

// Savunma Merkezi'nin üst çubuğu (Acil İzolasyon + Nöbetçi durumu).
internal sealed class SavunmaUstCubuk : TableLayoutPanel, IKendiRenginiYoneten, IThemeAware
{
    private readonly SavunmaDurumServisi _servis; private readonly DurumKarti _acilKarti = new(), _nobetKarti = new();

    // acil / nobet: durumu (metin, etkinlik) okunan eski menü düğmeleri; acilTikla / nobetTikla: eski işleyicileri (yetki + onay akışı) çalıştırır.
    public SavunmaUstCubuk(SavunmaDurumServisi servis, Action acilTikla, Action nobetTikla)
    {
        _servis = servis; DoubleBuffered = true; Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; ColumnCount = 2; RowCount = 1; Margin = Padding.Empty;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); RowStyles.Add(new RowStyle(SizeType.AutoSize));
        BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Padding = new Padding(DesignTokens.SpaceLg, DesignTokens.SpaceMd, DesignTokens.SpaceMd, DesignTokens.SpaceSm);
        foreach (var k in new[] { _acilKarti, _nobetKarti }) { k.Dock = DockStyle.Fill; k.Margin = Padding.Empty; k.AutoSize = true; }
        _acilKarti.KartIkonu = IkonAdi.Uyari; _nobetKarti.KartIkonu = IkonAdi.Kalkan;
        _acilKarti.Click += (_, _) => acilTikla(); _nobetKarti.Click += (_, _) => nobetTikla();
        _acilKarti.KilitliTiklandi += () => KabukYetki.Kontrol(FindForm(), Eylem.SistemDegistir, "Acil İzolasyon"); _nobetKarti.KilitliTiklandi += () => KabukYetki.Kontrol(FindForm(), Eylem.Koruma, "Nöbetçi Modu");
        Controls.Add(_acilKarti, 0, 0); Controls.Add(_nobetKarti, 1, 0);
        // Eski düğmelerin durumu değişince kartlar anında güncellenir (olay tabanlı).
        _servis.Degisti += Degisti;
        Yenile();
    }

    // Servis olayı herhangi bir iş parçacığından gelebilir: arayüz iş parçacığına aktarılır.
    private void Degisti() { if (IsDisposed) return; if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(Yenile); } else Yenile(); }

    // Kartları eski düğmelerin güncel durumundan ve rolden yeniden kurar.
    public void Yenile()
    {
        var rol = ServisBaglami.Oturum.Rol;
        bool aktif = _servis.AcilAktif, acilKilit = NavigasyonModeli.Kilitli(Eylem.SistemDegistir, rol);
        _acilKarti.Baslik = aktif ? "İZOLASYON AÇIK" : "ACİL İZOLASYON";
        _acilKarti.Aciklama = aktif ? "21, 23, 135, 139, 445, 3389 ve 5900 numaralı gelen kapılar engelli. Kaldırmak için tıklayın." : "Şüpheli durumda tehlikeli gelen kapıları tek komutla engeller. Onay ve yönetici izni ister.";
        _acilKarti.EylemYazisi = aktif ? "Kaldır" : "Uygula"; _acilKarti.Ton = acilKilit ? KartTonu.Kilitli : aktif ? KartTonu.Basari : KartTonu.Tehlike; _acilKarti.KartEtkin = !_servis.AcilMesgul;
        bool nAcik = _servis.NobetAktif, nKilit = NavigasyonModeli.Kilitli(Eylem.Koruma, rol);
        _nobetKarti.Baslik = nAcik ? "Nöbetçi Modu: AÇIK" : "Nöbetçi Modu: KAPALI";
        _nobetKarti.Aciklama = nAcik ? "Küçülünce tepsiden izler; 15 dakikada bir kontrol eder." : "Tepsiden izler; 15 dakikada bir kontrol eder.";
        _nobetKarti.EylemYazisi = nAcik ? "Kapat" : "Aç"; _nobetKarti.Ton = nKilit ? KartTonu.Kilitli : nAcik ? KartTonu.Basari : KartTonu.Notr; _nobetKarti.KartEtkin = !_servis.NobetMesgul;
        Invalidate(true);
    }

    // Test ve kabuk için kart durumları.
    public (string Baslik, KartTonu Ton, bool Etkin) AcilDurumu => (_acilKarti.Baslik, _acilKarti.Ton, _acilKarti.KartEtkin);
    public (string Baslik, KartTonu Ton, bool Etkin) NobetDurumu => (_nobetKarti.Baslik, _nobetKarti.Ton, _nobetKarti.KartEtkin);

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Padding = new Padding(DesignTokens.Px(this, DesignTokens.SpaceLg), DesignTokens.Px(this, DesignTokens.SpaceMd), DesignTokens.Px(this, DesignTokens.SpaceMd), DesignTokens.Px(this, DesignTokens.SpaceSm)); }
    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Yenile(); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _servis.Degisti -= Degisti;
        base.Dispose(disposing);
    }
}
