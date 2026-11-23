// Bu dosya sensörde (uzak Linux konağı) çalışan "komut" ajanıdır: KomutCalistirici'nin gönderdiği şablon çağrısını SABİT bir tabloya göre doğrular ve çalıştırır.
// Eski ajana (SensorAjani.Kaynak) DOKUNMAZ: ayrı bir Python programıdır ve her çalıştırmada stdin'den gönderilir ("python3 - run <Base64 JSON>"), Kali'ye ayrıca dağıtım gerekmez.
// Tablo (kali_araclari.json) çalıştırma anında Base64 olarak betiğin içine gömülür: argüman listeleri ve kurallar Windows'taki şablonlarla AYNI KAYNAKTAN gelir (ikili tanım yok).
// Güvenlik: serbest komut yok; kabuk yok (argv listesi doğrudan çalışır); hedef ve her değer ajanda YENİDEN doğrulanır (yalnızca yazdırılabilir ASCII, '-' ile başlayamaz, desenin tamamına uymalı);
//   hedef özel ağ (RFC 1918) ya da Windows'un gönderdiği izinli listede olmalı; istekteki sudo/süre alanları YOK SAYILIR: sudo ve süre şablonun kendi tablosundan gelir (istek süreyi yalnızca kısaltabilir).
// İstek: {"mod":"komut","sablon":ad,"hedef":h,"arg":[değerler],"izinli":[...]}. Cevap satırları: {"t":"cikti","akis":"stdout|stderr","s":satır}, {"t":"done","kod":N,"zamanasimi":bool}, {"t":"error","m":...}.
//   "python3 - onizle <b64>" aynı doğrulamayı yapar ama ÇALIŞTIRMAZ; çalışacak tam argv'yi {"t":"onizle","argv":[...]} olarak döndürür.
using System.Text;

// Programımızın adı.
namespace LabDenetim;

// Komut ajanı.
internal static class KomutAjani
{
    // Ajan sürümü (günlük/tanılama için).
    public const string Surum = "1";

    // Tablo (JSON metni) gömülmüş ajan kaynağını verir.
    public static string Kaynak(string tabloJson) => Sablon.Replace("@@TABLO@@", Convert.ToBase64String(Encoding.UTF8.GetBytes(tabloJson ?? "[]")));

