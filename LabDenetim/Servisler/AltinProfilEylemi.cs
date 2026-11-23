// "Altın Profil" (güvenli referans) özelliğinin arayüz tarafı: tarama bitince referansla kıyaslar, durum yazısını günceller, "kilitle / güncelle / kaldır" düğmesini işletir.
// Eskiden Form1'in AltinProfil.cs partial dosyasındaydı; kayıt deposu (AltinProfilDeposu) aynı dosyada kalır.

// Programımızın adı.
namespace LabDenetim;

// Altın Profil arayüz eylemleri.
internal sealed class AltinProfilEylemi
{
    private readonly ITaramaBaglami _b; private readonly Label _lblDurum;

    // lblDurum: düğmenin yanındaki durum yazısı.
    public AltinProfilEylemi(ITaramaBaglami baglam, Label lblDurum) { _b = baglam; _lblDurum = lblDurum; }

    // Tarama bittiğinde altın profille kıyaslar; sapma varsa uyarı satırı ekler.
    public void Karsilastir()
    {
        // Kayıtlı altın profil yoksa yapacak bir şey yok.
        var altin = AltinProfilDeposu.Yukle();
        // Profil yoksa çık.
        if (altin == null) return;
        // Profil başka bir hedef içinse kıyaslama anlamsızdır.
        if (!string.Equals(altin.Hedef, _b.Hedef, StringComparison.OrdinalIgnoreCase)) { _b.Gunluk($"ALTIN PROFİL -> Kilitli profil başka bir hedef ({altin.Hedef}) için; bu tarama kıyaslanmadı.", TaramaRenkleri.Elektrik); return; }
        // Farkları bul.
        var f = AltinProfilDeposu.Karsilastir(altin, _b.Bulgular);
        // Yeni kapı varsa uyar (istenen cümle biçimi).
        if (f.YeniKapilar.Count > 0)
            _b.BulguEkle(new Bulgu("Referans", $"Temiz referans profilinize göre sisteminizde {f.YeniKapilar.Count} yeni kapı açılmış!", Onem.Yuksek,
                $"Altın Profil ({altin.Tarih:dd.MM.yyyy HH:mm}) temiz kabul edilen durumdu. O günden beri açılan kapılar: {string.Join(", ", f.YeniKapilar)}", "Bu kapıları siz açmadıysanız ilgili satırlara tıklayıp Otomatik Onar ile kapatın; hangi programın açtığını asistan gösterir."));
        // Yeni kapı yok ama başka yeni sorun varsa yine uyar.
        else if (f.YeniSorunlar.Count > 0)
            _b.BulguEkle(new Bulgu("Referans", $"Temiz referans profilinize göre {f.YeniSorunlar.Count} yeni sorun çıkmış!", Onem.Orta,
                $"Altın Profil ({altin.Tarih:dd.MM.yyyy HH:mm}) ile karşılaştırıldı. Yeni sorunlar:\n" + string.Join("\n", f.YeniSorunlar.Take(8).Select(s => "  • " + s.Split('|', 2)[1])), "Yeni çıkan satırları inceleyin; yaptığınız bir değişiklik olabilir."));
        // Hiç sapma yoksa bilgi notu.
        else
            _b.BulguEkle(new Bulgu("Referans", "Altın Profil ile uyumlu: sapma yok", Onem.Bilgi, $"Altın Profil ({altin.Tarih:dd.MM.yyyy HH:mm}) ile aynı açık kapılar ve sorunlar.", "Yapılacak bir şey yok."));
    }

    // Düğmenin altındaki durum yazısını günceller.
    public void DurumYaz()
    {
        // Altın profil bilgisi.
        var a = AltinProfilDeposu.Yukle();
        // Durum yazısı.
        _lblDurum.Text = a == null ? "Henüz kilitli referans yok. Sisteminizi temizleyip tarama yaptıktan sonra bu düğmeyle kilitleyin." : $"Kilitli: {a.Tarih:dd.MM.yyyy HH:mm} · {a.Hedef} · puan {a.Puan}/100 · {a.Kapilar.Count} açık kapı";
        // Kilitliyse yazı yeşil, değilse silik.
        _lblDurum.ForeColor = a == null ? DesignTokens.Ozgun(TokenRol.TextMuted) : TaramaRenkleri.Yesil;
    }

    // "Bu Durumu Güvenli Referans (Altın Profil) Olarak Kilitle" düğmesi.
    public void Tikla()
    {
        // Tarama sürerken kilitleme yapılmasın.
        if (_b.TaramaSuruyor) { TemaliMesaj.Goster("Şu an bir tarama sürüyor. Bitmesini bekleyin.", "Biraz bekleyin", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Ekranda sonuç yoksa kilitlenecek bir şey yok.
        if (_b.Bulgular.Count == 0) { TemaliMesaj.Goster("Önce \"⚡ TARAMAYI BAŞLAT\" ile bir tarama yapın; temiz sonucu aldığınızda bu düğmeyle kilitleyin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Zaten kilitli bir profil varsa: güncelle / kaldır / vazgeç.
        var mevcut = AltinProfilDeposu.Yukle();
        // Kilitli profil var.
        if (mevcut != null)
        {
            // Üç seçenek sun.
            var s = TemaliMesaj.Goster($"Zaten kilitli bir Altın Profil var ({mevcut.Tarih:dd.MM.yyyy HH:mm}).\r\n\r\nEvet: Ekrandaki sonuçla YENİDEN kilitle (eskisi üzerine yazılır).\r\nHayır: Kilitli profili KALDIR.\r\nİptal: Hiçbir şey yapma.", "Altın Profil", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button3);
            // Kaldır.
            if (s == DialogResult.No) { AltinProfilDeposu.Sil(); DurumYaz(); _b.Durum("Altın Profil kaldırıldı."); return; }
            // Vazgeç.
            if (s != DialogResult.Yes) return;
        }
        // Ekrandaki sonuçlarda kritik varsa "temiz" sayılmaz: ayrıca sor (varsayılan Hayır).
        if (_b.Kirmizi > 0 && TemaliMesaj.Goster($"Ekrandaki sonuçlarda {_b.Kirmizi} KRİTİK sorun var; bu durum \"temiz\" sayılmaz.\r\nYine de referans olarak kilitlensin mi?", "Temiz değil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Kaydetmeyi dene.
        try
        {
            // Kilitle (zincir/referans uyarıları dışarıda tutulur).
            var k = AltinProfilDeposu.Kilitle(_b.Hedef, _b.Puan, _b.Bulgular);
            // Durum yazısını yenile.
            DurumYaz();
            // Günlüğe ve durum çubuğuna yaz.
            _b.Durum($"Altın Profil kilitlendi: {k.Kapilar.Count} açık kapı, puan {k.Puan}/100. Sonraki taramalar bununla kıyaslanacak.", true, TaramaRenkleri.Yesil);
        }
        // Yazılamazsa (disk/izin) göster.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Kilitlenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
