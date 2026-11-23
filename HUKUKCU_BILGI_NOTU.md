# BulutSoft Security Suite — Hukukçu İçin Ürün Tanıtım ve Veri İşleme Notu

**Amaç:** KVKK aydınlatma metni, veri işleme sözleşmesi ve kullanım koşullarının hazırlanabilmesi için ürünün ne yaptığını, hangi verilere dokunduğunu ve nerelere veri gönderdiğini dürüst ve eksiksiz anlatmak.
**Hazırlayan:** ürün sahibi ve geliştirme asistanı (teknik anlatım). **Bu belge hukuki görüş değildir**; hukuki nitelendirme (veri sorumlusu/işleyen, hukuka uygunluk sebebi, yurt dışı aktarım vb.) hukukçuya aittir. Aşağıdaki bilgiler programın kaynak kodundan ve testlerinden derlenmiştir; doğrulanmamış noktalar açıkça "doğrulanmadı" diye işaretlidir.
**Sürüm:** 1.0.0 (Windows masaüstü uygulaması). **Tarih:** 2026-11-20.

---

## 1. Ürün nedir?

Küçük ve orta ölçekli kurumların (KOBİ) kendi bilgisayarlarını ve kendi ağlarını **savunma amacıyla** denetlemesini sağlayan bir Windows masaüstü programıdır. Bir kurumun BT sorumlusu ya da yetkili kullanıcısı tarafından, kurumun kendi cihazlarında çalıştırılır.

**Yaptığı işler (özet):**
- Bilgisayarın güvenlik ayarlarını denetler (güvenlik duvarı, açık portlar, paylaşımlar, yerel hesaplar, açılışta çalışan öğeler, yazılım envanteri ve bilinen açıklar, zayıf yapılandırmalar).
- Bulguları puanlar ve rapor üretir (HTML/PDF, yönetim özeti, KVKK/ISO 27001 eşlemeli bilgi raporu).
- Kullanıcı onayıyla düzeltme önerilerini uygular (örneğin güvenlik duvarına engel kuralı eklemek, bir başlangıç öğesini devre dışı bırakmak). Her değişiklik onay ister, geri alınabilir.
- Saldırı belirtilerini izler ve uyarır (başarısız oturum açma denemeleri, yeni USB cihaz, ağda yeni cihaz, sahte "tuzak" dosya ve kapılar).
- Birden çok bilgisayarın tarama özetini bir klasörde toplayıp tek bir raporda birleştirir (sunucusuz).

**Yapmadığı işler:**
- Hiçbir bilgiyi BulutSoft'a ya da yazılım sağlayıcıya göndermez. Merkezî bir sunucu, bulut hesabı, telemetri veya kullanım izi yoktur.
- Klavye, ekran, pano, kamera ya da mikrofon içeriğini kaydetmez (kaynak kodda böyle bir işlev tespit edilmedi).
- Dosya içeriklerini toplamaz; dosya incelemede yalnızca kullanıcının kendi seçtiği dosyayı yerelde analiz eder.
- Saldırı aracı değildir: taramalar yalnızca özel ağ adreslerine yapılır; internetteki bir siteye ancak sahiplik doğrulanırsa ve yalnızca pasif gözlemle bakılır.

## 2. Etik ve güvenlik ilkeleri (üründe uygulanan)

| İlke | Üründeki karşılığı |
|---|---|
| Savunma amacı, izinli kapsam | Aktif taramalar özel/yerel ağ adresleriyle sınırlı; dış sitelerde yalnızca pasif gözlem ve sahiplik doğrulaması. |
| Kullanıcı kontrolü | Sistemi değiştiren her işlem açık onay ister; varsayılan buton "Hayır"; geri alma yolu gösterilir. |
| Geri alınabilirlik | Güvenlik duvarı kuralları ve başlangıç öğeleri geri alınır; yedekler atomik yazılır. |
| Yetkilendirme | Üç rol: İzleyici (yalnızca görür), Analist (tarar/rapor), Yönetici (sistemi değiştirir, kullanıcı yönetir). Varsayılan rol en az yetkili. |
| Salt okunur pilot mod | BT yöneticisi tek bir ayarla (ortam değişkeni ya da ProgramData bayrağı) programı "hiçbir değişiklik yapmaz" konumuna alabilir; normal kullanıcı bunu kapatamaz. |
| Şeffaflık | `BulutSoftSecuritySuite.exe --gizlilik` komutu dışarı giden tüm bağlantıları ve saklanan verileri listeler; yeni bir dış adres eklenirse otomatik test başarısız olur. |
| Koruma | Hizmet, veritabanı, yedekleme ve sistem süreçleri sonlandırılamaz; kendi cihazlar, ağ geçidi ve DNS engellenemez. |

