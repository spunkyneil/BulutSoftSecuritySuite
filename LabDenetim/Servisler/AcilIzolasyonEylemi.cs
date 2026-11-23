// Acil İzolasyon: yüksek riskli gelen kapıları (21, 23, 135, 139, 445, 3389, 5900) Windows Güvenlik Duvarı'nda tek komutla engeller/kaldırır; durumu Windows'a sorup doğrular.
// Eskiden Form1'in Kalkan.cs partial dosyasındaydı; durum SavunmaDurumServisi'nde (olay tabanlı) tutulur.

// Programımızın adı.
namespace LabDenetim;

// Acil izolasyon eylemi.
internal sealed class AcilIzolasyonEylemi
{
    // Acil izolasyonda engellenen yüksek riskli gelen kapılar.
    private static readonly int[] AcilPortlar = { 21, 23, 135, 139, 445, 3389, 5900 };
    private readonly ITaramaBaglami _b; private readonly SavunmaDurumServisi _savunma;

    public AcilIzolasyonEylemi(ITaramaBaglami baglam, SavunmaDurumServisi savunma) { _b = baglam; _savunma = savunma; }

    // Windows'a sorup Acil İzolasyon kurallarının şu an etkin olup olmadığını öğrenir ve durumu servise yazar.
    public void Yenile()
    {
        // Güvenlik duvarındaki BulutSoft kuralları; yedi kapının hepsi için etkin bir "engelle" kuralı varsa izolasyon açıktır.
        var kurallar = GuvenlikDuvari.Listele();
        _savunma.AcilAyarla(AcilPortlar.All(p => kurallar.Any(k => k.Ad == $"BulutSoft-Acil-{p}" && k.Etkin && k.Engel)));
    }

    // Menü: "Acil İzolasyon" / "İzolasyonu Kaldır" — onaydan sonra tek komut zinciriyle kuralları ekler veya siler.
    public void Degistir() => GuvenliCalistir.Baslat(() => DegistirAsync(), "AcilIzolasyonEylemi.Degistir");

