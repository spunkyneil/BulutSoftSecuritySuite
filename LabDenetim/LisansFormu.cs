// Bu dosya "Lisans" penceresini çizer: lisans durumu, makine kimliği, lisans dosyası yükleme ve (Yönetici için) Kali sanal makinesiyle lisans üretimi.
// Doğrulama çevrimdışıdır; Kali yalnızca lisans ÜRETİRKEN gerekir. Lisans yoksa/bittiyse yalnızca yeni tarama başlatma kısıtlanır.
using System.Text;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Lisans penceresi.
internal sealed class LisansFormu : TemaliForm
{
    // Renkler.
    private static readonly Color Zemin = DesignTokens.Ozgun(TokenRol.SurfaceApp), Kutu = DesignTokens.Ozgun(TokenRol.SurfaceCard), Yazi = DesignTokens.Ozgun(TokenRol.TextPrimary), Soluk = DesignTokens.Ozgun(TokenRol.TextMuted), Mavi = DesignTokens.Ozgun(TokenRol.StatusInfo);
    // Kontroller.
    private readonly Label _durum = new(), _mesaj = new(), _makine = new(), _bilgi = new();
#if INTERNAL_VENDOR_TOOLS
    // DAHİLİ SATICI ARAÇLARI (yalnızca INTERNAL_VENDOR_TOOLS ile derlenen iç sürümde; müşteri sürümünde YOKTUR).
    private readonly ComboBox _sensor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; private readonly TextBox _cikti = new();
#endif
    // Sensörler ve günlük kancası.
#if INTERNAL_VENDOR_TOOLS
    private readonly List<SensorProfili> _sensorler = SensorDeposu.Yukle();
#endif
    private readonly Action<string> _gunluk;

