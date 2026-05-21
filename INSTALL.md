# ADS Windows Authentication – Инсталация и deployment

## Компоненти

| Компонент | Описание | Къде се инсталира |
|-----------|----------|-------------------|
| **Client** | WinForms приложение – QR код вход | Клиентски PC |
| **Monitor** | Windows Service – мониторинг | Клиентски PC |
| **Service** | Windows Service – Remote Desktop и др. | Сервер / клиентски PC |
| **API** | ASP.NET Core Web API | Сервер (IIS / Kestrel) |
| **CredentialProvider** | C++ DLL за login екран | Клиентски PC |

## Клиентска инсталация (Client + Monitor)

### Вариант 1: Инсталатор (препоръчително)

1. **Build на solution** (Visual Studio или):
   ```powershell
   .\Build-All.ps1
   ```
2. **Инсталатор**: `ADS.WindowsAuth.Client\InstallerOutput\ADS-Windows-Auth-Client-Setup.exe`
3. Стартирай като администратор, изберете директория и икони
4. След инсталация – от **Client** използвайте „Инсталирай Credential Provider“ и „Инсталирай Monitor Service“

### Вариант 2: Ръчна копиране

1. Build на solution в **Release**
2. Копирай всичко от `ADS.WindowsAuth.Client\bin\Release\net8.0-windows8.0\` в целева папка
3. Стартирай `ADS.WindowsAuth.Client.exe` като администратор
4. От приложението: Credential Provider и Monitor Service

### Вариант 3: PowerShell скрипт (от API)

```
Invoke-WebRequest -Uri "https://ads-auth.nursanbulgaria.com/download/installer" -OutFile "Install-ADS.ps1"
.\Install-ADS.ps1
```

## API deployment (сървър)

1. Publish:
   ```powershell
   dotnet publish ADS.WindowsAuth.API -c Release -o ./publish
   ```
2. Копирай `./publish` на сървъра
3. Настрой `appsettings.json` (ConnectionStrings, JWT, и др.)
4. Стартирай като Windows Service или в IIS

## Service (ADS.WindowsAuth.Service)

За Remote Desktop и други услуги:

```powershell
dotnet publish ADS.WindowsAuth.Service -c Release -o ./ServicePublish
sc create "ADS.WindowsAuth.Service" binPath="C:\Path\To\ADS.WindowsAuth.Service.exe"
sc start "ADS.WindowsAuth.Service"
```

## Изисквания

- Windows 10/11 x64
- .NET 8 Runtime (за Client/Monitor/Service)
- .NET 8 SDK (само за build)
- Inno Setup 6 (само за създаване на инсталатор)
- Visual Studio с C++ workload (само за CredentialProvider)
