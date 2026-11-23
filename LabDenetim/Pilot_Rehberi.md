# Pilot Rehberi: Derin Denetim ve Yanlış Alarm Kalibrasyonu

Bu belge, derin denetim özelliklerinin gerçek bir ortamda (pilot) denenmesi ve **yanlış alarm oranının ölçülmesi** içindir.
**Durum: pilot henüz YAPILMADI.** Kod tarafı sahte veriyle ve tek bir gerçek makinede salt-okunur denendi; aşağıdaki adımlar pilotu yapacak kişi içindir.

## 1. Hazırlık (yaklaşık 30 dakika)

1. En az 3 farklı cihaz seçin (örn. bir iş istasyonu, bir sunucu, bir geliştirici bilgisayarı). Yalnızca **sahibi olduğunuz / yazılı izniniz olan** sistemler.
2. Her cihazda uygulamayı açın (yeni exe). **Yönetici** hesabıyla giriş yapın.
3. **KEV listesini indirin** (Ağ Haritası > "KEV Listesini İndir...", onay penceresinde adresi okuyun).
4. **NVD verisi** (isteğe bağlı ama önerilir): kendi indirdiğiniz NVD 2.0 JSON dosyalarını "NVD Verisi Yükle..." ile aktarın. Kurulu yazılım bilgisi NVD'ye gönderilmez.
5. **KB kataloğu** (isteğe bağlı): Windows yapı numaranıza göre hazırladığınız katalog dosyasını "KB Kataloğu Yükle..." ile aktarın (biçim: Tasarim_Bulgu_Semasi_ve_Moduller.md).

## 2. Denetimleri çalıştırın

Her cihazda: **Ağ Haritası > "Bu Bilgisayarı Denetle"**. İç ağdaki web sunucuları için ayrıca **"Aktif Web Denetimi..."** (yalnızca iç ağ; her seferinde onay).

## 3. Her bulguyu ETİKETLEYİN (pilotun asıl işi)

Sonuç penceresinde her bulgu için karar verin:

| Karar | Ne yapılır |
|---|---|
| **Gerçek sorun** | Hiçbir şey yapmayın (listede kalır). Düzeltildikten sonra aynı denetimi tekrar çalıştırın: bulgu kaybolmalı (doğrulama). |
| **Yanlış alarm** | Bulguyu seçin, nedenini yazın, **"Yanlış Alarm İşaretle"**ye basın. |
| **Emin değilim** | Not alın, sonra bir uzmana danışın; etiketlemeyin. |

Yanlış alarm olarak işaretleme nedenleri örnekleri: "bu bir test cihazı", "yol aslında tırnaklı", "yazılım başka bir ürün (eşleme hatalı)", "klasör aslında yalnızca yöneticiye yazılabilir".

## 4. Ölçüm

Pilotun sonunda "Ağ Haritası > Kural Kalitesi..." penceresine bakın ve **"Derin Denetim Raporu..."** ile HTML rapor alın ("Yanlış Alarm ve Kural Kalitesi" bölümü).

Hedefler:

- **Yüksek** önemli bulgularda yanlış alarm oranı **≤ %10**.
- Hiçbir kural, en az 10 örnekle **%30'u** aşmamalı (aşarsa "gözden geçirilmeli" olur ve puanı %25 düşer).
- Kaçırılan açık (false negative) için: bilinen bir açığı (örn. kasıtlı olarak yazılabilir bırakılmış bir test klasörü) kurup programın bulup bulmadığına bakın.

## 5. Sonuçları değerlendirme

1. Oranı yüksek kurallar için: kuralın JSON dosyasını (`pasif_kurallar.json` / `aktif_kurallar.json`, kullanıcı geçersiz kılma dosyası `%AppData%\BulutSoft\`) veya `cpe_esleme.json` tablosunu düzeltin.
2. Yanlış eşleşen ürünler için `%AppData%\BulutSoft\cpe_esleme.json` dosyasına kendi satırlarınızı ekleyin (kullanıcı tablosu önceliklidir).
3. Pilot sonuçlarını (cihaz sayısı, bulgu sayısı, kural başına yanlış alarm oranı) bir tabloya yazıp sürüm notuna ekleyin.

## 6. Güvenlik notu

Bu denetimler **okur**; sistemde ayar değiştirmez. Bulduğu sorunları düzeltmek için **sistemi değiştiren her adım elle** ve yönetici onayıyla yapılmalıdır. Pilot sırasında üretim sistemlerinde düzeltme yapmadan önce yedek alın.