    // Pencereyi kurar.
    public LisansFormu(Action<string> gunluk)
    {
        // Günlük.
        _gunluk = gunluk;
        // Pencere.
        Text = "Lisans"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; 
#if INTERNAL_VENDOR_TOOLS
        ClientSize = new Size(760, 640);
#else
        ClientSize = new Size(760, 290);
#endif
         BackColor = Zemin; ForeColor = Yazi; Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        // Başlık ve durum.
        Controls.Add(new Label { Text = "Lisans", Location = new Point(28, 18), Size = new Size(300, 36), Font = FontProvider.Al(FontRol.Display, FontStyle.Bold), ForeColor = Color.White, UseMnemonic = false });
        _durum.Location = new Point(28, 62); _durum.Size = new Size(700, 30); _durum.Font = FontProvider.Al(FontRol.Heading, FontStyle.Bold); _durum.UseMnemonic = false; Controls.Add(_durum);
        _mesaj.Location = new Point(28, 96); _mesaj.Size = new Size(700, 44); _mesaj.ForeColor = Soluk; _mesaj.UseMnemonic = false; Controls.Add(_mesaj);
        // Makine kimliği ve kopyala.
        Controls.Add(new Label { Text = "Bu bilgisayarın Makine Kimliği (lisans bu kimliğe bağlanır):", Location = new Point(28, 148), Size = new Size(500, 20), ForeColor = Soluk, UseMnemonic = false });
        _makine.Location = new Point(28, 170); _makine.Size = new Size(300, 30); _makine.Font = FontProvider.Al(FontRol.Mono, FontStyle.Bold); _makine.ForeColor = Mavi; _makine.UseMnemonic = false; Controls.Add(_makine);
        var kop = Dg("Kopyala", 90, new Point(340, 170)); kop.Click += (_, _) => { try { Clipboard.SetText(Lisans.MakineKimligi()); _bilgi.Text = "Makine kimliği panoya kopyalandı."; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "LisansFormu.LisansFormu"); } }; Controls.Add(kop);
        // Yükleme.
        var yukle = Dg("Lisans Dosyası Yükle…", 200, new Point(28, 214)); yukle.Click += (_, _) => Yukle(); Controls.Add(yukle);
        _bilgi.Location = new Point(240, 220); _bilgi.Size = new Size(490, 40); _bilgi.ForeColor = Soluk; _bilgi.UseMnemonic = false; Controls.Add(_bilgi);
#if INTERNAL_VENDOR_TOOLS
        // Sahip bölümü (lisans üretimi).
        Controls.Add(new Label { Text = "Sahip Bölümü: Lisans Üretimi (Kali sanal makinesi = lisans otoritesi)", Location = new Point(28, 276), Size = new Size(700, 24), Font = FontProvider.Gecici("Segoe UI Semibold", 11f), ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning), UseMnemonic = false });
        Controls.Add(new Label { Text = "Özel anahtar Kali'den ÇIKMAZ; yalnızca imza atar. Kali kapalıyken bile program ve lisanslar çalışmaya devam eder (doğrulama çevrimdışıdır). Bu bölüm yalnızca Yönetici içindir.", Location = new Point(28, 302), Size = new Size(700, 44), ForeColor = Soluk, UseMnemonic = false });
        Controls.Add(new Label { Text = "Sensör (Kali):", Location = new Point(28, 356), AutoSize = true, ForeColor = Soluk }); foreach (var s in _sensorler) _sensor.Items.Add(s.Ad); if (_sensor.Items.Count > 0) _sensor.SelectedIndex = 0; _sensor.Location = new Point(130, 352); Controls.Add(_sensor);
        var kur = Dg("1) Lisans Otoritesini Kur", 230, new Point(28, 392)); var sahip = Dg("2) Bu Bilgisayar İçin Sahip Lisansı Üret", 320, new Point(268, 392)); var musteri = Dg("3) Müşteri Lisansı Üret…", 230, new Point(28, 432));
        kur.Click += (_, _) => GuvenliCalistir.Baslat(() => OtoriteKurAsync(), "LisansFormu"); sahip.Click += (_, _) => GuvenliCalistir.Baslat(() => SahipLisansiAsync(), "LisansFormu"); musteri.Click += (_, _) => GuvenliCalistir.Baslat(() => MusteriLisansiAsync(), "LisansFormu"); Controls.AddRange(new Control[] { kur, sahip, musteri });
        // Yönetici değilse veya sensör yoksa devre dışı.
        bool yetki = Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.SistemDegistir) && _sensorler.Count > 0; foreach (var b in new[] { kur, sahip, musteri }) b.Enabled = yetki; _sensor.Enabled = yetki;
        if (!yetki) Controls.Add(new Label { Text = _sensorler.Count == 0 ? "Lisans üretmek için önce \"Uzak Sensörler\"de Kali sensörünüzü ekleyin." : "Lisans üretimi yalnızca Yönetici rolüne açıktır.", Location = new Point(270, 440), Size = new Size(460, 40), ForeColor = DesignTokens.Ozgun(TokenRol.StatusWarning), UseMnemonic = false });
        // Çıktı kutusu.
        _cikti.Location = new Point(28, 486); _cikti.Size = new Size(704, 126); _cikti.Multiline = true; _cikti.ReadOnly = true; _cikti.ScrollBars = ScrollBars.Vertical; _cikti.BackColor = Kutu; _cikti.ForeColor = Yazi; _cikti.BorderStyle = BorderStyle.None; _cikti.Font = FontProvider.Al(FontRol.Mono); _cikti.Text = "Sıra: (1) Otoriteyi bir kez kurun → (2) kendi bilgisayarınız için Sahip Lisansı üretin → (3) müşteriler için lisans üretin.\r\nMüşteri, kendi bilgisayarındaki Lisans penceresinden Makine Kimliği'ni size gönderir; siz lisans dosyasını (.bslic) ona yollarsınız.\r\nÖNEMLİ: Kali'deki ~/.bulutsoft/lisans_ozel.pem dosyasının yedeğini alın; kaybederseniz yeni lisans üretemezsiniz (mevcutlar çalışır)."; Controls.Add(_cikti);
