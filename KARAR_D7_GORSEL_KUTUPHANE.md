# D7 kararı — grafik ve ikon kütüphanesi (23.11.2026)

**Karar: EKLENMİYOR.** Skor halkası, trend çizgisi ve ikon seti kendi GDI+ kodumuzda kalır. ScottPlot, LiveCharts2, SkiaSharp tabanlı herhangi bir grafik paketi ve Lucide/SVG ikon kütüphanesi bu sürümde kullanılmaz. Ölçülebilir bir kazanç çıkmadı; maliyet ise ölçülebilir.

## Ölçümler

| Konu | Bugün | Kütüphane ile |
|---|---|---|
| Grafik kodu | Skor halkası `Halka.cs` 121 satır + trend çizgisi `DurumFormu.TrendCiz` ~30 satır (çizgi, 3 kılavuz, noktalar). Radar (357 sat.) ve küre (WebView2) zaten özel, kütüphane bunları değiştirmez | Aynı iş birkaç satır olurdu (kazanç ≈ 150 satır) |
| Kurulum boyutu (Hafif) | 46,5 MB | ScottPlot 5 ve LiveCharts2 ikisi de SkiaSharp ister: yalnızca `SkiaSharp.NativeAssets.Win32` paketi ~13 MB (x64 yerel DLL ≈ 9–10 MB) → Hafif paket yaklaşık %20–25 büyür |
| Bağımlılık yüzeyi | 3 paket (WebView2, QuestPDF, System.Management) | +2–4 paket, her biri için zafiyet taraması (`Yayinla.ps1`), sürüm kilitleme, yerel (native) DLL imza/yükleme sorunları |
| Veri | Trend en çok 20 nokta, tek seri, değer 0–100 | Kütüphanelerin güçlü olduğu alanlar (binlerce nokta, zoom, çoklu eksen, canlı akış) kullanılmıyor |
| İkon | 22 ikon, hepsi kullanımda; 24x24 ızgara, tema rengi, her DPI'da aynı | Lucide için SVG çözücü gerekir (Svg.NET veya SkiaSharp): ek bağımlılık; mevcut set 16–48 px'te okunur (test denetler) |

## Neden gerekmiyor

- **Kullanıcıya görünen bir eksik yok.** Halka ve trend okunaklı, tema ve DPI ile uyumlu, `DesignTokens` renkleriyle çiziliyor.
- **Kurumsal kısıtlar:** çevrimdışı ve küçük kurulum, yerel DLL'siz saf yönetilen kod, az bağımlılık (zafiyet taraması ve imza yükü).
- **Kendi çizimimiz test edilebilir:** halka ve trend çiziminin davranışı birim testlerle (ve ekran görüntüsüyle) kontrol ediliyor.

## Karar yeniden açılırsa (tetikleyiciler)

Aşağıdakilerden biri gerçekleşirse yeniden değerlendirilir: (1) trend/dağılım için çoklu seri, zoom ya da canlı akış isteği; (2) 50'den fazla yeni ikon ihtiyacı; (3) PDF/HTML raporlarda gelişmiş grafik ihtiyacı (bunun için önce QuestPDF'in kendi çizim yeteneği denenir). O zaman önce prototip + boyut/performans ölçümü yapılır.

## Korunma

`GorselKutuphaneKarariTestleri`: arayüz projesine onaysız paket (özellikle ScottPlot/LiveCharts/SkiaSharp/Svg/Lucide) eklenirse kırılır; ikon setinin her adı 16–48 px'te çizilir ve okunur olmalıdır.
