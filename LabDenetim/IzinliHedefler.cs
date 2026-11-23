// Bu dosya kullanıcının "izinli hedef" listesini kalıcı tutar: müşterinin alan adları ve (özel ağ dışındaki) adresler, yalnızca kullanıcı "bu hedef için yazılı yetkim var" dediğinde eklenir.
// Liste HedefDogrulama.TestHedefleri'ne yüklenir; komut şablonları bu listenin dışındaki alan adı/genel IP hedeflerini REDDEDER. Dosya: %AppData%\BulutSoft\izinli_hedefler.txt (satır başına bir girdi, '#' yorum).
// Program kendiliğinden hiçbir hedef eklemez; ekleme yalnızca arayüzdeki onaydan sonra yapılır ve olay günlüğüne yazılır.
// Not: yükleme ARAYÜZDEN (ilk kullanımda) yapılır, KomutCalistirici içinden değil: testler gerçek kullanıcı dosyasından etkilenmesin.
namespace LabDenetim;

// Kalıcı izinli hedefler.
internal static class IzinliHedefler
{
    // Dosya yolu.
    public static string Yol => BulutKlasor.Dosya("izinli_hedefler.txt");
    private static bool _yuklendi; private static readonly object Kilit = new();

    // Kayıtlı hedefleri (ilk çağrıda bir kez) HedefDogrulama'ya yükler; geçersiz satırlar yüklenmez ve günlüğe yazılır.
    public static void Hazirla()
    {
        // Bir kez.
        lock (Kilit) { if (_yuklendi) return; _yuklendi = true; }
        var (_, red) = HedefDogrulama.DosyadanYukle(Yol);
        if (red.Count > 0) ServisBaglami.OlayYazici.Ekle("kali", "Uyarı", $"KALİ İZİNLİ HEDEF UYARI -> {red.Count} geçersiz satır yüklenmedi (izinli_hedefler.txt değiştirilmiş olabilir).");
    }

    // Testler için: bir sonraki Hazirla yeniden yüklesin.
    internal static void Sifirla() { lock (Kilit) _yuklendi = false; }

    // Hedefi listeye ekler (çalışma anında) ve dosyaya kalıcı yazar. Geçersizse ArgumentException. Zaten varsa yinelenmez.
    public static void Ekle(string hedef)
    {
        // Geçerli mi? (TestHedefiEkle doğrular ve normalleştirir.)
        hedef = (hedef ?? "").Trim(); string norm = hedef.ToLowerInvariant();
        if (!HedefDogrulama.TestHedefleri().Contains(norm, StringComparer.OrdinalIgnoreCase)) HedefDogrulama.TestHedefiEkle(hedef);
        // Dosyaya ekle (atomik yazım).
        lock (Kilit)
        {
            var satirlar = File.Exists(Yol) ? File.ReadAllLines(Yol).Select(x => x.TrimEnd()).Where(x => x.Length > 0).ToList() : new List<string>();
            if (satirlar.Any(x => x.Equals(norm, StringComparison.OrdinalIgnoreCase))) return;
            satirlar.Add(norm); Depo.Yaz(Yol, string.Join("\r\n", satirlar) + "\r\n");
        }
        ServisBaglami.OlayYazici.Ekle("kali", "Uyarı", $"KALİ İZİNLİ HEDEF -> eklendi: {norm} (kullanıcı, hedef için yazılı yetkisi olduğunu onayladı).");
    }
}
