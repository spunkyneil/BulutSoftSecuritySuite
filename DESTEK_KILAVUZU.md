# BulutSoft Security Suite — Destek Kılavuzu (ilk hat)

Bu kılavuz kod ve testlerden derlenmiştir; gerçek müşteri sahasında henüz sınanmamıştır.

## Veriler nerede?
Her Windows kullanıcısı için ayrı: `%AppData%\BulutSoft`
| Dosya | Ne |
|---|---|
| `kimlik.json`, `muhur.json`, `muhur.key` | Hesaplar ve bütünlük mührü (DPAPI; başka kullanıcı/makinede açılmaz) |
| `bulgular_v2.json`, `ag_haritasi.json`, `fp_takip.json` | Bulgular, ağ haritası, yanlış-pozitif işaretleri |
| `denetim_izi.jsonl`, `sensor_denetim.log` | Denetim izi ve sensör günlüğü |
| `kalicilik_yedek.json` (+ `.bak`) | Başlangıç öğesi geri alma yedeği |
| `lisans.bslic` | Lisans |
| `ozetler\` | `--tara` özet dosyaları (varsayılan) |
Pilot bayrağı: `%ProgramData%\BulutSoft\salt_okunur.flag`

## Sık sorunlar
1. **"Zaten Açık" uyarısı:** aynı Windows kullanıcısında bir kopya çalışıyor (görev yöneticisinde `BulutSoftSecuritySuite`). Yönetici ile normal hesap ayrı sayılır; çalışan kopyayı kapatın.
2. **"Bu işlem için yetkiniz yok / PİLOT modunda":** rol İzleyici/Analist ise bir Yönetici gerekir. Mesaj "pilot" diyorsa makinede salt okunur bayrak var; sistemi değiştiren işlemler bilerek kapalıdır. Kaldırmak için yönetici olarak bayrak dosyasını silin.
3. **Parola unutuldu:** Kullanıcı yönetimindeki "Parolayı Kurtar", ilk kurulumda belirlenen **kurulum şifresi** ile çalışır. Kurulum şifresi de kayıpsa kimlik dosyası geri getirilemez; `kimlik.json` silinirse program ilk kurulum ekranına döner (eski hesaplar gider, tarama verileri kalır).
4. **Kimlik "bozuk" görünüyor:** mühür doğrulanamadı (dosya elle değişmiş ya da başka makineden kopyalanmış). Yedekten geri koyun ya da yukarıdaki sıfırlamayı uygulayın.
5. **`--tara` çıkış kodları:** 0 başarılı, 1 hatalı kullanım, 2 tarama/yazma hatası. Yönetici olarak çalıştırılmayan tarama bazı kontrolleri atlar; özette "Yönetici: hayır" yazar ve filo raporu uyarır.
6. **Filo raporunda makine eksik:** o makinenin `.json` özeti klasöre gelmemiştir ya da 5 MB üstü/bozuk/yeni şemalıdır; rapor uyarılar bölümü nedenini yazar.
7. **Süreç sonlandırılamıyor:** hizmet, veritabanı, yedekleme ve sistem süreçleri bilerek korunur; mesaj nedeni yazar.

## Toplanacak bilgi (destek talebinde)
Program sürümü, Windows sürümü, rolü, `--gizlilik` çıktısındaki pilot durumu, hatanın tam metni ve `denetim_izi.jsonl` ile `sensor_denetim.log` dosyalarının son satırları. Parola veya API anahtarı istenmez.

## Bilinen sınırlar
Sunucusuz çalışır; merkezî konsol ve otomatik güncelleme yoktur. Deneme kaydı ve lisans dosyası makine geneli: %ProgramData%\BulutSoft\Lisans (lisans.bslic buraya konursa tüm kullanıcılara geçerli). Kurum geneli lisans: Lisans penceresinde müşteri lisansı üretirken makine kimliği yerine * yazılır (imzalı, tüm makinelerde geçerli, süresi denetlenir). Uygulama imzasızdır: SmartScreen/AV uyarısı çıkabilir.
