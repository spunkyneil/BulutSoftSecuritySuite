// Bu dosya uygulamanın MERKEZİ TASARIM TOKENLARIDIR: yeni arayüz bileşenlerinin kullanacağı renk rolleri, boşluk ölçeği ve köşe yarıçapları burada.
// Koyu ve Açık tema değerleri AÇIKÇA yazılıdır (çalışma anı HSL ters çevirmesi yeni bileşenlerde kullanılmaz). Değerler Palet.cs ile uyumludur;
// böylece eski formlar (TemaYonetici/Palet ile boyanan) yeni kabukla aynı renkte görünür.
// KURAL (CLAUDE.md): yeni kodda doğrudan Color.FromArgb / new Font yazılmaz; renk için bu sınıf, yazı tipi için FontProvider kullanılır.
// Vurgu rengi BrandConfig.VurguRengi ile (white-label) değiştirilebilir; durum renkleri (başarı/uyarı/tehlike/bilgi) markadan bağımsızdır.
using System.Globalization;

// Programımızın adı.
namespace LabDenetim;

// Renk rolleri (semantik adlar).
internal enum TokenRol
{
    SurfaceApp, SurfaceSidebar, SurfaceCard, SurfaceElevated, SurfaceInput,
    BorderSubtle, BorderStrong,
    TextPrimary, TextSecondary, TextMuted, TextOnAccent,
    AccentPrimary, AccentHover,
    StatusSuccess, StatusWarning, StatusDanger, StatusInfo,
    StatusSuccessBg, StatusWarningBg, StatusDangerBg, StatusInfoBg
}

// Tasarım tokenları.
internal static class DesignTokens
{
    // ---- Boşluk ölçeği (4'lük) ----
    public const int SpaceXs = 4, SpaceSm = 8, SpaceMd = 12, SpaceLg = 16, SpaceXl = 24;
    // ---- Köşe yarıçapları ----
    public const int RadiusSm = 6, RadiusMd = 10, RadiusLg = 14;

    // ---- Koyu tema ----
    private static readonly Dictionary<TokenRol, string> Koyu = new()
    {
        [TokenRol.SurfaceApp] = "#141B2E", [TokenRol.SurfaceSidebar] = "#0F1522", [TokenRol.SurfaceCard] = "#1C2438", [TokenRol.SurfaceElevated] = "#222D4A", [TokenRol.SurfaceInput] = "#161D30",
        [TokenRol.BorderSubtle] = "#263044", [TokenRol.BorderStrong] = "#354466",
        [TokenRol.TextPrimary] = "#E8ECF4", [TokenRol.TextSecondary] = "#A0B0C4", [TokenRol.TextMuted] = "#8896AB", [TokenRol.TextOnAccent] = "#FFFFFF",
        [TokenRol.AccentPrimary] = "#6366F1", [TokenRol.AccentHover] = "#818CF8",
        [TokenRol.StatusSuccess] = "#10B981", [TokenRol.StatusWarning] = "#F59E0B", [TokenRol.StatusDanger] = "#EF4444", [TokenRol.StatusInfo] = "#06B6D4",
        [TokenRol.StatusSuccessBg] = "#1A3D2E", [TokenRol.StatusWarningBg] = "#3D3218", [TokenRol.StatusDangerBg] = "#3D1A1A", [TokenRol.StatusInfoBg] = "#1A2D48",
    };

    // ---- Açık tema ----
    private static readonly Dictionary<TokenRol, string> Acik = new()
    {
        [TokenRol.SurfaceApp] = "#F0F4F8", [TokenRol.SurfaceSidebar] = "#E6ECF3", [TokenRol.SurfaceCard] = "#FFFFFF", [TokenRol.SurfaceElevated] = "#F8FAFC", [TokenRol.SurfaceInput] = "#FFFFFF",
        [TokenRol.BorderSubtle] = "#E2E8F0", [TokenRol.BorderStrong] = "#CBD5E1",
        [TokenRol.TextPrimary] = "#0F172A", [TokenRol.TextSecondary] = "#475569", [TokenRol.TextMuted] = "#5A6B82", [TokenRol.TextOnAccent] = "#FFFFFF",
        [TokenRol.AccentPrimary] = "#4F46E5", [TokenRol.AccentHover] = "#4338CA",
        [TokenRol.StatusSuccess] = "#059669", [TokenRol.StatusWarning] = "#B45309", [TokenRol.StatusDanger] = "#DC2626", [TokenRol.StatusInfo] = "#0891B2",
        [TokenRol.StatusSuccessBg] = "#F0FDF4", [TokenRol.StatusWarningBg] = "#FEF3C7", [TokenRol.StatusDangerBg] = "#FEF2F2", [TokenRol.StatusInfoBg] = "#DBEAFE",
    };

    private static readonly Dictionary<string, Color> Onbellek = new(StringComparer.OrdinalIgnoreCase);

    // Şu an koyu tema mı?
    public static bool KoyuMu => TemaYonetici.Mod == TemaModu.Koyu;

    // Rolün seçili temadaki rengi.
    public static Color Renk(TokenRol rol, bool koyu) => Cevir(Hex(rol, koyu));
    // Rolün şu anki temadaki rengi.
    public static Color Renk(TokenRol rol) => Renk(rol, KoyuMu);

