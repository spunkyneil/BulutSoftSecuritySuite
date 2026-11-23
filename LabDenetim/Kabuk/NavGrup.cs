// Bu dosya yeni kabuğun SOL NAVİGASYON GRUPLARINI ve "kendi rengini yöneten kontrol" işaretçisini tanımlar.
// IKendiRenginiYoneten: DesignTokens'tan boyanan yeni kabuk bileşenleri bunu uygular; TemaYonetici bu kontrolleri (eski kontroller gibi) palete çevirmez,
// yalnızca altına gömülen eski kontrolleri boyamaya devam eder.

// Programımızın adı.
namespace LabDenetim;

// Sol menünün dört grubu.
internal enum NavGrup { GenelBakis, DenetimKesif, AktifSavunma, RaporlamaAraclar }

// Grup başlıkları.
internal static class NavGrupBilgi
{
    // Menüde görünen büyük harfli başlık.
    public static string Baslik(NavGrup g) => g switch
    {
        NavGrup.GenelBakis => "GENEL BAKIŞ",
        NavGrup.DenetimKesif => "DENETİM & KEŞİF",
        NavGrup.AktifSavunma => "AKTİF SAVUNMA",
        _ => "RAPORLAMA & ARAÇLAR"
    };

    // Ekmek kırıntısında görünen normal yazımlı ad.
    public static string Adi(NavGrup g) => g switch
    {
        NavGrup.GenelBakis => "Genel Bakış",
        NavGrup.DenetimKesif => "Denetim & Keşif",
        NavGrup.AktifSavunma => "Aktif Savunma",
        _ => "Raporlama & Araçlar"
    };
}

// Rengini kendisi (DesignTokens ile) yöneten kontrol işareti.
internal interface IKendiRenginiYoneten { }
