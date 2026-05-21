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
                var uninstallResult = await UninstallAsync();
                if (!uninstallResult.Success)
                {
                    return uninstallResult; // Предаваме грешката (напр. 1072 след рестарт)
                }

                // Изчакване докато registry ключа на сервиза изчезне напълно (max 60 сек).
                // sc delete отчита SUCCESS, но ключът стои ако services.msc/Task Manager
                // държи отворен handle — sc create тогава пак дава 1072.
                _logger.LogInfo("Изчакване registry ключа на сервиза да се изчисти...");
                await WaitForServiceKeyRemovedAsync(timeoutMs: 60000);
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

            // Копиране на всички DLL файлове (вкл. от корена)
            if (!string.IsNullOrEmpty(sourceDir))
            {
                foreach (var dll in Directory.GetFiles(sourceDir, "*.dll"))
                {
                    var dllName = Path.GetFileName(dll);
                    var targetDll = Path.Combine(INSTALL_PATH, dllName);
                    File.Copy(dll, targetDll, true);
                }
                _logger.LogInfo("DLL файлове копирани");

                // Копиране на runtimes/ (System.ServiceProcess.ServiceController.dll и др.) – задължително за Windows Service
                string runtimesSource = Path.Combine(sourceDir, "runtimes");
                if (Directory.Exists(runtimesSource))
                {
                    CopyDirectory(runtimesSource, Path.Combine(INSTALL_PATH, "runtimes"));
                    _logger.LogInfo("Папка runtimes копирана");
                }

                // Копиране на RemoteDesktopHost/ – необходим за Remote Desktop функционалност
                string rdHostSource = Path.Combine(sourceDir, "RemoteDesktopHost");
                if (Directory.Exists(rdHostSource))
                {
                    CopyDirectory(rdHostSource, Path.Combine(INSTALL_PATH, "RemoteDesktopHost"));
                    _logger.LogInfo("Папка RemoteDesktopHost копирана");
                }
                else
                {
                    _logger.LogWarning("RemoteDesktopHost/ папката не е намерена до Monitor.exe – Remote Desktop няма да работи.");
                }

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

            // Конфигуриране: рестарт при грешка (след boot)
            ConfigureServiceFailureRecovery();

            // Стъпка 4: Стартиране на сървиза
            // Забавяне – Windows понякога нуждае време да регистрира сървиза след sc create
            _logger.LogInfo("Изчакване 3 секунди преди стартиране на сървиза...");
            await Task.Delay(3000);

            _logger.LogInfo("Стартиране на сървиза...");
            bool started = await StartServiceAsync();
            if (!started)
            {
                _logger.LogWarning("Сървизът е инсталиран, но незабавният старт е неуспешен. Опит за повторен старт след 5 сек...");
                await Task.Delay(5000);
                started = await StartServiceAsync();
            }
            if (!started)
            {
                _logger.LogWarning("Сървизът е инсталиран (start= auto). Ще се стартира при следващ рестарт на Windows. Можеш да го стартираш ръчно от Services.");
            }

            _logger.LogInfo("Инсталацията на Monitor завърши успешно!");
            return new InstallResult
            {
                Success = true,
                Message = started
                    ? "Monitor е инсталиран и стартиран успешно като Windows Service!"
                    : "Monitor е инсталиран. Сървизът ще се стартира при рестарт или го стартирай ръчно от Services."
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
            // Изчакване 10 сек – процесът трябва напълно да спре преди delete (иначе грешка 1072)
            _logger.LogInfo("Изчакване 10 секунди преди премахване...");
            await Task.Delay(10000);

            _logger.LogInfo("Деинсталиране на Monitor сървиз...");
            bool uninstalled = await UninstallServiceAsync();
            if (!uninstalled)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Услугата е маркирана за изтриване. Рестартирай компютъра и опитай инсталацията отново."
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
    /// Изчаква докато registry ключът на сервиза изчезне напълно след sc delete.
    /// Windows маркира сервиза за изтриване, но реалното премахване от registry
    /// се забавя докато всички handle-ове (services.msc, Task Manager...) се освободят.
    /// </summary>
    private async Task WaitForServiceKeyRemovedAsync(int timeoutMs = 60000)
    {
        const string keyPath = @"SYSTEM\CurrentControlSet\Services\" + SERVICE_NAME;
        int elapsed = 0;
        const int pollMs = 500;

        while (elapsed < timeoutMs)
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null)
            {
                _logger.LogInfo($"Registry ключ на сервиза е изчистен след {elapsed} мс — готов за sc create");
                return;
            }
            await Task.Delay(pollMs);
            elapsed += pollMs;

            if (elapsed % 5000 == 0)
                _logger.LogInfo($"Изчакване registry ключа да се изчисти... ({elapsed / 1000} сек)");
        }

        _logger.LogWarning($"Registry ключ на сервиза не е изчистен след {timeoutMs / 1000} сек. Опит за sc create въпреки това...");
    }

    /// <summary>
    /// Инсталира сървиза с sc.exe. Повторен опит при 1072 (mark for deletion).
    /// </summary>
    private async Task<bool> InstallServiceAsync(string exePath)
    {
        const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    // depend= Tcpip – изчаква мрежата преди старт | start= auto – автоматичен старт при boot
                    Arguments = $"create \"{SERVICE_NAME}\" binPath= \"\"{exePath}\"\" start= auto depend= Tcpip DisplayName= \"ADS Windows Authentication Monitor\"",
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

                        if (process.ExitCode == 0)
                        {
                            _logger.LogInfo($"Инсталация на сървиз успешна");
                            ConfigureServiceFailureRecovery();
                            return true;
                        }

                        if (process.ExitCode == ERROR_SERVICE_MARKED_FOR_DELETE && attempt < 3)
                        {
                            _logger.LogWarning($"sc create 1072 (mark for deletion). Изчакване 20 сек и опит {attempt + 1}/3...");
                            await Task.Delay(20000);
                            continue;
                        }

                        _logger.LogInfo($"Инсталация на сървиз неуспешна (Exit Code: {process.ExitCode})");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Грешка при инсталация на сървиз", ex);
                if (attempt < 3)
                {
                    await Task.Delay(5000);
                    continue;
                }
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Конфигурира рестарт при грешка (Failure recovery) – sc failure
    /// </summary>
    private void ConfigureServiceFailureRecovery()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"failure \"{SERVICE_NAME}\" reset= 86400 actions= restart/60000",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                    _logger.LogInfo("Failure recovery конфигуриран: рестарт след 60 сек при грешка");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Неуспешно конфигуриране на failure recovery: {ex.Message}");
        }
    }

    /// <summary>
    /// Деинсталира сървиза с sc.exe. При грешка 1072 (marked for deletion) – изчаква и опитва отново.
    /// </summary>
    private async Task<bool> UninstallServiceAsync()
    {
        const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;
        int maxRetries = 3;
        int waitSecondsBetweenRetries = 15;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
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

                        if (process.ExitCode == 0)
                            return true;

                        if (process.ExitCode == ERROR_SERVICE_MARKED_FOR_DELETE && attempt < maxRetries)
                        {
                            _logger.LogWarning($"Услугата е маркирана за изтриване. Опит {attempt}/{maxRetries}. Изчакване {waitSecondsBetweenRetries} секунди...");
                            await Task.Delay(waitSecondsBetweenRetries * 1000);
                            continue;
                        }

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Грешка при деинсталация на сървиз", ex);
                if (attempt == maxRetries) return false;
                await Task.Delay(waitSecondsBetweenRetries * 1000);
            }
        }

        return false;
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
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        _logger.LogWarning($"sc start неуспешен (Exit Code: {process.ExitCode}). Output: {output}. Error: {error}");
                    }
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
    /// Рекурсивно копиране на директория (за runtimes и др.)
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
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
