// Bu dosya LİSANS altyapısıdır (ekrandan bağımsız, test edilebilir).
// TASARIM İLKELERİ:
// 1) Lisans dosyası DİJİTAL İMZALIDIR (ECDSA P-256). İmzayı yalnızca "lisans otoritesi" (sizin Kali sanal makineniz) atabilir; program yalnızca GENEL anahtarla
//    doğrular. Bu yüzden başkası kendi başına geçerli lisans (keygen) üretemez. Özel anahtar Kali'den ASLA çıkmaz.
// 2) Doğrulama tamamen ÇEVRİMDIŞIDIR: Kali kapalıysa, ağ yoksa program yine çalışır. Kali yalnızca lisans ÜRETİRKEN gerekir.
// 3) Lisans bir bilgisayar kimliğine (makine kimliği) bağlıdır; paylaşılması zorlaşır.
// 4) Deneme: 14 gün. Başlangıç tarihi birden çok yerde (dosya + kayıt defteri, DPAPI korumalı) saklanır, saat geri alınırsa kazanç sağlanmaz.
// 5) SİSTEMİ ÇÖKERTMEZ: lisans yoksa/bittiyse YALNIZCA yeni tarama başlatma kısıtlanır. Giriş, geçmiş, raporlar, çalışan kalkanlar (Nöbetçi, USB, Tuzaklar)
//    ve veriler ASLA kapatılmaz/silinmez. Kontrol hata verirse program kısıtlamaz (güvenli tarafta kalır).
// DÜRÜST NOT: Yerel kontrol, programı değiştirebilen kararlı biri tarafından atlanabilir; bu, dürüst müşteriyi yönlendiren makul bir korumadır.
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

// Programımızın adı.
namespace LabDenetim;

// Lisansın durumu.
internal enum LisansDurumu { Sahip, Lisansli, Deneme, DenemeBitti, LisansSuresiDoldu, Gecersiz }

// İmzalı lisansın içeriği.
internal sealed record LisansBilgi(string Musteri, string Makine, DateTime Verilis, DateTime? Bitis, string Surum, int Format = 1);

// Durum sonucu.
internal sealed record LisansSonucu(LisansDurumu Durum, int KalanGun, string Mesaj, LisansBilgi? Bilgi);

// Lisans yardımcıları.
internal static class Lisans
{
    // Deneme süresi (gün).
    public const int DenemeGun = 14;
    // Kurum (çok makineli) lisansın makine alanı değeri.
    public const string KurumMakinesi = "*";
    // Dosya yolları.
    public static string LisansYolu => BulutKlasor.Dosya("lisans.bslic");
    public static string OtoriteYolu => BulutKlasor.Dosya("lisans_otorite.pem");
    // JSON ayarı.
    private static readonly JsonSerializerOptions Js = new() { WriteIndented = false };

    // ---- Makine kimliği ----
    // Windows'un makine GUID'inden türetilen, geri çevrilemeyen kimlik (XXXX-XXXX-XXXX-XXXX).
    public static string MakineKimligi() { string g = ""; try { g = Convert.ToString(Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid"), CultureInfo.InvariantCulture) ?? ""; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Lisans.MakineKimligi"); } return MakineKimligi(g.Length > 0 ? g : Environment.MachineName); }

