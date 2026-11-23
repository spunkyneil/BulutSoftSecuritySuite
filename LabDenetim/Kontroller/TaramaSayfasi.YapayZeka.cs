// Bu dosya sağdaki asistanın İKİ MODUNU yönetir:
//  ◉ Yerel & Manuel Mod (ücretsiz, internetsiz): programın kendi bilgi bankası + "AI Danışman Metni" kopyalama.
//  ⚡ Claude API Modu: seçili bulguyu veya tüm taramayı Claude'a gönderip canlı analiz alır.
// API anahtarı yoksa, kredi bitmişse veya internet yoksa program HATA VERMEZ; Yerel moda döner, hiçbir işlev kaybolmaz.
using System.Runtime.InteropServices;
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Form1'in on ikinci parçası: yapay zeka asistanı.
internal sealed partial class TaramaSayfasi
{
    // Şu an Claude API modu mu seçili? (false = Yerel & Manuel mod)
    internal bool _modClaude;
    // Sürmekte olan Claude analizini durdurmak için şalter (analiz yoksa boş).
    internal CancellationTokenSource? _analizIptal;
    // Kullanıcı verilerin Claude'a gönderilmesini bu oturumda onayladı mı?
    

    // Claude'a verilen rol talimatı: savunma odaklı, sade Türkçe, Markdown yok.
    internal const string SistemTalimati =
        "Sen deneyimli, savunma odaklı bir siber güvenlik danışmanısın. Kullanıcı KENDİ izole test ortamındaki sistemleri denetliyor ve bilgisayardan hiç anlamıyor. " +
        "Cevabını sade Türkçe ile, günlük hayattan benzetmelerle ver. Yalnızca savunma ve kapatma önerileri ver; saldırı veya sömürü talimatı verme. " +
        "Biçim: DÜZ METİN kullan; Markdown işaretlerini (#, **, ```, tablo) KULLANMA. Bölüm başlıklarını BÜYÜK HARFLE yaz, komutları ayrı satırlara yaz. " +
        "Windows komutlarını yönetici olarak açılmış PowerShell için, Linux komutlarını sudo ile ver ve her komutun geri alma komutunu da yaz.";

    // Mod seçiciyi başlatır: en son kullanılan modu geri yükler. Form1 açılırken bir kez çağrılır.
    internal void YapayZekaKur()
    {
        // Kayıtlı ayarı oku (yoksa Yerel mod).
        var ayar = ApiDeposu.AyarOku();
        // Kayıtlı mod Claude ise ve anahtar duruyorsa Claude, değilse Yerel mod.
        ModSec(ayar.Mod == "claude" && ApiDeposu.AnahtarVarMi(), false);
    }

    // Mod düğmelerini ve ilgili düğme panelini seçili moda göre boyar/gösterir.
    internal void ModGoster()
    {
        // Yerel mod düğmesi: seçiliyse şimşek sarısı.
        ModDugmeBoya(btnModYerel, !_modClaude, Simsek);
        // Claude mod düğmesi: seçiliyse indigo (yapay zeka rengi).
        ModDugmeBoya(btnModClaude, _modClaude, DesignTokens.Ozgun(TokenRol.AccentHover));
        // Claude düğmeleri yalnızca Claude modunda görünsün.
        pnlClaude.Visible = _modClaude;
        // Manuel düğmesi yalnızca Yerel modda görünsün.
        pnlManuel.Visible = !_modClaude;
    }

