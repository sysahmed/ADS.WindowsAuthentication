using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за управление на Windows Firewall
/// Използва netsh и PowerShell команди за управление на firewall правила
/// </summary>
public class WindowsFirewallService : IWindowsFirewallService
{
    private readonly ILoggerService _logger;
    private readonly HashSet<string> _blockedDomains = new();
    private readonly Dictionary<string, string> _domainToIpCache = new();

    public WindowsFirewallService(ILoggerService logger)
    {
        _logger = logger;
    }

    public async Task<bool> BlockDomainAsync(string domain)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            domain = domain.Trim().ToLower();
            
            // Проверка дали вече е блокиран
            if (_blockedDomains.Contains(domain))
                return true;

            // Резолване на IP адресите на домейна
            var ipAddresses = await ResolveDomainToIpsAsync(domain);
            if (ipAddresses.Count == 0)
            {
                _logger.LogWarning($"Не може да се резолва домейн: {domain}");
                return false;
            }

            // Блокиране на всеки IP адрес чрез Windows Firewall
            bool allBlocked = true;
            foreach (var ip in ipAddresses)
            {
                if (!await BlockIpAddressAsync(ip))
                {
                    allBlocked = false;
                }
            }

            if (allBlocked)
            {
                _blockedDomains.Add(domain);
                _domainToIpCache[domain] = string.Join(",", ipAddresses);
                _logger.LogInfo($"Домейн {domain} е блокиран чрез Windows Firewall (IP: {string.Join(", ", ipAddresses)})");
            }

            return allBlocked;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при блокиране на домейн {domain}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> UnblockDomainAsync(string domain)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            domain = domain.Trim().ToLower();

            if (!_blockedDomains.Contains(domain))
                return true;

            // Получаване на IP адресите от кеша
            if (_domainToIpCache.TryGetValue(domain, out var ipList))
            {
                var ipAddresses = ipList.Split(',');
                foreach (var ip in ipAddresses)
                {
                    await UnblockIpAddressAsync(ip);
                }
            }

            _blockedDomains.Remove(domain);
            _domainToIpCache.Remove(domain);
            _logger.LogInfo($"Домейн {domain} е разблокиран");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при разблокиране на домейн {domain}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> BlockIpAddressAsync(string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            // Валидация на IP адрес
            if (!IPAddress.TryParse(ipAddress, out _))
            {
                _logger.LogWarning($"Невалиден IP адрес: {ipAddress}");
                return false;
            }

            // Създаване на firewall правило чрез netsh
            string ruleName = $"ADS_Block_{ipAddress.Replace(".", "_")}";
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block remoteip={ipAddress}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _logger.LogInfo($"IP адрес {ipAddress} е блокиран чрез Windows Firewall");
                    return true;
                }
                else
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    _logger.LogWarning($"Грешка при блокиране на IP {ipAddress}: {error}");
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при блокиране на IP адрес {ipAddress}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> UnblockIpAddressAsync(string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            string ruleName = $"ADS_Block_{ipAddress.Replace(".", "_")}";

            var processInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{ruleName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                _logger.LogInfo($"IP адрес {ipAddress} е разблокиран");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при разблокиране на IP адрес {ipAddress}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> BlockPortAsync(int port, string protocol = "TCP")
    {
        try
        {
            string ruleName = $"ADS_Block_Port_{port}_{protocol}";

            var processInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block protocol={protocol} remoteport={port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при блокиране на порт {port}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> UnblockPortAsync(int port, string protocol = "TCP")
    {
        try
        {
            string ruleName = $"ADS_Block_Port_{port}_{protocol}";

            var processInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{ruleName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при разблокиране на порт {port}: {ex.Message}", ex);
            return false;
        }
    }

    public Task<List<string>> GetBlockedDomainsAsync()
    {
        return Task.FromResult(_blockedDomains.ToList());
    }

    public Task<bool> IsDomainBlockedAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Task.FromResult(false);

        return Task.FromResult(_blockedDomains.Contains(domain.Trim().ToLower()));
    }

    private async Task<List<string>> ResolveDomainToIpsAsync(string domain)
    {
        var ipAddresses = new List<string>();

        try
        {
            // Проверка за кеш
            if (_domainToIpCache.TryGetValue(domain, out var cachedIps))
            {
                return cachedIps.Split(',').ToList();
            }

            // DNS резолване
            var hostEntry = await Dns.GetHostEntryAsync(domain);
            foreach (var ip in hostEntry.AddressList)
            {
                ipAddresses.Add(ip.ToString());
            }

            // Кеширане
            if (ipAddresses.Count > 0)
            {
                _domainToIpCache[domain] = string.Join(",", ipAddresses);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при резолване на домейн {domain}: {ex.Message}");
        }

        return ipAddresses;
    }
}

