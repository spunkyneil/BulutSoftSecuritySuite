// Kolay Görünüm: üç kart (bilgisayar kontrolü, web sitesi incelemesi, karne), küre yansıtma ve mod uygulama. Eskiden Form1'in KolayMod.cs partial'ıydı; artık ayrı sınıf:
// ana pencereden yalnızca TemaliForm, tarama ekranı, kabuk gezintisi, kabuğa erişim, Nöbetçi ve küre yöneticisi alır.
using LabDenetim.V2;

// Programımızın adı.
namespace LabDenetim;

// Kolay Görünüm eylemleri.
internal sealed class KolayModEylemleri
{
    private readonly TemaliForm _f; private readonly TaramaSayfasi _tarama; private readonly Action<string, string?, bool> _kabukGit; private readonly Func<AppShell?> _kabuk; private readonly NobetciEylemi _nobet; private readonly UzmanKureYoneticisi _kure;
    // kabuk: kurumsal kabuğa (kuruluncaya kadar null) erişim; kure: Uzman Görünümün harita küresi.
    public KolayModEylemleri(TemaliForm f, TaramaSayfasi tarama, Action<string, string?, bool> kabukGit, Func<AppShell?> kabuk, NobetciEylemi nobet, UzmanKureYoneticisi kure) { _f = f; _tarama = tarama; _kabukGit = kabukGit; _kabuk = kabuk; _nobet = nobet; _kure = kure; }

    internal KolayModPaneli _kolay = null!;
    internal KaliGostergeleri _kali = null!;

    // Form1 açılırken bir kez çağrılır. (Kolay panel kabuğun Durum Panosu sayfasına, mod anahtarı üst çubuğa KabukBagla ile yerleşir.)
    internal void Kur()
    {
        _kali = new KaliGostergeleri(_f, _tarama, _kabukGit, () => _kolay, _nobet, GostergeleriYenile);
        _kolay = new KolayModPaneli
        {
            BilgisayariKontrolEt = KolayBilgisayarAsync, SiteyiIncele = KolayWebAsync, RaporAl = KolayRaporAl, GostergeTiklandi = GostergeTiklandi,
            // "Ayrıntılar (Uzman Görünümü)": Uzman moda geçer ve Tarama Merkezi › Yeni Tarama + Asistan sekmesini açar.
            UzmanaGec = () => { ArayuzModu.Ayarla(ModTuru.Uzman); _kabukGit("tarama", "yeni", false); },
            MektupAc = s => { using var f = new MektupFormu(s.Gozlem, s.Bulgular, ServisBaglami.Oturum.Aktif?.Ad ?? "", ""); f.ShowDialog(_f); }
        };
        ArayuzModu.Degisti += () => { KullaniciGunlugu.Yaz("Görünümü " + (ArayuzModu.Mevcut == ModTuru.Kolay ? "Kolay" : "Uzman") + " yaptı"); if (_f.IsHandleCreated && !_f.IsDisposed) _f.BeginInvoke(ModuUygula); else ModuUygula(); };
        ModuUygula(); _kali.Bagla();
        // Canlı küre ve kalkan: kalkana tıklayınca mevcut Nöbetçi Modu düğmesi tetiklenir (pilot modda yalnızca durum yansıtılır); tema değişince güncellenir.
        _kolay.Kure.KalkanTiklandi += _nobet.KalkanTiklandiAsync;
        TemaYonetici.Degisti += KureYansit; GorselAyarlar.Degisti += () => { if (_f.IsHandleCreated && !_f.IsDisposed) _f.BeginInvoke(KureYansit); }; KureYansit();
    }

