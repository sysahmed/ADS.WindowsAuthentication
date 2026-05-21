# LoginEvents – защо не се записват

## Как работи потокът

1. **Monitor** (MonitorUserSessions) чете Windows Security Event Log за Event ID 4624 (успешен login)
2. Изпраща POST към `/api/activity/login` с Username, Domain, MachineName, LoginTime, LoginMethod, LogonType
3. **API** (ActivityController.RegisterUserLogin) получава заявката и записва в `LoginEvents` чрез `_databaseService.SaveLoginEventAsync()`

---

## Възможни причини за липсващи записи

### 1. Monitor няма права за четене на Security Event Log
- **Симптом:** SecurityException – "Access to the source 'Security' is denied"
- **Решение:** Monitor Service трябва да работи като **LocalSystem** или акаунт с права за четене на Security log  
- **Проверка:** Логове в `Monitor\LOGS\` – дали има "Няма права за четене на Security Event Log"

### 2. Windows е на български (или друг език)
- Monitor парсва съобщенията чрез `"Account Name:"`, `"Account Domain:"`, `"Logon Type:"`
- При български Windows текстът е различен (напр. "Име на акаунт:", "Домейн на акаунт:")
- Тогава `ExtractValueFromEventMessage()` връща празно и потребителят се пропуска (string.IsNullOrEmpty(username))
- **Решение:** Използване на EventLogReader и XML данни (TargetUserName, TargetDomainName и т.н.), независими от езика

### 3. API използва MockDatabaseService
- Ако `ApplicationDbContext` не се инициализира (грешен connection string, миграции), API ползва `MockDatabaseService`
- MockDatabaseService **не** записва в базата, само логира
- **Проверка:** При стартиране на API – дали има лог "MockDatabaseService" или "използвам MockDatabaseService"
- **Решение:** Правен connection string и миграции в `appsettings.json`

### 4. Никой не е логнал след стартиране на Monitor
- Monitor взима само събития с `TimeGenerated > lastCheck` – т.е. само **нови** login-и след пуснат сервис
- Ако никой не е влизал след инсталация, таблицата остава празна
- **Проверка:** Влез и излез от Windows и провери дали се появяват записи

### 5. Няма връзка Monitor → API
- Ако `ServiceUrl` в appsettings е празен или грешен, Monitor не изпраща събития
- **Проверка:** Логове в Monitor – "Login event изпратен към API" или "API URL не е конфигуриран"

---

## Как да провериш

1. **Monitor логове** (`ADS.WindowsAuth.Monitor\LOGS\` или `C:\ADS\Monitor\LOGS\`):
   - "User login засечен: X@Y" – означава, че Monitor е засекъл login
   - "Login event изпратен към API" – заявката е изпратена
   - "Няма права за четене" – проблем с права
   - "API URL не е конфигуриран" – липсва ServiceUrl

2. **API логове**:
   - "API: User login от X@Y на MachineName" – API е получил заявката

3. **База данни:**
   - Connection string в `appsettings.json` за API
   - Таблица `LoginEvents` съществува (миграции приложени)

4. **Български Windows:**
   - Ако в логовете се вижда "User login засечен" с празни username/domain – парсването вероятно не работи поради езика
