// Tuzakların (honeypot, canary dosyaları, fidye alarmı) arayüz bağlantısı: olayları günlüğe, olay çizgisine ve uyarı kutusuna taşır; önceki oturumdaki tuzakları yeniden açar.
// Motorlar BulutSoft.Core içindedir (TuzakMotorlari.cs). Eskiden Form1'in Tuzaklar.cs partial dosyasındaydı.

// Programımızın adı.
namespace LabDenetim;

// Tuzak eylemi.
internal sealed class TuzakEylemi
{
    private readonly ITaramaBaglami _b; private readonly UyariKutusu _kutu;
    // Üç tuzak motoru.
    public readonly HoneypotKapi Honeypot = new(); public readonly CanaryDosyalari Canary = new(); public readonly FidyeIzleyici Fidye = new();

    public TuzakEylemi(ITaramaBaglami baglam, UyariKutusu kutu) { _b = baglam; _kutu = kutu; }

    // Olayları bağlar. Form1 açılırken bir kez çağrılır.
    public void Kur()
    {
        // Honeypot'a biri bağlandı.
        Honeypot.Baglandi += (kaynak, port, veri) => Alarm("◈ HONEYPOT KAPISINA BAĞLANTI!", $"Kaynak: {kaynak} → kapı {port}", "Bu kapıda gerçek bir hizmet yok; bağlanan kişi/program yetkisiz keşif yapıyor olabilir." + (veri.Length > 0 ? $" Gönderdiği ilk veri: \"{veri}\"" : ""), $"TUZAK ALARM -> Honeypot kapısına bağlantı: {kaynak} → kapı {port}" + (veri.Length > 0 ? $" · ilk veri: \"{veri}\"" : ""));
        // Canary dosyaya dokunuldu.
        Canary.Tetiklendi += (ne, dosya) => Alarm("⚠ TUZAK DOSYAYA DOKUNULDU!", $"{dosya} — {ne}", "Bu dosyayı siz açmadıysanız, bilgisayarınızda dosyalara göz atan/çalan bir program veya kişi olabilir.", $"TUZAK ALARM -> Canary dosya {ne}: {dosya}");
        // Fidye davranışı.
        Fidye.Supheli += (neden, sayi) => { Alarm("⚠ FİDYE YAZILIMI DAVRANIŞI ŞÜPHESİ!", $"{sayi} dosya: {neden}", "Büyük bir kopyalama/arşiv açma değilse HEMEN ağı kesin (⚡ Acil İzolasyon) ve bilgisayarı kapatmayı düşünün.", $"TUZAK ALARM -> FİDYE YAZILIMI şüphesi: {sayi} dosya ({neden})"); _ = FidyeSurecIncele(); };
        // Program kapanırken tüm tuzakları bırak (Canary dosyaları SİLİNMEZ; kullanıcı kendi kaldırır).
        _b.KapanirkenCalistir(() => { Honeypot.Dispose(); Canary.Dispose(); Fidye.Dispose(); });
    }

    // Fidye alarmı gelince kısa bir ölçümle (4 sn) o anda diske en çok yazan süreçleri bulur ve günlüğe yazar.
    private async Task FidyeSurecIncele()
    {
        try
        {
            var liste = await Task.Run(() => SurecAvcisi.Olc(4)); var ilk = liste.Where(x => !SurecAvcisi.Korunuyor(x.Ad, x.Pid)).Take(3).ToList(); if (ilk.Count == 0 || !_b.PencereHazir) return;
            string metin = string.Join(", ", ilk.Select(x => $"{x.Ad} (PID {x.Pid}, {x.YazilanBayt / 1048576.0 / 4:0.0} MB/sn)"));
            _b.UiIsle(() => _b.Gunluk($"TUZAK ALARM -> Fidye şüphesi anında diske en çok yazan süreçler: {metin}. Ayrıntı için Süreç Avcısı'nı açın." + (ServisIstemci.Calisiyor ? "" : " (Bu yalnızca alarmdan sonraki 4 sn'lik ölçümdür: tam süreç atfı için koruma hizmeti — ETW, SYSTEM yetkisi — gerekli.)"), TaramaRenkleri.Kirmizi));
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "Tuzaklar.FidyeSurecIncele"); }
    }

    // Önceki oturumda açık olan tuzakları yeniden açar. Pilot (salt okunur) modda ve izinsiz rolde hiçbir şey açılmaz.
    public void GeriYukle()
    {
        var d = TuzakDurumu.Yukle(); if (d == null || PilotMod.SaltOkunur || !Yetki.Izinli(ServisBaglami.Oturum.Aktif?.Rol, Eylem.Koruma)) return;
        try { if (d.Fidye && !Fidye.Aktif) { Fidye.Baslat(); _b.Gunluk($"TUZAK -> Önceki oturumdaki fidye alarmı yeniden açıldı ({Fidye.Klasorler.Count} klasör).", TaramaRenkleri.Elektrik); } } catch (Exception hk2) { HataKayit.Sessiz(hk2, "Tuzaklar.TuzakGeriYukle"); }
        try { if (d.Canary && !Canary.Aktif) { Canary.Baslat(); _b.Gunluk("TUZAK -> Önceki oturumdaki canary izleme yeniden açıldı.", TaramaRenkleri.Elektrik); } } catch (Exception hk3) { HataKayit.Sessiz(hk3, "Tuzaklar.TuzakGeriYukle"); }
        try { if (d.Honeypot && !Honeypot.Aktif) { var h = Honeypot.Baslat(d.Port, d.Lab); _b.Gunluk(h == null ? $"TUZAK -> Önceki oturumdaki honeypot yeniden açıldı: {Honeypot.Dinlenen}" : "TUZAK -> Honeypot yeniden açılamadı: " + h, h == null ? TaramaRenkleri.Elektrik : TaramaRenkleri.Simsek); } } catch (Exception hk4) { HataKayit.Sessiz(hk4, "Tuzaklar.TuzakGeriYukle"); }
    }

    // Bir tuzak alarmını günlüğe, olay çizgisine ve ekran uyarı kutusuna taşır (başka iş parçacığından çağrılabilir).
    private void Alarm(string baslik, string satir1, string satir2, string log)
    {
        // Pencere yoksa bırak.
        if (!_b.PencereHazir) return;
        // Ekran iş parçacığında göster.
        _b.UiIsle(() =>
        {
            // Günlüğe yaz (aynı zamanda Olay Zaman Çizgisi'ne düşer).
            _b.Gunluk(log, TaramaRenkleri.Kirmizi);
            // Eski uyarı kutusunu kapat.
            _kutu.Goster(baslik, satir1, satir2, TaramaRenkleri.Kirmizi, 12000);
            // Sistem tepsisindeysek balon da çıksın.
            _b.TepsiBalonu(10000, "BulutSoft Tuzak Alarmı", satir1, true);
        });
    }
}
