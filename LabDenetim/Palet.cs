// Bu dosya uygulamanın RENK PALETİDİR (marka bütünlüğü için dışarıdan verilen bir stil kılavuzundaki Dark ve Light HEX değerleri).
// Yalnızca renk, düğme ve yazı tipi görünümünü tanımlar; kılavuzdaki metin, menü ya da işlevlerin hiçbiri bu programa taşınmamıştır.
// Yöntem: ekranların kodunda sabit yazılı olan koyu renkler, "rol" adı verilen anlamlara (sayfa zemini, kart, düğme vb.) eşlenir; her rolün Koyu ve Açık temada paletteki karşılığı vardır.
// TEK YOL (D3): renk bu tabloda yoksa olduğu gibi bırakılır (koyu temada özgün renk zaten doğru); açık temada böyle bir renk Palet.Bilinmeyen'e yazılır ve TemaTekYolTestleri başarısız olur — çözüm: rengi Tablo'ya eklemek ya da token rolü kullanmak. Eski HSL ters-çevirme yedeği KALDIRILDI.
namespace LabDenetim;

// Bir rengin ekrandaki anlamı.
internal enum PaletRol
{
    Yok, Sayfa, Kart, Girdi, Gomulu, GridBaslik, Izgara, Kenar, KenarUstunde, Secim, Hayalet, HayaletUstunde,
    Metin, IkincilMetin, Soluk,
    BirincilSolid, BirincilMetin, Mor,
    BasariSolid, BasariMetin, BasariArka,
    UyariSolid, UyariMetin, UyariArka,
    TehlikeSolid, TehlikeMetin, TehlikeAcik, TehlikeArka, TehlikeKenar,
    BilgiSolid, BilgiMetin, BilgiArka
}

// Palet tablosu ve eşleme.
internal static class Palet
{
    private static Color H(string hex) => ColorTranslator.FromHtml(hex);

    // TEK KAYNAK (D3): aşağıdaki roller DesignTokens ile AYNI değere sahiptir; değer burada tekrar yazılmaz, token'dan okunur (tembel: statik başlatıcıda DesignTokens'a dokunulmaz).
    private static readonly Dictionary<PaletRol, TokenRol> TokenRolu = new()
    {
        [PaletRol.Sayfa] = TokenRol.SurfaceApp,
        [PaletRol.Kart] = TokenRol.SurfaceCard,
        [PaletRol.Girdi] = TokenRol.SurfaceInput,
        [PaletRol.Izgara] = TokenRol.BorderSubtle,
        [PaletRol.KenarUstunde] = TokenRol.BorderStrong,
        [PaletRol.Metin] = TokenRol.TextPrimary,
        [PaletRol.BasariMetin] = TokenRol.StatusSuccess,
        [PaletRol.BasariArka] = TokenRol.StatusSuccessBg,
        [PaletRol.UyariMetin] = TokenRol.StatusWarning,
        [PaletRol.UyariArka] = TokenRol.StatusWarningBg,
        [PaletRol.TehlikeMetin] = TokenRol.StatusDanger,
        [PaletRol.TehlikeArka] = TokenRol.StatusDangerBg,
        [PaletRol.BilgiMetin] = TokenRol.StatusInfo,
        [PaletRol.BilgiArka] = TokenRol.StatusInfoBg,
    };

    // ---- Koyu tema (varsayılan): yalnızca token karşılığı OLMAYAN roller ----
    private static readonly Dictionary<PaletRol, Color> Koyu = new()
    {
        [PaletRol.Gomulu] = H("#0F1522"),
        [PaletRol.GridBaslik] = H("#1A2236"), [PaletRol.Kenar] = H("#2A3650"), 
        [PaletRol.Secim] = H("#222D4A"), [PaletRol.Hayalet] = H("#181F34"), [PaletRol.HayaletUstunde] = H("#222B42"),
        [PaletRol.IkincilMetin] = H("#A0B0C4"), [PaletRol.Soluk] = H("#8896AB"),
        [PaletRol.BirincilSolid] = H("#6366F1"), [PaletRol.BirincilMetin] = H("#818CF8"), [PaletRol.Mor] = H("#7C3AED"),
        [PaletRol.BasariSolid] = H("#059669"), 
        [PaletRol.UyariSolid] = H("#F59E0B"), 
        [PaletRol.TehlikeSolid] = H("#EF4444"), [PaletRol.TehlikeAcik] = H("#FCA5A5"), [PaletRol.TehlikeKenar] = H("#5A2424"),
        [PaletRol.BilgiSolid] = H("#06B6D4"), 
    };

