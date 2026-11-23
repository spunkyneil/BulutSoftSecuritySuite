// KVKK VERİ İHLALİ MÜDAHALE PLANI ŞABLONU: Kişisel Verileri Koruma Kurulu'nun 24.01.2019 tarih ve 2019/10 sayılı Kararı gereği ihlal, öğrenildiği tarihten itibaren gecikmeksizin ve en geç 72 saat içinde Kurul'a bildirilir;
// ilgili kişilere makul en kısa sürede bildirim yapılır; ihlale ilişkin bilgi, etki ve alınan önlemler kayıt altına alınır ve Kurul incelemesine hazır tutulur. Kaynak: kvkk.gov.tr/Icerik/5362/Veri-Ihlali-Bildirimi.
// Bu dosya HUKUKİ GÖRÜŞ DEĞİLDİR; kurumun kendi sorumluları ve hukuk müşaviri tarafından doldurulacak bir çalışma şablonudur. Adli kanıt paketine eklenir.

// Programımızın adı.
namespace LabDenetim;

// İhlal müdahale planı metni.
internal static class KvkkIhlalPlani
{
    // Şablon metni (boşluklar kurum tarafından doldurulur).
    public static string Metin(DateTime an) => string.Join("\r\n", new[]
    {
        "KVKK — KİŞİSEL VERİ İHLALİ MÜDAHALE PLANI (ŞABLON)",
        new string('=', 60),
        "Hazırlanma tarihi: " + an.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " (BulutSoft Security Suite adli kanıt paketi ile birlikte üretildi)",
        "Dayanak: KVKK Kurul Kararı 2019/10 (24.01.2019) ve 6698 sayılı Kanun m.12/5; Kişisel Veri Güvenliği Rehberi (Nisan 2025).",
        "UYARI: Bu bir çalışma şablonudur, hukuki görüş değildir. Kurumunuzun veri sorumlusu ve hukuk müşaviriyle doldurup onaylayın.",
        "",
        "1) SÜRE — 72 SAAT",
        "   - İhlalin ÖĞRENİLDİĞİ tarih/saat: ____________________",
        "   - Kurul'a bildirim için son an (öğrenmeden itibaren 72 saat): ____________________",
        "   - 72 saat içinde bildirilemiyorsa gecikmenin haklı gerekçesi bildirimle birlikte Kurul'a açıklanır.",
        "",
        "2) SORUMLULAR",
        "   - Müdahale yöneticisi: ____________________  - Kurul'a bildirimi yapacak yetkili: ____________________",
        "   - Hukuk/uyum sorumlusu: ____________________  - İlgili kişilere iletişim sorumlusu: ____________________",
        "   - Veri işleyen (varsa) ihlali gecikmeden veri sorumlusuna bildirmekle yükümlüdür; sözleşmede yazılı olmalıdır.",
        "",
        "3) İLK 24 SAAT — TESPİT VE KAYIT",
        "   [ ] Olay günlüğünü ve bu kanıt paketini (.zip, SHA-256 ve varsa .hmac/.tsr) değiştirmeden saklayın.",
        "   [ ] Hangi kişisel veriler, kaç kişi, hangi sistemlerde etkilendi? (Kategori ve yaklaşık sayı)",
        "   [ ] Zararın sürmesini önleyin (hesap/erişim kapatma, ağ izolasyonu); yapılan her işlemi saat damgasıyla yazın.",
        "",
        "4) KURUL BİLDİRİMİ",
        "   [ ] \"Kişisel Veri İhlal Bildirim Formu\" (kvkk.gov.tr) yalnızca veri sorumlusunca doldurulur.",
        "   [ ] Bilgiler aynı anda sağlanamıyorsa gecikmeden, aşamalı olarak iletilir.",
        "",
        "5) İLGİLİ KİŞİLERE BİLDİRİM",
        "   [ ] Etkilenen kişiler belirlendikten sonra makul en kısa sürede: iletişim adresi varsa doğrudan, yoksa web sitesi vb. uygun yöntemle.",
        "",
        "6) KAYIT VE İYİLEŞTİRME",
        "   [ ] İhlalin bilgileri, etkileri ve alınan önlemler kayıt altına alınır; Kurul incelemesine hazır tutulur.",
        "   [ ] Kök neden, düzeltici tedbirler ve planın gözden geçirme tarihi: ____________________",
    });
}
