# PowerShell скрипт за автоматична инсталация на ADS Windows Authentication System
# Изтеглен от: https://ads-auth.nursanbulgaria.com/download/installer

param(
    [string]$InstallPath = "C:\ADS",
    [string]$ServiceUrl = "https://ads-auth.nursanbulgaria.com",
    [switch]$SkipCredentialProvider
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ADS Windows Authentication - Инсталация" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Проверка за администраторски права
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin)
{
    Write-Host "ГРЕШКА: Скриптът трябва да се изпълни с администраторски права!" -ForegroundColor Red
    Write-Host "Моля, стартирайте PowerShell като администратор." -ForegroundColor Yellow
    exit 1
}

# Създаване на директории
Write-Host "[1/6] Създаване на директории..." -ForegroundColor Yellow
$clientPath = Join-Path $InstallPath "Client"
$monitorPath = Join-Path $InstallPath "Monitor"
$credentialProviderPath = Join-Path $InstallPath "CredentialProvider"

New-Item -ItemType Directory -Path $clientPath -Force | Out-Null
New-Item -ItemType Directory -Path $monitorPath -Force | Out-Null
New-Item -ItemType Directory -Path $credentialProviderPath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $monitorPath "LOGS") -Force | Out-Null

Write-Host "✓ Директории създадени" -ForegroundColor Green
Write-Host ""

# Download на Windows Forms клиент
Write-Host "[2/6] Изтегляне на Windows Forms клиент..." -ForegroundColor Yellow
try
{
    $clientUrl = "$ServiceUrl/download/client"
    $clientFile = Join-Path $clientPath "ADS.WindowsAuth.Client.exe"
    
    Write-Host "Изтегляне от: $clientUrl" -ForegroundColor Cyan
    Invoke-WebRequest -Uri $clientUrl -OutFile $clientFile -UseBasicParsing
    
    if (Test-Path $clientFile)
    {
        Write-Host "✓ Клиент изтеглен успешно" -ForegroundColor Green
    }
    else
    {
        Write-Host "⚠ Клиентът не е изтеглен (може да не е компилиран)" -ForegroundColor Yellow
    }
}
catch
{
    Write-Host "⚠ Грешка при изтегляне на клиент: $_" -ForegroundColor Yellow
}
Write-Host ""

# Download на Monitor Service
Write-Host "[3/6] Изтегляне на Monitor Service..." -ForegroundColor Yellow
Write-Host "⚠ Monitor Service трябва да се компилира локално" -ForegroundColor Yellow
Write-Host "   Команда: dotnet build ADS.WindowsAuth.Monitor\ADS.WindowsAuth.Monitor.csproj --configuration Release" -ForegroundColor Cyan
Write-Host "   След това копирайте файловете от bin\Release\net8.0 в $monitorPath" -ForegroundColor Cyan
Write-Host ""

