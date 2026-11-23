// Bu dosya beyaz listedeki komut ŞABLONLARINI sensörde (SSH) çalıştıran ortak çalıştırıcıdır. Araç-agnostiktir; mevcut SensorTaramaYurutucu.CalistirAsync üstüne kuruludur.
// Akış: şablon beyaz listede mi → sensör profili → HEDEF doğrulama → parametre doğrulama → araç kurulu mu (değilse kullanıcı onayıyla kurulum) → sudo denemesi → ajan çalıştırma → sonuç.
// Güvenlik: hiçbir adımda serbest komut metni birleştirilmez. Ajana yalnızca Base64 JSON gider (SensorAg.Komut). Kabuğa giden tek metinler ("command -v", "sudo -n", "apt-get install")
// KOMUT SABİTLERİ + şablon kaydında sıkı desenle doğrulanmış araç/paket adlarıdır.
// AJAN SÖZLEŞMESİ (ajan: KomutAjani.cs; tablo: kali_araclari.json → KomutKaydi.cs): istek {"mod":"komut","sablon":ad,"hedef":h,"arg":[değerler],"zaman":sn,"izinli":[...]}.
//   Ajan her çıktı satırı için {"t":"cikti","akis":"stdout"|"stderr","s":"satır"} yollar, bitişte {"t":"done","kod":N,"zamanasimi":bool}, hatada {"t":"error","m":"..."}.
//   Ajan hedefi ve değerleri KENDİ tarafında da yeniden doğrular; gerçek argüman listesini, "sudo -n" önekini ve süreyi kendi tablosundan alır (istek yalnızca süreyi kısaltabilir).
using System.Diagnostics;
using System.Text;
using System.Text.Json;


// Programımızın adı.
namespace LabDenetim;

// Şablon çalıştırıcı.
internal static class KomutCalistirici
{
    // Ajan kipinin adı.
    public const string AjanModu = "komut";
    // Sonuç kodları: -2 = çalıştırılmadı (atlandı), 124 = zaman aşımı (timeout komutuyla aynı).
    public const int AtlandiKodu = -2, ZamanAsimiKodu = 124;
    // Her akıştan (stdout/stderr) en çok bu kadar karakter tutulur.
    public const int EnFazlaCiktiKarakter = 4_000_000;
    // Ajan kendi zaman aşımını uygulayıp durumu bildirebilsin diye ssh sınırı şablon süresinden bu kadar saniye uzundur (testte değiştirilebilir).
    public static int AjanPayiSn { get; set; } = 10;
    // Ön kontrol ve paket kurulumu süreleri.
    private const int OnKontrolSn = 20, KurulumSn = 900;

    // Olay günlüğüne "kali" türünde yazar (günlük asla programı bozmaz; metin temizlenir).
    private static void Gunluk(string seviye, string mesaj) => ServisBaglami.OlayYazici.Ekle("kali", seviye, SensorCiktiAyristirici.Temizle(mesaj, 600));
    // Dışarıdan gelen uzun metni kısaltıp temizler.
    private static string Kisa(string s, int en) => SensorCiktiAyristirici.Temizle(s ?? "", en);

    // Şablon çağrısının özeti (gerçek argüman listesi sensör ajanında sabittir).
    private static string Ozet(KomutSablonu s, string hedef, IReadOnlyList<string> arg) => $"{s.Ad} {hedef}" + (arg.Count > 0 ? " " + string.Join(' ', arg) : "");

    // "Çalıştırılamadı" bulgusu (Bulgu.Atlandi bu başlıkla tanınır: "güvenli" sayılmaz, yalnızca bakılamamıştır).
    internal static Bulgu AtlandiBulgusu(string modul, string detay, string oneri) => new(modul, "Bu kontrol çalıştırılamadı", Onem.Bilgi, detay, oneri);

