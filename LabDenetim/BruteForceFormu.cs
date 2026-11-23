// Bu dosya "Giriş Saldırı Radarı" penceresini çizer: hatalı parola deneme yoğunluğunu tarar, saldırgan IP'leri listeler
// ve seçilenleri (onay + yönetici izniyle) Windows Güvenlik Duvarı'nda engeller. Tarama yalnızca OKUR; engelleme SİSTEMİ DEĞİŞTİRİR.
using System.Diagnostics;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Giriş Saldırı Radarı penceresi.
internal sealed class BruteForceFormu : SayfaDenetimi
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Kontroller.
    private readonly CheckBox _win = new() { Text = "Bu bilgisayar (Windows olay 4625)", Checked = true, AutoSize = true }, _ssh = new() { Text = "Sensör SSH günlüğü:", AutoSize = true }, _oto = new() { Text = "Her 60 sn'de otomatik yenile", AutoSize = true };
    private readonly ComboBox _sensor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 }, _sure = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly NumericUpDown _esik = new() { Minimum = 3, Maximum = 200, Value = 5, Width = 60 };
    private readonly DataGridView _t = new(); private readonly Label _durum = new(); private readonly Button _tara = new EfektliButon(), _engel = new EfektliButon(), _eski = new EfektliButon();
    // Sensörler, bulunan kaynaklar ve günlük kancası.
    private readonly List<SensorProfili> _sensorler = SensorDeposu.Yukle(); private List<SaldiriKaynagi> _kaynaklar = new(); private readonly Action<string> _gunluk; private readonly System.Windows.Forms.Timer _zaman = new() { Interval = 60_000 }; private bool _calisiyor;

    // Pencereyi kurar.
    public BruteForceFormu(Action<string> gunluk)
    {
        // Günlük.
        _gunluk = gunluk;
        // Pencere.
        Text = "Giriş Saldırı Radarı"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1080, 640); MinimumSize = new Size(940, 520); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Açıklama.
        var acik = new Label { Dock = DockStyle.Top, Height = 64, Padding = new Padding(14, 10, 14, 0), ForeColor = Soluk, UseMnemonic = false, Text = "Kısa sürede art arda hatalı parola deneyen IP adreslerini bulur (kaba kuvvet / parola püskürtme). Tarama yalnızca OKUR. Windows günlüğü için yönetici izni gerekir; sensör için Uzak Sensörler'de kayıtlı bir sensör ve günlük okuma yetkisi gerekir. Engelleme her zaman sizin onayınızla ve yönetici izniyle yapılır, otomatik engelleme yoktur." };
        // Ayar şeridi 1: kaynaklar.
        var s1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 6, 0, 0), WrapContents = false }; foreach (var s in _sensorler) _sensor.Items.Add(s.Ad); if (_sensor.Items.Count > 0) _sensor.SelectedIndex = 0; else { _ssh.Enabled = false; _sensor.Enabled = false; _ssh.Text = "Sensör SSH günlüğü (kayıtlı sensör yok)"; }
        s1.Controls.AddRange(new Control[] { _win, _ssh, _sensor, _eski });
        // Ayar şeridi 2: süre, eşik, düğmeler.
        foreach (var x in new[] { "15 dakika", "1 saat", "6 saat", "24 saat" }) _sure.Items.Add(x); _sure.SelectedIndex = 1;
        _tara.Text = "▶ Tara"; _tara.Size = new Size(100, 32); _engel.Text = "⛔ Seçilenleri Engelle"; _engel.Size = new Size(190, 32); _eski.Text = "⌛ Eskileri Kaldır"; _eski.Size = new Size(190, 32); foreach (var b in new[] { _tara, _engel, _eski }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(8, 0, 0, 0); } _engel.BackColor = DesignTokens.Ozgun(TokenRol.StatusDanger);
        var s2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 4, 0, 0), WrapContents = false }; s2.Controls.Add(new Label { Text = "Süre:", AutoSize = true, Margin = new Padding(0, 8, 4, 0), ForeColor = Soluk }); s2.Controls.Add(_sure); s2.Controls.Add(new Label { Text = "Eşik (hatalı deneme):", AutoSize = true, Margin = new Padding(14, 8, 4, 0), ForeColor = Soluk }); s2.Controls.Add(_esik); _oto.Margin = new Padding(14, 6, 0, 0); s2.Controls.Add(_oto); s2.Controls.Add(_tara); s2.Controls.Add(_engel);
        // Durum yazısı.
        _durum.Dock = DockStyle.Bottom; _durum.Height = 34; _durum.Padding = new Padding(14, 8, 14, 0); _durum.ForeColor = Soluk; _durum.UseMnemonic = false; _durum.Text = "Hazır. \"Tara\"ya basın.";
        // Tablo.
        _t.Dock = DockStyle.Fill; _t.ReadOnly = true; _t.AllowUserToAddRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = true; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = Kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false; _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; _t.DefaultCellStyle.BackColor = Kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.RowTemplate.Height = 32;
        // Sütunlar.
        foreach (var (ad, b, a) in new[] { ("Sev", "Seviye", 9), ("Ip", "Kaynak IP", 16), ("Say", "Hatalı Deneme", 10), ("Ks", "Farklı Kullanıcı", 10), ("Kul", "Denenen Kullanıcılar", 20), ("Kay", "Kaynak", 12), ("Ilk", "İlk / Son Deneme", 17), ("Dur", "Durum", 10) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; }
        // Koyu kaydırma çubukları.
        KoyuTema.UygulaTablo(_t);
        // Dock sırası: tablo, durum, şeritler, açıklama.
        Controls.Add(_t); Controls.Add(_durum); Controls.Add(s2); Controls.Add(s1); Controls.Add(acik);
        // Olaylar.
        // Açıklayıcı ipuçları (üzerine gelince görünür).
        var ipucu = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400 };
        ipucu.SetToolTip(_tara, "Seçili süredeki başarısız oturum açma denemelerini okur ve saldırı gibi görünen IP adreslerini listeler. Yalnızca okur, hiçbir şeyi değiştirmez.");
        ipucu.SetToolTip(_engel, "Seçili IP adreslerinden gelen bağlantıları Windows Güvenlik Duvarı'nda engeller. Her seferinde onayınızı ve Windows yönetici izni (UAC) ister; geri alınabilir.");
        ipucu.SetToolTip(_eski, "30 günden eski engelleri listeler ve onayınızla güvenlik duvarından kaldırır. Bu programdan önce konmuş engellere dokunmaz.");
        _tara.Click += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "BruteForceFormu"); _engel.Click += (_, _) => GuvenliCalistir.Baslat(() => EngelleAsync(), "BruteForceFormu"); _eski.Click += (_, _) => GuvenliCalistir.Baslat(() => EskileriKaldirAsync(), "BruteForceFormu"); _oto.CheckedChanged += (_, _) => { if (_oto.Checked) _zaman.Start(); else _zaman.Stop(); }; _zaman.Tick += (_, _) => GuvenliCalistir.Baslat(() => TaraAsync(), "BruteForceFormu");
        // Kapanırken sayaç dursun.
        FormClosed += (_, _) => _zaman.Dispose();
    }

    // Seçilen süre.
    private TimeSpan Sure() => _sure.SelectedIndex switch { 0 => TimeSpan.FromMinutes(15), 1 => TimeSpan.FromHours(1), 2 => TimeSpan.FromHours(6), _ => TimeSpan.FromHours(24) };

    // Seçili kaynaklardan denemeleri okuyup analiz eder.
    private async Task TaraAsync()
    {
        // Üst üste çalışmasın.
        if (_calisiyor) return; _calisiyor = true; _tara.Enabled = false; _durum.ForeColor = Soluk; _durum.Text = "Taranıyor...";
        // Hata olsa da düğme açılsın.
        try
        {
            // Pencere ve eşik.
            var sure = Sure(); int esik = (int)_esik.Value; var tum = new List<GirisDenemesi>(); var notlar = new List<string>(); int okunamayan = 0;
            // Windows.
            if (_win.Checked)
            {
                // Arka planda oku (günlük büyük olabilir).
                try { tum.AddRange(await Task.Run(() => BruteForce.WindowsOku(sure))); }
                // Yetki yoksa kullanıcıya yönetici olarak yeniden açmayı öner (onaylı).
                catch (BruteForce.KaynakHatasi ex) { okunamayan++; notlar.Add(ex.Message); if (ex.YetkiYok) YoneticiOner(); }
            }
            // Sensör.
            if (_ssh.Checked && _sensor.SelectedIndex >= 0)
            {
                // Seçili sensör.
                var p = _sensorler[_sensor.SelectedIndex];
                // Oku.
                try { tum.AddRange(await BruteForce.SensorOkuAsync(p, sure, CancellationToken.None)); } catch (BruteForce.KaynakHatasi ex) { okunamayan++; notlar.Add(ex.Message); } catch (Exception ex) { okunamayan++; notlar.Add("Sensör: " + ex.Message); }
            }
            // Analiz.
            _kaynaklar = BruteForce.Analiz(tum, DateTime.Now, sure, esik, TimeSpan.FromMinutes(2)); var engelli = BruteForce.EngelliKurallar(); Doldur(engelli);
            // Özet.
            int yuksek = _kaynaklar.Count(k => k.Seviye == "Yüksek");
            _durum.ForeColor = _kaynaklar.Count > 0 ? DesignTokens.Ozgun(TokenRol.StatusDanger) : okunamayan > 0 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.StatusSuccess);
            _durum.Text = (okunamayan > 0 ? "KÖR/KISMİ SONUÇ: seçili kaynaklardan en az biri OKUNAMADI; 'şüpheli kaynak yok' saldırı olmadığı anlamına GELMEZ. " : "") + $"{tum.Count} hatalı deneme okundu · {_kaynaklar.Count} şüpheli kaynak ({yuksek} yüksek risk)" + (notlar.Count > 0 ? " · " + string.Join(" | ", notlar) : "");
            // Şüpheli kaynak varsa olay günlüğüne KRİTİK yaz.
            if (_kaynaklar.Count > 0) _gunluk($"KRİTİK -> Giriş saldırısı şüphesi: {_kaynaklar.Count} IP, en yoğunu {_kaynaklar[0].Ip} ({_kaynaklar[0].Sayi} hatalı deneme).");
            // Köşe bildirimi.
            Bildirim.Goster(okunamayan > 0 ? "Radar taraması KISMİ" : "Radar taraması bitti", okunamayan > 0 ? "Bir kaynak okunamadı: sonuç 'saldırı yok' demek değildir." : $"{tum.Count} hatalı deneme · {_kaynaklar.Count} şüpheli kaynak ({yuksek} yüksek risk).", _kaynaklar.Count > 0 || okunamayan > 0 ? BildirimTuru.Uyari : BildirimTuru.Basari);
        }
        // Beklenmeyen hata.
        catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Hata: " + ex.Message; Bildirim.Goster("Radar taraması başarısız", ex.Message, BildirimTuru.Hata); }
        // Düğmeyi aç.
        finally { _calisiyor = false; if (!IsDisposed) _tara.Enabled = true; }
    }

    // Windows günlüğü için yönetici izni gerektiğini söyler ve (onayla) programı yönetici olarak yeniden açmayı önerir.
    private void YoneticiOner()
    {
        // Otomatik yenilemede tekrar tekrar sormasın.
        if (_oto.Checked) { _oto.Checked = false; }
        // Onay (varsayılan Hayır).
        if (TemaliMesaj.Goster("Windows Güvenlik günlüğünü okumak için programın YÖNETİCİ olarak çalışması gerekir.\r\n\r\nProgramı yönetici izniyle (UAC) yeniden açayım mı? Oturumunuz kapanır, giriş ekranı yeniden gelir.\r\n\r\n(\"Hayır\" derseniz yalnızca sensör SSH günlüğü kullanılabilir.)", "Yönetici İzni Gerekli", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Yönetici olarak başlat ve çık.
        try { Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--oturum-degisti") { UseShellExecute = true, Verb = "runas" }); ServisBaglami.Oturum.Aktif = null; Application.Exit(); }
        // UAC reddedildiyse hiçbir şey olmaz.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "BruteForceFormu.YoneticiOner"); }
    }

    // Tabloyu doldurur.
    private void Doldur(HashSet<string> engelli)
    {
        // Temizle ve yaz.
        _t.Rows.Clear();
        var kayit = EngelKaydi.Oku();
        // Ömrü dolmuş engel sayısı düğmede görünür (kullanıcı unutmasın; kaldırma yine onayla yapılır).
        int eskiSayi = EngelKaydi.Eskiler(kayit, engelli, DateTime.UtcNow).Count; _eski.Text = eskiSayi > 0 ? $"⌛ Eskileri Kaldır ({eskiSayi})" : "⌛ Eskileri Kaldır";
        foreach (var k in _kaynaklar)
        {
            // Satır.
            bool eng = engelli.Contains(BruteForce.KuralAdi(k.Ip)); int i = _t.Rows.Add(k.Seviye, k.Ip, k.Sayi, k.KullaniciSayisi, string.Join(", ", k.Kullanicilar), string.Join("+", k.Kaynaklar), $"{k.Ilk:HH:mm:ss} / {k.Son:HH:mm:ss}", eng ? EngelKaydi.Metin(EngelKaydi.Yas(k.Ip, kayit, DateTime.UtcNow)) : "-"); _t.Rows[i].Tag = k.Ip;
            // Renkler.
            _t.Rows[i].Cells[0].Style.ForeColor = k.Seviye == "Yüksek" ? DesignTokens.Ozgun(TokenRol.StatusDanger) : DesignTokens.Ozgun(TokenRol.StatusWarning); if (eng) _t.Rows[i].Cells[7].Style.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess);
        }
    }

    // Seçili IP'leri engeller (onay + UAC).
    // Ömrü dolmuş (30 günden eski) engelleri, kullanıcı onaylarsa güvenlik duvarından kaldırır. Kaydı olmayan engele dokunulmaz.
    private async Task EskileriKaldirAsync()
    {
        // Yetki.
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)) { TemaliMesaj.Goster("\"Eski Engelleri Kaldırma\" özelliği için yetkiniz yok.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Eski engelleri bul.
        List<string> eski; try { var kurallar = await Task.Run(BruteForce.EngelliKurallar); eski = EngelKaydi.Eskiler(EngelKaydi.Oku(), kurallar, DateTime.UtcNow); } catch (Exception ex) { _durum.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = "Engeller okunamadı: " + ex.Message; return; }
        // Yoksa bilgi ver.
        if (eski.Count == 0) { TemaliMesaj.Goster($"{EngelKaydi.VarsayilanGun} günden eski, kayıtlı engel yok. (Bu program öncesinde konmuş engellerin yaşı bilinmediği için onlara dokunulmaz.)", "Eski engel yok", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay (varsayılan Hayır).
        if (TemaliMesaj.Goster($"{EngelKaydi.VarsayilanGun} günden eski şu engeller Windows Güvenlik Duvarı'ndan KALDIRILACAK:\r\n\r\n{string.Join("\r\n", eski)}\r\n\r\nBu adresler yeniden bağlanabilir. Windows sizden yönetici izni (UAC penceresi) isteyecek. Devam edilsin mi?", "Eski Engelleri Kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Kaldır.
        _eski.Enabled = false; var (ok, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(GuvenlikDuvari.SilZinciri(eski.Select(BruteForce.KuralAdi))); _eski.Enabled = true;
        // Sonuç.
        if (ok) { try { EngelKaydi.Cikar(eski); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "BruteForceFormu.EskileriKaldirAsync"); } _gunluk($"GÜVENLİK DUVARI -> Eski IP engelleri kaldırıldı: {string.Join(", ", eski)}"); }
        try { Doldur(await Task.Run(BruteForce.EngelliKurallar)); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "BruteForceFormu.EskileriKaldirAsync"); }
        _durum.ForeColor = ok ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = ok ? $"{eski.Count} eski engel için kaldırma komutu çalıştırıldı. Durum sütununu kontrol edin." : "Kaldırılamadı: " + hata;
    }

    private async Task EngelleAsync()
    {
        // Yalnızca Yönetici güvenlik duvarını değiştirebilir.
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir)) { TemaliMesaj.Goster("\"IP Engelleme\" özelliği için yetkiniz yok.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Seçili satırlar.
        var secili = _t.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as string).Where(x => x != null).Cast<string>().Distinct().ToList(); if (secili.Count == 0) { TemaliMesaj.Goster("Önce tablodan engellenecek IP'leri seçin (Ctrl ile birden fazla).", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Korunan adresler: bu bilgisayar ve kayıtlı sensörler.
        var korunan = BruteForce.YerelAdresler(); foreach (var s in _sensorler) korunan.Add(s.Host);
        // Engellenebilenler ve reddedilenler.
        var uygun = secili.Where(i => BruteForce.EngelHatasi(i, korunan) == null).ToList(); var red = secili.Except(uygun).Select(i => $"{i} ({BruteForce.EngelHatasi(i, korunan)})").ToList();
        // Hiçbiri uygun değilse bildir.
        if (uygun.Count == 0) { TemaliMesaj.Goster("Seçilen adresler engellenemez:\r\n" + string.Join("\r\n", red), "Engellenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Özel ağ adresi uyarısı.
        var ozel = uygun.Where(BruteForce.OzelAg).ToList(); string ek = ozel.Count > 0 ? $"\r\n\r\nDİKKAT: {string.Join(", ", ozel)} kendi yerel ağınızdan bir adres. Engellerseniz o cihaz bu bilgisayara hiçbir şekilde bağlanamaz." : ""; var kamu = uygun.Where(i => !BruteForce.OzelAg(i)).ToList(); if (kamu.Count > 0) ek += "\r\n\r\nDİKKAT (ortak adres riski): " + string.Join(", ", kamu) + " internetteki bir adres. Bir ofisin, kafenin ya da VPN'in ortak çıkış adresi (NAT) olabilir; engellerseniz o adresin ARKASINDAKİ HERKES (meşru çalışanlar dahil) bu bilgisayara bağlanamaz. Emin değilseniz engellemeyin.";
        // Onay (varsayılan Hayır).
        if (TemaliMesaj.Goster($"Şu IP adreslerinden bu bilgisayara gelen TÜM bağlantılar Windows Güvenlik Duvarı'nda engellenecek:\r\n\r\n{string.Join("\r\n", uygun)}{ek}" + (red.Count > 0 ? "\r\n\r\nAtlanan: " + string.Join(", ", red) : "") + "\r\n\r\nWindows yönetici izni (UAC) istenecek. Kurallar \"Aktif Kalkanlarım\" penceresinden kaldırılabilir.\r\n\r\nEngellensin mi?", "IP Engelle", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Komut.
        _engel.Enabled = false; var (ok, hata) = await GuvenlikDuvari.YoneticiIleCalistirAsync(BruteForce.EngelZinciri(uygun, korunan)); _engel.Enabled = true;
        // Sonuç.
        Doldur(BruteForce.EngelliKurallar()); _durum.ForeColor = ok ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = ok ? $"{uygun.Count} IP için engel komutu çalıştırıldı. Durum sütununu kontrol edin." : "Engellenemedi: " + hata;
        // Günlük.
        if (ok) { try { EngelKaydi.Ekle(uygun, DateTime.UtcNow); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "BruteForceFormu.EngelleAsync"); } _gunluk($"GÜVENLİK DUVARI -> Saldırgan IP engellendi: {string.Join(", ", uygun)}"); }
    }
}
