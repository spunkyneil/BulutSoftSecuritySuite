# BulutSoft Security Suite — Kalıcı Proje Kuralları

Bu dosya her oturumda yüklenir. Aşağıdaki kurallar ZORUNLUDUR; kullanıcı açıkça istisna vermedikçe değiştirilemez.

## Mimari ve Kod Kalitesi Kuralları (Zorunlu)

1. **Çekirdek İzolasyonu:** `BulutSoft.Core` projesi kesinlikle `System.Windows.Forms`, `System.Drawing`, `WebView2` veya herhangi bir UI paketine referans veremez. `BulutSoftGuardService` yalnızca `BulutSoft.Core`'a referans verir.
2. **God-Form Yasağı:** `Form1` içine yeni iş mantığı veya yeni `partial` dosyası eklenmez. Yeni ekranlar `IModulePage` uygulayan `UserControl` olarak yazılır; gereksiz yeni modal `Form` açılmaz.
3. **Tema ve Tasarım Sistemi:** Yeni veya düzenlenen hiçbir kodda doğrudan `Color.FromArgb(...)`, `new Font(...)` veya `MessageBox.Show(...)` kullanılamaz. Tüm renk/yazı tipi/boşluk değerleri `DesignTokens` üzerinden alınır; bildirimler `TemaliMesaj` ile verilir.
4. **Polling Yasağı:** Tema boyama veya UI senkronizasyonu için `Timer` ile pencere taraması yapılamaz; tüm tema geçişleri olay tabanlı (`ThemeChanged`) yürütülür.
5. **Sessiz Hata Yasağı:** Yeni boş `catch { }` yazılamaz. Kritik olmayan ve bilinçli bastırılan istisnalar mutlaka `HataKayit.Sessiz(ex, "baglam")` üzerinden geçirilir.
6. **Kurumsal Sınırlar:** Dahili satıcı/lisans üretim araçları `#if INTERNAL_VENDOR_TOOLS` dışında derlenemez.
7. **Doğrulama Şartı:** Her görev sonunda `dotnet build` sıfır hata ile geçmeli ve birim testleri kırılmadan yeşil kalmalıdır.

## Çalışma notları (kullanıcı tercihleri ve proje gerçekleri)

- Kod yorumları, arayüz metinleri ve açıklamalar Türkçe yazılır.
- Kurulum/test talimatları kullanıcıya TEK ADIM hâlinde verilir; adım bitmeden sonrası söylenmez.
- Proje git deposu değildir; büyük değişikliklerden önce kaynak zip yedeği alınır.
- Birim testleri: `dotnet test --filter "Category!=Integration"`. Gerçek Windows hizmetine, dış sürece veya makine durumuna bağlı testler `[Trait("Category", "Integration")]` taşır.
- Sürüm tek kaynaktan gelir (kökteki `Directory.Build.props`); arayüze sabit sürüm metni yazılmaz.

## Kurumsal kabuk (Faz 2) — çalışma notları

