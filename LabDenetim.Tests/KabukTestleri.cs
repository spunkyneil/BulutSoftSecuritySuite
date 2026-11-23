// Kurumsal kabuk (AppShell) testleri: navigasyon kataloğu ve rol/mod kuralları, DenetimSayfasi (gömme, Kapat düğmesi gizleme, kapanış köprüsü),
// SekmeliSayfa, AppShell geçişleri ve gerçek çizim (ekran dışı pencerede pikseller), yeni kodda tasarım kuralları (renk/yazı tipi/MessageBox yasağı).
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

// Sınama sayfası.
internal sealed class SinamaSayfasi : UserControl, IModulePage, IKendiRenginiYoneten, IThemeAware
{
    public SinamaSayfasi(string id, string baslik, NavGrup grup) { SayfaId = id; Baslik = baslik; Grup = grup; BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); }
    public void TemaUygula() { BackColor = DesignTokens.Renk(TokenRol.SurfaceApp); Invalidate(true); }
    public string SayfaId { get; } public string Baslik { get; } public NavGrup Grup { get; } public Eylem? GerekliYetki => null; public bool SadeceUzman => false; public Control Gorunum => this;
    public int Acildi, Kapandi;
    public Task AcildigindaAsync(CancellationToken ct) { Acildi++; return Task.CompletedTask; }
    public Task KapanirkenAsync() { Kapandi++; return Task.CompletedTask; }
}

// Gömülecek eski form örneği.
internal sealed class SinamaFormu : SayfaDenetimi
{
    public readonly Button Kapat = new() { Text = "Kapat" }, Islem = new() { Text = "Yenile" }, TekTik = new() { Text = "Kapat (tek tık)" };
    public SinamaFormu() { Controls.AddRange(new Control[] { Kapat, Islem, TekTik }); }
}

