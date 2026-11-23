// Bu dosya üst çubuktaki BİLDİRİM ZİLİNİN veri kaynağıdır: son 30 dakikadaki Uyarı/Kritik olay sayısı. Olay tabanlıdır: ServisBaglami.OlayOkuyucu.Eklendi olayı gelince (kısa bir birleştirme beklemesiyle)
// olay günlüğü arka planda okunup özet çıkarılır ve arayüz iş parçacığında bildirilir. 30 dakikalık pencerenin KENDİLİĞİNDEN sönmesi (yeni olay gelmese de) için
// seyrek (60 sn) bir veri tazeleme zamanlayıcısı vardır; bu bir tema/pencere taraması DEĞİL, yalnızca süre dolumudur.
using System.Threading;

// Programımızın adı.
namespace LabDenetim;

// Bildirim sayacı kaynağı.
internal sealed class BildirimKaynagi : IDisposable
{
    private readonly SynchronizationContext? _arayuz; private readonly Func<IsikOzeti?> _hesapla; private readonly Action<int, bool, IsikOzeti?> _bildir;
    private System.Threading.Timer? _sureDolumu; private int _bekliyor, _kapali;

    // hesapla: olay günlüğünden özet çıkarır (varsayılan: KureIsiklari.Ozetle(OlayDeposu.Oku())); bildir: arayüz iş parçacığında (sayi, kritikVar, ozet).
    public BildirimKaynagi(Action<int, bool, IsikOzeti?> bildir, Func<IsikOzeti?>? hesapla = null)
    {
        _bildir = bildir; _hesapla = hesapla ?? (() => KureIsiklari.Ozetle(ServisBaglami.OlayOkuyucu.SonUyariKritik(KureIsiklari.Pencere, DateTime.Now), DateTime.Now)); _arayuz = SynchronizationContext.Current;
        ServisBaglami.OlayOkuyucu.Eklendi += OlayGeldi;
        _sureDolumu = new System.Threading.Timer(_ => Yenile(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    // Yalnızca Uyarı/Kritik olaylar sayacı etkiler.
    private void OlayGeldi(string seviye) { if (seviye is "Uyarı" or "Kritik") Yenile(); }

    // Yeniden hesaplar (art arda gelen olaylar tek hesaplamada birleşir).
    public void Yenile()
    {
        if (Volatile.Read(ref _kapali) == 1 || Interlocked.Exchange(ref _bekliyor, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400).ConfigureAwait(false);   // olay yağmurunu birleştir
                Interlocked.Exchange(ref _bekliyor, 0);
                var ozet = _hesapla(); int sayi = ozet == null ? 0 : ozet.Kritik + ozet.Uyari; bool kritik = ozet != null && ozet.Kritik > 0;
                if (Volatile.Read(ref _kapali) == 1) return;
                if (_arayuz != null) _arayuz.Post(_ => { if (Volatile.Read(ref _kapali) == 0) _bildir(sayi, kritik, ozet); }, null); else _bildir(sayi, kritik, ozet);
            }
            catch (Exception hk1) { Interlocked.Exchange(ref _bekliyor, 0); HataKayit.Sessiz(hk1, "BildirimKaynagi.Yenile"); }
        });
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _kapali, 1) == 1) return;
        ServisBaglami.OlayOkuyucu.Eklendi -= OlayGeldi; _sureDolumu?.Dispose(); _sureDolumu = null;
    }
}
