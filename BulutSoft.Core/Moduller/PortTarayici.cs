// Ağ adresleri için gerekli araçları çağır.
using System.Net;
// Ağ bağlantısı (TCP) kurmak için gerekli araçları çağır.
using System.Net.Sockets;
// Yazı ile bayt dönüşümü için gerekli araçları çağır.
using System.Text;

// Bu dosyanın ait olduğu klasör adı.
namespace LabDenetim.Moduller;

// Açık bulunan bir kapının bilgi kartı: numara, hizmet adı, kapıdaki tabela (banner).
public record PortSonucu(int Port, string Servis, string Banner);

// KAPI YOKLAMA MODÜLÜ: bir bilgisayarın hangi "kapıları" (portları) açık, bakar ve kapıdaki tabelayı okur.
public static class PortTarayici
{
    // Bilinen kapı numaralarının ne işe yaradığını gösteren sözlük.
    static readonly Dictionary<int, string> Bilinen = new()
    {
        // 21 = dosya aktarımı, 22 = güvenli uzaktan bağlantı, 23 = eski uzaktan bağlantı, 25 = e-posta gönderme, 53 = isim rehberi, 80 = web sitesi.
        [21] = "FTP", [22] = "SSH", [23] = "Telnet", [25] = "SMTP", [53] = "DNS", [80] = "HTTP",
        // 110/143 = e-posta alma, 139/445 = dosya paylaşımı, 443 = şifreli web sitesi.
        [110] = "POP3", [135] = "RPC", [139] = "NetBIOS", [143] = "IMAP", [443] = "HTTPS", [445] = "SMB",
        // 3306/5432 = veritabanları, 3389/5900 = ekran paylaşımı.
        [3306] = "MySQL", [3389] = "RDP", [5432] = "PostgreSQL", [5900] = "VNC",
        // 8080/8443 = alternatif web kapıları.
        [8080] = "HTTP-Alt", [8443] = "HTTPS-Alt"
    };

    // Hiçbir şey yazılmazsa yoklanacak standart kapı listesi.
    public static int[] VarsayilanPortlar => Bilinen.Keys.OrderBy(x => x).ToArray();

    // Bir kapı numarasının bilinen servis adını verir (bilinmiyorsa "Bilinmiyor").
    public static string ServisAdi(int port) => Bilinen.GetValueOrDefault(port, "Bilinmiyor");

    // "Hassas" kapılar: açık olması tek başına dikkat gerektiren hizmetler (FTP, Telnet, RPC, NetBIOS, SMB, MySQL, RDP, VNC).
    private static readonly HashSet<int> Hassas = new() { 21, 23, 135, 139, 445, 3306, 3389, 5900 };

    // Bu kapı hassas bir hizmete mi ait?
    // Önce bilgi bankası dosyasına (tehdit_veritabani.json) bakılır; kapı orada yoksa yerleşik liste kullanılır.
    public static bool HassasMi(int port) => TehditVeritabani.Hizmet(port) is { } h ? h.Hassas : Hassas.Contains(port);

    // AKILLI KORELASYON: hassas bir kapı açıksa satır asla GÜVENLİ (yeşil) olmaz; alt satırla (Zayıf ayarlar) çelişmez.
    // Telnet (23), SMB (445) ve RDP (3389) → UYARI (turuncu); diğer hassas kapılar → DİKKAT (sarı); geri kalanlar → bilgi (yeşil).
    public static Onem PortOnemi(int port)
    {
        // Bilgi bankası dosyasında bu kapı tanımlıysa onun önem derecesini kullan.
        if (TehditVeritabani.Hizmet(port) is { } h)
        {
            // Dosyadaki önem yazısını okunur hale getir; anlaşılmazsa "Bilgi".
            var onem = Enum.TryParse<Onem>(h.Onem, true, out var o) ? o : Onem.Bilgi;
            // Hassas işaretli bir kapı asla "Bilgi" (yeşil) olamaz.
            return h.Hassas && onem == Onem.Bilgi ? Onem.Dusuk : onem;
        }
        // Dosyada yoksa yerleşik kurallar: 23, 445, 3389 → UYARI; diğer hassas kapılar → DİKKAT; geri kalanlar → bilgi.
        return port is 23 or 445 or 3389 ? Onem.Orta : Hassas.Contains(port) ? Onem.Dusuk : Onem.Bilgi;
    }

    // Açık bir kapıdan tablo satırı (Bulgu) üretir; önem derecesini kapının hassasiyetine göre seçer.
    public static Bulgu BulguOlustur(PortSonucu p) => new(
        // Hangi kontrol buldu?
        "Port",
        // Başlık.
        $"Açık port {p.Port} ({p.Servis})",
        // Önem: hassas kapılar yeşil olamaz.
        PortOnemi(p.Port),
        // Ayrıntı: kapı ve servis kimliği.
        $"Port {p.Port} (Kapı)\n" + (string.IsNullOrEmpty(p.Banner) ? "Servis kimliği (yanıt) okunamadı" : "Servis kimliği (yanıt): " + p.Banner),
        // Öneri.
        HassasMi(p.Port) ? "Bu kapı hassas bir hizmete ait; kullanmıyorsanız kapatın. Sağdaki asistan hazır komutu verir." : "Bu hizmete ihtiyacınız yoksa kapatın.");

