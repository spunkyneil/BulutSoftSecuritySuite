import sys, json, socket, ipaddress, time, platform, base64, ssl, subprocess, re, datetime
import urllib.request, urllib.parse, urllib.error, warnings, http.client, struct, os, zlib
warnings.simplefilter("ignore")
from concurrent.futures import ThreadPoolExecutor, as_completed
SURUM = "6"
def yaz(o):
    sys.stdout.write(json.dumps(o, ensure_ascii=True) + "\n")
    sys.stdout.flush()
def ozel_mi(s):
    try:
        a = ipaddress.ip_address(s)
    except ValueError:
        return False
    return a.is_private or a.is_loopback or a.is_link_local
def kapilar(s):
    sonuc = set()
    for parca in s.split(","):
        if "-" in parca:
            a, b = parca.split("-", 1)
            a = int(a)
            b = int(b)
            if a < 1 or b > 65535 or a > b:
                raise ValueError("kapi araligi gecersiz")
            sonuc.update(range(a, b + 1))
        else:
            p = int(parca)
            if p < 1 or p > 65535:
                raise ValueError("kapi gecersiz")
            sonuc.add(p)
    if len(sonuc) > 20000:
        raise ValueError("en fazla 20000 kapi")
    return sorted(sonuc)
def dene(ip, port):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(0.6)
    try:
        if s.connect_ex((ip, port)) != 0:
            return None
        banner = ""
        try:
            s.settimeout(0.4)
            banner = s.recv(80).decode("utf-8", "replace").strip()
        except Exception:
            banner = ""
        return banner
    except Exception:
        return None
    finally:
        s.close()
def lab_ip(a):
    if a.is_loopback:
        return True
    if a.version == 4:
        o = a.packed
        return o[0] == 10 or (o[0] == 172 and 16 <= o[1] <= 31) or (o[0] == 192 and o[1] == 168)
    return a.is_link_local or a.is_site_local or (a.packed[0] & 0xFE) == 0xFC
def cozum(host):
    sonuc = []
    for bilgi in socket.getaddrinfo(host, None):
        ip = bilgi[4][0].split("%")[0]
        if ip not in sonuc:
            sonuc.append(ip)
    return sonuc
def ad_gecerli(ad):
    return bool(ad) and re.match(r"^[A-Za-z0-9]([A-Za-z0-9.-]{0,251}[A-Za-z0-9])?$", ad) is not None
def hedef_dogrula(host):
    if not host or not ad_gecerli(host):
        raise ValueError("gecersiz ad/adres")
    ipler = cozum(host)
    if not ipler:
        raise ValueError("adres cozulemedi")
    for ip in ipler:
        if not lab_ip(ipaddress.ip_address(ip)):
            raise ValueError("hedef ozel ag disina cozuluyor; reddedildi")
    return ipler
def http_topla(url):
    u = urllib.parse.urlparse(url)
    if u.scheme not in ("http", "https") or not u.hostname:
        raise ValueError("gecersiz web adresi")
    hedef_dogrula(u.hostname)
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    class Yonlendirme(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            hedef_dogrula(urllib.parse.urlparse(newurl).hostname)
            return urllib.request.HTTPRedirectHandler.redirect_request(self, req, fp, code, msg, headers, newurl)
    acici = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ctx), Yonlendirme())
    istek = urllib.request.Request(url, headers={"User-Agent": "BulutSoft-Sensor"})
    try:
        yanit = acici.open(istek, timeout=8)
    except urllib.error.HTTPError as e:
        yanit = e
    basliklar = {}
    for k in yanit.headers.keys():
        basliklar[k.lower()] = ";".join(yanit.headers.get_all(k) or [])
    cerezler = yanit.headers.get_all("Set-Cookie") or []
    durum = getattr(yanit, "status", None) or getattr(yanit, "code", 0)
    yaz({"t": "http", "url": url, "durum": durum, "basliklar": basliklar, "cerezler": cerezler})