    // Verilen GUID'den kimlik üretir (test edilebilir).
    public static string MakineKimligi(string guid) { var h = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("BulutSoft|" + guid)))[..16]; return string.Join("-", Enumerable.Range(0, 4).Select(i => h.Substring(i * 4, 4))); }

    // ---- Genel anahtar (lisans otoritesi) ----
    // Gömülü kaynağı önce BulutSoft.Core derlemesinde, sonra çalışan uygulamanın (arayüz exe) derlemesinde arar: ticari sürüm genel anahtarı
    // arayüz projesine gömebilir; lisans kodunun çekirdeğe taşınmış olması bunu bozmamalı.
    private static Stream? GomuluKaynak(string ad) => typeof(Lisans).Assembly.GetManifestResourceStream(ad) ?? Assembly.GetEntryAssembly()?.GetManifestResourceStream(ad);
    // Genel anahtarı verir: önce programa GÖMÜLÜ "otorite_genel.pem" (ticari sürüm), yoksa sahibin kurduğu dosya.
    public static string? GenelAnahtar()
    {
        // Gömülü kaynak.
        try { using var s = GomuluKaynak("otorite_genel.pem"); if (s != null) { using var r = new StreamReader(s); return r.ReadToEnd(); } } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Lisans.GenelAnahtar"); }
        // Sahibin kurduğu dosya.
        // Mühür (DPAPI) bozuksa dosya değiştirilmiş olabilir (sahte lisans için anahtar değiştirme saldırısı); güvenilmez, yok sayılır.
        try { return File.Exists(OtoriteYolu) && Butunluk.GuvenliMi(OtoriteYolu) ? File.ReadAllText(OtoriteYolu) : null; } catch { return null; }
    }

    // ---- Paket (lisans dosyası) ----
    // Lisansı imzalatıp dosya metnine çevirir. imzalayici: "veri" metni (ASCII) → DER ECDSA imzasının Base64'ü.
    public static string PaketOlustur(LisansBilgi b, Func<string, string> imzalayici)
    {
        // Veri: lisans JSON'unun Base64'ü (ASCII; imza bu metin üzerinden atılır, böylece biçim/kodlama sorunu olmaz).
        string veri = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(b, Js)));
        // İmza.
        string imza = imzalayici(veri);
        // Dosya.
        return JsonSerializer.Serialize(new Dictionary<string, string> { ["veri"] = veri, ["imza"] = imza }, JsonAyarlari.Girintili);
    }

    // İmzayı doğrular (lisans içeriğini verir; geçersizse null ve neden).
    public static LisansBilgi? ImzaDogrula(string dosyaMetni, string genelPem, out string hata)
    {
        // Çözme ve doğrulama hata verebilir.
        try
        {
            // Alanlar.
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(dosyaMetni); if (d == null || !d.TryGetValue("veri", out var veri) || !d.TryGetValue("imza", out var imza)) { hata = "Lisans dosyası biçimi geçersiz."; return null; }
            // Genel anahtar.
            using var ec = ECDsa.Create(); ec.ImportFromPem(genelPem);
            // İmza (DER biçimi: OpenSSL çıktısı).
            if (!ec.VerifyData(Encoding.ASCII.GetBytes(veri), Convert.FromBase64String(imza.Trim()), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)) { hata = "Lisans imzası geçersiz (dosya değiştirilmiş veya başka bir otoriteden)."; return null; }
            // İçerik.
            var b = JsonSerializer.Deserialize<LisansBilgi>(Encoding.UTF8.GetString(Convert.FromBase64String(veri))); if (b == null) { hata = "Lisans içeriği okunamadı."; return null; }
            // Tamam.
            hata = ""; return b;
        }
        // Bozuk dosya.
        catch (Exception ex) { hata = "Lisans doğrulanamadı: " + ex.Message; return null; }
    }

    // Lisansı bu makine ve tarihe göre değerlendirir (deneme dahil değil).
    public static LisansSonucu Degerlendir(string? dosyaMetni, string? genelPem, string makine, DateTime simdi)
    {
        // Dosya veya otorite yoksa lisans yok.
        if (string.IsNullOrWhiteSpace(dosyaMetni)) return new(LisansDurumu.Gecersiz, 0, "Lisans dosyası yok.", null);
        if (string.IsNullOrWhiteSpace(genelPem)) return new(LisansDurumu.Gecersiz, 0, "Lisans otoritesi (genel anahtar) kurulu değil.", null);
        // İmza.
        var b = ImzaDogrula(dosyaMetni, genelPem, out var hata); if (b == null) return new(LisansDurumu.Gecersiz, 0, hata, null);
        // Makine.
        // Kurum lisansı: imzalı lisansta makine "*" ise bu kurumun tüm bilgisayarlarında geçerlidir (müşteri adı lisansta yazılıdır).
        if (b.Makine != KurumMakinesi && !string.Equals(b.Makine, makine, StringComparison.OrdinalIgnoreCase)) return new(LisansDurumu.Gecersiz, 0, $"Bu lisans başka bir bilgisayar içindir (lisanslı: {b.Makine}, bu bilgisayar: {makine}).", b);
        // Süre.
        int kalan = b.Bitis == null ? int.MaxValue : (int)Math.Ceiling((b.Bitis.Value - simdi).TotalDays);
        if (b.Bitis != null && kalan < 0) return new(LisansDurumu.LisansSuresiDoldu, 0, $"Lisans {b.Bitis:dd.MM.yyyy} tarihinde sona erdi.", b);
        // Geçerli.
        return new(b.Surum == "Sahip" ? LisansDurumu.Sahip : LisansDurumu.Lisansli, b.Bitis == null ? -1 : kalan, b.Bitis == null ? "Süresiz lisans." : $"Lisans {b.Bitis:dd.MM.yyyy} tarihine kadar geçerli ({kalan} gün).", b);
    }

    // ---- Deneme ----
    // Kayıt defteri yolu (testte değiştirilebilir).
    public static string RegistryYolu { get; set; } = @"Software\BulutSoft\Lic";
    // Deneme dosyası.
    private static string DenemeDosyasi => BulutKlasor.Dosya("deneme.dat");
    // Makine geneli klasör (ProgramData): tüm Windows kullanıcıları aynı deneme kaydını ve lisansı görür. Testte değiştirilebilir.
    public static string MakineKlasoru { get; set; } = Path.Combine(BrandConfig.OrtakKlasor, "Lisans");
    // Makine geneli lisans dosyası ve deneme kaydı.
    public static string MakineLisansYolu => Path.Combine(MakineKlasoru, "lisans.bslic");
    private static string MakineDenemeDosyasi => Path.Combine(MakineKlasoru, "deneme.dat");

    // DPAPI ile korur / çözer (yalnızca bu Windows kullanıcısı okuyabilir).
    private static string Koru(string s, DataProtectionScope kapsam = DataProtectionScope.CurrentUser) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), Encoding.UTF8.GetBytes("bs-lic"), kapsam));
    private static string? Coz(string b64, DataProtectionScope kapsam = DataProtectionScope.CurrentUser) { try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(b64), Encoding.UTF8.GetBytes("bs-lic"), kapsam)); } catch { return null; } }

    // Deneme kaydı.
    private sealed record DenemeKaydi(DateTime Baslangic, DateTime SonGorulen);

    // Kayıtları okur (dosya ve kayıt defteri); yoksa null.
    private static List<DenemeKaydi> KayitlariOku()
    {
        // Sonuç.
        var l = new List<DenemeKaydi>();
        // Dosya.
        try { if (File.Exists(DenemeDosyasi) && Coz(File.ReadAllText(DenemeDosyasi)) is { } s && JsonSerializer.Deserialize<DenemeKaydi>(s) is { } k) l.Add(k); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "Lisans.KayitlariOku"); }
        // Kayıt defteri.
        try { using var key = Registry.CurrentUser.OpenSubKey(RegistryYolu); if (key?.GetValue("d") is string r && Coz(r) is { } s2 && JsonSerializer.Deserialize<DenemeKaydi>(s2) is { } k2) l.Add(k2); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "Lisans.KayitlariOku"); }
        // Makine geneli kayıt (başka bir Windows kullanıcısı da yazmış olabilir; deneme başlangıcı yalnızca ERKENE gider, yeni kullanıcı süreyi sıfırlayamaz).
        try { if (File.Exists(MakineDenemeDosyasi) && Coz(File.ReadAllText(MakineDenemeDosyasi), DataProtectionScope.LocalMachine) is { } s3 && JsonSerializer.Deserialize<DenemeKaydi>(s3) is { } k3) l.Add(k3); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "Lisans.KayitlariOku"); }
        // Sonuç.
        return l;
    }

    // Kaydı iki yere yazar (biri silinse diğeri kalır).
    private static void KayitYaz(DenemeKaydi k)
    {
        // Metin.
        string m = Koru(JsonSerializer.Serialize(k));
        // Dosya.
        try { Depo.Yaz(DenemeDosyasi, m); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "Lisans.KayitYaz"); }
        // Kayıt defteri.
        try { using var key = Registry.CurrentUser.CreateSubKey(RegistryYolu); key.SetValue("d", m); } catch (Exception hk7) { HataKayit.Sessiz(hk7, "Lisans.KayitYaz"); }
        // Makine geneli kayıt (klasörü başka kullanıcı oluşturduysa yazılamayabilir; sessizce geçilir).
        try { Depo.Yaz(MakineDenemeDosyasi, Koru(JsonSerializer.Serialize(k), DataProtectionScope.LocalMachine)); } catch (Exception hk8) { HataKayit.Sessiz(hk8, "Lisans.KayitYaz"); }
    }

    // Deneme başlangıcını ve "efektif şimdi"yi verir; kayıt yoksa bugünü başlangıç yapar. Saat geri alınırsa kazanç sağlanmaz.
    public static (DateTime Baslangic, DateTime Efektif, bool SaatGeri) DenemeDurumu(DateTime simdi, bool yaz = true)
    {
        // Mevcut kayıtlar.
        var l = KayitlariOku();
        // İlk çalıştırma.
        DateTime bas = l.Count > 0 ? l.Min(x => x.Baslangic) : simdi, son = l.Count > 0 ? l.Max(x => x.SonGorulen) : simdi;
        // Saat geri alınmış mı? (1 günden fazla)
        bool geri = simdi < son.AddDays(-1);
        // Efektif şimdi: hiçbir zaman son görülenden önce olmaz.
        var efektif = simdi > son ? simdi : son;
        // Yaz: başlangıç aynı kalır, son görülen ileri gider; iki kayıt farklıysa eşitlenir.
        if (yaz && (l.Count < 3 || l.Any(x => x.Baslangic != bas || x.SonGorulen != efektif))) KayitYaz(new DenemeKaydi(bas, efektif));
        // Sonuç.
        return (bas, efektif, geri);
    }

    // ---- Genel durum ----
    // Bu bilgisayarın lisans durumu (hata olursa güvenli tarafta kalır: kısıtlamaz).
    public static LisansSonucu Durum(DateTime? simdiVerilen = null, bool yaz = true)
    {
        // Hiçbir hata programı kısıtlamasın.
        try
        {
            // Zaman ve makine.
            var simdi = simdiVerilen ?? DateTime.Now; string makine = MakineKimligi();
            // Lisans dosyası.
            string? dosya = null; try { dosya = File.Exists(LisansYolu) ? File.ReadAllText(LisansYolu) : File.Exists(MakineLisansYolu) ? File.ReadAllText(MakineLisansYolu) : null; } catch (Exception hk9) { HataKayit.Sessiz(hk9, "Lisans.Durum"); }
            // Önce lisans.
            var lic = Degerlendir(dosya, GenelAnahtar(), makine, simdi); if (lic.Durum is LisansDurumu.Sahip or LisansDurumu.Lisansli) return lic;
            // Deneme.
            var (bas, efektif, geri) = DenemeDurumu(simdi, yaz); int kalan = DenemeGun - (int)Math.Floor((efektif - bas).TotalDays);
            // Uyarı eki.
            string ek = (geri ? " (Bilgisayar saati geri alınmış görünüyor; deneme süresi etkilenmez.)" : "") + (dosya != null ? " Lisans: " + lic.Mesaj : "");
            // Deneme sürüyor mu?
            if (lic.Durum == LisansDurumu.LisansSuresiDoldu) return new(LisansDurumu.LisansSuresiDoldu, 0, lic.Mesaj, lic.Bilgi);
            return kalan > 0 ? new(LisansDurumu.Deneme, kalan, $"Deneme sürümü: {kalan} gün kaldı." + ek, null) : new(LisansDurumu.DenemeBitti, 0, "14 günlük deneme süresi doldu." + ek, null);
        }
        // Beklenmeyen hata: kısıtlama yapma.
        catch (Exception ex) { return new(LisansDurumu.Deneme, 1, "Lisans kontrolü yapılamadı (kısıtlama uygulanmadı): " + ex.Message, null); }
    }

    // Yeni tarama başlatma izni (yalnızca bu kısıtlanır).
    public static (bool Izinli, string Mesaj) YeniTaramaIzni(LisansSonucu s) => s.Durum is LisansDurumu.DenemeBitti or LisansDurumu.LisansSuresiDoldu or LisansDurumu.Gecersiz && s.Durum != LisansDurumu.Gecersiz ? (false, s.Mesaj + "\r\n\r\nYeni tarama başlatmak için bir lisans gerekir. Geçmiş, raporlar, kalkanlar ve diğer özellikler çalışmaya devam eder. Lisans için \"Lisans\" penceresine bakın.") : (true, "");

    // Kısa başlık metni.
    public static string Kisa(LisansSonucu s) => s.Durum switch { LisansDurumu.Sahip => "Sahip Lisansı", LisansDurumu.Lisansli => s.KalanGun < 0 ? "Lisanslı" : $"Lisanslı ({s.KalanGun} gün)", LisansDurumu.Deneme => $"Deneme: {s.KalanGun} gün", LisansDurumu.DenemeBitti => "Deneme bitti", LisansDurumu.LisansSuresiDoldu => "Lisans süresi doldu", _ => "Lisans geçersiz" };
}

