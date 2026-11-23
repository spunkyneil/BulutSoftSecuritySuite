; BulutSoft Security Suite - Inno Setup 6 kurulum betiği (kurumsal B2B dağıtımı).
; Doğrudan derlemeyin; .\Yayinla.ps1 önce testleri çalıştırır, yayın klasörünü hazırlar ve bu betiği derler.
; Sürüm komut satırından gelir:  ISCC.exe /DSurum=1.0.0 BulutSoft.iss
;
; KURUMSAL SESSİZ KURULUM (yönetici hakkı gerekir; kurulum hiçbir diyalog/MsgBox göstermez):
;   BulutSoft_Setup_v1.0.0.exe /VERYSILENT /NORESTART
; Pilot (salt okunur) mod ile birlikte - program makinede hiçbir değişiklik yapmaz:
;   BulutSoft_Setup_v1.0.0.exe /VERYSILENT /NORESTART /TASKS="pilot"
; Kaldırma (kullanıcı verisine dokunmaz):
;   "%ProgramFiles%\BulutSoft\unins000.exe" /VERYSILENT /NORESTART
#ifndef Surum
  #define Surum "1.0.0"
#endif
; İKİ KURULUM PROFİLİ (ISCC /DProfil=Cevrimdisi veya /DProfil=Hafif; verilmezse Cevrimdisi):
;   Cevrimdisi : WebView2 çalışma zamanı kurucusu (~280 MB) pakete GÖMÜLÜDÜR; internetsiz/kapalı ağ makinelerinde de canlı küre çalışır.
;   Hafif      : WebView2 kurucusu gömülü DEĞİL (paket çok daha küçük); makinede WebView2 yoksa kurulum sırasında Microsoft'tan indirilir, Microsoft imzası doğrulanırsa kurulur.
;                İnternet yoksa/indirme ya da imza doğrulaması başarısızsa kurulum yine tamamlanır; yalnızca Kolay Görünüm küresi boş kalır, program çalışır.
#ifndef Profil
  #define Profil "Cevrimdisi"