#endif
        // Durumu yaz.
        Yenile(); Shown += (_, _) =>
        {
#if INTERNAL_VENDOR_TOOLS
            _cikti.SelectionLength = 0;
#endif
            ActiveControl = null;
        };
    }

    // Düğme üretir.
    private static Button Dg(string t, int g, Point k) => new EfektliButon() { Text = t, Size = new Size(g, 34), Location = k, FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false };

    // Durumu ekrana yazar.
    private void Yenile()
    {
        // Durum.
        var s = Lisans.Durum(); _durum.Text = Lisans.Kisa(s); _durum.ForeColor = s.Durum switch { LisansDurumu.Sahip or LisansDurumu.Lisansli => DesignTokens.Ozgun(TokenRol.StatusSuccess), LisansDurumu.Deneme => DesignTokens.Ozgun(TokenRol.StatusWarning), _ => DesignTokens.Ozgun(TokenRol.StatusDanger) };
        // Mesaj ve kimlik.
        _mesaj.Text = s.Mesaj + (s.Bilgi != null ? $"\r\nMüşteri: {s.Bilgi.Musteri}  ·  Sürüm: {s.Bilgi.Surum}" : ""); _makine.Text = Lisans.MakineKimligi();
    }

    // Lisans dosyası yükler (imza ve makine doğrulanır).
    private void Yukle()
    {
        // Dosya seç.
        using var f = new OpenFileDialog { Filter = "BulutSoft lisansı (*.bslic)|*.bslic|Tüm dosyalar|*.*", Title = "Lisans dosyasını seçin" }; if (f.ShowDialog(this) != DialogResult.OK) return;
        // Oku ve doğrula.
        try
        {
            // Metin (en çok 64 KB).
            var fi = new FileInfo(f.FileName); if (fi.Length > 65536) { _bilgi.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _bilgi.Text = "Dosya çok büyük; geçerli bir lisans değil."; return; } string m = File.ReadAllText(f.FileName);
            // Değerlendir.
            var s = Lisans.Degerlendir(m, Lisans.GenelAnahtar(), Lisans.MakineKimligi(), DateTime.Now);
            // Geçerli değilse kaydetme.
            if (s.Durum is not (LisansDurumu.Sahip or LisansDurumu.Lisansli)) { _bilgi.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _bilgi.Text = "Lisans kabul edilmedi: " + s.Mesaj; return; }
            // Kaydet.
            Depo.Yaz(Lisans.LisansYolu, m); _bilgi.ForeColor = DesignTokens.Ozgun(TokenRol.StatusSuccess); _bilgi.Text = "Lisans yüklendi."; _gunluk("LİSANS -> Lisans dosyası yüklendi: " + Lisans.Kisa(s)); Yenile();
        }
        // Okuma hatası.
        catch (Exception ex) { _bilgi.ForeColor = DesignTokens.Ozgun(TokenRol.StatusDanger); _bilgi.Text = "Okunamadı: " + ex.Message; }
    }