def sertifika_coz(der):
    bilgi = {}
    try:
        pem = ssl.DER_cert_to_PEM_cert(der)
        p = subprocess.run(["openssl", "x509", "-noout", "-subject", "-issuer", "-enddate", "-text"], input=pem, capture_output=True, text=True, timeout=10)
        if p.returncode != 0:
            return bilgi
        t = p.stdout
        m = re.search(r"^subject\s*=\s*(.+)$", t, re.M)
        if m:
            bilgi["subject"] = m.group(1).strip()
        m = re.search(r"^issuer\s*=\s*(.+)$", t, re.M)
        if m:
            bilgi["issuer"] = m.group(1).strip()
        m = re.search(r"^notAfter\s*=\s*(.+)$", t, re.M)
        if m:
            bilgi["bitis"] = datetime.datetime.strptime(m.group(1).strip(), "%b %d %H:%M:%S %Y %Z").strftime("%Y-%m-%dT%H:%M:%S")
        m = re.search(r"Public Key Algorithm:\s*(\S+)", t)
        if m:
            bilgi["anahtar_tur"] = m.group(1).strip()
        m = re.search(r"Public-Key:\s*\((\d+) bit\)", t)
        if m:
            bilgi["anahtar_bit"] = int(m.group(1))
        m = re.search(r"Signature Algorithm:\s*(\S+)", t)
        if m:
            bilgi["imza"] = m.group(1).strip()
        m = re.search(r"X509v3 Subject Alternative Name:[^\n]*\n\s*(.+)", t)
        if m:
            bilgi["san"] = m.group(1).strip()[:1000]
        m = re.search(r"Not Before\s*:\s*(.+)", t)
        if m:
            try:
                bilgi["baslangic"] = datetime.datetime.strptime(m.group(1).strip(), "%b %d %H:%M:%S %Y %Z").strftime("%Y-%m-%dT%H:%M:%S")
            except Exception:
                pass
    except Exception as e:
        bilgi["hata"] = str(e)[:200]
    return bilgi
def eski_dene(host, port, surum):
    try:
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        ctx.minimum_version = surum
        ctx.maximum_version = surum
        ctx.set_ciphers("ALL:@SECLEVEL=0")
    except Exception:
        return "desteklenmiyor"
    try:
        with socket.create_connection((host, port), timeout=6) as s:
            with ctx.wrap_socket(s, server_hostname=host):
                return "kabul"
    except ssl.SSLError as e:
        if "NO_PROTOCOLS_AVAILABLE" in str(e).upper():
            return "desteklenmiyor"
        return "red"
    except Exception:
        return "red"
def tls_topla(host, port):
    hedef_dogrula(host)
    if port < 1 or port > 65535:
        raise ValueError("gecersiz kapi")
    sonuc = {"t": "tls", "host": host, "port": port}
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    with socket.create_connection((host, port), timeout=8) as s:
        with ctx.wrap_socket(s, server_hostname=host) as ss:
            der = ss.getpeercert(binary_form=True)
            sonuc["surum"] = ss.version() or ""
            c = ss.cipher()
            sonuc["sifre"] = c[0] if c else ""
    sonuc.update(sertifika_coz(der) if der else {})
    sonuc["dogrulama"] = ""
    try:
        ctx2 = ssl.create_default_context()
        with socket.create_connection((host, port), timeout=8) as s2:
            with ctx2.wrap_socket(s2, server_hostname=host):
                pass
    except ssl.SSLCertVerificationError as e:
        sonuc["dogrulama"] = (getattr(e, "verify_message", "") or str(e))[:300]
    except ssl.SSLError as e:
        sonuc["dogrulama"] = ""
    except Exception:
        sonuc["dogrulama"] = ""
    sonuc["eski"] = {"TLS 1.0": eski_dene(host, port, ssl.TLSVersion.TLSv1), "TLS 1.1": eski_dene(host, port, ssl.TLSVersion.TLSv1_1)}
    yaz(sonuc)
