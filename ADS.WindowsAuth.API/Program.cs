using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Configuration;
using ADS.WindowsAuth.API.Hubs;
using ADS.WindowsAuth.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Явно писанене в конзолата при старт (за да проверите дали конзолата работи)
try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ADS Windows Auth - стартиране...");
}
catch { }

// Configure Serilog
// ВАЖНО: използваме AppDomain.CurrentDomain.BaseDirectory (не ContentRootPath) защото:
//  - ContentRootPath може да е null по времето на UseSerilog callback под IIS
//  - SetCurrentDirectory се вика СЛЕД Serilog инициализацията → "./logs" = System32\logs\
var _appBaseDir = AppDomain.CurrentDomain.BaseDirectory;
var _logDir     = Path.Combine(_appBaseDir, "logs");
try { Directory.CreateDirectory(_logDir); } catch { }

// SelfLog – ако Serilog има вътрешна грешка (напр. permission denied), пише в отделен файл
Serilog.Debugging.SelfLog.Enable(msg =>
{
    try { File.AppendAllText(Path.Combine(_logDir, "serilog-selflog.txt"), $"{DateTime.Now:O} {msg}{Environment.NewLine}"); }
    catch { }
});

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);

    configuration.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);

    var logPath = Path.Combine(_logDir, "ads-windows-auth-.log");
    configuration.WriteTo.File(logPath,
        rollingInterval: Serilog.RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
});

// Add secure configuration sources
builder.Configuration.AddSecureConfiguration();

// Явно задаване на URL – ако launchSettings не се приложат
builder.WebHost.UseUrls("http://localhost:5001");

// Configure services
ConfigureServices(builder);

var app = builder.Build();

// При хостиране под IIS текущата директория може да е System32 – задаваме я на приложението, за да се пишат логовете в папката на сайта (logs/...).
try
{
    var root = app.Environment.ContentRootPath;
    if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
    {
        Directory.SetCurrentDirectory(root);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] CurrentDirectory = {Directory.GetCurrentDirectory()}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SetCurrentDirectory: {ex.Message}");
}

// Configure middleware
ConfigureMiddleware(app);

// Configure endpoints
ConfigureEndpoints(app);

// Инициализиране на базата данни (създаване на таблици ако не съществуват)
await InitializeDatabaseAsync(app);

// Инициализиране на Portal Identity DB (AspNetUsers)
await InitializePortalDbAsync(app);

// Зареждане на активни сесии от базата данни при стартиране
await LoadSessionsOnStartup(app);

// Зареждане на блокирани IP адреси от базата данни
await LoadBlockedIpsOnStartup(app);

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Приложение готово. Отвори http://localhost:5001");

// Run the application
app.Run();

// Метод за зареждане на сесии при стартиране
static async Task InitializeDatabaseAsync(WebApplication app)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetService<ILoggerService>();
            
            if (dbContext != null)
            {
                logger?.LogInfo("Инициализиране на базата данни...");
                
                try
                {
                    // Ако е SQL Server, прилагаме миграции
                    await dbContext.Database.MigrateAsync();
                    logger?.LogInfo("Миграции приложени успешно");
                }
                catch (Exception migrateEx)
                {
                    var msg = migrateEx.Message ?? "";
                    // Таблиците вече съществуват (напр. от предишен EnsureCreated) – синхронизираме историята на миграциите
                    if (msg.Contains("already an object named", StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("There is already an object", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            await EnsureMigrationHistorySyncedAsync(dbContext, logger);
                            logger?.LogInfo("Историята на миграциите е синхронизирана с съществуващата база.");
                        }
                        catch (Exception syncEx)
                        {
                            logger?.LogWarning($"Синхронизация на миграционна история: {syncEx.Message}. Опитвам EnsureCreated...");
                            await TryEnsureCreatedFallbackAsync(dbContext, logger);
                        }
                    }
                    else
                    {
                        logger?.LogWarning($"Не можах да приложа миграции: {msg}. Опитвам EnsureCreated...");
                        await TryEnsureCreatedFallbackAsync(dbContext, logger);
                    }
                }
                finally
                {
                    // Винаги гарантираме LogEntries (за логове от API и Monitor) – иначе нищо не се записва в базата
                    await EnsureLogEntriesTableAsync(dbContext, logger);
                    // Винаги проверяваме дали MonitorConfigurations съществува (за /monitor-settings)
                    await EnsureMonitorConfigurationsTableAsync(dbContext, logger);
                    // Гарантираме таблицата за блокирани IP адреси
                    await EnsureBlockedIpsTableAsync(dbContext, logger);
                    // Гарантираме таблицата за имейл активност
                    await EnsureEmailActivitiesTableAsync(dbContext, logger);
                    // Гарантираме новите колони за whitelist в Policies
                    await EnsurePolicyWhitelistColumnsAsync(dbContext, logger);
                    // Гарантираме InputLogs (клавиши/кликове от Client) – беше само в error path
                    await EnsureInputLogsTableAsync(dbContext, logger);
                    // Гарантираме WorkSessions (Work Logger)
                    await EnsureWorkSessionsTableAsync(dbContext, logger);
                }
            }
            else
            {
                logger?.LogWarning("ApplicationDbContext не е наличен - пропускаме инициализация на базата данни");
            }
        }
    }
    catch (Exception ex)
    {
        try
        {
            var logger = app.Services.GetService<ILoggerService>();
            logger?.LogError($"Грешка при инициализация на базата данни: {ex.Message}", ex);
        }
        catch
        {
            // Ако не можем да логнем, игнорираме
        }
    }
}