    // Ajanın Python kaynağı (yalnızca standart kütüphane).
    private const string Sablon = """
    import sys, os, json, base64, re, ipaddress, subprocess, threading, time, signal
    TABLO = json.loads(base64.b64decode("@@TABLO@@").decode("utf-8"))
    KILIT = threading.Lock()
    EN_UZUN = 200000
    EN_COK = 8000000
    ANSI = re.compile(r"\x1b\[[0-9;?]*[A-Za-z]")
    OZEL = [ipaddress.ip_network(x) for x in ("10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16")]
    ALAN = re.compile(r"(?=.{1,253}\Z)[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+\Z", re.ASCII)
    def yaz(o):
        s = json.dumps(o, ensure_ascii=True)
        with KILIT:
            sys.stdout.write(s + "\n")
            sys.stdout.flush()
    def yazdirilabilir(s):
        return all(32 <= ord(c) <= 126 for c in s)
    def ipv4(s):
        p = s.split(".")
        if len(p) != 4:
            return None
        for x in p:
            if not (1 <= len(x) <= 3 and x.isascii() and x.isdigit()) or (len(x) > 1 and x[0] == "0") or int(x) > 255:
                return None
        return ipaddress.IPv4Address(s)
    def alan_gecerli(s):
        return ALAN.fullmatch(s) is not None and any(c.isalpha() for c in s.rsplit(".", 1)[1])
    def izinli_coz(liste):
        aglar = []
        alanlar = set()
        for g in (liste if isinstance(liste, list) else [])[:200]:
            if not isinstance(g, str) or not yazdirilabilir(g) or len(g) > 253:
                continue
            g = g.strip()
            try:
                if "/" in g:
                    ip, _, on = g.partition("/")
                    if ipv4(ip) is None or not (on.isascii() and on.isdigit()) or not (24 <= int(on) <= 32):
                        continue
                    aglar.append(ipaddress.ip_network(ip + "/" + on, strict=False))
                elif ipv4(g) is not None:
                    aglar.append(ipaddress.ip_network(g + "/32"))
                elif alan_gecerli(g.lower()):
                    alanlar.add(g.lower())
            except ValueError:
                continue
        return aglar, alanlar
    def hedef_dogrula(tur, hedef, izinli):
        if tur == "yok":
            if hedef != "":
                raise ValueError("bu sablon hedef almaz")
            return
        if not hedef or len(hedef) > 253 or not yazdirilabilir(hedef) or re.fullmatch(r"[A-Za-z0-9.-]+", hedef, re.ASCII) is None or hedef[0] in "-.":
            raise ValueError("hedef gecersiz")
        aglar, alanlar = izinli_coz(izinli)
        if re.fullmatch(r"[0-9.]+", hedef):
            a = ipv4(hedef)
            if a is None:
                raise ValueError("hedef gecerli IPv4 degil")
            if tur == "alan":
                raise ValueError("bu sablon alan adi ister")
            if not any(a in n for n in OZEL) and not any(a in n for n in aglar):
                raise ValueError("hedef ozel ag disinda ve izinli listede degil; reddedildi")
        else:
            h = hedef.lower()
            if tur == "ip":
                raise ValueError("bu sablon IP adresi ister")
            if not alan_gecerli(h):
                raise ValueError("alan adi gecersiz")
            if h not in alanlar:
                raise ValueError("alan adi izinli listede degil; reddedildi")
    def deger_dogrula(p, v):
        if not isinstance(v, str) or len(v) > 256 or not yazdirilabilir(v) or v.startswith("-"):
            raise ValueError("parametre degeri gecersiz: " + p["ad"])
        if re.fullmatch(p["desen"], v, re.ASCII) is None:
            raise ValueError("parametre kurala uymuyor: " + p["ad"])
        if p.get("min") is not None or p.get("max") is not None:
            if re.fullmatch(r"[0-9]{1,9}", v) is None:
                raise ValueError("parametre tam sayi olmali: " + p["ad"])
            n = int(v)
            if (p.get("min") is not None and n < p["min"]) or (p.get("max") is not None and n > p["max"]):
                raise ValueError("parametre aralik disinda: " + p["ad"])
        if p.get("tur") == "arayuz":
            try:
                ifler = os.listdir("/sys/class/net")
            except OSError:
                raise ValueError("arayuz listesi okunamadi")
            if v not in ifler:
                raise ValueError("arayuz yok: " + v)
    def inceleme_dizini():
        d = os.path.join(os.path.expanduser("~"), "bulutsoft_inceleme")
        os.makedirs(d, mode=0o700, exist_ok=True)
        return d
    def dosya_dogrula(d, ad):
        yol = os.path.realpath(os.path.join(d, ad))
        if os.path.dirname(yol) != os.path.realpath(d) or not os.path.isfile(yol):
            raise ValueError("dosya bulunamadi (bulutsoft_inceleme klasorunde olmali): " + ad)
    def argv_olustur(sab, hedef, degerler, izinli):
        hedef_dogrula(sab.get("hedefTuru", "ip-alan"), hedef, izinli)
        prm = sab.get("parametreler", [])
        if not isinstance(degerler, list) or len(degerler) != len(prm):
            raise ValueError("parametre sayisi uyusmuyor")
        bag = {"hedef": hedef, "zaman": str(int(time.time()))}
        d = None
        if any("{inceleme}" in o for o in sab["argv"]):
            d = inceleme_dizini()
            bag["inceleme"] = d
        for p, v in zip(prm, degerler):
            deger_dogrula(p, v)
            if p.get("tur") == "dosya":
                if d is None:
                    raise ValueError("sablon hatali")
                dosya_dogrula(d, v)
            bag[p["ad"]] = v
        def f(m):
            if m.group(1) not in bag:
                raise ValueError("bilinmeyen yer tutucu")
            return bag[m.group(1)]
        return [re.sub(r"\{([a-z_]+)\}", f, o) for o in sab["argv"]]
    def etkin_zaman(sab, istek):
        z = int(sab.get("zamanAsimi", 60))
        r = istek.get("zaman")
        if isinstance(r, int) and not isinstance(r, bool) and 1 <= r < z:
            z = r
        return z
    def sudo_sar(sab, argv, zaman):
        if sab.get("sudo"):
            return ["sudo", "-n", "timeout", "--kill-after=5", str(zaman)] + argv
        return argv
    def kos(sab, argv, zaman):
        env = dict(os.environ)
        env.update({"NO_COLOR": "1", "TERM": "dumb"})
        for k, v in (sab.get("ortam") or {}).items():
            env[str(k)] = str(v)
        kw = {"stdin": subprocess.DEVNULL, "env": env}
        if os.name == "posix":
            kw["start_new_session"] = True
        arac = sab.get("arac", argv[0])
        yok = arac + " sensorde kurulu degil (sudo apt install " + sab.get("paket", arac) + ")"
        if sab.get("arkaPlan"):
            kw["stdout"] = subprocess.DEVNULL
            kw["stderr"] = subprocess.DEVNULL
            try:
                pr = subprocess.Popen(argv, **kw)
            except FileNotFoundError:
                raise ValueError(yok)
            time.sleep(2)
            rc = pr.poll()
            if rc is None:
                yaz({"t": "cikti", "akis": "stdout", "s": "baslatildi (surec " + str(pr.pid) + "); arayuz sensorun masaustunde acildi"})
                return 0, False
            yaz({"t": "cikti", "akis": "stderr", "s": "surec hemen sonlandi (cikis kodu " + str(rc) + "); masaustu oturumu acik mi?"})
            return rc, False
        kw["stdout"] = subprocess.PIPE
        kw["stderr"] = subprocess.PIPE
        try:
            pr = subprocess.Popen(argv, **kw)
        except FileNotFoundError:
            raise ValueError(yok)
        durum = {"zaman": False, "bayt": 0, "kesik": False}
        def oldur():
            durum["zaman"] = True
            try:
                if os.name == "posix":
                    os.killpg(pr.pid, signal.SIGKILL)
                else:
                    pr.kill()
            except Exception:
                pass
        zamanlayici = threading.Timer(zaman + (10 if sab.get("sudo") else 0), oldur)
        zamanlayici.daemon = True
        zamanlayici.start()
        def oku(f, akis):
            for ham in iter(lambda: f.readline(1000000), b""):
                s = ham.decode("utf-8", "replace").rstrip("\r\n")
                s = ANSI.sub("", s)
                s = "".join(c if (c == "\t" or (ord(c) >= 32 and ord(c) != 127)) else " " for c in s)
                if len(s) > EN_UZUN:
                    s = s[:EN_UZUN] + " ...(kisaltildi)"
                if durum["bayt"] > EN_COK:
                    if not durum["kesik"]:
                        durum["kesik"] = True
                        yaz({"t": "cikti", "akis": "stderr", "s": "[ajan] cikti siniri asildi; kalan cikti atildi"})
                    continue
                durum["bayt"] += len(s)
                yaz({"t": "cikti", "akis": akis, "s": s})
        ts = [threading.Thread(target=oku, args=(pr.stdout, "stdout")), threading.Thread(target=oku, args=(pr.stderr, "stderr"))]
        for t in ts:
            t.start()
        for t in ts:
            t.join()
        rc = pr.wait()
        zamanlayici.cancel()
        if durum["zaman"]:
            yaz({"t": "cikti", "akis": "stderr", "s": "[ajan] zaman asimi (" + str(zaman) + " sn); surec sonlandirildi"})
            return 124, True
        if rc < 0:
            rc = 128 - rc
        return rc, rc == 124
    def calistir(arg, onizle):
        try:
            a = json.loads(base64.b64decode(arg).decode("utf-8"))
        except Exception:
            yaz({"t": "error", "m": "arguman cozulemedi"})
            return 2
        try:
            if not isinstance(a, dict) or str(a.get("mod")) != "komut":
                raise ValueError("bilinmeyen kontrol")
            ad = a.get("sablon")
            sab = next((x for x in TABLO if x.get("ad") == ad), None) if isinstance(ad, str) else None
            if sab is None:
                raise ValueError("bilinmeyen sablon")
            hedef = a.get("hedef", "")
            if not isinstance(hedef, str):
                raise ValueError("hedef gecersiz")
            zaman = etkin_zaman(sab, a)
            tam = sudo_sar(sab, argv_olustur(sab, hedef, a.get("arg", []), a.get("izinli", [])), zaman)
            if onizle:
                yaz({"t": "onizle", "argv": tam, "zaman": zaman})
                yaz({"t": "done", "kod": 0})
                return 0
            rc, za = kos(sab, tam, zaman)
            yaz({"t": "done", "kod": rc, "zamanasimi": za})
            return 0
        except ValueError as e:
            yaz({"t": "error", "m": str(e)[:300]})
            return 2
        except Exception as e:
            yaz({"t": "error", "m": (type(e).__name__ + ": " + str(e))[:300]})
            return 3
    def main():
        if len(sys.argv) < 3 or sys.argv[1] not in ("run", "onizle"):
            yaz({"t": "error", "m": "mod yok"})
            return 2
        return calistir(sys.argv[2], sys.argv[1] == "onizle")
    sys.exit(main())
    """;
}
