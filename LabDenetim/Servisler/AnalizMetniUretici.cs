// Bu dosya taramanın ayrıntılı Türkçe "yapay zeka danışman" SORU METNİNİ üretir (Claude derin analizi ve elle yapıştırılan prompt için). Eskiden Form1'in YapayZeka.cs partial dosyasındaydı;
// artık form durumundan bağımsız, saf bir servistir: tarama bağlamı AnalizBaglami ile verilir, metin döner (birim testlenir). Bilgisayar adı ve kullanıcı adı metne ASLA girmez.
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Soru metninin ihtiyaç duyduğu tarama bağlamı. Kontroller: "port", "http", "tls", "dns", "yapilandirma", "girdi" anahtarlarından işaretli olanlar.
internal sealed record AnalizBaglami(string Hedef, bool YerelHedef, string Profil, int ToplamKapi, IReadOnlyCollection<string> Kontroller, IReadOnlyList<Bulgu> Bulgular, int Kirmizi, int Sari, int Yesil, int Puan, string Isletim, DateTime Tarih, Func<Bulgu, string> Rozet);

// Soru metni üretici.
internal static class AnalizMetniUretici
{
    public static string Olustur(AnalizBaglami c, Bulgu? tek, bool sohbetIcin)
    {
        // Metni biriktirecek araç.
        var sb = new StringBuilder();
        // Sohbet ekranına yapıştırılacaksa kısa bir tanıtım ekle.
        if (sohbetIcin) sb.AppendLine("Merhaba! Ben bilgisayardan pek anlamayan biriyim. KENDİ izole test ortamımda (kendi bilgisayarım ve/veya kendi sanal makinem) bir güvenlik denetimi yaptım ve aşağıdaki sonuçları aldım. Lütfen deneyimli bir siber güvenlik uzmanı gibi, ama hiç bilgisi olmayan birine anlatır gibi sade Türkçe ile yardım et.\n");
        // Başlık.
        sb.AppendLine("== TARAMA ÖZETİ ==");
        // Hedef.
        sb.AppendLine($"- Hedef adres: {c.Hedef} ({(c.YerelHedef ? "bu bilgisayarın kendisi, Windows" : "ağdaki başka bir (sanal) makine, büyük olasılıkla Linux/Kali")})");
        // Programın çalıştığı sistem (bilgisayar/kullanıcı adı eklenmez).
        sb.AppendLine($"- Denetimi yapan sistem: {c.Isletim}");
        // Tarih.
        sb.AppendLine($"- Tarama tarihi: {c.Tarih:dd.MM.yyyy HH:mm}");
        // Profil adı.
        string profil = c.Profil;
        // Profil ve kapı sayısı.
        sb.AppendLine($"- Tarama profili: {profil}; yoklanan kapı (port) sayısı: {c.ToplamKapi}");
        // Çalıştırılan kontroller.
        var kontroller = new List<string>();
        // Hangi kutucuklar işaretliydi?
        if (c.Kontroller.Contains("port")) kontroller.Add("kapı yoklama"); if (c.Kontroller.Contains("http")) kontroller.Add("web sitesi korumaları"); if (c.Kontroller.Contains("tls")) kontroller.Add("şifreli bağlantı (TLS)");
        // Kalan kontroller.
        if (c.Kontroller.Contains("dns")) kontroller.Add("isim kayıtları (DNS)"); if (c.Kontroller.Contains("yapilandirma")) kontroller.Add("zayıf ayarlar"); if (c.Kontroller.Contains("girdi")) kontroller.Add("form kutusu testi");
        // Kontrolleri yaz.
        sb.AppendLine($"- Çalıştırılan kontroller: {string.Join(", ", kontroller)}");
        // Açık portları bulgulardan topla.
        var acik = c.Bulgular.Where(b => b.Modul == "Port").Select(b => b.Baslik.Replace("Açık port ", "")).ToList();
        // Açık portları yaz.
        sb.AppendLine($"- Açık portlar: {(acik.Count > 0 ? string.Join(", ", acik) : "hiçbiri bulunmadı")}");
        // Yapılamayan kontrol sayısı.
        int atlanan = c.Bulgular.Count(x => x.Atlandi);
        // Sayılar ve puan.
        sb.AppendLine($"- Sonuçlar: KRİTİK {c.Kirmizi}, UYARI/DİKKAT {c.Sari}, GÜVENLİ {c.Yesil}, yapılamayan kontrol {atlanan}. Güvenlik puanı: {c.Puan}/100.");
        // Tek bir bulguya odaklanılıyorsa onu ayrıca belirt.
        if (tek != null)
        {
            // Bölüm başlığı.
            sb.AppendLine().AppendLine("== ÖZELLİKLE ODAKLANILACAK BULGU ==");
            // Bulgunun ayrıntıları.
            sb.AppendLine($"[{c.Rozet(tek)}] {tek.Baslik} — Kontrol: {tek.Modul} — Ayrıntı: {tek.Detay.Replace("\n", " ")} — Programın önerisi: {tek.Oneri}");
        }
        // Tüm bulgular.
        sb.AppendLine().AppendLine("== TÜM BULGULAR ==");
        // Sıra numarası.
        int n = 1;
        // Her bulguyu bir satıra yaz.
        foreach (var b in c.Bulgular) sb.AppendLine($"{n++}. [{c.Rozet(b)}] {b.Baslik} — Kontrol: {b.Modul} — Ayrıntı: {b.Detay.Replace("\n", " ")} — Programın önerisi: {b.Oneri}");
        // İstekler bölümü.
        sb.AppendLine().AppendLine("== İSTEKLERİM ==");
        // Maddeler.
        sb.AppendLine("1. Her bulguyu, bilgisayardan hiç anlamayan birine anlatır gibi sade Türkçe ile açıkla (günlük hayattan benzetmelerle).");
        // Risk sırası.
        sb.AppendLine("2. Bulguları risk sırasına göre (en tehlikeli önce) sırala ve neden o sırada olduğunu söyle.");
        // Komutlar.
        sb.AppendLine("3. Her bulgu için kapatma/savunma adımlarını ve hazır komutları ver: Windows (Güvenlik Duvarı / PowerShell) ve Linux (Kali/Ubuntu) için ayrı ayrı.");
        // Geri alma.
        sb.AppendLine("4. Her komutun nasıl geri alınacağını da yaz.");
        // Yan etkiler.
        sb.AppendLine("5. Komutları çalıştırmadan önce dikkat edilmesi gereken yan etkileri belirt (örneğin hangi hizmet çalışmaz olur).");
        // Kontrol listesi.
        sb.AppendLine("6. Sonunda yapılacakları sıralayan kısa bir kontrol listesi ver.");
        // Savunma notu.
        sb.AppendLine("Not: Bu bir SAVUNMA amaçlı denetimdir; yalnızca kapatma/koruma önerileri ver, saldırı veya sömürü talimatı verme.");
        // Hazır metni ver.
        return sb.ToString();
    }
}
