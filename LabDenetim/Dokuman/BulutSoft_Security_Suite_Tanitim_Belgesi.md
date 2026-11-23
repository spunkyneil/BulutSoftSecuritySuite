# ⚡ BulutSoft Security Suite v1.0 — Ayrıntılı Tanıtım Belgesi

> Bu belge, programın **ne yaptığını, nasıl çalıştığını ve nasıl göründüğünü** baştan sona anlatır: iskelet (mimari), dosya yapısı, tasarım sistemi (renkler, yazı tipleri, boyutlar), ekrandaki her bölge ve her yazı, tarama motoru, yapay zeka asistanı, rapor çıktıları ve güvenlik ilkeleri.
> Değerlerin tamamı doğrudan program kodundan alınmıştır (kaynak: 31 dosya, 4.611 satır C#).
> Sunum hazırlarken her bölüm bir slayt veya slayt grubu olarak kullanılabilir; en sonda önerilen slayt akışı vardır.

---

## 1. Programın Kimliği

| Alan | Değer |
|---|---|
| **Resmi adı** | BulutSoft Security Suite v1.0 |
| **Pencere başlığı** | ⚡ BulutSoft \| Siber Güvenlik ve Ağ Denetim Merkezi |
| **Geliştirici imzası (sağ alt)** | ⚡ Geliştirici: BulutSoft Güvenlik Teknolojileri |
| **Çalıştırılabilir dosya** | `BulutSoftSecuritySuite.exe` |
| **Şirket / Yazar / Sürüm** | BulutSoft Güvenlik Teknolojileri / BulutSoft / 1.0.0 |
| **Tür** | Windows masaüstü uygulaması (Windows Forms), koyu temalı "SOC / komuta merkezi" arayüzü |
| **Amaç** | Kullanıcının **kendi izole laboratuvarındaki** (kendi bilgisayarı ve sanal makineleri) güvenlik açıklarını bulmak, sade Türkçe ile açıklamak, kapatma komutlarını üretmek ve raporlamak |
| **Hedef kitle** | Bilgisayardan hiç anlamayan kişiden, öğrenen siber güvenlik meraklısına kadar |
| **Marka motifi** | Şimşek (⚡), parlak sarı (#FFD60A) ve elektrik mavisi (#00B4FF) vurguları, koyu zemin |
| **Alt başlık (sol menü)** | "Laboratuvar sürümü — Yalnızca izole ağınız için" |

**Programın temel sözü:** *"Evinizin kapılarını, pencerelerini ve kilitlerini tek tek yoklayıp 'şurası açık kalmış' der; hiçbir şeye zarar vermez."*

---

## 2. Teknik İskelet (Mimari)

### 2.1 Teknoloji yığını

| Katman | Teknoloji |
|---|---|
| Dil / Çalışma ortamı | C#, **.NET 10** (`net10.0-windows`), Nullable açık, ImplicitUsings açık |
| Arayüz | **Windows Forms** (kod ile kurulan arayüz; `TableLayoutPanel` ızgaraları, GDI+ özel çizimler) |
| Geliştirme ortamı | Visual Studio 2026 (Türkçe arayüz) |
| Ek paket (NuGet) | **QuestPDF 2026.9.2** (PDF rapor); yapay zeka isteği için yalnızca .NET'in kendi `HttpClient` ve `System.Text.Json` kütüphaneleri |
| Asenkron yapı | Tüm ağ işleri `async/await` (arayüz donmaz), iptal için `CancellationToken` |
| Saklama | Kullanıcının `%AppData%\BulutSoft` klasörü: şifreli API anahtarı (`api.dat`), ayarlar (`ayar.json`), geçmiş kayıtları (`gecmis\*.json`), tur işareti |
| Şifreleme | Windows **DPAPI** (`ProtectedData`, CurrentUser kapsamı) — API anahtarı yalnızca aynı Windows hesabıyla açılır |
| Ölçekleme | `AutoScaleMode.Dpi` (yüksek DPI ekranlarda net görünür) |

### 2.2 Katmanlı yapı

```
┌────────────────────────────────────────────────────────────────────┐
│  ARAYÜZ KATMANI      Form1 (partial sınıf, 13 parça) + diyaloglar  │
│  Tema · Kartlar · Tablo(rozet) · Halka(gauge) · Günlük · Menü      │
├────────────────────────────────────────────────────────────────────┤
│  ASİSTAN KATMANI     Asistan (bilgi bankası) · YapayZeka (2 mod)   │
│  ClaudeApi (HTTP akışı) · ApiDeposu (şifreli saklama)              │
├────────────────────────────────────────────────────────────────────┤
│  VERİ / RAPOR        Gecmis (kayıt+karşılaştırma) · RaporUretici   │
├────────────────────────────────────────────────────────────────────┤
│  TARAMA MOTORU       Moduller\  (7 modül)  +  LabKapsam (güvenlik   │
│                      kilidi: yalnızca özel/yerel adresler)          │
└────────────────────────────────────────────────────────────────────┘
```

**Tasarım kararı:** Tarama motoru (`Moduller` klasörü) arayüzden bağımsızdır; arayüz tamamen değiştirilse bile motor aynen çalışır.

### 2.3 Dosya ve klasör yapısı (31 kaynak dosya)

| Dosya | Satır | Görevi |
|---|---:|---|
| `Program.cs` | 16 | Programın giriş noktası; `Form1`'i açar |
| `Form1.cs` | 438 | Ana pencerenin mantığı: Taramayı Başlat akışı, doğrulama uyarıları, kilitleme, sonuç ekleme, rapor kaydetme |
| `Form1.Designer.cs` | 649 | Ekranın iskeleti: tüm ızgaralar, düğmeler, kutular, ipuçları |
| `Tema.cs` | 326 | Renk paleti, yazı tipleri, düğme/tablo/panel boyama |
| `Kartlar.cs` | 114 | 4 istatistik kartı + güvenlik puanı hesabı |
| `Halka.cs` | 113 | Dairesel puan kadranı (GDI+ özel çizim, animasyonlu) |
| `Tablo.cs` | 71 | Tablodaki yuvarlak köşeli renkli rozetlerin çizimi |
| `Gunluk.cs` | 79 | Canlı Sistem Günlüğü (siyah komuta ekranı) |
| `Menu.cs` | 50 | Sol menü ve Gelişmiş paneli davranışları, Yardım penceresi |
| `Profil.cs` | 99 | Hızlı / Standart / Kapsamlı profil düğmeleri |
| `Bilgi.cs` | 58 | ⓘ bilgi simgeleri ve ilk açılış turunu yönetme |
| `Tur.cs` | 83 | 3 adımlık tanıtım turu penceresi |
| `Asistan.cs` | 389 | Siber İlk Yardım Asistanı: yerel bilgi bankası, komut üretimi, Otomatik Onar |
| `YapayZeka.cs` | 249 | İki modlu asistan: Yerel & Manuel / Claude API, prompt üretici |
| `ClaudeApi.cs` | 170 | Şifreli anahtar deposu + Claude'a canlı (akış) istek istemcisi |
| `ApiAyarFormu.cs` | 81 | 🔑 API Ayarı penceresi |
| `HedefSec.cs` | 30 | "Hedef Seç / Sanal Lab Bul" menüsü |
| `AgFormu.cs` | 186 | Ağdaki cihazları listeleyen pencere |
| `Gecmis.cs` | 170 | Geçmiş kayıtları deposu, karşılaştırma, ekrana yükleme |
| `GecmisFormu.cs` | 204 | Geçmiş Operasyonlar penceresi |
| `RaporUretici.cs` | 183 | BulutSoft markalı HTML ve PDF rapor üretimi |
| `LabKapsam.cs` | 45 | **Güvenlik kilidi**: yalnızca laboratuvar adreslerine izin verir |
| `Models.cs` | 18 | Veri modelleri (Bulgu, Önem, GeçmişKayıt, RaporBilgisi) |
| `Moduller\PortTarayici.cs` | 119 | Asenkron port taraması + servis kimliği (banner) okuma |
| `Moduller\YapilandirmaDenetimi.cs` | 112 | Tehlikeli servis/ayar tespiti (Telnet, FTP, SMB, RDP…) |
| `Moduller\HttpDenetimi.cs` | 77 | Web güvenlik başlıkları ve çerez bayrakları |
| `Moduller\TlsDenetimi.cs` | 98 | Sertifika ve şifreleme protokolü denetimi |
| `Moduller\DnsDenetimi.cs` | 52 | İsim kayıtları ve alt alan adı keşfi |
| `Moduller\GirdiDogrulamaTesti.cs` | 82 | Form kutusu (girdi doğrulama) testi |
| `Moduller\AgKesfi.cs` | 250 | Ağ kartlarını ve ağdaki aktif cihazları bulma (ping + ARP) |

**Kod yazım kuralı:** Kodun **her satırının üstünde** sade Türkçe yorum satırı vardır (sıfır bilgili kişinin okuyabilmesi için).

### 2.4 "⚡ TARAMAYI BAŞLAT" tuşuna basılınca olan olaylar (veri akışı)

1. **Doğrulama:** Boş/hatalı kutu varsa çökmez; "Lütfen önce yukarıdaki kutuya bir adres yazın…" gibi kibar bir uyarı penceresi çıkar ve imleç ilgili kutuya gider (Gelişmiş panelde gizliyse panel otomatik açılır).
2. **Kilitleme:** Tüm kutular, onay kutuları, profil düğmeleri ve Hedef Seç düğmesi pasif olur; **Durdur** aktif olur; ilerleme çubuğu görünür.
3. **Güvenlik kilidi (`LabKapsam`):** Hedef çözülür; yalnızca `127.x`, `10.x`, `172.16-31.x`, `192.168.x` ve yerel IPv6 adreslerine izin verilir. Dışarıdaki adres **reddedilir**.
4. **Port tarama:** En fazla 100 eş zamanlı bağlantıyla, kapı başına 1,5 sn zaman aşımı; açık kapılardan "servis kimliği" (banner) okunur.
5. **Seçili kontroller sırayla çalışır:** Zayıf ayarlar → Web başlıkları → TLS → DNS → Girdi testi. Bir modül hata verirse tabloya gri "ATLANDI" satırı olarak yazılır, **diğerleri devam eder**.
6. **Canlı güncelleme:** Her sonuç anında tabloya, günlüğe, sayaçlara ve kartlara yansır; puan hesabı yeniden yapılır; kadran animasyonla dolar.
7. **Bitiş:** Günlüğe özet yazılır, tarama **Geçmiş'e kaydedilir**, aynı hedefin önceki taramasıyla **karşılaştırma** günlüğe eklenir.
8. **Kilitler açılır**, ilerleme çubuğu gizlenir.

---

## 3. Tasarım Sistemi

### 3.1 Renk paleti (marka renkleri)

| Rol | Ad (kodda) | RGB | Hex |
|---|---|---|---|
| Ana zemin | Zemin | 13, 17, 23 | **#0D1117** |
| Kutu/kart zemini | ZeminKutu | 22, 27, 34 | **#161B22** |
| Menü/durum çubuğu zemini | ZeminKoyu | 6, 9, 14 | **#06090E** |
| Şimşek sarısı (marka, ana eylem) | Simsek | 255, 214, 10 | **#FFD60A** |
| Elektrik mavisi (marka, vurgu) | Elektrik | 0, 180, 255 | **#00B4FF** |
| Ana yazı | Yazi | 230, 237, 243 | **#E6EDF3** |
| Silik yazı (ipucu/başlık) | YaziSilik | 139, 148, 158 | **#8B949E** |
| "İyi" rakam / terminal yeşili | RakamYesil | 80, 250, 123 | **#50FA7B** |
| "Kötü" rakam / kırmızı | RakamKirmizi | 255, 95, 95 | **#FF5F5F** |

**Tablo ve panel renkleri**

| Alan | Hex |
|---|---|
| Tablo satırı (tek) / tablo zemini | #0F172A |
| Tablo satırı (aralıklı) | #162032 |
| Seçili satır | #1E3A5F (yazı beyaz) |
| Tablo yatay çizgi / başlık zemini | #1E293B |
| Tablo başlık yazısı | #38BDF8 |
| Asistan paneli zemini | #0B1220 |
| Asistan yazısı | #E2E8F0 |
| Komut kutusu zemini / yazısı | #020617 / #4ADE80 (terminal yeşili) |
| Panel ve günlük çerçevesi (ince, 1 px) | #334155 |
| Pasif düğme / pasif mod düğmesi | #30363D |
| Durdur düğmesi | zemin #A02828, yazı beyaz |
| Sol menü seçili/üzerine gelinen | #161B22 / #1C222C |

**Tehlike seviyesi renkleri (trafik lambası mantığı)**

| Seviye (rozet) | Anlam | Rozet rengi |
|---|---|---|
| **GÜVENLİ** | Normal, sorun yok | #10B981 (yeşil) |
| **DİKKAT** | Küçük eksik | #FACC15 (sarı) |
| **UYARI** | Orta dereceli zayıflık | #F59E0B (turuncu) |
| **KRİTİK** | Ciddi güvenlik açığı | #EF4444 (kırmızı) |
| **ATLANDI** | Kontrol yapılamadı (güvenli sayılmaz) | #94A3B8 (gri) |

**Özet kutuları (koyu neon):** Yeşil: zemin #062E22 / yazı-çerçeve #10B981 · Sarı-turuncu: #3B2806 / #F59E0B · Kırmızı: #3B0D11 / #EF4444.

**Düğme aileleri (hepsi düz, 1 px kenarlıklı, el imleci)**

| Düğme | Zemin | Kenarlık | Yazı |
|---|---|---|---|
| ⚡ TARAMAYI BAŞLAT | #FFD60A | — | siyah |
| Kurumsal (rapor, kopyala, API Ayarı) | #1E293B | #38BDF8 | #E0F2FE (API Ayarı: kenarlık #94A3B8) |
| 🖥️ Hedef Seç / Sanal Lab Bul | #082F49 | #38BDF8 | #E0F2FE |
| 🛡️ Otomatik Onar (dikkat) | #3B2806 | #F59E0B | #FDE68A |
| 🤖 Derin Analiz (yapay zeka) | #1E1B4B | #818CF8 | #E0E7FF |
| 📋 Prompt Hazırla (ücretsiz mod) | #052E16 | #4ADE80 | #DCFCE7 |
| Seçili mod / seçili profil | #FFD60A (Claude modu seçiliyse #818CF8) | — | siyah |

### 3.2 Yazı tipleri (tüm kullanımlar)

Programda yalnızca **iki yazı ailesi** vardır: **Segoe UI** (Windows'un modern arayüz yazı tipi) ve **Consolas** (eşit aralıklı, terminal hissi için). Raporlarda ayrıca HTML'de `Segoe UI, Arial, sans-serif` / `Consolas, monospace`, PDF'te QuestPDF'in yerleşik **Lato** yazı tipi kullanılır.

| Kullanım yeri | Yazı tipi | Boyut | Stil |
|---|---|---:|---|
| Sol menü marka yazısı "⚡ BulutSoft" | Segoe UI | 16 | Kalın |
| Sol menü "Security Suite v1.0" | Segoe UI | 9 | Kalın |
| Sol menü düğmeleri | Segoe UI | 10 | Kalın |
| Sol menü alt notu | Segoe UI | 8 | Normal |
| "Hedef adres:" ve "Profil:" etiketleri | Segoe UI | 10 | Kalın |
| Hedef adres kutusu | Segoe UI | 11 | Normal |
| Genel düğmeler (Başlat, Durdur, Gelişmiş, rapor, profil, Hedef Seç) | Segoe UI | 9 | Kalın |
| Mod seçici düğmeleri | Segoe UI | 8,5 | Kalın |
| ⓘ bilgi simgesi | Segoe UI | 11 | Kalın |
| İstatistik kartı başlığı (örn. TARANAN KAPI SAYISI) | Segoe UI | 8 | Kalın |
| İstatistik kartı büyük rakamı | **Consolas** | 24 | Kalın |
| Puan kadranı sayısı | Segoe UI | 15 | Kalın |
| Puan kadranı "/100" | Segoe UI | 7,5 | Kalın |
| Özet kutuları (YEŞİL/SARI/KIRMIZI) | Segoe UI | 9 | Kalın |
| Tablo hücre yazısı | Segoe UI | 9,5 | Normal |
| Tablo başlıkları | Segoe UI | 9 | Kalın |
| Tablo rozetleri (GÜVENLİ, KRİTİK…) | Segoe UI | 8,5 | Kalın |
| Asistan başlığı | Segoe UI | 10 | Kalın |
| Asistan açıklama metni | Segoe UI | 9,5 | Normal (başlıklar Kalın) |
| "ÇÖZÜM KOMUTU" etiketi | Segoe UI | 8 | Kalın |
| Çözüm komutu kutusu | **Consolas** | 9,5 | Normal |
| Tüm asistan düğmeleri | Segoe UI | 9 | Kalın |
| Onay kutusu "Tüm tarama özetini analiz et" | Segoe UI | 8,5 | Normal |
| Manuel mod notu | Segoe UI | 8 | Normal |
| Canlı Sistem Günlüğü başlığı | Segoe UI | 8 | Kalın |
| Canlı Sistem Günlüğü satırları | **Consolas** | 9 | Normal |
| Durum çubuğu / imza | Segoe UI | 9 | Kalın (imza) |
| Tanıtım turu başlığı / gövdesi / noktalar | Segoe UI | 15 / 10,5 / 14 | Kalın / Normal / Kalın |
| Diyalog pencerelerindeki etiket/düğmeler | Segoe UI | 9 (9,5 listelerde) | Kalın / Normal |
| Diğer (açıklama yazıları, onay kutuları, varsayılan) | Segoe UI | 9 (Windows varsayılanı) | Normal |

**Rapor yazı tipleri:** HTML rapor başlığı 30 px, bölüm başlıkları ~ 24 px, tablo 13,5 px, komut kutusu 12 px Consolas · PDF: gövde 9 pt, başlık 18 pt kalın.

### 3.3 İkonlar ve semboller

Kullanılan semboller: ⚡ (marka) · ◼ Panel · ◷ Geçmiş · ▤ Rapor · ? Yardım · ▶ Tur · 🖥️ Hedef Seç · 💻 / 🔎 (menü) · 🌐 HTML · 📄 PDF · 🧠 Yerel mod · 🔑 API · 🤖 Claude · 📋 Kopyala · 🛡️ Onar · 📌 "Bu Ne Demek?" · 🛠️ "Nasıl Çözerim?" · ● (rozet noktası) · ⓘ bilgi · ▾ ▴ açılır oklar.
*(Not: Emoji'ler Windows'un yazı tipinden geldiği için düğmelerde tek renkli küçük simge olarak görünür.)*

### 3.4 Boyutlar ve ızgara

| Öğe | Değer |
|---|---|
| Pencere başlangıç boyutu | **1620 × 960** (iç alan), ekranın ortasında açılır |
| En küçük pencere | 1500 × 820 |
| Sol menü genişliği | 210 px (tam yükseklik) |
| Alt durum çubuğu yüksekliği | 28 px |
| Sağ alan iç boşluk | 14 / 12 / 14 / 8 px |
| Sağ alan satırları (yukarıdan aşağıya) | Hedef çubuğu 56 · İnce ilerleme çubuğu 10 · Gelişmiş panel (içeriğe göre, gizliyken 0) · Kartlar 92 · Sayaç şeridi 40 · İçerik (kalan) · Canlı günlük 120 |
| İçerik satırı | Sonuç tablosu (kalan genişlik) + **Asistan paneli 410 px** |
| Asistan panel iç boşluk / satırlar | 12/10/12/12 · Başlık 28 · Mod seçici 38 · Açıklama (kalan) · Komut etiketi 20 · Komut kutusu 60 · Moda özel eylem 76 · Otomatik Onar 38 · Kopyala 38 |
| Düğme boyutları | Hedef Seç 225×38 · Gelişmiş 100×38 · Başlat 176×38 · Durdur 72×38 · Profil 60/80/84×36 · HTML/PDF Rapor 190×32 · Özet kutuları 170/200/170×28 · Menü düğmeleri 210×44 |
| Tablo | Başlık 36 px · en az satır 40 px · hücre iç boşluk 8/6/8/6 · rozet sütunu 146 px sabit · diğer sütunlar oransal (60/130/110/120) ve asgari 122/170/160/170 px |
| Rozet | 22 px yüksek, tam yuvarlak uçlu ("hap" şeklinde), içi %20 saydam neon, 1,2 px neon çizgi |
| Kart | Sol kenarda 4 px renkli şerit; puan kartında sağda 96 px'lik kadran |

### 3.5 Ekran yerleşimi (kuşbakışı çizim)

```
┌──────────┬─────────────────────────────────────────────────────────────────────────────┐
│⚡BulutSoft│ Hedef adres:ⓘ [127.0.0.1]  [🖥️ Hedef Seç / Sanal Lab Bul ▾]  Profil:ⓘ        │
│Security  │ [Hızlı][Standart][Kapsamlı]  [Gelişmiş ▾]  [⚡ TARAMAYI BAŞLAT] [Durdur]      │
│Suite v1.0│ ─ ince ilerleme çubuğu (yalnızca tarama sürerken) ────────────────────────── │
│          │ ┌ Gelişmiş panel (başta gizli): kapı no., web adresi, kutucuk, alan adı, ☑ ┐ │
│◼ Panel   │ ┌────────┐┌────────┐┌─────────────────────┐┌────────┐                       │
│⚡Yeni     │ │TARANAN ││BULUNAN ││GÜVENLİK PUANI   (◔) ││GEÇEN   │   ← 4 istatistik kartı │
│  Tarama  │ │KAPI    ││AÇIKLAR ││                 69  ││SÜRE    │                       │
│◷ Geçmiş  │ └────────┘└────────┘└─────────────────────┘└────────┘                       │
│  Operas. │ [YEŞİL:9] [SARI/TURUNCU:9] [KIRMIZI:0]        [🌐 HTML Rapor] [📄 PDF Rapor] │
│▤ Profes. │ ┌─ Sonuç tablosu ─────────────────────────┐┌─ ⚡ SİBER İLK YARDIM ve AI ─┐ │
│  Rapor   │ │ Tehlike  Kontrol  Ne bulundu?  Ayrıntı  ││  ÇÖZÜM ASİSTANI              │ │
│? Yardım  │ │ (GÜVENLİ) Port    Açık port 445 ...     ││ [🧠 Yerel & Manuel][⚡Claude] │ │
│▶ Tanıtım │ │ (UYARI)   Yapıl.  Dosya paylaşımı ...   ││ 📌 Bu Ne Demek? ...           │ │
│  Turu    │ │ (ATLANDI) HTTP    Bu kontrol çalıştır.. ││ 🛠️ Nasıl Çözerim? ...         │ │
│          │ │ ...                                     ││ ÇÖZÜM KOMUTU [netsh ...]      │ │
│Laboratuvar│ └─────────────────────────────────────────┘│ [mod düğmeleri][🛡️ Onar][📋]  │ │
│sürümü    │ ⚡ CANLI SİSTEM GÜNLÜĞÜ  (siyah, renkli kayan satırlar)                       │
├──────────┴─────────────────────────────────────────────────────────────────────────────┤
│ Durum yazısı (mavi)                                ⚡ Geliştirici: BulutSoft Güvenlik Tekn. │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Ekrandaki Her Bölge: Metinler, Davranışlar, Detaylar

### 4.1 Sol menü (210 px, #06090E zemin)

| Öğe | Metin | Davranış / fare ipucu |
|---|---|---|
| Marka | **⚡ BulutSoft** (16 pt, sarı) + **Security Suite v1.0** (9 pt, mavi) | — |
| Düğme 1 (başlangıçta seçili, sarı yazı) | ◼ Panel | Ana ekran. İpucu: "Ana ekran: hedefi yazın, taramayı başlatın, sonuçları görün." |
| Düğme 2 | ⚡ Yeni Tarama | Eski sonuçları, sayaçları ve süreyi temizler; hedef kutusuna odaklanır. Tarama sürerken uyarır. |
| Düğme 3 | ◷ Geçmiş Operasyonlar | Geçmiş penceresini açar (bkz. 7). |
| Düğme 4 | ▤ Profesyonel Rapor | HTML + PDF'i masaüstüne kaydeder, HTML'i tarayıcıda açar. |
| Düğme 5 | ? Yardım | Sade dille "Bu program ne işe yarar? / Nasıl kullanırım? / Güvenlik" penceresi. |
| Düğme 6 | ▶ Tanıtım Turu | 3 adımlık turu yeniden gösterir. |
| Alt not (8 pt, silik) | Laboratuvar sürümü / Yalnızca izole ağınız için | — |

### 4.2 Üst hedef çubuğu

| Öğe | Metin / değer | Açıklama |
|---|---|---|
| Etiket + ⓘ | **Hedef adres:** | ⓘ: "Denetlenecek bilgisayarın adresi (evin sokak numarası gibi). Kendi bilgisayarınız için 127.0.0.1 yazın. Sanal Kali bilgisayarı için 192.168… adresini yazın. Güvenlik için yalnızca kendi ağınızdaki adresler kabul edilir." |
| Kutu | Hazır değer **127.0.0.1**; silik örnek yazı "Örnek: 127.0.0.1"; 150 px, 11 pt | Boş bırakılırsa nazik uyarı çıkar |
| Düğme | **🖥️ Hedef Seç / Sanal Lab Bul ▾** | Açılır menü: "💻 1. Yerel Bilgisayarım (127.0.0.1)" ve "🔎 2. Sanal Ağları Tara (VirtualBox / VMware / Yerel Ağdaki Açık Makineleri Bul)…" |
| Profil | **Profil:** ⓘ + **[Hızlı] [Standart] [Kapsamlı]** (+ "· Özel" göstergesi) | Seçili profil sarı. Elle ayar yapılınca hiçbiri sarı kalmaz ve "· Özel" yazar. |
| Düğme | **Gelişmiş ▾ / ▴** | Gelişmiş paneli açar/kapatır |
| Düğme | **⚡ TARAMAYI BAŞLAT** (sarı) | Taramayı başlatır |
| Düğme | **Durdur** (koyu kırmızı) | Başta pasif; tarama sürerken aktif |
| İnce çubuk | İlerleme çubuğu | Yalnızca tarama sürerken görünür; kapı yoklarken gerçek yüzde, diğer adımlarda sürekli hareket |

**Profil içerikleri**

| Profil | Kapı listesi | Açık kontroller |
|---|---|---|
| Hızlı | 8 kapı: 21,22,23,25,80,443,445,3389 | Kapı yoklama + Zayıf ayarlar |
| **Standart (açılışta seçili)** | 17 kapı: 21,22,23,25,53,80,110,139,143,443,445,3306,3389,5432,5900,8080,8443 | + Web sitesi korumaları + İsim kayıtları |
| Kapsamlı | 1-1024, 1433, 1521, 3306, 3389, 5432, 5900, 5985, 8000-8100, 8443, 9000 | Standart'a ek olarak adres https ile başlıyorsa Şifreli bağlantı |
| Özel | Elle yazılan | Elle seçilen |

> "Form kutusu testi" hiçbir profilde otomatik açılmaz; kullanıcı bilerek açmalıdır.

### 4.3 Gelişmiş panel (başta gizli, açılınca alan açar)

Üstte sade açıklama: *"Bu bölümü değiştirmek zorunda değilsiniz; hazır değerler çoğu durumda yeterlidir. Kutuların üstüne fareyi getirirseniz ne işe yaradıklarını okursunuz."*

| Alan | Hazır değer | Silik örnek | Açıklama (ⓘ) |
|---|---|---|---|
| Kapı numaraları: | 17 kapılık Standart liste | "Örnek: 22,80,443 veya 8000-8100" | Port = bilgisayarın numaralı "kapısı"; virgül ve aralık (8000-8100) yazılabilir |
| Web sitesi adresi: | http://127.0.0.1:8080/ | "Örnek: http://127.0.0.1:8080/" | Tarayıcıya yazdığınız tam adres |
| Test edilecek kutucuk adı: | q | "Örnek: q" | Arama kutusu/form alan adı; zararsız deneme yazıları gönderir |
| Alan adı (isim kaydı): | localhost | "Örnek: localhost" | "Telefon rehberindeki isim gibi" |

**6 kontrol kutucuğu:** ☑ Kapıları yokla · ☑ Web sitesi korumaları · ☐ Şifreli bağlantı (https) · ☑ İsim kayıtları · ☑ Zayıf ayarlar · ☐ Form kutusu testi. (Her birinin fare balonunda günlük hayattan benzetme vardır: "Evin hangi kapılarının açık kaldığını görmek gibi", "Sertifika, sitenin kimlik kartı gibidir" vb.)

### 4.4 İstatistik kartları (canlı güncellenir)

| Kart | Başlık | Ne gösterir | Renk kuralı |
|---|---|---|---|
| 1 | TARANAN KAPI SAYISI | `5/17 … 17/17`; kapı yoklaması yoksa `--` | mavi |
| 2 | BULUNAN AÇIKLAR | Sarı + turuncu + kırmızı sonuç toplamı (yeşiller sayılmaz) | Açık yok: yeşil · sadece sarı/turuncu: sarı · kırmızı var: kırmızı |
| 3 | GÜVENLİK PUANI (100 ÜZERİNDEN) | Puan + **dairesel kadran** | 80+ yeşil · 50+ sarı/turuncu · altı kırmızı |
| 4 | GEÇEN SÜRE | `mm:ss`, taramayla birlikte akar | mavi |

**Puan formülü:** 100'den başlar; her **KRİTİK −15**, **UYARI −7**, **DİKKAT −3**; **GÜVENLİ ve ATLANDI düşürmez**; en düşük 0.

**Kadran (gauge):** 270°'lik açık yay (alt tarafı boş), iz rengi #1E293B, kalınlık 8 px, uçları yuvarlak; dolu kısım puana göre yeşil/turuncu/kırmızı, arkasında saydam "parlama" (glow); ortada puan (15 pt) ve altında "/100" (7,5 pt); değer her adımda %15 yaklaşan **yumuşak animasyonla** dolar (16 ms zamanlayıcı).

### 4.5 Sayaç şeridi ve rapor düğmeleri

- Solda üç neon sayaç: **YEŞİL (Normal): n · SARI/TURUNCU (Dikkat): n · KIRMIZI (Tehlike): n** (fare ipuçlarıyla anlamları açıklanır).
- Sağda **🌐 HTML Rapor İndir** ve **📄 PDF Rapor İndir** (kurumsal koyu düğmeler; "Farklı kaydet" penceresi açar).

### 4.6 Sonuç tablosu

Sütunlar: **Tehlike Seviyesi · Kontrol · Ne bulundu? · Ayrıntı · Ne Yapmalısın?**

- İlk sütunda GDI+ ile çizilen **yuvarlak köşeli rozet**; satırların tamamı koyu (#0F172A / #162032), seçili satır koyu siber mavi (#1E3A5F).
- Yazılar sütuna sığmazsa **kelime ortadan bölünmez**, alt satıra iner (sütun asgari genişlikleri + otomatik satır yüksekliği).
- **Sağ tık menüsü:** "Sonucu Kopyala" · "Tüm Raporu Masaüstüne Kaydet".
- Satıra tıklayınca sağdaki asistan paneli o sonucu açıklar.
- Çift tamponlu çizim (titreme ve silik kalıntı yok).

### 4.7 Canlı Sistem Günlüğü (siyah komuta ekranı)

Siyah zemin, Consolas 9 pt, her satırın başında soluk mavi-gri `[SS:dd:ss]` saat damgası. Satırlar **programın o an gerçekten yaptığı** işleri yazar (süs değil). Renkler: yeşil (hazır/başarı) · elektrik mavisi (bilgi/ilerleme) · sarı/turuncu (uyarı) · kırmızı (kritik/hata) · indigo (Claude).
Örnek akış:
```
[14:22:05] ⚡ BULUTSOFT MOTORU HAZIR -> Hedef: 127.0.0.1
[14:22:05] GÜVENLİK KİLİDİ -> 127.0.0.1 laboratuvar kapsamında, işlem devam ediyor.
[14:22:05] KAPI YOKLAMA BAŞLADI -> 17 kapı analiz ediliyor...
[14:22:06] KAPI YOKLAMA -> %50 (9/17)
[14:22:07] PORT 445 AÇIK -> SMB bulundu, servis kimliği (yanıt) okunuyor...
[14:22:07] UYARI -> Dosya paylaşımı (SMB) açık
[14:22:09] ⚡ OPERASYON TAMAMLANDI -> 19 sonuç, güvenlik puanı 69/100, süre 00:04
[14:22:09] GEÇMİŞ -> Operasyon kaydedildi (sol menü > Geçmiş Operasyonlar).
[14:22:09] KARŞILAŞTIRMA -> Önceki taramaya göre (…): 8 yeni sorun, 0 sorun kapanmış, puan -24.
```
En fazla 500 satır tutar (aşılırsa en eski 100 satır silinir).

### 4.8 Alt durum çubuğu

Solda mavi durum yazısı (örn. "Hazır. Hedefi kontrol edip "⚡ TARAMAYI BAŞLAT" butonuna basın."), sağda sarı kalın imza: **⚡ Geliştirici: BulutSoft Güvenlik Teknolojileri**.

---

## 5. Tarama Motoru (Moduller klasörü)

| Modül | Ne yapar? | Bulabildiği başlıca sonuçlar |
|---|---|---|
| **PortTarayici** | Eş zamanlı TCP bağlantı taraması (100 paralel, 1,5 sn); açık kapıdan 512 bayta kadar "servis kimliği" (banner) okur; web kapılarına `HEAD /` selamı gönderir | "Açık port N (Servis)" — Port N (Kapı) · Servis kimliği (yanıt) |
| **YapilandirmaDenetimi** | Açık kapılara göre zayıf ayar tespiti; FTP'ye `anonymous` ile deneme girişi | Telnet açık · FTP açık · **FTP kimliksiz girişe açık** · Çok eski SSH-1 · SSH açık · RDP/VNC ekran paylaşımı · **SMB/NetBIOS** · MySQL/PostgreSQL ağdan erişilebilir · Şifresiz web |
| **HttpDenetimi** | Web yanıt başlıkları ve çerezler | Eksik HSTS / CSP / X-Content-Type-Options / Referrer-Policy · Tıklama kandırmacası (X-Frame-Options) koruması yok · Sürüm sızıntısı (Server, X-Powered-By) · http şifresiz · Çerezde Secure / HttpOnly / SameSite eksik |
| **TlsDenetimi** | Sertifika ve protokol | Süresi bitmiş / 30 günden az kalmış · Öz-imzalı · Güvenilir değil · 2048 bitten zayıf RSA anahtarı · SHA-1 imza · **TLS 1.0/1.1 hâlâ kabul ediliyor** · kullanılan protokol bilgisi |
| **DnsDenetimi** | Ana kayıt + 15 yaygın alt isim (www, mail, ftp, admin, dev, test, staging, api, vpn, intranet, db, git, backup, old, portal) | admin/dev/test/staging/db/git/backup/old "hassas isim" → DİKKAT; diğerleri bilgi. **Lab dışı adrese çözülüyorsa durdurur.** |
| **GirdiDogrulamaTesti** | Kendi test sitenizin bir GET parametresine **zararsız işaretçi** yükler (sömürü yapmaz) | Yansıma (XSS belirtisi) · Şablon ifadesi değerlendirmesi (SSTI belirtisi) · Hata/bilgi sızıntısı · 5xx hata |
| **AgKesfi** | Ağ kartlarını bulur; her alt ağı ping + ARP ile tarar (bkz. 6) | Cihaz listesi: IP, tür, ad, MAC, ağ |
| **LabKapsam** | **Güvenlik kilidi** | 127.0.0.0/8, 10/8, 172.16/12, 192.168/16, yerel IPv6 dışını reddeder |

**Önem derecesi sistemi (4 kademe):** Bilgi → Düşük → Orta → Yüksek; ekranda rozet adları GÜVENLİ / DİKKAT / UYARI / KRİTİK.

---

## 6. Hedef Seç / Sanal Lab Bul ve Ağ Keşfi

**Amaç:** Her seferinde sanal makinede `ip a` yazıp IP aramamak.

**Menü:** 1) Yerel Bilgisayarım → kutuya 127.0.0.1 yazar. 2) Sanal Ağları Tara → pencere açar.

**Ağ tarama penceresi** (880×560, koyu tema; başlık "🖥️ Sanal Lab Bul — Ağdaki Cihazlar")
1. Bilgisayarın **açık ağ kartları** (`NetworkInterface`) bulunur; yalnızca özel adresli olanlar alınır. Kart türü adından tanınır: **VirtualBox Host-Only · VMware (VMnet) · Hyper-V · Wi-Fi (yerel ağ) · Ethernet (yerel ağ)**.
2. Her alt ağ (büyük ağlar /24'e daraltılır, en çok 254 adres) **ping** ile taranır (96 paralel, 450 ms).
3. **ARP tablosu** okunur: ping'e cevap vermeyen ama ağdaki cihazlar da yakalanır.
4. Ters isim sorgusu (1 sn) ile cihaz adı denenir.
5. **MAC önekinden cihaz türü** tahmin edilir: `08-00-27` VirtualBox VM · `0A-00-27` VirtualBox Host-Only adaptörü · `00-0C-29 / 00-50-56 / 00-05-69 / 00-1C-14` VMware · `00-15-5D` Hyper-V · `52-54-00` QEMU/KVM · `00-1C-42` Parallels.
6. Liste (sütunlar: IP Adresi · Cihaz Türü · Ad · MAC Adresi · Hangi Ağda?) sanal makineler önce olacak şekilde sıralanır; **Seç ve Hedef Adrese Yaz** (veya çift tıklama) IP'yi hedef kutusuna yazar. **🔄 Yeniden Tara**, **Kapat** düğmeleri vardır.
7. **Sanal makine bulunamazsa** (örn. VirtualBox "NAT" modunda kalmışsa) pencerede otomatik amber uyarı çıkar:
   > ⚠ Sanal makinenize ulaşılamadı. VirtualBox'ta Kali'ye sağ tıklayıp Ayarlar → Ağ bölümünden "Bağlı olduğu" kutusunu "Yalnızca Ana Makine (Host-Only)" veya "Köprü Bağdaştırıcısı (Bridged)" yapın. (Sanal makine tamamen açılmış olmalı. "NAT" modunda ana bilgisayar sanal makineyi göremez…)

**Etik not (pencerede yazar):** "Yalnızca KENDİ ağınızdaki ve kendi cihazlarınızdaki makineleri seçin; başkalarına ait cihazları izinsiz taramayın."

---

## 7. Siber İlk Yardım ve AI Çözüm Asistanı (sağ panel)

**Başlık:** ⚡ SİBER İLK YARDIM ve AI ÇÖZÜM ASİSTANI

### 7.1 Panel bölümleri (yukarıdan aşağıya)
1. **Mod seçici:** [🧠 Yerel & Manuel Mod (Ücretsiz)] [⚡ Claude API Modu] — seçili mod sarı (Claude seçiliyse indigo).
2. **Açıklama alanı** (renkli metin): `● ROZET  Bulgu başlığı` → ayrıntı → **📌 Bu Ne Demek?** → **🛠️ Nasıl Çözerim?**
3. **ÇÖZÜM KOMUTU** etiketi (ortama göre: "Windows (Yönetici olarak açılmış PowerShell)" veya "Linux (hedef bilgisayardaki terminal)" veya "ÖRNEK AYAR · Web sunucusu (nginx)") ve siyah **komut kutusu** (Consolas, yeşil).
4. **Moda özel eylem alanı** (aşağıda).
5. **🛡️ Otomatik Onar (Güvenlik Duvarı Kuralı Ekle)**
6. **📋 Çözüm Komutunu Kopyala** — **her iki modda da en altta sabit**.

### 7.2 🧠 Yerel & Manuel Mod (ücretsiz, internetsiz, **hiçbir işlev kaybolmaz**)
- **Yerel Uzman Motoru:** Programın içindeki Türkçe bilgi bankası, seçili satır için açıklama + hazır komut üretir (bkz. 7.4).
- Eylem alanı: *"Abonelik veya API gerekmez. Metni kopyalayıp ücretsiz bir yapay zeka sohbet ekranına yapıştırın."* + **📋 AI Danışman Metnini (Prompt) Hazırla ve Kopyala**.
  - Prompt; hedef, çalışan sistem sürümü (bilgisayar/kullanıcı adı **eklenmez**), tarih, profil, kapı sayısı, kontroller, açık portlar, sayılar, puan ve **tüm bulguları** içerir; sonunda 6 maddelik istek listesi (sade açıklama, risk sırası, Windows+Linux komutları, geri alma, yan etkiler, kontrol listesi) ve "savunma amaçlı denetim; saldırı talimatı verme" notu vardır.
  - Mesaj: *"Tarama verileriniz yapay zekaya sorulacak formatta kopyalandı! Aboneliğiniz olmasa bile herhangi bir ücretsiz yapay zeka sohbet ekranına (Ctrl+V ile) yapıştırarak detaylı çözüm alabilirsiniz."*

### 7.3 ⚡ Claude API Modu
- Eylem alanı: **🔑 API Ayarı** · ☐ **Tüm tarama özetini analiz et** · **🤖 Claude AI ile Derin Analiz Et**.
- **API Ayarı penceresi:** API anahtarı kutusu (nokta olarak görünür), model adı (varsayılan `claude-sonnet-5-5`), Kaydet / Anahtarı Sil / İptal. Anahtar **DPAPI ile şifreli** saklanır, ekranda asla gösterilmez; pencere "claude.ai sohbet aboneliği API erişimi sağlamaz; API için ayrı kredi gerekir" bilgisini verir.
- **Derin Analiz:** Seçili bulgu (veya işaretliyse tüm tarama) `https://api.anthropic.com/v1/messages` adresine `HttpClient` ile **canlı akış (streaming)** olarak gönderilir; cevap ekrana **harf harf** yazılır. Yerel açıklama yukarıda kalır; altına "━━━ 🤖 CLAUDE AI ANALİZİ ━━━" başlığıyla eklenir. Analiz sürerken düğme **⏹ Analizi Durdur** olur (en çok 3 dk).
- **Gizlilik onayı** (oturumda ilk gönderimde): Hangi bilgilerin gideceği (hedef IP, tarama özeti, bulgular, anahtar yalnızca kimlik doğrulama için) açıkça yazılır; bilgisayar ve kullanıcı adı gönderilmez; varsayılan seçenek "Hayır".
- **Claude'a verilen rol (sistem talimatı):** savunma odaklı danışman; sade Türkçe; saldırı/sömürü talimatı yok; düz metin (Markdown yok); Windows (yönetici PowerShell) ve Linux (sudo) komutları + geri alma komutu.
- **Çökmeyen yedek akış:** Anahtar yoksa, yanlışsa (401/403), kredi bitmişse, sınır aşılmışsa (429), sunucu yoğunsa (5xx), internet yoksa → sade Türkçe mesaj yazılır; anahtar/kredi sorunlarında program **kendiliğinden Yerel moda döner**, yukarıdaki açıklama ve komutlar ekranda kalır.

### 7.4 Yerel bilgi bankası (bulgu → sade açıklama + komut)

| Bulgu | "Bu ne demek?" özü | Hazır komut |
|---|---|---|
| Dosya paylaşımı (SMB) | Fidye yazılımlarının girdiği bilinen kapı; kullanılmıyorsa gereksiz | `netsh advfirewall firewall add rule name="BulutSoft-Blok-445" dir=in action=block protocol=TCP localport=445` + `Set-SmbServerConfiguration -EnableSMB1Protocol $false -Force` + geri alma |
| Telnet | Parola dahil her şey şifresiz gider ("kartpostalla parola yazmak gibi") | Port 23 engelleme komutu |
| FTP açık / FTP kimliksiz giriş | Şifresiz dosya aktarımı / kapıda kimlik sorulmuyor | Port 21 engelleme · vsftpd `anonymous_enable=NO` |
| SSH-1 / SSH açık | Eski sürüm zayıf / root girişi ve parola riski | `Protocol 2` · `PermitRootLogin no` (+ ssh yeniden başlat) |
| RDP / VNC | Ekrana uzaktan bağlantı, zayıf parola riski | Port engelleme |
| Veritabanı ağdan erişilebilir | Veritabanı yalnızca yerelde olmalı | Port engelleme |
| Genel "Açık port N" | Port normalde X için kullanılır (17 hizmet sözlüğü); gereksizse boşuna giriş yolu | Port engelleme (Windows: netsh · Linux: ufw) |
| Şifresiz web / http | Aradaki biri okuyabilir | nginx http→https yönlendirme örneği |
| Eksik koruma etiketi | Tarayıcıya "şu saldırı türüne karşı önlem al" denmiyor | `add_header <Etiket> "<değer>" always;` |
| Çerez bayrakları | Çerez çalınabilir/okunabilir | `Set-Cookie: …; Secure; HttpOnly; SameSite=Lax` |
| Sürüm sızıntısı | Saldırgan bilinen açığı arar | `server_tokens off;` |
| Eski TLS / sertifika sorunları | Kimlik kartı süresi dolmuş/zayıf | `ssl_protocols TLSv1.2 TLSv1.3;` · OpenSSL deneme sertifikası |
| Alt isim bulundu (admin, dev, test…) | Yönetim/deneme sistemleri dışarıdan görünmemeli | (açıklama; DNS kaydını silme önerisi) |
| Form kutusu testi bulguları | Girdi kontrolsüz işleniyor olabilir | (açıklama; HTML encode, parametreli sorgu) |
| ATLANDI | Kontrol çalışamadı — "güvenli" demek değildir | — |

*Komut ortamı otomatik seçilir: hedef bu bilgisayarsa **Windows** (netsh/PowerShell), uzak makine ise **Linux** (ufw/sed/systemctl). Tüm komutların altında **geri alma** komutu yazar.*

### 7.5 🛡️ Otomatik Onar (kontrollü sistem değişikliği)
- Yalnızca **bu bilgisayar** taranmışsa ve bulgu bir **port** ise aktif olur (aksi halde fare ipucu nedenini açıklar).
- Önce **Onay penceresi** çıkar: kural adı (`BulutSoft-Blok-PORT`), yön (gelen), işlem (**ENGELLE**), TCP port ve hizmet, yan etki uyarısı, geri alma komutu; **varsayılan düğme "Hayır"**.
- Onaylanırsa Windows **UAC (yönetici izni)** penceresi açılır; kullanıcı reddederse **hiçbir değişiklik yapılmaz**.
- Aynı adlı kural varsa tekrar eklenmez. Sonuç günlüğe ve mesaj kutusuna yazılır.

---

## 8. Geçmiş Operasyonlar

- **Otomatik kayıt:** Biten her tarama (hedef, tarih, profil, kapı sayısı, süre, puan, sayılar, tüm bulgular) `%AppData%\BulutSoft\gecmis\` altına JSON olarak yazılır; program kapanıp açılsa da silinmez; en çok **100 kayıt**.
- **Pencere (960×600):** "Önceki taramalarınız burada saklanır…" açıklaması; tablo sütunları **Tarih · Hedef · Profil · Puan (renkli) · Kritik · Uyarı/Dikkat · Güvenli · Süre**.
- **Karşılaştırma kutusu:** "Önceki taramaya göre (tarih): N yeni sorun, M sorun kapanmış, puan ±X." + **▲ Yeni çıkanlar** / **▼ Kapananlar** listesi + "Başlıca sorunlar".
- **Düğmeler:** 📂 Ekrana Yükle (tablo, kartlar, sayaçlar, süre, hedef ve asistan o kayıttan dolar) · 🗑 Seçileni Sil · Tümünü Sil (ikisi de onay ister, varsayılan "Hayır") · Kapat.

---

## 9. Raporlama

### 9.1 BulutSoft HTML raporu (renkli, tek dosya)
- **Üst başlık:** ⚡ **BulutSoft Güvenlik Denetim Raporu** (30 px, sarı) · alt başlık "Siber Güvenlik ve Ağ Denetim Merkezi · BulutSoft Security Suite v1.0" (mavi) · hedef, tarih, profil, süre.
- **Özet:** CSS ile çizilmiş **puan halkası** + kartlar: Taranan Kapı · Kritik · Uyarı/Dikkat · Güvenli · Atlanan Kontrol.
- **Bulgular tablosu:** Tehlike Seviyesi (rozet) · Kontrol · Ne Bulundu? · Ne Yapmalısın? — güvenli olmayan her bulgunun altında **📌 Bu ne demek?**, **🛠️ Nasıl çözerim?** ve koyu **komut kutusu**.
- **Sıralama:** KRİTİK → UYARI → DİKKAT → GÜVENLİ → ATLANDI.
- **Alt imza:** ⚡ **Bu analiz BulutSoft Otomasyon Motoru tarafından gerçekleştirilmiştir.**
- Yazdırma görünümünde beyaz zemin/siyah yazıya geçer.

### 9.2 PDF raporu
QuestPDF ile üretilir (Lato yazı tipi): başlık, marka satırı, hedef/tarih/profil/süre, puan ve sayılar, renkli seviye sütunlu tablo; alt bilgide aynı imza cümlesi ve sayfa numarası.

### 9.3 Kayıt yolları
- Üst düğmeler: "Farklı kaydet" penceresi.
- Sol menü "Profesyonel Rapor" / sağ tık: masaüstüne `BulutSoft_Rapor_YYYYAAGG_SSDDss.html` ve `.pdf`.

---

## 10. Kullanıcı Rehberliği (sıfır bilgiye göre tasarım)

| Mekanizma | Ayrıntı |
|---|---|
| **Hazır örnek değerler** | Tüm kutular dolu açılır (127.0.0.1 vb.) + silik örnek yazılar (placeholder) |
| **Nazik uyarılar** | Boş/hatalı kutu → "Bir eksik var" penceresi; program çökmez |
| **Fare ipuçları** | **37** fare balonu (düğmelerin, kutuların, kartların, sayaçların ne işe yaradığı; günlük hayat benzetmeleriyle) |
| **ⓘ simgeleri** | Hedef adres, Profil, Kapı numaraları, Web adresi, Kutucuk adı, Alan adı — üstüne gelince ipucu, tıklayınca büyük pencere |
| **Tanıtım turu** | İlk açılışta bir kez (sonra sol menüden): ① Hedefi yazın ② Taramayı başlatın ③ Sonuçları okuyun; Geri/İleri/Başla ⚡, nokta göstergesi (● ○ ○), 620×340 pencere |
| **Yardım penceresi** | "Bu program ne işe yarar? / Nasıl kullanırım? (5 adım) / Güvenlik" |
| **Renk dili** | Trafik lambası: yeşil normal · sarı/turuncu dikkat · kırmızı tehlike · gri atlandı |

---

## 11. Güvenlik ve Etik İlkeleri (programın tasarım sözleşmesi)

1. **Lab kilidi:** Tüm modüller yalnızca loopback ve özel (RFC1918) adreslerde çalışır; halka açık adres reddedilir.
2. **Zarar vermez:** Sömürü (exploit) yok; girdi testi zararsız işaretçi gönderir; FTP testi yalnızca `anonymous` girişi dener.
3. **Kullanıcı onayı:** Sistemi değiştiren (Otomatik Onar) ve veri gönderen (Claude) her işlem **açık onay** ister; varsayılan buton "Hayır".
4. **Gizlilik:** API anahtarı Windows hesabına özel şifreli saklanır, ekranda gösterilmez; Claude'a bilgisayar/kullanıcı adı gönderilmez; prompt'a da eklenmez.
5. **Silme güvenliği:** Geçmiş kayıtlarını silme onay ister.
6. **Hata toleransı:** Hiçbir modül hatası programı çökertmez; yapay zeka erişilemezse yerel mod devralır.
7. **Savunma odaklı yapay zeka:** Claude'a verilen rol yalnızca kapatma/koruma önerisi ister.

---

## 12. Doğrulanan ve Doğrulanmayan Özellikler (dürüst durum)

**Test edilenler (gerçek çalıştırma + ekran görüntüsü):** derleme (0 uyarı, 0 hata) · 127.0.0.1 taraması (17 kapı, 19 sonuç, puan 69) · profil geçişleri · tablo rozetleri ve asistan paneli · anahtarsız Claude modu → Yerel moda dönüş · prompt kopyalama (4.573 karakter) · ağ kartı algılama (VirtualBox Host-Only + Ethernet) ve ağ tarama penceresi (5-6 cihaz) · sanal makine yokken uyarı · geçmiş kaydetme ve karşılaştırma · HTML rapor görünümü · PDF üretimi.

**Henüz doğrulanmayanlar:** Claude'a **gerçek** API isteği (anahtar yok) · 🛡️ Otomatik Onar'ın gerçek kural eklemesi (bilerek çalıştırılmadı) · çalışan bir Kali VM ile gerçek seçim · geçmişten "Ekrana Yükle" adımı (ekranda) · PDF'in görsel incelemesi · Kapsamlı profilin uzun taraması.

**Bilinen sınırlar:** Bilgi bankası elle derlenmiş küçük bir listedir (resmi zafiyet veritabanı değildir); banner sürümüne bakmak yanıltıcı olabilir; Nessus gibi binlerce kontrollü bir tarayıcı değildir — gücü **sadelik, rehberlik ve kapatma komutları**ndadır. Ağ keşfi yalnızca yerel alt ağları tarar.

---

## 13. Sunum İçin Önerilen Slayt Akışı

| # | Slayt | Ana mesaj | Kaynak bölüm |
|---:|---|---|---|
| 1 | Kapak | ⚡ BulutSoft Security Suite v1.0 — "Evinizin kilitlerini yoklayan akıllı bekçi" | 1 |
| 2 | Problem | Güvenlik açıklarını bulmak zor, açıklamaları anlaşılmaz | 1 |
| 3 | Çözüm / Vizyon | Tara → Anla → Çöz → Raporla; sıfır bilgiyle bile kullanılır | 1, 10 |
| 4 | Ekran turu | Komuta merkezi ekranı (yerleşim çizimi/ekran görüntüsü) | 3.5, 4 |
| 5 | 4 adımda kullanım | Hedef → Profil → ⚡ TARAMAYI BAŞLAT → Sonuçlar | 4.2, 2.4 |
| 6 | Tarama motoru | 7 modül, ne bulur | 5 |
| 7 | Trafik lambası dili | Rozetler, puan formülü, kadran | 3.1, 4.4 |
| 8 | Sanal lab keşfi | Kali'yi otomatik bul, NAT uyarısı | 6 |
| 9 | Siber İlk Yardım Asistanı | Bu ne demek? Nasıl çözerim? Hazır komut | 7.1, 7.4 |
| 10 | İki modlu yapay zeka | Ücretsiz yerel mod ↔ Claude akışlı analiz; asla çökmez | 7.2, 7.3 |
| 11 | Otomatik Onar | Onaylı, geri alınabilir güvenlik duvarı kuralı | 7.5 |
| 12 | Canlı günlük + kartlar | Gerçek zamanlı operasyon akışı | 4.4, 4.7 |
| 13 | Geçmiş ve karşılaştırma | "2 sorun kapandı, puan +12" | 8 |
| 14 | Profesyonel rapor | HTML/PDF, imza | 9 |
| 15 | Tasarım sistemi | Renk paleti, yazı tipleri | 3 |
| 16 | Güvenlik ilkeleri | Lab kilidi, onaylar, şifreli anahtar | 11 |
| 17 | Mimari | Katmanlı yapı, 31 dosya, 4.611 satır | 2 |
| 18 | Yol haritası / Sonraki adımlar | Radar/ağ haritası, düzelt-doğrula döngüsü, tarama karşılaştırma grafiği | — |
| 19 | Kapanış | BulutSoft Güvenlik Teknolojileri | 1 |

---

## 14. Ek: Arayüzdeki Önemli Metinlerin Tam Listesi

**Düğmeler:** ⚡ TARAMAYI BAŞLAT · Durdur · Gelişmiş ▾ / ▴ · 🖥️ Hedef Seç / Sanal Lab Bul ▾ · Hızlı · Standart · Kapsamlı · 🌐 HTML Rapor İndir · 📄 PDF Rapor İndir · 🧠 Yerel & Manuel Mod (Ücretsiz) · ⚡ Claude API Modu · 🔑 API Ayarı · 🤖 Claude AI ile Derin Analiz Et (⏹ Analizi Durdur) · 📋 AI Danışman Metnini (Prompt) Hazırla ve Kopyala · 🛡️ Otomatik Onar (Güvenlik Duvarı Kuralı Ekle) · 📋 Çözüm Komutunu Kopyala · ✔ Seç ve Hedef Adrese Yaz · 🔄 Yeniden Tara · 📂 Ekrana Yükle · 🗑 Seçileni Sil · Tümünü Sil · Kaydet · Anahtarı Sil · Turu Kapat · ◀ Geri · İleri ▶ · Başla ⚡

**Başlıklar:** ⚡ SİBER İLK YARDIM ve AI ÇÖZÜM ASİSTANI · ⚡ CANLI SİSTEM GÜNLÜĞÜ · ÇÖZÜM KOMUTU · TARANAN KAPI SAYISI · BULUNAN AÇIKLAR · GÜVENLİK PUANI (100 ÜZERİNDEN) · GEÇEN SÜRE · SONUÇLARI NASIL OKURUM?

**Özet kutuları:** YEŞİL (Normal): n · SARI/TURUNCU (Dikkat): n · KIRMIZI (Tehlike): n

**Tablo başlıkları:** Tehlike Seviyesi · Kontrol · Ne bulundu? · Ayrıntı · Ne Yapmalısın?

**Rozetler:** GÜVENLİ · DİKKAT · UYARI · KRİTİK · ATLANDI

**Pencere başlıkları:** ⚡ BulutSoft | Siber Güvenlik ve Ağ Denetim Merkezi · ⚡ Tanıtım Turu · 🖥️ Sanal Lab Bul — Ağdaki Cihazlar · ◷ Geçmiş Operasyonlar · 🔑 API Ayarı · Yardım · Gönderim Onayı · Otomatik Onar - Onay

**Dil ve ton kuralları:** Tamamen Türkçe; teknik terim kullanılırsa parantez içinde günlük hayattan benzetme (kapı=port, sertifika=kimlik kartı, DNS=telefon rehberi, banner=servis kimliği); "Servis kimliği (yanıt) okunamadı", "Port 445 (Kapı)" gibi hem sade hem profesyonel ifadeler.

---

*Belge sürümü: 1 · Program: BulutSoft Security Suite v1.0 · Hazırlayan: BulutSoft Güvenlik Teknolojileri geliştirme ekibi*
