// Kolay görünümdeki göstergeler (Kali sensörü, IP sızıntı, bildirim kanalı, koruma hizmeti), Merkez sayfası geçişi ve Kali uyarısı. Eskiden Form1'in MerkezFormu.cs partial'ının sonundaydı;
// artık ayrı sınıf: ana pencereden yalnızca TemaliForm, tarama ekranı, kabuk gezintisi, Kolay panel ve Nöbetçi alır.

// Programımızın adı.
namespace LabDenetim;

// Kali/Merkez göstergeleri.
internal sealed class KaliGostergeleri
{
    private readonly TemaliForm _f; private readonly TaramaSayfasi _tarama; private readonly Action<string, string?, bool> _kabukGit; private readonly Func<KolayModPaneli> _kolay; private readonly NobetciEylemi _nobet; private readonly Action _kolayYenile;
    // kolayYenile: üç göstergeyi baştan çizdiren eylem (kendi Yenile'ni sarar).
    public KaliGostergeleri(TemaliForm f, TaramaSayfasi tarama, Action<string, string?, bool> kabukGit, Func<KolayModPaneli> kolay, NobetciEylemi nobet, Action kolayYenile) { _f = f; _tarama = tarama; _kabukGit = kabukGit; _kolay = kolay; _nobet = nobet; _kolayYenile = kolayYenile; }

    internal readonly System.Windows.Forms.Timer _kaliZaman = new() { Interval = 600_000 }; private bool _kaliUyarildi;
    // Merkez'in açılacağı sekme (Kolay göstergelerden gelen istek).
    internal int _merkezSekme = MerkezFormu.SekmeKali;

    // Merkez sayfasını açar (Kolay gösterge tıklamalarından ve bildirimlerden de çağrılır): Gelişmiş / Sensörler › Sensör & Bildirim Merkezi, istenen sekmeyle.
    // Kolay modda Uzman'a özgü sayfa menüde yoktur; bilinçli tıklama olduğu için yine de açılır.
    internal void MerkezAc(int sekme) { _merkezSekme = sekme; _kabukGit("lab", "merkez", true); }

    // Kolay görünümdeki üç gösterge.
    internal void Yenile()
    {
        // İlk gösterge: yalnızca uzak sensör gerçekten yapılandırılmışsa "Kali Sensör"; kurumsal KOBİ varsayılanında "Son Tarama Durumu" (tarih + puan).
        var k = KaliDurumYoneticisi.Gosterge(KaliDurumYoneticisi.Son); var g0 = KolayGostergeleri.Birinci(UzmanPaketi.Etkin(KaliDurumYoneticisi.Sensorler()), k, GecmisDeposu.Listele()); _kolay().Gosterge(0, g0.Baslik, g0.Yazi, g0.Renk);
        var s = IpSizintiTesti.Gosterge(IpSizintiTesti.SonDeger); _kolay().Gosterge(1, "IP Güvenlik Sızıntı Testi", s.Yazi, s.Renk);
        var b = WebhookYoneticisi.Gosterge(); _kolay().Gosterge(2, "Bildirim Kanalı", b.Yazi, b.Renk);
        var kg = _nobet.KorumaGostergesi(); _kolay().Gosterge(3, "Koruma Hizmeti", kg.Yazi, kg.Renk);
    }
    // Gösterge tıklaması: koruma hizmeti kendi işleyicisine, "Son Tarama Durumu" Geçmiş Operasyonlar'a, diğerleri Merkez'in ilgili sekmesine gider.
    internal void Tiklandi(int sira) { if (sira == 3) _nobet.KorumaGostergesiTiklandi(); else if (sira == 0 && !UzmanPaketi.Etkin(KaliDurumYoneticisi.Sensorler())) _kabukGit("tarama", "gecmis", false); else MerkezAc(sira); }

    // Kali denetimi, durum olayları ve periyodik yeniden denetim (Form1 açılırken bir kez kurulur).
    internal void Bagla()
    {
        void YenileOlayi() { if (_f.IsHandleCreated && !_f.IsDisposed) _f.BeginInvoke(() => { _kolayYenile(); KaliUyar(); }); }
        KaliDurumYoneticisi.Degisti += YenileOlayi; IpSizintiTesti.Degisti += YenileOlayi; WebhookYoneticisi.Degisti += YenileOlayi;
        _kaliZaman.Tick += (_, _) => KaliDurumYoneticisi.ArkaPlandaBaslat(); _kaliZaman.Start();
        _f.Shown += (_, _) => { KaliDurumYoneticisi.ArkaPlandaBaslat(); _kolayYenile(); };
        _f.FormClosed += (_, _) => { KaliDurumYoneticisi.Degisti -= YenileOlayi; IpSizintiTesti.Degisti -= YenileOlayi; WebhookYoneticisi.Degisti -= YenileOlayi; _kaliZaman.Stop(); };
    }

    // Kali kapalıysa/sorunluysa kullanıcıyı SESSİZ BIRAKMA: uyarı çıkar (oturum başına bir kez; tıklayınca merkez açılır).
    internal void KaliUyar()
    {
        var s = KaliDurumYoneticisi.Son; if (_kaliUyarildi || s.Durum is not (KaliDurumu.Kapali or KaliDurumu.Sorun)) return; _kaliUyarildi = true;
        Bildirim.Goster(s.Durum == KaliDurumu.Kapali ? "Kali Sensör Kapalı / Bağlantı Bekleniyor" : "Kali Sensör: " + s.Kisa, "Yeniden denemek veya sanal makineyi başlatmak için tıklayın.", BildirimTuru.Uyari, () => MerkezAc(MerkezFormu.SekmeKali));
        _tarama.Gunluk("KALİ -> " + s.Kisa + ": " + s.Ayrinti, DesignTokens.Ozgun(TokenRol.StatusWarning));
    }

    // Ağ Haritası'ndan internet hedefi girilince pasif gözleme geçiş (Uzman'a alır, mevcut onaylı akışı başlatır).
    internal void DisAgPasifeGec(string host) => _f.BeginInvoke(() => { if (ArayuzModu.Mevcut == ModTuru.Kolay) ArayuzModu.Ayarla(ModTuru.Uzman); _tarama.GorunumSec(Gorunum.Tarama); _tarama.txtHedef.Text = host; _tarama.btnBaslat.PerformClick(); });
}
