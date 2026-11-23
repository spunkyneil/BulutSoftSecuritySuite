// Bu dosya açılan pencereler (iletişim kutuları ve ana pencere) için profesyonel açılış efektini içerir:
// yumuşak belirme (fade-in) + yukarı doğru hafif kayma, ve Windows 11'de yuvarlatılmış köşe.
// Kullanım: pencerenin kurucusunda PencereEfekti.Baglan(this); (kapatırken efekt için PencereEfekti.YumusakKapat(this) çağrılabilir).
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

internal static class PencereEfekti
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int nitelik, ref int deger, int boyut);

    // Pencereye açılış efektini ve yuvarlak köşeyi bağlar. Efekt, yüklü bir pencerede yalnızca bir kez, ilk gösterimde çalışır.
    public static void Baglan(Form f, int sureMs = 220, int kaymaPx = 14)
    {
        f.HandleCreated += (_, _) => { try { int yuvarlak = 2; DwmSetWindowAttribute(f.Handle, 33, ref yuvarlak, sizeof(int)); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "PencereEfekti.Baglan"); } };
        bool oynadi = false;
        f.Shown += (_, _) =>
        {
            if (oynadi || f.IsDisposed) return; oynadi = true;
            // Hareket azaltma tercihi varsa (erişilebilirlik) efekt oynatılmaz.
            if (!SystemInformation.UIEffectsEnabled) { f.Opacity = 1; return; }
            Oynat(f, 0f, 1f, sureMs, kaymaPx, null);
        };
        // Belirme başlayana kadar pencere görünmesin (titreme olmasın).
        f.Opacity = 0;
    }

    // Görülmüş pencereler (efekt her pencere için yalnızca bir kez oynar) ve efekt uygulanmayan pencere türleri
    // (açılış ekranı, ana pencere ve kendi animasyonu olan bildirim pencereleri).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, object> Gorulen = new();
    private static readonly HashSet<string> Haric = new() { "Form1", "AcilisFormu", "GirisFormu", "BildirimKutusu", "UsbBildirimFormu" };

    // Tema yöneticisi her açılan pencereyi bir kez bu yöntemle karşılar: görünür hale gelmiş pencerede belirme efekti çalışır.
    public static void Otomatik(Form f)
    {
        // Ana kabuğa GÖMÜLÜ (TopLevel=false) formlar sayfa içeriğidir: ekrana sığdırma ve belirme efekti uygulanmaz.
        if (f.IsDisposed || !f.IsHandleCreated || !f.Visible || !f.TopLevel) return;
        if (Gorulen.TryGetValue(f, out _)) return; Gorulen.Add(f, new object());
        // Her pencere (açılış/giriş/ana pencere/bildirimler hariç) bulunduğu ekranın çalışma alanına SIĞDIRILIR; içerik özgün boyutta kalır ve kaydırılabilir.
        if (!Haric.Contains(f.GetType().Name)) EkranaSigdir(f);
        if (Haric.Contains(f.GetType().Name) || f.Opacity < 1 || !SystemInformation.UIEffectsEnabled) return;
        try { int yuvarlak = 2; DwmSetWindowAttribute(f.Handle, 33, ref yuvarlak, sizeof(int)); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "PencereEfekti.Otomatik"); }
        f.Opacity = 0; Oynat(f, 0f, 1f, 200, 12, null);
    }

    // Pencere ekranın çalışma alanından (görev çubuğu hariç) büyükse küçültülür, ortalanır ve içerik KAYDIRILABİLİR yapılır (AutoScroll + özgün boyut kadar AutoScrollMinSize),
    // böylece 1366x768 ekranda ya da %150 ölçekli 1080p'de hiçbir alan ulaşılamaz kalmaz. alan: testte verilen sahte çalışma alanı.
    public static void EkranaSigdir(Form f, Rectangle? alan = null)
    {
        try
        {
            if (f.IsDisposed || f.WindowState != FormWindowState.Normal || f.TopLevel == false) return; var wa = alan ?? Screen.FromControl(f).WorkingArea;
            int w = Math.Min(f.Width, wa.Width), h = Math.Min(f.Height, wa.Height); if (w == f.Width && h == f.Height) return;
            var ic = f.ClientSize; if (f.AutoScrollMinSize.IsEmpty) f.AutoScrollMinSize = ic; f.AutoScroll = true;
            f.MinimumSize = new Size(Math.Min(f.MinimumSize.Width, w), Math.Min(f.MinimumSize.Height, h));
            f.Size = new Size(w, h); f.Location = new Point(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2);
        }
        catch (Exception hk3) { HataKayit.Sessiz(hk3, "PencereEfekti.EkranaSigdir"); }
    }

    // Kapanırken yumuşakça solar, sonra gerçekten kapanır. Çağırın: PencereEfekti.YumusakKapat(this);
    public static void YumusakKapat(Form f, int sureMs = 160)
    {
        if (!f.Visible || !SystemInformation.UIEffectsEnabled) { f.Close(); return; }
        var sonuc = f.DialogResult;
        Oynat(f, 1f, 0f, sureMs, 8, () => { if (!f.IsDisposed) { f.Opacity = 1; f.Close(); if (sonuc != DialogResult.None && !f.IsDisposed) f.DialogResult = sonuc; } });
    }

    // Opaklığı ve konumu zamanla değiştirir (yavaşlayarak durur).
    private static void Oynat(Form f, float bas, float son, int sure, int kayma, Action? bitince)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); int y0 = f.Top; bool giris = son > bas;
        var t = new System.Windows.Forms.Timer { Interval = 15 };
        t.Tick += (_, _) =>
        {
            if (f.IsDisposed) { t.Stop(); t.Dispose(); return; }
            float x = Math.Min(1f, sw.ElapsedMilliseconds / (float)sure), e = 1f - (float)Math.Pow(1f - x, 3);
            f.Opacity = bas + (son - bas) * e;
            if (giris) f.Top = y0 + (int)(kayma * (1f - e)); else f.Top = y0 + (int)(kayma * e);
            if (x >= 1f) { t.Stop(); t.Dispose(); if (giris) { f.Top = y0; f.Opacity = 1; } bitince?.Invoke(); }
        };
        if (giris) f.Top = y0 + kayma;
        t.Start();
    }
}