ALT_ISIMLER = ["www", "mail", "ftp", "admin", "dev", "test", "staging", "api", "vpn", "intranet", "db", "git", "backup", "old", "portal"]
def dns_topla(alan):
    ana = hedef_dogrula(alan)
    sonuc = {"t": "dns", "alan": alan, "ana": ana, "alt": []}
    for a in ALT_ISIMLER:
        fq = a + "." + alan
        try:
            ipler = cozum(fq)
        except Exception:
            continue
        if not ipler:
            continue
        if all(lab_ip(ipaddress.ip_address(i)) for i in ipler):
            sonuc["alt"].append({"ad": fq, "ips": ipler})
    yaz(sonuc)
class Ihlal(Exception):
    pass
class Sinir(Exception):
    pass
UA = "Mozilla/5.0 (compatible; BulutSoft-PasifGozlem/1.0; yalnizca-pasif-gozlem)"
def genel_mi(a):
    if a.version == 6 and a.ipv4_mapped is not None:
        a = a.ipv4_mapped
    return a.is_global and not a.is_multicast
def pasif_cozum(host):
    ipler = cozum(host)
    if not ipler:
        raise Ihlal("adres cozulemedi")
    for ip in ipler:
        if not genel_mi(ipaddress.ip_address(ip)):
            raise Ihlal("hedef genel internete cozulmuyor; pasif gozlem reddedildi")
    return ipler
class PBag(http.client.HTTPConnection):
    def __init__(self, host, ip, port):
        http.client.HTTPConnection.__init__(self, host, port, timeout=8)
        self.ip = ip
    def connect(self):
        self.sock = socket.create_connection((self.ip, self.port), timeout=8)
class PBagS(http.client.HTTPSConnection):
    def __init__(self, host, ip, port, ctx):
        http.client.HTTPSConnection.__init__(self, host, port, timeout=8, context=ctx)
        self.ip = ip
    def connect(self):
        s = socket.create_connection((self.ip, self.port), timeout=8)
        self.sock = self._context.wrap_socket(s, server_hostname=self.host)
class Say:
    n = 0
def istek(say, host, ip, https, yol, metot, govde_oku):
    say.n += 1
    if say.n > 8:
        raise Sinir("istek siniri")
    if https:
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        c = PBagS(host, ip, 443, ctx)
    else:
        c = PBag(host, ip, 80)
    try:
        c.request(metot, yol, headers={"User-Agent": UA, "Accept": "text/html,text/plain;q=0.9,*/*;q=0.5", "Connection": "close"})
        r = c.getresponse()
        govde = ""
        if govde_oku and r.status == 200:
            govde = r.read(16384).decode("utf-8", "replace")
        basliklar = {}
        for k in r.headers.keys():
            basliklar[k.lower()] = ", ".join(r.headers.get_all(k) or [])
        return {"durum": r.status, "b": basliklar, "c": (r.headers.get_all("Set-Cookie") or [])[:30], "loc": r.headers.get("Location") or "", "govde": govde}
    finally:
        c.close()
def izle(say, host, ip, https, yol, govde_oku):
    yon = []
    for i in range(4):
        r = istek(say, host, ip, https, yol, "GET", govde_oku)
        if 300 <= r["durum"] <= 399 and r["loc"]:
            u = urllib.parse.urlparse(urllib.parse.urljoin(("https" if https else "http") + "://" + host + yol, r["loc"]))
            yon.append(str(r["durum"]) + " -> " + u.geturl()[:200])
            if (u.hostname or "").lower() != host.lower() or u.scheme not in ("http", "https") or u.port not in (None, 80, 443):
                return None, yon, https, yol
            https = u.scheme == "https"
            yol = (u.path or "/") + ("?" + u.query if u.query else "")
            continue
        return r, yon, https, yol
    return None, yon, https, yol
def tls_pasif(host, ip):
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    sonuc = {}
    with socket.create_connection((ip, 443), timeout=8) as s:
        with ctx.wrap_socket(s, server_hostname=host) as ss:
            der = ss.getpeercert(binary_form=True)
            sonuc["surum"] = ss.version() or ""
            c = ss.cipher()
            sonuc["sifre"] = c[0] if c else ""
    sonuc.update(sertifika_coz(der) if der else {})
    sonuc["dogrulama"] = ""
    try:
        ctx2 = ssl.create_default_context()
        with socket.create_connection((ip, 443), timeout=8) as s2:
            with ctx2.wrap_socket(s2, server_hostname=host):
                pass
    except ssl.SSLCertVerificationError as e:
        sonuc["dogrulama"] = (getattr(e, "verify_message", "") or str(e))[:300]
    except Exception:
        sonuc["dogrulama"] = ""
    return sonuc
