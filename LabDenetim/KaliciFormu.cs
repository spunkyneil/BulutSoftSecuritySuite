// Bu dosya "Kalıcılık Denetçisi" penceresini çizer: başlangıç öğelerini (Run anahtarları, Başlangıç klasörleri, Zamanlanmış Görevler, hizmetler)
// dijital imza ve risk bilgisiyle listeler; seçileni GERİ ALINABİLİR biçimde devre dışı bırakır. Tarama yalnızca OKUR.
// Devre dışı bırakma sistemi değiştirir: onay + (gerekirse) yönetici izni (UAC) ister, Microsoft imzalı öğelere dokunulmaz, silme yoktur.
using System.Diagnostics;

// Programımızın adı.
namespace LabDenetim;

// Kalıcılık Denetçisi penceresi.
internal sealed class KaliciFormu : SayfaDenetimi
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Kontroller.
    private readonly DataGridView _t = new(); private readonly Label _durum = new(), _ayrinti = new(); private readonly Button _tara = new EfektliButon(), _degistir = new EfektliButon(), _konum = new EfektliButon();
    private readonly CheckBox _gizleMs = new() { Text = "Microsoft imzalıları gizle", Checked = true, AutoSize = true }, _sadeceRisk = new() { Text = "Yalnızca riskliler (Orta ve üstü)", AutoSize = true };
    // Veri.
    private List<KaliciOge> _ogeler = new(); private readonly Action<string> _gunluk; private bool _calisiyor;

    // Pencereyi kurar.
    public KaliciFormu(Action<string> gunluk)
    {
        // Günlük.
        _gunluk = gunluk;
        // Pencere.
        Text = "Kalıcılık Denetçisi"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1180, 680); MinimumSize = new Size(980, 540); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Açıklama.
        var acik = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(14, 10, 14, 0), ForeColor = Soluk, UseMnemonic = false, Text = "Zararlı yazılımlar bilgisayara kalıcı yerleşmek için başlangıç noktalarını kullanır. Bu araç hepsini okur, dijital imzalarını doğrular ve riskini gösterir. Seçtiğiniz öğeyi GERİ ALINABİLİR biçimde devre dışı bırakabilirsiniz (silmez). Microsoft imzalı öğelere dokunulmaz." };
        // Düğme şeridi.
        _tara.Text = "▶ Tara ve İmzaları Doğrula"; _tara.Size = new Size(230, 32); _degistir.Text = "⏻ Devre Dışı Bırak / Yeniden Etkinleştir"; _degistir.Size = new Size(320, 32); _konum.Text = "Dosya Konumunu Aç"; _konum.Size = new Size(170, 32);
        foreach (var b in new[] { _tara, _degistir, _konum }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); } _degistir.BackColor = DesignTokens.Ozgun(TokenRol.StatusDanger);
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(10, 6, 0, 0), WrapContents = false }; _gizleMs.Margin = new Padding(8, 6, 8, 0); _sadeceRisk.Margin = new Padding(0, 6, 0, 0); serit.Controls.AddRange(new Control[] { _tara, _degistir, _konum, _gizleMs, _sadeceRisk });
        // Alt: ayrıntı ve durum.
        _ayrinti.Dock = DockStyle.Bottom; _ayrinti.Height = 74; _ayrinti.Padding = new Padding(14, 8, 14, 0); _ayrinti.ForeColor = Yazi; _ayrinti.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _ayrinti.UseMnemonic = false; _ayrinti.Text = "Bir satır seçin: komut, konum ve risk nedeni burada görünür.";
        _durum.Dock = DockStyle.Bottom; _durum.Height = 30; _durum.Padding = new Padding(14, 6, 14, 0); _durum.ForeColor = Soluk; _durum.UseMnemonic = false; _durum.Text = "Hazır. \"Tara\"ya basın.";
        // Tablo.
        _t.Dock = DockStyle.Fill; _t.ReadOnly = true; _t.AllowUserToAddRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = false; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = Kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false; _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; _t.DefaultCellStyle.BackColor = Kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.RowTemplate.Height = 30;
        // Sütunlar.
        foreach (var (ad, b, a) in new[] { ("Risk", "Risk", 8), ("Tur", "Tür", 13), ("Ad", "Ad", 19), ("Imza", "İmza", 11), ("Yay", "Yayıncı", 17), ("Yol", "Çalışan Dosya", 38), ("Dur", "Durum", 9) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; }
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(_t);
        // Dock sırası.
        Controls.Add(_t); Controls.Add(_ayrinti); Controls.Add(_durum); Controls.Add(serit); Controls.Add(acik);
        // Olaylar.
        _tara.Click += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "KaliciFormu"); _degistir.Click += (_, _) => GuvenliCalistir.Baslat(() => DegistirAsync(), "KaliciFormu"); _konum.Click += (_, _) => KonumAc(); _t.SelectionChanged += (_, _) => AyrintiYaz(); _gizleMs.CheckedChanged += (_, _) => Doldur(); _sadeceRisk.CheckedChanged += (_, _) => Doldur();
    }

    // Tara: numaralandır, imzaları doğrula, riskleri hesapla (arka planda).
    private async Task TaraAsync()
    {
        // Üst üste çalışmasın.
        if (_calisiyor) return; _calisiyor = true; _tara.Enabled = false; _durum.ForeColor = Soluk; _durum.Text = "Başlangıç öğeleri okunuyor ve dijital imzalar doğrulanıyor (birkaç on saniye sürebilir)...";
        // Hata olsa da düğme açılsın.
        try
        {
            // Ağır işi arka planda yap.
            _ogeler = await Task.Run(() => { var l = KaliciDenetci.Tara(); KaliciDenetci.Degerlendir(l); return l; }); Doldur();
            // Özet.
            int y = _ogeler.Count(o => o.Risk == "Yüksek"), orta = _ogeler.Count(o => o.Risk == "Orta"); _durum.ForeColor = y > 0 ? DesignTokens.Ozgun(TokenRol.StatusDanger) : orta > 0 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"{_ogeler.Count} öğe tarandı · {y} yüksek, {orta} orta risk · {_ogeler.Count(o => o.Microsoft)} Microsoft imzalı";
            // Köşe bildirimi.
            Bildirim.Goster("Kalıcılık taraması bitti", $"{_ogeler.Count} öğe · {y} yüksek, {orta} orta risk.", y > 0 ? BildirimTuru.Uyari : BildirimTuru.Basari);
            // Yüksek risk varsa günlüğe.
            if (y > 0) _gunluk($"KRİTİK -> Kalıcılık denetçisi: {y} yüksek riskli başlangıç öğesi bulundu.");
        }
        // Beklenmeyen hata.
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Hata: " + ex.Message; }
        // Düğmeyi aç.
        finally { _calisiyor = false; if (!IsDisposed) _tara.Enabled = true; }
    }

    // Süzgece göre görünen öğeler.
    private IEnumerable<KaliciOge> Gorunen() => _ogeler.Where(o => !(_gizleMs.Checked && o.Microsoft) && (!_sadeceRisk.Checked || o.Risk is "Orta" or "Yüksek")).OrderByDescending(o => o.Risk switch { "Yüksek" => 3, "Orta" => 2, "Düşük" => 1, _ => 0 }).ThenBy(o => o.Tur).ThenBy(o => o.Ad, StringComparer.OrdinalIgnoreCase);

    // Tabloyu doldurur.
    private void Doldur()
    {
        // Temizle ve yaz.
        _t.Rows.Clear();
        foreach (var o in Gorunen())
        {
            // Satır.
            int i = _t.Rows.Add(o.Risk, o.Tur, o.Ad, o.Imza, o.Yayinci, o.Yol, o.Etkin ? "Etkin" : "Devre dışı"); _t.Rows[i].Tag = o;
            // Risk rengi.
            _t.Rows[i].Cells[0].Style.ForeColor = o.Risk switch { "Yüksek" => DesignTokens.Ozgun(TokenRol.StatusDanger), "Orta" => DesignTokens.Ozgun(TokenRol.StatusWarning), "Düşük" => DesignTokens.Ozgun(TokenRol.StatusWarning), _ => DesignTokens.Ozgun(TokenRol.StatusSuccess) }; if (!o.Etkin) _t.Rows[i].Cells[6].Style.ForeColor = Soluk;
        }
    }

    // Seçili öğe (yoksa null).
    private KaliciOge? Secili() => _t.SelectedRows.Count > 0 ? _t.SelectedRows[0].Tag as KaliciOge : null;

    // Ayrıntı satırını yazar.
    private void AyrintiYaz() { var o = Secili(); _ayrinti.Text = o == null ? "Bir satır seçin: komut, konum ve risk nedeni burada görünür." : $"Risk: {o.Risk} — {o.Neden}\r\nKomut: {o.Komut}\r\nKonum: {o.Konum}"; }

    // Seçili dosyanın klasörünü Gezgin'de açar.
    private void KonumAc() { var o = Secili(); if (o == null || !File.Exists(o.Yol)) { TemaliMesaj.Goster("Önce var olan bir dosyası olan satırı seçin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + o.Yol + "\"") { UseShellExecute = true }); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "KaliciFormu.KonumAc"); } }

    // Seçili öğeyi devre dışı bırakır veya yeniden etkinleştirir.
    private async Task DegistirAsync()
    {
        // Yalnızca Yönetici sistemi değiştirebilir.
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)) { TemaliMesaj.Goster("\"Başlangıç Öğesini Devre Dışı Bırak\" özelliği için yetkiniz yok.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Seçim.
        var o = Secili(); if (o == null) { TemaliMesaj.Goster("Önce tablodan bir öğe seçin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Yapılacak işlem.
        bool devreDisi = o.Etkin; var plan = KaliciDenetci.DevreDisiPlani(o, devreDisi);
        // Reddedildiyse nedenini göster.
        if (plan.Ret != null) { TemaliMesaj.Goster(plan.Ret, "Yapılamaz", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay (varsayılan Hayır).
        string ne = devreDisi ? "DEVRE DIŞI BIRAKILACAK (silinmez; istediğiniz zaman yeniden etkinleştirebilirsiniz)" : "YENİDEN ETKİNLEŞTİRİLECEK";
        if (TemaliMesaj.Goster($"\"{o.Ad}\" ({o.Tur}) {ne}.\r\n\r\nRisk: {o.Risk} — {o.Neden}\r\nKomut: {o.Komut}\r\n\r\n" + (plan.Yonetici ? "Windows yönetici izni (UAC) istenecek.\r\n\r\n" : "") + "Devam edilsin mi?", devreDisi ? "Devre Dışı Bırak" : "Yeniden Etkinleştir", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Uygula.
        _degistir.Enabled = false;
        try
        {
            // HKCU: doğrudan yazılır.
            if (plan.DogrudanHkcu) { KaliciDenetci.HkcuYaz(o, devreDisi); }
            // Diğerleri: yönetici komutu.
            else { var (ok, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(plan.Komut!); if (!ok) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Yapılamadı: " + hata; return; } }
            // Yedek kaydı (geri almak için kayıt) ve durum.
            if (devreDisi) KaliciDenetci.YedekEkle(o); else { var l = KaliciDenetci.YedekOku(); l.RemoveAll(x => x.Tur == o.Tur && x.Ad == o.Ad && x.Konum == o.Konum); KaliciDenetci.YedekYaz(l); }
            // Durumu yeniden oku (gerçekten değişti mi?).
            o.Etkin = DurumOku(o, devreDisi); Doldur(); _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"\"{o.Ad}\" için komut çalıştırıldı. Durum sütununu kontrol edin."; _gunluk($"KALICILIK -> \"{o.Ad}\" ({o.Tur}) {(devreDisi ? "devre dışı bırakıldı" : "yeniden etkinleştirildi")}.");
        }
        // Hata.
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Hata: " + ex.Message; }
        // Düğmeyi aç.
        finally { _degistir.Enabled = true; }
    }

    // İşlemden sonra öğenin gerçek durumunu okur (okunamayan türlerde beklenen durumu varsayar).
    private static bool DurumOku(KaliciOge o, bool devreDisiIstendi)
    {
        // Run/Başlangıç klasörü için kayıt defterinden doğrula.
        if (o.Tur is "Run" or "Başlangıç Klasörü") return !KaliciDenetci.OnayliDevreDisi(o.Hklm, o.Tur == "Run" ? (o.Wow64 ? "Run32" : "Run") : "StartupFolder", o.Ad);
        // Diğerleri: beklenen.
        return !devreDisiIstendi;
    }
}
