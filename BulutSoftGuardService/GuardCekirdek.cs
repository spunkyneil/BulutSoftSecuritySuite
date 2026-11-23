// Hizmetin çekirdeği: ayarlar, Nöbetçi yoklaması, fidye izleyici, canary dosyaları, mühür kaydı, paket imzası ve SSH parmak izi kaydı.
// Tüm olaylar zincirli olay günlüğüne (Guard\olaylar.jsonl) yazılır; arayüz kapalıyken de kayıt sürer.
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using LabDenetim;
using LabDenetim.Moduller;
using Microsoft.Win32;

namespace BulutSoftGuardService;

internal sealed record GuardKonfig(bool Nobet = true, bool Fidye = true, bool Canary = false);

internal sealed class GuardCekirdek : IDisposable
{
    // Hızlı kontrolde bakılan kapılar (arayüzdeki Nöbetçi ile aynı liste).
    private const string NobetPortlari = "21,22,23,25,53,80,110,135,139,143,443,445,3306,3389,5432,5900,8080,8443";
    private readonly GuardAyarlari _a; private readonly object _kilit = new(); private GuardKonfig _konfig = new();
    private FidyeIzleyici? _fidye; private readonly List<(CanaryDosyalari Canary, string Klasor)> _canaryler = new();
    public DateTime? SonYoklama { get; private set; } public int AcikKapiSayisi { get; private set; } public string KonfigYolu => Path.Combine(_a.Klasor, "ayar.json");
    public YazmaIzleyici? Etw { get; set; }

    public GuardCekirdek(GuardAyarlari a)
    {
        _a = a; Directory.CreateDirectory(a.Klasor); if (!a.Gelistirme) KlasoruKoru(a.Klasor);
        // Motorların ortak kök klasörü ve olay günlüğü Guard klasörüdür (SYSTEM'in kendi profili değil).
        BulutKlasor.Kok = a.Klasor; OlayDeposu.Yol = Path.Combine(a.Klasor, "olaylar.jsonl"); OlayDeposu.AynaAktif = !a.Gelistirme;
        try { _konfig = Depo.Yukle<GuardKonfig>(KonfigYolu) ?? new(); } catch { _konfig = new(); }
    }

