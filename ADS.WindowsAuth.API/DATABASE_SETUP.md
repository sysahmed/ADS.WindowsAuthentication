# Инструкции за настройка на MySQL база данни

## Стъпка 1: Инсталация на MySQL

Ако нямаш MySQL инсталиран:
1. Изтегли MySQL от https://dev.mysql.com/downloads/mysql/
2. Инсталирай MySQL Server
3. Запомни root паролата

## Стъпка 2: Създаване на базата данни

Отвори MySQL Command Line Client или MySQL Workbench и изпълни:

```sql
CREATE DATABASE ADS_WindowsAuth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

## Стъпка 3: Конфигуриране на Connection String

Обнови `appsettings.json` или `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ADS_WindowsAuth;User=root;Password=ТВОЯТА_ПАРОЛА;Port=3306;CharSet=utf8mb4;"
  }
}
```

## Стъпка 4: Създаване на миграции

Отвори терминал в папката на ADS.WindowsAuth.API проекта и изпълни:

```bash
dotnet ef migrations add InitialCreate --project ../ADS.WindowsAuth.Core
```

## Стъпка 5: Прилагане на миграциите

```bash
dotnet ef database update --project ../ADS.WindowsAuth.Core
```

Или миграциите ще се приложат автоматично при стартиране на API-то (ако е конфигурирано в Program.cs).

## Стъпка 6: Проверка

След стартиране на API-то, проверь дали таблиците са създадени:

```sql
USE ADS_WindowsAuth;
SHOW TABLES;
```

Трябва да видиш:
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