- Ana pencere `Form1` kurumsal kabuğun (`LabDenetim\Kabuk\AppShell`) EV SAHİBİdir: sol menü (`SidebarNav`, 4 grup), üst çubuk (`GlobalHeader`), sayfalar (`IModulePage`). Sayfa/sekme kataloğu ve yetki kuralları tek yerde: `Kabuk\NavigasyonModeli.cs` (eski menü işleyicilerindeki `YetkiKontrol` çağrılarıyla birebir; değiştirirsen `KabukTestleri` yetki testini güncelle).
- Yeni ekran = `IModulePage` uygulayan `UserControl` + `IKendiRenginiYoneten` + `IThemeAware` (rengini `DesignTokens`'tan alır; aksi halde `TemaYonetici` token rengini "koyu özgün renk" sanıp açık temada çevirmez). Eski `Form` ekranlar `FormSayfaAdaptoru` ile gömülür (modal "Kapat" düğmesi gizlenir; `FormKapandi(adaptör, form, DialogResult)` ShowDialog dönüşü köprüsüdür).
- Sayfaya gitmek için tek yol: `AppShell.GitAsync(sayfaId, sekmeId, zorla)` (Form1'de `KabukGit`). Komut paleti, kısayollar, bildirim zili, kullanıcı menüsü hep buradan; eski `btnMenu*.PerformClick()` / `ShowDialog()` açma kalıbı YASAK.
- Renk/yazı tipi/boşluk: `DesignTokens` (Koyu/Açık açık değerli, `Palet.cs` ile uyumlu), `FontProvider` (6 rol, paylaşımlı — Dispose etme), `Ikonlar` (GDI+ vektör), `Cizim` (çizim yardımcıları). Marka: `BrandConfig` (Core; `brand.json` ile ezilir; yalnızca görünen metin + veri klasörü adı, hizmet/pipe/olay kaynağı adları sabit). Veri klasörü yolu için `BrandConfig.RoamingKlasor/OrtakKlasor/YerelKlasor` kullan, `"BulutSoft"` yazma (test denetler).
- Kabuk\ ve Tasarim\ klasörlerinde `Color.FromArgb` (yalnız DesignTokens.cs hariç), `new Font` (yalnız FontProvider.cs hariç), `MessageBox.Show`, `Timer`, boş `catch` YASAK (`KabukTestleri` kaynak taraması).
- KVKK: dış servise giden metin `VeriMaskeleyici` (Core) ile maskelenir ve `ClaudeOnayFormu` önizlemesiyle onaylatılır; varsayılan "Vazgeç".
- Bilinçli teknik borç (Faz 3): `btnMenuAcil` ve `btnMenuNobet` görünmez durum taşıyıcı olarak kaldı (Kalkan.cs/Nobet.cs metin/renk günceller; `SavunmaUstCubuk` bunları olayla yansıtır). Eski mantık UserControl'a taşınınca silinecek. `Form1` hâlâ eski tarama çalışma alanının (`pnlAna`) sahibidir.

## Faz 3 (hijyen, God-Form sökümü, token göçü) — çalışma notları

- `Color.FromArgb` yalnızca `DesignTokens.cs`, `Palet.cs`, `TemaYonetici.cs` içinde; `new Font` yalnızca `FontProvider.cs` içinde (testler denetler: `TokenGocuTestleri`). Eski formlar rengi `DesignTokens.Ozgun(TokenRol.X)` ile alır (koyu özgün değer; Palet tablosunda kayıtlı, açık temada canlı çevrilir); yeni bileşenler `DesignTokens.Renk(rol)`. Saydamlık: `DesignTokens.Saydam(renk, alfa)`; hesaplanan renk: `Hesapla`/`Karistir`.
- `FontProvider.Al(rol, stil)` PAYLAŞIMLIDIR: `using`/`Dispose` ASLA. Çizim sırasında değişken boyut/piksel birimi/simge ailesi gerekirse `FontProvider.Gecici(...)` (çağıran `using` ile atar). (KolayModPaneli'nde `using (f1)` paylaşımlı fontu atıp çizimi kilitlemişti.)
- async olay işleyici: `GuvenliCalistir.Baslat(async () => ..., "bağlam")` (async void yasak). Yalnızca görsel zamanlayıcılar `GorunurZamanlayici` ile küçültünce durur; güvenlik zamanlayıcıları (Nöbetçi, tuzak/fidye, Brute-Force) asla.
- Acil/Nöbetçi durumu `BulutSoft.Core.SavunmaDurumServisi` (olay tabanlı); `btnMenuAcil/btnMenuNobet` kalktı.
- Form1 inceltme: `AnalizMetniUretici`, `OturumEylemleri`, `PencereHafizasi`, `KabukYetki` ayrı sınıflar. Tarama alanından dışarı taşınan özellikler `Servisler\*Eylemi.cs` sınıflarıdır (KanitPaketi, AltinProfil, ZincirEylemi, AramaKabul, SensorYeriSecici, UsbKalkani, Tuzak, HedefSecici, AcilIzolasyon, MuhurKontrolu, NobetCihazKontrolcusu) ve Form1'e yalnızca dar `ITaramaBaglami` arayüzüyle bağlanır (Form1.cs içinde açık uygulanır); yeni özellik Form1 alanlarına değil bu arayüze bağlanır, sahte bağlamla test edilir (`TasinanEylemTestleri`). Partial sayısı çıtası 21 (`Form1InceltmeTestleri`): kalan partial'lar tarama ekranının kendisidir (Gunluk, Tablo, Filtre, Profil, Kartlar, Asistan, Trafik, Nöbetçi, Kolay mod...), UserControl'e dönüşünce silinir.
- DPI: `ApplicationHighDpiMode=PerMonitorV2`; `TemaliForm` `AutoScaleMode.Dpi` ile kodla yazılmış Size/Padding'i ölçekler. Yeni özel çizimde sabit piksel yerine `DesignTokens.Px(control, değer)` kullan. `DpiTasmaTestleri` ölçüm tabanlıdır; gerçek çok monitörlü/%125-%150 ekran denemesinin yerini tutmaz.
- Kurumsal dil: kullanıcı metinlerinde "laboratuvar" / "Sanal Lab" yok ("İç Ağ Keşfi", "yetkili test ortamı"); test denetler.
- Test yardımcıları `GuvenliCalistir.HataBildirici`'ı yakalar (arka plan hatası modal açıp testi kilitlemesin).

## Faz 4 (üretim dayanıklılığı) — çalışma notları

- Olay günlüğü: sık yoklamalar (küre ışığı, zil, durum panosu) `OlayDeposu.SonUyariKritik/SonOlaylar` (artımlı okuma + halka tampon, `OlayOnbellegi.cs`) kullanır; tam tarama yalnızca `OkuSayfali` (Olay Zaman Çizgisi) / `Oku` / `Dogrula` ile. Sık durum yoklaması `Dogrula(true)` (önbellekli); bütünlük eylemleri `Dogrula()` (tam). Zincir HMAC ve 2 MB arşiv rotasyonuna dokunma.
- Dış ağ: `new HttpClient` YASAK (yalnızca yerel hedef tarayıcıları: AktifHttp, SahiplikDogrulama, PasifToplayici, HttpDenetimi, GirdiDogrulamaTesti; hepsinde açık Timeout). Dış servisler `AgIstemciFabrikasi` profilleriyle (açık zaman aşımı, CancellationToken, Türkçe `AgHatasi`). Yeni dış çağrı = yeni `AgProfili` + zaman aşımı tablosu + sahte işleyicili test.
- Kalıcı durum yazımı `Depo.Yaz/YazJson/YazBayt` (benzersiz geçici ad + yer değiştirme); elle `+ ".tmp"` yasak (test denetler).
- `kure.html`: gizli/küçültülmüşken `bsDuraklat(true)` ile rAF döngüsü TAMAMEN durur; uyarlanabilir kare hızı ve durağan taban önbelleği. Gerçek WebView2 testi `KureDuraklatmaTestleri`.
- Test izolasyonu: kabuk ve çekirdek `ServisBaglami.Oturum/OlayOkuyucu/OlayYazici` arayüzlerini kullanır (Kabuk\ içinde doğrudan `Oturum.Aktif`/`OlayDeposu` yasak); testlerde sahte atayıp `ServisBaglami.Sifirla()`.

## Faz 5 / D1 / D2 — çalışma notları

- Paketler 23.11.2026 01:33'te D0–D7 sonrası kodla yeniden üretildi (zafiyet taraması temiz, 1516 test yeşil, SHA256 doğrulandı: Hafif 44,4 MB, Çevrimdisi 324,7 MB; `Kurulum Profil` testleri geçti). Exe/dll İMZASIZ (sertifika yok; `-Imzala` ile imzalanabilir). Gerçek makinede kurulum ve açılış denemesi YAPILMADI.
- Kurulum: iki profil (`Hafif` ~45 MB: WebView2'yi gerekirse indirir ve Microsoft imzasını doğrular; `Cevrimdisi` ~325 MB: WebView2 gömülü). `.\Yayinla.ps1` ikisini de üretir (önce zafiyet taraması + testler). Gerçek makinede kurulum DENENMEZ (Sandbox bu bilgisayarda çalışmıyor); betik/çıktı testleri `KurulumProfilTestleri`.
- Klasör düzeni: kökte yalnızca kaynak projeler, betikler, belgeler, `Kurucular\` (WebView2 yükleyicisi, kurulum derlemek için gerekli), `Kaynak Yedekleri\` (git yok: tek geri dönüş), `22.11.2026 tarihli Arşiv Yedek Program\`. `bin/obj/yayin` yeniden üretilebilir, repoda tutulmaz.
- MITRE: ATT&CK v20 taksonomisi (`Mitre.Surum`); "Defense Evasion" YOK (Stealth / Defense Impairment); T1562.001 → T1685, T1562.004 → T1686.003. Yeni ATT&CK sürümünde `Mitre.cs` ve `DogrulukBorcuTestleri` güncellenir.
- CIS Benchmarks içeriği kopyalanmaz, "CIS uyumlu" iddiası yazılmaz (SecureSuite Product Vendor üyeliği gerekir; test denetler).
- KVKK: raporlarda dayanak `Uyumluluk.Kaynak` (Rehber Nisan 2025 Tablo 4.1); adli kanıt paketine `07_KVKK_ihlal_mudahale_plani.txt` (72 saat) girer.
- Güvenli kodlama: DPAPI çağrıları entropili (Claude anahtarı dahil, eski kayıt göç eder); zayıf kripto yasak; sertifika bypass yalnızca yerel hedef tarayıcılarında. Matris: `ASVS_OZ_DEGERLENDIRME.md` (açık kalanlar orada).

## D4 — Tarama ekranı UserControl (1.–5. dilim, tamam)

- "Yeni Tarama + Asistan" ekranı TAMAMEN `Kontroller\TaramaSayfasi*.cs` içindedir (UserControl, `partial`): `TaramaSayfasi.cs` arayüzü kurar (denetimler `internal` alanlar), `TaramaSayfasi.Calistir.cs` tarama çalıştırma/sonuç/rapor/durum + `ITaramaBaglami` uygulaması + `Servisler\*Eylemi` denetleyicilerinin kurulumu, diğer parçalar (Asistan, YapayZeka, Trafik, Kartlar, Gorunum, Filtre, Profil, Tablo, Gunluk, Gecmis, DisAgAkis, Tema) eski Form1 mantığıdır. Yeni tarama ekranı kodu BURAYA yazılır; Form1'e değil.
- Form1 yalnızca kabuk iskeleti ve bağlantı noktasıdır (2 partial: Form1.cs + Designer; çıta `Form1InceltmeTestleri`, Form1.cs ~170 satır). Form1 tarama ekranına `_tarama.X` ile ulaşır.
- 4. dilim: Form1'de kalan mantık kendi sınıflarına taşındı (hepsi `Servisler\`): `NobetciEylemi` (Nöbetçi, tepsi, koruma hizmeti izleme; Form1'de `_nobet`), `KisayolEylemleri` (Komut Paleti, zamanlama, başarılar, eğitim; `_kisayol`), `KolayModEylemleri` (Kolay kartlar, küre yansıtma, mod; `_kolayMod`), `KaliGostergeleri` (Kali/IP/bildirim/koruma göstergeleri, Merkez geçişi; `_kolayMod._kali`), `UzmanKureYoneticisi` (harita küresi, şerit, ışık; `_kure`). Hepsi ana pencereden yalnızca `TemaliForm` + dar eylemler (`kabukGit`, `Func<AppShell?>`) alır; `ProcessCmdKey` (override) ve `PencereKur()` Form1.cs'te kalır. Yeni özellik bu sınıflara ya da kendi sınıfına yazılır, Form1'e değil.
- 5. dilim: kabuk bağlantı tablosu (`KabukBaglantilari`: hangi sayfa hangi eski ekranı/eylemi açar) `Kabuk\KabukBaglantiFabrikasi.cs`'e taşındı; Form1.cs ~150, Designer ~90 satır (yalnızca iskelet). Yeni sayfa/eylem bağlantısı fabrikaya eklenir. D4 tamam: tarama ekranı UserControl, Form1 ince kabuk.
- 3. dilim: tarama ekranı Form1'e DEĞİL `ITaramaSahibi` arayüzüne (`Servisler\TaramaSahibi.cs`, Form1'de açık uygulanır; alan adı `Sahibi`) bağlıdır; `Ana` kalktı, sahte sahiple pencere olmadan sınanır. Yardım/Hakkımızda/Tur `Kabuk\YardimEylemleri` sınıfına taşındı (Menu.cs, Bilgi.cs silindi). Yeni pencere düzeyi ihtiyaç = `ITaramaSahibi`'ye üye, Form1'e doğrudan başvuru değil.
- Sonraki: bkz. D3 bölümü.

## D5 — Eski Form ekranları gerçek UserControl sayfa (tamam)

- Kabuktaki 13 eski ekran (Durum, Olay, Geçmiş, Cihaz, Ağ Haritası, Kalıcılık, Kalkan, Tuzak, Gözetimsiz, Giriş Radarı, Araç Kutusu, Sensör, Merkez) artık `Form` DEĞİL, `Kabuk\SayfaDenetimi` (UserControl) türevidir; `FormSayfaAdaptoru` ve "TopLevel=false ile Form gömme" kalktı. Sınıf adları geriye uyum için `...Formu` kaldı.
- `SayfaDenetimi` eski kodun Form yüzeyini korur: `Close()`, `DialogResult`, `Shown`, `FormClosing/FormClosed` (atılınca da bir kez çalışır), `StartPosition` (yok sayılır). `MinimumSize` sayfayı kesmez: `AutoScrollMinSize` + `AutoScroll` olur (alan küçükse kaydırılır).
- Tutucu `Kabuk\DenetimSayfasi` (eski FormSayfaAdaptoru): üretir, `Dock=Fill` koyar, `DenetimHazir`/`DenetimKapandi` köprüleri, `HerAcilistaYenile`, modal "Kapat" düğmesini gizler. `KabukBaglantilari.XFormu` temsilcileri `Func<SayfaDenetimi>`.
- YENİ ekran = `IModulePage` + `IThemeAware` UserControl (CLAUDE.md kural 2); `SayfaDenetimi` yalnızca bu 13 eski ekranın taşıma tabanıdır, yeni ekranda kullanma. Testte tek sayfayı sınamak için `SayfaTestKabi.Goster(sayfa)` (test projesi).
- Sonraki: D3 (tek tema yolu: `DesignTokens.Ozgun`/Palet ikiliği).

## D3 — Tek tema yolu (tamam)

- Renk kaynağı TEK: `DesignTokens` (21 rol, iki tema). `Palet` yalnızca eski ekranların sabit yazılı KOYU renklerini açık temada rollere çeviren tablodur (`Palet.Tablo`); ortak 14 rolün (Sayfa, Kart, Girdi, Izgara, KenarUstunde, Metin, Basari/Uyari/Tehlike/Bilgi Metin+Arka) değeri Palet'te TEKRAR yazılmaz, `DesignTokens`'tan okunur (`TokenRolu`; test denetler). Token karşılığı olmayan Palet-özel roller (Gomulu, Secim, Kenar, IkincilMetin, Soluk, Hayalet, Mor, TehlikeKenar...) Palet.cs'te kalır.
- Eski HSL "ters çevirme" yedeği (`TemaYonetici.Cevir`) KALDIRILDI. Tabloda olmayan renk açık temada olduğu gibi bırakılır ve `Palet.Bilinmeyen`'e yazılır; `TemaTekYolTestleri` tüm kabuk sayfalarını (Kolay+Uzman, her sekme) ve ana iletişim pencerelerini açık temada gezip bu kümenin BOŞ olmasını ister. Yeni/eski ekrana sabit renk eklersen: `DesignTokens.Renk(rol)` ya da `Palet.Renk(rol, koyu)` kullan; eski koyu sabit renk gerekiyorsa `Palet.Tablo`'ya ekle.
- DesignTokens değerleri (iki temada) ve Palet değerleri "zaten çevrilmiş" sayılır (TemaYonetici yeniden çevirmez).
- TUZAK: `Palet` statik başlatıcısında `DesignTokens`/`BrandConfig`'e DOKUNMA (iş parçacıkları arasında statik başlatma kilitlenmesi; testler zaman aşımına düşer). Token'a bağımlı kümeler tembel (`Lazy`) kurulur.
- Sonraki: eski ekranların tek tek modern sayfalara yeniden yazımı (D6/D7 kararları).

## Kullanıcı geri bildirimi düzeltmeleri (2026-11-23)

- Küre şeridindeki "Demo yayları" düğmesi KALDIRILDI; örnek (sahte) yaylar hiçbir koşulda gösterilmez (`GorselAyarlar.DemoYaylar` her zaman false). Gerçek trafik olmayan görsel kullanıcıyı yanıltır.
- Olay Zaman Çizgisi'nde `Oturum` ve `Kullanıcı` türü olaylar GÖRÜNMEZ (`KullaniciGunlugu.ZamanCizgisindeGizli`); kayıtlar yine zincirli olay günlüğüne yazılır (denetim izi). Kullanıcıya özel etkinlik günlüğü `BulutSoft.Core\KullaniciGunlugu.cs`: `KullaniciGunlugu.Yaz("eylem")` ("KULLANICI -> ad: eylem"); "Hesabım ve Kullanıcılar" ekranının altında SEÇİLİ kullanıcının kayıtları (eylemler + oturum olayları) listelenir. Yeni kullanıcı eylemi eklerken `KullaniciGunlugu.Yaz` çağır (sayfa açma, tarama başlat/iptal, Nöbetçi aç/kapat, Acil İzolasyon, rapor, tema, görünüm, çıkış zaten yazılıyor).
- Kullanıcı menüsüne "Programdan Çıkış" eklendi (`KullaniciMenuOgesi.Cikis`; Nöbetçi açıksa FormClosing onayı sorar).
- TEMA HATASI (beyaz/yanlış renkli alan kalıyor, tekrar değiştirince düzeliyor): `TemaYonetici.Yakala` AÇIK temada oluşan denetimin üstten devraldığı, zaten açığa çevrilmiş rengi "özgün koyu" sanıyordu. Artık üstüyle aynı rengi taşıyan denetim (girdi denetimleri hariç: kutu, liste, düğme, ızgara) rengi MİRAS alır; sistem rengi yalnızca eski miras türlerinde (Label, Panel...) devralınır. `TemaGidisDonusTestleri` (koyu→açık→koyu, açık→koyu→açık ve "açıkta oluşup koyuya dönen sayfalar = doğrudan koyuda oluşanlar") bunu denetler. Yeni denetim türü eklerken girdi türüyse `kendiZemini` listesine bak.

## D6 — Test ve bağımlılık borcu (tamam)

- `ServisBaglami` sınırı TÜM arayüz projesinde geçerli: kabuk, `Servisler\`, `Kontroller\` ve eski ekranlar oturumu `ServisBaglami.Oturum`, olay günlüğünü `ServisBaglami.OlayYazici/OlayOkuyucu` ile kullanır; doğrudan `Oturum.Aktif` / `OlayDeposu.Ekle|Oku|OkuSayfali|SonOlaylar|SonUyariKritik` yalnızca `Program.cs`'te (kök nokta). `ServisBaglamiTestleri.Arayuz_Dogrudan...` tüm projeyi tarar. Salt işlevsel yardımcılar (`OlayDeposu.Siniflandir/Yol/CsvYaz`) serbest.
- Kaynak kodu okuyan 51 kural/lint testi `Trait("Tur","KaynakKurali")` ile etiketli (ayrı koşulur: `--filter "Tur=KaynakKurali"`); yeni kaynak tarayan test yazarsan etiketle, davranışa çevrilebiliyorsa çevir (örn. menü içeriği/komut listesi veri olarak: `GlobalHeader.MenuGruplari`, `KisayolEylemleri.Komutlar()`). Rapor: `TEST_RAPORU.md`.

## D7 — Grafik/ikon kütüphanesi kararı (tamam: EKLENMİYOR)

- Skor halkası, trend çizgisi ve ikon seti (`Tasarim\Ikonlar.cs`) GDI+ ile kendi kodumuzda kalır; ScottPlot/LiveCharts2/SkiaSharp/SVG ikon kütüphanesi eklenmez (Hafif paket ~%20-25 büyür, yerel DLL ve bağımlılık yükü; ölçülebilir kazanç yok). Gerekçe ve yeniden açma tetikleyicileri: `KARAR_D7_GORSEL_KUTUPHANE.md`. `GorselKutuphaneKarariTestleri` arayüz projesine onaysız paket eklenirse kırılır (yeni paket = belgeyi ve test listesini bilinçli güncelle).

## Bilinen eksikler (bu kurallar tamamlanana kadar geçerli)

- Eski koddaki `catch (Exception) { /* ... */ }` benzeri bilinçli bastırmalar `HataKayit.Sessiz` ile geçer; yeni boş catch yazma.
- Eski koddaki mevcut `Color.FromArgb` / `new Font` / `MessageBox.Show` kullanımları korunur; düzenlediğin satıra YENİ sabit renk/yazı tipi/boşluk eklenmez (satırdaki eski değerler, satırın yalnızca metin/mantık kısmı değiştiğinde olduğu gibi kalabilir).
- Dokunduğun dosyalardaki boş `catch` blokları `HataKayit.Sessiz(...)` ile değiştirilir (BulutSoft.Core'da hiç boş catch kalmamalı; bir test bunu denetler).
- Yeni pencere `Form` değil `TemaliForm`'dan türer (bir test denetler); elle üretilen pencere `TemaYonetici.KaydetVeUygula(form)` çağırır. Tema olayı: `TemaYonetici.TemaDegisti` (= `ThemeChanged`).