## 3. Mimari ve veri akışı

- **Yerel çalışır.** Veriler kullanıcının Windows profilinde (`%AppData%\BulutSoft`) saklanır; her Windows kullanıcısı için ayrıdır. Makine geneli yalnızca lisans/deneme kaydı ve pilot bayrağı `ProgramData`'dadır.
- **Sunucusuz toplu raporlama.** Her bilgisayar `--tara` ile bir JSON özet dosyası üretir; BT yöneticisi bunu kurumun kendi paylaşım klasörüne yazdırır ve `--filo` ile birleştirir. Paylaşımın erişim denetimi kuruma aittir.
- **İsteğe bağlı Uzman/MSP paketi:** ağır taramalar için kurumun kendi Linux (Kali) sanal makinesine SSH ile bağlanır. Varsayılan kurulumda yoktur.

### 3.1 Dışarı giden bağlantılar (tamamı)

Hiçbiri arka planda otomatik çalışan bir "veri toplama" değildir; ilgili özellik kullanıcı tarafından başlatılınca ya da ayarlanınca yapılır.

| Sunucu | Amaç | Giden veri | Ne zaman |
|---|---|---|---|
| www.cisa.gov | Bilinen istismar edilen açıklar listesi (KEV) indirme | Yalnızca indirme isteği; makine/tarama verisi yok | Kullanıcı güncelle derse |
| api.pwnedpasswords.com | Parola sızıntı kontrolü | Parolanın SHA-1 özetinin yalnızca ilk 5 karakteri (k-anonimlik); parola çıkmaz | Kullanıcı aracı kullanırsa |
| api.anthropic.com | Yapay zekâ yorumu (Claude) | Hedef IP, tarama özeti ve bulgular, kullanıcının kendi API anahtarı; bilgisayar ve kullanıcı adı gönderilmez | **Yalnızca** kullanıcı onay penceresine "Evet" derse |
| api.ipify.org, api6.ipify.org, www.cloudflare.com, ipwho.is | IP sızıntı testi (dışarıdan nasıl göründüğünü ölçme) | Boş istekler; genel IP adresi karşı hizmetçe görülür | Kullanıcı testi başlatırsa |
| discord.com, api.telegram.org, logic.azure.com (Teams) | Uyarı bildirimi | Uyarı metni (başlık, önem, makine adı) | Yalnızca kullanıcı kendi webhook/bot bilgisini girerse |

Not: Anthropic ve bildirim hizmetleri yurt dışında yerleşik üçüncü taraflardır; bu aktarımların KVKK m.9 bakımından nitelendirilmesi hukukçuya bırakılmıştır. Yapay zekâ ve bildirim özellikleri **varsayılan kapalıdır**.

## 4. İşlenen veri kategorileri

Aşağıdaki tablo, programın görebildiği veya sakladığı verileri sıralar. "Kişisel veri?" sütunu teknik değerlendirmedir; kesin nitelendirme hukukçuya aittir.