// Записва приложените миграции в __EFMigrationsHistory, когато таблиците вече съществуват
static async Task EnsureMigrationHistorySyncedAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    const string migrationId = "20260212103653_InitialCreate";
    const string productVersion = "8.0.22";

    // Създаваме таблицата за история ако липсва (SQL Server)
    await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        CREATE TABLE [dbo].[__EFMigrationsHistory] (
            [MigrationId] nvarchar(150) NOT NULL,
            [ProductVersion] nvarchar(32) NOT NULL,
            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
        );");

    // Добавяме запис за InitialCreate, ако още го няма (алиас Value за SqlQueryRaw<int>)
    var paramId = new SqlParameter("@mid", migrationId);
    var alreadyApplied = await dbContext.Database
        .SqlQueryRaw<int>("SELECT COUNT(1) AS [Value] FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = @mid", paramId)
        .FirstOrDefaultAsync();
    if (alreadyApplied == 0)
    {
        var paramMidInsert = new SqlParameter("@mid", migrationId);
        var paramVer = new SqlParameter("@ver", productVersion);
        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (@mid, @ver)",
            paramMidInsert,
            paramVer);
        logger?.LogInfo($"Записан в историята на миграциите: {migrationId}");
    }

    // Таблицата LogEntries може да липсва ако базата е създадена преди да е добавена – създаваме я ако няма
    await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID(N'dbo.LogEntries', N'U') IS NULL
        CREATE TABLE [dbo].[LogEntries] (
            [Id] int NOT NULL IDENTITY,
            [MachineName] nvarchar(255) NOT NULL,
            [Username] nvarchar(255) NOT NULL,
            [Domain] nvarchar(255) NOT NULL,
            [Level] nvarchar(50) NOT NULL,
            [Message] nvarchar(4000) NOT NULL,
            [Timestamp] datetime2 NOT NULL,
            [Source] nvarchar(100) NULL,
            [ExceptionType] nvarchar(255) NULL,
            [StackTrace] nvarchar(max) NULL,
            CONSTRAINT [PK_LogEntries] PRIMARY KEY ([Id])
        );");

    // Таблицата MonitorConfigurations може да липсва – създаваме я ако няма (за /monitor-settings)
    await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NULL
        CREATE TABLE [dbo].[MonitorConfigurations] (
            [Id] int NOT NULL IDENTITY,
            [MachineName] nvarchar(255) NOT NULL,
            [ServiceUrl] nvarchar(500) NOT NULL,
            [ApiKey] nvarchar(500) NULL,
            [RequireVpn] bit NOT NULL DEFAULT 0,
            [VpnCheckInterval] int NOT NULL DEFAULT 300,
            [VpnGateways] nvarchar(2000) NOT NULL DEFAULT '[]',
            [VpnProcessNames] nvarchar(1000) NOT NULL DEFAULT '[]',
            [OfflineMode] bit NOT NULL DEFAULT 0,
            [OfflineDataRetention] int NOT NULL DEFAULT 7,
            [ConnectionTimeout] int NOT NULL DEFAULT 30,
            [RetryInterval] int NOT NULL DEFAULT 60,
            [MaxRetries] int NOT NULL DEFAULT 3,
            [CreatedAt] datetime2 NOT NULL,
            [UpdatedAt] datetime2 NOT NULL,
            CONSTRAINT [PK_MonitorConfigurations] PRIMARY KEY ([Id])
        );
        IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MonitorConfigurations_MachineName' AND object_id = OBJECT_ID(N'dbo.MonitorConfigurations'))
        CREATE UNIQUE INDEX [IX_MonitorConfigurations_MachineName] ON [dbo].[MonitorConfigurations] ([MachineName]);");

    // Таблица VisitedWebsites за отчети на продуктивност (посетени URL-и от extension/Monitor)
    await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID(N'dbo.VisitedWebsites', N'U') IS NULL
        CREATE TABLE [dbo].[VisitedWebsites] (
            [Id] int NOT NULL IDENTITY,
            [Username] nvarchar(255) NOT NULL,
            [MachineName] nvarchar(255) NOT NULL,
            [Url] nvarchar(2000) NOT NULL,
            [Title] nvarchar(500) NULL,
            [Browser] nvarchar(100) NOT NULL,
            [VisitedAt] datetime2 NOT NULL,
            [DurationSeconds] int NOT NULL DEFAULT 0,
            CONSTRAINT [PK_VisitedWebsites] PRIMARY KEY ([Id])
        );
        IF OBJECT_ID(N'dbo.VisitedWebsites', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VisitedWebsites_Username_MachineName_VisitedAt' AND object_id = OBJECT_ID(N'dbo.VisitedWebsites'))
        CREATE INDEX [IX_VisitedWebsites_Username_MachineName_VisitedAt] ON [dbo].[VisitedWebsites] ([Username], [MachineName], [VisitedAt]);");

    // Таблица InputLogs за клавиши и кликове (от Monitor input capture)
    await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NULL
        CREATE TABLE [dbo].[InputLogs] (
            [Id] int NOT NULL IDENTITY,
            [MachineName] nvarchar(255) NOT NULL,
            [Username] nvarchar(255) NOT NULL,
            [Domain] nvarchar(255) NOT NULL,
            [Timestamp] datetime2 NOT NULL,
            [LogType] nvarchar(20) NOT NULL,
            [ApplicationName] nvarchar(500) NULL,
            [WindowTitle] nvarchar(1000) NULL,
            [Data] nvarchar(2000) NOT NULL,
            [IsPassword] bit NOT NULL DEFAULT 0,
            CONSTRAINT [PK_InputLogs] PRIMARY KEY ([Id])
        );
        IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InputLogs_MachineName_Timestamp' AND object_id = OBJECT_ID(N'dbo.InputLogs'))
        CREATE INDEX [IX_InputLogs_MachineName_Timestamp] ON [dbo].[InputLogs] ([MachineName], [Timestamp]);
        IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InputLogs_Username_Timestamp' AND object_id = OBJECT_ID(N'dbo.InputLogs'))
        CREATE INDEX [IX_InputLogs_Username_Timestamp] ON [dbo].[InputLogs] ([Username], [Timestamp]);");
}