    // Ajanın satırlarını toplayan yardımcı: stdout ve stderr AYRI AYRI biriktirilir.
    private sealed class Toplayici
    {
        public readonly StringBuilder Stdout = new(), Stderr = new(); public int? Kod; public string? Hata; public bool ZamanAsimi;
        // Sınırlı ekleme.
        private static void Ekle(StringBuilder sb, string s)
        {
            if (sb.Length >= EnFazlaCiktiKarakter) return;
            int kalan = EnFazlaCiktiKarakter - sb.Length;
            if (s.Length + 1 > kalan) { sb.Append(s.AsSpan(0, Math.Max(0, kalan))); sb.Append("\n… (çıktı kısaltıldı)\n"); } else sb.Append(s).Append('\n');
        }
        // Tek satırı işler: JSON olmayan satırlar atlanır (sensörden gelen veri güvenilmez).
        public void Isle(string satir)
        {
            if (satir.Length == 0 || satir[0] != '{') return;
            try
            {
                using var d = JsonDocument.Parse(satir); var k = d.RootElement;
                if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("t", out var t) || t.ValueKind != JsonValueKind.String) return;
                switch (t.GetString())
                {
                    case "cikti":
                        string akis = k.TryGetProperty("akis", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() ?? "" : "";
                        string s = k.TryGetProperty("s", out var sv) && sv.ValueKind == JsonValueKind.String ? sv.GetString() ?? "" : "";
                        Ekle(akis == "stderr" ? Stderr : Stdout, s); break;
                    case "done":
                        if (k.TryGetProperty("kod", out var kv) && kv.ValueKind == JsonValueKind.Number && kv.TryGetInt32(out int kod)) Kod = kod;
                        ZamanAsimi = k.TryGetProperty("zamanasimi", out var za) && za.ValueKind == JsonValueKind.True; break;
                    case "error":
                        Hata = k.TryGetProperty("m", out var m) && m.ValueKind == JsonValueKind.String ? SensorCiktiAyristirici.Temizle(m.GetString() ?? "", 300) : "bilinmeyen hata"; break;
                }
            }
            catch (JsonException hk1) { HataKayit.Sessiz(hk1, "KomutCalistirici.Isle"); }
        }
    }

    // Araç sensörde kurulu mu? ("command -v": 0 = var; 1 veya 127 = yok; başka kod = bağlantı/ön kontrol sorunu.)
    private static async Task<bool> KuruluMuAsync(SensorProfili p, KomutSablonu s, CancellationToken ct)
    {
        string komut = "command -v " + s.AracAdi;
        SensorTaramaYurutucu.Denetle(p, "komut-onkontrol: " + komut);
        var (k, h) = await SensorTaramaYurutucu.CalistirAsync(p, komut, null, _ => { }, ct, OnKontrolSn, 4096);
        ct.ThrowIfCancellationRequested();
        if (k == 0) return true; if (k is 1 or 127) return false;
        throw new InvalidOperationException($"Sensör ({p.Ad}) ön kontrolü çalışmadı: " + Kisa(SensorCiktiAyristirici.HataAciklama(h), 300));
    }

