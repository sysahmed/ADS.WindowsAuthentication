# Fix and Register Credential Provider
# Run from Administrator PowerShell

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "FIXING CREDENTIAL PROVIDER" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$CLSID = "{3E879088-249C-4C83-85B6-834A3A9C6D12}"

# 1. Find DLL
Write-Host "[1/5] Searching for DLL..." -ForegroundColor Yellow
$possiblePaths = @(
    "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll",
    "D:\ADS\ADS.WindowsAuth.CredentialProvider.dll",
    "$env:USERPROFILE\Downloads\ADS.WindowsAuth.CredentialProvider.dll",
    "$env:USERPROFILE\Desktop\ADS.WindowsAuth.CredentialProvider.dll"
)

$dllPath = $null
foreach ($path in $possiblePaths)
{
    if (Test-Path $path)
    {
        $dllPath = $path
        break
    }
}

if (-not $dllPath)
{
    Write-Host "X DLL not found!" -ForegroundColor Red
    Write-Host "Copy DLL to one of these locations:" -ForegroundColor Yellow
    $possiblePaths | ForEach-Object { Write-Host "  - $_" -ForegroundColor Gray }
    exit 1
}

Write-Host "OK DLL found: $dllPath" -ForegroundColor Green
Write-Host ""

# 1.5. Copy to C:\ADS if not there
$targetPath = "C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"
if ($dllPath -ne $targetPath)
{
    Write-Host "[1.5/5] Copying to C:\ADS..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "C:\ADS" -Force | Out-Null
    Copy-Item -Path $dllPath -Destination $targetPath -Force
    $dllPath = $targetPath
    Write-Host "OK Copied" -ForegroundColor Green
    Write-Host ""
}

# 2. Unregister old registration
Write-Host "[2/5] Unregistering old registration..." -ForegroundColor Yellow
& regsvr32.exe /u /s "`"$dllPath`"" 2>&1 | Out-Null
Start-Sleep -Seconds 1
Write-Host "OK Done" -ForegroundColor Green
Write-Host ""

# 3. New registration
Write-Host "[3/5] Registering DLL..." -ForegroundColor Yellow
$result = & regsvr32.exe /s "`"$dllPath`"" 2>&1
if ($LASTEXITCODE -ne 0)
{
    Write-Host "X Registration error!" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}
Write-Host "OK DLL registered" -ForegroundColor Green
Write-Host ""

# 4. Check Authentication Registry
Write-Host "[4/5] Checking Authentication Registry..." -ForegroundColor Yellow
$authPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\$CLSID"
if (-not (Test-Path $authPath))
{
    Write-Host "WARNING: Missing Authentication Registry key - creating..." -ForegroundColor Yellow
    New-Item -Path $authPath -Force | Out-Null
    Set-ItemProperty -Path $authPath -Name "(default)" -Value "ADS Windows Auth Credential Provider" -Type String -Force
    Write-Host "OK Created" -ForegroundColor Green
}
else
{
    Write-Host "OK Authentication Registry key exists" -ForegroundColor Green
}

# Configure ServiceUrl
$registryPath = "HKLM:\SOFTWARE\ADS\WindowsAuth"
if (-not (Test-Path $registryPath))
{
    New-Item -Path $registryPath -Force | Out-Null
}
Set-ItemProperty -Path $registryPath -Name "ServiceUrl" -Value "https://ads-auth.nursanbulgaria.com" -Type String -Force
Write-Host "OK ServiceUrl configured" -ForegroundColor Green
Write-Host ""

# 5. Final test
Write-Host "[5/5] Final test..." -ForegroundColor Yellow
$clsidPath = "HKLM:\SOFTWARE\Classes\CLSID\$CLSID"
$authPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\$CLSID"

if ((Test-Path $clsidPath) -and (Test-Path $authPath))
{
    Write-Host "OK Everything registered correctly" -ForegroundColor Green
}
else
{
    Write-Host "WARNING: There are registration issues" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "DONE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "RESTART THE COMPUTER!" -ForegroundColor Red
Write-Host ""
