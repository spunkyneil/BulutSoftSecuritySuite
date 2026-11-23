// Bu dosya KVKK / ISO 27001 eşleştirme testlerini içerir.
namespace LabDenetim.Tests;

// Uyumluluk testleri.
public class UyumlulukTestleri
{
    // Bulgu üretir.
    private static Bulgu B(string modul, string baslik, Onem o = Onem.Yuksek, string detay = "") => new(modul, baslik, o, detay, "x");

    // Yeşil ve atlanan sonuçlar etiketlenmez.
    [Fact] public void Yesil_VeAtlanan_Etiketsiz()
    {
        Assert.Null(Uyumluluk.Bul(B("Port", "iyi", Onem.Bilgi))); Assert.Null(Uyumluluk.Bul(new Bulgu("Port", "Bu kontrol çalıştırılamadı", Onem.Orta, "", "")));
        Assert.Equal("", Uyumluluk.Yazi(B("Port", "iyi", Onem.Bilgi))); Assert.Empty(Uyumluluk.Uzun(B("Port", "iyi", Onem.Bilgi)));
    }
    // Açık RDP kapısı: KVKK m.12/1, ağ güvenliği ve erişim kontrolü.
    [Fact] public void Rdp_Esleme()
    {
        var e = Uyumluluk.Bul(B("Port", "Açık kapı: 3389 (RDP)"))!; Assert.Equal("KVKK m.12/1", e.KvkkKisa); Assert.Contains("A.8.20", e.IsoKodlari); Assert.Contains("A.5.15", e.IsoKodlari);
    }
    // Şifresiz protokol: kriptografi kontrolü de eklenir.
    [Fact] public void Telnet_Kriptografi() => Assert.Contains("A.8.24", Uyumluluk.Bul(B("Port", "Açık kapı: 23 (Telnet)"))!.IsoKodlari);
    // TLS zayıflığı: kriptografi.
    [Fact] public void Tls_Kriptografi() => Assert.Contains("A.8.24", Uyumluluk.Bul(B("TLS", "Süresi dolmuş sertifika", Onem.Orta))!.IsoKodlari);
    // Defender kapalı: kötü amaçlı yazılımdan korunma.
    [Fact] public void Defender_Esleme() => Assert.Equal(new[] { "A.8.7" }, Uyumluluk.Bul(B("Karne", "Defender kapalı"))!.IsoKodlari);
    // Zincirleme risk: KVKK m.12/3 (denetim).
    [Fact] public void Zincir_K3() => Assert.Equal("KVKK m.12/3", Uyumluluk.Bul(B("Zincir", "Zincir"))!.KvkkKisa);
    // Uzun yazım 2013 karşılığını içerir.
    [Fact] public void Uzun_EskiKarsilik() => Assert.Contains(Uyumluluk.Uzun(B("Port", "Açık kapı: 3389 (RDP)")), s => s.Contains("A.8.20 Ağ Güvenliği (2013: A.13.1.1)"));
    // Özet sayımı.
    [Fact] public void Ozet_Sayar()
    {
        var o = Uyumluluk.Ozet(new[] { B("Port", "3389 RDP"), B("Port", "445 SMB"), B("Karne", "Defender kapalı") });
        Assert.Equal(3, o.First(x => x.Kontrol == "KVKK m.12/1").Adet); Assert.Equal(2, o.First(x => x.Kontrol == "ISO A.8.20").Adet); Assert.Contains(o, x => x.Kontrol == "ISO A.8.7" && x.Adet == 1);
    }
    // Her ISO kodunun tanımı var.
    [Fact] public void Tum_Kodlar_Tanimli()
    {
        foreach (var m in new[] { "Port", "HTTP", "TLS", "Girdi", "DNS", "Hosts", "Kalıcılık", "Karne", "Wi-Fi", "Hesap", "Yapılandırma", "Zincir", "Referans" })
            foreach (var c in Uyumluluk.Bul(B(m, "x"))!.IsoKodlari) Assert.True(Uyumluluk.Iso.ContainsKey(c), m + " -> " + c);
    }
    // HTML raporda özet ve sütun bulunur.
    [Fact] public void HtmlRapor_Uyumluluk()
    {
        string y = Path.Combine(Path.GetTempPath(), "bs_rapor_" + Guid.NewGuid().ToString("N") + ".html");
        try { RaporUretici.HtmlYaz(y, new RaporBilgisi("127.0.0.1", DateTime.Now, "Standart", 17, TimeSpan.FromSeconds(3), 60, 1, 0, 0, 0), new[] { B("Port", "Açık kapı: 3389 (RDP)") }); string h = File.ReadAllText(y); Assert.Contains("Uyumluluk Özeti", h); Assert.Contains("KVKK m.12/1", h); Assert.Contains("ISO/IEC 27001:2022 A.8.20", h); }
        finally { try { File.Delete(y); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "UyumlulukTestleri.temizlik"); } }
    }
    // PDF rapor üretilir.
    [Fact] public void PdfRapor_Uretilir()
    {
        string y = Path.Combine(Path.GetTempPath(), "bs_rapor_" + Guid.NewGuid().ToString("N") + ".pdf");
        try { RaporUretici.PdfYaz(y, new RaporBilgisi("127.0.0.1", DateTime.Now, "Standart", 17, TimeSpan.FromSeconds(3), 60, 1, 0, 0, 0), new[] { B("Port", "Açık kapı: 3389 (RDP)") }); Assert.True(new FileInfo(y).Length > 1000); }
        finally { try { File.Delete(y); } catch (Exception hkt2) { HataKayit.Sessiz(hkt2, "UyumlulukTestleri.temizlik"); } }
    }
}