/// <summary>
/// Винаги гарантира, че таблицата LogEntries съществува – иначе логовете от API и Monitor не се записват в базата.
/// </summary>
static async Task EnsureLogEntriesTableAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
        IF OBJECT_ID(N'dbo.LogEntries', N'U') IS NULL
        CREATE TABLE [dbo].[LogEntries] (
            [Id] int NOT NULL IDENTITY,
            [MachineName] nvarchar(255) NOT NULL,
            [Username] nvarchar(255) NOT NULL,
            [Domain] nvarchar(255) NOT NULL,
            [Level] nvarchar(50) NOT NULL,
            [Message] nvarchar(4000) NOT NULL,
            [Timestamp] datetime2 NOT NULL,
            [Source] nvarchar(100) NULL,
            [ExceptionType] nvarchar(255) NULL,
            [StackTrace] nvarchar(max) NULL,
            CONSTRAINT [PK_LogEntries] PRIMARY KEY ([Id])
        );");
        logger?.LogInfo("LogEntries: таблицата е проверена/създадена");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"LogEntries: не може да се създаде таблицата: {ex.Message}");
    }
}

/// <summary>
/// Винаги гарантира, че таблицата MonitorConfigurations съществува (за /monitor-settings).
/// </summary>
static async Task EnsureMonitorConfigurationsTableAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NULL
            CREATE TABLE [dbo].[MonitorConfigurations] (
                [Id] int NOT NULL IDENTITY,
                [MachineName] nvarchar(255) NOT NULL,
                [ServiceUrl] nvarchar(500) NOT NULL,
                [ApiKey] nvarchar(500) NULL,
                [RequireVpn] bit NOT NULL DEFAULT 0,
                [VpnCheckInterval] int NOT NULL DEFAULT 300,
                [VpnGateways] nvarchar(2000) NOT NULL DEFAULT '[]',
                [VpnProcessNames] nvarchar(1000) NOT NULL DEFAULT '[]',
                [OfflineMode] bit NOT NULL DEFAULT 0,
                [OfflineDataRetention] int NOT NULL DEFAULT 7,
                [ConnectionTimeout] int NOT NULL DEFAULT 30,
                [RetryInterval] int NOT NULL DEFAULT 60,
                [MaxRetries] int NOT NULL DEFAULT 3,
                [CreatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                [UpdatedAt] datetime2 NOT NULL DEFAULT GETDATE(),
                CONSTRAINT [PK_MonitorConfigurations] PRIMARY KEY ([Id])
            );
            IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MonitorConfigurations_MachineName' AND object_id = OBJECT_ID(N'dbo.MonitorConfigurations'))
            CREATE UNIQUE INDEX [IX_MonitorConfigurations_MachineName] ON [dbo].[MonitorConfigurations] ([MachineName]);
            IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MonitorConfigurations') AND name = N'ScreenshotEnabled')
                ALTER TABLE [dbo].[MonitorConfigurations] ADD [ScreenshotEnabled] bit NOT NULL DEFAULT 0;
            IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MonitorConfigurations') AND name = N'ScreenshotIntervalMinutes')
                ALTER TABLE [dbo].[MonitorConfigurations] ADD [ScreenshotIntervalMinutes] int NOT NULL DEFAULT 5;");
        logger?.LogInfo("MonitorConfigurations: таблицата е проверена/създадена");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"MonitorConfigurations: не може да се създаде таблицата: {ex.Message}");
    }
}

static async Task EnsureBlockedIpsTableAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.BlockedIps', N'U') IS NULL
            CREATE TABLE [dbo].[BlockedIps] (
                [Id] int NOT NULL IDENTITY,
                [IpAddress] nvarchar(50) NOT NULL,
                [FailedAttempts] int NOT NULL DEFAULT 0,
                [BlockedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                [LastAttemptAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                [UnblockedAt] datetime2 NULL,
                [UnblockedBy] nvarchar(255) NULL,
                [UnblockReason] nvarchar(500) NULL,
                CONSTRAINT [PK_BlockedIps] PRIMARY KEY ([Id])
            );
            IF OBJECT_ID(N'dbo.BlockedIps', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BlockedIps_IpAddress' AND object_id = OBJECT_ID(N'dbo.BlockedIps'))
            CREATE INDEX [IX_BlockedIps_IpAddress] ON [dbo].[BlockedIps] ([IpAddress]);");
        logger?.LogInfo("BlockedIps: таблицата е проверена/създадена");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"BlockedIps: не може да се създаде таблицата: {ex.Message}");
    }
}

static async Task EnsureEmailActivitiesTableAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.EmailActivities', N'U') IS NULL
            CREATE TABLE [dbo].[EmailActivities] (
                [Id] int NOT NULL IDENTITY,
                [Username] nvarchar(255) NOT NULL,
                [Domain] nvarchar(255) NOT NULL DEFAULT '',
                [MachineName] nvarchar(255) NOT NULL,
                [Subject] nvarchar(1000) NOT NULL DEFAULT '',
                [SenderOrRecipient] nvarchar(500) NULL,
                [EventType] nvarchar(50) NOT NULL DEFAULT 'Opened',
                [DetectionSource] nvarchar(100) NULL,
                [EventTime] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_EmailActivities] PRIMARY KEY ([Id])
            );
            IF OBJECT_ID(N'dbo.EmailActivities', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmailActivities_User' AND object_id = OBJECT_ID(N'dbo.EmailActivities'))
            CREATE INDEX [IX_EmailActivities_User] ON [dbo].[EmailActivities] ([Username], [MachineName], [EventTime]);");
        logger?.LogInfo("EmailActivities: таблицата е проверена/създадена");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"EmailActivities: не може да се създаде таблицата: {ex.Message}");
    }
}

