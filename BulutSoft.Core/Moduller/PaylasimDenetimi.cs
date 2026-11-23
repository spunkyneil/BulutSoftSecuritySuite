// Ağ paylaşımı denetimi: bu bilgisayarın ağa açtığı klasörleri listeler ve riskli olanları işaretler.
// YALNIZCA OKUR (Windows'un paylaşım listesi); hiçbir paylaşımı kapatmaz, değiştirmez; internete çıkmaz.
using System.Globalization;
using System.Management;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// Bir paylaşımın ham kaydı: ad, gösterdiği klasör, tür kodu, açıklama.
public sealed record PaylasimKaydi(string Ad, string Yol, uint Tur, string Aciklama);

// Ağ paylaşımı denetçisi.
public static class PaylasimDenetimi
{
    // Windows'un tür kodu: 0 = dosya paylaşımı; 2147483648 = yönetimsel dosya paylaşımı (C$ gibi).
    private const uint Disk = 0, YonetimselDisk = 2147483648;

    // Paylaşılması çok tehlikeli sayılan klasörler (küçük harf, sonunda ters bölü yok).
    private static readonly string[] HassasYollar = { "\\windows", "\\program files", "\\program files (x86)", "\\programdata", "\\users", "\\documents and settings" };

    // Yolu karşılaştırmaya hazırlar: küçük harf, sonda ters bölü yok.
    private static string Duz(string y) => (y ?? "").Trim().TrimEnd('\\', '/').ToLowerInvariant();

    // Yol bir sürücü kökü mü (C: gibi)?
    private static bool SurucuKoku(string yol) { var d = Duz(yol); return d.Length == 2 && d[1] == ':' && char.IsLetter(d[0]); }

    // Yol hassas bir sistem klasörü mü (sürücü kökü hariç, o ayrı denetlenir)?
    private static bool HassasMi(string yol)
    {
        // Sürücü harfini at, kalan yolu karşılaştır.
        var d = Duz(yol); if (d.Length < 3 || d[1] != ':') return false; var kalan = d[2..];
        // Hassas klasörün kendisi ya da altı.
        return HassasYollar.Any(h => kalan == h || kalan.StartsWith(h + "\\", StringComparison.Ordinal));
    }

