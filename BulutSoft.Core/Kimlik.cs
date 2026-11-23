// Bu dosya programın KİMLİK DOĞRULAMA ve YETKİ altyapısıdır (ekrandan bağımsız, test edilebilir).
// - Kullanıcı hesapları ve parolalar kimlik.json dosyasında saklanır; parolalar ASLA düz yazı tutulmaz: PBKDF2-SHA256 (600.000 tur) ile özetlenir.
// - "Kurulum şifresi": yönetici işlemlerinin (kullanıcı ekleme, rol değiştirme, parola sıfırlama, kurulum şifresini değiştirme) ek kilididir.
// - Roller: İzleyici (yalnızca görür/rapor alır), Analist (tarar, araçları kullanır), Yönetici (her şey).
// - Üst üste hatalı denemede hesap geçici kilitlenir (kaba kuvvet savunması). Önemli olaylar Olay Zaman Çizgisi'ne yazılır.
// DÜRÜST NOT: Bu yerel bir giriş kapısıdır. Bilgisayarınızın dosyalarına tam erişimi olan biri kimlik.json'u silerek programı "ilk kurulum"a döndürebilir.
// Gerçek koruma için Windows hesabınızı da parolalı tutun ve diskinizi şifreleyin (BitLocker).
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Kullanıcı rolleri (küçükten büyüğe yetki).
internal enum Rol { Izleyici = 0, Analist = 1, Yonetici = 2 }

// Yetki gerektiren eylem grupları.
internal enum Eylem { Gorme, Rapor, Tarama, Arac, Koruma, SistemDegistir, Yonetim }

// Bir kullanıcı hesabı.
internal sealed class Kullanici
{
    // Giriş adı (küçük harf).
    public string Ad { get; set; } = "";
    // Parola için rastgele tuz ve özet (Base64).
    public string Tuz { get; set; } = ""; public string Ozet { get; set; } = "";
    // Rol.
    public Rol Rol { get; set; } = Rol.Izleyici;
    // Devre dışı mı?
    public bool Devre { get; set; }
    // Oluşturma zamanı.
    public DateTime Olusturma { get; set; } = DateTime.Now;
    // Art arda hatalı deneme sayısı ve kilidin bitiş zamanı.
    public int Hatali { get; set; } public DateTime? KilitBitis { get; set; }
}

// Dosyanın tamamı.
internal sealed class KimlikVeri
{
    // PBKDF2 tur sayısı.
    public int Tekrar { get; set; }
    // Kurulum şifresi: tuz, özet, hatalı deneme ve kilit.
    public string KurulumTuz { get; set; } = ""; public string KurulumOzet { get; set; } = ""; public int KurulumHatali { get; set; } public DateTime? KurulumKilit { get; set; }
    // Kullanıcılar.
    public List<Kullanici> Kullanicilar { get; set; } = new();
}

// İşlem sonucu.
internal sealed record IslemSonucu(bool Basarili, string Mesaj);

// Giriş sonucu.
internal sealed record GirisSonucu(bool Basarili, string Mesaj, Kullanici? Kullanici);

// Dosya durumu.
internal enum KimlikDurumu { Yok, Hazir, Bozuk }

// O an oturum açmış kullanıcı.
internal static class Oturum
{
    // Aktif kullanıcı (giriş yapılmadıysa null: hiçbir yetki yok).
    public static Kullanici? Aktif { get; set; }
}

// Rol-eylem tablosu.
internal static class Yetki
{
    // Bu rol bu eylemi yapabilir mi? (Rol yoksa hayır.)
    public static bool Izinli(Rol? rol, Eylem e) => !(e == Eylem.SistemDegistir && PilotMod.SaltOkunur) && rol switch
    {
        Rol.Yonetici => true,
        Rol.Analist => e is Eylem.Gorme or Eylem.Rapor or Eylem.Tarama or Eylem.Arac or Eylem.Koruma,
        Rol.Izleyici => e is Eylem.Gorme or Eylem.Rapor,
        _ => false
    };

