using System.Diagnostics;
using System.Net.NetworkInformation;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за управление на връзки
/// </summary>
public class ConnectionService : IConnectionService
{
    private readonly ServiceConfiguration _config;
    private readonly ILoggerService _logger;
    private bool _isOffline = false;
    private DateTime _lastConnectionCheck = DateTime.MinValue;

    public ConnectionService(ServiceConfiguration config, ILoggerService logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> CheckConnectionAsync()
    {
        if (string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _isOffline = true;
            return false;
        }

        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(_config.ConnectionTimeout);
                
                // Добавяне на API ключ ако е конфигуриран
                if (!string.IsNullOrEmpty(_config.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
                }

                // Използваме /health endpoint за проверка на връзката
                HttpResponseMessage response = await client.GetAsync($"{_config.ServiceUrl}/health");
                
                if (response.IsSuccessStatusCode)
                {
                    _isOffline = false;
                    _lastConnectionCheck = DateTime.Now;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Няма връзка със сървъра: {ex.Message}");
        }

        _isOffline = true;
        return false;
    }

    public bool IsVpnConnected()
    {
        if (!_config.RequireVpn)
        {
            return true; // Не се изисква VPN
        }

        // Проверка за VPN процеси
        foreach (string processName in _config.VpnProcessNames)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                // Допълнителна проверка - ping към VPN gateway
                if (_config.VpnGateways.Count > 0)
                {
                    foreach (string gateway in _config.VpnGateways)
                    {
                        if (PingHost(gateway))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return true; // VPN процес е активен
                }
            }
        }

        // Проверка за Windows VPN връзки
        try
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in interfaces)
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ppp &&
                    ni.OperationalStatus == OperationalStatus.Up)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при проверка на VPN интерфейси: {ex.Message}");
        }

        return false;
    }

    public bool IsOfflineMode()
    {
        return _isOffline || _config.OfflineMode;
    }

    public async Task<bool> SyncOfflineDataAsync()
    {
        if (!IsOfflineMode())
        {
            return true; // Не е в offline режим
        }

        // Проверка за връзка
        bool hasConnection = await CheckConnectionAsync();
        
        if (!hasConnection)
        {
            return false;
        }

        // Синхронизация на данни
        // Тук може да се имплементира логика за изпращане на натрупани данни
        _logger.LogInfo("Синхронизация на offline данни...");

        return true;
    }

    private bool PingHost(string hostname)
    {
        try
        {
            Ping ping = new Ping();
            PingReply reply = ping.Send(hostname, 2000); // 2 секунди timeout
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}

