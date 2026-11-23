// Bu dosya ana pencerenin boyutunu ve yerini hatırlar; birden fazla ekranlı (çoklu monitör) bilgisayarlarda pencerenin
// ekran dışında kalmasını ve ekrandan büyük açılmasını engeller. Örnek: 1920x1080 dizüstü ekranı + yan monitör.
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Kaydedilen pencere bilgisi.
internal sealed record PencereBilgisi(int X, int Y, int G, int Y2, bool Buyuk);

// Pencere konumu hesaplayan yardımcı (ekrandan bağımsız, test edilebilir).
internal static class PencereHesap
{
    // Kayıtlı pencere, verilen ekranlardan en az birine yeterince değiyor mu? (en az 200x100 piksel görünmeli)
    public static bool Gorunur(Rectangle pencere, IEnumerable<Rectangle> ekranlar) => ekranlar.Any(e => { var k = Rectangle.Intersect(e, pencere); return k.Width >= 200 && k.Height >= 100; });

    // Pencereyi ekranın çalışma alanına sığdırır (alandan büyükse küçültür, dışarı taşıyorsa içeri çeker).
    public static Rectangle Sigdir(Rectangle p, Rectangle alan)
    {
        // Boyut alandan büyük olmasın.
        int g = Math.Min(p.Width, alan.Width), y = Math.Min(p.Height, alan.Height);
        // Konum alanın içinde kalsın.
        int x = Math.Max(alan.Left, Math.Min(p.X, alan.Right - g)), t = Math.Max(alan.Top, Math.Min(p.Y, alan.Bottom - y));
        // Sonuç.
        return new Rectangle(x, t, g, y);
    }
}

// Ana pencerenin KONUM/BOYUT HAFIZASI (eskiden Form1'in partial dosyasındaydı): açılışta kayıtlı konumu görünür bir ekrana yerleştirir, kapanışta kaydeder.
internal static class PencereHafizasi
{
    private static string PencereYolu => Path.Combine(BrandConfig.RoamingKlasor, "pencere.json");

    // Açılış ve kapanış olaylarına bağlanır.
    public static void Bagla(Form f)
    {
        // Açılış: kayıtlı konumu uygula, yoksa içinde bulunulan ekranın ortasına koy. Kapanış: konumu kaydet.
        f.Load += (_, _) => Yerlestir(f);
        f.FormClosing += (_, _) => Kaydet(f);
    }

    // Pencereyi doğru ekrana ve doğru boyuta yerleştirir.
    internal static void Yerlestir(Form f)
    {
        try
        {
            var alanlar = Screen.AllScreens.Select(s => s.WorkingArea).ToList();
            PencereBilgisi? b = null; if (File.Exists(PencereYolu)) b = JsonSerializer.Deserialize<PencereBilgisi>(File.ReadAllText(PencereYolu));
            var kayitli = b == null ? Rectangle.Empty : new Rectangle(b.X, b.Y, b.G, b.Y2);
            var alan = b != null && PencereHesap.Gorunur(kayitli, alanlar) ? alanlar.First(a => Rectangle.Intersect(a, kayitli).Width > 0) : Screen.PrimaryScreen?.WorkingArea ?? alanlar[0];
            // En küçük boyut ekrandan büyük olmasın (yüksek ekran ölçeklemesinde bile pencere sığsın).
            f.MinimumSize = new Size(Math.Min(f.MinimumSize.Width, alan.Width), Math.Min(f.MinimumSize.Height, alan.Height));
            var istenen = b != null ? kayitli : new Rectangle(0, 0, f.Width, f.Height);
            if (b == null) { istenen.X = alan.Left + (alan.Width - istenen.Width) / 2; istenen.Y = alan.Top + (alan.Height - istenen.Height) / 2; }
            var son = PencereHesap.Sigdir(istenen, alan); f.StartPosition = FormStartPosition.Manual; f.Bounds = son;
            if (b is { Buyuk: true }) f.WindowState = FormWindowState.Maximized;
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "PencereHafizasi.Yerlestir"); }
    }

    // Pencere konumunu kaydeder.
    internal static void Kaydet(Form f)
    {
        try
        {
            if (f.WindowState == FormWindowState.Minimized) return;   // küçültülmüşse konum anlamsızdır
            var r = f.WindowState == FormWindowState.Maximized ? f.RestoreBounds : f.Bounds;
            if (!PencereHesap.Gorunur(r, Screen.AllScreens.Select(s => s.WorkingArea))) return;   // ekran dışına taşmış (ör. test) pencereyi kaydetme
            Depo.YazJson(PencereYolu, new PencereBilgisi(r.X, r.Y, r.Width, r.Height, f.WindowState == FormWindowState.Maximized));
        }
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "PencereHafizasi.Kaydet", true); }
    }
}