    // Ham kayıtları değerlendirip bulgu üretir (saf mantık; test edilebilir).
    public static List<Bulgu> Degerlendir(IEnumerable<PaylasimKaydi> kayitlar)
    {
        // Sonuç listesi.
        var sonuc = new List<Bulgu>();
        // Yalnızca dosya paylaşımları: yazıcı, IPC ve cihaz paylaşımları atlanır.
        var liste = kayitlar.Where(k => k.Tur == Disk || k.Tur == YonetimselDisk).ToList();
        // Kullanıcının kendi açtığı paylaşımlar (adı $ ile bitmeyen, yönetimsel olmayan).
        var kullanici = liste.Where(k => k.Tur == Disk && !k.Ad.EndsWith('$')).ToList();
        // Gizli ($ ile biten) ama yönetimsel olmayan paylaşımlar: kullanıcı elle gizlemiş olabilir.
        var gizli = liste.Where(k => k.Tur == Disk && k.Ad.EndsWith('$')).ToList();
        // Bir paylaşımın gösterim satırı.
        static string Satir(PaylasimKaydi k) => $"  • \\\\bu-bilgisayar\\{k.Ad}  →  {k.Yol}" + (string.IsNullOrWhiteSpace(k.Aciklama) ? "" : $"  ({k.Aciklama})");

        // 1) Bütün sürücüyü paylaşan: en büyük risk.
        var kok = kullanici.Where(k => SurucuKoku(k.Yol)).ToList();
        if (kok.Count > 0) sonuc.Add(new Bulgu("Paylaşım", "Bir sürücünün tamamı ağa paylaşılmış", Onem.Yuksek, "Şu paylaşımlar sürücünün en üst klasörünü gösteriyor; sürücüdeki her şey ağdan erişilebilir olabilir:\n" + string.Join("\n", kok.Select(Satir)), "Paylaşımı kaldırıp yalnızca gereken klasörü paylaşın. Yönetici PowerShell: Remove-SmbShare -Name \"<paylaşım adı>\" (önce kimin kullandığını kontrol edin)."));
        // 2) Sistem / program / kullanıcı klasörlerini paylaşan.
        var hassas = kullanici.Where(k => HassasMi(k.Yol)).ToList();
        if (hassas.Count > 0) sonuc.Add(new Bulgu("Paylaşım", "Windows veya kullanıcı klasörleri ağa paylaşılmış", Onem.Yuksek, "Şu paylaşımlar sistem, program veya kullanıcı klasörlerini gösteriyor:\n" + string.Join("\n", hassas.Select(Satir)), "Bu klasörleri paylaşmayın; gerekirse yalnızca belirli bir alt klasörü paylaşın."));
        // 3) Diğer kullanıcı paylaşımları: hangi erişim izinleri olduğuna bakılmalı.
        var digerleri = kullanici.Except(kok).Except(hassas).ToList();
        if (digerleri.Count > 0) sonuc.Add(new Bulgu("Paylaşım", $"{digerleri.Count} klasör ağa paylaşılmış", Onem.Orta, "Şu klasörler ağdaki diğer bilgisayarlara açık:\n" + string.Join("\n", digerleri.Take(10).Select(Satir)) + (digerleri.Count > 10 ? $"\n  … ve {digerleri.Count - 10} paylaşım daha" : ""), "Her paylaşımın izinlerini kontrol edin: \"Everyone / Herkes\" yerine yalnızca gereken kişilere, mümkünse yalnızca \"Okuma\" izni verin. Görmek için PowerShell: Get-SmbShareAccess -Name \"<paylaşım adı>\""));
        // 4) Gizlenmiş paylaşım.
        if (gizli.Count > 0) sonuc.Add(new Bulgu("Paylaşım", "Adı gizlenmiş ($ ile biten) paylaşım var", Onem.Orta, "Şu paylaşımlar ağ listesinde görünmez ama adı bilen herkes erişmeyi deneyebilir:\n" + string.Join("\n", gizli.Select(Satir)), "Tanımıyorsanız kaldırın. Gizlemek bir koruma değildir; asıl koruma erişim izinleridir."));
        // 5) Hiç kullanıcı paylaşımı yoksa olumlu not.
        if (kullanici.Count + gizli.Count == 0) sonuc.Add(new Bulgu("Paylaşım", "Ağa açık klasör paylaşımı yok", Onem.Bilgi, $"{liste.Count} Windows yönetimsel paylaşımı (C$, ADMIN$ gibi) var; bunlar normaldir ve yalnızca yönetici hesaplarına açıktır. Başka paylaşım yok.", "Yapılacak bir şey yok."));
        // Sonucu ver.
        return sonuc;
    }

    // Windows'tan paylaşım listesini okur (yalnızca okuma) ve değerlendirir.
    public static List<Bulgu> Denetle()
    {
        // Ham kayıtlar.
        var kayitlar = new List<PaylasimKaydi>();
        // Windows paylaşım tablosunu sorgula.
        using var ara = new ManagementObjectSearcher("SELECT Name, Path, Type, Description FROM Win32_Share");
        // Her kayıt için alanları güvenle oku.
        foreach (ManagementObject m in ara.Get().Cast<ManagementObject>())
            // Tür kodu sayıya çevrilemezse kaydı atla.
            try { kayitlar.Add(new PaylasimKaydi(m["Name"]?.ToString() ?? "", m["Path"]?.ToString() ?? "", Convert.ToUInt32(m["Type"], CultureInfo.InvariantCulture), m["Description"]?.ToString() ?? "")); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "PaylasimDenetimi.Denetle"); }
        // Değerlendir.
        return Degerlendir(kayitlar);
    }
}
