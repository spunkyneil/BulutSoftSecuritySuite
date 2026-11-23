// Gözetimsiz Politika arayüzünün arka planı: kalıcılık, mühür/kurcalama savunması, durum metinleri, yönetici onayı, denetim izi ve pencerenin kurulabilmesi.
// Hiçbir sistem değiştirici eylem çalıştırılmaz (politika yalnızca karar ve kayıt tutar).
using System.Reflection;
using LabDenetim.V2;
using Xunit;

namespace LabDenetim.Tests;

[Collection("Veri")]
public class GozetimsizArayuzTestleri : IDisposable
{
    private readonly string _t = Path.Combine(Path.GetTempPath(), "bs_goz_" + Guid.NewGuid().ToString("N")); private readonly string _eski = BulutKlasor.Kok, _eskiOlay = OlayDeposu.Yol; private readonly int _eskiTekrar = KimlikDeposu.VarsayilanTekrar;
    public GozetimsizArayuzTestleri() { Directory.CreateDirectory(_t); BulutKlasor.Kok = _t; OlayDeposu.Yol = Path.Combine(_t, "olay.jsonl"); KimlikDeposu.VarsayilanTekrar = 1000; Oturum.Aktif = null; }
    public void Dispose() { BulutKlasor.Kok = _eski; OlayDeposu.Yol = _eskiOlay; KimlikDeposu.VarsayilanTekrar = _eskiTekrar; Oturum.Aktif = null; try { Directory.Delete(_t, true); } catch (Exception hkt1) { HataKayit.Sessiz(hkt1, "GozetimsizArayuzTestleri.temizlik"); } }
    static readonly DateTime T0 = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    // ---- Kalıcılık ----
    [Fact] public void Yukle_DosyaYoksa_HepsiKapali()
    {
        var p = UnattendedPolicy.Yukle(); Assert.Empty(p.Tum); Assert.Null(p.YukleUyarisi); foreach (var s in UnattendedPolicy.IzinliSiniflar) Assert.Equal(GozetimsizKarar.Kapali, p.Karar(s, "R", false, T0));
    }
    [Fact] public void Etkinlestir_Kapat_KullanimKaydet_Kalici_YenidenYuklenir()
    {
        var iz = new AuditTrail(BulutKl("iz.jsonl")); var p = UnattendedPolicy.Yukle(null, iz);
        p.Etkinlestir("fw_block_bruteforce", "ali", true, T0, 3, 12); p.Etkinlestir("reverify", "ali", true, T0, 5, 24); p.KullanimKaydet("reverify", T0.AddDays(8));
        var y = UnattendedPolicy.Yukle(); Assert.Equal(2, y.Tum.Count); Assert.Equal((true, 3, 12), (y.Ayar("fw_block_bruteforce")!.Etkin, y.Ayar("fw_block_bruteforce")!.SaatteEnCok, y.Ayar("fw_block_bruteforce")!.OmurSaat)); Assert.Equal(1, y.SonSaatKullanim("reverify", T0.AddDays(8).AddMinutes(5)));
        y.Kapat("fw_block_bruteforce", "ali", T0); var z = UnattendedPolicy.Yukle(); Assert.Null(z.Ayar("fw_block_bruteforce")); Assert.NotNull(z.Ayar("reverify"));
        Assert.Equal(new[] { "policy_changed", "policy_changed" }, iz.Kayitlar.Select(k => k.Action).Take(2));   // değişiklikler denetim izine yazıldı
    }
    static string BulutKl(string ad) => Path.Combine(BulutKlasor.Kok, ad);