| Veri | Kaynak | Kişisel veri? | Ne için | Nerede / ne kadar |
|---|---|---|---|---|
| Programın kendi kullanıcı hesapları (kullanıcı adı, rol, parola özeti, hatalı deneme sayısı) | Kurulumda/yönetici girer | Evet (kullanıcı adı) | Oturum ve yetki | `kimlik.json`; parolalar PBKDF2-SHA256 (600.000 tur) ile özetlenir, düz parola tutulmaz; 5 hatalı denemede kilit; bütünlük mühürlü |
| Denetim izi (kim hangi eylemi yaptı) | Program | Evet (kullanıcı adı) | Hesap verebilirlik | `denetim_izi.jsonl`; hash zincirli (kayıtların sonradan değiştirilmesi tespit edilir; dosyanın son satırlarının silinmesi tek başına tespit edilmeyebilir); otomatik silme yok |
| Yerel Windows hesap listesi (ad, devre dışı, parola gerekli mi) | Windows | Evet olabilir (hesap adı) | Zayıf hesap bulma | Tarama bulgusu içinde; Geçmiş'te en son 100 tarama saklanır |
| Başarısız oturum açma denemeleri (kullanıcı adı, kaynak IP, zaman) | Windows Güvenlik olay günlüğü (olay 4625), isteğe bağlı sensörün SSH günlüğü | Evet (IP ve kullanıcı adı) | Kaba kuvvet saldırısı tespiti | Ekranda; engel kararı için IP; engellenen IP kaydı `engel_kayit.json` (IP ve zaman) |
| Ağdaki cihazlar (MAC, IP, ad, tür, ilk/son görülme) | Yerel ağ keşfi | Evet olabilir (MAC/ad, bir kişiye bağlanabilirse) | Yeni/yetkisiz cihaz uyarısı | `cihaz_envanteri` dosyası; otomatik silme yok |
| Yazılım envanteri, açık portlar, paylaşımlar, süreç adları | Windows | Genelde hayır; kullanıcı adı yolda geçebilir | Açık/eski yazılım bulma | Tarama bulgularında |
| Canlı ağ bağlantıları (süreç adı, uzak IP/port) | Windows | Evet olabilir (çalışan faaliyeti bağlamında) | Şüpheli bağlantı gösterme | Canlı ekran gösterimi; kalıcı kayıt tespit edilmedi (**doğrulanmadı**) |
| USB cihaz adı/üretici | Windows | Hayır (cihaz bilgisi) | USB kalkanı uyarısı | Olay günlüğünde |
| Tuzak (honeypot) kapısına bağlananlar: IP ve gönderdikleri ilk metin | Ağ | Evet (IP; yazılan metin bir kişinin yanlışlıkla yazdığı bilgi olabilir) | Erken uyarı | Ekran günlüğünde; olay günlüğüne düşüp düşmediği **doğrulanmadı**. Tuzak varsayılan olarak yalnızca bu bilgisayarda dinler (127.0.0.1) |
| Olay günlüğü (önemli olaylar) | Program | Olay metnine bağlı | Geçmiş uyarılar | `olaylar.jsonl`; en fazla yaklaşık 5000 satır tutulur, aşılınca eskiler silinir |
| Tarama geçmişi | Program | Hedef ve bulgulara bağlı | Karşılaştırma | En son 100 kayıt; fazlası otomatik silinir |
| Tarama özeti (`--tara`) ve filo raporu | Program | Makine adı ve bulgular | Çoklu makine raporu | Kurumun seçtiği klasör; erişim denetimi kurumundur; rapor dosyaları şirket içinde dağıtılabilir |
| API anahtarı (Claude), bildirim kanalı bilgileri (bot anahtarı/webhook adresi) | Kullanıcı girer | Hayır (hizmet kimlik bilgisi) | İsteğe bağlı özellikler | Windows DPAPI ile yalnızca o Windows hesabıyla çözülebilecek biçimde saklanır |
| Sensör (Kali) bağlantı bilgisi: adres, kullanıcı, SSH anahtar yolu | Kullanıcı girer | Hayır | Uzman paketi | `sensorler.json`; SSH yalnızca anahtarla |
| Lisans bilgisi: müşteri adı, makine kimliği (türetilmiş), bitiş tarihi | Lisans dosyası | Müşteri adı olabilir | Lisans doğrulama | Yerelde; sunucu yok. Makine kimliği makine GUID'inden türetilen geri çevrilemez özet |

**Veri sahibi başvurusu için:** Program, belirli bir kişinin verisini aratma/silme özelliği sunmaz. Veriler yerel dosyalardır; kaldırma (uninstall) işlemi kullanıcı verisini **bilerek silmez** (lisans ve yedekler kaybolmasın diye). Silme/imha prosedürü kurum tarafında dosya düzeyinde yapılır. Bu nokta sözleşme ve aydınlatma metninde ele alınmalıdır.

## 5. Taraflar ve roller (teknik tespit; hukuki nitelendirme hukukçunun)

