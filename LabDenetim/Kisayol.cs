// Bu dosya kullanım kolaylığı özelliklerinin veri ve pencere sınıflarını içerir (eylemler Servisler\KisayolEylemleri.cs):
// 1) Klavye kısayolları (F5, Ctrl+F, Ctrl+K, Ctrl+D, Ctrl+E, F1). 2) Komut Paleti (Ctrl+K): yazarak her özelliği bulup çalıştırın.
// 3) Zamanlanmış tarama (program açıkken, sizin izninizle, belli saatte bir otomatik tarama). 4) Başarılar (tarama geçmişinizden hesaplanan rozetler).
// 5) Eğitim modu (gerçek olmayan örnek sonuçlarla programı risksiz öğrenin).
using System.Globalization;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Zamanlanmış tarama ayarı.
internal sealed record ZamanlamaAyari(bool Aktif, int SaatAraligi, DateTime Son);

// Zamanlama ayarını dosyada saklar.
internal static class ZamanlamaDeposu
{
    // Dosya yolu (testte değiştirilebilir).
    public static string Yol { get; set; } = Path.Combine(BrandConfig.RoamingKlasor, "zamanlama.json");

    // Ayarı okur; dosya yoksa veya bozuksa "kapalı" döner.
    public static ZamanlamaAyari Yukle()
    {
        // Okuma bozulabilir.
        try { if (File.Exists(Yol)) { var a = JsonSerializer.Deserialize<ZamanlamaAyari>(File.ReadAllText(Yol)); if (a != null && a.SaatAraligi is >= 1 and <= 168) return a; } }
        // Bozuksa varsayılan.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "Kisayol.Yukle"); }
        // Varsayılan: kapalı, 24 saat.
        return new ZamanlamaAyari(false, 24, DateTime.Now);
    }

    // Ayarı yazar.
    public static void Kaydet(ZamanlamaAyari a)
    {
        // Yazma bozulabilir.
        try { Depo.YazJson(Yol, a); }
        // Yazılamazsa geç.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "Kisayol.Kaydet"); }
    }
}

// Başarı rozeti.
internal sealed record Rozet(string Ad, string Aciklama, bool Kazanildi);

// Rozetleri geçmiş kayıtlarından hesaplar (ayrı dosya tutmaz, hile yapılamaz).
internal static class Basarilar
{
    // Üst üste kaç gün tarama yapıldığını verir (bugün veya dün biten seri sayılır).
    public static int Seri(IEnumerable<GecmisKayit> kayitlar, DateTime simdi)
    {
        // Tarama yapılan günler.
        var gunler = kayitlar.Select(k => k.Tarih.Date).ToHashSet();
        // Bugün yoksa dünden başla.
        var g = gunler.Contains(simdi.Date) ? simdi.Date : simdi.Date.AddDays(-1);
        // Geriye doğru say.
        int n = 0; while (gunler.Contains(g)) { n++; g = g.AddDays(-1); }
        // Seri.
        return n;
    }

    // Rozet listesini hesaplar.
    public static List<Rozet> Hesapla(List<GecmisKayit> k, DateTime simdi)
    {
        // Eskiden yeniye sıralı kopya.
        var s = k.OrderBy(x => x.Tarih).ToList();
        // En iyi iyileşme: aynı hedefte iki ardışık taramada puan artışı.
        int enIyi = 0; foreach (var h in s.GroupBy(x => x.Hedef.ToLowerInvariant())) { var l = h.ToList(); for (int i = 1; i < l.Count; i++) enIyi = Math.Max(enIyi, l[i].Puan - l[i - 1].Puan); }
        // Rozetler.
        return new List<Rozet>
        {
            new("İlk Adım", "İlk taramanızı tamamladınız.", s.Count >= 1),
            new("Deneyimli", "10 tarama tamamladınız.", s.Count >= 10),
            new("Düzenli Nöbetçi", "3 gün üst üste tarama yaptınız.", Seri(s, simdi) >= 3),
            new("Yüksek Puan", "Bir taramada 90 ve üzeri puan aldınız.", s.Any(x => x.Puan >= 90)),
            new("Temiz Sicil", "Son taramanızda kırmızı (kritik) sorun yok.", s.Count > 0 && s[^1].Kirmizi == 0),
            new("İyileştirici", "İki tarama arasında puanı 20+ artırdınız.", enIyi >= 20)
        };
    }
}

