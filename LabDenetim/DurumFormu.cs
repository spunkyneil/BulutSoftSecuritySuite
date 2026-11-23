// Bu dosya "Durum Panosu" penceresini çalıştırır. İçinde 5 sekme vardır:
// 1) Pano: puan, puan trendi, son olaylar, "bugün yapılacak 3 şey". 2) Düzeltme Planı: sorunlar, puan kazancına göre sıralı (MITRE etiketli).
// 3) Yönetici Özeti: teknik bilmeyen biri için tek sayfa (HTML olarak kaydedilir). 4) Karşılaştır: iki eski taramayı yan yana koyar.
// 5) SIEM: sonuçları CEF veya JSON olarak dosyaya yazar. Hiçbir sekme sistemde değişiklik yapmaz ve internete bir şey göndermez.
using System.Net;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Düzeltme planındaki tek madde.
internal sealed record PlanMaddesi(int Sira, Bulgu B, int Kazanc, int YeniPuan, string Mitre);

// Plan ve özet mantığı (ekrandan bağımsız; test edilebilir).
internal static class DurumAnalizi
{
    // Bir bulgunun düzeltilince kazandıracağı puan (puan hesabıyla aynı ağırlıklar).
    public static int Kazanc(Onem o) => o switch { Onem.Yuksek => 15, Onem.Orta => 7, Onem.Dusuk => 3, _ => 0 };

    // Düzeltme planı: sorunları en çok puan kazandıranlar başta olacak şekilde sıralar ve toplam puanı adım adım gösterir.
    public static List<PlanMaddesi> Plan(IEnumerable<Bulgu> bulgular, int mevcutPuan)
    {
        // Yalnızca gerçek sorunlar (yeşil ve atlananlar hariç), büyükten küçüğe.
        var sorunlar = bulgular.Where(b => b.Onem != Onem.Bilgi && !b.Atlandi).OrderByDescending(b => Kazanc(b.Onem)).ThenBy(b => b.Modul).ThenBy(b => b.Baslik).ToList();
        // Sonuç listesi ve yürüyen puan.
        var l = new List<PlanMaddesi>(); int p = mevcutPuan;
        // Her sorun için bir madde.
        for (int i = 0; i < sorunlar.Count; i++)
        {
            // Kazanç ve yeni puan (100'ü aşmaz).
            int k = Kazanc(sorunlar[i].Onem); p = Math.Min(100, p + k);
            // Maddeyi ekle.
            l.Add(new PlanMaddesi(i + 1, sorunlar[i], k, p, Mitre.Yazi(sorunlar[i])));
        }
        // Planı ver.
        return l;
    }

    // Puanı harf notuna ve sade bir cümleye çevirir.
    public static (string Harf, string Yorum) Not(int puan) => puan switch
    {
        >= 90 => ("A", "Çok iyi. Ciddi bir açık görünmüyor."),
        >= 75 => ("B", "İyi. Birkaç küçük iyileştirme yeterli."),
        >= 60 => ("C", "Orta. Önemli maddeler var, bu hafta ilgilenin."),
        >= 40 => ("D", "Zayıf. Bir an önce müdahale gerekiyor."),
        _ => ("F", "Kritik. Önce en kırmızı maddelerden başlayın.")
    };

    // Yönetici özeti satırları (teknik terim kullanmadan). Hem ekranda hem HTML'de aynı metin kullanılır.
    public static List<string> OzetSatirlari(string hedef, DateTime tarih, int puan, List<Bulgu> bulgular, int oncekiPuan)
    {
        // Sayılar.
        int kirmizi = bulgular.Count(b => b.Onem == Onem.Yuksek && !b.Atlandi), turuncu = bulgular.Count(b => b.Onem == Onem.Orta && !b.Atlandi), sari = bulgular.Count(b => b.Onem == Onem.Dusuk && !b.Atlandi);
        // Not.
        var (harf, yorum) = Not(puan);
        // Satırlar.
        var s = new List<string>
        {
            // Başlık satırı.
            $"{hedef} için güvenlik durumu — {tarih:dd.MM.yyyy HH:mm}",
            // Genel durum.
            $"Genel not: {harf} ({puan}/100). {yorum}",
            // Sayılar.
            $"Bulunan sorunlar: {kirmizi} ciddi, {turuncu} orta, {sari} küçük."
        };
        // Önceki tarama varsa trend cümlesi.
        if (oncekiPuan >= 0) s.Add(puan > oncekiPuan ? $"Önceki taramaya göre iyileşme var (+{puan - oncekiPuan} puan)." : puan < oncekiPuan ? $"Önceki taramaya göre kötüleşme var ({puan - oncekiPuan} puan)." : "Önceki taramayla aynı seviyede.");
        // En önemli 3 madde.
        var ilk = Plan(bulgular, puan).Take(3).ToList();
        // Varsa ilk üç iş.
        if (ilk.Count > 0)
        {
            // Alt başlık.
            s.Add("Öncelikli 3 iş (en çok puan kazandıranlar):");
            // Her madde: başlık ve ne yapılacağı.
            foreach (var m in ilk) s.Add($"  {m.Sira}. {m.B.Baslik} → {m.B.Oneri} (düzelirse +{m.Kazanc} puan)");
        }
        // Hiç sorun yoksa güzel haber.
        else s.Add("Düzeltilmesi gereken bir madde görünmüyor.");
        // Alt not: kapsam.
        s.Add("Not: Bu rapor yalnızca kendi kendi iç ağınızda yapılan otomatik kontrollere dayanır; tam bir sızma testinin yerini tutmaz.");
        // Hazır.
        return s;
    }