- **Kurum (müşteri):** programı kendi cihazlarında kurar ve kullanır; verilerin tamamı kurumun kontrolündeki bilgisayarlarda ve paylaşımlarda kalır. Teknik olarak veri sorumlusu rolüne yakındır.
- **BulutSoft (yazılım sağlayıcı):** verilere erişmez, telemetri almaz, sunucusu yoktur. Yazılımı lisanslar ve destek verir. Destek sırasında kurum dosya paylaşırsa ayrıca bir işleme faaliyeti doğabilir (destek kılavuzunda parola/anahtar istenmez, günlük dosyalarının son satırları istenir).
- **MSP/BT hizmet firması (Uzman paketi senaryosu):** birden çok müşteri adına kullanıyorsa o firma müşterinin veri işleyeni olabilir.
- **Üçüncü taraflar:** yalnızca kullanıcı etkinleştirdiğinde Anthropic, bildirim hizmetleri ve IP sızıntı testi hizmetleri (bölüm 3.1).

## 6. Çalışan izleme boyutu (özel dikkat)

Ürün çalışanları izlemek için tasarlanmamıştır. Ancak güvenlik amaçlı olarak şu veriler bir çalışanın faaliyeti hakkında dolaylı bilgi verebilir: başarısız oturum açma denemeleri (kullanıcı adı, IP), canlı ağ bağlantıları (hangi program hangi sunucuya bağlanıyor), süreç listesi, USB takılması, yazılım envanteri. Bu nedenle kurumun:
- Çalışanlara aydınlatma yapması (hangi verilerin neden işlendiği),
- Ölçülülük ve amaçla bağlılık ilkesine uygun bir politika belirlemesi,
- Erişimi rol ve yetkiyle sınırlaması gerekir.
Hukukçudan bu başlıkta işyeri izleme bakımından görüş ve şablon metin beklenir.

## 7. Teknik ve idari güvenlik tedbirleri (ürün tarafında)

- Kimlik doğrulama: PBKDF2-SHA256 (600.000 tur), tuzlu, sabit zamanlı karşılaştırma, hatalı denemede artan kilit, parola politikası (en az 8 karakter, harf+rakam, kolay parola reddi).
- Bütünlük: kimlik ve lisans anahtarı dosyaları Windows DPAPI ile mühürlenir; elle değiştirilirse program kimliği "bozuk" sayar. Yazımlar atomiktir (yarım dosya kalmaz), yedek kopya tutulur.
- Gizli bilgiler (API anahtarı, bildirim bilgileri) düz metin saklanmaz.
- Denetim izi hash zincirlidir; ortadaki bozuk satır kurcalama sayılır.
- Sistem değiştiren eylemler: onay + gerekirse UAC + geri alma + yetki kontrolü; pilot modda hiç çalışmaz.
- Paylaşımdaki dosyalara güvenilmez: `--filo` boyut ve biçim sınırları uygular, HTML rapor çıktıları güvenli biçimde kodlanır.
- Test: 1000'in üzerinde otomatik test. Kurulum paketi izole Windows ortamında sessiz kurulum, güncelleme ve kaldırma açısından denendi.

## 8. Bilinen sınırlar ve dürüst notlar (sözleşmeye yansıtılmalı)

1. Ürün hukuki uyumluluk ya da sertifikasyon **vermez**; KVKK/ISO eşlemeleri bilgilendirme amaçlıdır, tavsiye niteliğindedir ve yeterli olduğunu garanti etmez.
2. Tarama "tam güvenlik" garantisi vermez; yanlış alarm (yanlış pozitif) ve kaçırılan bulgu (yanlış negatif) olabilir.
3. Lisans doğrulaması yereldir; yeterince kararlı biri atlayabilir (kod içinde açıkça belirtilmiştir).
4. Kurulum paketi şu an **kod imzalama sertifikasıyla imzalı değildir** (satış aşamasında alınacak); Windows SmartScreen/antivirüs uyarı verebilir.
5. Düzeltme eylemleri (güvenlik duvarı kuralı vb.) yanlış kullanıldığında erişim kesintisine yol açabilir; programda korumalar (kendi cihaz, ağ geçidi, DNS engellenemez; onay ve geri alma) vardır ancak sorumluluk dağılımı sözleşmede netleştirilmelidir.
6. Veri tutma süreleri programda sınırlıdır (tarama geçmişi 100 kayıt, olay günlüğü ~5000 satır), ancak denetim izi, cihaz envanteri ve kullanıcı hesapları otomatik silinmez; kurumun imha politikası gerekir.
7. Üçüncü taraf özellikleri (yapay zekâ, bildirim, IP sızıntı testi) kullanıldığında veri kurumun kontrolünü terk eder.
8. Sensörlü (Kali) özellikler yalnızca geliştirme ortamında denenmiştir.

