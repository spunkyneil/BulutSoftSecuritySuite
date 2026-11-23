// Bu dosya WinForms OLAY İŞLEYİCİLERİNDEKİ async çağrıların TEK GÜVENLİ SARMALAYICISIDIR. "async void" yöntemler/lambdalar istisnayı çağırana iletemez; yakalanmazsa süreç düşebilir
// veya hata sessizce kaybolur. Bunun yerine olay işleyicisi "senkron" kalır ve işi bu sarmalayıcıya verir: istisna yakalanır, HataKayit'a yazılır ve Program.BeklenmeyenHata
// ile (olay günlüğü + çökme dosyası + hız sınırlı kullanıcı mesajı) tek merkezden bildirilir; iptal (OperationCanceledException) beklenen bir durumdur ve yalnızca sayılır.
// Çözümde "async void" kalmaması bir testle denetlenir (HijyenTestleri).

// Programımızın adı.
namespace LabDenetim;

// Olay işleyicilerinde async işi güvenle başlatır.
internal static class GuvenliCalistir
{
    // Beklenmeyen hatanın bildirileceği yer (varsayılan: Program.BeklenmeyenHata → olay günlüğü + çökme dosyası + hız sınırlı mesaj). Sınamada değiştirilebilir.
    internal static Action<string, Exception> HataBildirici { get; set; } = (baglam, ex) => Program.BeklenmeyenHata(baglam, ex, true);

    // İşi başlatır ve beklemez (olay işleyicisi senkron döner). Hata dışarı sızmaz.
    public static void Baslat(Func<Task> is_, string baglam) => _ = CalistirAsync(is_, baglam);

    // İşi çalıştırır; iptal ve hatayı yakalar (beklemek isteyenler için: örn. testler).
    public static async Task CalistirAsync(Func<Task> is_, string baglam)
    {
        try { await is_().ConfigureAwait(true); }
        catch (OperationCanceledException hk1) { HataKayit.Sessiz(hk1, baglam + ".iptal"); }
        catch (Exception hk2) { HataKayit.Sessiz(hk2, baglam); HataBildirici(baglam, hk2); }
    }
}