static async Task EnsurePolicyWhitelistColumnsAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'AllowedApplicationsJson')
                ALTER TABLE [dbo].[Policies] ADD [AllowedApplicationsJson] nvarchar(2000) NOT NULL DEFAULT '[]';
            IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'AppWhitelistMode')
                ALTER TABLE [dbo].[Policies] ADD [AppWhitelistMode] bit NOT NULL DEFAULT 0;
            IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'AllowedWebsitesJson')
                ALTER TABLE [dbo].[Policies] ADD [AllowedWebsitesJson] nvarchar(2000) NOT NULL DEFAULT '[]';
            IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'WebWhitelistMode')
                ALTER TABLE [dbo].[Policies] ADD [WebWhitelistMode] bit NOT NULL DEFAULT 0;");
        logger?.LogInfo("Policies: whitelist колоните са проверени/добавени");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"Policies: не може да се добавят whitelist колоните: {ex.Message}");
    }
}

static async Task LoadBlockedIpsOnStartup(WebApplication app)
{
    try
    {
        var bruteForce = app.Services.GetService<ADS.WindowsAuth.API.Services.BruteForceProtectionService>();
        if (bruteForce != null)
            await bruteForce.LoadBlockedIpsAsync();
    }
    catch (Exception ex)
    {
        try
        {
            var logger = app.Services.GetService<ILoggerService>();
            logger?.LogWarning($"Не можах да заредя блокирани IP при старт: {ex.Message}");
        }
        catch { }
    }
}

static async Task TryEnsureCreatedFallbackAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.EnsureCreatedAsync();
        logger?.LogInfo("Таблиците са създадени успешно");
    }
    catch (Exception ensureEx)
    {
        logger?.LogError($"Грешка при създаване на таблиците: {ensureEx.Message}", ensureEx);
    }
}

// Гарантира InputLogs таблица (клавиши и кликове от ADS Client) при всяко стартиране
static async Task EnsureInputLogsTableAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[InputLogs] (
                    [Id]              int           NOT NULL IDENTITY,
                    [MachineName]     nvarchar(255) NOT NULL,
                    [Username]        nvarchar(255) NOT NULL,
                    [Domain]          nvarchar(255) NOT NULL,
                    [Timestamp]       datetime2     NOT NULL,
                    [LogType]         nvarchar(20)  NOT NULL,
                    [ApplicationName] nvarchar(500) NULL,
                    [WindowTitle]     nvarchar(1000) NULL,
                    [Data]            nvarchar(2000) NOT NULL,
                    [IsPassword]      bit           NOT NULL DEFAULT 0,
                    CONSTRAINT [PK_InputLogs] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_InputLogs_MachineName_Timestamp] ON [dbo].[InputLogs] ([MachineName], [Timestamp]);
                CREATE INDEX [IX_InputLogs_Username_Timestamp]    ON [dbo].[InputLogs] ([Username],    [Timestamp]);
            END");
        logger?.LogInfo("EnsureInputLogsTableAsync: OK");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"EnsureInputLogsTableAsync: {ex.Message}");
    }
}

// Гарантира WorkSessions таблица (Work Logger) при всяко стартиране
static async Task EnsureWorkSessionsTableAsync(ApplicationDbContext dbContext, ILoggerService? logger)
{
    try
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.WorkSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[WorkSessions] (
                    [Id]             int            NOT NULL IDENTITY,
                    [Username]       nvarchar(255)  NOT NULL,
                    [Domain]         nvarchar(255)  NOT NULL DEFAULT '',
                    [MachineName]    nvarchar(255)  NOT NULL DEFAULT '',
                    [StartTime]      datetime2      NOT NULL,
                    [EndTime]        datetime2      NULL,
                    [Location]       nvarchar(50)   NOT NULL DEFAULT 'Home',
                    [Status]         nvarchar(50)   NOT NULL DEFAULT 'Active',
                    [Notes]          nvarchar(2000) NULL,
                    [PausedSeconds]  int            NOT NULL DEFAULT 0,
                    [PausedAt]       datetime2      NULL,
                    [CreatedAt]      datetime2      NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT [PK_WorkSessions] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_WorkSessions_Username_StartTime] ON [dbo].[WorkSessions] ([Username], [StartTime]);
                CREATE INDEX [IX_WorkSessions_Status]             ON [dbo].[WorkSessions] ([Status]);
            END");
        logger?.LogInfo("EnsureWorkSessionsTableAsync: OK");
    }
    catch (Exception ex)
    {
        logger?.LogWarning($"EnsureWorkSessionsTableAsync: {ex.Message}");
    }
}

static async Task LoadSessionsOnStartup(WebApplication app)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
            var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
            
            if (databaseService != null)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogInfo("Започва зареждане на активни сесии от базата данни при стартиране...");
                
                await sessionService.LoadSessionsFromDatabaseAsync(databaseService);
                
                logger.LogInfo("Зареждане на активни сесии от базата данни завършено.");
            }
            else
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogWarning("DatabaseService не е наличен - пропускаме зареждане на сесии от базата данни");
            }
        }
    }
    catch (Exception ex)
    {
        // Логваме грешката но не спираме приложението
        try
        {
            var logger = app.Services.GetRequiredService<ILoggerService>();
            logger.LogError($"Грешка при зареждане на сесии от базата данни при стартиране: {ex.Message}", ex);
        }
        catch
        {
            // Ако не можем да логнем, игнорираме
        }
    }
}

