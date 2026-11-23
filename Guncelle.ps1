# BulutSoft güncelleme betiği (çevrimdışı; programın kendisi internete çıkmaz). GPO başlangıç betiği, Intune veya elle çalıştırılır.
# Yönetici PowerShell gerekir. Windows PowerShell 5.1 ile uyumludur.
#
# Ne yapar: paylaşım klasöründeki en yeni BulutSoft_Setup_v*.exe dosyasını bulur, SHA256SUMS.txt ile doğrular,
# kurulu sürümden yeniyse sessizce kurar (eski sürümün üzerine; kullanıcı verisi %AppData%\BulutSoft'a dokunulmaz).
# Güvenlik: dosya önce YEREL geçici klasöre kopyalanır, özet kopya üzerinde doğrulanır (paylaşımda doğrulama ile çalıştırma arasında değiştirilemez).
# Özet dosyası yoksa ya da dosya adı onda yoksa HİÇBİR ŞEY kurulmaz. Authenticode imzası varsa durumu günlüğe yazılır; -ImzaZorunlu ile geçerli imza şart koşulur.
#
# Kullanım:
#   .\Guncelle.ps1 -Kaynak \\sunucu\yazilim\BulutSoft              # kuruluysa ve daha yeni sürüm varsa günceller
#   .\Guncelle.ps1 -Kaynak \\sunucu\yazilim\BulutSoft -DenemeModu   # hiçbir şey kurmaz, ne yapacağını söyler
#   .\Guncelle.ps1 -Kaynak ... -YoksaKur                           # kurulu değilse ilk kurulumu da yapar
#   .\Guncelle.ps1 -Kaynak ... -ImzaZorunlu                        # (sertifika alındığında) geçerli imza olmadan kurmaz
# Çıkış kodu: 0 = güncel/başarılı, 1 = hata (günlüğe bakın), 2 = kurulu değil ve -YoksaKur verilmedi.
param(
    [Parameter(Mandatory = $true)][string]$Kaynak,
    [switch]$DenemeModu,
    [switch]$YoksaKur,
    [switch]$ImzaZorunlu,
    [ValidateSet('Hafif', 'Cevrimdisi')][string]$Profil = 'Hafif',
    [string]$Gunluk = (Join-Path $env:ProgramData 'BulutSoft\guncelle.log')
)
$ErrorActionPreference = 'Stop'
$appId = '{17989392-9F62-41F9-B534-169A6CDF007C}_is1'

function Gunlukle([string]$m) {
    $satir = '{0} {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $m
    Write-Host $satir
    try { $d = Split-Path $Gunluk; if ($d -and -not (Test-Path $d)) { New-Item -ItemType Directory -Force $d | Out-Null }; Add-Content -Path $Gunluk -Value $satir -Encoding UTF8 } catch { }
}

