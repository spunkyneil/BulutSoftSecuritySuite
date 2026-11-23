// Bu dosya TÜM pencerelerin ortak tabanını içerir: pencere kendini kurucuda TemaYonetici'ye kaydeder (olay tabanlı tema; CLAUDE.md kural 4).
// Yeni bir pencere yazarken Form yerine TemaliForm'dan türetin; yoksa pencere temaya göre boyanmaz (bir test bunu denetler).

// Programımızın adı.
namespace LabDenetim;

// Temaya kayıtlı pencere tabanı.
public abstract class TemaliForm : Form, IThemeAware
{
    // Pencereyi temaya kaydeder (boyama tutamaç oluşunca, altına eklenen kontroller ControlAdded ile yapılır).
    protected TemaliForm()
    {
        // Monitör başına DPI (PerMonitorV2): kodla yazılmış piksel boyut/boşluklar (Size/Padding) DPI değişince birlikte ölçeklensin.
        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;
        TemaYonetici.KaydetVeUygula(this);
        HandleCreated += (_, _) => TemaYonetici.KoyuBaslikAyarla(this, TemaYonetici.Mod == TemaModu.Koyu);   // Win32 başlık çubuğu da temayla aynı (DWM koyu mod)
    }

    // Tema değişince: pencereyi ve altındakileri yeniden çizer (renkleri TemaYonetici zaten çevirmiştir).
    public virtual void TemaUygula() { TemaYonetici.KoyuBaslikAyarla(this, TemaYonetici.Mod == TemaModu.Koyu); Invalidate(true); }
}