/// <summary>
/// Инициализира Portal Identity DB — създава Identity таблиците ако не съществуват
/// и добавя default Admin потребител при нужда.
///
/// ВАЖНО: EnsureCreatedAsync() работи САМО за нова база данни.
/// Ако базата вече съществува (създадена от ApplicationDbContext.MigrateAsync()),
/// EnsureCreatedAsync() не прави НИЩО и Identity таблиците (AspNetRoles, AspNetUsers, ...)
/// никога не се създават → SqlException: Invalid object name 'AspNetRoles'.
/// Затова използваме raw SQL с IF NOT EXISTS — същия подход като MonitorConfigurations.
/// </summary>
static async Task InitializePortalDbAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var portalDb = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetService<ILoggerService>();

        // Гарантираме всички Identity таблици с IF NOT EXISTS (работи за нова И съществуваща база)
        await EnsureIdentityTablesAsync(portalDb, logger);
        logger?.LogInfo("Portal Identity DB инициализирана");

        // Създаваме роли ако не съществуват
        foreach (var role in new[] { "Admin", "User", "Monitor" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Винаги гарантираме admin потребител – ако не съществува, създаваме
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            adminUser = new PortalUser
            {
                UserName = "admin",
                Email = "admin@ads.local",
                DisplayName = "Administrator",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            var adminResult = await userManager.CreateAsync(adminUser, "Admin@12345");
            if (adminResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger?.LogInfo("Default Admin потребител създаден: admin / Admin@12345");
            }
            else
            {
                logger?.LogError($"Грешка при създаване на admin: {string.Join(", ", adminResult.Errors.Select(e => e.Description))}");
            }
        }

        // Създаваме user потребител ако не съществува
        var normalUser = await userManager.FindByNameAsync("user");
        if (normalUser == null)
        {
            normalUser = new PortalUser
            {
                UserName = "user",
                Email = "user@ads.local",
                DisplayName = "Потребител",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            var userResult = await userManager.CreateAsync(normalUser, "User@123");
            if (userResult.Succeeded)
            {
                await userManager.AddToRoleAsync(normalUser, "User");
                logger?.LogInfo("Default User потребител създаден: user / User@123");
            }
        }
    }
    catch (Exception ex)
    {
        try
        {
            var logger = app.Services.GetService<ILoggerService>();
            logger?.LogError($"Грешка при инициализация на Portal Identity DB: {ex.Message}", ex);
        }
        catch { }
    }
}

/// <summary>
/// Създава ASP.NET Core Identity таблиците с IF NOT EXISTS — безопасно за нова и съществуваща база.
/// EnsureCreatedAsync() не може да се използва тук защото не създава таблици в съществуваща база.
/// </summary>
static async Task EnsureIdentityTablesAsync(PortalDbContext portalDb, ILoggerService? logger)
{
    try
    {
        // Таблиците се създават в правилния ред (parent преди child с FK)
        // 1. AspNetRoles
        await portalDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetRoles] (
                [Id]               nvarchar(450) NOT NULL,
                [Name]             nvarchar(256) NULL,
                [NormalizedName]   nvarchar(256) NULL,
                [ConcurrencyStamp] nvarchar(max) NULL,
                CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'RoleNameIndex'
                           AND object_id = OBJECT_ID(N'dbo.AspNetRoles'))
            CREATE UNIQUE INDEX [RoleNameIndex]
                ON [dbo].[AspNetRoles] ([NormalizedName])
                WHERE [NormalizedName] IS NOT NULL;");

        // 2. AspNetUsers (с всички custom полета на PortalUser)
        await portalDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetUsers] (
                [Id]                   nvarchar(450)  NOT NULL,
                [DisplayName]          nvarchar(max)  NOT NULL DEFAULT '',
                [WindowsUsername]      nvarchar(max)  NULL,
                [Domain]               nvarchar(max)  NULL,
                [Role]                 nvarchar(max)  NOT NULL DEFAULT 'User',
                [IsActive]             bit            NOT NULL DEFAULT 1,
                [LastLoginAt]          datetime2      NULL,
                [CreatedAt]            datetime2      NOT NULL DEFAULT GETUTCDATE(),
                [Notes]                nvarchar(max)  NULL,
                [UserName]             nvarchar(256)  NULL,
                [NormalizedUserName]   nvarchar(256)  NULL,
                [Email]                nvarchar(256)  NULL,
                [NormalizedEmail]      nvarchar(256)  NULL,
                [EmailConfirmed]       bit            NOT NULL DEFAULT 0,
                [PasswordHash]         nvarchar(max)  NULL,
                [SecurityStamp]        nvarchar(max)  NULL,
                [ConcurrencyStamp]     nvarchar(max)  NULL,
                [PhoneNumber]          nvarchar(max)  NULL,
                [PhoneNumberConfirmed] bit            NOT NULL DEFAULT 0,
                [TwoFactorEnabled]     bit            NOT NULL DEFAULT 0,
                [LockoutEnd]           datetimeoffset NULL,
                [LockoutEnabled]       bit            NOT NULL DEFAULT 1,
                [AccessFailedCount]    int            NOT NULL DEFAULT 0,
                CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UserNameIndex'
                           AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
            CREATE UNIQUE INDEX [UserNameIndex]
                ON [dbo].[AspNetUsers] ([NormalizedUserName])
                WHERE [NormalizedUserName] IS NOT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'EmailIndex'
                           AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
            CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);");

        // 3. Child таблици (FK към AspNetRoles и AspNetUsers)
        await portalDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetRoleClaims] (
                [Id]         int           NOT NULL IDENTITY,
                [RoleId]     nvarchar(450) NOT NULL,
                [ClaimType]  nvarchar(max) NULL,
                [ClaimValue] nvarchar(max) NULL,
                CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
                    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetRoleClaims_RoleId'
                           AND object_id = OBJECT_ID(N'dbo.AspNetRoleClaims'))
            CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);

            IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetUserClaims] (
                [Id]         int           NOT NULL IDENTITY,
                [UserId]     nvarchar(450) NOT NULL,
                [ClaimType]  nvarchar(max) NULL,
                [ClaimValue] nvarchar(max) NULL,
                CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserClaims_UserId'
                           AND object_id = OBJECT_ID(N'dbo.AspNetUserClaims'))
            CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);

            IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetUserLogins] (
                [LoginProvider]       nvarchar(128) NOT NULL,
                [ProviderKey]         nvarchar(128) NOT NULL,
                [ProviderDisplayName] nvarchar(max) NULL,
                [UserId]              nvarchar(450) NOT NULL,
                CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
                CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserLogins_UserId'
                           AND object_id = OBJECT_ID(N'dbo.AspNetUserLogins'))
            CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);

            IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetUserRoles] (
                [UserId] nvarchar(450) NOT NULL,
                [RoleId] nvarchar(450) NOT NULL,
                CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
                CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId]
                    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserRoles_RoleId'
                           AND object_id = OBJECT_ID(N'dbo.AspNetUserRoles'))
            CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);

            IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
            CREATE TABLE [dbo].[AspNetUserTokens] (
                [UserId]        nvarchar(450) NOT NULL,
                [LoginProvider] nvarchar(128) NOT NULL,
                [Name]          nvarchar(128) NOT NULL,
                [Value]         nvarchar(max) NULL,
                CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
                CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId]
                    FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
            );

            IF OBJECT_ID(N'dbo.Fido2Keys', N'U') IS NULL
            CREATE TABLE [dbo].[Fido2Keys] (
                [Id]                int           NOT NULL IDENTITY,
                [UserId]            nvarchar(450) NOT NULL,
                [CredentialId]      nvarchar(500) NOT NULL,
                [PublicKeyCose]     nvarchar(max) NOT NULL,
                [SignCount]         decimal(20,0) NOT NULL DEFAULT 0,
                [DeviceDescription] nvarchar(max) NOT NULL DEFAULT '',
                [CreatedAt]         datetime2     NOT NULL DEFAULT GETUTCDATE(),
                [LastUsedAt]        datetime2     NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_Fido2Keys] PRIMARY KEY ([Id])
            );
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fido2Keys_CredentialId'
                           AND object_id = OBJECT_ID(N'dbo.Fido2Keys'))
            CREATE UNIQUE INDEX [IX_Fido2Keys_CredentialId] ON [dbo].[Fido2Keys] ([CredentialId]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fido2Keys_UserId'
                           AND object_id = OBJECT_ID(N'dbo.Fido2Keys'))
            CREATE INDEX [IX_Fido2Keys_UserId] ON [dbo].[Fido2Keys] ([UserId]);");

        logger?.LogInfo("Identity таблиците (AspNetUsers, AspNetRoles, ...) са проверени/създадени успешно");
    }
    catch (Exception ex)
    {
        logger?.LogError($"Грешка при създаване на Identity таблиците: {ex.Message}", ex);
        throw; // Рехвърляме — без Identity таблиците приложението не може да работи
    }
}

