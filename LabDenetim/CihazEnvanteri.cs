// Bu dosya "Ağ Cihaz Envanteri ve Yeni Cihaz Alarmı"nı çalıştırır: kendi iç ağınızda bulunan cihazları (MAC adresiyle) kalıcı bir listeye
// ("envanter") yazar. Sonraki taramalarda listede OLMAYAN bir cihaz görünürse "ağa tanımadığım yeni bir cihaz girdi" uyarısı verir.
// Taramayı mevcut Ağ Keşfi motoru yapar: yalnızca kendi özel (192.168..., 10..., 172.16...) alt ağlarınıza ping + ARP; internete çıkmaz.
// Envanter %AppData%\BulutSoft\cihaz_envanteri.json dosyasında durur.
using System.Text.Json;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Envanterdeki bir cihazın kalıcı kaydı.
internal sealed record KayitliCihaz(string Anahtar, string Mac, string Ip, string Ad, string Tur, DateTime IlkGorulme, DateTime SonGorulme, bool Guvenilir);

// Envanteri dosyada saklayan ve yeni cihazları bulan yardımcı.
internal static class CihazDeposu
{
    // Dosyanın yolu (testte değiştirilebilir).
    public static string Yol { get; set; } = Path.Combine(BrandConfig.RoamingKlasor, "cihaz_envanteri.json");

    // Cihazın benzersiz anahtarı: MAC varsa MAC, yoksa IP (MAC okunamayan cihazlar için zayıf yedek).
    public static string Anahtar(AgCihazi c) => c.Mac != "—" && c.Mac.Length > 0 ? c.Mac.ToUpperInvariant() : "IP:" + c.Ip;

    // MAC adresi "rastgele/gizlilik" türünde mi? (telefonlar her ağda farklı sahte MAC kullanabilir; ikinci hane 2,6,A,E ise yerel yönetimli adrestir.)
    public static bool RastgeleMac(string mac) => mac.Length >= 2 && "26AE".Contains(char.ToUpperInvariant(mac[1]));

    // Kayıtlı envanteri okur (yoksa boş).
    public static Dictionary<string, KayitliCihaz> Yukle()
    {
        // Okuma bozulabilir.
        return Depo.Yukle<Dictionary<string, KayitliCihaz>>(Yol) ?? new();
    }

    // Envanteri dosyaya yazar.
    public static void Kaydet(Dictionary<string, KayitliCihaz> env)
    {
        // Klasörü oluştur.
        Depo.YazJson(Yol, env, new JsonSerializerOptions { WriteIndented = true });
    }

    // Tarama sonucunu envanterle birleştirir. İlk taramaysa (envanter boş) herkes "temel" kabul edilir ve yeni sayılmaz.
    // Yeni (envanterde olmayan) cihazların anahtarlarını verir; envanteri günceller ama kaydetmez.
    public static List<string> Birlestir(Dictionary<string, KayitliCihaz> env, IEnumerable<AgCihazi> bulunan, out bool ilkTarama)
    {
        // Envanter boşsa bu ilk taramadır.
        ilkTarama = env.Count == 0;
        // Yeni cihazlar.
        var yeni = new List<string>(); var simdi = DateTime.Now;
        // Bulunan her cihaz.
        foreach (var c in bulunan)
        {
            // Bu bilgisayarın kendisi (ana makine) alarm üretmez.
            if (c.Tur.StartsWith("Bu bilgisayar", StringComparison.Ordinal)) continue;
            // Anahtar.
            string k = Anahtar(c);
            // Kayıtlıysa son görülmeyi ve adresi güncelle.
            if (env.TryGetValue(k, out var eski)) env[k] = eski with { Ip = c.Ip, Ad = c.Ad != "—" ? c.Ad : eski.Ad, SonGorulme = simdi };
            // Kayıtlı değilse ekle; ilk taramada otomatik güvenilir temel, sonra "yeni" olarak işaretle.
            else { env[k] = new KayitliCihaz(k, c.Mac, c.Ip, c.Ad, c.Tur, simdi, simdi, ilkTarama); if (!ilkTarama) yeni.Add(k); }
        }
        // Yeni cihaz anahtarları.
        return yeni;
    }
}

