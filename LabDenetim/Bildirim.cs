// Bu dosya "tarama bitti" gibi durumlarda ekranın sağ altında kısa süre görünen küçük bildirim kutusunu (toast) çizer.
// Kutu odağı ÇALMAZ (yazı yazarken kesmez), 6 saniye sonra kendiliğinden kapanır, tıklayınca hemen kapanır. Her pencereden çağrılabilir.
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// Bildirim türü: rengi belirler.
internal enum BildirimTuru { Bilgi, Basari, Uyari, Hata }

// Bildirim kutusu.
internal sealed class BildirimKutusu : TemaliForm
{
    // Açık kutular (üst üste binmesin diye yukarı doğru dizilir).
    private static readonly List<BildirimKutusu> Acik = new();
    // Kapanma sayacı.
    private readonly System.Windows.Forms.Timer _sayac = new() { Interval = 6000 };
    // Rengi ve yazısı.
    private readonly Color _serit; private readonly string _baslik, _mesaj;

    // Oluşturur.
    public BildirimKutusu(string baslik, string mesaj, BildirimTuru tur, Action? tiklaninca = null)
    {
        // Çerçevesiz, görev çubuğunda görünmeyen, hep üstte kutu.
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; Size = new Size(360, 92); BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); DoubleBuffered = true;
        _baslik = baslik; _mesaj = mesaj;
        _serit = tur switch { BildirimTuru.Basari => DesignTokens.Ozgun(TokenRol.StatusSuccess), BildirimTuru.Uyari => DesignTokens.Ozgun(TokenRol.StatusWarning), BildirimTuru.Hata => DesignTokens.Ozgun(TokenRol.StatusDanger), _ => DesignTokens.Ozgun(TokenRol.StatusInfo) };
        // Konum: çalışma alanının sağ altı; öncekilerin üstüne dizilir.
        var alan = Screen.FromPoint(Cursor.Position).WorkingArea; int ust = 0; foreach (var k in Acik) ust += k.Height + 8;
        Location = new Point(alan.Right - Width - 16, alan.Bottom - Height - 16 - ust);
        // Tıklayınca kapan, süre dolunca kapan.
        Click += (_, _) => { try { tiklaninca?.Invoke(); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Bildirim.BildirimKutusu"); } Close(); }; _sayac.Tick += (_, _) => Close();
        Shown += (_, _) => _sayac.Start();
        FormClosed += (_, _) => { _sayac.Stop(); _sayac.Dispose(); Acik.Remove(this); YenidenDiz(); };
        Acik.Add(this);
    }

    // Odak çalmadan göster.
    protected override bool ShowWithoutActivation => true;
    // Pencere stili: etkinleştirilmesin ve araç penceresi olsun (Alt+Tab'da görünmesin).
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; } }

    // Biri kapanınca kalanları aşağı kaydırır.
    private static void YenidenDiz()
    {
        int ust = 0; foreach (var k in Acik.ToList()) { var alan = Screen.FromPoint(k.Location).WorkingArea; k.Location = new Point(alan.Right - k.Width - 16, alan.Bottom - k.Height - 16 - ust); ust += k.Height + 8; }
    }

    // Çizim.
    protected override void OnPaint(PaintEventArgs e)
    {
        // Renkler geçerli temaya (koyu/açık) uyar.
        var g = e.Graphics; g.Clear(BackColor); var seritRenk = TemaYonetici.C(_serit);
        using (var b = new SolidBrush(seritRenk)) g.FillRectangle(b, 0, 0, 6, Height);
        using (var p = new Pen(TemaYonetici.C(DesignTokens.Ozgun(TokenRol.BorderStrong)))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        using var f1 = FontProvider.Gecici("Segoe UI", 10.5f, FontStyle.Bold); using var f2 = FontProvider.Gecici("Segoe UI", 9.5f);
        TextRenderer.DrawText(g, _baslik, f1, new Rectangle(18, 8, Width - 28, 22), seritRenk, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, _mesaj, f2, new Rectangle(18, 34, Width - 28, Height - 40), TemaYonetici.C(DesignTokens.Ozgun(TokenRol.TextPrimary)), TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

// Bildirim gösterici: hangi iş parçacığından çağrılırsa çağrılsın güvenlidir; hata verirse sessiz geçer (bildirim asıl işi bozmasın).
internal static class Bildirim
{
    // Testte ve gözetimsiz çalışmada kapatmak için.
    public static bool Kapali { get; set; }

    // Bildirim gösterir.
    public static void Goster(string baslik, string mesaj, BildirimTuru tur = BildirimTuru.Bilgi, Action? tiklaninca = null)
    {
        // Kapalıysa veya hiç pencere yoksa (test/komut satırı) gösterme.
        if (Kapali || Application.OpenForms.Count == 0) return;
        try
        {
            // Arayüz iş parçacığında göster.
            var ana = Application.OpenForms[0]; if (ana == null || ana.IsDisposed) return;
            void Ac() { try { new BildirimKutusu(baslik, mesaj, tur, tiklaninca).Show(); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Bildirim.Goster"); } }
            if (ana.InvokeRequired) ana.BeginInvoke(Ac); else Ac();
        }
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "Bildirim.Goster"); }
    }
}
