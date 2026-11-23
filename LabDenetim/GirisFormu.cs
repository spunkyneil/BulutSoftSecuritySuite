// Bu dosya program açılırken görünen GİRİŞ ekranını (ve ilk çalıştırmadaki KURULUM ekranını), "Şifremi unuttum" akışını,
// DESTEK penceresini ve genel amaçlı küçük "soru kutusu" penceresini (parola/seçim alanları için) içerir.
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Soru kutusundaki bir alan: etiket, gizli mi (parola), seçenek listesi (varsa açılır liste olur).
internal sealed record Alan(string Etiket, bool Gizli = false, string[]? Secenek = null);

// Genel soru kutusu: birkaç alan sorar ve değerleri verir.
internal sealed class SoruFormu : TemaliForm
{
    // Alan kutuları.
    private readonly List<Control> _kutular = new();

    // Pencereyi kurar.
    private SoruFormu(string baslik, string aciklama, Alan[] alanlar, string tamamMetni)
    {
        // Pencere.
        Text = baslik; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        // Açıklama: yüksekliği metne göre ölç (uzun metinler kesilmesin).
        int boy = string.IsNullOrEmpty(aciklama) ? 0 : TextRenderer.MeasureText(aciklama, Font, new Size(468, 0), TextFormatFlags.WordBreak).Height + 10; if (boy > 0) Controls.Add(new Label { Text = aciklama, Location = new Point(16, 12), Size = new Size(468, boy), ForeColor = DesignTokens.Ozgun(TokenRol.TextMuted), UseMnemonic = false });
        // Alanlar.
        int y = 14 + boy;
        foreach (var a in alanlar)
        {
            // Etiket.
            Controls.Add(new Label { Text = a.Etiket, Location = new Point(16, y), Size = new Size(468, 20), UseMnemonic = false });
            // Kutu: açılır liste veya yazı kutusu.
            Control k = a.Secenek != null ? new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(16, y + 22), Width = 468, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White } : new TextBox { Location = new Point(16, y + 22), Width = 468, UseSystemPasswordChar = a.Gizli, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            // Seçenekleri doldur.
            if (k is ComboBox cb) { cb.Items.AddRange(a.Secenek!); cb.SelectedIndex = 0; }
            // Ekle.
            Controls.Add(k); _kutular.Add(k); y += 60;
        }
        // Düğmeler.
        var tm = new EfektliButon { Text = tamamMetni, DialogResult = DialogResult.OK, Location = new Point(280, y + 6), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White }; var ip = new EfektliButon { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Location = new Point(398, y + 6), Size = new Size(86, 34), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated), ForeColor = Color.White };
        // Ekle.
        Controls.AddRange(new Control[] { tm, ip }); AcceptButton = tm; CancelButton = ip; ClientSize = new Size(500, y + 54);
    }

    // Soruyu sorar; Tamam denirse alan değerlerini, vazgeçilirse null verir.
    public static string[]? Sor(IWin32Window? sahip, string baslik, string aciklama, string tamam, params Alan[] alanlar)
    {
        // Pencere.
        using var f = new SoruFormu(baslik, aciklama, alanlar, tamam);
        // Göster ve değerleri topla.
        if (f.ShowDialog(sahip) != DialogResult.OK) return null; return f._kutular.Select(k => k is ComboBox c ? c.SelectedItem?.ToString() ?? "" : k.Text).ToArray();
    }
}

// Giriş / ilk kurulum penceresi: çerçevesiz, yuvarlak köşeli, saf GDI+ (ortak iskelet: KarsilamaFormu). Kimlik doğrulama, kilitleme ve kayıt mantığı KimlikDeposu'ndadır; burası yalnızca görünümdür.
internal sealed class GirisFormu : KarsilamaFormu
{
    // Alan simgeleri.
    private enum Ikon { Kisi, Kilit }
    // Bir giriş alanı: etiket, kutu, gizli mi (parola), güç göstergesi var mı, simge ve ekrandaki yeri.
    private sealed class AlanBilgi { public string Etiket = ""; public TextBox Tb = null!; public bool Gizli, Guc, Goster; public Ikon Simge; public Rectangle Giris; public int EtiketY; public int KutuSol; }
    // Sayfa planı (sağ panele göre yerler).
    private sealed record Plan(int AlanUst, int Aralik, int GirisY, int AciklamaY, int AciklamaBoy, int HataY, int DugmeY, int DugmeBoy, int BaglantiY, int NotY, int Yukseklik, int GirisH);

