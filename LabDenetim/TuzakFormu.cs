// Bu dosya "⌖ Tuzaklar (Erken Uyarı)" penceresini çizer: üç tuzağın (Honeypot kapı, Canary dosyalar, Fidye alarmı) açma/kapama
// düğmelerini ve ne işe yaradıklarını sade Türkçe anlatan yazıları içerir. Hiçbir tuzak kendiliğinden açılmaz; her biri sizin onayınızla açılır.
namespace LabDenetim;

// Tuzaklar penceresi.
internal sealed class TuzakFormu : SayfaDenetimi
{
    // Motorlar ve günlük yazıcı.
    private readonly HoneypotKapi _hp; private readonly CanaryDosyalari _cn; private readonly FidyeIzleyici _fy; private readonly Action<string> _gunluk;
    // Durum yazıları ve düğmeler.
    private readonly Label _hpDurum = new(), _cnDurum = new(), _fyDurum = new(); private readonly Button _hpDugme = new EfektliButon(), _cnDugme = new EfektliButon(), _fyDugme = new EfektliButon();
    // Honeypot ayarları: kapı numarası ve iç ağ seçeneği.
    private readonly TextBox _port = new() { Text = "2222", Width = 70 }; private readonly CheckBox _lab = new() { Text = "İç ağdan (özel IP'lerden) gelen bağlantıları da dinle", AutoSize = true };