## 9. Hukukçudan beklenen çıktılar

1. **KVKK aydınlatma metni şablonu** — (a) programı kullanan kurumun çalışanlarına, (b) varsa ziyaretçi/misafir ağ kullanıcılarına (ağ cihaz keşfi ve MAC/IP için). Bölüm 4 tablosu temel alınabilir.
2. **Veri işleme sözleşmesi** — kurum ile BulutSoft (destek ve lisans), kurum ile MSP (veri işleyen). Üçüncü taraf aktarımları ve yurt dışına aktarım hükümleri.
3. **Kullanım koşulları / lisans sözleşmesi (EULA)** — kullanım amacı (yalnızca yetkili olunan sistemler), yasak kullanımlar (izinsiz tarama), lisans türü ve süresi, deneme sürümü.
4. **Sorumluluk sınırlaması** — yanlış alarm/kaçırılan bulgu, düzeltme eyleminden doğan kesinti, "tavsiye niteliği", üçüncü taraf hizmetler.
5. **Saklama ve imha politikası taslağı** — bölüm 4 ve bölüm 8 madde 6 ışığında.
6. **Veri ihlali bildirim süreci** — kurumun kendi veri sorumlusu yükümlülükleri ve BulutSoft'un destek sırasındaki rolü.
7. **Çalışan bilgilendirme/işyeri izleme politikası şablonu** (bölüm 6).
8. **Destek süreci:** kurum destek için günlük dosyası gönderirse hangi verinin gönderilebileceği ve nasıl korunacağı.

## 10. Hukukçuya sorular

1. BulutSoft bu mimaride (veriye erişim yok, telemetri yok, sunucu yok) KVKK bakımından veri işleyen/sorumlu sayılır mı, yoksa yalnızca yazılım sağlayıcı mıdır? Destek sırasında dosya paylaşımı bunu değiştirir mi?
2. Kurumun güvenlik amaçlı olarak işlediği veriler (IP, kullanıcı adı, bağlantı bilgisi) için hangi hukuka uygunluk sebebi (ör. meşru menfaat, hukuki yükümlülük) uygundur? Açık rıza gerekir mi?
3. Yapay zekâ ve bildirim özellikleri yurt dışı aktarım sayılır mı? Kullanıcının onay penceresi (özellikle açık rıza) yeterli midir, ek taahhütname gerekir mi?
4. Kaldırmada kullanıcı verisinin korunması (bilerek silinmemesi) imha yükümlülükleri açısından sorun yaratır mı? Silme için programa bir özellik eklenmesi gerekir mi?
5. Bu ürünü ticari olarak satarken VERBİS ya da başka bir kayıt/bildirim yükümlülüğü doğar mı (özellikle MSP senaryosu)?
6. Ürünün KVKK/ISO eşleme raporu ("uyumluluk bilgi raporu") ifadesi yanıltıcı olabilir mi; sorumluluk reddi metni ne olmalı?
7. Tuzak (honeypot) kapısı ve canary dosyaları: saldırganın bilgisini kaydetmek hukuken sorun yaratır mı? Çalışanın yanlışlıkla bağlanması durumunda ne yapılmalı?

## 11. Ekler

- `BulutSoftSecuritySuite.exe --gizlilik` komutunun çıktısı: dışarı giden bağlantıların güncel ve otomatik üretilmiş listesi.
- `PAKETLER.md`: Çekirdek ile Uzman/MSP paketi farkı.
- `DAGITIM_KILAVUZU.md`, `DESTEK_KILAVUZU.md`: kurulum, güncelleme, kaldırma ve destek süreçleri.

*Bu belge, sözleşme taslaklarının hazırlanmasına yardımcı olmak için teknik gerçekleri sıralar. Hukuki sonuç doğurmaz, hukuki danışmanlık yerine geçmez.*
