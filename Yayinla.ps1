# BulutSoft Security Suite yayınlama hattı (kurumsal B2B).
# Sıra: (1) TÜM otomatik testler - tek test bile kalırsa dur, (2) win-x64 self-contained Release derleme,
#       (3) isteğe bağlı exe imzalama, (4) Inno Setup ile İKİ profil: BulutSoft_Setup_v{Surum}_Hafif.exe ve BulutSoft_Setup_v{Surum}_Cevrimdisi.exe, (5) SHA256SUMS.txt (iki satır).
# Kullanım:  .\Yayinla.ps1            (sürüm kökteki Directory.Build.props içindeki <Version> değerinden okunur)
#            .\Yayinla.ps1 -Surum 1.2.0
#            .\Yayinla.ps1 -Zip        (ayrıca taşınabilir .zip üretir)
#            .\Yayinla.ps1 -Profil Hafif   (yalnızca hafif paket; varsayılan: Hepsi = Hafif + Cevrimdisi)
param(
    [string]$Surum = '',
    [string]$Imzala = '',
    [switch]$Zip,
    [switch]$TestleriAtla,
    [ValidateSet('Hepsi', 'Hafif', 'Cevrimdisi')][string]$Profil = 'Hepsi'
)
$profiller = if ($Profil -eq 'Hepsi') { @('Hafif', 'Cevrimdisi') } else { @($Profil) }
# -Imzala <sertifika.pfx>: derlemeden sonra .exe ve .dll dosyalarını imzalar (Windows SDK signtool gerekir).
# Parola komut satırına yazılmaz: ortam değişkeni BULUT_PFX_PAROLA'dan okunur. Verilmezse imzalama atlanır.
# Hata olursa dur.
$ErrorActionPreference = 'Stop'
$kok = $PSScriptRoot
$proje = Join-Path $kok 'LabDenetim\LabDenetim.csproj'

# Sürüm: verilmediyse kökteki Directory.Build.props'tan (tek kaynak; tüm projeler ve arayüz etiketleri buradan okur) alınır.
$surumKaynagi = Join-Path $kok 'Directory.Build.props'
if ($Surum -eq '') {
    $m = Select-String -Path $surumKaynagi -Pattern '<Version>\s*([0-9]+(\.[0-9]+){1,3})\s*</Version>' | Select-Object -First 1
    if (-not $m) { throw "Sürüm bulunamadı: $surumKaynagi içinde <Version> yok. -Surum parametresi verin." }
    $Surum = $m.Matches[0].Groups[1].Value
}
if ($Surum -notmatch '^[0-9]+(\.[0-9]+){1,3}$') { throw "Geçersiz sürüm: $Surum" }
Write-Host "Sürüm: $Surum"

# 0b) Bağımlılık zafiyet taraması (ASVS V15): bilinen zafiyetli NuGet paketi (doğrudan ya da geçişli) varsa yayın DURUR. Tarama nuget.org'a ihtiyaç duyar; erişilemezse uyarı verilir, yayın sürer.
Write-Host "Bağımlılık zafiyet taraması..."
$zafiyetJson = dotnet list (Join-Path $kok 'BulutSoft.sln') package --vulnerable --include-transitive --format json 2>$null | Out-String
try { $zafiyet = $zafiyetJson | ConvertFrom-Json } catch { $zafiyet = $null }
if ($null -eq $zafiyet) { Write-Warning "Zafiyet taraması sonucu okunamadı (nuget.org erişimi?); yayın sürüyor." }
else {
    $bulunan = @($zafiyet.projects | ForEach-Object { $_.frameworks } | ForEach-Object { $_.topLevelPackages + $_.transitivePackages } | Where-Object { $_ -and $_.vulnerabilities })
    if ($bulunan.Count -gt 0) { $bulunan | ForEach-Object { Write-Host (" - {0} {1}: {2}" -f $_.id, $_.resolvedVersion, (($_.vulnerabilities | ForEach-Object { $_.severity + ' ' + $_.advisoryurl }) -join '; ')) }; throw "Bilinen zafiyetli paket var ($($bulunan.Count)); yayın durduruldu. Paketi güncelleyin." }
    Write-Host "Zafiyetli paket yok."
}

# 1) Testler ÖNCE. Tek bir test bile başarısızsa hiçbir şey derlenmez, eski çıktıya dokunulmaz.
Write-Host "Testler çalışıyor..."
if ($TestleriAtla) { Write-Warning "Testler atlandi (-TestleriAtla): CI ayri calistirir." } else { dotnet test (Join-Path $kok "LabDenetim.Tests") --filter "Category!=Integration" -nologo -v q }
if (-not $TestleriAtla -and $LASTEXITCODE -ne 0) { throw "Testler BAŞARISIZ (çıkış kodu $LASTEXITCODE). Yayın durduruldu; hiçbir dosya üretilmedi." }

