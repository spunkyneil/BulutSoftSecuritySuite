# BulutSoft Security Suite — Bağımsız Denetim Raporu

Hazırlanma tarihi: 22.11.2026 · Sürüm: 1.5.3 · Kapsam: kaynak kod, proje dosyaları, kurulum ve yayın betikleri
Yöntem: Klasör ve kaynak dosyalar doğrudan taranıp sayılarla ölçüldü (Desktop Commander eklentisi bu oturumda yok; dosya okuma ve arama araçları kullanıldı). Figma bağlantısı verilmediği için Figma dosyası incelenmedi. Programı bu rapor için baştan sona elle gezmedim; ekran bilgileri kod ve menü tanımlarından çıkarıldı.
Her sayı bu taramada ölçülmüştür. Ölçmediğim yerde "doğrulanmadı" yazdım.

---

## 1. Proje Özeti ve Hedef Kitle

**Ne:** Windows masaüstü bir ağ ve uç nokta savunma platformu (BulutSoft Security Suite). Yerel bilgisayarı ve ağı denetler (açık kapı, zayıf ayar, TLS/DNS/HTTP, paylaşımlar), bulguları puanlar ve rapor üretir; ayrıca canlı koruma sağlar: Nöbetçi modu, fidye yazılımı tuzakları, USB kalkanı, olay zaman çizgisi, uzak SSH sensörleri ve Kali araç entegrasyonu.

**Çözdüğü kurumsal problem:** Küçük ve orta ölçekli kurumlarda güvenlik uzmanı olmadan temel denetimi, bulguları sade dille anlatmayı ve yönetime/KVKK denetçisine sunulabilir resmi rapor üretmeyi tek yerde toplamak. Uzman ekip için de ham tarama, trafik ve araç kutusu sunar.

**Hedef kitle (kod yapısından çıkarım):**
- **Kolay Görünüm:** Teknik olmayan işletme sahibi veya yönetici. Üç büyük eylem kartı, konuşan ilerleme, trafik ışığı karnesi.
- **Uzman Görünüm:** BT/güvenlik sorumlusu. Tablo, radar, trafik, araç kutusu, sensörler.
- **Rol bazlı yetki:** İzleyici, Analist, Yönetici.
- **Ticari model:** Kurumsal B2B, imzalı lisans dosyası, Inno Setup ile kurulum.

---

## 2. Gerçek Klasör Ağacı ve Teknoloji Yığını

```
C:\LabGuvenlikDenetim
├─ LabDenetim\                  Ana uygulama (WinForms), 153 .cs dosyası, 28.387 satır
│  ├─ (kök)  135 .cs            Form1 parçaları, mantık sınıfları, formlar
│  ├─ Efektler\                 EfektliButon, PencereEfekti
│  ├─ Kontroller\               CanliTaramaKontrolu, Karsilama (açılış/giriş zemini), TemaliMesaj
│  ├─ Koruma\                   KanitImzasi, KorumaHizmeti, SurecYazmaSayaci
│  ├─ Moduller\                 AgKesfi, Dns/Http/Tls/Paylasim/Sistem/Yapilandirma denetimi, PortTarayici, TrafikIzleyici
│  ├─ Kure\                     kure.html + d3.min.js + topojson + countries-110m.json (çevrimdışı küre)
│  ├─ *.json (gömülü)           pasif_kurallar, aktif_kurallar, cpe_esleme, kali_araclari
│  └─ app.ico, Pilot_Rehberi.md, Sunum_*.md/html, Tasarim_Bulgu_Semasi_ve_Moduller.md
├─ LabDenetim.Tests\            xUnit, 61 dosya, 7.500 satır (son çalıştırma: 1327 test geçti)
├─ BulutSoftGuardService\       Windows hizmeti, 6 dosya, 469 satır (motorlar ana projeden gelir)
├─ Kurucular\                   WebView2 çevrimdışı kurucu
├─ Output\                      Üretilen kurulum paketleri + SHA256SUMS.txt
├─ yayin\                       Self-contained yayın çıktısı (kurulumsuz çalıştırılabilir)
├─ BulutSoft.iss, Yayinla.ps1, Guncelle.ps1, HizmetKur/Kaldir.ps1
└─ *.md                         DAGITIM, DESTEK, PAKETLER, HUKUKCU_BILGI_NOTU, DEGISIKLIKLER, PROGRAM_DURUM_OZETI
```