// "Ağ Cihaz Envanteri" penceresi.
internal sealed class CihazFormu : SayfaDenetimi
{
    // Tablo ve yazılar.
    private readonly DataGridView _tablo = new(); private readonly Label _ozet = new(), _uyari = new(); private readonly Button _tara = new EfektliButon();
    // Envanter ve son taramada yeni çıkanlar.
    private Dictionary<string, KayitliCihaz> _env = CihazDeposu.Yukle(); private HashSet<string> _yeni = new();
    // Tarama sürerken iptal şalteri.
    private CancellationTokenSource? _iptal;
    // Dışarıya (günlüğe) yazmak için.
    private readonly Action<string> _gunluk;

    // Pencereyi kurar.
    public CihazFormu(Action<string> gunluk)
    {
        // Günlük yazıcıyı sakla.
        _gunluk = gunluk;
        // Pencere ayarları.
        Text = "⌬ Ağ Cihaz Envanteri"; ClientSize = new Size(1040, 600); MinimumSize = new Size(860, 460); StartPosition = FormStartPosition.CenterParent; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Açıklama.
        var aciklama = new Label { Dock = DockStyle.Fill, UseMnemonic = false, ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), Text = "Ağınızda bulunan cihazlar kalıcı bir listeye (envanter) yazılır. İlk taramada bulunan herkes \"temel\" sayılır. Sonraki taramalarda listede OLMAYAN bir cihaz çıkarsa kırmızı \"YENİ\" olarak işaretlenir: bu, ağa tanımadığınız biri/bir şey girmiş olabileceği anlamına gelir. Yalnızca kendi özel ağınızı tarar." };
        // Uyarı şeridi (yeni cihaz varsa görünür).
        _uyari.Dock = DockStyle.Fill; _uyari.UseMnemonic = false; _uyari.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _uyari.BackColor = DesignTokens.Ozgun(TokenRol.StatusDangerBg); _uyari.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); _uyari.Padding = new Padding(10, 4, 10, 4); _uyari.Visible = false;
        // Özet.
        _ozet.Dock = DockStyle.Fill; _ozet.UseMnemonic = false; _ozet.ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted);
        // Tablo.
        _tablo.Dock = DockStyle.Fill; _tablo.ReadOnly = true; _tablo.AllowUserToAddRows = false; _tablo.RowHeadersVisible = false; _tablo.MultiSelect = false; _tablo.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _tablo.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Koyu renkler.
        _tablo.BackgroundColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.BorderStyle = BorderStyle.None; _tablo.EnableHeadersVisualStyles = false; _tablo.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo);
        // Satır renkleri.
        _tablo.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); _tablo.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _tablo.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _tablo.RowTemplate.Height = 34;
        // Sütunlar.
        foreach (var (ad, baslik, agirlik) in new[] { ("Durum", "Durum", 14), ("Ad", "Bilgisayar adı", 22), ("Ip", "IP adresi", 14), ("Mac", "MAC adresi", 18), ("Tur", "Tür", 24), ("Ilk", "İlk görülme", 16), ("Son", "Son görülme", 16) }) { _tablo.Columns.Add(ad, baslik); _tablo.Columns[ad]!.FillWeight = agirlik; }
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(_tablo);
        // Düğmeler.
        _tara.Text = "↻ Ağı Şimdi Tara"; var guven = Dugme("✓ Seçiliyi Güvenilir İşaretle", 230); var cikar = Dugme("✖ Seçiliyi Envanterden Çıkar", 230); var sifirla = Dugme("Envanteri Sıfırla", 150); var kapat = Dugme("Kapat", 90);
        // Tara düğmesi stili.
        _tara.Size = new Size(170, 34); _tara.FlatStyle = FlatStyle.Flat; _tara.BackColor = DesignTokens.Ozgun(TokenRol.StatusSuccessBg); _tara.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _tara.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold); _tara.Margin = new Padding(8, 4, 0, 0); _tara.UseMnemonic = false; _tara.Cursor = Cursors.Hand;
        // Tıklamalar.
        _tara.Click += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "CihazEnvanteri"); guven.Click += (_, _) => Guvenilir(); cikar.Click += (_, _) => Cikar(); sifirla.Click += (_, _) => Sifirla(); kapat.Click += (_, _) => Close();
        // Düğme paneli.
        var dugmeler = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; dugmeler.Controls.AddRange(new Control[] { kapat, sifirla, cikar, guven, _tara });
        // Ana ızgara.
        var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(14) }; tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Satır boyları.
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        // Yerleştir.
        tbl.Controls.Add(aciklama, 0, 0); tbl.Controls.Add(_uyari, 0, 1); tbl.Controls.Add(_ozet, 0, 2); tbl.Controls.Add(_tablo, 0, 3); tbl.Controls.Add(dugmeler, 0, 4); Controls.Add(tbl);
        // Pencere kapanırken taramayı durdur.
        FormClosing += (_, _) => _iptal?.Cancel();
        // Açılınca kayıtlı envanteri göster.
        Shown += (_, _) => Doldur();
    }

    // Koyu düğme üretir.
    private static Button Dugme(string t, int g) => new EfektliButon() { Text = t, Size = new Size(g, 34), Margin = new Padding(8, 4, 0, 0), UseMnemonic = false, FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold), Cursor = Cursors.Hand };

    // Tabloyu envanterden doldurur.
    private void Doldur()
    {
        // Tabloyu boşalt.
        _tablo.Rows.Clear();
        // Yeniler üstte, sonra son görülmeye göre.
        foreach (var c in _env.Values.OrderByDescending(x => _yeni.Contains(x.Anahtar)).ThenByDescending(x => x.SonGorulme))
        {
            // Durum yazısı.
            string durum = _yeni.Contains(c.Anahtar) ? "● YENİ" : c.Guvenilir ? "✓ Güvenilir" : "Bilinen";
            // Rastgele MAC notu.
            string tur = c.Tur + (CihazDeposu.RastgeleMac(c.Mac) ? " (rastgele MAC: telefon olabilir)" : "");
            // Satırı ekle.
            int i = _tablo.Rows.Add(durum, c.Ad, c.Ip, c.Mac, tur, c.IlkGorulme.ToString("dd.MM.yyyy HH:mm"), c.SonGorulme.ToString("dd.MM.yyyy HH:mm")); _tablo.Rows[i].Tag = c;
            // Yeniyse kırmızı, güvenilirse yeşil boya.
            _tablo.Rows[i].Cells[0].Style.ForeColor = _yeni.Contains(c.Anahtar) ? DesignTokens.Ozgun(TokenRol.StatusDanger) : c.Guvenilir ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusWarning);
            // Yeni satırın zemini hafif kırmızı olsun.
            if (_yeni.Contains(c.Anahtar)) _tablo.Rows[i].DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.StatusDangerBg);
        }
        // Özet ve uyarı.
        _ozet.Text = _env.Count == 0 ? "Envanter boş. \"Ağı Şimdi Tara\"ya basın; ilk tarama temel olarak kaydedilir." : $"{_env.Count} cihaz kayıtlı · yeni: {_yeni.Count} · dosya: {CihazDeposu.Yol}";
        // Yeni cihaz uyarısı.
        _uyari.Visible = _yeni.Count > 0; _uyari.Text = $"DİKKAT: Ağda envanterde olmayan {_yeni.Count} yeni cihaz var! Tanıyorsanız \"Güvenilir İşaretle\", tanımıyorsanız ağ parolanızı/yönlendiriciyi kontrol edin.";
    }

    // Ağı tarar ve envanterle birleştirir.
    private async Task TaraAsync()
    {
        // Çift tıklamayı engelle.
        _tara.Enabled = false; _iptal = new CancellationTokenSource();
        // Hata olsa da düğme açılsın.
        try
        {
            // Durum yazısı.
            _ozet.Text = "Ağ taranıyor (ping + ARP), biraz sürebilir...";
            // Taramayı yap.
            var bulunan = await AgKesfi.TaraAsync(new Progress<string>(m => { if (!IsDisposed) _ozet.Text = m; }), new Progress<int>(_ => { }), _iptal.Token);
            // Envanterle birleştir.
            var yeni = CihazDeposu.Birlestir(_env, bulunan, out bool ilk);
            // Yenileri sakla.
            _yeni = yeni.ToHashSet();
            // Kaydet.
            CihazDeposu.Kaydet(_env);
            // Günlüğe yaz.
            _gunluk(ilk ? $"AĞ ENVANTERİ -> İlk tarama: {_env.Count} cihaz temel olarak kaydedildi." : yeni.Count > 0 ? $"AĞ ENVANTERİ ALARM -> {yeni.Count} YENİ CİHAZ: {string.Join(", ", yeni.Select(k => _env[k].Ip + " (" + _env[k].Tur + ")"))}" : $"AĞ ENVANTERİ -> Yeni cihaz yok ({_env.Count} cihaz kayıtlı).");
            // Ekranı güncelle.
            Doldur();
        }
        // Durdurulduysa sessiz geç.
        catch (OperationCanceledException hk1) { HataKayit.Sessiz(hk1, "CihazEnvanteri.TaraAsync"); }
        // Başka hata.
        catch (Exception ex) { if (!IsDisposed) TemaliMesaj.Goster(ex.Message, "Tarama yapılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        // Düğmeyi aç.
        finally { if (!IsDisposed) _tara.Enabled = true; }
    }

    // Seçili cihazı güvenilir işaretler.
    private void Guvenilir()
    {
        // Seçili satır yoksa uyar.
        if (_tablo.SelectedRows.Count == 0 || _tablo.SelectedRows[0].Tag is not KayitliCihaz c) { TemaliMesaj.Goster("Önce tablodan bir cihaz seçin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Güvenilir yap ve yeni işaretini kaldır.
        _env[c.Anahtar] = c with { Guvenilir = true }; _yeni.Remove(c.Anahtar);
        // Kaydet ve göster.
        CihazDeposu.Kaydet(_env); Doldur();
    }

    // Seçili cihazı envanterden çıkarır (bir sonraki taramada yeniden görünürse yeni sayılır).
    private void Cikar()
    {
        // Seçili satır yoksa uyar.
        if (_tablo.SelectedRows.Count == 0 || _tablo.SelectedRows[0].Tag is not KayitliCihaz c) { TemaliMesaj.Goster("Önce tablodan bir cihaz seçin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay iste (varsayılan Hayır).
        if (TemaliMesaj.Goster($"{c.Ip} ({c.Mac}) envanterden çıkarılsın mı?\r\n\r\nAğda hâlâ varsa bir sonraki taramada YENİ olarak görünür.", "Envanterden Çıkar", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Çıkar, kaydet, göster.
        _env.Remove(c.Anahtar); _yeni.Remove(c.Anahtar); CihazDeposu.Kaydet(_env); Doldur();
    }

    // Tüm envanteri siler.
    private void Sifirla()
    {
        // Onay iste (varsayılan Hayır).
        if (TemaliMesaj.Goster("Tüm envanter silinsin mi?\r\n\r\nBir sonraki tarama yeniden \"temel\" kabul edilir ve eski yeni-cihaz uyarıları kaybolur.", "Envanteri Sıfırla", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Temizle ve kaydet.
        _env.Clear(); _yeni.Clear(); CihazDeposu.Kaydet(_env); Doldur();
    }
}
