// Form1'den taşınan özelliklerin (Servisler\*Eylemi) Form1 OLMADAN, sahte ITaramaBaglami ile sınanması (D4: Form1 taşıma, 1. dilim).
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

[Collection("Veri")]
public sealed class TasinanEylemTestleri : IDisposable
{
    private readonly string _eskiKok = BulutKlasor.Kok, _eskiAltin = AltinProfilDeposu.Yol; private readonly string _t = Path.Combine(Path.GetTempPath(), "eylem_" + Guid.NewGuid().ToString("N"));
    public TasinanEylemTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; AltinProfilDeposu.Yol = Path.Combine(_t, "altin.json"); }
    public void Dispose() { BulutKlasor.Kok = _eskiKok; AltinProfilDeposu.Yol = _eskiAltin; try { Directory.Delete(_t, true); } catch (IOException hk) { HataKayit.Sessiz(hk, "TasinanEylemTestleri.temizlik"); } }

    // Sahte tarama bağlamı: Form1'siz çalışır, çağrıları kaydeder.
    private sealed class SahteBaglam : ITaramaBaglami
    {
        public List<Bulgu> Liste = new(); public readonly List<Bulgu> Eklenen = new(); public readonly List<string> Gunlukler = new(), Durumlar = new(); public string HedefDeger = "10.0.0.5"; public bool Suruyor { get; set; }
        public IWin32Window Sahip => null!; public IReadOnlyList<Bulgu> Bulgular => Liste; public string Hedef { get => HedefDeger; set => HedefDeger = value; }
        public int Kirmizi => Liste.Count(b => b.Onem == Onem.Yuksek); public bool TaramaSuruyor => Suruyor; public int Puan => 70;
        public void Gunluk(string mesaj, Color renk) => Gunlukler.Add(mesaj); public bool PencereHazir => true; public void UiIsle(Action is_) => is_(); public void TepsiBalonu(int ms, string baslik, string metin, bool uyari) { }
        public void KapanirkenCalistir(Action is_) { } public void Durum(string metin, bool gunluge = true, Color? renk = null) => Durumlar.Add(metin);
        public void BulguEkle(Bulgu b) { Eklenen.Add(b); Liste.Add(b); } public RaporBilgisi RaporBilgisiOlustur() => throw new NotSupportedException(); public (string NeDemek, string Nasil, string Komut) RaporAciklayici(Bulgu b) => ("", "", "");
    }
    private static Bulgu Port(int p) => new("Port", $"Açık port {p} (test)", Onem.Orta, "ayrıntı", "öneri");

    [Fact]
    public void AltinProfil_YeniKapiAcilinca_ReferansBulgusuEkler_AyniysaUyumluDer()
    {
        var b = new SahteBaglam(); b.Liste.Add(Port(80)); AltinProfilDeposu.Kilitle(b.Hedef, 90, b.Liste);
        var lbl = new Label(); var eylem = new AltinProfilEylemi(b, lbl);
        b.Eklenen.Clear(); eylem.Karsilastir(); Assert.Single(b.Eklenen); Assert.Equal("Referans", b.Eklenen[0].Modul); Assert.Equal(Onem.Bilgi, b.Eklenen[0].Onem);   // aynı durum: "uyumlu: sapma yok"
        b.Liste.Add(Port(445)); b.Eklenen.Clear(); eylem.Karsilastir(); Assert.Contains(b.Eklenen, x => x.Onem == Onem.Yuksek && x.Baslik.Contains("yeni kapı", StringComparison.Ordinal));
        b.HedefDeger = "10.9.9.9"; b.Eklenen.Clear(); eylem.Karsilastir(); Assert.Empty(b.Eklenen); Assert.Contains(b.Gunlukler, g => g.Contains("başka bir hedef", StringComparison.Ordinal));   // başka hedef: kıyaslanmaz
        eylem.DurumYaz(); Assert.Contains("Kilitli", lbl.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ZincirEylemi_SeritiGizler_ZincirYoksaSakinSatirYazar()
    {
        var b = new SahteBaglam(); var serit = new Label { Visible = true }; var kap = new Panel { Width = 600 }; var eylem = new ZincirEylemi(b, serit, kap, () => false);
        eylem.SeritYenile(); Assert.False(serit.Visible);   // uyarı yok: şerit gizli
        b.Liste.Add(new Bulgu("Referans", "Referansa göre 1 yeni kapı açılmış!", Onem.Yuksek, "d", "o")); eylem.SeritYenile(); Assert.True(serit.Visible); Assert.Contains("yeni kapı", serit.Text, StringComparison.Ordinal);
        var trafik = new ZincirEylemi(b, serit, kap, () => true); trafik.SeritYenile(); Assert.False(serit.Visible);   // trafik görünümünde şerit gösterilmez
        new ZincirEylemi(new SahteBaglam(), new Label(), kap, () => false).Analiz();   // bulgu yok: hata vermeden sakin satır
    }

    [Fact]
    public void AramaKabul_AramaMetniVeFiltreyeGoreSatirGorunur()
    {
        var b = new SahteBaglam(); var txt = new TextBox(); bool filtreUygulandi = false; bool renkUyar = true;
        var eylem = new AramaKabulEylemi(b, txt, new DataGridView(), new ContextMenuStrip(), () => filtreUygulandi = true, _ => renkUyar); eylem.Kur();
        Assert.True(eylem.SatirGorunur(Port(445)));                      // arama boş: hepsi görünür
        txt.Text = "445"; Assert.True(filtreUygulandi); Assert.True(eylem.SatirGorunur(Port(445))); Assert.False(eylem.SatirGorunur(Port(80)));
        renkUyar = false; Assert.False(eylem.SatirGorunur(Port(445)));   // renk filtresine uymayan, aramaya uysa da gizli
    }

    [Fact]
    public void HedefSecici_YerelSecince_AdresKutusuVeDurumGuncellenir()
    {
        var b = new SahteBaglam(); b.HedefDeger = "x"; var eylem = new HedefSeciciEylemi(b, new ContextMenuStrip(), new Button());
        eylem.Yerel(); Assert.Equal("127.0.0.1", b.HedefDeger); Assert.Contains(b.Durumlar, d => d.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    [Fact]
    public void MuhurKontrolu_MuhurYokkenHataVermezVeGunlukYazmaz()
    {
        var b = new SahteBaglam(); MuhurKontrolu.AcilistaDenetle(b); Assert.Empty(b.Gunlukler);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void ServislerKlasoru_Form1ParcasiIcermez_BaglamArayuzuDarKalir()
    {
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null; i++, kok = Path.GetDirectoryName(kok)) if (File.Exists(Path.Combine(kok, "Directory.Build.props"))) break;
        if (kok == null) return;
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim", "Servisler"), "*.cs")) Assert.DoesNotContain("partial class Form1", File.ReadAllText(f), StringComparison.Ordinal);
        Assert.True(typeof(ITaramaBaglami).GetMembers().Length <= 24, "ITaramaBaglami şişmemeli (dar yüzey)");
    }
}
