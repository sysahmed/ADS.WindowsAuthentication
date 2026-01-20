using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Monitor;
using ADS.WindowsAuth.Monitor.Services;
using Microsoft.Extensions.Configuration;

string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;

var builder = Host.CreateApplicationBuilder(args);

// Явно зареждане на appsettings.json от директорията на приложението (за Windows Service)
string appsettingsPath = Path.Combine(applicationDirectory, "appsettings.json");
if (File.Exists(appsettingsPath))
{
    builder.Configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: true);
}

// Зареждане на конфигурация
IConfigurationSection serviceConfigSection = builder.Configuration.GetSection("ServiceConfiguration");
string? apiUrl = serviceConfigSection.GetValue<string>("ServiceUrl");

// Логване на конфигурацията за debugging (временен файл преди LoggerService)
try
{
    string tempLogPath = Path.Combine(applicationDirectory, "LOGS");
    if (!Directory.Exists(tempLogPath))
    {
        Directory.CreateDirectory(tempLogPath);
    }
    string configLogPath = Path.Combine(tempLogPath, $"CONFIG_STARTUP_{DateTime.Now:yyyyMMdd}.LOG");
    string configInfo = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Application Directory: {applicationDirectory}\n" +
                       $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] ServiceUrl from config: {apiUrl ?? "NULL"}\n" +
                       $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Config file exists: {File.Exists(Path.Combine(applicationDirectory, "appsettings.json"))}\n";
    File.AppendAllText(configLogPath, configInfo);
}
catch { }

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

// Регистрация на Credential Provider DLL (ако съществува)
try
{
    CredentialProviderInstaller installer = new CredentialProviderInstaller(loggerService, applicationDirectory);
    installer.RegisterCredentialProvider();
}
catch (Exception ex)
{
    loggerService.LogError($"Грешка при регистрация на Credential Provider: {ex.Message}");
}

ServiceConfiguration serviceConfig = new ServiceConfiguration
{
    ServiceUrl = serviceConfigSection.GetValue<string>("ServiceUrl") ?? string.Empty,
    ApiKey = serviceConfigSection.GetValue<string>("ApiKey") ?? string.Empty,
    MachineId = serviceConfigSection.GetValue<string>("MachineId") ?? Environment.MachineName,
    RequireVpn = serviceConfigSection.GetValue<bool>("RequireVpn", false),
    VpnCheckInterval = serviceConfigSection.GetValue<int>("VpnCheckInterval", 300),
    VpnGateways = serviceConfigSection.GetSection("VpnGateways").Get<List<string>>() ?? new List<string>(),
    VpnProcessNames = serviceConfigSection.GetSection("VpnProcessNames").Get<List<string>>() ?? new List<string> { "FortiClient", "rasdial" },
    OfflineMode = serviceConfigSection.GetValue<bool>("OfflineMode", false),
    OfflineDataRetention = serviceConfigSection.GetValue<int>("OfflineDataRetention", 7),
    OfflineStoragePath = serviceConfigSection.GetValue<string>("OfflineStoragePath") ?? Path.Combine(applicationDirectory, "OfflineData"),
    ConnectionTimeout = serviceConfigSection.GetValue<int>("ConnectionTimeout", 30),
    RetryInterval = serviceConfigSection.GetValue<int>("RetryInterval", 60),
    MaxRetries = serviceConfigSection.GetValue<int>("MaxRetries", 3)
};

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
IConnectionService connectionService = new ConnectionService(serviceConfig, loggerService);
IWindowsFirewallService firewallService = new WindowsFirewallService(loggerService);

// Конфигуриране като Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ADS.WindowsAuth.Monitor";
});

// Добавяне на сервиси
builder.Services.AddSingleton<ILoggerService>(loggerService);
builder.Services.AddSingleton<IActivityMonitorService>(activityMonitorService);
builder.Services.AddSingleton<IPolicyService>(policyService);
builder.Services.AddSingleton<IConnectionService>(connectionService);
builder.Services.AddSingleton<IWindowsFirewallService>(firewallService);
builder.Services.AddSingleton<ServiceConfiguration>(serviceConfig);
builder.Services.AddSingleton<ActiveDirectorySettings>(adSettings);

// Добавяне на Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