    // Yönetici özetini tek sayfalık HTML'e çevirir (tüm metinler güvenli şekilde kodlanır).
    public static string OzetHtml(List<string> satirlar)
    {
        // HTML başlığı ve sade stil.
        var sb = new StringBuilder("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Yönetici Özeti</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:780px;margin:40px auto;color:#0f172a;line-height:1.55}h1{font-size:22px;border-bottom:3px solid #0ea5e9;padding-bottom:8px}p{margin:10px 0}.k{background:#f1f5f9;border-left:4px solid #0ea5e9;padding:8px 12px}.n{color:#64748b;font-size:13px}</style></head><body>");
        // İlk satır başlık.
        sb.Append("<h1>").Append(WebUtility.HtmlEncode(satirlar[0])).Append("</h1>");
        // Diğer satırlar.
        foreach (var x in satirlar.Skip(1))
            // Not satırı küçük, girintili satırlar kutu, diğerleri normal paragraf.
            sb.Append(x.StartsWith("Not:") ? "<p class=\"n\">" : x.StartsWith("  ") ? "<p class=\"k\">" : "<p>").Append(WebUtility.HtmlEncode(x.Trim())).Append("</p>");
        // Kapanış.
        return sb.Append("</body></html>").ToString();
    }

    // İki taramanın karşılaştırmasını HTML'e çevirir.
    public static string KarsilastirmaHtml(GecmisKayit yeni, GecmisKayit eski, Karsilastirma k)
    {
        // Başlık.
        var sb = new StringBuilder("<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Karşılaştırmalı Rapor</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:820px;margin:40px auto;color:#0f172a}h1{font-size:22px}h2{font-size:16px;margin-top:24px}li{margin:4px 0}.r{color:#b91c1c}.g{color:#15803d}</style></head><body>");
        // Ana başlık ve özet.
        sb.Append("<h1>Karşılaştırmalı Güvenlik Raporu</h1><p>").Append(WebUtility.HtmlEncode($"Eski: {eski.Tarih:dd.MM.yyyy HH:mm} ({eski.Hedef}, puan {eski.Puan}) → Yeni: {yeni.Tarih:dd.MM.yyyy HH:mm} ({yeni.Hedef}, puan {yeni.Puan})")).Append("</p><p>").Append(WebUtility.HtmlEncode(k.Ozet)).Append("</p>");
        // Yeni çıkan sorunlar.
        sb.Append("<h2 class=\"r\">Yeni çıkan sorunlar (" + k.Yeni.Count + ")</h2><ul>");
        foreach (var x in k.Yeni) sb.Append("<li>").Append(WebUtility.HtmlEncode(x)).Append("</li>");
        // Kapanan sorunlar.
        sb.Append("</ul><h2 class=\"g\">Kapanan sorunlar (" + k.Kapanan.Count + ")</h2><ul>");
        foreach (var x in k.Kapanan) sb.Append("<li>").Append(WebUtility.HtmlEncode(x)).Append("</li>");
        // Kapanış.
        return sb.Append("</ul></body></html>").ToString();
    }
}