#if INTERNAL_VENDOR_TOOLS
    // Seçili sensör.
    private SensorProfili? Sensor() => _sensor.SelectedIndex >= 0 ? _sensorler[_sensor.SelectedIndex] : null;

    // Çıktıya satır ekler.
    private void Yaz(string s) { if (!IsDisposed) _cikti.AppendText("\r\n" + s); }

    // 1) Otoriteyi kurar.
    private async Task OtoriteKurAsync()
    {
        // Sensör.
        var p = Sensor(); if (p == null) return;
        // Onay: Kali'de dosya oluşturulacak.
        if (TemaliMesaj.Goster($"\"{p.Ad}\" sensöründe (Kali) ~/.bulutsoft klasörüne bir lisans imzalama anahtarı OLUŞTURULACAK (zaten varsa dokunulmaz).\r\n\r\nÖzel anahtar Kali'den çıkmaz. Genel anahtar bu bilgisayara kaydedilir.\r\n\r\nDevam edilsin mi?", "Lisans Otoritesini Kur", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Çalıştır.
        Yaz("Kali'ye bağlanılıyor..."); try { await LisansOtoritesi.KurAsync(p, CancellationToken.None); Yaz("Otorite hazır. Genel anahtar kaydedildi: " + Lisans.OtoriteYolu); _gunluk("LİSANS -> Lisans otoritesi kuruldu."); Yenile(); } catch (Exception ex) { Yaz("HATA: " + ex.Message); }
    }

    // 2) Bu bilgisayar için sahip lisansı üretir ve yükler.
    private async Task SahipLisansiAsync()
    {
        // Sensör.
        var p = Sensor(); if (p == null) return;
        // Onay.
        if (TemaliMesaj.Goster($"Bu bilgisayar ({Lisans.MakineKimligi()}) için SÜRESİZ bir Sahip Lisansı üretilip yüklenecek.\r\n\r\nSahip lisansı, kendi bilgisayarınızın deneme süresine takılmasını engeller. Devam edilsin mi?", "Sahip Lisansı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Üret.
        Yaz("Kali imzalıyor..."); try { var b = new LisansBilgi("Sahip (" + Environment.UserName + ")", Lisans.MakineKimligi(), DateTime.Now.Date, null, "Sahip"); string paket = await LisansOtoritesi.ImzalaAsync(p, b, CancellationToken.None); Depo.Yaz(Lisans.LisansYolu, paket); Yaz("Sahip lisansı üretildi ve yüklendi."); _gunluk("LİSANS -> Sahip lisansı üretildi ve yüklendi."); Yenile(); } catch (Exception ex) { Yaz("HATA: " + ex.Message); }
    }

    // 3) Müşteri lisansı üretir ve dosyaya kaydeder.
    private async Task MusteriLisansiAsync()
    {
        // Sensör.
        var p = Sensor(); if (p == null) return;
        // Bilgiler.
        var v = SoruFormu.Sor(this, "Müşteri Lisansı", "Müşterinin Lisans penceresindeki Makine Kimliği'ni (XXXX-XXXX-XXXX-XXXX) girin.", "Üret", new Alan("Müşteri Adı"), new Alan("Makine Kimliği"), new Alan("Geçerlilik", false, new[] { "30 gün", "90 gün", "1 yıl", "Süresiz" })); if (v == null) return;
        // Doğrulama.
        string ad = new string(v[0].Where(c => !char.IsControl(c)).ToArray()).Trim(), mk = v[1].Trim().ToUpperInvariant();
        if (ad.Length is < 2 or > 60) { Yaz("HATA: Müşteri adı 2-60 karakter olmalı."); return; } if (mk != Lisans.KurumMakinesi && !Regex.IsMatch(mk, "^[0-9A-F]{4}(-[0-9A-F]{4}){3}$")) { Yaz("HATA: Makine kimliği XXXX-XXXX-XXXX-XXXX biçiminde olmalı (kurum geneli lisans için * yazın)."); return; }
        // Süre.
        DateTime? bitis = v[2] switch { "30 gün" => DateTime.Now.Date.AddDays(30), "90 gün" => DateTime.Now.Date.AddDays(90), "1 yıl" => DateTime.Now.Date.AddYears(1), _ => null };
        // Üret.
        Yaz($"Kali imzalıyor ({ad})..."); try
        {
            // İmzalı paket.
            string paket = await LisansOtoritesi.ImzalaAsync(p, new LisansBilgi(ad, mk, DateTime.Now.Date, bitis, "Pro"), CancellationToken.None);
            // Kaydet.
            using var f = new SaveFileDialog { FileName = $"BulutSoft_Lisans_{Regex.Replace(ad, @"[^\w\-]", "_")}.bslic", Filter = "BulutSoft lisansı (*.bslic)|*.bslic" }; if (f.ShowDialog(this) != DialogResult.OK) { Yaz("Kaydedilmedi."); return; }
            File.WriteAllText(f.FileName, paket); Yaz($"Müşteri lisansı kaydedildi: {f.FileName}\r\n({ad} · {(bitis == null ? "süresiz" : bitis.Value.ToString("dd.MM.yyyy") + " tarihine kadar")})"); _gunluk($"LİSANS -> Müşteri lisansı üretildi: {ad}.");
        }
        // Hata.
        catch (Exception ex) { Yaz("HATA: " + ex.Message); }
    }
#endif
}