// Komut Paleti penceresi: yazdıkça komutları süzer, Enter ile çalıştırır.
internal sealed class KomutPaleti : TemaliForm
{
    // Komut listesi: ad + çalıştırılacak iş.
    internal readonly List<(string Ad, Action Is)> _komutlar;
    // Arama kutusu ve liste.
    internal readonly TextBox _ara = new(); private readonly ListBox _liste = new();
    // Seçilen iş (pencere kapandıktan sonra çalıştırılır).
    public Action? Secilen { get; private set; }

    // Pencereyi kurar.
    public KomutPaleti(List<(string Ad, Action Is)> komutlar)
    {
        // Komutları sakla.
        _komutlar = komutlar;
        // Çerçevesiz, ortada, koyu pencere.
        FormBorderStyle = FormBorderStyle.FixedToolWindow; Text = "Komut Paleti — yazın, Enter'a basın"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 380); BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ShowInTaskbar = false;
        // Arama kutusu.
        _ara.Dock = DockStyle.Top; _ara.Font = FontProvider.Al(FontRol.Heading, FontStyle.Regular); _ara.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); _ara.ForeColor = Color.White; _ara.BorderStyle = BorderStyle.FixedSingle;
        // Liste.
        _liste.Dock = DockStyle.Fill; _liste.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular); _liste.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); _liste.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _liste.BorderStyle = BorderStyle.None; _liste.IntegralHeight = false;
        // Önce liste, sonra kutu eklenir (Dock sırası).
        Controls.Add(_liste); Controls.Add(_ara);
        // Yazdıkça süz.
        _ara.TextChanged += (_, _) => Doldur();
        // Çift tıklayınca çalıştır.
        _liste.DoubleClick += (_, _) => Calistir();
        // Klavye: yukarı/aşağı liste gezer, Enter çalıştırır, Esc kapatır.
        _ara.KeyDown += (_, e) =>
        {
            // Aşağı ok.
            if (e.KeyCode == Keys.Down) { if (_liste.SelectedIndex < _liste.Items.Count - 1) _liste.SelectedIndex++; e.Handled = true; e.SuppressKeyPress = true; }
            // Yukarı ok.
            else if (e.KeyCode == Keys.Up) { if (_liste.SelectedIndex > 0) _liste.SelectedIndex--; e.Handled = true; e.SuppressKeyPress = true; }
            // Enter.
            else if (e.KeyCode == Keys.Enter) { Calistir(); e.Handled = true; e.SuppressKeyPress = true; }
            // Esc.
            else if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; e.SuppressKeyPress = true; }
        };
        // Açılınca kutuya odaklan ve listeyi doldur.
        Shown += (_, _) => { Doldur(); _ara.Focus(); };
    }

    // Listeyi aramaya göre doldurur (Türkçe harf duyarsız).
    internal void Doldur()
    {
        // Türkçe karşılaştırma.
        var ci = new CultureInfo("tr-TR").CompareInfo; string q = _ara.Text.Trim();
        // Liste temizle ve eşleşenleri ekle.
        _liste.Items.Clear(); foreach (var k in _komutlar) if (q.Length == 0 || ci.IndexOf(k.Ad, q, CompareOptions.IgnoreCase) >= 0) _liste.Items.Add(k.Ad);
        // İlkini seç.
        if (_liste.Items.Count > 0) _liste.SelectedIndex = 0;
    }

    // Seçili komutu belirleyip pencereyi kapatır.
    internal void Calistir()
    {
        // Seçili ad.
        if (_liste.SelectedItem is not string ad) return;
        // İşi bul.
        Secilen = _komutlar.First(k => k.Ad == ad).Is; Close();
    }
}

