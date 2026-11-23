// Üst çubuktaki "Hedef Seç / İç Ağ Keşfi" menüsü: yerel bilgisayarı seçer ya da ağ keşif penceresinden bir cihazın IP'sini adres kutusuna yazar. Eskiden Form1'in HedefSec.cs partial dosyasındaydı.

// Programımızın adı.
namespace LabDenetim;

// Hedef seçme eylemi.
internal sealed class HedefSeciciEylemi
{
    private readonly ITaramaBaglami _b; private readonly ContextMenuStrip _menu; private readonly Control _dugme;

    public HedefSeciciEylemi(ITaramaBaglami baglam, ContextMenuStrip menu, Control dugme) { _b = baglam; _menu = menu; _dugme = dugme; }

    // Düğmeye basılınca menüyü düğmenin hemen altında aç.
    public void Ac() => _menu.Show(_dugme, new Point(0, _dugme.Height));

    // Menü: "Yerel Bilgisayarım" — adres kutusuna 127.0.0.1 yaz.
    public void Yerel()
    {
        // Adres kutusunu doldur.
        _b.Hedef = "127.0.0.1";
        // Durum çubuğuna ve günlüğe bilgi ver.
        _b.Durum("Hedef: Yerel bilgisayar (127.0.0.1) seçildi.");
    }

    // Menü: ağ keşif penceresini açar; seçilen cihazın IP'sini adres kutusuna yazar.
    public void Ag()
    {
        // Ağ tarama penceresini yarat.
        using var pencere = new AgFormu();
        // Pencereyi aç ve kullanıcı bir cihaz seçip "Seç" dediyse devam et.
        if (pencere.ShowDialog(_b.Sahip) == DialogResult.OK && !string.IsNullOrWhiteSpace(pencere.SecilenIp))
        {
            // Seçilen IP'yi adres kutusuna yaz.
            _b.Hedef = pencere.SecilenIp;
            // Durum çubuğuna ve günlüğe bilgi ver.
            _b.Durum($"Hedef: {pencere.SecilenIp} seçildi (ağ taramasından).");
        }
    }
}
