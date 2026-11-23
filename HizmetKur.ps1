# BulutSoft Koruma Hizmeti (BulutSoftGuard) KURULUM betiği. Yönetici PowerShell gerekir.
# Normalde kurulum paketi (BulutSoft_Setup_vX.exe) hizmeti kendisi kurar; bu betik elle kurulum / geliştirme içindir.
# Kullanım:   .\HizmetKur.ps1 -Exe "C:\Program Files\BulutSoft\BulutSoftGuardService.exe"
param([string]$Exe = "$env:ProgramFiles\BulutSoft\BulutSoftGuardService.exe")
$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Yönetici olarak çalıştırın." }
if (-not (Test-Path $Exe)) { throw "Hizmet exe'si bulunamadı: $Exe" }
# NOT: pipe yetkisi, hizmet exe'sinin YANINDAKİ BulutSoftSecuritySuite.exe'ye bağlıdır; ikisi aynı klasörde olmalıdır.
if (-not (Test-Path (Join-Path (Split-Path $Exe) 'BulutSoftSecuritySuite.exe'))) { Write-Warning "Hizmet exe'sinin yanında BulutSoftSecuritySuite.exe yok: arayüz hizmete yazma komutu gönderemez." }
$var = Get-Service BulutSoftGuard -ErrorAction SilentlyContinue
if (-not $var) { sc.exe create BulutSoftGuard binPath= "`"$Exe`"" start= delayed-auto obj= LocalSystem DisplayName= "BulutSoft Guard" | Out-Null }
else { sc.exe stop BulutSoftGuard | Out-Null; Start-Sleep 2; sc.exe config BulutSoftGuard binPath= "`"$Exe`"" start= delayed-auto | Out-Null }
sc.exe description BulutSoftGuard "BulutSoft Security Suite koruma hizmeti - nöbetçi, tuzak ve fidye izleme" | Out-Null
sc.exe failure BulutSoftGuard reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null
sc.exe start BulutSoftGuard
Get-Service BulutSoftGuard | Format-Table Name, Status, StartType -AutoSize
