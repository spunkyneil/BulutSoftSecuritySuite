// Bu dosya yeni kabuğun SAYFA SÖZLEŞMESİDİR: ana çalışma alanında gösterilen her ekran (UserControl ya da gömülü eski Form) bunu uygular.
// Yeni ekranlar Form değil IModulePage uygulayan UserControl olarak yazılır (CLAUDE.md kural 2); eski Form'lar FormSayfaAdaptoru ile aynı sözleşmeye uydurulur.
// Yeni UserControl sayfalar rengini DesignTokens'tan alır: IKendiRenginiYoneten + IThemeAware uygulayıp TemaUygula'da BackColor'u token rolüne bağlar
// (aksi halde TemaYonetici, üst kontrolün token rengini "özgün koyu renk" sanıp açık temada çevirmez).

// Programımızın adı.
namespace LabDenetim;

// Kabukta gösterilen sayfa.
internal interface IModulePage
{
    // Sayfanın kararlı kimliği (ör. "tarama", "savunma"); NavigasyonModeli kataloğundaki kimlikle aynıdır.
    string SayfaId { get; }
    // Sayfa başlığı (üst çubukta görünür).
    string Baslik { get; }
    // Sol menüde hangi grupta durduğu.
    NavGrup Grup { get; }
    // Sayfayı açmak için gereken yetki (null: herkes görebilir; eylemler sayfa içinde ayrıca denetlenir).
    Eylem? GerekliYetki { get; }
    // Yalnızca Uzman görünümünde mi görünür?
    bool SadeceUzman { get; }
    // Çalışma alanına konan kontrol.
    Control Gorunum { get; }
    // Sayfa görünür olurken çağrılır (veri yükleme vb.).
    Task AcildigindaAsync(CancellationToken ct);
    // Başka sayfaya geçerken çağrılır (zamanlayıcı durdurma vb.).
    Task KapanirkenAsync();
}
