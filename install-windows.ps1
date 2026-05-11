#Requires -RunAsAdministrator
<#
.SYNOPSIS
    RedZone - Windows Server Kurulum Scripti
.DESCRIPTION
    RedZone'u Windows Service olarak kurar, veritabanını oluşturur,
    güvenlik duvarı kuralı ekler ve servisi başlatır.
.PARAMETER Port
    Uygulamanın dinleyeceği port (varsayılan: 5000)
.PARAMETER JwtKey
    JWT imzalama anahtarı. Belirtilmezse rastgele üretilir.
.EXAMPLE
    .\install-windows.ps1
    .\install-windows.ps1 -Port 8080
#>

param(
    [int]$Port = 5000,
    [string]$JwtKey = ""
)

$ErrorActionPreference = "Stop"
$ServiceName  = "RedZone"
$DisplayName  = "RedZone Risk Yönetim Sistemi"
$InstallDir   = "C:\Program Files\RedZone"
$DataDir      = "C:\ProgramData\RedZone"
$LogDir       = "C:\ProgramData\RedZone\logs"
$DbPath       = "$DataDir\redzone.db"
$AppSettings  = "$InstallDir\appsettings.Production.json"

function Write-Step($msg) { Write-Host "`n[+] $msg" -ForegroundColor Cyan }
function Write-OK($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    !! $msg" -ForegroundColor Yellow }

# ── .NET Runtime kontrolü ──────────────────────────────────────────────────
Write-Step ".NET 8 Runtime kontrol ediliyor..."
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "`n[HATA] .NET 8 Runtime bulunamadi." -ForegroundColor Red
    Write-Host "Lutfen https://dotnet.microsoft.com/download/dotnet/8.0 adresinden indirin." -ForegroundColor Yellow
    Write-Host "Indirme baglantisi aciliyor..." -ForegroundColor Yellow
    Start-Process "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-8.0.0-windows-hosting-bundle-installer"
    exit 1
}
$version = & dotnet --version
Write-OK ".NET $version bulundu"

# ── Mevcut servis durdur ───────────────────────────────────────────────────
Write-Step "Mevcut kurulum kontrol ediliyor..."
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warn "Mevcut RedZone servisi durduruluyor..."
    Stop-Service -Name $ServiceName -Force
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
    Write-OK "Eski servis kaldırıldı"
}

# ── Klasörler ──────────────────────────────────────────────────────────────
Write-Step "Klasörler oluşturuluyor..."
@($InstallDir, $DataDir, $LogDir) | ForEach-Object {
    New-Item -ItemType Directory -Force -Path $_ | Out-Null
}
Write-OK "Klasörler hazır: $InstallDir, $DataDir"

# ── Dosyaları kopyala ─────────────────────────────────────────────────────
Write-Step "Uygulama dosyaları kopyalanıyor..."
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir = Join-Path $ScriptDir "publish"

if (-not (Test-Path $PublishDir)) {
    Write-Step "Uygulama derleniyor (bu 1-2 dakika sürebilir)..."
    $ProjectFile = Join-Path $ScriptDir "RiskManagement\RiskManagement.csproj"
    & dotnet publish $ProjectFile -c Release -o $PublishDir --self-contained false -r win-x64
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[HATA] Derleme basarisiz oldu." -ForegroundColor Red; exit 1
    }
}

Copy-Item -Path "$PublishDir\*" -Destination $InstallDir -Recurse -Force
Write-OK "Dosyalar $InstallDir klasörüne kopyalandı"

# ── JWT anahtarı ──────────────────────────────────────────────────────────
if (-not $JwtKey) {
    $bytes  = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $JwtKey = [Convert]::ToBase64String($bytes)
    Write-Warn "JWT anahtarı otomatik üretildi (kaydedin!): $JwtKey"
}

