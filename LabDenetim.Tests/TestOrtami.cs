// Test derlemesi yüklenirken bir kez çalışır: hiçbir test kullanıcının GERÇEK olay günlüğüne yazmasın.
using System.Runtime.CompilerServices;

namespace LabDenetim.Tests;

internal static class TestOrtami
{
    // Olay günlüğü geçici bir klasöre yönlendirilir (kendi yolunu ayrıca ayarlayan testler bunu etkilemez).
    [ModuleInitializer]
    internal static void Basla()
    {
        OlayDeposu.Yol = Path.Combine(Path.GetTempPath(), "bs_test_olaylar_" + Environment.ProcessId + ".jsonl"); OlayDeposu.AynaAktif = false;
        // Makine geneli lisans klasörü de geçici yere gider (gerçek ProgramDataBulutSoft'a hiçbir test yazmaz).
        Lisans.MakineKlasoru = Path.Combine(Path.GetTempPath(), "bs_test_makine_" + Environment.ProcessId);
        // Pilot (salt okunur) bayrağı da gerçek makine klasöründen değil geçici klasörden aranır: bilgisayarda kalmış bir bayrak testleri etkilemesin.
        PilotMod.MakineKlasoru = Path.Combine(Path.GetTempPath(), "bs_test_pilot_" + Environment.ProcessId);
        // Koruma hizmeti kurulu bir bilgisayarda testler GERÇEK hizmete ve HKLM mühür kaydına dokunmasın / ondan etkilenmesin:
        // ikinci kanal HKCU altında bu sürece özel bir test anahtarına, istemci ise var olmayan bir boruya yönlendirilir.
        IkinciKanal.Kok = Microsoft.Win32.Registry.CurrentUser; IkinciKanal.AltAnahtar = @"SOFTWARE\BulutSoftTest_Genel_" + Environment.ProcessId + @"\Guard"; ServisIstemci.PipeAdi = "BulutSoftTestBos_" + Environment.ProcessId;
    }
}
