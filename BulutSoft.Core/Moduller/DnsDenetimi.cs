// Ağ adresleri için gerekli araçları çağır.
using System.Net;
// Ağ kartlarını (DNS sunucularını) okumak için.
using System.Net.NetworkInformation;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// İSİM KAYDI MODÜLÜ: bir alan adının (isim) hangi numaraya bağlı olduğuna ve unutulmuş alt isimlere bakar.
public static class DnsDenetimi
{
    // Denenecek yaygın alt isimler (admin.lab.local gibi). Telefon rehberinde bu isimleri arıyoruz.
    static readonly string[] Alt = { "www", "mail", "ftp", "admin", "dev", "test", "staging", "api", "vpn", "intranet", "db", "git", "backup", "old", "portal" };

    // Verilen alan adını denetler.
    public static async Task<List<Bulgu>> DenetleAsync(string alanAdi, CancellationToken ct)
    {
        // Bulguları toplayacağımız boş liste.
        var liste = new List<Bulgu>();
        // Ana ismin numarasını (adresini) rehberden öğren.
        var ana = await Dns.GetHostAddressesAsync(alanAdi, ct);
        // Numara laboratuvar dışındaysa güvenlik için dur.
        if (!ana.All(LabKapsam.IpLabdaMi))
            // Hata vererek işlemi kes.
            throw new InvalidOperationException("Alan adı yetkili ağ dışı bir adrese çözülüyor; güvenlik için işlem durduruldu.");
        // Ana kaydı bilgi olarak yaz.
        liste.Add(new Bulgu("DNS", "Ana isim kaydı", Onem.Bilgi, string.Join(", ", ana.Select(a => a.ToString())), "Yapılacak bir şey yok."));

        // Yaygın alt isimleri tek tek dene.
        foreach (var a in Alt)
        {
            // Kullanıcı "Durdur"a bastıysa dur.
            ct.ThrowIfCancellationRequested();
            // Tam ismi oluştur (örn. admin + . + lab.local).
            var fqdn = $"{a}.{alanAdi}";
            // Kayıt yoksa hata verir; bu normaldir.
            try
            {
                // Bu alt ismin numarasını rehberden sor.
                var ipler = await Dns.GetHostAddressesAsync(fqdn, ct);
                // Laboratuvar dışına çıkıyorsa atla.
                if (!ipler.All(LabKapsam.IpLabdaMi)) continue;
                // Bu isim "hassas" mı (yönetim, test, yedek gibi dışarı açık olmaması gerekenler)?
                var hassas = a is "admin" or "dev" or "test" or "staging" or "backup" or "old" or "db" or "git";
                // Bulguyu listeye ekle; hassassa "düşük", değilse "bilgi" olarak işaretle.
                liste.Add(new Bulgu("DNS", $"Alt isim bulundu: {fqdn}",
                    hassas ? Onem.Dusuk : Onem.Bilgi, string.Join(", ", ipler.Select(i => i.ToString())),
                    hassas ? "Dışarıya açık olması gerekmiyorsa bu kaydı silin veya erişimi kısıtlayın." : "Yapılacak bir şey yok."));
            }
            // Durdurma isteğini yut(ma), yukarı ilet.
            catch (OperationCanceledException) { throw; }
            // Kayıt yok: normal, geç.
            catch (Exception hk1) { HataKayit.Sessiz(hk1, "DnsDenetimi.DenetleAsync"); }
        }
        // Listeyi geri ver.
        return liste;
    }

    // Windows'un hosts dosyasının yolu (isim -> adres "özel rehberi").
    public static string HostsYolu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    // Standart (zararsız) sayılan hosts satırlarındaki isimler.
    static readonly string[] StandartIsimler = { "localhost", "ip6-localhost", "ip6-loopback", "broadcasthost", "kubernetes.docker.internal", "host.docker.internal", "gateway.docker.internal" };

    // Yönlendirilirse tehlikeli olan, güvenlik/güncelleme/banka benzeri isimlerin parçaları.
    static readonly string[] HassasIsimler = { "microsoft", "windows", "windowsupdate", "defender", "google", "apple", "icloud", "paypal", "bank", "akbank", "garanti", "isbank", "ziraat", "yapikredi", "kaspersky", "avast", "avg", "eset", "norton", "mcafee", "bitdefender", "malwarebytes", "virustotal", "github", "gmail", "outlook", "office", "live.com" };

