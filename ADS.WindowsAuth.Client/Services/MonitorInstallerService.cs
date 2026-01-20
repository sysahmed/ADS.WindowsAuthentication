using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Сервис за инсталация на Monitor сервиз
/// </summary>
public class MonitorInstallerService
{
    private readonly ILoggerService _logger;
    private const string SERVICE_NAME = "ADS.WindowsAuth.Monitor";
    private const string INSTALL_PATH = @"C:\ADS\Monitor";
    private const string EXE_NAME = "ADS.WindowsAuth.Monitor.exe";

    public MonitorInstallerService(ILoggerService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Проверява дали приложението работи като администратор
    /// </summary>
    public bool IsRunningAsAdministrator()
    {
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Намира Monitor EXE файла в различни локации
    /// </summary>
    public string? FindMonitorExe()
    {
        var possiblePaths = new[]
        {
            // 1. В папката на клиента (най-често - автоматично копиран при build)
            Path.Combine(Application.StartupPath, EXE_NAME),
            Path.Combine(Application.StartupPath, "Monitor", EXE_NAME),

            // 2. В bin папката на Monitor проекта
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.Monitor", "bin", "Release", "net8.0-windows8.0", EXE_NAME),
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.Monitor", "bin", "Debug", "net8.0-windows8.0", EXE_NAME),
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.Monitor", "bin", "Release", "net8.0", EXE_NAME),
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.Monitor", "bin", "Debug", "net8.0", EXE_NAME),
            Path.Combine(Application.StartupPath, "..", "bin", "Release", "net8.0-windows8.0", EXE_NAME),
            Path.Combine(Application.StartupPath, "..", "bin", "Debug", "net8.0-windows8.0", EXE_NAME),

            // 3. В Downloads и Desktop
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", EXE_NAME),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), EXE_NAME),

            // 4. В C:\ADS\Monitor (ако вече е инсталиран)
            Path.Combine(INSTALL_PATH, EXE_NAME),
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    _logger.LogInfo($"Monitor EXE намерен: {fullPath}");
                    return fullPath;
                }
            }
            catch
            {
                // Продължаваме с следващия път
            }
        }

        _logger.LogWarning("Monitor EXE файлът не е намерен в никоя от очакваните локации");
        return null;
    }

    /// <summary>
    /// Инсталира Monitor като Windows Service
    /// </summary>
    public async Task<InstallResult> InstallAsync(string? exePath = null)
    {
        try
        {
            // Проверка за администраторски права
            if (!IsRunningAsAdministrator())
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Приложението трябва да се стартира като администратор за инсталация на сервиз!"
                };
            }