// Zamanlanmış tarama ayar penceresi.
internal sealed class ZamanlamaFormu : TemaliForm
{
    // Kutular.
    internal readonly CheckBox _aktif = new(); private readonly ComboBox _saat = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    // Sonuç ayarı (Tamam denirse dolu).
    public ZamanlamaAyari? Sonuc { get; private set; }

    // Pencereyi kurar.
    public ZamanlamaFormu(string hedef, ZamanlamaAyari mevcut)
    {
        // Pencere ayarları.
        Text = "Zamanlanmış Tarama"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, 270); BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Body);
        // Açıklama.
        var acik = new Label { Text = $"Program AÇIKKEN, seçtiğiniz aralıkta \"{hedef}\" hedefini kendiliğinden tarar ve sonucu Geçmiş'e kaydeder.\r\n\r\nÖnemli: Bilgisayar kapalıysa veya program kapalıysa tarama yapılmaz (arka plan servisi kurmaz). Tarama yalnızca okur; sistemde hiçbir şeyi değiştirmez.", Location = new Point(16, 14), Size = new Size(488, 110), UseMnemonic = false };
        // Onay kutusu.
        _aktif.Text = "Zamanlanmış taramayı AÇ"; _aktif.Checked = mevcut.Aktif; _aktif.Location = new Point(18, 134); _aktif.AutoSize = true;
        // Aralık listesi.
        foreach (var s in new[] { 1, 3, 6, 12, 24, 72, 168 }) _saat.Items.Add(s == 168 ? "Haftada bir (168 saat)" : $"{s} saatte bir");
        // Mevcut seçim.
        int ix = new[] { 1, 3, 6, 12, 24, 72, 168 }.ToList().IndexOf(mevcut.SaatAraligi); _saat.SelectedIndex = ix < 0 ? 4 : ix; _saat.Location = new Point(18, 168); _saat.Width = 220;
        // Düğmeler.
        var tamam = new EfektliButon { Text = "Kaydet", Location = new Point(300, 214), Size = new Size(100, 34), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White }; var iptal = new EfektliButon { Text = "Vazgeç", Location = new Point(410, 214), Size = new Size(94, 34), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = Color.White };
        // Kaydet'e basılınca sonucu hazırla.
        tamam.Click += (_, _) => Sonuc = new ZamanlamaAyari(_aktif.Checked, new[] { 1, 3, 6, 12, 24, 72, 168 }[_saat.SelectedIndex], DateTime.Now);
        // Parçaları ekle.
        Controls.AddRange(new Control[] { acik, _aktif, _saat, tamam, iptal }); AcceptButton = tamam; CancelButton = iptal;
    }
}

// Başarılar penceresi.
internal sealed class BasarilarFormu : TemaliForm
{
    // Pencereyi kurar.
    public BasarilarFormu(List<GecmisKayit> kayitlar)
    {
        // Pencere ayarları.
        Text = "Başarılar"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 440); BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        // Rozetler.
        var r = Basarilar.Hesapla(kayitlar, DateTime.Now);
        // Başlık: seri ve sayı.
        var ust = new Label { Text = $"Tarama serisi: {Basarilar.Seri(kayitlar, DateTime.Now)} gün · Kayıtlı tarama: {kayitlar.Count} · Rozet: {r.Count(x => x.Kazanildi)}/{r.Count}", Dock = DockStyle.Top, Height = 44, Padding = new Padding(14, 12, 0, 0), ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning), Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold), UseMnemonic = false };
        // Rozet satırları.
        var kap = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(14, 6, 0, 0) };
        // Her rozet için bir etiket.
        foreach (var x in r) kap.Controls.Add(new Label { Text = (x.Kazanildi ? "★ " : "☆ ") + x.Ad + "  —  " + x.Aciklama, AutoSize = false, Size = new Size(520, 38), ForeColor = x.Kazanildi ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.TextMuted), UseMnemonic = false });
        // Önce dolgu, sonra üst.
        Controls.Add(kap); Controls.Add(ust);
    }
}