# Инсталация на Monitor Service
Write-Host "[4/6] Инсталация на Monitor Service..." -ForegroundColor Yellow
try
{
    $monitorExe = Join-Path $monitorPath "ADS.WindowsAuth.Monitor.exe"
    
    if (Test-Path $monitorExe)
    {
        # Проверка дали сервизът вече съществува
        $existingService = Get-Service -Name "ADS.WindowsAuth.Monitor" -ErrorAction SilentlyContinue
        
        if ($existingService)
        {
            Write-Host "Сервизът вече съществува. Премахване..." -ForegroundColor Yellow
            Stop-Service -Name "ADS.WindowsAuth.Monitor" -Force -ErrorAction SilentlyContinue
            sc.exe delete "ADS.WindowsAuth.Monitor" | Out-Null
            Start-Sleep -Seconds 2
        }
        
        # Инсталация
        $binPath = "`"$monitorExe`""
        sc.exe create "ADS.WindowsAuth.Monitor" binpath= $binPath start= auto | Out-Null
        
        if ($LASTEXITCODE -eq 0)
        {
            Write-Host "✓ Monitor Service инсталиран успешно" -ForegroundColor Green
            
            # Стартиране на сервиза
            Start-Service -Name "ADS.WindowsAuth.Monitor"
            Write-Host "✓ Monitor Service стартиран" -ForegroundColor Green
        }
        else
        {
            Write-Host "⚠ Грешка при инсталация на Monitor Service" -ForegroundColor Yellow
        }
    }
    else
    {
        Write-Host "⚠ Monitor Service не е намерен в $monitorPath" -ForegroundColor Yellow
        Write-Host "   Моля, копирайте компилираните файлове там първо." -ForegroundColor Cyan
    }
}
catch
{
    Write-Host "⚠ Грешка при инсталация на Monitor Service: $_" -ForegroundColor Yellow
}
Write-Host ""

# Инсталация на Credential Provider DLL
if (-not $SkipCredentialProvider)
{
    Write-Host "[5/6] Инсталация на Credential Provider DLL..." -ForegroundColor Yellow
    Write-Host "⚠ Credential Provider трябва да се компилира в Visual Studio (Release x64)" -ForegroundColor Yellow
    Write-Host "   След това копирайте DLL-а в $credentialProviderPath" -ForegroundColor Cyan
    Write-Host ""
    
    $dllFile = Join-Path $credentialProviderPath "ADS.WindowsAuth.CredentialProvider.dll"
    
    if (Test-Path $dllFile)
    {
        Write-Host "Регистрация на Credential Provider..." -ForegroundColor Cyan
        regsvr32 /s "`"$dllFile`""
        
        if ($LASTEXITCODE -eq 0)
        {
            Write-Host "✓ Credential Provider регистриран успешно" -ForegroundColor Green
        }
        else
        {
            Write-Host "⚠ Грешка при регистрация на Credential Provider" -ForegroundColor Yellow
        }
    }
    else
    {
        Write-Host "⚠ Credential Provider DLL не е намерен в $credentialProviderPath" -ForegroundColor Yellow
    }
}
else
{
    Write-Host "[5/6] Пропускане на Credential Provider (SkipCredentialProvider)" -ForegroundColor Yellow
}
Write-Host ""

# Конфигуриране
Write-Host "[6/6] Конфигуриране..." -ForegroundColor Yellow
try
{
    # Конфигуриране на Monitor Service
    $appsettingsPath = Join-Path $monitorPath "appsettings.json"
    if (Test-Path $appsettingsPath)
    {
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        $appsettings.ServiceConfiguration.ServiceUrl = $ServiceUrl
        $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
        Write-Host "✓ appsettings.json обновен" -ForegroundColor Green
    }
    
    # Registry настройки за Credential Provider
    if (-not $SkipCredentialProvider)
    {
        $registryPath = "HKLM:\SOFTWARE\ADS\WindowsAuth"
        New-Item -Path $registryPath -Force | Out-Null
        Set-ItemProperty -Path $registryPath -Name "ServiceUrl" -Value $ServiceUrl -Force
        Write-Host "✓ Registry настройки конфигурирани" -ForegroundColor Green
    }
}
catch
{
    Write-Host "⚠ Грешка при конфигуриране: $_" -ForegroundColor Yellow
}
Write-Host ""

# Край
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Инсталацията е завършена!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Следващи стъпки:" -ForegroundColor Yellow
Write-Host "1. Рестартирайте компютъра за Credential Provider" -ForegroundColor White
Write-Host "2. Проверете статуса на Monitor Service:" -ForegroundColor White
Write-Host "   sc.exe query ADS.WindowsAuth.Monitor" -ForegroundColor Cyan
Write-Host "3. Проверете логовете:" -ForegroundColor White
Write-Host "   Get-Content `"$monitorPath\LOGS\NURSAN*.LOG`" -Tail 50" -ForegroundColor Cyan
Write-Host ""