    // "22,80,8000-8100" gibi yazıyı sayı listesine çevirir.
    public static int[] PortListesiOku(string metin)
    {
        // Sıralı ve tekrarsız bir sayı kümesi hazırla.
        var sonuc = new SortedSet<int>();
        // Yazıyı virgüllerden parçala.
        foreach (var parca in metin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Parçayı tire işaretinden ikiye böl (aralık mı diye bak).
            var ar = parca.Split('-');
            // İki sayı varsa (8000-8100) aradaki tüm sayıları ekle.
            if (ar.Length == 2 && int.TryParse(ar[0], out var a) && int.TryParse(ar[1], out var b))
                // 1 ile 65535 arasında kalmasını sağla ve ekle.
                for (int p = Math.Max(1, a); p <= Math.Min(65535, b); p++) sonuc.Add(p);
            // Tek sayı ise ve geçerliyse onu ekle.
            else if (int.TryParse(parca, out var tek) && tek is >= 1 and <= 65535) sonuc.Add(tek);
        }
        // Kümeyi liste olarak geri ver.
        return sonuc.ToArray();
    }

    // Verilen kapıları yoklar; açık olanları döndürür. async: program donmadan çalışır.
    // bulununca: açık bir kapı bulunduğu AN çağrılan isteğe bağlı haber verici (canlı günlük için).
    public static async Task<List<PortSonucu>> TaraAsync(IPAddress ip, int[] portlar, IProgress<int> ilerleme, CancellationToken ct, Action<PortSonucu>? bulununca = null)
    {
        // Açık bulunan kapıların listesi.
        var acik = new List<PortSonucu>();
        // Birden çok işçi aynı listeye yazacağı için kilit nesnesi (sıra bekleme tabelası gibi).
        var kilit = new object();
        // Şimdiye kadar yoklanan kapı sayısı.
        int tamam = 0;
        // Aynı anda en fazla 100 kapı yokla ki ağ yorulmasın.
        await Parallel.ForEachAsync(portlar, new ParallelOptions { MaxDegreeOfParallelism = 100, CancellationToken = ct },
            async (port, t) =>
            {
                // Hata olursa (kapı kapalıysa) sessizce geç.
                try
                {
                    // Bu kapı için yeni bir bağlantı hazırla.
                    using var istemci = new TcpClient();
                    // 1,5 saniye içinde cevap gelmezse vazgeçmek için zamanlayıcı kur.
                    using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(t);
                    // Süreyi 1500 milisaniye yap.
                    zamanAsimi.CancelAfter(1500);
                    // Kapıyı çalmayı dene.
                    await istemci.ConnectAsync(ip, port, zamanAsimi.Token);
                    // Kapı açıldıysa üstündeki tabelayı oku.
                    string banner = await BannerAlAsync(istemci, port, t);
                    // Kilit altında sonucu listeye ekle (karışıklık olmasın).
                    var sonuc = new PortSonucu(port, Bilinen.GetValueOrDefault(port, "Bilinmiyor"), banner);
                    // Listeye ekle (kilit altında).
                    lock (kilit) acik.Add(sonuc);
                    // Açık kapının bulunduğunu hemen haber ver (varsa).
                    bulununca?.Invoke(sonuc);
                }
                // Kapı kapalı veya cevap yok: normal durum, sorun değil.
                catch (Exception hk1) { HataKayit.Sessiz(hk1, "PortTarayici.TaraAsync"); }
                // İşlem bitince yoklanan sayısını artır ve ilerleme çubuğuna haber ver.
                finally { ilerleme.Report(Interlocked.Increment(ref tamam)); }
            });
        // Sonuçları kapı numarasına göre sıralı ver.
        return acik.OrderBy(x => x.Port).ToList();
    }

    // Açık kapıdaki "tabelayı" (banner: hizmetin kendini tanıttığı ilk cümle) okur.
    static async Task<string> BannerAlAsync(TcpClient c, int port, CancellationToken ct)
    {
        // Okunamazsa boş döneceğiz.
        try
        {
            // En fazla 2 saniye bekleyecek bir zamanlayıcı kur.
            using var zs = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // Süreyi 2000 milisaniye yap.
            zs.CancelAfter(2000);
            // Bağlantının veri hattını al.
            var ns = c.GetStream();
            // Web kapılarına önce "merhaba" (HEAD isteği) göndermek gerekir.
            if (port is 80 or 8080)
                // Basit bir web selamı gönder.
                await ns.WriteAsync(Encoding.ASCII.GetBytes("HEAD / HTTP/1.0\r\n\r\n"), zs.Token);
            // Gelen cevabı koyacağımız 512 baytlık boş alan.
            var tampon = new byte[512];
            // Gelen cevabı oku ve kaç bayt geldiğini öğren.
            int n = await ns.ReadAsync(tampon, zs.Token);
            // Baytları yazıya çevir.
            var s = Encoding.ASCII.GetString(tampon, 0, n);
            // Yazdırılamayan garip karakterleri at, sadece ilk satırı al.
            return new string(s.Where(ch => (ch >= ' ' && ch < (char)127) || ch == '\n').ToArray())
                .Split('\n')[0].Trim();
        }
        // Okunamadıysa boş yazı ver.
        catch { return ""; }
    }
}
