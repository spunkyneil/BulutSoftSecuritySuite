// Bu dosya Hafta 3'ün veri tarafıdır: ağ haritası modeli, nmap XML çözücüsü (NmapXml), haritayı birleştiren NetworkMapBuilder ve dosya deposu.
// Sensörden gelen nmap çıktısı GÜVENİLMEZ veri kabul edilir: DTD/dış varlık kapalı, boyut ve adet sınırlı, metinler temizlenir.
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

// V2 ad alanı.
namespace LabDenetim.V2;

// ---------- Ağ haritası modeli ----------
// Açık/bilinen bir hizmet kapısı.
public sealed record ServiceEndpoint(int Port, string Proto, string State, string Service, string? Product, string? Version, string? ExtraInfo, string[] Cpe);
// Port → süreç → Windows hizmeti zinciri (sonraki haftalarda doldurulur).
public sealed record ServiceBinding(int Port, string Proto, int Pid, string ProcessName, string? ImagePath, string? WindowsServiceName, string? ServiceAccount, string? StartType, string SignatureState, bool PathQuoted);
// Yama durumu (WMI, sonraki haftalar).
public sealed record PatchState(string Source, DateTime CollectedAt, List<string> InstalledKbs, DateTime? LastInstallDate, string OsBuild, List<string> MissingKbs, string CatalogVersion);
// Yüklü yazılım (sonraki haftalar).
public sealed record InstalledSoftware(string Name, string? Version, string? Publisher, string? InstallLocation, string UninstallKey, string? CandidateCpe, double CpeConfidence);
// Alt ağ.
public sealed record Subnet(string Cidr, string Name, string SensorId);
// Varlık (cihaz).
public sealed record Asset(string AssetId, string[] Addresses, string? Hostname, string? OsGuess, string Role, double AssetWeight, List<ServiceEndpoint> Services, List<ServiceBinding> Bindings, List<InstalledSoftware> Software, PatchState? Patch, string[] Tags, string Provenance);
// Varlıklar arası bağlantı.
public sealed record Link(string FromAssetId, string ToAssetId, string Kind, string? Port);
// Ağ haritası.
public sealed record NetworkMap(string MapId, DateTime BuiltAt, List<Subnet> Subnets, List<Asset> Assets, List<Link> Links);

// ---------- Nmap çıktısı (normalleştirilmiş) ----------
// Nmap'in bulduğu tek cihaz.
public sealed record NmapHost(string Address, string? Mac, string? Vendor, string? Hostname, string? OsGuess, List<ServiceEndpoint> Services);
// Bir tarama sonucu.
public sealed record NormalizedScan(string Target, DateTime ScannedAtUtc, List<NmapHost> Hosts);

// ---------- Nmap XML çözücüsü ----------
public static class NmapXml
{
    // Güvenlik sınırları.
    public const int EnCokCihaz = 1024, EnCokPort = 2000, EnCokXmlKarakter = 8_000_000;

    // Metni temizler (denetim karakterleri atılır, uzunluk sınırlanır).
    private static string T(string? s, int en = 200) { s = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim(); return s.Length > en ? s[..en] : s; }

