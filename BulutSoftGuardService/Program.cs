// BulutSoft Guard Service: SYSTEM olarak çalışan, oturumdan bağımsız koruma hizmeti.
// Sorumlulukları: Nöbetçi yoklaması (15 dk kapı kontrolü), fidye izleme, canary (tuzak dosya) izleme, zincirli olay günlüğü, ikinci mühür kanalı (HKLM),
// kanıt paketi imzası ve SSH sunucu parmak izi kaydı. Arayüz bu hizmetle yerel "named pipe" üzerinden konuşur.
//
// Çalıştırma biçimleri:
//   * Hizmet olarak (üretim):  sc.exe create ... (bkz. Kurulum/HizmetKur.ps1)
//   * Konsolda (geliştirme/test):  BulutSoftGuardService.exe --gelistirme --pipe=TestGuard --kok=C:\gecici --uygulama=C:\yol\istemci.exe
//     (--gelistirme: HKLM yerine HKCU\SOFTWARE\BulutSoftTest kullanır ve yönetici gerektirmez; ÜRETİMDE kullanılmaz.)
using BulutSoftGuardService;
using LabDenetim;

// Hizmet ASLA beklenmeyen bir hatayla düşmez: iş parçacığı/görev istisnaları olay günlüğüne yazılır (K9 standardı).
AppDomain.CurrentDomain.UnhandledException += (_, e) => { try { OlayDeposu.Ekle("Sistem", "Kritik", "KORUMA HİZMETİ ALARM -> beklenmeyen hata: " + (e.ExceptionObject as Exception)?.GetType().Name + ": " + (e.ExceptionObject as Exception)?.Message); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Program.Program"); } };
TaskScheduler.UnobservedTaskException += (_, e) => { try { OlayDeposu.Ekle("Sistem", "Uyarı", "KORUMA HİZMETİ UYARI -> gözlenmemiş görev hatası: " + e.Exception.GetBaseException().Message); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Program.Program"); } e.SetObserved(); };
var b = Host.CreateApplicationBuilder(args);
// Bir arka plan hizmeti istisna fırlatsa bile ana makine durmasın (diğer görevler sürsün).
b.Services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
var ayar = GuardAyarlari.Oku(args);
b.Services.AddSingleton(ayar);
b.Services.AddSingleton<GuardCekirdek>();
b.Services.AddHostedService<GuardWorker>();
b.Services.AddHostedService<PipeSunucu>();
if (!ayar.Gelistirme) b.Services.AddWindowsService(o => o.ServiceName = KorumaProtokol.HizmetAdi);
await b.Build().RunAsync();
