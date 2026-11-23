// Bu dosya KOYU / AÇIK TEMA altyapısıdır. Tek bir tercih (tema.json) tüm pencereleri (açılış, giriş, ana pencere, diyaloglar) aynı anda boyar.
// Yöntem: pencereler koyu renklerle tasarlandı. Açık temada her renk, aynı tonda ama açıklığı TERSİNE çevrilmiş (HSL) karşılığıyla değiştirilir;
// böylece koyu lacivert zemin açık gri-maviye, açık yazı koyu yazıya dönüşür. Orijinal renkler saklanır, koyu temaya dönünce aynen geri gelir.
// Koyu temada kaydırma çubukları da Windows'un koyu görünümüne alınır (beyaz çubuk kalmaz).
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Tema modu.
internal enum TemaModu { Koyu, Acik }

// Tema yöneticisi.
internal static class TemaYonetici
{
    // Windows'un denetim görünümü (kaydırma çubuğu vb.) temasını değiştiren sistem fonksiyonu.
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);
    // Pencere çerçevesinin (başlık çubuğu) rengini değiştiren sistem fonksiyonu (Windows 10 20H1+ koyu başlık, Windows 11 renk özellikleri; desteklemeyen sürümde hata kodu döner, sessizce geçilir).
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int ozellik, ref int deger, int boyut);
    // Pencere çerçevesini (başlık çubuğu dahil) yeniden hesaplatıp boyatan sistem fonksiyonu (SWP_FRAMECHANGED).
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint bayrak);

    // Tercih dosyası.
    private static string Yol => BulutKlasor.Dosya("tema.json");
    // Şu anki mod (ilk erişimde dosyadan okunur).
    private static TemaModu? _mod;
    public static TemaModu Mod { get => _mod ??= Oku(); private set => _mod = value; }
    // Tema değişince BİR KEZ çağrılır (özel çizim yapan kontroller yeniden çizsin). Olay tabanlıdır: pencere taraması/zamanlayıcı YOKTUR (CLAUDE.md, kural 4).
    public static event Action? TemaDegisti;
    // Aynı olayın İngilizce adı (proje kurallarındaki ThemeChanged).
    public static event Action? ThemeChanged { add => TemaDegisti += value; remove => TemaDegisti -= value; }
    // Eski ad (mevcut abonelikler çalışmaya devam etsin).
    public static event Action? Degisti { add => TemaDegisti += value; remove => TemaDegisti -= value; }
    // Bir kontrolün özgün (koyu) renklerinin saklandığı yer.
    private sealed class Orj { public Dictionary<string, Color> R = new(); public bool Dinliyor; public bool InhB, InhF; public int Ayarliyor; }
    private static readonly ConditionalWeakTable<Control, Orj> Orjinaller = new();
    // Kayıtlı pencereler (zayıf başvuru: pencere atılınca listede asılı kalmaz) ve aynı pencerenin iki kez kaydedilmesini önleyen işaret.
    private static readonly List<WeakReference<Form>> Kayitli = new();
    private static readonly ConditionalWeakTable<Form, object> Kayitlilar = new();
    // Sınama için: şu an kayıtlı (atılmamış) pencere sayısı.
    internal static int KayitliPencereSayisi { get { Temizle(); return Kayitli.Count; } }

    // Dosyadan okur (yoksa koyu).
    private static TemaModu Oku() { try { if (File.Exists(Yol) && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Yol)) is { } d && d.TryGetValue("Mod", out var m) && m == "Acik") return TemaModu.Acik; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "TemaYonetici.Oku"); } return TemaModu.Koyu; }

    // Yazar.
    private static void Kaydet() { try { Depo.YazJson(Yol, new Dictionary<string, string> { ["Mod"] = Mod == TemaModu.Acik ? "Acik" : "Koyu" }); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "TemaYonetici.Kaydet"); } }

    // Program başında bir kez çağrılır: kayıtlı temayı önceden okur. (Eskiden burada 15 ms'lik bir zamanlayıcı açık pencereleri tarardı; kaldırıldı.)
    public static void Baslat() { _ = Mod; }

    // Bir pencereyi temaya KAYDEDER: tutamacı oluşunca (ve altına eklenen her kontrol için ControlAdded ile) TEK SEFERLİK boyanır, pencere kapanınca/atılınca abonelikten çıkar.
    // TemaliForm tabanı bunu kurucuda çağırır; tabanı kullanmayan (elle üretilen) pencereler de bu metodu çağırmalıdır.
    public static void KaydetVeUygula(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (Kayitlilar.TryGetValue(form, out _)) return;
        Kayitlilar.Add(form, new object()); Kayitli.Add(new WeakReference<Form>(form));
        // İlk boyama: kurucu bitmeden önce yapılırsa özgün renkler yanlış yakalanır; bu yüzden tutamaç oluşunca.
        void Boya1() { if (!form.IsDisposed) Uygula(form); }
        if (form.IsHandleCreated) Boya1();
        form.HandleCreated += (_, _) => Boya1();
        // Ekrana sığdırma ve açılış efekti: pencere ilk göründüğünde bir kez (eskiden zamanlayıcı her pencereye bunu uygulardı).
        form.VisibleChanged += (_, _) => { if (form.Visible) PencereEfekti.Otomatik(form); };
        form.Shown += (_, _) => PencereEfekti.Otomatik(form);
        // Bellek sızıntısı olmasın: kapanınca/atılınca listeden çık.
        void Cik() => Kayitli.RemoveAll(w => !w.TryGetTarget(out var x) || ReferenceEquals(x, form));
        form.FormClosed += (_, _) => Cik(); form.Disposed += (_, _) => Cik();
    }

    // Atılmış pencerelerin kayıtlarını siler.
    private static void Temizle() => Kayitli.RemoveAll(w => !w.TryGetTarget(out var f) || f.IsDisposed);

    // Kayıtlı ve açık (atılmamış) pencereler.
    private static List<Form> AcikPencereler() { Temizle(); var l = new List<Form>(); foreach (var w in Kayitli) if (w.TryGetTarget(out var f)) l.Add(f); return l; }

    // Kontrol ağacındaki tema duyarlı kontrollere haber verir.
    private static void TemaDuyarlilaraBildir(Control c) { if (c is IThemeAware t) t.TemaUygula(); foreach (Control k in c.Controls) TemaDuyarlilaraBildir(k); }

    // Temayı değiştirir, kaydeder ve açık tüm pencereleri yeniden boyar.
    public static void Degistir()
    {
        // Mod.
        Mod = Mod == TemaModu.Koyu ? TemaModu.Acik : TemaModu.Koyu; Kaydet();
        // Kayıtlı tüm açık pencereleri TEK SEFERDE yeniden boya ve tema duyarlı kontrollere haber ver.
        foreach (var f in AcikPencereler()) { Boya(f, true); f.Invalidate(true); TemaDuyarlilaraBildir(f); }
        // Özel çizimliler ve diğer aboneler yenilensin.
        TemaDegisti?.Invoke();
    }

    // Verilen koyu rengin geçerli temadaki karşılığı (özel çizim yapan kodlar bunu kullanır).
    public static Color C(Color koyu) => Palet.Esle(koyu, false, Mod == TemaModu.Koyu);

    // Aynı çeviri, ama rengin ARKA PLAN olarak kullanıldığı durum için (örneğin sarı bir düğme zemini birincil renge gider, sarı bir yazı kehribara).
    public static Color CA(Color koyu) => Palet.Esle(koyu, true, Mod == TemaModu.Koyu);

    // Özel çizilen denetimin temizleme (zemin) rengi: üstteki ilk dolu zemin; yoksa paletin sayfa rengi (siyah köşeleri önler).
    public static Color Zemin(Control? c)
    {
        for (var p = c?.Parent; p != null; p = p.Parent)
            if (p.BackColor.A == 255 && !p.BackColor.IsSystemColor) return p.BackColor;
        return Palet.Renk(PaletRol.Sayfa, Mod == TemaModu.Koyu);
    }

    // Bir pencereyi (ve altındakileri) henüz boyanmadıysa boyar; ayrıca sonradan eklenen kontrolleri de izler.
    private static void Uygula(Control c) { var o = Orjinaller.GetOrCreateValue(c); if (!o.Dinliyor) Boya(c, false); }

    // Kontrolü ve altındakileri boyar. zorla: tema değişti, mutlaka yeniden boya.
    private static void Boya(Control c, bool zorla)
    {
        // Kendi rengini DesignTokens'tan yöneten yeni kabuk bileşenleri (IKendiRenginiYoneten) çevrilmez/yeniden boyanmaz; yalnızca altlarındaki (eski) kontroller boyanmaya devam eder.
        if (c is IKendiRenginiYoneten)
        {
            var ok = Orjinaller.GetOrCreateValue(c);
            if (!ok.Dinliyor) { ok.Dinliyor = true; c.ControlAdded += (_, e) => { if (e.Control != null) Boya(e.Control, false); }; }
            foreach (Control k in c.Controls) Boya(k, zorla);
            return;
        }
        // Özgün renkler kaydı.
        var o = Orjinaller.GetOrCreateValue(c);
        // İlk kez: özgün renkleri yakala ve sonradan eklenenleri izle.
        if (!o.Dinliyor)
        {
            Yakala(c, o); o.Dinliyor = true; c.ControlAdded += (_, e) => { if (e.Control != null) Boya(e.Control, false); };
            // Program kodu bir rengi sonradan kendisi değiştirirse (örneğin seçili düğmeyi sarıya, durum yazısını yeşile boyarsa) yeni değer "özgün" sayılır ve palete çevrilir; böylece açık temada eski renk sızmaz.
            c.BackColorChanged += (_, _) => { if (o.Ayarliyor > 0 || (c.Parent != null && o.InhB) || (c is Form && o.R["B"] == SystemColors.Control)) return; o.R["B"] = c.BackColor; Renklendir(c, o); if (c is Button && c.IsHandleCreated) c.BeginInvoke(new Action(() => { if (!c.IsDisposed) Renklendir(c, o); })); };
            c.ForeColorChanged += (_, _) => { if (o.Ayarliyor > 0 || (c.Parent != null && o.InhF)) return; o.R["F"] = c.ForeColor; Renklendir(c, o); };
            // Izgaradaki tek tek hücrelere kodla verilen renkler de palete çevrilir.
                        if (c is DataGridView dg) dg.CellFormatting += (_, e) =>
            {
                // Izgaranın kendi (zaten çevrilmiş) varsayılan renkleriyle aynı olanlara dokunulmaz; yalnızca program kodunun hücreye özel verdiği renkler çevrilir.
                if (e.CellStyle == null) return; var koyu = Mod == TemaModu.Koyu; var d = dg.DefaultCellStyle; var a = dg.AlternatingRowsDefaultCellStyle; var r = dg.RowsDefaultCellStyle;
                Color Cevir2(Color cur, bool arka, params Color[] varsayilan) { foreach (var v in varsayilan) if (cur == v) return cur; return Palet.Esle(cur, arka, koyu); }
                e.CellStyle.BackColor = Cevir2(e.CellStyle.BackColor, true, d.BackColor, a.BackColor, r.BackColor);
                e.CellStyle.ForeColor = Cevir2(e.CellStyle.ForeColor, false, d.ForeColor, a.ForeColor, r.ForeColor);
                e.CellStyle.SelectionBackColor = Cevir2(e.CellStyle.SelectionBackColor, true, d.SelectionBackColor, a.SelectionBackColor, r.SelectionBackColor);
                e.CellStyle.SelectionForeColor = Cevir2(e.CellStyle.SelectionForeColor, false, d.SelectionForeColor, a.SelectionForeColor, r.SelectionForeColor);
            };
        }
        // Renkleri geçerli moda göre ayarla.
        Renklendir(c, o);
        // Alt kontroller.
        foreach (Control k in c.Controls) Boya(k, zorla);
    }

    // Özgün (koyu) renkleri saklar.
    private static void Yakala(Control c, Orj o)
    {
        // Ortak renkler.
        o.R["B"] = c.BackColor; o.R["F"] = c.ForeColor;
        // Renk üstten MİRAS mı alınıyor (ebeveynle aynı)? Yalnızca miras alabilen denetimlerde (Label, Panel...) "miras al"a döndürülür. Yazı kutusu, liste, düğme gibileri her zaman AÇIK renk atar (aksi hâlde sistem beyazına döner).
        // D3 düzeltmesi: AÇIK temada oluşan bir denetim, üstten devraldığı (zaten açık temaya çevrilmiş) rengi "özgün koyu" sanıp yakalıyordu; koyuya dönünce yanlış renge (koyu zeminde koyu yazı, koyu zeminde beyaz ekran) takılıyordu.
        // Artık üstüyle aynı rengi taşıyan HER denetim "miras alıyor" sayılır (rengi üstten gelir, çevrilmez). Tek istisna arka planda: kendi varsayılan zemini olan girdi denetimleri (kutu, liste, düğme...) sıfırlanınca sistem beyazına döner; onlar açıkça boyanır.
        bool kendiZemini = c is TextBoxBase or ListControl or ListView or TreeView or UpDownBase or DataGridView or DateTimePicker or MonthCalendar or (ButtonBase and not CheckBox and not RadioButton);
        // Sistem rengi (kodda hiç renk verilmemiş üst zincir) devralınırsa yazı/zemin sistem siyahı/beyazı olur; bu yüzden SİSTEM rengi yalnızca eski "miras alan" türlerde (Label, Panel...) devralınır, diğerlerinde çevrilip açıkça boyanır.
        bool eskiMiras = c is Label or Panel or GroupBox or CheckBox or RadioButton or TabPage;
        o.InhB = !kendiZemini && c.Parent != null && c.BackColor == c.Parent.BackColor && (eskiMiras || !c.BackColor.IsSystemColor);
        o.InhF = c.Parent != null && c.ForeColor == c.Parent.ForeColor && (eskiMiras || (!kendiZemini && !c.ForeColor.IsSystemColor));   // girdi denetimleri (kutu, liste, düğme) yazı rengini de açıkça alır: ResetForeColor onları sistem siyahına döndürür
        // Türe özel renkler.
        switch (c)
        {
            case DataGridView g: o.R["bg"] = g.BackgroundColor; o.R["rB"] = g.RowsDefaultCellStyle.BackColor; o.R["rF"] = g.RowsDefaultCellStyle.ForeColor; o.R["rSB"] = g.RowsDefaultCellStyle.SelectionBackColor; o.R["rSF"] = g.RowsDefaultCellStyle.SelectionForeColor; o.R["grid"] = g.GridColor; o.R["dB"] = g.DefaultCellStyle.BackColor; o.R["dF"] = g.DefaultCellStyle.ForeColor; o.R["dSB"] = g.DefaultCellStyle.SelectionBackColor; o.R["dSF"] = g.DefaultCellStyle.SelectionForeColor; o.R["hB"] = g.ColumnHeadersDefaultCellStyle.BackColor; o.R["hF"] = g.ColumnHeadersDefaultCellStyle.ForeColor; o.R["aB"] = g.AlternatingRowsDefaultCellStyle.BackColor; o.R["aF"] = g.AlternatingRowsDefaultCellStyle.ForeColor; break;
            case LinkLabel l: o.R["l"] = l.LinkColor; o.R["al"] = l.ActiveLinkColor; o.R["vl"] = l.VisitedLinkColor; break;
            case Button b: o.R["bd"] = b.FlatAppearance.BorderColor; o.R["mo"] = b.FlatAppearance.MouseOverBackColor; o.R["md"] = b.FlatAppearance.MouseDownBackColor; break;
        }
    }

    // Çerçeveyi taşıma/boyut/sıra/etkinleştirme DEĞİŞTİRMEDEN yeniden çizdirir (SWP_NOMOVE|NOSIZE|NOZORDER|NOACTIVATE|FRAMECHANGED). Başarı durumunu döndürür.
    internal static bool CerceveYenile(IntPtr tutamac) => tutamac != IntPtr.Zero && SetWindowPos(tutamac, IntPtr.Zero, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0004 | 0x0010 | 0x0020);

    // Pencerenin Win32 başlık çubuğunu koyu/açık temaya uydurur: DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 20H1+ ve 11); eski Windows 10 sürümleri için 19 yedeği.
    // Başarısız olursa (eski Windows) sessizce geçilir; pencere yine açılır. TemaliForm tutamaç oluşunca ve tema değişince çağırır.
    internal static void KoyuBaslikAyarla(Form f, bool koyuTema)
    {
        try
        {
            if (!f.IsHandleCreated) return;
            int deger = koyuTema ? 1 : 0;
            if (DwmSetWindowAttribute(f.Handle, 20, ref deger, 4) != 0) { int d2 = koyuTema ? 1 : 0; DwmSetWindowAttribute(f.Handle, 19, ref d2, 4); }
            CerceveYenile(f.Handle);   // başlık çubuğu rengi pencere odağı değişmeden anında yenilensin
        }
        catch (Exception hk) { HataKayit.Sessiz(hk, "TemaYonetici.KoyuBaslikAyarla"); }
    }

    // Rengi Windows'un beklediği 0x00BBGGRR biçimine çevirir.
    private static int RenkRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    // Başlık çubuğunu ve pencere çerçevesini formun (temaya göre ayarlı) zemin ve yazı rengine uydurur. Tutamaç hazır olunca (veya hazırsa hemen) uygulanır.
    private static void PencereCercevesi(Form f)
    {
        // Tek seferlik uygulama.
        void T()
        {
            // Hata olsa da pencere açılsın.
            try
            {
                // Koyu başlık çubuğu (20): koyu temada açık, açık temada kapalı.
                KoyuBaslikAyarla(f, Mod == TemaModu.Koyu);
                // Windows 11: başlık zemini (35), yazısı (36) ve çerçeve (34) formun renkleriyle aynı olsun.
                int zemin = RenkRef(f.BackColor), yazi = RenkRef(f.ForeColor); DwmSetWindowAttribute(f.Handle, 35, ref zemin, 4); DwmSetWindowAttribute(f.Handle, 36, ref yazi, 4); DwmSetWindowAttribute(f.Handle, 34, ref zemin, 4);
            }
            catch (Exception hk3) { HataKayit.Sessiz(hk3, "TemaYonetici.PencereCercevesi"); }
        }
        if (f.IsHandleCreated) T(); else f.HandleCreated += (_, _) => T();
    }

    // Özgün renkleri (koyu) veya çevrilmiş karşılıklarını uygular.
    private static void Renklendir(Control c, Orj o)
    {
        // Kendi atadığımız renk değişimleri "program kodu rengi değiştirdi" olayı sayılmasın.
        o.Ayarliyor++; try { RenklendirIc(c, o); } finally { o.Ayarliyor--; }
    }

    // Düğmeyi paletteki stile çevirir: Birincil (dolu), Hayalet (soluk zemin + kenarlık), Tehlike Hayalet vb. Düğmenin ilk zemin rengi hangi stile ait olduğunu belirler.
    private static void ButonStili(Button b, Orj o, bool koyu)
    {
        // Hata olsa da düğme görünsün.
        try
        {
            var rol = Palet.Rol(o.R["B"], true); Color arka, yazi, kenar;
            switch (rol)
            {
                // Dolu düğmeler: beyaz yazı.
                case PaletRol.BirincilSolid or PaletRol.Mor or PaletRol.BasariSolid or PaletRol.TehlikeSolid or PaletRol.BilgiSolid:
                    arka = Palet.Renk(rol, koyu); yazi = Color.White; kenar = arka; break;
                // Uyarı düğmesi: koyu yazı.
                case PaletRol.UyariSolid:
                    arka = Palet.Renk(rol, koyu); yazi = Color.FromArgb(15, 23, 42); kenar = arka; break;
                // Tehlike hayalet düğmesi.
                case PaletRol.TehlikeArka:
                    arka = koyu ? Color.FromArgb(36, 20, 20) : Palet.Renk(PaletRol.TehlikeArka, false); yazi = Palet.Renk(PaletRol.TehlikeMetin, koyu); kenar = Palet.Renk(PaletRol.TehlikeKenar, koyu); break;
                // Başarı / uyarı soluk düğmeleri.
                case PaletRol.BasariArka or PaletRol.UyariArka:
                    arka = Palet.Renk(rol, koyu); yazi = Palet.Renk(rol == PaletRol.BasariArka ? PaletRol.BasariMetin : PaletRol.UyariMetin, koyu); kenar = yazi; break;
                // Nötr zeminli düğmeler: Hayalet stil.
                case PaletRol.GridBaslik or PaletRol.Girdi or PaletRol.Kart or PaletRol.Gomulu or PaletRol.Sayfa or PaletRol.Secim or PaletRol.Izgara or PaletRol.Kenar:
                    arka = Palet.Renk(PaletRol.Hayalet, koyu); var fr = Palet.Rol(o.R["F"], false); yazi = fr is PaletRol.Metin or PaletRol.Yok ? Palet.Renk(PaletRol.IkincilMetin, koyu) : C(o.R["F"]); kenar = Palet.Renk(PaletRol.Kenar, koyu); break;
                // Tanınmayan: eski yol.
                default:
                    arka = CA(o.R["B"]); yazi = C(o.R["F"]); kenar = o.R["bd"] != Color.Empty ? C(o.R["bd"]) : arka; break;
            }
            // Yalnızca tanınan rollerde düğmeyi yeniden boya.
            if (rol != PaletRol.Yok) { b.BackColor = arka; b.ForeColor = yazi; b.FlatAppearance.BorderColor = kenar; b.FlatAppearance.MouseOverBackColor = rol is PaletRol.GridBaslik or PaletRol.Girdi or PaletRol.Kart or PaletRol.Gomulu or PaletRol.Sayfa or PaletRol.Secim or PaletRol.Izgara or PaletRol.Kenar ? Palet.Renk(PaletRol.HayaletUstunde, koyu) : ControlPaint.Light(arka, 0.12f); b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(arka, 0.08f); }
            else { if (o.R["bd"] != Color.Empty) b.FlatAppearance.BorderColor = C(o.R["bd"]); if (o.R["mo"] != Color.Empty) b.FlatAppearance.MouseOverBackColor = CA(o.R["mo"]); if (o.R["md"] != Color.Empty) b.FlatAppearance.MouseDownBackColor = CA(o.R["md"]); }
        }
        catch (Exception hk4) { HataKayit.Sessiz(hk4, "TemaYonetici.ButonStili"); }
    }

    // Asıl boyama.
    private static void RenklendirIc(Control c, Orj o)
    {
        // Hedef renk: H = yazı/vurgu olarak, HA = arka plan olarak çevrilir.
        Color H(string k) => C(o.R[k]);
        Color HA(string k) => CA(o.R[k]);
        bool koyu = Mod == TemaModu.Koyu;
        // Ortak.
        if (c.Parent != null && o.InhB) c.ResetBackColor(); else if (c is not Form || o.R["B"] != SystemColors.Control) c.BackColor = HA("B");
        if (c.Parent != null && o.InhF) c.ResetForeColor(); else c.ForeColor = H("F");
        // Türe özel.
        switch (c)
        {
            case DataGridView g:
                g.BackgroundColor = HA("bg"); g.GridColor = Palet.Esle(o.R["grid"], true, koyu); if (Palet.Rol(o.R["grid"], true) == PaletRol.GridBaslik) g.GridColor = Palet.Renk(PaletRol.Izgara, koyu);
                g.DefaultCellStyle.BackColor = HA("dB"); g.DefaultCellStyle.ForeColor = H("dF"); g.DefaultCellStyle.SelectionBackColor = HA("dSB"); g.DefaultCellStyle.SelectionForeColor = H("dSF");
                g.RowsDefaultCellStyle.BackColor = HA("rB"); g.RowsDefaultCellStyle.ForeColor = H("rF"); g.RowsDefaultCellStyle.SelectionBackColor = HA("rSB"); g.RowsDefaultCellStyle.SelectionForeColor = H("rSF");
                g.ColumnHeadersDefaultCellStyle.BackColor = HA("hB"); g.ColumnHeadersDefaultCellStyle.ForeColor = H("hF"); g.ColumnHeadersDefaultCellStyle.SelectionBackColor = HA("hB");
                // Kodda hiç verilmemiş varsayılanlar (seçim, hücre zemini, yazı) sistem renklerine düşmesin: palete bağlanır.
                if (o.R["dB"].IsEmpty) g.DefaultCellStyle.BackColor = Palet.Renk(PaletRol.Kart, koyu); if (o.R["dF"].IsEmpty) g.DefaultCellStyle.ForeColor = Palet.Renk(PaletRol.Metin, koyu);
                if (o.R["dSB"].IsEmpty) g.DefaultCellStyle.SelectionBackColor = Palet.Renk(PaletRol.Secim, koyu); if (o.R["dSF"].IsEmpty) g.DefaultCellStyle.SelectionForeColor = Palet.Renk(PaletRol.Metin, koyu);
                if (o.R["aB"] != Color.Empty) { g.AlternatingRowsDefaultCellStyle.BackColor = HA("aB"); g.AlternatingRowsDefaultCellStyle.ForeColor = H("aF"); }
                break;
            case LinkLabel l: l.LinkColor = H("l"); l.ActiveLinkColor = H("al"); l.VisitedLinkColor = H("vl"); break;
            case Button b: ButonStili(b, o, koyu); break;
        }
        // Pencerelerde başlık çubuğu da temaya uysun (koyu uygulamada beyaz başlık çubuğu kalmasın).
        if (c is Form fm && o.R["B"] != SystemColors.Control) PencereCercevesi(fm);
        // Kaydırma çubuklu denetimlerin Windows temasını moda göre ayarla (koyuda beyaz çubuk kalmasın).
        if (c is TextBox or ListBox or ComboBox or RichTextBox or ListView or TreeView or DataGridView or ScrollBar or ScrollableControl and not Form)
        {
            // Tutamaç hazır olunca (veya hazırsa hemen) uygula.
            void T() { try { SetWindowTheme(c.Handle, Mod == TemaModu.Koyu ? "DarkMode_Explorer" : "Explorer", null); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "TemaYonetici.RenklendirIc"); } }
            if (c.IsHandleCreated) T(); else c.HandleCreated += (_, _) => T();
        }
    }
}
