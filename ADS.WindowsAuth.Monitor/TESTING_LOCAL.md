# Тестване на Monitor Service локално

## Стъпка 1: Стартирай API-то

Отвори терминал и стартирай API-то:

```bash
dotnet run --project ADS.WindowsAuth.API
```

API-то ще стартира на `http://localhost:5000` (или портът който е конфигуриран).

## Стъпка 2: Обнови конфигурацията на Monitor Service

За локално тестване, обнови `appsettings.Development.json` или `appsettings.json`:

```json
{
  "ServiceConfiguration": {
    "ServiceUrl": "http://localhost:5000",
    ...
  }
}
```

## Стъпка 3: Стартирай Monitor Service

Отвори нов терминал и стартирай Monitor Service:

```bash
dotnet run --project ADS.WindowsAuth.Monitor
```

Или ако е инсталиран като Windows Service:

```powershell
# Проверка дали е инсталиран
Get-Service | Where-Object {$_.Name -like "*Monitor*"}

# Ако не е инсталиран, инсталирай го
sc.exe create ADS.WindowsAuth.Monitor binPath="C:\Path\To\ADS.WindowsAuth.Monitor.exe"

# Стартирай го
sc.exe start ADS.WindowsAuth.Monitor
```

## Стъпка 4: Проверка на данните

След като Monitor Service започне да изпраща данни, провери в SQL Server:

```sql
USE ADS_WindowsAuth;

-- Проверка на приложения
SELECT TOP 10 * FROM ApplicationEvents ORDER BY EventTime DESC;

-- Проверка на файлове
SELECT TOP 10 * FROM FileActivities ORDER BY EventTime DESC;

-- Проверка на системна информация
SELECT TOP 10 * FROM SystemInfos ORDER BY EventTime DESC;

-- Проверка на USB устройства
SELECT TOP 10 * FROM UsbDevices ORDER BY EventTime DESC;

-- Проверка на потребителски активности
SELECT TOP 10 * FROM UserActivities ORDER BY StartTime DESC;
```

## Стъпка 5: Проверка на логовете

API логове: `ADS.WindowsAuth.API/bin/Debug/net8.0/LOGS/`
Monitor логове: Провери Event Viewer или лог файловете на Monitor Service

## Важно

- Уверете се че API-то е стартирано преди Monitor Service
- Проверь connection string в API appsettings.json
- Ако има грешки, провери логовете и дали базата данни е достъпна

