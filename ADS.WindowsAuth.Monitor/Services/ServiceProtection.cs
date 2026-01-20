using System.Diagnostics;
using System.ServiceProcess;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Monitor.Services;

/// <summary>
/// Сервис за защита на Monitor Service - автоматично рестартиране при спиране
/// </summary>
public class ServiceProtection
{
    private readonly ILoggerService _logger;
    private readonly string _serviceName = "ADS.WindowsAuth.Monitor";
    private readonly Timer _monitorTimer;
    private bool _isMonitoring = false;

    public ServiceProtection(ILoggerService logger)
    {
        _logger = logger;
        _monitorTimer = new Timer(CheckServiceStatus, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Стартиране на мониторинга
    /// </summary>
    public void StartMonitoring()
    {
        if (_isMonitoring)
            return;

        _isMonitoring = true;
        _monitorTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)); // Проверка на всяка минута
        _logger.LogInfo("Service Protection мониторингът е стартиран");
    }

    /// <summary>
    /// Спиране на мониторинга
    /// </summary>
    public void StopMonitoring()
    {
        _isMonitoring = false;
        _monitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInfo("Service Protection мониторингът е спрян");
    }

    /// <summary>
    /// Проверка на статуса на сервиса
    /// </summary>
    private void CheckServiceStatus(object? state)
    {
        try
        {
            using var service = new ServiceController(_serviceName);
            service.Refresh();

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogWarning($"⚠️ Monitor Service е спрян! Опит за автоматично рестартиране...");
                
                try
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    _logger.LogInfo("Monitor Service е успешно рестартиран");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Грешка при рестартиране на Monitor Service: {ex.Message}", ex);
                    
                    // Опит за рестартиране чрез PowerShell (ако има права)
                    try
                    {
                        RestartServiceViaPowerShell();
                    }
                    catch
                    {
                        _logger.LogError("Неуспешен опит за рестартиране чрез PowerShell");
                    }
                }
            }
            else if (service.Status == ServiceControllerStatus.Paused)
            {
                _logger.LogWarning($"Monitor Service е на пауза. Опит за възобновяване...");
                try
                {
                    service.Continue();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    _logger.LogInfo("Monitor Service е успешно възобновен");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Грешка при възобновяване на Monitor Service: {ex.Message}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка на статуса на сервиса: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Рестартиране на сервиса чрез PowerShell (ако има права)
    /// </summary>
    private void RestartServiceViaPowerShell()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-Command \"Restart-Service -Name '{_serviceName}' -Force\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Verb = "runas" // Изпълнение като администратор
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    _logger.LogInfo("Monitor Service е рестартиран чрез PowerShell");
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    _logger.LogWarning($"Грешка при рестартиране чрез PowerShell: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при рестартиране чрез PowerShell: {ex.Message}", ex);
        }
    }
}

