# ADS Windows Auth – План за действие

## Текущо състояние

| Компонент | Работи? | Проблем |
|-----------|---------|---------|
| **Credential Provider (QR/barcode)** | ✅ Да | Влизане с QR код |
| **Monitor service** | ⚠️ Частично | Потокът е готов, но има проблеми |
| **Логове към API** | ❌ Не | Не стигат до API или не се виждат |
| **Monitor view (машини)** | ❌ Не | Данни само в памет, изчезват при рестарт |
| **Политики** | ❌ Не | Не ограничават приложения/сайтове |
| **Remote Desktop** | ❌ Не | Липсва host на клиента |

---

## Корен на проблемите

### 1. Monitor като сервис използва SYSTEM като потребител
- Monitor работи като **Windows Service** → `Environment.UserName` = **"SYSTEM"**
- Всички събития (applications, screen time, файлове) се изпращат с `Username = "SYSTEM"`
- Политиките се проверяват за `(machineName, "SYSTEM")` → не съвпадат с реалния потребител
- **Фикс:** Да се взима активно логнат потребител чрез WMI (owner на explorer.exe)

### 2. Monitor не получава политики правилно
- `SyncPoliciesFromApi` тегли от `/api/Policy/machine/{machine}/user/{user}`
- Ако user = "SYSTEM", политиките за конкретни потребители не се връщат
- PolicyService в Monitor може да няма данни от API при стартиране
- **Фикс:** След фикс #1 ще се използва реален потребител

### 3. Логове – проблеми в веригата
- **LoggerService** изпраща към `/api/logs/upload`
- **Възможни проблеми:**
  - ServiceUrl не е конфигуриран
  - API не е достъпен
  - API използва MockDatabaseService (няма connection string)
  - LoginEvents: парсването е на английски, а при български Windows съобщенията са различно
- **Фикс:** Да се проверят конфигурация и права; да се добави езиково-независимо парсване

### 4. Monitor view – данни само в памет
- `/monitor` чете от `ActivityMonitorService` (in-memory)
- При рестарт на API данните се губят
- **Фикс:** Да се показват машини от БД (UserActivities, LogEntries, MonitorConfigurations)

### 5. Remote Desktop – липсва host
- API и Service имат viewer и hub
- Трябва **host агент** на клиентската машина: screen capture + SignalR → streaming
- **Фикс:** Да се добави host компонент в Monitor или отделно приложение

---

## План по фази

### ФАЗА 1: Monitor да използва реалния потребител

**Цел:** Вместо SYSTEM да се използва активно логнат потребител.

**Стъпки:**
1. Добавяне на `RefreshLoggedInUser()` – WMI, owner на explorer.exe
2. Периодично обновяване (напр. на 30 сек)
3. В payload-ите към API да се ползва `EffectiveUsername` вместо `_username`
4. В проверките за политики да се ползва същият потребител

**Резултат:** Application events, screen time и политики да се асоциират с реалния потребител.

---

### ФАЗА 2: Политики – зареждане и прилагане

**Цел:** Политиките да се зареждат от API и да се прилагат на Monitor.

**Стъпки:**
1. Да се провери, че Monitor извиква `/api/Policy/machine/{m}/user/{u}` с **реалния** потребител (след Фаза 1)
2. Да се провери Policy API – дали съществуват политики в БД
3. Да се потвърди, че PolicyService.ApplyPolicy() / IsApplicationBlocked() се извикват и резултатът се спазва (напр. Kill на блокирано приложение)
4. За блокиране на сайтове – MonitorAndFilterWebsites да използва политиките и hosts/firewall

**Резултат:** Блокиране на приложения и сайтове според политиките.

---

### ФАЗА 3: Логове – от клиента до UI

**Цел:** Логовете да стигат до API и да се виждат в UI.

**Стъпки:**
1. **Config:** `ServiceUrl` в Monitor appsettings да е правилен
2. **API:** Connection string за БД
3. **LoginEvents:** Езиково-независимо парсване (EventLogReader / XML) за Security 4624
4. **UI:** В `/logs` да се показват логове от `/api/logs/database/machine/{name}`

**Резултат:** Видими логове по машина в web UI.

---

### ФАЗА 4: Monitor view – машини от БД

**Цел:** /monitor да показва машини дори след рестарт на API.

**Стъпки:**
1. Monitor view да чете от UserActivities + LogEntries + MonitorConfigurations
2. "Онлайн" = last activity/log в последните ~10 минути
3. Да се премахне зависимостта само от in-memory ActivityMonitor

**Резултат:** Списък машини, който не се губи при рестарт.

---

### ФАЗА 5: Remote Desktop (по избор)

**Цел:** Remote Desktop да работи до края.

**Стъпки:**
1. Host компонент: screen capture + SignalR client на клиента
2. Интеграция в Monitor или отделен host agent
3. Съгласуване с API/Service hub за streaming

**Резултат:** Пълен remote desktop от браузър към клиент.

---

## Ред на изпълнение

1. **Фаза 1** – реалният потребител (критично за policies и данни)
2. **Фаза 3** – логове (диагностика и проверка на потока)
3. **Фаза 4** – monitor view от БД (стабилен списък машини)
4. **Фаза 2** – политики (след като потребителят е верен)
5. **Фаза 5** – Remote Desktop (когато базовите неща работят)

---

## Бърза проверка преди код

1. **Monitor – ServiceUrl:**
   - `ADS.WindowsAuth.Monitor\appsettings.json` → `ServiceConfiguration:ServiceUrl`
   - Трябва да е URL на API (напр. `https://ads-auth.nursanbulgaria.com`)

2. **API – Connection string:**
   - `ADS.WindowsAuth.API\appsettings.json` → `ConnectionStrings:DefaultConnection`
   - Трябва да сочи към SQL Server с база `ADS_WindowsAuth`

3. **Миграции:**
   - Таблиците `UserActivities`, `LogEntries`, `LoginEvents`, `Policies`, `MonitorConfigurations` да съществуват

4. **Monitor като сервис:**
   - Да е инсталиран (от Client или ръчно)
   - Да е стартиран и да работи под LocalSystem

5. **Политики в API:**
   - В API UI – да има създадени политики с TargetMachines/TargetUsers и BlockedApplications/BlockedWebsites

---

## Следваща стъпка

Препоръчва се да започнеш с **Фаза 1** – фикс за реалния потребител в Monitor, след което да продължиш с останалите фази в указания ред.
