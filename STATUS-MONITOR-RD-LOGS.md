# Състояние: Monitor, Remote Desktop и Логове

## Какво искаш (цел)

1. **При инсталация** – DLL + Monitor на машината да се свързват директно към API
2. **Логове** – Monitor да изпраща информация за логове към API
3. **Преглед** – Да виждаш компютрите в системата: колко са свързани, колко не

---

## Какво вече работи

### Monitor → API връзка
- Monitor изпраща `/api/activity/start` при стартиране (username, domain, machine)
- Monitor изпраща `/api/activity/login` при login събития
- Monitor изпраща application start/stop, screentime, files, usb, network
- Offline buffering – когато API е down, събитията се пазят локално и се изпращат при възстановяване

### Логове
- **LoggerService** (в Monitor) изпраща логове към `/api/logs/upload` (MachineName, Username, Level, Message, Timestamp)
- API записва логовете в БД (LogEntries таблица)
- API endpoint: `/api/logs/database/machine/{machineName}` – връща логове от БД

### API web UI – Monitor страница
- **`/monitor`** – показва машини с:
  - Общо машини / Активни сега / Офлайн
  - Карточки: машина, потребител, last seen, онлайн/офлайн
- **`/monitor-settings`** – настройки за всяка машина (ServiceUrl, ApiKey, Offline mode и т.н.)
- **`/logs`** – избор на машина и преглед на активност (но НЕ на логове от БД)

### Remote Desktop
- **API**: `/remotedesktop`, `/remotedesktop/connect`, viewer със SignalR hub
- **Service**: Отделен Windows Service с RemoteDesktopHub
- Модели: RemoteDesktopSession, CreateSession, RegisterHost, RegisterViewer
- Host трябва да стартира компонент на клиента за streaming (screen capture)

### Инсталация
- **Client installer** – Client + Monitor EXE в една папка
- **Monitor appsettings.json** – ServiceUrl по подразбиране: `https://ads-auth.nursanbulgaria.com`
- При инсталация може да се промени ServiceUrl в appsettings или чрез MonitorSettings в API

---

## Какво липсва / проблеми

### 1. Monitor изглед – данни само в памет
- **Проблем**: `/monitor` чете от `ActivityMonitorService` (in-memory)
- При рестарт на API всички машини изчезват от списъка
- **Решение**: Да се показват машини от БД (UserActivities + LogEntries + MonitorConfigurations)

### 2. "Свързан" vs "Офлайн"
- **Текущо**: `IsActive` = сесия без `EndTime` (логически активна сесия)
- **Липсва**: Ясен критерий "жив" vs "мъртъв" – напр. last activity/heartbeat през последните 5–10 минути
- **Решение**: Heartbeat endpoint – Monitor да изпраща периодично ping (напр. на 1–2 мин) или да се използва LastSeen от логове/activity

### 3. Преглед на логове (LogEntries) в UI
- **Текущо**: `/logs` показва активност (applications, screen time), не логове от БД
- **Има**: `/api/logs/database/machine/{machineName}` – връща логове
- **Решение**: Добавяне на tab или страница за преглед на логове от БД по машина

### 4. Списък машини при празен ActivityMonitor
- Ако API е рестартирал или все още никой не е изпратил activity, списъкът е празен
- **Решение**: Да се включват машини от LogEntries (последни логове) и MonitorConfigurations

### 5. Remote Desktop – host на клиента
- Има API + Service + Viewer, но host компонентът (screen capture + SignalR client на клиентската машина) трябва да се стартира от Monitor или отделен agent

---

## Препоръчани стъпки (по приоритет)

### A. Monitor да показва машини от БД (вместо само in-memory)
1. Нови/променени API endpoints или логика в HomeController.Monitor:
   - Машини от UserActivities (последна активност)
   - + машини от LogEntries (последни логове)
   - + машини от MonitorConfigurations
2. Критерий "онлайн": LastSeen / LastLog < 10 минути
3. Критерий "офлайн": LastSeen / LastLog > 10 минути

### B. Heartbeat от Monitor
1. Monitor да изпраща периодично (напр. на 1–2 мин) `/api/activity/heartbeat` с `{ MachineName, Username, Domain }`
2. API да записва timestamp в БД или in-memory
3. Monitor view да определя онлайн/офлайн по heartbeat timestamp

### C. Логове в UI
1. В `/logs` или нова страница: dropdown за машина + таблица с логове от `/api/logs/database/machine/{name}`
2. Филтри по Level (INFO, WARNING, ERROR), дата

### D. Конфигурация при първа инсталация
1. Client/Monitor при първо стартиране – да пита за API URL (или да чете от appsettings)
2. Monitor-settings в API – вече има, може да се достъпи от админ

---

## Обобщение

| Функция                    | Статус   | Бележки                                              |
|---------------------------|----------|------------------------------------------------------|
| Monitor → API връзка      | ✓ Готово | ServiceUrl в appsettings, offline buffering          |
| Изпращане на логове       | ✓ Готово | LoggerService → /api/logs/upload → LogEntries        |
| Преглед машини (/monitor)  | ⚠ Частично | In-memory, изчезва при рестарт                       |
| Онлайн / Офлайн брой      | ⚠ Частично | По сесии, не по реален heartbeat                     |
| Логове в UI               | ⚠ Частично | API има, UI показва активност, не DB логове          |
| Remote Desktop            | ⚠ Частично | API + Service + Viewer готови, host трябва agent    |
