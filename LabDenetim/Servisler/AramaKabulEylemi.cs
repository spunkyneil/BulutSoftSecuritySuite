// Sonuç tablosundaki arama kutusu ve sağ tık menüsündeki "kabul edilen risk" işlemi. Eskiden Form1'in AramaKabul.cs partial dosyasındaydı; kayıt deposu (KabulDeposu) aynı dosyada kalır.

// Programımızın adı.
namespace LabDenetim;

// Arama ve kabul edilen risk eylemleri.
internal sealed class AramaKabulEylemi
{
    private readonly ITaramaBaglami _b; private readonly TextBox _txtAra; private readonly DataGridView _dgv; private readonly ContextMenuStrip _ctxMenu;
    private readonly Action _filtreyiUygula; private readonly Func<Bulgu, bool> _filtreyeUyar;
    // Arama kutusundaki metin ve sağ tık menüsündeki satır.
    private string _arama = ""; private ToolStripMenuItem _miKabul = null!;

    // filtreyiUygula: renk filtresi/arama değişince tabloyu yeniden süzer; filtreyeUyar: bulgu geçerli renk filtresine uyuyor mu.
    public AramaKabulEylemi(ITaramaBaglami baglam, TextBox txtAra, DataGridView dgv, ContextMenuStrip ctxMenu, Action filtreyiUygula, Func<Bulgu, bool> filtreyeUyar)
    { _b = baglam; _txtAra = txtAra; _dgv = dgv; _ctxMenu = ctxMenu; _filtreyiUygula = filtreyiUygula; _filtreyeUyar = filtreyeUyar; }

    // Arama kutusunu ve sağ tık menüsünü bağlar. Form1 açılırken bir kez çağrılır.
    public void Kur()
    {
        // Yazdıkça süz.
        _txtAra.TextChanged += (_, _) => { _arama = _txtAra.Text.Trim(); _filtreyiUygula(); };
        // Sağ tık menüsüne yeni satır ekle.
        _miKabul = new ToolStripMenuItem("Kabul edilen risk olarak işaretle / işareti kaldır");
        // Tıklanınca.
        _miKabul.Click += (_, _) => KabulDegistir();
        // Menüye ekle.
        _ctxMenu.Items.Add(new ToolStripSeparator()); _ctxMenu.Items.Add(_miKabul);
    }

    // Satır hem renk filtresine hem arama metnine uyuyor mu?
    public bool SatirGorunur(Bulgu b) =>
        _filtreyeUyar(b) && (_arama.Length == 0 || (b.Modul + " " + b.Baslik + " " + b.Detay + " " + b.Oneri).Contains(_arama, StringComparison.OrdinalIgnoreCase));

    // Seçili satırı kabul edilen risk olarak işaretler veya işareti kaldırır.
    private void KabulDegistir()
    {
        // Seçili sonuç yoksa uyar.
        if (_dgv.SelectedRows.Count == 0 || _dgv.SelectedRows[0].Tag is not Bulgu b) { TemaliMesaj.Goster("Önce tablodan bir satıra tıklayın.", "Bir eksik var", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Üretilmiş özet uyarıları işaretlenemez.
        if (b.Modul is "Zincir" or "Referans" or "Trafik") { TemaliMesaj.Goster("Bu tür uyarılar kabul edilen risk olarak işaretlenemez; asıl nedeni olan satırı işaretleyin.", "İşaretlenemez", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Zaten Bilgi ve kabul edilmemişse işaretlemeye gerek yok.
        bool kabulduGercek = KabulDeposu.Kabul(b);
        // Bilgi seviyesindeki normal sonuç işaretlenemez.
        if (!kabulduGercek && b.Onem == Onem.Bilgi) { TemaliMesaj.Goster("Bu zaten sorun olmayan (yeşil) bir sonuç.", "Gerek yok", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        // Onay iste (varsayılan Hayır) — yalnızca işaretlerken.
        if (!kabulduGercek && TemaliMesaj.Goster($"\"{b.Baslik}\" sonucu KABUL EDİLEN RİSK olarak işaretlensin mi?\r\n\r\nSonraki taramalarda bu sonuç sorun sayılmaz ve puanınızı düşürmez. Bunu yalnızca nedenini bilerek açık bıraktığınız şeyler için yapın.", "Kabul Edilen Risk", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        // Değiştir ve kaydet.
        bool simdi = KabulDeposu.Degistir(b);
        // Bildir.
        _b.Durum(simdi ? $"Kabul edilen risk olarak işaretlendi: {b.Baslik}. Bir sonraki taramadan itibaren geçerli olur." : $"İşaret kaldırıldı: {b.Baslik}. Bir sonraki taramada normal şekilde değerlendirilir.");
        // Olay zaman çizgisine yaz.
        ServisBaglami.OlayYazici.Ekle("Referans", "Bilgi", (simdi ? "Kabul edilen risk: " : "Kabul işareti kaldırıldı: ") + b.Baslik);
    }
}
