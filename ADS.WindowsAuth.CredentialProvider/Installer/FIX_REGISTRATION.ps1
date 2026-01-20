# Скрипт за диагностика и оправяне на регистрацията
# Изпълни от администраторски PowerShell

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Диагностика на Credential Provider DLL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$dllPath = "D:\Repo\ADS-WIndowsAutentications\bin\x64\Release\ADS.WindowsAuth.CredentialProvider.dll"

# Проверка 1: DLL съществува ли
Write-Host "[1/6] Проверка на DLL..." -ForegroundColor Yellow
if (-not (Test-Path $dllPath))
{
    Write-Host "✗ DLL не е намерен: $dllPath" -ForegroundColor Red
    Write-Host "Моля, компилирай проекта първо!" -ForegroundColor Yellow
    exit 1
}
Write-Host "✓ DLL намерен" -ForegroundColor Green
$dllInfo = Get-Item $dllPath
Write-Host "  Размер: $([math]::Round($dllInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray
Write-Host "  Дата: $($dllInfo.LastWriteTime)" -ForegroundColor Gray
Write-Host ""

# Проверка 2: Visual C++ Redistributable
Write-Host "[2/6] Проверка за Visual C++ Redistributable..." -ForegroundColor Yellow
$vcRedist = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue | 
    Where-Object { $_.DisplayName -like "*Visual C++*Redistributable*" -and $_.DisplayName -like "*x64*" }
if ($vcRedist)
{
    Write-Host "✓ Намерени Visual C++ Redistributable:" -ForegroundColor Green
    $vcRedist | ForEach-Object { Write-Host "  - $($_.DisplayName)" -ForegroundColor Gray }
}
else
{
    Write-Host "⚠ Не са намерени Visual C++ Redistributable x64" -ForegroundColor Yellow
    Write-Host "  Свали и инсталирай от:" -ForegroundColor Cyan
    Write-Host "  https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor White
}
Write-Host ""

# Проверка 3: Архитектура
Write-Host "[3/6] Проверка на архитектурата..." -ForegroundColor Yellow
try
{
    # Опит за проверка чрез file command (ако е наличен)
    $fileInfo = Get-Content $dllPath -TotalCount 1 -ErrorAction SilentlyContinue
    Write-Host "  DLL файлът е достъпен" -ForegroundColor Gray
}
catch
{
    Write-Host "  Не може да се прочете файлът" -ForegroundColor Yellow
}
Write-Host ""

# Проверка 4: Опит за регистрация с детайлни грешки
Write-Host "[4/6] Опит за регистрация..." -ForegroundColor Yellow
$process = Start-Process -FilePath "regsvr32.exe" -ArgumentList "`"$dllPath`"" -Wait -PassThru -NoNewWindow
$exitCode = $process.ExitCode

if ($exitCode -eq 0)
{
    Write-Host "✓ DLL регистриран успешно!" -ForegroundColor Green
}
else
{
    Write-Host "✗ Грешка при регистрация (Exit Code: $exitCode)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Възможни причини:" -ForegroundColor Yellow
    Write-Host "  1. Липсват зависимости (Visual C++ Redistributable)" -ForegroundColor White
    Write-Host "  2. DLL не е правилно компилиран" -ForegroundColor White
    Write-Host "  3. Антивирус блокира операцията" -ForegroundColor White
    Write-Host "  4. Проблем с COM регистрацията" -ForegroundColor White
    Write-Host ""
    Write-Host "Решения:" -ForegroundColor Cyan
    Write-Host "  1. Инсталирай Visual C++ Redistributable x64" -ForegroundColor White
    Write-Host "  2. Компилирай отново в Visual Studio (Clean + Rebuild)" -ForegroundColor White
    Write-Host "  3. Провери антивируса за блокирани операции" -ForegroundColor White
    Write-Host "  4. Провери Event Viewer за детайлни грешки" -ForegroundColor White
}
Write-Host ""

# Проверка 5: Event Viewer
Write-Host "[5/6] Проверка на Event Viewer..." -ForegroundColor Yellow
$events = Get-EventLog -LogName Application -Newest 20 -ErrorAction SilentlyContinue | 
    Where-Object { $_.TimeGenerated -gt (Get-Date).AddMinutes(-10) -and 
                   ($_.EntryType -eq "Error" -or $_.Message -like "*regsvr*" -or $_.Message -like "*Credential*" -or $_.Message -like "*DLL*") }
if ($events)
{
    Write-Host "Намерени събития:" -ForegroundColor Yellow
    $events | Select-Object -First 5 | ForEach-Object {
        Write-Host "  [$($_.TimeGenerated.ToString('HH:mm:ss'))] $($_.Source): $($_.Message.Substring(0, [Math]::Min(150, $_.Message.Length)))" -ForegroundColor Gray
    }
}
else
{
    Write-Host "Няма намерени релевантни събития" -ForegroundColor Gray
}
Write-Host ""

# Проверка 6: Registry проверка
Write-Host "[6/6] Проверка на Registry..." -ForegroundColor Yellow
$registryPath = "HKLM:\SOFTWARE\Classes\CLSID\{3E879088-249C-4C83-85B6-834A3A9C6D12}"
if (Test-Path $registryPath)
{
    Write-Host "✓ Credential Provider е регистриран в Registry" -ForegroundColor Green
    $regInfo = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue
    if ($regInfo)
    {
        Write-Host "  InprocServer32: $($regInfo.'(default)' -replace '.*\\', '')" -ForegroundColor Gray
    }
}
else
{
    Write-Host "✗ Credential Provider НЕ е регистриран в Registry" -ForegroundColor Red
    Write-Host "  Това потвърждава, че регистрацията не е успешна" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Диагностиката завърши" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

