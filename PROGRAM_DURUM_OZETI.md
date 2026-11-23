# BulutSoft Security Suite — Durum Özeti (20 Kasım 2026)

## Program ne hale geldi?
Windows için, KOBİ'lerin kendi bilgisayarlarını ve yerel ağlarını **savunma amacıyla** denetleyen bir güvenlik programı. Tek bilgisayarda çalışır, sunucu gerektirmez, hiçbir veri BulutSoft'a gitmez.

**Çekirdek (her müşteride, Kali/sensör gerekmez)**
- Bu bilgisayarın güvenlik denetimi, puanlı Güvenlik Karnesi, KVKK ve ISO 27001 eşlemeli raporlar (HTML/PDF), yönetim özeti.
- Onaylı ve geri alınabilir düzeltmeler (güvenlik duvarı kuralı, başlangıç öğesini kapatma). Korumalı süreçler ve kendi cihazlar engellenemez.
- Araç Kutusu: link analizi, dosya inceleme, parola sızıntısı, dosya bütünlüğü, vaka kaydı, filo raporu.
- Kolay görünüm (jargonsuz) ve Uzman görünümü.

**Kurumsal dağıtım**
- `--tara`: arayüz açmadan tarama, JSON özet. `--filo`: klasördeki özetlerden tek HTML/CSV rapor.
- Pilot (salt okunur) mod: program makinede hiçbir değişiklik yapmaz.
- Makine geneli lisans/deneme kaydı, kurum lisansı, `--gizlilik` (veri akışı belgesi).
- Kurulum paketi: `BulutSoft_Setup_v1.0.0.exe` (42 MB), Türkçe sihirbaz, sessiz kurulum, SHA256 özet dosyası, `Guncelle.ps1` ile çevrimdışı güncelleme.

**İsteğe bağlı Uzman/MSP Paketi:** Kali sensörü; sensör yokken ekranda "Uzman Sensör (İsteğe Bağlı) — Kullanılmıyor" görünür, hata gibi durmaz.

## Bu turda sağlamlaştırılanlar
- Tüm veri dosyaları atomik yazılıyor (elektrik kesintisinde bozulmaz, yedekten dönülür); kimlik kilitlenme riski kapatıldı.
- Süreç Avcısı hizmet/veritabanı/yedekleme süreçlerini öldürmüyor; IP engelleri yaşlanıyor ve onayla kaldırılıyor; ağ geçidi/DNS engellenemiyor.
- Testler gerçek kullanıcı günlüğünü kirletmiyor.

## Doğrulananlar
- **1073 otomatik test geçiyor**, Release derlemesi 0 hata.
- Kurulum Windows Sandbox'ta denendi: sessiz kurulum, izinler, pilot bayrağı, kaldırmada kullanıcı verisinin korunması (15/15 geçti).
- Filo sayfası düğmeleri, güncelleme betiği (8 sahte senaryo), gerçek hizmet koruması denendi.

## Henüz doğrulanmayanlar (dürüstlük)
- Sandbox 2. tur: gerçek güncelleme, SYSTEM görevle `--tara`, gerçek `netsh` engel/kaldır, Türkçe sihirbaz ekran görüntüsü. Hazır, **sonucu bekliyor**.
- Gerçek UAC'li güvenlik duvarı akışı, 50 makinelik yük, Kali'li özelliklerin müşteri ortamı.
- 3–5 gerçek makinelik pilot.

## Ertelenenler / sizin kararlarınız
- Kod imzalama sertifikası (satışa bırakıldı; şu an imzasız, SmartScreen uyarı verebilir).
- QuestPDF lisans uygunluğu.
- KVKK aydınlatma metni ve veri işleme sözleşmesi: hukukçu için `HUKUKCU_BILGI_NOTU.md` hazır; tasarıyı hukukçudan alınca inceleyeceğim.
- Modem entegrasyonu ve çekirdek/arayüz ayrımı: başlamadı.

## Önemli dosyalar
`PAKETLER.md` (Çekirdek/Uzman), `DAGITIM_KILAVUZU.md`, `DESTEK_KILAVUZU.md`, `HUKUKCU_BILGI_NOTU.md`, `Guncelle.ps1`, `Yayinla.ps1`, `BulutSoft.iss`, `Output\BulutSoft_Setup_v1.0.0.exe`.

## Gece planı (siz dinlenirken)
Yalnızca **tasarım işleri**: renk tutarlılığı, düğme biçimleri ve yerleşimi, başlık/etiket yazımı (Title Case), ipuçları, kaydırma çubuğu ve tema uyumu. İşlev ve güvenlik koduna dokunmuyorum; her değişikliği ekran görüntüsüyle doğrulayıp testleri çalıştırıyorum.
