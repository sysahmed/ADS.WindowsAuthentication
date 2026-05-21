# Install ADS.WindowsAuth.Monitor as Windows Service
# Run from Administrator PowerShell

param(
    [string]$ExePath = "",
    [string]$InstallPath = "C:\ADS\Monitor"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "INSTALLING ADS WINDOWS AUTH MONITOR" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check for administrator rights
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin)
{
    Write-Host "X ERROR: Script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator" -ForegroundColor Yellow
    exit 1
}

$serviceName = "ADS.WindowsAuth.Monitor"
$serviceDisplayName = "ADS Windows Authentication Monitor"
$serviceDescription = "Monitors user activity and manages Credential Provider installation"

# Find the executable
if ([string]::IsNullOrEmpty($ExePath))
{
    $possiblePaths = @(
        "$PSScriptRoot\ADS.WindowsAuth.Monitor.exe",
        "$PSScriptRoot\bin\Release\net8.0\ADS.WindowsAuth.Monitor.exe",
        "$PSScriptRoot\bin\Debug\net8.0\ADS.WindowsAuth.Monitor.exe",
        "$PSScriptRoot\bin\x64\Release\net8.0\ADS.WindowsAuth.Monitor.exe",
        "$PSScriptRoot\bin\x64\Debug\net8.0\ADS.WindowsAuth.Monitor.exe"
    )

    foreach ($path in $possiblePaths)
    {
        if (Test-Path $path)
        {
            $ExePath = (Resolve-Path $path).Path
            break
        }
    }
}

if ([string]::IsNullOrEmpty($ExePath) -or -not (Test-Path $ExePath))
{
    Write-Host "X Executable not found!" -ForegroundColor Red
    Write-Host "Build the project first (Release configuration) or specify the path:" -ForegroundColor Yellow
    Write-Host '  .\INSTALL_SERVICE.ps1 -ExePath "C:\Path\To\ADS.WindowsAuth.Monitor.exe"' -ForegroundColor Cyan
    exit 1
}

$ExePath = (Resolve-Path $ExePath).Path
Write-Host "OK Found executable: $ExePath" -ForegroundColor Green
Write-Host ""

# Create installation directory
Write-Host "[0/4] Creating installation directory..." -ForegroundColor Yellow
if (-not (Test-Path $InstallPath))
{
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
    Write-Host "OK Directory created: $InstallPath" -ForegroundColor Green
}
else
{
    Write-Host "OK Directory exists: $InstallPath" -ForegroundColor Green
}

# Copy files to installation directory
Write-Host ""
Write-Host "[1/4] Copying files to installation directory..." -ForegroundColor Yellow
$exeDir = Split-Path $ExePath -Parent
$exeName = Split-Path $ExePath -Leaf

# Copy executable and all DLLs
Copy-Item "$ExePath" "$InstallPath\" -Force
Copy-Item "$exeDir\*.dll" "$InstallPath\" -Force -ErrorAction SilentlyContinue
Copy-Item "$exeDir\*.json" "$InstallPath\" -Force -ErrorAction SilentlyContinue
Copy-Item "$exeDir\*.pdb" "$InstallPath\" -Force -ErrorAction SilentlyContinue

# Copy Credential Provider DLL if exists
$credentialProviderDll = Join-Path $exeDir "ADS.WindowsAuth.CredentialProvider.dll"
$credentialProviderDllInSubfolder = Join-Path $exeDir "CredentialProvider\ADS.WindowsAuth.CredentialProvider.dll"

# Check in subfolder first, then root
if (Test-Path $credentialProviderDllInSubfolder)
{
    $credentialProviderDir = Join-Path $InstallPath "CredentialProvider"
    if (-not (Test-Path $credentialProviderDir))
    {
        New-Item -ItemType Directory -Path $credentialProviderDir -Force | Out-Null
    }
    Copy-Item $credentialProviderDllInSubfolder $credentialProviderDir -Force
    Write-Host "OK Credential Provider DLL copied from CredentialProvider subfolder" -ForegroundColor Green
}
elseif (Test-Path $credentialProviderDll)
{
    $credentialProviderDir = Join-Path $InstallPath "CredentialProvider"
    if (-not (Test-Path $credentialProviderDir))
    {
        New-Item -ItemType Directory -Path $credentialProviderDir -Force | Out-Null
    }
    Copy-Item $credentialProviderDll $credentialProviderDir -Force
    Write-Host "OK Credential Provider DLL copied" -ForegroundColor Green
}

