# PowerShell скрипт за публикуване на API с автоматично спиране и рестартиране
# Използване: .\Publish-API.ps1

param(
    [string]$PublishPath = "X:\WEB\ADS-Auth",
    [string]$AppPoolName = "ADS-Auth",  # Име на IIS Application Pool (ако използваш IIS)
    [switch]$SkipAppPoolRestart = $false
)

Write-Host "=== ADS Windows Auth API - Публикуване ===" -ForegroundColor Cyan
Write-Host ""

# Проверка дали пътят съществува
if (-not (Test-Path $PublishPath)) {
    Write-Host "Създаване на директория: $PublishPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $PublishPath -Force | Out-Null
}

# Стъпка 1: Спиране на Application Pool (ако използваш IIS)
if (-not $SkipAppPoolRestart) {
    Write-Host "Стъпка 1: Спиране на Application Pool..." -ForegroundColor Yellow
    try {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        $appPool = Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue
        if ($appPool) {
            if ($appPool.Value -eq "Started") {
                Stop-WebAppPool -Name $AppPoolName
                Write-Host "✓ Application Pool спрян" -ForegroundColor Green
                Start-Sleep -Seconds 2
            } else {
                Write-Host "Application Pool вече е спрян" -ForegroundColor Gray
            }
        } else {
            Write-Host "Application Pool не е намерен (може да не използваш IIS)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "⚠ Грешка при спиране на Application Pool: $_" -ForegroundColor Yellow
        Write-Host "  Продължавам с публикуването..." -ForegroundColor Gray
    }
}

# Стъпка 2: Проверка за работещи dotnet процеси
Write-Host "Стъпка 2: Проверка за работещи dotnet процеси..." -ForegroundColor Yellow
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -like "*$PublishPath*" -or 
    $_.CommandLine -like "*ADS.WindowsAuth.API*"
}

if ($dotnetProcesses) {
    Write-Host "Намерени работещи dotnet процеси. Спиране..." -ForegroundColor Yellow
    foreach ($proc in $dotnetProcesses) {
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Процес спрян (PID: $($proc.Id))" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠ Неуспешно спиране на процес (PID: $($proc.Id))" -ForegroundColor Yellow
        }
    }
    Start-Sleep -Seconds 3
} else {
    Write-Host "✓ Няма работещи dotnet процеси" -ForegroundColor Green
}

# Стъпка 3: Създаване на app_offline.htm (за ASP.NET Core)
Write-Host "Стъпка 3: Създаване на app_offline.htm..." -ForegroundColor Yellow
$appOfflinePath = Join-Path $PublishPath "app_offline.htm"
$appOfflineContent = @"
<!DOCTYPE html>
<html>
<head>
    <title>Приложението се обновява</title>
    <meta http-equiv="refresh" content="10">
</head>
<body>
    <h1>Приложението се обновява...</h2>
    <p>Моля, изчакайте няколко секунди.</p>
</body>
</html>
"@
Set-Content -Path $appOfflinePath -Value $appOfflineContent -Force
Write-Host "✓ app_offline.htm създаден" -ForegroundColor Green
Start-Sleep -Seconds 2

# Стъпка 4: Публикуване на проекта
Write-Host "Стъпка 4: Публикуване на проекта..." -ForegroundColor Yellow
$projectPath = Join-Path $PSScriptRoot "ADS.WindowsAuth.API.csproj"
$publishProfile = Join-Path $PSScriptRoot "Properties\PublishProfiles\FolderProfile.pubxml"

try {
    dotnet publish $projectPath `
        -c Release `
        -p:PublishProfile=$publishProfile `
        -p:PublishUrl=$PublishPath `
        --no-build `
        --no-restore

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Публикуването е успешно!" -ForegroundColor Green
    } else {
        Write-Host "✗ Грешка при публикуване (Exit Code: $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "✗ Грешка при публикуване: $_" -ForegroundColor Red
    exit 1
}

# Стъпка 5: Премахване на app_offline.htm
Write-Host "Стъпка 5: Премахване на app_offline.htm..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
if (Test-Path $appOfflinePath) {
    Remove-Item $appOfflinePath -Force
    Write-Host "✓ app_offline.htm премахнат" -ForegroundColor Green
}

# Стъпка 6: Рестартиране на Application Pool
if (-not $SkipAppPoolRestart) {
    Write-Host "Стъпка 6: Рестартиране на Application Pool..." -ForegroundColor Yellow
    try {
        $appPool = Get-WebAppPoolState -Name $AppPoolName -ErrorAction SilentlyContinue
        if ($appPool) {
            Start-WebAppPool -Name $AppPoolName
            Write-Host "✓ Application Pool рестартиран" -ForegroundColor Green
        }
    } catch {
        Write-Host "⚠ Грешка при рестартиране на Application Pool: $_" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Публикуването е завършено! ===" -ForegroundColor Green
Write-Host "Път: $PublishPath" -ForegroundColor Cyan