    // Guard klasörü: SYSTEM ve Yöneticiler tam yetkili; kullanıcılar YALNIZCA OKUR (arayüz olay günlüğünü okuyabilsin, değiştiremesin).
    private static void KlasoruKoru(string yol)
    {
        try
        {
            var g = new DirectorySecurity(); g.SetAccessRuleProtection(true, false);
            var inh = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            g.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inh, PropagationFlags.None, AccessControlType.Allow));
            g.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inh, PropagationFlags.None, AccessControlType.Allow));
            g.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inh, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(yol).SetAccessControl(g);
        }
        catch (Exception hk1) { HataKayit.Sessiz(hk1, "GuardCekirdek.DirectoryInfo"); }
    }

    public GuardKonfig Konfig { get { lock (_kilit) return _konfig; } }

    // Ayarı değiştirir, kaydeder ve motorları buna göre başlatır/durdurur.
    public void Ayarla(string ad, bool deger)
    {
        lock (_kilit)
        {
            _konfig = ad switch { "nobet" => _konfig with { Nobet = deger }, "fidye" => _konfig with { Fidye = deger }, "canary" => _konfig with { Canary = deger }, _ => _konfig };
            try { Depo.YazJson(KonfigYolu, _konfig); } catch (Exception hk2) { HataKayit.Sessiz(hk2, "GuardCekirdek.Ayarla", true); }
            Uygula();
        }
        OlayDeposu.Ekle("Sistem", "Bilgi", $"KORUMA HİZMETİ -> ayar değişti: {ad} = {(deger ? "AÇIK" : "KAPALI")}");
    }

    // Ayarlara göre fidye izleyici ve canary'leri başlatır ya da durdurur.
    public void Uygula()
    {
        lock (_kilit)
        {
            if (_konfig.Fidye && _fidye == null)
            {
                var f = new FidyeIzleyici(); f.Supheli += (neden, sayi) =>
                {
                    OlayDeposu.Ekle("Tuzak", "Kritik", $"TUZAK ALARM -> FİDYE YAZILIMI şüphesi (koruma hizmeti): {sayi} dosya ({neden})");
                    // Yazan süreç atfı: ETW açıksa sürekli sayaçtan, değilse 4 sn'lik ölçümle.
                    try { var s = Etw?.EnCokYazanlar(3) ?? SurecAvcisi.Olc(4).Where(x => !SurecAvcisi.Korunuyor(x.Ad, x.Pid)).Take(3).Select(x => $"{x.Ad} (PID {x.Pid})").ToList(); if (s.Count > 0) OlayDeposu.Ekle("Tuzak", "Kritik", "TUZAK ALARM -> En çok yazan süreçler: " + string.Join(", ", s)); } catch (Exception hk3) { HataKayit.Sessiz(hk3, "GuardCekirdek.Uygula"); }
                };
                f.Baslat(IzlenecekKlasorler()); _fidye = f; OlayDeposu.Ekle("Sistem", "Bilgi", $"KORUMA HİZMETİ -> fidye izleme açıldı ({f.Klasorler.Count} klasör).");
            }
            else if (!_konfig.Fidye && _fidye != null) { _fidye.Dispose(); _fidye = null; }
            if (_konfig.Canary && _canaryler.Count == 0 && !PilotMod.SaltOkunur)
            {
                foreach (var belgeler in Profiller().Select(p => Path.Combine(p, "Documents")).Where(Directory.Exists))
                {
                    string klasor = Path.Combine(belgeler, "Eski_Parolalar_YEDEK"); var c = new CanaryDosyalari(); CanaryDosyalari.Klasor = klasor;
                    try { c.Baslat(); c.Tetiklendi += (ne, dosya) => OlayDeposu.Ekle("Tuzak", "Kritik", $"TUZAK ALARM -> Canary dosya {ne}: {dosya} (koruma hizmeti)"); _canaryler.Add((c, klasor)); } catch { c.Dispose(); }
                }
            }
            else if (!_konfig.Canary && _canaryler.Count > 0) { foreach (var (c, k) in _canaryler) { CanaryDosyalari.Klasor = k; try { c.Kaldir(); } catch (Exception hk4) { HataKayit.Sessiz(hk4, "GuardCekirdek.Uygula"); } } _canaryler.Clear(); }
        }
    }

    public bool FidyeAktif => _fidye?.Aktif == true; public int FidyeKlasorSayisi => _fidye?.Klasorler.Count ?? 0; public int CanarySayisi => _canaryler.Count;

    // Kullanıcı profilleri (Public/Default hariç).
    private IEnumerable<string> Profiller()
    {
        try { return Directory.GetDirectories(_a.ProfilKoku).Where(d => { var n = Path.GetFileName(d); return !new[] { "Public", "Default", "Default User", "All Users" }.Contains(n, StringComparer.OrdinalIgnoreCase); }).ToList(); } catch { return Array.Empty<string>(); }
    }

    // Fidye için izlenecekler: her profilin belge klasörleri + sistem dışı sabit diskler.
    public List<string> IzlenecekKlasorler()
    {
        var l = new List<string>();
        foreach (var p in Profiller()) foreach (var ad in new[] { "Documents", "Desktop", "Pictures", "Videos", "Music", "Downloads" }.Concat(Directory.Exists(p) ? Directory.GetDirectories(p, "OneDrive*").Select(Path.GetFileName).OfType<string>() : Array.Empty<string>())) { var d = Path.Combine(p, ad); if (Directory.Exists(d)) l.Add(d); }
        try { string sistem = Path.GetPathRoot(Environment.SystemDirectory) ?? ""; foreach (var d in DriveInfo.GetDrives()) if (d.IsReady && d.DriveType == DriveType.Fixed && !string.Equals(d.RootDirectory.FullName, sistem, StringComparison.OrdinalIgnoreCase)) l.Add(d.RootDirectory.FullName); } catch (Exception hk5) { HataKayit.Sessiz(hk5, "GuardCekirdek.IzlenecekKlasorler"); }
        return l.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Nöbetçi yoklaması: bu bilgisayarın kapıları taranır; ilk sonuç "taban" olur, sonraki yeni kapılar KRİTİK olay olarak yazılır.
    public async Task NobetKontrolAsync(CancellationToken ct)
    {
        try
        {
            var acik = await PortTarayici.TaraAsync(IPAddress.Loopback, PortTarayici.PortListesiOku(NobetPortlari), new Progress<int>(_ => { }), ct);
            var simdi = acik.Select(p => p.Port).ToHashSet(); string yol = Path.Combine(_a.Klasor, "nobet_taban.json"); var taban = Depo.Yukle<List<int>>(yol);
            if (taban == null) OlayDeposu.Ekle("Nöbetçi", "Bilgi", $"NÖBETÇİ BİLGİ -> koruma hizmeti ilk durumu kaydetti ({simdi.Count} açık kapı).");
            else
            {
                var yeni = simdi.Except(taban).OrderBy(x => x).ToList(); var kapanan = taban.Except(simdi).ToList();
                if (yeni.Count > 0) OlayDeposu.Ekle("Nöbetçi", "Kritik", $"NÖBETÇİ ALARM -> YENİ KAPI AÇILDI (koruma hizmeti): {string.Join(", ", yeni)}");
                else OlayDeposu.Ekle("Nöbetçi", "Bilgi", $"NÖBETÇİ BİLGİ -> koruma hizmeti kontrolü tamam: değişiklik yok ({simdi.Count} açık kapı){(kapanan.Count > 0 ? ", kapanan: " + string.Join(", ", kapanan) : "")}.");
            }
            Depo.YazJson(yol, simdi.OrderBy(x => x).ToList()); SonYoklama = DateTime.Now; AcikKapiSayisi = simdi.Count;
        }
        catch (OperationCanceledException hk6) { HataKayit.Sessiz(hk6, "GuardCekirdek.NobetKontrolAsync", true); }
        catch (Exception ex) { OlayDeposu.Ekle("Sistem", "Uyarı", "KORUMA HİZMETİ UYARI -> nöbetçi yoklaması başarısız: " + ex.Message); }
    }

    // ---- İkinci mühür kanalı ----
    public bool MuhurYaz(string ad, string ozet)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(ad ?? "", @"^[a-z0-9_.\-]{1,40}\z") || !System.Text.RegularExpressions.Regex.IsMatch(ozet ?? "", @"^[0-9A-Fa-f]{64}\z")) return false;
        bool onceVar = IkinciKanal.Oku(ad!) != null; bool ok = IkinciKanal.Yaz(ad!, ozet!.ToUpperInvariant());
        if (ok && ad == "kimlik")
        {
            IkinciKanal.YazHam("KimlikMuhurZamani", DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
            // GEÇİŞ (bir kez): kimlik mühür kanalı ilk kez kurulurken damgalar yazılır ve günlüğe işlenir.
            if (!onceVar) { IkinciKanal.YazHam("KimlikIlkKurulum", DateTime.UtcNow.ToString("o"), RegistryValueKind.String); IkinciKanal.YazHam("KanalKuruldu", "1", RegistryValueKind.String); IkinciKanal.YazHam("AnahtarSurum", 1, RegistryValueKind.DWord); OlayDeposu.Ekle("Sistem", "Bilgi", "KORUMA HİZMETİ -> kimlik mühür kanalı (HKLM) İLK KEZ kuruldu: mevcut sağlam kimlik özeti kaydedildi (geçiş)."); }
        }
        if (ok) OlayDeposu.Ekle("Sistem", "Bilgi", $"KORUMA HİZMETİ -> mühür kaydı güncellendi: {ad}"); return ok;
    }

    // ---- Kanıt paketi imzası (HMAC-SHA256; anahtar yalnızca hizmette) ----
    // Anahtar HKLM\SOFTWARE\BulutSoft\Guard\Gizli\KanitHmacAnahtar altında, DPAPI (LocalMachine) ile korunmuş blob olarak durur ve alt anahtarın ACL'i YALNIZCA SYSTEM/Yöneticiler'e açıktır.
    // (DPAPI LocalMachine tek başına yetmez: makinedeki her süreç çözebilir; asıl koruma ACL'dir. İkisi birlikte derinlemesine savunmadır.)
    private static readonly byte[] AnahtarEntropi = Encoding.UTF8.GetBytes("bs-kanit-hmac-v1");
    private byte[] PaketAnahtari()
    {
        lock (_kilit)
        {
            using var k = IkinciKanal.Kok.CreateSubKey(IkinciKanal.GizliAnahtar, true);
            if (k.GetValue("KanitHmacAnahtar") is byte[] blob) { try { var acik = ProtectedData.Unprotect(blob, AnahtarEntropi, DataProtectionScope.LocalMachine); if (acik.Length == 32) return acik; } catch (Exception hk7) { HataKayit.Sessiz(hk7, "GuardCekirdek.PaketAnahtari"); } }
            var yeni = RandomNumberGenerator.GetBytes(32); k.SetValue("KanitHmacAnahtar", ProtectedData.Protect(yeni, AnahtarEntropi, DataProtectionScope.LocalMachine), RegistryValueKind.Binary);
            if (!_a.Gelistirme) { try { var s = new RegistrySecurity(); s.SetAccessRuleProtection(true, false); s.AddAccessRule(new RegistryAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), RegistryRights.FullControl, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow)); s.AddAccessRule(new RegistryAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), RegistryRights.FullControl, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow)); k.SetAccessControl(s); } catch (Exception hk8) { HataKayit.Sessiz(hk8, "GuardCekirdek.PaketAnahtari"); } }
            return yeni;
        }
    }
    public string PaketImzala(string ozetHex, string meta) => Convert.ToHexString(HMACSHA256.HashData(PaketAnahtari(), Encoding.UTF8.GetBytes("BS-PAKET|" + ozetHex.ToUpperInvariant() + "|" + meta)));
    public bool PaketDogrula(string ozetHex, string meta, string imza) { try { return IkinciKanal.Esit(PaketImzala(ozetHex, meta), imza); } catch { return false; } }

    // ---- SSH sunucu parmak izi kaydı: HKLM\SOFTWARE\BulutSoft\Guard\SensorHostKeys\<sensör adı> = "adres|SHA256:..." ----
    private static readonly System.Text.RegularExpressions.Regex AdDeseni = new(@"^[\p{L}0-9 _\-]{1,40}\z", System.Text.RegularExpressions.RegexOptions.Compiled);
    public string? HostKeyOku(string ad) { try { if (!AdDeseni.IsMatch(ad ?? "")) return null; using var k = IkinciKanal.Kok.OpenSubKey(IkinciKanal.HostKeyAnahtari); return k?.GetValue(ad!) as string; } catch { return null; } }
    public bool HostKeyYaz(string ad, string host, string parmakIzi)
    {
        if (!AdDeseni.IsMatch(ad ?? "") || !System.Text.RegularExpressions.Regex.IsMatch(host ?? "", @"^[0-9.]{7,15}\z") || !System.Text.RegularExpressions.Regex.IsMatch(parmakIzi ?? "", @"^SHA256:[A-Za-z0-9+/=]{20,64}\z")) return false;
        try { using var k = IkinciKanal.Kok.CreateSubKey(IkinciKanal.HostKeyAnahtari, true); string onceki = (k.GetValue(ad!) as string) ?? ""; k.SetValue(ad!, host + "|" + parmakIzi, RegistryValueKind.String); OlayDeposu.Ekle("Sistem", onceki.Length > 0 && onceki != host + "|" + parmakIzi ? "Uyarı" : "Bilgi", $"KORUMA HİZMETİ{(onceki.Length > 0 && onceki != host + "|" + parmakIzi ? " UYARI" : "")} -> SSH sunucu parmak izi {(onceki.Length > 0 ? "GÜNCELLENDİ" : "kaydedildi")}: {ad} ({host})"); return true; } catch { return false; }
    }

    // ---- Watchdog (kalıcı aç/kapa): Nöbetçi + fidye izleme birlikte; durum HKLM'de de saklanır ----
    public void Watchdog(bool acik) { Ayarla("nobet", acik); Ayarla("fidye", acik); IkinciKanal.YazHam("WatchdogAcik", acik ? 1 : 0, RegistryValueKind.DWord); }
    // Yoklama aralığı (hem döngü hem "sonraki yoklama" hesabı bunu kullanır).
    public TimeSpan YoklamaAraligi => _a.Gelistirme ? TimeSpan.FromSeconds(20) : TimeSpan.FromMinutes(15);

    public void Dispose() { lock (_kilit) { _fidye?.Dispose(); foreach (var (c, _) in _canaryler) c.Dispose(); } Etw?.Dispose(); }
}
