# Скрипт за създаване на MSI инсталатор
# Използва WiX Toolset

param(
    [string]$WixPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Създаване на MSI инсталатор" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Проверка за WiX Toolset
$candle = Join-Path $WixPath "candle.exe"
$light = Join-Path $WixPath "light.exe"

if (-not (Test-Path $candle) -or -not (Test-Path $light))
{
    Write-Host "ГРЕШКА: WiX Toolset не е намерен!" -ForegroundColor Red
    Write-Host "Инсталирай от: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Или укажи пътя:" -ForegroundColor Cyan
    Write-Host "  .\INSTALL_MSI.ps1 -WixPath `"C:\Path\To\WiX\bin`"" -ForegroundColor White
    exit 1
}

Write-Host "✓ WiX Toolset намерен" -ForegroundColor Green
Write-Host ""

# Пътища
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$wxsFile = Join-Path $scriptPath "SIMPLE_MSI.wxs"

# Търсене на DLL в различни локации
$dllSource = $null
$possibleDllPaths = @(
    Join-Path $scriptPath "..\bin\x64\Release\ADS.WindowsAuth.CredentialProvider.dll",
    "D:\Repo\ADS-WIndowsAutentications\bin\x64\Release\ADS.WindowsAuth.CredentialProvider.dll",
    "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"
)

foreach ($path in $possibleDllPaths)
{
    if (Test-Path $path)
    {
        $dllSource = $path
        break
    }
}

# Проверка на файлове
if (-not (Test-Path $wxsFile))
{
    Write-Host "✗ WiX файл не е намерен: $wxsFile" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrEmpty($dllSource) -or -not (Test-Path $dllSource))
{
    Write-Host "✗ DLL не е намерен!" -ForegroundColor Red
    Write-Host "Търсени пътища:" -ForegroundColor Yellow
    $possibleDllPaths | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    Write-Host ""
    Write-Host "Моля, компилирай проекта първо (Release x64)!" -ForegroundColor Yellow
    Write-Host "Или копирай DLL-а в една от тези локации." -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ DLL намерен: $dllSource" -ForegroundColor Green

# Копиране на DLL в локацията, която WiX очаква (относителен път)
$wixDllPath = Join-Path $scriptPath "..\bin\x64\Release\ADS.WindowsAuth.CredentialProvider.dll"
$wixDllDir = Split-Path -Parent $wixDllPath

if (-not (Test-Path $wixDllDir))
{
    Write-Host "Създаване на директория: $wixDllDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $wixDllDir -Force | Out-Null
}

if ($dllSource -ne $wixDllPath)
{
    Write-Host "Копиране на DLL в локацията за WiX..." -ForegroundColor Yellow
    Copy-Item -Path $dllSource -Destination $wixDllPath -Force
    Write-Host "✓ DLL копиран в: $wixDllPath" -ForegroundColor Green
}

Write-Host "✓ Всички файлове са налични" -ForegroundColor Green
Write-Host ""

# Компилиране
Write-Host "[1/2] Компилиране на WiX файл..." -ForegroundColor Yellow
$wixobjFile = Join-Path $scriptPath "SIMPLE_MSI.wixobj"
& $candle -out $wixobjFile $wxsFile

if ($LASTEXITCODE -ne 0)
{
    Write-Host "✗ Грешка при компилиране!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Компилиране успешно" -ForegroundColor Green
Write-Host ""

Write-Host "[2/2] Създаване на MSI..." -ForegroundColor Yellow
$msiFile = Join-Path $scriptPath "ADS.WindowsAuth.CredentialProvider.msi"
& $light -out $msiFile $wixobjFile

if ($LASTEXITCODE -eq 0)
{
    Write-Host "✓ MSI създаден успешно!" -ForegroundColor Green
    Write-Host "  Файл: $msiFile" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "За инсталация:" -ForegroundColor Yellow
    Write-Host "  msiexec /i `"$msiFile`"" -ForegroundColor Cyan
}
else
{
    Write-Host "✗ Грешка при създаване на MSI!" -ForegroundColor Red
    exit 1
}

