// Bu dosya "◈ Canlı Ağ Trafiği" görünümünü çalıştırır: bilgisayardan dışarıya yapılan canlı bağlantılar bir tabloda
// her 2 saniyede yenilenir; bir satıra tıklayınca sağdaki Asistan sade Türkçe ile ne olduğunu anlatır.
using System.Text.RegularExpressions;
using LabDenetim.Moduller;

// Programımızın adı.
namespace LabDenetim;

// Form1'in on altıncı parçası: canlı ağ trafiği.
internal sealed partial class TaramaSayfasi
{
    // Tabloyu her 2 saniyede bir yenileyen zamanlayıcı (yalnızca bu görünüm açıkken çalışır).
    internal readonly GorunurZamanlayici _trafikZaman = new() { Interval = 2000 };   // görsel tablo yenilemesi: trafik sayfası görünmüyorsa/simge durumundaysa durur
    // Şu an okuma sürüyor mu? (üst üste okuma başlamasın)
    internal bool _trafikOkunuyor;
    // Her bağlantının anahtarı → tablodaki satırı (yalnızca değişenler güncellenir, tablo titremez).
    internal readonly Dictionary<string, DataGridViewRow> _trafikSatirlari = new();

    // Trafik tablosunu ve zamanlayıcıyı hazırlar. Form1 açılırken bir kez çağrılır.
    internal void TrafikKur()
    {
        // Tablonun renklerini ve sütunlarını kur.
        TrafikTabloKur();
        // Her tıkta bağlantıları yeniden oku.
        _trafikZaman.Sahip = this; _trafikZaman.Tick += (_, _) => GuvenliCalistir.Baslat(() => TrafikYenileAsync(), "Trafik");
        // Satır seçilince asistan o bağlantıyı anlatsın.
        dgvTrafik.SelectionChanged += dgvTrafik_SelectionChanged;
        // İlk sütundaki rozetleri de kendimiz çizelim (tehlike tablosuyla aynı çizim kodu).
        dgvTrafik.CellPainting += dgvBulgular_CellPainting;
    }

