// Bu dosya "Güvenlik Araç Kutusu" penceresini çizer. Solda 8 araç düğmesi, sağda seçilen aracın sayfası vardır:
// Link Analizi, Tarayıcı Eklentileri, Kara Liste, Parola Sızıntısı, Dosya Bütünlüğü, Vaka Defteri, Güvenlik Duvarı Yedeği, Sistem ve Koruma.
// Sistemde değişiklik yapan her işlem (duvar geri yükleme, internete sorma, bildirim) açık onay ve gerekirse yönetici izni (UAC) ister.
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Araç Kutusu penceresi.
internal sealed class AracKutusuFormu : SayfaDenetimi
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Sayfalar ve sol düğmeler.
    private readonly List<Control> _sayfalar = new(); private readonly List<Button> _dugmeler = new();
    // Günlüğe yazma kancası.
    private readonly Action<string> _gunluk;

    // Pencereyi kurar.
    public AracKutusuFormu(Action<string> gunluk)
    {
        // Günlük.
        _gunluk = gunluk;
        // Pencere ayarları.
        Text = "Güvenlik Araç Kutusu"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1040, 640); MinimumSize = new Size(900, 560); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Sol menü ve içerik alanı.
        var sol = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 210, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar), Padding = new Padding(8, 10, 0, 0) }; var alan = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        // Sayfalar.
        var s = new (string Ad, Control C)[] { ("◈ Link Analizi", LinkSayfa()), ("◫ Tarayıcı Eklentileri", EklentiSayfa()), ("⊘ Kara Liste (IP)", KaraSayfa()), ("⚿ Parola Sızıntısı", ParolaSayfa()), ("✓ Parola Politikası", PolitikaSayfa()), ("▤ Dosya Bütünlüğü", FimSayfa()), ("# Özet (Hash) Hesapla", HashSayfa()), ("⌕ Dosya İnceleyici", DosyaSayfa()), ("▦ Filo Raporu", FiloSayfa()), ("✎ Vaka Defteri", VakaSayfa()), ("▦ Duvar Yedeği", DuvarSayfa()), ("✦ Sistem ve Koruma", SistemSayfa()) };
        // Her biri için düğme.
        for (int i = 0; i < s.Length; i++)
        {
            // Kopya sayı.
            int no = i; s[i].C.Dock = DockStyle.Fill; s[i].C.Visible = false; alan.Controls.Add(s[i].C); _sayfalar.Add(s[i].C);
            // Düğme.
            var d = new EfektliButon { Text = s[i].Ad, Size = new Size(194, 42), FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 0, 0, 4) }; d.Click += (_, _) => Sec(no); _dugmeler.Add(d); sol.Controls.Add(d);
        }
        // Dock sırası: önce içerik, sonra sol.
        Controls.Add(alan); Controls.Add(sol); Sec(0);
        // Açılışta metin kutuları seçili görünmesin.
        Shown += (_, _) => { foreach (var c in Tumu(this)) if (c is TextBox { ReadOnly: true } tb) tb.SelectionLength = 0; ActiveControl = null; };
    }

    // Altındaki tüm kontroller.
    private static IEnumerable<Control> Tumu(Control k) { foreach (Control c in k.Controls) { yield return c; foreach (var a in Tumu(c)) yield return a; } }

    // Sayfa seçer.
    private void Sec(int no)
    {
        // Güvenlik duvarı yedeği (8) ve sistem/koruma (9) sayfaları sistemi değiştirir: yalnızca Yönetici.
        if (no >= 10 && !Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)) { Bilgi(Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)); return; }
        // Hepsini ayarla.
        for (int i = 0; i < _sayfalar.Count; i++) { _sayfalar[i].Visible = i == no; _dugmeler[i].BackColor = i == no ? DesignTokens.Ozgun(TokenRol.AccentPrimary) : DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _dugmeler[i].ForeColor = i == no ? Color.White : Soluk; _dugmeler[i].FlatAppearance.BorderColor = i == no ? Mavi : DesignTokens.Ozgun(TokenRol.SurfaceSidebar); }
    }

    // ---- Küçük yapı taşları ----
    // Açıklama etiketi.
    private static Label Acik(string t, int boy = 60) => new() { Text = t, Dock = DockStyle.Top, Height = boy, ForeColor = Soluk, UseMnemonic = false };
    // Düğmelerin açıklayıcı ipuçları (üzerine gelince görünür). Metin, düğme yazısıyla eşleşir.
    private static readonly ToolTip DugmeIpucu = new() { AutoPopDelay = 12000, InitialDelay = 400, ReshowDelay = 200 };
    private static readonly Dictionary<string, string> DugmeIpuclari = new()
    {
        ["Analiz Et"] = "Yazdığınız linki açmadan, yalnızca yazısına bakarak şüpheli olup olmadığını değerlendirir.",
        ["Eklentileri Tara"] = "Tarayıcılarınızdaki eklentileri ve izinlerini okur; hiçbir şeyi kaldırmaz.",
        ["IP Kontrol Et"] = "Yazdığınız IP adresinin kara listenizde olup olmadığına bakar; internete çıkmaz.",
        ["Bağlantılarımı Tara"] = "Bu bilgisayarın şu an kurduğu bağlantıları kara listenizle karşılaştırır.",
        ["Listeyi Düzenle"] = "Kara liste dosyasını açar; her satıra bir IP yazabilirsiniz.",
        ["Sızıntıda Mı? (İnternete Sorar)"] = "Parolanın yalnızca ilk 5 harflik özetini sızıntı servisine sorar; parolanın kendisi bilgisayarınızdan çıkmaz.",
        ["Politikayı Kaydet"] = "Kurum parola kurallarını (uzunluk, sınıf sayısı, yasaklı sözcükler) kaydeder.",
        ["Temel Oluştur"] = "Seçtiğiniz klasördeki dosyaların özetlerini (fotoğrafını) temel olarak saklar.",
        ["Karşılaştır"] = "Temelle şimdiki durumu karşılaştırır; değişen, eklenen ve silinen dosyaları gösterir.",
        ["Hesapla"] = "Seçtiğiniz dosyanın özetini (hash) hesaplar.",
        ["Durdur"] = "Süren hesaplamayı durdurur.",
        ["Sonucu Kopyala"] = "Sonucu panoya kopyalar.",
        ["Gözat…"] = "Bilgisayarınızdan dosya ya da klasör seçersiniz.",
        ["İncele"] = "Dosyayı çalıştırmadan inceler: gerçek türü, imzası, şüpheli metinler. Dosya bilgisayarınızdan çıkmaz.",
        ["Raporu Kaydet"] = "İnceleme sonucunu dosya olarak kaydeder.",
        ["Kopyala"] = "Metni panoya kopyalar.",
        ["Rapor Oluştur"] = "Klasördeki tarama özetlerinden tek HTML ve CSV rapor üretir; özet dosyalarına dokunmaz.",
        ["Raporu Aç"] = "Oluşturulan raporu tarayıcıda açar.",
        ["Yeni Vaka"] = "Şüpheli bir olay için yeni bir kayıt açar.",
        ["Not Ekle"] = "Seçili vakaya tarihli bir not ekler.",
        ["Durumu Kaydet"] = "Seçili vakanın durumunu (Açık, İncelemede, Çözüldü, Kapalı) kaydeder.",
        ["Metne Aktar"] = "Seçili vakayı paylaşılabilir düz metin olarak dışa aktarır.",
        ["Yedek Al (Yönetici İzni)"] = "Güvenlik duvarı kurallarını dosyaya yedekler. Windows yönetici izni (UAC) ister.",
        ["Seçiliyi Geri Yükle"] = "Seçili yedeği geri yükler; mevcut kuralların yerine geçer. Yönetici izni ister ve onayınızı alır.",
        ["Ayar Dosyalarını Mühürle"] = "Ayar dosyalarının değişmediğini sonradan doğrulayabilmek için mühürler.",
        ["Mühürleri Doğrula"] = "Ayar dosyalarının mühürlendikten sonra değişip değişmediğini kontrol eder.",
        ["Tanılama Paketi Hazırla"] = "Destek için güvenli bir özet paketi hazırlar; parola ya da anahtar içermez.",
        ["Bildirim Kanalı (Webhook)…"] = "Kritik olaylarda telefonunuza ya da ekibe haber vermek için bildirim kanalı ayarlar (varsayılan kapalı).",
    };
    // Düğme.
    private static Button Dg(string t, int g = 170)
    {
        // Düğmeyi oluştur ve varsa ipucunu bağla.
        var b = new EfektliButon { Text = t, Size = new Size(g, 34), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 4, 8, 4) };
        if (DugmeIpuclari.TryGetValue(t, out var ip)) DugmeIpucu.SetToolTip(b, ip);
        return b;
    }
    // Salt okunur çıktı kutusu.
    private static TextBox Cikti() => new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, BackColor = Kutu, ForeColor = Yazi, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Font = FontProvider.Al(FontRol.Mono) };
    // Giriş kutusu.
    private static TextBox Giris(string ipucu = "") => new() { Dock = DockStyle.Top, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular), PlaceholderText = ipucu };
    // Giriş kutusunu verilen genişlikte tutan kap (kutular gereksiz uzamasın).
    private static Panel Kap(TextBox t, int genislik) { var p = new Panel { Dock = DockStyle.Top, Height = 38 }; t.Dock = DockStyle.None; t.Location = new Point(0, 2); t.Width = genislik; p.Controls.Add(t); return p; }
    // Düğme şeridi.
    private static FlowLayoutPanel Serit(params Control[] c) { var f = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, WrapContents = false }; f.Controls.AddRange(c); return f; }
    // Sayfa kabı: Dock sırası alttan yukarı eklenir.
    private static Panel Sayfa(params Control[] ustten) { var p = new Panel(); for (int i = ustten.Length - 1; i >= 0; i--) p.Controls.Add(ustten[i]); return p; }
    // Bilgi kutusu.
    private static void Bilgi(string m) => TemaliMesaj.Goster(m, "Araç Kutusu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    // Onay kutusu (varsayılan Hayır).
    private static bool Onay(string m, string b) => TemaliMesaj.Goster(m, b, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    // ---- 1) Link ----
    private Control LinkSayfa()
    {
        // Bileşenler.
        var g = Giris("Şüpheli linki buraya yapıştırın (linke GİTMEZ, yalnızca yazısına bakar)"); var c = Cikti(); var d = Dg("Analiz Et");
        // Tıklama.
        void Yap() { var r = LinkAnalizi.Analiz(g.Text); c.Text = $"SONUÇ: {r.Seviye}  (risk {r.Risk}/100)\r\n\r\n" + string.Join("\r\n\r\n", r.Nedenler.Select(x => "• " + x)); }
        d.Click += (_, _) => Yap(); g.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Yap(); e.SuppressKeyPress = true; } };
        // Sayfa.
        return Sayfa(Acik("Phishing (oltalama) analizi: Size gelen bir SMS/e-posta linkinin şüpheli olup olmadığını yalnızca yazısına bakarak değerlendirir. Linke tıklamaz, internete çıkmaz."), Kap(g, 640), Serit(d), c);
    }

    // ---- 2) Eklentiler ----
    private Control EklentiSayfa()
    {
        // Tablo.
        var t = Tablo(("Tr", "Tarayıcı", 9), ("Ad", "Eklenti", 24), ("Sv", "Sürüm", 8), ("R", "Risk", 7), ("Iz", "Dikkat çeken izinler", 52)); var d = Dg("Eklentileri Tara"); var ozet = Acik("", 24);
        // Tıklama.
        d.Click += (_, _) => { t.Rows.Clear(); var l = EklentiDenetimi.Tara(); foreach (var e in l) { int i = t.Rows.Add(e.Tarayici, e.Ad, e.Surum, e.Riskler.Count, e.Riskler.Count == 0 ? "-" : string.Join(" | ", e.Riskler)); if (e.Riskler.Count >= 4) t.Rows[i].Cells[3].Style.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); else if (e.Riskler.Count > 0) t.Rows[i].Cells[3].Style.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); } ozet.Text = l.Count == 0 ? "Chrome/Edge/Brave eklentisi bulunamadı." : $"{l.Count} eklenti bulundu; en çok izin isteyenler üstte. Tanımadığınız veya kullanmadığınız eklentileri tarayıcınızdan kaldırın."; };
        // Sayfa.
        return Sayfa(Acik("Tarayıcı eklentileri çok yetkilidir: kötü bir eklenti şifrelerinizi ve gezinti geçmişinizi görebilir. Bu araç yalnızca OKUR, hiçbir şeyi kaldırmaz.", 44), Serit(d), ozet, t);
    }

    // ---- 3) Kara liste ----
    private Control KaraSayfa()
    {
        // Bileşenler.
        var g = Giris("Kontrol edilecek IP (ör. 203.0.113.7)"); var c = Cikti(); var k = Dg("IP Kontrol Et"); var b = Dg("Bağlantılarımı Tara", 190); var e = Dg("Listeyi Düzenle", 160);
        // IP kontrol.
        k.Click += (_, _) => c.Text = KaraListe.Kontrol(g.Text);
        // Bağlantı taraması.
        b.Click += (_, _) => { var l = KaraListe.BaglantilariTara(); c.Text = l.Count == 0 ? $"Şu an kara listenizdeki ({KaraListe.Oku().Count} kayıt) hiçbir adresle bağlantı yok." : "DİKKAT: Kara listedeki adreslerle bağlantı var!\r\n\r\n" + string.Join("\r\n", l); if (l.Count > 0) _gunluk($"KRİTİK -> Kara listedeki {l.Count} adresle bağlantı bulundu."); };
        // Listeyi Not Defteri'nde aç.
        e.Click += (_, _) => { try { Directory.CreateDirectory(BulutKlasor.Kok); if (!File.Exists(KaraListe.Yol)) File.WriteAllText(KaraListe.Yol, "# Kara liste: her satıra bir IP veya ağ (CIDR) yazın. # ile başlayan satırlar açıklamadır.\r\n# Örnek:\r\n# 203.0.113.7\r\n# 198.51.100.0/24\r\n", new UTF8Encoding(false)); Process.Start(new ProcessStartInfo("notepad.exe", "\"" + KaraListe.Yol + "\"") { UseShellExecute = true }); } catch (Exception x) { Bilgi(x.Message); } };
        // Sayfa.
        return Sayfa(Acik("IP itibarı (yerel): Kendi tuttuğunuz kara listeye bakar; internete sormaz. Listeye zararlı olduğunu bildiğiniz IP veya ağları siz eklersiniz (kaynak: kendi araştırmanız, kurumunuzun uyarıları).", 56), Kap(g, 300), Serit(k, b, e), c);
    }

    // ---- 4) Parola ----
    private Control ParolaSayfa()
    {
        // Parola kutusu (yıldızlı).
        var g = Giris("Kontrol edilecek parola"); g.UseSystemPasswordChar = true; var c = Cikti(); var d = Dg("Sızıntıda Mı? (İnternete Sorar)", 250);
        // Tıklama.
        d.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Boşsa uyar.
            if (g.Text.Length == 0) { Bilgi("Önce bir parola yazın."); return; }
            // Onay: ne gönderileceğini açıkça söyle.
            if (!Onay("Bu işlem İNTERNETE bağlanır (api.pwnedpasswords.com).\r\n\r\n• Parolanın kendisi ASLA gönderilmez.\r\n• Yalnızca parolanın SHA-1 özetinin ilk 5 harfi gönderilir (k-anonimlik); gerisi bilgisayarınızda karşılaştırılır.\r\n• Parola kaydedilmez, günlüğe yazılmaz.\r\n\r\nDevam edilsin mi?", "İnternete sorulsun mu?")) return;
            // Düğmeyi kilitle.
            d.Enabled = false; c.Text = "Soruluyor...";
            // Sor.
            try { long n = await ParolaSizinti.SorAsync(g.Text); c.Text = n > 0 ? $"BU PAROLA {n:N0} KEZ SIZMIŞ!\r\n\r\nBu parolayı hiçbir yerde kullanmayın; kullandığınız her hesapta değiştirin ve her hesap için farklı parola seçin (bir parola yöneticisi kullanın)." : "Bu parola bilinen sızıntı listesinde bulunmadı.\r\n\r\nBu, parolanın güçlü olduğunun garantisi değildir: uzun (14+ karakter), tahmin edilemez ve her hesap için farklı olmalı."; }
            // Ağ yoksa.
            catch (Exception x) { c.Text = "Sorulamadı (internet bağlantısı var mı?): " + x.Message; }
            // Kutuyu temizle ve düğmeyi aç.
            finally { g.Clear(); d.Enabled = true; }
        }, "AracKutusu");
        // Sayfa.
        return Sayfa(Acik("Parolanız veri sızıntılarında geçmiş mi? k-anonimlik yöntemiyle kontrol edilir: parola bilgisayarınızdan çıkmaz, yalnızca özetinin ilk 5 harfi sorulur. Her kontrolden önce onayınız istenir.", 56), Kap(g, 340), Serit(d), c);
    }

    // ---- 4b) Parola politikası (tamamen yerel; internete çıkmaz) ----
    private Control PolitikaSayfa()
    {
        // Bileşenler.
        var pol = ParolaPolitikasi.Yukle(); var g = Giris("Ölçülecek parola (bu bilgisayardan çıkmaz)"); g.UseSystemPasswordChar = true; var goster = new CheckBox { Text = "Göster", AutoSize = true, ForeColor = Soluk, Margin = new Padding(8, 8, 0, 0) };
        var seviye = new Label { Dock = DockStyle.Top, Height = 36, Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold), ForeColor = Soluk, Text = "Parola yazın", UseMnemonic = false }; var c = Cikti();
        var uz = new NumericUpDown { Minimum = ParolaPolitikasi.EnAzUzunluk, Maximum = ParolaPolitikasi.EnCokUzunluk, Value = pol.MinUzunluk, Width = 60, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White }; var sn = new NumericUpDown { Minimum = 1, Maximum = 4, Value = pol.MinSinif, Width = 50, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White };
        var yas = new TextBox { Width = 230, Text = string.Join(", ", pol.YasakliListe), BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "şirket adı, kullanıcı adı..." }; var kaydet = Dg("Politikayı Kaydet", 160);
        // Etiket yardımcısı.
        Label E(string t) => new() { Text = t, AutoSize = true, ForeColor = Soluk, Margin = new Padding(0, 9, 6, 0), UseMnemonic = false };
        // Anlık değerlendirme (parola hiçbir yere yazılmaz).
        void Olc()
        {
            var p = new ParolaPolitika((int)uz.Value, (int)sn.Value, yas.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()); var s = ParolaPolitikasi.Degerlendir(g.Text, p, ServisBaglami.Oturum.Aktif?.Ad);
            if (g.Text.Length == 0) { seviye.Text = "Parola yazın"; seviye.ForeColor = Soluk; c.Text = $"Geçerli politika: en az {p.MinUzunluk} karakter, 4 sınıftan (küçük, büyük, rakam, özel) en az {p.MinSinif}'i, bilinen zayıf kalıp ve ardışık/tekrarlı karakter yok."; return; }
            seviye.Text = $"{s.Seviye}  ({s.Puan}/100)" + (s.Uygun ? "  ✓ politikaya uygun" : "  ✗ politikaya uymuyor"); seviye.ForeColor = s.Puan < 30 ? DesignTokens.Ozgun(TokenRol.StatusDanger) : s.Puan < 50 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : s.Puan < 70 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess);
            var sb = new StringBuilder(); sb.AppendLine($"Tahmini güç: {s.EntropiBit} bit").AppendLine(); if (s.Ihlaller.Count > 0) { sb.AppendLine("Uymayan noktalar:"); foreach (var i in s.Ihlaller) sb.AppendLine("  ✗ " + i); sb.AppendLine(); } if (s.Oneriler.Count > 0) { sb.AppendLine("Öneriler:"); foreach (var o in s.Oneriler) sb.AppendLine("  • " + o); } else sb.AppendLine("Bu parola politikaya uygun görünüyor.");
            c.Text = sb.ToString();
        }
        g.TextChanged += (_, _) => Olc(); uz.ValueChanged += (_, _) => Olc(); sn.ValueChanged += (_, _) => Olc(); goster.CheckedChanged += (_, _) => g.UseSystemPasswordChar = !goster.Checked;
        kaydet.Click += (_, _) => { try { ParolaPolitikasi.Kaydet(new ParolaPolitika((int)uz.Value, (int)sn.Value, yas.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList())); _gunluk("PAROLA POLİTİKASI -> Kurum politikası güncellendi."); Bilgi("Politika kaydedildi."); Olc(); } catch (Exception x) { Bilgi("Kaydedilemedi: " + x.Message); } };
        Disposed += (_, _) => g.Clear(); Olc();
        // Ayar şeridi.
        var ayar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, WrapContents = false }; ayar.Controls.AddRange(new Control[] { E("En az uzunluk"), uz, E("  Sınıf (1-4)"), sn, E("  Yasaklı sözcükler"), yas, kaydet });
        var kutu = Kap(g, 340); kutu.Controls.Add(goster); goster.Location = new Point(350, 4);
        return Sayfa(Acik("Bir parolanın kurumsal kurallara (uzunluk, karakter çeşitliliği, bilinen zayıf kalıplar, kurum/kullanıcı adı) uyup uymadığını ölçer. Tamamen bu bilgisayarda çalışır: parola internete gönderilmez, dosyaya veya günlüğe yazılmaz. Politika ayarları (parola değil) kaydedilir.", 56), kutu, seviye, ayar, c);
    }

    // ---- 5d) Filo raporu (toplu özet) ----
    private Control FiloSayfa()
    {
        // Bileşenler.
        var yol = Giris("Özet klasörü (her makinenin \"--tara\" ile ürettiği .json dosyaları)"); var gz = Dg("Gözat…", 110); var uret = Dg("Rapor Oluştur", 150); var ac = Dg("Raporu Aç", 120); ac.Enabled = false; var c = Cikti(); string? son = null;
        gz.Click += (_, _) => { using var f = new FolderBrowserDialog { Description = "Tarama özetlerinin bulunduğu klasör" }; if (f.ShowDialog(this) == DialogResult.OK) yol.Text = f.SelectedPath; };
        // Rapor üretimi (yalnızca okur; rapor dosyalarını aynı klasöre yazar).
        uret.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Klasör var mı?
            string d = yol.Text.Trim().Trim('"'); if (!Directory.Exists(d)) { Bilgi("Geçerli bir klasör seçin."); return; }
            // Çalışırken kilitle.
            uret.Enabled = false; ac.Enabled = false; c.Text = "Rapor hazırlanıyor...";
            try
            {
                // Arka planda çalıştır.
                var (kod, mesaj) = await Task.Run(() => FiloRaporu.Calistir(d, null));
                // Sonuç.
                if (kod == 0) { son = mesaj.Split("  (")[0]; c.Text = "Rapor yazıldı:\r\n" + mesaj + "\r\n\r\nAynı klasörde .csv özeti de var. Raporu açmak için \"Raporu Aç\"a basın."; ac.Enabled = true; _gunluk("FİLO RAPORU -> " + Path.GetFileName(son)); }
                else c.Text = mesaj;
            }
            catch (Exception x) { c.Text = "Rapor üretilemedi: " + x.Message; }
            finally { uret.Enabled = true; }
        }, "AracKutusu");
        // Raporu varsayılan tarayıcıda aç (yalnızca az önce ürettiğimiz .html dosyası).
        ac.Click += (_, _) => { if (son != null && File.Exists(son)) try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(son) { UseShellExecute = true }); } catch (Exception x) { Bilgi(x.Message); } };
        return Sayfa(Acik("Birden çok bilgisayarın tarama özetini tek raporda toplar: makine başına puan ve değişim, en sık sorunlar, KVKK/ISO özeti, eski taramalar. Her bilgisayarda \"BulutSoftSecuritySuite.exe --tara --cikti <paylaşılan klasör>\" çalıştırın, sonra burada o klasörü seçin. Hiçbir veri bilgisayardan çıkmaz.", 70), Kap(yol, 560), Serit(gz, uret, ac), c);
    }

    // ---- 5c) Dosya adli inceleyici ----
    private Control DosyaSayfa()
    {
        // Bileşenler.
        var yol = Giris("Şüpheli dosyanın yolu (dosya ÇALIŞTIRILMAZ, yalnızca okunur)"); var gz = Dg("Gözat…", 110); var incele = Dg("İncele", 120); var kaydet = Dg("Raporu Kaydet", 150); var kopya = Dg("Kopyala", 110); var c = Cikti(); DosyaRaporu? son = null;
        gz.Click += (_, _) => { using var f = new OpenFileDialog { Title = "İncelenecek dosya" }; if (f.ShowDialog(this) == DialogResult.OK) yol.Text = f.FileName; };
        // İnceleme.
        incele.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Dosya var mı?
            string d = yol.Text.Trim().Trim('"'); if (!File.Exists(d)) { Bilgi("Geçerli bir dosya seçin."); return; }
            // Çalışırken düğmeyi kilitle.
            incele.Enabled = false; c.Text = "İnceleniyor...";
            try { son = await DosyaInceleyici.InceleAsync(d); c.Text = DosyaInceleyici.Metin(son).Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"); _gunluk($"DOSYA İNCELEME -> {son.Ad}: {son.Seviye} ({son.Puan}/100)"); }
            catch (Exception x) { c.Text = "İncelenemedi: " + x.Message; son = null; }
            finally { incele.Enabled = true; }
        }, "AracKutusu");
        // Dosyaya kaydet.
        kaydet.Click += (_, _) => { if (son == null) { Bilgi("Önce bir dosyayı inceleyin."); return; } using var f = new SaveFileDialog { FileName = "DosyaRaporu_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt", Filter = "Metin (*.txt)|*.txt" }; if (f.ShowDialog(this) == DialogResult.OK) try { File.WriteAllText(f.FileName, DosyaInceleyici.Metin(son), new UTF8Encoding(false)); } catch (Exception x) { Bilgi(x.Message); } };
        kopya.Click += (_, _) => { if (c.Text.Length > 0) try { Clipboard.SetText(c.Text); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "AracKutusu.DosyaSayfa"); } };
        return Sayfa(Acik("Şüpheli bir dosyayı çalıştırmadan inceler: gerçek türü (uzantıya aldanmaz), dijital imzası, zaman damgaları, internetten gelip gelmediği, SHA-256 özeti ve içindeki şüpheli ifadeler. Sonuç kesin virüs hükmü değil, risk değerlendirmesidir. Dosya bu bilgisayardan çıkmaz.", 56), Kap(yol, 560), Serit(gz, incele, kaydet, kopya), c);
    }

    // ---- 5b) Özet (hash) hesaplama ve doğrulama ----
    private Control HashSayfa()
    {
        // Bileşenler.
        var yol = Giris("Dosya yolu (ör. C:\\Indirilenler\\kurulum.iso)"); var gz = Dg("Gözat…", 110); var hesap = Dg("Hesapla", 120); var iptal = Dg("Durdur", 100); iptal.Enabled = false; var c = Cikti(); var ilerleme = new Label { Dock = DockStyle.Top, Height = 22, ForeColor = Soluk, UseMnemonic = false };
        var md5 = new CheckBox { Text = "MD5", AutoSize = true, ForeColor = Soluk }; var s1 = new CheckBox { Text = "SHA-1", AutoSize = true, ForeColor = Soluk }; var s256 = new CheckBox { Text = "SHA-256", AutoSize = true, Checked = true, ForeColor = Yazi }; var s512 = new CheckBox { Text = "SHA-512", AutoSize = true, ForeColor = Soluk };
        var beklenen = Giris("Üreticinin yayınladığı özet (yapıştırın; sha256sum çıktısı da olur)"); var karsi = Dg("Karşılaştır", 140); var kopya = Dg("Sonucu Kopyala", 160); Dictionary<HashTuru, string>? son = null; CancellationTokenSource? cts = null;
        gz.Click += (_, _) => { using var f = new OpenFileDialog { Title = "Özeti hesaplanacak dosya" }; if (f.ShowDialog(this) == DialogResult.OK) yol.Text = f.FileName; };
        // Seçili türler.
        List<HashTuru> Turler() { var l = new List<HashTuru>(); if (md5.Checked) l.Add(HashTuru.MD5); if (s1.Checked) l.Add(HashTuru.SHA1); if (s256.Checked) l.Add(HashTuru.SHA256); if (s512.Checked) l.Add(HashTuru.SHA512); return l; }
        // Karşılaştırma metni.
        string Karsilastirma() { if (son == null || string.IsNullOrWhiteSpace(beklenen.Text)) return ""; var (e, t, m) = HashAraci.Karsilastir(son, beklenen.Text); return "\r\n\r\n" + (e == true ? "✓ " : e == false ? "✗ " : "• ") + m; }
        // Çıktıyı yaz.
        void Yaz(string dosya) { var sb = new StringBuilder($"Dosya: {dosya}\r\nBoyut: {new FileInfo(dosya).Length:N0} bayt\r\n\r\n"); foreach (var (t, h) in son!) sb.AppendLine(HashAraci.Satir(t, h, Path.GetFileName(dosya))); sb.AppendLine(); foreach (var t in son.Keys) if (t is HashTuru.MD5 or HashTuru.SHA1) sb.AppendLine(HashAraci.Not(t)); c.Text = sb.ToString() + Karsilastirma(); }
        hesap.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            var tr = Turler(); if (tr.Count == 0) { Bilgi("En az bir özet türü seçin."); return; } string d = yol.Text.Trim().Trim('"'); if (!File.Exists(d)) { Bilgi("Geçerli bir dosya seçin."); return; }
            hesap.Enabled = false; iptal.Enabled = true; cts = new(); c.Text = "Hesaplanıyor..."; var p = new Progress<int>(y => ilerleme.Text = $"%{y}");
            try { son = await HashAraci.DosyaOzetleriAsync(d, tr, p, cts.Token); Yaz(d); ilerleme.Text = "Bitti."; _gunluk($"ÖZET -> {Path.GetFileName(d)}: " + string.Join(", ", son.Keys)); }
            catch (OperationCanceledException) { c.Text = "Durduruldu."; ilerleme.Text = ""; } catch (Exception x) { c.Text = "Hesaplanamadı: " + x.Message; ilerleme.Text = ""; son = null; }
            finally { hesap.Enabled = true; iptal.Enabled = false; }
        }, "AracKutusu");
        iptal.Click += (_, _) => cts?.Cancel();
        karsi.Click += (_, _) => { if (son == null) { Bilgi("Önce \"Hesapla\" ile dosyanın özetini hesaplayın."); return; } if (string.IsNullOrWhiteSpace(beklenen.Text)) { Bilgi("Karşılaştırılacak özeti yapıştırın."); return; } Yaz(yol.Text.Trim().Trim('"')); };
        kopya.Click += (_, _) => { if (c.Text.Length > 0) try { Clipboard.SetText(c.Text); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "AracKutusu.HashSayfa"); } };
        var turSerit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false }; turSerit.Controls.AddRange(new Control[] { new Label { Text = "Özet türü:", AutoSize = true, ForeColor = Soluk, Margin = new Padding(0, 6, 8, 0) }, md5, s1, s256, s512 });
        return Sayfa(Acik("Bir dosyanın MD5 / SHA-1 / SHA-256 / SHA-512 özetini hesaplar ve üreticinin yayınladığı özetle karşılaştırır: indirdiğiniz dosya bozulmuş veya değiştirilmiş mi? Dosya bu bilgisayardan çıkmaz. Güvenlik için SHA-256 önerilir; MD5 ve SHA-1 yalnızca kazara bozulmayı gösterir.", 56), Kap(yol, 560), Serit(gz, hesap, iptal), turSerit, Kap(beklenen, 560), Serit(karsi, kopya), ilerleme, c);
    }

    // ---- 5) Dosya bütünlüğü ----
    private Control FimSayfa()
    {
        // Bileşenler.
        var g = Giris("İzlenecek klasör (ör. C:\\Belgelerim\\Onemli)"); var c = Cikti(); var gz = Dg("Gözat…", 110); var t = Dg("Temel Oluştur", 150); var k = Dg("Karşılaştır", 150);
        // Gözat.
        gz.Click += (_, _) => { using var f = new FolderBrowserDialog(); if (f.ShowDialog(this) == DialogResult.OK) g.Text = f.SelectedPath; };
        // Klasör doğrula.
        bool Gecerli() { if (!Directory.Exists(g.Text.Trim())) { Bilgi("Geçerli bir klasör seçin."); return false; } return true; }
        // Temel.
        t.Click += (_, _) => GuvenliCalistir.Baslat(async () => { if (!Gecerli()) return; t.Enabled = false; c.Text = "Dosyalar özetleniyor..."; try { int n = await Task.Run(() => DosyaButunlugu.TemelOlustur(g.Text.Trim())); c.Text = $"Temel kaydedildi: {n} dosya.\r\nDosyalar değişirse \"Karşılaştır\" ile görürsünüz.\r\n(Çok büyük klasörler: en fazla {DosyaButunlugu.EnFazlaDosya} dosya, 100 MB'tan büyük dosyalar atlanır.)"; _gunluk($"DOSYA BÜTÜNLÜĞÜ -> Temel oluşturuldu: {n} dosya."); } catch (Exception x) { c.Text = x.Message; } finally { t.Enabled = true; } }, "AracKutusu");
        // Karşılaştır.
        k.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Geçersizse çık.
            if (!Gecerli()) return; k.Enabled = false; c.Text = "Karşılaştırılıyor...";
            // Hesapla.
            try
            {
                // Fark.
                var f = await Task.Run(() => DosyaButunlugu.Karsilastir(g.Text.Trim())); if (f == null) { c.Text = "Bu klasör için temel yok. Önce \"Temel Oluştur\"a basın."; return; }
                // Metin.
                var sb = new StringBuilder(); sb.AppendLine($"{f.Toplam} dosya denetlendi." + (f.Kesildi ? " (Sınıra ulaşıldı; kalanlar denetlenmedi.)" : "")).AppendLine();
                // Bölümler.
                void B(string ad, List<string> l) { sb.AppendLine($"{ad} ({l.Count}):"); foreach (var x in l.Take(200)) sb.AppendLine("  " + x); if (l.Count == 0) sb.AppendLine("  (yok)"); sb.AppendLine(); }
                B("DEĞİŞEN dosyalar", f.Degisen); B("YENİ dosyalar", f.Yeni); B("SİLİNEN dosyalar", f.Silinen);
                // Yazı.
                c.Text = sb.ToString(); if (f.Degisen.Count + f.Yeni.Count + f.Silinen.Count > 0) _gunluk($"KRİTİK -> Dosya bütünlüğü: {f.Degisen.Count} değişen, {f.Yeni.Count} yeni, {f.Silinen.Count} silinen dosya ({g.Text.Trim()}).");
            }
            // Hata.
            catch (Exception x) { c.Text = x.Message; }
            // Düğmeyi aç.
            finally { k.Enabled = true; }
        }, "AracKutusu");
        // Sayfa.
        return Sayfa(Acik("Dosya Bütünlüğü İzleme (FIM): Önemli bir klasörün \"fotoğrafını\" (SHA-256 özetleri) çeker; sonra izinsiz değişen, eklenen veya silinen dosyaları gösterir. Yalnızca okur.", 50), Kap(g, 560), Serit(gz, t, k), c);
    }

    // ---- 6) Vaka defteri ----
    private Control VakaSayfa()
    {
        // Veri ve bileşenler.
        var vakalar = VakaDeposu.Yukle(); var liste = new ListBox { Dock = DockStyle.Left, Width = 300, BackColor = Kutu, ForeColor = Yazi, BorderStyle = BorderStyle.None, IntegralHeight = false }; var detay = Cikti(); var yeniNot = Giris("Yeni not yazın"); var durum = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Margin = new Padding(0, 8, 8, 0) }; durum.Items.AddRange(VakaDeposu.Durumlar);
        // Listeyi doldur.
        void Doldur(string? sec = null) { liste.Items.Clear(); foreach (var v in vakalar.OrderByDescending(x => x.Acilis)) liste.Items.Add($"{v.Acilis:dd.MM HH:mm} · {v.Durum} · {v.Baslik}"); var ix = sec == null ? -1 : vakalar.OrderByDescending(x => x.Acilis).ToList().FindIndex(x => x.Id == sec); if (ix >= 0) liste.SelectedIndex = ix; else if (liste.Items.Count > 0) liste.SelectedIndex = 0; }
        // Seçili vaka.
        Vaka? Secili() => liste.SelectedIndex < 0 ? null : vakalar.OrderByDescending(x => x.Acilis).ElementAt(liste.SelectedIndex);
        // Seçim değişince ayrıntı.
        liste.SelectedIndexChanged += (_, _) => { var v = Secili(); detay.Text = v == null ? "Vaka yok. \"Yeni Vaka\" ile başlayın." : VakaDeposu.Metin(v); if (v != null) durum.SelectedItem = v.Durum; };
        // Yeni vaka.
        var yeni = Dg("Yeni Vaka", 120); yeni.Click += (_, _) => { string? b = Soru("Yeni vaka", "Vaka başlığı (ör. \"Şüpheli USB cihazı\"):"); if (string.IsNullOrWhiteSpace(b)) return; var v = VakaDeposu.Yeni(b, ""); vakalar.Add(v); VakaDeposu.Kaydet(vakalar); Doldur(v.Id); };
        // Not ekle.
        var ekle = Dg("Not Ekle", 110); ekle.Click += (_, _) => { var v = Secili(); if (v == null || string.IsNullOrWhiteSpace(yeniNot.Text)) { Bilgi("Önce bir vaka seçin ve not yazın."); return; } v.Notlar.Add(new VakaNotu(DateTime.Now, yeniNot.Text.Trim())); yeniNot.Clear(); VakaDeposu.Kaydet(vakalar); Doldur(v.Id); };
        // Durum kaydet.
        var dk = Dg("Durumu Kaydet", 140); dk.Click += (_, _) => { var v = Secili(); if (v == null || durum.SelectedItem is not string d) return; int i = vakalar.IndexOf(v); vakalar[i] = v with { Durum = d, Notlar = v.Notlar.Append(new VakaNotu(DateTime.Now, "Durum: " + d)).ToList() }; VakaDeposu.Kaydet(vakalar); Doldur(v.Id); };
        // Dışa aktar.
        var dis = Dg("Metne Aktar", 130); dis.Click += (_, _) => { var v = Secili(); if (v == null) return; using var f = new SaveFileDialog { FileName = "Vaka_" + v.Acilis.ToString("yyyyMMdd_HHmm") + ".txt", Filter = "Metin (*.txt)|*.txt" }; if (f.ShowDialog(this) == DialogResult.OK) try { File.WriteAllText(f.FileName, VakaDeposu.Metin(v), new UTF8Encoding(false)); } catch (Exception x) { Bilgi(x.Message); } };
        // Başlangıç.
        Doldur(); if (liste.Items.Count == 0) detay.Text = "Vaka yok. \"Yeni Vaka\" ile başlayın.";
        // Sağ taraf.
        var sag = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 0, 0) }; var sr = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, WrapContents = true }; sr.Controls.AddRange(new Control[] { yeni, ekle, durum, dk, dis }); sag.Controls.Add(detay); sag.Controls.Add(yeniNot); sag.Controls.Add(sr);
        // Sayfa.
        var p = new Panel(); p.Controls.Add(sag); p.Controls.Add(liste); p.Controls.Add(Acik("Vaka Defteri: şüpheli bir olayı (bulgu, USB, uyarı) takip etmek için durumu ve tarihli notları saklar. Yalnızca bilgisayarınızda tutulur.", 34)); return p;
    }

    // Küçük metin soru penceresi.
    private string? Soru(string baslik, string soru)
    {
        // Pencere.
        using var f = new Form { Text = baslik, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(440, 130), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Zemin, ForeColor = Yazi };
        TemaYonetici.KaydetVeUygula(f);   // elle üretilen pencere de temaya kaydedilir
        // Bileşenler.
        var l = new Label { Text = soru, Location = new Point(14, 12), Size = new Size(410, 22), UseMnemonic = false }; var t = new TextBox { Location = new Point(14, 40), Width = 410 }; var tm = new EfektliButon { Text = "Tamam", DialogResult = DialogResult.OK, Location = new Point(244, 84), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat }; var ip = new EfektliButon { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Location = new Point(340, 84), Size = new Size(84, 30), FlatStyle = FlatStyle.Flat };
        // Ekle.
        f.Controls.AddRange(new Control[] { l, t, tm, ip }); f.AcceptButton = tm; f.CancelButton = ip;
        // Sonuç.
        return f.ShowDialog(this) == DialogResult.OK ? t.Text : null;
    }

    // ---- 7) Güvenlik duvarı yedeği ----
    private Control DuvarSayfa()
    {
        // Yedek klasörü.
        string klasor = GuvenlikDuvari.YedekKlasoru; var liste = new ListBox { Dock = DockStyle.Fill, BackColor = Kutu, ForeColor = Yazi, BorderStyle = BorderStyle.None, Font = FontProvider.Al(FontRol.Mono), IntegralHeight = false }; var durum = Acik("", 30);
        // Listeyi yenile.
        void Yenile() { liste.Items.Clear(); if (Directory.Exists(klasor)) foreach (var f in Directory.GetFiles(klasor, "yedek_*.wfw").OrderByDescending(x => x)) liste.Items.Add(Path.GetFileName(f)); }
        // Yedek al.
        var al = Dg("Yedek Al (Yönetici İzni)", 230); al.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Onay.
            if (!Onay("Güvenlik duvarı kurallarınızın bir yedeği alınacak (dosyaya yazılır; kurallar değişmez).\r\n\r\nWindows yönetici izni (UAC) isteyecek. Devam edilsin mi?", "Yedek Al")) return;
            // Dosya adı.
            Directory.CreateDirectory(klasor); string ad = "yedek_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wfw";
            // Çalıştır.
            var (ok, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(GuvenlikDuvari.YedekAlZinciri(Path.Combine(klasor, ad))); Yenile();
            // Sonuç.
            durum.Text = ok && File.Exists(Path.Combine(klasor, ad)) ? "Yedek alındı: " + ad : "Yedek alınamadı. " + hata; if (ok) _gunluk("GÜVENLİK DUVARI -> Kural yedeği alındı: " + ad);
        }, "AracKutusu");
        // Geri yükle.
        var geri = Dg("Seçiliyi Geri Yükle", 200); geri.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Seçili ad ve güvenli ad kontrolü.
            if (liste.SelectedItem is not string ad || !Regex.IsMatch(ad, @"^yedek_\d{8}_\d{6}\.wfw$")) { Bilgi("Önce listeden bir yedek seçin."); return; }
            // Güvenlik: yedek klasörü yalnızca yönetici tarafından yazılabilir olmalı; aksi hâlde dosya başkası tarafından değiştirilmiş olabilir ve YÖNETİCİ İZNİYLE uygulanırdı.
            string tam = Path.Combine(klasor, ad); if (!GuvenlikDuvari.KlasorYalnizYoneticiYazar(klasor) || !File.Exists(tam)) { Bilgi("Güvenlik nedeniyle geri yükleme yapılmadı: yedek klasörü yalnızca yönetici tarafından yazılabilir değil ya da dosya yok." + Environment.NewLine + Environment.NewLine + "Klasör: " + klasor + Environment.NewLine + "Yeni bir yedek alın (yönetici izniyle klasör korumalı oluşturulur)."); return; }
            // Uyarı: tüm kuralları değiştirir; hangi dosyanın uygulanacağı (tarih, boyut, özet) açıkça gösterilir.
            var fi = new FileInfo(tam); string ozet = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tam)))[..16]; string nl = Environment.NewLine;
            if (!Onay($"DİKKAT: \"{ad}\" yedeği geri yüklenirse şu anki TÜM güvenlik duvarı kuralları bu yedekteki kurallarla DEĞİŞTİRİLİR.{nl}{nl}Uygulanacak dosya: {tam}{nl}Oluşturulma: {fi.CreationTime:dd.MM.yyyy HH:mm:ss} · Boyut: {fi.Length / 1024} KB · SHA-256: {ozet}…{nl}{nl}Geri yüklemeden önce güncel bir yedek almanız önerilir. Yönetici izni (UAC) istenecek.{nl}{nl}Geri yüklensin mi?", "Geri Yükle")) return;
            // Çalıştır.
            var (ok, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync($"netsh advfirewall import \"{tam}\""); int kod = GuvenlikDuvari.SonCikisKodu; durum.Text = ok && kod == 0 ? "Geri yükleme tamamlandı (çıkış kodu 0). Sonucu Windows Güvenlik Duvarı'ndan doğrulayın." : ok ? $"Komut çalıştı ama çıkış kodu {kod} (başarısız olmuş olabilir). Sonucu Windows Güvenlik Duvarı'ndan doğrulayın." : "Geri yüklenemedi. " + hata; if (ok && kod == 0) _gunluk("GÜVENLİK DUVARI -> Yedek geri yüklendi: " + ad); else if (ok) _gunluk($"GÜVENLİK DUVARI -> Yedek geri yükleme komutu çıkış kodu {kod} verdi: " + ad);
        }, "AracKutusu");
        // Başlangıç.
        Yenile();
        // Sayfa.
        return Sayfa(Acik("Güvenlik duvarı kurallarını dosyaya yedekler ve gerektiğinde geri yükler. Yedek alma ve geri yükleme Windows yönetici izni ister; geri yükleme mevcut kuralları DEĞİŞTİRİR, bu yüzden onay sorulur.", 56), Serit(al, geri), durum, liste);
    }

    // ---- 8) Sistem ve koruma ----
    private Control SistemSayfa()
    {
        // Çıktı.
        var c = Cikti(); c.Text = "Bu sayfa: kendini koruma (mühür), tanılama paketi ve bildirim kanalı içindir.\r\n";
        // Mühürle.
        var m = Dg("Ayar Dosyalarını Mühürle", 240); m.Click += (_, _) => { if (!Onay("Programın ayar/referans dosyaları (altın profil, kabul edilenler, cihaz envanteri, vakalar, kara liste...) şu anki hâliyle MÜHÜRLENECEK.\r\n\r\nSonra biri bu dosyaları değiştirirse \"Doğrula\" ile fark edilir. Sizin kendi değişiklikleriniz sonrası yeniden mühürlemeniz gerekir.\r\n\r\nDevam?", "Mühürle")) return; try { c.Text = $"{Muhur.Muhurle()} dosya mühürlendi."; } catch (Exception x) { c.Text = x.Message; } };
        // Doğrula.
        var d = Dg("Mühürleri Doğrula", 190); d.Click += (_, _) => { try { var l = Muhur.Dogrula(); c.Text = l.Count == 0 ? "Henüz mühür yok. Önce \"Ayar Dosyalarını Mühürle\"ye basın." : string.Join("\r\n", l.Select(x => $"{x.Dosya,-24} {x.Durum}")) + (l.Any(x => x.Durum is "DEĞİŞMİŞ" or "SİLİNMİŞ") ? "\r\n\r\nDİKKAT: Değişmiş/silinmiş dosya var. Siz yapmadıysanız bilgisayarınızı inceleyin." : "\r\n\r\nTüm mühürler sağlam."); if (l.Any(x => x.Durum is "DEĞİŞMİŞ" or "SİLİNMİŞ")) _gunluk("KRİTİK -> Kendini koruma: ayar dosyalarından biri izinsiz değişmiş/silinmiş."); } catch (Exception x) { c.Text = x.Message; } };
        // Tanılama paketi.
        var t = Dg("Tanılama Paketi Hazırla", 220); t.Click += (_, _) => { try { string y = TanilamaPaketi.Olustur(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); c.Text = "Hazır (Masaüstü): " + y + "\r\n\r\nİçerik: sürüm, işletim sistemi, ayar dosyalarının var/yok bilgisi ve son 200 olay satırı. Parola, API anahtarı ve tarama sonuçları KONMAZ. Göndermeden önce zip'i açıp içine bakabilirsiniz."; } catch (Exception x) { c.Text = x.Message; } };
        // Webhook.
        var w = Dg("Bildirim Kanalı (Webhook)…", 250); w.Click += (_, _) => GuvenliCalistir.Baslat(async () => {
            // Mevcut.
            var a = Webhook.Yukle(); string? adres = Soru("Bildirim kanalı", "https:// ile başlayan webhook adresi (boş bırakırsanız KAPATILIR):"); if (adres == null) return; adres = adres.Trim();
            // Kapatma.
            if (adres.Length == 0) { Webhook.Kaydet(new WebhookAyari(false, "")); c.Text = "Bildirim kanalı kapatıldı."; return; }
            // Geçerlilik.
            if (!Webhook.AdresGecerli(adres)) { Bilgi("Adres geçersiz. Yalnızca https:// ile başlayan, kullanıcı adı/parola içermeyen ve yerel olmayan adresler kabul edilir."); return; }
            // Onay.
            if (!Onay("KRİTİK bir olay olduğunda (ör. yeni kapı açıldı, USB klavye, kara liste eşleşmesi) olayın KISA METNİ bu adrese internet üzerinden gönderilecek:\r\n\r\n" + adres + "\r\n\r\nMetin cihaz adı/IP gibi bilgiler içerebilir. Yalnızca güvendiğiniz bir kanalı girin.\r\n\r\nAçılsın ve bir deneme mesajı gönderilsin mi?", "Bildirim Kanalı")) return;
            // Kaydet ve dene.
            Webhook.Kaydet(new WebhookAyari(true, adres)); bool ok = await Webhook.GonderAsync(adres, "BulutSoft deneme mesajı: bildirim kanalı çalışıyor."); c.Text = ok ? "Kanal açıldı, deneme mesajı gönderildi." : "Kanal açıldı ama deneme mesajı gönderilemedi (adresi/interneti kontrol edin).";
        }, "AracKutusu");
        // Sayfa.
        var sr = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90 }; sr.Controls.AddRange(new Control[] { m, d, t, w });
        // Geri ver.
        return Sayfa(Acik("Kendini koruma: ayar dosyalarına izinsiz oynanırsa fark eder. Tanılama: destek için güvenli bir özet. Bildirim kanalı: kritik olayları telefonunuza/ekibinize haber verir (varsayılan KAPALI).", 56), sr, c);
    }

    // Koyu tablo üretir.
    private static DataGridView Tablo(params (string Ad, string Baslik, int Agirlik)[] kolon)
    {
        // Tablo.
        var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Kutu, GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false };
        // Renkler.
        g.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); g.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; g.DefaultCellStyle.BackColor = Kutu; g.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); g.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); g.DefaultCellStyle.WrapMode = DataGridViewTriState.True; g.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        // Sütunlar.
        foreach (var (ad, b, a) in kolon) { g.Columns.Add(ad, b); g.Columns[ad]!.FillWeight = a; }
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(g); return g;
    }
}
