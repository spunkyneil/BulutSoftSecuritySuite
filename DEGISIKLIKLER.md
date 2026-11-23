# Değişiklik günlüğü

## 1.6.0 (23.11.2026) — Dayanıklılık, tek sayfa kabuğu ve tema düzeltmeleri (testler 1516 başarılı)
- Ana pencere sadeleştirildi: Form1 ince kabuk; nöbetçi, kısayol, kolay mod ve küre mantığı ayrı sınıflara taşındı. 13 eski ekran artık kabuğun içinde sayfa olarak açılır.
- Tek tema kaynağı (DesignTokens); açık/koyu tema geçişinde kalan beyaz ekran hatası giderildi.
- Kullanıcı menüsüne "Programdan Çıkış" eklendi; oturum olayları Olay Zaman Çizelgesi'nden çıkarıldı, her kullanıcının etkinlik günlüğü Kullanıcılar ekranında görünür.
- Uzman görünümündeki "Demo Yayları" düğmesi kaldırıldı.
- Servis sınırı tüm programa genişletildi; kaynak kuralı testleri eklendi. Yayın betiği (Yayinla.ps1) iki paket (Hafif/Çevrimdışı) ve SHA256 özetleri üretir.

## 1.5.3 (22.11.2026) — Küre titremesi (ikinci düzeltme) ve Kolay kart başlıkları (testler 1327 başarılı)
- Küre: ülke sınırlarındaki uzun kenarlar yüklemede bir kez sıklaştırılır, her karedeki uyarlamalı örnekleme kapatıldı (kare başına çizim süresi yaklaşık 2,5 kat azaldı); ülke adları tam piksele oturtulur.
- Kolay Görünüm: üç kartın başlığı tek satıra sığar; yazı boyutu en uzun başlığa göre ortak seçilir (13→11 punto), kartlar biraz genişledi.

## 1.5.2 (22.11.2026) — Küre titremesi azaltıldı, çok daha fazla zıplayan ışık (testler 1327 başarılı)
- Hız: sıradan ülkeler tek yolda çizilir, görünmeyen/ekran dışı ülkeler işlenmez; küre gölgesi, yazı ve yay ucu bulanıklıkları kaldırıldı; yazılar yumuşakça açılıp kapanır; yay hızı süre bazlı.
- Işıklar: eşzamanlı yay sayısı (Güvende 70, Dikkat 85, Kritik 110, Nöbetçi kapalı 45), 77 şehir noktası; yakınlaşınca yaylar ekranda görünen şehirler arasında başlar.

