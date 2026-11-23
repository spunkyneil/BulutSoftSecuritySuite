// Bu dosya "Hesabım ve Kullanıcılar" penceresini çalıştırır.
// Herkes: kendi parolasını değiştirebilir. Yönetici ayrıca: kullanıcı ekler, rol verir, parola sıfırlar, hesabı kapatır/siler,
// kurulum şifresini değiştirir. Yönetici işlemlerinin hepsi KURULUM ŞİFRESİ ister.
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Hesap ve kullanıcı yönetimi penceresi.
internal sealed class KullaniciFormu : TemaliForm
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Tablo ve durum yazısı.
    private readonly DataGridView _t = new(); private readonly Label _durum = new();
    // Etkinlik günlüğü: seçili kullanıcının programda yaptıkları (Olay Zaman Çizgisi'nde gösterilmez).
    private readonly DataGridView _gunluk = new(); private readonly Label _gunlukBaslik = new();
    // Giriş yapan kullanıcının adı.
    private readonly string _ben = ServisBaglami.Oturum.Aktif?.Ad ?? "";
    // Yönetici mi?
    private readonly bool _yonetici = ServisBaglami.Oturum.Aktif?.Rol == Rol.Yonetici;

    // Pencereyi kurar.
    public KullaniciFormu()
    {
        // Pencere.
        Text = "Hesabım ve Kullanıcılar"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(900, 780); MinimumSize = new Size(820, 660); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        // Hesap özeti.
        var r = ServisBaglami.Oturum.Aktif?.Rol ?? Rol.Izleyici; var ust = new Label { Dock = DockStyle.Top, Height = 78, Padding = new Padding(14, 10, 0, 0), UseMnemonic = false, Text = $"Giriş yapan: {_ben}   ·   Rol: {Yetki.RolAdi(r)}\r\n{Yetki.RolAciklama(r)}", ForeColor = Yazi, Font = FontProvider.Al(FontRol.Subheading, FontStyle.Regular) };
        // Kendi parola düğmesi.
        var kendi = Dg("Parolamı Değiştir", 180); kendi.Click += (_, _) => ParolamiDegistir();
        // Üst düğme şeridi.
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(10, 4, 0, 0) }; serit.Controls.Add(kendi);
        // Yönetici bölümü.
        var govde = new Panel { Dock = DockStyle.Fill };
        // Durum.
        _durum.Dock = DockStyle.Bottom; _durum.Height = 40; _durum.Padding = new Padding(14, 8, 0, 0); _durum.ForeColor = Soluk; _durum.UseMnemonic = false;
        // Yöneticiyse tabloyu ve işlem düğmelerini kur.
        if (_yonetici)
        {
            // Düğmeler.
            var ekle = Dg("Kullanıcı Ekle", 150); var rol = Dg("Rol Değiştir", 140); var sif = Dg("Parola Sıfırla", 150); var dev = Dg("Kapat / Aç", 130); var sil = Dg("Sil", 80); var ks = Dg("Kurulum Şifresini Değiştir", 250);
            // Tıklamalar.
            ekle.Click += (_, _) => Ekle(); rol.Click += (_, _) => RolDegistir(); sif.Click += (_, _) => Sifirla(); dev.Click += (_, _) => Devre(); sil.Click += (_, _) => Sil(); ks.Click += (_, _) => KurulumDegistir();
            // Şeride ekle.
            foreach (var b in new[] { ekle, rol, sif, dev, sil, ks }) serit.Controls.Add(b); serit.Height = 90;
            // Tablo.
            _t.Dock = DockStyle.Fill; _t.ReadOnly = true; _t.AllowUserToAddRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = false; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = Kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false;
            // Renkler.
            _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; _t.DefaultCellStyle.BackColor = Kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.RowTemplate.Height = 32;
            // Sütunlar.
            foreach (var (ad, b, a) in new[] { ("Ad", "Kullanıcı", 24), ("Rol", "Rol", 16), ("Durum", "Durum", 22), ("Tarih", "Oluşturulma", 20) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; }
            // Koyu kaydırma çubukları.
            KoyuTema.UygulaTablo(_t); govde.Controls.Add(_t); Doldur();
            // Not.
            _durum.Text = "Yönetici işlemleri KURULUM ŞİFRESİ ister. Son aktif yönetici silinemez/kapatılamaz/düşürülemez; kendi hesabınızı kapatamaz veya silemezsiniz.";
        }
        // Yönetici değilse açıklama.
        else govde.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(14, 14, 0, 0), ForeColor = Soluk, UseMnemonic = false, Text = "Kullanıcı ekleme, rol verme ve parola sıfırlama yalnızca Yönetici hesabıyla yapılır.\r\nYetki için bir Yönetici'ye başvurun." });
        // Etkinlik günlüğü paneli (alt): yalnızca SEÇİLİ kullanıcıya ait kayıtlar; yönetici tablodan başka kullanıcı seçince o kullanıcının günlüğü gelir.
        _gunlukBaslik.Dock = DockStyle.Top; _gunlukBaslik.Height = 30; _gunlukBaslik.Padding = new Padding(14, 8, 0, 0); _gunlukBaslik.ForeColor = Mavi; _gunlukBaslik.Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold); _gunlukBaslik.UseMnemonic = false;
        _gunluk.Dock = DockStyle.Fill; _gunluk.ReadOnly = true; _gunluk.AllowUserToAddRows = false; _gunluk.RowHeadersVisible = false; _gunluk.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _gunluk.MultiSelect = false; _gunluk.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _gunluk.BorderStyle = BorderStyle.None; _gunluk.EnableHeadersVisualStyles = false;
        _gunluk.BackgroundColor = Kutu; _gunluk.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _gunluk.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _gunluk.ColumnHeadersDefaultCellStyle.ForeColor = Mavi; _gunluk.DefaultCellStyle.BackColor = Kutu; _gunluk.DefaultCellStyle.ForeColor = Yazi; _gunluk.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _gunluk.DefaultCellStyle.SelectionForeColor = Yazi;
        _gunluk.Columns.Add("Zaman", "Zaman"); _gunluk.Columns.Add("Olay", "Yaptığı işlem"); _gunluk.Columns[0].FillWeight = 24; _gunluk.Columns[1].FillWeight = 100; KoyuTema.UygulaTablo(_gunluk);
        var gunlukPanel = new Panel { Dock = DockStyle.Bottom, Height = 270 }; gunlukPanel.Controls.Add(_gunluk); gunlukPanel.Controls.Add(_gunlukBaslik);
        if (_yonetici) _t.SelectionChanged += (_, _) => GunlukYukle(_t.SelectedRows.Count > 0 ? _t.SelectedRows[0].Tag as string : null); else GunlukYukle(_ben);
        // Dock sırası: gövde, günlük, durum, şerit, üst.
        Controls.Add(govde); Controls.Add(gunlukPanel); Controls.Add(_durum); Controls.Add(serit); Controls.Add(ust);
        if (_yonetici && _t.Rows.Count > 0) GunlukYukle(_t.SelectedRows.Count > 0 ? _t.SelectedRows[0].Tag as string : _ben);
    }

    // Seçili kullanıcının etkinlik günlüğünü (eylemleri + oturum olayları, en yeniden eskiye) yükler.
    private void GunlukYukle(string? ad)
    {
        _gunluk.Rows.Clear();
        if (string.IsNullOrWhiteSpace(ad)) { _gunlukBaslik.Text = "Etkinlik günlüğü: bir kullanıcı seçin"; return; }
        var kayitlar = KullaniciGunlugu.Oku(ad);
        _gunlukBaslik.Text = $"Etkinlik günlüğü: {ad}  ·  {kayitlar.Count} kayıt (yalnızca bu kullanıcıya ait; Olay Zaman Çizgisi'nde görünmez)";
        foreach (var o in kayitlar) _gunluk.Rows.Add(o.Zaman.ToString("dd.MM.yyyy HH:mm:ss"), KullaniciGunlugu.Metin(o, ad));
    }

    // Düğme üretir.
    private static Button Dg(string t, int g) => new EfektliButon() { Text = t, Size = new Size(g, 34), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 4, 8, 4) };

    // Tabloyu doldurur.
    private void Doldur()
    {
        // Temizle ve yaz.
        _t.Rows.Clear(); foreach (var k in KimlikDeposu.Liste().OrderBy(x => x.Ad)) { int i = _t.Rows.Add(k.Ad + (k.Ad == _ben ? "  (siz)" : ""), Yetki.RolAdi(k.Rol), k.Devre ? "Devre dışı" : k.KilitBitis > DateTime.Now ? "Geçici kilitli" : "Aktif", k.Olusturma.ToString("dd.MM.yyyy HH:mm")); _t.Rows[i].Tag = k.Ad; if (k.Devre) _t.Rows[i].DefaultCellStyle.ForeColor = Soluk; }
    }

    // Seçili kullanıcının adı (yoksa uyarır).
    private string? Secili()
    {
        // Seçim yoksa.
        if (_t.SelectedRows.Count == 0 || _t.SelectedRows[0].Tag is not string ad) { TemaliMesaj.Goster("Önce tablodan bir kullanıcı seçin.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
        // Ad.
        return ad;
    }

    // İşlem sonucunu gösterir.
    private void Sonuc(IslemSonucu r) { _durum.ForeColor = r.Basarili ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = r.Mesaj; if (r.Basarili) Doldur(); }

    // Rol seçeneklerinin yazıları.
    private static readonly string[] RolYazilari = { "İzleyici — yalnızca görür", "Analist — tarar ve araçları kullanır", "Yönetici — her şey" };
    // Yazıdan rol.
    private static Rol RolDan(string s) => s.StartsWith("Yönetici") ? Rol.Yonetici : s.StartsWith("Analist") ? Rol.Analist : Rol.Izleyici;

    // Kendi parolasını değiştirir.
    private void ParolamiDegistir()
    {
        // Sor.
        var v = SoruFormu.Sor(this, "Parolamı Değiştir", "Yeni parola en az 8 karakter, en az bir harf ve bir rakam içermeli.", "Değiştir", new Alan("Mevcut Parolanız", true), new Alan("Yeni Parola", true), new Alan("Yeni Parola (Tekrar)", true)); if (v == null) return;
        // Tekrar.
        if (v[1] != v[2]) { TemaliMesaj.Goster("Yeni parola iki kutuda aynı değil.", "Parola", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Değiştir.
        var r = KimlikDeposu.SifreDegistir(_ben, v[0], v[1]); _durum.ForeColor = r.Basarili ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.StatusDanger); _durum.Text = r.Mesaj; if (!r.Basarili) TemaliMesaj.Goster(r.Mesaj, "Parola", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // Yeni kullanıcı ekler.
    private void Ekle()
    {
        // Sor.
        var v = SoruFormu.Sor(this, "Kullanıcı Ekle", "Kullanıcı adı: küçük harf/rakam/_/./- (3-32). Parola en az 8 karakter, harf+rakam.", "Ekle", new Alan("Kullanıcı Adı"), new Alan("Parola", true), new Alan("Parola (Tekrar)", true), new Alan("Rol", false, RolYazilari), new Alan("Kurulum Şifresi (Yönetici İşlemi İçin)", true)); if (v == null) return;
        // Tekrar.
        if (v[1] != v[2]) { Sonuc(new(false, "Parola iki kutuda aynı değil.")); return; }
        // Ekle.
        Sonuc(KimlikDeposu.KullaniciEkle(_ben, v[4], v[0], v[1], RolDan(v[3])));
    }

    // Rol değiştirir.
    private void RolDegistir()
    {
        // Seçili.
        var ad = Secili(); if (ad == null) return; if (ad == _ben) { Sonuc(new(false, "Kendi rolünüzü değiştiremezsiniz (yetkinizi yanlışlıkla kaybetmemeniz için).")); return; }
        // Sor.
        var v = SoruFormu.Sor(this, "Rol Değiştir: " + ad, "", "Değiştir", new Alan("Yeni Rol", false, RolYazilari), new Alan("Kurulum Şifresi", true)); if (v == null) return;
        // Değiştir.
        Sonuc(KimlikDeposu.RolDegistir(_ben, v[1], ad, RolDan(v[0])));
    }

    // Parola sıfırlar.
    private void Sifirla()
    {
        // Seçili.
        var ad = Secili(); if (ad == null) return;
        // Sor.
        var v = SoruFormu.Sor(this, "Parola Sıfırla: " + ad, "Kullanıcıya yeni parolayı güvenli bir yolla iletin; ilk girişten sonra kendi parolasını değiştirmesini söyleyin.", "Sıfırla", new Alan("Yeni Parola", true), new Alan("Yeni Parola (Tekrar)", true), new Alan("Kurulum Şifresi", true)); if (v == null) return;
        // Tekrar.
        if (v[0] != v[1]) { Sonuc(new(false, "Parola iki kutuda aynı değil.")); return; }
        // Sıfırla.
        Sonuc(KimlikDeposu.SifreSifirla(_ben, v[2], ad, v[0]));
    }

    // Hesabı kapatır veya açar.
    private void Devre()
    {
        // Seçili.
        var ad = Secili(); if (ad == null) return; if (ad == _ben) { Sonuc(new(false, "Kendi hesabınızı kapatamazsınız.")); return; }
        // Mevcut durum.
        bool kapali = KimlikDeposu.Liste().FirstOrDefault(k => k.Ad == ad)?.Devre ?? false;
        // Onay ve kurulum şifresi.
        var v = SoruFormu.Sor(this, (kapali ? "Hesabı Aç: " : "Hesabı Kapat: ") + ad, kapali ? "Hesap yeniden giriş yapabilir hâle gelir." : "Hesap giriş yapamaz hâle gelir (silinmez; sonra açılabilir).", kapali ? "Aç" : "Kapat", new Alan("Kurulum Şifresi", true)); if (v == null) return;
        // Uygula.
        Sonuc(KimlikDeposu.DevreDegistir(_ben, v[0], ad, !kapali));
    }

    // Kullanıcıyı siler.
    private void Sil()
    {
        // Seçili.
        var ad = Secili(); if (ad == null) return; if (ad == _ben) { Sonuc(new(false, "Kendi hesabınızı silemezsiniz.")); return; }
        // Onay (varsayılan Hayır).
        if (TemaliMesaj.Goster($"\"{ad}\" kullanıcısı KALICI olarak silinsin mi?\r\n\r\nGeri alınamaz. (Geçici olarak kapatmak için \"Kapat / Aç\"ı kullanın.)", "Kullanıcıyı Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Kurulum şifresi.
        var v = SoruFormu.Sor(this, "Silmeyi Onayla", "", "Sil", new Alan("Kurulum Şifresi", true)); if (v == null) return;
        // Sil.
        Sonuc(KimlikDeposu.Sil(_ben, v[0], ad));
    }

    // Kurulum şifresini değiştirir.
    private void KurulumDegistir()
    {
        // Sor.
        var v = SoruFormu.Sor(this, "Kurulum Şifresini Değiştir", "Yeni kurulum şifresi en az 8 karakter, harf+rakam içermeli ve hiçbir hesap parolasıyla aynı olmamalı. Unutursanız kurtarma yolu yoktur.", "Değiştir", new Alan("Mevcut Kurulum Şifresi", true), new Alan("Yeni Kurulum Şifresi", true), new Alan("Yeni Kurulum Şifresi (Tekrar)", true)); if (v == null) return;
        // Tekrar.
        if (v[1] != v[2]) { Sonuc(new(false, "Yeni kurulum şifresi iki kutuda aynı değil.")); return; }
        // Değiştir.
        Sonuc(KimlikDeposu.KurulumSifresiDegistir(_ben, v[0], v[1]));
    }
}
