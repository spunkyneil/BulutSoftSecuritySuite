// Yardım menüsü eylemleri: "Yardım" metni, "Hakkımızda & Destek" penceresi ve "Tanıtım Turu" (ilk açılışta bir kez, sonra menüden). Eskiden Form1'in Menu.cs ve Bilgi.cs partial dosyalarındaydı;
// artık ana pencerenin yalnızca IWin32Window sahibi ve Hakkımızda kapandıktan sonra çalışacak bir eylem verilir.

// Programımızın adı.
namespace LabDenetim;

// Yardım eylemleri.
internal sealed class YardimEylemleri
{
    private readonly IWin32Window _sahip; private readonly Action _hakkindaKapaninca;

    // hakkindaKapaninca: bilgi bankası yenilenmiş olabileceği için ekrandaki açıklamayı tazeler.
    public YardimEylemleri(IWin32Window sahip, Action hakkindaKapaninca) { _sahip = sahip; _hakkindaKapaninca = hakkindaKapaninca; }

    // Tanıtım turunun daha önce gösterilip gösterilmediğini hatırlayan küçük dosyanın yeri.
    internal static string TurIsaretYolu => Path.Combine(BrandConfig.RoamingKlasor, "tur_gorulduu.txt");

    // Menü: "Yardım" — programın ne işe yaradığını sade dille anlatır.
    public void Yardim() =>
        TemaliMesaj.Goster(
            "BU PROGRAM NE İŞE YARAR?\r\nKendi sanal bilgisayarınızın (test ortamınızın) güvenlik açıklarını arar. Evinizin kapılarını, pencerelerini ve kilitlerini tek tek yoklayıp \"şurası açık kalmış\" diye listeler. Hiçbir şeye zarar vermez.\r\n\r\n" +
            "NASIL KULLANIRIM?\r\n1) \"Hedef adres\" kutusuna kontrol edilecek bilgisayarın adresini yazın (hazır: 127.0.0.1, yani kendi bilgisayarınız).\r\n2) \"⚡ TARAMAYI BAŞLAT\" butonuna basın.\r\n3) \"◼ Panel\" sekmesindeki Canlı Ağ ve Port Radarı'nda bulunan açık kapılar neon düğümler olarak belirir (yeşil güvenli, sarı dikkat, kırmızı kritik). Bir düğüme tıklayınca ayrıntısına gidersiniz.\r\n4) \"⚡ Yeni Tarama\" sekmesindeki tabloda sonuçlar renkli rozetlerle çıkar: GÜVENLİ normal, UYARI/DİKKAT dikkat, KRİTİK tehlike. Üstteki renkli kutulara tıklayarak yalnızca o seviyeyi görebilirsiniz.\r\n5) Bir satıra tıklayınca sağdaki Siber İlk Yardım Asistanı ne demek olduğunu, nasıl çözeceğinizi, hazır çözüm komutunu ve (bu bilgisayarı tarıyorsanız) kapıyı hangi programın açtığını gösterir.\r\n6) \"Rapor İndir\" butonlarıyla raporu kaydedin.\r\n\r\n" +
            "GÜVENLİK: Program yalnızca kendi ağınızdaki adreslere (127..., 192.168..., 10... ile başlayanlar) çalışır; internetteki başkalarının bilgisayarlarına bakamaz.",
            "Yardım", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // Menü: "Hakkımızda & Destek" — geliştirici, proje bilgisi, e-posta ve güvenlik ilkesi penceresini açar.
    public void HakkindaAc()
    {
        using var pencere = new HakkindaFormu(); pencere.ShowDialog(_sahip);
        _hakkindaKapaninca();
    }

    // İlk açılışta tanıtım turunu bir kez gösterir (daha önce gösterildiyse göstermez).
    public void TurIlkAcilista()
    {
        // Dosya okunamazsa turu hiç göstermeden devam et (program bozulmasın).
        try { if (File.Exists(TurIsaretYolu)) return; } catch (IOException) { return; }
        TurAc();
        // Gösterildi diye işaret bırak; yazılamazsa sessizce geç.
        try { Depo.Yaz(TurIsaretYolu, "1"); }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "YardimEylemleri.TurIlkAcilista", true); }
    }

    // Tanıtım turunu pencere olarak açar (menüden de bu çağrılır).
    public void TurAc() { using var tur = new TurFormu(); tur.ShowDialog(_sahip); }
}
