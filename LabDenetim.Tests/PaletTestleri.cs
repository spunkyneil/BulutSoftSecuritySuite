// Renk paleti testleri: Koyu (varsayılan) ve Açık temanın HEX değerleri, rol eşlemesi, çift çevirme güvenliği ve "koyu renk sızıntısı kalmadı" korumaları.
using System.Text.RegularExpressions;
using Xunit;

namespace LabDenetim.Tests;

public class PaletTestleri
{
    static Color H(string hex) => ColorTranslator.FromHtml(hex);
    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    [Fact]
    public void Koyu_Tema_Palet_Degerleri_Birebir()
    {
        Assert.Equal(H("#141B2E"), Palet.Esle(C(8, 13, 24), true, true));      // sayfa
        Assert.Equal(H("#1C2438"), Palet.Esle(C(15, 23, 42), true, true));     // kart
        Assert.Equal(H("#161D30"), Palet.Esle(C(22, 30, 46), true, true));     // giriş kutusu
        Assert.Equal(H("#0F1522"), Palet.Esle(C(4, 8, 16), true, true));       // gömülü / kenar çubuğu koyusu
        Assert.Equal(H("#E8ECF4"), Palet.Esle(C(224, 242, 254), false, true)); // metin
        Assert.Equal(H("#8896AB"), Palet.Esle(C(148, 163, 184), false, true)); // soluk metin
        Assert.Equal(H("#6366F1"), Palet.Esle(C(12, 74, 110), true, true));    // birincil düğme
        Assert.Equal(H("#10B981"), Palet.Esle(C(74, 222, 128), false, true));  // başarı metni
        Assert.Equal(H("#EF4444"), Palet.Esle(C(248, 113, 113), false, true)); // tehlike metni
        Assert.Equal(H("#F59E0B"), Palet.Esle(C(251, 191, 36), false, true));  // uyarı metni
    }

    [Fact]
    public void Acik_Tema_Palet_Degerleri_Birebir()
    {
        Assert.Equal(H("#F0F4F8"), Palet.Esle(C(8, 13, 24), true, false));
        Assert.Equal(H("#FFFFFF"), Palet.Esle(C(15, 23, 42), true, false));
        Assert.Equal(H("#EEF2F7"), Palet.Esle(C(30, 41, 59), true, false));    // ızgara başlığı
        Assert.Equal(H("#DBEAFE"), Palet.Esle(C(30, 58, 95), true, false));    // seçim
        Assert.Equal(H("#0F172A"), Palet.Esle(C(224, 242, 254), false, false));
        Assert.Equal(H("#475569"), Palet.Esle(C(148, 163, 184), false, false));
        Assert.Equal(H("#6366F1"), Palet.Esle(C(255, 214, 10), true, false));  // sarı DÜĞME zemini birincil renge gider
        Assert.Equal(H("#4F46E5"), Palet.Esle(C(255, 214, 10), false, false)); // marka sarısı YAZI açık zeminde birincil (indigo) metin rengine gider
    }

    [Fact]
    public void Ayni_Renk_Zemin_Ve_Yazi_Olarak_Farkli_Davranir()
    {
        var sari = C(255, 214, 10);
        Assert.NotEqual(Palet.Esle(sari, true, true), Palet.Esle(sari, false, true));
        // Saf beyaz: zemin olarak kart, yazı olarak metin rengi.
        Assert.Equal(H("#FFFFFF"), Palet.Esle(Color.White, true, false)); Assert.Equal(H("#0F172A"), Palet.Esle(Color.White, false, false));
    }

    [Fact]
    public void Saydamlik_Korunur_Saydam_Renge_Dokunulmaz()
    {
        Assert.Equal(Color.Transparent, Palet.Esle(Color.Transparent, true, false)); Assert.Equal(40, Palet.Esle(Color.FromArgb(40, 15, 23, 42), true, false).A);
    }

    [Fact]
    public void Sistem_Renkleri_Her_Iki_Temada_Palete_Gider()
    {
        Assert.Equal(H("#161D30"), Palet.Esle(SystemColors.Window, true, true)); Assert.Equal(H("#FFFFFF"), Palet.Esle(SystemColors.Window, true, false));
        Assert.Equal(H("#E8ECF4"), Palet.Esle(SystemColors.WindowText, false, true)); Assert.Equal(H("#0F172A"), Palet.Esle(SystemColors.WindowText, false, false));
        Assert.Equal(H("#222D4A"), Palet.Esle(SystemColors.Highlight, true, true));
    }

