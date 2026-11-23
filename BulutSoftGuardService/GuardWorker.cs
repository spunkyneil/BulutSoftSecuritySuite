// Hizmetin arka plan işçisi: açılışta motorları kurar ve 15 dakikada bir Nöbetçi yoklaması yapar.
using LabDenetim;

namespace BulutSoftGuardService;

internal sealed class GuardWorker : BackgroundService
{
    private readonly GuardCekirdek _c; private readonly GuardAyarlari _a;
    public GuardWorker(GuardCekirdek c, GuardAyarlari a) { _c = c; _a = a; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        OlayDeposu.Ekle("Sistem", "Bilgi", $"KORUMA HİZMETİ -> başladı (sürüm {typeof(GuardWorker).Assembly.GetName().Version?.ToString(3)}; Nöbetçi {(_c.Konfig.Nobet ? "AÇIK" : "KAPALI")}, fidye {(_c.Konfig.Fidye ? "AÇIK" : "KAPALI")}, canary {(_c.Konfig.Canary ? "AÇIK" : "KAPALI")}).");
        // ETW (süreç bazlı yazma atfı): yetki varsa açılır, yoksa 4 sn'lik ölçüm yedek kalır.
        try { var etw = new YazmaIzleyici(); if (!_a.Gelistirme && etw.Baslat()) { _c.Etw = etw; OlayDeposu.Ekle("Sistem", "Bilgi", "KORUMA HİZMETİ -> ETW süreç bazlı yazma izleme AÇIK."); } else { OlayDeposu.Ekle("Sistem", "Uyarı", "KORUMA HİZMETİ UYARI -> ETW kapalı (" + (etw.Hata ?? "geliştirme kipi") + "); yazan süreç atfı 4 sn'lik ölçümle yapılır."); etw.Dispose(); } } catch (Exception ex) { OlayDeposu.Ekle("Sistem", "Uyarı", "KORUMA HİZMETİ UYARI -> ETW başlatılamadı: " + ex.Message); }
        _c.Uygula();
        using var zaman = new PeriodicTimer(_c.YoklamaAraligi);
        try { if (_c.Konfig.Nobet) await _c.NobetKontrolAsync(ct); while (await zaman.WaitForNextTickAsync(ct)) if (_c.Konfig.Nobet) await _c.NobetKontrolAsync(ct); }
        catch (OperationCanceledException hk1) { HataKayit.Sessiz(hk1, "GuardWorker.ExecuteAsync"); }
    }

    public override Task StopAsync(CancellationToken ct) { OlayDeposu.Ekle("Sistem", "Uyarı", "KORUMA HİZMETİ UYARI -> durduruluyor: koruma bu andan sonra çalışmıyor."); _c.Dispose(); return base.StopAsync(ct); }
}
