// Bu dosya KULLANICI GÖRSEL/GİZLİLİK TERCİHLERİNİ saklar (gorsel_ayarlar.json): küredeki "Görsel Demo Yayları" (varsayılan KAPALI) ve Claude'a giden metnin maskelenmesi (varsayılan AÇIK).
// Değişince Degisti olayı tetiklenir; küre panelleri ve ayar düğmeleri olayla güncellenir (tarama/zamanlayıcı yok). Dosya Depo ile atomik yazılır; bozuk/eksik dosya varsayılanlara döner.
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Görsel ve gizlilik tercihleri.
internal static class GorselAyarlar
{
    // Ayar dosyasının içeriği.
    internal sealed record Veri(bool DemoYaylar = false, bool ClaudeMaskele = true);

    private static readonly object Kilit = new();
    private static Veri? _veri;

    // Ayar dosyası (veri klasöründe).
    public static string Yol => BulutKlasor.Dosya("gorsel_ayarlar.json");

    // Ayar değişti.
    public static event Action? Degisti;

    // Küredeki örnek (demo) yaylar HİÇBİR ZAMAN gösterilmez: gerçek trafik olmayan görsel kullanıcıyı yanıltabilir; küre yalnızca gerçek koruma durumunu ve gerçek olay uyarısını yansıtır. (Eski ayar dosyasındaki değer yok sayılır.)
    public static bool DemoYaylar => false;

    // Claude'a gönderilecek metinde özel IP son okteti, kullanıcı adı ve yerel yollar maskelensin mi? Varsayılan: evet.
    public static bool ClaudeMaskele { get => Oku().ClaudeMaskele; set => Yaz(Oku() with { ClaudeMaskele = value }); }

    // Önbelleği atar (testler ve veri klasörü değişince).
    public static void Sifirla() { lock (Kilit) _veri = null; }

    private static Veri Oku()
    {
        lock (Kilit)
        {
            if (_veri != null) return _veri;
            try { _veri = File.Exists(Yol) ? JsonSerializer.Deserialize<Veri>(File.ReadAllText(Yol)) ?? new Veri() : new Veri(); }
            catch (Exception hk1) { HataKayit.Sessiz(hk1, "GorselAyarlar.Oku"); _veri = new Veri(); }
            return _veri;
        }
    }

    private static void Yaz(Veri yeni)
    {
        lock (Kilit)
        {
            if (yeni == _veri) return;
            _veri = yeni;
            try { Depo.YazJson(Yol, yeni); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "GorselAyarlar.Yaz"); }
        }
        Degisti?.Invoke();
    }
}
