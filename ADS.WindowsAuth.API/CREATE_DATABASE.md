# Инструкции за създаване на MySQL база данни

## Стъпка 1: Проверка на MySQL

Провери дали MySQL е инсталиран и работи:

```bash
mysql --version
```

Ако не е инсталиран, изтегли от: https://dev.mysql.com/downloads/mysql/

## Стъпка 2: Създаване на базата данни

### Вариант А: Използвай SQL скрипта

Отвори MySQL Command Line Client или MySQL Workbench и изпълни:

```bash
mysql -u root -p < ADS.WindowsAuth.API/Data/Scripts/CreateDatabase.sql
```

Или отвори MySQL Workbench и изпълни съдържанието на файла `CreateDatabase.sql`.

### Вариант Б: Ръчно създаване

```sql
CREATE DATABASE IF NOT EXISTS ADS_WindowsAuth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

След това изпълни останалите команди от `CreateDatabase.sql`.

## Стъпка 3: Проверка на connection string

Провери `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ADS_WindowsAuth;User=root;Password=ТВОЯТА_ПАРОЛА;Port=3306;CharSet=utf8mb4;"
  }
}
```

**Важно:** Замени `ТВОЯТА_ПАРОЛА` с реалната MySQL root парола!

## Стъпка 4: Проверка на базата данни

След създаване, провери дали таблиците са създадени:

```sql
USE ADS_WindowsAuth;
SHOW TABLES;
```

Трябва да видиш 13 таблици:
- UserActivities
- ApplicationEvents
- FileActivities
- NetworkActivities
- SystemInfos
- UsbDevices
- ScreenTimes
- AuthSessions
- Policies
- AdUsers
- AdGroups
- AdUserGroups
- WindowsEvents

## Стъпка 5: Стартиране на API-то

След като базата данни е създадена, стартирай API-то:

```bash
dotnet run --project ADS.WindowsAuth.API
```

API-то ще се опита автоматично да приложи миграциите при стартиране (ако има такива).

## Логове

Логовете се записват в:
- `ADS.WindowsAuth.API/bin/Debug/net8.0/LOGS/` (при разработка)
- `ADS.WindowsAuth.API/LOGS/` (при production)

Файловете се именуват като `MACHINENAME_YYYYMMDD.LOG`.

