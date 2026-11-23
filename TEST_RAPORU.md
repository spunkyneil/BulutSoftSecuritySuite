# Test sayısı ve niteliği raporu (D6) — 23.11.2026

Bu rapor `LabDenetim.Tests` projesinin neyi, nasıl sınadığını ve bilinçli bıraktığımız sınırları özetler. Sayılar betikle çıkarılmıştır (kaynak: test yöntemleri `[Fact]/[Theory]`).

## Sayılar

| Ölçü | Değer |
|---|---|
| Test dosyası | 95 |
| Test yöntemi (`[Fact]`/`[Theory]`) | 961 |
| Çalışan test (Theory durumlarıyla) | 1514 (varsayılan koşu: `Category!=Integration`) |
| Kaynak kodu okuyan "mimari kural" testi | 51 (`Trait("Tur","KaynakKurali")`) |
| Gerçek pencere/Form1 kurup çalıştıran arayüz testi | ~42 |
| Ağır "uzun" test (varsayılan koşudan çıkarıldı, `Category=Integration`) | 3 tema gezisi + gerçek Windows hizmeti/dış süreç testleri |
| Varsayılan koşu süresi | ~3 dakika |

## Nitelik dağılımı

1. **Davranış testleri (çoğunluk):** Çekirdek mantık (tarama, kural motorları, kimlik, olay zinciri, maskeleme, rapor üretimi...), sahte bağlamla denetleyici testleri (`TasinanEylemTestleri`, `ServisBaglamiTestleri`), gerçek ekran kurup çizen testler (`KabukYerlesim`, `SayfaTasma`, `DpiTasma`).
2. **Mimari kural testleri (51, kaynak tarar, BİLİNÇLİ):** Boş `catch`, doğrudan `MessageBox`/`Color.FromArgb`/`new Font`, async void, zayıf kripto, sabit sürüm/marka adı, Form1 boyutu ve partial sayısı, Core'un UI'a bağlanmaması, `HttpClient` yasağı gibi **kuralların kendisini** denetler. Bunlar davranış değil kuraldır; kaynak taraması doğru yöntemdir. `dotnet test --filter "Tur=KaynakKurali"` ile ayrıca (3 sn) koşulur.
3. **Uzun/ortam testleri (`Category=Integration`):** Tüm sayfaları açık/koyu temada gezen `TemaGidisDonusTestleri` (3 sınama, her biri 2–3 dk), gerçek Windows hizmeti ve dış sürece bağlı testler. Tema/miras kuralı (`TemaYonetici.Yakala`) değişince elle çalıştırılır.

## D6'da yapılanlar

- **`ServisBaglami` sınırı tüm arayüze yayıldı:** Eskiden yalnızca `Kabuk\` oturumu/olay günlüğünü `ServisBaglami` üzerinden kullanıyordu. Şimdi `Servisler\`, `Kontroller\` ve 13 eski ekran dahil TÜM arayüz kodu kullanıyor (17 dosya, +`IOlayOkuyucu.OkuSayfali`). Tek istisna `Program.cs` (uygulama kök noktası: hizmet yazıcısını bağlar, beklenmeyen hatayı yazar). Test (`Arayuz_DogrudanOturumAktif...`) tüm projeyi tarar; yeni doğrudan erişim eklenirse kırılır. Böylece her ekran sahte oturum/olay günlüğüyle test edilebilir.
- **Kaynak tarayan testlerden davranışa çevrilenler:**
  - `KomutPaleti_Komutlari_EksiksizVeBenzersiz...` — gerçek Form1'den `Komutlar()` listesi alınır (eskiden `Kisayol.cs` metninde "new ZamanlamaFormu" aranıyordu).
  - `KullaniciMenusu_HerOgeTamBirKez...` — menü içeriği `GlobalHeader.MenuGruplari` verisinden okunur; yeni bir `KullaniciMenuOgesi` menüye eklenmeden bırakılamaz (eskiden "Programdan Çıkış" metni aranıyordu).
- **Geriye kalan kaynak testleri neden olduğu gibi duruyor:** 51 test kural/lint niteliğinde; davranışa çevirmek ya imkânsız (ör. "kodda boş catch yok") ya da kuralı zayıflatır. Bunlar etiketlendi, sayılabilir ve ayrı koşulabilir.

## Bilinen sınırlar / öneriler

- Arayüz testleri gerçek WinForms pencereleri kurar (ekran dışı konum); çok monitörlü/%125–%150 DPI gerçek ekran denemesinin yerini tutmaz (`DpiTasmaTestleri` ölçüm tabanlıdır).
- Tam pakette zaman zaman yük altında zamanlama testleri (örn. `AtomikYazma` paralel yazma, hız sınırı) bir kez düşebilir; tek başına tekrarında geçer. Düzenli düşerse `Depo.Yaz` dosya değiştirme adımına bakılmalı.
- Kurulum paketi testleri (`KurulumProfilTestleri`) üretilmiş paket varsa özetleri/boyutu doğrular; gerçek temiz makinede kurulum DENENMEDİ (Windows Sandbox bu bilgisayarda çalışmıyor).
- Sonraki iyileştirme adayı: eski ekranların (`...Formu` sınıfları) mantığının sahte `ServisBaglami` ile ayrı sınıflara çıkarılıp davranış testine bağlanması.
