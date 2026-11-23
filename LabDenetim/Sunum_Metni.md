# BulutSoft Security Suite — Kısa Sunum Metni

## Ne için?
KOBİ'lerin ve BT ekiplerinin **kendi ağlarını** tek bir Windows uygulamasından denetlemesi, zayıflıkları sade dille görmesi, düzeltmeleri önceliklendirmesi ve sonucu yönetime ve denetime raporlaması için tasarlandı. Yalnızca özel (kendi) ağ adreslerini tarar; internetteki başkalarını taramaz.

## Temel yetenekler
- Port, web (HTTP başlıkları), TLS sertifikası ve DNS taraması; Windows güvenlik karnesi, başlangıç öğeleri, Wi-Fi ve hesap kontrolleri.
- Her bulgu için sade açıklama, hazır çözüm ve 0-100 güvenlik puanı.

## Son eklenenler
1. **Uzak sensör mimarisi:** Ağır taramalar ana bilgisayardan değil, ayrı bir Kali sanal makinesinden çalışır. Bağlantı şifreli SSH ve anahtarla yapılır, sensörde serbest komut çalışmaz. Port, HTTP, TLS ve DNS kontrolleri sensörden yapılabilir.
2. **KVKK ve ISO 27001 eşleştirmesi:** Her bulgunun yanında ilgili KVKK m.12 ve ISO/IEC 27001:2022 maddesi (tabloda, HTML ve PDF raporda).
3. **Giriş Saldırı Radarı:** Art arda hatalı parola deneyen IP'leri bulur (Windows olay 4625 ve sensördeki SSH günlüğü); engelleme onay ve yönetici izniyle yapılır.
4. **Kalıcılık Denetçisi ve Süreç Avcısı:** Başlangıç öğelerini dijital imza ve risk bilgisiyle listeler, geri alınabilir biçimde devre dışı bırakır. Fidye alarmından sonra diske çok yazan programları bulur.
5. **Yönetim raporları:** Tek sayfalık Yönetim Kurulu Risk Özeti, durum panosu, 30/60/90 günlük eylem planı, MITRE ATT&CK etiketleri, SIEM çıktısı (CEF/JSON).
6. **Kurumsal altyapı:** Roller (İzleyici, Analist, Yönetici), kurulum şifresi, koyu/açık tema, imzalı lisans (14 gün deneme; lisansı Kali imzalar, doğrulama çevrimdışı), olay zaman çizgisi, vaka defteri.

## Tasarım ilkeleri
Sistemi değiştiren hiçbir işlem otomatik yapılmaz: her biri onay ister, gerekirse yönetici izni (UAC) gerektirir ve mümkün olduğunca geri alınabilir. Veri dışarı gönderilmez; internete çıkan özellikler yalnızca onayla çalışır. Lisans bitse bile yalnızca yeni tarama kısıtlanır; kalkanlar, geçmiş ve raporlar çalışmaya devam eder.

## Bilinen sınırlar (dürüst durum)
- Uyumluluk eşleştirmesi rehber niteliğindedir; hukuki görüş veya bağımsız denetim yerine geçmez.
- Yerel lisans kontrolü, kararlı biri tarafından atlanabilir; ticari kullanımda sunucu doğrulaması gerekir.
- Bazı akışlar (sensörden HTTP/TLS/DNS, IP engelleme, UAC gerektiren işlemler) henüz gerçek operasyonel ortamda tam denenmedi.
- Şu an tek sensör ve tek merkez; çoklu makine yönetimi yok.

## Operasyonel testte bakılacaklar
Bir test ağında: (1) tarama ve rapor okunabilirliği, (2) Yönetim Kurulu özetinin karar vermeye yetip yetmediği, (3) sensör kurulum süresi ve kolaylığı, (4) yanlış alarm oranı (kalıcılık, brute-force), (5) rol ayrımı.

## Operasyon ekibine sorular
- Raporlar ve yönetim özeti, iş kararı almaya yetiyor mu? Hangi bulgular gürültü sayılır?
- Eşikler uygun mu (örn. 5 hatalı giriş = şüpheli)? Hangi sistemler kapsama girmeli (Linux sunucular, ağ cihazları)?
- Hangi rapor biçimi ve sıklığı gerekir?

## Geliştiricilere sorular
- SSH + Python ajanı (diske yazmadan, standart girişten) mimarisi ölçeklenir mi? Çoklu sensör için ne önerirsiniz?
- Lisans (ECDSA imzalı dosya) ve kimlik deposu (PBKDF2) modeli yeterli mi? Neyi güçlendirirsiniz?
- Windows'a bağımlılık ve test kapsamı için öncelikli iyileştirme nedir?