    // ---- Açık tema ----
    private static readonly Dictionary<PaletRol, Color> Acik = new()
    {
        [PaletRol.Gomulu] = H("#EEF2F7"),
        [PaletRol.GridBaslik] = H("#EEF2F7"), [PaletRol.Kenar] = H("#E2E8F0"), 
        [PaletRol.Secim] = H("#DBEAFE"), [PaletRol.Hayalet] = H("#F1F5F9"), [PaletRol.HayaletUstunde] = H("#DBEAFE"),
        [PaletRol.IkincilMetin] = H("#64748B"), [PaletRol.Soluk] = H("#475569"),
        [PaletRol.BirincilSolid] = H("#6366F1"), [PaletRol.BirincilMetin] = H("#4F46E5"), [PaletRol.Mor] = H("#6D28D9"),
        [PaletRol.BasariSolid] = H("#059669"), 
        [PaletRol.UyariSolid] = H("#F59E0B"), 
        [PaletRol.TehlikeSolid] = H("#EF4444"), [PaletRol.TehlikeAcik] = H("#991B1B"), [PaletRol.TehlikeKenar] = H("#FECACA"),
        [PaletRol.BilgiSolid] = H("#06B6D4"), 
    };

    // Koyu renk (R,G,B) -> (arka plan olarak rolü, yazı rengi olarak rolü). Programın kodunda bulunan sabit renkler.
    private static readonly Dictionary<int, (PaletRol Arka, PaletRol On)> Tablo = Kur();