    [Fact] public void Kurcalama_MuhursuzDosya_HepsiKapaliBaslar_UyariVerir()
    {
        var p = UnattendedPolicy.Yukle(); p.Etkinlestir("reverify", "ali", true, T0.AddDays(-30)); Assert.NotNull(UnattendedPolicy.Yukle().Ayar("reverify"));
        // Saldırgan dosyayı elle değiştirir (mührü yenilemeden): bir sınıf eklemeye çalışır.
        string icerik = File.ReadAllText(UnattendedPolicy.Yol).Replace("\"Siniflar\": {", "\"Siniflar\": {\"fw_block_bruteforce\": {\"Etkin\": true, \"EtkinlestirmeZamani\": \"2020-01-01T00:00:00Z\", \"SaatteEnCok\": 999, \"OmurSaat\": 999},");
        File.WriteAllText(UnattendedPolicy.Yol, icerik);
        var y = UnattendedPolicy.Yukle(); Assert.Empty(y.Tum); Assert.Contains("mührü", y.YukleUyarisi); Assert.Equal(GozetimsizKarar.Kapali, y.Karar("fw_block_bruteforce", "R", false, T0));   // güvenli yön: hepsi kapalı
    }
    [Fact] public void BozukDosya_VeIzinsizSinif_GuvenliYukleme()
    {
        File.WriteAllText(UnattendedPolicy.Yol, "{bozuk"); var b = UnattendedPolicy.Yukle(); Assert.Empty(b.Tum); Assert.NotNull(b.YukleUyarisi);
        // Mühürlü ama izinli listede olmayan sınıf adı yok sayılır.
        File.WriteAllText(UnattendedPolicy.Yol, "{\"Siniflar\":{\"process_kill\":{\"Etkin\":true,\"EtkinlestirmeZamani\":\"2020-01-01T00:00:00Z\",\"SaatteEnCok\":5,\"OmurSaat\":24},\"reverify\":{\"Etkin\":true,\"EtkinlestirmeZamani\":\"2020-01-01T00:00:00Z\",\"SaatteEnCok\":0,\"OmurSaat\":24}},\"Kullanim\":{}}"); Butunluk.Muhurla(UnattendedPolicy.Yol);
        var y = UnattendedPolicy.Yukle(); Assert.Empty(y.Tum);   // process_kill izinli değil; reverify geçersiz sınır (0)
    }
    [Fact] public void YardimciKayitli_DosyayaYazilmaz_VeYuklenmez()
    {
        var p = UnattendedPolicy.Yukle(); p.YardimciKayitli = true; p.Etkinlestir("fw_block_bruteforce", "ali", true, T0.AddDays(-30)); Assert.DoesNotContain("Yardimci", File.ReadAllText(UnattendedPolicy.Yol));
        Assert.False(UnattendedPolicy.Yukle().YardimciKayitli);   // yeniden açınca yardımcı kayıtlı DEĞİL
    }

    // ---- Durum ve karar metinleri ----
    [Fact] public void DurumMetinleri_Sinifa_Ve_Zamana_Gore()
    {
        var p = new UnattendedPolicy(); Assert.Equal(("Kapalı", 0), GozetimsizMetin.Durum(p, "reverify", T0));
        p.Etkinlestir("reverify", "ali", true, T0); var d = GozetimsizMetin.Durum(p, "reverify", T0.AddDays(2)); Assert.Contains("5 gün kaldı", d.Yazi); Assert.Equal(2, d.Vurgu);
        var son = GozetimsizMetin.Durum(p, "reverify", T0.AddDays(8)); Assert.Equal(("Etkin · gerçek çalışma", 1), son);
        p.Etkinlestir("fw_block_bruteforce", "ali", true, T0); Assert.Contains("ENGELLİ", GozetimsizMetin.Durum(p, "fw_block_bruteforce", T0.AddDays(30)).Yazi);   // UAC'li + yardımcı yok
        p.YardimciKayitli = true; Assert.Equal("Etkin · gerçek çalışma", GozetimsizMetin.Durum(p, "fw_block_bruteforce", T0.AddDays(30)).Yazi);
    }
    [Fact] public void KararMetinleri_HerKarariAciklar()
    {
        foreach (var k in Enum.GetValues<GozetimsizKarar>()) Assert.False(string.IsNullOrWhiteSpace(GozetimsizMetin.Karar(k)));
        Assert.Contains("yönetici yetkisi", GozetimsizMetin.Karar(GozetimsizKarar.YardimciYok));
    }
    [Fact] public void Bilgi_VeUacGerekir_Siniflar()
    {
        foreach (var s in UnattendedPolicy.IzinliSiniflar) { Assert.NotEqual(s, UnattendedPolicy.Bilgi(s).Ad); Assert.False(string.IsNullOrWhiteSpace(UnattendedPolicy.Bilgi(s).Aciklama)); }
        Assert.False(UnattendedPolicy.UacGerekir("reverify")); Assert.True(UnattendedPolicy.UacGerekir("fw_block_bruteforce")); Assert.True(UnattendedPolicy.UacGerekir("startup_disable_unsigned_highrisk"));
        Assert.Contains("Acil İzolasyon", GozetimsizMetin.HerZamanOnayli); Assert.Contains("Süreç sonlandırma", GozetimsizMetin.HerZamanOnayli);
    }
    [Fact] public void KuruKalanGun_Hesabi()
    {
        var p = new UnattendedPolicy(); p.Etkinlestir("reverify", "a", true, T0); Assert.Equal(7, p.KuruKalanGun("reverify", T0)); Assert.Equal(1, p.KuruKalanGun("reverify", T0.AddDays(6))); Assert.Equal(0, p.KuruKalanGun("reverify", T0.AddDays(7))); Assert.Equal(0, p.KuruKalanGun("fw_block_bruteforce", T0));
    }