    // Rolün ekranda görünen Türkçe adı.
    public static string RolAdi(Rol r) => r switch { Rol.Yonetici => "Yönetici", Rol.Analist => "Analist", _ => "İzleyici" };

    // Rolün ne yapabildiğini anlatan kısa açıklama.
    public static string RolAciklama(Rol r) => r switch { Rol.Yonetici => "Her şey: tarama, sistem değiştiren işlemler (izolasyon, güvenlik duvarı), kullanıcı yönetimi.", Rol.Analist => "Tarama yapar, raporlar ve araçları kullanır, kalkanları/tuzakları yönetir. Sistemi değiştiren işlemleri ve kullanıcı yönetimini yapamaz.", _ => "Yalnızca sonuçları görür ve rapor alır. Tarama başlatamaz, hiçbir şeyi değiştiremez." };

    // Yetkisiz kullanıcıya gösterilecek mesaj.
    public static string RedMesaji(Rol? r, Eylem e) => e == Eylem.SistemDegistir && PilotMod.SaltOkunur ? PilotMod.Mesaj : $"Bu işlem için yetkiniz yok.\r\n\r\nMevcut rolünüz: {(r == null ? "oturum yok" : RolAdi(r.Value))}. Gerekli yetki: {(e is Eylem.SistemDegistir or Eylem.Yonetim ? "Yönetici" : "Analist veya Yönetici")}.\r\nYetki için bir Yönetici'ye başvurun.";
}

// Parola kuralları.
internal static class SifrePolitikasi
{
    // Çok bilinen kolay parolalar.
    private static readonly string[] Yasak = { "12345678", "password1", "parola123", "qwerty123", "abcd1234", "admin123", "bulutsoft1", "1q2w3e4r" };

    // Kurallara uyuyorsa null, uymuyorsa nedenini verir.
    public static string? Kontrol(string sifre, string? kullanici = null)
    {
        // Uzunluk.
        if (string.IsNullOrEmpty(sifre) || sifre.Length < 8) return "Parola en az 8 karakter olmalı.";
        // En çok 128.
        if (sifre.Length > 128) return "Parola en fazla 128 karakter olabilir.";
        // Harf ve rakam.
        if (!sifre.Any(char.IsLetter) || !sifre.Any(char.IsDigit)) return "Parola en az bir harf ve en az bir rakam içermeli.";
        // Kullanıcı adını içermesin.
        if (!string.IsNullOrEmpty(kullanici) && sifre.Contains(kullanici, StringComparison.OrdinalIgnoreCase)) return "Parola kullanıcı adını içermemeli.";
        // Tek karakter tekrarı.
        if (sifre.Distinct().Count() < 4) return "Parola çok basit (çok az farklı karakter).";
        // Bilinen kolay parola.
        if (Yasak.Contains(sifre.ToLowerInvariant())) return "Bu parola çok yaygın ve tahmin edilmesi kolay.";
        // Uygun.
        return null;
    }

    // Kullanıcı adı kuralı: 3-32 karakter, küçük harf/rakam/_/./-.
    public static bool AdGecerli(string ad) => Regex.IsMatch(ad ?? "", @"^[a-z0-9_.\-]{3,32}$");
}

// Hesapları dosyada tutan ve tüm işlemleri yapan depo.
internal static class KimlikDeposu
{
    // Dosya yolu.
    public static string Yol => BulutKlasor.Dosya("kimlik.json");
    // PBKDF2 tur sayısı (testte düşürülebilir).
    public static int VarsayilanTekrar { get; set; } = 600_000;
    // Eşzamanlı yazma kilidi.
    private static readonly object Kilit = new();
    // Kaç hatalı denemeden sonra kilitlensin.
    private const int KilitEsigi = 5;
    // Kullanıcıya her hatada aynı mesaj verilir (hesap var mı yok mu belli olmasın).
    private const string GenelHata = "Kullanıcı adı veya parola hatalı.";

