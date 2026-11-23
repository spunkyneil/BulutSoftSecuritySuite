// Bu dosya ana ekranın "Tarama yeri" seçimini ve sensörden kapı taramasını bağlar (Aşama 2).
// "Bu bilgisayar" seçiliyken hiçbir şey değişmez: mevcut tarama motoru çalışır. Bir Kali sensörü seçilirse, kapı yoklaması
// ağ kartınızdan DEĞİL sensörden yapılır; bulunan açık kapılar yine aynı tabloya, radara, geçmişe ve raporlara akar.
using System.Net;
using System.Text;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Sensör taramasının yardımcıları.
internal static class SensorTaramaYardimcisi
{
    // Kapı numaralarını sıkıştırıp (örn. 1-1024,3306) ajanın kabul ettiği boyutta (en çok 100 karakter) gruplara böler.
    public static List<string> PortGruplari(int[] portlar)
    {
        // Sıralı ve tekrarsız liste.
        var s = portlar.Where(p => p is >= 1 and <= 65535).Distinct().OrderBy(x => x).ToList();
        // Ardışık kapıları aralık yap (bir aralık en çok 10000 kapı olsun).
        var parcalar = new List<string>(); int i = 0;
        // Tüm kapılar.
        while (i < s.Count)
        {
            // Aralığın sonunu bul.
            int j = i; while (j + 1 < s.Count && s[j + 1] == s[j] + 1 && s[j + 1] - s[i] < 10000) j++;
            // Tek kapı veya aralık.
            parcalar.Add(j > i ? $"{s[i]}-{s[j]}" : s[i].ToString()); i = j + 1;
        }
        // 100 karakterlik gruplara böl.
        var gruplar = new List<string>(); var sb = new StringBuilder();
        // Her parça.
        foreach (var p in parcalar)
        {
            // Sığmıyorsa grubu kapat.
            if (sb.Length > 0 && sb.Length + 1 + p.Length > 100) { gruplar.Add(sb.ToString()); sb.Clear(); }
            // Ekle.
            if (sb.Length > 0) sb.Append(','); sb.Append(p);
        }
        // Son grup.
        if (sb.Length > 0) gruplar.Add(sb.ToString());
        // Sonuç.
        return gruplar;
    }

    // Sensörden kapı taraması yapar (ajan JSON akışı); sonuçları mevcut PortSonucu biçimine çevirir. Hata olursa InvalidOperationException atar.
    public static async Task<List<PortSonucu>> PortTaraAsync(SensorProfili p, string ip, int[] portlar, IProgress<int>? ilerleme, CancellationToken ct, Action<PortSonucu>? bulundu = null)
    {
        // Sonuçlar ve ilerleme kaydırması.
        var sonuc = new List<PortSonucu>(); int ofset = 0;
        // Her kapı grubu için bir ssh çağrısı.
        foreach (var grup in PortGruplari(portlar))
        {
            // Komut (girdiler doğrulanır; geçersizse ArgumentException).
            string komut = SensorKomutlari.TcpKomutu(ip, grup); int grupToplam = 0; string? ajanHata = null;
            // Çalıştır ve satırları işle.
            var (kod, hata) = await SensorTaramaYurutucu.CalistirAsync(p, komut, SensorAjani.Kaynak, satir =>
            {
                // Çöz (bozuk satır yok sayılır).
                var o = SensorCiktiAyristirici.Ayristir(satir); if (o == null) return;
                // Türüne göre.
                switch (o.T)
                {
                    // Grup başladı.
                    case "start": grupToplam = o.Toplam; break;
                    // Açık kapı.
                    case "open": if (o.Kapi is >= 1 and <= 65535) { var ps = new PortSonucu(o.Kapi, PortTarayici.ServisAdi(o.Kapi), o.Baslik); sonuc.Add(ps); bulundu?.Invoke(ps); } break;
                    // İlerleme.
                    case "prog": ilerleme?.Report(ofset + o.Biten); break;
                    // Ajan hata bildirdi.
                    case "error": ajanHata = o.Mesaj; break;
                }
            }, ct, 600);
            // Kullanıcı durdurduysa.
            ct.ThrowIfCancellationRequested();
            // Başarısızsa anlaşılır hata ver (tarama sessizce "temiz" görünmesin).
            if (kod != 0) throw new InvalidOperationException($"Sensör ({p.Ad}) taraması başarısız: " + (ajanHata ?? SensorCiktiAyristirici.HataAciklama(hata)) + "\r\n\r\nSensör bağlantısını \"Uzak Sensörler\" penceresinden sınayın veya Tarama yeri olarak \"Bu bilgisayar\"ı seçin.");
            // Bir sonraki grup için ilerlemeyi kaydır.
            ofset += grupToplam; ilerleme?.Report(ofset);
        }
        // Kapı sırasıyla ver.
        return sonuc.OrderBy(x => x.Port).ToList();
    }
}
