# Проверка защо API-то не се стартира

## Стъпка 1: Проверка на connection string

Провери дали connection string е правилен в `appsettings.json` или `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ADS_WindowsAuth;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

## Стъпка 2: Проверка дали базата данни съществува

В SQL Server Management Studio:

```sql
SELECT name FROM sys.databases WHERE name = 'ADS_WindowsAuth';
```

Ако не съществува, изпълни SQL скрипта:
`ADS.WindowsAuth.API/Data/Scripts/CreateDatabase_SQLServer.sql`

## Стъпка 3: Проверка на логовете

Логовете са в: `ADS.WindowsAuth.API/bin/Debug/net8.0/LOGS/` или `ADS.WindowsAuth.API/bin/Release/net8.0/LOGS/`

Провери за грешки:
```powershell
Get-Content ADS.WindowsAuth.API\bin\Debug\net8.0\LOGS\*.LOG | Select-String -Pattern "ERROR|Exception|Failed" | Select-Object -Last 20
```

## Стъпка 4: Стартиране с подробни логове

```bash
dotnet run --project ADS.WindowsAuth.API --verbosity detailed
```

## Стъпка 5: Проверка на порта

Провери дали портът не е зает:

```powershell
netstat -ano | Select-String ":5000"
```

Ако е зает, промени порта в `launchSettings.json` или `appsettings.json`.

## Стъпка 6: Проверка на IIS (ако се използва)

Ако API-то е публикувано в IIS, провери:
- Application Pool е стартиран
- Правилният .NET runtime е инсталиран
- Web.config е правилно конфигуриран

## Най-чести проблеми:

1. **Базата данни не съществува** - Изпълни SQL скрипта
2. **Connection string е неправилен** - Провери appsettings.json
3. **SQL Server не е достъпен** - Провери дали SQL Server работи
4. **Портът е зает** - Промени порта
5. **Missing dependencies** - Изпълни `dotnet restore`