    [Fact]
    public void Paletteki_Zaten_Cevrilmis_Renk_Tekrar_Cevrilince_Bozulmaz_Koyu()
    {
        foreach (var hex in new[] { "#141B2E", "#1C2438", "#161D30", "#2A3650", "#E8ECF4", "#8896AB", "#6366F1" }) Assert.Equal(H(hex), Palet.Esle(H(hex), true, true));
    }

    // Kaynak kodda bulunan HER sabit renk paletle eşlenmiş olmalı: yeni bir renk eklenirse (ve palete bağlanmazsa) açık temada koyu renk sızabilir; bu test bunu yakalar.
    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kodda_Bulunan_Her_Sabit_Renk_Palete_Bagli()
    {
        string? dizin = AppContext.BaseDirectory; string? kaynak = null;
        for (int i = 0; i < 8 && dizin != null; i++, dizin = Path.GetDirectoryName(dizin)) if (File.Exists(Path.Combine(dizin, "LabDenetim", "LabDenetim.csproj"))) { kaynak = Path.Combine(dizin, "LabDenetim"); break; }
        if (kaynak == null) return;   // kaynak ağacı yoksa atla
        var eksik = new SortedSet<string>(); var re = new Regex(@"Color\.FromArgb\((\d+),\s*(\d+),\s*(\d+)\)");
        foreach (var f in new[] { kaynak, Path.Combine(Path.GetDirectoryName(kaynak)!, "BulutSoft.Core") }.Where(Directory.Exists).SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories)))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || f.EndsWith("Palet.cs") || f.EndsWith("TemaYonetici.cs")) continue;
            foreach (Match m in re.Matches(File.ReadAllText(f)))
            {
                var c = C(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
                if (Palet.Rol(c, true) == PaletRol.Yok && Palet.Rol(c, false) == PaletRol.Yok) eksik.Add($"{c.R},{c.G},{c.B} ({Path.GetFileName(f)})");
            }
        }
        Assert.True(eksik.Count == 0, "Palete bağlanmamış renkler (Palet.cs içine ekleyin): " + string.Join("; ", eksik));
    }

    // Açık temada zemin rolleri açık, koyu yazı rolleri koyu olmalı (koyu zemin sızıntısı yok).
    [Theory]
    [InlineData((int)PaletRol.Sayfa)] [InlineData((int)PaletRol.Kart)] [InlineData((int)PaletRol.Girdi)] [InlineData((int)PaletRol.Gomulu)] [InlineData((int)PaletRol.GridBaslik)] [InlineData((int)PaletRol.Izgara)] [InlineData((int)PaletRol.Kenar)] [InlineData((int)PaletRol.Secim)] [InlineData((int)PaletRol.Hayalet)]
    public void Acik_Temada_Zemin_Rolleri_Acik(int rolNo) => Assert.True(Palet.Renk((PaletRol)rolNo, false).GetBrightness() > 0.8f, (PaletRol)rolNo + " açık temada koyu kaldı");

    [Theory]
    [InlineData((int)PaletRol.Metin)] [InlineData((int)PaletRol.IkincilMetin)] [InlineData((int)PaletRol.Soluk)] [InlineData((int)PaletRol.BirincilMetin)] [InlineData((int)PaletRol.BasariMetin)] [InlineData((int)PaletRol.UyariMetin)] [InlineData((int)PaletRol.TehlikeMetin)] [InlineData((int)PaletRol.BilgiMetin)]
    public void Acik_Temada_Yazi_Rolleri_Beyaz_Zeminde_Okunur(int rolNo) => Assert.True(Palet.Renk((PaletRol)rolNo, false).GetBrightness() < 0.6f, (PaletRol)rolNo + " beyaz zeminde silik");

    [Theory]
    [InlineData((int)PaletRol.Sayfa)] [InlineData((int)PaletRol.Kart)] [InlineData((int)PaletRol.Girdi)] [InlineData((int)PaletRol.Gomulu)] [InlineData((int)PaletRol.GridBaslik)]
    public void Koyu_Temada_Zemin_Rolleri_Koyu(int rolNo) => Assert.True(Palet.Renk((PaletRol)rolNo, true).GetBrightness() < 0.2f, (PaletRol)rolNo + " koyu temada açık kaldı");
}