    // Rolün "#RRGGBB" metni (marka vurgusu uygulanmış).
    public static string Hex(TokenRol rol, bool koyu)
    {
        if (rol is TokenRol.AccentPrimary or TokenRol.AccentHover)
        {
            string vurgu = BrandConfig.Aktif.VurguRengi;
            if (!string.Equals(vurgu, BrandConfig.VarsayilanVurgu, StringComparison.OrdinalIgnoreCase))
                return rol == TokenRol.AccentPrimary ? vurgu : Karistir(vurgu, koyu ? "#FFFFFF" : "#000000", koyu ? 0.18 : 0.15);
        }
        return (koyu ? Koyu : Acik)[rol];
    }

    // Rolün KOYU "özgün" rengi (tema ne olursa olsun). Eski formlar koyu renklerle tasarlandı ve TemaYonetici/Palet bunları açık temada çevirir;
    // bu yüzden eski ekranlar rengi buradan alır: değer Palet tablosunda kayıtlıdır, böylece canlı tema değişiminde de doğru çevrilir. Yeni bileşenler Renk(rol) kullanır.
    public static Color Ozgun(TokenRol rol) => Renk(rol, true);

    // Rol adı kısayolları (şu anki tema).
    public static Color SurfaceApp => Renk(TokenRol.SurfaceApp);
    public static Color SurfaceSidebar => Renk(TokenRol.SurfaceSidebar);
    public static Color SurfaceCard => Renk(TokenRol.SurfaceCard);
    public static Color SurfaceElevated => Renk(TokenRol.SurfaceElevated);
    public static Color SurfaceInput => Renk(TokenRol.SurfaceInput);
    public static Color BorderSubtle => Renk(TokenRol.BorderSubtle);
    public static Color BorderStrong => Renk(TokenRol.BorderStrong);
    public static Color TextPrimary => Renk(TokenRol.TextPrimary);
    public static Color TextSecondary => Renk(TokenRol.TextSecondary);
    public static Color TextMuted => Renk(TokenRol.TextMuted);
    public static Color TextOnAccent => Renk(TokenRol.TextOnAccent);
    public static Color AccentPrimary => Renk(TokenRol.AccentPrimary);
    public static Color AccentHover => Renk(TokenRol.AccentHover);
    public static Color StatusSuccess => Renk(TokenRol.StatusSuccess);
    public static Color StatusWarning => Renk(TokenRol.StatusWarning);
    public static Color StatusDanger => Renk(TokenRol.StatusDanger);
    public static Color StatusInfo => Renk(TokenRol.StatusInfo);
    public static Color StatusSuccessBg => Renk(TokenRol.StatusSuccessBg);
    public static Color StatusWarningBg => Renk(TokenRol.StatusWarningBg);
    public static Color StatusDangerBg => Renk(TokenRol.StatusDangerBg);
    public static Color StatusInfoBg => Renk(TokenRol.StatusInfoBg);

    // Rengin saydamlığını ayarlar (alfa 0-255); yeni kodda doğrudan Color.FromArgb yazılmaz, bu yardımcı kullanılır.
    public static Color Saydam(Color c, int alfa) => Color.FromArgb(Math.Clamp(alfa, 0, 255), c);

    // Hesaplanan renk (kanal değerleri çalışma anında üretilir: parçacık/küre çizimleri). Sabit renk için Renk/Ozgun, saydamlık için Saydam kullanılır.
    public static Color Hesapla(int alfa, int r, int g, int b) => Color.FromArgb(Math.Clamp(alfa, 0, 255), Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));

    // İki rengi (alfa dahil) t (0-1) oranında karıştırır; geçiş/üzerine gelme canlandırmaları için.
    public static Color Karistir(Color a, Color b, float t) => Hesapla((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    // Boşluk/yarıçap değerini denetimin DPI'ına göre piksele çevirir (PerMonitorV2).
    public static int Px(Control c, int deger) => (int)Math.Round(deger * (c.DeviceDpi / 96.0));
    public static int Px(int deger, int dpi) => (int)Math.Round(deger * (dpi / 96.0));

    // kure.html'e yollanacak JSON: { tip:"tokenlar", koyu:bool, renkler:{ surfaceApp:"#...", ... } }.
    public static string KureJson(bool koyu)
    {
        var renkler = new Dictionary<string, string>();
        foreach (TokenRol r in Enum.GetValues<TokenRol>()) { string ad = r.ToString(); renkler[char.ToLowerInvariant(ad[0]) + ad[1..]] = Hex(r, koyu); }
        return System.Text.Json.JsonSerializer.Serialize(new { tip = "tokenlar", koyu, renkler }, JsonAyarlari.Girintisiz);
    }

    private static Color Cevir(string hex)
    {
        lock (Onbellek) { if (!Onbellek.TryGetValue(hex, out var c)) Onbellek[hex] = c = ColorTranslator.FromHtml(hex); return c; }
    }

    // İki rengi oranla karıştırır ("#RRGGBB").
    private static string Karistir(string a, string b, double oran)
    {
        var x = ColorTranslator.FromHtml(a); var y = ColorTranslator.FromHtml(b);
        int K(int p, int q) => (int)Math.Round(p + (q - p) * oran);
        return string.Create(CultureInfo.InvariantCulture, $"#{K(x.R, y.R):X2}{K(x.G, y.G):X2}{K(x.B, y.B):X2}");
    }
}
