// Bu dosya her bulguyu KVKK ve ISO/IEC 27001 maddeleriyle eşleştirir (tabloda, HTML/PDF raporda ve SIEM çıktısında görünür).
// KVKK: 6698 sayılı Kanun m.12 "Veri güvenliğine ilişkin yükümlülükler" (yetkisiz erişimin önlenmesi, teknik/idari tedbirler, denetim).
// ISO/IEC 27001:2022 Ek A kontrolleri kullanılır; eski 2013 sürümünün karşılığı (biliniyorsa) parantez içinde verilir.
// ÖNEMLİ: Eşleştirme REHBER niteliğindedir; "bu bulgu şu kontrolün eksikliğine işaret edebilir" der. Hukuki/denetçi görüşü yerine geçmez.
namespace LabDenetim;

// Bir bulgunun uyumluluk etiketi.
internal sealed record UyumEtiketi(string KvkkKisa, string KvkkAciklama, List<string> IsoKodlari);

// Eşleştirme mantığı.
internal static class Uyumluluk
{
    // Eşleştirmenin dayandığı resmî kaynak: KVKK "Kişisel Veri Güvenliği Rehberi" (KVKK Yayınları No: 72, Nisan 2025), Tablo 4.1 "Teknik Tedbirler" (kvkk.gov.tr).
    public const string Kaynak = "KVKK Kişisel Veri Güvenliği Rehberi (Nisan 2025), Tablo 4.1 Teknik Tedbirler; 6698 sayılı Kanun m.12";

    // Bulgu modülü → rehberdeki teknik tedbir başlığı (Tablo 4.1: Yetki Kontrol, Erişim Logları, Kullanıcı Hesap Yönetimi, Ağ Güvenliği, Uygulama Güvenliği, Şifreleme, Sızma Testi,
    // Saldırı Tespit ve Önleme Sistemleri, Log Kayıtları, Veri Maskeleme, Yedekleme, Güvenlik Duvarları, Güncel Anti-Virüs Sistemleri, Anahtar Yönetimi...).
    public static string? RehberTedbiri(Bulgu b) => b.Modul switch
    {
        "Port" or "Wi-Fi" or "DNS" or "Hosts" => "Ağ Güvenliği",
        "HTTP" or "Girdi" or "Yapılandırma" => "Uygulama Güvenliği",
        "TLS" => "Şifreleme",
        "Hesap" => "Kullanıcı Hesap Yönetimi",
        "Kalıcılık" or "Referans" or "Zincir" => "Saldırı Tespit ve Önleme Sistemleri",
        "Karne" => b.Baslik.Contains("duvar", StringComparison.OrdinalIgnoreCase) || b.Detay.Contains("firewall", StringComparison.OrdinalIgnoreCase) ? "Güvenlik Duvarları" : "Güncel Anti-Virüs Sistemleri",
        _ => null
    };

    // Raporlara konan uyarı metni.
    public const string Not = "Uyumluluk eşleştirmesi rehber niteliğindedir: bulgunun ilgili KVKK maddesi veya ISO/IEC 27001 kontrolüyle ilişkili bir eksikliğe işaret edebileceğini gösterir. Hukuki görüş veya bağımsız denetim yerine geçmez.";