    // Trafik tablosunu koyu siber temaya boyar (sonuç tablosuyla aynı görünüm).
    internal void TrafikTabloKur()
    {
        // Çift tamponlu çizim (titreme olmasın).
        typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(dgvTrafik, true);
        // Genel ayarlar: değiştirilemez, tek satır seçilir, satır başlığı yok.
        dgvTrafik.ReadOnly = true; dgvTrafik.AllowUserToAddRows = false; dgvTrafik.RowHeadersVisible = false; dgvTrafik.MultiSelect = false; dgvTrafik.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        // Koyu zemin, ince yatay çizgi, çerçevesiz.
        dgvTrafik.BackgroundColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); dgvTrafik.GridColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); dgvTrafik.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal; dgvTrafik.BorderStyle = BorderStyle.None;
        // Başlık satırı: koyu kurumsal zemin, açık mavi kalın yazı.
        dgvTrafik.EnableHeadersVisualStyles = false; dgvTrafik.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None; dgvTrafik.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; dgvTrafik.ColumnHeadersHeight = 36;
        // Başlık renkleri ve yazısı.
        dgvTrafik.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); dgvTrafik.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.StatusInfo); dgvTrafik.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated);
        // Başlık yazı tipi ve iç boşluk.
        dgvTrafik.ColumnHeadersDefaultCellStyle.Font = FontProvider.Al(FontRol.Caption, FontStyle.Bold); dgvTrafik.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0); dgvTrafik.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        // Hücreler: koyu zemin, açık yazı, iç boşluk.
        dgvTrafik.DefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceCard); dgvTrafik.DefaultCellStyle.ForeColor = DesignTokens.Ozgun(TokenRol.TextPrimary); dgvTrafik.DefaultCellStyle.Font = FontProvider.Al(FontRol.Body); dgvTrafik.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
        // Seçili satır: koyu siber mavi, beyaz yazı.
        dgvTrafik.DefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); dgvTrafik.DefaultCellStyle.SelectionForeColor = Color.White; dgvTrafik.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        // Aralıklı satır rengi.
        dgvTrafik.AlternatingRowsDefaultCellStyle.BackColor = DesignTokens.Ozgun(TokenRol.SurfaceInput); dgvTrafik.AlternatingRowsDefaultCellStyle.SelectionBackColor = DesignTokens.Ozgun(TokenRol.SurfaceElevated); dgvTrafik.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
        // Satır yüksekliği.
        dgvTrafik.RowTemplate.MinimumHeight = 40; dgvTrafik.RowTemplate.Height = 40; dgvTrafik.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        // Sütunlar.
        dgvTrafik.Columns.Add("Isik", "Tehlike Seviyesi"); dgvTrafik.Columns.Add("Uygulama", "Uygulama (PID)"); dgvTrafik.Columns.Add("Hedef", "Yerel Port → Hedef (IP : Port)"); dgvTrafik.Columns.Add("Durum", "Durum"); dgvTrafik.Columns.Add("Not", "Ne Anlama Geliyor?");
        // Sütun genişlikleri: ilk sütun sabit, diğerleri oransal.
        dgvTrafik.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; dgvTrafik.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.None; dgvTrafik.Columns[0].Width = 146; dgvTrafik.Columns[0].Resizable = DataGridViewTriState.False;
        // Diğer sütunların oranları ve en az genişlikleri.
        dgvTrafik.Columns[1].FillWeight = 110; dgvTrafik.Columns[1].MinimumWidth = 150; dgvTrafik.Columns[2].FillWeight = 130; dgvTrafik.Columns[2].MinimumWidth = 200; dgvTrafik.Columns[3].FillWeight = 50; dgvTrafik.Columns[3].MinimumWidth = 90; dgvTrafik.Columns[4].FillWeight = 130; dgvTrafik.Columns[4].MinimumWidth = 170;
        // Kaydırma çubukları koyu olsun.
        KoyuTema.UygulaTablo(dgvTrafik);
    }

    // Trafik görünümü açılınca zamanlayıcıyı başlatır, kapanınca durdurur.
    internal void TrafikZamanla(bool ac)
    {
        // Açılıyorsa hemen bir okuma yap ve zamanlayıcıyı başlat.
        if (ac) { _ = TrafikYenileAsync(); _trafikZaman.Start(); }
        // Kapanıyorsa durdur.
        else _trafikZaman.Stop();
    }

    // Canlı bağlantıları arka planda okuyup tabloyu günceller.
    internal async Task TrafikYenileAsync()
    {
        // Görünüm kapalıysa veya zaten okunuyorsa bir şey yapma.
        if (_trafikOkunuyor || !dgvTrafik.Visible) return;
        // Okuma başladı.
        _trafikOkunuyor = true;
        // Hata olsa da program bozulmasın.
        try
        {
            // Bağlantıları arka planda oku (ekran donmasın).
            var liste = await Task.Run(TrafikIzleyici.Oku);
            // Bu arada görünüm kapandıysa yazma.
            if (IsDisposed || !dgvTrafik.Visible) return;
            // Tabloya uygula.
            TrafikUygula(liste);
        }
        // Okuma hatası sessizce geçilir.
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "Trafik.TrafikYenileAsync"); }
        // Okuma bitti.
        finally { _trafikOkunuyor = false; }
    }

    // Okunan listeyi tabloya uygular: yeni bağlantıları ekler, kapananları siler, durumu değişenleri günceller.
    internal void TrafikUygula(List<TrafikBaglantisi> liste)
    {
        // Şu an var olan bağlantıların anahtarları.
        var simdi = new HashSet<string>();
        // Her bağlantı için.
        foreach (var t in liste)
        {
            // Bağlantıyı tanıyan anahtar.
            string anahtar = $"{t.Pid}|{t.YerelPort}|{t.UzakIp}|{t.UzakPort}";
            // Listede var say.
            simdi.Add(anahtar);
            // Tabloda zaten varsa sadece durumunu güncelle.
            if (_trafikSatirlari.TryGetValue(anahtar, out var mevcut)) { if ((mevcut.Cells[3].Value as string) != t.Durum) mevcut.Cells[3].Value = t.Durum; continue; }
            // Yeni bağlantı için asistanın kullanacağı sonuç kartı.
            var b = TrafikBulgusu(t);
            // Tabloya yeni satır ekle.
            int i = dgvTrafik.Rows.Add(Rozet(b).Metin, $"{t.Program} (PID {t.Pid})", $"{t.YerelPort}  →  {t.UzakIp}:{t.UzakPort}" + (t.DisAg ? "" : "  (yerel ağ)"), t.Durum, t.Not);
            // Satırın içine sonucu sakla.
            dgvTrafik.Rows[i].Tag = b; _trafikSatirlari[anahtar] = dgvTrafik.Rows[i];
        }
        // Kapanan bağlantıların satırlarını sil.
        foreach (var eski in _trafikSatirlari.Keys.Where(k => !simdi.Contains(k)).ToList()) { dgvTrafik.Rows.Remove(_trafikSatirlari[eski]); _trafikSatirlari.Remove(eski); }
        // Durum çubuğuna özet yaz (günlüğü kalabalık etmemek için doğrudan yazı).
        int dikkat = liste.Count(x => x.Seviye != Onem.Bilgi);
        // Özet.
        Sahibi.DurumYaz($"Canlı Ağ Trafiği: {liste.Count} bağlantı, {dikkat} tanesi dikkat gerektiriyor · her 2 saniyede yenilenir · bir satıra tıklayın.");
    }

    // Bir bağlantıdan asistanın anlatacağı sonuç kartı üretir.
    internal static Bulgu TrafikBulgusu(TrafikBaglantisi t) => new(
        // Hangi kontrol? Trafik izleyici.
        "Trafik",
        // Başlık: program ve hedef.
        $"{t.Program} → {t.UzakIp}:{t.UzakPort}",
        // Önem derecesi.
        t.Seviye,
        // Ayrıntı: program, portlar, durum, hedef türü ve dosya yolu.
        $"Uygulama: {t.Program} (PID {t.Pid})\nYerel port {t.YerelPort} → {t.UzakIp}:{t.UzakPort}\nDurum: {t.Durum} · Hedef: {(t.DisAg ? "internet (dış ağ)" : "yerel ağ")}" + (t.Yol.Length > 0 ? $"\nDosya yolu: {t.Yol}" : ""),
        // Öneri.
        t.Not);

    // Bir trafik bağlantısı için sade açıklama, çözüm adımları ve hazır inceleme komutları üretir.
    internal Cozum TrafikCozumu(Bulgu b)
    {
        // Başlıktan hedef adresi ve portu çıkar ("program → 1.2.3.4:443").
        var m = Regex.Match(b.Baslik, @"→ (?<ip>.+):(?<port>\d+)$");
        // Hedef IP ve port.
        string ip = m.Success ? m.Groups["ip"].Value : "?"; int port = m.Success ? int.Parse(m.Groups["port"].Value) : 0;
        // Ayrıntıdan PID'yi çıkar.
        var pm = Regex.Match(b.Detay, @"PID (\d+)"); string pid = pm.Success ? pm.Groups[1].Value : "0";
        // Hedef yerel ağda mı?
        bool yerelAg = b.Detay.Contains("Hedef: yerel ağ");
        // Hedefin türünü anlatan cümle.
        string hedefTuru = yerelAg ? "Hedef, kendi yerel ağınızdaki bir cihaz." : "Hedef, internetteki (dış ağdaki) bir sunucu.";
        // Port türüne göre sade açıklama.
        string neDemek = (port switch
        {
            // Şifreli web.
            443 or 8443 => "Bu program, karşıdaki bir sunucuyla ŞİFRELİ (HTTPS) konuşuyor. Araya giren biri içeriği okuyamaz (mektubu kilitli bir zarfa koymak gibi). Tarayıcılar, güncellemeler ve çoğu uygulama böyle çalışır; tanıdığınız bir programsa bu normaldir.",
            // Şifresiz web.
            80 or 8080 or 8000 => "Bu program, karşıdaki sunucuyla ŞİFRESİZ (HTTP) konuşuyor. Yazılanları ağ üzerinde aradaki biri okuyabilir (açık kartpostal gibi). Önemli bilgi taşımıyorsa sorun değildir; parola veya kişisel bilgi taşıyorsa şifreli (https) sürümü kullanılmalıdır.",
            // SSH.
            22 => "Bu program, karşıdaki makineye şifreli uzaktan bağlantı (SSH) kuruyor. Genelde güvenlidir; bağlantıyı siz veya bir yönetici başlattıysa normaldir.",
            // Şifresiz eski hizmetler.
            21 or 23 or 25 or 110 or 143 => "Bu program eski ve ŞİFRESİZ bir hizmetle (FTP, Telnet veya e-posta) konuşuyor. Yazılanlar, parola dahil, aradaki biri tarafından okunabilir.",
            // Dosya paylaşımı ve uzak masaüstü.
            445 or 139 or 3389 or 5900 => yerelAg ? "Bu program, yerel ağdaki bir cihazla dosya paylaşımı veya uzak masaüstü bağlantısı kuruyor. Ev/ofis ağında olağan olabilir." : "Bu program, internetteki bir adrese dosya paylaşımı veya uzak masaüstü bağlantısı kuruyor. Bu normalde yalnızca yerel ağda olur; tanımadığınız bir adresse şüphelidir.",
            // İsim sorgusu.
            53 => "Bu bağlantı isim çözümleme (DNS) içindir: bir site adını numaraya çevirmek için (telefon rehberine bakmak gibi). Normaldir.",
            // Bilinen şüpheli portlar.
            4444 or 1337 or 31337 or 6667 or 12345 or 5555 or 6666 or 9999 => $"Bu port ({port}) bazı zararlı yazılımların ve izinsiz uzaktan erişim araçlarının sık kullandığı bir port. Bağlantıyı kuran programı tanımıyorsanız mutlaka araştırın.",
            // Diğer portlar.
            _ => $"Bu program, alışılmadık bir porta ({port}) bağlanıyor. Oyunlar, görüntülü konuşma, dosya paylaşım ve yedekleme programları gibi tanıdığınız uygulamalar bunu yapabilir; tanımıyorsanız araştırın."
        }) + " " + hedefTuru;
        // Çözüm adımları.
        string nasil = "Programı tanıyorsanız (tarayıcı, güncelleme, oyun vb.) bir şey yapmanız gerekmez. Tanımıyorsanız önce yukarıdaki dosya yoluna bakın; şüpheliyse programı kapatıp virüs taraması yapın. İsterseniz alttaki komutla bu hedefe GİDEN bağlantıları güvenlik duvarında engelleyebilirsiniz.";
        // Komut için IP'nin yalnızca güvenli karakterler içerdiğinden emin ol.
        bool ipGuvenli = Regex.IsMatch(ip, @"^[0-9a-fA-F\.:]+$");
        // Hazır komutlar: inceleme + isteğe bağlı giden bağlantı engelleme + geri alma.
        string komut = $"# Bu programın bağlantılarını görmek için:\r\nGet-NetTCPConnection -OwningProcess {pid} | Format-Table -AutoSize" + (ipGuvenli ? $"\r\n\r\n# Bu hedefe giden bağlantıları engellemek için (isteğe bağlı):\r\nnetsh advfirewall firewall add rule name=\"BulutSoft-Blok-Cikis-{ip}\" dir=out action=block remoteip={ip}\r\n\r\n# Geri almak için:\r\n# netsh advfirewall firewall delete rule name=\"BulutSoft-Blok-Cikis-{ip}\"" : "");
        // Yardım paketini ver (Otomatik Onar bu bağlantı için kapalıdır).
        return new(neDemek, nasil, komut, "İNCELEME VE ENGELLEME KOMUTLARI · Windows (Yönetici PowerShell)", null);
    }

    // Trafik tablosunda satır seçilince asistanı günceller.
    internal void dgvTrafik_SelectionChanged(object? sender, EventArgs e)
    {
        // Claude analizi sürerken kutunun içeriği değişmesin.
        if (_analizIptal != null) return;
        // Seçili satır yoksa asistanı başlangıç haline getir.
        if (dgvTrafik.SelectedRows.Count == 0 || dgvTrafik.SelectedRows[0].Tag is not Bulgu b) { AsistanSifirla(); return; }
        // Seçili bağlantı için yardım paketini göster.
        AsistanGoster(b);
    }
}