# ── appsettings.Production.json yaz ──────────────────────────────────────
Write-Step "Yapılandırma dosyası yazılıyor..."
$config = @{
    Jwt            = @{ Key = $JwtKey }
    AppSettings    = @{
        DemoMode       = $false
        UseSqlite      = $true
        SqlitePath     = $DbPath
        MaxUploadSizeMb = 25
    }
    Urls           = "http://0.0.0.0:$Port"
    Logging        = @{
        LogLevel = @{
            Default                   = "Warning"
            "Microsoft.AspNetCore"    = "Warning"
        }
    }
} | ConvertTo-Json -Depth 5

# Üretim config'i kurulum dizinine yaz (publish'takini geçersiz kılar)
Set-Content -Path $AppSettings -Value $config -Encoding UTF8
Write-OK "Yapılandırma: $AppSettings"

# ── Windows Service kur ────────────────────────────────────────────────────
Write-Step "Windows Service kuruluyor..."
$ExePath  = Join-Path $InstallDir "RiskManagement.exe"
$BinPath  = "`"$ExePath`" --environment Production"

& sc.exe create $ServiceName binPath= $BinPath start= auto DisplayName= $DisplayName | Out-Null
& sc.exe description $ServiceName "RedZone - KOBİ Risk Yönetimi ve İç Denetim Platformu" | Out-Null
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
Write-OK "Servis oluşturuldu: $ServiceName"

# ── Güvenlik duvarı kuralı ────────────────────────────────────────────────
Write-Step "Güvenlik duvarı kuralı ekleniyor..."
$ruleName = "RedZone-Port-$Port"
Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction   Inbound `
    -Protocol    TCP `
    -LocalPort   $Port `
    -Action      Allow `
    -Profile     Domain,Private | Out-Null
Write-OK "Güvenlik duvarı: TCP $Port açıldı (Domain ve Private profilleri)"

# ── Servisi başlat ────────────────────────────────────────────────────────
Write-Step "RedZone başlatılıyor..."
Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$svc = Get-Service -Name $ServiceName
if ($svc.Status -eq "Running") {
    Write-OK "Servis çalışıyor"
} else {
    Write-Host "[HATA] Servis başlatılamadı. Günlükler: $LogDir" -ForegroundColor Red
    exit 1
}

# ── Sağlık kontrolü ───────────────────────────────────────────────────────
Write-Step "Sağlık kontrolü yapılıyor..."
Start-Sleep -Seconds 3
try {
    $response = Invoke-WebRequest -Uri "http://localhost:$Port/healthz" -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-OK "Uygulama yanıt veriyor: /healthz → $($response.StatusCode)"
    }
} catch {
    Write-Warn "Sağlık kontrolü başarısız. Uygulama henüz başlamıyor olabilir."
    Write-Warn "Günlük dosyasını kontrol edin: $LogDir"
}

# ── Özet ──────────────────────────────────────────────────────────────────
$hostname = $env:COMPUTERNAME
Write-Host "`n" + ("="*60) -ForegroundColor Green
Write-Host "  RedZone kurulumu tamamlandı!" -ForegroundColor Green
Write-Host ("="*60) -ForegroundColor Green
Write-Host "  Adres  : http://$hostname`:$Port" -ForegroundColor White
Write-Host "  Servis : $ServiceName (otomatik başlar)" -ForegroundColor White
Write-Host "  Veri   : $DataDir" -ForegroundColor White
Write-Host "  Config : $AppSettings" -ForegroundColor White
Write-Host ""
Write-Host "  Varsayilan giris: admin / Admin123!" -ForegroundColor Yellow
Write-Host "  Ilk giriste sifrenizi degistirin." -ForegroundColor Yellow
Write-Host ""
Write-Host "  Servis yonetimi:" -ForegroundColor Gray
Write-Host "    Durdur : Stop-Service $ServiceName" -ForegroundColor Gray
Write-Host "    Baslat : Start-Service $ServiceName" -ForegroundColor Gray
Write-Host "    Kaldir : .\uninstall-windows.ps1" -ForegroundColor Gray
Write-Host ("="*60) -ForegroundColor Green