**Teknoloji:**
- **Dil/çatı:** C#, .NET 10 (`net10.0-windows`), Windows Forms, `Nullable` açık.
- **Arayüz çizimi:** Çoğunlukla GDI+ ile özel çizilen kontroller (EylemKarti, RadarPaneli, SiberKure, PuanHalkasi, DurumGostergesi, ModAnahtari). Hazır UI kütüphanesi yok.
- **Küre:** WebView2 içinde HTML5 canvas + d3 (yerel dosya, internet gerektirmez).
- **Paketler (3 adet, sürümler sabit):** Microsoft.Web.WebView2 1.0.4314.35, QuestPDF 2026.9.2 (PDF rapor), System.Management 10.0.0.
- **Hizmet:** Microsoft.Extensions.Hosting.WindowsServices + Microsoft.Diagnostics.Tracing.TraceEvent (ETW ile yazma kaynağı).
- **Dağıtım:** Inno Setup (yönetici hakkı), PowerShell yayın hattı (önce tüm testler, geçmezse durur).
- **Kalıcı veri:** Veritabanı yok; `%AppData%\BulutSoft` ve `ProgramData\BulutSoft` altında 36 JSON/metin dosyası.
- **Mimari desen:** MVVM/DI yok. `Form1` 42 `partial` dosyaya bölünmüş (God-form). 139 `static class` ile mantık modülleri. Olay (event) ve `Timer` tabanlı güncelleme.

**Bağımlılık yönü:** Hizmet projesi ana arayüz projesine `ProjectReference` ile bağlı. Yani hizmet, WinForms derlemesini de taşıyor.

---

## 3. Ekranlar ve Kullanıcı Akışı

**Açılıştan çıkışa:**
1. `Main`: komut satırı (`--tara`, `--yardim`) arayüzsüz çalışır ve çıkış kodu verir.
2. Genel hata yakalayıcılar kurulur → tek kopya kilidi (Mutex) → tema başlar → olay günlüğü hizmete bağlanır.
3. `AcilisFormu` (5 sn açılış ekranı; "Kullanıcı Değiştir" ile yeniden girişte atlanır).
4. Kimlik durumu: **Yok** → ilk kurulum, **Hazır** → giriş, **Bozuk** → güvenlik nedeniyle açılmaz (kurtarma yönergesi gösterilir).
5. `GirisFormu` (ilk kurulumda kurulum parolası/sorusu).
6. `Form1` ana pencere → mod anahtarı **Kolay Görünüm | Uzman Görünüm** → çıkış.

**Kolay Görünüm:** Karşılama metni, 3 eylem kartı ("Bilgisayarımı ve Şirketimi Kontrol Et", "Bir Web Sitesini İncele ve Sahibine Bildir", "Durum Karnesi ve Resmi Rapor Al"), canlı küre + koruma kalkanı + durum bandı, 4 durum göstergesi (Kali, IP sızıntı, bildirim vb.), konuşan ilerleme ekranı, trafik ışığı karnesi, site sahibine mektup penceresi (`MektupFormu`).

**Uzman Görünüm — sol menü (24 öğe):** Panel, Yeni Tarama, Geçmiş Operasyonlar, Profesyonel Rapor, Yardım, Tanıtım Turu, Hakkımızda ve Destek, Canlı Ağ Trafiği, Aktif Kalkanlarım, Nöbetçi Modu, Acil İzolasyon, Ağ Cihaz Envanteri, Lisans, Kalıcılık Denetçisi, Ağ Haritası, Gözetimsiz Politika, Sensör ve Bildirim Merkezi, Giriş Saldırı Radarı, Tema, Kullanıcı Değiştir, Hesabım ve Kullanıcılar, Uzak Sensörler (VM), Güvenlik Araç Kutusu, Durum Panosu, Olay Zaman Çizgisi.

