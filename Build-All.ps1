# ADS Windows Authentication - Build & Installer Script
# Стартирай: .\Build-All.ps1
# Изисква: .NET 8 SDK, Inno Setup 6 (за инсталатор)

param(
    [switch]$SkipInstaller,
    [switch]$ClientOnly
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "=== ADS Windows Authentication - Build ===" -ForegroundColor Cyan
Write-Host ""

# 1. Build Core + Monitor (Monitor needed for Client's CopyMonitorFiles)
Write-Host "[1/3] Building Core and Monitor..." -ForegroundColor Yellow
dotnet build "ADS.WindowsAuth.Core\ADS.WindowsAuth.Core.csproj" -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Core build failed" }
dotnet build "ADS.WindowsAuth.Monitor\ADS.WindowsAuth.Monitor.csproj" -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Monitor build failed" }

if (-not $ClientOnly) {
    dotnet build "ADS.WindowsAuth.API\ADS.WindowsAuth.API.csproj" -c Release --verbosity minimal
    dotnet build "ADS.WindowsAuth.Service\ADS.WindowsAuth.Service.csproj" -c Release --verbosity minimal
}

# 2. Build Client (includes Monitor files via CopyMonitorFiles target)
Write-Host "[2/3] Building Client..." -ForegroundColor Yellow
dotnet build "ADS.WindowsAuth.Client\ADS.WindowsAuth.Client.csproj" -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "Client build failed" }

# 3. Create installer (Inno Setup)
if (-not $SkipInstaller) {
    Write-Host "[3/3] Creating installer..." -ForegroundColor Yellow
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $iscc) {
        & $iscc "ADS.WindowsAuth.Client\Installer\ADS-Windows-Auth-Client.iss"
        if ($LASTEXITCODE -eq 0) {
            $outFile = Resolve-Path "ADS.WindowsAuth.Client\InstallerOutput\*.exe" -ErrorAction SilentlyContinue
            if ($outFile) {
                Write-Host ""
                Write-Host "SUCCESS: Installer created:" -ForegroundColor Green
                Write-Host "  $outFile" -ForegroundColor White
            }
        } else { Write-Host "Inno Setup failed." -ForegroundColor Red }
    } else {
        Write-Host "Inno Setup not found. Install from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
        Write-Host "Client output: ADS.WindowsAuth.Client\bin\Release\net8.0-windows8.0\" -ForegroundColor Gray
    }
} else {
    Write-Host "[3/3] Skipped (use -SkipInstaller to disable)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