    // ---- Yönetici onayı (kurulum şifresi) ----
    [Fact] public void YoneticiOnayi_DogruYanlisVeYonetici_Olmayan()
    {
        Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili);
        Assert.True(KimlikDeposu.YoneticiOnayi("ibrahim", "KurulumSifre9").Basarili);
        Assert.True(KimlikDeposu.KullaniciEkle("ibrahim", "KurulumSifre9", "ayse", "Analist12345", Rol.Analist).Basarili);
        var r = KimlikDeposu.YoneticiOnayi("ayse", "KurulumSifre9"); Assert.False(r.Basarili); Assert.Contains("Yönetici", r.Mesaj);   // doğru şifre bile olsa yönetici olmayan reddedilir
        Assert.False(KimlikDeposu.YoneticiOnayi("yok", "KurulumSifre9").Basarili);
        Assert.False(KimlikDeposu.YoneticiOnayi("ibrahim", "yanlis-sifre").Basarili);
    }
    [Fact] public void YoneticiOnayi_CokYanlisDeneme_Kilitler()
    {
        Assert.True(KimlikDeposu.Kur("KurulumSifre9", "ibrahim", "Yonetici1234").Basarili); IslemSonucu son = default!;
        for (int i = 0; i < 12; i++) son = KimlikDeposu.YoneticiOnayi("ibrahim", "yanlis" + i);
        Assert.False(son.Basarili); Assert.False(KimlikDeposu.YoneticiOnayi("ibrahim", "KurulumSifre9").Basarili); Assert.Contains("bekleyin", KimlikDeposu.YoneticiOnayi("ibrahim", "KurulumSifre9").Mesaj);   // kaba kuvvet kilidi
    }

    // ---- Önizleme kaydı ----
    [Fact] public void OnizlemeKaydi_DenetimIzineYazilir_VeZincirSaglamKalir()
    {
        var iz = new AuditTrail(BulutKl("denetim_izi.jsonl")); var p = UnattendedPolicy.Yukle(null, iz); p.Etkinlestir("reverify", "ali", true, T0);
        var k = p.Karar("reverify", "ONIZLEME", false, T0); iz.Ekle("ali", "dry_run", null, null, false, $"önizleme: reverify → {k}", T0);
        var yeni = new AuditTrail(BulutKl("denetim_izi.jsonl")); Assert.Equal(2, yeni.Kayitlar.Count); Assert.Contains("KuruCalistirma", yeni.Kayitlar[1].Detail); Assert.True(yeni.Dogrula().Saglam);
        // Dosya kurcalanırsa bozulma yakalanır.
        File.WriteAllText(BulutKl("denetim_izi.jsonl"), File.ReadAllText(BulutKl("denetim_izi.jsonl")).Replace("ali", "mallory")); Assert.False(new AuditTrail(BulutKl("denetim_izi.jsonl")).Dogrula().Saglam);
    }

    // ---- Pencere ----
    [Fact] public void Pencere_Kurulur_UcSinifListelenir_HicbirSeyDegistirmez()
    {
        Exception? hata = null; int satir = 0, kayitOnce = 0; string durum1 = "";
        var t = new Thread(() =>
        {
            try
            {
                using var f = new GozetimsizFormu(_ => { }); using var host = SayfaTestKabi.Goster(f); Application.DoEvents();
                var tablo = (DataGridView)typeof(GozetimsizFormu).GetField("_t", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f)!; satir = tablo.Rows.Count; durum1 = Convert.ToString(tablo.Rows[0].Cells["D"].Value) ?? "";
                kayitOnce = Directory.Exists(_t) ? Directory.GetFiles(_t).Length : 0; f.Close();
            }
            catch (Exception ex) { hata = ex; }
        }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join(); if (hata != null) throw hata;
        Assert.Equal(3, satir); Assert.Equal("Kapalı", durum1); Assert.False(File.Exists(UnattendedPolicy.Yol));   // açmak politika dosyası oluşturmaz, hiçbir sınıf etkinleşmez
    }
}