# Copy RemoteDesktopHost (InputCapture, screen capture) - Monitor го стартира в потребителска сесия
$rdHostSource = Join-Path $exeDir "RemoteDesktopHost"
if (Test-Path $rdHostSource)
{
    $rdHostDest = Join-Path $InstallPath "RemoteDesktopHost"
    if (-not (Test-Path $rdHostDest)) { New-Item -ItemType Directory -Path $rdHostDest -Force | Out-Null }
    Copy-Item "$rdHostSource\*" $rdHostDest -Recurse -Force
    Write-Host "OK RemoteDesktopHost copied (InputCapture, screen capture)" -ForegroundColor Green
}
else
{
    Write-Host "WARNING: RemoteDesktopHost folder not found. Build Monitor first - InputCapture will not work." -ForegroundColor Yellow
}

$serviceExePath = Join-Path $InstallPath $exeName
Write-Host "OK Files copied to: $InstallPath" -ForegroundColor Green
Write-Host ""

# Check if service already exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($existingService)
{
    Write-Host "[1/3] Service already exists. Stopping and removing..." -ForegroundColor Yellow
    if ($existingService.Status -eq 'Running')
    {
        Stop-Service -Name $serviceName -Force
        Start-Sleep -Seconds 2
    }
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "OK Old service removed" -ForegroundColor Green
}
else
{
    Write-Host "[1/3] Service does not exist. Creating new..." -ForegroundColor Yellow
}

Write-Host ""

# Install the service
Write-Host "[2/4] Installing service..." -ForegroundColor Yellow
$result = sc.exe create $serviceName binPath= "`"$serviceExePath`"" DisplayName= "$serviceDisplayName" start= auto

if ($LASTEXITCODE -eq 0)
{
    Write-Host "OK Service installed" -ForegroundColor Green
    
    # Set description
    sc.exe description $serviceName "$serviceDescription" | Out-Null
    
    Write-Host ""
    Write-Host "[3/4] Starting service..." -ForegroundColor Yellow
    Start-Service -Name $serviceName
    Start-Sleep -Seconds 2
    
    $service = Get-Service -Name $serviceName
    if ($service.Status -eq 'Running')
    {
        Write-Host "OK Service started successfully!" -ForegroundColor Green
    }
    else
    {
        Write-Host "WARNING: Service installed but not running. Status: $($service.Status)" -ForegroundColor Yellow
        Write-Host "Check Event Viewer for errors" -ForegroundColor Yellow
    }
}
else
{
    Write-Host "X Error installing service!" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[4/4] Service will automatically install Credential Provider..." -ForegroundColor Yellow
Write-Host "OK Service will check and install DLL on startup" -ForegroundColor Green
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "DONE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Service installed and started!" -ForegroundColor Green
Write-Host ""
Write-Host "Service location: $InstallPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "The service will:" -ForegroundColor Cyan
Write-Host "  - Automatically install Credential Provider on startup" -ForegroundColor White
Write-Host "  - Check every 5 minutes if DLL is installed" -ForegroundColor White
Write-Host "  - Update DLL if newer version is found" -ForegroundColor White
Write-Host "  - Monitor user activity (processes, screen time, network, USB, files)" -ForegroundColor White
Write-Host "  - Send all data to API automatically" -ForegroundColor White
Write-Host ""
Write-Host "To manage the service:" -ForegroundColor Yellow
Write-Host "  Start:   Start-Service -Name `"$serviceName`"" -ForegroundColor White
Write-Host "  Stop:    Stop-Service -Name `"$serviceName`"" -ForegroundColor White
Write-Host "  Status:  Get-Service -Name `"$serviceName`"" -ForegroundColor White
Write-Host "  Remove:  sc.exe delete `"$serviceName`"" -ForegroundColor White
Write-Host "  Logs:    Get-Content `"$InstallPath\LOGS\*.LOG`" -Tail 50" -ForegroundColor White
Write-Host ""

