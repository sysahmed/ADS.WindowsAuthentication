# Скрипт за инсталация на Credential Provider на друга машина
# Изпълни от администраторски PowerShell НА ЦЕЛЕВАТА МАШИНА

param(
    [string]$DllSourcePath = "",
    [string]$InstallPath = "C:\ADS",
    [string]$ServiceUrl = "https://ads-auth.nursanbulgaria.com"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Инсталация на Credential Provider" -ForegroundColor Cyan
Write-Host "На друга машина" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Проверка за администраторски права
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin)
{
    Write-Host "ГРЕШКА: Скриптът трябва да се изпълни с администраторски права!" -ForegroundColor Red
    Write-Host "Моля, стартирай PowerShell като администратор." -ForegroundColor Yellow
    exit 1
}

# Стъпка 1: Намиране на DLL
Write-Host "[1/5] Намиране на DLL..." -ForegroundColor Yellow

if ([string]::IsNullOrEmpty($DllSourcePath))
{
    # Търсене в стандартни локации
    $possiblePaths = @(
        "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll",
        "D:\ADS\ADS.WindowsAuth.CredentialProvider.dll",
        "$env:USERPROFILE\Downloads\ADS.WindowsAuth.CredentialProvider.dll",
        "$env:USERPROFILE\Desktop\ADS.WindowsAuth.CredentialProvider.dll"
    )
    
    foreach ($path in $possiblePaths)
    {
        if (Test-Path $path)
        {
            $DllSourcePath = $path
            break
        }
    }
}