    // Küre paneline tema, son tarama/alarm durumu ve Nöbetçi Modu (kalkan) bilgisini yollar.
    internal void KureYansit()
    {
        if (_kolay is null || _kolay.IsDisposed) return;
        // Durum: kırmızı bulgu varsa Kritik, sarı varsa Dikkat, yoksa Güvende.
        var durum = _tarama._kirmizi > 0 ? KureDurumu.Kritik : _tarama._sari > 0 ? KureDurumu.Dikkat : KureDurumu.Guvende;
        // Kalkan: Nöbetçi arayüzde açıksa YA DA koruma hizmeti çalışıp Nöbetçi'yi yürütüyorsa. Hizmet durumu ana gösterge olarak bannerda yazılır.
        bool koyu = TemaYonetici.Mod == TemaModu.Koyu, kalkan = _nobet._nobetAktif || _nobet._hizmetNobet, demo = GorselAyarlar.DemoYaylar; string hizmet = _nobet._hizmetCalisiyor ? "calisiyor" : ServisIstemci.Kurulu ? "kurulu" : "yok";
        _kolay.Kure.DemoYaylar = demo; _kolay.Kure.Ayarla(koyu, durum, kalkan, PilotMod.SaltOkunur, hizmet);
        // Uzman Görünümdeki harita küresi de aynı durumu yansıtır.
        var uk = _kure._uzmanKure; if (uk != null) uk.DemoYaylar = demo; uk?.Ayarla(koyu, durum, kalkan, PilotMod.SaltOkunur, hizmet);
    }

    // Seçili moda göre görünümü kurar.
    internal void ModuUygula()
    {
        // Kapanmış pencere (statik mod olayına bağlı kalmış olabilir) hiçbir şey yapmaz.
        if (_f.IsDisposed) return;
        bool kolay = ArayuzModu.Mevcut == ModTuru.Kolay;
        // Kabuk (sol menü, sekmeler, üst çubuk anahtarı) moda göre kendini günceller; sayfa içeriği kabukta.
        var kb = _kabuk(); if (kb is not null) kb.Mod = ArayuzModu.Mevcut;
        _f.Invalidate(true);
        if (kolay) GostergeleriYenile();
    }

    // Kolay görünümdeki üç gösterge (Kali / IP sızıntı / bildirim) — sonraki bölümlerde doldurulur.
    internal void GostergeleriYenile() => _kali.Yenile();
    internal void GostergeTiklandi(int sira) => _kali.Tiklandi(sira);

    // Sonucu Uzman tablosuna da yazar (eski ekranlar, rapor ve geçmiş aynı veriyi görsün).
    internal void KolayTabloyuDoldur(IEnumerable<Bulgu> l, string hedef, bool yerel)
    {
        _tarama._bulgular.Clear(); _tarama.dgvBulgular.Rows.Clear(); _tarama._yesil = _tarama._sari = _tarama._kirmizi = 0; _tarama._taranan = _tarama._toplamKapi = 0; _tarama._saat.Reset(); _tarama._sureOverride = null; _tarama.SayaclariYaz();
        _tarama.txtHedef.Text = hedef; _tarama._sonHedefYerel = yerel; foreach (var b in l) _tarama.Ekle(b); _tarama.GecmisKaydet();
    }

