// Bu dosya Windows'un beyaz kaydırma çubuklarını koyu temaya çevirir.
// Windows 10/11'in kendi "koyu Gezgin" görünümünü (DarkMode_Explorer) kutulara uygular; yeni bir çizim yapmaz, bu yüzden hızlıdır.
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// Koyu kaydırma çubuğu yardımcısı.
internal static class KoyuTema
{
    // Windows'a "bu pencereye şu görsel temayı uygula" diyen sistem çağrısı.
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr pencere, string uygulamaAdi, string? ekAd);

    // Verilen kutunun kaydırma çubuklarını koyu yapar (kutunun ekran karşılığı hazır olunca).
    public static void Uygula(Control kutu)
    {
        // Ekran karşılığı zaten varsa hemen uygula.
        if (kutu.IsHandleCreated) SetWindowTheme(kutu.Handle, "DarkMode_Explorer", null);
        // Yoksa oluşunca uygula.
        else kutu.HandleCreated += (_, _) => SetWindowTheme(kutu.Handle, "DarkMode_Explorer", null);
    }

    // Tabloyu ve içindeki (sonradan eklenen) kaydırma çubuklarını koyu yapar.
    public static void UygulaTablo(DataGridView tablo)
    {
        // Tablonun kendisi.
        Uygula(tablo);
        // Zaten var olan çubuklar.
        foreach (Control alt in tablo.Controls) Uygula(alt);
        // Sonradan eklenecek çubuklar (tablo büyüyüp kaydırma gerekince oluşur).
        tablo.ControlAdded += (_, e) => { if (e.Control != null) Uygula(e.Control); };
    }
}
