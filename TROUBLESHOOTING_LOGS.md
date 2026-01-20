# 🔍 Диагностика на проблеми с логове

## Проблем: Не получавам логове от Monitor Service

### Стъпка 1: Проверка на конфигурацията

1. **Провери `appsettings.json` на Monitor Service:**
   ```json
   {
     "ServiceConfiguration": {
       "ServiceUrl": "https://ads-auth.nursanbulgaria.com"
     }
   }
   ```

2. **Провери дали ServiceUrl е правилно зададен:**
   - Отвори `C:\ADS\Monitor\appsettings.json` (или където е инсталиран Monitor Service)
   - Провери дали `ServiceUrl` е правилно зададен

### Стъпка 2: Проверка на логовете на Monitor Service

1. **Провери локалните логове:**
   - Отвори `C:\ADS\Monitor\LOGS\` (или `C:\ADS\Logs\`)
   - Търси файлове `NURSAN*.LOG` и `API_ERROR_*.LOG`
   - Провери дали има грешки при изпращане към API

2. **Провери конфигурационния лог:**
   - Търси `CONFIG_*.LOG` файл
   - Провери дали API URL е правилно конфигуриран

### Стъпка 3: Тест на връзката

Изпълни тестовия скрипт:
```powershell
cd D:\Repo\ADS-WIndowsAutentications\ADS.WindowsAuth.Monitor
.\Test-Logging.ps1
```

Това ще:
- Изпрати тестов лог към API
- Покаже дали има грешки
- Провери дали логовете се получават

### Стъпка 4: Проверка на API endpoint

1. **Провери дали endpoint работи:**
   - Отвори: `https://ads-auth.nursanbulgaria.com/api/logs/upload`
   - Трябва да върне 405 Method Not Allowed (защото е POST endpoint)

2. **Тест с Postman/curl:**
   ```bash
   curl -X POST https://ads-auth.nursanbulgaria.com/api/logs/upload \
     -H "Content-Type: application/json" \
     -d '{
       "MachineName": "TEST-MACHINE",
       "Username": "test",
       "Domain": "test",
       "Level": "INFO",
       "Message": "Test log",
       "Source": "Test"
     }'
   ```

### Стъпка 5: Проверка на базата данни

1. **Провери дали таблицата съществува:**
   - Провери дали `LogEntries` таблицата съществува в базата данни
   - Ако не съществува, създай миграция:
     ```bash
     dotnet ef migrations add AddLogEntries
     dotnet ef database update
     ```

2. **Провери connection string:**
   - В `appsettings.json` на API-то
   - Провери дали `DefaultConnection` е правилно зададен

### Стъпка 6: Проверка на Monitor Service статус

1. **Провери дали сервисът работи:**
   ```powershell
   Get-Service -Name "ADS.WindowsAuth.Monitor"
   ```

2. **Провери логовете на Windows Event Viewer:**
   - Отвори Event Viewer
   - Търси в "Windows Logs" -> "Application"
   - Търси грешки от "ADS.WindowsAuth.Monitor"

### Често срещани проблеми:

1. **API URL не е конфигуриран:**
   - Решение: Добави `ServiceUrl` в `appsettings.json`

2. **Връзката с API не работи:**
   - Решение: Провери firewall, VPN, мрежовата връзка

3. **Базата данни не е достъпна:**
   - Решение: Провери connection string и дали базата данни работи

4. **Миграциите не са приложени:**
   - Решение: Създай и приложи миграциите за `LogEntries` таблицата

### Как да видиш логовете:

1. **Веб интерфейс:**
   - Отвори: `https://ads-auth.nursanbulgaria.com/system-logs`
   - Филтрирай по машина

2. **API endpoint:**
   - `GET /api/logs/database/machine/{machineName}`
   - Връща логове за конкретна машина

3. **Локални файлове:**
   - `C:\ADS\Monitor\LOGS\NURSAN*.LOG` - основни логове
   - `C:\ADS\Monitor\LOGS\API_ERROR_*.LOG` - грешки при изпращане към API
   - `C:\ADS\Monitor\LOGS\CONFIG_*.LOG` - конфигурационна информация