    private async Task DegistirAsync()
    {
        // Yetki kontrolü: bu işlem için rolünüz yeterli mi?
        if (!KabukYetki.Kontrol(_b.Sahip, Eylem.SistemDegistir, "Acil İzolasyon")) return;
        // Çift tıklamaya karşı geçici olarak kilitle.
        _savunma.AcilMesgulAyarla(true);
        // Hata olsa da düğme geri açılsın.
        try
        {
            // Açık değilse izolasyonu başlat.
            if (!_savunma.AcilAktif)
            {
                // Ne olacağını açıkça anlatıp onay iste (varsayılan: Hayır).
                var onay = TemaliMesaj.Goster("ACİL İZOLASYON\r\n\r\nWindows Güvenlik Duvarı'na şu yüksek riskli GELEN kapıları ENGELLEYEN kurallar eklenecek:\r\n\r\n  21 (FTP), 23 (Telnet), 135 (RPC), 139 (NetBIOS), 445 (SMB), 3389 (Uzak Masaüstü), 5900 (VNC)\r\n\r\nBu kapıları kullanan hizmetlere (dosya paylaşımı, uzak masaüstü vb.) başka bilgisayarlardan erişilemez olacak. İnternet kullanımınız ve bu bilgisayardan dışarı yaptığınız bağlantılar etkilenmez.\r\nWindows sizden yönetici izni (UAC penceresi) isteyecek. Geri almak için aynı düğme \"✓ İzolasyonu Kaldır\" olacak.\r\n\r\nDevam edilsin mi?", "Acil İzolasyon - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                // Onaylamadıysa hiçbir şey yapma.
                if (onay != DialogResult.Yes) { KullaniciGunlugu.Yaz("Acil İzolasyon'u iptal etti (değişiklik yapılmadı)"); _b.Durum("Acil izolasyon iptal edildi; hiçbir değişiklik yapılmadı."); return; }
                KullaniciGunlugu.Yaz("Acil İzolasyon'u onayladı");
                // Günlüğe yaz.
                _b.Gunluk("ACİL İZOLASYON -> Yüksek riskli gelen kapılar engelleniyor...", DesignTokens.Ozgun(TokenRol.StatusDanger));
                // Yedi kuralı tek komut zinciriyle, tek yönetici izniyle ekle.
                var (tamam, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(GuvenlikDuvari.EkleZinciri(AcilPortlar, "BulutSoft-Acil-"));
                // İzin verilmediyse bildir ve dur.
                if (!tamam) { _b.Gunluk("ACİL İZOLASYON -> " + hata, TaramaRenkleri.Simsek); _b.Durum(hata, false); return; }
                // Gerçekten eklendi mi diye Windows'a yeniden sor.
                Yenile();
                // Sonucu günlüğe ve mesaj kutusuna yaz.
                if (_savunma.AcilAktif) { _b.Gunluk($"[DOĞRULANDI] ACİL İZOLASYON AKTİF -> {AcilPortlar.Length} kapı engellendi (BulutSoft-Acil-*).", TaramaRenkleri.Yesil); _b.Durum("Acil izolasyon AKTİF: yüksek riskli gelen kapılar engellendi.", false); TemaliMesaj.Goster($"Acil izolasyon aktif: {string.Join(", ", AcilPortlar)} numaralı gelen kapılar engellendi.\r\n\r\nKaldırmak için aynı düğmeye (\"✓ İzolasyonu Kaldır\") basın veya \"✦ Aktif Kalkanlarım\" penceresini kullanın.", "Acil İzolasyon Aktif", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                // Doğrulanamadıysa dürüstçe söyle.
                else { _b.Gunluk("ACİL İZOLASYON -> Kurallar doğrulanamadı.", DesignTokens.Ozgun(TokenRol.StatusWarning)); TemaliMesaj.Goster("Komut çalıştırıldı ama kuralların tamamı doğrulanamadı. \"✦ Aktif Kalkanlarım\" penceresinden kontrol edin.", "Doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            // Açıksa izolasyonu kaldır.
            else
            {
                // Onay iste (varsayılan: Hayır).
                var onay = TemaliMesaj.Goster("Acil izolasyon kuralları (BulutSoft-Acil-*) Windows Güvenlik Duvarı'ndan KALDIRILACAK.\r\n\r\nSonuç: 21, 23, 135, 139, 445, 3389 ve 5900 numaralı kapılara gelen bağlantılar artık bu kurallarla engellenmeyecek.\r\nWindows sizden yönetici izni (UAC penceresi) isteyecek.\r\n\r\nDevam edilsin mi?", "İzolasyonu Kaldır - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                // Onaylamadıysa hiçbir şey yapma.
                if (onay != DialogResult.Yes) { KullaniciGunlugu.Yaz("Acil İzolasyon'u kaldırmaktan vazgeçti"); _b.Durum("İzolasyon kaldırma iptal edildi; kurallar yerinde."); return; }
                KullaniciGunlugu.Yaz("Acil İzolasyon'u kaldırdı");
                // Günlüğe yaz.
                _b.Gunluk("ACİL İZOLASYON -> Kurallar kaldırılıyor...", TaramaRenkleri.Elektrik);
                // Acil kuralların adları.
                var adlar = AcilPortlar.Select(p => $"BulutSoft-Acil-{p}");
                // Tek komut zinciriyle, tek yönetici izniyle sil.
                var (tamam, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(GuvenlikDuvari.SilZinciri(adlar));
                // İzin verilmediyse bildir ve dur.
                if (!tamam) { _b.Gunluk("ACİL İZOLASYON -> " + hata, TaramaRenkleri.Simsek); _b.Durum(hata, false); return; }
                // Gerçekten silindi mi diye Windows'a yeniden sor.
                Yenile();
                // Hiç acil kural kalmadıysa başarılı.
                bool kalan = GuvenlikDuvari.Listele().Any(k => k.Ad.StartsWith("BulutSoft-Acil-", StringComparison.Ordinal));
                // Sonucu yaz.
                if (!kalan) { _b.Gunluk("[DOĞRULANDI] ACİL İZOLASYON KALDIRILDI -> Acil kurallar temizlendi.", TaramaRenkleri.Yesil); _b.Durum("Acil izolasyon kaldırıldı.", false); TemaliMesaj.Goster("Acil izolasyon kaldırıldı; kurallar temizlendi.", "Tamam", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                // Kalan kural varsa söyle.
                else { _b.Gunluk("ACİL İZOLASYON -> Bazı kurallar kaldırılamadı.", DesignTokens.Ozgun(TokenRol.StatusWarning)); TemaliMesaj.Goster("Bazı acil kurallar hâlâ görünüyor. \"✦ Aktif Kalkanlarım\" penceresinden tek tek kaldırabilirsiniz.", "Tamamlanmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }
        // Düğmeyi tekrar kullanılabilir yap.
        finally { _savunma.AcilMesgulAyarla(false); }
    }
}
