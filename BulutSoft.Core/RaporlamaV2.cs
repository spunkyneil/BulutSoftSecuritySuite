// Bu dosya Hafta 8'dir: derin denetim bulgularını (Finding v2) MEVCUT raporlara bağlar ve kendi "Derin Denetim Raporu"nu üretir.
//  • V2Kayit: bulguları eski Bulgu biçimine çevirir ve KVKK/ISO/MITRE etiketlerini hatırlar (Uyumluluk ve Mitre sınıfları bunları kullanır; böylece HTML/PDF rapor,
//    Durum Panosu ve Yönetim Kurulu özeti yeni bulguları da doğru etiketle gösterir).
//  • DerinRapor: HTML (yazdırılabilir), JSON ve PDF çıktısı; veri kaynağı sürümleri/özetleri, uyumluluk özeti, yanlış alarm ve kural kalitesi, denetim izi bütünlüğü ve sınırlar.
// Rapor, bilgisayardan dışarı hiçbir şey göndermez.
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

// V2 ad alanı.
namespace LabDenetim.V2;

// Finding ↔ eski Bulgu köprüsü ve etiket kaydı.
internal static class V2Kayit
{
    // "Modül|Başlık" → uyumluluk etiketleri.
    private static readonly ConcurrentDictionary<string, ComplianceTags> Etiketler = new();
    private static string Anahtar(Bulgu b) => b.Modul + "|" + b.Baslik;

    // Finding'i eski Bulgu'ya çevirir ve etiketlerini kaydeder.
    public static Bulgu Donustur(Finding f) { var b = f.ToBulgu(); Etiketler[Anahtar(b)] = f.Compliance; return b; }
    // Bir Bulgu için kayıtlı etiketler (yoksa null).
    public static ComplianceTags? Etiket(Bulgu b) => Etiketler.TryGetValue(Anahtar(b), out var t) ? t : null;

    // Kullanılan MITRE tekniklerinin adı ve taktiği (bilinmeyenler genel adla gösterilir).
    private static readonly Dictionary<string, (string Ad, string Taktik)> Mitre = new()
    {
        ["T1190"] = ("Exploit Public-Facing Application", "Initial Access"), ["T1203"] = ("Exploitation for Client Execution", "Execution"), ["T1189"] = ("Drive-by Compromise", "Initial Access"),
        ["T1543.003"] = ("Create or Modify System Process: Windows Service", "Persistence"), ["T1574.001"] = ("Hijack Execution Flow: DLL Search Order Hijacking", "Persistence"), ["T1574.002"] = ("Hijack Execution Flow: DLL Side-Loading", "Persistence"),
        ["T1574.009"] = ("Hijack Execution Flow: Path Interception by Unquoted Path", "Privilege Escalation"), ["T1574.010"] = ("Hijack Execution Flow: Services File Permissions Weakness", "Privilege Escalation"),
        ["T1552.001"] = ("Unsecured Credentials: Credentials In Files", "Credential Access"), ["T1505.003"] = ("Server Software Component: Web Shell", "Persistence"), ["T1530"] = ("Data from Cloud Storage", "Collection"),
        ["T1213"] = ("Data from Information Repositories", "Collection"), ["T1083"] = ("File and Directory Discovery", "Discovery"), ["T1592"] = ("Gather Victim Host Information", "Reconnaissance"), ["T1078"] = ("Valid Accounts", "Initial Access"), ["T1557"] = ("Adversary-in-the-Middle", "Credential Access")
    };
    // MITRE etiketi (ilk teknik).
    public static MitreEtiketi? MitreEtiketi(Bulgu b)
    {
        var t = Etiket(b); var id = t?.MitreAttack.FirstOrDefault(); if (id == null) return null;
        return Mitre.TryGetValue(id, out var m) ? new MitreEtiketi(id, m.Ad, m.Taktik) : new MitreEtiketi(id, "MITRE ATT&CK tekniği", "");
    }
}