#endif
[Setup]
; Sabit kimlik: aynı kimlikli yeni sürüm eskisinin üzerine temiz kurulur (güncelleme). ASLA DEĞİŞTİRMEYİN.
AppId={{17989392-9F62-41F9-B534-169A6CDF007C}
AppName=BulutSoft Security Suite
AppVersion={#Surum}
AppVerName=BulutSoft Security Suite {#Surum}
AppPublisher=BulutSoft Güvenlik Teknolojileri
VersionInfoVersion={#Surum}
VersionInfoCompany=BulutSoft Güvenlik Teknolojileri
VersionInfoProductName=BulutSoft Security Suite
VersionInfoDescription=BulutSoft Security Suite Kurulumu
; 64-bit Program Files altına, yönetici hakkıyla, makine geneli kurulum.
PrivilegesRequired=admin
DefaultDirName={autopf}\BulutSoft
DefaultGroupName=BulutSoft
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Güncelleme: çalışan program otomatik kapatılır, kurulum sonrası yeniden başlatılmaz.
CloseApplications=yes
RestartApplications=no
; Görünüm.
SetupIconFile=LabDenetim\app.ico
UninstallDisplayIcon={app}\BulutSoftSecuritySuite.exe
UninstallDisplayName=BulutSoft Security Suite
; Çıktı ve sıkıştırma.
OutputDir=Output
OutputBaseFilename=BulutSoft_Setup_v{#Surum}_{#Profil}
Compression=lzma2/ultra64
SolidCompression=yes
; İLERİDE kod imzalama (Authenticode) sertifikası alındığında yalnızca aşağıdaki iki satırın başındaki ";" işaretini kaldırın.
; "bulutimza" adı derleyiciye şöyle tanıtılır (Yayinla.ps1 içinde ISCC çağrısına ekleyin; parola komut satırına yazılmaz, ortam değişkeninden okunmalıdır):
;   ISCC.exe /Sbulutimza="signtool.exe sign /f $qC:\imza\bulutsoft.pfx$q /p $q%BULUT_PFX_PAROLA%$q /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $f" ...
;SignTool=bulutimza
;SignedUninstaller=yes
[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
[Files]
Source: "yayin\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
[Icons]
Name: "{group}\BulutSoft Security Suite"; Filename: "{app}\BulutSoftSecuritySuite.exe"
Name: "{autodesktop}\BulutSoft Security Suite"; Filename: "{app}\BulutSoftSecuritySuite.exe"; Tasks: masaustu
[Tasks]
Name: "masaustu"; Description: "Masaüstüne kısayol ekle"; Flags: unchecked
Name: "pilot"; Description: "Pilot (salt okunur) mod: program bu bilgisayarda hiçbir değişiklik yapmaz"; Flags: unchecked
[Dirs]
; Güvenlik duvarı yedekleri: yalnızca yönetici yazabilir (yukarıdaki icacls adımı kalıtılan hakları da kaldırır). Kaldırmada SİLİNMEZ.
Name: "{commonappdata}\BulutSoft\fw_yedek"; Permissions: admins-full system-full users-readexec; Flags: uninsneveruninstall
; Lisans/deneme kaydı klasörü: tüm kullanıcılar yazabilir (makine geneli deneme ve lisans dosyası). Kaldırmada SİLİNMEZ.
Name: "{commonappdata}\BulutSoft\Lisans"; Permissions: users-modify; Flags: uninsneveruninstall
; Pilot bayrağı yalnızca yönetici yazabilsin diye klasör kurulumda oluşturulur (Users grubuna yazma hakkı verilmez).
Name: "{commonappdata}\BulutSoft"; Tasks: pilot; Permissions: admins-full system-full users-readexec; Flags: uninsneveruninstall
[Files]
#if Profil == "Cevrimdisi"
; WebView2 çevrimdışı kurucusu (Microsoft imzalı, Evergreen Standalone x64). Yalnızca gerekirse geçici klasöre açılır ve kurulumdan sonra silinir.
Source: "Kurucular\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion; Check: WebView2Gerekli
#endif
; Bayrak dosyası: varlığı pilot modu açar (PilotMod.cs). Kaldırmak için dosyayı silin.
Source: "pilot_bayrak.txt"; DestDir: "{commonappdata}\BulutSoft"; DestName: "salt_okunur.flag"; Tasks: pilot; Flags: ignoreversion uninsneveruninstall
[Run]
; KORUMA HİZMETİ (BulutSoftGuard): SYSTEM olarak, "Otomatik (Gecikmeli Başlatma)". Nöbetçi, fidye izleme, canary, zincirli olay günlüğü ve ikinci mühür kanalı oturumdan bağımsız çalışır.
; Zaten kuruluysa (güncelleme) yeniden oluşturulmaz; yalnızca yapılandırma güncellenir ve başlatılır.
Filename: "{sys}\sc.exe"; Parameters: "create BulutSoftGuard binPath= ""{app}\BulutSoftGuardService.exe"" start= delayed-auto obj= LocalSystem DisplayName= ""BulutSoft Guard"""; Flags: runhidden waituntilterminated; Check: not HizmetKurulu
Filename: "{sys}\sc.exe"; Parameters: "config BulutSoftGuard binPath= ""{app}\BulutSoftGuardService.exe"" start= delayed-auto"; Flags: runhidden waituntilterminated; Check: HizmetKurulu
Filename: "{sys}\sc.exe"; Parameters: "description BulutSoftGuard ""BulutSoft Security Suite koruma hizmeti - nöbetçi, tuzak ve fidye izleme"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure BulutSoftGuard reset= 86400 actions= restart/60000/restart/120000/restart/300000"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start BulutSoftGuard"; Flags: runhidden waituntilterminated
; Güvenlik duvarı yedek klasörü yalnızca yönetici yazabilsin (yedekler yönetici izniyle geri yüklenir; kullanıcı yazabilirse politika değiştirilebilirdi).
Filename: "{sys}\icacls.exe"; Parameters: """{commonappdata}\BulutSoft\fw_yedek"" /inheritance:r /grant:r *S-1-5-32-544:(OI)(CI)F *S-1-5-18:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX"; Flags: runhidden waituntilterminated
; Windows Olay Günlüğü'nde "BulutSoft" kaynağını kaydet: program olay kayıtlarını buraya da yansıtır (yönetici olmayan kullanıcı oradan silemez). Hata olursa kurulum sürer.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""if (-not [System.Diagnostics.EventLog]::SourceExists('BulutSoft')) {{ New-EventLog -LogName Application -Source BulutSoft }"""; Flags: runhidden waituntilterminated
#if Profil == "Cevrimdisi"
; WebView2 çalışma zamanı (Kolay Görünüm'deki canlı küre için): yalnızca makinede yoksa, sessizce kurulur. Hata olursa kurulum sürer; küre paneli boş kalır, program çalışır.
Filename: "{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Parameters: "/silent /install"; StatusMsg: "WebView2 çalışma zamanı kuruluyor..."; Flags: waituntilterminated runhidden; Check: WebView2Gerekli
#endif
; Sessiz kurulumda ("/VERYSILENT") bu satır çalışmaz ve hiçbir diyalog çıkmaz.
Filename: "{app}\BulutSoftSecuritySuite.exe"; Description: "Programı başlat"; Flags: nowait postinstall skipifsilent
; KULLANICI VERİSİ KORUMASI: bilerek [UninstallDelete] ve [InstallDelete] BÖLÜMÜ YOKTUR (tek istisna: aşağıdaki [Code] bölümü yalnızca WebView2 önbelleğini siler).
; %AppData%\BulutSoft (lisans, geçmiş, kimlik, güvenlik duvarı ve kalıcılık yedekleri) kaldırma sırasında KESİNLİKLE silinmez.
[UninstallRun]
; Kaldırma: hizmet durdurulur ve silinir. Guard klasörü (olay günlüğü) ve HKLM mühür kayıtları KALDIRMADA SİLİNMEZ (kanıt korunur).
Filename: "{sys}\sc.exe"; Parameters: "stop BulutSoftGuard"; Flags: runhidden waituntilterminated; RunOnceId: "GuardDurdur"
Filename: "{sys}\sc.exe"; Parameters: "delete BulutSoftGuard"; Flags: runhidden waituntilterminated; RunOnceId: "GuardSil"
[Code]
// Koruma hizmeti kurulu mu?
function HizmetKurulu: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\BulutSoftGuard');
end;

// WebView2 çalışma zamanı kurulu mu? (makine geneli 32-bit/64-bit kayıt ya da kullanıcı kaydı)
function WebView2Gerekli: Boolean;
var Surum: String;
begin
  Result := True;
  if RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Surum) and (Surum <> '') and (Surum <> '0.0.0.0') then Result := False
  else if RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Surum) and (Surum <> '') and (Surum <> '0.0.0.0') then Result := False;
end;

#if Profil == "Hafif"
// HAFİF PROFİL: WebView2 yoksa Microsoft'un küçük "Evergreen Bootstrapper" kurucusu indirilir. Çalıştırılmadan ÖNCE Authenticode imzası ve yayıncı (Microsoft Corporation) doğrulanır;
// doğrulanamayan dosya ASLA çalıştırılmaz. Her hata sessizce geçilir (kurulum bozulmaz; yalnızca canlı küre paneli boş kalır).
const WebView2IndirmeAdresi = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';

function MicrosoftImzaliMi(Dosya: String): Boolean;
var Kod: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -Command "$s = Get-AuthenticodeSignature -LiteralPath ''' + Dosya + '''; if ($s.Status -eq ''Valid'' -and $s.SignerCertificate.Subject -like ''*O=Microsoft Corporation*'') { exit 0 } else { exit 1 }"',
    '', SW_HIDE, ewWaitUntilTerminated, Kod) and (Kod = 0);
end;

procedure WebView2IndirVeKur;
var Dosya: String; Kod: Integer;
begin
  if not WebView2Gerekli then Exit;
  try
    DownloadTemporaryFile(WebView2IndirmeAdresi, 'WebView2Kurucu.exe', '', nil);
    Dosya := ExpandConstant('{tmp}\WebView2Kurucu.exe');
    if MicrosoftImzaliMi(Dosya) then Exec(Dosya, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Kod)
    else Log('WebView2 kurucusunun Microsoft imzası doğrulanamadı; çalıştırılmadı.');
  except
    Log('WebView2 indirilemedi (internet yok olabilir): ' + GetExceptionMessage);
  end;
end;
#endif

// Güncelleme: dosyalar değiştirilmeden önce çalışan hizmet durdurulur (aksi hâlde exe kilitli kalır).
procedure CurStepChanged(CurStep: TSetupStep);
var kod: Integer;
begin
  if (CurStep = ssInstall) and HizmetKurulu then
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop BulutSoftGuard', '', SW_HIDE, ewWaitUntilTerminated, kod);
#if Profil == "Hafif"
  if CurStep = ssPostInstall then WebView2IndirVeKur;
#endif
end;

// KALDIRMADA YALNIZCA WebView2 ÖNBELLEĞİ silinir (kullanıcı verisi DEĞİLDİR: Kolay Görünüm küresinin tarayıcı önbelleği/çerezleri). Diğer her şey (kimlik, lisans, geçmiş, olay günlüğü, yedekler) korunur.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    DelTree(ExpandConstant('{localappdata}\BulutSoft\WebView2'), True, True, True);
end;
