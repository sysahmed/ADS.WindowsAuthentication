# Force unlock DLL by killing processes and unregistering
# Run from Administrator PowerShell

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "FORCE UNLOCKING DLL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$dllPath = "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"

# 1. Check if DLL exists
Write-Host "[1/5] Checking DLL..." -ForegroundColor Yellow
if (-not (Test-Path $dllPath))
{
    Write-Host "X DLL not found: $dllPath" -ForegroundColor Red
    exit 1
}
Write-Host "OK DLL found" -ForegroundColor Green
Write-Host ""

# 2. Find processes using the DLL
Write-Host "[2/5] Finding processes using the DLL..." -ForegroundColor Yellow
$processes = Get-Process | Where-Object {
    try {
        $_.Modules | Where-Object { $_.FileName -eq $dllPath }
    } catch {
        $false
    }
}

if ($processes)
{
    Write-Host "Found processes using DLL:" -ForegroundColor Yellow
    $processes | ForEach-Object {
        Write-Host "  - $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Stopping processes..." -ForegroundColor Yellow
    $processes | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "OK Processes stopped" -ForegroundColor Green
}
else
{
    Write-Host "OK No processes found using DLL" -ForegroundColor Green
}

Write-Host ""

# 3. Unregister DLL
Write-Host "[3/5] Unregistering DLL..." -ForegroundColor Yellow
$result = & regsvr32.exe /u /s "`"$dllPath`"" 2>&1

if ($LASTEXITCODE -eq 0)
{
    Write-Host "OK DLL unregistered" -ForegroundColor Green
}
else
{
    Write-Host "WARNING: Unregistration may have failed" -ForegroundColor Yellow
}

Write-Host ""

# 4. Wait and try to delete/rename
Write-Host "[4/5] Waiting and trying to unlock file..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

$fileUnlocked = $false
$maxRetries = 10
$retryCount = 0

while ($retryCount -lt $maxRetries -and -not $fileUnlocked)
{
    try
    {
        # Try to rename file (this will fail if locked)
        $tempName = $dllPath + ".old"
        if (Test-Path $tempName)
        {
            Remove-Item $tempName -Force -ErrorAction SilentlyContinue
        }
        
        Rename-Item -Path $dllPath -NewName "ADS.WindowsAuth.CredentialProvider.dll.old" -Force -ErrorAction Stop
        Rename-Item -Path "ADS.WindowsAuth.CredentialProvider.dll.old" -NewName "ADS.WindowsAuth.CredentialProvider.dll" -Force -ErrorAction Stop
        $fileUnlocked = $true
        Write-Host "OK File is unlocked!" -ForegroundColor Green
    }
    catch
    {
        $retryCount++
        if ($retryCount -lt $maxRetries)
        {
            Write-Host "File still locked, waiting... (attempt $retryCount/$maxRetries)" -ForegroundColor Yellow
            Start-Sleep -Seconds 2
        }
    }
}

Write-Host ""

# 5. Final check
Write-Host "[5/5] Final check..." -ForegroundColor Yellow
if ($fileUnlocked)
{
    Write-Host "OK File is ready to be replaced!" -ForegroundColor Green
}
else
{
    Write-Host "X File is still locked" -ForegroundColor Red
    Write-Host ""
    Write-Host "SOLUTION: Restart the computer!" -ForegroundColor Red
    Write-Host "After restart, the DLL will be unlocked." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Then run:" -ForegroundColor Cyan
    Write-Host "  .\UPDATE_DLL.ps1" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "DONE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

