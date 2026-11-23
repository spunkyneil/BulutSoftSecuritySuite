// Bu dosya sonuç tablosuna iki kolaylık ekler:
//  1) Tablonun üstündeki ARAMA kutusu: yazdıkça tabloda yalnızca eşleşen satırlar kalır.
//  2) "Kabul edilen risk": bilerek açık bıraktığınız bir kapıyı/ayarı işaretlersiniz; sonraki taramalarda o sonuç kırmızı/sarı sayılmaz,
//     güvenlik puanınızı düşürmez ve her seferinde sizi rahatsız etmez. İşaretler %AppData%\BulutSoft\kabul_edilen.json dosyasında durur.
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Kabul edilen risklerin kalıcı listesi.
internal static class KabulDeposu
{
    // Dosyanın yolu (testte değiştirilebilir).
    public static string Yol { get; set; } = Path.Combine(BrandConfig.RoamingKlasor, "kabul_edilen.json");
    // Bellekteki liste (dosyadan bir kez okunur).
    private static HashSet<string>? _liste;
    // Kabul edilen sonuçların başına eklenen etiket.
    public const string Etiket = "[KABUL EDİLEN RİSK] ";

    // Bir sonucun anahtarı: modül + başlık.
    public static string Anahtar(Bulgu b) => b.Modul + "|" + b.Baslik;

    // Listeyi okur (yoksa boş liste).
    private static HashSet<string> Liste()
    {
        // Zaten okunduysa onu kullan.
        if (_liste != null) return _liste;
        // Okuma bozulabilir.
        try { _liste = File.Exists(Yol) ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(Yol)) ?? new() : new(); }
        // Bozuk dosya: boş başla.
        catch { _liste = new(); }
        // Listeyi ver.
        return _liste;
    }

    // Bu sonuç kabul edilmiş mi?
    public static bool Kabul(Bulgu b) => Liste().Contains(Anahtar(b));

    // Bir sonucu kabul edilen listeye ekler veya çıkarır; yeni durumu verir.
    public static bool Degistir(Bulgu b)
    {
        // Anahtar.
        string k = Anahtar(b); var l = Liste();
        // Varsa çıkar, yoksa ekle.
        bool simdiKabul = !l.Remove(k); if (simdiKabul) l.Add(k);
        // Dosyaya yaz.
        Directory.CreateDirectory(Path.GetDirectoryName(Yol)!);
        // Yazım.
        Depo.YazJson(Yol, l.OrderBy(x => x).ToList(), new JsonSerializerOptions { WriteIndented = true });
        // Yeni durum.
        return simdiKabul;
    }

    // Kabul edilmişse sonucu "bilgi" seviyesine indirip etiketler; değilse aynen verir.
    public static Bulgu Uygula(Bulgu b) =>
        b.Onem != Onem.Bilgi && !b.Atlandi && b.Modul is not ("Zincir" or "Referans") && Kabul(b)
            ? b with { Onem = Onem.Bilgi, Detay = Etiket + b.Detay }
            : b;
}
