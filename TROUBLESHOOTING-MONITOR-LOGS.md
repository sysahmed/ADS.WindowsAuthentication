# Защо не се логват неща от Monitor

## Кратък преглед на потока

| Компонент | Къде отива | Източник |
|-----------|------------|----------|
| **LoggerService** (логове от Monitor) | `POST /api/logs/upload` → таблица `LogEntries` | Monitor Service |
| **InputCapture** (клавиши и кликове) | `POST /api/logs/input` → таблица `InputLogs` | Monitor **или** Client |
| **Activity** (старт/стоп приложения, screen time) | `/api/activity/*` | Monitor Service |

---

## ⚠️ Важно: Session 0 ограничение

**Monitor** работи като **Windows Service** в **Session 0**. Това означава:

- ✅ **LoggerService** – изпращане към API **работи** (HTTP заявките минават)
- ✅ **Activity** (процеси, screen time, login events) – **работи**
- ❌ **InputCapture** (клавиши/кликове) – **НЕ получава** вход от потребителската сесия в Session 0

**InputCapture** (клавиши и кликове) от **Monitor** **не ще работи** – това е ограничение на Windows. За реални данни трябва да работи **ADS Client** в сесията на потребителя – той използва същия InputCapture и изпраща към `/api/logs/input`.

---

## Проверки за LoggerService (системни логове в портала)

### 1. ServiceUrl е конфигуриран

Monitor трябва да има `ServiceUrl` в:
- `appsettings.json`: `ServiceConfiguration:ServiceUrl`
- **или** Registry: `HKLM\SOFTWARE\ADS\WindowsAuth\ServiceUrl`

**Проверка:** В `C:\ADS\Monitor\LOGS\` проверете:
- `CONFIG_YYYYMMDD.LOG` – дали пише: *"LoggerService инициализиран с API URL: https://..."* или *"БЕЗ API URL"*
- `CONFIG_STARTUP_YYYYMMDD.LOG` – `Final API URL for LoggerService:` – дали е NULL или има стойност

### 2. appsettings.json на правилното място

Monitor работи от `C:\ADS\Monitor` (или зададената InstallPath при инсталация). Трябва да има:
```
C:\ADS\Monitor\appsettings.json
C:\ADS\Monitor\ADS.WindowsAuth.Monitor.exe
C:\ADS\Monitor\LOGS\
```

**Проверка:**
```powershell
Get-Content "C:\ADS\Monitor\appsettings.json" | Select-String "ServiceUrl"
```

### 3. Мрежова връзка Monitor → API

Monitor трябва да достига до `https://ads-auth.nursanbulgaria.com` (или зададения URL).

**Проверка:** На машината с Monitor:
```powershell
Invoke-WebRequest -Uri "https://ads-auth.nursanbulgaria.com/api/logs/db-check" -Method GET -UseBasicParsing
```

### 4. Грешки при изпращане

LoggerService при неуспех записва в `API_ERROR_YYYYMMDD.LOG`:
```
C:\ADS\Monitor\LOGS\API_ERROR_*.LOG
```

Ако има съдържание – прочетете съобщението за грешка.

### 5. API и база данни

- **Connection string:** `appsettings.Production.json` на API сървъра – `ConnectionStrings:DefaultConnection`
- **Таблица LogEntries:** API я създава при старт. Проверете: https://ads-auth.nursanbulgaria.com/api/logs/db-check

---

## Проверки за InputCapture (клавиши и кликове)

1. **Client трябва да е стартиран** в сесията на потребителя – Monitor **не** може да прихваща клавиши/кликове в Session 0.
2. **Client** стартира InputCapture при `Form_Load` и изпраща към `ApiConfiguration:BaseUrl` + `/api/logs/input`.
3. Проверете в `C:\ADS\Client\appsettings.json` наличието на `ApiConfiguration:BaseUrl`.

---

## Бързо тестване

1. **Тест за API/база:** отворете  
   https://ads-auth.nursanbulgaria.com/api/logs/test-add  
   След това проверете „Системни логове“ – трябва да има нов запис.

2. **Статистики InputLogs:**  
   https://ads-auth.nursanbulgaria.com/api/logs/input-stats  

3. **Рестартиране на Monitor:**
   ```powershell
   Restart-Service -Name "ADS.WindowsAuth.Monitor"
   Get-Content "C:\ADS\Monitor\LOGS\NURSAN*.LOG" -Tail 30
   ```

---

## Резюме: какво да проверите

| Проблем | Какво да направите |
|---------|---------------------|
| Няма системни логове в портала | 1) ServiceUrl в Monitor 2) Конфиг в C:\ADS\Monitor 3) API_ERROR_*.LOG за грешки |
| Няма клавиши/кликове | Client да работи в потребителска сесия; Monitor не ги прихваща |
| Няма машини в списъка | Monitor да е изпращал поне един лог; проверете db-check |
