# ADS Windows Auth Client – Инсталатор

## Изграждане на инсталатора

### Препоръчан начин (от корена на решение)

```powershell
cd D:\Repo\ADS-WIndowsAutentications
.\Build-All.ps1
```

Скриптът `Build-All.ps1` в корена на решение:
- Build-ва Core, Monitor, Client
- Използва Inno Setup за `.exe` инсталатор
- Резултат: `ADS.WindowsAuth.Client\InstallerOutput\ADS-Windows-Auth-Client-Setup.exe`

Опции:
- `.\Build-All.ps1 -ClientOnly` — пропуска API и Service
- `.\Build-All.ps1 -SkipInstaller` — само build, без Inno Setup

### Ръчен build

1. Инсталирай **Inno Setup** от https://jrsoftware.org/isinfo.php
2. Build на Client (копира Monitor автоматично):

```powershell
dotnet build ADS.WindowsAuth.Core\ADS.WindowsAuth.Core.csproj -c Release
dotnet build ADS.WindowsAuth.Monitor\ADS.WindowsAuth.Monitor.csproj -c Release
dotnet build ADS.WindowsAuth.Client\ADS.WindowsAuth.Client.csproj -c Release
```

3. Компилирай с Inno Setup:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "ADS.WindowsAuth.Client\Installer\ADS-Windows-Auth-Client.iss"
```

## Какво инсталира

- Копира файловете в `C:\Program Files\ADS.WindowsAuth.Client\`
- Създава shortcuts в Start Menu
- По избор: икона на работния плот
- Позволява деинсталация от Програми и функции
