// Bu dosya "Gözetimsiz Politika" penceresini çizer: hangi sınıfların (süreli IP engeli, yeniden doğrulama vb.) kimsenin başında olmadan çalışabileceğini yönetir.
// İLKELER: her sınıf varsayılan KAPALI; etkinleştirmek yalnızca Yönetici + KURULUM ŞİFRESİ + açık onay (varsayılan "Hayır") ister; ilk 7 gün yalnızca "ne yapacağını yazar" (kuru çalıştırma);
// kapatmak her an tek tıkla; her değişiklik zincirleme özetli DENETİM İZİNE yazılır. Bu pencere SİSTEMİ DEĞİŞTİRMEZ: yalnızca politikayı yönetir ve kararı ÖNİZLER.
// Dürüstlük: bu sürümde yönetici yetkisi isteyen eylemleri gözetimsiz çalıştıracak "Yükseltilmiş Yardımcı" KURULU DEĞİLDİR; bu yüzden UAC'li sınıflar etkinleştirilse bile engellenir.
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Pencere metinleri (test edilebilsin diye ayrı, saf işlevler).
internal static class GozetimsizMetin
{
    // Sınıfın durum yazısı ve vurgu: 0 = nötr, 1 = iyi, 2 = uyarı.
    public static (string Yazi, int Vurgu) Durum(UnattendedPolicy p, string sinif, DateTime simdi)
    {
        var a = p.Ayar(sinif); if (a == null || !a.Etkin) return ("Kapalı", 0);
        if (UnattendedPolicy.UacGerekir(sinif) && !p.YardimciKayitli) return ("Etkin ama ENGELLİ: yükseltilmiş yardımcı kurulu değil", 2);
        int kalan = p.KuruKalanGun(sinif, simdi); if (kalan > 0) return ($"Etkin · kuru çalıştırma dönemi ({kalan} gün kaldı: yalnızca plan yazar)", 2);
        return ("Etkin · gerçek çalışma", 1);
    }

    // Kararın sade açıklaması.
    public static string Karar(GozetimsizKarar k) => k switch
    {
        GozetimsizKarar.Kapali => "KAPALI: bu sınıf etkinleştirilmemiş; hiçbir şey otomatik yapılmaz, her seferinde onay istenir.",
        GozetimsizKarar.KuruCalistirma => "KURU ÇALIŞTIRMA: sınıf etkin ama ilk 7 gün dolmadı; eylem YAPILMAZ, yalnızca ne yapacağı denetim izine yazılır.",
        GozetimsizKarar.Izinli => "İZİNLİ: koşullar uygun; bu durumda eylem gözetimsiz yapılabilirdi (önizleme: hiçbir şey yapılmadı).",
        GozetimsizKarar.HizSiniri => "HIZ SINIRI: son 1 saatte izin verilen eylem sayısına ulaşıldı; yeni eylem yapılmaz.",
        GozetimsizKarar.GozdenGecirmeGerekli => "GÖZDEN GEÇİRİLMELİ: bu kuralın yanlış alarm oranı yüksek olduğu için gözetimsizlikten çıkarıldı.",
        GozetimsizKarar.YardimciYok => "ENGELLİ: bu eylem yönetici yetkisi (UAC) ister ve bu sürümde yükseltilmiş yardımcı kurulu değil; gözetimsiz yapılamaz.",
        _ => "BİLİNMEYEN sınıf: gözetimsiz çalışamaz."
    };

    // Gözetimsiz OLAMAYACAK işlemler (her zaman onaylı).
    public static readonly string[] HerZamanOnayli = { "Acil İzolasyon", "Ağ kartını kapatma", "Süreç sonlandırma", "Hizmet durdurma", "Kayıt defteri güvenlik ayarları", "hosts/DNS değişikliği", "Dosya silme / karantina" };
}

