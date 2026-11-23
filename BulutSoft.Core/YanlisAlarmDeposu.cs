// Bu dosya yanlış alarm takibini KALICI yapar: hangi kural kaç benzersiz bulgu üretti, kaçı yanlış alarm olarak işaretlendi, hangi bulgular işaretli.
// Amaç: yanlış alarm oranı yüksek kural "gözden geçir" etiketi alır (kapatılmaz) ve gözetimsiz otomatik politikadan çıkar (UnattendedPolicy bu takipçiyi kullanır).
// Oran = yanlış alarm sayısı / benzersiz bulgu sayısı. Aynı bulgu her taramada yeniden sayılmaz (kararlı kimlikle tekilleştirilir).
using System.Text.Json;

// V2 ad alanı.
namespace LabDenetim.V2;

// Bir bulgunun yanlış alarm işareti.
internal sealed record FpIsareti(string RuleId, string Kullanici, DateTime Zaman, string Neden);

// Kalıcı yanlış alarm deposu.
internal sealed class FpDeposu
{
    // Kural başına görülen benzersiz bulgu kimlikleri (en çok 5000/kural: bellek ve dosya şişmesin).
    public Dictionary<string, List<string>> Gorulen { get; set; } = new();
    // Bulgu kimliği → işaret.
    public Dictionary<string, FpIsareti> Isaretler { get; set; } = new();
    public const int KuralBasinaEnCok = 5000;

    public static string Yol => BulutKlasor.Dosya("fp_takip.json");
    private static readonly JsonSerializerOptions Sec = new() { WriteIndented = true };

    // Yükler (yoksa/bozuksa boş depo).
    public static FpDeposu Yukle() { try { return File.Exists(Yol) ? JsonSerializer.Deserialize<FpDeposu>(File.ReadAllText(Yol), Sec) ?? new() : new(); } catch { return new(); } }
    // Atomik yazar.
    public void Kaydet() { Directory.CreateDirectory(BulutKlasor.Kok); Depo.Yaz(Yol, JsonSerializer.Serialize(this, Sec)); }

    // Görülen bulguları sayar (yalnızca yeni kimlikler eklenir).
    public void Gor(IEnumerable<Finding> bulgular)
    {
        foreach (var f in bulgular)
        {
            if (!Gorulen.TryGetValue(f.RuleId, out var l)) Gorulen[f.RuleId] = l = new();
            if (l.Count < KuralBasinaEnCok && !l.Contains(f.Id)) l.Add(f.Id);
        }
    }

    // Yanlış alarm olarak işaretler (ilk işaretlemede true).
    public bool Isaretle(Finding f, string kullanici, string neden, DateTime simdi)
    {
        Gor(new[] { f }); if (Isaretler.ContainsKey(f.Id)) return false;
        Isaretler[f.Id] = new FpIsareti(f.RuleId, string.IsNullOrWhiteSpace(kullanici) ? "bilinmiyor" : kullanici, simdi, (neden ?? "").Length > 300 ? neden![..300] : neden ?? ""); return true;
    }
    // İşareti geri alır.
    public bool Geri(string bulguId) => Isaretler.Remove(bulguId);
    public bool Isaretli(string bulguId) => Isaretler.ContainsKey(bulguId);

    // Kural için (toplam benzersiz bulgu, yanlış alarm sayısı).
    public (int Toplam, int Fp) Say(string ruleId) => (Gorulen.TryGetValue(ruleId, out var l) ? l.Count : 0, Isaretler.Values.Count(x => x.RuleId == ruleId));

    // Politika için bellek içi takipçi (eşik: %30, en az 10 örnek).
    public FalsePositiveTracker Takipci(double esik = 0.3, int enAzOrnek = 10)
    {
        var t = new FalsePositiveTracker(esik, enAzOrnek);
        foreach (var r in Gorulen.Keys.Concat(Isaretler.Values.Select(x => x.RuleId)).Distinct()) { var (top, fp) = Say(r); t.Ayarla(r, Math.Max(top, fp), fp); }
        return t;
    }

    // Oranı yüksek kuralın işaretsiz bulgularında etki bu çarpanla düşürülür (kural KAPATILMAZ; yalnızca puan azaltılır ve uyarı eklenir).
    public const double AzaltmaCarpani = 0.75; public const string AzaltmaNotu = "[Kalibrasyon] Bu kuralın yanlış alarm oranı yüksek; puan %25 düşürüldü.";

    // Bulgulara yanlış alarm bilgisini işler: işaretliler "FalsePositive" olur; oranı yüksek kuralların işaretsiz bulguları "Suspected" (gözden geçir) alır ve puanı düşer.
    // Birden çok kez uygulanırsa aynı sonucu verir (puan iki kez düşürülmez): uyarı notu işaretçi olarak kullanılır.
    public List<Finding> Uygula(IEnumerable<Finding> bulgular)
    {
        var tk = Takipci();
        return bulgular.Select(f =>
        {
            var (top, fp) = Say(f.RuleId); bool isaretli = Isaretler.TryGetValue(f.Id, out var iz); bool gozden = tk.GozdenGecir(f.RuleId);
            var durum = isaretli ? FpState.Confirmed : gozden ? FpState.Suspected : FpState.None;
            var izle = new FalsePositiveTracking(durum, f.RuleId, iz?.Kullanici, iz?.Zaman, iz?.Neden, fp, Math.Max(top, fp));
            var s = f with { FalsePositiveTracking = izle, State = isaretli ? FindingState.FalsePositive : f.State == FindingState.FalsePositive ? FindingState.Open : f.State };
            // Puan düşürme (yalnızca işaretsiz + gözden geçirilecek kural + henüz düşürülmemiş).
            if (!isaretli && gozden && !s.Evidence.Contains(AzaltmaNotu))
            { double yeni = Math.Round(s.ImpactWeight * AzaltmaCarpani, 1); s = s with { ImpactWeight = yeni, Score = Finding.HesaplaSkor(yeni, s.AssetWeight), Evidence = s.Evidence.Append(AzaltmaNotu).ToArray() }; }
            return s;
        }).ToList();
    }

    // Kural kalitesi satırları (kalibrasyon raporu): kural, benzersiz bulgu, yanlış alarm, oran, durum. Orana göre azalan.
    public List<(string Kural, int Toplam, int Fp, double Oran, string Durum)> KuralKalitesi()
    {
        var tk = Takipci();
        return Gorulen.Keys.Concat(Isaretler.Values.Select(x => x.RuleId)).Distinct().Select(r => { var (t, f) = Say(r); int top = Math.Max(t, f); double oran = top == 0 ? 0 : (double)f / top; return (r, top, f, oran, tk.GozdenGecir(r) ? "Gözden geçirilmeli" : top < tk.EnAzOrnek ? "Yetersiz örnek" : "İyi"); }).OrderByDescending(x => x.Item4).ThenByDescending(x => x.Item2).ToList();
    }
}
