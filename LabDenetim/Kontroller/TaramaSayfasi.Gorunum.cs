// Bu dosya sol menüdeki "◼ Panel" (Canlı Ağ ve Port Radarı) ile "⚡ Yeni Tarama" (tablo + asistan) görünümleri arasındaki geçişi yönetir
// ve radara canlı veri besler.
using System.Text.RegularExpressions;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Form1'in on beşinci parçası: görünümler ve radar.
// Ana pencerenin görünümleri: radar, tarama tablosu, canlı trafik.
public enum Gorunum { Radar, Tarama, Trafik }

internal sealed partial class TaramaSayfasi
{
    // Şu an radar mı görünüyor? (false = tablo + asistan)
    internal bool _radarGorunumu = true;
    // Şu an seçili görünüm.
    internal Gorunum _gorunum = Gorunum.Radar;

    // Radarı ve filtre kutularını çalışır hale getirir. Form1 açılırken bir kez çağrılır.
    internal void GorunumKur()
    {
        // Harita küresi (Uzman Görünüm) ve geçiş şeridi.
        Sahibi.UzmanKureKur();
        // Radarda bir düğüme tıklanınca ilgili satırı göster.
        radar.DugumTiklandi += RadarDugumTiklandi;
        // Hedef adres değişince radardaki hedef yazısı da değişsin.
        txtHedef.TextChanged += (_, _) => RadarYenile();
        // Açılışta radar (Panel) görünümü.
        GorunumSec(true);
    }

    // Eski çağrılar için: true = radar, false = tarama tablosu.
    internal void GorunumSec(bool radarGorunumu) => GorunumSec(radarGorunumu ? Gorunum.Radar : Gorunum.Tarama);

    // Görünümü değiştirir: Radar (◼ Panel), Tarama (⚡ Yeni Tarama) veya Trafik (◈ Canlı Ağ Trafiği).
    internal void GorunumSec(Gorunum g)
    {
        // Seçimi sakla.
        _gorunum = g; _radarGorunumu = g == Gorunum.Radar;
        // Radar yalnızca radar görünümünde görünsün.
        radar.Visible = g == Gorunum.Radar && !Sahibi.HaritaKureAcik; Sahibi.UzmanKureGorunum(g);
        // Tablo + asistan ızgarası tarama ve trafik görünümünde görünsün.
        splIcerik.Visible = g != Gorunum.Radar;
        // Sol kapta hangi tablo görünecek.
        dgvBulgular.Visible = g != Gorunum.Trafik; dgvTrafik.Visible = g == Gorunum.Trafik;
        // Altın profil çubuğu ve uyarı şeridi trafik görünümünde görünmesin.
        pnlAltin.Visible = txtAra.Visible = g != Gorunum.Trafik; _zincir.SeritYenile();
        // Canlı trafik yalnızca kendi görünümünde yenilensin.
        TrafikZamanla(g == Gorunum.Trafik);
        // Radar verisini tazele.
        RadarYenile();
    }

    // Bulgulardan kapı düğümlerini çıkarıp radara gönderir.
    internal void RadarYenile()
    {
        // Radar henüz kurulmadıysa bir şey yapma.
        if (radar == null) return;
        // Kapı numarası → (en yüksek seviye, servis adı).
        var sozluk = new Dictionary<int, (RadarSeviye Seviye, string Ad)>();
        // Her bulguya bak.
        foreach (var b in _bulgular)
        {
            // Yapılamayan kontroller radarda gösterilmez.
            if (b.Atlandi) continue;
            // Bulgunun bir kapı numarası var mı? ("Port 445 (Kapı)")
            var m = Regex.Match(b.Baslik + " " + b.Detay, @"[Pp]ort (\d{1,5})");
            // Yoksa (DNS, web başlıkları vb.) atla.
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out int port)) continue;
            // Bulgunun radar seviyesi.
            var seviye = b.Onem == Onem.Yuksek ? RadarSeviye.Kritik : b.Onem == Onem.Bilgi ? RadarSeviye.Guvenli : RadarSeviye.Uyari;
            // Servis adı: "Açık port 445 (SMB)" başlığından, yoksa bilinen listeden.
            var ad = Regex.Match(b.Baslik, @"Açık port \d+ \((.+?)\)");
            // Adı belirle.
            string servis = ad.Success ? ad.Groups[1].Value : PortTarayici.ServisAdi(port);
            // Aynı kapıda birden çok bulgu varsa en yüksek seviye kalsın.
            if (!sozluk.TryGetValue(port, out var onceki) || seviye > onceki.Seviye) sozluk[port] = (seviye, servis);
        }
        // Sözlüğü düğüm listesine çevir.
        var dugumler = sozluk.OrderBy(k => k.Key).Select(k => new RadarDugum(k.Key, k.Value.Ad, k.Value.Seviye)).ToList();
        // Radara gönder (tarama sürüyor mu bilgisiyle).
        radar.Guncelle(txtHedef.Text.Trim(), dugumler, !btnBaslat.Enabled);
    }

    // Radarda bir düğüme tıklandı: tablo görünümüne geç ve o kapının satırını seç.
    internal void RadarDugumTiklandi(int port)
    {
        // Tablo + asistan görünümüne geç.
        GorunumSec(false);
        // Satır gizli kalmasın diye filtreyi kaldır.
        _filtre = Filtre.Yok; FiltreyiUygula();
        // En tehlikeli eşleşen satırı bul.
        DataGridViewRow? hedef = null; int en = -1;
        // Her satıra bak.
        foreach (DataGridViewRow r in dgvBulgular.Rows)
        {
            // Satırın sonucu bu kapıya mı ait?
            if (r.Tag is not Bulgu b || b.Atlandi || !Regex.IsMatch(b.Baslik + " " + b.Detay, $@"[Pp]ort {port}\b")) continue;
            // Önem puanı: kritik 3, uyarı 2, dikkat 1, güvenli 0.
            int puan = b.Onem switch { Onem.Yuksek => 3, Onem.Orta => 2, Onem.Dusuk => 1, _ => 0 };
            // Daha tehlikeli ise bunu seç.
            if (puan > en) { en = puan; hedef = r; }
        }
        // Satır bulunduysa seç ve görünür yap.
        if (hedef != null)
        {
            // Önceki seçimi kaldır ve bunu seç.
            dgvBulgular.ClearSelection(); hedef.Selected = true;
            // Tabloyu o satıra kaydır.
            try { dgvBulgular.CurrentCell = hedef.Cells[1]; dgvBulgular.FirstDisplayedScrollingRowIndex = hedef.Index; } catch (Exception hk1) { HataKayit.Sessiz(hk1, "Gorunum.RadarDugumTiklandi"); }
        }
        // Kullanıcıya bilgi ver.
        Durum($"Radar: Port {port} satırı gösterildi.");
    }
}