# 2) win-x64, self-contained, Release.
$cikti = Join-Path $kok 'yayin'
if (Test-Path $cikti) { Remove-Item $cikti -Recurse -Force }
dotnet publish $proje -c Release -r win-x64 --self-contained true -o $cikti -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız (çıkış kodu $LASTEXITCODE)." }
# 2b) Koruma hizmeti (BulutSoftGuard) AYNI klasöre yayınlanır: hizmet exe'si arayüz exe'sinin yanında durur (pipe yetkisi bu yola bağlıdır).
$hizmetProje = Join-Path $kok 'BulutSoftGuardService\BulutSoftGuardService.csproj'
dotnet publish $hizmetProje -c Release -r win-x64 --self-contained true -o $cikti -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Koruma hizmeti yayını başarısız (çıkış kodu $LASTEXITCODE)." }
if (-not (Test-Path (Join-Path $cikti 'BulutSoftGuardService.exe'))) { throw "BulutSoftGuardService.exe yayın klasöründe yok." }

# 3) İstenirse exe/dll imzala (sertifika yoksa hiçbir şey yapılmaz).
if ($Imzala -ne '') {
    if (-not (Test-Path $Imzala)) { throw "Sertifika bulunamadı: $Imzala" }
    if (-not $env:BULUT_PFX_PAROLA) { throw "BULUT_PFX_PAROLA ortam değişkeni ayarlı değil (sertifika parolası)." }
    $st = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
    if (-not $st) { throw "signtool.exe bulunamadı (Windows SDK kurulu olmalı)." }
    $hedefler = Get-ChildItem $cikti -Include 'BulutSoftSecuritySuite.exe','BulutSoftSecuritySuite.dll','BulutSoftGuardService.exe','BulutSoftGuardService.dll' -Recurse
    foreach ($h in $hedefler) { & $st sign /f $Imzala /p $env:BULUT_PFX_PAROLA /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $h.FullName; if ($LASTEXITCODE -ne 0) { throw "İmzalama başarısız: $($h.FullName)" } }
    Write-Host "İmzalandı: $($hedefler.Count) dosya"
}

# 3b) WebView2 çevrimdışı kurucusu YALNIZCA Cevrimdisi profilde pakete gömülür (Hafif profil kurulumda indirir ve Microsoft imzasını doğrular).
if ($profiller -contains 'Cevrimdisi') {
    $wv2 = Join-Path $PSScriptRoot 'Kurucular\MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
    if (-not (Test-Path $wv2)) { throw "WebView2 kurucusu yok: $wv2 (Microsoft 'Evergreen Standalone Installer x64' dosyasını bu yola koyun)." }
    if ((Get-AuthenticodeSignature $wv2).Status -ne 'Valid') { throw "WebView2 kurucusunun Microsoft imzası geçerli değil: $wv2" }
}

# 4) Inno Setup 6 (ISCC.exe): PATH'te ya da bilinen kurulum yollarında aranır.
$iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $aday = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
    $iscc = $aday | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { throw "ISCC.exe (Inno Setup 6) bulunamadı. https://jrsoftware.org/isdl.php adresinden kurun." }
$cikis = Join-Path $kok 'Output'
New-Item -ItemType Directory -Force $cikis | Out-Null
$sumSatirlari = @(); $kurulumlar = @()
foreach ($p in $profiller) {
    $kurulum = Join-Path $cikis "BulutSoft_Setup_v${Surum}_$p.exe"
    if (Test-Path $kurulum) { Remove-Item $kurulum -Force }
    Push-Location $kok
    try { & $iscc "/DSurum=$Surum" "/DProfil=$p" (Join-Path $kok 'BulutSoft.iss') } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw "ISCC derlemesi başarısız ($p, çıkış kodu $LASTEXITCODE)." }
    if (-not (Test-Path $kurulum)) { throw "Beklenen kurulum dosyası üretilmedi: $kurulum" }
    # 5) Bütünlük özeti: sha256sum uyumlu biçim ("<özet>  <dosya adı>"), kurulum dosyalarının hemen yanında.
    $ozet = (Get-FileHash -Algorithm SHA256 -Path $kurulum).Hash.ToLowerInvariant()
    $sumSatirlari += "$ozet  $(Split-Path $kurulum -Leaf)"; $kurulumlar += $kurulum
    Write-Host ("Hazır [{0}]: {1}  ({2:N1} MB)" -f $p, $kurulum, ((Get-Item $kurulum).Length / 1MB))
}
$sumDosya = Join-Path $cikis 'SHA256SUMS.txt'
[IO.File]::WriteAllText($sumDosya, (($sumSatirlari -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))

# İsteğe bağlı taşınabilir zip.
if ($Zip) {
    $zip = Join-Path $kok "BulutSoft_SecuritySuite_v$Surum.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $cikti '*') -DestinationPath $zip
    Write-Host "Zip: $zip"
}

# Bilgi.
Write-Host "Özetler: $sumDosya"
Write-Host "Not: Programın ayarları kullanıcının %AppData%\BulutSoft klasöründe tutulur; kurulum ve kaldırma ona dokunmaz."
