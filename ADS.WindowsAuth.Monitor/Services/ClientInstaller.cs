using System.Diagnostics;
using System.IO;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Monitor.Services;

/// <summary>
/// Сервис за инсталация на Windows Forms клиент
/// </summary>
public class ClientInstaller
{
    private readonly ILoggerService _logger;
    private readonly string _applicationDirectory;
    /// <summary>Последен път когато логнахме "Клиент не е намерен" – за да не запълваме логовете на всеки 5 мин.</summary>
    private DateTime? _lastSourceNotFoundLoggedUtc;
    private static readonly TimeSpan SourceNotFoundLogInterval = TimeSpan.FromHours(24);

    public ClientInstaller(ILoggerService logger, string applicationDirectory)
    {
        _logger = logger;
        _applicationDirectory = applicationDirectory;
    }

    /// <summary>
    /// Проверява и инсталира клиента ако е необходимо
    /// </summary>
    public bool CheckAndInstall()
    {
        try
        {
            const string INSTALL_PATH = @"C:\ADS\Client";
            string TARGET_EXE = Path.Combine(INSTALL_PATH, "ADS.WindowsAuth.Client.exe");

            // Проверка дали е вече инсталиран
            if (File.Exists(TARGET_EXE))
            {
                _lastSourceNotFoundLoggedUtc = null; // успех – нулираме за следващи опити
                var targetFileInfo = new FileInfo(TARGET_EXE);
                var sourceExePath = FindSourceClient();

                if (!string.IsNullOrEmpty(sourceExePath))
                {
                    var sourceFileInfo = new FileInfo(sourceExePath);
                    // Ако source файлът е по-нов, обновяваме
                    if (sourceFileInfo.LastWriteTime > targetFileInfo.LastWriteTime)
                    {
                        _logger.LogInfo("Намерен по-нов клиент. Обновяване...");
                        return InstallClient();
                    }
                }

                return true;
            }

            // Клиентът не е инсталиран – проверяваме дали изобщо има source
            var sourcePath = FindSourceClient();
            if (string.IsNullOrEmpty(sourcePath))
            {
                if (_lastSourceNotFoundLoggedUtc.HasValue && DateTime.UtcNow - _lastSourceNotFoundLoggedUtc.Value < SourceNotFoundLogInterval)
                    return false; // без повторно логване
                _lastSourceNotFoundLoggedUtc = DateTime.UtcNow;
                _logger.LogInfo("Клиент не е инсталиран. Инсталиране...");
                _logger.LogWarning("Клиент не е намерен. Пропускане на инсталация. (Следващо съобщение след 24 ч.)");
                return false;
            }

            _lastSourceNotFoundLoggedUtc = null;
            _logger.LogInfo("Клиент не е инсталиран. Инсталиране...");
            return InstallClient();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка на клиент: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Търси source клиент файл
    /// </summary>
    private string? FindSourceClient()
    {
        string[] possiblePaths = new[]
        {
            Path.Combine(_applicationDirectory, "Client", "ADS.WindowsAuth.Client.exe"),
            Path.Combine(_applicationDirectory, "ADS.WindowsAuth.Client.exe"),
            Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "ADS.WindowsAuth.Client", "bin", "Release", "net8.0-windows", "ADS.WindowsAuth.Client.exe"),
            Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "ADS.WindowsAuth.Client", "bin", "x64", "Release", "net8.0-windows", "ADS.WindowsAuth.Client.exe"),
            Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "bin", "Release", "net8.0-windows", "ADS.WindowsAuth.Client.exe"),
            @"C:\ADS\Client\ADS.WindowsAuth.Client.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Инсталира клиента
    /// </summary>
    public bool InstallClient()
    {
        try
        {
            const string INSTALL_PATH = @"C:\ADS\Client";
            string TARGET_EXE = Path.Combine(INSTALL_PATH, "ADS.WindowsAuth.Client.exe");

            // Търсене на клиент
            string? sourceExePath = FindSourceClient();

            if (string.IsNullOrEmpty(sourceExePath))
            {
                _logger.LogWarning("Клиент не е намерен. Пропускане на инсталация.");
                return false;
            }

            _logger.LogInfo($"Намерен клиент: {sourceExePath}");

            // Стъпка 1: Създаване на директория
            if (!Directory.Exists(INSTALL_PATH))
            {
                Directory.CreateDirectory(INSTALL_PATH);
                _logger.LogInfo($"Създадена директория: {INSTALL_PATH}");
            }

            // Стъпка 2: Копиране на файлове
            if (sourceExePath != TARGET_EXE)
            {
                _logger.LogInfo($"Копиране на клиент в {INSTALL_PATH}...");
                try
                {
                    // Копиране на exe
                    if (File.Exists(TARGET_EXE))
                    {
                        // Опит за спиране на процеса ако работи
                        try
                        {
                            var processes = Process.GetProcessesByName("ADS.WindowsAuth.Client");
                            foreach (var process in processes)
                            {
                                process.Kill();
                                process.WaitForExit(5000);
                            }
                        }
                        catch { }

                        File.Delete(TARGET_EXE);
                    }

                    File.Copy(sourceExePath, TARGET_EXE, true);
                    _logger.LogInfo("Клиент копиран успешно");

                    // Копиране на допълнителни файлове (DLLs, config)
                    var sourceDir = Path.GetDirectoryName(sourceExePath);
                    if (!string.IsNullOrEmpty(sourceDir))
                    {
                        // Копиране на DLLs
                        foreach (var dll in Directory.GetFiles(sourceDir, "*.dll"))
                        {
                            var dllName = Path.GetFileName(dll);
                            var targetDll = Path.Combine(INSTALL_PATH, dllName);
                            File.Copy(dll, targetDll, true);
                        }

                    // Копиране на config файлове
                    foreach (var config in Directory.GetFiles(sourceDir, "*.json"))
                    {
                        var configName = Path.GetFileName(config);
                        var targetConfig = Path.Combine(INSTALL_PATH, configName);
                        File.Copy(config, targetConfig, true);
                    }

                    // Копиране на Credential Provider DLL ако съществува в папката на клиента
                    string credentialProviderDll = Path.Combine(sourceDir, "ADS.WindowsAuth.CredentialProvider.dll");
                    if (File.Exists(credentialProviderDll))
                    {
                        string targetCredentialProviderDll = Path.Combine(INSTALL_PATH, "ADS.WindowsAuth.CredentialProvider.dll");
                        File.Copy(credentialProviderDll, targetCredentialProviderDll, true);
                        _logger.LogInfo("Credential Provider DLL копиран от папката на клиента");
                    }
                }
            }
                catch (Exception ex)
                {
                    _logger.LogError($"Грешка при копиране на клиент: {ex.Message}");
                    return false;
                }
            }

            // Стъпка 3: Създаване на shortcut на Desktop (опционално)
            try
            {
                CreateDesktopShortcut(TARGET_EXE);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не може да се създаде shortcut: {ex.Message}");
            }

            // Стъпка 4: Автозареждане при login – за да работи InputCapture (клавиши/кликове) в потребителска сесия
            try
            {
                CreateStartupShortcut(TARGET_EXE);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Не може да се добави в автозареждане: {ex.Message}");
            }

            _logger.LogInfo("Клиент е инсталиран успешно!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при инсталация на клиент: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Осигурява shortcut за автозареждане (All Users Startup). Ако липсва – създава го.
    /// </summary>
    private void EnsureStartupShortcut(string exePath)
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        string shortcutPath = Path.Combine(startupFolder, "ADS Windows Auth Client.lnk");
        if (!Directory.Exists(startupFolder) || !File.Exists(exePath)) return;
        if (File.Exists(shortcutPath)) return; // вече има shortcut
        CreateStartupShortcut(exePath);
    }

    /// <summary>
    /// Създава shortcut в All Users Startup – Client ще стартира при login за всички потребители.
    /// </summary>
    private void CreateStartupShortcut(string exePath)
    {
        try
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            string shortcutPath = Path.Combine(startupFolder, "ADS Windows Auth Client.lnk");

            string vbsScript = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")
sLinkFile = ""{shortcutPath}""
Set oLink = oWS.CreateShortcut(sLinkFile)
oLink.TargetPath = ""{exePath}""
oLink.WorkingDirectory = ""{Path.GetDirectoryName(exePath)}""
oLink.Description = ""ADS Windows Authentication Client""
oLink.WindowStyle = 7
oLink.Save
";
            string tempVbs = Path.GetTempFileName() + ".vbs";
            File.WriteAllText(tempVbs, vbsScript);
            using (var proc = Process.Start("cscript.exe", $"//Nologo \"{tempVbs}\""))
            {
                proc?.WaitForExit(5000);
            }
            try { File.Delete(tempVbs); } catch { }
            _logger.LogInfo("Shortcut за автозареждане създаден (All Users Startup) – InputCapture ще работи при login.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при създаване на shortcut за автозареждане: {ex.Message}");
        }
    }

    /// <summary>
    /// Създава shortcut на Desktop
    /// </summary>
    private void CreateDesktopShortcut(string exePath)
    {
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string shortcutPath = Path.Combine(desktopPath, "ADS Windows Auth Client.lnk");

            // Използване на WScript за създаване на shortcut
            string vbsScript = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")
sLinkFile = ""{shortcutPath}""
Set oLink = oWS.CreateShortcut(sLinkFile)
oLink.TargetPath = ""{exePath}""
oLink.WorkingDirectory = ""{Path.GetDirectoryName(exePath)}""
oLink.Description = ""ADS Windows Authentication Client""
oLink.Save
";

            string tempVbs = Path.GetTempFileName() + ".vbs";
            File.WriteAllText(tempVbs, vbsScript);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cscript.exe",
                Arguments = $"//Nologo \"{tempVbs}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(startInfo))
            {
                process?.WaitForExit(5000);
            }

            File.Delete(tempVbs);
            _logger.LogInfo("Shortcut създаден на Desktop");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при създаване на shortcut: {ex.Message}");
        }
    }
}

