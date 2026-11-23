// Küresel statik bağımlılıkların soyutlanması testleri (Faz 4): kabuk bileşenleri Oturum.Aktif / OlayDeposu yerine IOturumBaglami / IOlayOkuyucu / IOlayYazici üzerinden çalışır;
// böylece disk, DPAPI ve küresel durum olmadan sahte uygulamalarla sınanabilir.
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public sealed class ServisBaglamiTestleri : IDisposable
{
    public void Dispose() => ServisBaglami.Sifirla();

    private sealed class SahteOturum : IOturumBaglami { public Kullanici? Aktif { get; set; } public Rol? Rol => Aktif?.Rol; }
    private sealed class SahteOlaylar : IOlayOkuyucu, IOlayYazici
    {
        public readonly List<OlayKaydi> Kayitlar = new(); public int OkumaSayisi;
        public event Action<string>? Eklendi;
        public List<OlayKaydi> SonOlaylar(int adet) { OkumaSayisi++; return Kayitlar.OrderByDescending(k => k.Zaman).Take(adet).ToList(); }
        public List<OlayKaydi> SonUyariKritik(TimeSpan pencere, DateTime simdi) { OkumaSayisi++; return Kayitlar.Where(k => k.Seviye is "Uyarı" or "Kritik" && simdi - k.Zaman <= pencere).ToList(); }
        public (List<OlayKaydi> Sayfa, int Toplam) OkuSayfali(int sayfa, int boyut) => (Kayitlar.OrderByDescending(k => k.Zaman).Skip(sayfa * boyut).Take(boyut).ToList(), Kayitlar.Count);
        public void Ekle(string tur, string seviye, string mesaj) { Kayitlar.Add(new OlayKaydi(DateTime.Now, tur, seviye, mesaj)); Eklendi?.Invoke(seviye); }
    }

    [Fact]
    public void Varsayilanlar_StatikSiniflaraYonlenir_Sifirla_GeriAlir()
    {
        var eski = Oturum.Aktif;
        try
        {
            Oturum.Aktif = new Kullanici { Ad = "a", Rol = Rol.Analist }; Assert.Equal(Rol.Analist, ServisBaglami.Oturum.Rol); Assert.Same(Oturum.Aktif, ServisBaglami.Oturum.Aktif);
            ServisBaglami.Oturum.Aktif = null; Assert.Null(Oturum.Aktif); Assert.Null(ServisBaglami.Oturum.Rol);
            ServisBaglami.Oturum = new SahteOturum { Aktif = new Kullanici { Ad = "s", Rol = Rol.Yonetici } }; Assert.Equal(Rol.Yonetici, ServisBaglami.Oturum.Rol); Assert.Null(Oturum.Aktif);   // statik duruma dokunulmadı
            ServisBaglami.Sifirla(); Assert.IsType<StatikOturumBaglami>(ServisBaglami.Oturum);
        }
        finally { Oturum.Aktif = eski; }
    }

    [Fact]
    public void BildirimKaynagi_SahteOlayOkuyucuyla_DiskeDokunmadanCalisir()
    {
        var sahte = new SahteOlaylar(); ServisBaglami.OlayOkuyucu = sahte; ServisBaglami.OlayYazici = sahte;
        sahte.Kayitlar.Add(new OlayKaydi(DateTime.Now.AddMinutes(-5), "Tuzak", "Kritik", "eski kritik")); sahte.Kayitlar.Add(new OlayKaydi(DateTime.Now.AddMinutes(-90), "Tuzak", "Uyarı", "çok eski"));
        using var geldi = new ManualResetEventSlim(); int sayi = -1; bool kritik = false;
        using var kaynak = new BildirimKaynagi((n, k, _) => { sayi = n; kritik = k; geldi.Set(); });
        ServisBaglami.OlayYazici.Ekle("Nöbetçi", "Uyarı", "yeni uyarı");   // yazıcı → okuyucu olayı → kaynak yeniden hesaplar
        Assert.True(geldi.Wait(TimeSpan.FromSeconds(5)), "bildirim hesaplanmadı"); Assert.Equal(2, sayi); Assert.True(kritik);   // son 30 dk: 1 kritik + 1 uyarı; 90 dk önceki sayılmaz
        Assert.True(sahte.OkumaSayisi >= 1);
    }

    [Fact]
    public void KabukYetki_SahteOturumla_YetkiliRoldeModalAcmadanGecer()
    {
        ServisBaglami.Oturum = new SahteOturum { Aktif = new Kullanici { Ad = "yonetici", Rol = Rol.Yonetici } };
        Assert.True(KabukYetki.Kontrol(null, Eylem.Rapor, "Rapor")); Assert.True(KabukYetki.Kontrol(null, null, ""));
    }

    // D6: sınır TÜM arayüz projesine yayıldı — kabuk, servisler, kontroller VE eski ekranlar oturumu/olay günlüğünü ServisBaglami üzerinden kullanır; doğrudan statik erişim yalnızca
    // Program.cs'te (uygulama kök noktası: hizmet yazıcısını bağlar, beklenmeyen hatayı yazar) kalır. Salt işlevsel yardımcılar (OlayDeposu.Siniflandir/Yol/CsvYaz) serbesttir.
    [Fact, Trait("Tur", "KaynakKurali")]
    public void Arayuz_DogrudanOturumAktifVeOlayDeposuGirisCikisiKullanmaz_YalnizProgramCs()
    {
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null; i++, kok = Path.GetDirectoryName(kok)) if (File.Exists(Path.Combine(kok, "Directory.Build.props"))) break;
        if (kok == null) return; var ihlal = new List<string>();
        var desen = new System.Text.RegularExpressions.Regex(@"(?<![\w.])Oturum\.Aktif|(?<![\w.])OlayDeposu\.(Ekle|Oku|OkuSayfali|SonOlaylar|SonUyariKritik|Dogrula|Eklendi)(?![\w])");
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || Path.GetFileName(f) == "Program.cs") continue;
            int n = 0; foreach (var s in File.ReadLines(f)) { n++; if (s.TrimStart().StartsWith("//")) continue; if (desen.IsMatch(s)) ihlal.Add($"{Path.GetFileName(f)}:{n}"); }
        }
        Assert.True(ihlal.Count == 0, "Doğrudan statik Oturum/OlayDeposu (ServisBaglami kullanın): " + string.Join(", ", ihlal));
    }
}