// Lisans otoritesi (Kali sanal makinesi): anahtar kurulumu ve imzalama. Özel anahtar Kali'den ÇIKMAZ.
internal static class LisansOtoritesi
{
    // Kali'de çalışan SABİT komutlar (kullanıcı girdisi içermez).
    // Kurulum: anahtar yoksa üretir (VAR OLANIN ÜZERİNE ASLA YAZMAZ), genel anahtarı yazdırır.
    public const string KurKomutu = "mkdir -p ~/.bulutsoft && chmod 700 ~/.bulutsoft && { [ -f ~/.bulutsoft/lisans_ozel.pem ] || openssl ecparam -name prime256v1 -genkey -noout -out ~/.bulutsoft/lisans_ozel.pem; } && chmod 600 ~/.bulutsoft/lisans_ozel.pem && openssl ec -in ~/.bulutsoft/lisans_ozel.pem -pubout 2>/dev/null";
    // İmzalama: standart girişteki ASCII metni imzalar, DER imzayı Base64 tek satır yazar.
    public const string ImzaKomutu = "openssl dgst -sha256 -sign ~/.bulutsoft/lisans_ozel.pem | base64 -w0; echo";

    // Otoriteyi kurar: Kali'de anahtar üretir/varsa kullanır, genel anahtarı alıp yerel dosyaya yazar. Dönüş: genel anahtar PEM.
    public static async Task<string> KurAsync(SensorProfili p, CancellationToken ct)
    {
        // Çıktı satırları.
        var l = new List<string>(); var (kod, hata) = await SensorTaramaYurutucu.CalistirAsync(p, KurKomutu, null, l.Add, ct, 60);
        // Genel anahtar PEM'i.
        string pem = string.Join("\n", l.Select(x => x.Trim()).Where(x => x.Length > 0)); if (kod != 0 || !pem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)) throw new InvalidOperationException("Kali'de anahtar kurulamadı: " + (hata.Length > 0 ? SensorCiktiAyristirici.HataAciklama(hata) : "beklenmeyen cevap") + " (openssl kurulu mu?)");
        // Geçerliliği doğrula.
        using (var ec = ECDsa.Create()) ec.ImportFromPem(pem);
        // Yerel dosyaya yaz.
        Butunluk.YazGuvenli(Lisans.OtoriteYolu, pem + "\n"); return pem;
    }

    // Verilen lisansı Kali'ye imzalatır ve dosya metnini verir (yerelde de doğrular).
    public static async Task<string> ImzalaAsync(SensorProfili p, LisansBilgi b, CancellationToken ct)
    {
        // Genel anahtar kurulu olmalı.
        string pem = Lisans.GenelAnahtar() ?? throw new InvalidOperationException("Önce \"Lisans Otoritesini Kur\" ile Kali'de anahtar oluşturun.");
        // İmzalayıcı: Kali'ye gönder.
        string? sonuc = null; Exception? ex = null;
        // Senkron kalıp: PaketOlustur eşzamanlı imzalayıcı bekler; bu yüzden imzayı önce alıp sonra paketliyoruz.
        string veri = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(b, JsonAyarlari.Girintisiz)));
        // Kali'de imzala.
        var l = new List<string>(); var (kod, hata) = await SensorTaramaYurutucu.CalistirAsync(p, ImzaKomutu, veri, l.Add, ct, 60);
        // İmza satırı.
        sonuc = l.Select(x => x.Trim()).FirstOrDefault(x => x.Length > 20); if (kod != 0 || sonuc == null) { ex = new InvalidOperationException("Kali imzalayamadı: " + (hata.Length > 0 ? SensorCiktiAyristirici.HataAciklama(hata) : "özel anahtar yok mu? \"Lisans Otoritesini Kur\"u çalıştırın.")); throw ex; }
        // Paket: aynı veri ve imza.
        string paket = JsonSerializer.Serialize(new Dictionary<string, string> { ["veri"] = veri, ["imza"] = sonuc }, JsonAyarlari.Girintili);
        // Yerelde doğrula (Kali'den gelen imza bozuksa kullanıcıya hatalı lisans verilmesin).
        if (Lisans.ImzaDogrula(paket, pem, out var h) == null) throw new InvalidOperationException("Kali'nin imzası bu programın genel anahtarıyla doğrulanamadı: " + h);
        // Hazır.
        return paket;
    }
}