void ConfigureServices(WebApplicationBuilder builder)
{
    // Configure routing - изключваме lowercase URLs за да запазим точното casing на endpoints
    builder.Services.AddRouting(options =>
    {
        options.LowercaseUrls = false;
        options.LowercaseQueryStrings = false;
    });

    // HttpClient Factory
    builder.Services.AddHttpClient();

    // Session за WebAuthn challenge tracking
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(10);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

    // WebAuthn/FIDO2 Service
    builder.Services.AddSingleton<ADS.WindowsAuth.API.Services.WebAuthnService>();

    // Configure services - AddControllersWithViews за да поддържаме Razor Views
    builder.Services.AddControllersWithViews();
    builder.Services.AddEndpointsApiExplorer();
    
    // HttpClient за комуникация с Remote Desktop Service
    builder.Services.AddHttpClient();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
        { 
            Title = "ADS Windows Authentication API", 
            Version = "v1",
            Description = "API за Windows аутентикация с QR код"
        });
        
        // Игнорираме MVC контролерите (HomeController и т.н.) - само API контролерите
        c.DocInclusionPredicate((docName, apiDesc) =>
        {
            // Включваме само контролери с [ApiController] attribute
            return apiDesc.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor actionDescriptor &&
                   actionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ApiControllerAttribute), false).Any();
        });
        
        // Добавяне на JWT схема за Swagger (опционално)
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        
        // Не изискваме JWT за всички endpoints - само за тези които имат [Authorize]
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Configure database
    ConfigureDatabase(builder);

    // Configure authentication
    ConfigureAuthentication(builder);

    // Configure services
    // Определяне на application directory
    string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
    
    // Logger Service - EnhancedLoggerService + DatabaseLoggingDecorator (запис в LogEntries)
    var enhancedLogger = new EnhancedLoggerService(applicationDirectory);
    builder.Services.AddSingleton<ILoggerService>(sp =>
        new ADS.WindowsAuth.API.Services.DatabaseLoggingDecorator(
            enhancedLogger,
            sp.GetRequiredService<IServiceScopeFactory>()));
    
    // Windows Auth Service - ResilientWindowsAuthService с Polly retry (exponential backoff + jitter)
    builder.Services.AddSingleton<IWindowsAuthService, ResilientWindowsAuthService>();
    
    // Session Service - изисква IWindowsAuthService и ILoggerService
    builder.Services.AddSingleton<ISessionService>(sp => 
        new SessionService(
            sp.GetRequiredService<ILoggerService>(), 
            sp.GetRequiredService<IWindowsAuthService>()));
    
    // Activity Monitor Service - изисква ILoggerService
    builder.Services.AddSingleton<IActivityMonitorService>(sp => 
        new ActivityMonitorService(sp.GetRequiredService<ILoggerService>()));
    
    // Policy Service - изисква ILoggerService и IServiceScopeFactory за запис в БД
    builder.Services.AddSingleton<IPolicyService>(sp =>
        new PolicyService(sp.GetRequiredService<ILoggerService>(), sp.GetRequiredService<IServiceScopeFactory>()));
    
    // JWT Service - трябва JwtSettings
    builder.Services.AddSingleton<IJwtService>(sp =>
    {
        // Предаваме environment информация за правилна проверка на Development режим
        bool isDevelopment = builder.Environment.IsDevelopment();
        var secureSettings = SecureConfigurationProvider.GetSecureSettings(builder.Configuration, isDevelopment);
        var jwtSettings = new JwtSettings
        {
            Issuer = builder.Configuration["Jwt:Issuer"] ?? "ADS.WindowsAuth.API",
            Audience = builder.Configuration["Jwt:Audience"] ?? "ADS.WindowsAuth.Client",
            Key = secureSettings.JwtKey,
            ExpiryMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiryMinutes", 60)
        };
        
        if (!jwtSettings.IsValid())
        {
            throw new InvalidOperationException("JWT configuration is invalid");
        }
        
        return new JwtService(jwtSettings, sp.GetRequiredService<ILoggerService>());
    });
    // Active Directory Service
    builder.Services.AddSingleton<IAdService>(sp =>
    {
        var adSettings = new ADS.WindowsAuth.Core.Models.ActiveDirectorySettings
        {
            Enabled = builder.Configuration.GetValue<bool>("ActiveDirectory:Enabled", false),
            DomainName = builder.Configuration["ActiveDirectory:DomainName"] ?? "",
            LdapPath = builder.Configuration["ActiveDirectory:LdapPath"] ?? "",
            ServiceAccount = builder.Configuration["ActiveDirectory:ServiceAccount"] ?? "",
            ServicePassword = builder.Configuration["ActiveDirectory:ServicePassword"] ?? ""
        };
        return new AdService(adSettings, sp.GetRequiredService<ILoggerService>());
    });

    // Database Service (Scoped) - правилно свързване с DbContext lifecycle
    builder.Services.AddScoped<IDatabaseService>(sp =>
    {
        try
        {
            // Опитваме се да вземем ApplicationDbContext от текущия scope
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var loggerService = sp.GetRequiredService<ILoggerService>();
            var adService = sp.GetService<IAdService>();
            
            // Проверка дали context-ът е валиден
            if (context == null)
            {
                loggerService.LogWarning("ApplicationDbContext не е наличен - използвам MockDatabaseService");
                return new MockDatabaseService(loggerService);
            }
            
            return new DatabaseService(context, loggerService, adService);
        }
        catch (InvalidOperationException)
        {
            // DbContext не е наличен в scope-а - използваме Mock
            var logger = sp.GetService<ILoggerService>();
            logger?.LogWarning("ApplicationDbContext не е наличен в scope - използвам MockDatabaseService");
            return new MockDatabaseService(logger!);
        }
        catch (Exception ex)
        {
            var logger = sp.GetService<ILoggerService>();
            logger?.LogError($"Грешка при създаване на DatabaseService: {ex.Message}. Използвам MockDatabaseService.", ex);
            return new MockDatabaseService(logger!);
        }
    });

    // ============================================================
    // ASP.NET Core Identity — Portal потребители (AspNetUsers)
    // ============================================================
    ConfigurePortalIdentity(builder);

    // Remote Desktop Session Service (in-memory, singleton)
    builder.Services.AddSingleton<IRemoteDesktopSessionService, RemoteDesktopSessionService>();

    // Brute Force Protection Service (singleton — in-memory + DB persistence)
    builder.Services.AddSingleton<ADS.WindowsAuth.API.Services.BruteForceProtectionService>();
    builder.Services.AddSingleton<ADS.WindowsAuth.API.Services.MachineDataService>();

    // SignalR (10MB для screen frames)
    builder.Services.AddSignalR(options =>
    {
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
        options.EnableDetailedErrors = true;
    });

    // CORS - SignalR изисква AllowCredentials + конкретен origin (не AllowAnyOrigin)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
}

