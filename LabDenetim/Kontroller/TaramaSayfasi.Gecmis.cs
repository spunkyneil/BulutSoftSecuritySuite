// Bu dosya "Geçmiş Operasyonlar" menüsünün arayüz kısmıdır; kayıt deposu BulutSoft.Core içindedir (GecmisDeposu.cs).
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

// Form1'in on üçüncü parçası: geçmiş operasyonlar ve rapor bilgileri.
internal sealed partial class TaramaSayfasi
{
    // Eski bir kayıt ekrana yüklenirken günlüğe tek tek uyarı yazılmasın diye bayrak.
    internal bool _gecmisYukleniyor;
    // Ekrandaki sonuçların süresi (eski kayıt yüklüyse onun süresi; yoksa boş ve kronometre kullanılır).
    internal TimeSpan? _sureOverride;
    // Ekrandaki sonuçların tarama tarihi ve profil adı (rapor başlığında kullanılır).
    internal DateTime _sonTarih = DateTime.Now;
    // Ekrandaki sonuçların profil adı.
    internal string _sonProfilAdi = "Standart";

    // Şu anki profilin adını verir.
    internal string ProfilAdiAl() => _profilSira switch { 0 => "Hızlı", 1 => "Standart", 2 => "Kapsamlı", _ => "Özel" };

    // Biten taramayı geçmişe kaydeder ve önceki taramayla karşılaştırır.
    internal void GecmisKaydet()
    {
        // Kayıt başarısız olursa program bozulmasın.
        try
        {
            // Hedef adres.
            string hedef = txtHedef.Text.Trim();
            // Aynı hedefin bir önceki kaydı (yeni kayıt eklenmeden önce bulunur).
            var onceki = GecmisDeposu.Listele().FirstOrDefault(k => string.Equals(k.Hedef, hedef, StringComparison.OrdinalIgnoreCase));
            // Şimdiki zaman.
            var simdi = DateTime.Now;
            // Kaydı hazırla.
            var kayit = new GecmisKayit(simdi.ToString("yyyyMMdd_HHmmss_fff"), simdi, hedef, _sonHedefYerel, ProfilAdiAl(), _toplamKapi, (int)_saat.Elapsed.TotalSeconds, PuanHesapla(), _kirmizi, _sari, _yesil, _bulgular.Count(Atlandi), _bulgular.ToList());
            // Dosyaya yaz.
            GecmisDeposu.Kaydet(kayit);
            // Rapor başlığı için tarih ve profili sakla.
            _sonTarih = simdi; _sonProfilAdi = kayit.Profil;
            // Kolay Görünümdeki "Son Tarama Durumu" kartı yeni kayıtla anında güncellenir.
            Sahibi.KolayGostergeleriYenile();
            // Günlüğe yaz.
            Gunluk("GEÇMİŞ -> Operasyon kaydedildi (sol menü > Geçmiş Operasyonlar).", Elektrik);
            // Önceki bir tarama varsa karşılaştırmayı da yaz.
            if (onceki != null)
            {
                // Karşılaştır.
                var k = GecmisDeposu.Karsilastir(kayit, onceki);
                // Günlüğe renkli yaz: iyileşme yeşil, kötüleşme turuncu.
                Gunluk("KARŞILAŞTIRMA -> " + k.Ozet, k.PuanFarki >= 0 ? RakamYesil : DesignTokens.Ozgun(TokenRol.StatusWarning));
            }
        }
        // Kaydedilemezse (disk dolu vb.) sessizce geç.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "Gecmis.GecmisKaydet"); }
    }


    // Eski bir kaydı ekrana getirir (tablo, kartlar, sayaçlar, asistan).
    internal void YukleKayit(GecmisKayit k)
    {
        // Tarama sürerken sonuçlar değiştirilmesin.
        if (!btnBaslat.Enabled) { TemaliMesaj.Goster("Şu an bir tarama sürüyor. Bitmesini bekleyin veya önce \"Durdur\"a basın.", "Biraz bekleyin", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Ekrandaki eski sonuçları temizle.
        _bulgular.Clear(); dgvBulgular.Rows.Clear();
        // Sayaçları sıfırla.
        _yesil = _sari = _kirmizi = 0;
        // Günlük kalabalık etmesin.
        _gecmisYukleniyor = true;
        // Hata olsa da bayrak mutlaka kapansın.
        try { foreach (var b in k.Bulgular) Ekle(b); }
        // Bayrağı kapat.
        finally { _gecmisYukleniyor = false; }
        // Kayıttaki hedefi, kapı sayısını, süreyi ve tarihi ekrana yansıt.
        txtHedef.Text = k.Hedef; _sonHedefYerel = k.Yerel; _sonHedefIp = System.Net.IPAddress.TryParse(k.Hedef, out var kayitIp) ? kayitIp : System.Net.IPAddress.Loopback; _taranan = _toplamKapi = k.KapiSayisi; _sureOverride = TimeSpan.FromSeconds(k.SureSn); _sonTarih = k.Tarih; _sonProfilAdi = k.Profil;
        // Sayaçları ve kartları güncelle.
        SayaclariYaz();
        // İlk satırı seç ki asistan dolsun.
        if (dgvBulgular.Rows.Count > 0) { dgvBulgular.ClearSelection(); dgvBulgular.Rows[0].Selected = true; }
        // Durum çubuğuna ve günlüğe bilgi ver.
        Durum($"Geçmiş kayıt yüklendi: {k.Tarih:dd.MM.yyyy HH:mm} · {k.Hedef} · puan {k.Puan}/100. (Yeni tarama başlatana kadar rapor bu kayıttan alınır.)");
    }

    // Rapor başlığında görünecek tarama bilgilerini toplar.
    internal RaporBilgisi RaporBilgisiOlustur() =>
        new(txtHedef.Text.Trim(), _sonTarih, _sonProfilAdi, _toplamKapi, _sureOverride ?? _saat.Elapsed, PuanHesapla(), _kirmizi, _sari, _yesil, _bulgular.Count(Atlandi));

    // Rapora, her bulgu için bilgi bankasından "Bu ne demek / Nasıl çözerim / Komut" ekler.
    internal (string NeDemek, string Nasil, string Komut) RaporAciklayici(Bulgu b)
    {
        // Bilgi bankasından yardım paketini al.
        var c = CozumUret(b);
        // Raporun istediği biçimde geri ver.
        return (c.NeDemek, c.NasilCozerim, c.Komut);
    }
}