    // ISO/IEC 27001:2022 Ek A kontrolleri: kod → (Türkçe ad, 2013 karşılığı).
    public static readonly Dictionary<string, (string Ad, string Eski)> Iso = new()
    {
        ["A.5.15"] = ("Erişim Kontrolü", "A.9.1.1"),
        ["A.5.17"] = ("Kimlik Doğrulama Bilgisi", "A.9.4.3"),
        ["A.8.2"] = ("Ayrıcalıklı Erişim Hakları", "A.9.2.3"),
        ["A.8.5"] = ("Güvenli Kimlik Doğrulama", "A.9.4.2"),
        ["A.8.7"] = ("Kötü Amaçlı Yazılımdan Korunma", "A.12.2.1"),
        ["A.8.8"] = ("Teknik Zafiyetlerin Yönetimi", "A.12.6.1"),
        ["A.8.9"] = ("Yapılandırma Yönetimi", ""),
        ["A.8.15"] = ("Günlük Kaydı", "A.12.4.1"),
        ["A.8.16"] = ("Faaliyetlerin İzlenmesi", ""),
        ["A.8.20"] = ("Ağ Güvenliği", "A.13.1.1"),
        ["A.8.21"] = ("Ağ Hizmetlerinin Güvenliği", "A.13.1.2"),
        ["A.8.24"] = ("Kriptografi Kullanımı", "A.10.1.1"),
        ["A.8.26"] = ("Uygulama Güvenliği Gereksinimleri", "A.14.1.2"),
        ["A.8.28"] = ("Güvenli Kodlama", ""),
        ["A.8.3"] = ("Bilgiye Erişimin Kısıtlanması", "A.9.4.1"),
        ["A.8.4"] = ("Kaynak Koda Erişim", "A.9.4.5"),
        ["A.8.13"] = ("Bilgi Yedekleme", "A.12.3.1"),
        ["A.8.19"] = ("Yazılımın Kurulumu", "A.12.5.1")
    };

    // KVKK m.12 alt başlıkları.
    private static readonly (string Kisa, string Aciklama) K1 = ("KVKK m.12/1", "Kişisel verilere hukuka aykırı erişimin önlenmesi için uygun güvenlik düzeyini sağlayacak teknik tedbirler"), K3 = ("KVKK m.12/3", "Teknik ve idari tedbirlerin alınması ve bunlara yönelik gerekli denetimin yapılması");

    // Metinde anahtar kelimelerden biri geçiyor mu? (büyük/küçük harf fark etmez)
    private static bool Var(string m, params string[] k) { foreach (var a in k) if (m.Contains(a, StringComparison.OrdinalIgnoreCase)) return true; return false; }

    // Bir bulgunun etiketi; sorun olmayan (yeşil) ve atlanan sonuçlar için null.
    public static UyumEtiketi? Bul(Bulgu b)
    {
        // Yalnızca gerçek sorunlar etiketlenir.
        if (b.Onem == Onem.Bilgi || b.Atlandi) return null;
        // Derin denetim (Finding v2) bulguları kendi KVKK/ISO etiketlerini taşır.
        if (V2.V2Kayit.Etiket(b) is { } v2) return new UyumEtiketi(v2.Kvkk.FirstOrDefault() ?? K1.Kisa, K1.Aciklama, v2.Iso27001.Distinct().ToList());
        // Aranacak metin.
        string m = b.Baslik + " " + b.Detay; var iso = new List<string>(); var k = K1;
        // Modüle göre karar ver.
        switch (b.Modul)
        {
            case "Port":
                // Her açık kapı: ağ güvenliği ve ağ hizmetleri.
                iso.Add("A.8.20"); iso.Add("A.8.21");
                // Uzak erişim hizmetleri: erişim kontrolü.
                if (Var(m, "3389", "RDP", "5900", "VNC", "445", "139", "SMB", "SSH")) iso.Add("A.5.15");
                // Şifresiz protokoller: kriptografi.
                if (Var(m, "telnet", "ftp", "http ", "80 ", "23 ", "21 ")) iso.Add("A.8.24");
                break;
            case "HTTP": iso.AddRange(new[] { "A.8.9", "A.8.26" }); break;
            case "TLS": iso.AddRange(new[] { "A.8.24", "A.8.20" }); break;
            case "Girdi": iso.AddRange(new[] { "A.8.26", "A.8.28" }); break;
            case "DNS": iso.AddRange(new[] { "A.8.20", "A.8.9" }); break;
            case "Hosts": iso.AddRange(new[] { "A.8.9", "A.8.16" }); break;
            case "Kalıcılık": iso.AddRange(new[] { "A.8.7", "A.8.9", "A.8.16" }); break;
            case "Karne":
                // Hangi güvenlik kontrolü eksik?
                if (Var(m, "defender", "antivir", "virüs")) iso.Add("A.8.7");
                else if (Var(m, "duvar", "firewall")) iso.Add("A.8.20");
                else if (Var(m, "UAC", "hesap denetimi")) iso.Add("A.8.2");
                else if (Var(m, "SMB", "yama", "güncelleme", "patch")) iso.Add("A.8.8");
                else if (Var(m, "bitlocker", "şifreleme")) iso.Add("A.8.24");
                else if (Var(m, "RDP", "uzak masaüstü")) iso.AddRange(new[] { "A.8.20", "A.5.15" });
                else iso.Add("A.8.9");
                break;
            case "Wi-Fi": iso.AddRange(new[] { "A.8.20", "A.8.24" }); break;
            case "Hesap": iso.AddRange(new[] { "A.5.15", "A.8.5", "A.5.17" }); break;
            case "Yapılandırma": iso.AddRange(new[] { "A.8.9", "A.8.8" }); break;
            // Zincirleme risk ve referans sapması: risk değerlendirme ve denetim.
            case "Zincir": k = K3; iso.AddRange(new[] { "A.8.8", "A.8.20" }); break;
            case "Referans": k = K3; iso.AddRange(new[] { "A.8.9", "A.8.16" }); break;
            // Tanınmayan modül.
            default: return null;
        }
        // Tekrarları ayıkla.
        return new UyumEtiketi(k.Kisa, k.Aciklama, iso.Distinct().ToList());
    }

