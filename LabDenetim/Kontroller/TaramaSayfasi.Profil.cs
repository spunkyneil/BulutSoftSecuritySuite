// Bu dosya "Profil" düğmelerinin (Hızlı / Standart / Kapsamlı) ne yapacağını anlatır.
// Profil seçilince kapı listesi ve kontrol kutucukları sizin yerinize otomatik doldurulur.
namespace LabDenetim;

// Form1'in yedinci parçası: tarama profilleri.
internal sealed partial class TaramaSayfasi
{
    // Profil kendi kendine alanları doldururken "elle değişti" sanılmasın diye bayrak.
    internal bool _profilUygulaniyor;
    // Seçili profilin sırası: 0 = Hızlı, 1 = Standart, 2 = Kapsamlı, 3 = Özel (elle ayarlı).
    internal int _profilSira = -1;

    // Profil düğmelerini çalışır hale getirir. Form1 açılırken bir kez çağrılır.
    internal void ProfilKur()
    {
        // "Hızlı" düğmesine basılınca 0 numaralı profili uygula.
        btnProfilHizli.Click += (_, _) => ProfilSec(0);
        // "Standart" düğmesine basılınca 1 numaralı profili uygula.
        btnProfilStandart.Click += (_, _) => ProfilSec(1);
        // "Kapsamlı" düğmesine basılınca 2 numaralı profili uygula.
        btnProfilKapsamli.Click += (_, _) => ProfilSec(2);
        // Kullanıcı kapı numaralarını elle değiştirirse profil "Özel" olsun.
        txtPortlar.TextChanged += ElleDegisti;
        // Kullanıcı bir kontrol kutucuğunu elle değiştirirse profil "Özel" olsun.
        foreach (var c in new[] { chkPort, chkHttp, chkTls, chkDns, chkYapilandirma, chkGirdi }) c.CheckedChanged += ElleDegisti;
        // Başlangıçta "Standart" profil seçili olsun (önerilen).
        ProfilSec(1);
    }

    // Seçili profile göre düğmeleri boyar: seçili olan şimşek sarısı, diğerleri koyu gri; "Özel"de hiçbiri sarı olmaz.
    internal void ProfilGoster()
    {
        // Üç düğmeyi sırasıyla gez.
        var dugmeler = new[] { btnProfilHizli, btnProfilStandart, btnProfilKapsamli };
        // Her düğme için karar ver.
        for (int i = 0; i < dugmeler.Length; i++)
        {
            // Bu düğme seçili profil mi?
            bool secili = i == _profilSira;
            // Seçiliyse sarı zemin siyah yazı, değilse koyu gri zemin açık yazı.
            dugmeler[i].BackColor = secili ? Simsek : DesignTokens.Ozgun(TokenRol.BorderStrong);
            // Yazı rengi.
            dugmeler[i].ForeColor = secili ? Color.Black : Yazi;
        }
        // "Özel" yazısı yalnızca elle ayar yapılınca görünsün.
        lblProfilOzel.Text = _profilSira == 3 ? "· Özel" : "";
    }

    // Kullanıcı gelişmiş ayarlardan bir şeyi elle değiştirdiğinde profili "Özel (elle ayarlı)" yapar.
    internal void ElleDegisti(object? sender, EventArgs e)
    {
        // Değişikliği profil kendisi yaptıysa karışma.
        if (_profilUygulaniyor) return;
        // Zaten "Özel" ise bir şey yapma.
        if (_profilSira == 3) return;
        // "Özel" olarak işaretle.
        _profilSira = 3;
        // Düğmelerin görünümünü güncelle.
        ProfilGoster();
    }

    // Profil seçilince kapı listesini ve kontrol kutucuklarını doldurur (sira: 0, 1 veya 2).
    internal void ProfilSec(int sira)
    {
        // Profil değişiklikleri "elle değişti" sayılmasın.
        _profilUygulaniyor = true;
        // Hata olsa da bayrak mutlaka kapansın.
        try
        {
            // Seçilen profilin sırasını sakla.
            _profilSira = sira;
            // Seçilen profile göre kapı listesini yaz.
            txtPortlar.Text = sira switch
            {
                // Hızlı: en yaygın 8 kapı.
                0 => "21,22,23,25,80,443,445,3389",
                // Standart: 17 yaygın kapı.
                1 => "21,22,23,25,53,80,110,139,143,443,445,3306,3389,5432,5900,8080,8443",
                // Kapsamlı: 1-1024 arası tüm kapılar ve sık kullanılan yüksek numaralar.
                _ => "1-1024,1433,1521,3306,3389,5432,5900,5985,8000-8100,8443,9000"
            };
            // Kapı yoklama ve zayıf ayarlar her profilde açık.
            chkPort.Checked = true; chkYapilandirma.Checked = true;
            // Web sitesi korumaları ve isim kayıtları Hızlı profilde kapalı, diğerlerinde açık.
            chkHttp.Checked = sira >= 1; chkDns.Checked = sira >= 1;
            // Şifreli bağlantı yalnızca Kapsamlı profilde ve adres https ile başlıyorsa açık.
            chkTls.Checked = sira == 2 && txtUrl.Text.Trim().StartsWith("https", StringComparison.OrdinalIgnoreCase);
            // Form kutusu testi hiçbir profilde otomatik açılmaz: bunu bilerek siz açmalısınız.
            chkGirdi.Checked = false;
        }
        // Bayrağı kapat.
        finally { _profilUygulaniyor = false; }
        // Düğmelerin görünümünü güncelle.
        ProfilGoster();
        // Profilin adı.
        string ad = sira switch { 0 => "Hızlı", 1 => "Standart", _ => "Kapsamlı" };
        // Kaç kapı seçildiğini say.
        int kapiSayisi = Moduller.PortTarayici.PortListesiOku(txtPortlar.Text).Length;
        // Açık kontrol kutucuklarının sayısı.
        int kontrolSayisi = new[] { chkPort, chkHttp, chkTls, chkDns, chkYapilandirma, chkGirdi }.Count(c => c.Checked);
        // Durum çubuğuna ve günlüğe bilgi ver.
        Durum($"Profil: {ad} seçildi ({kapiSayisi} kapı, {kontrolSayisi} kontrol).");
    }
}
