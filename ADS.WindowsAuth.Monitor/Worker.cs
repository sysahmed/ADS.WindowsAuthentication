using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Management;

namespace ADS.WindowsAuth.Monitor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IActivityMonitorService _activityMonitor;
    private readonly IPolicyService _policyService;
    private readonly IConnectionService _connectionService;
    private readonly ILoggerService _loggerService;
    private readonly ServiceConfiguration _serviceConfig;
    private readonly IWindowsFirewallService _firewallService;
    private readonly HttpClient _httpClient;
    private readonly Services.ServiceProtection _serviceProtection;
    private readonly string _machineName;
    private readonly string _username;
    private readonly string _domain;

    private Dictionary<int, DateTime> _processStartTimes = new();
    private Dictionary<int, string> _processNames = new();
    private Dictionary<string, DateTime> _fileOpenTimes = new();
    private DateTime _lastVpnCheck = DateTime.MinValue;
    private DateTime _lastSystemInfoSend = DateTime.MinValue;
    private HashSet<string> _knownUsbDevices = new();

    public Worker(ILogger<Worker> logger, IActivityMonitorService activityMonitor, 
                 IPolicyService policyService, IConnectionService connectionService,
                 ILoggerService loggerService, ServiceConfiguration serviceConfig,
                 IWindowsFirewallService firewallService)
    {
        _logger = logger;
        _activityMonitor = activityMonitor;
        _policyService = policyService;
        _connectionService = connectionService;
        _loggerService = loggerService;
        _serviceConfig = serviceConfig;
        _firewallService = firewallService;
        _serviceProtection = new Services.ServiceProtection(loggerService);
        
        _httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(_serviceConfig.ServiceUrl))
        {
            _httpClient.BaseAddress = new Uri(_serviceConfig.ServiceUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_serviceConfig.ConnectionTimeout);
            
            if (!string.IsNullOrEmpty(_serviceConfig.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _serviceConfig.ApiKey);
            }
        }
        
        _machineName = Environment.MachineName;
        _username = Environment.UserName;
        _domain = Environment.UserDomainName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker стартиран на {MachineName} за потребител {Username}@{Domain}", 
            _machineName, _username, _domain);

        // Периодична проверка и инсталация на Credential Provider и Client (на всеки 5 минути)
        var credentialProviderInstaller = new Services.CredentialProviderInstaller(
            _loggerService, 
            AppDomain.CurrentDomain.BaseDirectory);
        
        var clientInstaller = new Services.ClientInstaller(
            _loggerService,
            AppDomain.CurrentDomain.BaseDirectory);
        
        // Първоначална инсталация при стартиране
        try
        {
            credentialProviderInstaller.CheckAndInstall();
            clientInstaller.CheckAndInstall();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Грешка при първоначална инсталация");
        }

        // Периодична проверка (на всеки 5 минути)
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    
                    if (stoppingToken.IsCancellationRequested)
                        break;
                    
                    credentialProviderInstaller.CheckAndInstall();
                    clientInstaller.CheckAndInstall();
                }
                catch (OperationCanceledException)
                {
                    // Нормално при спиране на сервиса
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Грешка при проверка на инсталации");
                }
                
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }, stoppingToken);

        // Проверка за VPN ако е изискван
        if (_serviceConfig.RequireVpn && !_connectionService.IsVpnConnected())
        {
            _logger.LogWarning("VPN връзката е изисквана, но не е активна. Очакване на VPN...");
            
            // Очакване на VPN връзка
            while (!_connectionService.IsVpnConnected() && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);
            }
            
            _logger.LogInformation("VPN връзката е установена");
        }

        // Проверка за връзка със сървъра
        bool hasConnection = await _connectionService.CheckConnectionAsync();
        if (!hasConnection && !_serviceConfig.OfflineMode)
        {
            _logger.LogWarning("Няма връзка със сървъра. Работа в offline режим.");
        }

        // Започване на мониторинг
        _activityMonitor.StartMonitoring(_username, _domain, _machineName);
        
        // СТЪПКА 3: Стартиране на защита на сервиса
        _serviceProtection.StartMonitoring();
        
        // Тестово логване за проверка на връзката с API
        _loggerService.LogInfo($"Monitor Service стартиран на {_machineName} за потребител {_username}@{_domain}");
        _loggerService.LogInfo($"API URL: {_serviceConfig.ServiceUrl}");
        _loggerService.LogInfo($"Връзка с API: {(hasConnection ? "Успешна" : "Неуспешна")}");
        
        // Изпращане на заявка към API (ако има връзка)
        if (hasConnection)
        {
            try
            {
                await _httpClient.PostAsJsonAsync("/api/activity/start", new
                {
                    Username = _username,
                    Domain = _domain,
                    MachineName = _machineName
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при изпращане на заявка за стартиране на мониторинг");
            }
        }

        // Стартиране на различни мониторинг задачи
        var processMonitorTask = MonitorProcesses(stoppingToken);
        var screenTimeTask = MonitorScreenTime(stoppingToken);
        var policyCheckTask = CheckPolicies(stoppingToken);
        var networkMonitorTask = MonitorNetworkActivity(stoppingToken);
        var systemInfoTask = MonitorSystemInfo(stoppingToken);
        var usbMonitorTask = MonitorUsbDevices(stoppingToken);
        var fileActivityTask = MonitorFileActivity(stoppingToken);
        var websiteFilterTask = MonitorAndFilterWebsites(stoppingToken);
        var configurationSyncTask = SyncConfigurationFromApi(stoppingToken);
        var policiesSyncTask = SyncPoliciesFromApi(stoppingToken);
        var vpnMonitorTask = MonitorVpnSoftware(stoppingToken); // СТЪПКА 1: VPN мониторинг
        var dnsMonitorTask = MonitorDnsChanges(stoppingToken); // СТЪПКА 2: DNS мониторинг
        var loginMonitorTask = MonitorUserSessions(stoppingToken); // Мониторинг на user login събития

        await Task.WhenAll(
            processMonitorTask, 
            screenTimeTask, 
            policyCheckTask,
            networkMonitorTask,
            systemInfoTask,
            usbMonitorTask,
            fileActivityTask,
            websiteFilterTask,
            configurationSyncTask,
            policiesSyncTask,
            vpnMonitorTask,
            dnsMonitorTask,
            loginMonitorTask
        );
    }

    private async Task MonitorProcesses(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Process[] processes = Process.GetProcesses();
                
                foreach (Process process in processes)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(process.MainWindowTitle) && 
                            process.ProcessName != "explorer" && 
                            process.ProcessName != "dwm")
                            continue;

                        int processId = process.Id;
                        string processName = process.ProcessName;
                        string executablePath = process.MainModule?.FileName ?? "";

                        // Проверка дали процесът е нов
                        if (!_processStartTimes.ContainsKey(processId))
                        {
                            _processStartTimes[processId] = DateTime.Now;
                            _processNames[processId] = processName;
                            
                            // Проверка дали приложението е блокирано
                            if (_policyService.IsApplicationBlocked(_machineName, _username, processName))
                            {
                                _logger.LogWarning("Опит за стартиране на блокирано приложение: {ProcessName}", processName);
                                process.Kill();
                                continue;
                            }

                            _activityMonitor.RegisterApplicationStart(_username, _machineName, processName, executablePath);
                            
                            // Изпращане към API
                            try
                            {
                                await _httpClient.PostAsJsonAsync("/api/activity/application/start", new
                                {
                                    Username = _username,
                                    Domain = _domain,
                                    MachineName = _machineName,
                                    ApplicationName = processName,
                                    ExecutablePath = executablePath,
                                    ProcessId = processId,
                                    Timestamp = DateTime.Now
                                }, cancellationToken);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Игнориране на процеси, които не могат да се достъпят
                        _logger.LogDebug(ex, "Грешка при достъп до процес");
                    }
                }

                // Проверка за затворени процеси
                var currentProcessIds = processes.Select(p => p.Id).ToHashSet();
                var closedProcessIds = _processStartTimes.Keys.Where(id => !currentProcessIds.Contains(id)).ToList();
                
                foreach (var closedId in closedProcessIds)
                {
                    var startTime = _processStartTimes[closedId];
                    var duration = (int)(DateTime.Now - startTime).TotalSeconds;
                    string? processName = _processNames.GetValueOrDefault(closedId);
                    
                    _processStartTimes.Remove(closedId);
                    _processNames.Remove(closedId);
                    
                    // Изпращане на информация за затворен процес
                    if (!string.IsNullOrEmpty(processName))
                    {
                        try
                        {
                            await _httpClient.PostAsJsonAsync("/api/activity/application/stop", new
                            {
                                Username = _username,
                                MachineName = _machineName,
                                ApplicationName = processName,
                                DurationSeconds = duration
                            }, cancellationToken);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на процеси");
            }

            await Task.Delay(5000, cancellationToken); // Проверка на всеки 5 секунди
        }
    }

    private async Task MonitorScreenTime(CancellationToken cancellationToken)
    {
        DateTime sessionStart = DateTime.Now;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                int screenTimeSeconds = (int)(DateTime.Now - sessionStart).TotalSeconds;
                _activityMonitor.UpdateScreenTime(_username, _machineName, screenTimeSeconds);
                
                // Изпращане към API
                try
                {
                    await _httpClient.PostAsJsonAsync("/api/activity/screentime/update", new
                    {
                        Username = _username,
                        MachineName = _machineName,
                        Seconds = screenTimeSeconds
                    }, cancellationToken);
                }
                catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при обновяване на screen time");
            }

            await Task.Delay(60000, cancellationToken); // Обновяване на всяка минута
        }
    }

    private async Task CheckPolicies(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Проверка за VPN ако е изискван
                if (_serviceConfig.RequireVpn && 
                    (DateTime.Now - _lastVpnCheck).TotalSeconds >= _serviceConfig.VpnCheckInterval)
                {
                    _lastVpnCheck = DateTime.Now;
                    
                    if (!_connectionService.IsVpnConnected())
                    {
                        _logger.LogWarning("VPN връзката е изисквана, но не е активна");
                        // Може да се изпрати нотификация или да се блокира достъпът
                    }
                }

                // Проверка за връзка със сървъра
                bool hasConnection = await _connectionService.CheckConnectionAsync();
                if (!hasConnection && _connectionService.IsOfflineMode())
                {
                    // Синхронизация на offline данни при възстановяване на връзката
                    await _connectionService.SyncOfflineDataAsync();
                }

                // Четене на политики от API-то
                List<Policy> policies = new();
                if (_httpClient.BaseAddress != null)
                {
                    try
                    {
                        var apiPolicies = await _httpClient.GetFromJsonAsync<List<Policy>>($"/api/Policy/machine/{_machineName}/user/{_username}", cancellationToken);
                        if (apiPolicies != null)
                        {
                            policies = apiPolicies;
                        }
                    }
                    catch
                    {
                        // Fallback към локалния PolicyService
                        policies = _policyService.GetActivePoliciesForMachine(_machineName, _username);
                    }
                }
                else
                {
                    // Използваме локалния PolicyService ако няма API URL
                    policies = _policyService.GetActivePoliciesForMachine(_machineName, _username);
                }
                
                foreach (var policy in policies)
                {
                    if (policy.MaxScreenTimeSeconds > 0)
                    {
                        var activity = _activityMonitor.GetUserActivity(_username, _machineName);
                        if (activity != null && activity.ScreenTimeSeconds >= policy.MaxScreenTimeSeconds)
                        {
                            _logger.LogWarning("Достигнато максимално време на екрана: {Seconds} секунди", activity.ScreenTimeSeconds);
                            // Тук може да се блокира достъпът или да се изпрати предупреждение
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при проверка на политики");
            }

            await Task.Delay(30000, cancellationToken); // Проверка на всеки 30 секунди
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker спира...");
        
        // Спиране на мониторинг
        _activityMonitor.StopMonitoring(_username, _machineName);
        
        // СТЪПКА 3: Спиране на защита на сервиса
        _serviceProtection.StopMonitoring();
        
        // Изпращане на заявка към API
        try
        {
            await _httpClient.PostAsJsonAsync("/api/activity/stop", new
            {
                Username = _username,
                MachineName = _machineName
            }, cancellationToken);
        }
        catch { }

        await base.StopAsync(cancellationToken);
    }

    private async Task MonitorNetworkActivity(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                var activeConnections = networkInterfaces
                    .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    .Select(ni => new
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        Speed = ni.Speed,
                        BytesReceived = ni.GetIPStatistics().BytesReceived,
                        BytesSent = ni.GetIPStatistics().BytesSent
                    })
                    .ToList();

                try
                {
                    await _httpClient.PostAsJsonAsync("/api/activity/network", new
                    {
                        Username = _username,
                        Domain = _domain,
                        MachineName = _machineName,
                        NetworkInterfaces = activeConnections,
                        Timestamp = DateTime.Now
                    }, cancellationToken);
                }
                catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на мрежова активност");
            }

            await Task.Delay(60000, cancellationToken);
        }
    }

    private async Task MonitorSystemInfo(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if ((DateTime.Now - _lastSystemInfoSend).TotalMinutes >= 5)
                {
                    var systemInfo = new
                    {
                        MachineName = _machineName,
                        Username = _username,
                        Domain = _domain,
                        OsVersion = Environment.OSVersion.ToString(),
                        ProcessorCount = Environment.ProcessorCount,
                        TotalMemory = GC.GetTotalMemory(false),
                        WorkingSet = Environment.WorkingSet,
                        Uptime = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds
                    };

                    try
                    {
                        await _httpClient.PostAsJsonAsync("/api/activity/system", new
                        {
                            Username = _username,
                            Domain = _domain,
                            MachineName = _machineName,
                            SystemInfo = systemInfo,
                            Timestamp = DateTime.Now
                        }, cancellationToken);
                        
                        _lastSystemInfoSend = DateTime.Now;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на системна информация");
            }

            await Task.Delay(60000, cancellationToken);
        }
    }

    private async Task MonitorUsbDevices(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var usbDevices = new List<object>();
                
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBControllerDevice"))
                    {
                        foreach (ManagementObject device in searcher.Get())
                        {
                            string deviceId = device["Dependent"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(deviceId) && !_knownUsbDevices.Contains(deviceId))
                            {
                                _knownUsbDevices.Add(deviceId);
                                
                                using (var deviceObj = new ManagementObject(deviceId))
                                {
                                    deviceObj.Get();
                                    usbDevices.Add(new
                                    {
                                        DeviceId = deviceId,
                                        Description = deviceObj["Description"]?.ToString() ?? "",
                                        Manufacturer = deviceObj["Manufacturer"]?.ToString() ?? "",
                                        Name = deviceObj["Name"]?.ToString() ?? ""
                                    });
                                }
                            }
                        }
                    }

                    if (usbDevices.Any())
                    {
                        try
                        {
                            await _httpClient.PostAsJsonAsync("/api/activity/usb", new
                            {
                                Username = _username,
                                Domain = _domain,
                                MachineName = _machineName,
                                UsbDevices = usbDevices,
                                Timestamp = DateTime.Now
                            }, cancellationToken);
                        }
                        catch { }
                    }
                }
                catch
                {
                    // WMI може да не е достъпен
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на USB устройства");
            }

            await Task.Delay(30000, cancellationToken);
        }
    }

    private async Task MonitorFileActivity(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var recentFiles = new List<object>();
                
                try
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var documentsPath = Path.Combine(userProfile, "Documents");
                    var desktopPath = Path.Combine(userProfile, "Desktop");

                    if (Directory.Exists(documentsPath))
                    {
                        var recentDocs = new DirectoryInfo(documentsPath)
                            .GetFiles("*", SearchOption.TopDirectoryOnly)
                            .Where(f => (DateTime.Now - f.LastWriteTimeUtc).TotalHours < 1)
                            .Select(f => new
                            {
                                FileName = f.Name,
                                FilePath = f.FullName,
                                Size = f.Length,
                                LastModified = f.LastWriteTimeUtc
                            })
                            .Take(10);

                        recentFiles.AddRange(recentDocs);
                    }
                }
                catch { }

                if (recentFiles.Any())
                {
                    try
                    {
                        await _httpClient.PostAsJsonAsync("/api/activity/files", new
                        {
                            Username = _username,
                            Domain = _domain,
                            MachineName = _machineName,
                            RecentFiles = recentFiles,
                            Timestamp = DateTime.Now
                        }, cancellationToken);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на файлова активност");
            }

            await Task.Delay(120000, cancellationToken);
        }
    }

    private async Task MonitorAndFilterWebsites(CancellationToken cancellationToken)
    {
        string hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        HashSet<string> blockedDomains = new();
        HashSet<string> firewallBlockedDomains = new();
        DateTime lastHostsUpdate = DateTime.MinValue;
        DateTime lastFirewallUpdate = DateTime.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Проверка на политиките за блокирани уебсайтове - ВИНАГИ от API-то
                var newBlockedDomains = new HashSet<string>();

                // Получаване на политики от API-то
                if (_httpClient.BaseAddress != null)
                {
                    try
                    {
                        var apiPolicies = await _httpClient.GetFromJsonAsync<List<Policy>>($"/api/Policy/machine/{_machineName}/user/{_username}", cancellationToken);
                        if (apiPolicies != null && apiPolicies.Any())
                        {
                            foreach (var policy in apiPolicies)
                            {
                                if (policy.IsActive && policy.BlockedWebsites != null)
                                {
                                    foreach (var blockedSite in policy.BlockedWebsites)
                                    {
                                        string domain = ExtractDomainFromUrl(blockedSite);
                                        if (!string.IsNullOrEmpty(domain))
                                        {
                                            newBlockedDomains.Add(domain);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Грешка при получаване на политики от API: {ex.Message}");
                        // Fallback към локалния PolicyService само ако API-то не е достъпно
                        try
                        {
                            var policies = _policyService.GetActivePoliciesForMachine(_machineName, _username);
                            foreach (var policy in policies)
                            {
                                foreach (var blockedSite in policy.BlockedWebsites)
                                {
                                    string domain = ExtractDomainFromUrl(blockedSite);
                                    if (!string.IsNullOrEmpty(domain))
                                    {
                                        newBlockedDomains.Add(domain);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Игнорираме грешки
                        }
                    }
                }

                // Обновяване на hosts файл ако има промени (за обратна съвместимост)
                if (!blockedDomains.SetEquals(newBlockedDomains) || 
                    (DateTime.Now - lastHostsUpdate).TotalMinutes >= 5)
                {
                    await UpdateHostsFile(hostsFilePath, newBlockedDomains);
                    blockedDomains = newBlockedDomains;
                    lastHostsUpdate = DateTime.Now;

                    if (newBlockedDomains.Count > 0)
                    {
                        _logger.LogInformation($"Блокирани домейни в hosts файл: {string.Join(", ", newBlockedDomains)}");
                    }
                }

                // СТЪПКА 3: Windows Firewall блокиране (по-силна защита)
                // Обновяване на firewall правила ако има промени или на всеки 10 минути
                if (!firewallBlockedDomains.SetEquals(newBlockedDomains) || 
                    (DateTime.Now - lastFirewallUpdate).TotalMinutes >= 10)
                {
                    try
                    {
                        // Разблокиране на домейни които вече не са блокирани
                        var domainsToUnblock = firewallBlockedDomains.Except(newBlockedDomains).ToList();
                        foreach (var domain in domainsToUnblock)
                        {
                            await _firewallService.UnblockDomainAsync(domain);
                                _logger.LogInformation($"Домейн {domain} е разблокиран от Windows Firewall");
                        }

                        // Блокиране на нови домейни
                        var domainsToBlock = newBlockedDomains.Except(firewallBlockedDomains).ToList();
                        foreach (var domain in domainsToBlock)
                        {
                            bool blocked = await _firewallService.BlockDomainAsync(domain);
                            if (blocked)
                            {
                                _logger.LogInformation($"Домейн {domain} е блокиран чрез Windows Firewall");
                            }
                            else
                            {
                                _logger.LogWarning($"Неуспешно блокиране на домейн {domain} чрез Windows Firewall");
                            }
                        }

                        firewallBlockedDomains = newBlockedDomains;
                        lastFirewallUpdate = DateTime.Now;

                        if (newBlockedDomains.Count > 0)
                        {
                            _logger.LogInformation($"Общо {newBlockedDomains.Count} домейна блокирани чрез Windows Firewall");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Грешка при обновяване на Windows Firewall правила");
                    }
                }

                // СТЪПКА 4: Мониторинг за опити за заобикаляне - проверка на hosts файл
                await MonitorHostsFileChanges(hostsFilePath, newBlockedDomains);

                // Мониторинг на браузъри и блокиране на отворени блокирани сайтове
                await MonitorBrowserTabs(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг и филтриране на уебсайтове");
            }

            await Task.Delay(30000, cancellationToken); // Проверка на всеки 30 секунди
        }
    }

    /// <summary>
    /// Мониторинг за опити за заобикаляне - проверява дали hosts файлът е променен
    /// </summary>
    private async Task MonitorHostsFileChanges(string hostsFilePath, HashSet<string> expectedBlockedDomains)
    {
        try
        {
            if (!File.Exists(hostsFilePath))
                return;

            // Четене на hosts файла
            var lines = await File.ReadAllLinesAsync(hostsFilePath);
            var currentBlockedDomains = new HashSet<string>();

            // Извличане на блокираните домейни от hosts файла
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("127.0.0.1") || trimmedLine.StartsWith("::1"))
                {
                    var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        // Проверка дали е наш блок (има коментар # ADS)
                        if (trimmedLine.Contains("# ADS"))
                        {
                            var domain = parts[1].ToLower();
                            currentBlockedDomains.Add(domain);
                        }
                    }
                }
            }

            // Проверка за липсващи блокировки (опити за заобикаляне)
            var missingBlocks = expectedBlockedDomains.Except(currentBlockedDomains).ToList();
            if (missingBlocks.Any())
            {
                _logger.LogWarning($"⚠️ ОПИТ ЗА ЗАОБИКАЛЯНЕ: Липсват блокировки в hosts файл за: {string.Join(", ", missingBlocks)}");
                
                // Изпращане на алерт до API-то
                try
                {
                    if (_httpClient.BaseAddress != null)
                    {
                        await _httpClient.PostAsJsonAsync("/api/logs/upload", new
                        {
                            MachineName = _machineName,
                            Username = _username,
                            Domain = _domain,
                            Level = "WARNING",
                            Message = $"Опит за заобикаляне на блокировки: Липсват блокировки за домейни: {string.Join(", ", missingBlocks)}",
                            Timestamp = DateTime.Now,
                            Source = "MonitorService",
                            ExceptionType = "BypassAttempt",
                            StackTrace = $"Hosts file missing blocks for: {string.Join(", ", missingBlocks)}"
                        });
                    }
                }
                catch
                {
                    // Игнорираме грешки при изпращане на алерт
                }

                // Автоматично възстановяване на блокировките
                await UpdateHostsFile(hostsFilePath, expectedBlockedDomains);
                _logger.LogInformation($"Автоматично възстановени блокировки в hosts файл");
            }

            // Проверка за неочаквани промени (добавени блокировки които не са от нас)
            var unexpectedBlocks = currentBlockedDomains.Except(expectedBlockedDomains).ToList();
            if (unexpectedBlocks.Any())
            {
                _logger.LogInformation($"Намерени допълнителни блокировки в hosts файл (не от системата): {string.Join(", ", unexpectedBlocks)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при мониторинг на hosts файл за промени");
        }
    }

    private string ExtractDomainFromUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            // Премахване на протокол
            url = url.Replace("http://", "").Replace("https://", "").Replace("www.", "");
            
            // Премахване на път и параметри
            int slashIndex = url.IndexOf('/');
            if (slashIndex > 0)
                url = url.Substring(0, slashIndex);

            int questionIndex = url.IndexOf('?');
            if (questionIndex > 0)
                url = url.Substring(0, questionIndex);

            return url.Trim().ToLower();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task UpdateHostsFile(string hostsFilePath, HashSet<string> blockedDomains)
    {
        try
        {
            // Четене на текущия hosts файл
            List<string> lines = new();
            if (File.Exists(hostsFilePath))
            {
                lines = File.ReadAllLines(hostsFilePath).ToList();
            }

            // Премахване на старите блокировки от ADS
            lines.RemoveAll(line => line.Contains("# ADS Windows Auth Block") || 
                                   (line.Contains("127.0.0.1") && line.Contains("# ADS")));

            // Добавяне на нови блокировки
            if (blockedDomains.Count > 0)
            {
                lines.Add("");
                lines.Add("# ADS Windows Auth Block - Start");
                foreach (var domain in blockedDomains)
                {
                    lines.Add($"127.0.0.1 {domain} # ADS");
                    lines.Add($"127.0.0.1 www.{domain} # ADS");
                }
                lines.Add("# ADS Windows Auth Block - End");
            }

            // Записване на hosts файл (изисква администраторски права)
            try
            {
                await File.WriteAllLinesAsync(hostsFilePath, lines);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Няма права за запис в hosts файл. Блокирането на уебсайтове няма да работи.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при обновяване на hosts файл");
        }
    }

    private async Task MonitorBrowserTabs(CancellationToken cancellationToken)
    {
        try
        {
            // Списък с браузъри за мониторинг
            string[] browsers = { "chrome", "msedge", "firefox", "opera", "brave" };

            foreach (string browserName in browsers)
            {
                try
                {
                    Process[] browserProcesses = Process.GetProcessesByName(browserName);
                    foreach (Process browser in browserProcesses)
                    {
                        // Проверка на отворени прозорци/табове
                        // Забележка: Това е опростена версия - реалното блокиране изисква по-сложна логика
                        // Може да се използва Windows API за получаване на URL-и от браузъри
                        
                        // Засега само логваме ако браузърът е отворен
                        // В бъдеще може да се добави функционалност за получаване на URL-и и блокиране
                    }
                }
                catch
                {
                    // Игнорираме грешки при достъп до процеси
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при мониторинг на браузъри");
        }

            await Task.CompletedTask;
    }

    private async Task SyncConfigurationFromApi(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Проверка за обновена конфигурация от API-то (на всеки 5 минути)
                if (_httpClient.BaseAddress != null)
                {
                    try
                    {
                        var response = await _httpClient.GetFromJsonAsync<MonitorConfigurationResponse>($"/api/monitor/configuration/{_machineName}", cancellationToken);
                        if (response != null)
                        {
                            // Обновяване на конфигурацията
                            if (!string.IsNullOrEmpty(response.ServiceUrl))
                                _serviceConfig.ServiceUrl = response.ServiceUrl;
                            if (!string.IsNullOrEmpty(response.ApiKey))
                                _serviceConfig.ApiKey = response.ApiKey;
                            _serviceConfig.RequireVpn = response.RequireVpn;
                            _serviceConfig.VpnCheckInterval = response.VpnCheckInterval;
                            
                            // Парсване на JSON arrays
                            if (!string.IsNullOrEmpty(response.VpnGateways))
                            {
                                try
                                {
                                    _serviceConfig.VpnGateways = System.Text.Json.JsonSerializer.Deserialize<List<string>>(response.VpnGateways) ?? new List<string>();
                                }
                                catch { }
                            }
                            if (!string.IsNullOrEmpty(response.VpnProcessNames))
                            {
                                try
                                {
                                    _serviceConfig.VpnProcessNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(response.VpnProcessNames) ?? new List<string> { "FortiClient", "rasdial" };
                                }
                                catch { }
                            }
                            
                            _serviceConfig.OfflineMode = response.OfflineMode;
                            _serviceConfig.OfflineDataRetention = response.OfflineDataRetention;
                            _serviceConfig.ConnectionTimeout = response.ConnectionTimeout;
                            _serviceConfig.RetryInterval = response.RetryInterval;
                            _serviceConfig.MaxRetries = response.MaxRetries;

                            // Обновяване на HttpClient
                            if (!string.IsNullOrEmpty(_serviceConfig.ServiceUrl))
                            {
                                _httpClient.BaseAddress = new Uri(_serviceConfig.ServiceUrl);
                                _httpClient.Timeout = TimeSpan.FromSeconds(_serviceConfig.ConnectionTimeout);
                                
                                if (!string.IsNullOrEmpty(_serviceConfig.ApiKey))
                                {
                                    _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
                                    _httpClient.DefaultRequestHeaders.Add("X-API-Key", _serviceConfig.ApiKey);
                                }
                            }

                            _logger.LogInformation("Конфигурацията е синхронизирана от API-то");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"Грешка при синхронизация на конфигурация: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при синхронизация на конфигурация от API");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Проверка на всеки 5 минути
        }
    }

    private async Task SyncPoliciesFromApi(CancellationToken cancellationToken)
    {
        // Този метод вече не е нужен, защото политиките се четат директно от API-то
        // в MonitorAndFilterWebsites и CheckPolicies
        // Оставяме го празен за да не счупи кода, но може да се премахне в бъдеще
            await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    /// <summary>
    /// СТЪПКА 1: Мониторинг на VPN софтуер и опити за заобикаляне
    /// </summary>
    private async Task MonitorVpnSoftware(CancellationToken cancellationToken)
    {
        // Списък с известни VPN приложения (може да се конфигурира от API)
        var knownVpnProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "forticlient", "openvpn", "nordvpn", "expressvpn", "surfshark",
            "protonvpn", "windscribe", "tunnelbear", "hotspotshield",
            "cyberghost", "privateinternetaccess", "vyprvpn", "purevpn",
            "ipvanish", "strongvpn", "hidemyass", "vpn", "vpnclient",
            "vpnservice", "vpngate", "softether", "wireguard", "zerotier"
        };

        // Добавяне на конфигурирани VPN процеси
        if (_serviceConfig.VpnProcessNames != null)
        {
            foreach (var vpnProcess in _serviceConfig.VpnProcessNames)
            {
                knownVpnProcesses.Add(vpnProcess);
            }
        }

        HashSet<string> detectedVpnProcesses = new();
        DateTime lastVpnCheck = DateTime.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var currentVpnProcesses = new HashSet<string>();

                // Проверка за VPN процеси
                Process[] allProcesses = Process.GetProcesses();
                foreach (var process in allProcesses)
                {
                    try
                    {
                        string processName = process.ProcessName.ToLower();
                        
                        // Проверка дали е известен VPN процес
                        if (knownVpnProcesses.Any(vpn => processName.Contains(vpn, StringComparison.OrdinalIgnoreCase)))
                        {
                            currentVpnProcesses.Add(processName);
                            
                            // Ако е нов VPN процес
                            if (!detectedVpnProcesses.Contains(processName))
                            {
                                _logger.LogWarning($"⚠️ VPN софтуер открит: {processName} (PID: {process.Id})");
                                
                                // Изпращане на алерт до API
                                try
                                {
                                    if (_httpClient.BaseAddress != null)
                                    {
                                        await _httpClient.PostAsJsonAsync("/api/logs/upload", new
                                        {
                                            MachineName = _machineName,
                                            Username = _username,
                                            Domain = _domain,
                                            Level = "WARNING",
                                            Message = $"VPN софтуер открит: {processName} (PID: {process.Id})",
                                            Timestamp = DateTime.Now,
                                            Source = "MonitorService",
                                            ExceptionType = "VpnDetected",
                                            StackTrace = $"Process: {processName}, Path: {process.MainModule?.FileName ?? "N/A"}"
                                        }, cancellationToken);
                                    }
                                }
                                catch
                                {
                                    // Игнорираме грешки при изпращане
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Игнорираме процеси които не могат да се достъпят
                    }
                }

                // Проверка за спрели VPN процеси
                var stoppedVpnProcesses = detectedVpnProcesses.Except(currentVpnProcesses).ToList();
                foreach (var stoppedVpn in stoppedVpnProcesses)
                {
                    _logger.LogInformation($"VPN процес спрян: {stoppedVpn}");
                }

                detectedVpnProcesses = currentVpnProcesses;

                // Периодична проверка на VPN интерфейси (на всеки 2 минути)
                if ((DateTime.Now - lastVpnCheck).TotalMinutes >= 2)
                {
                    await CheckVpnInterfaces(cancellationToken);
                    lastVpnCheck = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на VPN софтуер");
            }

            await Task.Delay(30000, cancellationToken); // Проверка на всеки 30 секунди
        }
    }

    /// <summary>
    /// Проверка на VPN мрежови интерфейси
    /// </summary>
    private async Task CheckVpnInterfaces(CancellationToken cancellationToken)
    {
        try
        {
            var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var vpnInterfaces = new List<string>();

            foreach (var ni in networkInterfaces)
            {
                // Проверка за VPN типове интерфейси
                if ((ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ppp ||
                     ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel ||
                     ni.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) ||
                     ni.Description.Contains("TAP", StringComparison.OrdinalIgnoreCase)) &&
                    ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    vpnInterfaces.Add(ni.Description);
                }
            }

            if (vpnInterfaces.Any())
            {
                _logger.LogInformation($"VPN интерфейси открити: {string.Join(", ", vpnInterfaces)}");
                
                // Ако VPN не е разрешен, изпращаме алерт
                if (!_serviceConfig.RequireVpn)
                {
                    try
                    {
                        if (_httpClient.BaseAddress != null)
                        {
                            await _httpClient.PostAsJsonAsync("/api/logs/upload", new
                            {
                                MachineName = _machineName,
                                Username = _username,
                                Domain = _domain,
                                Level = "WARNING",
                                Message = $"VPN интерфейси открити (не са разрешени): {string.Join(", ", vpnInterfaces)}",
                                Timestamp = DateTime.Now,
                                Source = "MonitorService",
                                ExceptionType = "UnauthorizedVpn",
                                StackTrace = $"Interfaces: {string.Join(", ", vpnInterfaces)}"
                            }, cancellationToken);
                        }
                    }
                    catch
                    {
                        // Игнорираме грешки
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при проверка на VPN интерфейси");
        }
    }

    /// <summary>
    /// СТЪПКА 2: Мониторинг за промени в DNS настройките
    /// </summary>
    private async Task MonitorDnsChanges(CancellationToken cancellationToken)
    {
        Dictionary<string, string> lastDnsServers = new();
        DateTime lastDnsCheck = DateTime.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Проверка на DNS настройки (на всеки 5 минути)
                if ((DateTime.Now - lastDnsCheck).TotalMinutes >= 5)
                {
                    var currentDnsServers = await GetDnsServersAsync();
                    
                    // Проверка за промени
                    foreach (var adapter in currentDnsServers)
                    {
                        if (lastDnsServers.TryGetValue(adapter.Key, out var lastDns))
                        {
                            if (lastDns != adapter.Value)
                            {
                                _logger.LogWarning($"⚠️ DNS промяна открита на {adapter.Key}: {lastDns} -> {adapter.Value}");
                                
                                // Изпращане на алерт до API
                                try
                                {
                                    if (_httpClient.BaseAddress != null)
                                    {
                                        await _httpClient.PostAsJsonAsync("/api/logs/upload", new
                                        {
                                            MachineName = _machineName,
                                            Username = _username,
                                            Domain = _domain,
                                            Level = "WARNING",
                                            Message = $"DNS промяна открита на {adapter.Key}: {lastDns} -> {adapter.Value}",
                                            Timestamp = DateTime.Now,
                                            Source = "MonitorService",
                                            ExceptionType = "DnsChange",
                                            StackTrace = $"Adapter: {adapter.Key}, Old: {lastDns}, New: {adapter.Value}"
                                        }, cancellationToken);
                                    }
                                }
                                catch
                                {
                                    // Игнорираме грешки
                                }
                            }
                        }
                        else
                        {
                            // Нов адаптер
                            _logger.LogInformation($"DNS адаптер открит: {adapter.Key} -> {adapter.Value}");
                        }
                    }

                    // Проверка за премахнати адаптери
                    foreach (var oldAdapter in lastDnsServers.Keys)
                    {
                        if (!currentDnsServers.ContainsKey(oldAdapter))
                        {
                            _logger.LogInformation($"DNS адаптер премахнат: {oldAdapter}");
                        }
                    }

                    lastDnsServers = currentDnsServers;
                    lastDnsCheck = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Грешка при мониторинг на DNS промени");
            }

            await Task.Delay(60000, cancellationToken); // Проверка на всяка минута
        }
    }

    /// <summary>
    /// Получаване на текущите DNS сървъри за всички мрежови адаптери
    /// </summary>
    private async Task<Dictionary<string, string>> GetDnsServersAsync()
    {
        var dnsServers = new Dictionary<string, string>();

        try
        {
            // Използване на netsh за получаване на DNS настройки
            var processInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface ip show dns",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                string output = await process.StandardOutput.ReadToEndAsync();
                
                // Парсване на изхода
                string currentAdapter = "";
                foreach (var line in output.Split('\n'))
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith("Configuration for interface"))
                    {
                        // Извличане на името на адаптера
                        var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"Configuration for interface ""(.+)""");
                        if (match.Success)
                        {
                            currentAdapter = match.Groups[1].Value;
                        }
                    }
                    else if (trimmedLine.StartsWith("DNS servers configured through DHCP:") || 
                             trimmedLine.StartsWith("Statically Configured DNS Servers:"))
                    {
                        // Пропускаме заглавките
                    }
                    else if (!string.IsNullOrEmpty(currentAdapter) && 
                             System.Net.IPAddress.TryParse(trimmedLine, out _))
                    {
                        // DNS сървър IP адрес
                        if (!dnsServers.ContainsKey(currentAdapter))
                        {
                            dnsServers[currentAdapter] = trimmedLine;
                        }
                        else
                        {
                            dnsServers[currentAdapter] += $", {trimmedLine}";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при получаване на DNS настройки: {ex.Message}");
        }

        return dnsServers;
    }

    /// <summary>
    /// Мониторинг на user login събития чрез Windows Event Log
    /// </summary>
    private async Task MonitorUserSessions(CancellationToken cancellationToken)
    {
        DateTime lastCheck = DateTime.Now;
        HashSet<string> processedEvents = new HashSet<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Четене на Security Event Log за Event ID 4624 (успешно влизане)
                // Забележка: Изисква администраторски права
                try
                {
                    using (var eventLog = new System.Diagnostics.EventLog("Security"))
                    {
                        // Четене на последните събития от последната проверка
                        var entries = eventLog.Entries.Cast<System.Diagnostics.EventLogEntry>()
                            .Where(e => e.TimeGenerated > lastCheck && e.InstanceId == 4624)
                            .OrderBy(e => e.TimeGenerated)
                            .ToList();

                        foreach (var entry in entries)
                        {
                            try
                            {
                                // Уникален идентификатор за събитието
                                string eventKey = $"{entry.TimeGenerated:yyyyMMddHHmmssfff}_{entry.Index}";
                                
                                if (processedEvents.Contains(eventKey))
                                    continue;

                                // Парсване на данните от събитието
                                string message = entry.Message ?? "";
                                string username = ExtractValueFromEventMessage(message, "Account Name:");
                                string domain = ExtractValueFromEventMessage(message, "Account Domain:");
                                string logonTypeStr = ExtractValueFromEventMessage(message, "Logon Type:");
                                
                                // Пропускаме системни акаунти
                                if (string.IsNullOrEmpty(username) || 
                                    username.EndsWith("$") || 
                                    username.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                                    username.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) ||
                                    username.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase))
                                {
                                    processedEvents.Add(eventKey);
                                    continue;
                                }

                                int logonType = 0;
                                int.TryParse(logonTypeStr?.Trim(), out logonType);

                                // Определяне на метода на влизане
                                string loginMethod = "Password";
                                if (logonType == 2) loginMethod = "Interactive"; // Интерактивно влизане
                                else if (logonType == 10) loginMethod = "RemoteInteractive"; // Remote Desktop
                                else if (logonType == 11) loginMethod = "CachedInteractive"; // Кеширани credentials

                                _loggerService.LogInfo($"User login засечен: {username}@{domain} на {_machineName} (Logon Type: {logonType})");

                                // Изпращане към API (само ако има конфигуриран BaseAddress)
                                if (_httpClient.BaseAddress != null)
                                {
                                    try
                                    {
                                        await _httpClient.PostAsJsonAsync("/api/activity/login", new
                                        {
                                            Username = username,
                                            Domain = domain,
                                            MachineName = _machineName,
                                            LoginTime = entry.TimeGenerated,
                                            LoginMethod = loginMethod,
                                            LogonType = logonType
                                        }, cancellationToken);
                                        
                                        _loggerService.LogInfo($"Login event изпратен към API за {username}@{domain}");
                                    }
                                    catch (Exception apiEx)
                                    {
                                        _loggerService.LogError($"Грешка при изпращане на login event към API: {apiEx.Message}", apiEx);
                                    }
                                }
                                else
                                {
                                    _loggerService.LogWarning($"Login event не може да бъде изпратен - API URL не е конфигуриран");
                                }

                                processedEvents.Add(eventKey);
                            }
                            catch (Exception entryEx)
                            {
                                _loggerService.LogError($"Грешка при обработка на event log entry: {entryEx.Message}", entryEx);
                            }
                        }

                        lastCheck = DateTime.Now;
                    }
                }
                catch (System.Security.SecurityException secEx)
                {
                    _loggerService.LogError($"Няма права за четене на Security Event Log. Monitor Service трябва да работи като администратор: {secEx.Message}", secEx);
                    // Изчакваме по-дълго преди следващ опит
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    continue;
                }
                catch (Exception logEx)
                {
                    _loggerService.LogError($"Грешка при четене на Event Log: {logEx.Message}", logEx);
                }

                // Почистване на стари обработени събития (пазим само последните 1000)
                if (processedEvents.Count > 1000)
                {
                    processedEvents = new HashSet<string>(processedEvents.Skip(500));
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Грешка при мониторинг на user sessions: {ex.Message}", ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken); // Проверка на всеки 30 секунди
        }
    }

    /// <summary>
    /// Извлича стойност от Event Log съобщение
    /// </summary>
    private string ExtractValueFromEventMessage(string message, string fieldName)
    {
        try
        {
            var lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }
        catch
        {
            // Игнорираме грешки при парсване
        }
        return string.Empty;
    }
}

/// <summary>
/// Response модел за Monitor Configuration от API
/// </summary>
public class MonitorConfigurationResponse
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool RequireVpn { get; set; }
    public int VpnCheckInterval { get; set; } = 300;
    public string VpnGateways { get; set; } = "[]";
    public string VpnProcessNames { get; set; } = "[]";
    public bool OfflineMode { get; set; }
    public int OfflineDataRetention { get; set; } = 7;
    public int ConnectionTimeout { get; set; } = 30;
    public int RetryInterval { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
}