def dns_paket(ad, tur, kimlik):
    p = struct.pack(">HHHHHH", kimlik, 0x0100, 1, 0, 0, 0)
    for e in ad.strip(".").split("."):
        b = e.encode("ascii")
        if len(b) == 0 or len(b) > 63:
            raise ValueError("gecersiz dns adi")
        p += bytes([len(b)]) + b
    return p + b"\x00" + struct.pack(">HH", tur, 1)
def dns_ad(m, i):
    et = []
    son = i
    atladi = False
    koruma = 0
    while True:
        koruma += 1
        if koruma > 64 or i >= len(m):
            raise ValueError("bozuk ad")
        u = m[i]
        if u == 0:
            if not atladi:
                son = i + 1
            break
        if (u & 0xC0) == 0xC0:
            if not atladi:
                son = i + 2
            atladi = True
            i = ((u & 0x3F) << 8) | m[i + 1]
            continue
        et.append(m[i + 1:i + 1 + u].decode("ascii", "replace"))
        i += 1 + u
    return ".".join(et), son
def dns_coz(m, kimlik):
    if len(m) < 12 or struct.unpack(">H", m[0:2])[0] != kimlik:
        raise ValueError("cevap eslesmiyor")
    tc = (m[2] & 0x02) != 0
    rc = m[3] & 0x0F
    qd, an = struct.unpack(">HH", m[4:8])
    kayit = []
    if rc != 0:
        return kayit, tc
    i = 12
    for q in range(qd):
        _, i = dns_ad(m, i)
        i += 4
    for k in range(an):
        if i >= len(m):
            break
        _, i = dns_ad(m, i)
        tur = struct.unpack(">H", m[i:i + 2])[0]
        rl = struct.unpack(">H", m[i + 8:i + 10])[0]
        i += 10
        r = i
        v = ""
        if tur == 16:
            p = r
            while p < r + rl:
                u = m[p]
                p += 1
                v += m[p:p + u].decode("utf-8", "replace")
                p += u
        elif tur == 15:
            ad, _ = dns_ad(m, r + 2)
            v = str(struct.unpack(">H", m[r:r + 2])[0]) + " " + ad
        elif tur == 2:
            v, _ = dns_ad(m, r)
        elif tur == 257 and rl >= 2:
            tl = m[r + 1]
            v = str(m[r]) + " " + m[r + 2:r + 2 + tl].decode("ascii", "replace") + ' "' + m[r + 2 + tl:r + rl].decode("utf-8", "replace") + '"'
        if v:
            kayit.append((tur, v))
        i += rl
    return kayit, tc
def dns_sunucular():
    l = []
    try:
        for s in open("/etc/resolv.conf"):
            p = s.split()
            if len(p) >= 2 and p[0] == "nameserver" and ":" not in p[1]:
                l.append(p[1])
    except Exception:
        pass
    return l[:3]
def dns_sor(ad, tur):
    kimlik = int.from_bytes(os.urandom(2), "big") or 1
    paket = dns_paket(ad, tur, kimlik)
    son = None
    for sv in dns_sunucular():
        try:
            u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            u.settimeout(3)
            u.sendto(paket, (sv, 53))
            cevap = u.recv(4096)
            u.close()
            liste, tc = dns_coz(cevap, kimlik)
            if not tc:
                return [v for t, v in liste if t == tur]
            t = socket.create_connection((sv, 53), timeout=3)
            t.sendall(struct.pack(">H", len(paket)) + paket)
            uz = int.from_bytes(t.recv(2), "big")
            tam = b""
            while len(tam) < uz:
                x = t.recv(uz - len(tam))
                if not x:
                    break
                tam += x
            t.close()
            liste, _ = dns_coz(tam, kimlik)
            return [v for tt, v in liste if tt == tur]
        except Exception as e:
            son = e
    raise RuntimeError("dns sorgusu yapilamadi" + ((": " + str(son)[:100]) if son else " (sistem dns yok)"))