**Ana alan görünümleri:** ◼ Panel (**Klasik radar | Harita küresi**), ⚡ Yeni Tarama (sonuç tablosu + Asistan), ◈ Canlı Ağ Trafiği.

**Yardımcı pencereler (sınıf olarak 45'e yakın):** AgFormu, AgHaritasiFormu, AracKutusuFormu, BruteForceFormu, BulguSonucFormu, CihazFormu, DurumFormu, GecmisFormu, GozetimsizFormu, HakkindaFormu, KaliciFormu, KalkanFormu, KomutPaleti, ZamanlamaFormu, BasarilarFormu, KullaniciFormu, LisansFormu, MerkezFormu (Kali Araç Rehberi), OlayFormu, SahiplikFormu, SensorFormu, SurecAvciFormu, TurFormu, TuzakFormu, UsbBildirimFormu, ApiAyarFormu, DestekFormu, TemaliMesaj penceresi.

**Yetki matrisi:** `Rol` (İzleyici=0, Analist=1, Yönetici=2) × `Eylem` (Gorme, Rapor, Tarama, Arac, Koruma, SistemDegistir, Yonetim). Menü ve düğmeler `Yetki.Izinli` ile kapılanır.

---

## 4. Mevcut UI/UX ve Tasarım Envanteri (Figma Hazırlığı)

**Genel durum:** Karma. Renkler merkezi bir palet dosyasında tanımlı (`Palet.cs`), ama kodun içinde de çok sayıda sabit renk var; çalışma anında bir eşleyici bunları temaya çeviriyor.

| Ölçü | Sayı |
|---|---|
| `Color.FromArgb(...)` sabit renk kullanımı | 757 |
| `ColorTranslator.FromHtml(...)` | 36 (çoğu Palet.cs içinde) |
| `TemaYonetici.C(...)` ile tema eşlemesi | 41 |
| `new Font(...)` | 184 |
| `new Padding(...)` | 203 |
| Sabit piksel `new Size(...)` | 189 |

**Tema mimarisi:** Koyu tema varsayılan ve ana tasarım. Açık tema, koyu renklerin rol tablosuyla eşlenmesi veya yedek olarak HSL açıklığı ters çevrilmesiyle üretiliyor (`TemaYonetici` + `Palet`). `TemaYonetici` her 15 ms'de açık pencereleri tarayıp boyar. Tercih `tema.json` içinde.

**Merkezi palet (Palet.cs, 32 rol; HEX):**

| Rol | Koyu | Açık |
|---|---|---|
| Sayfa | #141B2E | #F0F4F8 |
| Kart | #1C2438 | #FFFFFF |
| Girdi | #161D30 | #FFFFFF |
| Gömülü | #0F1522 | #EEF2F7 |
| Kenar | #2A3650 | #E2E8F0 |
| Seçim | #222D4A | #DBEAFE |
| Metin | #E8ECF4 | #0F172A |
| İkincil metin | #A0B0C4 | #64748B |
| Soluk | #8896AB | #475569 |
| Birincil (solid / metin) | #6366F1 / #818CF8 | #6366F1 / #4F46E5 |
| Başarı (solid / metin / arka) | #059669 / #10B981 / #1A3D2E | #059669 / #059669 / #F0FDF4 |
| Uyarı (solid / metin / arka) | #F59E0B / #F59E0B / #3D3218 | #F59E0B / #B45309 / #FEF3C7 |
| Tehlike (solid / metin / arka) | #EF4444 / #EF4444 / #3D1A1A | #EF4444 / #DC2626 / #FEF2F2 |
| Bilgi (solid / metin / arka) | #06B6D4 / #06B6D4 / #1A2D48 | #06B6D4 / #0891B2 / #DBEAFE |

**Kodda en çok kullanılan sabit renkler (tekrar sayısıyla):** #1E293B (63), #4ADE80 (50), #F87171 (48), #E0F2FE (46), #94A3B8 (36), #38BDF8 (36), #FBBF24 (35), #0F172A (34), #0C4A6E (25), #E2E8F0 (23), #1E3A5F (22), #0D1117 (19), #E6EDF3 (17), #F59E0B (16), #080D18 (16). Bunların bir kısmı Palet'te rolü olmayan ikinci bir "tailwind benzeri" skala; Figma'da token olarak birleştirilmeli.

**Küre sayfası (kure.html) ayrı bir palete sahip:** Kendi CSS değişkenleri (#141B2E, #F0F4F8, #8896AB…) ve durum renkleri (Güvende #10B981/#059669, Dikkat #F59E0B/#D97706, Kritik #EF4444/#DC2626, Pasif #8896AB/#64748B). C# paletiyle senkron olduğunu gösteren otomatik bir mekanizma yok; elle eşleşiyor.

**Tipografi:**
- Aile: Segoe UI (157 kullanım), Consolas (19, kod/IP), Segoe UI Symbol (4), Segoe UI Semibold (2), Segoe UI Emoji (1).
- Boyutlar (kullanım): 9,5 pt (50), 9 pt (30), 10 pt (25), 10,5 pt (15), 11 pt (13), 8,5 pt (8), 13/14/15 pt (5'er), 8 pt (4), 12,5/12/18/20 pt (2–3'er).
- Merkezi bir tip ölçeği (başlık/gövde/etiket) yok; boyutlar yerinde yazılmış.

**Boşluk ve yerleşim:** Docked `TableLayoutPanel`/`FlowLayoutPanel` + yerinde yazılmış piksel değerleri. Örnek: Kolay kart 88 piksel yükseklik, kart başlığı 11–13 pt otomatik sığdırma, kart köşe yarıçapı 18, kenarlık 1,5 / odakta 2,5 px. Merkezi bir boşluk ölçeği (4/8/16) yok.

**Bileşenler:** `EfektliButon`, `TemaliMesaj` (tüm bilgi/uyarı penceresi; 175 çağrı, yalnızca 7 doğrudan `MessageBox.Show`), `EylemKarti`, `DurumGostergesi`, `PuanHalkasi`, `ModAnahtari`, `MarkaPaneli`, `RadarPaneli`, `SiberKure` (GDI+ nokta matris küre), `KureWebPaneli` (WebView2).

**İkonografi:** Menüde Unicode glifler (◼ ⚡ ◷ ▤ ◈ …), kartlarda koddan çizilen vektör simgeler, `app.ico`. SVG/PNG varlık klasörü yok.

**Figma için not:** Rol tablosu (Palet) token kaynağı olarak kullanılabilir. Sabit renkler önce rollere eşlenmeli, yoksa Figma'da iki ayrı renk sistemi oluşur.

---

## 5. Jeneriklik Durumu (White-label Hazırlığı)

**Kodun içine gömülü (dışarıdan yönetilemez):**
- Marka adı "BulutSoft": 62 kaynak dosyada, 181 geçiş. Derleme adı `BulutSoftSecuritySuite`, şirket adı `BulutSoft Güvenlik Teknolojileri` (csproj), `%AppData%\BulutSoft` ve `ProgramData\BulutSoft` klasörleri, `HKLM\SOFTWARE\BulutSoft`, hizmet adı `BulutSoftGuard`, kurulum adı ve klasörü (`.iss`).
- Logo: `app.ico` gömülü kaynak + `MarkaPaneli` içinde koddan çizilen marka alanı. Dışarıdan değiştirilebilen logo dosyası yok.
- Arayüz metinleri: Türkçe ve kod içinde; `.resx` / yerelleştirme altyapısı yok (0 dosya). Çok dilli sürüm için tüm metinlerin taşınması gerekir.
- İş kuralları (rol-eylem matrisi, puanlama, bulgu önem eşikleri, kilit süreleri, olay sınıflandırma) kodda sabit.

**Dışarıdan yönetilebilen:**
- Müşteri adı ve lisans bilgisi: imzalı lisans dosyası (`lisans.bslic`, ECDSA, çevrimdışı doğrulama; kurum lisansında makine "*").
- Denetim kuralları: `pasif_kurallar.json`, `aktif_kurallar.json`, `cpe_esleme.json` gömülü varsayılan + `%AppData%\BulutSoft` altında ekleme/değiştirme/kapatma. `kali_araclari.json` gömülü ve kullanıcı tarafından geçersiz kılınamaz (bilinçli güvenlik kararı).
- Bildirim kanalları, gözetimsiz politika, izinli hedefler, parola politikası, tema, arayüz modu: ayar dosyaları.
- Veritabanı: yok; tüm durum yerel dosyada. Çok kullanıcılı/merkezi yönetim (sunucu, veritabanı) yok.

**Sonuç:** Başka bir kuruma özel marka ve dil için bugün yaklaşık 60+ dosyada ve kurulum betiklerinde değişiklik gerekir. Müşteriye özel kural ve lisans kısmı ise dışarıdan yönetilebiliyor.

---

## 6. Kritik Fonksiyonlar ve Hata Yönetimi

**Kimlik ve yetki:**
- Parolalar PBKDF2-SHA256 (600.000 tur) ile özetlenir; düz yazı yok.
- 5 hatalı denemeden sonra kilit: 30 sn, katlanarak en çok 15 dk; olaylar günlüğe yazılır.
- `kimlik.json` dosyası bütünlük mührü taşır; koruma hizmeti varsa mühür HKLM'de ayrıca tutulur (dosyayı geri alma/saldırı tespiti). Bozuk kimlik dosyasında program güvenlik gereği açılmaz.
- Roller `Yetki.Izinli(rol, eylem)` ile kapılanır. Yetkisiz eylem engellenir ve açıklanır.

**Olay günlüğü (denetim izi):** JSON satırları, zincirli HMAC (araya girme/silme/sıra değiştirme bozulma olarak görünür), 2 MB'ı geçince arşive taşınır (silinmez), Windows Olay Günlüğü'ne ayna, Kritik olayda webhook. Koruma hizmeti çalışıyorsa tek yazar odur; ulaşılamazsa arayüz yerel dosyaya yazar (olay kaybolmaz).

**Koruma hizmeti (BulutSoftGuard):** Adlandırılmış boru (named pipe) ile arayüzle konuşur, ETW ile yazma kaynağını doğrular, anahtarlar HKLM altında DPAPI (LocalMachine) + yalnızca SYSTEM/Yöneticiler ACL'i ile saklanır. Arayüz kapalıyken de fidye tuzağı/nöbetçi çalışır.

**Gizli bilgi:** Claude API anahtarı DPAPI (CurrentUser) ile şifrelenir. Dış çağrılar yalnızca kullanıcı isterse yapılır: Anthropic API ("Derin Analiz"), CISA KEV listesi, ipify/ipwho.is/Cloudflare (IP sızıntı denetimi), pwnedpasswords, Discord/Telegram webhook, zaman damgası sunucuları. Küre ve kurallar tamamen yerel.

**Hata davranışı:**
- Üç kanaldan genel yakalayıcı (arayüz iş parçacığı, arka plan, ilgilenilmeyen görev): olay günlüğüne Kritik kayıt + `%AppData%\BulutSoft\cokme_*.txt`, kullanıcıya en çok 10 sn'de bir anlaşılır pencere, program çalışmaya devam eder.
- Dosya yazımı: geçici dosya + taşıma (`Depo.YazJson`, 20 kullanım) ve mühür; yarım yazma bozulmaya yol açmaz.
- Kesinti senaryoları: Hizmet kapalı → yerel yazma + "koruma yalnızca program açıkken" bandı. WebView2 yoksa küre paneli boş kalır, Uzman Görünümde klasik radara otomatik dönülür. Ağ yoksa dış sorgular başarısız olur; yerel denetimler sürer (doğrulanmadı: her dış çağrının zaman aşımı değerleri tek tek incelenmedi).
- Tek kopya kilidi, ayar dosyalarının çift yazımla bozulmasını önler.

---

## 7. Teknik Borç ve Zayıf Noktalar

Önem sırasıyla:

1. **Kaynak kontrolü yok.** Klasör git deposu değil; tek geri dönüş noktası el ile alınan zip (`BulutSoft_Kaynak_20261121.zip`). Ekip çalışması, sürüm geçmişi ve CI için en acil iş. (Yayın hattı yerel PowerShell; sunucuda CI yok.)
2. **God-form.** `Form1` 42 `partial` dosyaya dağılmış, tek sınıf; `Form1.Designer.cs` 850 satır. En büyük dosya `SensorMantigi.cs` (1.104 satır). Mantık ve arayüz iç içe; büyüdükçe değişiklik riski artar. 139 `static class` ve genel durum (örn. `Oturum.Aktif`, `OlayDeposu`) birim izolasyonunu zorlaştırıyor (yine de 1327 test var).
3. **Sessiz hata yutma.** 528 `catch`ın 226'sı boş (`catch { }`). Bazıları bilinçli ("günlük programı bozmasın"), ama bozulan bir kayıt veya başarısız yazma kullanıcıya hiç görünmeyebilir. Hangileri bilinçli olanlar ayrıştırılmalı.
4. **13 `async void` ve 20 `Timer`.** Özellikle `TemaYonetici` her 15 ms'de tüm açık pencereleri tarıyor (sürekli CPU ve olay yükü). `async void` içindeki istisnalar genel yakalayıcıya düşer.
5. **Renk/tema çift sistemi.** 757 sabit renk + çalışma anı eşleyici + ayrı JS paleti. Yeni tema/marka eklemek veya yüksek karşıtlık modu yapmak zor; eşleyiciye yeni renk eklemek unutulursa açık temada okunmaz renk çıkar.
6. **Ölçeklenme.** Olay günlüğü her okumada tüm dosyayı JSON çözüyor (`OlayDeposu.Oku`); Uzman küresi bunu 8 saniyede bir arka planda çağırıyor. Küçük dosyada sorun yok; birkaç MB arşivli günlükte yük artar. Veritabanı yok, çok istemcili merkezi görünüm yok.
7. **Yerelleştirme ve beyaz etiket yok** (bölüm 5). Marka ve Türkçe metin 60+ dosyaya dağılmış.
8. **Sabit piksel yerleşim (189 yer).** Yüksek DPI ve çok küçük pencerelerde taşma riski; kart başlığı sığmaması bunun bir örneğiydi. Merkezi boşluk/tip ölçeği yok.
9. **Test bağımlılığı.** Test paketinde en az bir test makinedeki gerçek bir Windows hizmetine bağlı (`Adim2GercekTestleri`); yayın sırasında bir kez rastgele başarısız oldu ve yeniden çalıştırınca geçti. CI ortamında kararsız olabilir.
10. **Hizmet–arayüz bağı.** Hizmet projesi WinForms ana projesine referans veriyor; hizmetin bağımsız, küçük bir çekirdek kütüphaneye dayanması daha temiz olur.
11. **Küre performansı.** Çok sayıda ülke yolu her karede çiziliyor (WebView2 canvas). Bu oturumda ~2,5 kat hızlandırıldı; zayıf ekran kartlarında hâlâ ölçülmeli.
12. **Dağıtım.** Kurulum paketi ~325 MB (WebView2 çevrimdışı kurucusu dahil). İmzalama isteğe bağlı; sertifika verilmezse kurulum/exe imzasız çıkar (SmartScreen uyarısı).

**Büyüdüğünde patlayacak yerler (özet):** tek form sınıfı, boş `catch` blokları, tam dosya okuyan olay günlüğü, tema eşleyicisi, kaynak kontrolsüz dağıtım.

---

## Ek: Sayısal Özet

| Kalem | Değer |
|---|---|
| Ana uygulama | 153 .cs, 28.387 satır |
| Testler | 61 dosya, 7.500 satır, 1327 test (son çalıştırmada hepsi geçti) |
| Hizmet | 6 dosya, 469 satır |
| Yorum satırı oranı | yaklaşık %30,5 (satır başı `//`) |
| `static class` | 139 |
| Boş `catch { }` / toplam `catch` | 226 / 528 |
| `async void` | 13 |
| `Timer` | 20 |
| `Task.Run` | 43 |
| `TemaliMesaj.Goster` / `MessageBox.Show` | 175 / 7 |
| Kalıcı veri dosyası türü | 36 |
