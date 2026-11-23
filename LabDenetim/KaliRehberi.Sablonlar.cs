// Bu dosya Kali Araç Rehberi'ni komut şablonlarına (kali_araclari.json) bağlar: hangi rehber aracı hangi şablon(lar)la uygulamadan çalıştırılır.
// Şablonu olan araçlar "Calistirilir" sınıfına geçer; KAPALI (saldırı odaklı) araçlar bu haritayla ASLA açılamaz (aşağıda ve testte denetlenir).
// Haritada olmayan araçlar sınıflarını korur (henüz bağlanmamış olanlar "Egitim": Kali'de elle kullanılır).
namespace LabDenetim;

// Rehber ↔ şablon bağlantısı.
internal static partial class KaliRehberi
{
    // Rehber aracının adı → şablon adları. Her şablon tam bir araca bağlıdır (testle denetlenir).
    private static readonly Dictionary<string, string[]> SablonHaritasi = new(StringComparer.Ordinal)
    {
        ["Dnsenum"] = new[] { "dnsenum-alan" }, ["TheHarvester"] = new[] { "theharvester-alan" }, ["Recon-ng"] = new[] { "reconng-modul" }, ["SpiderFoot"] = new[] { "spiderfoot-tarama" }, ["Netdiscover"] = new[] { "netdiscover-ag" },
        ["Wireshark"] = new[] { "tshark-pcap-ozet" }, ["Tcpdump"] = new[] { "tcpdump-yakala" }, ["Macchanger"] = new[] { "macchanger-goster" }, ["Netcat"] = new[] { "netcat-baglanti" }, ["Proxychains4"] = new[] { "proxychains-baglanti" },
        ["Tor"] = new[] { "tor-durum", "tor-baslat", "tor-durdur" }, ["WPScan"] = new[] { "wpscan-url" }, ["Sleuth Kit"] = new[] { "mmls-bolum", "fls-liste" }, ["Foremost"] = new[] { "foremost-kurtar" }, ["Binwalk"] = new[] { "binwalk-tara" },
        ["Volatility3"] = new[] { "volatility3-eklenti" }, ["Radare2"] = new[] { "radare2-ozet" }, ["GDB"] = new[] { "gdb-ozet" }, ["Maltego"] = new[] { "maltego-ac" }, ["Burp Suite"] = new[] { "burpsuite-ac" }
    };

    // Bağlantı haritası (testler ve arayüz için salt okunur görünüm).
    public static IReadOnlyDictionary<string, string[]> Harita => SablonHaritasi;

    // Dışarıya verilen liste (ilk kullanımda üretilir; statik başlatma sırasına bağımlı değildir).
    private static IReadOnlyList<KaliAraci>? _tum;
    public static IReadOnlyList<KaliAraci> Tum => _tum ??= Uret();

    // Ham kayıtlardan sonuç listesini üretir: şablonu olan (ve KAPALI olmayan) araçlar "Calistirilir" olur.
    private static IReadOnlyList<KaliAraci> Uret() => Ham.Select(a => a.Sinif != AracSinifi.Kapali && SablonHaritasi.TryGetValue(a.Ad, out var s) ? a with { Sinif = AracSinifi.Calistirilir, Sablonlar = s } : a).ToList();

    // Bir rehber aracının şablon tanımları (gömülü tablodan).
    public static IReadOnlyList<KomutTanimi> SablonTanimlari(KaliAraci a) => (a.Sablonlar ?? Array.Empty<string>()).Select(KomutKaydi.Tanim).Where(t => t != null).Select(t => t!).ToList();
}
