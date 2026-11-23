// Hizmetin çalışma ayarları (komut satırından). Üretimde hiçbir bayrak verilmez: Guard klasörü ProgramData altındadır, kayıt kökü HKLM'dir.
using LabDenetim;
using Microsoft.Win32;

namespace BulutSoftGuardService;

internal sealed class GuardAyarlari
{
    public bool Gelistirme { get; init; }
    public string PipeAdi { get; init; } = KorumaProtokol.PipeAdi;
    public string Klasor { get; init; } = Path.Combine(BrandConfig.OrtakKlasor, "Guard");
    // Pipe'a yazma komutları göndermesine izin verilen tek uygulama dosyası (hizmetin yanındaki arayüz exe'si).
    public string IzinliUygulama { get; init; } = Path.Combine(AppContext.BaseDirectory, KorumaProtokol.AppExeAdi);
    // Fidye izleme için profil kökü (testte değiştirilebilir).
    public string ProfilKoku { get; init; } = @"C:\Users";

    public static GuardAyarlari Oku(string[] args)
    {
        string? Al(string ad) => args.FirstOrDefault(a => a.StartsWith("--" + ad + "=", StringComparison.OrdinalIgnoreCase))?[(ad.Length + 3)..];
        bool dev = args.Contains("--gelistirme", StringComparer.OrdinalIgnoreCase);
        var a = new GuardAyarlari { Gelistirme = dev, PipeAdi = Al("pipe") ?? KorumaProtokol.PipeAdi, Klasor = Al("kok") ?? new GuardAyarlari().Klasor, IzinliUygulama = Al("uygulama") ?? new GuardAyarlari().IzinliUygulama, ProfilKoku = Al("profiller") ?? @"C:\Users" };
        // Geliştirme kipinde kayıt kökü HKCU'dur (yönetici gerekmez); üretimde LabDenetim.IkinciKanal varsayılanı HKLM'dir.
        if (dev) { IkinciKanal.Kok = Registry.CurrentUser; IkinciKanal.AltAnahtar = @"SOFTWARE\BulutSoftTest\Muhur"; }
        return a;
    }
}