## 1.5.1 (22.11.2026) — Küre: sürekli dönüş, ülke adları, daha çok yay (testler 1327 başarılı)
- İmleç küre dışındayken küre görünür hızda sürekli döner (yaklaşık 60 sn'de bir tur); imleç 8 sn dışarıda kalırsa odak/yakınlaşma geri alınır.
- Ülke adları imleç yanında değil, kürenin üzerinde kendi konumunda yazılır (çakışmada küçükler gizlenir, yakınlaştıkça açılır).
- Ülkeden ülkeye zıplayan örnek yaylar çoğaltıldı (45 nokta, daha çok eşzamanlı yay). Kolay ve Uzman Görünümde aynı.

## 1.5.0 (22.11.2026) — Harita küresi ve bildirim ışığı (testler 1326 başarılı)
- Küre: üzerine gelinen ülkenin gerçek şekli parlar, adı imlecin yanında çıkar; tekerlekle yakınlaşma; ülkeye tıklayınca odaklanma (tekrar tıklayınca geri dönüş).
- İmleç küre dışındayken küre gerçek saate göre döner (saatte 15°). Kalkan küre ortasından aşağı çekildi; "TÜRKİYE" yazısı ülkenin konumunda.
- Kolay Görünüm: üç eylem kartı daraltıldı.
- Uzman Görünüm (◼ Panel): "Klasik radar | Harita küresi" geçişi; harita küresinde Uyarı/Kritik olaylar (son 30 dk) ışık yakar, ışığa tıklayınca Olay Zaman Çizgisi açılır. Olay kayıtlarında konum olmadığı için ışık Ankara'da yanar.

## 1.4.0 (22.11.2026) — Kali araç şablonları (testler 1321 başarılı)
Kali sensöründen çalıştırılabilen araç sayısı 6'dan 26'ya çıktı; savunma ve bilgi toplama araçları artık uygulamadan çalıştırılır. Yeni özellik sürümü.
- **Komut şablonları**: tek kaynak `kali_araclari.json` (programa gömülü, kullanıcı dosyasıyla değiştirilemez). 23 şablon: dnsenum, theHarvester, recon-ng, SpiderFoot, netdiscover, tcpdump, tshark, netcat, macchanger (yalnızca gösterim), Tor (durum/başlat/durdur), proxychains, WPScan, Sleuth Kit (mmls/fls), foremost, binwalk, Volatility3, radare2, gdb; Maltego ve Burp Suite Kali masaüstünde başlatılır. Serbest komut yoktur: argüman listeleri sabit, değerler desene ve aralığa uymak zorundadır.
- **Yeni komut ajanı**: sensörde ayrı Python programı; hedef ve değerleri sensörde YENİDEN doğrular (özel ağ veya izinli liste), sudo ve süreyi kendi tablosundan alır, stdout/stderr ayrı akar, çıkış kodu bildirilir, zaman aşımında süreç öldürülür, sudo'lu araçlar `sudo -n timeout` ile sarılır. Parametre değerleri yalnızca yazdırılabilir ASCII'dir (Unicode rakamlarla desen atlatma kapatıldı).
- **Rehber ve arayüz**: şablonu olan 20 araç "uygulamadan çalıştırılabilir" oldu (saldırı odaklı 19 araç kapalı kalır). "Sensörde Çalıştır": işlem seç → hedef/parametre → hedef doğrulama → sensörde çalışacak TAM komutun önizlemesi → onay (varsayılan Hayır) → sonuç (durum, süre, ham çıktı dosyası) ve olay günlüğüne bitiş kaydı. Durdur düğmesi eklendi. Araç kurulu değilse kurulum ayrıca onay ister; parolasız sudo yoksa anlaşılır hata verir.
- **İzinli hedefler**: müşteri alan adları ve genel IP'ler yalnızca "yazılı yetkim var" onayıyla `%AppData%\BulutSoft\izinli_hedefler.txt` listesine eklenir (kalıcı, olay günlüğüne yazılır); listenin dışındaki hedefler reddedilir.
- **Yetki**: rehberden araç çalıştırmak artık Analist/Yönetici ister (İzleyici çalıştıramaz).
- **Bilinen sınırlar**: şablon bayrakları henüz gerçek bir Kali'de doğrulanmadı (tabloda `dogrulandi: false`; onay penceresinde belirtilir). ZAP, Kismet, Ghidra headless, Mitmproxy ve Autopsy henüz bağlanmadı.

## 1.3.2 (21.11.2026)
Gerçek kurulumda görülen iki sorun düzeltildi. (1) ETW süreç alarmı Microsoft imzalı sistem süreçlerini (örn. CompatTelRunner) yanlışlıkla alarm sayıyordu: Windows/Program Files altındaki Microsoft imzalı süreçler artık alarm üretmez (en çok 10 dakikada bir 'Bilgi'); alarm yolları sürücü harfiyle yazılır. İmza + yol kararı verir, ad taklidi bu istisnadan yararlanamaz. (2) Açılış günlüğü, koruma hizmeti çalışırken 'bilgisayar izlenmiyor' diyordu; hizmet ve arayüz durumu ayrı yazılır.

## 1.3.1 (21.11.2026)
Düzeltme sürümü. 1.3.0'da Kolay Görünüm'deki dünya küresi görünmüyordu (kure.html bannerında bir değişken tanımlanmadan kullanılıyordu; sayfa her karede hata verip küreyi çizmiyordu). Düzeltildi ve bu hatayı yakalayan test eklendi. Başka değişiklik yok.

## 1.3.0 (kırmızı takım denetimi düzeltmeleri; testler 1129 başarılı)
Güvenlik sertleştirme sürümü hazırlığı. Yeni özellik eklenmedi; denetimde bulunan saldırı patikaları kapatıldı.
- Kimlik: `kimlik.json.mac` silinince dosya artık KABUL EDİLMEZ ve yeniden mühürlenmez (eski "geçiş" yalnızca önemsiz dosyalarda kaldı). Mühür/yarım yazım izi olup dosyası silinmişse program ilk kuruluma DÖNMEZ ("bozuk" sayılır).
- Olay günlüğü: satır başına zincirli HMAC + baş mührü (silinen/değiştirilen satır ve kesilen son görünür); 2 MB'ta kırpma yerine `olay_arsiv` klasörüne arşiv; olaylar Windows Olay Günlüğü'ne de yansıtılır (kaynak "BulutSoft", kurulumda kaydedilir).
- Güvenlik duvarı yedekleri `%ProgramData%\BulutSoft\fw_yedek` altında yalnızca yönetici yazabilir; geri yüklemeden önce klasör sahipliği/yetkileri doğrulanır ve uygulanacak dosyanın tarih/boyut/özeti gösterilir.
- Nöbetçi ve tuzak durumu kalıcı (yeniden açılınca geri yüklenir); açılışta koruma durumu günlüğe açıkça yazılır; Nöbetçi açıkken çıkışta uyarı. "Gerçek zamanlı koruma" ifadeleri düzeltildi (15 dakikalık kontrol); kürede "Yaylar örnek görseldir".
- Süreç Avcısı: koruma ada + yola bakar (Temp/İndirilenler/AppData'dan çalışan "svchost" gibi ad taklidi dokunulmaz sayılmaz); WMI hatasında engellenmez, kullanıcıya açıkça sorulur. Alarm anında diske en çok yazan süreçler günlüğe yazılır.
- Fidye alarmı: İndirilenler/Videolar/Müzik/OneDrive/ek diskler/ağ sürücüleri izlenir; yavaş şifreleme (60 sn ve 10 dk pencereleri); AppData'nın tamamı artık dışlanmaz.
- Honeypot: eşzamanlı bağlantı sınırı, kaynak sözlüğü temizliği, rastgele banner. Sensör dosyası yüklenirken doğrulanır; bilinen sunucuda SSH parmak izi katı ("yes"). Radar: okunamayan kaynakta "kör/kısmi sonuç" uyarısı; ortak (NAT) adres engelleme uyarısı. Komutlarda tırnaklama ve çıkış kodu. Global hata yakalayıcı (olay günlüğü + cokme_*.txt).
- Kanıt paketi: operatör ve sürüm bilgisi, zip için dış `.sha256` dosyası (kod imzalama/zaman damgası henüz yok).
- Arayüz: tema uyumlu mesaj penceresi (`TemaliMesaj`, 159 çağrı); Light temada puan halkası okunur; küçük ekranda ana pencere kaydırılır; tek palet kaynağı (CanliTarama); ölü kod temizliği; WebView2 önbelleği kaldırmada silinir.
- Test ortamı: pilot bayrağı geçici klasörden aranır (bilgisayarda kalan bayrak testleri bozmasın).
## 1.3.0 (devam): koruma hizmeti ve kalan denetim maddeleri (testler 1161 başarılı)
- **Koruma hizmeti (BulutSoftGuard)**: SYSTEM olarak, "Otomatik (Gecikmeli Başlatma)". Nöbetçi yoklaması (15 dk), fidye izleme (tüm kullanıcı profilleri + ek diskler), canary, zincirli olay günlüğü (`%ProgramData%\BulutSoft\Guard`), Windows Olay Günlüğü aynası. Arayüz hizmetle yerel named pipe ile konuşur; değiştiren komutlar yalnızca kurulu BulutSoftSecuritySuite.exe'den kabul edilir. Arayüzde hizmet durumu ana gösterge (kürede banner, günlük, hizmet alarmları).
- **İkinci mühür kanalı (K1/K2)**: kimlik'in kritik özeti HKLM\SOFTWARE\BulutSoft\Muhur altında (kullanıcı okur, yalnızca hizmet yazar). Yerel mühür silinse bile hizmetle eşleşen dosya onarılarak kabul edilir; sahte yerel mühür hizmette yakalanır; dosyası silinen kimlik ilk kuruluma dönmez. Hizmet kapalıyken kritik hesap/parola değişikliği yapılamaz (giriş çalışır).
- **SSH**: özel anahtar rastgele parolayla şifreli üretilir, parola DPAPI'de; bağlantıda SSH_ASKPASS ile yalnızca ssh sürecinin ortamına verilir (düz anahtar diskte yok; eski düz anahtarlar yerinde şifrelenir). Sunucu parmak izi hizmet kanalına (HKLM) kaydedilir; known_hosts ile uyuşmazsa bağlantı REDDEDİLİR; bilinmeyen sunucu için parmak izi kullanıcıya onaylatılır (accept-new kalktı).
- **Kanıt paketi**: hizmet anahtarıyla dış HMAC (.hmac) ve kullanıcı onayıyla RFC 3161 zaman damgası (.tsr; freetsa.org, yedek digicert; yalnızca SHA-256 özeti gönderilir).
- **Süreç bazlı yazma sayacı (ETW)**: hizmet Kernel-File yazma olaylarına abone olur; tek süreç 10 sn'de 40+ farklı dosyaya yazarsa PID, yol ve imza ile alarm (ETW yönetici/SYSTEM ister; yoksa 4 sn'lik ölçüm yedek).
- **Mühür birleştirme**: Muhur (HMAC dosyası) kaldırıldı; tek mekanik Butunluk (.mac) + HKLM kanalı.
- **Ekran**: tüm pencereler çalışma alanına sığdırılır, içerik kaydırılabilir (1366x768, %150 1080p testli). Kurulum formu alçak ekranda sıkıştırılır.
- Kurulum: `sc.exe` ile hizmet oluşturma/başlatma/kaldırma kurulum paketine eklendi; `HizmetKur.ps1`, `HizmetKaldir.ps1` elle kurulum içindir.
## 1.3.0 (21.11.2026) — koruma hizmeti sürümü. Üçüncü tur: hizmet protokolü, tek yazar günlük, parmak izi onayı (testler 1173 başarılı)
- Hizmet IPC komutları şartnameye uyduruldu: GET_STATUS, GET_SEAL_STATE, VERIFY_KIMLIK, RUN_CHECK_NOW, SET_WATCHDOG, ARCHIVE_LOG, VERIFY_KANIT (+ olay-yaz); protokol sürümü "v1"; komut adları büyük/küçük harf ve "_"/"-" farkından bağımsız. HKLM düzeni `SOFTWARE\BulutSoft\Guard` (KimlikMuhurHash, KimlikMuhurZamani, KimlikIlkKurulum, KanalKuruldu, AnahtarSurum, WatchdogAcik; alt anahtarlar SensorHostKeys, Gizli).
- Olay günlüğü TEK YAZAR: hizmet çalışıyorsa arayüz olayları hizmete iletir (ulaşılamazsa yerel yazar, kayıp yok); okuma yerel + hizmet günlüğünü birleştirir.
- Kolay Görünüm'e "Koruma Hizmeti" göstergesi: AKTİF (hizmet) + son/sonraki yoklama, "hizmet yanıt vermiyor", "KORUMA KAPALI — kurulu değil"; tıklayınca (onayla) yükseltilmiş başlatma (çıkış kodu okunur). Hizmete bağlanamazsa 3 deneme (5 sn arayla). Kalkan tıklaması hizmet çalışıyorsa hizmette korumayı açar/kapatır; Pilot modda neden pasif olduğu yazılır.
- SSH: sunucu parmak izi SENSÖR ADINA göre HKLM'de; uyuşmazlıkta bağlantı reddedilir ve "sensör kimliği değişti" penceresi (kayıtlı/şimdiki parmak izi, varsayılan Hayır); accept-new kaldırıldı (her zaman StrictHostKeyChecking=yes, ilk bağlantıda parmak izi onayı).
- ETW alarmı yazılan yol örneklerini içerir (Create/NameCreate eşlemesi). Kanıt paketi: KAPSAM.txt, makine adı, dürüst güvence açıklamaları, DPAPI(LocalMachine)+ACL'li HMAC anahtarı, TSA 10 sn zaman aşımı ve kanit_ayar.json ile TSA seçimi, zaman damgası alınamazsa not dosyası.
- Kurulum: hizmet adı "BulutSoft Guard", yeniden başlatma eylemleri 60/120/300 sn.
## 1.2.1 (21.11.2026)
Kurulum sürümü. Programa dokunulmadı.
- WebView2 çalışma zamanı kurulum paketine gömüldü (Microsoft imzalı çevrimdışı kurucu). Yalnızca bilgisayarda yoksa, sessizce kurulur; Kolay Görünüm küresi böylece internetsiz bilgisayarda da çalışır. Kurulum paketi büyüdü.

## 1.2.0 (21.11.2026)
Görünüm sürümü. Güvenlik koduna, kimlik doğrulamaya ve tarama motoruna dokunulmadı; testler (1102) başarılı.
- Kolay Görünüm: canlı 3B Türkiye merkezli küre ve koruma kalkanı (WebView2, tamamen çevrimdışı, yerel `Kure` klasörü). Kalkana tıklamak Nöbetçi Modu'nu açıp kapatır; renk güvenlik durumuna göre değişir. WebView2 çalışma zamanı yoksa panel boş kalır, program etkilenmez.
- Tarama ekranı: yatay yeşil çubuk yerine saf C# `CanliTaramaKontrolu` (4 aşama, radar, halka, telemetri). Yüzde %1'den %100'e akıcı ilerler; tarama motoru gerçek yüzde vermediği için aşama süreleri önceki taramalardan öğrenilen bir TAHMİNDİR (`%LocalAppData%\BulutSoft\tarama_sureleri.txt`).
- Açılış ve Giriş ekranları yenilendi (saf GDI+): çerçevesiz, yuvarlak köşeli, sürüklenebilir; canlı atlas haritası, siber nöronlar ve şimşek logosu; 7 adımlı açılış; Caps Lock uyarısı; yanlış parolada titreme ve satır içi hata; Açık/Koyu tema düğmesi. Adım adı "Ayar Dosyası Muhafızı" oldu.

## 1.1.1 (21.11.2026)
Etkileşim sürümü. Programın işlevlerine ve güvenlik koduna dokunulmadı; testler (1102) başarılı.
- Tüm düğmeler (240) animasyonlu `EfektliButon` oldu: üzerine gelince yumuşak geçiş ve ışıma/gölge, basınca çökme, tıklama dalgası, klavye odak halkası. Sistem Button sınıfından türer; mevcut renkler ve "seçili" durumlar korunur.
- Açılan her pencerede (ana pencere, açılış ekranı ve bildirim pencereleri hariç) belirme + hafif kayma efekti ve Windows 11'de yuvarlak köşe. Windows'ta animasyon efektleri kapalıysa oynamaz.
- Radar: kıtalı, sürekli dönen ve fareyle çevrilebilen küre; açık kapı düğümleri kapsül etiketli.

## 1.1.0 (20.11.2026)
Tasarım ve arayüz sürümü. Programın işlevlerine ve güvenlik koduna dokunulmadı; testler (1102) başarılı.

**Tema ve renk**
- Stil kılavuzu paleti (Koyu ve Açık) tüm arayüze uygulandı: sayfa, kart, girdi, metin, birincil/başarı/uyarı/tehlike renkleri (`Palet.cs`). Koyu varsayılandır.
- Düğme stilleri: Birincil (dolu), Hayalet (kenarlıklı), Tehlike Hayalet, Başarı/Uyarı soluk. Tablo (DataGridView) başlık, satır ve seçim renkleri, kaydırma çubukları, koyu pencere başlık çubuğu iki temada düzeltildi.
- Siyah köşe/kenarlık hatası giderildi (Kolay/Uzman görünüm anahtarı ve Kolay panel kartları): özel çizilen denetimler artık temanın zeminini kullanır.
- Açık temada kalan koyu sızıntılar (radar, günlük kutusu, kartlar) temizlendi. Sabit renk eklenirse uyaran bir koruma testi eklendi.

**Radar**
- "Canlı Ağ ve Port Radarı" yeniden tasarlandı: ortada sürekli dönen 3B siber küre (nokta matris kıtalar, tel kafes, radar halkaları ve çentikler, dönen radar kolu). Fareyle sürükleyerek açı değiştirilir.
- Bulunan açık kapılar kürenin etrafında kapsül etiketli düğmeler olarak gösterilir (gerçek tarama verisi); kritik düğümler nabız atar; alt kısımda hedef IP kapsülü.
- Kritik bulgu varsa başlıkta yanıp sönen 🚨 alarm ve "N KRİTİK" yazısı. Boş radarda soldan sağa kayan yönlendirme yazısı.
- NOT: küre üzerindeki renkli yaylar görsel BENZETİMDİR (rastgele şehirler arası), gerçek trafik değildir.

**Yeni bileşenler (henüz ekranlara uygulanmadı)**
- `EfektliButon`: hover geçişi, ışıma/gölge, basınca çökme, tıklama dalgası, odak halkası; Birincil/Başarı/Tehlike/Hayalet/Tehlike Hayalet.
- `PencereEfekti`: açılışta belirme + hafif kayma, Windows 11 yuvarlak köşe, yumuşak kapanış.

**Araçlar ve belgeler**
- Program envanteri: 34 pencere sınıfı, 199 tıklanabilir düğme; her pencere ve düğme için Koyu/Açık görsel sayfaları (`Output\Envanter`).
- Sunum şablonu ve yayın notları güncellendi.

## 1.0.0 (hazırlık)
- Filo: `--tara` (başsız tarama, JSON özet) ve `--filo` (toplu HTML/CSV rapor).
- Pilot salt okunur mod ve `--gizlilik` veri akışı belgesi.
- Süreç Avcısı: hizmet/veritabanı/yedek süreçlerine koruma. Kalıcılık yedeği atomik yazım. Tek kopya kilidi.
- Kurulum: makine geneli sessiz kurulum, sabit uygulama kimliği (güncelleme), pilot görevi.
