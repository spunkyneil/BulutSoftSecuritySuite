// Zincirleme risk analizi ve tablonun üstündeki uyarı şeridi. Eskiden Form1'in Zincir.cs partial dosyasındaydı; analiz mantığı (ZincirAnalizi) aynı dosyada kalır.

// Programımızın adı.
namespace LabDenetim;

// Zincir/referans uyarı şeridi eylemleri.
internal sealed class ZincirEylemi
{
    private readonly ITaramaBaglami _b; private readonly Label _serit; private readonly Control _kap; private readonly Func<bool> _trafikGorunumu;

    // serit: uyarı şeridi etiketi; kap: tablo kabı (genişlik ölçümü); trafikGorunumu: şu an canlı trafik görünümü mü (şerit orada gizlenir).
    public ZincirEylemi(ITaramaBaglami baglam, Label serit, Control kap, Func<bool> trafikGorunumu) { _b = baglam; _serit = serit; _kap = kap; _trafikGorunumu = trafikGorunumu; }

    // Tarama bittiğinde çağrılır: zincirleme riskleri bulur, tabloya ve günlüğe yazar.
    public void Analiz()
    {
        // Zincirleri üret.
        var zincirler = ZincirAnalizi.Analiz(_b.Bulgular);
        // Her birini tabloya ekle (tabloya eklenince günlüğe de düşer).
        foreach (var z in zincirler) _b.BulguEkle(z);
        // Bilgi ver.
        if (zincirler.Count > 0) _b.Gunluk($"ZİNCİRLEME ANALİZ -> {zincirler.Count} zincirleme risk bulundu (tablonun üstündeki kırmızı şeride bakın).", TaramaRenkleri.Kirmizi);
        // Hiç yoksa sakin bir satır yaz.
        else _b.Gunluk("ZİNCİRLEME ANALİZ -> Birbirine bağlanıp tehlike oluşturan açık bulunmadı.", TaramaRenkleri.Yesil);
    }

    // Tablonun üstündeki uyarı şeridini, ekrandaki sonuçlardaki zincir ve referans uyarılarıyla günceller.
    public void SeritYenile()
    {
        // Şerit henüz kurulmadıysa çık.
        if (_serit == null) return;
        // Şeritte gösterilecek satırlar: zincir ve altın profil uyarıları.
        var satirlar = new List<string>();
        // Zincirleme risk uyarıları.
        foreach (var b in _b.Bulgular.Where(x => x.Modul == "Zincir")) satirlar.Add(b.Baslik + ": " + b.Detay.Split('\n')[0]);
        // Altın profil uyarıları.
        foreach (var b in _b.Bulgular.Where(x => x.Modul == "Referans" && x.Onem != Onem.Bilgi)) satirlar.Add("⚿ " + b.Baslik);
        // Hiç uyarı yoksa şeridi gizle.
        if (satirlar.Count == 0) { _serit.Visible = false; return; }
        // Metni yaz.
        _serit.Text = string.Join("\r\n", satirlar);
        // Yazı sığsın diye yüksekliği ölç (genişlik: kabın içi).
        int genislik = Math.Max(200, _kap.ClientSize.Width - _serit.Padding.Horizontal - 4);
        // Ölçülen yüksekliğe iç boşluk ekle.
        _serit.Height = TextRenderer.MeasureText(_serit.Text, _serit.Font, new Size(genislik, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + _serit.Padding.Vertical;
        // Şeridi, trafik görünümü dışında göster.
        _serit.Visible = !_trafikGorunumu();
    }
}