    // XML'i çözer. Bozuk XML'de XmlException fırlatır (çağıran yakalar).
    public static NormalizedScan Parse(string xml, DateTime? simdiUtc = null)
    {
        // Güvenli okuyucu: gerçek nmap çıktısı "<!DOCTYPE nmaprun>" içerir, bu yüzden DTD YOK SAYILIR (işlenmez, varlık tanımı yüklenmez); dış kaynak yok, boyut sınırlı.
        var ayar = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = EnCokXmlKarakter, MaxCharactersFromEntities = 0 };
        XDocument d; using (var sr = new StringReader(xml)) using (var r = XmlReader.Create(sr, ayar)) d = XDocument.Load(r);
        var kok = d.Root; if (kok == null || kok.Name.LocalName != "nmaprun") throw new XmlException("nmaprun bulunamadı.");
        // Tarama zamanı (Unix saniyesi) ve hedef.
        DateTime zaman = simdiUtc ?? DateTime.UtcNow;
        if (long.TryParse((string?)kok.Attribute("start"), out long unix) && unix > 0 && unix < 4_102_444_800) zaman = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        string hedef = T(((string?)kok.Attribute("args") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), 64);
        // Cihazlar.
        var hosts = new List<NmapHost>();
        foreach (var h in kok.Elements("host").Take(EnCokCihaz))
        {
            // Yalnızca "up" olanlar.
            if ((string?)h.Element("status")?.Attribute("state") != "up") continue;
            // Adresler.
            string? ip = null, mac = null, vendor = null;
            foreach (var a in h.Elements("address"))
            {
                string tur = (string?)a.Attribute("addrtype") ?? "", ad = (string?)a.Attribute("addr") ?? "";
                if (tur == "ipv4" && IPAddress.TryParse(ad, out var ia) && ia.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ip = ia.ToString();
                else if (tur == "mac" && System.Text.RegularExpressions.Regex.IsMatch(ad, "^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")) { mac = ad.ToUpperInvariant(); vendor = T((string?)a.Attribute("vendor"), 80); }
            }
            // IPv4 yoksa cihaz atlanır.
            if (ip == null) continue;
            // Ad ve işletim sistemi tahmini.
            string? hostname = T((string?)h.Element("hostnames")?.Elements("hostname").FirstOrDefault()?.Attribute("name"), 253); if (hostname.Length == 0) hostname = null;
            string? os = T((string?)h.Element("os")?.Elements("osmatch").FirstOrDefault()?.Attribute("name"), 100); if (os.Length == 0) os = null;
            // Portlar.
            var portlar = new List<ServiceEndpoint>();
            foreach (var p in (h.Element("ports")?.Elements("port") ?? Enumerable.Empty<XElement>()).Take(EnCokPort))
            {
                if (!int.TryParse((string?)p.Attribute("portid"), out int no) || no is < 1 or > 65535) continue;
                string proto = (string?)p.Attribute("protocol") is "tcp" or "udp" or "sctp" ? (string)p.Attribute("protocol")! : "tcp";
                var s = p.Element("service");
                portlar.Add(new ServiceEndpoint(no, proto, T((string?)p.Element("state")?.Attribute("state"), 20), T((string?)s?.Attribute("name"), 40),
                    NullIfEmpty(T((string?)s?.Attribute("product"))), NullIfEmpty(T((string?)s?.Attribute("version"), 80)), NullIfEmpty(T((string?)s?.Attribute("extrainfo"))),
                    (s?.Elements("cpe") ?? Enumerable.Empty<XElement>()).Select(c => T(c.Value, 150)).Where(c => c.StartsWith("cpe:", StringComparison.Ordinal)).Take(5).ToArray()));
            }
            hosts.Add(new NmapHost(ip, mac, NullIfEmpty(vendor), hostname, os, portlar));
        }
        return new NormalizedScan(hedef, zaman, hosts);
    }
    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    // Ajanın sıkıştırılmış (zlib + Base64) çıktısını açar; açılmış boyut sınırlıdır (sıkıştırma bombası savunması).
    public static string Ac(string xmlz)
    {
        // Çok uzun Base64 reddedilir.
        if (string.IsNullOrEmpty(xmlz) || xmlz.Length > 700_000) throw new InvalidDataException("Nmap çıktısı geçersiz veya çok büyük.");
        byte[] z = Convert.FromBase64String(xmlz);
        using var ms = new MemoryStream(z); using var zs = new ZLibStream(ms, CompressionMode.Decompress); using var cikis = new MemoryStream();
        var tampon = new byte[16384]; int n;
        while ((n = zs.Read(tampon, 0, tampon.Length)) > 0) { cikis.Write(tampon, 0, n); if (cikis.Length > EnCokXmlKarakter) throw new InvalidDataException("Nmap çıktısı açılınca çok büyük."); }
        return System.Text.Encoding.UTF8.GetString(cikis.ToArray());
    }
}

// ---------- Harita birleştirici ----------
public static class NetworkMapBuilder
{
    // Hizmetlerden rol tahmini.
    public static string RolTahmin(IEnumerable<ServiceEndpoint> s, string? os)
    {
        var acik = s.Where(x => x.State == "open").Select(x => x.Port).ToHashSet();
        if (os != null && os.Contains("windows server", StringComparison.OrdinalIgnoreCase)) return "server";
        if (acik.Overlaps(new[] { 80, 443, 8080, 8443, 3306, 5432, 1433, 1521, 25, 53, 389, 21 })) return "server";
        if (acik.Overlaps(new[] { 135, 139, 445, 3389 })) return "workstation";
        if (os != null && (os.Contains("router", StringComparison.OrdinalIgnoreCase) || os.Contains("switch", StringComparison.OrdinalIgnoreCase) || os.Contains("routeros", StringComparison.OrdinalIgnoreCase))) return "network";
        return "unknown";
    }
    // Rol için varsayılan varlık ağırlığı (kullanıcı sonra değiştirebilir).
    public static double VarsayilanAgirlik(string rol) => rol switch { "server" => 0.8, "network" => 0.7, "workstation" => 0.5, "guest" => 0.3, _ => 0.5 };

