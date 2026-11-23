// Bu dosya yeni kabuk bileşenlerinin ORTAK TABANLARINI içerir: tokenlarla kendini boyayan özel çizimli kontrol, token zeminli panel ve yetki denetimi.
// Bu sınıflar IKendiRenginiYoneten olduğu için TemaYonetici onları palete çevirmez; tema değişince IThemeAware.TemaUygula ile yeniden çizilirler (olay tabanlı, tarama yok).

// Bu bileşenler tasarımcıda düzenlenmez (kodla kurulur); tasarımcı serileştirme uyarısı kapatıldı.
#pragma warning disable WFO1000
// Programımızın adı.
namespace LabDenetim;

// Özel çizimli kabuk kontrolü: zemini DesignTokens'tan boyar, DPI'a duyarlıdır.
internal abstract class KabukKontrol : Control, IKendiRenginiYoneten, IThemeAware
{
    protected KabukKontrol()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    // Zemin rolü (varsayılan: uygulama zemini).
    protected virtual TokenRol ZeminRolu => TokenRol.SurfaceApp;

    // Tema değişince yeniden çizilir.
    public virtual void TemaUygula() => Invalidate();

    // Boşluk/boyut değerini bu kontrolün DPI'ına göre pikele çevirir.
    protected int Px(int deger) => DesignTokens.Px(this, deger);

    // DPI değişince boyutları yeniden hesaplatır.
    protected virtual void Boyutlandir() { }

    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Boyutlandir(); PerformLayout(); Invalidate(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Boyutlandir(); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var f = new SolidBrush(DesignTokens.Renk(ZeminRolu)); e.Graphics.FillRectangle(f, ClientRectangle);
    }

    // Klavye ile etkinleştirilebilen (tek düğme gibi) kontroller için Enter/Space.
    protected virtual bool EtkinlestirmeTusu(Keys k) => k is Keys.Enter or Keys.Space;
}

// Token zeminli panel (kabuğun yerleşim kapları).
internal class TokenPanel : Panel, IKendiRenginiYoneten, IThemeAware
{
    private TokenRol _zemin;
    public TokenPanel(TokenRol zemin = TokenRol.SurfaceApp) { _zemin = zemin; BackColor = DesignTokens.Renk(zemin); DoubleBuffered = true; }
    // Zemin rolü.
    public TokenRol Zemin { get => _zemin; set { _zemin = value; BackColor = DesignTokens.Renk(value); } }
    public virtual void TemaUygula() { BackColor = DesignTokens.Renk(_zemin); Invalidate(); }
}

// Yatay/dikey dizilim kabı (saydam zeminli; ebeveyn rengini gösterir).
internal sealed class KabukAkis : FlowLayoutPanel, IKendiRenginiYoneten
{
    public KabukAkis() { BackColor = Color.Transparent; WrapContents = false; Margin = Padding.Empty; }
}

// Yetki denetimi: eski Form1.YetkiKontrol ile aynı davranış (neden gösterilir, yetkisiz deneme olay günlüğüne yazılır).
internal static class KabukYetki
{
    // Gerekli yetki varsa true; yoksa nedenini gösterip false döner.
    public static bool Kontrol(IWin32Window? sahip, Eylem? eylem, string ozellik)
    {
        if (eylem == null || Yetki.Izinli(ServisBaglami.Oturum.Rol, eylem.Value)) return true;
        TemaliMesaj.Goster(sahip, (ozellik.Length > 0 ? "\"" + ozellik + "\" özelliği için yetkiniz yok.\r\n\r\n" : "") + Yetki.RedMesaji(ServisBaglami.Oturum.Rol, eylem.Value), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        try { ServisBaglami.OlayYazici.Ekle("Oturum", "Uyarı", $"OTURUM -> Yetkisiz işlem denemesi: {ServisBaglami.Oturum.Aktif?.Ad ?? "?"} ({ozellik} {eylem})"); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KabukYetki.Kontrol"); }
        return false;
    }
}
