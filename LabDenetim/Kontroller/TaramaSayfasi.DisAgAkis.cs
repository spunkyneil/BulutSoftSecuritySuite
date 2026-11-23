// Bu dosya "Dış Ağ (Yalnızca Pasif)" tarama akışıdır. Hedef genel internetteyse program reddetmez; kullanıcıdan ONAY alır,
// hız sınırını uygular ve yalnızca pasif HTTP/TLS/DNS/security.txt gözlemi yapar. Port taraması, form/yük testi ve kaba kuvvet bu akışta YOKTUR (Kapsam.cs'te kilitli).
// İç ağ hedefleri bu akışa girmez: eski (tam) tarama akışı aynen çalışır.
namespace LabDenetim;

// Son pasif gözlem ve ondan çıkan bulgular (Etik Bildirim Raporu bunlardan üretilir; Durum Panosu da buradan okur).
internal static class PasifDeposu
{
    // Son gözlem ve bulgular.
    public static PasifGozlem? Gozlem { get; set; } public static List<PasifBulgu> Bulgular { get; set; } = new();
}

// Form1'in dış ağ pasif gözlem parçası.
internal sealed partial class TaramaSayfasi
{

    // Hedef dış ağdaysa tüm pasif akışı yürütür ve true döner; iç ağ/reddedilen hedeflerde false döner (eski akış sürer).
    internal async Task<bool> DisAgPasifDeneAsync()
    {
        // Hedef boşsa eski kontroller uyarsın.
        var hedef = txtHedef.Text.Trim(); if (hedef.Length == 0) return false;
        // Sınıflandır (en çok 10 sn; çözülemezse eski akış uygun hatayı verir).
        KapsamKarari karar; try { using var zs = new CancellationTokenSource(TimeSpan.FromSeconds(10)); karar = await Kapsam.SiniflaAsync(hedef, zs.Token); } catch { return false; }
        // Dış ağ değilse bu akış işi değil.
        if (karar.Mod != KapsamModu.DisAgPasif) return false;
        // --- Onay (varsayılan "Hayır") ---
        string onay = $"\"{karar.Host}\" genel internette (dış ağ).\n\nBu hedefte YALNIZCA PASİF gözlem yapılır:\n  • Ana sayfaya standart tarayıcı gibi birkaç GET/HEAD isteği\n  • Bir TLS el sıkışması (sertifika okuma)\n  • DNS sorguları (SPF/DMARC/CAA) ve security.txt araması\n\nPort taraması, form/yük testi ve şifre deneme YAPILMAZ (kod seviyesinde kilitli).\n\nBu hedefi gözlemlemek için sistem sahibi olduğunuzu veya yazılı izniniz bulunduğunu onaylıyor musunuz?";
        // Hassas kurum alanları için ek uyarı.
        if (Kapsam.HassasAlanMi(karar.Host)) onay += "\n\nUYARI: Bu alan adı kamu/askeri/eğitim kurumuna benziyor. İzinsiz gözlem hukuki sorun doğurabilir; yalnızca yetkiliyseniz devam edin.";
        // Soru.
        if (TemaliMesaj.Goster(onay, "Dış Ağ — Pasif Gözlem Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) { Durum("Dış ağ gözlemi onaylanmadı; hiçbir istek gönderilmedi."); return true; }
        // --- Hız sınırı ---
        var hak = Kapsam.GozlemHakki(DateTime.Now); if (!hak.Izinli) { TemaliMesaj.Goster(hak.Neden, "Hız Sınırı", MessageBoxButtons.OK, MessageBoxIcon.Information); return true; }
        // --- Arayüzü hazırla (eski tarama akışıyla aynı sıfırlama) ---
        _bulgular.Clear(); dgvBulgular.Rows.Clear(); _yesil = _sari = _kirmizi = 0; _taranan = _toplamKapi = 0; _saat.Reset(); _sureOverride = null; SayaclariYaz(); _iptal = new CancellationTokenSource(); var ct = _iptal.Token; Calisiyor(true);
        // Dış hedefte "bu bilgisayar" değildir.
        _sonHedefYerel = false; _sonHedefIp = karar.Adresler.FirstOrDefault() ?? System.Net.IPAddress.Loopback;
        // Korumalı bölge.
        try
        {
            // Günlük.
            Gunluk($"⚡ DIŞ AĞ PASİF GÖZLEM -> Hedef: {karar.Host} (yalnızca pasif; port/form/şifre testi kilitli)", RakamYesil);
            // Gözlem yeri: sensör seçiliyse oradan, değilse bu bilgisayardan.
            PasifGozlem g;
            if (_sensorYeri.Secilen() is { } snsr)
            {
                // Sensörden.
                Gunluk($"GÖZLEM YERİ -> \"{snsr.Ad}\" sensörü (SSH); hedef sensörde de genel adres kontrolünden geçer.", Elektrik); Durum("Pasif gözlem sensörden yapılıyor...");
                g = await SensorPasif.GozlemAsync(snsr, karar.Host, ct);
            }
            else
            {
                // Bu bilgisayardan.
                Gunluk("GÖZLEM YERİ -> Bu bilgisayar (yerel).", Elektrik);
                g = await PasifToplayici.GozlemYapAsync(karar, m => { Durum(m, false); Gunluk("PASİF -> " + m, Elektrik); }, ct);
            }
            // Kuralları uygula.
            var kurallar = PasifKurallar.Yukle(out var uyarilar); foreach (var u in uyarilar) Gunluk("KURAL UYARISI -> " + u, Simsek);
            var bulgular = PasifKurallar.Degerlendir(g, kurallar); bulgular = bulgular.Concat(DesteksizYazilim.WebBulgulari(g.Http).Where(x => !bulgular.Any(y => y.RuleId == x.RuleId))).OrderByDescending(x => x.Etki).ToList(); PasifDeposu.Gozlem = g; PasifDeposu.Bulgular = bulgular;
            // Tabloya yaz.
            foreach (var b in bulgular) Ekle(b.ToBulgu());
            // Gözlem notları (ör. 443 kapalı) bilgi olarak.
            foreach (var n in g.Notlar) Ekle(new Bulgu("Pasif Gözlem", "Gözlem notu", Onem.Bilgi, n, "Gerekirse hedefi sahibiyle doğrulayın."));
            // Hiç sorun yoksa güzel haber.
            if (bulgular.Count == 0) Ekle(new Bulgu("Pasif Gözlem", "Pasif gözlemde sorun bulunmadı", Onem.Bilgi, "HTTP başlıkları, TLS sertifikası, DNS (SPF/DMARC) ve security.txt kontrol edildi.", "Yapılacak bir şey yok."));
            // Bitiş.
            int dk = bulgular.Sum(x => x.TahminiDk);
            Durum($"Bitti. {bulgular.Count} bulgu, tahmini toplam düzeltme süresi ~{dk} dk. Durum Panosu'ndan \"Etik Bildirim Raporu\" üretebilirsiniz.", false);
            Gunluk($"⚡ PASİF GÖZLEM TAMAMLANDI -> {bulgular.Count} bulgu, güvenlik puanı {PuanHesapla()}/100, süre {_saat.Elapsed:mm\\:ss}", RakamYesil);
            // Köşe bildirimi.
            Bildirim.Goster("Pasif gözlem tamamlandı", $"{bulgular.Count} bulgu · güvenlik puanı {PuanHesapla()}/100 · süre {_saat.Elapsed:mm\\:ss}", BildirimTuru.Basari);
            // Geçmişe kaydet.
            GecmisKaydet();
        }
        // Durdurma.
        catch (OperationCanceledException) { Durum("Gözlem durduruldu."); }
        // Kapsam kilidi ihlali (güvenlik amaçlı durdurma).
        catch (KapsamIhlaliException ex) { Durum("Güvenlik kilidi gözlemi durdurdu: " + ex.Message, true, RakamKirmizi); TemaliMesaj.Goster(ex.Message, "Güvenlik Kilidi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        // Diğer hata.
        catch (Exception ex) { Durum("Bir sorun oldu: " + ex.Message, true, RakamKirmizi); TemaliMesaj.Goster(ex.Message, "Gözlem durduruldu", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        // Kilitleri aç.
        finally { Calisiyor(false); }
        // İşlendi.
        return true;
    }
}
