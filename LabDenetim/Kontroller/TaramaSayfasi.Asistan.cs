// Bu dosya sağdaki "Siber İlk Yardım ve Çözüm Asistanı" panelini çalıştırır:
// tablodan bir satıra tıklayınca (1) sade açıklama, (2) hazır çözüm komutu, (3) kopyala / otomatik onar düğmelerini gösterir.
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Form1'in onuncu parçası: çözüm asistanı.
internal sealed partial class TaramaSayfasi
{
    // Son taramanın hedefi bu bilgisayar mı? (Otomatik onarım yalnızca bu bilgisayarda çalışır.)
    internal bool _sonHedefYerel;
    // Son taramanın hedefinin sayısal adresi (onarım sonrası canlı doğrulamada o adrese yeniden bağlanılır).
    internal IPAddress _sonHedefIp = IPAddress.Loopback;
    // Seçili sonucun güvenlik duvarıyla kapatılabilecek port numarası (yoksa boş).
    internal int? _seciliPort;
    // Açıklama kutusunda kullanılan iki yazı tipi: normal ve kalın.
    internal Font? _fNormal, _fKalin;

    // Asistanın bir sonuç için hazırladığı yardım paketi.
    internal sealed record Cozum(string NeDemek, string NasilCozerim, string Komut, string KomutEtiketi, int? Port);