    // Tablodaki kısa yazı: iki satır (KVKK ve ISO kodları).
    public static string Yazi(Bulgu b) { var e = Bul(b); return e == null ? "" : e.KvkkKisa + "\r\nISO " + string.Join(", ", e.IsoKodlari); }

    // Kodun adı ve eski karşılığıyla uzun yazımı.
    public static string IsoUzun(string kod) => Iso.TryGetValue(kod, out var v) ? $"{kod} {v.Ad}" + (v.Eski.Length > 0 ? $" (2013: {v.Eski})" : "") : kod;

    // Raporlardaki uzun metin satırları (KVKK ve her ISO kontrolü).
    public static List<string> Uzun(Bulgu b)
    {
        // Etiket yoksa boş.
        var e = Bul(b); if (e == null) return new();
        // KVKK satırı ve ISO satırları.
        var l = new List<string> { $"{e.KvkkKisa} — {e.KvkkAciklama}" }; if (RehberTedbiri(b) is { } ted) l.Add("KVKK Rehberi (2025) teknik tedbir: " + ted); l.AddRange(e.IsoKodlari.Select(c => "ISO/IEC 27001:2022 " + IsoUzun(c))); return l;
    }

    // Özet: hangi kontrolde kaç bulgu var (rapor başında tablo olarak gösterilir). Kontrol, ad, adet.
    public static List<(string Kontrol, string Ad, int Adet)> Ozet(IEnumerable<Bulgu> bulgular)
    {
        // Sayaçlar.
        var sayac = new Dictionary<string, int>(); var kvkk = new Dictionary<string, int>();
        // Her bulgu.
        foreach (var b in bulgular) { var e = Bul(b); if (e == null) continue; kvkk[e.KvkkKisa] = kvkk.GetValueOrDefault(e.KvkkKisa) + 1; foreach (var c in e.IsoKodlari) sayac[c] = sayac.GetValueOrDefault(c) + 1; }
        // Önce KVKK, sonra ISO (çok bulgudan aza).
        var l = new List<(string, string, int)>(); foreach (var (kod, n) in kvkk.OrderByDescending(x => x.Value)) l.Add((kod, kod == K1.Kisa ? K1.Aciklama : K3.Aciklama, n));
        foreach (var (kod, n) in sayac.OrderByDescending(x => x.Value).ThenBy(x => x.Key)) l.Add(("ISO " + kod, IsoUzun(kod)[(kod.Length + 1)..], n));
        // Sonuç.
        return l;
    }
}
