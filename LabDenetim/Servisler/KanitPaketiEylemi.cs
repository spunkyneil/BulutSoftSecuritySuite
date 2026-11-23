// "Adli Kanıt Paketi (.ZIP)" düğmesinin işi: paketi masaüstüne üretir, koruma hizmeti HMAC'ini ekler, isteğe bağlı RFC 3161 zaman damgası alır.
// Eskiden Form1'in KanitPaketi.cs partial dosyasındaydı; artık Form1'e yalnızca ITaramaBaglami üzerinden bağlıdır.
using System.Diagnostics;

// Programımızın adı.
namespace LabDenetim;

// Kanıt paketi düğme eylemi.
internal sealed class KanitPaketiEylemi
{
    private readonly ITaramaBaglami _b; private readonly Control _dugme;

    // dugme: işlem sürerken kapatılan (çift tıklamayı engelleyen) düğme.
    public KanitPaketiEylemi(ITaramaBaglami baglam, Control dugme) { _b = baglam; _dugme = dugme; }

    // Düğmeye basılınca paketi arka planda üretir (async void yok).
    public void Tikla() => GuvenliCalistir.Baslat(() => TiklaAsync(), "KanitPaketiEylemi.Tikla");

    private async Task TiklaAsync()
    {
        // Çift tıklamayı engelle.
        _dugme.Enabled = false;
        // Hata olsa da düğme geri açılsın.
        try
        {
            // Sonuç yoksa yine de paket yapılabilir; ama kullanıcıya haber ver.
            if (_b.Bulgular.Count == 0 && TemaliMesaj.Goster("Ekranda tarama sonucu yok. Paket yine de bağlantılar, açık kapılar, hosts ve sistem bilgisiyle hazırlanabilir (sonuç bölümü boş olur).\r\n\r\nDevam edilsin mi?", "Adli Kanıt Paketi", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            // Hedef klasör: masaüstü.
            var masaustu = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            // Şimdiki an.
            var an = DateTime.Now;
            // Rapor bilgisi ve bulgu kopyası (ekran iş parçacığında alınır).
            var bilgi = _b.RaporBilgisiOlustur(); var kopya = _b.Bulgular.ToList();
            // HTML raporu ekran iş parçacığında üretilecek (asistan bilgilerini kullandığı için).
            string htmlGecici = Path.Combine(Path.GetTempPath(), "bulutsoft_html_" + Guid.NewGuid().ToString("N") + ".html");
            // Raporu şimdi yaz.
            RaporUretici.HtmlYaz(htmlGecici, bilgi, kopya, _b.RaporAciklayici);
            // Durum yaz.
            _b.Durum("Adli kanıt paketi hazırlanıyor...");
            // Ağır işi arka planda yap (ekran donmasın).
            string operatorAd = ServisBaglami.Oturum.Aktif is { } ok ? $"{ok.Ad} ({Yetki.RolAdi(ok.Rol)})" : "oturum yok"; string yol = await Task.Run(() => KanitPaketi.Olustur(masaustu, an, bilgi, kopya, hedef => File.Copy(htmlGecici, hedef, true), operatorAd));
            // Geçici HTML'yi sil.
            try { File.Delete(htmlGecici); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "KanitPaketi.btnKanit_Click", true); }
            // DIŞ GÜVENCELER: (1) koruma hizmetindeki gizli anahtarla HMAC (.hmac; hizmet yoksa atlanır), (2) kullanıcı ONAYLARSA RFC 3161 zaman damgası (.tsr; internet gerekir).
            string? hmacYolu = await Task.Run(() => KanitImzasi.HmacYaz(yol, operatorAd)); string guvenceler = ".sha256" + (hmacYolu != null ? " + .hmac (koruma hizmeti)" : " (koruma hizmeti yok: .hmac üretilemedi)");
            string nl = Environment.NewLine + Environment.NewLine;
            if (TemaliMesaj.Goster("Paket oluşturuldu: " + Path.GetFileName(yol) + nl + "Paketin O ANDA var olduğunu bağımsız bir üçüncü tarafla kanıtlamak için RFC 3161 ZAMAN DAMGASI alınabilir." + nl + "İNTERNETE ÇIKILIR: yalnızca zip'in SHA-256 özeti (64 karakter) bir zaman damgası sunucusuna (freetsa.org, olmazsa timestamp.digicert.com) gönderilir. Paketin içeriği, dosya adı, kullanıcı ve bilgisayar adı GÖNDERİLMEZ." + nl + "Zaman damgası alınsın mı?", "Zaman damgası (internet gerekir)", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                string neden = ""; DateTimeOffset? zaman = null; foreach (var tsa in KanitImzasi.Tsalar) { (zaman, neden) = await KanitImzasi.ZamanDamgasiAlAsync(yol, tsa, CancellationToken.None); if (zaman != null) break; }
                if (zaman != null) { guvenceler += $" + .tsr (zaman damgası {zaman:dd.MM.yyyy HH:mm:ss} UTC)"; _b.Gunluk($"KANIT PAKETİ -> zaman damgası alındı: {zaman:o} ({Path.GetFileName(yol)}).", TaramaRenkleri.Yesil); }
                else { guvenceler += " (zaman damgası ALINAMADI)"; KanitImzasi.ZamanDamgasiYokNotuYaz(yol, neden); TemaliMesaj.Goster("Zaman damgası alınamadı (internet ya da sunucu sorunu olabilir). Paket yine de hazır; zaman damgasız." + nl + "Ayrıntı: " + neden, "Zaman damgası", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            // Başarı bildirimi.
            _b.Durum("Adli kanıt paketi masaüstüne kaydedildi: " + Path.GetFileName(yol) + " · dış güvence: " + guvenceler, true, TaramaRenkleri.Yesil);
            // Dosyayı Gezgin'de göster (açılamazsa sorun değil).
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{yol}\"") { UseShellExecute = true }); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "KanitPaketi.btnKanit_Click", true); }
        }
        // Yazma sırasında sorun olursa göster.
        catch (Exception ex) { TemaliMesaj.Goster(ex.Message, "Kanıt paketi oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        // Düğmeyi tekrar aç.
        finally { _dugme.Enabled = true; }
    }
}