void ConfigureDatabase(WebApplicationBuilder builder)
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // Използваме InMemory database като fallback
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("ADS_WindowsAuth_Memory_Fallback"), 
            ServiceLifetime.Scoped);
    }
    else
    {
        // SQL Server с правилна конфигурация
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString,
                sqlServerOptions => sqlServerOptions
                    .EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)
                    .CommandTimeout(60)),
            ServiceLifetime.Scoped); // Явно задаваме Scoped lifetime
    }
}

void ConfigureAuthentication(WebApplicationBuilder builder)
{
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ADS.WindowsAuth.API";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ADS.WindowsAuth.Client";
    bool isDevelopment = builder.Environment.IsDevelopment();
    var jwtKey = SecureConfigurationProvider.GetSecureSettings(builder.Configuration, isDevelopment).JwtKey;

    // ВАЖНО: SignInManager<T>.SignInAsync() вътрешно извиква
    //   HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, ...)  ("Identity.Application")
    // Затова cookie схемата ТРЯБВА да носи точно това име.
    // AddIdentityCookies() регистрира всичките 4 Identity схеми:
    //   Identity.Application, Identity.External, Identity.TwoFactorRememberMe, Identity.TwoFactorUserId
    // SignInManager ги ползва вътрешно при PasswordSignIn (External check, TwoFactor и т.н.)
    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme          = IdentityConstants.ApplicationScheme; // "Identity.Application"
        options.DefaultSignInScheme    = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    });

    // Регистрираме всичките Identity cookie схеми
    var identityCookies = authBuilder.AddIdentityCookies();
    identityCookies.ApplicationCookie!.Configure(options =>
    {
        options.LoginPath            = "/Account/Login";
        options.LogoutPath           = "/Account/Logout";
        options.AccessDeniedPath     = "/Account/Login";
        options.ExpireTimeSpan       = TimeSpan.FromHours(8);
        options.SlidingExpiration    = true;
        options.Cookie.HttpOnly      = true;
        options.Cookie.SecurePolicy  = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite      = SameSiteMode.Lax;
        options.Cookie.Name          = "ADS.Portal";
        // API заявки (/api/...) → 401 вместо redirect към /Account/Login
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

    // JWT Bearer за API клиенти (Windows Forms Client, мобилно приложение и т.н.)
    authBuilder.AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero
        };
    });

    // Authorize policy: приема и Identity cookie и Bearer JWT
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("PortalOrBearer", policy =>
            policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, "Bearer")
                  .RequireAuthenticatedUser());
        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                IdentityConstants.ApplicationScheme, "Bearer")
            .RequireAuthenticatedUser()
            .Build();
    });
}