if ([string]::IsNullOrEmpty($DllSourcePath) -or -not (Test-Path $DllSourcePath))
{
    Write-Host "✗ DLL файлът не е намерен!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Моля, укажи пътя до DLL-а:" -ForegroundColor Yellow
    Write-Host "  .\INSTALL_ON_REMOTE_MACHINE.ps1 -DllSourcePath `"C:\Path\To\ADS.WindowsAuth.CredentialProvider.dll`"" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Или копирай DLL-а в една от тези локации:" -ForegroundColor Yellow
    $possiblePaths | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    exit 1
}

Write-Host "✓ DLL намерен: $DllSourcePath" -ForegroundColor Green
$dllInfo = Get-Item $DllSourcePath
Write-Host "  Размер: $([math]::Round($dllInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
Write-Host "  Дата: $($dllInfo.LastWriteTime)" -ForegroundColor Gray
Write-Host ""

# Стъпка 2: Копиране на DLL
Write-Host "[2/5] Копиране на DLL в $InstallPath..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
$targetDll = Join-Path $InstallPath "ADS.WindowsAuth.CredentialProvider.dll"
Copy-Item $DllSourcePath -Destination $targetDll -Force

if (Test-Path $targetDll)
{
    Write-Host "✓ DLL копиран в: $targetDll" -ForegroundColor Green
}
else
{
    Write-Host "✗ Грешка при копиране!" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Стъпка 3: Регистрация
Write-Host "[3/5] Регистрация на DLL..." -ForegroundColor Yellow
$process = Start-Process -FilePath "regsvr32.exe" -ArgumentList "`"$targetDll`"" -Wait -PassThru -NoNewWindow

if ($process.ExitCode -eq 0)
{
    Write-Host "✓ DLL регистриран успешно!" -ForegroundColor Green
}
else
{
    Write-Host "✗ Грешка при регистрация (Exit Code: $($process.ExitCode))" -ForegroundColor Red
    Write-Host ""
    Write-Host "Възможни причини:" -ForegroundColor Yellow
    Write-Host "  1. Липсват зависимости (Visual C++ Redistributable x64)" -ForegroundColor White
    Write-Host "  2. DLL не е правилно компилиран" -ForegroundColor White
    Write-Host ""
    Write-Host "Решения:" -ForegroundColor Cyan
    Write-Host "  1. Инсталирай Visual C++ Redistributable x64:" -ForegroundColor White
    Write-Host "     https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Gray
    Write-Host "  2. Провери Event Viewer за детайлни грешки" -ForegroundColor White
    exit 1
}
Write-Host ""

# Стъпка 4: Конфигуриране на Registry
Write-Host "[4/5] Конфигуриране на API URL..." -ForegroundColor Yellow
$registryPath = "HKLM:\SOFTWARE\ADS\WindowsAuth"

try
{
    New-Item -Path $registryPath -Force | Out-Null
    Set-ItemProperty -Path $registryPath -Name "ServiceUrl" -Value $ServiceUrl -Type String -Force
    $serviceUrlValue = Get-ItemProperty -Path $registryPath -Name "ServiceUrl" -ErrorAction SilentlyContinue
    Write-Host "✓ API URL конфигуриран: $($serviceUrlValue.ServiceUrl)" -ForegroundColor Green
}
catch
{
    Write-Host "⚠ Грешка при конфигуриране на Registry: $_" -ForegroundColor Yellow
    Write-Host "  Можеш да го направиш ръчно:" -ForegroundColor Cyan
    Write-Host "  New-Item -Path `"$registryPath`" -Force" -ForegroundColor White
    Write-Host "  Set-ItemProperty -Path `"$registryPath`" -Name `"ServiceUrl`" -Value `"$ServiceUrl`"" -ForegroundColor White
}
Write-Host ""

# Стъпка 5: Проверка
Write-Host "[5/5] Проверка на инсталацията..." -ForegroundColor Yellow
$clsidPath = "HKLM:\SOFTWARE\Classes\CLSID\{3E879088-249C-4C83-85B6-834A3A9C6D12}"
$credProviderPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{3E879088-249C-4C83-85B6-834A3A9C6D12}"

$allGood = $true

if (Test-Path $clsidPath)
{
    Write-Host "✓ CLSID е регистриран" -ForegroundColor Green
    $inprocPath = "$clsidPath\InprocServer32"
    if (Test-Path $inprocPath)
    {
        $inproc = Get-ItemProperty -Path $inprocPath -Name "(default)" -ErrorAction SilentlyContinue
        if ($inproc.'(default)')
        {
            if ($inproc.'(default)' -eq $targetDll)
            {
                Write-Host "✓ Registry сочи към правилния DLL" -ForegroundColor Green
            }
            else
            {
                Write-Host "⚠ Registry сочи към: $($inproc.'(default)')" -ForegroundColor Yellow
            }
        }
    }
}
else
{
    Write-Host "✗ CLSID НЕ е регистриран!" -ForegroundColor Red
    $allGood = $false
}

if (Test-Path $credProviderPath)
{
    Write-Host "✓ Credential Provider е регистриран в Authentication" -ForegroundColor Green
}
else
{
    Write-Host "✗ Credential Provider НЕ е в Authentication!" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
if ($allGood)
{
    Write-Host "✓ Инсталацията завърши успешно!" -ForegroundColor Green
    Write-Host ""
    Write-Host "ВАЖНО - Следващи стъпки:" -ForegroundColor Yellow
    Write-Host "1. РЕСТАРТИРАЙ КОМПЮТЪРА!" -ForegroundColor Red
    Write-Host "   Credential Provider се зарежда само при стартиране." -ForegroundColor White
    Write-Host ""
    Write-Host "2. След рестартиране, заключи екрана (Win+L)" -ForegroundColor White
    Write-Host "   Трябва да видиш QR код tile." -ForegroundColor White
    Write-Host ""
    Write-Host "3. ⚠️ ЗА ТЕСТВАНЕ:" -ForegroundColor Yellow
    Write-Host "   - Използвай Remote Desktop или виртуална машина" -ForegroundColor White
    Write-Host "   - НЕ тествай на основния компютър!" -ForegroundColor Red
}
else
{
    Write-Host "⚠ Има проблеми с инсталацията!" -ForegroundColor Yellow
    Write-Host "   Провери грешките по-горе." -ForegroundColor White
}
Write-Host "========================================" -ForegroundColor Cyan