    // Cihaz kimliği: MAC varsa MAC, yoksa IP.
    public static string KimlikUret(string ip, string? mac) => mac != null ? "mac:" + mac : "ip:" + ip;

    // Taramayı mevcut haritayla birleştirir; yeni harita döndürür (öncekini değiştirmez). Taramada görünmeyen cihazlar silinmez.
    public static NetworkMap Merge(NetworkMap? onceki, NormalizedScan tarama, string sensorId, DateTime simdiUtc)
    {
        // Çalışma kopyası.
        var varliklar = (onceki?.Assets ?? new()).ToList(); var altAglar = (onceki?.Subnets ?? new()).ToList();
        string kaynak = "nmap@" + sensorId;
        foreach (var h in tarama.Hosts)
        {
            // Eşleşen varlık: kimlik, ya da bu IP'yi taşıyan eski varlık (IP-only kayıt MAC'e yükselebilir).
            string yeniId = KimlikUret(h.Address, h.Mac);
            int i = varliklar.FindIndex(a => a.AssetId == yeniId); if (i < 0) i = varliklar.FindIndex(a => a.Addresses.Contains(h.Address) && (a.AssetId == "ip:" + h.Address || h.Mac == null));
            string rol = RolTahmin(h.Services, h.OsGuess);
            if (i < 0)
            {
                varliklar.Add(new Asset(yeniId, new[] { h.Address }, h.Hostname, h.OsGuess, rol, VarsayilanAgirlik(rol), h.Services, new(), new(), null, h.Vendor != null ? new[] { "vendor:" + h.Vendor } : Array.Empty<string>(), kaynak));
            }
            else
            {
                var a = varliklar[i];
                // Kaynak listesine ekle (tekrarsız).
                var kaynaklar = a.Provenance.Split(',', StringSplitOptions.RemoveEmptyEntries).Append(kaynak).Distinct().ToArray();
                // Adresler: yeni IP başa, eskiler arkada tekrarsız.
                var adresler = new[] { h.Address }.Concat(a.Addresses).Distinct().Take(8).ToArray();
                // Rol: kullanıcı/önceki bilinen rol "unknown" değilse korunur; ağırlık HER ZAMAN korunur (kullanıcı değiştirmiş olabilir).
                string yenirol = a.Role != "unknown" ? a.Role : rol;
                varliklar[i] = a with { AssetId = h.Mac != null ? yeniId : a.AssetId, Addresses = adresler, Hostname = h.Hostname ?? a.Hostname, OsGuess = h.OsGuess ?? a.OsGuess, Role = yenirol, Services = h.Services, Provenance = string.Join(",", kaynaklar) };
            }
            // Alt ağ (/24).
            var o = IPAddress.Parse(h.Address).GetAddressBytes(); string cidr = $"{o[0]}.{o[1]}.{o[2]}.0/24";
            if (!altAglar.Any(s => s.Cidr == cidr)) altAglar.Add(new Subnet(cidr, cidr, sensorId));
        }
        return new NetworkMap(onceki?.MapId ?? Guid.NewGuid().ToString("N"), simdiUtc, altAglar, varliklar, onceki?.Links ?? new());
    }

    // Varlığın ağırlığını değiştirir (0-1 arası sınırlanır); yeni harita döndürür.
    public static NetworkMap AgirlikAyarla(NetworkMap m, string assetId, double agirlik)
        => m with { Assets = m.Assets.Select(a => a.AssetId == assetId ? a with { AssetWeight = Math.Round(Math.Clamp(double.IsNaN(agirlik) ? 0.5 : agirlik, 0, 1), 2) } : a).ToList() };
}

// ---------- Dosya deposu ----------
public static class AssetStore
{
    // Dosya yolu (testte BulutKlasor.Kok değiştirilir).
    public static string Yol => BulutKlasor.Dosya("ag_haritasi.json");
    private static readonly JsonSerializerOptions Sec = new() { WriteIndented = true };
    // Haritayı okur; yoksa veya bozuksa null.
    public static NetworkMap? Load() { try { return File.Exists(Yol) ? JsonSerializer.Deserialize<NetworkMap>(File.ReadAllText(Yol), Sec) : null; } catch { return null; } }
    // Haritayı yazar (önce geçici dosyaya, sonra yer değiştirir: yarım dosya kalmasın).
    public static void Save(NetworkMap m) { Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, JsonSerializer.Serialize(m, Sec)); }
}
