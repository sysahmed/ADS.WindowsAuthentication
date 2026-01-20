# Скрипт за автоматичен rebuild и install на Credential Provider DLL
# Използване: .\RebuildAndInstall.ps1

param(
    [switch]$SkipBuild = $false,
    [switch]$SkipRestart = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Credential Provider - Rebuild & Install" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Намиране на пътищата
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = Split-Path -Parent $scriptPath
$projectFile = Join-Path $scriptPath "ADS.WindowsAuth.CredentialProvider.vcxproj"
$outputDir = Join-Path $solutionDir "bin\x64\Release"
$dllName = "ADS.WindowsAuth.CredentialProvider.dll"
$sourceDll = Join-Path $outputDir $dllName
$targetDir = "C:\ADS"
$targetDll = Join-Path $targetDir $dllName
$clsid = "{3E879088-249C-4C83-85B6-834A3A9C6D12}"

Write-Host "[1/5] Проверка на пътищата..." -ForegroundColor Yellow
if (-not (Test-Path $projectFile)) {
    Write-Host "❌ Проект файлът не е намерен: $projectFile" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Проект файл: $projectFile" -ForegroundColor Green
Write-Host ""

# Стъпка 1: Rebuild
if (-not $SkipBuild) {
    Write-Host "[2/5] Rebuild на проекта (Release x64)..." -ForegroundColor Yellow
    
    # Намиране на MSBuild
    $msbuildPath = & "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null
    
    if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
        # Fallback към стандартния път
        $msbuildPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
        if (-not (Test-Path $msbuildPath)) {
            $msbuildPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
        }
        if (-not (Test-Path $msbuildPath)) {
            $msbuildPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
        }
    }
    
    if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
        Write-Host "❌ MSBuild не е намерен. Моля, компилирайте проекта ръчно в Visual Studio." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Използване на MSBuild: $msbuildPath" -ForegroundColor Cyan
    
    # Rebuild
    $buildArgs = @(
        $projectFile,
        "/t:Rebuild",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        "/v:minimal",
        "/nologo"
    )
    
    Write-Host "Изпълняване на MSBuild..." -ForegroundColor Cyan
    & $msbuildPath $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build неуспешен!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Build успешен!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[2/5] Пропускане на build (SkipBuild)" -ForegroundColor Yellow
    Write-Host ""
}

# Стъпка 2: Проверка дали DLL съществува
Write-Host "[3/5] Проверка на DLL..." -ForegroundColor Yellow
if (-not (Test-Path $sourceDll)) {
    Write-Host "❌ DLL не е намерен: $sourceDll" -ForegroundColor Red
    Write-Host "Моля, компилирайте проекта първо." -ForegroundColor Yellow
    exit 1
}
Write-Host "✓ DLL намерен: $sourceDll" -ForegroundColor Green
Write-Host ""

# Стъпка 3: Отмяна на стара регистрация
Write-Host "[4/5] Отмяна на стара регистрация..." -ForegroundColor Yellow
if (Test-Path $targetDll) {
    Write-Host "Отмяна на регистрация на стария DLL..." -ForegroundColor Cyan
    regsvr32 /u /s "`"$targetDll`""
    Start-Sleep -Milliseconds 500
}
Write-Host "✓ Старата регистрация е отменена" -ForegroundColor Green
Write-Host ""

# Стъпка 4: Копиране на DLL
Write-Host "[5/5] Копиране и регистрация на DLL..." -ForegroundColor Yellow

# Създаване на директория
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "Създадена директория: $targetDir" -ForegroundColor Cyan
}

# Копиране
try {
    if (Test-Path $targetDll) {
        # Опит за unlock на файла (ако е locked)
        $file = Get-Item $targetDll -ErrorAction SilentlyContinue
        if ($file) {
            $file.IsReadOnly = $false
        }
        Remove-Item $targetDll -Force -ErrorAction Stop
    }
    Copy-Item $sourceDll $targetDll -Force
    Write-Host "✓ DLL копиран в: $targetDll" -ForegroundColor Green
} catch {
    Write-Host "❌ Грешка при копиране: $_" -ForegroundColor Red
    Write-Host "Моля, затворете всички програми които могат да използват DLL-а и опитайте отново." -ForegroundColor Yellow
    exit 1
}

# Регистрация
Write-Host "Регистрация на DLL..." -ForegroundColor Cyan
regsvr32 /s "`"$targetDll`""

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ DLL регистриран успешно!" -ForegroundColor Green
} else {
    Write-Host "⚠ Възможно е DLL-ът да не е регистриран правилно. Проверете ръчно с: regsvr32 `"$targetDll`"" -ForegroundColor Yellow
}

Write-Host ""

# Стъпка 5: Рестартиране на Explorer (опционално)
if (-not $SkipRestart) {
    Write-Host "[6/6] Рестартиране на Explorer за зареждане на новия DLL..." -ForegroundColor Yellow
    Write-Host "ВНИМАНИЕ: Това ще затвори всички Explorer прозорци!" -ForegroundColor Yellow
    Write-Host "Натиснете Enter за продължение или Ctrl+C за отказ..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process explorer.exe
    Write-Host "✓ Explorer рестартиран" -ForegroundColor Green
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ Готово! Credential Provider е обновен." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "За тестване:" -ForegroundColor Yellow
Write-Host "1. Излезте от Windows (Win+L)" -ForegroundColor Cyan
Write-Host "2. Влезте отново" -ForegroundColor Cyan
Write-Host "3. Изберете QR плочката" -ForegroundColor Cyan
Write-Host "4. Проверете лога: C:\ADS\Logs\ADS_CredentialProvider_QR.log" -ForegroundColor Cyan
Write-Host ""

