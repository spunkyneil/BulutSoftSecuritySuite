// Form1 inceltme testleri (Faz 3 Adım 5): bağımsız mantık Form1 partial dosyalarından çıkarıldı (AnalizMetniUretici, OturumEylemleri, PencereHafizasi); boş partial kabuklar silindi;
// partial dosya sayısı yukarı çıkamaz (çıta testi). Kalan partial dosyaların listesi bilinçli olarak burada tutulur.
using System.Text.RegularExpressions;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class Form1InceltmeTestleri
{
    private static string? Kok() { string? d = AppContext.BaseDirectory; for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (File.Exists(Path.Combine(d, "Directory.Build.props"))) return d; return null; }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Form1PartialDosyaSayisi_ErkenCitayiAsmaz_TasinanDosyalardaForm1ParcasiYok()
    {
        string? kok = Kok(); if (kok == null) return;
        var partiallar = Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim"), "*.cs").Where(f => Regex.IsMatch(File.ReadAllText(f), @"partial class Form1\b")).Select(Path.GetFileName).ToList();
        Assert.True(partiallar.Count <= 2, $"Form1 partial dosya sayısı çıtayı aştı ({partiallar.Count} > 2): " + string.Join(", ", partiallar));
        // Bağımsız hâle getirilen dosyalarda artık Form1 parçası yok.
        foreach (var d in new[] { "KullaniciFormu.cs", "LisansFormu.cs", "PencereYonetimi.cs", "AgHaritasiFormu.cs", "BruteForceFormu.cs", "DurumFormu.cs", "GozetimsizFormu.cs", "KaliciFormu.cs", "OlayGunlugu.cs", "SensorFormu.cs", "Asistan.cs", "YapayZeka.cs", "Trafik.cs", "Kartlar.cs", "Gorunum.cs", "Filtre.cs", "Profil.cs", "Tablo.cs", "Gunluk.cs", "Gecmis.cs", "DisAgAkis.cs", "Tema.cs", "Menu.cs", "Bilgi.cs", "Nobet.cs", "KolayMod.cs", "Kisayol.cs", "MerkezFormu.cs", "UzmanKure.cs", "KanitPaketi.cs", "AltinProfil.cs", "AracKutusu.cs", "CihazEnvanteri.cs", "Zincir.cs", "AramaKabul.cs", "SensorTarama.cs", "UsbKalkani.cs", "Tuzaklar.cs", "HedefSec.cs", "Kalkan.cs" })
            Assert.DoesNotContain(d, partiallar);
        // YetkiKontrol tek yerde: KabukYetki (Form1'de kopya yok).
        Assert.DoesNotContain("private bool YetkiKontrol(", string.Join("\n", Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim"), "*.cs").Select(File.ReadAllText)), StringComparison.Ordinal);
    }

    private static AnalizBaglami Baglam(IReadOnlyList<Bulgu> b, bool yerel = true, string[]? kontrol = null) =>
        new("192.168.1.5", yerel, "Standart", 17, kontrol ?? new[] { "port", "dns" }, b, 1, 1, 1, 70, "Windows 11", new DateTime(2026, 11, 22, 9, 30, 0), x => x.Onem == Onem.Yuksek ? "KRİTİK" : x.Onem == Onem.Orta ? "UYARI" : "GÜVENLİ");

    [Fact]
    public void AnalizMetniUretici_BolumleriBulgulariVeIstekleriYazar_KisiselBilgiIcermez()
    {
        var b = new List<Bulgu> { new("Port", "Açık port 445 (SMB)", Onem.Yuksek, "SMB dışarıya açık\nikinci satır", "Kapatın."), new("DNS", "SPF eksik", Onem.Orta, "kayıt yok", "SPF ekleyin."), new("Port", "Açık port 80 (HTTP)", Onem.Bilgi, "normal", "-") };
        string m = AnalizMetniUretici.Olustur(Baglam(b), null, sohbetIcin: false);
        Assert.Contains("== TARAMA ÖZETİ ==", m, StringComparison.Ordinal); Assert.Contains("Hedef adres: 192.168.1.5 (bu bilgisayarın kendisi, Windows)", m, StringComparison.Ordinal); Assert.Contains("Tarih: ", m.Replace("Tarama tarihi: ", "Tarih: "), StringComparison.Ordinal);
        Assert.Contains("Tarama tarihi: 22.11.2026 09:30", m, StringComparison.Ordinal); Assert.Contains("Tarama profili: Standart; yoklanan kapı (port) sayısı: 17", m, StringComparison.Ordinal); Assert.Contains("Çalıştırılan kontroller: kapı yoklama, isim kayıtları (DNS)", m, StringComparison.Ordinal);
        Assert.Contains("Açık portlar: 445 (SMB), 80 (HTTP)", m, StringComparison.Ordinal); Assert.Contains("KRİTİK 1, UYARI/DİKKAT 1, GÜVENLİ 1, yapılamayan kontrol 0. Güvenlik puanı: 70/100.", m, StringComparison.Ordinal);
        Assert.Contains("1. [KRİTİK] Açık port 445 (SMB) — Kontrol: Port — Ayrıntı: SMB dışarıya açık ikinci satır — Programın önerisi: Kapatın.", m, StringComparison.Ordinal);   // satır sonları tek satıra iner
        Assert.Contains("== İSTEKLERİM ==", m, StringComparison.Ordinal); Assert.Contains("SAVUNMA amaçlı", m, StringComparison.Ordinal);
        Assert.DoesNotContain("ÖZELLİKLE ODAKLANILACAK", m, StringComparison.Ordinal); Assert.DoesNotContain("Merhaba!", m, StringComparison.Ordinal);
        Assert.DoesNotContain(Environment.MachineName, m, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("Kullanıcı adı", m, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalizMetniUretici_TekBulguOdagi_SohbetSelamlamasi_UzakHedefAciklamasi()
    {
        var b = new List<Bulgu> { new("Port", "Açık port 22 (SSH)", Onem.Orta, "ayrıntı", "Öneri.") };
        string m = AnalizMetniUretici.Olustur(Baglam(b, yerel: false, kontrol: Array.Empty<string>()), b[0], sohbetIcin: true);
        Assert.StartsWith("Merhaba!", m, StringComparison.Ordinal); Assert.Contains("== ÖZELLİKLE ODAKLANILACAK BULGU ==", m, StringComparison.Ordinal); Assert.Contains("[UYARI] Açık port 22 (SSH)", m, StringComparison.Ordinal);
        Assert.Contains("ağdaki başka bir (sanal) makine", m, StringComparison.Ordinal); Assert.Contains("Çalıştırılan kontroller: ", m, StringComparison.Ordinal);
        Assert.Equal(m, AnalizMetniUretici.Olustur(Baglam(b, yerel: false, kontrol: Array.Empty<string>()), b[0], sohbetIcin: true));   // saf: aynı girdi aynı metin
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void FormdanServiseGecis_YapayZekaFormuYalnizcaBaglamKurar()
    {
        string? kok = Kok(); if (kok == null) return;
        string kod = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Kontroller", "TaramaSayfasi.YapayZeka.cs"));
        Assert.Contains("AnalizMetniUretici.Olustur(new AnalizBaglami(", kod, StringComparison.Ordinal); Assert.DoesNotContain("sb.AppendLine(\"== TARAMA ÖZETİ ==\")", kod, StringComparison.Ordinal);
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Dilim4_NobetciKolayKisayolKaliKureAyriSiniflardadir_Form1SadeceKabukIskeleti()
    {
        string? kok = Kok(); if (kok == null) return; string lab = Path.Combine(kok, "LabDenetim");
        foreach (var (dosya, sinif) in new[] { ("NobetciEylemi", "NobetciEylemi"), ("KolayModEylemleri", "KolayModEylemleri"), ("KisayolEylemleri", "KisayolEylemleri"), ("KaliGostergeleri", "KaliGostergeleri"), ("UzmanKureYoneticisi", "UzmanKureYoneticisi") })
        {
            string kod = File.ReadAllText(Path.Combine(lab, "Servisler", dosya + ".cs"));
            Assert.Contains("internal sealed class " + sinif, kod, StringComparison.Ordinal);
            Assert.DoesNotContain("partial class Form1", kod, StringComparison.Ordinal); Assert.DoesNotContain("Form1 ", string.Join(Environment.NewLine, kod.Split((char)10).Where(s => !s.TrimStart().StartsWith("//"))), StringComparison.Ordinal);   // ana pencere yalnızca TemaliForm olarak görülür
        }
        Assert.True(File.ReadAllLines(Path.Combine(lab, "Form1.cs")).Length <= 190, "Form1.cs yalnızca iskelet kalmalı");
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Dilim5_KabukBaglantiTablosuFabrikadadir_Form1SadeceCagirir_DesignerEskiMenuYorumlariTemiz()
    {
        string? kok = Kok(); if (kok == null) return; string lab = Path.Combine(kok, "LabDenetim");
        string form = File.ReadAllText(Path.Combine(lab, "Form1.cs")); string fab = File.ReadAllText(Path.Combine(lab, "Kabuk", "KabukBaglantiFabrikasi.cs"));
        Assert.DoesNotContain("new KabukBaglantilari", form, StringComparison.Ordinal); Assert.Contains("new KabukBaglantilari", fab, StringComparison.Ordinal);
        Assert.DoesNotContain("Form1", string.Join(Environment.NewLine, fab.Split((char)10).Where(s => !s.TrimStart().StartsWith("//"))), StringComparison.Ordinal);   // fabrika Form1'e bağlı değil
        Assert.True(File.ReadAllLines(Path.Combine(lab, "Form1.Designer.cs")).Length <= 100, "Designer yalnızca iskelet kalmalı"); Assert.True(File.ReadAllLines(Path.Combine(lab, "Form1.cs")).Length <= 150, "Form1.cs 150 satırı aşmamalı");
    }
}
