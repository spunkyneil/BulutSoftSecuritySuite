// Bu dosya KULLANICI MENÜSÜ EYLEMLERİNİN (Hesabım ve Kullanıcılar, Lisans, Kullanıcı Değiştir) ve pencere başlığındaki oturum/lisans bilgisinin ev sahibidir. Eskiden Form1'in iki partial dosyasındaydı;
// artık Form1'den bağımsız, yalnızca ihtiyaç duyduğu üç şeyi (pencere, "tarama sürüyor mu", günlük) alır. Yetki denetimi KabukYetki ile yapılır (eski Form1.YetkiKontrol kalktı).

// Programımızın adı.
namespace LabDenetim;

// Oturum ve lisans eylemleri.
internal sealed class OturumEylemleri
{
    private readonly Form _sahip; private readonly Func<bool> _taramaSuruyor; private readonly Action<string> _gunluk; private readonly string _temelBaslik;

    // sahip: ana pencere; taramaSuruyor: çıkış uyarısı için; gunluk: lisans penceresinin canlı günlüğü; temelBaslik: pencere başlığının sabit kısmı.
    public OturumEylemleri(Form sahip, Func<bool> taramaSuruyor, Action<string> gunluk, string temelBaslik) { _sahip = sahip; _taramaSuruyor = taramaSuruyor; _gunluk = gunluk; _temelBaslik = temelBaslik; }

    // "Hesabım ve Kullanıcılar" penceresini açar.
    public void HesapAc() { using var p = new KullaniciFormu(); p.ShowDialog(_sahip); }

    // "Lisans" penceresini açar (herkes durumu görebilir) ve başlığı yeniler.
    public void LisansAc() { using var p = new LisansFormu(_gunluk); p.ShowDialog(_sahip); BaslikYaz(); }

    // Pencere başlığına kullanıcıyı ve lisans durumunu yazar.
    public void BaslikYaz()
    {
        try { string t = _temelBaslik; if (ServisBaglami.Oturum.Aktif != null) t += $"   —   {ServisBaglami.Oturum.Aktif.Ad} ({Yetki.RolAdi(ServisBaglami.Oturum.Aktif.Rol)})"; _sahip.Text = t + "   —   " + Lisans.Kisa(Lisans.Durum()); }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "OturumEylemleri.BaslikYaz"); }
    }

    // "Kullanıcı Değiştir": oturumu kapatıp giriş ekranına döner (program yeni süreç olarak yeniden başlar).
    public void OturumDegistir()
    {
        string uyari = _taramaSuruyor() ? "\r\n\r\nDİKKAT: Şu an bir tarama sürüyor; çıkış yaparsanız yarıda kalır." : "";
        if (TemaliMesaj.Goster($"Oturum kapatılsın ve başka bir kullanıcıyla giriş ekranına dönülsün mü?\r\n\r\nProgram kısa süre için kapanıp giriş ekranıyla yeniden açılır.{uyari}", "Kullanıcı Değiştir", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try { ServisBaglami.OlayYazici.Ekle("Oturum", "Bilgi", $"OTURUM -> Oturum kapatıldı: {ServisBaglami.Oturum.Aktif?.Ad ?? "?"}"); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "OturumEylemleri.OturumDegistir"); }
        ServisBaglami.Oturum.Aktif = null;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--oturum-degisti") { UseShellExecute = false }); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "OturumEylemleri.OturumDegistir", true); }
        Application.Exit();
    }
}
