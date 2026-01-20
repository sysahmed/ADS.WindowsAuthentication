using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Service.Hubs;
using Microsoft.Extensions.Configuration;

string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;

var builder = WebApplication.CreateBuilder(args);

// Явно зареждане на appsettings.json от директорията на приложението (за Windows Service)
string appsettingsPath = Path.Combine(applicationDirectory, "appsettings.json");
if (File.Exists(appsettingsPath))
{
    builder.Configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: true);
}

// Зареждане на конфигурация за LoggerService
IConfigurationSection serviceConfigSection = builder.Configuration.GetSection("RemoteDesktop");
string? apiUrl = serviceConfigSection.GetValue<string>("ServiceUrl");

// Създаване на LoggerService с API URL (ако е наличен)
ILoggerService loggerService = new LoggerService(applicationDirectory, apiUrl);

// Логване след създаване на LoggerService
if (string.IsNullOrEmpty(apiUrl))
{
    loggerService.LogWarning($"ServiceUrl не е конфигуриран в appsettings.json. Логовете няма да се изпращат към API.");
}
else
{
    loggerService.LogInfo($"LoggerService инициализиран с API URL: {apiUrl}");
}

// Зареждане на AD настройки
IConfigurationSection adSection = builder.Configuration.GetSection("ActiveDirectory");
ActiveDirectorySettings adSettings = new ActiveDirectorySettings
{
    Enabled = adSection.GetValue<bool>("Enabled", false),
    DomainName = adSection.GetValue<string>("DomainName") ?? string.Empty,
    LdapPath = adSection.GetValue<string>("LdapPath") ?? string.Empty,
    ServiceAccount = adSection.GetValue<string>("ServiceAccount") ?? string.Empty,
    ServicePassword = adSection.GetValue<string>("ServicePassword") ?? string.Empty
};

// Създаване на сервиси
IActivityMonitorService activityMonitorService = new ActivityMonitorService(loggerService);
IPolicyService policyService = new PolicyService(loggerService);
IAdService adService = new AdService(adSettings, loggerService);
IWindowsAuthService windowsAuthService = new WindowsAuthService(loggerService);
ISessionService sessionService = new SessionService(loggerService, windowsAuthService);

// Remote Desktop сервиси
IScreenCaptureService screenCaptureService = new ScreenCaptureService(loggerService);
IRemoteInputService remoteInputService = new RemoteInputService(loggerService);
IRemoteDesktopSessionService remoteDesktopSessionService = new RemoteDesktopSessionService(loggerService);

// Конфигуриране като Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ADS.WindowsAuth.Service";
});

// Добавяне на сервиси
builder.Services.AddSingleton<ILoggerService>(loggerService);
builder.Services.AddSingleton<IActivityMonitorService>(activityMonitorService);
builder.Services.AddSingleton<IPolicyService>(policyService);
builder.Services.AddSingleton<IAdService>(adService);
builder.Services.AddSingleton<IWindowsAuthService>(windowsAuthService);
builder.Services.AddSingleton<ISessionService>(sessionService);
builder.Services.AddSingleton<ActiveDirectorySettings>(adSettings);

// Remote Desktop сервиси
builder.Services.AddSingleton<IScreenCaptureService>(screenCaptureService);
builder.Services.AddSingleton<IRemoteInputService>(remoteInputService);
builder.Services.AddSingleton<IRemoteDesktopSessionService>(remoteDesktopSessionService);

// Добавяне на контролери
builder.Services.AddControllers();

// Добавяне на CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Добавяне на SignalR за real-time обновления
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB за screen frames
    options.EnableDetailedErrors = true;
});

// Добавяне на Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Конфигуриране на HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ActivityHub>("/hubs/activity");
app.MapHub<RemoteDesktopHub>("/hubs/remotedesktop");

// Периодично почистване
var cleanupTimer = new System.Timers.Timer(60000);
cleanupTimer.Elapsed += (sender, e) =>
{
    sessionService.CleanupExpiredSessions();
    remoteDesktopSessionService.CleanupExpiredSessionsAsync().Wait();
};
cleanupTimer.Start();

loggerService.LogInfo("ADS Windows Auth Service стартиран");

app.Run();