    // Dosyanın durumu: yok (ilk kurulum), hazır veya bozuk.
    public static KimlikDurumu Durum()
    {
        // Dosya yoksa: iz yoksa gerçekten ilk kurulumdur. Mühür ya da yarım yazım izi varsa dosya SİLİNMİŞ demektir; sessizce ilk kuruluma (yeni yönetici oluşturmaya) dönülmez.
        if (!File.Exists(Yol)) return File.Exists(Butunluk.MuhurYolu(Yol)) || File.Exists(Yol + ".tmp") || IkinciKanal.Oku("kimlik") != null ? KimlikDurumu.Bozuk : KimlikDurumu.Yok;
        // Okunabiliyor ve en az bir aktif yönetici + kurulum şifresi var mı?
        var v = Oku(); return v != null && v.KurulumOzet.Length > 0 && v.Kullanicilar.Any(k => k.Rol == Rol.Yonetici && !k.Devre) ? KimlikDurumu.Hazir : KimlikDurumu.Bozuk;
    }

    // Dosyayı okur (bozuksa null).
    // Mühür (DPAPI) bozuksa YA DA HİÇ YOKSA dosya değiştirilmiş sayılır ve okunmaz (kimlik "bozuk" görünür; kurtarma akışı devreye girer). Eski "mührü yoksa yeniden mühürle" geçişi kimlik için kaldırıldı.
    // İKİNCİ KANAL: koruma hizmeti kuruluysa kimliğin güvenlik açısından kritik özeti HKLM'de de tutulur. Yerel mühür geçersiz/yok olsa bile dosya, HKLM'deki özetle BİREBİR eşleşiyorsa kabul edilir
    // (yerel mühür onarılır); yerel mühür geçerli görünse bile HKLM özetiyle uyuşmuyorsa (sahte mühür) REDDEDİLİR. Hizmette kayıt hiç yoksa yalnızca yerel mühür geçerlidir.
    private static KimlikVeri? Oku()
    {
        try
        {
            if (!File.Exists(Yol)) return null;
            bool yerelOk = Butunluk.GuvenliMiKati(Yol); string? kayit = IkinciKanal.Oku("kimlik");
            if (!yerelOk && kayit == null) return null;
            var v = JsonSerializer.Deserialize<KimlikVeri>(File.ReadAllText(Yol)); if (v == null) return null;
            if (kayit != null) { if (!IkinciKanal.Esit(KimlikOzeti(v), kayit)) return null; if (!yerelOk) Butunluk.Muhurla(Yol); }
            else if (ServisIstemci.Calisiyor) ServisIstemci.MuhurYaz("kimlik", KimlikOzeti(v));   // hizmet yeni kuruldu: mevcut sağlam durum ilk kez kaydedilir
            return v;
        }
        catch { return null; }
    }

    // Bir kimlik dosyasının kritik özeti (hizmet doğrulaması için; dosya okunamazsa null).
    internal static string? OzetDosyadan(string yol) { try { var v = JsonSerializer.Deserialize<KimlikVeri>(File.ReadAllText(yol)); return v == null ? null : KimlikOzeti(v); } catch { return null; } }