void ConfigurePortalIdentity(WebApplicationBuilder builder)
{
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        builder.Services.AddDbContext<PortalDbContext>(opts =>
            opts.UseInMemoryDatabase("ADS_Portal_Memory"), ServiceLifetime.Scoped);
    }
    else
    {
        builder.Services.AddDbContext<PortalDbContext>(opts =>
            opts.UseSqlServer(connectionString,
                sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null)
                          .CommandTimeout(60)),
            ServiceLifetime.Scoped);
    }

    builder.Services
        .AddIdentityCore<PortalUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.SignIn.RequireConfirmedAccount = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<PortalDbContext>()
        .AddSignInManager<SignInManager<PortalUser>>()
        .AddDefaultTokenProviders();
}

void ConfigureMiddleware(WebApplication app)
{
    // Логване на всички заявки за debugging - ПРЕДИ всичко друго
    app.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetService<ILoggerService>();
        logger?.LogInfo($"[MIDDLEWARE] Заявка: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
        await next();
    });
    
    // Exception handling - трябва да е първо
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        // Production: обработка на грешки без да използваме Error view/controller (избягваме втори exception)
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                var ex = feature?.Error;
                var logger = context.RequestServices.GetService<ILoggerService>();
                if (logger != null && ex != null)
                {
                    var fullMessage = ex.Message + (ex.StackTrace != null ? "\n" + ex.StackTrace : "");
                    if (ex.InnerException != null)
                        fullMessage += "\nInner: " + ex.InnerException.Message + (ex.InnerException.StackTrace ?? "");
                    logger.LogError($"Production unhandled: {context.Request.Method} {context.Request.Path} - {ex.Message}", ex);
                }

                if (context.Response.HasStarted) return;
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(
                    @"<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Грешка</title></head><body>" +
                    @"<h1>Грешка</h1><p>Възникна грешка при обработката на заявката.</p>" +
                    @"<p>Проверете логовете в папка <code>logs</code> или stdout логовете на сървъра.</p>" +
                    @"<p><a href='/'>Начало</a></p></body></html>");
            });
        });
    }

    // Global exception handling middleware - хваща изключения извън ExceptionHandler
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetService<ILoggerService>();
            logger?.LogError($"Грешка: {context.Request.Method} {context.Request.Path} - {ex.Message}", ex);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(
                    @"<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Грешка</title></head><body>" +
                    @"<h1>Грешка</h1><p>Възникна грешка. Проверете логовете.</p><a href='/'>Начало</a></body></html>");
            }
        }
    });

    app.UseRouting();

    // Static files за CSS, JS, images и т.н.
    app.UseStaticFiles();

    // Session middleware (трябва след UseRouting, преди UseAuthentication)
    app.UseSession();
    
    app.UseCors("AllowAll");
    
    app.UseAuthentication();
    app.UseAuthorization();
    
    // Swagger middleware - след Authentication но преди endpoints
    // Работи и в Development и в Production
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADS Windows Auth API v1");
        c.RoutePrefix = "swagger"; // Swagger ще е достъпен на /swagger
        c.DisplayRequestDuration();
    });
}

void ConfigureEndpoints(WebApplication app)
{
    // Health checks endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.Now }));

    // MapControllers ПРЕДИ – attribute routes [Route("/")] на Login да имат приоритет
    app.MapControllers();

    // Map conventional route – за /Home, /Account/Login и т.н.
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    // SignalR hub за Remote Desktop – AllowAnonymous за да може Host приложението да се свързва без auth cookie
    app.MapHub<RemoteDesktopHub>("/hubs/remotedesktop").AllowAnonymous();

    // Периодично почистване на изтекли Remote Desktop сесии
    var rdSessionService = app.Services.GetRequiredService<IRemoteDesktopSessionService>();
    var cleanupTimer = new System.Timers.Timer(60000);
    cleanupTimer.Elapsed += (_, _) => rdSessionService.CleanupExpiredSessionsAsync().Wait();
    cleanupTimer.Start();
}
