// Arayüz ile konuşan yerel named pipe sunucusu (\\.\pipe\BulutSoftGuard). Protokol: satır başına bir JSON isteği, bir JSON cevabı.
// ERİŞİM: pipe'a her yerel kimliği doğrulanmış kullanıcı bağlanabilir; OKUMA komutları (ping, durum, olaylar, paket-dogrula, hostkey-oku) herkese açıktır.
// DEĞİŞTİREN komutlar (ayar, muhur-yaz, paket-imzala, hostkey-yaz) YALNIZCA istemci süreci kurulu uygulamanın exe'siyse kabul edilir (istemci sürecinin gerçek dosya yolu denetlenir).
// DÜRÜST SINIR: kurulu uygulamanın İÇİNE kod enjekte eden (aynı kullanıcı haklarıyla) bir zararlı bu denetimi geçebilir; yönetici olmayan ayrı bir zararlı geçemez
// (kurulu exe Program Files altındadır ve yönetici olmadan değiştirilemez).
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using LabDenetim;
using Microsoft.Win32.SafeHandles;

namespace BulutSoftGuardService;

internal sealed class PipeSunucu : BackgroundService
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr proc, int flags, StringBuilder ad, ref int boyut);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint erisim, bool miras, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    // Herkese açık (yalnızca OKUMA) komutlar. Komut adları büyük/küçük harf ve "_" / "-" farkından bağımsızdır (GET_STATUS = get-status).
    private static readonly HashSet<string> Acik = new() { "ping", "durum", "get-status", "get-seal-state", "verify-kimlik", "verify-kanit", "olaylar", "paket-dogrula", "hostkey-oku", "olay-dogrula" };
    // Olay yazma sınırı (soket/komut seli günlüğü şişirmesin): dakikada en çok bu kadar.
    private int _dakikaSayac; private DateTime _dakikaBasi = DateTime.UtcNow; private const int OlayYazSiniri = 600;
    // Kimlik dosyası yolu: yalnızca bir kullanıcının BulutSoft klasöründeki kimlik.json olabilir (hizmet rastgele dosya okumasın). Geliştirme kipinde var olan her yol.
    private bool KimlikYoluGecerli(string y) => !string.IsNullOrWhiteSpace(y) && y.Length < 300 && (_a.Gelistirme ? y.EndsWith("kimlik.json", StringComparison.OrdinalIgnoreCase) : System.Text.RegularExpressions.Regex.IsMatch(y, @"^[A-Za-z]:\\Users\\[^\\]+\\AppData\\Roaming\\BulutSoft\\kimlik\.json\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    private readonly GuardCekirdek _c; private readonly GuardAyarlari _a;
    public PipeSunucu(GuardCekirdek c, GuardAyarlari a) { _c = c; _a = a; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var guv = new PipeSecurity();
        guv.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        guv.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        guv.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? s = null;
            try
            {
                s = NamedPipeServerStreamAcl.Create(_a.PipeAdi, PipeDirection.InOut, 16, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, guv);
                await s.WaitForConnectionAsync(ct); var bagli = s; _ = Task.Run(() => Isle(bagli, ct), ct); s = null;
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(500, ct).ContinueWith(_ => { }); }
            finally { s?.Dispose(); }
        }
    }

    // Tek bağlantı: bir satır oku, işle, bir satır yaz.
    private async Task Isle(NamedPipeServerStream s, CancellationToken ct)
    {
        using (s)
        {
            try
            {
                using var zaman = CancellationTokenSource.CreateLinkedTokenSource(ct); zaman.CancelAfter(8000);
                using var r = new StreamReader(s, Encoding.UTF8, false, 4096, true); using var w = new StreamWriter(s, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
                string? satir = await r.ReadLineAsync(zaman.Token); if (string.IsNullOrWhiteSpace(satir) || satir.Length > 16384) return;
                ServisCevap cevap; try { var istek = JsonSerializer.Deserialize<ServisIstek>(satir); cevap = istek == null ? new(false, "boş istek") : Dagit(istek, s); } catch (Exception ex) { cevap = new(false, "hatalı istek: " + ex.Message); }
                await w.WriteLineAsync(JsonSerializer.Serialize(cevap));
            }
            catch (Exception hk1) { HataKayit.Sessiz(hk1, "PipeSunucu.Isle"); }
        }
    }

    // İstemci süreci kurulu uygulama exe'si mi?
    private bool UygulamaMi(NamedPipeServerStream s)
    {
        try
        {
            if (!GetNamedPipeClientProcessId(s.SafePipeHandle, out uint pid)) return false; IntPtr h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid); if (h == IntPtr.Zero) return false;
            try { var sb = new StringBuilder(1024); int n = sb.Capacity; if (!QueryFullProcessImageName(h, 0, sb, ref n)) return false; return string.Equals(Path.GetFullPath(sb.ToString()), Path.GetFullPath(_a.IzinliUygulama), StringComparison.OrdinalIgnoreCase); } finally { CloseHandle(h); }
        }
        catch { return false; }
    }

    private ServisCevap Dagit(ServisIstek i, NamedPipeServerStream s)
    {
        var arg = i.Arg ?? new(); string A(string k) => arg.TryGetValue(k, out var v) ? v : "";
        string komut = (i.Komut ?? "").Trim().ToLowerInvariant().Replace('_', '-'); if (komut.Length > 40) return new(false, "geçersiz komut");
        if (i.V != "1") return new(false, "desteklenmeyen protokol sürümü (v1 gerekli)");
        if (!Acik.Contains(komut) && !UygulamaMi(s)) { OlayDeposu.Ekle("Sistem", "Uyarı", $"KORUMA HİZMETİ UYARI -> yetkisiz istemci '{komut}' komutunu reddedildi."); return new(false, "yetkisiz istemci: yalnızca kurulu BulutSoft uygulaması değiştirebilir"); }
        switch (komut)
        {
            case "ping": return new(true);
            case "get-seal-state":
            {
                string y = A("yol"); if (!KimlikYoluGecerli(y)) return new(false, "geçersiz kimlik yolu");
                string? ozet = File.Exists(y) ? KimlikDeposu.OzetDosyadan(y) : null; string? kayit = IkinciKanal.Oku("kimlik");
                return new(true, null, new() { ["kimlikVar"] = File.Exists(y).ToString(), ["muhurVar"] = File.Exists(y + ".mac").ToString(), ["kimlikHash"] = ozet ?? "", ["muhurHash"] = kayit ?? "", ["kanal"] = _a.Gelistirme ? "HKCU-test" : "HKLM", ["kanalKuruldu"] = (IkinciKanal.OkuHam("KanalKuruldu") as string ?? "") });
            }
            case "verify-kimlik":
            {
                string y = A("yol"); if (!KimlikYoluGecerli(y)) return new(false, "geçersiz kimlik yolu"); string? kayit = IkinciKanal.Oku("kimlik");
                if (!File.Exists(y)) return kayit != null ? new(false, "kimlik dosyası SİLİNMİŞ ama hizmette kayıt var: kurtarma gerekli (ilk kuruluma dönülmez)", new() { ["durum"] = "kurtarma" }) : new(false, "kimlik dosyası yok ve hizmette kayıt da yok: ilk kurulum", new() { ["durum"] = "ilk-kurulum" });
                string? ozet = KimlikDeposu.OzetDosyadan(y); if (ozet == null) return new(false, "kimlik dosyası okunamadı", new() { ["durum"] = "bozuk" });
                if (kayit == null) return new(false, "hizmette kayıt yok (mühür kanalı henüz kurulmamış)", new() { ["durum"] = "kayit-yok" });
                return IkinciKanal.Esit(ozet, kayit) ? new(true, null, new() { ["durum"] = "gecerli" }) : new(false, "kimlik özeti hizmet kaydıyla UYUŞMUYOR: bütünlük ihlali", new() { ["durum"] = "ihlal" });
            }
            case "run-check-now": _ = Task.Run(() => _c.NobetKontrolAsync(CancellationToken.None)); return new(true, null, new() { ["not"] = "yoklama başlatıldı" });
            case "set-watchdog": if (bool.TryParse(A("acik"), out bool wd)) { _c.Watchdog(wd); return new(true); } return new(false, "geçersiz değer");
            case "archive-log": return OlayDeposu.ArsivleSimdi() ? new(true) : new(false, "arşivlenecek günlük yok");
            case "verify-kanit": return new(_c.PaketDogrula(A("ozet"), A("meta"), A("imza")));
            case "olay-yaz":
            {
                if ((DateTime.UtcNow - _dakikaBasi).TotalSeconds > 60) { _dakikaBasi = DateTime.UtcNow; _dakikaSayac = 0; }
                if (++_dakikaSayac > OlayYazSiniri) return new(false, "olay yazma sınırı aşıldı");
                string tur = A("tur"), sev = A("seviye"), msj = A("mesaj"); if (tur.Length is 0 or > 40 || tur.Any(char.IsControl) || sev is not ("Bilgi" or "Uyarı" or "Kritik") || msj.Length is 0 or > 2000) return new(false, "geçersiz olay");
                OlayDeposu.Ekle(tur, sev, msj); return new(true);
            }
            case "get-status":
            case "durum":
                var k = _c.Konfig; var z = OlayDeposu.Dogrula(true);
                return new(true, null, new() { ["surum"] = typeof(PipeSunucu).Assembly.GetName().Version?.ToString(3) ?? "?", ["nobet"] = k.Nobet.ToString(), ["fidye"] = k.Fidye.ToString(), ["canary"] = k.Canary.ToString(), ["fidyeAktif"] = _c.FidyeAktif.ToString(), ["fidyeKlasor"] = _c.FidyeKlasorSayisi.ToString(), ["canarySayisi"] = _c.CanarySayisi.ToString(), ["sonYoklama"] = _c.SonYoklama?.ToString("o") ?? "", ["acikKapi"] = _c.AcikKapiSayisi.ToString(), ["olayZinciriSaglam"] = z.Saglam.ToString(), ["olayZinciriNot"] = z.Not, ["etw"] = (_c.Etw?.Calisiyor == true).ToString(), ["korumaAktif"] = (k.Nobet || k.Fidye).ToString(), ["hizmetAyakta"] = "True", ["sonAlarm"] = (OlayDeposu.Oku().Where(o => o.Seviye == "Kritik").Select(o => (DateTime?)o.Zaman).Max()?.ToString("o") ?? ""), ["sonrakiYoklama"] = (k.Nobet && _c.SonYoklama != null ? _c.SonYoklama.Value + _c.YoklamaAraligi : (DateTime?)null)?.ToString("o") ?? "", ["etwNot"] = _c.Etw?.Calisiyor == true ? "" : "tam süreç atfı için ETW (SYSTEM) gerekli; yedek: 4 sn'lik ölçüm" });
            case "olaylar":
                long.TryParse(A("sonrasi"), out long tik); var l = OlayDeposu.Oku().Where(o => o.Zaman.Ticks > tik).OrderBy(o => o.Zaman).TakeLast(30).ToList();
                return new(true, null, new() { ["json"] = JsonSerializer.Serialize(l) });
            case "olay-dogrula": var d = OlayDeposu.Dogrula(); return new(d.Saglam, d.Not, new() { ["satir"] = d.Satir.ToString(), ["zincirli"] = d.Zincirli.ToString() });
            case "ayar": if (A("ad") is "nobet" or "fidye" or "canary" && bool.TryParse(A("deger"), out bool b)) { _c.Ayarla(A("ad"), b); return new(true); } return new(false, "geçersiz ayar");
            case "muhur-yaz": return _c.MuhurYaz(A("ad"), A("ozet")) ? new(true) : new(false, "mühür yazılamadı (ad/özet biçimi ya da yetki)");
            case "paket-imzala": return new(true, null, new() { ["imza"] = _c.PaketImzala(A("ozet"), A("meta")) });
            case "paket-dogrula": return new(_c.PaketDogrula(A("ozet"), A("meta"), A("imza")));
            case "hostkey-oku": var hk = _c.HostKeyOku(A("ad")); return new(hk != null, hk == null ? "kayıt yok" : null, hk == null ? null : new() { ["kayit"] = hk });
            case "hostkey-yaz": return _c.HostKeyYaz(A("ad"), A("host"), A("parmakIzi")) ? new(true) : new(false, "parmak izi kaydedilemedi");
            default: return new(false, "bilinmeyen komut");
        }
    }
}
