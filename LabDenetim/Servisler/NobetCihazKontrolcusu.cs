// Nöbetçi modu kontrolünde ağda yeni cihaz var mı bakar (yalnızca envanter daha önce oluşturulmuşsa; özel ağda ping+ARP). Eskiden Form1'in CihazEnvanteri.cs partial dosyasındaydı.

using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Nöbetçi cihaz kontrolü.
internal static class NobetCihazKontrolcusu
{
    // balon: (başlık, metin) ile sistem tepsisi balonu gösterir.
    public static async Task KontrolEtAsync(ITaramaBaglami _b, Action<string, string> balon)
    {
        // Hata olursa nöbet bozulmasın.
        try
        {
            // Envanter yoksa kullanıcı özelliği hiç kullanmamıştır; ağa istek göndermeyiz.
            var env = CihazDeposu.Yukle(); if (env.Count == 0) return;
            // Ağı tara.
            var bulunan = await AgKesfi.TaraAsync(new Progress<string>(_ => { }), new Progress<int>(_ => { }), CancellationToken.None);
            // Envanterle birleştir.
            var yeni = CihazDeposu.Birlestir(env, bulunan, out _);
            // Kaydet.
            CihazDeposu.Kaydet(env);
            // Yeni cihaz varsa uyar.
            if (yeni.Count > 0)
            {
                // Günlüğe yaz (aynı zamanda olay çizgisine düşer).
                _b.Gunluk($"NÖBETÇİ ALARM -> AĞDA YENİ CİHAZ: {string.Join(", ", yeni.Select(k => env[k].Ip + " (" + env[k].Tur + ")"))}", TaramaRenkleri.Kirmizi);
                // Balon.
                balon("BulutSoft Nöbetçi · ağda yeni cihaz", $"DİKKAT: Ağınıza yeni bir cihaz girdi: {env[yeni[0]].Ip}. Menüden \"Ağ Cihaz Envanteri\"ni açın.");
            }
        }
        // Sessizce geç.
        catch (Exception hk2) { HataKayit.Sessiz(hk2, "NobetCihazKontrolcusu.KontrolEtAsync"); }
    }
}