    // Verilen adresin bu bilgisayarın kendi adresi olup olmadığını söyler.
    internal static bool YerelAdresMi(IPAddress ip)
    {
        // 127.0.0.1 gibi "kendi bilgisayarım" adresiyse evet.
        if (IPAddress.IsLoopback(ip)) return true;
        // Bilgisayarın ağ kartlarındaki adreslerden biriyse evet.
        try { return NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses).Any(a => a.Address.Equals(ip)); }
        // Okunamazsa "hayır" de (güvenli taraf).
        catch { return false; }
    }

    // Windows'a "kaydırma çubuğunu en üste al" mesajı göndermek için gereken sistem çağrısı.
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr pencere, int mesaj, IntPtr w, IntPtr l);

    // Açıklama kutusunu en üste kaydırır (yazı eklenince kutu kendiliğinden en alta iner; biz geri alıyoruz).
    internal void AciklamaEnUste()
    {
        // Kutunun ekran karşılığı henüz yoksa bir şey yapma.
        if (!rtbDetay.IsHandleCreated) return;
        // 0x0115 = dikey kaydırma mesajı, 6 = "en üste git".
        SendMessage(rtbDetay.Handle, 0x0115, (IntPtr)6, IntPtr.Zero);
    }

    // Açıklama kutusuna renkli yazı ekler.
    internal void Yaz(string metin, Color renk, bool kalin = false)
    {
        // Yazı tiplerini ilk kullanımda hazırla.
        _fNormal ??= FontProvider.Al(FontRol.Body);
        // Kalın yazı tipi.
        _fKalin ??= FontProvider.Al(FontRol.Body, FontStyle.Bold);
        // İmleci en sona götür.
        rtbDetay.SelectionStart = rtbDetay.TextLength;
        // Hiçbir şey seçili olmasın.
        rtbDetay.SelectionLength = 0;
        // Yazı rengini ayarla.
        rtbDetay.SelectionColor = renk;
        // Yazı tipini ayarla.
        rtbDetay.SelectionFont = kalin ? _fKalin : _fNormal;
        // Yazıyı ekle.
        rtbDetay.AppendText(metin);
    }

    // Asistanı başlangıç haline getirir (hiçbir sonuç seçili değilken görünen yardım yazısı).
    internal void AsistanSifirla()
    {
        // Eski içeriği temizle.
        rtbDetay.Clear();
        // Başlık.
        Yaz("SONUÇLARI NASIL OKURUM?\n\n", Elektrik, true);
        // Rozet açıklamaları.
        Yaz("GÜVENLİ", DesignTokens.Ozgun(TokenRol.StatusSuccess), true); Yaz("  Normal, sorun yok.\n", Yazi);
        // Dikkat açıklaması.
        Yaz("DİKKAT / UYARI", DesignTokens.Ozgun(TokenRol.StatusWarning), true); Yaz("  Sıra gelince düzeltin.\n", Yazi);
        // Kritik açıklaması.
        Yaz("KRİTİK", DesignTokens.Ozgun(TokenRol.StatusDanger), true); Yaz("  Önce bunları düzeltin.\n", Yazi);
        // Atlandı açıklaması.
        Yaz("ATLANDI", DesignTokens.Ozgun(TokenRol.TextMuted), true); Yaz("  Kontrol yapılamadı; \"güvenli\" demek değildir.\n\n", Yazi);
        // Yönlendirme.
        Yaz("Soldaki tablodan bir satıra tıklayın. Burada o sonucun ne demek olduğunu, nasıl çözüleceğini ve hazır çözüm komutunu göreceksiniz.", YaziSilik);
        // En üste kaydır.
        rtbDetay.SelectionStart = 0; AciklamaEnUste();
        // Komut kutusunu boşalt.
        txtKomut.Text = "";
        // Komut başlığını sıfırla.
        lblKomutBaslik.Text = "ÇÖZÜM KOMUTU";
        // Seçili port yok.
        _seciliPort = null;
        // İki düğmeyi kapat.
        btnKopyala.Enabled = false; btnOnar.Enabled = false;
    }

    // Tabloda başka satır seçilince asistanı günceller.
    internal void dgvBulgular_SelectionChanged(object? sender, EventArgs e)
    {
        // Claude analizi sürerken kutunun içeriği değişmesin (cevap karışmasın).
        if (_analizIptal != null) return;
        // Seçili satır yoksa asistanı başlangıç haline getir.
        if (dgvBulgular.SelectedRows.Count == 0 || dgvBulgular.SelectedRows[0].Tag is not Bulgu b) { AsistanSifirla(); return; }
        // Seçili sonuç için yardım paketini göster.
        AsistanGoster(b);
    }

    // Seçili sonuç için açıklamayı, komutu ve düğmelerin durumunu ekrana koyar.
    internal void AsistanGoster(Bulgu b)
    {
        // Sonuç için yardım paketini hazırla.
        var c = CozumUret(b);
        // Rozetin yazısını ve rengini al.
        var (rozet, renk) = Rozet(b);
        // Eski içeriği temizle.
        rtbDetay.Clear();
        // Rozet adını renkli yaz.
        Yaz("● " + rozet + "  ", renk, true);
        // Sonucun başlığını beyaz ve kalın yaz.
        Yaz(b.Baslik + "\n", Color.White, true);
        // Ayrıntıyı silik yaz.
        Yaz(b.Detay + "\n\n", YaziSilik);
        // "Bu Ne Demek?" bölümü.
        Yaz("▸ Bu Ne Demek?\n", Elektrik, true); Yaz(c.NeDemek + "\n\n", Yazi);
        // "Nasıl Çözerim?" bölümü.
        Yaz("✓ Nasıl Çözerim?\n", RakamYesil, true); Yaz(c.NasilCozerim, Yazi);
        // En üste kaydır.
        rtbDetay.SelectionStart = 0; AciklamaEnUste();
        // Komut kutusunu ve başlığını doldur.
        txtKomut.Text = c.Komut; lblKomutBaslik.Text = c.KomutEtiketi;
        // Seçili portu sakla.
        _seciliPort = c.Port;
        // Komut varsa kopyala düğmesi açık olsun.
        btnKopyala.Enabled = c.Komut.Length > 0;
        // Otomatik onar: yalnızca port varsa ve hedef bu bilgisayarsa açık olsun.
        bool oto = c.Port != null && _sonHedefYerel;
        // Düğmenin durumunu ayarla.
        btnOnar.Enabled = oto;
        // Düğmenin neden açık/kapalı olduğunu fare açıklamasıyla anlat.
        ipucu.SetToolTip(btnOnar, oto
            ? $"Bu bilgisayarın Windows Güvenlik Duvarı'na, TCP {c.Port} numaralı porta gelen bağlantıları engelleyen bir kural ekler. Önce onayınız istenir; yönetici izni (UAC) gerekir."
            : c.Port == null
                ? "Bu bulgu güvenlik duvarı kuralıyla çözülmez; yukarıdaki \"Nasıl Çözerim?\" adımlarına bakın."
                : "Otomatik onarım yalnızca BU bilgisayarı taradığınızda çalışır. Uzak bir bilgisayar için komutu kopyalayıp o bilgisayarda çalıştırın.");
        // Yerel bilgisayar taranıyorsa ve sonuç bir kapıya aitse: o kapıyı hangi programın açtığını arka planda bul ve göster.
        if (_sonHedefYerel && c.Port is int kapi) _ = ProgramBulGosterAsync(kapi, b);
    }

    // Port numarasının ne işe yaradığını sade dille söyler (bilgi bankası dosyasından: tehdit_veritabani.json).
    internal static string HizmetAciklama(int port) => TehditVeritabani.HizmetAciklama(port);

    // Bir sonuç için sade açıklama, çözüm adımları ve hazır komut üretir.
    // Kurallar artık sabit kodda değil, %AppData%\BulutSoft\tehdit_veritabani.json dosyasındadır (bkz. TehditVeritabani.cs).
    internal Cozum CozumUret(Bulgu b)
    {
        // Başlık ve ayrıntıdan "Port 445" gibi bir port numarası ara.
        int? port = null;
        // Düzenli ifadeyle port numarasını yakala.
        var m = Regex.Match(b.Baslik + " " + b.Detay, @"[Pp]ort (\d{1,5})");
        // Bulunduysa ve geçerli bir port numarasıysa sakla.
        if (m.Success && int.TryParse(m.Groups[1].Value, out var pp) && pp is >= 1 and <= 65535) port = pp;
        // Başlığı kısa isimle sakla.
        string t = b.Baslik;
        // Bu bilgisayar mı taranıyor? Komutun türünü (Windows/Linux) belirler.
        bool win = _sonHedefYerel;

        // Otomatik onarımla kapatılıp doğrulanmış kapı.
        if (b.Baslik.EndsWith("— Onarıldı", StringComparison.Ordinal))
            return new("Bu kapı için güvenlik duvarı engelleme kuralı eklendi ve etkisi doğrulandı. Gelen bağlantılar artık engelleniyor.",
                "Yapılacak başka bir şey yok. Bu hizmeti yeniden kullanmak isterseniz aşağıdaki komutla kuralı kaldırabilir veya soldaki \"Aktif Kalkanlarım\" penceresinden tek tıkla geri alabilirsiniz.",
                port is int op ? $"netsh advfirewall firewall delete rule name=\"BulutSoft-Blok-{op}\"" : "", "KURALI KALDIRMA KOMUTU · Windows (Yönetici PowerShell)", null);
        // Kontrol yapılamadıysa.
        if (Atlandi(b))
            return new("Bu kontrol çalışamadı (örneğin hedefte bir web sitesi yoktu veya bağlantı kurulamadı). Bu \"güvenli\" demek değildir; sadece bakılamadı.",
                "Adresin doğru olduğundan ve hizmetin açık olduğundan emin olup taramayı tekrar çalıştırın. Hata mesajı Ayrıntı satırında yazıyor.", "", "ÇÖZÜM KOMUTU", null);
        // Canlı ağ trafiği bağlantıları kendi açıklama koduyla anlatılır.
        if (b.Detay.StartsWith(KabulDeposu.Etiket, StringComparison.Ordinal)) return new("Bu sonucu daha önce \"kabul edilen risk\" olarak işaretlediniz. Bilerek açık bıraktığınız bir durum olduğu için sorun sayılmıyor ve puanınızı düşürmüyor.", "Fikrinizi değiştirirseniz satıra sağ tıklayıp \"Kabul edilen risk olarak işaretle / işareti kaldır\"ı seçin.", "", "ÇÖZÜM KOMUTU", null);
        // Canlı trafik satırı.
        if (b.Modul == "Trafik") return TrafikCozumu(b);
        // Zincirleme risk, hosts/DNS bütünlüğü ve altın profil uyarıları kendi sade açıklamalarıyla anlatılır.
        if (b.Modul is "Zincir" or "Hosts" or "Referans" or "Kalıcılık" or "Karne" or "Hesap" or "Wi-Fi") return YeniModulCozumu(b);

        // Bilgi bankası kurallarını yukarıdan aşağıya dene; ilk uyan kazanır.
        foreach (var k in TehditVeritabani.Kurallar)
        {
            // Kural bu bulguya uymuyorsa sonrakine geç.
            if (!TehditVeritabani.Uyar(k, b)) continue;
            // Bulgu port gerektiriyorsa ama port yoksa bu kural uymaz.
            if (k.PortGerekli && port is null) continue;
            // Kullanılacak port: bulgudaki; yoksa kuralın varsayılanı.
            int? kp = port ?? k.Port;
            // Metinlerdeki {port}, {hizmet} yer tutucuları için değerler.
            var v = new Dictionary<string, string> { ["port"] = kp?.ToString() ?? "", ["hizmet"] = kp is int hp ? HizmetAciklama(hp) : "bir ağ hizmeti" };
            // Koruma etiketi kuralıysa etiket adını ve önerilen değeri hazırla.
            if (k.EtiketCikar)
            {
                // Etiketin adını başlıktan çıkar; yoksa X-Frame-Options say.
                string ad = t.Contains(':') ? t[(t.IndexOf(':') + 1)..].Trim() : "X-Frame-Options";
                // Etiket ve önerilen değeri sözlüğe koy.
                v["etiket"] = ad; v["deger"] = TehditVeritabani.EtiketDegeri(ad);
            }
            // Açıklama ve çözüm metinlerini doldur.
            string neDemek = TehditVeritabani.Doldur(k.NeDemek, v), nasil = TehditVeritabani.Doldur(k.NasilCozerim, v);
            // Güvenlik duvarı kuralıysa komut şablondan üretilir.
            if (k.GuvenlikDuvari && kp is int fp)
            {
                // Şablonun {port} yerini bu portla doldur.
                var fv = new Dictionary<string, string> { ["port"] = fp.ToString() };
                // Windows'ta netsh, Linux'ta ufw şablonu.
                string komut = TehditVeritabani.Doldur(TehditVeritabani.Sablon(win ? "GuvenlikDuvariWindows" : "GuvenlikDuvariLinux"), fv);
                // Ek komut varsa (örneğin SMBv1'i kapatma) altına ekle.
                string ek = win ? k.EkKomutWindows : k.EkKomutLinux;
                // Ek komutu ekle.
                if (!string.IsNullOrWhiteSpace(ek)) komut += "\r\n\r\n" + TehditVeritabani.Doldur(ek, fv);
                // Yardım paketini ver; Otomatik Onar bu port için açılabilir.
                return new(neDemek, nasil, komut, TehditVeritabani.Sablon(win ? "EtiketWindows" : "EtiketLinux", "ÇÖZÜM KOMUTU"), fp);
            }
            // Diğer kurallar: komut ve başlık doğrudan kuraldan.
            return new(neDemek, nasil, TehditVeritabani.Doldur(k.Komut, v), string.IsNullOrEmpty(k.Etiket) ? "ÇÖZÜM KOMUTU" : k.Etiket, null);
        }
        // Hiçbir kural uymadıysa ne bulduysak onun için genel yardım.
        return new(b.Onem == Onem.Bilgi
                ? "Bu bir sorun değil; programın gördüğü bir not. Gereksiz bir şeyse kapatmayı düşünebilirsiniz."
                : "Güvenliği zayıflatabilecek bir durum bulundu.",
            b.Oneri, "", "ÇÖZÜM KOMUTU", null);
    }

    // Zincir / Hosts / Referans bulguları için sade Türkçe "ne demek, nasıl çözerim, komut" paketi hazırlar.
    internal Cozum YeniModulCozumu(Bulgu b)
    {
        // Güvenli bulgu (sorun yok) için kısa not.
        if (b.Onem == Onem.Bilgi) return new("Bu bir sorun değil; kontrol temiz çıktı.", "Yapılacak bir şey yok.", "", "ÇÖZÜM KOMUTU", null);
        // Kabul edilen risk olarak işaretlenmiş sonuç.
        if (b.Detay.StartsWith(KabulDeposu.Etiket, StringComparison.Ordinal)) return new("Bu sonucu daha önce \"kabul edilen risk\" olarak işaretlediniz. Bilerek açık bıraktığınız bir durum olduğu için sorun sayılmıyor ve puanınızı düşürmüyor.", "Fikrinizi değiştirirseniz satıra sağ tıklayıp \"Kabul edilen risk olarak işaretle / işareti kaldır\"ı seçin.", "", "ÇÖZÜM KOMUTU", null);
        // Kalıcılık: açılışta kendiliğinden çalışan programlar.
        if (b.Modul == "Kalıcılık")
            return new("Bilgisayar her açıldığında kendiliğinden başlayan programlar (başlangıç öğeleri) saldırganların en sevdiği yerdir: bir kez yerleşirlerse bilgisayarı yeniden başlatsanız bile geri gelirler (kapıya ikinci bir anahtar kopyalamak gibi). Bu satırdaki öğelerin bir kısmı normal programlar olabilir; tanımadıklarınıza dikkat edin.",
                "Öğeleri tek tek tanıyıp tanımadığınıza bakın. Tanımadığınızı Görev Yöneticisi > Başlangıç sekmesinden devre dışı bırakın; şüpheli görev/hizmet için virüs taraması yapın.",
                "# Başlangıç programlarını listele:\r\nGet-CimInstance Win32_StartupCommand | Format-Table Name,Command,Location -AutoSize\r\n\r\n# Zamanlanmış görevleri (Microsoft dışı) listele:\r\nGet-ScheduledTask | Where-Object { $_.TaskPath -notlike '\\Microsoft*' -and $_.State -ne 'Disabled' } | Format-Table TaskName,TaskPath -AutoSize",
                "İNCELEME KOMUTU · Windows (PowerShell)", null);
        // Karne: Windows temel korumaları.
        if (b.Modul == "Karne")
            return new("Bu, Windows'un kendi koruma kalkanlarından birinin durumu. Virüs koruması, güvenlik duvarı ve UAC gibi korumalar kapalıysa saldırgan ya da zararlı yazılım çok daha kolay iş görür (evin alarmının kapalı olması gibi).",
                b.Oneri, "# Virüs koruması durumu:\r\nGet-MpComputerStatus | Format-List AntivirusEnabled,RealTimeProtectionEnabled,AntivirusSignatureAge\r\n\r\n# Güvenlik duvarı profilleri:\r\nGet-NetFirewallProfile | Format-Table Name,Enabled -AutoSize", "İNCELEME KOMUTU · Windows (PowerShell)", null);
        // Wi-Fi: bağlı kablosuz ağın güvenliği.
        if (b.Modul == "Wi-Fi")
            return new("Wi-Fi, bilgisayarınızın internete havadan bağlandığı yoldur. Şifreleme zayıfsa veya hiç yoksa aynı ağdaki biri trafiğinizi dinleyebilir, parolalarınızı görebilir ya da sizi sahte sitelere yönlendirebilir (konuşmanızı herkesin duyduğu açık bir odada yapmak gibi). \"İkiz ağ\", gerçek ağın adını taklit eden sahte bir ağdır.",
                b.Oneri, "# Bağlı Wi-Fi ayrıntısı (kimlik doğrulama, şifreleme, sinyal):\r\nnetsh wlan show interfaces\r\n\r\n# Çevredeki ağlar ve güvenlik türleri:\r\nnetsh wlan show networks mode=bssid", "İNCELEME KOMUTU · Windows (PowerShell)", null);
        // Hesap: yerel kullanıcı hesapları.
        if (b.Modul == "Hesap")
            return new("Bilgisayardaki kullanıcı hesapları kapıdaki anahtar sahipleri gibidir. Parolasız, misafir veya fazla sayıda yönetici hesabı varsa saldırganın içeri girmesi ve yetki yükseltmesi kolaylaşır.",
                b.Oneri, "# Yerel hesapları listele:\r\nGet-LocalUser | Format-Table Name,Enabled,PasswordRequired -AutoSize\r\n\r\n# Yönetici grubunu listele:\r\nGet-LocalGroupMember -SID S-1-5-32-544", "İNCELEME KOMUTU · Windows (PowerShell)", null);
        // Zincirleme risk.
        if (b.Modul == "Zincir")
            return new("Tek tek bakınca küçük görünen açıklar birleşince büyük tehlike olur. Saldırganlar çoğu zaman tek bir kapıyı değil, birkaç küçük zayıflığı art arda kullanır: önce bilgi toplar, sonra açık bir kapıdan içeri girer (evin açık bırakılmış penceresi + unutulmuş yedek anahtar gibi). Bu uyarı, sizdeki açıkların birbirine nasıl bağlanabildiğini gösteriyor.",
                "Zinciri en kolay yerinden koparın: gereksiz açık kapıyı kapatın (Otomatik Onar), şifresiz web hizmetini https yapın, gereksiz alt alan adlarını silin. Zincirin tek halkası kopsa bile saldırı yolu kapanır.",
                _sonHedefYerel ? "# Bu bilgisayarda açık kapıları ve onları açan programları listeler:\r\nGet-NetTCPConnection -State Listen | Sort-Object LocalPort | Format-Table LocalAddress,LocalPort,OwningProcess -AutoSize" : "# Hedef makinede (Linux) dinleyen kapıları listeler:\r\nsudo ss -tulpn",
                "İNCELEME KOMUTU", null);
        // Altın profil sapması.
        if (b.Modul == "Referans")
            return new("\"Altın Profil\", sisteminizin temiz olduğu bir günde kilitlediğiniz referanstır. Şimdiki tarama bu referanstan farklı çıktı: o günden beri yeni bir kapı açılmış veya yeni bir sorun çıkmış. Bunu siz yapmadıysanız bilgisayarınıza yeni bir program/hizmet kurulmuş veya biri ayar değiştirmiş olabilir.",
                "Yeni kapıyı hangi programın açtığını bu satıra tıklayınca asistan gösterir. Tanımıyorsanız Otomatik Onar ile kapatın. Değişiklik bilinçliyse (yeni program kurdunuz) sorunları düzeltip temiz bir tarama alın ve Altın Profili yeniden kilitleyin.",
                "", "ÇÖZÜM KOMUTU", null);
        // Aşağıdakiler hosts / DNS bütünlüğü uyarılarıdır.
        bool dnsSunucu = b.Baslik.Contains("DNS sunucusu", StringComparison.OrdinalIgnoreCase);
        // DNS sunucusu uyarısı.
        if (dnsSunucu)
            return new("DNS, bir sitenin adını (ör. banka.com) numarasına çeviren telefon rehberidir. Rehber sizi kandırıcı biri tarafından değiştirilirse (DNS zehirlenmesi) gerçek bir siteye girdiğinizi sanırken sahte siteye yönlendirilebilirsiniz. Burada tanımadığımız bir DNS adresi görüldü; internet sağlayıcınızın kendi DNS'i de olabilir.",
                "Sağlayıcınızın DNS'ini biliyorsanız sorun yok. Emin değilseniz DNS'i otomatik yapın veya güvenilir birini (1.1.1.1 / 8.8.8.8) seçin; modemin (router) yönetim parolasını da değiştirin.",
                "# DNS sunucularını görmek için:\r\nGet-DnsClientServerAddress -AddressFamily IPv4 | Format-Table InterfaceAlias,ServerAddresses -AutoSize\r\n\r\n# Bir kartın DNS'ini otomatik hale getirmek için (kart adını değiştirin, Yönetici PowerShell):\r\nSet-DnsClientServerAddress -InterfaceAlias \"Wi-Fi\" -ResetServerAddresses",
                "İNCELEME / TEMİZLEME KOMUTU · Windows (Yönetici PowerShell)", null);
        // hosts dosyası uyarısı.
        return new("hosts dosyası, Windows'un \"özel telefon rehberi\"dir. Buraya yazılan bir site adı, internetteki gerçek adresine bakılmadan doğrudan yazdığınız numaraya gider. Zararlı yazılımlar bu dosyaya satır ekleyerek sizi sahte bir bankaya/güncelleme sitesine yönlendirebilir veya güvenlik programlarının sitelerini engelleyebilir. Standart dışı satırlar bulundu.",
            "Satırları siz eklemediyseniz silin (Not Defteri'ni yönetici olarak açın), ardından bilgisayarı virüs taramasından geçirin. Test için bilinçli eklediyseniz sorun yok. Reklam engelleyici listeleri 0.0.0.0 satırları ekleyebilir; bunlar normaldir.",
            "# Etkin (yorum olmayan) satırları görmek için:\r\nGet-Content \"$env:SystemRoot\\System32\\drivers\\etc\\hosts\" | Where-Object { $_.Trim() -and -not $_.Trim().StartsWith('#') }\r\n\r\n# Düzenlemek için (Yönetici PowerShell):\r\nnotepad \"$env:SystemRoot\\System32\\drivers\\etc\\hosts\"",
            "İNCELEME / TEMİZLEME KOMUTU · Windows (Yönetici PowerShell)", null);
    }

    // "❐ Çözüm Komutunu Kopyala" düğmesine basılınca komutu panoya kopyalar.
    internal void btnKopyala_Click(object? sender, EventArgs e)
    {
        // Komut kutusu boşsa bir şey yapma.
        if (txtKomut.TextLength == 0) return;
        // Korumalı bölge: pano bazen meşgul olabilir.
        try { Clipboard.SetText(txtKomut.Text); Durum("Çözüm komutu kopyalandı. Yönetici olarak açtığınız PowerShell'e (veya ilgili terminale) yapıştırabilirsiniz (Ctrl+V)."); }
        // Olmazsa kibarca bildir.
        catch (Exception ex) { TemaliMesaj.Goster("Kopyalanamadı: " + ex.Message, "Sorun oldu"); }
    }

    // "✚ Otomatik Onar" düğmesine basılınca Windows Güvenlik Duvarı'na engelleme kuralı ekler (önce onay ister).
    internal void btnOnar_Click(object? sender, EventArgs e) => GuvenliCalistir.Baslat(() => btnOnar_ClickAsync(sender, e), "Asistan.btnOnar_Click");
    internal async Task btnOnar_ClickAsync(object? sender, EventArgs e)
    {
        // Yetki kontrolü: bu işlem için rolünüz yeterli mi?
        if (!KabukYetki.Kontrol(this, Eylem.SistemDegistir, "Otomatik Onar")) return;
        // Güvenlik: yalnızca bu bilgisayar ve geçerli bir port için çalışır.
        if (_seciliPort is not int port || !_sonHedefYerel) return;
        // Kuralın adı: BulutSoft ile başlar ki sonradan kolay bulunup silinebilsin.
        string kuralAdi = $"BulutSoft-Blok-{port}";
        // Korumalı bölge.
        try
        {
            // Aynı adlı kural zaten var mı diye bak (bunun için yönetici izni gerekmez).
            if (await Task.Run(() => KuralVarMi(kuralAdi)))
            {
                // Varsa kullanıcıya söyle ve dur.
                TemaliMesaj.Goster($"\"{kuralAdi}\" adlı kural Güvenlik Duvarı'nda zaten ekli. Yeni bir şey yapılmadı.", "Zaten ekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // Ne yapılacağını açıkça anlatıp onay iste. Varsayılan buton "Hayır".
            var onay = TemaliMesaj.Goster(
                $"Bu bilgisayarın Windows Güvenlik Duvarı'na şu kural eklenecek:\r\n\r\n  Kural adı : {kuralAdi}\r\n  Yön       : Gelen bağlantılar\r\n  İşlem     : ENGELLE\r\n  Port      : TCP {port} ({HizmetAciklama(port)})\r\n\r\nBu portu kullanan hizmetlere başka bilgisayarlardan erişilemez olacak. Windows sizden yönetici izni (UAC penceresi) isteyecek.\r\n\r\nGeri almak için:\r\n  netsh advfirewall firewall delete rule name=\"{kuralAdi}\"\r\n\r\nDevam edilsin mi?",
                "Otomatik Onar - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            // Kullanıcı onaylamadıysa hiçbir şey yapma.
            if (onay != DialogResult.Yes) { Durum("Otomatik onarım iptal edildi; hiçbir değişiklik yapılmadı."); return; }
            // Günlüğe yaz.
            Gunluk($"OTOMATİK ONARIM -> Güvenlik duvarına kural ekleniyor: {kuralAdi}", Simsek);
            if (PilotMod.SaltOkunur) { Durum(PilotMod.Mesaj); return; }
            // netsh komutunu yönetici izniyle (UAC) çalıştırmak için ayarlar.
            var psi = new ProcessStartInfo("netsh", $"advfirewall firewall add rule name=\"{kuralAdi}\" dir=in action=block protocol=TCP localport={port}")
            {
                // Yönetici izni isteyebilmek için kabuk üzerinden çalıştır.
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
            };
            // Komutu arka planda çalıştır ve çıkış kodunu bekle (ekran donmasın).
            int kod = await Task.Run(() => { using var pr = Process.Start(psi); pr!.WaitForExit(); return pr.ExitCode; });
            // 0 = başarılı.
            if (kod == 0)
            {
                // Başarıyı günlüğe ve durum çubuğuna yaz.
                Gunluk($"OTOMATİK ONARIM -> BAŞARILI: TCP {port} gelen bağlantılar engellendi ({kuralAdi}).", RakamYesil);
                // Kuralın etkisini 1 saniye içinde yeniden kontrol et; kapandıysa satırı canlı olarak yeşile çevir.
                var (dogrulandi, dogrulamaMetni) = await OnarimiDogrulaAsync(port, kuralAdi);
                // Durum çubuğuna sonucu yaz.
                Durum(dogrulandi ? $"[DOĞRULANDI] Port {port} onarıldı." : $"Kural eklendi: {kuralAdi}. Etkiyi görmek için taramayı yeniden çalıştırın.", false);
                // Kullanıcıya bilgi ver.
                TemaliMesaj.Goster($"Kural eklendi: {kuralAdi}\r\n\r\n{dogrulamaMetni}\r\nGeri almak için:\r\nnetsh advfirewall firewall delete rule name=\"{kuralAdi}\"", "Onarım tamam", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            // Başarısızsa bildir.
            else
            {
                // Hatayı günlüğe yaz.
                Gunluk($"OTOMATİK ONARIM -> BAŞARISIZ (çıkış kodu {kod}).", RakamKirmizi);
                // Kullanıcıya söyle.
                TemaliMesaj.Goster($"Kural eklenemedi (çıkış kodu {kod}). Çözüm komutunu kopyalayıp yönetici olarak açtığınız PowerShell'de elle deneyebilirsiniz.", "Onarım başarısız", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        // Kullanıcı Windows'un yönetici izni penceresini (UAC) reddettiyse.
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // Nazikçe bildir; hiçbir değişiklik yapılmadı.
            Gunluk("OTOMATİK ONARIM -> Yönetici izni verilmedi; hiçbir değişiklik yapılmadı.", Simsek);
            Durum("Yönetici izni verilmedi; hiçbir değişiklik yapılmadı.", false);
        }
        // Başka bir hata olduysa.
        catch (Exception ex)
        {
            // Kullanıcıya söyle.
            TemaliMesaj.Goster("Onarım sırasında bir sorun oldu: " + ex.Message, "Sorun oldu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Seçili sonuç bir kapıya aitse, o kapıyı hangi programın açık tuttuğunu bulup asistan panelinin altına yazar.
    internal async Task ProgramBulGosterAsync(int kapi, Bulgu b)
    {
        // Arama başarısız olsa da program bozulmasın.
        try
        {
            // Aramayı arka planda yap (ekran donmasın).
            string sonuc = await Task.Run(() => PortSahibiBul(kapi));
            // Bu arada başka satır seçildiyse veya Claude analizi başladıysa yazma.
            if (IsDisposed || !ReferenceEquals(SecimBulgusu(), b) || _analizIptal != null) return;
            // Başlık.
            Yaz("\n\n◈ BU KAPIYI KULLANAN PROGRAM:\n", DesignTokens.Ozgun(TokenRol.StatusWarning), true);
            // Bulunan bilgi.
            Yaz(sonuc, Yazi);
            // Son satırın altında boş satır bırak: "ÇÖZÜM KOMUTU" başlığına yapışıp kesilmesin.
            Yaz("\n\n", Yazi);
            // Görünümü en üste geri al.
            AciklamaEnUste();
        }
        // Hata olursa sessizce geç.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "Asistan.ProgramBulGosterAsync"); }
    }

    // Verilen portu (TCP, dinleyen) hangi programın açık tuttuğunu "netstat -ano" ile bulur.
    internal static string PortSahibiBul(int port)
    {
        // Komut başarısız olabilir.
        try
        {
            // netstat'ı görünmez pencerede çalıştır (yönetici izni gerekmez).
            using var p = Process.Start(new ProcessStartInfo("netstat", "-ano -p TCP") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            // Başlatılamadıysa.
            if (p == null) return "Program bilgisi alınamadı.";
            // Çıktıyı oku.
            string cikti = p.StandardOutput.ReadToEnd();
            // Bitmesini bekle.
            p.WaitForExit(5000);
            // Bu portu dinleyen satırı bul: yerel adres ":port", karşı adres 0.0.0.0:0 veya [::]:0, son sütun PID.
            var m = Regex.Match(cikti, @"^\s*TCP\s+\S+:" + port + @"\s+(?:0\.0\.0\.0:0|\[::\]:0)\s+\S+\s+(\d+)\s*$", RegexOptions.Multiline);
            // Bulunamadıysa söyle.
            if (!m.Success) return "Şu anda bu portu dinleyen bir program bulunamadı (kapı kapanmış veya geçici olabilir).";
            // Süreç numarası (PID).
            int pid = int.Parse(m.Groups[1].Value);
            // PID 4 = Windows çekirdeği.
            if (pid is 0 or 4) return $"System (PID {pid}) — Windows çekirdeği.\nBu kapı, Windows'un kendi hizmeti (örneğin dosya paylaşımı sürücüsü) tarafından açık tutuluyor.";
            // Program adı ve yolu.
            string ad, yol = "";
            // Süreç kapanmış olabilir.
            try
            {
                // Süreci bul.
                using var sp = Process.GetProcessById(pid);
                // Program adı.
                ad = sp.ProcessName + ".exe";
                // Dosya yolu (yetki yoksa okunamaz).
                try { yol = sp.MainModule?.FileName ?? ""; } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Asistan.PortSahibiBul"); }
            }
            // Süreç bulunamadı.
            catch { return $"PID {pid} bulundu ama program bilgisi okunamadı (süreç kapanmış olabilir)."; }
            // Metni hazırla.
            var sb = new System.Text.StringBuilder();
            // Program ve PID.
            sb.Append($"Program: {ad} (PID {pid})");
            // svchost ise içindeki Windows hizmetlerinin adlarını da bul.
            if (ad.StartsWith("svchost", StringComparison.OrdinalIgnoreCase)) { string hizmet = HizmetAdlari(pid); if (hizmet.Length > 0) sb.Append($"\nWindows hizmeti: {hizmet}"); }
            // Dosya yolu varsa ekle.
            if (yol.Length > 0) sb.Append($"\nDosya yolu: {yol}");
            // Sonucu ver.
            return sb.ToString();
        }
        // Beklenmeyen bir sorun olursa.
        catch (Exception ex) { return "Program bilgisi alınamadı: " + ex.Message; }
    }

    // Bir svchost sürecinin içinde çalışan Windows hizmetlerinin adlarını verir ("tasklist /svc").
    internal static string HizmetAdlari(int pid)
    {
        // Komut başarısız olabilir.
        try
        {
            // tasklist'i görünmez pencerede çalıştır.
            using var p = Process.Start(new ProcessStartInfo("tasklist", $"/FI \"PID eq {pid}\" /SVC /FO CSV /NH") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            // Başlatılamadıysa boş dön.
            if (p == null) return "";
            // Çıktıyı oku.
            string cikti = p.StandardOutput.ReadToEnd(); p.WaitForExit(4000);
            // Üçüncü sütun hizmet adlarıdır.
            var m = Regex.Match(cikti, "\"[^\"]*\",\"\\d+\",\"([^\"]*)\"");
            // Bulunduysa ve boş/"N/A" değilse ver.
            return m.Success && m.Groups[1].Value is not ("" or "N/A" or "Yok") ? m.Groups[1].Value : "";
        }
        // Hata olursa boş dön.
        catch { return ""; }
    }

    // Verilen adrese ve porta 1,5 saniye içinde bağlanılabiliyor mu? (bağlanılabiliyorsa port hâlâ açıktır)
    internal static bool TcpAcikMi(IPAddress ip, int port)
    {
        // Bağlantı başarısız olabilir.
        try
        {
            // Yeni bağlantı.
            using var c = new TcpClient();
            // Bağlanmayı dene ve en fazla 1,5 saniye bekle.
            return c.ConnectAsync(ip, port).Wait(1500) && c.Connected;
        }
        // Bağlanılamadı: kapalı.
        catch { return false; }
    }

    // Güvenlik duvarında verilen adlı kural etkin ve "engelle" türünde mi?
    internal static bool KuralEtkinMi(string ad)
    {
        // Komut başarısız olabilir.
        try
        {
            // netsh "show rule" komutunu görünmez pencerede çalıştır.
            using var pr = Process.Start(new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{ad}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            // Başlatılamadıysa.
            if (pr == null) return false;
            // Çıktıyı oku.
            string cikti = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(4000);
            // Kural yoksa komut 0 dışında bir kod verir.
            if (pr.ExitCode != 0) return false;
            // Küçük harfe çevir (Windows dili Türkçe/İngilizce olabilir).
            string k = cikti.ToLowerInvariant();
            // "Block" (İngilizce) veya "Engelle" (Türkçe) geçiyorsa engelleme kuralıdır.
            return k.Contains("block") || k.Contains("engelle");
        }
        // Hata olursa "yok" say.
        catch { return false; }
    }

    // Otomatik onarımdan 1 saniye sonra portu yeniden kontrol eder; kapandıysa satırı canlı olarak yeşil GÜVENLİ (Onarıldı) yapar.
    internal async Task<(bool Dogrulandi, string Metin)> OnarimiDogrulaAsync(int port, string kuralAdi)
    {
        // Günlüğe yaz.
        Gunluk($"DOĞRULAMA -> TCP {port} 1 saniye içinde yeniden kontrol ediliyor...", Elektrik);
        // Kuralın devreye girmesi için 1 saniye bekle.
        await Task.Delay(1000);
        // Port hâlâ açık mı diye aynı adrese bağlanmayı dene.
        bool acik = await Task.Run(() => TcpAcikMi(_sonHedefIp, port));
        // Sonuç metni.
        string metin;
        // Port kapandıysa doğrulandı.
        if (!acik) metin = $"Doğrulandı: TCP {port} artık kapalı.";
        // Hâlâ açık görünüyorsa kuralın varlığını kontrol et (Windows, kendi içinden yapılan bağlantıları süzmez).
        else if (await Task.Run(() => KuralEtkinMi(kuralAdi))) metin = $"Doğrulandı: güvenlik duvarı kuralı etkin ({kuralAdi}). Not: Windows, bu bilgisayarın kendi içinden yapılan bağlantıları engellemez; dışarıdan gelen bağlantılar engellenir.";
        // Hiçbiri doğrulanamadıysa dürüstçe söyle.
        else
        {
            // Günlüğe turuncu yaz.
            Gunluk($"DOĞRULAMA -> Port {port} için kural etkisi doğrulanamadı.", DesignTokens.Ozgun(TokenRol.StatusWarning));
            // Satırı değiştirme.
            return (false, "Kural eklendi ama etkisi otomatik doğrulanamadı. Taramayı yeniden çalıştırarak kontrol edin.");
        }
        // Tablodaki satırları yeşil GÜVENLİ (Onarıldı) yap, sayaçları, puanı ve radarı güncelle.
        SatiriOnarildiYap(port, kuralAdi);
        // Günlüğe yeşil [DOĞRULANDI] satırını yaz.
        Gunluk($"[DOĞRULANDI] Port {port} -> {metin}", RakamYesil);
        // Sonucu ver.
        return (true, metin);
    }

    // Verilen kapıya ait tablo satırlarını "GÜVENLİ (Onarıldı)" durumuna geçirir ve her şeyi canlı günceller.
    internal void SatiriOnarildiYap(int port, string kuralAdi)
    {
        // Bulgular listesini gez.
        for (int i = 0; i < _bulgular.Count; i++)
        {
            // Eski sonuç.
            var eski = _bulgular[i];
            // Bu kapıya ait Port veya Yapılandırma sonucu değilse (ya da zaten onarıldıysa) atla.
            if ((eski.Modul != "Port" && eski.Modul != "Yapılandırma") || eski.Atlandi || eski.Baslik.EndsWith("— Onarıldı", StringComparison.Ordinal) || !Regex.IsMatch(eski.Baslik + " " + eski.Detay, $@"[Pp]ort {port}\b")) continue;
            // Yeni (onarılmış) sonuç: yeşil seviye, onarıldı notu.
            var yeni = eski with { Baslik = eski.Baslik + "\n— Onarıldı", Onem = Onem.Bilgi, Detay = $"Port {port} (Kapı)\nGüvenlik duvarı kuralı etkin: {kuralAdi}", Oneri = "Yapılacak bir şey yok. Geri almak için asistanın verdiği \"delete rule\" komutunu kullanın." };
            // Listeye yaz.
            _bulgular[i] = yeni;
            // Tablodaki ilgili satırı bul.
            foreach (DataGridViewRow r in dgvBulgular.Rows)
            {
                // Bu satır eski sonuca mı ait?
                if (!ReferenceEquals(r.Tag, eski)) continue;
                // Hücreleri güncelle: rozet, başlık, ayrıntı, öneri.
                r.Cells[0].Value = Rozet(yeni).Metin; r.Cells[2].Value = yeni.Baslik; r.Cells[3].Value = yeni.Detay; r.Cells[4].Value = yeni.Oneri;
                // Satırın içindeki sonucu da değiştir.
                r.Tag = yeni;
                // Satırı yeniden çiz.
                dgvBulgular.InvalidateRow(r.Index);
                // Bir satır yeter.
                break;
            }
        }
        // Sayaçları tüm sonuçlara göre baştan hesapla.
        _yesil = _sari = _kirmizi = 0;
        // Her sonuç için ilgili sayacı artır.
        foreach (var b in _bulgular) { if (b.Atlandi) continue; if (b.Onem == Onem.Bilgi) _yesil++; else if (b.Onem == Onem.Yuksek) _kirmizi++; else _sari++; }
        // Sayaçları, kartları, puan kadranını (animasyonla) ve radarı güncelle.
        SayaclariYaz();
        // Açık bir filtre varsa yeni durumuna göre yeniden uygula.
        if (_filtre != Filtre.Yok) FiltreyiUygula();
        // Seçili satır güncellendiyse asistanı da yenile.
        if (SecimBulgusu() is Bulgu secili) AsistanGoster(secili);
    }

    // Verilen adlı bir Güvenlik Duvarı kuralı var mı? (netsh "show rule" kural varsa 0 döner.)
    internal static bool KuralVarMi(string ad)
    {
        // netsh'i görünmez pencerede, çıktısını okuyarak çalıştır.
        using var pr = Process.Start(new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{ad}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
        // Başlatılamadıysa "yok" de.
        if (pr == null) return false;
        // Çıktıyı oku (kapanmaması için).
        pr.StandardOutput.ReadToEnd();
        // Bitmesini bekle.
        pr.WaitForExit();
        // Çıkış kodu 0 ise kural vardır.
        return pr.ExitCode == 0;
    }
}