    // Güvenlik açısından KRİTİK alanların özeti (hatalı deneme sayacı ve kilit zamanı HARİÇ; onlar her girişte değişir ve hizmeti ilgilendirmez).
    internal static string KimlikOzeti(KimlikVeri v)
    {
        var kritik = new object[] { v.Tekrar, v.KurulumTuz, v.KurulumOzet, v.Kullanicilar.OrderBy(k => k.Ad, StringComparer.Ordinal).Select(k => new object[] { k.Ad, k.Tuz, k.Ozet, (int)k.Rol, k.Devre }).ToArray() };
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(kritik))));
    }

    // Dosyayı güvenli yazar (önce geçici dosya, sonra yer değiştirme: yarım yazılmış dosya kalmaz).
    // Yaz'ın istisnasız sürümü: hizmet yanıt vermiyorsa kullanıcıya anlaşılır mesaj döner.
    private static bool TryYaz(KimlikVeri v, out string hata) { try { Yaz(v); hata = ""; return true; } catch (IOException ex) { hata = ex.Message; return false; } }

    // Kritik alanlar değişiyorsa ÖNCE hizmete bildirilir (hizmet kayıt kuruluysa ve yanıt vermiyorsa değişiklik YAPILMAZ: güvenlik değişikliği iki kanalda da kayıtlı olmalı).
    private static void Yaz(KimlikVeri v)
    {
        string ozet = KimlikOzeti(v), json = JsonSerializer.Serialize(v, JsonAyarlari.Girintili); string? kayit = IkinciKanal.Oku("kimlik");
        if (kayit != null && !IkinciKanal.Esit(kayit, ozet) && !ServisIstemci.MuhurYaz("kimlik", ozet)) throw new IOException("Koruma hizmeti (BulutSoftGuard) yanıt vermiyor; hesap/parola değişikliği güvenli kaydedilemedi. Hizmeti başlatıp yeniden deneyin.");
        Butunluk.YazGuvenli(Yol, json);
        if (kayit == null && ServisIstemci.Calisiyor) ServisIstemci.MuhurYaz("kimlik", ozet);
    }

    // Parolanın PBKDF2 özetini hesaplar.
    private static byte[] Ozetle(string sifre, byte[] tuz, int tekrar) => Rfc2898DeriveBytes.Pbkdf2(sifre, tuz, tekrar, HashAlgorithmName.SHA256, 32);

    // Yeni tuzla özet üretir (tuz, özet).
    private static (string, string) Yeni(string sifre, int tekrar) { var tuz = RandomNumberGenerator.GetBytes(16); return (Convert.ToBase64String(tuz), Convert.ToBase64String(Ozetle(sifre, tuz, tekrar))); }

    // Verilen parola kayıtlı özetle eşleşiyor mu? (sabit zamanlı karşılaştırma)
    private static bool Dogru(string sifre, string tuz, string ozet, int tekrar) => CryptographicOperations.FixedTimeEquals(Ozetle(sifre ?? "", Convert.FromBase64String(tuz), tekrar), Convert.FromBase64String(ozet));

    // Olay günlüğüne yazar.
    private static void Olay(string seviye, string mesaj) { try { ServisBaglami.OlayYazici.Ekle("Oturum", seviye, "OTURUM -> " + mesaj); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Kimlik.Olay"); } }

    // Hatalı denemeden sonra kilit süresi: 5. hatada 30 sn, sonra katlanarak en çok 15 dk.
    public static TimeSpan KilitSuresi(int hatali) => hatali < KilitEsigi ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Min(900, 30 * Math.Pow(2, Math.Min(10, hatali - KilitEsigi))));

    // İlk kurulum: kurulum şifresi ve ilk yönetici hesabı.
    public static IslemSonucu Kur(string kurulumSifresi, string yoneticiAd, string yoneticiSifre)
    {
        // Kilit.
        lock (Kilit)
        {
            // Zaten kuruluysa yapma.
            if (File.Exists(Yol)) return new(false, "Kurulum zaten yapılmış.");
            // Kurallar.
            string ad = (yoneticiAd ?? "").Trim().ToLowerInvariant();
            if (!SifrePolitikasi.AdGecerli(ad)) return new(false, "Kullanıcı adı 3-32 karakter olmalı; yalnızca küçük harf, rakam, _ . - içerebilir.");
            var h = SifrePolitikasi.Kontrol(kurulumSifresi); if (h != null) return new(false, "Kurulum şifresi: " + h);
            h = SifrePolitikasi.Kontrol(yoneticiSifre, ad); if (h != null) return new(false, "Yönetici parolası: " + h);
            if (kurulumSifresi == yoneticiSifre) return new(false, "Kurulum şifresi ile yönetici parolası FARKLI olmalı.");
            // Oluştur.
            var v = new KimlikVeri { Tekrar = VarsayilanTekrar }; (v.KurulumTuz, v.KurulumOzet) = Yeni(kurulumSifresi, v.Tekrar);
            var (tuz, ozet) = Yeni(yoneticiSifre, v.Tekrar); v.Kullanicilar.Add(new Kullanici { Ad = ad, Tuz = tuz, Ozet = ozet, Rol = Rol.Yonetici });
            // Yaz.
            if (!TryYaz(v, out var yh)) return new(false, yh); Olay("Bilgi", $"İlk kurulum tamamlandı, yönetici hesabı oluşturuldu: {ad}"); return new(true, "Kurulum tamamlandı.");
        }
    }

    // Giriş yapar.
    public static GirisSonucu GirisYap(string ad, string sifre)
    {
        // Kilit.
        lock (Kilit)
        {
            // Dosya.
            var v = Oku(); if (v == null) return new(false, "Kimlik dosyası okunamadı.", null);
            // Ad.
            string a = (ad ?? "").Trim().ToLowerInvariant(); var k = v.Kullanicilar.FirstOrDefault(x => x.Ad == a);
            // Bilinmeyen kullanıcı: yine de özet hesapla (süreden hesap varlığı anlaşılmasın).
            if (k == null) { Ozetle(sifre ?? "", new byte[16], v.Tekrar); Olay("Uyarı", $"Başarısız giriş (bilinmeyen kullanıcı): {Temiz(a)}"); return new(false, GenelHata, null); }
            // Kilitli mi?
            if (k.KilitBitis > DateTime.Now) return new(false, $"Çok fazla hatalı deneme. {(int)Math.Ceiling((k.KilitBitis.Value - DateTime.Now).TotalSeconds)} saniye sonra tekrar deneyin.", null);
            // Parola doğru mu?
            if (!Dogru(sifre ?? "", k.Tuz, k.Ozet, v.Tekrar))
            {
                // Hata say, gerekirse kilitle.
                k.Hatali++; var s = KilitSuresi(k.Hatali); if (s > TimeSpan.Zero) k.KilitBitis = DateTime.Now + s; Yaz(v);
                Olay(s > TimeSpan.Zero ? "Kritik" : "Uyarı", s > TimeSpan.Zero ? $"KRİTİK: {a} hesabı {k.Hatali} hatalı denemeden sonra {(int)s.TotalSeconds} sn kilitlendi (kaba kuvvet şüphesi)" : $"Başarısız giriş: {a} ({k.Hatali}. hata)");
                return new(false, GenelHata, null);
            }
            // Devre dışı hesap (parola doğruysa söylenir).
            if (k.Devre) { Olay("Uyarı", $"Devre dışı hesapla giriş denemesi: {a}"); return new(false, "Bu hesap devre dışı bırakılmış. Bir yöneticiye başvurun.", null); }
            // Başarı: sayaçları sıfırla.
            k.Hatali = 0; k.KilitBitis = null; Yaz(v); Olay("Bilgi", $"Giriş yapıldı: {a} ({Yetki.RolAdi(k.Rol)})"); return new(true, "", k);
        }
    }

    // Olay satırına yazılacak adı temizler (satır sonu/uzunluk).
    private static string Temiz(string s) { var t = new string(s.Where(c => !char.IsControl(c)).ToArray()); return t.Length > 40 ? t[..40] : t; }

    // Kurulum şifresini doğrular (hatalıysa sayar ve kilitler). v: güncel veri.
    private static IslemSonucu KurulumDogrula(KimlikVeri v, string sifre, string kim)
    {
        // Kilitli mi?
        if (v.KurulumKilit > DateTime.Now) return new(false, $"Kurulum şifresi için çok fazla hatalı deneme. {(int)Math.Ceiling((v.KurulumKilit.Value - DateTime.Now).TotalSeconds)} saniye bekleyin.");
        // Doğru mu?
        if (Dogru(sifre ?? "", v.KurulumTuz, v.KurulumOzet, v.Tekrar)) { if (v.KurulumHatali != 0) { v.KurulumHatali = 0; v.KurulumKilit = null; } return new(true, ""); }
        // Hata.
        v.KurulumHatali++; var s = KilitSuresi(v.KurulumHatali); if (s > TimeSpan.Zero) v.KurulumKilit = DateTime.Now + s; Yaz(v);
        Olay(s > TimeSpan.Zero ? "Kritik" : "Uyarı", s > TimeSpan.Zero ? $"KRİTİK: Kurulum şifresi {v.KurulumHatali} kez yanlış girildi ({Temiz(kim)}), {(int)s.TotalSeconds} sn kilitlendi" : $"Yanlış kurulum şifresi girildi ({Temiz(kim)})");
        return new(false, "Kurulum şifresi hatalı.");
    }

    // Kurulum şifresi doğrulaması ve ardından işlemi çalıştıran ortak kalıp.
    private static IslemSonucu Yonet(string kim, string kurulumSifresi, Func<KimlikVeri, IslemSonucu> islem)
    {
        // Kilit.
        lock (Kilit)
        {
            // Veri.
            var v = Oku(); if (v == null) return new(false, "Kimlik dosyası okunamadı.");
            // Yönetici mi? (işlemi yapan kişinin rolü dosyadan doğrulanır, ekrandaki değere güvenilmez)
            var y = v.Kullanicilar.FirstOrDefault(x => string.Equals(x.Ad, kim ?? "", StringComparison.OrdinalIgnoreCase)); if (y == null || y.Rol != Rol.Yonetici || y.Devre) return new(false, "Bu işlem yalnızca Yönetici hesabıyla yapılır.");
            // Kurulum şifresi.
            var d = KurulumDogrula(v, kurulumSifresi, kim ?? ""); if (!d.Basarili) return d;
            // İşlem.
            var r = islem(v); if (r.Basarili && !TryYaz(v, out var yh)) return new(false, yh); return r;
        }
    }

    // Yönetici onayı: işlemi yapanın Yönetici olduğunu dosyadan doğrular ve KURULUM ŞİFRESİNİ denetler (hatalıysa sayar/kilitler). Başka hiçbir şeyi değiştirmez.
    public static IslemSonucu YoneticiOnayi(string yapan, string kurulumSifresi) => Yonet(yapan, kurulumSifresi, _ => new(true, ""));

    // Aktif yönetici sayısı.
    private static int Yoneticiler(KimlikVeri v) => v.Kullanicilar.Count(k => k.Rol == Rol.Yonetici && !k.Devre);

    // Yeni kullanıcı ekler.
    public static IslemSonucu KullaniciEkle(string yapan, string kurulumSifresi, string ad, string sifre, Rol rol) => Yonet(yapan, kurulumSifresi, v =>
    {
        // Ad ve parola kuralları.
        string a = (ad ?? "").Trim().ToLowerInvariant(); if (!SifrePolitikasi.AdGecerli(a)) return new(false, "Kullanıcı adı 3-32 karakter olmalı; yalnızca küçük harf, rakam, _ . - içerebilir.");
        if (v.Kullanicilar.Any(k => k.Ad == a)) return new(false, "Bu kullanıcı adı zaten var.");
        var h = SifrePolitikasi.Kontrol(sifre, a); if (h != null) return new(false, h);
        // Ekle.
        var (tuz, ozet) = Yeni(sifre, v.Tekrar); v.Kullanicilar.Add(new Kullanici { Ad = a, Tuz = tuz, Ozet = ozet, Rol = rol }); Olay("Bilgi", $"{yapan} yeni kullanıcı ekledi: {a} ({Yetki.RolAdi(rol)})"); return new(true, $"{a} eklendi.");
    });

    // Kullanıcının rolünü değiştirir (son aktif yönetici düşürülemez).
    public static IslemSonucu RolDegistir(string yapan, string kurulumSifresi, string ad, Rol yeni) => Yonet(yapan, kurulumSifresi, v =>
    {
        // Kullanıcı.
        var k = v.Kullanicilar.FirstOrDefault(x => x.Ad == ad); if (k == null) return new(false, "Kullanıcı bulunamadı.");
        // Son yönetici koruması.
        if (k.Rol == Rol.Yonetici && yeni != Rol.Yonetici && !k.Devre && Yoneticiler(v) <= 1) return new(false, "Son aktif yöneticinin rolü düşürülemez.");
        // Değiştir.
        var eski = k.Rol; k.Rol = yeni; Olay("Uyarı", $"{yapan}, {ad} kullanıcısının rolünü değiştirdi: {Yetki.RolAdi(eski)} -> {Yetki.RolAdi(yeni)}"); return new(true, "Rol değiştirildi.");
    });

    // Bir kullanıcının parolasını yönetici olarak sıfırlar (kurulum şifresi gerekir).
    public static IslemSonucu SifreSifirla(string yapan, string kurulumSifresi, string ad, string yeniSifre) => Yonet(yapan, kurulumSifresi, v =>
    {
        // Kullanıcı ve kural.
        var k = v.Kullanicilar.FirstOrDefault(x => x.Ad == ad); if (k == null) return new(false, "Kullanıcı bulunamadı.");
        var h = SifrePolitikasi.Kontrol(yeniSifre, ad); if (h != null) return new(false, h);
        // Yeni özet, kilit sıfırla.
        (k.Tuz, k.Ozet) = Yeni(yeniSifre, v.Tekrar); k.Hatali = 0; k.KilitBitis = null; Olay("Uyarı", $"{yapan}, {ad} kullanıcısının parolasını sıfırladı"); return new(true, "Parola sıfırlandı.");
    });

    // Hesabı devre dışı bırakır veya yeniden etkinleştirir (son aktif yönetici kapatılamaz).
    public static IslemSonucu DevreDegistir(string yapan, string kurulumSifresi, string ad, bool devre) => Yonet(yapan, kurulumSifresi, v =>
    {
        // Kullanıcı.
        var k = v.Kullanicilar.FirstOrDefault(x => x.Ad == ad); if (k == null) return new(false, "Kullanıcı bulunamadı.");
        // Son yönetici koruması.
        if (devre && k.Rol == Rol.Yonetici && !k.Devre && Yoneticiler(v) <= 1) return new(false, "Son aktif yönetici devre dışı bırakılamaz.");
        // Uygula.
        k.Devre = devre; Olay("Uyarı", $"{yapan}, {ad} hesabını {(devre ? "devre dışı bıraktı" : "etkinleştirdi")}"); return new(true, devre ? "Hesap devre dışı bırakıldı." : "Hesap etkinleştirildi.");
    });

    // Kullanıcıyı siler (son aktif yönetici silinemez).
    public static IslemSonucu Sil(string yapan, string kurulumSifresi, string ad) => Yonet(yapan, kurulumSifresi, v =>
    {
        // Kullanıcı.
        var k = v.Kullanicilar.FirstOrDefault(x => x.Ad == ad); if (k == null) return new(false, "Kullanıcı bulunamadı.");
        // Son yönetici koruması.
        if (k.Rol == Rol.Yonetici && !k.Devre && Yoneticiler(v) <= 1) return new(false, "Son aktif yönetici silinemez.");
        // Sil.
        v.Kullanicilar.Remove(k); Olay("Uyarı", $"{yapan}, {ad} hesabını sildi"); return new(true, "Kullanıcı silindi.");
    });

    // Kurulum şifresini değiştirir (eski kurulum şifresi gerekir).
    public static IslemSonucu KurulumSifresiDegistir(string yapan, string eskiKurulum, string yeniKurulum) => Yonet(yapan, eskiKurulum, v =>
    {
        // Kural.
        var h = SifrePolitikasi.Kontrol(yeniKurulum); if (h != null) return new(false, "Yeni kurulum şifresi: " + h);
        // Yeni kurulum şifresi, kimsenin parolasıyla aynı olmasın (yönetici kendi parolasını kurulum şifresi yapmasın).
        if (v.Kullanicilar.Any(k => Dogru(yeniKurulum, k.Tuz, k.Ozet, v.Tekrar))) return new(false, "Kurulum şifresi, bir hesap parolasıyla aynı olamaz.");
        // Yaz.
        (v.KurulumTuz, v.KurulumOzet) = Yeni(yeniKurulum, v.Tekrar); Olay("Kritik", $"KRİTİK: {yapan} kurulum şifresini değiştirdi"); return new(true, "Kurulum şifresi değiştirildi.");
    });

    // Kullanıcı kendi parolasını değiştirir (eski parola gerekir; kurulum şifresi gerekmez).
    public static IslemSonucu SifreDegistir(string ad, string eskiSifre, string yeniSifre)
    {
        // Kilit.
        lock (Kilit)
        {
            // Veri ve kullanıcı.
            var v = Oku(); var k = v?.Kullanicilar.FirstOrDefault(x => string.Equals(x.Ad, ad ?? "", StringComparison.OrdinalIgnoreCase)); if (v == null || k == null) return new(false, "Kullanıcı bulunamadı.");
            // Kilitli mi?
            if (k.KilitBitis > DateTime.Now) return new(false, "Hesap geçici olarak kilitli. Biraz bekleyin.");
            // Eski parola.
            if (!Dogru(eskiSifre ?? "", k.Tuz, k.Ozet, v.Tekrar)) { k.Hatali++; var s = KilitSuresi(k.Hatali); if (s > TimeSpan.Zero) k.KilitBitis = DateTime.Now + s; Yaz(v); Olay("Uyarı", $"Parola değiştirmede yanlış eski parola: {k.Ad}"); return new(false, "Mevcut parola hatalı."); }
            // Yeni parola kuralı.
            var h = SifrePolitikasi.Kontrol(yeniSifre, k.Ad); if (h != null) return new(false, h);
            if (yeniSifre == eskiSifre) return new(false, "Yeni parola eskisinden farklı olmalı.");
            // Yaz.
            (k.Tuz, k.Ozet) = Yeni(yeniSifre, v.Tekrar); k.Hatali = 0; k.KilitBitis = null; if (!TryYaz(v, out var yh)) return new(false, yh); Olay("Bilgi", $"{k.Ad} kendi parolasını değiştirdi"); return new(true, "Parolanız değiştirildi.");
        }
    }

    // "Şifremi unuttum": giriş yapmadan, KURULUM ŞİFRESİNİ bilen kişi bir hesabın parolasını sıfırlar (kurulum şifresi sahiplik kanıtıdır).
    // Güvenlik: kurulum şifresi yanlışsa hata sayılır ve kilitlenir; hesap adı yoksa da aynı genel mesaj verilir.
    public static IslemSonucu SifreKurtar(string ad, string kurulumSifresi, string yeniSifre)
    {
        // Kilit.
        lock (Kilit)
        {
            // Veri.
            var v = Oku(); if (v == null) return new(false, "Kimlik dosyası okunamadı.");
            // Önce kurulum şifresi (hatalıysa sayılır ve gerekirse kilitlenir).
            string a = (ad ?? "").Trim().ToLowerInvariant(); var d = KurulumDogrula(v, kurulumSifresi, "şifre kurtarma: " + a); if (!d.Basarili) return d;
            // Hesap ve yeni parola kuralı.
            var k = v.Kullanicilar.FirstOrDefault(x => x.Ad == a); if (k == null) return new(false, "Bu kullanıcı adıyla bir hesap bulunamadı.");
            var h = SifrePolitikasi.Kontrol(yeniSifre, a); if (h != null) return new(false, h);
            // Yeni özet; kilit ve hata sayacı sıfırlanır.
            (k.Tuz, k.Ozet) = Yeni(yeniSifre, v.Tekrar); k.Hatali = 0; k.KilitBitis = null; if (!TryYaz(v, out var yh)) return new(false, yh); Olay("Kritik", $"KRİTİK: {a} hesabının parolası giriş ekranından kurulum şifresiyle sıfırlandı"); return new(true, "Parola sıfırlandı. Yeni parolanızla giriş yapabilirsiniz.");
        }
    }

    // Kullanıcı listesinin kopyası (ekranda göstermek için; özetler dahil edilmez).
    public static List<Kullanici> Liste() => Oku()?.Kullanicilar.Select(k => new Kullanici { Ad = k.Ad, Rol = k.Rol, Devre = k.Devre, Olusturma = k.Olusturma, Hatali = k.Hatali, KilitBitis = k.KilitBitis }).ToList() ?? new();
}
