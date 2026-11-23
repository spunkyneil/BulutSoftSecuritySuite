# BulutSoft Security Suite — Paketler

Ürün iki katmanlıdır. **Çekirdek** her müşteride, ek bir kurulum olmadan çalışır. **Uzman / MSP Paketi** isteğe bağlıdır ve ayrı bir Linux (Kali) sensör makinesi gerektirir.

## Çekirdek (varsayılan, sensör gerektirmez)
Tek Windows bilgisayarda çalışır; Kali, SSH, sanal makine ya da başka bir sunucu gerekmez.

- Bu bilgisayarın güvenlik denetimi: açık kapılar, Windows ve tarayıcı ayarları, paylaşımlar, kalıcılık (açılışta çalışan öğeler), zayıf yapılandırmalar, güncelleme eksikleri.
- Güvenlik Karnesi, KVKK ve ISO 27001 eşlemesi, HTML/PDF raporlar, yönetim kurulu özeti.
- Düzeltme eylemleri: yalnızca kullanıcı onayıyla, geri alınabilir (güvenlik duvarı kuralı, başlangıç öğesini devre dışı bırakma, korunan süreçler).
- Araç Kutusu: link analizi, dosya inceleme, parola sızıntısı (k-anonimlik), dosya bütünlüğü izleme, vaka kaydı.
- Kolay görünüm: teknik terim ve Kali göstergesi yok; sensör yokken gösterge "Uzman Sensör (İsteğe Bağlı) – Kullanılmıyor" der, hata veya eksik gibi görünmez.
- Gözetimsiz kullanım: `--tara` (JSON özet), `--filo` (klasördeki özetlerden toplu HTML/CSV rapor), pilot (salt okunur) mod, sessiz kurulum.

## Uzman / MSP Paketi (isteğe bağlı, Kali sensörü gerekir)
Uzman Görünümü'nde, "Uzak Sensörler" bölümünden bir sensör eklenince etkinleşir. Sensör yoksa bu özellikler görünmez ya da "kullanılmıyor" der; hiçbir yerde ağ denemesi yapılmaz.

- Ağır taramaları (ör. nmap) ana bilgisayar yerine ayrı bir sanal makineye taşıma.
- Sensörün SSH günlüklerinden Giriş Saldırı Radarı (kaba kuvvet tespiti).
- IP Güvenlik Sızıntı Testi'nin Kali tarafı (bilgisayarın ve sensörün internete çıkışını karşılaştırma).
- Kali konsolu, nmap komut önizlemesi, Kali araç rehberi.
- Sensör durum izleme ve kopma uyarısı.

## Neden böyle
- KOBİ müşterisi için ilk kurulumda tek paket yeter; Linux sanal makinesi kurma yükü satışı zorlaştırır.
- Uzman/MSP paketi, birden çok müşteriye hizmet veren BT firmaları ve ağır tarama isteyen uzmanlar içindir.
- Sensör olmadan ürünün hiçbir çekirdek özelliği devre dışı kalmaz; yalnızca Uzman Paketi özellikleri kullanılmaz.

## Durum notu (dürüstlük)
Sensörlü özellikler yalnızca geliştirme ortamındaki Kali sanal makinesiyle denenmiştir; müşteri ortamında doğrulanmamıştır. Paket ayrımı şu an ekran ve açıklama düzeyindedir: lisans anahtarı iki paketi ayrı fiyatlandırmaz (Lisans.cs `Surum` alanı "Pro" ve "Sahip" ayrımı yapar, "Çekirdek/Uzman" ayrımı yoktur).