    // ---- Kart 1: Bilgisayarımı ve Şirketimi Kontrol Et ----
    internal async Task<List<Bulgu>?> KolayBilgisayarAsync(IProgress<string> ilerleme, CancellationToken ct)
    {
        var tum = new List<Bulgu>(); var kaynak = new GercekYerelKaynak(); var fp = FpDeposu.Yukle();
        // 1) Güvenlik ayarları, hesaplar, başlangıç programları (mevcut sistem denetimi: salt-okunur).
        ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.BilgisayarAdimlari, 1));
        tum.AddRange(await Task.Run(() => LabDenetim.Moduller.SistemDenetimi.Hepsi(), ct)); ct.ThrowIfCancellationRequested();
        // 2) Dışarıdan erişilebilen riskli kapılar.
        ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.BilgisayarAdimlari, 2));
        tum.AddRange(await Task.Run(() => KolayKapiBulgulari(kaynak.Dinleyenler()), ct)); ct.ThrowIfCancellationRequested();
        // 3) Hizmetler, Windows güncellemeleri, yazılımlar, program korumaları (salt-okunur yerel denetim).
        ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.BilgisayarAdimlari, 3));
        var katalog = KbCatalog.Yukle(); var nvd = NvdIndeks.Yukle(); var kev = VulnFeedStore.Yukle(); var harita = AssetStore.Load();
        var s = await Task.Run(() => YerelDenetimAkisi.Calistir(kaynak, katalog, harita, DateTime.UtcNow, fp, nvd, kev), ct);
        AssetStore.Save(s.Harita); BulguDeposu.Degistir(YerelDenetimAkisi.VarlikKimligi(kaynak), s.Bulgular); fp.Kaydet();
        tum.AddRange(s.Bulgular.Where(f => f.State != FindingState.FalsePositive).Select(V2Kayit.Donustur));
        // 4) Karne.
        ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.BilgisayarAdimlari, 4));
        KolayTabloyuDoldur(tum, "127.0.0.1", true);
        Bildirim.Goster("Kontrol tamamlandı", $"{KarneHesabi.Hesapla(tum).Baslik}", KarneHesabi.Hesapla(tum).Renk == 0 ? BildirimTuru.Basari : BildirimTuru.Uyari);
        return tum;
    }

    // Ağdan erişilebilen (yalnızca yerel olmayan) riskli kapılardan bulgu üretir.
    internal static List<Bulgu> KolayKapiBulgulari(IEnumerable<DinleyenPort> dinleyenler)
    {
        var riskli = new Dictionary<int, Onem> { [21] = Onem.Yuksek, [23] = Onem.Yuksek, [135] = Onem.Orta, [139] = Onem.Orta, [445] = Onem.Orta, [1433] = Onem.Orta, [3306] = Onem.Orta, [3389] = Onem.Orta, [5432] = Onem.Orta, [5900] = Onem.Yuksek };
        var l = new List<Bulgu>();
        foreach (var d in dinleyenler.Where(x => riskli.ContainsKey(x.Port) && !(System.Net.IPAddress.TryParse(x.Adres, out var ip) && System.Net.IPAddress.IsLoopback(ip))).GroupBy(x => x.Port).Select(g => g.First()))
            l.Add(new Bulgu("Port", $"Açık port {d.Port} ({LabDenetim.Moduller.PortTarayici.ServisAdi(d.Port)})", riskli[d.Port], $"Bu bilgisayarda {d.Port} numaralı kapı ağdan gelen bağlantıları kabul ediyor ({d.Adres}).", "Bu hizmete ihtiyacınız yoksa kapatın; gerekiyorsa yalnızca güvendiğiniz bilgisayarlara açın ve güçlü şifre kullanın."));
        return l;
    }

    // ---- Kart 2: Bir Web Sitesini İncele ve Sahibine Bildir ----
    internal async Task<WebSonucu?> KolayWebAsync(IProgress<string> ilerleme, CancellationToken ct)
    {
        // Adres sor.
        var c = SoruFormu.Sor(_f, "Web Sitesini İncele", "İncelemek istediğiniz sitenin adresini yazın (örn. ornek.com). Siteye hiç zarar verilmez; yalnızca dışarıdan bakılır.", "İncele", new Alan("Web sitesi adresi"));
        if (c == null) return null; string adres = c[0].Trim(); var host = Kapsam.HostCikar(adres.Length > 0 && !adres.Contains("://") ? "https://" + adres : adres);
        if (host == null) { TemaliMesaj.Goster(_f, "Geçerli bir web sitesi adresi yazın (örn. ornek.com).", "Adres anlaşılamadı", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
        // Sınıflandır: yalnızca internetteki siteler.
        KapsamKarari karar; try { using var zs = CancellationTokenSource.CreateLinkedTokenSource(ct); zs.CancelAfter(TimeSpan.FromSeconds(10)); karar = await Kapsam.SiniflaAsync(host, zs.Token); } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { TemaliMesaj.Goster(_f, "Bu adres bulunamadı veya internete ulaşılamıyor.", "Siteye ulaşılamadı", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
        if (karar.Mod == KapsamModu.IcAg) { TemaliMesaj.Goster(_f, "Bu adres sizin kendi ağınızda.\r\n\r\nKendi bilgisayarınız ve ağınız için \"Bilgisayarımı ve Şirketimi Kontrol Et\" kartını kullanın (daha ayrıntılı web denetimi Uzman Görünümü'ndedir).", "Bu bir internet sitesi değil", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
        if (karar.Mod != KapsamModu.DisAgPasif) { TemaliMesaj.Goster(_f, karar.Neden, "Bu adres incelenemez", MessageBoxButtons.OK, MessageBoxIcon.Warning); return null; }
        // Site kullanıcının kendi sitesi mi? Evet derse sahiplik kanıtı istenir ve DERİN (nazik aktif) denetim açılır. Varsayılan Hayır.
        bool derin = false;
        if (TemaliMesaj.Goster(_f, $"\"{karar.Host}\" sizin kendi siteniz mi?\r\n\r\nEVET derseniz sitenin sahibi olduğunuzu kısa bir dosya ya da DNS kaydıyla kanıtlarsınız; sonra ek DERİN kontroller yapılır:\r\n  • Herkese açık kalmış gizli dosyalar (.git, .env, yedekler)\r\n  • Açık bırakılmış yönetim sayfası ve tehlikeli yöntemler\r\n  • Hata sayfalarından bilgi sızması\r\n  • Apache / Nginx için kopyala-yapıştır düzeltme önerileri\r\n\r\nHAYIR derseniz yalnızca dışarıdan, sıradan bir ziyaretçi gibi bakılır.", "Derin Denetim: Bu Site Sizin mi?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            // Zaten bu oturumda doğrulandıysa tekrar sorma.
            if (SahiplikDogrulama.OturumDogrulu(karar.Host)) derin = true;
            else { using var sf = new SahiplikFormu(karar.Host); derin = sf.ShowDialog(_f) == DialogResult.OK; if (!derin) TemaliMesaj.Goster(_f, "Sahiplik doğrulanmadığı için derin denetim yapılmayacak; yalnızca dışarıdan bakılabilir.", "Derin denetim kapalı", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }
        // Onay (varsayılan Hayır).
        string onay =$"\"{karar.Host}\" internetteki bir site.\r\n\r\nYalnızca dışarıdan, sıradan bir ziyaretçi gibi bakılacak:\r\n  • Ana sayfa birkaç kez açılır\r\n  • Güvenlik sertifikası (kimlik kartı) okunur\r\n  • E-posta kayıtları ve iletişim dosyası aranır\r\n\r\nSiteye girilmez, şifre denenmez, yük bindirilmez.\r\n\r\nBu siteyi incelemek için site sahibi olduğunuzu veya yazılı izniniz bulunduğunu onaylıyor musunuz?";
        if (derin) onay = $"\"{karar.Host}\" sitesinin sahipliği doğrulandı.\r\n\r\nDERİN denetim yapılacak (yavaş ve nazik, saniyede en çok 2 istek, toplam 300 istekten az):\r\n  • Ana sayfa, sertifika, e-posta kayıtları, iletişim dosyası\r\n  • Gizli dosya ve yedek adreslerinin var olup olmadığı (içerikleri SAKLANMAZ)\r\n  • Dizin listeleme, hata sayfası bilgi sızması, açık yöntemler\r\n\r\nSiteye giriş yapılmaz, şifre denenmez, veri değiştirilmez ya da silinmez.\r\n\r\nDevam edilsin mi?";
        if (Kapsam.HassasAlanMi(karar.Host)) onay += "\r\n\r\nUYARI: Bu adres kamu/askeri/eğitim kurumuna benziyor. İzinsiz inceleme hukuki sorun doğurabilir; yalnızca yetkiliyseniz devam edin.";
        if (TemaliMesaj.Goster(_f, onay, "İnternet Sitesi İnceleme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return null;
        var hak = Kapsam.GozlemHakki(DateTime.Now); if (!hak.Izinli) { TemaliMesaj.Goster(_f, hak.Neden, "Biraz bekleyin", MessageBoxButtons.OK, MessageBoxIcon.Information); return null; }
        // 1/4 ... 3/4: toplayıcının teknik mesajları adımlara çevrilir.
        ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, 1)); int son = 1;
        var g = await PasifToplayici.GozlemYapAsync(karar, m => { int a = KonusanIlerleme.WebAdimi(m); if (a > son) { son = a; ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, a)); } }, ct);
        // 4/4: kurallar, özet, mektup.
        ilerleme.Report(KonusanIlerleme.Cumle(KonusanIlerleme.WebAdimlari, 4));
        var kurallar = PasifKurallar.Yukle(out _); var pb = PasifKurallar.Degerlendir(g, kurallar); pb = pb.Concat(DesteksizYazilim.WebBulgulari(g.Http).Where(x => !pb.Any(y => y.RuleId == x.RuleId))).OrderByDescending(x => x.Etki).ToList(); var notlar = new List<string>(g.Notlar);
        // DERİN denetim: sahiplik doğrulandıysa nazik aktif sondalar (kapıda canlı doğrulama yeniden denetlenir).
        if (derin)
        {
            ilerleme.Report("Ek) Sitenizde derin kontroller yapılıyor (yavaş ve nazik, birkaç dakika sürebilir)...");
            AktifGozlem? ag = null; string? hata = null;
            foreach (var sema in new[] { "https", "http" })
            {
                try { ag = await AktifHttp.GozlemAsync($"{sema}://{karar.Host}", null, m => ilerleme.Report("Derin kontrol: " + m), AktifHttp.VarsayilanHizMs, ct); break; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { hata = ex.Message; }
            }
            if (ag != null)
            {
                var ab = AktifHttp.Degerlendir(ag); var varOlan = pb.Select(x => x.RuleId).ToHashSet(); pb = pb.Concat(ab.Where(x => !varOlan.Contains(x.RuleId))).OrderByDescending(x => x.Etki).ThenBy(x => x.RuleId).ToList(); notlar.AddRange(ag.Notlar); notlar.Add($"Derin denetim tamamlandı ({ag.IstekSayisi} nazik istek gönderildi).");
            }
            else notlar.Add("Derin denetim yapılamadı: " + (hata ?? "bilinmeyen neden") + " (pasif sonuçlar gösteriliyor).");
            // Apache / Nginx düzeltme önerileri bulguların ayrıntısına eklenir.
            pb = SunucuOnerileri.Zenginlestir(pb, karar.Host);
        }
        PasifDeposu.Gozlem = g; PasifDeposu.Bulgular = pb;
        var tablo = pb.Select(b => b.ToBulgu()).ToList(); foreach (var n in notlar) tablo.Add(new Bulgu("Pasif Gözlem", "Gözlem notu", Onem.Bilgi, n, "Gerekirse hedefi sahibiyle doğrulayın."));
        if (pb.Count == 0) tablo.Add(new Bulgu("Pasif Gözlem", "Pasif gözlemde sorun bulunmadı", Onem.Bilgi, "HTTP başlıkları, TLS sertifikası, DNS (SPF/DMARC) ve security.txt kontrol edildi.", "Yapılacak bir şey yok."));
        KolayTabloyuDoldur(tablo, karar.Host, false);
        // Derin denetimde: tek tıkla Apache veya Nginx düzeltme dosyası (yalnızca metin; hiçbir şey otomatik uygulanmaz).
        if (derin) DuzeltmeDosyasiSor(karar.Host, pb);
        return new WebSonucu(g, pb, tablo);
    }

    // Düzeltme dosyası: kullanıcı sunucusunu seçer, bulguların Apache/Nginx ayarları tek metin dosyasına yazılır. Hiçbir sunucuya dokunulmaz.
    internal void DuzeltmeDosyasiSor(string host, List<PasifBulgu> bulgular)
    {
        // Önerisi olan bulgu yoksa sorma.
        var idler = bulgular.Where(b => b.Onem != Onem.Bilgi).Select(b => b.RuleId).Where(SunucuOnerileri.Var).ToList(); if (idler.Count == 0) return;
        // Sunucu seçimi.
        var c = SoruFormu.Sor(_f, "Düzeltme Dosyası Hazırla", $"{idler.Count} bulgu için hazır sunucu ayarı var. Hangi web sunucusunu kullanıyorsunuz? Ayarlar yalnızca bir metin dosyasına yazılır; hiçbir şey otomatik uygulanmaz.", "Dosyayı Hazırla", new Alan("Web sunucusu", false, new[] { "Nginx", "Apache" }));
        if (c == null) return; var tur = c[0] == "Apache" ? SunucuTuru.Apache : SunucuTuru.Nginx;
        // Kaydet.
        using var d = new SaveFileDialog { Title = "Düzeltme dosyasını kaydet", Filter = "Metin dosyası (*.txt)|*.txt|Yapılandırma (*.conf)|*.conf", FileName = $"{host}_{tur}_duzeltmeler" };
        if (d.ShowDialog(_f) != DialogResult.OK) return;
        try { File.WriteAllText(d.FileName, SunucuOnerileri.TopluDosya(idler, host, tur), new System.Text.UTF8Encoding(false)); Bildirim.Goster("Düzeltme dosyası kaydedildi", Path.GetFileName(d.FileName) + " — uygulamadan önce yedek alın.", BildirimTuru.Basari); }
        catch (Exception ex) { TemaliMesaj.Goster(_f, "Dosya yazılamadı: " + ex.Message, "Kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // ---- Kart 3: Durum Karnesi ve Resmi Rapor ----
    internal void KolayRaporAl()
    {
        if (_tarama._bulgular.Count == 0) { TemaliMesaj.Goster(_f, "Henüz bir kontrol yapılmadı.\r\n\r\nÖnce \"Bilgisayarımı ve Şirketimi Kontrol Et\" veya \"Bir Web Sitesini İncele\" kartını kullanın; sonra karneyi buradan indirebilirsiniz.", "Önce bir kontrol yapın", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var d = new SaveFileDialog { Title = "Güvenlik karnesini kaydet", Filter = "1 sayfalık yönetim özeti (*.html)|*.html|Ayrıntılı rapor (*.pdf)|*.pdf", FileName = "Guvenlik_Karnesi_" + DateTime.Now.ToString("yyyyMMdd_HHmm") };
        if (d.ShowDialog(_f) != DialogResult.OK) return;
        try
        {
            string hedef = _tarama.txtHedef.Text.Trim(); int puan = _tarama.PuanHesapla(); var liste = _tarama._bulgular.ToList();
            if (Path.GetExtension(d.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                RaporUretici.PdfYaz(d.FileName, new RaporBilgisi(hedef, _tarama._sonTarih, "Kolay Görünüm", _tarama._toplamKapi, _tarama._saat.Elapsed, puan, _tarama._kirmizi, _tarama._sari, _tarama._yesil, liste.Count(TaramaSayfasi.Atlandi)), liste);
            else File.WriteAllText(d.FileName, YonetimKuruluRaporu.Html(hedef, _tarama._sonTarih, puan, liste, GecmisDeposu.Listele()), new System.Text.UTF8Encoding(false));
            Bildirim.Goster("Karne kaydedildi", Path.GetFileName(d.FileName), BildirimTuru.Basari);
        }
        catch (Exception ex) { TemaliMesaj.Goster(_f, "Karne yazılamadı: " + ex.Message, "Kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