def pasif_topla(host, org):
    if not ad_gecerli(host):
        raise Ihlal("gecersiz ad/adres")
    ipler = pasif_cozum(host)
    ip = ipler[0]
    say = Say()
    notlar = []
    sonuc = {"t": "pasif", "host": host, "ips": ipler[:8], "notlar": notlar, "http_yon": "bilinmiyor"}
    try:
        r, yon, https, yol = izle(say, host, ip, True, "/", False)
        if r is None:
            notlar.append("https ana sayfasi okunamadi veya baska alan adina yonlendirildi")
        else:
            sonuc["http"] = {"url": "https://" + host + yol, "durum": r["durum"], "basliklar": r["b"], "cerezler": r["c"], "yonler": yon}
    except Sinir:
        notlar.append("istek siniri")
    except Exception as e:
        notlar.append("http gozlemi yapilamadi: " + str(e)[:120])
    try:
        say.n += 1
        c = PBag(host, ip, 80)
        try:
            c.request("HEAD", "/", headers={"User-Agent": UA, "Connection": "close"})
            r = c.getresponse()
            loc = r.getheader("Location") or ""
            sonuc["http_yon"] = "evet" if 300 <= r.status <= 399 and loc.lower().startswith("https://") else "hayir"
        finally:
            c.close()
    except Exception:
        notlar.append("http:// (80. port) cevap vermedi; yonlendirme kontrolu yapilamadi")
    try:
        sonuc["tls"] = tls_pasif(host, ip)
    except Exception as e:
        notlar.append("tls gozlemi yapilamadi (443 kapali olabilir): " + str(e)[:120])
    if org and ad_gecerli(org) and (host == org or host.endswith("." + org)):
        try:
            sonuc["dns"] = {"org": org, "txt": dns_sor(org, 16)[:30], "dmarc": dns_sor("_dmarc." + org, 16)[:10], "mx": dns_sor(org, 15)[:20], "ns": dns_sor(org, 2)[:20], "caa": dns_sor(org, 257)[:20]}
        except Exception as e:
            notlar.append("dns sorgulari yapilamadi: " + str(e)[:120])
    for yol0 in ("/.well-known/security.txt", "/security.txt"):
        try:
            r, yon, https, yol = izle(say, host, ip, True, yol0, True)
            if r is not None and r["durum"] == 200 and "contact" in r["govde"].lower():
                sonuc["sectxt"] = {"url": ("https" if https else "http") + "://" + host + yol, "govde": r["govde"], "tur": r["b"].get("content-type", ""), "https": https}
                break
        except Sinir:
            notlar.append("istek siniri")
            break
        except Exception:
            pass
    yaz(sonuc)
def nmap_topla(hedef, profil, zam):
    try:
        net = ipaddress.ip_network(hedef, strict=False)
    except ValueError:
        raise ValueError("hedef gecersiz")
    if net.version != 4 or not net.is_private or net.is_loopback or net.is_link_local or net.prefixlen < 24:
        raise ValueError("hedef ozel ag (en cok /24) olmali; reddedildi")
    if profil == "hizli":
        ek = ["-F"]
    elif profil == "standart":
        ek = ["--top-ports", "1000"]
    else:
        raise ValueError("bilinmeyen profil")
    if zam not in (2, 3, 4):
        raise ValueError("zamanlama 2, 3 veya 4 olmali")
    komut = ["nmap", "-sV", "--version-light", "-T" + str(zam), "--max-retries", "2", "--host-timeout", "60s", "-oX", "-"] + ek + [str(net)]
    try:
        p = subprocess.run(komut, capture_output=True, timeout=165)
    except FileNotFoundError:
        raise ValueError("nmap sensorde kurulu degil (sudo apt install nmap)")
    except subprocess.TimeoutExpired:
        raise ValueError("nmap zaman asimina ugradi")
    xml = p.stdout.decode("utf-8", "replace")
    if "<nmaprun" not in xml:
        raise ValueError("nmap hata verdi: " + p.stderr.decode("utf-8", "replace")[:150])
    z = base64.b64encode(zlib.compress(xml.encode("utf-8"), 9)).decode("ascii")
    if len(z) > 600000:
        raise ValueError("nmap ciktisi cok buyuk; hedefi daraltin")
    yaz({"t": "nmap", "hedef": str(net), "profil": profil, "xmlz": z})
