// Bu dosya Kali Araç Rehberi'nin "Sensörde Çalıştır" akışının MANTIĞINI içerir (form kodundan ayrı, testlenebilir): şablondan giriş alanları üretir, girişleri şablon girdilerine çevirir,
// onay metnini ve sonuç özetini hazırlar. Çalıştırmanın kendisi (hedef/parametre doğrulama, kurulu mu, sudo, ajan) KomutCalistirici'dedir; burası yalnızca arayüz yardımcılarıdır.
using System.Text;
using System.Text.RegularExpressions;

// Programımızın adı.
namespace LabDenetim;

// Bir giriş alanı: Ad ("hedef" ya da parametre adı), ekrandaki etiket, hazır seçenekler (açılır liste) ve hedef alanı mı.
internal sealed record GirdiAlani(string Ad, string Etiket, string[]? Secenek, bool Hedef);

// Rehber çalıştırma yardımcıları.
internal static class RehberCalistirma
{
    private static readonly Regex DuzSecenek = new(@"\A[A-Za-z0-9._/-]+(\|[A-Za-z0-9._/-]+)+\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex OnekliSecenek = new(@"\A([A-Za-z0-9._/-]*)\(([A-Za-z0-9._|-]+)\)\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    // Şablonun sorulacak alanları: önce hedef (varsa), sonra parametreler (tablodaki sırayla).
    public static List<GirdiAlani> Alanlar(KomutTanimi t)
    {
        var l = new List<GirdiAlani>();
        if (t.HedefTuru != "yok") l.Add(new GirdiAlani("hedef", t.HedefTuru switch { "ip" => "Hedef (özel ağ IP adresi)", "alan" => "Müşteri alan adı (örn. musteri.com)", _ => "Hedef (IP adresi veya alan adı)" }, null, true));
        foreach (var p in t.Parametreler ?? new()) { var s = Secenekler(p.Desen, p.Varsayilan ?? ""); l.Add(new GirdiAlani(p.Ad, ParametreEtiketi(p, s != null), s, false)); }
        return l;
    }

    // Parametrenin etiketi (ad, türü, varsayılan ve sayı aralığı).
    private static string ParametreEtiketi(TanimParametre p, bool secenekli)
    {
        string t = p.Tur switch { "dosya" => "Dosya adı (Kali'de ~/bulutsoft_inceleme klasöründe)", "arayuz" => "Ağ arayüzü (örn. eth0)", _ => char.ToUpper(p.Ad[0], System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + p.Ad[1..].Replace('_', ' ') };
        if (p.Min != null || p.Max != null) t += $" ({p.Min?.ToString() ?? "…"}-{p.Max?.ToString() ?? "…"})";
        if (!secenekli && !string.IsNullOrEmpty(p.Varsayilan)) t += $" — boşsa: {p.Varsayilan}";
        return t;
    }

    // Desen yalnızca sabit seçeneklerden oluşuyorsa ("a|b|c" ya da "önek(a|b)") açılır liste seçenekleri; varsayılan başa alınır. Değilse null (serbest kutu).
    internal static string[]? Secenekler(string desen, string varsayilan)
    {
        string[]? s = null;
        try
        {
            if (DuzSecenek.IsMatch(desen)) s = desen.Split('|');
            else { var m = OnekliSecenek.Match(desen); if (m.Success) s = m.Groups[2].Value.Split('|').Select(x => m.Groups[1].Value + x).ToArray(); }
        }
        catch (RegexMatchTimeoutException) { return null; }
        return s?.OrderBy(x => x == varsayilan ? 0 : 1).ToArray();
    }

    // Alanlardan gelen değerleri (aynı sırada) hedef + şablon girdilerine çevirir. Boş değerler girdiye KONMAZ: şablonun varsayılanı kullanılır.
    public static (string Hedef, Dictionary<string, string> Girdiler) Girdiler(KomutTanimi t, string[] degerler)
    {
        var alanlar = Alanlar(t); string hedef = ""; var g = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < alanlar.Count; i++)
        {
            string v = i < degerler.Length ? (degerler[i] ?? "").Trim() : "";
            if (alanlar[i].Hedef) hedef = v; else if (v.Length > 0) g[alanlar[i].Ad] = v;
        }
        return (hedef, g);
    }

    // Açılır liste/başlık satırı.
    public static string SablonBasligi(KomutTanimi t) => t.Ad + (string.IsNullOrEmpty(t.Aciklama) ? "" : " — " + t.Aciklama);

    // Çalıştırma düğmesinin yazısı (masaüstünde açılan araçlar farklı).
    public static string DugmeMetni(IReadOnlyList<KomutTanimi> sablonlar) => sablonlar.Count > 0 && sablonlar.All(x => x.ArkaPlan) ? "Kali Masaüstünde Başlat..." : "Sensörde Çalıştır...";

    // Onay penceresi metni: sensörde gerçekten çalışacak komut + riskler (sudo, internet, hedef yetkisi, masaüstü/lisans, doğrulanmadı notu).
    public static string OnayMetni(KomutTanimi t, string sensorAdi, string hedef, string komut)
    {
        var sb = new StringBuilder();
        sb.Append($"Sensörde ({sensorAdi}) şu komut çalıştırılacak:\r\n\r\n  {komut}\r\n\r\n");
        if (!string.IsNullOrEmpty(t.Aciklama)) sb.Append(t.Aciklama).Append("\r\n\r\n");
        if (t.Sudo) sb.Append("• Bu komut yönetici (sudo) yetkisiyle çalışır.\r\n");
        if (t.Internet) sb.Append("• Bu işlem internete çıkar / dış kaynaklara sorgu gönderir.\r\n");
        if (t.ArkaPlan) sb.Append("• Arayüz Kali sanal makinesinin masaüstünde açılır; program içeride ne yapıldığını göremez. Lisans şartları size/müşterinize aittir.\r\n");
        if (!t.Dogrulandi) sb.Append("• Not: bu şablon henüz gerçek bir Kali'de doğrulanmadı; araç sürümüne göre hata verebilir (hata çıktıda görünür).\r\n");
        sb.Append($"• Tahmini süre ~{t.Tahmini} sn, en çok {t.ZamanAsimi} sn.\r\n\r\n");
        sb.Append(t.HedefTuru != "yok" ? $"Hedefe ({hedef}) yalnızca bu araçla denetim amaçlı istekler gönderilir. Bu hedefi denetleme yetkiniz (yazılı izin) var mı?\r\n\r\n" : "");
        return sb.Append("Çalıştırılsın mı?").ToString();
    }

    // Sonucun tek satırlık durumu.
    public static string DurumSatiri(KomutSonucu r) => r.CikisKodu switch
    {
        KomutCalistirici.AtlandiKodu => "ATLANDI — araç kurulu değil veya kurulum onaylanmadı",
        KomutCalistirici.ZamanAsimiKodu => "ZAMAN AŞIMI — süreç durduruldu (kısmi çıktı aşağıda olabilir)",
        0 => "BAŞARILI (çıkış kodu 0)",
        var k => $"ÇIKIŞ KODU {k} — aracın kendi durumu; stderr ve ayrıntı aşağıda"
    };

    // Sonuç penceresi metni: komut, durum, süre/bulgu sayısı, ham çıktı dosyası ve (varsa) çıktının kendisi (en çok enFazla karakter).
    public static string SonucMetni(KomutSonucu r, int enFazla = 300_000)
    {
        var sb = new StringBuilder();
        sb.Append("$ ").Append(r.Komut).Append("\r\n").Append(DurumSatiri(r)).Append($"\r\nSüre: {r.Sure.TotalSeconds:F1} sn · {r.Bulgular.Count(b => !b.Atlandi)} bulgu\r\n");
        if (r.HamCiktiDosyasi.Length > 0) sb.Append("Ham çıktı dosyası: ").Append(r.HamCiktiDosyasi).Append("\r\n");
        sb.Append("────────────────────────────────────────\r\n");
        string icerik = "";
        if (r.HamCiktiDosyasi.Length > 0)
        {
            try { using var f = new StreamReader(r.HamCiktiDosyasi, Encoding.UTF8); var tampon = new char[enFazla + 1]; int n = f.ReadBlock(tampon, 0, tampon.Length); icerik = new string(tampon, 0, Math.Min(n, enFazla)) + (n > enFazla ? "\n… (kısaltıldı; tamamı ham çıktı dosyasında)" : ""); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { icerik = "(ham çıktı dosyası okunamadı)"; }
        }
        else icerik = string.Join("\n", r.Bulgular.Where(b => b.Atlandi).Select(b => b.Detay + (b.Oneri.Length > 0 ? "\n" + b.Oneri : "")));
        return sb.Append(icerik.Replace("\r\n", "\n").Replace("\n", "\r\n")).ToString();
    }
}
