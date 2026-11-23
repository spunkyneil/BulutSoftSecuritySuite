// Bu dosya Süreç Avcısı penceresini içerir; ölçüm ve sonlandırma mantığı BulutSoft.Core içindedir (SurecAvcisi.cs).
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Programımızın adı.
namespace LabDenetim;

// Süreç Avcısı penceresi.
internal sealed class SurecAvciFormu : TemaliForm
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Kontroller.
    private readonly DataGridView _t = new(); private readonly Label _durum = new(); private readonly Button _olc = new EfektliButon(), _oldur = new EfektliButon(); private readonly Action<string> _gunluk; private bool _calisiyor; private int _saniye = 8;

    // Pencereyi kurar.
    public SurecAvciFormu(Action<string> gunluk)
    {
        // Günlük.
        _gunluk = gunluk;
        // Pencere.
        Text = "Süreç Avcısı"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1000, 560); MinimumSize = new Size(860, 460); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        // Açıklama.
        var acik = new Label { Dock = DockStyle.Top, Height = 66, Padding = new Padding(14, 10, 14, 0), ForeColor = Soluk, UseMnemonic = false, Text = "Fidye yazılımı alarmı geldiyse: kısa bir süre boyunca hangi programların diske çok veri yazdığını ölçer. Beklenmedik bir programın başta olması şüphe uyandırır. Sonlandırma onay ister; sistem süreçlerine ve programın kendisine dokunulmaz. (Yönetici olarak çalıştırılan süreçler ölçülemeyebilir.)" };
        // Düğmeler.
        _olc.Text = "▶ Ölç (8 sn)"; _olc.Size = new Size(140, 32); _oldur.Text = "■ Seçili Süreci Sonlandır"; _oldur.Size = new Size(220, 32); foreach (var b in new[] { _olc, _oldur }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); } _oldur.BackColor = DesignTokens.Ozgun(TokenRol.StatusDanger);
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 6, 0, 0) }; serit.Controls.AddRange(new Control[] { _olc, _oldur });
        // Durum.
        _durum.Dock = DockStyle.Bottom; _durum.Height = 32; _durum.Padding = new Padding(14, 7, 14, 0); _durum.ForeColor = Soluk; _durum.UseMnemonic = false; _durum.Text = "Hazır. \"Ölç\"e basın.";
        // Tablo.
        _t.Dock = DockStyle.Fill; _t.ReadOnly = true; _t.AllowUserToAddRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = false; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = Kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false; _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; _t.DefaultCellStyle.BackColor = Kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.RowTemplate.Height = 30;
        // Sütunlar.
        foreach (var (ad, b, a) in new[] { ("Pid", "PID", 8), ("Ad", "Program", 18), ("Hiz", "Yazılan (MB/sn)", 14), ("Top", "Toplam (MB)", 12), ("Isl", "Yazma İşlemi", 12), ("Yol", "Dosya Yolu", 46) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; }
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(_t);
        // Dock sırası.
        Controls.Add(_t); Controls.Add(_durum); Controls.Add(serit); Controls.Add(acik);
        // Olaylar.
        _olc.Click += (_, _) => GuvenliCalistir.Baslat(() => OlcAsync(), "SurecAvciFormu"); _oldur.Click += (_, _) => Oldur();
    }

    // Ölçüm.
    private async Task OlcAsync()
    {
        // Üst üste çalışmasın.
        if (_calisiyor) return; _calisiyor = true; _olc.Enabled = false; _durum.ForeColor = Soluk; _durum.Text = $"{_saniye} saniye ölçülüyor... (şimdi fidye davranışı sürüyorsa yakalanır)";
        // Hata olsa da düğme açılsın.
        try
        {
            // Arka planda ölç.
            var liste = await Task.Run(() => SurecAvcisi.Olc(_saniye)); _t.Rows.Clear();
            // En çok yazan 30 süreç.
            foreach (var s in liste.Take(30)) { int i = _t.Rows.Add(s.Pid, s.Ad, (s.YazilanBayt / 1048576.0 / _saniye).ToString("0.00"), (s.YazilanBayt / 1048576.0).ToString("0.0"), s.YazmaIslemi, s.Yol); _t.Rows[i].Tag = s; if (SurecAvcisi.Korunuyor(s.Ad, s.Pid)) _t.Rows[i].DefaultCellStyle.ForeColor = Soluk; }
            // Özet.
            _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = liste.Count == 0 ? "Ölçüm süresince belirgin disk yazması yok." : $"{liste.Count} süreç yazdı; en çok yazan: {liste[0].Ad} ({liste[0].YazilanBayt / 1048576.0 / _saniye:0.0} MB/sn). Soluk satırlar korumalı sistem süreçleridir.";
            // Köşe bildirimi.
            Bildirim.Goster("Ölçüm bitti", liste.Count == 0 ? "Belirgin disk yazması yok." : $"{liste.Count} süreç yazdı; en çok yazan: {liste[0].Ad}.", BildirimTuru.Basari);
        }
        // Hata.
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Hata: " + ex.Message; }
        // Düğmeyi aç.
        finally { _calisiyor = false; if (!IsDisposed) _olc.Enabled = true; }
    }

    // Seçili süreci (onayla) sonlandırır.
    private void Oldur()
    {
        // Yalnızca Yönetici.
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)) { TemaliMesaj.Goster("\"Süreç Sonlandırma\" özelliği için yetkiniz yok.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Seçim.
        if (_t.SelectedRows.Count == 0 || _t.SelectedRows[0].Tag is not SurecYazma s) { TemaliMesaj.Goster("Önce tablodan bir süreç seçin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Korumalı.
        if (SurecAvcisi.KorumaNedeni(s.Ad, s.Pid, s.Yol) is { } neden) { TemaliMesaj.Goster(neden, "Korumalı Süreç", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Hizmet durumu sorulamadıysa kullanıcı açıkça uyarılır (engellenmez, ama bilinçli karar verilir). Ad ile yol uyuşmuyorsa da uyarılır.
        string nl2 = Environment.NewLine + Environment.NewLine, hizmetNotu = SurecAvcisi.HizmetSorgusuBasarisiz ? nl2 + "UYARI: Bu sürecin bir Windows hizmeti olup olmadığı DOĞRULANAMADI (Windows yönetim sorgusu çalışmadı). Hizmetse sonlandırmak sistemi bozabilir." : "";
        if (SurecAvcisi.Korunuyor(s.Ad, s.Pid)) hizmetNotu += nl2 + "DİKKAT: Bu süreç sistem sürecine benzer bir ad taşıyor ama sistem klasörleri dışından çalışıyor (ad taklidi olabilir).";
        else if (SurecAvcisi.KullaniciYazilabilirYol(s.Yol)) hizmetNotu += nl2 + "DİKKAT: Bu süreç kullanıcının yazabildiği bir klasörden çalışıyor.";
        // Microsoft imzalı süreçlere dokunma (dosya yoksa imza atlanır).
        if (s.Yol.Length > 0 && File.Exists(s.Yol) && ImzaDenetimi.Kontrol(new[] { s.Yol }).TryGetValue(s.Yol, out var im) && im.Microsoft) { TemaliMesaj.Goster("Bu program Microsoft imzalı; işletim sistemini bozmamak için sonlandırılamaz.", "Korumalı Süreç", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay (varsayılan Hayır).
        if (TemaliMesaj.Goster($"\"{s.Ad}\" (PID {s.Pid}) ve alt süreçleri SONLANDIRILACAK.\r\n\r\nDosya: {s.Yol}\r\n\r\nKaydedilmemiş verisi varsa kaybolur. Emin değilseniz önce bu programın ne olduğunu araştırın.\r\n\r\nSonlandırılsın mı?" + hizmetNotu, "Süreci Sonlandır", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Sonlandır.
        try { SurecAvcisi.Sonlandir(s.Pid, s.Ad); _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _durum.Text = $"\"{s.Ad}\" sonlandırıldı. Gerekirse yeniden ölçün."; _gunluk($"SÜREÇ AVCISI -> \"{s.Ad}\" (PID {s.Pid}) sonlandırıldı."); }
        // Hata (yetki vb.).
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Sonlandırılamadı: " + ex.Message; }
    }
}