    // Pencereyi kurar.
    public TuzakFormu(HoneypotKapi hp, CanaryDosyalari cn, FidyeIzleyici fy, Action<string> gunluk)
    {
        // Motorları sakla.
        _hp = hp; _cn = cn; _fy = fy; _gunluk = gunluk;
        // Pencere ayarları.
        Text = "⌖ Tuzaklar (Erken Uyarı)"; ClientSize = new Size(820, 900); MinimumSize = new Size(720, 560); StartPosition = FormStartPosition.CenterParent; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Ana dikey yerleşim.
        var ana = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16) };
        // Üst açıklama.
        ana.Controls.Add(Yazi("Tuzak, saldırganı veya zararlı yazılımı kendi isteği dışında ele veren sahte bir yemdir. Aşağıdaki üç tuzak yalnızca İZLER ve sizi uyarır; hiçbir şeyi engellemez, internete çıkmaz. Her biri sizin onayınızla açılır ve istediğiniz an kapatılır (bu oturum kapanınca da durur).", 760, DesignTokens.Ozgun(TokenRol.TextMuted), 9.5f, FontStyle.Regular));
        // 1) Honeypot.
        ana.Controls.Add(Baslik("1) Honeypot Kapı (sahte SSH kapısı)"));
        ana.Controls.Add(Yazi("Gerçek bir hizmet değil, sahte bir \"SSH\" kapısı açar (varsayılan 2222). Biri bu kapıyı yoklarsa kim olduğu ve yazdığı ilk şey kaydedilir ve alarm çalar. Normal kullanıcıların bu kapıyla işi olmaz; bağlanan herkes şüphelidir. Varsayılan olarak yalnızca bu bilgisayarın içinden (127.0.0.1) gelenleri dinler. Not: bu kapı kendi taramanızda \"açık kapı\" olarak görünür; tuzağın kendisidir.", 760, DesignTokens.Ozgun(TokenRol.TextSecondary), 9.5f, FontStyle.Regular));
        // Port satırı.
        var portSatiri = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) }; portSatiri.Controls.Add(Yazi("Kapı numarası:", 100, DesignTokens.Ozgun(TokenRol.TextSecondary), 9.5f, FontStyle.Bold)); portSatiri.Controls.Add(_port); ana.Controls.Add(portSatiri);
        // İç ağ kutusu.
        _lab.ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary); _lab.Margin = new Padding(0, 6, 0, 6); ana.Controls.Add(_lab);
        // Durum + düğme.
        ana.Controls.Add(_hpDurum); ana.Controls.Add(_hpDugme);
        // 2) Canary.
        ana.Controls.Add(Baslik("2) Canary (Kanarya) Dosyalar"));
        ana.Controls.Add(Yazi("Belgeler klasörünüzde \"Eski_Parolalar_YEDEK\" adlı bir klasöre cazip adlı SAHTE dosyalar (parolalar.txt vb.) koyar. Biri bunları değiştirir, siler, ekler veya adını değiştirirse alarm çalar; gerçek bir dosyaya asla dokunulmaz. Dikkat: dosyanın yalnızca AÇILIP OKUNMASI, Windows'un \"son erişim\" ayarı kapalıysa yakalanamaz; değiştirme/silme/ad değiştirme her zaman yakalanır. Kapatınca yalnızca bu programın koyduğu (işaretli) sahte dosyalar silinir.", 760, DesignTokens.Ozgun(TokenRol.TextSecondary), 9.5f, FontStyle.Regular));
        ana.Controls.Add(_cnDurum); ana.Controls.Add(_cnDugme);
        // 3) Fidye alarmı.
        ana.Controls.Add(Baslik("3) Fidye Yazılımı Davranış Alarmı"));
        ana.Controls.Add(Yazi("Belgeler, Masaüstü ve Resimler klasörlerini izler. 10 saniyede 40'tan fazla farklı dosya değişirse veya dosyalar bilinen fidye uzantılarına (.locked, .encrypted, .wncry ...) dönüşürse alarm çalar ve ⚡ Acil İzolasyon'u önerir. Büyük bir klasör kopyalama veya arşiv açma da geçici olarak alarm verebilir; bu durumda alarmı yok sayabilirsiniz. Dosya içeriğine bakmaz, yalnızca değişiklik sayısını izler.", 760, DesignTokens.Ozgun(TokenRol.TextSecondary), 9.5f, FontStyle.Regular));
        ana.Controls.Add(_fyDurum); ana.Controls.Add(_fyDugme);
        // Düğme stilleri ve tıklamalar.
        foreach (var d in new[] { _hpDugme, _cnDugme, _fyDugme }) { d.Size = new Size(380, 38); d.FlatStyle = FlatStyle.Flat; d.FlatAppearance.BorderColor = DesignTokens.Ozgun(TokenRol.StatusInfo); d.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); d.Cursor = Cursors.Hand; d.UseMnemonic = false; d.Margin = new Padding(0, 4, 0, 14); }
        // Durum yazıları.
        foreach (var l in new[] { _hpDurum, _cnDurum, _fyDurum }) { l.AutoSize = true; l.MaximumSize = new Size(760, 0); l.UseMnemonic = false; l.Margin = new Padding(0, 6, 0, 0); l.Font = FontProvider.Al(FontRol.Body, FontStyle.Bold); }
        // Tıklamalar.
        _hpDugme.Click += (_, _) => HoneypotDegistir(); _cnDugme.Click += (_, _) => CanaryDegistir(); _fyDugme.Click += (_, _) => FidyeDegistir();
        // 4) Süreç Avcısı: alarmdan sonra diske çok yazan programları bulur.
        ana.Controls.Add(Baslik("4) Süreç Avcısı (Alarmdan Sonra: Dosyaları Kim Yazıyor?)"));
        ana.Controls.Add(Yazi("Fidye alarmı geldiğinde veya şüphelendiğinizde: birkaç saniye boyunca hangi programların diske çok veri yazdığını ölçer. Beklenmedik bir programın başta olması şüphe uyandırır. Sonlandırma ayrıca onay ister; sistem süreçlerine ve Microsoft imzalı programlara dokunulmaz.", 760, DesignTokens.Ozgun(TokenRol.TextSecondary), 9.5f, FontStyle.Regular));
        var avci = new EfektliButon { Text = "Süreç Avcısı'nı Aç", Size = new Size(380, 38), FlatStyle = FlatStyle.Flat, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 4, 0, 14), BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White }; avci.FlatAppearance.BorderColor = DesignTokens.Ozgun(TokenRol.StatusInfo); avci.Click += (_, _) => { using var f = new SurecAvciFormu(_ => { }); f.ShowDialog(this); }; ana.Controls.Add(avci);
        // Kapat düğmesi.
        var kapat = new EfektliButon { Text = "Kapat", Size = new Size(120, 36), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary), UseMnemonic = false }; kapat.Click += (_, _) => Close(); ana.Controls.Add(kapat);
        // Pencereye koy.
        Controls.Add(ana);
        // İlk görünümü kur.
        Yenile();
    }

    // Başlık yazısı üretir.
    private static Label Baslik(string t) => new() { Text = t, AutoSize = true, ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Font = FontProvider.Al(FontRol.Subheading, FontStyle.Bold), Margin = new Padding(0, 14, 0, 4), UseMnemonic = false };

    // Açıklama yazısı üretir.
    private static Label Yazi(string t, int g, Color renk, float boy, FontStyle stil) => new() { Text = t, AutoSize = true, MaximumSize = new Size(g, 0), ForeColor = renk, Font = FontProvider.Gecici("Segoe UI", boy, stil), Margin = new Padding(0, 0, 0, 4), UseMnemonic = false };

    // Durum yazılarını ve düğmeleri motorların gerçek durumuna göre günceller.
    private void Yenile()
    {
        // Honeypot.
        _hpDurum.Text = _hp.Aktif ? $"● AÇIK — dinlenen: {_hp.Dinlenen}" : "○ Kapalı"; _hpDurum.ForeColor = _hp.Aktif ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.TextMuted);
        // Honeypot düğmesi.
        Boya(_hpDugme, _hp.Aktif, "◈ Honeypot Kapıyı Aç", "✓ Honeypot Kapıyı Kapat"); _port.Enabled = _lab.Enabled = !_hp.Aktif;
        // Canary.
        _cnDurum.Text = _cn.Aktif ? $"● AÇIK — izlenen klasör: {CanaryDosyalari.Klasor}" : "○ Kapalı"; _cnDurum.ForeColor = _cn.Aktif ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.TextMuted);
        // Canary düğmesi.
        Boya(_cnDugme, _cn.Aktif, "◈ Tuzak Dosyaları Oluştur ve İzle", "✓ İzlemeyi Durdur ve Sahte Dosyaları Sil");
        // Fidye.
        _fyDurum.Text = _fy.Aktif ? $"● AÇIK — izlenen: {string.Join(", ", _fy.Klasorler)}" : "○ Kapalı"; _fyDurum.ForeColor = _fy.Aktif ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.TextMuted);
        // Fidye düğmesi.
        Boya(_fyDugme, _fy.Aktif, "◈ Fidye Alarmını Aç", "✓ Fidye Alarmını Kapat");
        // Durum kalıcı saklanır: program yeniden açılınca açık olan tuzaklar tekrar açılır.
        TuzakDurumu.Kaydet(_hp.Aktif, int.TryParse(_port.Text.Trim(), out int pt) ? pt : 2222, _lab.Checked, _cn.Aktif, _fy.Aktif);
    }

    // Düğmeyi açık/kapalı durumuna göre boyar.
    private static void Boya(Button d, bool aktif, string kapaliYazi, string acikYazi) { d.Text = aktif ? acikYazi : kapaliYazi; d.BackColor = aktif ? DesignTokens.Ozgun(TokenRol.StatusSuccessBg) : DesignTokens.Ozgun(TokenRol.SurfaceElevated); d.ForeColor = aktif ? DesignTokens.Ozgun(TokenRol.StatusSuccess) : DesignTokens.Ozgun(TokenRol.TextPrimary); }

    // Honeypot'u açar/kapatır.
    private void HoneypotDegistir()
    {
        // Açıksa kapat.
        if (_hp.Aktif) { _hp.Durdur(); _gunluk("TUZAK -> Honeypot kapısı kapatıldı."); Yenile(); return; }
        // Kapı numarasını oku.
        if (!int.TryParse(_port.Text.Trim(), out int port)) { TemaliMesaj.Goster("Kapı numarası bir sayı olmalı (örn. 2222).", "Hatalı değer", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay iste (varsayılan Hayır).
        if (TemaliMesaj.Goster($"Bu bilgisayarda {port} numaralı kapıda sahte bir SSH hizmeti dinlenecek{(_lab.Checked ? " (iç ağ adresleri dahil)" : " (yalnızca bu bilgisayarın içinden)")}.\r\n\r\nGerçek bir hizmet açılmaz; bağlananlar yalnızca kaydedilir. İstediğiniz an kapatabilirsiniz.\r\n\r\nAçılsın mı?", "Honeypot - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Başlat.
        var hata = _hp.Baslat(port, _lab.Checked);
        // Hata varsa göster.
        if (hata != null) { TemaliMesaj.Goster(hata, "Açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Günlüğe yaz.
        _gunluk($"TUZAK -> Honeypot kapısı açıldı: {_hp.Dinlenen}"); Yenile();
    }

    // Canary dosyaları açar/kapatır.
    private void CanaryDegistir()
    {
        // Açıksa durdur ve sahte dosyaları sil.
        if (_cn.Aktif)
        {
            // Sil.
            int n = _cn.Kaldir(); _gunluk($"TUZAK -> Canary izleme durduruldu, {n} sahte dosya silindi."); Yenile(); return;
        }
        // Onay iste (varsayılan Hayır).
        if (TemaliMesaj.Goster($"Belgeler klasörünüzde şu klasör oluşturulacak ve içine 3 SAHTE dosya (parolalar.txt, banka_bilgileri.txt, ozel_anahtar_yedek.txt) konacak:\r\n\r\n{CanaryDosyalari.Klasor}\r\n\r\nGerçek dosyalarınıza dokunulmaz. Kapatınca bu sahte dosyalar silinir.\r\n\r\nDevam edilsin mi?", "Canary - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Kurmayı dene.
        try { _cn.Baslat(); _gunluk($"TUZAK -> Canary dosyalar oluşturuldu ve izleniyor: {CanaryDosyalari.Klasor}"); }
        // Hata olursa göster.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Kurulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        // Yenile.
        Yenile();
    }

    // Fidye alarmını açar/kapatır.
    private void FidyeDegistir()
    {
        // Açıksa kapat.
        if (_fy.Aktif) { _fy.Durdur(); _gunluk("TUZAK -> Fidye alarmı kapatıldı."); Yenile(); return; }
        // Onay iste (varsayılan Hayır).
        if (TemaliMesaj.Goster($"Şu klasörlerde dosya değişikliklerinin SAYISI izlenecek (içeriğe bakılmaz):\r\n\r\n{string.Join("\r\n", FidyeIzleyici.VarsayilanKlasorler())}\r\n\r\nAçılsın mı?", "Fidye Alarmı - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Başlat.
        _fy.Baslat(); _gunluk($"TUZAK -> Fidye alarmı açıldı ({_fy.Klasorler.Count} klasör izleniyor)."); Yenile();
    }
}
