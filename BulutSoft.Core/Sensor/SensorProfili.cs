// Uzak sensör profili ve kayıtlı sensörlerin listesi (sensorler.json).
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Bir sensörün bağlantı bilgisi (parola YOKTUR; yalnızca anahtar dosyasının yolu saklanır).
internal sealed record SensorProfili(string Ad, string Host, int Port, string Kullanici, string AnahtarYolu);

// Sensörlerin listesi dosyada saklanır.
internal static class SensorDeposu
{
    // Dosya yolu.
    public static string Yol => BulutKlasor.Dosya("sensorler.json");

    // Listeyi okur; yoksa boş döner.
    // Dosyadaki her profil YÜKLENİRKEN de kayıttaki kurallarla doğrulanır: elle/zararlıyla değiştirilmiş sensorler.json saldırgan bir adrese SSH yönlendiremesin.
    // Geçersiz profiller atılır ve olay günlüğüne yazılır (anahtar dosyasının var olup olmadığı burada aranmaz; o kayıt/kullanım anında denetlenir).
    public static List<SensorProfili> Yukle()
    {
        var ham = Depo.Yukle<List<SensorProfili>>(Yol) ?? new(); var iyi = new List<SensorProfili>();
        foreach (var p in ham)
        {
            string? hata = p == null ? "boş kayıt" : !SensorGuvenlik.AdGecerli(p.Ad) ? "ad geçersiz" : !SensorGuvenlik.HostGecerli(p.Host) ? "adres özel ağdan IPv4 değil" : !SensorGuvenlik.PortGecerli(p.Port) ? "kapı geçersiz" : !SensorGuvenlik.KullaniciGecerli(p.Kullanici) ? "kullanıcı adı geçersiz" : (string.IsNullOrWhiteSpace(p.AnahtarYolu) || p.AnahtarYolu.StartsWith('-') || p.AnahtarYolu.Any(char.IsControl)) ? "anahtar yolu geçersiz" : null;
            if (hata == null) iyi.Add(p!); else { try { OlayDeposu.Ekle("Sistem", "Uyarı", $"SENSÖR UYARI -> sensorler.json içindeki bir profil yüklenmedi ({hata}): dosya değiştirilmiş olabilir."); } catch (Exception hk1) { HataKayit.Sessiz(hk1, "SensorMantigi.Yukle"); } }
        }
        return iyi;
    }

    // Listeyi yazar.
    public static void Kaydet(List<SensorProfili> l) { Depo.YazJson(Yol, l, new JsonSerializerOptions { WriteIndented = true }); }
}
