using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Monitor;
using ADS.WindowsAuth.Monitor.Services;
using Microsoft.Extensions.Configuration;

string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;

// За Windows Service, опитваме се да намерим правилната директория
// Ако appsettings.json не е в BaseDirectory, опитваме други локации
if (!File.Exists(Path.Combine(applicationDirectory, "appsettings.json")))
{
    // Опит 1: Директорията на изпълнимия файл
    string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? applicationDirectory;
    if (File.Exists(Path.Combine(exeDirectory, "appsettings.json")))
    {
        applicationDirectory = exeDirectory;
    }
    // Опит 2: Стандартна локация C:\ADS\Monitor
    else if (File.Exists(@"C:\ADS\Monitor\appsettings.json"))
    {
        applicationDirectory = @"C:\ADS\Monitor";
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Явно зареждане на appsettings.json от директорията на приложението (за Windows Service)
string appsettingsPath = Path.Combine(applicationDirectory, "appsettings.json");
bool configFileExists = File.Exists(appsettingsPath);

// Логване преди LoggerService (временен файл)
try
{
    string tempLogPath = Path.Combine(applicationDirectory, "LOGS");
    if (!Directory.Exists(tempLogPath))
    {
        Directory.CreateDirectory(tempLogPath);
    }
    string preLoggerLogPath = Path.Combine(tempLogPath, $"PRE_LOGGER_{DateTime.Now:yyyyMMdd}.LOG");
    string preLoggerInfo = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Application Directory: {applicationDirectory}\n" +
                           $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] AppSettings path: {appsettingsPath}\n" +
                           $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] AppSettings exists: {configFileExists}\n";
    File.AppendAllText(preLoggerLogPath, preLoggerInfo);
}
catch { }

if (configFileExists)
{
    builder.Configuration.AddJsonFile(appsettingsPath, optional: false, reloadOnChange: true);
}

// Зареждане на конфигурация
IConfigurationSection serviceConfigSection = builder.Configuration.GetSection("ServiceConfiguration");
string? apiUrl = serviceConfigSection.GetValue<string>("ServiceUrl");

// Ако GetValue връща null, опитваме директно от конфигурацията
if (string.IsNullOrEmpty(apiUrl))
{
    apiUrl = builder.Configuration["ServiceConfiguration:ServiceUrl"];
}

// Логване на конфигурацията преди Registry проверка
string configFromFile = apiUrl ?? "NULL";
string configFromRegistry = "NULL";

// Първо опитваме да прочетем ServiceUrl от Registry (ако е конфигуриран там)
if (string.IsNullOrEmpty(apiUrl))
{
    try
    {
        using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ADS\WindowsAuth", false))
        {
            if (key != null)
            {
                var registryUrl = key.GetValue("ServiceUrl") as string;
                if (!string.IsNullOrEmpty(registryUrl))
                {
                    apiUrl = registryUrl;
                    configFromRegistry = registryUrl;
                }
                else
                {
                    configFromRegistry = "NULL (key exists but ServiceUrl is empty)";
                }
            }
            else
            {
                configFromRegistry = "NULL (Registry key does not exist)";
            }
        }
    }
    catch (Exception regEx)
    {
        configFromRegistry = $"NULL (Exception: {regEx.Message})";
    }
}
else
{
    configFromRegistry = "NOT_CHECKED (config file had value)";
}

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
                       $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Config file exists: {File.Exists(Path.Combine(applicationDirectory, "appsettings.json"))}\n" +
                       $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] ServiceUrl from config file: {configFromFile}\n" +
                       $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] ServiceUrl from Registry: {configFromRegistry}\n" +
                       $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] Final API URL for LoggerService: {apiUrl ?? "NULL"}\n";
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

// Актуализиране на ServiceUrl от Registry (ако е конфигуриран там)
if (string.IsNullOrEmpty(serviceConfig.ServiceUrl))
{
    try
    {
        using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ADS\WindowsAuth", false))
        {
            if (key != null)
            {
                var registryUrl = key.GetValue("ServiceUrl") as string;
                if (!string.IsNullOrEmpty(registryUrl))
                {
                    serviceConfig.ServiceUrl = registryUrl;
                    loggerService.LogInfo($"ServiceUrl зареден от Registry: {registryUrl}");
                }
            }
        }
    }
    catch (Exception regEx)
    {
        loggerService.LogWarning($"Грешка при четене на ServiceUrl от Registry: {regEx.Message}");
    }
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
