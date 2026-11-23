// Kolay Mod testleri: ilk açılışta Kolay görünüm, geçiş, kabuk menüsünün sadeleşmesi, konuşan ilerleme, mektup metni, kapı bulguları ve panelin kurulabilmesi.
// (Eski sol menü yerine kurumsal kabuk: Kolay'da yalnızca Durum Panosu, Tarama Merkezi ve Raporlar görünür.)
using System.Reflection;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class KolayModTestleri
{
    // Form1'i geçici klasörde kurar, ölçümleri döndürür.
    static T Form1de<T>(Func<Form1, T> olc, ModTuru? baslangic = null, Rol? rol = null)
    {
        T sonuc = default!; Exception? hata = null;
        var yakalanan = new List<string>(); var eskiBildirici = GuvenliCalistir.HataBildirici; GuvenliCalistir.HataBildirici = (b, ex) => yakalanan.Add(b + ": " + ex);   // arka plan hatası modal pencere açıp testi bekletmesin; hata olarak yansır
        var t = new Thread(() =>
        {
            string eskiKok = BulutKlasor.Kok, eskiReg = Lisans.RegistryYolu; BulutKlasor.Kok = Path.Combine(Path.GetTempPath(), "km_" + Guid.NewGuid().ToString("N")); Lisans.RegistryYolu = @"Software\BulutSoftTest_" + Guid.NewGuid().ToString("N");
            // rol verilirse oturum açılmış gibi davranılır (yetkisiz sayfa denemesi modal uyarı açıp testi bekletmesin).
            if (rol != null) Oturum.Aktif = new Kullanici { Ad = "test", Rol = rol.Value };
            try { ArayuzModu.Sifirla(); if (baslangic != null) ArayuzModu.Ayarla(baslangic.Value); using var f = new Form1(); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.ClientSize = new Size(1500, 860); f.Show(); Application.DoEvents(); sonuc = olc(f); f.Close(); }
            catch (Exception ex) { hata = ex; }
            finally { if (hata == null && yakalanan.Count > 0) hata = new Exception("GuvenliCalistir hatası: " + string.Join(" | ", yakalanan)); ArayuzModu.Sifirla(); if (rol != null) Oturum.Aktif = null; try { Directory.Delete(BulutKlasor.Kok, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "KolayModTestleri.temizlik"); } BulutKlasor.Kok = eskiKok; Lisans.RegistryYolu = eskiReg; }
        });
        t.SetApartmentState(ApartmentState.STA); t.Start(); if (!t.Join(TimeSpan.FromSeconds(90))) throw new TimeoutException(); GuvenliCalistir.HataBildirici = eskiBildirici; if (hata != null) throw new Exception("Form1 hatası: " + hata, hata); return sonuc;
    }
    // Alan: Form1 alanı ya da (tarama ekranı UserControl'e taşındığı için) Form1'in aynı adlı yönlendirme özelliği.
    static object Alan(Form1 f, string ad)
    {
        const BindingFlags b = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        if (typeof(Form1).GetField(ad, b)?.GetValue(f) is { } v1) return v1;
        if (ad == "pnlAna") return f._tarama;
        // Form1'den çıkan denetleyiciler: Kolay mod, küre yöneticisi, Nöbetçi.
        foreach (var sahip in new object[] { f._kolayMod, f._kure, f._nobet }) if (sahip.GetType().GetField(ad, b)?.GetValue(sahip) is { } v2) return v2;
        return typeof(TaramaSayfasi).GetField(ad, b)!.GetValue(f._tarama)!;   // tarama ekranı denetimleri/durumu TaramaSayfasi'dedir
    }
    static AppShell Kabuk(Form1 f) => (AppShell)Alan(f, "_kabuk");

    [Fact] public void IlkAcilis_KolayGorunum_KartliPanel_TablolarGizli_MenuSade()
    {
        var (kolayGorunur, tabloEkranda, menuOgesi, mod, aktif) = Form1de(f =>
        {
            var k = Kabuk(f); var kolay = (KolayModPaneli)Alan(f, "_kolay"); var pnl = (Control)Alan(f, "pnlAna");
            return (kolay.Visible && kolay.Parent != null, pnl.Parent != null, k.Kenar.OgeSayisi, k.Ust.Mod, k.AktifSayfaId);   // tarama çalışma alanı Kolay'da hiçbir sayfaya yerleşmemiştir
        });
        Assert.True(kolayGorunur); Assert.False(tabloEkranda); Assert.Equal(3, menuOgesi); Assert.Equal(ModTuru.Kolay, mod); Assert.Equal("durum", aktif);
    }
    [Fact] public void UzmanGorunum_EskiEkranAynenGelir_Menu_Tam()
    {
        var (kolay, ogeler, mod, radar, kure) = Form1de(f =>
        {
            ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); var k = Kabuk(f); k.GitAsync("durum", "radar").GetAwaiter().GetResult();
            return (((KolayModPaneli)Alan(f, "_kolay")).Visible, k.Kenar.OgeSayisi, k.Mod, ((Control)Alan(f, "radar")).Parent != null, Alan(f, "_uzmanKure") != null);
        });
        Assert.False(kolay); Assert.Equal(10, ogeler); Assert.Equal(ModTuru.Uzman, mod); Assert.True(radar); Assert.True(kure);   // hiçbir sayfa silinmedi, hepsi menüde
    }
    [Fact] public void Gecis_IkiYonde_TercihKalici()
    {
        var (a, b, c) = Form1de(f =>
        {
            var k = Kabuk(f); ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); k.GitAsync("tarama", "yeni").GetAwaiter().GetResult(); bool u = ((Control)Alan(f, "pnlAna")).Visible;
            ArayuzModu.Ayarla(ModTuru.Kolay); Application.DoEvents(); k.GitAsync("durum").GetAwaiter().GetResult(); return (u, ((KolayModPaneli)Alan(f, "_kolay")).Visible, File.Exists(ArayuzModu.Yol));
        });
        Assert.True(a); Assert.True(b); Assert.True(c);
    }
    [Fact] public void UzmanKayitliysa_Uzmanla_Baslar() => Assert.Equal(ModTuru.Uzman, Form1de(f => Kabuk(f).Mod, ModTuru.Uzman));
    [Fact] public void KolayModdaKabukMenusu_SadeceUcSayfa_UzmandaOnSayfa_Sigar()
    {
        var (kolay, uzman, kolayGerekli, uzmanGerekli) = Form1de(f =>
        {
            var k = Kabuk(f); int ko = k.Kenar.OgeSayisi, kg = k.Kenar.GerekenYukseklik; ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents();
            return (ko, k.Kenar.OgeSayisi, kg, k.Kenar.GerekenYukseklik);
        });
        Assert.Equal(3, kolay); Assert.Equal(10, uzman); Assert.True(kolayGerekli < uzmanGerekli);   // Kolay menü daha kısa (tam menünün sığması KabukYerlesimTestleri'nde, ekran yeterliyse)
    }
    [Fact] public void Panel_Kartlari_Gostergeler_KarneGoster()
    {
        var (kartlar, gosterge, karneYazi) = Form1de(f =>
        {
            var p = (KolayModPaneli)Alan(f, "_kolay"); p.KarneGoster(KarneHesabi.Hesapla(new[] { new Bulgu("Port", "Açık port 445 (SMB)", Onem.Yuksek, "", "Kapatın.") })); p.Gosterge(0, "Kali Sensör", "Aktif", 0);
            return (p.Controls.Cast<Control>().Count(), 3, p.SonKarne!.Baslik);
        });
        Assert.Equal(3, kartlar); Assert.Equal(3, gosterge); Assert.Contains("85 / 100", karneYazi);
    }

    [Fact] public void KolayIlkGosterge_UzakSensorYokken_SonTaramaDurumuKartidir()
    {
        var (baslik, yazi) = Form1de(f =>
        {
            var kolay = (KolayModPaneli)Alan(f, "_kolay"); var dizi = (Array)typeof(KolayModPaneli).GetField("_gostergeler", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(kolay)!; var kart = dizi.GetValue(0)!;
            string Al(string ad) => (string)kart.GetType().GetField(ad, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(kart)!;
            return (Al("_baslik"), Al("_deger"));
        });
        Assert.Equal("Son Tarama Durumu", baslik);                       // geçici veri klasöründe yapılandırılmış sensör yok → sade KOBİ varsayılanı
        Assert.Matches(@"^(Henüz tarama yapılmadı|\d\d\.\d\d\.\d{4} \d\d:\d\d · puan \d{1,3}/100)$", yazi);   // gerçek geçmiş kaydı varsa tarih + puan, yoksa "henüz yok" (geçmiş deposu gerçek kullanıcı klasörünü okur)
    }

    // ---- Köprüler: eski modal akışlar yeni kabukta kopmaz ----
    [Fact] public void Gecmis_EkranaYukle_KaydiYukler_VeYeniTaramaSekmesineGecirir()
    {
        var (aktif, sekme, hedef) = Form1de(f =>
        {
            ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); var k = Kabuk(f); Assert.True(k.GitAsync("tarama", "gecmis").GetAwaiter().GetResult());
            var adaptor = Tum(k).OfType<DenetimSayfasi>().Single(a => a.Baslik == "Geçmiş Operasyonlar"); var g = Assert.IsType<GecmisFormu>(adaptor.Denetim);
            var kayit = new GecmisKayit("t1", DateTime.Now, "10.9.8.7", false, "Hızlı", 8, 3, 90, 0, 1, 7, 0, new List<Bulgu>());
            typeof(GecmisFormu).GetProperty("Secilen")!.SetValue(g, kayit); g.DialogResult = DialogResult.OK; g.Close(); Application.DoEvents();
            var tarama = (SekmeliSayfa)k.Sayfa("tarama")!;
            return (k.AktifSayfaId, tarama.AktifSekme, ((TextBox)Alan(f, "txtHedef")).Text);
        });
        Assert.Equal("tarama", aktif); Assert.Equal("yeni", sekme); Assert.Equal("10.9.8.7", hedef);
    }
    [Fact] public void KolayModdaGecmisEkranaYukle_UzmanaGecerVeYeniTaramaAcilir()
    {
        var (mod, sekme) = Form1de(f =>
        {
            var k = Kabuk(f); Assert.Equal(ModTuru.Kolay, k.Mod); k.GitAsync("tarama", "gecmis").GetAwaiter().GetResult();
            var g = Assert.IsType<GecmisFormu>(Tum(k).OfType<DenetimSayfasi>().Single(a => a.Baslik == "Geçmiş Operasyonlar").Denetim);
            typeof(GecmisFormu).GetProperty("Secilen")!.SetValue(g, new GecmisKayit("t2", DateTime.Now, "10.1.1.1", false, "Hızlı", 8, 3, 90, 0, 0, 8, 0, new List<Bulgu>())); g.DialogResult = DialogResult.OK; g.Close(); Application.DoEvents(); Application.DoEvents();
            return (ArayuzModu.Mevcut, ((SekmeliSayfa)k.Sayfa("tarama")!).AktifSekme);
        });
        Assert.Equal(ModTuru.Uzman, mod); Assert.Equal("yeni", sekme);
    }
    [Fact] public void KolayAyrintilarDugmesi_UzmanaGecerVeYeniTaramaSekmesiniAcar()
    {
        var (mod, aktif, sekme) = Form1de(f =>
        {
            var k = Kabuk(f); var kolay = (KolayModPaneli)Alan(f, "_kolay"); kolay.UzmanaGec!.Invoke(); Application.DoEvents(); Application.DoEvents();
            return (ArayuzModu.Mevcut, k.AktifSayfaId, ((SekmeliSayfa)k.Sayfa("tarama")!).AktifSekme);
        });
        Assert.Equal(ModTuru.Uzman, mod); Assert.Equal("tarama", aktif); Assert.Equal("yeni", sekme);
    }
    [Fact] public void SavunmaMerkezi_AcilIzolasyonCubugu_EskiDugmeDurumunuAnindaYansitir()
    {
        var (acil1, acil2, nobet, kilit) = Form1de(f =>
        {
            ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); var k = Kabuk(f); k.GitAsync("savunma").GetAwaiter().GetResult();
            var cubuk = Tum(k).OfType<SavunmaUstCubuk>().Single(); var svc = (SavunmaDurumServisi)Alan(f, "_savunma");
            var a1 = cubuk.AcilDurumu.Baslik; svc.AcilAyarla(true); var a2 = cubuk.AcilDurumu.Baslik; svc.NobetAyarla(true); Application.DoEvents();
            var n = cubuk.NobetDurumu.Baslik; var tn = cubuk.AcilDurumu.Ton; svc.NobetAyarla(false); return (a1, a2, n, tn);   // açık nöbetçi pencereyi kapatırken onay sorar: önce kapat
        }, rol: Rol.Analist);
        Assert.Equal("ACİL İZOLASYON", acil1); Assert.Equal("İZOLASYON AÇIK", acil2); Assert.Equal("Nöbetçi Modu: AÇIK", nobet); Assert.Equal(KartTonu.Kilitli, kilit);   // Analist sistemi değiştiremez: Acil İzolasyon kartı kilitli
    }
    [Fact] public void Kisayollar_KabukUzerindenSayfalaraGider_EskiModalAcmaz()
    {
        var sonuclar = Form1de(f =>
        {
            ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); var k = Kabuk(f); var l = new List<string?>();
            var kabuk = typeof(Form1).GetMethod("KabukGit", BindingFlags.NonPublic | BindingFlags.Instance)!;
            foreach (var (s, sk) in new[] { ("olay", (string?)null), ("durum", "pano"), ("tarama", "gecmis"), ("rapor", null), ("trafik", null), ("ag", "cihaz") }) { kabuk.Invoke(f, new object?[] { s, sk, true }); Application.DoEvents(); l.Add(k.AktifSayfaId); }
            return l;
        }, rol: Rol.Yonetici);
        Assert.Equal(new[] { "olay", "durum", "tarama", "rapor", "trafik", "ag" }, sonuclar);
    }
    [Fact] public void KomutPaleti_Komutlari_EksiksizVeBenzersiz_ZamanlamaBasarilarEgitimNobetErisilebilir()
    {
        // Davranış testi (D6): Komut Paleti'nin komut listesi gerçek Form1'den alınır; modal araçların erişim yolları (zamanlama, başarılar, eğitim modu, Nöbetçi, yardım) kopmamış olmalı.
        var adlar = Form1de(f => f._kisayol.Komutlar().Select(k => k.Ad).ToList(), rol: Rol.Yonetici);
        Assert.Equal(adlar.Count, adlar.Distinct().Count());
        foreach (var beklenen in new[] { "Zamanlanmış tarama ayarları", "Başarılar ve rozetler", "Eğitim modu: örnek sonuçları yükle (gerçek değil)", "Nöbetçi Modu", "Taramayı başlat (F5)", "Yardım (F1)", "Tanıtım Turu", "Acil İzolasyon (onay sorar)", "Olay Zaman Çizgisi (Ctrl+E)" })
            Assert.Contains(beklenen, adlar);
        Assert.True(adlar.Count >= 25);
    }

    [Fact] public void Kisayollar_ProcessCmdKey_Ctrl_D_E_F_F5_KabuktanSayfaAcar()
    {
        // Ctrl+K (komut paleti) ve F1 (yardım) modal pencere açtığı için çalıştırılmaz; kaynak eşlemeleri aşağıda denetlenir.
        var (d, e, f5, cf) = Form1de(f =>
        {
            ArayuzModu.Ayarla(ModTuru.Uzman); Application.DoEvents(); var k = Kabuk(f); ((Button)Alan(f, "btnBaslat")).Enabled = false;   // F5 yalnızca sayfaya gider; gerçek tarama başlamasın
            var isle = typeof(Form1).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
            bool Bas(Keys t) { var a = new object[] { default(Message), t }; var r = (bool)isle.Invoke(f, a)!; Application.DoEvents(); return r; }
            Assert.True(Bas(Keys.Control | Keys.D)); var ctrlD = (k.AktifSayfaId, ((SekmeliSayfa)((KolayUzmanSayfasi)k.Sayfa("durum")!).Gorunum.Controls.OfType<SekmeliSayfa>().Single()).AktifSekme);
            Assert.True(Bas(Keys.Control | Keys.E)); var ctrlE = k.AktifSayfaId;
            Assert.True(Bas(Keys.F5)); var f5s = (k.AktifSayfaId, ((SekmeliSayfa)k.Sayfa("tarama")!).AktifSekme);
            k.GitAsync("durum").GetAwaiter().GetResult(); Assert.True(Bas(Keys.Control | Keys.F)); var ctrlF = (k.AktifSayfaId, ((SekmeliSayfa)k.Sayfa("tarama")!).AktifSekme);
            return (ctrlD, ctrlE, f5s, ctrlF);
        }, rol: Rol.Yonetici);
        Assert.Equal(("durum", "pano"), d); Assert.Equal("olay", e); Assert.Equal(("tarama", "yeni"), f5); Assert.Equal(("tarama", "yeni"), cf);
        string? kok = AppContext.BaseDirectory; for (int i = 0; i < 8 && kok != null && !File.Exists(Path.Combine(kok, "Directory.Build.props")); i++) kok = Path.GetDirectoryName(kok); if (kok == null) return;
        string kisayol = File.ReadAllText(Path.Combine(kok, "LabDenetim", "Form1.cs"));
        Assert.Contains("case Keys.F1: _yardim.Yardim(); return true;", kisayol, StringComparison.Ordinal); Assert.Contains("case Keys.Control | Keys.K: _kisayol.KomutPaletiAc(); return true;", kisayol, StringComparison.Ordinal);
    }

    // ---- Saf yardımcılar ----
    [Fact] public void KonusanIlerleme_DortAdim_VeToplayiciMesajlari()
    {
        Assert.Equal("1/4: Siteye güvenli bir ziyaretçi gibi bağlanılıyor...", KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, 1)); Assert.StartsWith("4/4:", KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, 4)); Assert.StartsWith("4/4:", KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, 99)); Assert.StartsWith("1/4:", KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, -5));
        Assert.Equal(1, KonusanIlerleme.WebAdimi("HTTP başlıkları okunuyor…")); Assert.Equal(2, KonusanIlerleme.WebAdimi("TLS sertifikası okunuyor…")); Assert.Equal(2, KonusanIlerleme.WebAdimi("DNS kayıtları (SPF/DMARC/CAA) okunuyor…")); Assert.Equal(3, KonusanIlerleme.WebAdimi("security.txt aranıyor (RFC 9116)…")); Assert.Equal(0, KonusanIlerleme.WebAdimi("bilinmeyen")); Assert.Equal(0, KonusanIlerleme.WebAdimi(null));
        Assert.Equal(4, KonusanIlerleme.BilgisayarAdimlari.Length);
    }
    static PasifGozlem Gozlem(string metin = "") => new("ornek.com", DateTime.UtcNow, "test", null, null, null, SecurityTxt.Coz(metin, "https://ornek.com/.well-known/security.txt", true, "text/plain", DateTime.UtcNow), new());
    [Fact] public void MektupMetni_NazikVeAnlasilir_YerTutucularVar_BulguSirali()
    {
        var kurallar = PasifKurallar.Yukle(out _, null, "pasif_kurallar.json", Path.Combine(Path.GetTempPath(), "yok_" + Guid.NewGuid().ToString("N") + ".json"));
        var g = new PasifGozlem("ornek.com", DateTime.UtcNow, "test", new HttpGozlem("https://ornek.com/", 200, new Dictionary<string, string>(), new List<string>(), new List<string>(), "bilinmiyor"), null, null, SecurityTxt.Yok(), new List<string>());
        var pb = PasifKurallar.Degerlendir(g, kurallar); var m = MektupMetni.Uret(g, pb, "", "");
        Assert.Contains("Sayın Güvenlik / Bilgi İşlem Sorumlusu", m); Assert.Contains("[Adınız]", m); Assert.Contains("[İletişim bilginiz]", m); Assert.Contains("Ne demek:", m); Assert.Contains("Olası zarar:", m); Assert.Contains("hiçbir sisteme girmeden", m); Assert.Contains("Saygılarımla", m);
        Assert.True(m.IndexOf("1)") < m.IndexOf("2)")); Assert.Equal(pb.Count, System.Text.RegularExpressions.Regex.Matches(m, @"^\d+\) ", System.Text.RegularExpressions.RegexOptions.Multiline).Count);
        Assert.Contains("Ali Veli", MektupMetni.Uret(g, pb, "Ali Veli", "ali@ornek.com")); Assert.Contains("görmedim", MektupMetni.Uret(g, new List<PasifBulgu>(), "a", "b"));   // bulgu yoksa teşekkür mektubu
    }
    [Fact] public void KapiBulgulari_YalnizRiskliVeYerelOlmayan_Tekrarsiz()
    {
        var l = KolayModEylemleri.KolayKapiBulgulari(new[] { new DinleyenPort(445, "tcp", "0.0.0.0", 4), new DinleyenPort(445, "tcp", "::", 4), new DinleyenPort(3389, "tcp", "127.0.0.1", 9), new DinleyenPort(80, "tcp", "0.0.0.0", 1), new DinleyenPort(23, "tcp", "192.168.1.5", 7), new DinleyenPort(5432, "tcp", "::1", 8) });
        Assert.Equal(2, l.Count); Assert.Contains(l, b => b.Baslik.StartsWith("Açık port 445") && b.Onem == Onem.Orta); Assert.Contains(l, b => b.Baslik.StartsWith("Açık port 23") && b.Onem == Onem.Yuksek);
        Assert.Equal("Dosya Paylaşım Kapısı Ardına Kadar Açık", HalkDiliCevirici.Cevir(l.First(b => b.Baslik.Contains("445"))).Baslik);
    }

    // ---- Mektup penceresi ----
    [Fact] public void MektupFormu_Kurulur_AdresVeMesajGorunur()
    {
        Exception? hata = null; string mesaj = "";
        var t = new Thread(() =>
        {
            try
            {
                var g = Gozlem("Contact: mailto:guvenlik@ornek.com\nExpires: " + DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ") + "\n");
                using var f = new MektupFormu(g, new List<PasifBulgu>(), "Ali", "ali@x.com"); f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-5000, -5000); f.Show(); Application.DoEvents();
                mesaj = string.Join("|", f.Controls.Cast<Control>().SelectMany(Tum).OfType<TextBox>().Select(x => x.Text)) + "|" + string.Join("|", f.Controls.Cast<Control>().SelectMany(Tum).OfType<Label>().Select(x => x.Text)); f.Close();
            }
            catch (Exception ex) { hata = ex; }
        }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); if (hata != null) throw hata;
        Assert.Contains("Kime Gönderilecek?", mesaj); Assert.Contains("Gönderilecek Nazik Mesaj", mesaj); Assert.Contains("guvenlik@ornek.com", mesaj); Assert.Contains("Ali", mesaj);
    }
    static IEnumerable<Control> Tum(Control c) { yield return c; foreach (Control k in c.Controls) foreach (var x in Tum(k)) yield return x; }
}
