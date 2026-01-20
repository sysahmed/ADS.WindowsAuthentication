# Deploy Credential Provider Script
# Този скрипт автоматично deploy-ва новия Credential Provider DLL

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Credential Provider Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Пътища
$buildDll = "d:\Repo\ADS-WIndowsAutentications\ADS.WindowsAuth.CredentialProvider\bin\x64\Release\ADS.WindowsAuth.CredentialProvider.dll"
$targetDll = "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"

# Проверка дали build-натият DLL съществува
if (-not (Test-Path $buildDll)) {
    Write-Host "❌ ГРЕШКА: Build-натият DLL не е намерен!" -ForegroundColor Red
    Write-Host "   Път: $buildDll" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Моля, първо build-ни проекта!" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Build-натият DLL е намерен" -ForegroundColor Green
Write-Host "  Размер: $((Get-Item $buildDll).Length) bytes" -ForegroundColor Gray
Write-Host "  Дата: $((Get-Item $buildDll).LastWriteTime)" -ForegroundColor Gray
Write-Host ""

# Стъпка 1: Спиране на LogonUI процеси
Write-Host "[1/4] Спиране на LogonUI процеси..." -ForegroundColor Yellow
try {
    $logonProcesses = Get-Process -Name "LogonUI" -ErrorAction SilentlyContinue
    if ($logonProcesses) {
        $logonProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
        Write-Host "✓ LogonUI процесите са спрени" -ForegroundColor Green
    }
    else {
        Write-Host "✓ Няма активни LogonUI процеси" -ForegroundColor Green
    }
}
catch {
    Write-Host "⚠ Предупреждение: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

# Стъпка 2: Изтриване на стария DLL
Write-Host "[2/4] Изтриване на стария DLL..." -ForegroundColor Yellow
if (Test-Path $targetDll) {
    try {
        # Опит за unregister на стария DLL
        & regsvr32 /u /s $targetDll 2>$null
        Start-Sleep -Milliseconds 500
        
        # Изтриване
        Remove-Item $targetDll -Force -ErrorAction Stop
        Write-Host "✓ Старият DLL е изтрит" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠ Не може да се изтрие старият DLL: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  Опит за презаписване..." -ForegroundColor Yellow
    }
}
else {
    Write-Host "✓ Няма стар DLL за изтриване" -ForegroundColor Green
}
Write-Host ""

# Стъпка 3: Копиране на новия DLL
Write-Host "[3/4] Копиране на новия DLL..." -ForegroundColor Yellow
try {
    Copy-Item $buildDll -Destination $targetDll -Force -ErrorAction Stop
    Write-Host "✓ Новият DLL е копиран успешно" -ForegroundColor Green
    Write-Host "  От: $buildDll" -ForegroundColor Gray
    Write-Host "  Към: $targetDll" -ForegroundColor Gray
}
catch {
    Write-Host "❌ ГРЕШКА при копиране: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Стъпка 4: Регистриране на новия DLL
Write-Host "[4/4] Регистриране на новия DLL..." -ForegroundColor Yellow
try {
    & regsvr32 /s $targetDll 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ DLL-ът е регистриран успешно" -ForegroundColor Green
    }
    else {
        Write-Host "⚠ Възможна грешка при регистрация (Exit Code: $LASTEXITCODE)" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ ГРЕШКА при регистрация: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Завършване
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ DEPLOYMENT ЗАВЪРШЕН УСПЕШНО!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Следващи стъпки:" -ForegroundColor Yellow
Write-Host "1. Lock екрана (Win+L)" -ForegroundColor White
Write-Host "2. Сканирай QR кода от телефона" -ForegroundColor White
Write-Host "3. Одобри от mobile app" -ForegroundColor White
Write-Host "4. Трябва автоматично да те unlock-не!" -ForegroundColor White
Write-Host ""
