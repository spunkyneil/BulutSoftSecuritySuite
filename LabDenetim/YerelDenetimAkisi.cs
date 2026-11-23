// Bu dosya yerel denetimin akışını yönetir: hizmet eşleme + yama denetimi + (ağ haritasında bu bilgisayarın varlığını güncelleme) + bulguların şemaya göre kaydı.
// Her şey SALT-OKUNURDUR: sistemde hiçbir ayar değiştirilmez, hiçbir dosya silinmez.
using System.Net;
using System.Text.Json;

// V2 ad alanı.
namespace LabDenetim.V2;

// Yerel denetimin sonucu.
internal sealed record YerelDenetimSonucu(NetworkMap Harita, List<Finding> Bulgular, List<Finding> Reddedilen, PatchState Yama, int DinleyenSayisi, int HizmetSayisi, int YazilimSayisi = 0, List<string>? YazilimNotlari = null);

// Akış.
internal static class YerelDenetimAkisi
{
    // Bu bilgisayarın harita kimliği.
    public static string VarlikKimligi(IYerelKaynak k) => "host:" + k.MakineAdi().ToUpperInvariant();

    // Denetimi çalıştırır. Şemaya uymayan bulgu KABUL EDİLMEZ (reddedilenlere yazılır).
    public static YerelDenetimSonucu Calistir(IYerelKaynak k, KbCatalog? katalog, NetworkMap? onceki, DateTime simdi, FpDeposu? fp = null, NvdIndeks? nvd = null, VulnFeedStore? kev = null)
    {
        string id = VarlikKimligi(k);
        // Mevcut ağırlık varsa korunur (kullanıcı değiştirmiş olabilir), yoksa iş istasyonu varsayılanı.
        double agirlik = onceki?.Assets.FirstOrDefault(a => a.AssetId == id)?.AssetWeight ?? NetworkMapBuilder.VarsayilanAgirlik("workstation");
        // Bir kez toplanan ham veri iki aşamada da kullanılır.
        var hizmetler = k.Hizmetler(); var dinleyenler = k.Dinleyenler();
        var baglar = ServiceMapper.Esle(k, hizmetler, dinleyenler);
        var yama = PatchAuditor.Topla(k, katalog, simdi);
        // Yazılım envanteri → CPE → NVD/KEV eşleştirmesi (yalnızca yerel veriyle; envanter dışarı GÖNDERİLMEZ).
        var yazilimlar = k.Yazilimlar(); var vuln = VulnMatcher.Esle(yazilimlar, nvd, kev, id, agirlik, simdi);
        // PE analizi (derleme korumaları + DLL arama riski): hizmet ve dinleyen süreç dosyaları. Hizmet klasör yazılabilirliği SVC-WRITABLE-001 ile zaten raporlandığı için çift bulgu üretilmez.
        var adaylar = PeBulgulari.Adaylar(hizmetler, baglar); var pe = PeBulgulari.Bulgular(k, adaylar, id, agirlik, simdi, adaylar.Where(a => a.Servis).Select(a => a.Yol).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var tum = ServiceMapper.Bulgular(k, id, agirlik, simdi, hizmetler).Concat(PatchAuditor.Bulgular(k, katalog, id, agirlik, simdi, yama)).Concat(vuln.Bulgular).Concat(pe).ToList();
        // Şema doğrulaması: geçmeyenler listeye girmez.
        var gecerli = new List<Finding>(); var red = new List<Finding>();
        foreach (var f in tum) (FindingSchema.Validate(f).Count == 0 ? gecerli : red).Add(f);
        // Yanlış alarm bilgisi (kalıcı depo varsa): yeni bulgular sayılır, işaretliler "FalsePositive" olur, oranı yüksek kurallar "gözden geçir" alır.
        if (fp != null) { fp.Gor(gecerli); gecerli = fp.Uygula(gecerli); }
        // Bu bilgisayarın varlığını haritaya yaz.
        var harita = VarlikGuncelle(onceki, k, id, agirlik, baglar, yama, simdi, yazilimlar);
        return new YerelDenetimSonucu(harita, gecerli.OrderByDescending(f => f.Score).ToList(), red, yama, dinleyenler.Count, hizmetler.Count, yazilimlar.Count, vuln.Notlar);
    }

    // Haritada bu bilgisayarın kaydını oluşturur/günceller (ağırlık ve etiketler korunur).
    internal static NetworkMap VarlikGuncelle(NetworkMap? onceki, IYerelKaynak k, string id, double agirlik, List<ServiceBinding> baglar, PatchState yama, DateTime simdi, List<InstalledSoftware>? yazilimlar = null)
    {
        var varliklar = (onceki?.Assets ?? new()).ToList();
        // Yerel IPv4 adresleri (okuma hatası olursa boş).
        string[] adresler; try { adresler = Dns.GetHostAddresses(k.MakineAdi()).Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.ToString()).Distinct().Take(8).ToArray(); } catch { adresler = Array.Empty<string>(); }
        var hizmetler = baglar.GroupBy(b => (b.Port, b.Proto)).Select(g => g.First()).Select(b => new ServiceEndpoint(b.Port, b.Proto, "open", b.WindowsServiceName ?? b.ProcessName, null, null, null, Array.Empty<string>())).ToList();
        int i = varliklar.FindIndex(a => a.AssetId == id);
        var yeni = new Asset(id, adresler, k.MakineAdi(), "Windows " + yama.OsBuild, "workstation", agirlik, hizmetler, baglar, yazilimlar ?? (i >= 0 ? varliklar[i].Software : new()), yama, i >= 0 ? varliklar[i].Tags : Array.Empty<string>(), "wmi@local");
        if (i >= 0) varliklar[i] = yeni with { Role = varliklar[i].Role }; else varliklar.Add(yeni);
        return new NetworkMap(onceki?.MapId ?? Guid.NewGuid().ToString("N"), simdi, onceki?.Subnets ?? new(), varliklar, onceki?.Links ?? new());
    }
}

// Şemaya uyan bulguların dosya deposu (en son yerel denetim sonucu).
internal static class BulguDeposu
{
    public static string Yol => BulutKlasor.Dosya("bulgular_v2.json");
    private static readonly JsonSerializerOptions Sec = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) } };
    // Bir varlığa ait bulguları yenileriyle DEĞİŞTİRİR (diğer varlıkların bulguları, örn. web denetimi sonuçları, korunur).
    public static void Degistir(string assetId, IEnumerable<Finding> yeni) { var l = Oku(); l.RemoveAll(f => f.AssetId == assetId); l.AddRange(yeni); Kaydet(l); }
    // Yazar (önce geçici dosya, sonra yer değiştirme).
    public static void Kaydet(IEnumerable<Finding> l) { Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, JsonSerializer.Serialize(l.ToList(), Sec)); }
    // Okur (yoksa/bozuksa boş).
    public static List<Finding> Oku() { try { return File.Exists(Yol) ? JsonSerializer.Deserialize<List<Finding>>(File.ReadAllText(Yol), Sec) ?? new() : new(); } catch { return new(); } }
}
