// Bu dosya Raporlar sayfasındaki SIEM kartlarının (CEF / JSON Lines) dışa aktarma işini yapar: kaydetme penceresi + mevcut SiemCikti yazıcıları.
// (Eskiden yalnızca Durum Panosu penceresindeydi; artık Raporlar & Kanıt Paketi sayfasından da doğrudan çalışır.)
using System.Globalization;

// Programımızın adı.
namespace LabDenetim;

// SIEM dosyası dışa aktarma.
internal static class SiemDisaAktar
{
    // Bulguları CEF (cef=true) ya da JSON Lines biçiminde kullanıcının seçtiği dosyaya yazar.
    public static void Yaz(IWin32Window? sahip, bool cef, IReadOnlyList<Bulgu> bulgular, string hedef, DateTime tarih)
    {
        if (bulgular.Count == 0) { TemaliMesaj.Goster(sahip, "Önce bir tarama yapın.", "Raporlar", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var d = new SaveFileDialog
        {
            Title = cef ? "CEF dosyasını kaydet" : "JSON Lines dosyasını kaydet", Filter = cef ? "CEF dosyası (*.cef)|*.cef|Metin (*.log)|*.log" : "JSON Lines (*.jsonl)|*.jsonl",
            FileName = "Bulgular_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + (cef ? ".cef" : ".jsonl")
        };
        if (d.ShowDialog(sahip) != DialogResult.OK) return;
        try
        {
            int n = cef ? SiemCikti.CefYaz(d.FileName, bulgular.ToList(), hedef, tarih) : SiemCikti.JsonYaz(d.FileName, bulgular.ToList(), hedef, tarih);
            TemaliMesaj.Goster(sahip, $"{n} olay yazıldı:\r\n{d.FileName}", "Raporlar", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "SiemDisaAktar.Yaz"); TemaliMesaj.Goster(sahip, "Dosya yazılamadı: " + hk1.Message, "Kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