    private const string KurulumAciklama = "İki şey belirleyin: KURULUM ŞİFRESİ (yönetici işlemlerinin ek kilidi; unutulursa kurtarma yolu yoktur) ve her gün kullanacağınız yönetici hesabı. İkisinin parolası FARKLI olmalı.";
    private const string GirisAciklama = "Devam etmek için hesabınızla giriş yapın.";

    // Kurulum modunda mı (kimlik dosyası yok)?
    private readonly bool _kurulum;
    // Alanlar.
    private readonly TextBox _ad = new(), _sifre = new(), _kurulumSifre = new(), _kurulumTekrar = new(), _sifreTekrar = new();
    private readonly List<AlanBilgi> _alanlar = new(); private readonly Plan _plan; private readonly Sag _sag;
    private string _hata = ""; private bool _caps, _mesgul; private float _dx; private readonly System.Windows.Forms.Timer _sallanZaman = new() { Interval = 15 }; private DateTime _sallanBasi;
    // Son başarılı kullanıcı adının saklandığı dosya (yalnızca ad; parola ASLA saklanmaz).
    private static string SonKullaniciYolu => BulutKlasor.Dosya("son_kullanici.txt");

    // Yerleşim ölçümünde kullanılan yazı tipi (tek örnek; her çağrıda yenisi üretilmez).
    private static readonly Font OlcuYazisi = new("Segoe UI", 12.5f, FontStyle.Regular, GraphicsUnit.Pixel);

