# Troubleshooting - Internal Server Error

## Проблем: "The page cannot be displayed because an internal server error has occurred"

### Production – къде е истинската грешка?

При деплой под IIS истинската причина се записва в:

1. **Stdout логове (IIS)**  
   В `web.config` е зададено: `stdoutLogFile=".\logs\stdout"`.  
   Файловете са в папката на приложението (където е `ADS.WindowsAuth.API.dll`), подпапка **`logs`**:  
   `logs\stdout_*.log` – проверете най-новия файл за `fail`, `Exception`, `Error`.

2. **Файлови логове на приложението**  
   В същата папка (или в `wwwroot` в зависимост от конфигурацията) търсете папка **`logs`** и файлове като `ads-windows-auth-*.log` – там се записват необработени грешки с пълен stack trace.

3. **Event Viewer**  
   Windows Logs → Application – за грешки от ASP.NET Core / IIS.

След последната промяна в кода, при необработена грешка в production вместо обобщеното съобщение от IIS трябва да се показва страница с текст „Грешка“ и „Проверете логовете“. Ако все още виждате само „The page cannot be displayed“, грешката вероятно е **при стартиране** на приложението (преди първата заявка) – тогава първо проверявайте stdout логовете и Event Viewer.

### Възможни причини:

1. **Проблем с базата данни**
   - Базата данни не е създадена
   - Connection string е неправилен
   - SQL Server не е достъпен

2. **Проблем с миграциите**
   - Опитва се да приложи миграции които не съществуват
   - Таблиците вече съществуват но има конфликт

3. **Проблем с dependency injection**
   - DbContext не може да се създаде
   - DatabaseService не може да се инстанцира

## Решение:

### Стъпка 1: Проверка на базата данни

```sql
USE ADS_WindowsAuth;
SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
```

Трябва да видиш 13 таблици.

### Стъпка 2: Проверка на connection string

В `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ADS_WindowsAuth;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

### Стъпка 3: Проверка на логовете

Логовете са в: `ADS.WindowsAuth.API/bin/Debug/net8.0/LOGS/`

Провери за грешки:
```powershell
Get-Content ADS.WindowsAuth.API\bin\Debug\net8.0\LOGS\*.LOG | Select-String -Pattern "ERROR|Exception" | Select-Object -Last 20
```

### Стъпка 4: Временно изключване на миграциите

Ако проблемът е с миграциите, коментирай кода в `Program.cs`:

```csharp
// Миграция на базата данни при стартиране
using (var scope = app.Services.CreateScope())
{
    // Коментирай този блок ако има проблеми
    /*
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    ...
    */
}
```

### Стъпка 5: Проверка на Swagger

Отвори: `http://localhost:5000/swagger`

Ако Swagger работи, проблемът е в конкретен endpoint.
Ако Swagger не работи, проблемът е в общата конфигурация.

### Стъпка 6: Тестване на Health Check

```bash
curl http://localhost:5000/health
```

Трябва да върне `Healthy`.

