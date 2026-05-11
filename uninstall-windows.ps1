#Requires -RunAsAdministrator
param([int]$Port = 5000)

$ServiceName = "RedZone"
$InstallDir  = "C:\Program Files\RedZone"
$RuleName    = "RedZone-Port-$Port"

Write-Host "`nRedZone kaldırılıyor..." -ForegroundColor Yellow

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    Stop-Service  -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Write-Host "  Servis kaldırıldı." -ForegroundColor Green
}

Remove-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue
Write-Host "  Güvenlik duvarı kuralı kaldırıldı." -ForegroundColor Green

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
    Write-Host "  Uygulama dosyaları silindi." -ForegroundColor Green
}

Write-Host "`nNOT: Veritabanı ve yapılandırma korundu: C:\ProgramData\RedZone" -ForegroundColor Cyan
Write-Host "Tüm verileri silmek için: Remove-Item 'C:\ProgramData\RedZone' -Recurse" -ForegroundColor Gray
