# Monitor Service - Инсталация и Свързване

## Как работи Monitor Service

Monitor Service е Windows Service който:
1. **Мониторира активността** на машината (процеси, screen time, USB, файлове)
2. **Изпраща данни** към API сървъра (`ServiceUrl` от `appsettings.json`)
3. **Работи във фонов режим** като Windows Service

## Стъпки за инсталация

### Стъпка 1: Компилиране на Monitor Service

```cmd
cd ADS.WindowsAuth.Monitor
dotnet build --configuration Release
```

EXE файлът ще се намери в:
```
ADS.WindowsAuth.Monitor\bin\Release\net8.0-windows\ADS.WindowsAuth.Monitor.exe
```

### Стъпка 2: Подготовка на файлове

1. Създай директория: `C:\ADS\Monitor`
2. Копирай всички файлове от `bin\Release\net8.0-windows\` в `C:\ADS\Monitor\`:
   - `ADS.WindowsAuth.Monitor.exe`
   - Всички `.dll` файлове
   - `appsettings.json`
   - `appsettings.Development.json` (ако има)

### Стъпка 3: Конфигуриране на appsettings.json

Отвори `C:\ADS\Monitor\appsettings.json` и провери:

```json
{
  "ServiceConfiguration": {
    "ServiceUrl": "https://ads-auth.nursanbulgaria.com",
    "ApiKey": "",
    "MachineId": "",
    "RequireVpn": false,
    "ConnectionTimeout": 30
  }
}
```

**Важно:** `ServiceUrl` трябва да сочи към твоя API сървър!

### Стъпка 4: Регистрация като Windows Service

**Вариант А: Използвай CMD скрипта**
```cmd
REM Стартирай като Администратор
install-service.cmd
```

**Вариант Б: Ръчна регистрация**
```cmd
REM Отвори CMD като Администратор
sc.exe create "ADS.WindowsAuth.Monitor" binPath= "C:\ADS\Monitor\ADS.WindowsAuth.Monitor.exe" start= auto DisplayName= "ADS Windows Authentication Monitor"
```

### Стъпка 5: Стартиране на сервиза

```cmd
sc.exe start "ADS.WindowsAuth.Monitor"
```

## Как се свързва към API

### 1. При стартиране

Monitor Service:
1. Зарежда конфигурацията от `appsettings.json`
2. Създава `HttpClient` с `BaseAddress = ServiceUrl`
3. Проверява VPN връзка (ако е изисквана)
4. Проверява връзката към API сървъра

### 2. Изпращане на данни

Monitor Service изпраща данни към следните endpoints:

```
POST {ServiceUrl}/api/activity/start
POST {ServiceUrl}/api/activity/application/start
POST {ServiceUrl}/api/activity/application/stop
POST {ServiceUrl}/api/activity/screentime/update
POST {ServiceUrl}/api/activity/network
POST {ServiceUrl}/api/activity/system
POST {ServiceUrl}/api/activity/usb
POST {ServiceUrl}/api/activity/files
POST {ServiceUrl}/api/activity/stop
```

### 3. Интервали на изпращане

- **Процеси:** На всеки 5 секунди
- **Screen Time:** На всяка минута
- **Мрежова активност:** На всяка минута
- **Системна информация:** На всеки 5 минути
- **USB устройства:** На всеки 30 секунди
- **Файлова активност:** На всеки 2 минути

## Проверка дали работи

### 1. Проверка на статус на сервиза

```cmd
sc.exe query "ADS.WindowsAuth.Monitor"
```

**Очакван изход:**
```
SERVICE_NAME: ADS.WindowsAuth.Monitor
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 4  RUNNING
        ...
