# Unlock DLL by unregistering Credential Provider
# Run from Administrator PowerShell

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "UNLOCKING DLL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$dllPath = "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"

# 1. Check if DLL exists
Write-Host "[1/3] Checking DLL..." -ForegroundColor Yellow
if (-not (Test-Path $dllPath))
{
    Write-Host "X DLL not found: $dllPath" -ForegroundColor Red
    exit 1
}
Write-Host "OK DLL found" -ForegroundColor Green
Write-Host ""

# 2. Unregister DLL (this will unlock it)
Write-Host "[2/3] Unregistering DLL (this will unlock it)..." -ForegroundColor Yellow
$result = & regsvr32.exe /u /s "`"$dllPath`"" 2>&1

if ($LASTEXITCODE -eq 0)
{
    Write-Host "OK DLL unregistered" -ForegroundColor Green
}
else
{
    Write-Host "WARNING: Unregistration may have failed, but continuing..." -ForegroundColor Yellow
    Write-Host $result -ForegroundColor Gray
}

Write-Host ""

# 3. Wait for file to be released
Write-Host "[3/3] Waiting for file to be released..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Try to check if file is still locked
$fileUnlocked = $false
$maxRetries = 5
$retryCount = 0

while ($retryCount -lt $maxRetries -and -not $fileUnlocked)
{
    try
    {
        # Try to open file for writing (this will fail if locked)
        $file = [System.IO.File]::OpenWrite($dllPath)
        $file.Close()
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
        else
        {
            Write-Host "WARNING: File may still be locked" -ForegroundColor Yellow
            Write-Host "Try restarting the computer, then run UPDATE_DLL.ps1" -ForegroundColor White
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "DONE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Now you can:" -ForegroundColor Cyan
Write-Host "  1. Copy new DLL to C:\ADS\" -ForegroundColor White
Write-Host "  2. Run UPDATE_DLL.ps1 to register it" -ForegroundColor White
Write-Host ""
Write-Host "Or if file is still locked:" -ForegroundColor Yellow
Write-Host "  - Restart the computer" -ForegroundColor White
Write-Host "  - Then run UPDATE_DLL.ps1" -ForegroundColor White
Write-Host ""