    // Parolasız sudo var mı? ("sudo -n": parola istenirse anlaşılır hata verir; parola programa ASLA verilmez.)
    private static async Task SudoDenetleAsync(SensorProfili p, CancellationToken ct)
    {
        SensorTaramaYurutucu.Denetle(p, "komut-onkontrol: sudo -n true");
        var (k, h) = await SensorTaramaYurutucu.CalistirAsync(p, "sudo -n true", null, _ => { }, ct, OnKontrolSn, 4096);
        ct.ThrowIfCancellationRequested();
        if (k == 0) return;
        if (h.Contains("password", StringComparison.OrdinalIgnoreCase) || h.Contains("parola", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Sensör kullanıcısı '{p.Kullanici}' için PAROLASIZ sudo gerekiyor ('sudo -n' parola istedi). Sensörde ilgili komut için NOPASSWD yetkisi tanımlayın (örn. /etc/sudoers.d); parola programa verilmez.");
        throw new InvalidOperationException("Sensörde sudo kullanılamadı: " + Kisa(SensorCiktiAyristirici.HataAciklama(h), 300));
    }

    // Paketi kurar (YALNIZCA kullanıcı onayından sonra çağrılır) ve araç artık var mı diye yeniden bakar.
    private static async Task KurAsync(SensorProfili p, KomutSablonu s, CancellationToken ct)
    {
        string komut = "sudo -n apt-get install -y " + s.PaketAdi;
        Gunluk("Bilgi", $"KALİ KOMUT -> paket kuruluyor: {s.PaketAdi} (sensör {p.Ad}, kullanıcı onayıyla)");
        SensorTaramaYurutucu.Denetle(p, "komut-kurulum: " + komut);
        var (k, h) = await SensorTaramaYurutucu.CalistirAsync(p, komut, null, _ => { }, ct, KurulumSn, 4096);
        ct.ThrowIfCancellationRequested();
        if (k != 0) throw new InvalidOperationException($"'{s.PaketAdi}' paketi sensörde kurulamadı (çıkış kodu {k}): " + Kisa(SensorCiktiAyristirici.HataAciklama(h), 300));
        if (!await KuruluMuAsync(p, s, ct)) throw new InvalidOperationException($"'{s.PaketAdi}' paketi kuruldu görünüyor ama '{s.AracAdi}' hâlâ bulunamadı.");
        Gunluk("Bilgi", $"KALİ KOMUT -> paket kuruldu: {s.PaketAdi}");
    }

    // Çıktıyı ayrıştırır; ayrıştırıcı bulunamazsa varsayılan kullanılır, hata verirse "çalıştırılamadı" bulgusu eklenir.
    private static List<Bulgu> Ayristir(KomutSablonu s, string hedef, string stdout)
    {
        var ay = AyiriciDeposu.GetirVeyaVarsayilan(s.AyrastiriciAdi, out bool bulundu);
        if (!bulundu) Gunluk("Uyarı", $"KALİ KOMUT -> '{s.AyrastiriciAdi}' ayrıştırıcısı bulunamadı; varsayılan ({AyiriciDeposu.Varsayilan}) kullanıldı ({s.Ad}).");
        try { return ay.Ayristir(s.Ad, hedef, stdout); }
        catch (Exception ex) { Gunluk("Uyarı", $"KALİ KOMUT -> çıktı ayrıştırılamadı ({s.Ad}): {ex.GetType().Name}"); return new() { AtlandiBulgusu(s.Ad, "Araç çıktısı ayrıştırılamadı; ham çıktı dosyasına bakın.", "Ham çıktıyı elle inceleyin.") }; }
    }

    // Çalıştırmadan ÖNCEKİ doğrulamalar: 1) şablon beyaz listede mi (depoda yoksa gömülü tablo kaydedilir: tekrarlı çağrı zararsız, tablo bozuksa hiçbiri kaydedilmez) 2) sensör profili
    // 3) hedef (türüne uygun ve izinli) 4) parametreler. Reddedilirse InvalidOperationException atar; logla=true ise red sebebi olay günlüğüne de yazılır.
    private static (KomutSablonu Sablon, IReadOnlyList<string> Arg) Dogrula(SensorProfili p, string sablonAdi, string hedef, IReadOnlyDictionary<string, string>? girdiler, bool logla)
    {
        // Günlük (yalnızca çalıştırmada).
        void Yaz(string m) { if (logla) Gunluk("Uyarı", m); }
        var sablon = KomutSablonuDeposu.Getir(sablonAdi);
        if (sablon == null) { try { KomutKaydi.Hazirla(); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { Yaz("KALİ KOMUT -> araç tablosu yüklenemedi: " + ex.Message); } sablon = KomutSablonuDeposu.Getir(sablonAdi); }
        if (sablon == null) { Yaz($"KALİ KOMUT REDDEDİLDİ -> '{sablonAdi}' adlı şablon beyaz listede yok."); throw new InvalidOperationException("Bu komut şablonu beyaz listede tanımlı değil; serbest komut çalıştırılamaz."); }
        var ph = SensorGuvenlik.ProfilHatasi(p);
        if (ph != null) { Yaz($"KALİ KOMUT REDDEDİLDİ -> {sablon.Ad}: sensör profili geçersiz: {ph}"); throw new InvalidOperationException(ph); }
        var he = HedefHatasiBul(sablon, hedef);
        if (he != null) { Yaz($"KALİ KOMUT REDDEDİLDİ -> {sablon.Ad} ({sablon.Kategori}): {he}"); throw new InvalidOperationException(he); }
        try { return (sablon, KomutSablonuDeposu.Uyarilastir(sablon, girdiler)); }
        catch (InvalidOperationException ex) { Yaz("KALİ KOMUT REDDEDİLDİ -> " + ex.Message); throw; }
    }

    // Şablonun hedef kuralı: "yok" ise hedef boş olmalı; değilse temel doğrulama (özel ağ / izinli liste) ve hedef türü.
    internal static string? HedefHatasiBul(KomutSablonu sablon, string hedef) => sablon.HedefTuru == "yok" ? HedefDogrulama.TurHatasi("yok", hedef) : HedefDogrulama.HedefHatasi(sablon.Kategori, hedef) ?? HedefDogrulama.TurHatasi(sablon.HedefTuru, hedef);

    // Çalışacak TAM komutu (sudo sarmalı dahil) sensördeki ajana sorarak verir; hiçbir şey çalıştırılmaz. Arayüz onay penceresinde gösterir (sensörde gerçekten çalışacak argv).
    public static async Task<string> OnizleAsync(SensorProfili p, string sablonAdi, string hedef, IReadOnlyDictionary<string, string>? girdiler, CancellationToken ct)
    {
        // Aynı doğrulamalar (günlüğe yazmadan).
        var (sablon, arg) = Dogrula(p, sablonAdi, hedef, girdiler, false);
        var istek = new { mod = AjanModu, sablon = sablon.Ad, hedef, arg, zaman = sablon.ZamanAsimiSn, izinli = HedefDogrulama.TestHedefleri() };
        string uzak = "python3 - onizle " + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(istek))); List<string>? argv = null; string? ajanHata = null;
        var (_, sshHata) = await SensorTaramaYurutucu.CalistirAsync(p, uzak, KomutAjani.Kaynak(KomutKaydi.AjanTablosuJson), satir =>
        {
            if (satir.Length == 0 || satir[0] != '{') return;
            try
            {
                using var d = JsonDocument.Parse(satir); var k = d.RootElement; if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("t", out var t) || t.ValueKind != JsonValueKind.String) return;
                if (t.GetString() == "onizle" && k.TryGetProperty("argv", out var a) && a.ValueKind == JsonValueKind.Array) argv = a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToList();
                else if (t.GetString() == "error") ajanHata = k.TryGetProperty("m", out var m) && m.ValueKind == JsonValueKind.String ? SensorCiktiAyristirici.Temizle(m.GetString() ?? "", 300) : "bilinmeyen hata";
            }
            catch (JsonException hk2) { HataKayit.Sessiz(hk2, "KomutCalistirici.OnizleAsync"); }
        }, ct, 30, SensorAg.EnUzunSatir);
        ct.ThrowIfCancellationRequested();
        if (ajanHata != null) throw new InvalidOperationException($"Sensör ({p.Ad}) komutu doğrulamadı: {ajanHata}");
        if (argv == null || argv.Count == 0) throw new InvalidOperationException($"Sensör ({p.Ad}) komut önizlemesi alınamadı: " + Kisa(SensorCiktiAyristirici.HataAciklama(sshHata), 300));
        // Gösterim için boşluk/özel karakter içeren öğeler tırnaklanır (yalnızca ekranda; sensörde argv listesi doğrudan çalışır).
        return string.Join(' ', argv.Select(x => x.Length > 0 && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/' or ':' or '=' or ',' or '@' or '+') ? x : "'" + x.Replace("'", "'\\''") + "'"));
    }

    // Şablonu sensörde çalıştırır. Önkoşullar (beyaz liste, profil, hedef, parametre, sudo, bağlantı) sağlanmazsa InvalidOperationException atar ve hiçbir komut çalışmaz.
    // Araç kurulu değilse onayCallback(soru) çağrılır: true → kurulur ve çalışır; false/null → çalıştırılmaz (sonuç "atlandı", CikisKodu = -2).
    // Aracın kendi çıkış kodu sonuçta döner (sıfır dışı kod hata sayılmaz: bazı araçlar bulgu varsa sıfır dışı döner); çıktı da üretmediyse "çalıştırılamadı" bulgusu eklenir.
    public static async Task<KomutSonucu> CalistirAsync(SensorProfili p, string sablonAdi, string hedef, IReadOnlyDictionary<string, string>? girdiler, Func<string, bool>? onayCallback, CancellationToken ct)
    {
        // 1-4) Beyaz liste, sensör profili, hedef ve parametre doğrulaması (reddedilirse günlüğe yazılır ve hiçbir şey çalışmaz).
        var (sablon, arg) = Dogrula(p, sablonAdi, hedef, girdiler, true);
        // Başlangıç.
        DateTime baslangic = DateTime.Now; string ozet = Ozet(sablon, hedef, arg); var toplamSw = Stopwatch.StartNew();
        Gunluk("Bilgi", $"KALİ KOMUT BAŞLADI -> {sablon.Ad} ({sablon.Kategori}) hedef={hedef}, sensör={p.Ad}, tahmini {sablon.TahminiSaniye} sn, zaman aşımı {sablon.ZamanAsimiSn} sn{(sablon.SudoGerekli ? ", sudo" : "")}");
        try
        {
            // 5) Araç kurulu mu? Değilse yalnızca kullanıcı onaylarsa kurulur.
            bool sudoTamam = false;
            if (!await KuruluMuAsync(p, sablon, ct))
            {
                bool onay = false; try { onay = onayCallback?.Invoke($"'{sablon.AracAdi}' sensörde ({p.Ad}) kurulu değil. 'sudo apt-get install -y {sablon.PaketAdi}' ile kurulsun mu?") == true; } catch { onay = false; }
                if (!onay)
                {
                    Gunluk("Bilgi", $"KALİ KOMUT ATLANDI -> {sablon.Ad}: '{sablon.AracAdi}' kurulu değil ve kurulum onaylanmadı.");
                    return new KomutSonucu(sablon.Ad, hedef, ozet, baslangic, toplamSw.Elapsed, AtlandiKodu, "", new() { AtlandiBulgusu(sablon.Ad, $"'{sablon.AracAdi}' sensörde kurulu değil ve kurulum onaylanmadı.", $"Sensörde 'sudo apt-get install {sablon.PaketAdi}' çalıştırın ya da kurulumu onaylayın.") });
                }
                await SudoDenetleAsync(p, ct); sudoTamam = true; await KurAsync(p, sablon, ct);
            }
            // 6) Sudo gereken şablonlarda parolasız sudo denenir.
            if (sablon.SudoGerekli && !sudoTamam) await SudoDenetleAsync(p, ct);

            // 7) Ajanı çalıştır: stdout ve stderr ayrı toplanır; zaman aşımı ŞABLONDAN gelir (SensorAg.ToplaAsync'in 180 sn sabiti kullanılmaz).
            var t = new Toplayici(); int toplamSn = sablon.ZamanAsimiSn + Math.Max(0, AjanPayiSn);
            // İstek: sudo ve gerçek argüman listesi GÖNDERİLMEZ (ajan bunları kendi tablosundan alır); süre yalnızca kısaltabilir; izinli hedef listesi ajanın hedef doğrulaması içindir.
            var istek = new { mod = AjanModu, sablon = sablon.Ad, hedef, arg, zaman = sablon.ZamanAsimiSn, izinli = HedefDogrulama.TestHedefleri() };
            var sw = Stopwatch.StartNew();
            var (_, sshHata) = await SensorTaramaYurutucu.CalistirAsync(p, SensorAg.Komut(istek), KomutAjani.Kaynak(KomutKaydi.AjanTablosuJson), t.Isle, ct, toplamSn, SensorAg.EnUzunSatir);
            sw.Stop(); ct.ThrowIfCancellationRequested();
            // Ajan hata bildirdiyse.
            if (t.Hata != null) throw new InvalidOperationException($"Sensör ({p.Ad}) komutu başarısız: {t.Hata}");
            // Çıkış kodu: ajan "done" gönderdiyse onu kullan; göndermediyse zaman aşımı mı bağlantı sorunu mu ayırt et.
            int kod; bool zamanAsimi = false;
            if (t.Kod is int bk) { kod = bk; zamanAsimi = t.ZamanAsimi || bk == ZamanAsimiKodu; }
            else if (sshHata.Contains("Zaman aşımı", StringComparison.OrdinalIgnoreCase) || sw.Elapsed.TotalSeconds >= toplamSn * 0.9) { kod = ZamanAsimiKodu; zamanAsimi = true; }
            else throw new InvalidOperationException($"Sensör ({p.Ad}) komutu çalışmadı: " + Kisa(SensorCiktiAyristirici.HataAciklama(sshHata), 300));

            // 8) Çıktı: denetim karakterleri temizlenir; ham çıktı dosyaya yazılır; stdout ayrıştırılır.
            string stdout = SensorArac.Temizle(t.Stdout.ToString(), EnFazlaCiktiKarakter + 64), stderr = SensorArac.Temizle(t.Stderr.ToString(), EnFazlaCiktiKarakter + 64);
            var bulgular = Ayristir(sablon, hedef, stdout);
            if (zamanAsimi) bulgular.Add(AtlandiBulgusu(sablon.Ad, $"Komut {sablon.ZamanAsimiSn} saniye içinde bitmedi ve durduruldu; sonuç eksik olabilir.", "Süreyi artırın ya da hedefi/kapsamı daraltın."));
            else if (kod != 0 && stdout.Trim().Length == 0) bulgular.Add(AtlandiBulgusu(sablon.Ad, $"Araç {kod} çıkış koduyla bitti ve çıktı üretmedi." + (stderr.Trim().Length > 0 ? " Hata çıktısı: " + Kisa(stderr, 300) : ""), "Ham çıktı dosyasına ve sensör günlüğüne bakın."));
            var sure = toplamSw.Elapsed;
            var ic = new StringBuilder();
            ic.Append($"# şablon: {sablon.Ad} | kategori: {sablon.Kategori} | hedef: {hedef} | sensör: {p.Ad}\n# başlangıç: {baslangic:o} | süre: {sure.TotalSeconds:F1} sn | çıkış kodu: {kod}{(zamanAsimi ? " (zaman aşımı)" : "")}\n--- stdout ---\n").Append(stdout);
            if (stderr.Trim().Length > 0) ic.Append("\n--- stderr ---\n").Append(stderr);
            if (sshHata.Trim().Length > 0) ic.Append("\n--- ssh ---\n").Append(Kisa(sshHata, 2000));
            string dosya = ""; try { dosya = SonucKaydi.HamCiktiYaz(sablon.Kategori, sablon.Ad, ic.ToString()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Gunluk("Uyarı", $"KALİ KOMUT -> ham çıktı dosyası yazılamadı ({sablon.Ad}): {ex.GetType().Name}"); }
            Gunluk(kod == 0 ? "Bilgi" : "Uyarı", $"KALİ KOMUT BİTTİ -> {sablon.Ad} hedef={hedef} çıkış kodu {kod}{(zamanAsimi ? " (ZAMAN AŞIMI)" : "")}, süre {sure.TotalSeconds:F1} sn, {bulgular.Count} bulgu, ham çıktı dosyası: {(dosya.Length > 0 ? Path.GetFileName(dosya) : "yok")}");
            return new KomutSonucu(sablon.Ad, hedef, ozet, baslangic, sure, kod, dosya, bulgular);
        }
        catch (OperationCanceledException) { Gunluk("Bilgi", $"KALİ KOMUT İPTAL EDİLDİ -> {sablon.Ad} hedef={hedef}"); throw; }
        catch (InvalidOperationException ex) { Gunluk("Uyarı", $"KALİ KOMUT BAŞARISIZ -> {sablon.Ad} hedef={hedef}: {ex.Message}"); throw; }
    }
}