    // Bir mod düğmesini seçili/seçili değil görünümüne boyar.
    internal static void ModDugmeBoya(Button b, bool secili, Color vurgu)
    {
        // Düz görünüm.
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; b.Cursor = Cursors.Hand;
        // Seçiliyse vurgu rengi zemin ve siyah yazı; değilse koyu gri zemin ve açık yazı.
        b.BackColor = secili ? vurgu : DesignTokens.Ozgun(TokenRol.BorderStrong); b.ForeColor = secili ? Color.Black : DesignTokens.Ozgun(TokenRol.TextPrimary);
        // Yazı tipi.
        b.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold);
    }

    // Modu değiştirir (claude: true = Claude API Modu, false = Yerel & Manuel Mod).
    internal void ModSec(bool claude, bool kaydet = true)
    {
        // Analiz sürerken mod değişmesin.
        if (_analizIptal != null) return;
        // Yeni modu sakla.
        _modClaude = claude;
        // Düğmeleri güncelle.
        ModGoster();
        // Seçimi bir sonraki açılış için hatırla.
        if (kaydet) ApiDeposu.AyarKaydet(ApiDeposu.AyarOku() with { Mod = claude ? "claude" : "yerel" });
        // İlk açılışta günlüğü kalabalık etme.
        if (!kaydet) return;
        // Günlüğe ve durum çubuğuna hangi modda olduğumuzu yaz.
        Durum(claude
            ? (ApiDeposu.AnahtarVarMi() ? "Mod: Claude API (derin analiz için bir satır seçip \"Derin Analiz Et\"e basın)." : "Mod: Claude API. Önce \"⚙ API Ayarı\"ndan anahtarınızı girin; girmezseniz Yerel mod çalışmaya devam eder.")
            : "Mod: Yerel & Manuel (ücretsiz). Programın kendi bilgi bankası çalışıyor.");
    }

    // "⚙ API Ayarı" düğmesine basılınca ayar penceresini açar.
    internal void btnApiAyar_Click(object? sender, EventArgs e)
    {
        // Ayar penceresini yarat.
        using var f = new ApiAyarFormu();
        // Aç; kaydedildiyse bilgi ver.
        if (f.ShowDialog(this) == DialogResult.OK) Durum(ApiDeposu.AnahtarVarMi() ? "API ayarları kaydedildi." : "API ayarları kaydedildi (anahtar yok; Yerel mod çalışır).");
    }

    // Tabloda seçili sonucu verir (seçili yoksa boş).
    // Hangi tablo görünürse (sonuç veya canlı trafik) onun seçili satırı verilir.
    internal Bulgu? SecimBulgusu() { var t = dgvTrafik.Visible ? dgvTrafik : dgvBulgular; return t.SelectedRows.Count > 0 ? t.SelectedRows[0].Tag as Bulgu : null; }

    // "❐ AI Danışman Metnini (Prompt) Hazırla ve Kopyala" düğmesine basılınca çalışır (ücretsiz, internetsiz).
    internal void btnPromptKopyala_Click(object? sender, EventArgs e)
    {
        // Henüz tarama yoksa kullanıcıya nazikçe söyle.
        if (_bulgular.Count == 0) { TemaliMesaj.Goster("Önce \"⚡ TARAMAYI BAŞLAT\" butonuna basıp bir tarama yapın; sonra tarama verilerini kopyalayabilirsiniz.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Pano bazen meşgul olabilir.
        try
        {
            // Tüm tarama verilerini ayrıntılı soru metnine çevirip panoya koy.
            Clipboard.SetText(AnalizMetniOlustur(null, true));
            // Kullanıcıya ne yapacağını söyle.
            TemaliMesaj.Goster("Tarama verileriniz yapay zekaya sorulacak formatta kopyalandı! Aboneliğiniz olmasa bile herhangi bir ücretsiz yapay zeka sohbet ekranına (Ctrl+V ile) yapıştırarak detaylı çözüm alabilirsiniz.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // Durum çubuğuna ve günlüğe yaz.
            Durum("AI Danışman metni panoya kopyalandı (Ctrl+V ile yapıştırabilirsiniz).");
        }
        // Kopyalanamazsa bildir.
        catch (Exception ex) { TemaliMesaj.Goster("Kopyalanamadı: " + ex.Message, "Sorun oldu"); }
    }

    // Tarama verilerini ayrıntılı bir Türkçe soru metnine çevirir (üretim AnalizMetniUretici'de; burada yalnızca form durumu bağlama çevrilir).
    // tek: yalnızca bu bulguya odaklan (boşsa tüm tarama). sohbetIcin: true ise bir sohbet ekranına yapıştırmak için selamlama ekler.
    internal string AnalizMetniOlustur(Bulgu? tek, bool sohbetIcin)
    {
        // Form durumunu saf servise verilecek bağlama çevir; metni AnalizMetniUretici üretir.
        string profil = _profilSira switch { 0 => "Hızlı", 1 => "Standart", 2 => "Kapsamlı", _ => "Özel (elle ayarlı)" };
        var kontroller = new List<string>(); if (chkPort.Checked) kontroller.Add("port"); if (chkHttp.Checked) kontroller.Add("http"); if (chkTls.Checked) kontroller.Add("tls"); if (chkDns.Checked) kontroller.Add("dns"); if (chkYapilandirma.Checked) kontroller.Add("yapilandirma"); if (chkGirdi.Checked) kontroller.Add("girdi");
        return AnalizMetniUretici.Olustur(new AnalizBaglami(txtHedef.Text.Trim(), _sonHedefYerel, profil, _toplamKapi, kontroller, _bulgular, _kirmizi, _sari, _yesil, PuanHesapla(), RuntimeInformation.OSDescription, DateTime.Now, b => Rozet(b).Metin), tek, sohbetIcin);
    }

    // "◆ Claude AI ile Derin Analiz Et" düğmesine basılınca çalışır. Analiz sürerken düğme "Durdur" olur.
    internal void btnDerinAnaliz_Click(object? sender, EventArgs e) => GuvenliCalistir.Baslat(() => btnDerinAnaliz_ClickAsync(sender, e), "YapayZeka.btnDerinAnaliz_Click");
    internal async Task btnDerinAnaliz_ClickAsync(object? sender, EventArgs e)
    {
        // Analiz zaten sürüyorsa bu tıklama onu durdurur.
        if (_analizIptal != null) { _analizIptal.Cancel(); return; }
        // Henüz tarama yoksa nazikçe söyle.
        if (_bulgular.Count == 0) { TemaliMesaj.Goster("Önce \"⚡ TARAMAYI BAŞLAT\" butonuna basıp bir tarama yapın; sonra Claude'a analiz ettirebilirsiniz.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Kayıtlı API anahtarını oku.
        string? anahtar = ApiDeposu.AnahtarOku();
        // Anahtar yoksa önce ayar penceresini aç.
        if (string.IsNullOrWhiteSpace(anahtar))
        {
            // Ne olduğunu sade dille anlat.
            TemaliMesaj.Goster("Claude analizi için önce kendi API anahtarınızı girmeniz gerekir. Şimdi ayar penceresi açılacak.\r\n\r\nAnahtarınız yoksa sorun değil: sol üstteki \"◉ Yerel & Manuel Mod (Ücretsiz)\"ü kullanabilirsiniz.", "API anahtarı gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
            // Ayar penceresini aç.
            btnApiAyar_Click(null, EventArgs.Empty);
            // Yeniden oku.
            anahtar = ApiDeposu.AnahtarOku();
            // Hâlâ yoksa Yerel moda dön ve çık.
            if (string.IsNullOrWhiteSpace(anahtar)) { ModSec(false); Durum("API anahtarı girilmedi; Yerel & Manuel mod devrede (hiçbir işlev kaybolmadı)."); return; }
        }
        // Analiz edilecek kapsam: onay kutusu işaretliyse tüm tarama, değilse seçili satır.
        Bulgu? tek = chkTumTarama.Checked ? null : SecimBulgusu();
        // Seçili satır da yok ve tüm tarama işaretli değilse tüm taramayı analiz et.
        if (!chkTumTarama.Checked && tek == null) chkTumTarama.Checked = true;
        // KVKK güvenli çıkış: dışarı çıkacak metin YEREL olarak maskelenir (özel IP son oktetleri, kullanıcı adları, yerel yollar; seçenek varsayılan AÇIK) ve
        // onay penceresinde AYNEN önizletilir. Her gönderimde sorulur (metin seçime göre her seferinde değişir); varsayılan düğme "Vazgeç"tir.
        string? gonderilecek = ClaudeOnayFormu.Sor(this, AnalizMetniOlustur(tek, false));
        // Onaylamadıysa hiçbir şey gönderme.
        if (gonderilecek == null) { Durum("Claude analizi iptal edildi; hiçbir şey gönderilmedi."); return; }
        // Model adı.
        string model = ApiDeposu.AyarOku().Model;
        // 3 dakikada biten bir durdurma şalteri hazırla.
        _analizIptal = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        // Düğme "Durdur" olsun ve mod değiştirme kilitlensin.
        btnDerinAnaliz.Text = "■ Analizi Durdur";
        // Yerel uzman açıklaması ekranda kalır; altına Claude'un cevabı eklenecek.
        Yaz("\n\n━━━ ◆ CLAUDE AI ANALİZİ ━━━\n", DesignTokens.Ozgun(TokenRol.AccentHover), true);
        // Günlüğe yaz.
        Gunluk($"CLAUDE AI -> Analiz isteniyor ({(tek == null ? "tüm tarama" : tek.Baslik)})...", DesignTokens.Ozgun(TokenRol.AccentHover));
        // Hata olursa program çökmesin.
        try
        {
            // Claude'a gönder; cevap geldikçe ekrana yaz.
            await ClaudeIstemci.AnalizAsync(anahtar!, model, SistemTalimati, gonderilecek, parca => Yaz(parca, Yazi), _analizIptal.Token);
            // Bittiğini belirt.
            Yaz("\n\n━━━ Analiz bitti ━━━\n", YaziSilik);
            // Günlüğe yaz.
            Gunluk("CLAUDE AI -> Analiz tamamlandı.", RakamYesil);
        }
        // Kullanıcı durdurdu (veya süre doldu).
        catch (OperationCanceledException) { Yaz("\n\n■ Analiz durduruldu.\n", Simsek); Gunluk("CLAUDE AI -> Analiz durduruldu.", Simsek); }
        // Anlaşılır bir Claude hatası (anahtar yanlış, kredi bitti, yoğunluk vb.).
        catch (ClaudeHatasi ex)
        {
            // Sade mesajı yaz.
            Yaz("\n\n⚠ " + ex.Message + "\n", DesignTokens.Ozgun(TokenRol.StatusWarning), true);
            // Günlüğe yaz.
            Gunluk("CLAUDE AI -> " + ex.Message, RakamKirmizi);
            // Anahtar/kredi sorunuysa Yerel moda dön; yerel açıklama ve komutlar zaten ekranda.
            if (ex.YerelModaDon)
            {
                // Şalteri önce kapat ki mod değişebilsin.
                _analizIptal?.Dispose(); _analizIptal = null;
                // Yerel moda geç.
                ModSec(false);
                // Kullanıcıyı rahatlat.
                Yaz("\n◉ Yerel & Manuel Mod devrede. Yukarıdaki açıklama ve komutlar çalışmaya devam ediyor. İsterseniz \"❐ AI Danışman Metnini (Prompt) Hazırla ve Kopyala\" ile verilerinizi ücretsiz bir yapay zeka sohbetine yapıştırabilirsiniz.\n", DesignTokens.Ozgun(TokenRol.StatusSuccess));
            }
        }
        // Beklenmeyen başka bir sorun olursa da program çökmesin.
        catch (Exception ex) { Yaz("\n\n⚠ Beklenmeyen bir sorun oldu: " + ex.Message + "\n", DesignTokens.Ozgun(TokenRol.StatusWarning)); }
        // Ne olursa olsun düğmeyi eski haline getir.
        finally { _analizIptal?.Dispose(); _analizIptal = null; btnDerinAnaliz.Text = "◆ Claude AI ile Derin Analiz Et"; }
    }
}
