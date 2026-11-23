// Bu dosya önemli dosyaların (kimlik.json, lisans otorite anahtarı) başkası tarafından DEĞİŞTİRİLMEDİĞİNİ anlamak için "mühür" üretir.
// Yöntem: dosyanın SHA-256 özeti Windows DPAPI (CurrentUser) ile şifrelenip yanına ".mac" dosyası olarak yazılır. DPAPI anahtarı bu Windows kullanıcısına bağlıdır:
// dosya başka bir bilgisayara/kullanıcıya kopyalanıp elle düzenlenirse mühür doğrulanamaz.
// DÜRÜST SINIR: Aynı Windows kullanıcısı olarak çalışan bir kötü yazılım mührü de yeniden üretebilir (DPAPI bunu engellemez); bu katman "yanlışlıkla/uzaktan/başka kullanıcıdan"
// gelen değişikliği yakalar, yönetici haklı saldırgana karşı tam koruma değildir. Mührü olmayan ESKİ dosyalar ilk okunuşta bir kez mühürlenir (geçiş).
using System.Security.Cryptography;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Mühür durumu.
internal enum ButunlukDurumu { Yok, Gecerli, Bozuk }

// Mühür yardımcısı.
internal static class Butunluk
{
    // Mühür dosyasının yolu.
    public static string MuhurYolu(string yol) => yol + ".mac";
    // DPAPI ek gizli verisi: dosya adına bağlıdır (bir dosyanın mührü başka dosyaya kopyalanamaz).
    private static byte[] Ek(string yol) => Encoding.UTF8.GetBytes("bs-butunluk-v1|" + Path.GetFileName(yol).ToLowerInvariant());
    // Dosyanın özeti.
    private static byte[] Ozet(string yol) => SHA256.HashData(File.ReadAllBytes(yol));

    // Mühür dosyasını güvenli yazar (önce ".tmp", sonra yer değiştirme; yarım mühür kalmaz). Birden çok özet yan yana konabilir (32 bayt × n).
    internal static void MuhurYaz(string yol, params byte[][] ozetler)
    {
        // Özetleri birleştir ve DPAPI ile şifrele.
        var bir = ozetler.SelectMany(o => o).ToArray(); string m = MuhurYolu(yol), g = m + ".tmp";
        File.WriteAllText(g, Convert.ToBase64String(ProtectedData.Protect(bir, Ek(yol), DataProtectionScope.CurrentUser))); File.Move(g, m, true);
    }

    // Dosyayı mühürler (DPAPI kullanılamazsa sessizce atlar; program çalışmaya devam eder).
    public static void Muhurla(string yol)
    {
        // Dosya yoksa mühür de yok.
        try { if (!File.Exists(yol)) return; MuhurYaz(yol, Ozet(yol)); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Butunluk.Muhurla"); }
    }

    // Mühürlü dosyayı ÇÖKMEYE DAYANIKLI yazar: önce hem eski hem yeni özeti içeren mühür, sonra dosya, sonra yalnızca yeni özetli mühür.
    // Hangi adımda kesilirse kesilsin dosya, mührün kabul ettiği iki sürümden biri olur; kullanıcı "bozuk dosya" yüzünden kilitli kalmaz.
    public static void YazGuvenli(string yol, string icerik)
    {
        // Klasör ve geçici dosya.
        var d = Path.GetDirectoryName(yol); if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
        var bayt = new UTF8Encoding(false).GetBytes(icerik); string gecici = yol + ".tmp"; File.WriteAllBytes(gecici, bayt);
        // Yeni özet; eski dosya mühürle uyumluysa onun özeti de geçici olarak kabul edilir (bozuk/oynanmış eski dosya kabul edilmez).
        var yeni = SHA256.HashData(bayt); bool eskiSaglam = File.Exists(yol) && Kontrol(yol) != ButunlukDurumu.Bozuk;
        try { if (eskiSaglam) MuhurYaz(yol, yeni, Ozet(yol)); else MuhurYaz(yol, yeni); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Butunluk.YazGuvenli"); }
        // Dosyayı yer değiştir.
        File.Move(gecici, yol, true);
        // Mührü yalnızca yeni özetle sadeleştir.
        try { MuhurYaz(yol, yeni); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "Butunluk.YazGuvenli"); }
    }

    // Mührü doğrular: Yok (mühür dosyası yok), Gecerli, Bozuk (mühür var ama uyuşmuyor/çözülemiyor).
    public static ButunlukDurumu Kontrol(string yol)
    {
        // Mühür yoksa.
        try
        {
            if (!File.Exists(MuhurYolu(yol))) return ButunlukDurumu.Yok;
            // Çöz ve karşılaştır (sabit zamanlı).
            var beklenen = ProtectedData.Unprotect(Convert.FromBase64String(File.ReadAllText(MuhurYolu(yol)).Trim()), Ek(yol), DataProtectionScope.CurrentUser);
            var simdi = Ozet(yol); for (int i = 0; i + 32 <= beklenen.Length; i += 32) if (CryptographicOperations.FixedTimeEquals(beklenen.AsSpan(i, 32), simdi)) return ButunlukDurumu.Gecerli;
            return ButunlukDurumu.Bozuk;
        }
        // Çözülemeyen (başka kullanıcıdan gelmiş/bozuk) mühür geçersizdir.
        catch { return ButunlukDurumu.Bozuk; }
    }

    // KATI doğrulama (kimlik gibi güvenlik açısından kritik dosyalar için): yalnızca GEÇERLİ mühür kabul edilir.
    // Mühür dosyası silinmişse ("Yok") dosya KABUL EDİLMEZ ve yeniden mühürlenmez; aksi hâlde saldırgan dosyayı değiştirip mührü silerek bütünlük denetimini aşabilirdi.
    public static bool GuvenliMiKati(string yol) => Kontrol(yol) == ButunlukDurumu.Gecerli;

    // Dosya okunmaya güvenli mi? Bozuk mühür → false; mühür yoksa (eski dosya) bir kez mühürler ve true döner.
    // DİKKAT: bu geçiş davranışı yalnızca önemsiz dosyalar içindir; kimlik gibi kritik dosyalar GuvenliMiKati kullanır.
    public static bool GuvenliMi(string yol)
    {
        // Duruma göre.
        var d = Kontrol(yol); if (d == ButunlukDurumu.Bozuk) return false; if (d == ButunlukDurumu.Yok) Muhurla(yol); return true;
    }
}
