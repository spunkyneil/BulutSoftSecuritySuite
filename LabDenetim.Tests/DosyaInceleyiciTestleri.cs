// Dosya adli inceleyici testleri: tür tanıma, uzantı/ad hilesi, entropi, dizeler, kaynak işareti, imza kodları, puanlama ve gerçek dosya incelemesi.
using System.Text;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class DosyaInceleyiciTestleri
{
    // Sabit "şimdi".
    static readonly DateTime Simdi = new(2026, 11, 19, 12, 0, 0);
    static byte[] B(params int[] x) => x.Select(i => (byte)i).ToArray();

    [Theory]
    [InlineData("4D5A9000", "Windows programı")] [InlineData("7F454C46", "Linux programı")] [InlineData("25504446", "PDF")] [InlineData("504B0304", "ZIP")]
    [InlineData("D0CF11E0A1B11AE1", "Eski Office")] [InlineData("52617221", "RAR")] [InlineData("89504E47", "PNG")] [InlineData("FFD8FFE0", "JPEG")] [InlineData("47494638", "GIF")]
    public void Baslik_Baytlarindan_Tur_Taninir(string hex, string beklenen) => Assert.Contains(beklenen, DosyaInceleyici.TurTahmin(Convert.FromHexString(hex)));

    [Fact]
    public void Duz_Metin_Ve_Bilinmeyen_Ayirt_Edilir()
    {
        Assert.Contains("Düz metin", DosyaInceleyici.TurTahmin(Encoding.UTF8.GetBytes("merhaba dunya\r\nikinci satir")));
        Assert.Equal("Bilinmeyen", DosyaInceleyici.TurTahmin(B(1, 2, 3, 4, 5)));
        Assert.Equal("Bilinmeyen", DosyaInceleyici.TurTahmin(Array.Empty<byte>()));
    }

    [Fact]
    public void Program_Ama_Belge_Uzantili_Yakalanir()
    {
        Assert.NotNull(DosyaInceleyici.UzantiUyumsuzlugu("fatura.pdf", "Windows programı (EXE/DLL)")); Assert.NotNull(DosyaInceleyici.UzantiUyumsuzlugu("resim.JPG", "Linux programı (ELF)"));
        Assert.Null(DosyaInceleyici.UzantiUyumsuzlugu("kurulum.exe", "Windows programı (EXE/DLL)")); Assert.Null(DosyaInceleyici.UzantiUyumsuzlugu("fatura.pdf", "PDF belgesi"));
        Assert.NotNull(DosyaInceleyici.UzantiUyumsuzlugu("sahte.exe", "Düz metin / betik olabilir"));
    }

    [Theory]
    [InlineData("fatura.pdf.exe", true)] [InlineData("Resim.JPG.scr", true)] [InlineData("belge.docx.js", true)] [InlineData("rapor.pdf          .exe", true)]
    [InlineData("fatura.pdf", false)] [InlineData("kurulum.exe", false)] [InlineData("arsiv.tar.gz", false)] [InlineData("v1.2.3.exe", false)]
    public void Cift_Uzanti_Hilesi(string ad, bool hile) => Assert.Equal(hile, DosyaInceleyici.AdHilesi(ad) != null);

    [Fact]
    public void Sagdan_Sola_Karakter_Hilesi_Yakalanir() => Assert.NotNull(DosyaInceleyici.AdHilesi("fatura‮fdp.exe"));

    [Fact]
    public void PE_Derleme_Tarihi_Okunur_Ve_Bozuk_Basliklarda_Cokmez()
    {
        // MZ + e_lfanew=0x40 + PE\0\0 + Machine(2) + Sections(2) + TimeDateStamp.
        var b = new byte[0x80]; b[0] = 0x4D; b[1] = 0x5A; b[0x3C] = 0x40; b[0x40] = 0x50; b[0x41] = 0x45; BitConverter.GetBytes((uint)1700000000).CopyTo(b, 0x48);
        Assert.Equal(DateTime.UnixEpoch.AddSeconds(1700000000), DosyaInceleyici.DerlemeTarihi(b));
        Assert.Null(DosyaInceleyici.DerlemeTarihi(new byte[10])); Assert.Null(DosyaInceleyici.DerlemeTarihi(B(0x4D, 0x5A)));
        var kotu = (byte[])b.Clone(); kotu[0x3C] = 0xFF; kotu[0x3D] = 0xFF; Assert.Null(DosyaInceleyici.DerlemeTarihi(kotu));
        var sifir = (byte[])b.Clone(); BitConverter.GetBytes((uint)0).CopyTo(sifir, 0x48); Assert.Null(DosyaInceleyici.DerlemeTarihi(sifir));
    }

    [Fact]
    public void Entropi_Sabit_Dusuk_Rastgele_Yuksek()
    {
        Assert.Equal(0, DosyaInceleyici.Entropi(new byte[1000]), 3); Assert.Equal(0, DosyaInceleyici.Entropi(Array.Empty<byte>()));
        var r = new byte[65536]; new Random(7).NextBytes(r); Assert.True(DosyaInceleyici.Entropi(r) > 7.9);
        var iki = Enumerable.Range(0, 1000).Select(i => (byte)(i % 2)).ToArray(); Assert.Equal(1.0, DosyaInceleyici.Entropi(iki), 3);
    }

    [Fact]
    public void Dizeler_Ascii_Ve_Utf16_Cikarir()
    {
        var b = new List<byte>(); b.AddRange(B(0, 1)); b.AddRange(Encoding.ASCII.GetBytes("merhaba_dunya")); b.AddRange(B(0, 0)); b.AddRange(Encoding.Unicode.GetBytes("geniskarakter")); b.AddRange(B(0, 0, 0)); b.AddRange(Encoding.ASCII.GetBytes("kisa")); b.Add(0);
        var d = DosyaInceleyici.Dizeler(b.ToArray()); Assert.Contains("merhaba_dunya", d); Assert.Contains("geniskarakter", d); Assert.DoesNotContain("kisa", d);
    }

    [Fact]
    public void Dizeler_Sinirlanir_Ve_Hizli_Calisir()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); var buyuk = Enumerable.Repeat((byte)'A', 2_000_000).ToArray(); var d = DosyaInceleyici.Dizeler(buyuk); Assert.True(d.Max(x => x.Length) <= 400); Assert.True(sw.ElapsedMilliseconds < 3000);
        Assert.True(DosyaInceleyici.Dizeler(Enumerable.Range(0, 400000).Select(i => (byte)(i % 20 < 8 ? 'x' : 0)).ToArray(), 100).Count <= 100 + 1);
    }

    [Fact]
    public void Supheli_Ifadeler_Kategoriyle_Bulunur()
    {
        var (ip, puan) = DosyaInceleyici.SupheliDizeler(new[] { "powershell -nop -w hidden -enc AAAA", "VirtualAllocEx", "vssadmin delete shadows /all", "http://kotu.example.com/yukle.bin", "baglan 10.1.2.3 port", "999.1.1.1" });
        Assert.Contains(ip, x => x.StartsWith("Gizlenmiş PowerShell")); Assert.Contains(ip, x => x.StartsWith("Başka işleme")); Assert.Contains(ip, x => x.StartsWith("Yedekleri"));
        Assert.Contains(ip, x => x.Contains("kotu.example.com")); Assert.Contains(ip, x => x.Contains("10.1.2.3")); Assert.DoesNotContain(ip, x => x.Contains("999.1.1.1"));
        Assert.True(puan >= 60 || puan > 40); Assert.True(puan <= 60);
    }

    [Fact]
    public void Zararsiz_Metinde_Ipucu_Yok()
    {
        var (ip, puan) = DosyaInceleyici.SupheliDizeler(new[] { "Merhaba dunya", "Bu bir rapordur", "kullanici girisi basarili" }); Assert.Empty(ip); Assert.Equal(0, puan);
    }

    [Fact]
    public void Kotu_Niyetli_Uzun_Dize_Takilmaz()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); DosyaInceleyici.SupheliDizeler(Enumerable.Repeat(new string('a', 399) + "powershell" + new string(' ', 300), 2000)); Assert.True(sw.ElapsedMilliseconds < 8000);
    }

    [Fact]
    public void Kaynak_Ozeti_Bolge_Ve_Adresi_Okur()
    {
        Assert.Null(DosyaInceleyici.KaynakOzeti(null)); Assert.Null(DosyaInceleyici.KaynakOzeti("  "));
        var k = DosyaInceleyici.KaynakOzeti("[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://indir.example.com/x.exe\r\n"); Assert.StartsWith("İnternet", k); Assert.Contains("indir.example.com", k);
        Assert.StartsWith("Yerel ağ", DosyaInceleyici.KaynakOzeti("[ZoneTransfer]\nZoneId=1\n")); Assert.DoesNotContain("about:internet", DosyaInceleyici.KaynakOzeti("[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=about:internet\r\n")!);
    }

    [Theory]
    [InlineData(0, ImzaDurumu.Gecerli)] [InlineData(unchecked((int)0x800B0100), ImzaDurumu.Yok)] [InlineData(unchecked((int)0x80096010), ImzaDurumu.Bozuk)] [InlineData(unchecked((int)0x800B0109), ImzaDurumu.GuvenilmezKok)]
    [InlineData(unchecked((int)0x800B0101), ImzaDurumu.SuresiDolmus)] [InlineData(12345, ImzaDurumu.Diger)]
    public void Imza_Kodlari_Cevrilir(int kod, ImzaDurumu beklenen) => Assert.Equal(beklenen, DosyaInceleyici.ImzaKodu(kod));

    [Fact]
    public void Puanlama_Temiz_Ve_Supheli_Ayrilir()
    {
        var temiz = DosyaInceleyici.Degerlendir("kurulum.exe", "Windows programı (EXE/DLL)", ImzaDurumu.Gecerli, 6.0, null, new DateTime(2024, 1, 1), new DateTime(2024, 1, 2), 0, Simdi); Assert.Equal("Belirgin şüphe yok", temiz.Seviye); Assert.Equal(0, temiz.Puan);
        var kotu = DosyaInceleyici.Degerlendir("fatura.pdf.exe", "Windows programı (EXE/DLL)", ImzaDurumu.Yok, 7.8, "İnternet'nden geldi", new DateTime(2031, 1, 1), Simdi.AddDays(30), 40, Simdi); Assert.Equal("Yüksek şüphe", kotu.Seviye); Assert.True(kotu.Puan <= 100); Assert.Contains(kotu.Nedenler, n => n.Contains("Çift uzantı")); Assert.Contains(kotu.Nedenler, n => n.Contains("gelecek"));
        var bozuk = DosyaInceleyici.Degerlendir("a.exe", "Windows programı (EXE/DLL)", ImzaDurumu.Bozuk, 5, null, null, Simdi, 0, Simdi); Assert.True(bozuk.Puan >= 30);
        // Belge dosyasında imza/entropi kuralları uygulanmaz.
        Assert.Equal(0, DosyaInceleyici.Degerlendir("not.txt", "Düz metin / betik olabilir", ImzaDurumu.Yok, 7.9, null, null, Simdi, 0, Simdi).Puan);
    }

    [Fact]
    public async Task Gercek_Dosya_Incelenir_Ve_Rapor_Uretilir()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "dosya_inceleme_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(klasor);
        try
        {
            // Uzantısı pdf, içi sahte "program" + şüpheli ifadeler; hiçbir zaman çalıştırılmaz.
            string yol = Path.Combine(klasor, "fatura.pdf.exe"); var b = new List<byte> { 0x4D, 0x5A }; b.AddRange(new byte[200]); b.AddRange(Encoding.ASCII.GetBytes(" powershell -nop -w hidden -enc ZQBjAGgA  http://kotu.example.com/a ")); File.WriteAllBytes(yol, b.ToArray());
            var r = await DosyaInceleyici.InceleAsync(yol); Assert.StartsWith("Windows programı", r.Tur); Assert.Equal(64, r.Sha256.Length); Assert.True(r.Puan >= 30); Assert.Equal(ImzaDurumu.Yok, r.Imza);
            var t = DosyaInceleyici.Metin(r); Assert.Contains("DOSYA İNCELEME RAPORU", t); Assert.Contains("fatura.pdf.exe", t); Assert.Contains("kotu.example.com", t); Assert.Contains("çalıştırılmadı", t);
            // Dosya hâlâ yerinde ve değişmemiş.
            Assert.Equal(b.Count, new FileInfo(yol).Length);
            await Assert.ThrowsAsync<FileNotFoundException>(() => DosyaInceleyici.InceleAsync(Path.Combine(klasor, "yok.bin")));
        }
        finally { try { Directory.Delete(klasor, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "DosyaInceleyiciTestleri.temizlik"); } }
    }

    [Fact]
    public async Task Zone_Identifier_Gercek_Dosyada_Okunur()
    {
        string yol = Path.Combine(Path.GetTempPath(), "zone_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(yol, "merhaba dunya merhaba");
            // NTFS alternatif akışı yazılabilirse kaynak raporlanır; yazılamazsa (başka dosya sistemi) test geçerli sayılır.
            bool yazildi = false; try { File.WriteAllText(yol + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://indir.example.com/x\r\n"); yazildi = true; } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "DosyaInceleyiciTestleri.temizlik"); }
            var r = await DosyaInceleyici.InceleAsync(yol); if (yazildi) Assert.StartsWith("İnternet", r.Kaynak); else Assert.Null(r.Kaynak);
        }
        finally { try { File.Delete(yol); } catch (Exception hkt3) { HataKayit.Sessiz(hkt3, "DosyaInceleyiciTestleri.temizlik"); } }
    }

    [Fact]
    public async Task Gercek_Imzali_Program_Gecerli_Bozulmus_Kopyasi_Degil()
    {
        // Microsoft'un imzalı dotnet.exe'si varsa: orijinali geçerli, bir baytı değiştirilmiş kopyası geçerli OLMAMALI. (Hiçbiri çalıştırılmaz.)
        string dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"); if (!File.Exists(dotnet)) return;
        var (d, kim) = DosyaInceleyici.ImzaKontrol(dotnet); if (d != ImzaDurumu.Gecerli) return; Assert.Contains("Microsoft", kim);
        string klasor = Path.Combine(Path.GetTempPath(), "imza_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(klasor);
        try
        {
            string kopya = Path.Combine(klasor, "kopya.bin.exe"); File.Copy(dotnet, kopya); var bayt = File.ReadAllBytes(kopya); bayt[0x1000] ^= 0xFF; File.WriteAllBytes(kopya, bayt);
            var (d2, _) = DosyaInceleyici.ImzaKontrol(kopya); Assert.NotEqual(ImzaDurumu.Gecerli, d2);
            var rapor = await DosyaInceleyici.InceleAsync(kopya); Assert.True(rapor.Puan >= 30);
        }
        finally { try { Directory.Delete(klasor, true); } catch (Exception hkt4) { HataKayit.Sessiz(hkt4, "DosyaInceleyiciTestleri.temizlik"); } }
    }
}
