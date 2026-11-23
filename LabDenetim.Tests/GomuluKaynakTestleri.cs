// Gömülü kaynak ve sistem çağrısı koruması (Faz 1.5): kural/eşleme JSON dosyaları BulutSoft.Core derlemesindedir ve oradan okunur;
// UI derlemesi üzerinden (GetExecutingAssembly) okuma yapılmaz; TCP tablosu okuyucusu tablo büyümesine karşı dayanıklıdır.
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Xunit;

// Test ad alanı.
namespace LabDenetim.Tests;

public class GomuluKaynakTestleri
{
    private static readonly string[] Kaynaklar = { "pasif_kurallar.json", "aktif_kurallar.json", "cpe_esleme.json" };

    [Fact]
    public void KuralVeEslemeDosyalari_CekirdekDerlemesindedir_ArayuzDerlemesindeDegil()
    {
        var cekirdek = typeof(PasifKurallar).Assembly; var arayuz = typeof(TemaliForm).Assembly;
        Assert.Equal("BulutSoft.Core", cekirdek.GetName().Name); Assert.NotEqual(cekirdek, arayuz);
        foreach (var k in Kaynaklar) { Assert.Contains(k, cekirdek.GetManifestResourceNames()); Assert.DoesNotContain(k, arayuz.GetManifestResourceNames()); }
    }

    [Fact]
    public void KuralMotoru_VeCpeEslemesi_GomuluDosyadanYuklenir()
    {
        var yok = Path.Combine(Path.GetTempPath(), "yok_" + Guid.NewGuid().ToString("N") + ".json");
        Assert.True(PasifKurallar.Yukle(out var u1, null, "pasif_kurallar.json", yok).Count > 0); Assert.Empty(u1);
        Assert.True(PasifKurallar.Yukle(out var u2, null, "aktif_kurallar.json", yok).Count > 0); Assert.Empty(u2);
        Assert.True(PasifKurallar.Yukle(out var u3, null, "yok_boyle_bir_kaynak.json", yok).Count == 0); Assert.NotEmpty(u3);   // bulunamayan kaynak uyarı verir
    }

    [Fact, Trait("Tur", "KaynakKurali")]
    public void Kaynakta_GetExecutingAssembly_KullanilmazVeAramaCekirdekteYapilir()
    {
        string? d = AppContext.BaseDirectory; string? kok = null;
        for (int i = 0; i < 8 && d != null; i++, d = Path.GetDirectoryName(d)) if (Directory.Exists(Path.Combine(d, "BulutSoft.Core"))) { kok = d; break; }
        if (kok == null) return;
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, "BulutSoft.Core"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
            Assert.DoesNotContain("GetExecutingAssembly", File.ReadAllText(f), StringComparison.Ordinal);
        }
        // Arayüzde yalnızca kendi gömülü kaynakları (araç tablosu, simge) okunabilir; kural/eşleme dosyaları için okuma YOK.
        foreach (var f in Directory.EnumerateFiles(Path.Combine(kok, "LabDenetim"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
            string kod = File.ReadAllText(f);
            foreach (var k in Kaynaklar) Assert.False(kod.Contains("GetManifestResourceStream(\"" + k + "\")", StringComparison.Ordinal), Path.GetFileName(f) + " " + k + " dosyasını arayüz derlemesinden okuyor");
        }
    }

    [Fact]
    public void TcpTablosu_YerelDinleyiciyiBulur_VeHataKodunuKontrolEder()
    {
        var dinleyici = new TcpListener(IPAddress.Loopback, 0); dinleyici.Start();
        try
        {
            int port = ((IPEndPoint)dinleyici.LocalEndpoint).Port; bool bulundu = false; int adet = 0;
            Assert.True(TcpTablosu.Oku(2, tablo =>
            {
                adet = Marshal.ReadInt32(tablo);
                for (int i = 0; i < adet; i++)
                {
                    IntPtr s = tablo + 4 + i * 24; int durum = Marshal.ReadInt32(s, 0), portHam = Marshal.ReadInt32(s, 8); int p = ((portHam & 0xFF) << 8) | ((portHam >> 8) & 0xFF);
                    if (p == port && durum == 2) bulundu = true;   // 2 = LISTEN
                }
            }));
            Assert.True(adet > 0); Assert.True(bulundu, "açık dinleyici tabloda görünmeli");
            // Geçersiz adres ailesi: dönüş kodu denetlenir, false döner ve hata kaydedilir (sessizce yutulmaz).
            HataKayit.Sifirla(); Assert.False(TcpTablosu.Oku(9999, _ => { })); Assert.True(HataKayit.Sayi("TcpTablosu.Oku(boyut)") + HataKayit.Sayi("TcpTablosu.Oku(tablo)") >= 1);
        }
        finally { dinleyici.Stop(); }
    }
}
