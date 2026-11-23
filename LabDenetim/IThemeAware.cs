// Bu dosya tema duyarlı kontroller için ortak arayüzü içerir (olay tabanlı tema; CLAUDE.md kural 4).
// Tema değişince TemaYonetici kayıtlı pencerelerin altındaki bu arayüzü uygulayan her kontrolü BİR KEZ bilgilendirir.

// Programımızın adı.
namespace LabDenetim;

// Temaya göre kendini yeniden çizmesi gereken kontrol veya pencere.
public interface IThemeAware
{
    // Tema değişti: kendini temaya göre yenile (örneğin yeniden çiz).
    void TemaUygula();
}