// Durum Panosu penceresi.
internal sealed class DurumFormu : SayfaDenetimi
{
    // Pencerenin renkleri (programın koyu temasıyla uyumlu).
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Panel = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Ekrandaki sonuçlar ve bilgiler.
    private readonly List<Bulgu> _b; private readonly string _hedef; private readonly int _puan; private readonly DateTime _tarih;
    // Geçmiş kayıtları (yeniden eskiye).
    private readonly List<GecmisKayit> _gecmis = GecmisDeposu.Listele();
    // Sekme içerikleri.
    private readonly List<Control> _sayfalar = new(); private readonly List<Button> _sekmeDugmeleri = new();
    // Karşılaştırma bileşenleri.
    private readonly ComboBox _cEski = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 }, _cYeni = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    // Karşılaştırma sonucu kutusu ve en son karşılaştırma.
    private readonly TextBox _kSonuc = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Panel, ForeColor = Yazi, Font = FontProvider.Al(FontRol.Mono), BorderStyle = BorderStyle.None };
    // Son karşılaştırma bilgisi (HTML kaydı için).
    private (GecmisKayit Yeni, GecmisKayit Eski, Karsilastirma K)? _sonK;
    // Yönetici özeti satırları.
    private List<string> _ozet = new();
    // Puan trendi çizim alanı.
    private readonly Panel _trend = new() { Dock = DockStyle.Fill, BackColor = Panel };

    // Pencereyi kurar.
    public DurumFormu(List<Bulgu> bulgular, string hedef, int puan, DateTime tarih)
    {
        // Bilgileri sakla.
        _b = bulgular; _hedef = string.IsNullOrWhiteSpace(hedef) ? "Bilinmeyen hedef" : hedef; _puan = puan; _tarih = tarih;
        // Pencere ayarları.
        Text = "Durum Panosu"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1000, 680); MinimumSize = new Size(860, 560); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body); DoubleBuffered = true;
        // Sekme düğmeleri için üst şerit.
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, BackColor = Zemin, Padding = new Padding(10, 6, 0, 0) };
        // İçerik alanı.
        var alan = new Panel { Dock = DockStyle.Fill, BackColor = Zemin, Padding = new Padding(12) };
        // Sayfaları üret.
        var sayfalar = new (string Ad, Control Icerik)[] { ("▦ Pano", PanoKur()), ("✔ Düzeltme Planı", PlanKur()), ("✎ Yönetici Özeti", OzetKur()), ("⇄ Karşılaştır", KarsilastirKur()), ("⇪ SIEM Çıktısı", SiemKur()) };
        // Her sayfa için düğme ve içerik.
        for (int i = 0; i < sayfalar.Length; i++)
        {
            // Sayfa numarası (kapanışta doğru değeri tutmak için kopya).
            int no = i;
            // İçerik tüm alanı kaplasın, başta gizli olsun.
            sayfalar[i].Icerik.Dock = DockStyle.Fill; sayfalar[i].Icerik.Visible = false; alan.Controls.Add(sayfalar[i].Icerik); _sayfalar.Add(sayfalar[i].Icerik);
            // Sekme düğmesi.
            var d = new EfektliButon { Text = sayfalar[i].Ad, Size = new Size(170, 34), FlatStyle = FlatStyle.Flat, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 0, 6, 0) };
            // Tıklayınca o sekmeyi göster.
            d.Click += (_, _) => Sekme(no); _sekmeDugmeleri.Add(d); serit.Controls.Add(d);
        }
        // Önce alanı, sonra şeridi ekle (Dock sırası için).
        Controls.Add(alan); Controls.Add(serit);
        // İlk sekme.
        Sekme(0);
        // Açılışta metin kutularındaki yazılar mavi seçili görünmesin.
        Shown += (_, _) => { foreach (var c in Tumu(this)) if (c is TextBox tb) { tb.SelectionLength = 0; tb.TabStop = false; } ActiveControl = null; };
    }

    // Bir kontrolün altındaki tüm kontrolleri düz liste olarak verir.
    private static IEnumerable<Control> Tumu(Control k) { foreach (Control c in k.Controls) { yield return c; foreach (var a in Tumu(c)) yield return a; } }

    // Seçilen sekmeyi gösterir, diğerlerini gizler.
    private void Sekme(int no)
    {
        // Hepsini dolaş.
        for (int i = 0; i < _sayfalar.Count; i++)
        {
            // Yalnızca seçileni göster.
            _sayfalar[i].Visible = i == no;
            // Seçili düğme mavi, diğerleri koyu.
            _sekmeDugmeleri[i].BackColor = i == no ? DesignTokens.Ozgun(TokenRol.AccentPrimary) : Panel; _sekmeDugmeleri[i].ForeColor = i == no ? Color.White : Soluk; _sekmeDugmeleri[i].FlatAppearance.BorderColor = i == no ? Mavi : DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        }
    }

    // Küçük yardımcı: koyu zeminli etiket.
    private static Label Etiket(string t, float boy = 10f, bool kalin = false, Color? renk = null) => new() { Text = t, AutoSize = false, Dock = DockStyle.Fill, ForeColor = renk ?? Yazi, Font = FontProvider.Gecici("Segoe UI", boy, kalin ? FontStyle.Bold : FontStyle.Regular), UseMnemonic = false };

    // Küçük yardımcı: koyu düğme.
    private static Button Dugme(string t, int g) => new EfektliButon() { Text = t, Size = new Size(g, 36), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 4, 8, 0) };

    // 1) Pano sekmesi.
    private Control PanoKur()
    {
        // Ana ızgara: üstte puan, ortada trend, altta iki liste.
        var t = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, BackColor = Zemin }; t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Satır boyları.
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 42)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        // Puan ve not.
        var (harf, yorum) = DurumAnalizi.Not(_puan);
        // Puan kutusu (taramadan sonra anlamlı).
        string puanYazi = _b.Count == 0 ? "Henüz tarama yok.\r\n\"⚡ TARAMAYI BAŞLAT\" ile bir tarama yapın veya Geçmiş'ten bir kaydı yükleyin." : $"{_puan}/100   Not: {harf}\r\n{yorum}";
        // Üst bant iki sütunu kaplasın.
        var ust = Etiket(puanYazi, 17f, true, _b.Count == 0 ? Soluk : _puan >= 75 ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : _puan >= 50 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusDanger)); ust.Padding = new Padding(14, 8, 0, 0); ust.BackColor = Panel; t.Controls.Add(ust, 0, 0); t.SetColumnSpan(ust, 2);
        // Trend çizimi: başlık + çizim alanı.
        var trendKutu = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Margin = new Padding(0, 8, 0, 0) }; _trend.Paint += TrendCiz; _trend.Resize += (_, _) => _trend.Invalidate(); trendKutu.Controls.Add(_trend); trendKutu.Controls.Add(new Label { Text = "  Güvenlik puanı trendi (son taramalar, soldan sağa eskiden yeniye)", Dock = DockStyle.Top, Height = 26, ForeColor = Soluk, BackColor = Panel, UseMnemonic = false });
        // Trend iki sütunu kaplasın.
        t.Controls.Add(trendKutu, 0, 1); t.SetColumnSpan(trendKutu, 2);
        // Bugün yapılacak 3 şey.
        var uc = Plan3Kutusu(); t.Controls.Add(uc, 0, 2);
        // Son olaylar.
        var olay = new ListBox { Dock = DockStyle.Fill, BackColor = Panel, ForeColor = Yazi, BorderStyle = BorderStyle.None, Font = FontProvider.Al(FontRol.Mono), IntegralHeight = false, HorizontalScrollbar = true };
        // En yeni 12 olayı yaz.
        foreach (var o in ServisBaglami.OlayOkuyucu.SonOlaylar(12)) olay.Items.Add($"{o.Zaman:dd.MM HH:mm}  [{o.Seviye,-6}] {o.Mesaj}");
        // Boşsa bilgi.
        if (olay.Items.Count == 0) olay.Items.Add("Henüz önemli bir olay kaydı yok.");
        // Başlıklı kutu.
        var olayKutu = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Margin = new Padding(8, 8, 0, 0) }; olayKutu.Controls.Add(olay); olayKutu.Controls.Add(new Label { Text = "  Son olaylar (Olay Zaman Çizgisi'nden)", Dock = DockStyle.Top, Height = 26, ForeColor = Soluk, BackColor = Panel, UseMnemonic = false });
        // Yerleştir.
        t.Controls.Add(olayKutu, 1, 2);
        // Geri ver.
        return t;
    }

    // "Bugün yapılacak 3 şey" kutusu.
    private Control Plan3Kutusu()
    {
        // Plandan ilk 3.
        var ilk = DurumAnalizi.Plan(_b, _puan).Take(3).ToList();
        // Metin.
        string m = ilk.Count == 0 ? (_b.Count == 0 ? "Tarama yapıldığında burada ilk 3 öncelikli iş görünür." : "Harika! Bugün yapılacak bir düzeltme görünmüyor.") : string.Join("\r\n\r\n", ilk.Select(x => $"{x.Sira}. {x.B.Baslik}\r\n   → {x.B.Oneri}\r\n   (+{x.Kazanc} puan)"));
        // Kaydırılabilir salt-okunur kutu.
        var kutu = new TextBox { Multiline = true, ReadOnly = true, Text = m, Dock = DockStyle.Fill, BackColor = Panel, ForeColor = Yazi, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Font = FontProvider.Al(FontRol.Body) };
        // Başlıklı kap.
        var kap = new Panel { Dock = DockStyle.Fill, BackColor = Panel, Margin = new Padding(0, 8, 8, 0) }; kap.Controls.Add(kutu); kap.Controls.Add(new Label { Text = "  Bugün yapılacak 3 şey", Dock = DockStyle.Top, Height = 26, ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning), BackColor = Panel, UseMnemonic = false });
        // Geri ver.
        return kap;
    }

    // Puan trend çizgisini çizer.
    private void TrendCiz(object? s, PaintEventArgs e)
    {
        // Pürüzsüz çizim.
        var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // Son 20 kaydı eskiden yeniye diz; o an ekrandaki puan henüz kayıtlı değilse sona eklenmez (geçmişte zaten vardır).
        var p = _gecmis.Take(20).Reverse().Select(k => k.Puan).ToList();
        // Çizim alanı.
        var r = new Rectangle(40, 26, Math.Max(10, _trend.Width - 60), Math.Max(10, _trend.Height - 50));
        // Yatay kılavuz çizgileri ve değerleri (0, 50, 100).
        using var kalem = new Pen(DesignTokens.Ozgun(TokenRol.SurfaceElevated)); using var fr = new SolidBrush(Soluk);
        // Üç çizgi.
        foreach (int v in new[] { 0, 50, 100 }) { float y = r.Bottom - v / 100f * r.Height; g.DrawLine(kalem, r.Left, y, r.Right, y); g.DrawString(v.ToString(), Font, fr, 6, y - 8); }
        // Yeterli veri yoksa açıklama.
        if (p.Count < 2) { g.DrawString(p.Count == 0 ? "Trend için en az 2 tarama gerekir (şu an kayıt yok)." : "Trend için en az 2 tarama gerekir (şu an 1 kayıt var).", Font, fr, r.Left + 10, r.Top + r.Height / 2 - 8); return; }
        // Noktaları hesapla.
        var nok = p.Select((v, i) => new PointF(r.Left + i * (r.Width / (float)(p.Count - 1)), r.Bottom - v / 100f * r.Height)).ToArray();
        // Çizgi ve noktalar.
        using var cizgi = new Pen(Mavi, 2.5f); g.DrawLines(cizgi, nok);
        // Her noktaya küçük daire ve son noktaya değer.
        using var nokta = new SolidBrush(Mavi); foreach (var n in nok) g.FillEllipse(nokta, n.X - 4, n.Y - 4, 8, 8);
        // Son değer yazısı.
        g.DrawString(p[^1].ToString(), FontProvider.Al(FontRol.Caption, FontStyle.Bold), Brushes.White, nok[^1].X - 10, nok[^1].Y - 22);
    }

    // 2) Düzeltme Planı sekmesi.
    private Control PlanKur()
    {
        // Ana kap.
        var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Zemin }; t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Açıklama.
        var plan = DurumAnalizi.Plan(_b, _puan);
        // Başlık metni.
        t.Controls.Add(Etiket(plan.Count == 0 ? (_b.Count == 0 ? "Önce bir tarama yapın; plan taramadan çıkan sorunlarla oluşur." : "Düzeltilecek madde yok. Puanınız zaten en iyi durumda.") : $"{plan.Count} madde var. Üstten başlayın: en çok puan kazandıran iş en üstte. Hepsi bitince puan {_puan} → {plan[^1].YeniPuan} olur. MITRE sütunu, bu zayıflığın hangi saldırı yöntemine kapı açtığını gösterir.", 10f, false, Soluk), 0, 0);
        // Tablo.
        var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        // Sütunlar.
        foreach (var (ad, baslik, agirlik) in new[] { ("S", "#", 4), ("Onem", "Önem", 9), ("Baslik", "Sorun", 28), ("Oneri", "Ne yapmalı?", 38), ("K", "Kazanç", 7), ("P", "Yeni puan", 8), ("M", "MITRE ATT&CK", 22) }) { g.Columns.Add(ad, baslik); g.Columns[ad]!.FillWeight = agirlik; }
        // Satırlar.
        foreach (var m in plan) { int i = g.Rows.Add(m.Sira, m.B.Onem switch { Onem.Yuksek => "KRİTİK", Onem.Orta => "UYARI", _ => "DİKKAT" }, m.B.Baslik, m.B.Oneri, "+" + m.Kazanc, m.YeniPuan, m.Mitre); g.Rows[i].Cells[1].Style.ForeColor = m.B.Onem == Onem.Yuksek ? DesignTokens.Ozgun(TokenRol.StatusDanger) : m.B.Onem == Onem.Orta ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusWarning); }
        // Koyu renkler (programın diğer tablolarıyla aynı).
        g.BackgroundColor = Panel; g.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); g.BorderStyle = BorderStyle.None; g.EnableHeadersVisualStyles = false; g.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); g.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; g.DefaultCellStyle.BackColor = Panel; g.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); g.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); g.DefaultCellStyle.SelectionForeColor = Color.White; g.RowTemplate.Height = 34; g.DefaultCellStyle.WrapMode = DataGridViewTriState.True; g.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(g);
        // Yerleştir.
        t.Controls.Add(g, 0, 1);
        // Geri ver.
        return t;
    }

    // 3) Yönetici Özeti sekmesi.
    private Control OzetKur()
    {
        // Önceki tarama puanı (bu hedefin, en son kayıt hariç değil: en son kayıt şu anki olabilir; ikinci kayıt öncekidir).
        var ayni = _gecmis.Where(k => string.Equals(k.Hedef, _hedef, StringComparison.OrdinalIgnoreCase)).ToList();
        // Ekrandaki tarama kayıtlıysa bir öncekini, değilse en sonuncuyu al.
        int onceki = ayni.Count >= 2 ? ayni[1].Puan : -1;
        // Satırları üret.
        _ozet = _b.Count == 0 ? new List<string> { "Yönetici özeti için önce bir tarama yapın.", "Tarama bittikten sonra bu sekme, teknik terim kullanmadan tek sayfalık bir özet üretir." } : DurumAnalizi.OzetSatirlari(_hedef, _tarih, _puan, _b, onceki);
        // Ana kap.
        var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Zemin }; t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        // Metin kutusu.
        t.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Text = string.Join("\r\n\r\n", _ozet), Dock = DockStyle.Fill, BackColor = Panel, ForeColor = Yazi, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular) }, 0, 0);
        // Kaydet düğmesi.
        var kaydet = Dugme("⇩ HTML Olarak Kaydet", 210); kaydet.Click += (_, _) => { if (_b.Count == 0) { Bilgi("Önce bir tarama yapın."); return; } Kaydet("Yonetici_Ozeti_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html", "HTML dosyası (*.html)|*.html", y => File.WriteAllText(y, DurumAnalizi.OzetHtml(_ozet), new UTF8Encoding(false))); };
        // Düğme paneli.
        var d = new FlowLayoutPanel { Dock = DockStyle.Fill }; d.Controls.Add(kaydet);
        // Yönetim Kurulu Risk Özeti: tek sayfalık A4 rapor (tarayıcıda Yazdır > PDF).
        var yk = Dugme("⇩ Yönetim Kurulu Risk Özeti (1 Sayfa)", 330); yk.Click += (_, _) => { if (_b.Count == 0) { Bilgi("Önce bir tarama yapın."); return; } Kaydet("Yonetim_Kurulu_Risk_Ozeti_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html", "HTML dosyası (*.html)|*.html", y => File.WriteAllText(y, YonetimKuruluRaporu.Html(_hedef, _tarih, _puan, _b, _gecmis), new UTF8Encoding(false))); }; d.Controls.Add(yk);
        // Etik Açık Bildirim Raporu: yalnızca dış ağ pasif gözleminden sonra (site sahibine gönderilecek yapıcı rapor; Markdown / HTML / PDF özeti).
        var eb = Dugme("⇩ Etik Açık Bildirim Raporu", 250); eb.Click += (_, _) =>
        {
            // Pasif gözlem yoksa uyar.
            if (PasifDeposu.Gozlem == null) { Bilgi("Etik Açık Bildirim Raporu, dış ağdaki (internetteki) bir hedefin pasif gözleminden üretilir.\r\n\r\nÖnce ana ekranda hedef olarak bir alan adı yazıp taramayı başlatın."); return; }
            // Gönderen adı: oturumdaki kullanıcı (iletişim bilgisini kullanıcı e-postaya kendisi ekler).
            var g = PasifDeposu.Gozlem; var bl = PasifDeposu.Bulgular; string ad = ServisBaglami.Oturum.Aktif?.Ad ?? "";
            Kaydet("Etik_Bildirim_" + System.Text.RegularExpressions.Regex.Replace(g.Host, @"[^A-Za-z0-9.\-]", "_") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md", "Markdown e-posta metni (*.md)|*.md|HTML dosyası (*.html)|*.html|PDF özeti (*.pdf)|*.pdf", y =>
            {
                // Uzantıya göre biçim.
                if (y.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) File.WriteAllText(y, EtikBildirim.Html(g, bl, ad, ""), new UTF8Encoding(false));
                else if (y.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) EtikBildirim.PdfYaz(y, g, bl);
                else File.WriteAllText(y, EtikBildirim.Markdown(g, bl, ad, ""), new UTF8Encoding(false));
            });
        }; d.Controls.Add(eb); t.Controls.Add(d, 0, 1);
        // Geri ver.
        return t;
    }

    // 4) Karşılaştırma sekmesi.
    private Control KarsilastirKur()
    {
        // Ana kap.
        var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, BackColor = Zemin }; t.RowStyles.Add(new RowStyle(SizeType.Absolute, 84)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        // Seçim satırı: iki liste + düğme.
        var sec = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true }; var kar = Dugme("⇄ Karşılaştır", 150);
        // Listeleri doldur.
        foreach (var k in _gecmis) { string ad = $"{k.Tarih:dd.MM.yyyy HH:mm} · {k.Hedef} · puan {k.Puan}"; _cEski.Items.Add(ad); _cYeni.Items.Add(ad); }
        // Varsayılan: yeni = en son, eski = bir önceki.
        if (_gecmis.Count > 0) _cYeni.SelectedIndex = 0; if (_gecmis.Count > 1) _cEski.SelectedIndex = 1;
        // Etiketler ve kontroller.
        sec.Controls.Add(new Label { Text = "Eski tarama:", AutoSize = true, Margin = new Padding(0, 8, 4, 0), ForeColor = Soluk }); sec.Controls.Add(_cEski); sec.Controls.Add(new Label { Text = "Yeni tarama:", AutoSize = true, Margin = new Padding(10, 8, 4, 0), ForeColor = Soluk }); sec.Controls.Add(_cYeni); sec.Controls.Add(kar);
        // Tıklayınca karşılaştır.
        kar.Click += (_, _) => Karsilastir();
        // Başlangıç mesajı.
        _kSonuc.Text = _gecmis.Count < 2 ? "Karşılaştırma için en az iki kayıtlı tarama gerekir. (Şu an " + _gecmis.Count + " kayıt var.) Her tarama bitince otomatik kaydedilir." : "İki tarama seçip \"Karşılaştır\"a basın.";
        // Kaydet düğmesi.
        var kaydet = Dugme("⇩ HTML Olarak Kaydet", 210); kaydet.Click += (_, _) => { if (_sonK is not { } s) { Bilgi("Önce \"Karşılaştır\"a basın."); return; } Kaydet("Karsilastirma_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html", "HTML dosyası (*.html)|*.html", y => File.WriteAllText(y, DurumAnalizi.KarsilastirmaHtml(s.Yeni, s.Eski, s.K), new UTF8Encoding(false))); };
        // Alt panel.
        var d = new FlowLayoutPanel { Dock = DockStyle.Fill }; d.Controls.Add(kaydet);
        // Yerleştir.
        t.Controls.Add(sec, 0, 0); t.Controls.Add(_kSonuc, 0, 1); t.Controls.Add(d, 0, 2);
        // Geri ver.
        return t;
    }

    // Seçili iki taramayı karşılaştırıp sonucu yazar.
    private void Karsilastir()
    {
        // İkisi de seçili olmalı.
        if (_cEski.SelectedIndex < 0 || _cYeni.SelectedIndex < 0) { Bilgi("İki taramayı da seçin."); return; }
        // Aynı kayıt seçildiyse uyar.
        if (_cEski.SelectedIndex == _cYeni.SelectedIndex) { Bilgi("Aynı taramayı seçtiniz. Farklı iki kayıt seçin."); return; }
        // Tarihe göre eski/yeni düzelt (kullanıcı ters seçmiş olabilir).
        var a = _gecmis[_cEski.SelectedIndex]; var b = _gecmis[_cYeni.SelectedIndex]; var eski = a.Tarih <= b.Tarih ? a : b; var yeni = a.Tarih <= b.Tarih ? b : a;
        // Karşılaştır.
        var k = GecmisDeposu.Karsilastir(yeni, eski); _sonK = (yeni, eski, k);
        // Sonuç yazısı.
        var sb = new StringBuilder(); sb.AppendLine(k.Ozet).AppendLine();
        // Hedefler farklıysa uyar.
        if (!string.Equals(yeni.Hedef, eski.Hedef, StringComparison.OrdinalIgnoreCase)) sb.AppendLine("UYARI: İki tarama farklı hedeflere ait; sonuçlar birebir kıyaslanamaz.").AppendLine();
        // Yeni sorunlar.
        sb.AppendLine($"YENİ ÇIKAN SORUNLAR ({k.Yeni.Count}):"); foreach (var x in k.Yeni) sb.AppendLine("  + " + x); if (k.Yeni.Count == 0) sb.AppendLine("  (yok)");
        // Kapanan sorunlar.
        sb.AppendLine().AppendLine($"KAPANAN SORUNLAR ({k.Kapanan.Count}):"); foreach (var x in k.Kapanan) sb.AppendLine("  - " + x); if (k.Kapanan.Count == 0) sb.AppendLine("  (yok)");
        // Ekrana yaz.
        _kSonuc.Text = sb.ToString();
    }

    // 5) SIEM sekmesi.
    private Control SiemKur()
    {
        // Ana kap.
        var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Zemin }; t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        // Açıklama.
        t.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, BackColor = Panel, ForeColor = Yazi, BorderStyle = BorderStyle.None, Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular), Text = "SIEM nedir? Şirketlerde tüm güvenlik olaylarını tek yerde toplayan sistemdir (Splunk, Wazuh, Elastic, QRadar gibi).\r\n\r\nBu sekme, ekrandaki tarama sonuçlarını o sistemlerin okuyabildiği iki biçimde DOSYAYA yazar:\r\n\r\n• CEF: her sonuç tek satır, endüstri standardı.\r\n• JSON Lines: her sonuç tek satırlık bir JSON (MITRE ATT&CK alanlarıyla).\r\n\r\nProgram dosyayı hiçbir yere göndermez; siz kaydedip kendi sisteminize elle veya kendi ajanınızla aktarırsınız.\r\n\r\nSorun olmayan (yeşil) sonuçlar da yazılır; \"kontrol çalıştırılamadı\" notları yazılmaz. Önem puanı: kritik 9, uyarı 6, dikkat 3, bilgi 0." }, 0, 0);
        // İki dışa aktarma düğmesi.
        var cef = Dugme("⇪ CEF Olarak Kaydet", 210); var json = Dugme("⇪ JSON Lines Olarak Kaydet", 240);
        // CEF kaydı.
        cef.Click += (_, _) => { if (_b.Count == 0) { Bilgi("Önce bir tarama yapın."); return; } Kaydet("Bulgular_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".cef", "CEF dosyası (*.cef)|*.cef|Metin (*.log)|*.log", y => Bilgi($"{SiemCikti.CefYaz(y, _b.ToList(), _hedef, _tarih)} olay yazıldı:\r\n{y}")); };
        // JSON kaydı.
        json.Click += (_, _) => { if (_b.Count == 0) { Bilgi("Önce bir tarama yapın."); return; } Kaydet("Bulgular_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl", "JSON Lines (*.jsonl)|*.jsonl", y => Bilgi($"{SiemCikti.JsonYaz(y, _b.ToList(), _hedef, _tarih)} olay yazıldı:\r\n{y}")); };
        // Düğme paneli.
        var d = new FlowLayoutPanel { Dock = DockStyle.Fill }; d.Controls.Add(cef); d.Controls.Add(json); t.Controls.Add(d, 0, 1);
        // Geri ver.
        return t;
    }

    // Bilgi kutusu.
    private static void Bilgi(string m) => TemaliMesaj.Goster(m, "Durum Panosu", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // Kaydetme penceresi açar; kullanıcı yol seçerse yazma işlemini çalıştırır (hata olursa kullanıcıya söyler).
    private void Kaydet(string onerilen, string filtre, Action<string> yaz)
    {
        // Kaydetme penceresi (varsayılan: Belgeler).
        using var f = new SaveFileDialog { FileName = onerilen, Filter = filtre, InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        // Vazgeçildiyse çık.
        if (f.ShowDialog(this) != DialogResult.OK) return;
        // Yazmayı dene.
        try { yaz(f.FileName); if (!f.FileName.EndsWith(".cef") && !f.FileName.EndsWith(".log") && !f.FileName.EndsWith(".jsonl")) Bilgi("Kaydedildi:\r\n" + f.FileName); }
        // Hata varsa söyle.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