            // Намиране на EXE
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = FindMonitorExe();
                if (string.IsNullOrEmpty(exePath))
                {
                    return new InstallResult
                    {
                        Success = false,
                        Message = "Monitor EXE файлът не е намерен! Моля, копирай ADS.WindowsAuth.Monitor.exe в папката на приложението."
                    };
                }
            }

            if (!File.Exists(exePath))
            {
                return new InstallResult
                {
                    Success = false,
                    Message = $"Monitor EXE файлът не съществува: {exePath}"
                };
            }

            _logger.LogInfo($"Започва инсталация на Monitor от: {exePath}");

            // Проверка дали вече е инсталиран
            if (IsServiceInstalled())
            {
                _logger.LogInfo("Monitor сервизът вече е инсталиран. Спиране и премахване...");
                await UninstallAsync();
                await Task.Delay(2000); // Изчакване на деинсталация
            }

            // Стъпка 1: Създаване на директория
            if (!Directory.Exists(INSTALL_PATH))
            {
                Directory.CreateDirectory(INSTALL_PATH);
                _logger.LogInfo($"Създадена директория: {INSTALL_PATH}");
            }

            // Стъпка 2: Копиране на файлове
            string targetExe = Path.Combine(INSTALL_PATH, EXE_NAME);
            string sourceDir = Path.GetDirectoryName(exePath) ?? "";

            _logger.LogInfo($"Копиране на Monitor в {INSTALL_PATH}...");

            // Копиране на EXE
            File.Copy(exePath, targetExe, true);
            _logger.LogInfo("Monitor EXE копиран успешно");

            // Копиране на всички DLL файлове
            if (!string.IsNullOrEmpty(sourceDir))
            {
                foreach (var dll in Directory.GetFiles(sourceDir, "*.dll"))
                {
                    var dllName = Path.GetFileName(dll);
                    var targetDll = Path.Combine(INSTALL_PATH, dllName);
                    File.Copy(dll, targetDll, true);
                }
                _logger.LogInfo("DLL файлове копирани");

                // Копиране на конфигурационни файлове
                foreach (var config in Directory.GetFiles(sourceDir, "*.json"))
                {
                    var configName = Path.GetFileName(config);
                    var targetConfig = Path.Combine(INSTALL_PATH, configName);
                    File.Copy(config, targetConfig, true);
                }
                _logger.LogInfo("Конфигурационни файлове копирани");
            }

            // Стъпка 3: Инсталиране като Windows Service с sc.exe
            _logger.LogInfo("Инсталиране на Windows Service...");
            bool installed = await InstallServiceAsync(targetExe);
            if (!installed)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Грешка при инсталиране на Windows Service!"
                };
            }

            // Стъпка 4: Стартиране на сървиза
            _logger.LogInfo("Стартиране на сървиза...");
            bool started = await StartServiceAsync();
            if (!started)
            {
                _logger.LogWarning("Сървизът е инсталиран, но не може да се стартира автоматично. Стартирай го ръчно от Services.");
            }

            _logger.LogInfo("Инсталацията на Monitor завърши успешно!");
            return new InstallResult
            {
                Success = true,
                Message = "Monitor е инсталиран и стартиран успешно като Windows Service!"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при инсталация на Monitor", ex);
            return new InstallResult
            {
                Success = false,
                Message = $"Грешка при инсталация: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Деинсталира Monitor сървиза
    /// </summary>
    public async Task<InstallResult> UninstallAsync()
    {
        try
        {
            if (!IsRunningAsAdministrator())
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Приложението трябва да се стартира като администратор за деинсталация!"
                };
            }

            if (!IsServiceInstalled())
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Monitor сървизът не е инсталиран."
                };
            }

            _logger.LogInfo("Спиране на Monitor сървиз...");
            await StopServiceAsync();
            await Task.Delay(2000);

            _logger.LogInfo("Деинсталиране на Monitor сървиз...");
            bool uninstalled = await UninstallServiceAsync();
            if (!uninstalled)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Грешка при деинсталиране на сървиза!"
                };
            }

            _logger.LogInfo("Monitor е деинсталиран успешно!");
            return new InstallResult
            {
                Success = true,
                Message = "Monitor е деинсталиран успешно!"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при деинсталация на Monitor", ex);
            return new InstallResult
            {
                Success = false,
                Message = $"Грешка при деинсталация: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Инсталира сървиза с sc.exe
    /// </summary>
    private async Task<bool> InstallServiceAsync(string exePath)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"create \"{SERVICE_NAME}\" binPath= \"\"{exePath}\"\" start= auto DisplayName= \"ADS Windows Authentication Monitor\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    _logger.LogInfo($"sc create output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogWarning($"sc create error: {error}");
                    }

                    bool success = process.ExitCode == 0;
                    _logger.LogInfo($"Инсталация на сървиз {(success ? "успешна" : "неуспешна")} (Exit Code: {process.ExitCode})");
                    return success;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при инсталация на сървиз", ex);
            return false;
        }
    }

    /// <summary>
    /// Деинсталира сървиза с sc.exe
    /// </summary>
    private async Task<bool> UninstallServiceAsync()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete \"{SERVICE_NAME}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    _logger.LogInfo($"sc delete output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogWarning($"sc delete error: {error}");
                    }

                    return process.ExitCode == 0;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при деинсталация на сървиз", ex);
            return false;
        }
    }

    /// <summary>
    /// Стартира сървиза
    /// </summary>
    private async Task<bool> StartServiceAsync()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"start \"{SERVICE_NAME}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при стартиране на сървиз", ex);
            return false;
        }
    }

    /// <summary>
    /// Спира сървиза
    /// </summary>
    private async Task<bool> StopServiceAsync()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"stop \"{SERVICE_NAME}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return true; // Не проверяваме exit code при спиране
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при спиране на сървиз: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Проверява дали сървизът е инсталиран
    /// </summary>
    public bool IsServiceInstalled()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"query \"{SERVICE_NAME}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
