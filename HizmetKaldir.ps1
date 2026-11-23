# BulutSoft Koruma Hizmeti KALDIRMA betiği. Yönetici PowerShell gerekir.
# Hizmet durdurulur ve silinir. KORUNUR (silinmez): C:\ProgramData\BulutSoft\Guard (zincirli olay günlüğü) ve HKLM\SOFTWARE\BulutSoft\Muhur (mühür kayıtları).
# Mühür kaydını da temizlemek isterseniz (ör. kimlik dosyalarını bilinçli sıfırlıyorsanız) betik sonundaki satırın yorumunu kaldırın.
$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "Yönetici olarak çalıştırın." }
if (Get-Service BulutSoftGuard -ErrorAction SilentlyContinue) { sc.exe stop BulutSoftGuard | Out-Null; Start-Sleep 2; sc.exe delete BulutSoftGuard }
else { Write-Host "Hizmet kurulu değil." }
# reg delete HKLM\SOFTWARE\BulutSoft\Muhur /f
