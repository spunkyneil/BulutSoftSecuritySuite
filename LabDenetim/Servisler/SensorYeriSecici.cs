// "Gelişmiş" paneldeki "Tarama yeri" seçimi (Bu bilgisayar / kayıtlı uzak sensörler). Eskiden Form1'in SensorTarama.cs partial dosyasındaydı; sensör yardımcıları (SensorTaramaYardimcisi) aynı dosyada kalır.
using System.Net;

// Programımızın adı.
namespace LabDenetim;

// Tarama yeri seçici.
internal sealed class SensorYeriSecici
{
    private readonly TableLayoutPanel _tblGel; private readonly ToolTip _ipucu; private ComboBox _cmbYer = null!; private List<SensorProfili> _sensorler = new();

    // tblGel: "Gelişmiş" paneli (boş hücrelere yerleşir); ipucu: fare açıklaması.
    public SensorYeriSecici(TableLayoutPanel tblGel, ToolTip ipucu) { _tblGel = tblGel; _ipucu = ipucu; }

    // "Gelişmiş" panelindeki boş hücrelere "Tarama yeri" etiketi ve seçim kutusunu koyar.
    public void Kur()
    {
        // Etiket.
        var et = new Label { Text = "Tarama yeri:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) };
        // Seçim kutusu.
        _cmbYer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 6, 16, 6), FlatStyle = FlatStyle.Flat, BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput), ForeColor = Color.White };
        // Açıklama.
        _ipucu.SetToolTip(_cmbYer, "Kapı taramasının nereden yapılacağını seçer.\r\n\r\nBu bilgisayar: eskisi gibi, bu Windows'tan yapılır (varsayılan).\r\nSensör adı: tarama ayrı sanal makineden (Kali) şifreli SSH ile yapılır. Sensörün erişebildiği özel bir hedef IP girin (örn. 192.168.56.1); 127.0.0.1 sensörün kendisidir.\r\n\r\nSensörleri sol menüdeki \"Uzak Sensörler\"den eklersiniz."); _ipucu.SetToolTip(et, _ipucu.GetToolTip(_cmbYer));
        // Yerleştir.
        _tblGel.Controls.Add(et, 2, 3); _tblGel.Controls.Add(_cmbYer, 3, 3);
        // Listeyi doldur.
        Yenile();
    }

    // Seçim kutusunu kayıtlı sensörlerle yeniler (önceki seçim korunur).
    public void Yenile()
    {
        // Önceki seçim.
        string? onceki = Secilen()?.Ad;
        // Yeniden doldur.
        _sensorler = SensorDeposu.Yukle(); _cmbYer.Items.Clear(); _cmbYer.Items.Add("Bu bilgisayar"); foreach (var s in _sensorler) _cmbYer.Items.Add("Sensör: " + s.Ad);
        // Seçimi geri koy.
        int i = onceki == null ? 0 : _sensorler.FindIndex(x => x.Ad == onceki) + 1; _cmbYer.SelectedIndex = Math.Max(0, i);
    }

    // Seçili sensör (yoksa null: bu bilgisayar).
    public SensorProfili? Secilen() => _cmbYer == null || _cmbYer.SelectedIndex <= 0 || _cmbYer.SelectedIndex > _sensorler.Count ? null : _sensorler[_cmbYer.SelectedIndex - 1];

    // Sensör seçiliyken hedef uygun mu? Uygun değilse nedenini söyleyen hata atar.
    public void HedefiDogrula(IPAddress ip)
    {
        // Sensör yoksa kontrol gerekmez.
        var s = Secilen(); if (s == null) return;
        // 127.0.0.1 sensörün kendisidir; kullanıcı Windows'u taradığını sanıp yanılmasın.
        if (IPAddress.IsLoopback(ip)) throw new InvalidOperationException($"Tarama yeri \"{s.Ad}\" sensörü seçili, ama hedef {ip}. Bu adres sensörün KENDİSİDİR, Windows değil.\r\n\r\nWindows'u sensörden taramak için hedef olarak sensörün erişebildiği adresi yazın (örn. VirtualBox host-only: 192.168.56.1), ya da Tarama yeri olarak \"Bu bilgisayar\"ı seçin.");
        // Hedef IPv4 olmalı (ajan yalnızca IPv4 yoklar).
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) throw new InvalidOperationException("Sensör taraması şimdilik yalnızca IPv4 hedeflerini destekler.");
    }
}