// Pencere.
internal sealed class GozetimsizFormu : SayfaDenetimi
{
    // Kontroller.
    private readonly DataGridView _t = new(); private readonly Label _ust = new(), _ayr = new(), _uyari = new(), _butunluk = new(); private readonly TextBox _gunluk = new();
    private readonly Button _ac = new EfektliButon(), _kapat = new EfektliButon(), _onizle = new EfektliButon(), _yenile = new EfektliButon();
    // Veri.
    private UnattendedPolicy _pol; private readonly AuditTrail _iz; private readonly Action<string> _olay;
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted);

    public GozetimsizFormu(Action<string> olay)
    {
        _olay = olay;
        // Kalıcı denetim izi ve politika (yanlış alarm takipçisiyle).
        _iz = new AuditTrail(BulutKlasor.Dosya("denetim_izi.jsonl")); _pol = UnattendedPolicy.Yukle(FpDeposu.Yukle().Takipci(), _iz);
        Text = "Gözetimsiz Politika"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(1100, 700); MinimumSize = new Size(900, 560); BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body);
        _ust.Dock = DockStyle.Top; _ust.Height = 92; _ust.Padding = new Padding(14, 10, 14, 0); _ust.ForeColor = Soluk; _ust.UseMnemonic = false;
        _ust.Text = "Burada, kimsenin başında olmadan kendiliğinden yapılabilecek işleri YÖNETİRSİNİZ. Her sınıf varsayılan KAPALIDIR. Etkinleştirmek için Yönetici hesabı ve kurulum şifresi gerekir; ilk 7 gün yalnızca \"ne yapacağını yazar\" (kuru çalıştırma). Bu pencere sistemde hiçbir şeyi DEĞİŞTİRMEZ.\r\nBu sürümde yönetici yetkisi isteyen eylemleri gözetimsiz çalıştıracak \"Yükseltilmiş Yardımcı\" kurulu DEĞİLDİR; bu yüzden UAC isteyen sınıflar etkinleştirilse bile engellenir.";
        _uyari.Dock = DockStyle.Top; _uyari.Height = 0; _uyari.Padding = new Padding(14, 6, 14, 0); _uyari.ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning); _uyari.UseMnemonic = false;
        foreach (var b in new[] { _ac, _kapat, _onizle, _yenile }) { b.FlatStyle = FlatStyle.Flat; b.BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary); b.ForeColor = Color.White; b.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); b.Cursor = Cursors.Hand; b.UseMnemonic = false; b.Margin = new Padding(0, 0, 8, 0); b.Size = new Size(190, 30); }
        _ac.Text = "Etkinleştir..."; _kapat.Text = "Kapat (tek tık)"; _kapat.BackColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _onizle.Text = "Kararı Önizle"; _yenile.Text = "Yenile"; _yenile.Size = new Size(100, 30);
        var serit = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(10, 6, 0, 0), WrapContents = false }; serit.Controls.AddRange(new Control[] { _ac, _kapat, _onizle, _yenile });
        _t.Dock = DockStyle.Top; _t.Height = 150; _t.ReadOnly = true; _t.AllowUserToAddRows = false; _t.RowHeadersVisible = false; _t.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _t.MultiSelect = false; _t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; _t.BackgroundColor = Kutu; _t.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.BorderStyle = BorderStyle.None; _t.EnableHeadersVisualStyles = false; _t.RowTemplate.Height = 32;
        _t.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); _t.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); _t.DefaultCellStyle.BackColor = Kutu; _t.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); _t.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        foreach (var (ad, b, a) in new[] { ("S", "Sınıf", 34), ("D", "Durum", 36), ("H", "Saatte en çok", 9), ("O", "Ömür (sa)", 8), ("K", "Son 1 saat", 8), ("U", "Yönetici yetkisi", 10) }) { _t.Columns.Add(ad, b); _t.Columns[ad]!.FillWeight = a; }
        KoyuTema.UygulaTablo(_t);
        _ayr.Dock = DockStyle.Top; _ayr.Height = 112; _ayr.Padding = new Padding(14, 8, 14, 0); _ayr.ForeColor = Yazi; _ayr.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _ayr.UseMnemonic = false;
        _butunluk.Dock = DockStyle.Top; _butunluk.Height = 62; _butunluk.Padding = new Padding(14, 6, 14, 0); _butunluk.ForeColor = Soluk; _butunluk.UseMnemonic = false;
        _gunluk.Dock = DockStyle.Fill; _gunluk.Multiline = true; _gunluk.ReadOnly = true; _gunluk.ScrollBars = ScrollBars.Vertical; _gunluk.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceSidebar); _gunluk.ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary); _gunluk.Font = FontProvider.Al(FontRol.Mono); _gunluk.BorderStyle = BorderStyle.None; _gunluk.TabStop = false;
        Controls.Add(_gunluk); Controls.Add(_butunluk); Controls.Add(_ayr); Controls.Add(_t); Controls.Add(serit); Controls.Add(_uyari); Controls.Add(_ust);
        _t.SelectionChanged += (_, _) => Aciklama(); _ac.Click += (_, _) => Etkinlestir(); _kapat.Click += (_, _) => Kapat(); _onizle.Click += (_, _) => Onizle(); _yenile.Click += (_, _) => Doldur();
        Doldur();
    }

    // Seçili sınıf adı.
    private string? Secili => _t.CurrentRow?.Tag as string;

    // Tabloyu, uyarıları ve günlüğü yeniler.
    private void Doldur()
    {
        string? secili = Secili; var simdi = DateTime.UtcNow; _pol = UnattendedPolicy.Yukle(FpDeposu.Yukle().Takipci(), _iz);
        _uyari.Text = _pol.YukleUyarisi ?? ""; _uyari.Height = string.IsNullOrEmpty(_pol.YukleUyarisi) ? 0 : 40;
        _t.Rows.Clear();
        foreach (var s in UnattendedPolicy.IzinliSiniflar)
        {
            var (yazi, vurgu) = GozetimsizMetin.Durum(_pol, s, simdi); var a = _pol.Ayar(s);
            int i = _t.Rows.Add(UnattendedPolicy.Bilgi(s).Ad, yazi, a?.SaatteEnCok.ToString() ?? "—", a?.OmurSaat.ToString() ?? "—", a == null ? "—" : _pol.SonSaatKullanim(s, simdi).ToString(), UnattendedPolicy.UacGerekir(s) ? "gerekir" : "gerekmez"); _t.Rows[i].Tag = s;
            _t.Rows[i].Cells["D"].Style.ForeColor = vurgu == 1 ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : vurgu == 2 ? DesignTokens.Ozgun(TokenRol.StatusWarning) : DesignTokens.Ozgun(TokenRol.TextMuted);
            if (s == secili) _t.CurrentCell = _t.Rows[i].Cells[0];
        }
        // Denetim izi bütünlüğü ve son kayıtlar.
        var (saglam, bozuk) = AuditTrail.Dogrula(_iz.Kayitlar); _butunluk.ForeColor = saglam ? Soluk : DesignTokens.Ozgun(TokenRol.StatusDanger);
        _butunluk.Text = $"Denetim izi: {_iz.Kayitlar.Count} kayıt · zincir " + (saglam ? "sağlam" : $"BOZUK (kayıt {bozuk}) — dosyayla oynanmış olabilir") + "\r\nGözetimsiz OLAMAYACAKLAR (her zaman onaylı): " + string.Join(", ", GozetimsizMetin.HerZamanOnayli) + ".";
        _gunluk.Text = _iz.Kayitlar.Count == 0 ? "Henüz kayıt yok. Politika değişiklikleri, kuru çalıştırma planları ve engellenen eylemler burada görünür." : string.Join("\r\n", _iz.Kayitlar.Reverse().Take(80).Select(k => $"{k.Time.ToLocalTime():dd.MM HH:mm:ss}  {k.Actor,-18} {k.Action,-16} {k.Detail}"));
        _gunluk.SelectionStart = 0; _gunluk.SelectionLength = 0; Aciklama();
    }

    // Seçili sınıfın açıklaması.
    private void Aciklama()
    {
        if (Secili is not string s) { _ayr.Text = "Bir sınıf seçin."; return; }
        var (ad, ac) = UnattendedPolicy.Bilgi(s);
        _ayr.Text = $"{ad}\r\n{ac}\r\n" + (UnattendedPolicy.UacGerekir(s) ? "Bu eylem yönetici yetkisi (UAC) ister: gözetimsiz çalışması için yükseltilmiş yardımcı gerekir (bu sürümde YOK)." : "Bu eylem yönetici yetkisi istemez.") + "\r\nHız sınırı ve ömür etkinleştirirken seçilir. Yanlış alarm oranı yüksek bir kural gözetimsizlikten kendiliğinden çıkar.";
    }

    // Yönetici + kurulum şifresi + açık onayla sınıfı etkinleştirir.
    private void Etkinlestir()
    {
        if (Secili is not string s) { TemaliMesaj.Goster(this, "Önce listeden bir sınıf seçin.", "Gözetimsiz Politika", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Yonetim)) { TemaliMesaj.Goster(this, "Gözetimsiz bir sınıfı yalnızca YÖNETİCİ etkinleştirebilir.\r\n\r\n" + Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Yonetim), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var (ad, ac) = UnattendedPolicy.Bilgi(s);
        if (TemaliMesaj.Goster(this, $"\"{ad}\" gözetimsiz çalışabilecek.\r\n\r\n{ac}\r\n\r\n• İlk 7 gün yalnızca plan yazılır (kuru çalıştırma), hiçbir şey yapılmaz.\r\n• İstediğiniz an \"Kapat\" ile durdurabilirsiniz.\r\n• Her değişiklik denetim izine yazılır.\r\n" + (UnattendedPolicy.UacGerekir(s) ? "• Bu sürümde yükseltilmiş yardımcı olmadığı için bu sınıf etkin olsa da yönetici gerektiren eylemleri YAPAMAZ.\r\n" : "") + "\r\nEtkinleştirilsin mi?", "Gözetimsiz etkinleştirme", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var c = SoruFormu.Sor(this, "Kurulum şifresi", "Etkinleştirmeyi onaylamak için kurulum şifrenizi girin ve sınırları seçin.", "Etkinleştir", new Alan("Kurulum şifresi", true), new Alan("Saatte en çok eylem", false, new[] { "1", "3", "5", "10" }), new Alan("Eylem ömrü (saat; süreli engel gibi)", false, new[] { "6", "12", "24", "72" }));
        if (c == null) return;
        var r = KimlikDeposu.YoneticiOnayi(ServisBaglami.Oturum.Aktif!.Ad, c[0]); if (!r.Basarili) { TemaliMesaj.Goster(this, r.Mesaj, "Onaylanmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        try { _pol.Etkinlestir(s, ServisBaglami.Oturum.Aktif.Ad, true, DateTime.UtcNow, int.Parse(c[1]), int.Parse(c[2])); _olay($"GÖZETİMSİZ -> \"{ad}\" etkinleştirildi (kuru çalıştırma dönemiyle)."); Bildirim.Goster("Gözetimsiz sınıf etkinleştirildi", ad + " · ilk 7 gün yalnızca plan yazılır.", BildirimTuru.Uyari); }
        catch (Exception ex) { TemaliMesaj.Goster(this, "Etkinleştirilemedi: " + ex.Message, "Gözetimsiz Politika", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        Doldur();
    }

    // Sınıfı kapatır (onay istemez: güvenli yön).
    private void Kapat()
    {
        if (Secili is not string s) return; if (_pol.Ayar(s) == null) { TemaliMesaj.Goster(this, "Bu sınıf zaten kapalı.", "Gözetimsiz Politika", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (!Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Koruma)) { TemaliMesaj.Goster(this, Yetki.RedMesaji(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Koruma), "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        _pol.Kapat(s, ServisBaglami.Oturum.Aktif?.Ad ?? "bilinmiyor", DateTime.UtcNow); _olay($"GÖZETİMSİZ -> \"{UnattendedPolicy.Bilgi(s).Ad}\" KAPATILDI."); Doldur();
    }

    // Sınıf için şu anki kararı önizler (hiçbir şey çalıştırılmaz) ve denetim izine "dry_run" olarak yazar.
    private void Onizle()
    {
        if (Secili is not string s) return;
        var k = _pol.Karar(s, "ONIZLEME", UnattendedPolicy.UacGerekir(s), DateTime.UtcNow);
        _iz.Ekle(ServisBaglami.Oturum.Aktif?.Ad ?? "bilinmiyor", "dry_run", null, null, UnattendedPolicy.UacGerekir(s), $"önizleme: {s} → {k}", DateTime.UtcNow);
        TemaliMesaj.Goster(this, $"{UnattendedPolicy.Bilgi(s).Ad}\r\n\r\n{GozetimsizMetin.Karar(k)}\r\n\r\n(Önizleme: sistemde hiçbir şey yapılmadı; sonuç denetim izine yazıldı.)", "Karar önizlemesi", MessageBoxButtons.OK, MessageBoxIcon.Information); Doldur();
    }
}
