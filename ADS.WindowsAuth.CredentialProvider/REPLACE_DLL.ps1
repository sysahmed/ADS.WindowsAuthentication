на клиента ли трябва да е?
# Simple script to replace DLL
# Copy this script to C:\ADS\ on host machine
# Run: PowerShell -ExecutionPolicy Bypass -File "C:\ADS\REPLACE_DLL.ps1"

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "REPLACING CREDENTIAL PROVIDER DLL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$dllPath = "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"
$newDllPath = ""

# Find new DLL
Write-Host "[1/4] Finding new DLL..." -ForegroundColor Yellow
$possiblePaths = @(
    "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll",
    "$env:USERPROFILE\Downloads\ADS.WindowsAuth.CredentialProvider.dll",
    "$env:USERPROFILE\Desktop\ADS.WindowsAuth.CredentialProvider.dll",
    "C:\Temp\ADS.WindowsAuth.CredentialProvider.dll"
)

foreach ($path in $possiblePaths)
{
    if (Test-Path $path)
    {
        $newDllPath = $path
        Write-Host "OK Found: $newDllPath" -ForegroundColor Green
        break
    }
}

if ([string]::IsNullOrEmpty($newDllPath))
{
    Write-Host "[1/4] Finding new DLL..." -ForegroundColor Yellow
    $possiblePaths = @(
        "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll",
        "$env:USERPROFILE\Downloads\ADS.WindowsAuth.CredentialProvider.dll",
        "$env:USERPROFILE\Desktop\ADS.WindowsAuth.CredentialProvider.dll",
        "C:\Temp\ADS.WindowsAuth.CredentialProvider.dll"
    )

    foreach ($path in $possiblePaths)
    {
        if (Test-Path $path)
        {
            $script:newDllPath = $path
            Write-Host "OK Found: $script:newDllPath" -ForegroundColor Green
            break
        }
    }
}
else
{
    Write-Host "[1/4] Using specified DLL path..." -ForegroundColor Yellow
    Write-Host "OK Using: $script:newDllPath" -ForegroundColor Green
}

if ([string]::IsNullOrEmpty($script:newDllPath))
{
    Write-Host "X New DLL not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Copy the new DLL to one of these locations:" -ForegroundColor Yellow
    $possiblePaths | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    Write-Host ""
    Write-Host "Or specify the path:" -ForegroundColor Yellow
    Write-Host '  .\REPLACE_DLL.ps1 -NewDllPath "C:\Path\To\DLL.dll"' -ForegroundColor Cyan
    exit 1
}

Write-Host ""

# Unregister old DLL
Write-Host "[2/4] Unregistering old DLL..." -ForegroundColor Yellow
if (Test-Path $dllPath)
{
    & regsvr32.exe /u /s "`"$dllPath`"" 2>&1 | Out-Null
    Write-Host "OK Unregistered" -ForegroundColor Green
}
else
{
    Write-Host "WARNING: Old DLL not found" -ForegroundColor Yellow
}

Write-Host ""

# Wait and copy
Write-Host "[3/4] Waiting and copying..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Ensure directory exists
if (-not (Test-Path "C:\ADS"))
{
    New-Item -ItemType Directory -Path "C:\ADS" -Force | Out-Null
}

# Try to delete old DLL
if (Test-Path $dllPath)
{
    try
    {
        Remove-Item -Path $dllPath -Force -ErrorAction Stop
        Write-Host "OK Old DLL deleted" -ForegroundColor Green
    }
    catch
    {
        Write-Host "WARNING: Could not delete old DLL, will try to overwrite" -ForegroundColor Yellow
    }
}

# Copy new DLL (only if source and destination are different)
if ($newDllPath -ne $dllPath)
{
    try
    {
        Copy-Item -Path $newDllPath -Destination $dllPath -Force
        Write-Host "OK New DLL copied" -ForegroundColor Green
    }
    catch
    {
        Write-Host "X Error copying DLL!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "SOLUTION: Restart the computer, then run this script again" -ForegroundColor Yellow
        exit 1
    }
}
else
{
    Write-Host "OK DLL is already in the correct location" -ForegroundColor Green
}

Write-Host ""

# Register new DLL
Write-Host "[4/4] Registering new DLL..." -ForegroundColor Yellow
$result = & regsvr32.exe /s "`"$dllPath`"" 2>&1
if ($LASTEXITCODE -eq 0)
{
    Write-Host "OK DLL registered" -ForegroundColor Green
}
else
{
    Write-Host "X Registration error!" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "DONE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "RESTART THE COMPUTER!" -ForegroundColor Red
Write-Host ""

