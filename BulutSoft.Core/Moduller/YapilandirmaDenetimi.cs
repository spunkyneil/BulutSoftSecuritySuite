// Ağ adresleri için gerekli araçları çağır.
using System.Net;
// Ağ bağlantısı araçlarını çağır.
using System.Net.Sockets;
// Yazı dönüşümü araçlarını çağır.
using System.Text;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// ZAYIF AYARLAR MODÜLÜ: açık bulunan kapılara bakıp tehlikeli ayarları (örn. şifresiz giriş) yakalar.
public static class YapilandirmaDenetimi
{
    // Açık kapı listesini değerlendirir.
    public static async Task<List<Bulgu>> DenetleAsync(IPAddress ip, List<PortSonucu> acik, CancellationToken ct)
    {
        // Bulguları toplayacağımız boş liste.
        var liste = new List<Bulgu>();
        // Açık her kapı için bak.
        foreach (var p in acik)
        {
            // Kapı numarasına göre karar ver.
            switch (p.Port)
            {
                // 23 numaralı kapı: Telnet.
                case 23:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "Telnet açık (şifresiz uzaktan bağlantı)", Onem.Yuksek, "Port 23 (Kapı)\nTelnet'te parolalar düz yazıyla gider; ağı dinleyen herkes okur (postaya kartpostalla parola yazmak gibi).", "Telnet'i kapatın, yerine SSH kullanın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 21 numaralı kapı: FTP (dosya aktarımı).
                case 21:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "FTP açık (şifresiz dosya aktarımı)", Onem.Orta, $"Port 21 (Kapı)\n{Kimlik(p.Banner)}", "Şifreli SFTP veya FTPS'e geçin."));
                    // Anonim (kimliksiz) girişe izin veriyor mu diye dene.
                    if (await FtpAnonimAsync(ip, ct))
                        // Evet veriyorsa bulguyu ekle.
                        liste.Add(new Bulgu("Yapılandırma", "FTP kimliksiz girişe açık", Onem.Yuksek, "Port 21 (Kapı)\n'anonymous' adıyla, parolasız içeri girilebildi (kapıda kimlik sorulmuyor).", "Anonim girişi kapatın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 22 numaralı kapı ve tabelası çok eski SSH-1 ise.
                case 22 when p.Banner.StartsWith("SSH-1.", StringComparison.Ordinal):
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "Çok eski ve kırılmış SSH sürümü", Onem.Yuksek, $"Port 22 (Kapı)\n{Kimlik(p.Banner)}", "Yalnızca SSH-2 kullanılsın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 22 numaralı kapı (normal SSH).
                case 22:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "SSH açık", Onem.Bilgi, $"Port 22 (Kapı)\n{Kimlik(p.Banner)}", "Parola yerine anahtar ile girişi tercih edin; root girişini kapatın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 3389 (Windows uzak masaüstü) veya 5900 (VNC ekran paylaşımı).
                case 3389 or 5900:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", $"{p.Servis} (ekran paylaşımı) erişime açık", Onem.Orta, $"Port {p.Port} (Kapı)", "Güvenlik duvarının arkasına alın; güçlü parola kullanın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 445 veya 139: Windows dosya paylaşımı.
                case 445 or 139:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "Dosya paylaşımı (SMB) açık", Onem.Orta, $"Port {p.Port} (Kapı)", "Gerekmiyorsa kapatın; eski SMBv1'i devre dışı bırakın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 3306 veya 5432: veritabanı.
                case 3306 or 5432:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "Veritabanı ağdan erişilebilir", Onem.Orta, $"{p.Servis} · Port {p.Port} (Kapı)", "Veritabanını yalnızca aynı bilgisayardan erişilir yapın veya güvenlik duvarıyla kısıtlayın."));
                    // Bu kapı için işimiz bitti.
                    break;
                // 80 veya 8080: şifresiz web.
                case 80 or 8080:
                    // Bulguyu listeye ekle.
                    liste.Add(new Bulgu("Yapılandırma", "Şifresiz web sitesi hizmeti", Onem.Dusuk, $"Port {p.Port} (Kapı)\n{Kimlik(p.Banner)}", "Siteyi şifreli (https) yapın."));
                    // Bu kapı için işimiz bitti.
                    break;
            }
        }
        // Listeyi geri ver.
        return liste;
    }

    // Servisin kendini tanıttığı ilk yanıtı (servis kimliği) okunur yazıya çevirir; boşsa "okunamadı" der.
    static string Kimlik(string banner) => string.IsNullOrWhiteSpace(banner) ? "Servis kimliği (yanıt) okunamadı" : "Servis kimliği (yanıt): " + banner;

    // FTP kapısına "anonymous" adıyla, parolasız girmeyi dener; başarılıysa true verir.
    static async Task<bool> FtpAnonimAsync(IPAddress ip, CancellationToken ct)
    {
        // Bir şey ters giderse "hayır" de.
        try
        {
            // Yeni bir hat aç.
            using var c = new TcpClient();
            // 21 numaralı kapıyı çal.
            await c.ConnectAsync(ip, 21, ct);
            // Gelen cevapları okuyacak araç.
            using var rd = new StreamReader(c.GetStream(), Encoding.ASCII);
            // Mesaj yollayacak araç.
            using var wr = new StreamWriter(c.GetStream(), Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            // Karşılama cümlesini oku.
            await rd.ReadLineAsync(ct);
            // Kullanıcı adı olarak "anonymous" gönder.
            await wr.WriteLineAsync("USER anonymous");
            // Cevabı oku.
            await rd.ReadLineAsync(ct);
            // Sıradan bir e-posta benzeri parola gönder.
            await wr.WriteLineAsync("PASS lab@example.local");
            // Sunucunun kararını oku.
            var yanit = await rd.ReadLineAsync(ct);
            // "230" ile başlıyorsa giriş kabul edilmiştir.
            return yanit?.StartsWith("230", StringComparison.Ordinal) == true;
        }
        // Bağlanılamadı veya hata oldu: kabul edilmedi say.
        catch { return false; }
    }
}