try {
    $yonetici = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $yonetici -and -not $DenemeModu) { Gunlukle 'HATA: yönetici PowerShell gerekir.'; exit 1 }
    if (-not (Test-Path $Kaynak)) { Gunlukle "HATA: kaynak klasör bulunamadı: $Kaynak"; exit 1 }

    # En yeni kurulum dosyası (sürüm sayısal karşılaştırılır: 1.10.0 > 1.9.0).
    # Dosya adı: BulutSoft_Setup_v<sürüm>_<Hafif|Cevrimdisi>.exe (eski adlandırma _profilsiz de kabul edilir). İstenen profil tercih edilir; yoksa herhangi biri.
    $adaylar = @(Get-ChildItem $Kaynak -Filter 'BulutSoft_Setup_v*.exe' -File | ForEach-Object {
        if ($_.Name -match '^BulutSoft_Setup_v(\d+(\.\d+){1,3})(_(Hafif|Cevrimdisi))?\.exe$') { [pscustomobject]@{ Dosya = $_; Surum = [version]$Matches[1]; Profil = $Matches[4] } } })
    if ($adaylar.Count -eq 0) { Gunlukle "HATA: $Kaynak içinde BulutSoft_Setup_v<sürüm>.exe yok."; exit 1 }
    $enYeni = ($adaylar | Sort-Object Surum -Descending | Select-Object -First 1).Surum
    $yeni = @($adaylar | Where-Object { $_.Surum -eq $enYeni } | Sort-Object { $_.Profil -eq $Profil } -Descending)[0]

    # Kurulu sürüm (yoksa $null).
    $kayit = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$appId" -ErrorAction SilentlyContinue
    $kurulu = $null; if ($kayit -and $kayit.DisplayVersion) { try { $kurulu = [version]$kayit.DisplayVersion } catch { } }
    if ($kurulu) { Gunlukle "Kurulu sürüm: $kurulu | Kaynaktaki en yeni: $($yeni.Surum)" } else { Gunlukle "Kurulu değil | Kaynaktaki en yeni: $($yeni.Surum)" }
    if ($kurulu -and $kurulu -ge $yeni.Surum) { Gunlukle 'Güncel; yapılacak bir şey yok.'; exit 0 }
    if (-not $kurulu -and -not $YoksaKur) { Gunlukle 'Program kurulu değil ve -YoksaKur verilmedi; kurulum yapılmadı.'; exit 2 }

    # Özet dosyası ZORUNLU ve bu dosya adı için satır olmalı (yoksa kurulmaz).
    $sum = Join-Path $Kaynak 'SHA256SUMS.txt'
    if (-not (Test-Path $sum)) { Gunlukle 'HATA: SHA256SUMS.txt yok; doğrulanamayan dosya kurulmaz.'; exit 1 }
    $beklenen = $null
    foreach ($s in (Get-Content $sum)) { if ($s -match '^([0-9a-fA-F]{64})\s+\*?(.+)$' -and $Matches[2].Trim() -eq $yeni.Dosya.Name) { $beklenen = $Matches[1].ToLower() } }
    if (-not $beklenen) { Gunlukle "HATA: SHA256SUMS.txt içinde $($yeni.Dosya.Name) satırı yok."; exit 1 }

    # Yerel kopya, özet kopya üzerinden doğrulanır.
    $gecici = Join-Path ([IO.Path]::GetTempPath()) ('bulutsoft_guncelle_' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $gecici | Out-Null
    try {
        $yerel = Join-Path $gecici $yeni.Dosya.Name
        Copy-Item $yeni.Dosya.FullName $yerel
        $gercek = (Get-FileHash $yerel -Algorithm SHA256).Hash.ToLower()
        if ($gercek -ne $beklenen) { Gunlukle "HATA: SHA256 uyuşmuyor (beklenen $beklenen, bulunan $gercek). Kurulum İPTAL."; exit 1 }
        Gunlukle 'SHA256 doğrulandı.'

        # İmza durumu (sertifika alınana kadar çoğunlukla NotSigned; -ImzaZorunlu ile şart koşulur).
        $imza = (Get-AuthenticodeSignature $yerel).Status
        Gunlukle "Authenticode durumu: $imza"
        if ($ImzaZorunlu -and $imza -ne 'Valid') { Gunlukle 'HATA: -ImzaZorunlu verildi ama geçerli imza yok. Kurulum İPTAL.'; exit 1 }

        if ($DenemeModu) { Gunlukle "DENEME MODU: $($yeni.Dosya.Name) kurulacaktı; hiçbir şey yapılmadı."; exit 0 }
        $p = Start-Process $yerel -ArgumentList '/VERYSILENT', '/NORESTART' -Wait -PassThru
        if ($p.ExitCode -ne 0) { Gunlukle "HATA: kurulum çıkış kodu $($p.ExitCode)."; exit 1 }
        $son = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$appId" -ErrorAction SilentlyContinue).DisplayVersion
        Gunlukle "Güncellendi. Kurulu sürüm şimdi: $son"
        exit 0
    }
    finally { Remove-Item $gecici -Recurse -Force -ErrorAction SilentlyContinue }
}
catch { Gunlukle ("HATA: " + $_.Exception.Message); exit 1 }