    // Sayfa planını hesaplar (pencere yüksekliği alan sayısına göre büyür; en az 510).
    private static Plan PlanHesapla(bool kurulum)
    {
        // Alçak ekranlarda (1366x768 gibi) kurulum formu sığsın diye alan aralığı ve kutu yüksekliği küçültülür.
        bool dar = Screen.PrimaryScreen is { } ekran && ekran.WorkingArea.Height < 830;
        int sagGen = Genislik - MarkaGenisligi, w = sagGen - 68, n = kurulum ? 5 : 2, aralik = kurulum ? (dar ? 60 : 70) : 80;
        int aciklamaY = 102, aciklamaBoy = kurulum ? TextRenderer.MeasureText(KurulumAciklama, OlcuYazisi, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 2 : 18;
        int alanUst = aciklamaY + aciklamaBoy + 22, hataY = alanUst + n * aralik - 2, dugmeY = hataY + 36, dugmeBoy = kurulum ? 44 : 45, baglantiY = dugmeY + dugmeBoy + 16, notBoy = 50;
        int yuk = Math.Max(510, baglantiY + 22 + 16 + notBoy + 26);
        return new Plan(alanUst, aralik, 22, aciklamaY, aciklamaBoy, hataY, dugmeY, dugmeBoy, baglantiY, yuk - 26 - notBoy, yuk, kurulum && dar ? 32 : kurulum ? 38 : 44);
    }

    // Pencereyi kurar.
    public GirisFormu(bool kurulum) : base(PlanHesapla(kurulum).Yukseklik)
    {
        _kurulum = kurulum; _plan = PlanHesapla(kurulum); Text = kurulum ? "Kurulum" : "Oturum Aç";
        // Alanlar.
        if (kurulum) { AlanEkle("Kurulum Şifresi", _kurulumSifre, true, true, Ikon.Kilit); AlanEkle("Kurulum Şifresi (Tekrar)", _kurulumTekrar, true, false, Ikon.Kilit); AlanEkle("Yönetici Kullanıcı Adı", _ad, false, false, Ikon.Kisi); AlanEkle("Yönetici Parolası", _sifre, true, true, Ikon.Kilit); AlanEkle("Yönetici Parolası (Tekrar)", _sifreTekrar, true, false, Ikon.Kilit); }
        else { AlanEkle("Kullanıcı Adı", _ad, false, false, Ikon.Kisi); AlanEkle("Parola", _sifre, true, false, Ikon.Kilit); }
        _sag = new Sag(this) { CikisIstendi = Cikis }; Yerlestir(_sag);
        // Alt kutular sağ panelin üstünde durur.
        foreach (var a in _alanlar) _sag.Controls.Add(a.Tb);
        Marka.OzellikSoluk = false;
        // Enter: giriş / kurulum.
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Dene(); } };
        // Caps Lock denetimi.
        foreach (var a in _alanlar.Where(x => x.Gizli)) { a.Tb.KeyUp += (_, _) => CapsGuncelle(); a.Tb.GotFocus += (_, _) => CapsGuncelle(); }
        Activated += (_, _) => CapsGuncelle();
        // Titreme animasyonu.
        _sallanZaman.Tick += (_, _) => { double s = (DateTime.UtcNow - _sallanBasi).TotalSeconds; if (s >= 0.45) { _sallanZaman.Stop(); _dx = 0; } else _dx = (float)(Math.Sin(s * 48) * 9 * (1 - s / 0.45)); Konumla(); _sag.Invalidate(); };
        FormClosed += (_, _) => _sallanZaman.Dispose();
        // Açılışta: son kullanıcı adını doldur ve doğru kutuya odaklan.
        Shown += (_, _) => { if (!kurulum) { try { if (File.Exists(SonKullaniciYolu)) { var s = File.ReadAllText(SonKullaniciYolu).Trim(); if (SifrePolitikasi.AdGecerli(s)) { _ad.Text = s; _sifre.Focus(); return; } } } catch (Exception hk1) { HataKayit.Sessiz(hk1, "GirisFormu.GirisFormu"); } _ad.Focus(); } else _kurulumSifre.Focus(); };
    }

    // Alan ekler: kutu ayarlanır, yeri planlanır.
    private void AlanEkle(string etiket, TextBox t, bool gizli, bool guc, Ikon simge)
    {
        int i = _alanlar.Count, w = Genislik - MarkaGenisligi - 68, girisH = _plan.GirisH;
        var a = new AlanBilgi { Etiket = etiket, Tb = t, Gizli = gizli, Guc = guc, Simge = simge, EtiketY = _plan.AlanUst + i * _plan.Aralik, Giris = new Rectangle(34, _plan.AlanUst + i * _plan.Aralik + _plan.GirisY, w, girisH), KutuSol = 34 + 38 };
        // Kenarlıksız yazı kutusu (çerçeveyi panel çizer); renkler Koyu temadaki özgün değerlerdir, TemaYonetici geçerli temaya çevirir.
        t.BorderStyle = BorderStyle.None; t.BackColor = ColorTranslator.FromHtml("#161E2E"); t.ForeColor = Color.White; t.Font = FontProvider.Gecici("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel); t.UseSystemPasswordChar = gizli; t.Width = w - 38 - (gizli ? 66 : 14); t.TabIndex = i; t.TabStop = true;
        t.GotFocus += (_, _) => _sag?.Invalidate(); t.LostFocus += (_, _) => _sag?.Invalidate(); t.TextChanged += (_, _) => _sag?.Invalidate();
        _alanlar.Add(a); Konumla(a);
    }

    // Kutuları (titreme payıyla) yerlerine koyar.
    private void Konumla() { foreach (var a in _alanlar) Konumla(a); }
    private void Konumla(AlanBilgi a) { int dx = ReferenceEquals(a.Tb, _sifre) && !_kurulum ? (int)_dx : 0; a.Tb.Location = new Point(a.KutuSol + dx, a.Giris.Y + (a.Giris.Height - a.Tb.Height) / 2); }

    // Çıkış: pencereyi kapatır (program kapanır).
    private void Cikis() { DialogResult = DialogResult.Cancel; Close(); }

    // Parola gücü: yalnızca bilgi amaçlı gösterge (asıl kural SifrePolitikasi'nda).
    internal static (string, Color) Guc(string s)
    {
        // Puan: uzunluk ve karakter çeşitliliği.
        int p = (s.Length >= 8 ? 1 : 0) + (s.Length >= 12 ? 1 : 0) + (s.Any(char.IsUpper) && s.Any(char.IsLower) ? 1 : 0) + (s.Any(char.IsDigit) ? 1 : 0) + (s.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);
        // Etiket.
        return p >= 4 ? ("Güçlü", DesignTokens.Ozgun(TokenRol.StatusSuccess)) : p >= 3 ? ("Orta", DesignTokens.Ozgun(TokenRol.StatusWarning)) : ("Zayıf", DesignTokens.Ozgun(TokenRol.StatusDanger));
    }

    // Caps Lock açıksa uyarı gösterilir (odaktaki parola kutusunun etiket satırında).
    private void CapsGuncelle() { bool k = IsKeyLocked(Keys.CapsLock); if (k != _caps) { _caps = k; _sag.Invalidate(); } else if (k) _sag.Invalidate(); }

    // Hata satırını yazar.
    private void Hata(string m) { _hata = m; _sag.Invalidate(); }

    // Yanlış parolada kutuyu hafifçe titretir.
    private void Sallan() { if (!SystemInformation.UIEffectsEnabled) return; _sallanBasi = DateTime.UtcNow; _sallanZaman.Start(); }

    // "Şifremi unuttum": kurulum şifresiyle parola sıfırlama.
    private void SifreUnuttum()
    {
        // Sor.
        var v = SoruFormu.Sor(this, "Şifremi Unuttum", "Parolanızı sıfırlamak için KURULUM ŞİFRESİ gerekir (ilk kurulumda belirlediğiniz ek şifre). Yeni parola en az 8 karakter, harf+rakam içermelidir.\r\n\r\nKurulum şifresini bilmiyorsanız: bir Yönetici'den \"Hesabım ve Kullanıcılar > Parola Sıfırla\" ile parolanızı sıfırlamasını isteyin. Kurulum şifresi de unutulduysa kurtarma yolu yoktur (güvenlik tasarımı); \"Destek ve yardım\" bölümüne bakın.", "Sıfırla", new Alan("Kullanıcı Adı"), new Alan("Kurulum Şifresi", true), new Alan("Yeni Parola", true), new Alan("Yeni Parola (Tekrar)", true)); if (v == null) return;
        // Tekrar.
        if (v[2] != v[3]) { TemaliMesaj.Goster("Yeni parola iki kutuda aynı değil.", "Şifremi Unuttum", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        // Sıfırla.
        var r = KimlikDeposu.SifreKurtar(v[0], v[1], v[2]); TemaliMesaj.Goster(r.Mesaj, "Şifremi Unuttum", MessageBoxButtons.OK, r.Basarili ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        // Başarılıysa kullanıcı adını doldur ve parola kutusuna odaklan.
        if (r.Basarili) { _ad.Text = v[0].Trim().ToLowerInvariant(); _sifre.Clear(); _sifre.Focus(); Hata(""); }
    }

    // Destek penceresini açar.
    private void DestekAc() { using var d = new DestekFormu(); d.ShowDialog(this); }

    // Girişi veya kurulumu dener.
    private void Dene()
    {
        if (_mesgul) return;
        // Kurulum.
        if (_kurulum)
        {
            // Tekrarlar eşleşmeli.
            if (_kurulumSifre.Text != _kurulumTekrar.Text) { Hata("Kurulum şifresi iki kutuda aynı değil."); return; }
            if (_sifre.Text != _sifreTekrar.Text) { Hata("Yönetici parolası iki kutuda aynı değil."); return; }
            // Kur.
            var r = KimlikDeposu.Kur(_kurulumSifre.Text, _ad.Text, _sifre.Text); if (!r.Basarili) { Hata(r.Mesaj); return; }
            // Kurulumdan sonra otomatik giriş.
            var g = KimlikDeposu.GirisYap(_ad.Text, _sifre.Text); if (g.Basarili) { Basarili(g.Kullanici!); } else Hata(g.Mesaj); return;
        }
        // Giriş: kısa süre düğmeyi kapat (özet hesabı birkaç yüz ms sürer).
        _mesgul = true; Cursor = Cursors.WaitCursor; _hata = ""; _sag.Invalidate(); _sag.Update();
        // Dene.
        try { var g = KimlikDeposu.GirisYap(_ad.Text, _sifre.Text); if (g.Basarili) Basarili(g.Kullanici!); else { Hata("⚠ " + g.Mesaj); _sifre.Clear(); _sifre.Focus(); Sallan(); } }
        // Düğmeyi aç.
        finally { _mesgul = false; Cursor = Cursors.Default; if (!IsDisposed) _sag.Invalidate(); }
    }

    // Başarılı giriş: oturumu ayarla, son kullanıcı adını hatırla ve kapat.
    private void Basarili(Kullanici k)
    {
        // Oturum.
        ServisBaglami.Oturum.Aktif = k;
        // Yalnızca kullanıcı adı hatırlanır.
        try { Depo.Yaz(SonKullaniciYolu, k.Ad); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "GirisFormu.Basarili"); }
        // Kapat.
        DialogResult = DialogResult.OK; Close();
    }

    // Sağ panel: başlık, alanlar, düğme, bağlantılar ve güvenlik notu (tümü GDI+ ile çizilir).
    private sealed class Sag : SagPanelTemel
    {
        private readonly GirisFormu _f; private readonly Font _fLink = new("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Pixel), _fLinkU = new("Segoe UI", 12.5f, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Pixel), _fDugme = new("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel), _fUyari = new("Segoe UI", 11.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        public Sag(GirisFormu f) { _f = f; Width = Genislik - MarkaGenisligi; }

        protected override void Ciz(Graphics g, KTema t)
        {
            var p = _f._plan; int w = IcGenislik;
            // Başlık ve açıklama.
            TextRenderer.DrawText(g, _f._kurulum ? "İlk Kurulum" : "Hoş Geldiniz", FBaslik, new Rectangle(Kenar, 66, w, 32), t.Metin, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (_f._kurulum) KCizim.YazCok(g, KurulumAciklama, FAciklama, new Rectangle(Kenar, p.AciklamaY, w, p.AciklamaBoy), t.Soluk); else KCizim.Yaz(g, GirisAciklama, FAciklama, new Rectangle(Kenar, p.AciklamaY, w, p.AciklamaBoy), t.Soluk);
            // Alanlar.
            foreach (var a in _f._alanlar) AlanCiz(g, t, a);
            // Hata satırı (satır içi, kırmızı).
            if (_f._hata.Length > 0) KCizim.YazCok(g, _f._hata, _fUyari, new Rectangle(Kenar, p.HataY, w, 34), t.Tehlike);
            Dugme(g, t, new Rectangle(Kenar, p.DugmeY, w, p.DugmeBoy));
            Baglantilar(g, t, p.BaglantiY);
            Not(g, t, new Rectangle(Kenar, p.NotY, w, 50));
        }

        // Etiket, güç / Caps Lock uyarısı ve çerçeveli giriş kutusu.
        private void AlanCiz(Graphics g, KTema t, AlanBilgi a)
        {
            int w = IcGenislik; var etiket = new Rectangle(Kenar, a.EtiketY, w - 150, 20); KCizim.Yaz(g, a.Etiket, FSatirK, etiket, t.Metin2);
            bool odak = a.Tb.Focused;
            if (a.Gizli && odak && _f._caps) KCizim.Yaz(g, "⚠ Caps Lock Açık", _fUyari, new Rectangle(Kenar + w - 160, a.EtiketY, 160, 20), t.Kehribar, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            else if (a.Guc && a.Tb.Text.Length > 0) { var (s, c) = GirisFormu.Guc(a.Tb.Text); KCizim.Yaz(g, "Güç: " + s, _fUyari, new Rectangle(Kenar + w - 160, a.EtiketY, 160, 20), TemaYonetici.C(c), TextFormatFlags.Right | TextFormatFlags.VerticalCenter); }
            int dx = ReferenceEquals(a.Tb, _f._sifre) && !_f._kurulum ? (int)_f._dx : 0; var r = new Rectangle(a.Giris.X + dx, a.Giris.Y, a.Giris.Width, a.Giris.Height);
            if (odak) KCizim.Cerceve(g, new RectangleF(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), 12, KTema.A(t.Ana, 52), 3f);
            KCizim.Doldur(g, r, 10, a.Tb.BackColor); KCizim.Cerceve(g, r, 10, odak ? t.Ana : t.Kenar, 1.5f);
            Simge(g, a.Simge, new PointF(r.X + 19, r.Y + r.Height / 2f), odak ? t.Ana : t.Soluk);
            if (a.Gizli)
            {
                var gr = new Rectangle(r.Right - 66, r.Y + (r.Height - 26) / 2, 58, 26); bool h = Bolge(gr, () => { a.Goster = !a.Goster; a.Tb.UseSystemPasswordChar = !a.Goster; Invalidate(); a.Tb.Focus(); });
                if (h) KCizim.Doldur(g, gr, 6, t.Yuksek);
                TextRenderer.DrawText(g, a.Goster ? "Gizle" : "Göster", FKucuk, gr, h ? t.Metin : t.Soluk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        // Kişi ve kilit simgeleri (vektör).
        private static void Simge(Graphics g, Ikon s, PointF m, Color c)
        {
            using var k = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (s == Ikon.Kisi) { g.DrawEllipse(k, m.X - 3.5f, m.Y - 7f, 7f, 7f); g.DrawArc(k, m.X - 6.5f, m.Y + 1.5f, 13f, 11f, 195, 150); }
            else { using var b = new SolidBrush(KTema.A(c, 60)); var r = new RectangleF(m.X - 5.5f, m.Y - 1.5f, 11f, 8.5f); g.FillRectangle(b, r); g.DrawRectangle(k, r.X, r.Y, r.Width, r.Height); g.DrawArc(k, m.X - 3.5f, m.Y - 7.5f, 7f, 9f, 180, 180); }
        }

        // Gradyan "Giriş Yap →" düğmesi.
        private void Dugme(Graphics g, KTema t, Rectangle r)
        {
            bool h = !_f._mesgul && Bolge(r, _f.Dene);
            if (!_f._mesgul) for (int i = 3; i >= 1; i--) KCizim.Doldur(g, new RectangleF(r.X + 2 - i, r.Y + 6 - i + (h ? 1 : 3), r.Width - 4 + 2 * i, r.Height - 2 + 2 * i), 10 + i, KTema.A(t.Ana, h ? 18 : 12));
            var yr = h ? new Rectangle(r.X, r.Y - 1, r.Width, r.Height) : r; using (var yol = KCizim.Yol(yr, 10)) using (var f = new LinearGradientBrush(yr, h ? Aydin(t.Ana) : t.Ana, h ? Aydin(t.Ana2) : t.Ana2, 20f)) { g.FillPath(f, yol); }
            if (_f._mesgul) KCizim.Doldur(g, yr, 10, KTema.A(t.Form, 70));
            string m = _f._mesgul ? "Kimlik Doğrulanıyor..." : _f._kurulum ? "Kurulumu Tamamla  →" : "Giriş Yap  →";
            TextRenderer.DrawText(g, m, _fDugme, yr, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        private static Color Aydin(Color c) => KTema.Karistir(c, Color.White, 0.10f);

        // Şifremi Unuttum / Destek ve Yardım / Çıkış (aralarında eşit boşluk).
        private void Baglantilar(Graphics g, KTema t, int y)
        {
            var liste = new List<(string Ad, Action A, Color R)>(); if (!_f._kurulum) liste.Add(("Şifremi Unuttum", _f.SifreUnuttum, t.Ana)); liste.Add(("Destek ve Yardım", _f.DestekAc, t.Soluk)); liste.Add(("Çıkış", _f.Cikis, t.Soluk));
            var gen = liste.Select(l => TextRenderer.MeasureText(g, l.Ad, _fLink, new Size(400, 22), TextFormatFlags.NoPadding).Width).ToArray(); int w = IcGenislik; int boşluk = liste.Count > 1 ? (w - gen.Sum()) / (liste.Count - 1) : 0; int x = Kenar;
            for (int i = 0; i < liste.Count; i++) { var r = new Rectangle(x, y, gen[i], 22); bool h = Bolge(new Rectangle(r.X - 6, r.Y, r.Width + 12, r.Height), liste[i].A); TextRenderer.DrawText(g, liste[i].Ad, h ? _fLinkU : _fLink, r, h && liste[i].R == t.Soluk ? t.Metin : liste[i].R, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding); x += gen[i] + boşluk; }
        }

        // Alt güvenlik notu: kalkan simgesi ve kayan satırlı yazı ("Olay Zaman Çizgisi" kalın).
        private void Not(Graphics g, KTema t, Rectangle r)
        {
            KCizim.Doldur(g, r, 10, t.Yuksek); KCizim.Cerceve(g, r, 10, t.Kenar);
            float cx = r.X + 19, cy = r.Y + 19; using (var b = new SolidBrush(KTema.A(t.Basari, 40))) using (var k = new Pen(t.Basari, 1.5f) { LineJoin = LineJoin.Round }) { var s = new[] { new PointF(cx, cy - 8), new PointF(cx + 7, cy - 5), new PointF(cx + 6, cy + 3), new PointF(cx, cy + 8), new PointF(cx - 6, cy + 3), new PointF(cx - 7, cy - 5) }; g.FillPolygon(b, s); g.DrawPolygon(k, s); }
            var parcalar = new (string S, bool K)[] { ("Oturum", false), ("açma", false), ("ve", false), ("yetki", false), ("olayları", false), ("Olay", true), ("Zaman", true), ("Çizgisi'ne", true), ("kaydedilir.", false), ("Parolalar", false), ("hiçbir", false), ("zaman", false), ("düz", false), ("yazı", false), ("olarak", false), ("saklanmaz.", false) };
            int x = r.X + 38, y = r.Y + 8, sag = r.Right - 12;
            foreach (var (s, k) in parcalar)
            {
                var f = k ? FNotK : FNot; int gw = TextRenderer.MeasureText(g, s, f, new Size(400, 20), TextFormatFlags.NoPadding).Width; if (x + gw > sag) { x = r.X + 38; y += 17; }
                TextRenderer.DrawText(g, s, f, new Point(x, y), k ? t.Metin2 : t.Soluk, TextFormatFlags.NoPadding); x += gw + 4;
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) { _fLink.Dispose(); _fLinkU.Dispose(); _fDugme.Dispose(); _fUyari.Dispose(); } base.Dispose(disposing); }
    }
}

// Destek penceresi: sık sorunlar, tanılama paketi ve iletişim (tüm metin kaydırma gerektirmeden sığar).
internal sealed class DestekFormu : TemaliForm
{
    // Pencereyi kurar.
    public DestekFormu()
    {
        // Pencere.
        Text = "Destek ve Yardım"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent; BackColor = DesignTokens.Ozgun(TokenRol.SurfaceApp); ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); Font = FontProvider.Al(FontRol.Body); Icon = Logo.Ikon;
        // Sol marka paneli yok; tek sütunlu, ferah düzen. Başlık.
        const int W = 640; var kap = new FlowLayoutPanel { Location = new Point(30, 22), Width = W, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent };
        // Başlık.
        kap.Controls.Add(new Label { Text = "Destek ve Yardım", AutoSize = true, Font = FontProvider.Al(FontRol.Display, FontStyle.Bold), ForeColor = Color.White, Margin = new Padding(0, 0, 0, 10), UseMnemonic = false });
        // Bölümler: başlık + açıklama (genişliğe göre otomatik satır kırar).
        void Bolum(string baslik, string metin)
        {
            // Başlık.
            kap.Controls.Add(new Label { Text = baslik, AutoSize = true, Font = FontProvider.Gecici("Segoe UI Semibold", 10.5f), ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo), Margin = new Padding(0, 10, 0, 2), UseMnemonic = false });
            // Metin: sabit genişlik, yükseklik metne göre ölçülür.
            var lb = new Label { Text = metin, AutoSize = true, MaximumSize = new Size(W, 0), ForeColor = DesignTokens.Ozgun(TokenRol.TextSecondary), Margin = new Padding(0), UseMnemonic = false }; kap.Controls.Add(lb);
        }
        // İçerik.
        Bolum("Parolamı unuttum", "Giriş ekranındaki \"Şifremi Unuttum\" bağlantısına basın. Kullanıcı adınız ve Kurulum Şifresi ile yeni parola belirlersiniz. Kurulum Şifresini bilmiyorsanız bir Yönetici, \"Hesabım ve Kullanıcılar > Parola Sıfırla\" ile sıfırlayabilir.");
        Bolum("Hesabım kilitlendi", "5 hatalı denemeden sonra hesap 30 saniye kilitlenir; hata sürerse süre katlanır (en çok 15 dakika) ve süre dolunca kilit kendiliğinden açılır. Beklemek istemezseniz \"Şifremi Unuttum\" ile parolanızı sıfırlayabilirsiniz.");
        Bolum("Kurulum Şifresini unuttum", "Güvenlik tasarımı gereği kurtarma yolu yoktur. Bilgisayarın sahibiyseniz kimlik dosyalarını (kimlik.json, kimlik.json.mac ve varsa kimlik.json.tmp) silip ilk kuruluma dönebilirsiniz; bu, tüm kullanıcı hesaplarını siler. Koruma hizmeti kuruluysa yönetici PowerShell'de ayrıca: reg delete HKLM\\SOFTWARE\\BulutSoft\\Guard /v KimlikMuhurHash /f Dosyanın yeri \"Kayıt Klasörünü Aç\" ile bulunur.");
        Bolum("Program açılmıyor / kimlik dosyası geçersiz", "kimlik.json bozulmuş, değiştirilmiş ya da mührü (kimlik.json.mac) silinmiş olabilir. Yedeğiniz varsa ikisini birlikte geri koyun. Güvenlik nedeniyle program, bozuk dosyayla sessizce yeni kuruluma geçmez.");
        Bolum("Destek talebi göndermek", "\"Tanılama Paketi Hazırla\" masaüstüne bir zip oluşturur: sürüm, işletim sistemi ve son olay satırları. Parola, API anahtarı ve tarama sonuçları konmaz. Olay satırlarında kullanıcı adları geçebilir; göndermeden önce zip'i açıp bakabilirsiniz. Ardından \"İletişim\" bölümündeki adrese gönderin.");
        // Düğmeler.
        Button D(string tx, int gen) => new EfektliButon() { Text = tx, Size = new Size(gen, 38), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.AccentPrimary), ForeColor = Color.White, Font = FontProvider.Al(FontRol.Body, FontStyle.Bold), Cursor = Cursors.Hand, UseMnemonic = false, Margin = new Padding(0, 0, 10, 0) };
        // Tanılama.
        var tan = D("Tanılama Paketi Hazırla", 200); tan.Click += (_, _) => { try { string y = TanilamaPaketi.Olustur(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); TemaliMesaj.Goster("Hazır (Masaüstü):\r\n" + y, "Tanılama Paketi", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception x) { TemaliMesaj.Goster(x.Message, "Tanılama Paketi", MessageBoxButtons.OK, MessageBoxIcon.Warning); } };
        // Klasörü aç.
        var kl = D("Kayıt Klasörünü Aç", 170); kl.Click += (_, _) => { try { Directory.CreateDirectory(BulutKlasor.Kok); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + BulutKlasor.Kok + "\"") { UseShellExecute = true }); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "GirisFormu.DestekFormu"); } };
        // İletişim.
        var il = D("İletişim", 110); il.Click += (_, _) => { using var h = new HakkindaFormu(); h.ShowDialog(this); };
        // Kapat.
        var kp = D("Kapat", 100); kp.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); kp.DialogResult = DialogResult.Cancel; kp.Margin = new Padding(0);
        // Düğme şeridi.
        var sr = new FlowLayoutPanel { Width = W, Height = 48, Margin = new Padding(0, 22, 0, 0), WrapContents = false }; sr.Controls.AddRange(new Control[] { tan, kl, il, kp }); kap.Controls.Add(sr);
        // Pencereyi içeriğe göre boyutlandır.
        Controls.Add(kap); CancelButton = kp; ClientSize = new Size(W + 60, kap.PreferredSize.Height + 50);
    }
}