    private static int Anahtar(int r, int g, int b) => (r << 16) | (g << 8) | b;
    private static Dictionary<int, (PaletRol, PaletRol)> Kur()
    {
        var t = new Dictionary<int, (PaletRol, PaletRol)>();
        void E(PaletRol arka, PaletRol on, params (int r, int g, int b)[] renkler) { foreach (var c in renkler) t[Anahtar(c.r, c.g, c.b)] = (arka, on); }
        // Zeminler.
        E(PaletRol.Sayfa, PaletRol.Sayfa, (8, 13, 24), (13, 17, 23), (10, 15, 28));
        E(PaletRol.Kart, PaletRol.Kart, (15, 23, 42), (20, 29, 46), (22, 27, 34));
        E(PaletRol.Girdi, PaletRol.Girdi, (22, 30, 46), (22, 32, 50), (22, 32, 52));
        E(PaletRol.Gomulu, PaletRol.Gomulu, (10, 17, 32), (11, 18, 32), (5, 9, 18), (4, 8, 16), (4, 8, 15), (6, 9, 14), (0, 0, 0));
        E(PaletRol.GridBaslik, PaletRol.GridBaslik, (30, 41, 59));
        E(PaletRol.Izgara, PaletRol.Izgara, (28, 34, 44));
        E(PaletRol.Kenar, PaletRol.Kenar, (51, 65, 85), (48, 54, 61), (38, 46, 60));
        E(PaletRol.Secim, PaletRol.Secim, (30, 58, 95), (30, 64, 110), (24, 40, 70), (8, 47, 73));
        // Yazılar.
        E(PaletRol.Metin, PaletRol.Metin, (224, 242, 254), (226, 232, 240), (230, 237, 243), (224, 231, 255));
        // Saf beyaz: zemin olarak kart rengi, yazı olarak metin rengi (açık temada zaten çevrilmiş beyaz kart zemini tekrar çevrilince kararmasın).
        E(PaletRol.Kart, PaletRol.Metin, (255, 255, 255));
        E(PaletRol.IkincilMetin, PaletRol.IkincilMetin, (203, 213, 225), (200, 210, 222));
        E(PaletRol.Soluk, PaletRol.Soluk, (139, 148, 158), (148, 163, 184), (100, 116, 139));
        // Birincil (indigo): ana düğme, vurgu ve başlıklar.
        E(PaletRol.BirincilSolid, PaletRol.BirincilMetin, (12, 74, 110), (255, 214, 10));
        E(PaletRol.Mor, PaletRol.BirincilMetin, (30, 27, 75));
        E(PaletRol.BilgiSolid, PaletRol.BirincilMetin, (56, 189, 248), (0, 180, 255), (125, 211, 252), (129, 140, 248));
        // Başarı (yeşil).
        E(PaletRol.BasariSolid, PaletRol.BasariMetin, (22, 100, 60), (22, 163, 74), (74, 222, 128), (80, 250, 123), (16, 185, 129));
        E(PaletRol.BasariArka, PaletRol.BasariMetin, (6, 46, 34), (6, 78, 59), (5, 46, 22));
        // Uyarı (kehribar).
        E(PaletRol.UyariSolid, PaletRol.UyariMetin, (251, 191, 36), (245, 158, 11), (250, 204, 21), (253, 230, 138), (255, 232, 80), (254, 240, 138), (255, 235, 156), (251, 146, 60), (255, 160, 60), (255, 204, 153));
        E(PaletRol.UyariArka, PaletRol.UyariMetin, (59, 40, 6), (89, 60, 9));
        // Tek kullanımlık kalan renkler.
        E(PaletRol.BasariArka, PaletRol.BasariMetin, (10, 66, 48), (198, 239, 206), (220, 252, 231));
        E(PaletRol.Soluk, PaletRol.Soluk, (110, 130, 160));
        E(PaletRol.UyariArka, PaletRol.UyariMetin, (120, 100, 5));
        E(PaletRol.BilgiSolid, PaletRol.BilgiMetin, (14, 116, 144));
        E(PaletRol.IkincilMetin, PaletRol.IkincilMetin, (160, 174, 192));
        E(PaletRol.TehlikeSolid, PaletRol.TehlikeMetin, (160, 40, 40));
        E(PaletRol.Kart, PaletRol.Kart, (17, 24, 39));
        E(PaletRol.Gomulu, PaletRol.Gomulu, (2, 6, 23));
        E(PaletRol.BirincilSolid, PaletRol.BirincilMetin, (2, 132, 199));
        // Tehlike (kırmızı).
        E(PaletRol.TehlikeSolid, PaletRol.TehlikeMetin, (127, 29, 29), (248, 113, 113), (239, 68, 68), (255, 95, 95), (255, 150, 150));
        E(PaletRol.TehlikeArka, PaletRol.TehlikeMetin, (59, 13, 17), (69, 10, 10), (89, 20, 26), (50, 15, 20));
        E(PaletRol.TehlikeArka, PaletRol.TehlikeAcik, (254, 202, 202), (252, 165, 165));
        // DesignTokens'ın koyu "özgün" değerleri (DesignTokens.Ozgun): eski formlar bu renklerle boyanır ve açık temada ilgili role çevrilir.
        E(PaletRol.Sayfa, PaletRol.Sayfa, (20, 27, 46)); E(PaletRol.Gomulu, PaletRol.Gomulu, (15, 21, 34)); E(PaletRol.Kart, PaletRol.Kart, (28, 36, 56)); E(PaletRol.Secim, PaletRol.Secim, (34, 45, 74));
        E(PaletRol.Girdi, PaletRol.Girdi, (22, 29, 48)); E(PaletRol.Izgara, PaletRol.Izgara, (38, 48, 68)); E(PaletRol.KenarUstunde, PaletRol.KenarUstunde, (53, 68, 102));
        E(PaletRol.Metin, PaletRol.Metin, (232, 236, 244)); E(PaletRol.IkincilMetin, PaletRol.IkincilMetin, (160, 176, 196)); E(PaletRol.Soluk, PaletRol.Soluk, (136, 150, 171));
        E(PaletRol.BirincilSolid, PaletRol.BirincilMetin, (99, 102, 241)); E(PaletRol.BilgiSolid, PaletRol.BilgiMetin, (6, 182, 212));
        E(PaletRol.BasariArka, PaletRol.BasariMetin, (26, 61, 46)); E(PaletRol.UyariArka, PaletRol.UyariMetin, (61, 50, 24)); E(PaletRol.TehlikeArka, PaletRol.TehlikeMetin, (61, 26, 26)); E(PaletRol.BilgiArka, PaletRol.BilgiMetin, (26, 45, 72));
        return t;
    }

