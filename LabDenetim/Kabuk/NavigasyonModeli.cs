// Bu dosya kabuğun NAVİGASYON KATALOĞUDUR (saf mantık, arayüz yok; birim testlenir): hangi sayfa hangi grupta, hangi rol ve modda görünür, hangi sekmeleri var.
// Yetki kuralları eski menü işleyicilerindeki YetkiKontrol çağrılarıyla BİREBİR aynıdır: Kalkan/Tuzak/Nöbetçi/Politika/Cihaz/Giriş Saldırı Radarı/Kalıcılık = Koruma; Araç/Sensör/Durum/Ağ Haritası = Arac;
// Sensör & Bildirim Merkezi = Gorme; Acil İzolasyon = SistemDegistir (Savunma Merkezi'nin üst çubuğunda ayrıca denetlenir). Sayfa düzeyinde yetki, tüm sekmeleri aynı yetkiyi isteyen sayfalarda verilir;
// karışık sayfalarda (Durum Panosu: sekmeler Arac ister, Kolay panel herkese açık) yetki sekmeye yazılır.

// Programımızın adı.
namespace LabDenetim;

// Sayfa içindeki sekme tanımı.
internal sealed record SekmeTanimi(string Id, string Baslik, Eylem? Yetki = null, bool SadeceUzman = false);

// Sol menüdeki sayfa tanımı.
internal sealed record SayfaTanimi(string Id, string Baslik, NavGrup Grup, Eylem? Yetki, bool SadeceUzman, IkonAdi Ikon, IReadOnlyList<SekmeTanimi> Sekmeler);

// Navigasyon kataloğu ve kuralları.
internal static class NavigasyonModeli
{
    private static readonly SekmeTanimi[] Yok = Array.Empty<SekmeTanimi>();

    // Tüm sayfalar (sol menüde görünme sırasıyla).
    public static readonly IReadOnlyList<SayfaTanimi> Sayfalar = new[]
    {
        // 1. GENEL BAKIŞ
        new SayfaTanimi("durum", "Durum Panosu", NavGrup.GenelBakis, null, false, IkonAdi.Panel, new[] { new SekmeTanimi("radar", "Radar / Küre", null, true), new SekmeTanimi("pano", "Durum Ayrıntıları", Eylem.Arac, true) }),
        new SayfaTanimi("olay", "Olay Zaman Çizgisi", NavGrup.GenelBakis, null, true, IkonAdi.Olay, Yok),
        // 2. DENETİM & KEŞİF
        new SayfaTanimi("tarama", "Tarama Merkezi", NavGrup.DenetimKesif, null, false, IkonAdi.Tarama, new[] { new SekmeTanimi("yeni", "Yeni Tarama + Asistan", null, true), new SekmeTanimi("gecmis", "Geçmiş Operasyonlar") }),
        new SayfaTanimi("ag", "Ağ & Cihaz Envanteri", NavGrup.DenetimKesif, Eylem.Arac, true, IkonAdi.Ag, new[] { new SekmeTanimi("cihaz", "Cihaz Envanteri", Eylem.Koruma), new SekmeTanimi("harita", "Ağ Haritası", Eylem.Arac) }),
        new SayfaTanimi("kalici", "Kalıcılık Denetçisi", NavGrup.DenetimKesif, Eylem.Koruma, true, IkonAdi.Kalici, Yok),   // eski menü işleyicisi YetkiKontrol(Koruma) istiyordu; devre dışı bırakma pencere içinde ayrıca Yönetici ister
        // 3. AKTİF SAVUNMA
        new SayfaTanimi("savunma", "Savunma Merkezi", NavGrup.AktifSavunma, Eylem.Koruma, true, IkonAdi.Kalkan, new[] { new SekmeTanimi("kalkan", "Aktif Kalkanlarım", Eylem.Koruma), new SekmeTanimi("tuzak", "Tuzaklar & Fidye", Eylem.Koruma), new SekmeTanimi("nobet", "Nöbetçi & Politika", Eylem.Koruma), new SekmeTanimi("brute", "Giriş Saldırı Radarı", Eylem.Koruma) }),   // eski menüde dördü de YetkiKontrol(Koruma) ister
        new SayfaTanimi("trafik", "Canlı Ağ Trafiği", NavGrup.AktifSavunma, null, true, IkonAdi.Trafik, Yok),
        // 4. RAPORLAMA & ARAÇLAR
        new SayfaTanimi("rapor", "Raporlar & Kanıt Paketi", NavGrup.RaporlamaAraclar, Eylem.Rapor, false, IkonAdi.Rapor, Yok),
        new SayfaTanimi("arac", "Güvenlik Araç Kutusu", NavGrup.RaporlamaAraclar, Eylem.Arac, true, IkonAdi.Arac, Yok),
        new SayfaTanimi("lab", "Gelişmiş / Sensörler", NavGrup.RaporlamaAraclar, null, true, IkonAdi.Lab, new[] { new SekmeTanimi("sensor", "Uzak Sensörler (VM)", Eylem.Arac), new SekmeTanimi("merkez", "Sensör & Bildirim Merkezi", Eylem.Gorme) }),
    };

    // Kimliğe göre sayfa.
    public static SayfaTanimi? Bul(string id) => Sayfalar.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    // Kolay görünümde yalnızca Uzman'a özgü olmayan sayfalar menüde görünür.
    public static IEnumerable<SayfaTanimi> Gorunenler(ModTuru mod) => Sayfalar.Where(p => mod == ModTuru.Uzman || !p.SadeceUzman);

    // Sayfanın moda göre görünen sekmeleri.
    public static IEnumerable<SekmeTanimi> GorunenSekmeler(SayfaTanimi p, ModTuru mod) => p.Sekmeler.Where(s => mod == ModTuru.Uzman || !s.SadeceUzman);

    // Gereken yetki bu rolde yok mu? (yetki null ise kilit yok)
    public static bool Kilitli(Eylem? yetki, Rol? rol) => yetki != null && !Yetki.Izinli(rol, yetki.Value);

    // Menüde kilitli öğenin fare açıklaması.
    public static string KilitAciklamasi(Rol? rol) => "⚿ Rolünüz (" + (rol == null ? "oturum yok" : Yetki.RolAdi(rol.Value)) + ") bu özelliği kullanamaz. Yetki için bir Yönetici'ye başvurun.";

    // Sayfa ve (varsa) sekme için erişim kararı: izin ya da gereken yetki.
    public static (bool Izinli, Eylem? Eksik) Erisim(string sayfaId, string? sekmeId, Rol? rol)
    {
        var p = Bul(sayfaId); if (p == null) return (false, null);
        if (Kilitli(p.Yetki, rol)) return (false, p.Yetki);
        var s = sekmeId == null ? null : p.Sekmeler.FirstOrDefault(x => x.Id == sekmeId);
        if (s != null && Kilitli(s.Yetki, rol)) return (false, s.Yetki);
        return (true, null);
    }
}
