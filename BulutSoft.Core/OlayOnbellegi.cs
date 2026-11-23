// Bu dosya OlayDeposu'nun ARTIMLI OKUMA katmanıdır: arayüzün sık yoklamaları (küre ışığı, bildirim zili, durum panosu) günlüğü her seferinde baştan ayrıştırmaz.
// Yöntem: her kaynak dosya (yerel günlük ve varsa koruma hizmetinin günlüğü) için okunan bayt konumu (offset) ve bellek içi halka tamponlar tutulur;
// dosya değişmediyse hiçbir şey okunmaz/ayrıştırılmaz, büyüdüyse yalnızca eklenen satırlar ayrıştırılır. Dosya küçülür ya da yeniden oluşursa (2 MB arşiv rotasyonu) önbellek sıfırlanıp baştan kurulur.
// Zincirli HMAC bütünlüğüne ve arşivlemeye dokunulmaz; bütünlük doğrulama ve tam tarama ayrı (Dogrula/Oku/OkuSayfali) kalır.
using System.Globalization;
using System.Text;
using System.Text.Json;

// Programımızın adı.
namespace LabDenetim;

internal static partial class OlayDeposu
{
    // Her tamponda tutulan en çok kayıt.
    internal const int HalkaKapasitesi = 2000;

    // Bir kaynak dosyanın artımlı okuma durumu.
    private sealed class KaynakDurumu
    {
        public long Konum; public DateTime Olusma; public readonly List<OlayKaydi> Tum = new(); public readonly List<OlayKaydi> Onemli = new();
        public void Sifirla(DateTime olusma) { Konum = 0; Olusma = olusma; Tum.Clear(); Onemli.Clear(); }
        public void Ekle(OlayKaydi k)
        {
            Tum.Add(k); if (Tum.Count > HalkaKapasitesi * 2) Tum.RemoveRange(0, Tum.Count - HalkaKapasitesi);
            if (k.Seviye is "Uyarı" or "Kritik") { Onemli.Add(k); if (Onemli.Count > HalkaKapasitesi * 2) Onemli.RemoveRange(0, Onemli.Count - HalkaKapasitesi); }
        }
    }

    private static readonly Dictionary<string, KaynakDurumu> Kaynaklar = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object OnbellekKilit = new();
    private static long _ayrisanSatir;

    // Sınama/ölçüm için: önbellek tarafından şimdiye dek JSON'dan çözülen toplam satır sayısı (değişmeyen dosyada artmamalı).
    internal static long AyrisanSatirSayisi => Interlocked.Read(ref _ayrisanSatir);

    // Önbelleği boşaltır (sınamalarda yol değişince / elle).
    internal static void OnbellegiSifirla() { lock (OnbellekKilit) Kaynaklar.Clear(); }

    // Kaynak dosyadan yalnızca yeni eklenen TAMAMLANMIŞ satırları okuyup tamponlara ekler (OnbellekKilit içinde çağrılır).
    private static KaynakDurumu? KaynakGuncelle(string yol)
    {
        try
        {
            if (!Kaynaklar.TryGetValue(yol, out var k)) Kaynaklar[yol] = k = new KaynakDurumu();
            var fi = new FileInfo(yol);
            if (!fi.Exists) { k.Sifirla(default); return k; }
            // Yeniden oluşmuş (arşivlenmiş) ya da kırpılmış dosya: baştan kur.
            if (fi.CreationTimeUtc != k.Olusma || fi.Length < k.Konum) k.Sifirla(fi.CreationTimeUtc);
            if (fi.Length == k.Konum) return k;   // DEĞİŞMEDİ: ayrıştırma yok
            using var fs = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long son = fs.Length; if (son <= k.Konum) return k;
            fs.Seek(k.Konum, SeekOrigin.Begin); var tampon = new byte[son - k.Konum]; int okunan = 0, n;
            while (okunan < tampon.Length && (n = fs.Read(tampon, okunan, tampon.Length - okunan)) > 0) okunan += n;
            int sonSatir = Array.LastIndexOf(tampon, (byte)'\n', okunan - 1); if (sonSatir < 0) return k;   // henüz tamamlanmamış satır: sonraki yoklamada
            foreach (var satir in Encoding.UTF8.GetString(tampon, 0, sonSatir).Split('\n'))
            {
                if (satir.Length == 0) continue;
                Interlocked.Increment(ref _ayrisanSatir);
                try { var kayit = JsonSerializer.Deserialize<OlayKaydi>(satir); if (kayit != null) k.Ekle(kayit); } catch (JsonException hk) { HataKayit.Sessiz(hk, "OlayDeposu.KaynakGuncelle"); }
            }
            k.Konum += sonSatir + 1;
            return k;
        }
        catch (IOException hk) { HataKayit.Sessiz(hk, "OlayDeposu.KaynakGuncelle"); return null; }
        catch (UnauthorizedAccessException hk) { HataKayit.Sessiz(hk, "OlayDeposu.KaynakGuncelle"); return null; }
    }

