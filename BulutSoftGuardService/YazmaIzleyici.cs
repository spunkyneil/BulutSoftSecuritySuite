// ETW tabanlı yazan süreç atfı: Microsoft-Windows-Kernel-File sağlayıcısının YAZMA olaylarına sürekli abone olur.
// Her olayın başlığında süreç kimliği (PID) vardır; SurecYazmaSayaci süreç başına "kısa sürede kaç farklı dosya" eşiğini uygular.
// Eşik aşılınca süreç yolu ve imza bilgisi birlikte olay günlüğüne yazılır. Alarm anından SONRA ölçmek yerine sürekli abonelik olduğu için hızlı biten süreç de yakalanır.
// ETW oturumu açmak YÖNETİCİ/SYSTEM ister; hizmet SYSTEM olarak çalıştığı için sorun olmaz. Açılamazsa (yetki yok / sağlayıcı yok) Calisiyor=false kalır ve
// çekirdek 4 sn'lik ölçüm yöntemine döner. DÜRÜST NOT: bu sınıf canlı ETW oturumuyla yönetici olmayan geliştirme oturumunda DENENEMEZ; sayma mantığı birim testlidir.
using System.Diagnostics;
using LabDenetim;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace BulutSoftGuardService;

internal sealed class YazmaIzleyici : IDisposable
{
    private const string Oturum = "BulutSoftGuard-DosyaYazma"; private const ulong YazmaAnahtari = 0x80 | 0x10 | 0x20;   // Kernel-File: WRITE | FILENAME | FILEIO (ad eşlemesi için Create/NameCreate olayları)
    // Dosya anahtarı → yol (sınırlı; dolunca temizlenir): yazma olayında yol yoktur, ad önceki Create/NameCreate olayından gelir.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, string> _adlar = new(); private const int AdSiniri = 200_000;
    // Güvenilir sistem sürecinin yoğun yazması en çok 10 dakikada bir "Bilgi" olarak kaydedilir (alarm değil).
    private readonly Dictionary<string, DateTime> _bilgiSon = new();
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)] private static extern uint QueryDosDevice(string ad, System.Text.StringBuilder hedef, int boyut);
    // ETW dosya adları NT biçimindedir (\Device\HarddiskVolume3\Windows\...); okunabilir sürücü harfine çevirir (çevrilemezse olduğu gibi).
    internal static string NtYoluCevir(string yol)
    {
        try { if (!yol.StartsWith("\\Device\\", StringComparison.OrdinalIgnoreCase)) return yol; foreach (var d in DriveInfo.GetDrives()) { var sb = new System.Text.StringBuilder(512); string harf = d.Name.TrimEnd('\\'); if (QueryDosDevice(harf, sb, sb.Capacity) > 0 && yol.StartsWith(sb.ToString() + "\\", StringComparison.OrdinalIgnoreCase)) return harf + yol[sb.Length..]; } } catch (Exception hk1) { HataKayit.Sessiz(hk1, "YazmaIzleyici.NtYoluCevir"); }
        return yol;
    }
    private TraceEventSession? _s; private Task? _is; private readonly CancellationTokenSource _iptal = new();
    public SurecYazmaSayaci Sayac { get; } = new(); public bool Calisiyor { get; private set; } public string? Hata { get; private set; }

    public YazmaIzleyici()
    {
        // Eşik aşılınca: süreç yolu + imza bilgisiyle alarm (gürültülü/korumalı süreçler elenir).
        Sayac.Asildi += (pid, sayi) =>
        {
            try
            {
                using var p = Process.GetProcessById(pid); string ad = p.ProcessName; string? yol = null; try { yol = p.MainModule?.FileName; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "YazmaIzleyici.YazmaIzleyici"); }
                if (pid == Environment.ProcessId || SurecAvcisi.KorumaNedeni(ad, pid, yol) != null) return;   // sistem/yedekleme gibi korumalı süreçler alarm üretmez
                string imza = "bilinmiyor"; bool ms = false; if (!string.IsNullOrEmpty(yol) && File.Exists(yol)) { var d = ImzaDenetimi.Kontrol(new[] { yol }); if (d.TryGetValue(yol, out var im)) { imza = $"{im.Durum}{(im.Yayinci.Length > 0 ? " · " + im.Yayinci : "")}"; ms = im.Microsoft; } }
                // Microsoft imzalı ve Windows/Program Files altındaki süreç (CompatTelRunner, TiWorker vb.) kendi işini yapıyor: alarm DEĞİL, en çok 10 dakikada bir Bilgi.
                if (SurecAvcisi.GuvenilirSistemSureciMi(yol, ms)) { var an = DateTime.UtcNow; bool yaz; lock (_bilgiSon) { yaz = yol != null && (!_bilgiSon.TryGetValue(yol, out var son) || (an - son).TotalMinutes > 10); if (yaz) _bilgiSon[yol!] = an; } if (yaz) { OlayDeposu.Ekle("Tuzak", "Bilgi", $"TUZAK BİLGİ -> güvenilir sistem süreci yoğun yazdı (alarm değil): {ad} (PID {pid}), {Sayac.PencereSn} sn'de {sayi} dosya · {imza}"); } return; }
                var ornek = Sayac.YolOrnekleri(pid).Select(NtYoluCevir).ToList(); OlayDeposu.Ekle("Tuzak", "Kritik", $"TUZAK ALARM -> SÜREÇ BAZLI FİDYE ŞÜPHESİ (ETW): {ad} (PID {pid}) {Sayac.PencereSn} sn'de {sayi} farklı dosyaya yazdı · exe: {yol ?? "okunamadı"} · imza: {imza} · yazılan yollardan örnekler: {(ornek.Count > 0 ? string.Join(" | ", ornek) : "çözülemedi")}");
            }
            catch (Exception hk3) { HataKayit.Sessiz(hk3, "YazmaIzleyici.YazmaIzleyici"); }
        };
    }

    // Aboneliği başlatır. Yetki yoksa false döner ve Hata dolar.
    public bool Baslat()
    {
        try
        {
            if (TraceEventSession.IsElevated() != true) { Hata = "ETW için yönetici/SYSTEM yetkisi gerekli."; return false; }
            _s = new TraceEventSession(Oturum) { StopOnDispose = true };
            _s.EnableProvider("Microsoft-Windows-Kernel-File", TraceEventLevel.Informational, YazmaAnahtari);
            _s.Source.Dynamic.All += Olay;
            _is = Task.Run(() => { try { _s.Source.Process(); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "YazmaIzleyici.Baslat"); } }, _iptal.Token);
            _ = Task.Run(async () => { while (!_iptal.IsCancellationRequested) { await Task.Delay(30000); Sayac.Temizle(); } });
            Calisiyor = true; return true;
        }
        catch (Exception ex) { Hata = ex.Message; Calisiyor = false; return false; }
    }

    // Kernel-File "Write" olayı: kimlik 16. Dosya kimliği olarak FileKey (yoksa FileObject) kullanılır.
    private void Olay(TraceEvent e)
    {
        try
        {
            int id = (int)e.ID; object? k = null; try { k = e.PayloadByName("FileKey"); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "YazmaIzleyici.Olay"); } if (k == null) { try { k = e.PayloadByName("FileObject"); } catch (Exception hk6) { HataKayit.Sessiz(hk6, "YazmaIzleyici.Olay"); } }
            long anahtar = k is ulong u ? (long)u : k is long l ? l : 0;
            // Create (12) ve NameCreate (10): dosya anahtarı → yol eşlemesi.
            if ((id == 10 || id == 12) && anahtar != 0) { string? ad = null; try { ad = e.PayloadByName("FileName") as string; } catch (Exception hk7) { HataKayit.Sessiz(hk7, "YazmaIzleyici.Olay"); } if (!string.IsNullOrEmpty(ad)) { if (_adlar.Count > AdSiniri) _adlar.Clear(); _adlar[anahtar] = ad!; } return; }
            if (id != 16 || e.ProcessID <= 4) return;
            _adlar.TryGetValue(anahtar, out var yol); Sayac.Yazma(e.ProcessID, anahtar != 0 ? anahtar : e.ThreadID, null, yol);
        }
        catch (Exception hk8) { HataKayit.Sessiz(hk8, "YazmaIzleyici.Olay"); }
    }

    // Şu anda en çok farklı dosyaya yazan süreçler (ad + PID).
    public List<string> EnCokYazanlar(int n)
    {
        var l = new List<string>();
        foreach (var (pid, dosya) in Sayac.EnCok(n + 3)) { try { using var p = Process.GetProcessById(pid); string? yol = null; try { yol = p.MainModule?.FileName; } catch (Exception hk9) { HataKayit.Sessiz(hk9, "YazmaIzleyici.EnCokYazanlar"); } if (SurecAvcisi.KorumaNedeni(p.ProcessName, pid, yol) == null) l.Add($"{p.ProcessName} (PID {pid}, {dosya} dosya)"); } catch (Exception hk10) { HataKayit.Sessiz(hk10, "YazmaIzleyici.EnCokYazanlar"); } if (l.Count >= n) break; }
        return l;
    }

    public void Dispose() { _iptal.Cancel(); try { _s?.Dispose(); } catch (Exception hk11) { HataKayit.Sessiz(hk11, "YazmaIzleyici.Dispose"); } }
}