```

### 2. Проверка на логове

Логовете се намират в:
```
C:\ADS\Monitor\LOGS\NURSAN*.LOG
```

**Проверка на последните логове:**
```cmd
type "C:\ADS\Monitor\LOGS\NURSAN*.LOG" | more
```

**Или в PowerShell:**
```powershell
Get-Content "C:\ADS\Monitor\LOGS\NURSAN*.LOG" -Tail 50
```

### 3. Проверка на Event Viewer

1. Отвори `Event Viewer` (eventvwr.msc)
2. Отиди на `Windows Logs` → `Application`
3. Търси за "ADS.WindowsAuth.Monitor"

### 4. Проверка на API заявки

Провери дали API сървърът получава заявки:
- Отвори логовете на API сървъра
- Търси за `/api/activity/start` заявки

## Често срещани проблеми

### Проблем 1: "Access Denied" при инсталация

**Решение:**
- Увери се че изпълняваш командите като **Администратор**
- Провери дали имаш права за инсталиране на Windows Services

### Проблем 2: Сервизът не се стартира

**Проверки:**
1. Провери дали EXE файлът съществува на правилния път
2. Провери дали всички DLL файлове са копирани
3. Провери логовете в `C:\ADS\Monitor\LOGS\`
4. Провери Event Viewer за грешки

**Команда за проверка:**
```cmd
sc.exe query "ADS.WindowsAuth.Monitor"
```

### Проблем 3: "ServiceUrl не е достъпен"

**Проверки:**
1. Провери дали API сървърът работи
2. Провери дали `ServiceUrl` в `appsettings.json` е правилен
3. Провери дали има мрежова връзка
4. Провери дали firewall не блокира заявките

**Тест на връзката:**
```cmd
curl https://ads-auth.nursanbulgaria.com/health
```

### Проблем 4: "Cannot find appsettings.json"

**Решение:**
- Увери се че `appsettings.json` е в същата папка като EXE файла
- Път: `C:\ADS\Monitor\appsettings.json`

### Проблем 5: Сервизът спира веднага след стартиране

**Проверки:**
1. Провери логовете за грешки
2. Провери дали всички зависимости са налични
3. Провери дали .NET 8 Runtime е инсталиран

**Команда за проверка на .NET:**
```cmd
dotnet --version
```

## Деинсталация

### Стъпка 1: Спиране на сервиза

```cmd
sc.exe stop "ADS.WindowsAuth.Monitor"
```

### Стъпка 2: Премахване на сервиза

```cmd
sc.exe delete "ADS.WindowsAuth.Monitor"
```

### Стъпка 3: Изтриване на файлове (опционално)

```cmd
rmdir /s "C:\ADS\Monitor"
```

## Полезни команди

### Стартиране
```cmd
sc.exe start "ADS.WindowsAuth.Monitor"
```

### Спиране
```cmd
sc.exe stop "ADS.WindowsAuth.Monitor"
```

### Рестартиране
```cmd
sc.exe stop "ADS.WindowsAuth.Monitor"
sc.exe start "ADS.WindowsAuth.Monitor"
```

### Проверка на статус
```cmd
sc.exe query "ADS.WindowsAuth.Monitor"
```

### Преглед на логове
```cmd
type "C:\ADS\Monitor\LOGS\NURSAN*.LOG"
```

## Конфигурация

### appsettings.json структура

```json
{
  "ServiceConfiguration": {
    "ServiceUrl": "https://ads-auth.nursanbulgaria.com",  // API URL
    "ApiKey": "",                                          // Опционален API ключ
    "MachineId": "",                                       // Уникален ID на машината
    "RequireVpn": false,                                   // Дали изисква VPN
    "VpnCheckInterval": 300,                              // Интервал за VPN проверка (секунди)
    "VpnGateways": [],                                    // VPN gateway адреси
    "VpnProcessNames": ["FortiClient", "rasdial"],       // VPN процеси
    "OfflineMode": false,                                 // Дали работи offline
    "OfflineDataRetention": 7,                            // Дни за съхранение на offline данни
    "ConnectionTimeout": 30,                              // Timeout за връзка (секунди)
    "RetryInterval": 60,                                  // Интервал между опити (секунди)
    "MaxRetries": 3                                       // Максимален брой опити
  }
}
```

## Следващи стъпки

След успешна инсталация:
1. Провери логовете за потвърждение на свързване
2. Провери API сървъра за получаване на данни
3. Провери дали активността се записва правилно

