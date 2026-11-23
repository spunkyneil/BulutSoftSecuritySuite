// Süreç bazlı yazma sayacı: "hangi süreç, kısa sürede kaç FARKLI dosyaya yazdı?" sorusunu yanıtlar.
// Veri kaynağı ETW (Microsoft-Windows-Kernel-File) olaylarıdır; bu sınıf yalnızca sayma mantığıdır (ETW'den bağımsız, test edilebilir).
// Dosya izleyici (FileSystemWatcher) yazan süreci söylemez; bu sayaç eşiği SÜREÇ başına uygular: tek bir süreç 10 sn'de eşiği aşarsa alarm verir.
namespace LabDenetim;

internal sealed class SurecYazmaSayaci
{
    // Pencere (sn) ve eşik: bir süreç pencerede bu kadar FARKLI dosyaya yazarsa alarm.
    public int PencereSn { get; set; } = 10; public int Esik { get; set; } = 40; public int BeklemeSn { get; set; } = 60;
    // Süreç başına son birkaç yazılan YOL (alarm raporunda "hangi dosyalara yazdı?" için).
    private readonly Dictionary<int, Queue<string>> _yollar = new(); private const int YolOrnekSayisi = 5;
    private readonly Dictionary<int, Dictionary<long, DateTime>> _surecler = new(); private readonly Dictionary<int, DateTime> _sonAlarm = new(); private readonly object _kilit = new();
    // Eşik aşıldı: süreç kimliği, pencere içindeki farklı dosya sayısı.
    public event Action<int, int>? Asildi;

    // Bir yazma olayı: süreç kimliği ve dosya kimliği (FileObject/FileKey; yol çözümü gerekmez). an: testte zaman verilir.
    public void Yazma(int pid, long dosya, DateTime? an = null, string? yol = null)
    {
        var t = an ?? DateTime.UtcNow; int sayi;
        lock (_kilit)
        {
            if (!_surecler.TryGetValue(pid, out var d)) _surecler[pid] = d = new();
            d[dosya] = t;
            if (!string.IsNullOrEmpty(yol)) { if (!_yollar.TryGetValue(pid, out var q)) _yollar[pid] = q = new(); if (!q.Contains(yol)) { q.Enqueue(yol); while (q.Count > YolOrnekSayisi) q.Dequeue(); } }
            // Pencere dışındakileri at (bu sürecin sözlüğü pencerede olanlarla sınırlı kalır).
            if (d.Count > Esik) foreach (var k in d.Where(x => (t - x.Value).TotalSeconds > PencereSn).Select(x => x.Key).ToList()) d.Remove(k);
            sayi = d.Count(x => (t - x.Value).TotalSeconds <= PencereSn);
            if (sayi < Esik || (_sonAlarm.TryGetValue(pid, out var son) && (t - son).TotalSeconds < BeklemeSn)) return;
            _sonAlarm[pid] = t;
        }
        try { Asildi?.Invoke(pid, sayi); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SurecYazmaSayaci.Yazma"); }
    }

    // Bir sürecin son yazdığı yol örnekleri (en çok 5; çözülebilenler).
    public List<string> YolOrnekleri(int pid) { lock (_kilit) return _yollar.TryGetValue(pid, out var q) ? q.ToList() : new(); }

    // Şu anda (pencerede) en çok farklı dosyaya yazan süreçler.
    public List<(int Pid, int Dosya)> EnCok(int n, DateTime? an = null)
    {
        var t = an ?? DateTime.UtcNow;
        lock (_kilit) return _surecler.Select(x => (x.Key, x.Value.Count(y => (t - y.Value).TotalSeconds <= PencereSn))).Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).Take(n).ToList();
    }

    // Çıkmış süreçlerin kayıtlarını temizler (bellek şişmesin).
    public void Temizle(DateTime? an = null) { var t = an ?? DateTime.UtcNow; lock (_kilit) foreach (var pid in _surecler.Where(x => x.Value.Count == 0 || x.Value.Values.All(v => (t - v).TotalSeconds > PencereSn * 3)).Select(x => x.Key).ToList()) { _surecler.Remove(pid); _sonAlarm.Remove(pid); _yollar.Remove(pid); } }
}