[Collection("Veri")]
public class KabukTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_kabuk_" + Guid.NewGuid().ToString("N")); private readonly string _eskiKok;
    // Oturum açık (Yönetici) varsayılır: yetkisiz sayfa denemesi modal uyarı açıp testi bekletmesin.
    public KabukTestleri() { _eskiKok = BulutKlasor.Kok; Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; Oturum.Aktif = new Kullanici { Ad = "test", Rol = Rol.Yonetici }; if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir(); }
    public void Dispose() { if (TemaYonetici.Mod == TemaModu.Acik) TemaYonetici.Degistir(); Oturum.Aktif = null; BulutKlasor.Kok = _eskiKok; try { Directory.Delete(_t, true); } catch (IOException hkt1) { HataKayit.Sessiz(hkt1, "KabukTestleri.temizlik"); } }

    private static void Sta(Action a)
    {
        Exception? hata = null; var t = new Thread(() => { try { a(); } catch (Exception e) { hata = e; } });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(90))) throw new TimeoutException("Kabuk testi zaman aşımı"); if (hata != null) throw new Exception("Kabuk testi hatası: " + hata, hata);
    }

    private static TemaTestFormu EkranDisiPencere(Control icerik)
    {
        var f = new TemaTestFormu { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Size = new Size(1280, 800), ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None };
        f.Controls.Add(icerik); f.Show(); Application.DoEvents(); return f;
    }

    private static AppShell KabukKur(out Dictionary<string, SinamaSayfasi> sayfalar)
    {
        var k = new AppShell(); sayfalar = new();
        foreach (var t in NavigasyonModeli.Sayfalar) { var s = new SinamaSayfasi(t.Id, t.Baslik, t.Grup); sayfalar[t.Id] = s; k.SayfaKaydet(s); }
        return k;
    }

    // ---------- Navigasyon kataloğu ----------
    [Fact]
    public void Katalog_DortGrup_OnSayfa_SartnameyeUygun()
    {
        var gruplar = NavigasyonModeli.Sayfalar.GroupBy(p => p.Grup).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToArray());
        Assert.Equal(new[] { "durum", "olay" }, gruplar[NavGrup.GenelBakis]); Assert.Equal(new[] { "tarama", "ag", "kalici" }, gruplar[NavGrup.DenetimKesif]);
        Assert.Equal(new[] { "savunma", "trafik" }, gruplar[NavGrup.AktifSavunma]); Assert.Equal(new[] { "rapor", "arac", "lab" }, gruplar[NavGrup.RaporlamaAraclar]);
        Assert.Equal(10, NavigasyonModeli.Sayfalar.Count); Assert.Equal(10, NavigasyonModeli.Sayfalar.Select(p => p.Id).Distinct().Count()); Assert.Equal(10, NavigasyonModeli.Sayfalar.Select(p => p.Ikon).Distinct().Count());
        Assert.Equal(new[] { "yeni", "gecmis" }, NavigasyonModeli.Bul("tarama")!.Sekmeler.Select(s => s.Id)); Assert.Equal(new[] { "cihaz", "harita" }, NavigasyonModeli.Bul("ag")!.Sekmeler.Select(s => s.Id));
        Assert.Equal(new[] { "kalkan", "tuzak", "nobet", "brute" }, NavigasyonModeli.Bul("savunma")!.Sekmeler.Select(s => s.Id)); Assert.Equal(new[] { "sensor", "merkez" }, NavigasyonModeli.Bul("lab")!.Sekmeler.Select(s => s.Id));
        Assert.True(NavigasyonModeli.Bul("lab")!.SadeceUzman);
    }

    [Fact]
    public void KolayMod_YalnizcaDurumTaramaVeRaporuGosterir_UzmanHepsini()
    {
        Assert.Equal(new[] { "durum", "tarama", "rapor" }, NavigasyonModeli.Gorunenler(ModTuru.Kolay).Select(p => p.Id)); Assert.Equal(10, NavigasyonModeli.Gorunenler(ModTuru.Uzman).Count());
        Assert.Equal(new[] { "gecmis" }, NavigasyonModeli.GorunenSekmeler(NavigasyonModeli.Bul("tarama")!, ModTuru.Kolay).Select(s => s.Id));
    }

    [Fact]
    public void Yetki_EskiMenuKurallariylaAyni_IzleyiciAnalistYonetici()
    {
        // Eski YetkiRozetleriYaz tablosu: Kalkan/Tuzak/Nöbetçi/Cihaz/Politika=Koruma; Araç/Sensör/Durum=Arac. İzleyici yalnızca Gorme+Rapor.
        Assert.False(NavigasyonModeli.Erisim("ag", null, Rol.Izleyici).Izinli); Assert.False(NavigasyonModeli.Erisim("arac", null, Rol.Izleyici).Izinli);
        Assert.True(NavigasyonModeli.Erisim("rapor", null, Rol.Izleyici).Izinli); Assert.True(NavigasyonModeli.Erisim("durum", null, Rol.Izleyici).Izinli); Assert.True(NavigasyonModeli.Erisim("olay", null, Rol.Izleyici).Izinli);
        // Savunma Merkezi ve Kalıcılık: eski menü işleyicileri YetkiKontrol(Koruma) istiyordu; İzleyici açamaz, Analist açar.
        Assert.False(NavigasyonModeli.Erisim("savunma", null, Rol.Izleyici).Izinli); Assert.False(NavigasyonModeli.Erisim("kalici", null, Rol.Izleyici).Izinli); Assert.True(NavigasyonModeli.Erisim("kalici", null, Rol.Analist).Izinli); Assert.True(NavigasyonModeli.Erisim("savunma", null, Rol.Analist).Izinli);
        foreach (var s in new[] { "kalkan", "tuzak", "nobet", "brute" }) { var (izin, eksik) = NavigasyonModeli.Erisim("savunma", s, Rol.Izleyici); Assert.False(izin); Assert.Equal(Eylem.Koruma, eksik); Assert.True(NavigasyonModeli.Erisim("savunma", s, Rol.Analist).Izinli); }
        // Durum Panosu: Kolay panel herkese açık; Durum Ayrıntıları sekmesi Arac ister (eski btnMenuDurum).
        Assert.True(NavigasyonModeli.Erisim("durum", null, Rol.Izleyici).Izinli); Assert.False(NavigasyonModeli.Erisim("durum", "pano", Rol.Izleyici).Izinli); Assert.True(NavigasyonModeli.Erisim("durum", "pano", Rol.Analist).Izinli);
        // Sensör & Bildirim Merkezi: görüntüleme için Gorme yeterli; Uzak Sensörler Arac ister.
        Assert.True(NavigasyonModeli.Erisim("lab", "merkez", Rol.Izleyici).Izinli); Assert.False(NavigasyonModeli.Erisim("lab", "sensor", Rol.Izleyici).Izinli); Assert.True(NavigasyonModeli.Erisim("lab", "sensor", Rol.Analist).Izinli);
        Assert.True(NavigasyonModeli.Erisim("ag", "harita", Rol.Analist).Izinli); Assert.True(NavigasyonModeli.Erisim("lab", "sensor", Rol.Analist).Izinli);
        foreach (var p in NavigasyonModeli.Sayfalar) { Assert.True(NavigasyonModeli.Erisim(p.Id, null, Rol.Yonetici).Izinli); foreach (var s in p.Sekmeler) Assert.True(NavigasyonModeli.Erisim(p.Id, s.Id, Rol.Yonetici).Izinli); }
        Assert.False(NavigasyonModeli.Erisim("ag", null, null).Izinli);   // oturumsuz (rol yok) yetki isteyen sayfayı açamaz
        Assert.False(NavigasyonModeli.Erisim("yok-sayfa", null, Rol.Yonetici).Izinli);
    }

    // ---------- DenetimSayfasi ----------
    [Fact]
    public void ModalKapatDugmesi_YalnizcaKapatYazanlar_GercekIslemlerKalir()
    {
        Sta(() =>
        {
            Assert.True(DenetimSayfasi.ModalKapatMi(new Button { Text = "Kapat" })); Assert.True(DenetimSayfasi.ModalKapatMi(new Button { Text = "✕ Kapat" })); Assert.True(DenetimSayfasi.ModalKapatMi(new Button { Text = "  kapat " }));
            Assert.False(DenetimSayfasi.ModalKapatMi(new Button { Text = "Kapat (tek tık)" })); Assert.False(DenetimSayfasi.ModalKapatMi(new Button { Text = "Yenile" })); Assert.False(DenetimSayfasi.ModalKapatMi(new Button { Text = "▤ CSV Olarak Dışa Aktar" })); Assert.False(DenetimSayfasi.ModalKapatMi(new Button { Text = "Honeypot Kapıyı Kapat" }));
        });
    }

    [Fact]
    public void DenetimSayfasi_FormuGomer_KapatiGizler_KapanisiBildirir_YenidenAcar()
    {
        Sta(() =>
        {
            var a = new DenetimSayfasi("test", "Test", NavGrup.GenelBakis, () => new SinamaFormu()); int hazir = 0; var kapanislar = new List<DialogResult>(); a.DenetimHazir = _ => hazir++; a.DenetimKapandi += (_, _, r) => kapanislar.Add(r);
            using var host = EkranDisiPencere(a);
            a.AcildigindaAsync(CancellationToken.None).GetAwaiter().GetResult();
            var f = Assert.IsType<SinamaFormu>(a.Denetim); Assert.Equal(DockStyle.Fill, f.Dock); Assert.Same(a, f.Parent); Assert.Equal(1, hazir);
            Assert.False(f.Kapat.Visible); Assert.True(f.Islem.Visible); Assert.True(f.TekTik.Visible);
            a.AcildigindaAsync(CancellationToken.None).GetAwaiter().GetResult(); Assert.Same(f, a.Denetim); Assert.Equal(1, hazir);   // ikinci açılış aynı formu korur
            f.DialogResult = DialogResult.OK; f.Close(); Application.DoEvents();
            Assert.Single(kapanislar); Assert.Equal(DialogResult.OK, kapanislar[0]); Assert.Null(a.Denetim);
            a.AcildigindaAsync(CancellationToken.None).GetAwaiter().GetResult(); Assert.NotNull(a.Denetim); Assert.NotSame(f, a.Denetim); Assert.Equal(2, hazir);
        });
    }

    [Fact]
    public void GomuluSayfa_ElverisliBoyutta_PencereEfektiAlmaz()
    {
        Sta(() =>
        {
            var a = new DenetimSayfasi("test", "Test", NavGrup.GenelBakis, () => new SinamaFormu());
            using var host = EkranDisiPencere(a); a.AcildigindaAsync(CancellationToken.None).GetAwaiter().GetResult(); Application.DoEvents();
            var f = a.Denetim!; Assert.Equal(a.ClientSize, f.Size);
        });
    }

    [Fact]
    public void BolumSayfasi_EnAzIcerikVerilirse_KabuguDegilSayfayiKaydirir()
    {
        Sta(() =>
        {
            var icerik = new Panel(); var p = new BolumSayfasi("tarama/yeni", "Yeni", NavGrup.DenetimKesif, () => icerik, enAzIcerik: new Size(1000, 700)); p.Size = new Size(600, 400);
            using var host = EkranDisiPencere(p); p.AcildigindaAsync(CancellationToken.None).GetAwaiter().GetResult(); Application.DoEvents();
            Assert.True(p.AutoScroll); Assert.Equal(DesignTokens.Px(p, 1000), p.AutoScrollMinSize.Width); Assert.Same(p, icerik.Parent); Assert.Equal(DockStyle.Fill, icerik.Dock);
            var yok = new BolumSayfasi("x", "X", NavGrup.GenelBakis, () => new Panel()); Assert.False(yok.AutoScroll);   // en az alan verilmezse kaydırma yok
        });
    }

    // ---------- SekmeliSayfa ----------
    [Fact]
    public void SekmeliSayfa_IlkSekmeyiAcar_SekmeGeciriVeKolayModdaUzmanSekmesiniGizler()
    {
        Sta(() =>
        {
            var tanim = NavigasyonModeli.Bul("tarama")!; var yeni = new SinamaSayfasi("yeni", "Yeni", NavGrup.DenetimKesif); var gecmis = new SinamaSayfasi("gecmis", "Geçmiş", NavGrup.DenetimKesif);
            var s = new SekmeliSayfa(tanim, new Dictionary<string, IModulePage> { ["yeni"] = yeni, ["gecmis"] = gecmis }); using var host = EkranDisiPencere(s);
            s.Mod = ModTuru.Uzman; Assert.True(s.AcildigindaAsync(CancellationToken.None).Wait(5000)); Assert.Equal("yeni", s.AktifSekme); Assert.Equal(1, yeni.Acildi); Assert.True(yeni.Visible);
            Assert.True(s.SekmeGitAsync("gecmis").GetAwaiter().GetResult()); Assert.Equal("gecmis", s.AktifSekme); Assert.Equal(1, yeni.Kapandi); Assert.False(yeni.Visible); Assert.Equal(1, gecmis.Acildi); Assert.Equal("Geçmiş Operasyonlar", s.AktifSekmeBasligi);
            Assert.False(s.SekmeGitAsync("olmayan").GetAwaiter().GetResult());
            var kolay = new SekmeliSayfa(tanim, new Dictionary<string, IModulePage> { ["yeni"] = new SinamaSayfasi("yeni", "Yeni", NavGrup.DenetimKesif), ["gecmis"] = new SinamaSayfasi("gecmis", "Geçmiş", NavGrup.DenetimKesif) }) { Mod = ModTuru.Kolay };
            using var host2 = EkranDisiPencere(kolay); kolay.AcildigindaAsync(CancellationToken.None).GetAwaiter().GetResult(); Assert.Equal("gecmis", kolay.AktifSekme);
        });
    }

    // ---------- AppShell ----------
    [Fact]
    public void AppShell_SayfaGecisi_BaslikKirintiVeSeciliyiGunceller_EskiSayfayiKapatir()
    {
        Sta(() =>
        {
            var k = KabukKur(out var s); using var host = EkranDisiPencere(k); k.Mod = ModTuru.Uzman;
            string? degisen = null; k.SayfaDegisti += id => degisen = id;
            Assert.True(k.GitAsync("durum").GetAwaiter().GetResult()); Assert.Equal("durum", k.AktifSayfaId); Assert.Equal(1, s["durum"].Acildi); Assert.True(s["durum"].Visible); Assert.Equal("Durum Panosu", k.Ust.Baslik); Assert.Equal("Genel Bakış › Durum Panosu", k.Ust.Kirinti); Assert.Equal("durum", k.Kenar.Secili);
            Assert.True(k.GitAsync("trafik").GetAwaiter().GetResult()); Assert.Equal("trafik", degisen); Assert.Equal(1, s["durum"].Kapandi); Assert.False(s["durum"].Visible); Assert.True(s["trafik"].Visible); Assert.Equal("Aktif Savunma › Canlı Ağ Trafiği", k.Ust.Kirinti);
            Assert.True(k.GitAsync("trafik").GetAwaiter().GetResult()); Assert.Equal(1, s["trafik"].Acildi);   // aynı sayfaya tekrar gitmek yeniden açmaz
            Assert.False(k.GitAsync("kayitsiz").GetAwaiter().GetResult());
        });
    }

    [Fact]
    public void AppShell_KolayModdaUzmanSayfadaysaDurumPanosunaDoner_ZorlaAcilanSayfaKalir()
    {
        Sta(() =>
        {
            var k = KabukKur(out _); using var host = EkranDisiPencere(k); k.Mod = ModTuru.Uzman;
            k.GitAsync("savunma").GetAwaiter().GetResult(); k.Mod = ModTuru.Kolay; Application.DoEvents(); Assert.Equal("durum", k.AktifSayfaId);
            k.GitAsync("olay", null, zorla: true).GetAwaiter().GetResult(); Assert.Equal("olay", k.AktifSayfaId); k.Mod = ModTuru.Kolay; Assert.Equal("olay", k.AktifSayfaId);   // zil gibi bilinçli geçiş korunur
        });
    }

    [Fact]
    public void AppShell_KatalogdaOlmayanSayfaKaydiniReddeder()
    {
        Sta(() => { var k = new AppShell(); Assert.Throws<ArgumentException>(() => k.SayfaKaydet(new SinamaSayfasi("yok", "Yok", NavGrup.GenelBakis))); });
    }

    // ---------- Gerçek çizim ----------
    private static Color Piksel(Bitmap b, int x, int y) => b.GetPixel(Math.Clamp(x, 0, b.Width - 1), Math.Clamp(y, 0, b.Height - 1));

    [Fact]
    public void AppShell_GercekCizim_KoyuVeAcikTemadaTokenRenkleriniKullanir()
    {
        Sta(() =>
        {
            var k = KabukKur(out _); using var host = EkranDisiPencere(k); k.Mod = ModTuru.Uzman; k.GitAsync("savunma").GetAwaiter().GetResult(); k.Ust.BildirimAyarla(3, true); Application.DoEvents(); host.Refresh();
            string? cikti = Environment.GetEnvironmentVariable("BULUT_EKRAN_KLASORU");
            foreach (bool koyu in new[] { true, false })
            {
                if (!koyu) { TemaYonetici.Degistir(); Application.DoEvents(); host.Refresh(); }
                using var bmp = new Bitmap(host.ClientSize.Width, host.ClientSize.Height); host.DrawToBitmap(bmp, new Rectangle(Point.Empty, host.ClientSize));
                if (cikti != null) { Directory.CreateDirectory(cikti); bmp.Save(Path.Combine(cikti, koyu ? "kabuk_koyu.png" : "kabuk_acik.png"), System.Drawing.Imaging.ImageFormat.Png); }
                Assert.Equal(DesignTokens.Renk(TokenRol.SurfaceSidebar, koyu).ToArgb(), Piksel(bmp, 6, 400).ToArgb());           // sol menü zemini
                Assert.Equal(DesignTokens.Renk(TokenRol.SurfaceApp, koyu).ToArgb(), Piksel(bmp, 700, 400).ToArgb());             // sayfa alanı zemini
                Assert.Equal(DesignTokens.Renk(TokenRol.SurfaceApp, koyu).ToArgb(), Piksel(bmp, 700, 5).ToArgb());               // üst çubuk zemini
                var renkler = new HashSet<int>(); for (int y = 0; y < bmp.Height; y += 3) for (int x = 0; x < 320; x += 2) renkler.Add(bmp.GetPixel(x, y).ToArgb());
                Assert.True(renkler.Count > 12, "Sol menü ve üst çubuk metin/ikon çizmeli (renk sayısı: " + renkler.Count + ")");
                bool vurgu = false; for (int y = 60; y < 520 && !vurgu; y++) if (Piksel(bmp, 10, y).ToArgb() == DesignTokens.Renk(TokenRol.AccentPrimary, koyu).ToArgb()) vurgu = true;   // seçili öğenin sol vurgu çubuğu
                Assert.True(vurgu, "Seçili öğe vurgu çubuğu görünmeli (koyu=" + koyu + ")");
            }
        });
    }

    [Fact]
    public void SidebarNav_KompaktKip_GenisligiDaraltirVeOlayVerir()
    {
        Sta(() =>
        {
            var k = KabukKur(out _); using var host = EkranDisiPencere(k); k.Mod = ModTuru.Uzman; int genis = k.Kenar.Width; bool? olay = null; k.Kenar.KompaktDegisti += v => olay = v;
            k.Kenar.Kompakt = true; Application.DoEvents(); Assert.True(olay); Assert.True(k.Kenar.Width < genis); Assert.Equal(k.Kenar.HedefGenislik, k.Kenar.Width);
            k.Kenar.Kompakt = false; Application.DoEvents(); Assert.Equal(genis, k.Kenar.Width);
        });
    }

    // ---------- Tasarım kuralları (CLAUDE.md kural 3 ve 4) ----------
    [Fact, Trait("Tur", "KaynakKurali")]
    public void YeniKabukVeTasarimKodu_DogrudanRenkYaziTipiMessageBoxVeZamanlayiciIcermez()
    {
        string? d = AppContext.BaseDirectory, kok = null; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) { kok = d; break; }
        if (kok == null) return;
        var kotu = new List<string>();
        // Kabuk\ ve Tasarim\ klasörleri + Faz 2'de yazılan kök dizindeki yeni dosyalar (yeni/düzenlenen kodda doğrudan renk, yazı tipi, MessageBox yasağı).
        foreach (var klasor in new[] { "Kabuk", "Tasarim", "." })
            foreach (var f in klasor == "." ? new[] { Path.Combine(kok, "LabDenetim", "ClaudeOnayFormu.cs") } : Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim", klasor), "*.cs").ToArray())
            {
                string ad = Path.GetFileName(f);
                var kod = string.Join("\n", File.ReadAllLines(f).Where(s => !s.TrimStart().StartsWith("//", StringComparison.Ordinal)).Select(s => { int k = s.IndexOf("//", StringComparison.Ordinal); return k >= 0 ? s[..k] : s; }));
                if (ad != "DesignTokens.cs" && kod.Contains("Color.FromArgb(", StringComparison.Ordinal)) kotu.Add(ad + ": Color.FromArgb");
                if (ad != "FontProvider.cs" && Regex.IsMatch(kod, @"new\s+Font\(")) kotu.Add(ad + ": new Font");
                if (kod.Contains("MessageBox.Show(", StringComparison.Ordinal)) kotu.Add(ad + ": MessageBox.Show");
                if (Regex.IsMatch(kod, @"new\s+(System\.Windows\.Forms\.)?Timer\(")) kotu.Add(ad + ": Timer");
                if (Regex.IsMatch(kod, @"catch\s*(\(\s*[\w\.]*\s*\))?\s*\{\s*\}")) kotu.Add(ad + ": boş catch");
            }
        Assert.True(kotu.Count == 0, "Yeni kabuk/tasarım kodunda kural ihlali: " + string.Join("; ", kotu));
    }
}