    // Yerel günlük ve (farklıysa) koruma hizmetinin günlüğü için güncel durumlar.
    private static List<KaynakDurumu> GuncelKaynaklar()
    {
        var l = new List<KaynakDurumu>(2); var y = KaynakGuncelle(Yol); if (y != null) l.Add(y);
        try
        {
            var hy = HizmetOkumaYolu?.Invoke();
            if (!string.IsNullOrEmpty(hy) && !string.Equals(Path.GetFullPath(hy), Path.GetFullPath(Yol), StringComparison.OrdinalIgnoreCase)) { var h = KaynakGuncelle(hy); if (h != null) l.Add(h); }
        }
        catch (Exception hk) { HataKayit.Sessiz(hk, "OlayDeposu.GuncelKaynaklar"); }
        return l;
    }

    // En yeni olaylar (en yeniden eskiye), artımlı okumayla. Durum panosu gibi "son birkaç olay" ihtiyaçları için.
    public static List<OlayKaydi> SonOlaylar(int adet)
    {
        lock (OnbellekKilit) return GuncelKaynaklar().SelectMany(k => k.Tum).OrderByDescending(o => o.Zaman).Take(Math.Max(0, adet)).ToList();
    }

    // Son "pencere" içindeki Uyarı/Kritik olaylar (artımlı okuma): küre ışığı ve bildirim zili için. Eski/gelecekteki saatli kayıtları süzme KureIsiklari'nda yapılır.
    public static List<OlayKaydi> SonUyariKritik(TimeSpan pencere, DateTime simdi)
    {
        lock (OnbellekKilit)
        {
            var alt = simdi - pencere;
            return GuncelKaynaklar().SelectMany(k => k.Onemli).Where(o => o.Zaman >= alt).OrderByDescending(o => o.Zaman).ToList();
        }
    }

    // SAYFALI tam tarama (Olay Zaman Çizgisi): satırları en yeniden eskiye sıralar ama yalnızca istenen sayfayı JSON'dan çözer. Toplam, tüm (yerel + hizmet) satır sayısıdır.
    public static (List<OlayKaydi> Sayfa, int Toplam) OkuSayfali(int sayfa, int boyut)
    {
        var sonuc = new List<OlayKaydi>(); boyut = Math.Max(1, boyut); sayfa = Math.Max(0, sayfa);
        try
        {
            var ham = new List<(DateTime Zaman, string Satir)>();
            void Topla(string yol, bool kilitle)
            {
                if (!File.Exists(yol)) return;
                IEnumerable<string> satirlar;
                if (kilitle) { lock (Kilit) satirlar = File.ReadAllLines(yol, Encoding.UTF8); }
                else { using var fs = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var sr = new StreamReader(fs, Encoding.UTF8); var ek = new List<string>(); string? s; while ((s = sr.ReadLine()) != null) ek.Add(s); satirlar = ek; }
                foreach (var s in satirlar) if (s.Length > 0) ham.Add((ZamanAnahtari(s), s));
            }
            Topla(Yol, true);
            try { var hy = HizmetOkumaYolu?.Invoke(); if (!string.IsNullOrEmpty(hy) && !string.Equals(Path.GetFullPath(hy), Path.GetFullPath(Yol), StringComparison.OrdinalIgnoreCase)) Topla(hy, false); } catch (Exception hk) { HataKayit.Sessiz(hk, "OlayDeposu.OkuSayfali"); }
            ham.Sort((a, b) => b.Zaman.CompareTo(a.Zaman));
            foreach (var (_, satir) in ham.Skip(sayfa * boyut).Take(boyut)) { try { var k = JsonSerializer.Deserialize<OlayKaydi>(satir); if (k != null) sonuc.Add(k); } catch (JsonException hk) { HataKayit.Sessiz(hk, "OlayDeposu.OkuSayfali"); } }
            return (sonuc, ham.Count);
        }
        catch (Exception hk) { HataKayit.Sessiz(hk, "OlayDeposu.OkuSayfali"); return (sonuc, sonuc.Count); }
    }

    // Satırdaki "Zaman" değerini JSON çözmeden (ucuz) okur; bulunamazsa en eski sayılır.
    private static DateTime ZamanAnahtari(string satir)
    {
        const string a = "\"Zaman\":\""; int i = satir.IndexOf(a, StringComparison.Ordinal); if (i < 0) return DateTime.MinValue;
        int b = i + a.Length, e = satir.IndexOf('"', b); if (e <= b) return DateTime.MinValue;
        return DateTime.TryParse(satir.AsSpan(b, e - b), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var z) ? z : DateTime.MinValue;
    }
}