def ip_al(url):
    try:
        rq = urllib.request.Request(url, headers={"User-Agent": "BulutSoft-IpTesti/1.0"})
        with urllib.request.urlopen(rq, timeout=6) as r:
            return r.read(4096).decode("utf-8", "replace")
    except Exception:
        return None
def ip_json_ip(s):
    try:
        v = json.loads(s).get("ip")
        return v if isinstance(v, str) else ""
    except Exception:
        return ""
def ip_topla():
    s = {"t": "ipcheck"}
    s["ipify"] = ip_al("https://api.ipify.org?format=json")
    s["ipify6"] = ip_al("https://api6.ipify.org?format=json")
    s["trace"] = ip_al("https://www.cloudflare.com/cdn-cgi/trace")
    try:
        s["cozucu"] = socket.gethostbyname("whoami.akamai.net")
    except Exception:
        s["cozucu"] = ""
    ip = ip_json_ip(s["ipify"]) if s["ipify"] else ""
    s["whois_ip"] = ip_al("https://ipwho.is/" + ip) if ip else None
    s["whois_dns"] = ip_al("https://ipwho.is/" + s["cozucu"]) if s["cozucu"] else None
    ns = []
    try:
        with open("/etc/resolv.conf") as f:
            for satir in f:
                p = satir.split()
                if len(p) >= 2 and p[0] == "nameserver":
                    ns.append(p[1])
    except Exception:
        pass
    s["dns"] = ns[:8]
    ar = []
    try:
        for ad in sorted(os.listdir("/sys/class/net"))[:30]:
            try:
                with open("/sys/class/net/" + ad + "/operstate") as f:
                    ar.append(ad + ":" + f.read().strip())
            except Exception:
                ar.append(ad + ":?")
    except Exception:
        pass
    s["arayuzler"] = ar
    yaz(s)
def arac_hedef(s):
    ip, _, port = s.partition(":")
    a = ipaddress.ip_address(ip)
    if a.version != 4 or not a.is_private or a.is_loopback or a.is_link_local:
        raise ValueError("hedef ozel ag IPv4 adresi olmali; reddedildi")
    pn = None
    if port != "":
        pn = int(port)
        if pn < 1 or pn > 65535:
            raise ValueError("kapi gecersiz")
    return str(a), pn
def arac_topla(arac, hedef):
    if arac == "searchsploit":
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9 ._-]{1,59}", hedef) or any(x.startswith("-") for x in hedef.split()):
            raise ValueError("arama metni gecersiz")
        komut = ["searchsploit", "--disable-colour"] + hedef.split()
        kur = "sudo apt install exploitdb"
    elif arac == "whois":
        if not ad_gecerli(hedef) or "." not in hedef or hedef.startswith("-") or re.fullmatch(r"[0-9.]+", hedef):
            raise ValueError("alan adi gecersiz")
        komut = ["whois", hedef]
        kur = "sudo apt install whois"
    elif arac == "sslscan":
        ip, pn = arac_hedef(hedef)
        komut = ["sslscan", "--no-colour", ip + ":" + str(pn or 443)]
        kur = "sudo apt install sslscan"
    elif arac == "ssh-audit":
        ip, pn = arac_hedef(hedef)
        komut = ["ssh-audit", "-p", str(pn or 22), ip]
        kur = "sudo apt install ssh-audit"
    elif arac == "nikto":
        ip, pn = arac_hedef(hedef)
        pn = pn or 80
        komut = ["nikto", "-h", ip, "-p", str(pn), "-Tuning", "123b", "-maxtime", "150s", "-nointeractive", "-ask", "no"]
        if pn in (443, 8443):
            komut.append("-ssl")
        kur = "sudo apt install nikto"
    else:
        raise ValueError("bilinmeyen arac")
    try:
        p = subprocess.run(komut, capture_output=True, timeout=170)
    except FileNotFoundError:
        raise ValueError(arac + " sensorde kurulu degil (" + kur + ")")
    except subprocess.TimeoutExpired:
        raise ValueError(arac + " zaman asimina ugradi")
    c = (p.stdout + b"\n" + p.stderr).decode("utf-8", "replace")
    c = re.sub(r"\x1b\[[0-9;]*[A-Za-z]", "", c)
    yaz({"t": "arac", "arac": arac, "hedef": hedef, "komut": " ".join(komut), "cikti": c[:40000]})
