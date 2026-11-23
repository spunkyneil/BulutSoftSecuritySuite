// Şifreli bağlantı araçlarını çağır.
using System.Net.Security;
// Ağ bağlantısı araçlarını çağır.
using System.Net.Sockets;
// Şifreleme sürümü (protokol) araçlarını çağır.
using System.Security.Authentication;
// Sertifika (kimlik kartı) araçlarını çağır.
using System.Security.Cryptography.X509Certificates;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// ŞİFRELİ BAĞLANTI MODÜLÜ: sitenin sertifikasına (kimlik kartına) ve şifreleme sürümüne bakar.
public static class TlsDenetimi
{
    // Verilen adres ve kapıdaki şifreli bağlantıyı denetler.
    public static async Task<List<Bulgu>> DenetleAsync(string host, int port, CancellationToken ct)
    {
        // Bulguları toplayacağımız boş liste.
        var liste = new List<Bulgu>();
        // Sunucudan gelecek sertifika; şimdilik boş.
        X509Certificate2? sertifika = null;
        // Sertifikada çıkan sorunlar; şimdilik "sorun yok".
        SslPolicyErrors hatalar = SslPolicyErrors.None;

        // Sunucuya bağlanmak için hat aç.
        using var tcp = new TcpClient();
        // Sunucunun kapısını çal.
        await tcp.ConnectAsync(host, port, ct);
        // Hat üzerinde şifreli konuşma başlatıcı; sertifikayı yakalayıp biz inceleyeceğiz.
        using var ssl = new SslStream(tcp.GetStream(), false, (_, c, _, e) =>
        {
            // Sertifika geldiyse bir kopyasını sakla.
            if (c != null) sertifika = new X509Certificate2(c);
            // Sorunları sakla.
            hatalar = e;
            // true: bağlantıyı kesme, incelemeyi biz yapacağız.
            return true;
        });
        // Şifreli konuşmayı başlat.
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);

        // Sertifikada sorun varsa bildir.
        if (hatalar != SslPolicyErrors.None)
            // Bulguyu listeye ekle.
            liste.Add(new Bulgu("TLS", "Sertifika güvenilir değil", Onem.Orta, "Sorun türü: " + hatalar, "Güvenilir bir kuruluştan (test ortamında kendi kurumunuzdan) geçerli sertifika alın."));
        // Sertifika geldiyse içini incele.
        if (sertifika != null)
        {
            // Süresi bittiyse.
            if (sertifika.NotAfter < DateTime.Now)
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("TLS", "Sertifikanın süresi bitmiş", Onem.Yuksek, $"Bitiş tarihi: {sertifika.NotAfter:d}", "Sertifikayı yenileyin (kimlik kartı süresi dolmuş gibi)."));
            // 30 günden az kaldıysa.
            else if (sertifika.NotAfter < DateTime.Now.AddDays(30))
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("TLS", "Sertifika yakında bitecek", Onem.Dusuk, $"Bitiş tarihi: {sertifika.NotAfter:d}", "Yenilemeyi planlayın."));
            // Sahibi ile imzalayanı aynıysa (kendi kendine verilmiş kimlik).
            if (sertifika.Subject == sertifika.Issuer)
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("TLS", "Sertifika kendi kendine verilmiş", Onem.Dusuk, sertifika.Subject, "Gerçek kullanımda güvenilir bir kuruluştan sertifika alın."));
            // Anahtarın uzunluğunu (gücünü) öğren.
            var anahtarBoyu = sertifika.GetRSAPublicKey()?.KeySize ?? 0;
            // 2048'den küçükse zayıftır.
            if (anahtarBoyu is > 0 and < 2048)
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("TLS", "Şifre anahtarı zayıf", Onem.Yuksek, $"{anahtarBoyu} bit", "En az 2048 bitlik anahtar kullanın."));
            // Eski ve zayıf imza yöntemi (SHA-1) kullanılıyorsa.
            if (sertifika.SignatureAlgorithm.FriendlyName?.Contains("sha1", StringComparison.OrdinalIgnoreCase) == true)
                // Bulguyu listeye ekle.
                liste.Add(new Bulgu("TLS", "Eski ve zayıf imza yöntemi (SHA-1)", Onem.Yuksek, sertifika.SignatureAlgorithm.FriendlyName, "SHA-256 veya daha yenisiyle sertifikayı yeniden oluşturun."));
        }
        // Anlaşılan şifreleme sürümünü bilgi olarak yaz.
        liste.Add(new Bulgu("TLS", "Kullanılan şifreleme", Onem.Bilgi, $"{ssl.SslProtocol}, {ssl.NegotiatedCipherSuite}", "Yapılacak bir şey yok."));

        // Eski tip şifreleme sürümlerini kabul ediyor mu diye dene (obsolete uyarısını sustur).
#pragma warning disable SYSLIB0039
        // Eski iki sürümü sırayla dene.
        foreach (var p in new[] { SslProtocols.Tls11, SslProtocols.Tls })
        {
            // Kabul etmezse hata verir, bu iyi bir şeydir.
            try
            {
                // Yeni bir hat aç.
                using var t2 = new TcpClient();
                // Kapıyı çal.
                await t2.ConnectAsync(host, port, ct);
                // Şifreli konuşma başlatıcı.
                using var s2 = new SslStream(t2.GetStream(), false, (_, _, _, _) => true);
                // Yalnızca eski sürümle konuşmayı dene.
                await s2.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host, EnabledSslProtocols = p }, ct);
                // Buraya geldiysek eski sürüm kabul edildi: bu kötüdür.
                liste.Add(new Bulgu("TLS", $"Eski şifreleme sürümü açık: {p}", Onem.Yuksek, $"{p} kabul ediliyor.", "TLS 1.0 ve 1.1'i kapatın; sadece TLS 1.2 ve üstüne izin verin."));
            }
            // Reddedildi: iyi haber, bir şey yapma.
            catch (Exception hk1) { HataKayit.Sessiz(hk1, "TlsDenetimi.DenetleAsync"); }
        }
        // Uyarı susturmayı bitir.
#pragma warning restore SYSLIB0039
        // Listeyi geri ver.
        return liste;
    }
}