    // Bilinen güvenilir genel DNS sunucuları (Google, Cloudflare, Quad9, OpenDNS).
    static readonly string[] BilinenDns = { "8.8.8.8", "8.8.4.4", "1.1.1.1", "1.0.0.1", "9.9.9.9", "149.112.112.112", "208.67.222.222", "208.67.220.220", "2001:4860:4860::8888", "2001:4860:4860::8844", "2606:4700:4700::1111", "2606:4700:4700::1001", "2620:fe::fe", "2620:fe::9" };

    // Hosts dosyasını ve ağ kartlarının DNS sunucularını okuyup bütünlük (zehirlenme) kontrolü yapar.
    // Yalnızca BU bilgisayarı ilgilendirir; ağa istek göndermez, hiçbir şeyi değiştirmez.
    public static List<Bulgu> HostsVeDnsDenetle(string? hostsYolu = null)
    {
        // Bulguları toplayacağımız boş liste.
        var liste = new List<Bulgu>();
        // Okunacak hosts dosyası (testte başka yol verilebilir).
        string yol = hostsYolu ?? HostsYolu;

        // ---- 1) hosts dosyası ----
        try
        {
            // Dosya yoksa bile bu normal bir durumdur.
            if (!File.Exists(yol)) liste.Add(new Bulgu("Hosts", "hosts dosyası bulunamadı", Onem.Bilgi, yol, "Yapılacak bir şey yok."));
            else
            {
                // Dosyadaki satırları oku.
                var satirlar = File.ReadAllLines(yol);
                // Standart dışı satırlar: (adres, isim, orijinal satır).
                var hassas = new List<string>(); var dis = new List<string>(); var ozel = new List<string>(); var engel = new List<string>();
                // Her satırı gez.
                foreach (var ham in satirlar)
                {
                    // Yorum (#) kısmını at ve boşlukları temizle.
                    var s = ham.Split('#')[0].Trim();
                    // Boş satırı geç.
                    if (s.Length == 0) continue;
                    // Satırı boşluk/Tab'e göre parçala: ilk parça adres, kalanlar isimler.
                    var p = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    // Geçersiz (tek parçalı) satırı geç.
                    if (p.Length < 2 || !IPAddress.TryParse(p[0], out var adres)) continue;
                    // Bu satırdaki isimlerin hepsi standart mı?
                    if (p.Skip(1).All(n => StandartIsimler.Contains(n, StringComparer.OrdinalIgnoreCase))) continue;
                    // Satırın kısa gösterimi.
                    string gosterim = $"{p[0]} → {string.Join(" ", p.Skip(1))}";
                    // İsimlerden biri hassas (güvenlik, güncelleme, banka...) bir isim mi?
                    bool hassasMi = p.Skip(1).Any(n => HassasIsimler.Any(h => n.Contains(h, StringComparison.OrdinalIgnoreCase)));
                    // Adres 0.0.0.0 veya 127.x ise bu bir "engelleme" satırıdır.
                    bool engelMi = IPAddress.IsLoopback(adres) || adres.Equals(IPAddress.Any) || adres.Equals(IPAddress.IPv6Any);
                    // Sınıflandır: hassas isim başka yere yönlendirildiyse en tehlikelisi.
                    if (hassasMi && !engelMi) hassas.Add(gosterim);
                    // Engelleme satırı (reklam engelleyici listeleri gibi).
                    else if (engelMi) engel.Add(gosterim);
                    // Genel (internet) adrese yönlendirme.
                    else if (!LabKapsam.IpLabdaMi(adres)) dis.Add(gosterim);
                    // Yerel ağ adresine yönlendirme.
                    else ozel.Add(gosterim);
                }
                // En fazla 8 satırı gösterip gerisini sayıyla özetleyen yardımcı.
                static string Ozet(List<string> l) => string.Join("\n", l.Take(8).Select(x => "  • " + x)) + (l.Count > 8 ? $"\n  … ve {l.Count - 8} satır daha" : "");
                // Güvenlik/banka/güncelleme isimleri başka adrese yönlendirilmiş: çok tehlikeli.
                if (hassas.Count > 0) liste.Add(new Bulgu("Hosts", "hosts dosyasında güvenlik/hesap sitesi yönlendirilmiş", Onem.Yuksek, $"hosts: {yol}\nBilinen önemli sitelerin adresi başka bir yere çevrilmiş:\n{Ozet(hassas)}", "Bu satırları siz eklemediyseniz hemen silin ve bilgisayarı virüs taramasından geçirin; parolalarınızı değiştirmeyi düşünün."));
                // İnternetteki bir adrese yönlendirme.
                if (dis.Count > 0) liste.Add(new Bulgu("Hosts", "hosts dosyasında standart dışı yönlendirme (internet adresi)", Onem.Orta, $"hosts: {yol}\nŞu isimler dışarıdaki bir adrese yönlendirilmiş:\n{Ozet(dis)}", "Satırları siz eklemediyseniz silin. Bilinçli eklediyseniz (test sunucusu vb.) not alın."));
                // Yerel ağdaki bir adrese yönlendirme.
                if (ozel.Count > 0) liste.Add(new Bulgu("Hosts", "hosts dosyasında yerel ağ yönlendirmesi", Onem.Dusuk, $"hosts: {yol}\nŞu isimler yerel ağdaki bir adrese bağlanmış:\n{Ozet(ozel)}", "Test ortamı için bilinçli eklediyseniz sorun yok; hatırlamıyorsanız inceleyin."));
                // Engelleme satırları.
                if (engel.Count > 0) liste.Add(new Bulgu("Hosts", engel.Count > 20 ? $"hosts dosyasında {engel.Count} engelleme satırı (reklam/izleyici listesi gibi)" : "hosts dosyasında engelleme satırı", Onem.Dusuk, $"hosts: {yol}\nŞu isimler 0.0.0.0 / 127.0.0.1'e çevrilip erişime kapatılmış:\n{Ozet(engel)}", "Reklam engelleyici kullanıyorsanız normaldir. Siz eklemediyseniz bazı sitelerin (ör. güvenlik güncellemesi) engellenmediğinden emin olun."));
                // Hiç standart dışı satır yoksa temiz.
                if (hassas.Count + dis.Count + ozel.Count + engel.Count == 0) liste.Add(new Bulgu("Hosts", "hosts dosyası temiz", Onem.Bilgi, $"hosts: {yol}\nStandart dışı yönlendirme yok.", "Yapılacak bir şey yok."));
            }
        }
        // Dosya okunamazsa (izin vb.) kontrol yapılamadı notu düş.
        catch (Exception ex) { liste.Add(new Bulgu("Hosts", "Bu kontrol çalıştırılamadı", Onem.Bilgi, "hosts dosyası okunamadı: " + ex.Message, "Dosya izinlerini kontrol edip tekrar deneyin.")); }

        // ---- 2) Ağ kartlarının DNS sunucuları ----
        try
        {
            // Açık (çalışan) ve gerçek ağ kartları.
            foreach (var kart in NetworkInterface.GetAllNetworkInterfaces().Where(k => k.OperationalStatus == OperationalStatus.Up && k.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)))
            {
                // Kartın DNS sunucu adresleri (IPv6 link-local fe80::/10 otomatik gelenleri hariç).
                var dnsler = kart.GetIPProperties().DnsAddresses.Where(a => !(a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && a.IsIPv6SiteLocal)).ToList();
                // Hiç DNS yoksa geç.
                if (dnsler.Count == 0) continue;
                // Bilinmeyen genel (internet) DNS sunucuları: ne bilinen listede ne de yerel/özel ağda.
                var supheli = dnsler.Where(a => !LabKapsam.IpLabdaMi(a) && !BilinenDns.Contains(a.ToString())).ToList();
                // Şüpheli varsa DİKKAT.
                if (supheli.Count > 0) liste.Add(new Bulgu("Hosts", $"Bilinmeyen DNS sunucusu ({kart.Name})", Onem.Dusuk, $"Ağ kartı: {kart.Name}\nDNS sunucuları: {string.Join(", ", dnsler)}\nBilinmeyen adres(ler): {string.Join(", ", supheli)}", "İnternet sağlayıcınızın DNS'i olabilir. Siz veya sağlayıcınız ayarlamadıysa (ya da modem ayarı kurcalandıysa) DNS'i otomatik/1.1.1.1/8.8.8.8 yapın."));
                // Hepsi güvenilir/yerel ise bilgi notu.
                else liste.Add(new Bulgu("Hosts", $"DNS sunucuları normal ({kart.Name})", Onem.Bilgi, $"Ağ kartı: {kart.Name}\nDNS sunucuları: {string.Join(", ", dnsler)}", "Yapılacak bir şey yok."));
            }
        }
        // Ağ kartları okunamazsa kontrol yapılamadı notu düş.
        catch (Exception ex) { liste.Add(new Bulgu("Hosts", "Bu kontrol çalıştırılamadı", Onem.Bilgi, "DNS sunucuları okunamadı: " + ex.Message, "Tekrar deneyin.")); }
        // Listeyi geri ver.
        return liste;
    }
}
