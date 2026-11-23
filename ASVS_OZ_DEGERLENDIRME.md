# BulutSoft Security Suite — OWASP ASVS 5.0 (Mayıs 2025) Öz-Değerlendirme

**Hedef düzey:** L2. **Tarih:** 22.11.2026. **Kapsam:** masaüstü uygulaması (`LabDenetim`), çekirdek kütüphane (`BulutSoft.Core`), koruma hizmeti (`BulutSoftGuardService`).
**Yöntem:** ASVS'nin 17 bölümünden bu ürüne uygun olanlar seçildi (web arayüzü, OAuth/OIDC, WebRTC, GraphQL, WebSocket ürünümüzde yok). Her satırda **kanıt** (kod veya test) var; kanıtı olmayan satır "Karşılandı" yazılmadı.
**Dürüst sınır:** Bu belge bir **öz-değerlendirmedir**, bağımsız bir denetim değildir. ASVS ~350 gereksinim içerir; burada her bölümün ürüne uygun başlıkları ele alındı, gereksinim-gereksinim tam sayım yapılmadı.

Durumlar: **K** = Karşılandı (kanıtlı) · **Kıs** = Kısmen · **A** = Açık (bilinen eksik) · **N/A** = Kapsam dışı.

| Bölüm | Konu | Durum | Kanıt / not |
|---|---|---|---|
| V5 Dosya işleme | Kalıcı dosyaların atomik yazımı; yarım dosya bırakmama | K | `Depo.Yaz/YazBayt` (benzersiz geçici ad + yer değiştirme); `AtomikYazmaTestleri` (paralel yazarlarda bozuk JSON yok, elle `.tmp` yasağı) |
| V5 | Yol/klasör adı doğrulama (marka klasörü) | K | `BrandConfig` sıkı doğrulama (regex, ayrılmış aygıt adları, reparse noktası reddi); `HijyenTestleri` |
| V5 | Kullanıcıdan dosya yükleme | N/A | Ürün dosya kabul eden sunucu değil |
| V6 Kimlik doğrulama | Parola saklama | K | PBKDF2-HMAC-SHA256, 600.000 tur, rastgele 16 bayt tuz (`Kimlik.cs`); `GuvenliKodlamaTestleri.Parola_PBKDF2...` |
| V6 | Sabit zamanlı karşılaştırma, kilitleme | K | `CryptographicOperations.FixedTimeEquals`; hatalı denemede kilit (`KilitBitis`), bilinmeyen kullanıcıda da özet hesabı (zamanlama sızıntısı yok) |
| V6 | Çok faktörlü kimlik doğrulama | **A** | Yok. L2 için önerilir; yerel masaüstü hesabı olduğundan öncelik düşük, planda değil |
| V8 Yetkilendirme | Rol tabanlı erişim (Yönetici / Analist / İzleyici) | K | `Yetki.Izinli`, `NavigasyonModeli` yetki kuralları, `KabukYetki`; `KabukTestleri`, `ServisBaglamiTestleri` |
| V11 Kriptografi | Onaylı algoritmalar, güvenli rastgelelik | K | HMAC-SHA256, `RandomNumberGenerator`; **BinaryFormatter, MD5, `new Random`, TripleDES, DES yok** (`GuvenliKodlamaTestleri.Kaynak_Zayif...`) |
| V11 | SHA-1 kullanımı | K (istisna) | Yalnızca pwnedpasswords k-anonimliği için zorunlu (`AracMantigi.Ozet`); yalnızca 5 karakterlik önek gönderilir. Test başka yerde SHA-1'i yasaklar |
| V11 | Sır koruma (DPAPI) ve entropi | K | Claude API anahtarı artık uygulamaya özgü entropiyle korunuyor; eski entropisiz kayıt ilk okumada göç ediyor (`GuvenliKodlamaTestleri.ClaudeAnahtari...`). Diğer tüm DPAPI çağrılarının entropisi var, testle denetleniyor |
| V11 | Anahtar yönetimi | Kıs | Olay zinciri anahtarı DPAPI (kullanıcı), kanıt HMAC anahtarı hizmette DPAPI. **Dürüst sınır:** aynı Windows kullanıcısı olarak çalışan kötü amaçlı yazılım anahtarı okuyabilir; bu yüzden olaylar Windows Olay Günlüğü'ne de yansıtılır (`OlayDeposu` açıklaması) |
| V12 Güvenli iletişim | Dış servis çağrıları yalnızca HTTPS, sertifika doğrulaması açık, açık zaman aşımı | K | `AgIstemciFabrikasi` (profil başına zaman aşımı, bypass yok); `AgDayanikliligiTestleri`; webhook adresi yalnızca HTTPS, kullanıcı bilgisi ve loopback reddedilir (`Webhook.AdresGecerli`) |
| V12 | Sertifika doğrulamasını kapatan istemciler | K (sınırlı) | Yalnızca kullanıcının kendi yerel/özel ağ hedeflerini tarayan 5 istemcide (kendi imzalı sertifikalar yaygın); `GuvenliKodlamaTestleri.SertifikaDogrulamaBypassi...` başka yerde yasaklar |
| V13 Yapılandırma / sır yönetimi | Sırlar kaynak kodda/günlükte yok | K | API anahtarı yalnızca DPAPI dosyasında; `GuvenliKodlamaTestleri.Gunlukler_SirVeAnahtarIcermez`; dış servise giden metin `VeriMaskeleyici` ve onay penceresiyle |
| V13 | Güvenli varsayılanlar | K | Pilot (salt okunur) mod, varsayılan "Vazgeç" onayları, kapsam kilidi (yalnızca özel/yerel adresler) |
| V14 Veri koruma | Kişisel veriyi dışarı göndermeme / maskeleme | K | `VeriMaskeleyici`, `ClaudeOnayFormu` önizleme; makine/kullanıcı adı AI metnine girmez (`Form1InceltmeTestleri`) |
| V15 Güvenli kodlama | Güvenilmeyen girdi, async hata yönetimi | K | `async void` yok (`GuvenliCalistir`), boş `catch` yok (`HijyenTestleri`); girdi doğrulama (`LabKapsam`) |
| V15 | Bağımlılık zafiyet taraması | K | `Yayinla.ps1` yayından önce `dotnet list package --vulnerable --include-transitive` çalıştırır; zafiyetli paket varsa yayın DURUR (22.11.2026 taraması: 4 projede zafiyetli paket yok). nuget.org erişilemezse uyarı verir, yayın sürer; `KurulumProfilTestleri` betiğin bu adımı içerdiğini denetler |
| V16 Loglama ve hata işleme | Güvenlik olaylarının kaydı, bütünlük | K | Zincirli HMAC olay günlüğü + arşiv rotasyonu + Windows Olay Günlüğü aynası; `OlayOnbellekTestleri`, `OlayDeposu.Dogrula` |
| V16 | Hata mesajlarında iç ayrıntı sızmaması | Kıs | Ağ hataları Türkçe ve ham istisna adı içermez (`AgHatasi`); eski bazı ekranlar hâlâ `ex.Message` gösteriyor → kısmen |
| V3, V4, V7, V9, V10, V17 | Web arayüzü, API, oturum çerezleri, token, OAuth, WebRTC | N/A | Ürün web sunucusu/API değildir (WebView2 yalnızca yerel `kure.html` gösterir ve başka adrese gitmesi engellidir: `KureWebPaneli`) |

## Açık kalanlar (öncelik sırasıyla)

1. **Ham `ex.Message` gösteren eski ekranlar** (V16): kullanıcıya yalnızca anlaşılır Türkçe mesaj gösterilmeli; ayrıntı günlüğe.
2. **Çok faktörlü kimlik doğrulama** (V6, L2/L3): şu an gerekçeli olarak yok.
3. **Anahtar yönetimi sınırı** (V11): hizmet yüklüyse HMAC anahtarı makine düzeyinde; donanım destekli (TPM) saklama planda yok.

## Dayanak kaynaklar
- OWASP ASVS 5.0.0 (Mayıs 2025): github.com/OWASP/ASVS/tree/v5.0.0 ve devguide.owasp.org
- Microsoft Learn: .NET kriptografi modeli ve DPAPI (`ProtectedData`, entropi önerisi), güvenli kodlama yönergeleri
- KVKK Kişisel Veri Güvenliği Rehberi (Nisan 2025), Tablo 4.1 teknik tedbirler
