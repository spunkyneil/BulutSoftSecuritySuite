// Tarama ekranının ÇALIŞMA mantığı (eskiden Form1.cs içindeydi): hedef/ayar doğrulama, taramayı başlat/durdur, sonuç ekleme, sayaçlar, rapor kaydetme, durum satırı.
using System.Diagnostics;
using System.Net;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Tarama ekranı (çalışma bölümü).
internal sealed partial class TaramaSayfasi
{
    // Barındıran pencere (durum çubuğu, tepsi, kabuk gezintisi): Form1'e değil yalnızca ITaramaSahibi arayüzüne bağlıdır.
    internal ITaramaSahibi Sahibi = null!;

    // Bulunan tüm sonuçların listesi (rapor yazarken buradan okunur).
    internal readonly List<Bulgu> _bulgular = new();
    // "Durdur" düğmesinin çalışmasını sağlayan şalter.
    internal CancellationTokenSource? _iptal;
    // Trafik lambası sayaçları: yeşil, sarı/turuncu, kırmızı kaç sonuç var.
    internal int _yesil, _sari, _kirmizi;

    // Kutulardaki yazıları kontrol eder; sorun varsa kibar uyarı verip false döner.
    internal bool KutulariKontrolEt()
    {
        // Hiçbir kontrol işaretli değilse uyar.
        if (!(chkPort.Checked || chkHttp.Checked || chkTls.Checked || chkDns.Checked || chkYapilandirma.Checked || chkGirdi.Checked))
            // Uyarıyı göster ve dur.
            return Uyar("Lütfen önce yukarıdan en az bir kontrol kutucuğunu işaretleyin.", chkPort);
        // Hedef adres her zaman gerekli; boşsa uyar.
        if (string.IsNullOrWhiteSpace(txtHedef.Text))
            // Uyarıyı göster ve dur.
            return Uyar("Lütfen önce yukarıdaki kutuya bir adres yazın (örnek: 127.0.0.1).", txtHedef);
        // Kapı yoklama veya zayıf ayarlar işaretliyse kapı numaraları gerekli.
        if ((chkPort.Checked || chkYapilandirma.Checked) && PortTarayici.PortListesiOku(txtPortlar.Text).Length == 0)
            // Uyarıyı göster ve dur.
            return Uyar("Lütfen kapı numaralarını yazın (örnek: 22,80,443). Bilmiyorsanız kutuyu hazır haline getirmek için yazıyı silip tekrar açın.", txtPortlar);
        // Web adresi gereken kontroller işaretliyse adresi kontrol et.
        if (chkHttp.Checked || chkTls.Checked || chkGirdi.Checked)
        {
            // Adres boşsa uyar.
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
                // Uyarıyı göster ve dur.
                return Uyar("Lütfen önce 'Web sitesi adresi' kutusuna bir adres yazın (örnek: http://127.0.0.1:8080/).", txtUrl);
            // Adres geçerli bir web adresi değilse uyar.
            if (!Uri.TryCreate(txtUrl.Text.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                // Uyarıyı göster ve dur.
                return Uyar("Web adresi http:// veya https:// ile başlamalıdır (örnek: http://127.0.0.1:8080/).", txtUrl);
        }
        // Form kutusu testi işaretliyse kutucuk adı gerekli.
        if (chkGirdi.Checked && string.IsNullOrWhiteSpace(txtParametre.Text))
            // Uyarıyı göster ve dur.
            return Uyar("Lütfen test edilecek kutucuğun adını yazın (örnek: q).", txtParametre);
        // İsim kayıtları işaretliyse alan adı gerekli.
        if (chkDns.Checked && string.IsNullOrWhiteSpace(txtAlanAdi.Text))
            // Uyarıyı göster ve dur.
            return Uyar("Lütfen alan adını yazın (örnek: localhost).", txtAlanAdi);
        // Hepsi tamam.
        return true;
    }


    // Kibar uyarı penceresi gösterir ve sorunlu kutuya imleci götürür.
    internal bool Uyar(string mesaj, Control odak)
    {
        // Uyarı penceresini göster.
        TemaliMesaj.Goster(mesaj, "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information);
        // Sorunlu kutu gizli "Gelişmiş" panelindeyse paneli aç ki kullanıcı kutuyu görsün.
        if (odak != txtHedef && !tblGel.Visible) GelismisAc(true);
        // İmleci sorunlu kutuya götür.
        odak.Focus();
        // "Tamam değil" cevabı ver.
        return false;
    }


    // Çalışırken kutuları ve butonları kilitler; bitince açar.
    internal void Calisiyor(bool calisiyor)
    {
        // Çalışırken tekrar basılmasın diye başlat ve rapor butonları kapansın.
        btnBaslat.Enabled = btnHtml.Enabled = btnPdf.Enabled = !calisiyor;
        // Sadece çalışırken "Durdur" açık olsun.
        btnIptal.Enabled = calisiyor;
        // İlerleme çubuğu yalnızca çalışırken görünsün.
        progress.Visible = calisiyor;
        // Çalışırken kutular değiştirilemesin.
        foreach (var c in new Control[] { txtHedef, txtPortlar, txtUrl, txtParametre, txtAlanAdi, chkPort, chkHttp, chkTls, chkDns, chkYapilandirma, chkGirdi, flwProfil, btnHedefSec })
            // Kutuyu kapat/aç.
            c.Enabled = !calisiyor;
        // Çalışırken çubuk sürekli hareket etsin (ne kadar süreceği bilinmiyor).
        progress.Style = calisiyor ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        // Hareket hızını ayarla.
        progress.MarqueeAnimationSpeed = calisiyor ? 30 : 0;
        // Bitince çubuğu sıfırla.
        if (!calisiyor) progress.Value = 0;
        // Çalışmaya başlarken kronometreyi sıfırdan başlat ve kartları canlı yenileyen zamanlayıcıyı aç.
        if (calisiyor) { _saat.Restart(); _zamanlayici.Start(); }
        // Bitince kronometreyi ve zamanlayıcıyı durdur, son rakamları yaz.
        else { _saat.Stop(); _zamanlayici.Stop(); KartlariGuncelle(); }
        // Radardaki "TARANIYOR / BEKLEMEDE" durumunu güncelle.
        RadarYenile();
    }


    // "⚡ TARAMAYI BAŞLAT" butonuna basılınca çalışır. async: program donmadan arka planda çalışır.
    internal void btnBaslat_Click(object? sender, EventArgs e) => GuvenliCalistir.Baslat(() => btnBaslat_ClickAsync(sender, e), "Form1.btnBaslat_Click");

    internal async Task btnBaslat_ClickAsync(object? sender, EventArgs e)
    {
        // Yetki kontrolü: bu işlem için rolünüz yeterli mi?
        if (!KabukYetki.Kontrol(this, Eylem.Tarama, "Tarama Başlat")) return;
        // Lisans kontrolü: yalnızca YENİ tarama başlatma kısıtlanır; kontrol hata verirse kısıtlanmaz (güvenli tarafta kalınır).
        var lisansIzni = Lisans.YeniTaramaIzni(Lisans.Durum()); if (!lisansIzni.Izinli) { TemaliMesaj.Goster(lisansIzni.Mesaj, "Lisans", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Hedef genel internetteyse (dış ağ) "yalnızca pasif gözlem" akışı çalışır ve burada biter; iç ağ hedefleri eski tam akışla devam eder.
        if (await DisAgPasifDeneAsync()) return;
        // Önce kutuları kontrol et; sorun varsa uyarı verip çık (program çökmez).
        if (!KutulariKontrolEt()) return;
        // Kullanıcı günlüğü: kim, hangi hedefte tarama başlattı.
        KullaniciGunlugu.Yaz("Tarama başlattı (hedef: " + txtHedef.Text.Trim() + ")");
        // Eski sonuç listesini boşalt.
        _bulgular.Clear();
        // Tablodaki eski satırları sil.
        dgvBulgular.Rows.Clear();
        // Sayaçları sıfırla.
        _yesil = _sari = _kirmizi = 0;
        // Kapı sayaçlarını da sıfırla.
        _taranan = _toplamKapi = 0;
        // Kronometreyi sıfırla (kartta 00:00 görünsün).
        _saat.Reset();
        // Eski kayıttan kalan süreyi de temizle (kronometre yeniden kullanılsın).
        _sureOverride = null;
        // Sayaç yazılarını güncelle.
        SayaclariYaz();
        // Yeni bir "durdur şalteri" hazırla.
        _iptal = new CancellationTokenSource();
        // Şalterin işaretini kısa isimle sakla.
        var ct = _iptal.Token;
        // Her şeyi kilitle, çubuğu hareket ettir.
        Calisiyor(true);
        // Günlüğe ilk operasyon satırını yaz.
        Gunluk($"⚡ DENETİM MOTORU HAZIR -> Hedef: {txtHedef.Text.Trim()}", RakamYesil);

        // Hata olursa programın çökmemesi için korumalı bölge başlat.
        try
        {
            // Hedefin sizin yetkili ortamınızda olduğunu doğrula; değilse hata verip durur.
            var ip = await LabKapsam.DogrulaVeCozAsync(txtHedef.Text, ct);
            // Güvenlik kilidinin hedefi onayladığını günlüğe yaz.
            Gunluk($"GÜVENLİK KİLİDİ -> {ip} yetkili kapsamda, işlem devam ediyor.", Elektrik);
            // Sensör seçiliyse hedefin sensör için uygun olduğunu doğrula ve nereden taranacağını günlüğe yaz.
            _sensorYeri.HedefiDogrula(ip); if (_sensorYeri.Secilen() is { } snsr) Gunluk($"TARAMA YERİ -> Kapı yoklaması ve HTTP/TLS/DNS kontrolleri \"{snsr.Ad}\" sensöründen (SSH) yapılacak.", Elektrik);
            // Hedef bu bilgisayarın kendisi mi? (Asistanın "Otomatik Onar" düğmesi buna göre açılır.)
            _sonHedefYerel = YerelAdresMi(ip);
            // Hedefin sayısal adresini sakla (onarım sonrası doğrulama için).
            _sonHedefIp = ip;
            // Yazdığınız kapı numaralarını sayı listesine çevir.
            var portlar = PortTarayici.PortListesiOku(txtPortlar.Text);
            // Bulunan açık kapıların listesini hazırla.
            List<PortSonucu> acik = new();

            // Kapı yoklama veya zayıf ayar kutusu işaretliyse kapılara bak.
            if (chkPort.Checked || chkYapilandirma.Checked)
            {
                // Durum yazısını güncelle.
                Durum($"Kapılar yoklanıyor: {portlar.Length} kapı...");
                // Kapı yoklarken çubuk gerçek ilerlemeyi göstersin.
                progress.Style = ProgressBarStyle.Continuous;
                // Çubuğun sonunu kapı sayısı yap, başa al.
                progress.Maximum = Math.Max(1, portlar.Length); progress.Value = 0;
                // Her kapı yoklanınca çubuğu ilerleten yardımcı.
                // Karttaki "toplam kapı" rakamını yaz.
                _toplamKapi = portlar.Length;
                // Her kapı yoklanınca hem çubuğu hem karttaki "yoklanan kapı" rakamını güncelle.
                // Günlüğe "yüzde kaçı bitti" satırını yalnızca %25, %50, %75, %100'de yazmak için sayaç.
                int sonCeyrek = 0;
                // İlerleme yardımcısı: çubuğu, kartı ve günlüğü günceller.
                var ilerleme = new Progress<int>(v =>
                {
                    // Çubuğu ilerlet.
                    progress.Value = Math.Min(v, progress.Maximum);
                    // Karttaki yoklanan sayısını güncelle.
                    _taranan = v;
                    // Kartları yenile.
                    KartlariGuncelle();
                    // Şu an yüzde kaçta olduğumuzu çeyrek (25'lik) cinsinden hesapla.
                    int ceyrek = v * 4 / Math.Max(1, portlar.Length);
                    // Yeni bir çeyreğe ulaştıysak günlüğe yaz.
                    if (ceyrek > sonCeyrek) { sonCeyrek = ceyrek; Gunluk($"KAPI YOKLAMA -> %{ceyrek * 25} ({v}/{portlar.Length})", Elektrik); }
                });
                // Günlüğe taramanın başladığını yaz.
                Gunluk($"KAPI YOKLAMA BAŞLADI -> {portlar.Length} kapı analiz ediliyor...", RakamYesil);
                // Kapıları yokla; açık bir kapı bulunduğu anda günlüğe sarı satır yaz.
                // Sensör seçiliyse tarama sensörden, değilse bu bilgisayardan yapılır.
                acik = _sensorYeri.Secilen() is { } sensor ? await SensorTaramaYardimcisi.PortTaraAsync(sensor, ip.ToString(), portlar, ilerleme, ct, p => GunlukGuvenli($"PORT {p.Port} AÇIK -> {Buyuk(p.Servis)} bulundu, servis kimliği (yanıt) okunuyor...", Simsek)) : await PortTarayici.TaraAsync(ip, portlar, ilerleme, ct, p => GunlukGuvenli($"PORT {p.Port} AÇIK -> {Buyuk(p.Servis)} bulundu, servis kimliği (yanıt) okunuyor...", Simsek));
                // Sonraki adımlar için çubuk tekrar sürekli hareket etsin.
                progress.Style = ProgressBarStyle.Marquee;
                // Hareket hızını yeniden ver.
                progress.MarqueeAnimationSpeed = 30;
                // Bulunan her açık kapıyı tabloya ekle.
                foreach (var p in acik)
                    // Tabloya "bilgi" (yeşil) olarak yaz.
                {
                    Ekle(PortTarayici.BulguOlustur(p));
                    // Tabela eski/desteği bitmiş bir yazılımı gösteriyorsa ayrı bir uyarı satırı ekle (mevcut satıra dokunmaz).
                    foreach (var eski in DesteksizYazilim.PortBulgulari(p)) Ekle(eski);
                }
                // Hiç açık kapı yoksa güzel haber ver.
                if (acik.Count == 0)
                    // Yeşil bir sonuç ekle.
                    Ekle(new Bulgu("Port", "Yoklanan kapıların hiçbiri açık değil", Onem.Bilgi, $"{portlar.Length} kapı yoklandı.", "Yapılacak bir şey yok."));
            }
            // Zayıf ayar kutusu işaretliyse ayarları değerlendir.
            if (chkYapilandirma.Checked)
            {
                // Durum yazısını güncelle.
                Durum("Zayıf ayarlar aranıyor...");
                // Her bulguyu tabloya ekle.
                foreach (var b in await YapilandirmaDenetimi.DenetleAsync(ip, acik, ct)) Ekle(b);
            }
            // Web koruması kutusu işaretliyse web sitesine bak.
            if (chkHttp.Checked)
            {
                // Durum yazısını güncelle.
                Durum("Web sitesi korumaları kontrol ediliyor...");
                // Hata olursa diğer kontrolleri durdurma.
                await Guvenli(async () =>
                {
                    // Sensör seçiliyse kontrol SENSÖRDEN yapılır (hedef orada özel ağ kontrolünden geçer).
                    if (_sensorYeri.Secilen() is { } snsHttp) { foreach (var b in await SensorAg.HttpAsync(snsHttp, txtUrl.Text.Trim(), ct)) Ekle(b); return; }
                    // Web adresinin de sizin yetkili ortamınızda olduğunu doğrula.
                    await LabKapsam.DogrulaVeCozAsync(new Uri(txtUrl.Text.Trim()).Host, ct);
                    // Sonuçları tabloya ekle.
                    foreach (var b in await HttpDenetimi.DenetleAsync(txtUrl.Text.Trim(), ct)) Ekle(b);
                }, "HTTP");
            }
            // Şifreli bağlantı kutusu işaretliyse sertifikaya bak.
            if (chkTls.Checked)
            {
                // Durum yazısını güncelle.
                Durum("Şifreli bağlantı (sertifika) kontrol ediliyor...");
                // Hata olursa diğer kontrolleri durdurma.
                await Guvenli(async () =>
                {
                    // Web adresini parçalarına ayır.
                    var uri = new Uri(txtUrl.Text.Trim());
                    // Sensör seçiliyse kontrol SENSÖRDEN yapılır.
                    if (_sensorYeri.Secilen() is { } snsTls) { foreach (var b in await SensorAg.TlsAsync(snsTls, uri.Host, uri.Port, ct)) Ekle(b); return; }
                    // Adresin yetkili test ortamınızda olduğunu doğrula.
                    await LabKapsam.DogrulaVeCozAsync(uri.Host, ct);
                    // Sonuçları tabloya ekle.
                    foreach (var b in await TlsDenetimi.DenetleAsync(uri.Host, uri.Port, ct)) Ekle(b);
                }, "TLS");
            }
            // İsim kayıtları kutusu işaretliyse alan adına bak.
            if (chkDns.Checked)
            {
                // Durum yazısını güncelle.
                Durum("İsim kayıtları aranıyor...");
                // Hata olursa diğer kontrolleri durdurma.
                await Guvenli(async () =>
                {
                    // Sensör seçiliyse kontrol SENSÖRDEN yapılır (isimler sensörün DNS'iyle çözülür; Windows hosts dosyasındaki özel kayıtlar orada olmayabilir).
                    if (_sensorYeri.Secilen() is { } snsDns) { foreach (var b in await SensorAg.DnsAsync(snsDns, txtAlanAdi.Text.Trim(), ct)) Ekle(b); return; }
                    // Sonuçları tabloya ekle.
                    foreach (var b in await DnsDenetimi.DenetleAsync(txtAlanAdi.Text.Trim(), ct)) Ekle(b);
                }, "DNS");
                // Hedef bu bilgisayarsa hosts dosyasını ve DNS sunucularını da (zehirlenme/yönlendirme için) denetle.
                if (_sonHedefYerel)
                {
                    // Durum yazısını güncelle.
                    Durum("hosts dosyası ve DNS sunucuları kontrol ediliyor...");
                    // Okuma işlemini arka planda yap, sonuçları tabloya ekle.
                    await Guvenli(async () => { foreach (var b in await Task.Run(() => DnsDenetimi.HostsVeDnsDenetle(), ct)) Ekle(b); }, "Hosts");
                }
            }
            // Form kutusu testi işaretliyse siteyi dene.
            if (chkGirdi.Checked)
            {
                // Durum yazısını güncelle.
                Durum("Form kutusu testi yapılıyor...");
                // Hata olursa diğer kontrolleri durdurma.
                await Guvenli(async () =>
                {
                    // Sonuçları tabloya ekle.
                    foreach (var b in await GirdiDogrulamaTesti.TestEtAsync(txtUrl.Text.Trim(), txtParametre.Text.Trim(), ct)) Ekle(b);
                }, "Girdi");
            }
            // Hedef bu bilgisayarsa sistemin içine bak: kalıcılık, güvenlik karnesi, yerel hesaplar (yalnızca okuma).
            if (_sonHedefYerel)
            {
                // Durum yazısını güncelle.
                Durum("Kalıcılık, Windows güvenlik karnesi ve yerel hesaplar inceleniyor...");
                // Ağır okumayı arka planda yap, sonuçları tabloya ekle.
                await Guvenli(async () => { foreach (var b in await Task.Run(() => SistemDenetimi.Hepsi(), ct)) Ekle(b); }, "Sistem");
            }
            // Zincirleme risk analizi: bulunan sorunları birbirine bağlayıp saldırgan yolunu göster.
            _zincir.Analiz();
            // Kilitli Altın Profil varsa temiz referansla kıyasla.
            _altin.Karsilastir();
            // Her şey bitti; kaç sonuç bulunduğunu yaz.
            Durum($"Bitti. Toplam {_bulgular.Count} sonuç bulundu. Satırlara tıklayarak ne demek olduklarını okuyun, sonra \"Rapor İndir\" butonlarıyla raporu kaydedin.", false);
            // Günlüğe kısa bitiş özeti yaz: sonuç sayısı, puan ve süre.
            Gunluk($"⚡ OPERASYON TAMAMLANDI -> {_bulgular.Count} sonuç, güvenlik puanı {PuanHesapla()}/100, süre {_saat.Elapsed:mm\\:ss}", RakamYesil);
            // Ekranın köşesinde "tarama bitti" bildirimi göster (yüksek riskli sonuç varsa uyarı rengiyle).
            Bildirim.Goster("Tarama tamamlandı", $"{_bulgular.Count} sonuç · güvenlik puanı {PuanHesapla()}/100 · süre {_saat.Elapsed:mm\\:ss}", _bulgular.Any(b => b.Onem == Onem.Yuksek) ? BildirimTuru.Uyari : BildirimTuru.Basari);
            // Bitmiş taramayı geçmişe kaydet (program kapansa da silinmesin) ve önceki taramayla karşılaştır.
            GecmisKaydet();
        }
        // Kullanıcı "Durdur"a bastıysa.
        catch (OperationCanceledException) { Durum("Tarama durduruldu."); }
        // Başka bir hata olduysa.
        catch (Exception ex)
        {
            // Durum yazısına hatayı yaz.
            Durum("Bir sorun oldu: " + ex.Message, true, RakamKirmizi);
            // Kullanıcıya pencere ile göster.
            TemaliMesaj.Goster(ex.Message, "Tarama durduruldu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        // Ne olursa olsun sonunda kilitleri aç.
        finally { Calisiyor(false); }

        // Bir kontrol hata verirse diğerleri çalışmaya devam etsin diye sarmalayıcı.
        async Task Guvenli(Func<Task> isle, string modul)
        {
            // Korumalı bölge.
            try { await isle(); }
            // Durdurma isteği ise yukarı ilet.
            catch (OperationCanceledException) { throw; }
            // Diğer hatayı tabloya not olarak yaz.
            catch (Exception ex) { Ekle(new Bulgu(modul, "Bu kontrol çalıştırılamadı", Onem.Bilgi, ex.Message, "Adresi ve bağlantıyı kontrol edip tekrar deneyin.")); }
        }
    }


    // Tabloya yeni bir sonuç satırı ekler ve trafik lambası rengine boyar.
    internal void Ekle(Bulgu b)
    {
        // Kullanıcının "kabul edilen risk" dediği sonuçlar bilgi seviyesine indirilir (puanı düşürmez).
        b = KabulDeposu.Uygula(b);
        // Sonucu rapor listesine kaydet.
        _bulgular.Add(b);
        // Tabloya satır ekle ve yerini (numarasını) al.
        int i = dgvBulgular.Rows.Add(Rozet(b).Metin, b.Modul, b.Baslik, b.Detay, b.Oneri, Uyumluluk.Yazi(b));
        // Satırın içine sonucu sakla ki rozeti çizebilelim ve tıklanınca açıklayabilelim.
        dgvBulgular.Rows[i].Tag = b;
        // Açık bir filtre varsa ve bu sonuç filtreye uymuyorsa satırı gizle.
        if (!_arama.SatirGorunur(b)) { dgvBulgular.CurrentCell = null; dgvBulgular.Rows[i].Visible = false; }
        // Satır görünür ve zaten seçiliyse (ilk satır kendiliğinden seçilir) asistanı hemen güncelle.
        if (dgvBulgular.Rows[i].Visible && dgvBulgular.Rows[i].Selected) dgvBulgular_SelectionChanged(null, EventArgs.Empty);
        // İlgili sayacı artır (yapılamayan kontroller hiçbir sayaca girmez).
        if (Atlandi(b)) { }
        // Yeşil.
        else if (b.Onem == Onem.Bilgi) _yesil++;
        // Kırmızı.
        else if (b.Onem == Onem.Yuksek) _kirmizi++;
        // Sarı/turuncu.
        else _sari++;
        // Yeşil olmayan her bulguyu günlüğe renkli satır olarak yaz (yeşiller günlüğü kalabalık etmesin).
        if (b.Onem != Onem.Bilgi && !_gecmisYukleniyor)
            // Kırmızı bulgu kırmızı, turuncu bulgu turuncu, sarı bulgu sarı yazılır.
            Gunluk($"{(b.Onem == Onem.Yuksek ? "KRİTİK" : "UYARI")} -> {b.Baslik}", b.Onem == Onem.Yuksek ? RakamKirmizi : b.Onem == Onem.Orta ? DesignTokens.Ozgun(TokenRol.StatusWarning) : Simsek);
        // Sayaç yazılarını güncelle.
        SayaclariYaz();
    }


    // Sayaç kutularındaki yazıları günceller.
    internal void SayaclariYaz()
    {
        // Yeşil sayacın yazısı.
        lblYesil.Text = $"YEŞİL (Normal): {_yesil}";
        // Sarı/turuncu sayacın yazısı.
        lblSari.Text = $"SARI/TURUNCU (Dikkat): {_sari}";
        // Kırmızı sayacın yazısı.
        lblKirmizi.Text = $"KIRMIZI (Tehlike): {_kirmizi}";
        // Kolay görünümdeki küre rengi son tarama sonucunu izlesin.
        Sahibi.KureYansit();
        // Üstteki dört kartı da yeni rakamlara göre güncelle (kartlar henüz kurulmadıysa atla).
        if (lblKartKapi != null) KartlariGuncelle();
        // Veri değiştiği için radarı da tazele (radar henüz kurulmadıysa atlanır).
        if (radar != null) RadarYenile();
        // Zincirleme risk / altın profil uyarı şeridini tazele.
        _zincir.SeritYenile();
    }


    // Önem derecesinin trafik lambasındaki adını verir.
    internal static string IsikAdi(Onem o) => o switch
    {
        // Yüksek: kırmızı.
        Onem.Yuksek => "KIRMIZI: Tehlike",
        // Orta: turuncu.
        Onem.Orta => "TURUNCU: Dikkat (önemli)",
        // Düşük: sarı.
        Onem.Dusuk => "SARI: Dikkat",
        // Bilgi: yeşil.
        _ => "YEŞİL: Normal"
    };
    // Önem derecesinin satır rengini verir.
    internal static Color IsikRengi(Onem o) => o switch
    {
        // Yüksek: açık kırmızı.
        Onem.Yuksek => DesignTokens.Ozgun(TokenRol.StatusDanger),
        // Orta: açık turuncu.
        Onem.Orta => DesignTokens.Ozgun(TokenRol.StatusWarning),
        // Düşük: açık sarı.
        Onem.Dusuk => DesignTokens.Ozgun(TokenRol.StatusWarning),
        // Bilgi: açık yeşil.
        _ => DesignTokens.Ozgun(TokenRol.StatusSuccessBg)
    };
    // Tabloda fareyle tıklanınca (özellikle sağ tıkta) o satırı seçer.
    internal void dgvBulgular_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        // Başlık satırına veya boş yere tıklandıysa bir şey yapma.
        if (e.RowIndex < 0) return;
        // Önceki seçimleri kaldır.
        dgvBulgular.ClearSelection();
        // Tıklanan satırı seç.
        dgvBulgular.Rows[e.RowIndex].Selected = true;
    }


    // Sağ tık menüsü: "Sonucu Kopyala".
    internal void miKopyala_Click(object? sender, EventArgs e)
    {
        // Seçili satır yoksa uyar.
        if (dgvBulgular.SelectedRows.Count == 0 || dgvBulgular.SelectedRows[0].Tag is not Bulgu b)
        {
            // Kibar uyarı göster.
            TemaliMesaj.Goster("Lütfen önce tablodan kopyalamak istediğiniz satıra tıklayın.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // Çık.
            return;
        }
        // Panoya (kopyala-yapıştır hafızasına) yazılacak metin.
        var metin = $"[{IsikAdi(b.Onem)}] {b.Baslik}\r\nAyrıntı: {b.Detay}\r\nNe Yapmalısın?: {b.Oneri}";
        // Korumalı bölge: pano bazen meşgul olabilir.
        try { Clipboard.SetText(metin); Durum("Sonuç kopyalandı. Şimdi istediğiniz yere yapıştırabilirsiniz (Ctrl+V)."); }
        // Olmazsa kibarca bildir.
        catch (Exception ex) { TemaliMesaj.Goster("Kopyalanamadı: " + ex.Message, "Sorun oldu"); }
    }


    // Sağ tık menüsü: "Tüm Raporu Masaüstüne Kaydet" (hem web hem PDF).
    internal void miMasaustu_Click(object? sender, EventArgs e)
    {
        // Henüz sonuç yoksa uyar.
        if (_bulgular.Count == 0) { TemaliMesaj.Goster("Önce \"⚡ TARAMAYI BAŞLAT\" butonuna basıp bir tarama yapın.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Korumalı bölge.
        try
        {
            // Masaüstü klasörünün yerini öğren.
            var masaustu = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            // Dosya adlarının ortak kısmı: tarih ve saat (üst üste yazılmasın).
            var ad = Path.Combine(masaustu, $"BulutSoft_Rapor_{DateTime.Now:yyyyMMdd_HHmmss}");
            // Web sayfası raporunu yaz.
            RaporUretici.HtmlYaz(ad + ".html", RaporBilgisiOlustur(), _bulgular, RaporAciklayici);
            // PDF raporunu yaz.
            RaporUretici.PdfYaz(ad + ".pdf", RaporBilgisiOlustur(), _bulgular);
            // Durum yazısını güncelle.
            Durum("Rapor masaüstüne kaydedildi: " + Path.GetFileName(ad) + ".html ve .pdf");
            // Web sayfası raporunu tarayıcıda aç (açılamazsa sorun değil, dosya zaten kaydedildi).
            try { Process.Start(new ProcessStartInfo(ad + ".html") { UseShellExecute = true }); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Form1.miMasaustu_Click"); }
        }
        // Yazma sırasında sorun olursa göster.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Rapor kaydedilemedi"); }
    }


    // Durum yazısını değiştirir; istenirse aynı cümleyi günlüğe de yazar (renk verilmezse mavi).
    internal void Durum(string metin, bool gunluge = true, Color? renk = null)
    {
        // Ekrandaki durum yazısını güncelle.
        Sahibi.DurumYaz(metin);
        // Günlüğe de yazılacaksa büyük harfle ve renkli olarak ekle.
        if (gunluge) Gunluk(Buyuk(metin), renk ?? Elektrik);
    }


    // "Durdur" butonuna basılınca şalteri kapat.
    internal void btnIptal_Click(object? sender, EventArgs e) { if (_iptal != null) KullaniciGunlugu.Yaz("Taramayı iptal etti"); _iptal?.Cancel(); }


    // "▤ HTML Rapor İndir" butonuna basılınca raporu kaydet.
    internal void btnHtml_Click(object? sender, EventArgs e) => RaporKaydet("Web sayfası|*.html", "rapor.html",
        // Seçilen yere web sayfası raporunu yaz.
        yol => RaporUretici.HtmlYaz(yol, RaporBilgisiOlustur(), _bulgular, RaporAciklayici));


    // "▦ PDF Rapor İndir" butonuna basılınca raporu kaydet.
    internal void btnPdf_Click(object? sender, EventArgs e) => RaporKaydet("PDF dosyası|*.pdf", "rapor.pdf",
        // Seçilen yere PDF raporunu yaz.
        yol => RaporUretici.PdfYaz(yol, RaporBilgisiOlustur(), _bulgular));


    // Dosyayı nereye kaydedeceğini sorup raporu yazar.
    internal void RaporKaydet(string filtre, string ad, Action<string> yaz)
    {
        // Henüz sonuç yoksa kullanıcıyı uyar.
        if (_bulgular.Count == 0) { TemaliMesaj.Goster("Önce \"⚡ TARAMAYI BAŞLAT\" butonuna basıp bir tarama yapın.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // "Farklı kaydet" penceresini hazırla.
        using var sfd = new SaveFileDialog { Filter = filtre, FileName = ad };
        // Kullanıcı vazgeçtiyse çık.
        if (sfd.ShowDialog() != DialogResult.OK) return;
        // Korumalı bölge.
        try { yaz(sfd.FileName); Durum("Rapor kaydedildi: " + sfd.FileName); }
        // Yazma sırasında sorun olursa göster.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Rapor kaydedilemedi"); }
    }


    // F5: Tarama Merkezi › Yeni Tarama + Asistan'a geçip taramayı başlatır (Kolay görünümde bu ekran yoktur).
    internal void TaramaKisayolu() => GuvenliCalistir.Baslat(() => TaramaKisayoluAsync(), "Form1.TaramaKisayolu");

    internal async Task TaramaKisayoluAsync()
    {
        if (!Sahibi.KabukHazir || ArayuzModu.Mevcut == ModTuru.Kolay) return;
        if (await Sahibi.SayfayaGitAsync("tarama", "yeni") && btnBaslat.Enabled) btnBaslat.PerformClick();
    }


    // Ctrl+F: sonuç tablosundaki arama kutusuna gider (Kolay görünümdeyse Uzman'a geçilir).
    internal void SonucAraKisayolu() => GuvenliCalistir.Baslat(() => SonucAraKisayoluAsync(), "Form1.SonucAraKisayolu");

    internal async Task SonucAraKisayoluAsync()
    {
        if (!Sahibi.KabukHazir) return;
        if (ArayuzModu.Mevcut == ModTuru.Kolay) ArayuzModu.Ayarla(ModTuru.Uzman);
        if (await Sahibi.SayfayaGitAsync("tarama", "yeni")) { txtAra.Focus(); txtAra.SelectAll(); }
    }
    // ---- Dışarı taşınan özellikler (Servisler\*Eylemi) ----
    internal KanitPaketiEylemi _kanit = null!; internal AltinProfilEylemi _altin = null!; internal ZincirEylemi _zincir = null!; internal AramaKabulEylemi _arama = null!; internal SensorYeriSecici _sensorYeri = null!;
    internal AcilIzolasyonEylemi _acil = null!; internal HedefSeciciEylemi _hedefSec = null!; internal UsbKalkaniEylemi _usb = null!; internal TuzakEylemi _tuzak = null!;
    internal readonly UyariKutusu _uyariKutusu = new();

    // Denetleyicileri kurar: ana pencere ve bu ekranın denetimleri hazır olduktan hemen sonra (Form1 kurucusu) çağrılır.
    internal void EylemleriKur()
    {
        _usb = new UsbKalkaniEylemi(this, _uyariKutusu); _tuzak = new TuzakEylemi(this, _uyariKutusu);
        _kanit = new KanitPaketiEylemi(this, btnKanit); _altin = new AltinProfilEylemi(this, lblAltinDurum); _zincir = new ZincirEylemi(this, lblSerit, pnlSolAlan, () => _gorunum == Gorunum.Trafik);
        _acil = new AcilIzolasyonEylemi(this, Sahibi.Savunma); _hedefSec = new HedefSeciciEylemi(this, ctxHedef, btnHedefSec);
        _sensorYeri = new SensorYeriSecici(tblGel, ipucu);
        _arama = new AramaKabulEylemi(this, txtAra, dgvBulgular, ctxMenu, FiltreyiUygula, b => FiltreyeUyar(_filtre, b));
        OlaylariBagla();
    }

    // Tarama ekranı denetimlerinin olaylarını kendi işleyicilerine bağlar.
    internal void OlaylariBagla()
    {
        miKopyala.Click += miKopyala_Click;
        miMasaustu.Click += miMasaustu_Click;
        btnGelismis.Click += btnGelismis_Click;
        btnBaslat.Click += btnBaslat_Click;
        btnIptal.Click += btnIptal_Click;
        btnHedefSec.Click += (_, _) => _hedefSec.Ac();
        miHedefYerel.Click += (_, _) => _hedefSec.Yerel();
        miHedefAg.Click += (_, _) => _hedefSec.Ag();
        btnHtml.Click += btnHtml_Click;
        btnPdf.Click += btnPdf_Click;
        btnKanit.Click += (_, _) => _kanit.Tikla();
        dgvBulgular.SelectionChanged += dgvBulgular_SelectionChanged;
        dgvBulgular.CellMouseDown += dgvBulgular_CellMouseDown;
        dgvBulgular.CellPainting += dgvBulgular_CellPainting;
        btnKopyala.Click += btnKopyala_Click;
        btnOnar.Click += btnOnar_Click;
        btnModYerel.Click += (_, _) => ModSec(false);
        btnModClaude.Click += (_, _) => ModSec(true);
        btnApiAyar.Click += btnApiAyar_Click;
        btnDerinAnaliz.Click += btnDerinAnaliz_Click;
        btnPromptKopyala.Click += btnPromptKopyala_Click;
        btnAltin.Click += (_, _) => _altin.Tikla();
    }


    // "Gelişmiş" panelini açar veya kapatır (açık: true).
    internal void GelismisAc(bool ac)
    {
        // Paneli göster ya da gizle.
        tblGel.Visible = ac;
        // Düğmenin yazısındaki ok yönünü değiştir.
        btnGelismis.Text = ac ? "Gelişmiş ▴" : "Gelişmiş ▾";
    }

    // "Gelişmiş" düğmesine basılınca: açıksa kapat, kapalıysa aç.
    internal void btnGelismis_Click(object? sender, EventArgs e) => GelismisAc(!tblGel.Visible);

    // ---- ITaramaBaglami: dışarı taşınan özelliklerin (Servisler\*Eylemi) ana pencere'e dar bağlantısı ----
    IWin32Window ITaramaBaglami.Sahip => Sahibi.Pencere;
    IReadOnlyList<Bulgu> ITaramaBaglami.Bulgular => _bulgular;
    string ITaramaBaglami.Hedef { get => txtHedef.Text.Trim(); set => txtHedef.Text = value; }
    int ITaramaBaglami.Kirmizi => _kirmizi;
    bool ITaramaBaglami.TaramaSuruyor => !btnBaslat.Enabled;
    int ITaramaBaglami.Puan => PuanHesapla();
    void ITaramaBaglami.Gunluk(string mesaj, Color renk) => Gunluk(mesaj, renk);
    void ITaramaBaglami.Durum(string metin, bool gunluge, Color? renk) => Durum(metin, gunluge, renk);
    void ITaramaBaglami.BulguEkle(Bulgu b) => Ekle(b);
    bool ITaramaBaglami.PencereHazir => Sahibi.PencereHazir;
    void ITaramaBaglami.UiIsle(Action is_) => Sahibi.UiIsle(is_);
    void ITaramaBaglami.TepsiBalonu(int ms, string baslik, string metin, bool uyari) => Sahibi.TepsiBalonu(ms, baslik, metin, uyari);
    void ITaramaBaglami.KapanirkenCalistir(Action is_) => Sahibi.KapanirkenCalistir(is_);
    RaporBilgisi ITaramaBaglami.RaporBilgisiOlustur() => RaporBilgisiOlustur();
    (string NeDemek, string Nasil, string Komut) ITaramaBaglami.RaporAciklayici(Bulgu b) => RaporAciklayici(b);
}
