# BulutSoft Security Suite — Toplu Dağıtım Kılavuzu (BT yöneticisi için)

> Durum: `BulutSoft.iss` Inno Setup 6.7.4 ile hatasız derlendi (`BulutSoft_Setup_v1.0.0.exe`, ~42 MB, **imzasız**). Kurulumun kendisi (sessiz kurulum, izinler, pilot bayrağı, güncelleme, kaldırmada kullanıcı verisinin korunması) henüz hiçbir makinede **denenmedi**; ilk denemeyi sanal makinede yapın. İmzalama için kod imzalama sertifikası ve `signtool` gerekir.

## 1. Kurulum paketini üretme
```powershell
.\Yayinla.ps1                 # testler -> win-x64 self-contained derleme -> Inno Setup -> SHA256SUMS.txt
.\Yayinla.ps1 -Surum 1.2.0    # sürümü elle ver (verilmezse LabDenetim.csproj içindeki <Version>)
.\Yayinla.ps1 -Zip            # ayrıca taşınabilir zip üretir
```
- Tek bir otomatik test başarısızsa hat **durur**, hiçbir dosya üretilmez.
- Çıktı: `Output\BulutSoft_Setup_v<sürüm>.exe` ve yanında `Output\SHA256SUMS.txt`.
- Dağıtımdan önce özeti doğrulayın: `(Get-FileHash BulutSoft_Setup_v1.0.0.exe -Algorithm SHA256).Hash` değeri `SHA256SUMS.txt` ile aynı olmalı.

## 2. Sessiz kurulum (yönetici hakkıyla, tüm kullanıcılar için, `C:\Program Files\BulutSoft`)
```powershell
BulutSoft_Setup_v1.0.0.exe /VERYSILENT /NORESTART
```
Kurulum hiçbir diyalog göstermez. Yönetici hakkı olmadan çalıştırılamaz (kullanıcıya özel kurulum yoktur).

Pilot (salt okunur) mod açık kurulum — program güvenlik duvarı, süreç ve başlangıç öğesi değiştirmez:
```powershell
BulutSoft_Setup_v1.0.0.exe /VERYSILENT /NORESTART /TASKS="pilot"
```
Pilot modu sonradan kapatmak: `%ProgramData%\BulutSoft\salt_okunur.flag` dosyasını yönetici olarak silin.

Kaldırma: `"%ProgramFiles%\BulutSoft\unins000.exe" /VERYSILENT /NORESTART`
Kaldırma `%AppData%\BulutSoft` klasörüne (lisans, geçmiş, kimlik, güvenlik duvarı ve kalıcılık yedekleri) **dokunmaz**; ProgramData altındaki lisans klasörü ve pilot bayrağı da silinmez.

## 3. GPO (başlangıç betiği) ile
Bilgisayar Yapılandırması → Windows Ayarları → Betikler → Başlangıç. Betik, paylaşımdaki kurulum dosyasını çalıştırır:
```powershell
\\sunucu\yazilim\BulutSoft_Setup_v1.0.0.exe /VERYSILENT /NORESTART /TASKS="pilot"
```
Aynı sürüm yeniden çalıştırılırsa üzerine kurulur; zararsızdır.

## 4. Intune (Win32 uygulaması)
- Kurulum komutu: `BulutSoft_Setup_v1.0.0.exe /VERYSILENT /NORESTART`
- Kaldırma komutu: `"%ProgramFiles%\BulutSoft\unins000.exe" /VERYSILENT /NORESTART`
- Algılama kuralı: dosya `C:\Program Files\BulutSoft\BulutSoftSecuritySuite.exe` var.

## 5. Filo taraması (sunucusuz)
Her makinede zamanlanmış görev (SYSTEM veya yönetici hesabıyla, haftalık):
```powershell
"C:\Program Files\BulutSoft\BulutSoftSecuritySuite.exe" --tara --cikti "\\sunucu\ozetler"
```
BT yöneticisi tek makinede toplu raporu üretir:
```powershell
BulutSoftSecuritySuite.exe --filo \\sunucu\ozetler
```
Paylaşım klasörüne yalnızca makine hesaplarına "yazma", yöneticilere "okuma" verin. Raporlama güvenmediği dosyaları atlar.

## 6. Gizlilik ve veri akışı
`BulutSoftSecuritySuite.exe --gizlilik` programın dışarı giden tüm bağlantılarını listeler.

## 7. Güncelleme
Yeni sürümün `BulutSoft_Setup_v<sürüm>.exe` dosyasını aynı komutla dağıtın. Sabit uygulama kimliği sayesinde eski sürümün üzerine kurulur; çalışan program otomatik kapatılır, kurulum sonrası yeniden başlatılmaz.

## 8. Kod imzalama (ileride)
Sertifika alındığında: `BulutSoft.iss` içindeki `;SignTool=bulutimza` ve `;SignedUninstaller=yes` satırlarının başındaki `;` işaretini kaldırın, `Yayinla.ps1` içindeki ISCC çağrısına `/Sbulutimza="signtool.exe sign ... $f"` ekleyin (şablon `.iss` dosyasının `[Setup]` bölümünde yorum olarak hazır). Program exe'lerini imzalamak için `.\Yayinla.ps1 -Imzala sertifika.pfx` (parola `BULUT_PFX_PAROLA` ortam değişkeninden okunur).

## 9. Doğrulanması gerekenler (ilk pilotta)
- `--tara` Görev Zamanlayıcı'da SYSTEM hesabıyla çalışınca özet dosyası ağ paylaşımına yazılabiliyor mu (makine hesabı yetkisi).
- Salt okunur bayrağı klasörünün yetkisi: normal kullanıcı dosyayı silemiyor mu.
- Kurulum Türkçe görünüyor mu, simge doğru mu, `/VERYSILENT` hiç diyalog çıkarmıyor mu.
- Kaldırmadan sonra `%AppData%\BulutSoft` içeriği duruyor mu.
- Kurulum yönetici hakkıyla yapıldıktan sonra her Windows kullanıcısı kendi verisini `%AppData%\BulutSoft` altında ayrı tutar; kullanıcı hesapları makine geneli paylaşılmaz.

## Kurulum profilleri (Faz 5)

`.\Yayinla.ps1` iki paket üretir (aynı sürüm, aynı uygulama; fark yalnızca WebView2 çalışma zamanı):

| Paket | Boyut | Ne zaman |
|---|---|---|
| `BulutSoft_Setup_v<sürüm>_Hafif.exe` | ~45 MB | İnternet var. WebView2 yoksa kurulum sırasında Microsoft'tan indirilir; **Microsoft imzası doğrulanmadan çalıştırılmaz**. İndirilemezse kurulum yine biter, yalnızca Kolay Görünüm küresi boş kalır. |
| `BulutSoft_Setup_v<sürüm>_Cevrimdisi.exe` | ~325 MB | İnternetsiz / kapalı ağ. WebView2 kurucusu pakete gömülü. |

Her ikisi de aynı komutlarla sessiz kurulur (`/VERYSILENT /NORESTART`) ve aynı uygulama kimliğiyle birbirinin üzerine güncellenir. `SHA256SUMS.txt` iki satır içerir. `Guncelle.ps1` varsayılan olarak Hafif paketi seçer (`-Profil Cevrimdisi` ile değişir).