    // Paletteki tüm renk değerleri (zaten çevrilmiş bir rengin tekrar çevrilmesini önlemek için).
    private static readonly HashSet<int> Degerler = new(Koyu.Values.Concat(Acik.Values).Select(c => Anahtar(c.R, c.G, c.B)));
    // DesignTokens'ın iki temadaki tüm renkleri de "zaten çevrilmiş" sayılır (yeni bileşenler rengi doğrudan token'dan alır; TemaYonetici bunlara dokunmaz).
    // (Tembel kurulur: statik başlatıcıda DesignTokens/BrandConfig'e dokunmak iş parçacıkları arasında kilitlenme riski taşır.)
    private static readonly Lazy<HashSet<int>> TokenDegerleri = new(() => { var s = new HashSet<int>(); foreach (TokenRol r in Enum.GetValues<TokenRol>()) foreach (bool koyu in new[] { true, false }) { var c = DesignTokens.Renk(r, koyu); s.Add(Anahtar(c.R, c.G, c.B)); } return s; });

    // Tabloda karşılığı BULUNAMAYIP yedek yönteme (HSL ters çevirme) düşen renkler ve sayıları: hedef boş kalmasıdır (tek tema yolu; bir test denetler).
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Bilinmeyen = new();
    private static void Kaydet(Color c, bool arka) => Bilinmeyen.AddOrUpdate($"{(arka ? "arka" : "yazi")} #{c.R:X2}{c.G:X2}{c.B:X2}", 1, (_, n) => n + 1);

    // Rolün seçili temadaki rengi.
    public static Color Renk(PaletRol rol, bool koyu) => TokenRolu.TryGetValue(rol, out var tr) ? DesignTokens.Renk(tr, koyu) : rol != PaletRol.Yok && (koyu ? Koyu : Acik).TryGetValue(rol, out var c) ? c : Color.Magenta;

    // Rengin rolü (arka plan olarak mı, yazı olarak mı kullanıldığına göre); tabloda yoksa Yok.
    public static PaletRol Rol(Color c, bool arka)
    {
        if (c.IsEmpty || c.A == 0 || c.IsSystemColor) return PaletRol.Yok;
        return Tablo.TryGetValue(Anahtar(c.R, c.G, c.B), out var e) ? (arka ? e.Arka : e.On) : PaletRol.Yok;
    }

    // Programdaki koyu rengi seçili temanın paletteki karşılığına çevirir (saydamlık korunur).
    public static Color Esle(Color c, bool arka, bool koyu)
    {
        // Saydam ya da boşa dokunma.
        if (c.IsEmpty || c.A == 0) return c;
        // Sistem renkleri (kodda hiç renk verilmemiş denetimler): pencere zemini giriş rengine, denetim zemini sayfa rengine, yazılar metin rengine, seçim paletteki seçim rengine gider (iki temada da; beyaz kutular koyu temada kalmasın).
        if (c.IsSystemColor)
        {
            var rl = c.ToKnownColor() switch { KnownColor.Window => PaletRol.Girdi, KnownColor.Control or KnownColor.ControlLight => PaletRol.Sayfa, KnownColor.WindowText or KnownColor.ControlText => PaletRol.Metin, KnownColor.GrayText => PaletRol.Soluk, KnownColor.Highlight => PaletRol.Secim, KnownColor.HighlightText => PaletRol.Metin, _ => PaletRol.Yok };
            if (rl == PaletRol.Yok && !koyu) Kaydet(c, arka);
            return rl != PaletRol.Yok ? Renk(rl, koyu) : c;
        }
        var rol = Rol(c, arka);
        // Tabloda olmayan ama paletteki bir değer ise (daha önce çevrilmiş) olduğu gibi bırakılır.
        if (rol == PaletRol.Yok && (Degerler.Contains(Anahtar(c.R, c.G, c.B)) || TokenDegerleri.Value.Contains(Anahtar(c.R, c.G, c.B)))) return c;
        if (rol == PaletRol.Yok) { if (!koyu) Kaydet(c, arka); return c; }
        var sonuc = Renk(rol, koyu);
        return c.A == 255 ? sonuc : Color.FromArgb(c.A, sonuc);
    }
}
