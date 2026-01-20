# Опростен скрипт за публикуване - само спиране на процеси и публикуване
# Използване: .\Publish-API-Simple.ps1

param(
    [string]$PublishPath = "X:\WEB\ADS-Auth"
)

Write-Host "=== Публикуване на API ===" -ForegroundColor Cyan
Write-Host ""

# Спиране на всички dotnet процеси които използват API DLL
Write-Host "Спиране на работещи процеси..." -ForegroundColor Yellow
$processes = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    try {
        $_.Modules | Where-Object { $_.FileName -like "*ADS.WindowsAuth.API*" }
    } catch {
        $false
    }
}

if ($processes) {
    foreach ($proc in $processes) {
        Write-Host "  Спиране на процес (PID: $($proc.Id))..." -ForegroundColor Gray
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 3
    Write-Host "✓ Процеси спрени" -ForegroundColor Green
} else {
    Write-Host "✓ Няма работещи процеси" -ForegroundColor Green
}

# Публикуване
Write-Host "Публикуване..." -ForegroundColor Yellow
$projectPath = Join-Path $PSScriptRoot "ADS.WindowsAuth.API.csproj"

dotnet publish $projectPath -c Release -p:PublishUrl=$PublishPath

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Готово!" -ForegroundColor Green
} else {
    Write-Host "✗ Грешка!" -ForegroundColor Red
    exit $LASTEXITCODE
}