def calistir(arg):
    try:
        a = json.loads(base64.b64decode(arg).decode("utf-8"))
    except Exception:
        yaz({"t": "error", "m": "arguman cozulemedi"})
        return 2
    try:
        mod = str(a.get("mod"))
        if mod == "http":
            http_topla(str(a["url"]))
        elif mod == "tls":
            tls_topla(str(a["host"]), int(a["port"]))
        elif mod == "dns":
            dns_topla(str(a["domain"]))
        elif mod == "ipcheck":
            ip_topla()
        elif mod == "nmap":
            nmap_topla(str(a["hedef"]), str(a.get("profil") or "hizli"), int(a.get("zamanlama") or 3))
        elif mod == "arac":
            arac_topla(str(a["arac"]), str(a["hedef"]))
        elif mod == "pasif":
            pasif_topla(str(a["host"]).lower(), str(a.get("org") or "").lower())
        else:
            yaz({"t": "error", "m": "bilinmeyen kontrol"})
            return 2
    except Exception as e:
        yaz({"t": "error", "m": (type(e).__name__ + ": " + str(e))[:300]})
        return 3
    yaz({"t": "done"})
    return 0
def main():
    if len(sys.argv) < 2:
        yaz({"t": "error", "m": "mod yok"})
        return 2
    mod = sys.argv[1]
    if mod == "hello":
        yaz({"t": "hello", "v": SURUM, "py": platform.python_version(), "host": socket.gethostname(), "os": platform.system() + " " + platform.release()})
        return 0
    if mod == "tcp" and len(sys.argv) == 4:
        ip = sys.argv[2]
        try:
            net = ipaddress.ip_network(ip, strict=False)
        except ValueError:
            yaz({"t": "error", "m": "hedef gecersiz"})
            return 3
        if net.version != 4 or net.prefixlen < 24 or not ozel_mi(str(net.network_address)):
            yaz({"t": "error", "m": "hedef ozel ag degil veya aralik cok genis (en cok /24); reddedildi"})
            return 3
        hedefler = [str(h) for h in (net.hosts() if net.prefixlen < 31 else net)]
        try:
            liste = kapilar(sys.argv[3])
        except ValueError as e:
            yaz({"t": "error", "m": str(e)})
            return 2
        bas = time.time()
        toplam = len(liste) * len(hedefler)
        yaz({"t": "start", "ip": ip, "n": toplam})
        acik = 0
        bitti = 0
        with ThreadPoolExecutor(max_workers=64) as ex:
            isler = {ex.submit(dene, h, p): (h, p) for h in hedefler for p in liste}
            for f in as_completed(isler):
                h, p = isler[f]
                bitti += 1
                b = f.result()
                if b is not None:
                    acik += 1
                    yaz({"t": "open", "ip": h, "p": p, "b": b[:80]})
                if bitti % 200 == 0:
                    yaz({"t": "prog", "d": bitti, "n": toplam})
        yaz({"t": "done", "n": toplam, "open": acik, "s": round(time.time() - bas, 1)})
        return 0
    if mod == "run" and len(sys.argv) == 3:
        return calistir(sys.argv[2])
    yaz({"t": "error", "m": "bilinmeyen komut"})
    return 2
sys.exit(main())