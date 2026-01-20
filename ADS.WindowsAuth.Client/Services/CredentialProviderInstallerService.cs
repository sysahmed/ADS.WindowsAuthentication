using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using System.Windows.Forms;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Сервис за инсталация на Credential Provider DLL
/// </summary>
public class CredentialProviderInstallerService
{
    private readonly ILoggerService _logger;
    private const string DLL_NAME = "ADS.WindowsAuth.CredentialProvider.dll";
    private const string INSTALL_PATH = @"C:\ADS";
    private const string CLSID = "{3E879088-249C-4C83-85B6-834A3A9C6D12}";

    public CredentialProviderInstallerService(ILoggerService logger)
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
    /// Rebuild-ва Credential Provider проекта автоматично
    /// </summary>
    public async Task<BuildResult> RebuildProjectAsync()
    {
        try
        {
            _logger.LogInfo("Започва автоматичен rebuild на Credential Provider проекта...");

            // Намиране на MSBuild
            string? msbuildPath = FindMsBuildPath();
            if (string.IsNullOrEmpty(msbuildPath))
            {
                return new BuildResult
                {
                    Success = false,
                    Message = "MSBuild не е намерен. Моля, инсталирай Visual Studio или Build Tools."
                };
            }

            // Намиране на проект файла
            string? projectPath = FindProjectFile();
            if (string.IsNullOrEmpty(projectPath))
            {
                return new BuildResult
                {
                    Success = false,
                    Message = "Credential Provider проект файлът не е намерен."
                };
            }

            _logger.LogInfo($"Използване на MSBuild: {msbuildPath}");
            _logger.LogInfo($"Rebuild на проект: {projectPath}");

            // Rebuild команда
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = msbuildPath,
                Arguments = $"\"{projectPath}\" /t:Rebuild /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process == null)
                {
                    return new BuildResult
                    {
                        Success = false,
                        Message = "Неуспешно стартиране на MSBuild процеса."
                    };
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                bool success = process.ExitCode == 0;
                
                if (!string.IsNullOrEmpty(output))
                {
                    _logger.LogInfo($"MSBuild Output: {output}");
                }
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogWarning($"MSBuild Errors: {error}");
                }

                if (success)
                {
                    _logger.LogInfo("Rebuild успешен!");
                    return new BuildResult
                    {
                        Success = true,
                        Message = "Rebuild успешен!"
                    };
                }
                else
                {
                    _logger.LogError($"Rebuild неуспешен (Exit Code: {process.ExitCode})");
                    return new BuildResult
                    {
                        Success = false,
                        Message = $"Rebuild неуспешен. Exit Code: {process.ExitCode}\n\nOutput: {output}\nErrors: {error}"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при rebuild на проекта", ex);
            return new BuildResult
            {
                Success = false,
                Message = $"Грешка при rebuild: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Намира пътя към MSBuild
    /// </summary>
    private string? FindMsBuildPath()
    {
        // Опит 1: vswhere (Visual Studio Installer)
        try
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string vswherePath = Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");
            
            if (File.Exists(vswherePath))
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = vswherePath,
                    Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process? process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();
                        
                        if (!string.IsNullOrEmpty(output) && File.Exists(output))
                        {
                            return output;
                        }
                    }
                }
            }
        }
        catch
        {
            // Продължаваме с fallback опции
        }

        // Fallback: Стандартни пътища
        string[] possiblePaths = new[]
        {
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
        };

        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Проверява дали сме на development машина (има source код и MSBuild)
    /// </summary>
    public bool IsDevelopmentMachine()
    {
        // Проверка за MSBuild
        string? msbuildPath = FindMsBuildPath();
        if (string.IsNullOrEmpty(msbuildPath))
        {
            return false;
        }

        // Проверка за проект файл
        string? projectPath = FindProjectFile();
        if (string.IsNullOrEmpty(projectPath))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Намира проект файла на Credential Provider
    /// </summary>
    private string? FindProjectFile()
    {
        string[] possiblePaths = new[]
        {
            // От клиента към проекта
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.CredentialProvider", "ADS.WindowsAuth.CredentialProvider.vcxproj"),
            Path.Combine(Application.StartupPath, "..", "ADS.WindowsAuth.CredentialProvider", "ADS.WindowsAuth.CredentialProvider.vcxproj"),
            
            // От solution root
            Path.Combine(Path.GetDirectoryName(Application.StartupPath) ?? "", "..", "ADS.WindowsAuth.CredentialProvider", "ADS.WindowsAuth.CredentialProvider.vcxproj"),
            
            // Абсолютни пътища (ако клиентът е в bin папката)
            Path.Combine(Application.StartupPath, "..", "..", "ADS.WindowsAuth.CredentialProvider", "ADS.WindowsAuth.CredentialProvider.vcxproj")
        };

        foreach (string path in possiblePaths)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    _logger.LogInfo($"Проект файл намерен: {fullPath}");
                    return fullPath;
                }
            }
            catch
            {
                // Продължаваме
            }
        }

        _logger.LogWarning("Проект файлът не е намерен в никоя от очакваните локации");
        return null;
    }

    /// <summary>
    /// Намира DLL файла в различни локации
    /// </summary>
    public string? FindDllFile()
    {
        var possiblePaths = new[]
        {
            // 1. В папката на клиента (най-често)
            Path.Combine(Application.StartupPath, DLL_NAME),
            Path.Combine(Application.StartupPath, "CredentialProvider", DLL_NAME),
            
            // 2. В bin папката на проекта (от solution root)
            Path.Combine(Application.StartupPath, "..", "..", "..", "ADS.WindowsAuth.CredentialProvider", "bin", "x64", "Release", DLL_NAME),
            Path.Combine(Application.StartupPath, "..", "..", "..", "bin", "x64", "Release", DLL_NAME),
            Path.Combine(Application.StartupPath, "..", "bin", "x64", "Release", DLL_NAME),
            
            // 3. В solution root bin папка
            Path.Combine(Path.GetDirectoryName(Application.StartupPath) ?? "", "..", "..", "bin", "x64", "Release", DLL_NAME),
            
            // 4. В Downloads и Desktop
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", DLL_NAME),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), DLL_NAME),
            
            // 5. В C:\ADS (ако вече е инсталиран)
            Path.Combine(INSTALL_PATH, DLL_NAME),
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    _logger.LogInfo($"DLL намерен: {fullPath}");
                    return fullPath;
                }
            }
            catch
            {
                // Продължаваме с следващия път
            }
        }

        _logger.LogWarning("DLL файлът не е намерен в никоя от очакваните локации");
        return null;
    }

    /// <summary>
    /// Проверява дали има по-нова версия на DLL файла
    /// </summary>
    public bool HasNewerVersion(string? dllPath = null)
    {
        try
        {
            string targetDll = Path.Combine(INSTALL_PATH, DLL_NAME);
            
            // Ако не е инсталиран, няма какво да обновяваме
            if (!File.Exists(targetDll))
            {
                return false;
            }

            // Намиране на source DLL
            if (string.IsNullOrEmpty(dllPath))
            {
                dllPath = FindDllFile();
                if (string.IsNullOrEmpty(dllPath))
                {
                    return false;
                }
            }

            if (!File.Exists(dllPath))
            {
                return false;
            }

            // Сравняване на датите на файловете
            FileInfo targetInfo = new FileInfo(targetDll);
            FileInfo sourceInfo = new FileInfo(dllPath);

            // Ако source файлът е по-нов от target файла
            return sourceInfo.LastWriteTime > targetInfo.LastWriteTime;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Инсталира или обновява Credential Provider DLL
    /// </summary>
    public async Task<InstallResult> InstallAsync(string? dllPath = null, bool forceUpdate = false)
    {
        try
        {
            // Проверка за администраторски права
            if (!IsRunningAsAdministrator())
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Приложението трябва да се стартира като администратор за инсталация!"
                };
            }

            // Намиране на DLL
            if (string.IsNullOrEmpty(dllPath))
            {
                dllPath = FindDllFile();
                if (string.IsNullOrEmpty(dllPath))
                {
                    return new InstallResult
                    {
                        Success = false,
                        Message = "DLL файлът не е намерен! Моля, копирай ADS.WindowsAuth.CredentialProvider.dll в папката на приложението."
                    };
                }
            }

            if (!File.Exists(dllPath))
            {
                return new InstallResult
                {
                    Success = false,
                    Message = $"DLL файлът не съществува: {dllPath}"
                };
            }

            string targetDll = Path.Combine(INSTALL_PATH, DLL_NAME);
            bool isUpdate = File.Exists(targetDll) && IsInstalled();
            bool hasNewerVersion = HasNewerVersion(dllPath);

            if (isUpdate && !forceUpdate && !hasNewerVersion)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Credential Provider вече е инсталиран и няма по-нова версия."
                };
            }

            _logger.LogInfo($"Започва {(isUpdate ? "обновяване" : "инсталация")} на Credential Provider от: {dllPath}");

            // Стъпка 1: Създаване на директория
            if (!Directory.Exists(INSTALL_PATH))
            {
                Directory.CreateDirectory(INSTALL_PATH);
                _logger.LogInfo($"Създадена директория: {INSTALL_PATH}");
            }

            // Стъпка 2: Отмяна на стара регистрация (ако съществува)
            if (File.Exists(targetDll))
            {
                _logger.LogInfo("Отмяна на стара регистрация...");
                await UnregisterDllAsync(targetDll);
                
                // По-дълго изчакване за освобождаване на файла
                _logger.LogInfo("Изчакване файлът да се освободи...");
                await Task.Delay(3000);
                
                // Опит за освобождаване на файла чрез преименуване
                string tempDll = targetDll + ".old";
                int retryCount = 0;
                const int maxRetries = 5;
                bool fileUnlocked = false;
                
                while (retryCount < maxRetries && !fileUnlocked)
                {
                    try
                    {
                        // Премахване на стария temp файл ако съществува
                        if (File.Exists(tempDll))
                        {
                            File.Delete(tempDll);
                        }
                        
                        // Преименуване на файла (по-надеждно от директно изтриване)
                        File.Move(targetDll, tempDll);
                        fileUnlocked = true;
                        _logger.LogInfo($"Старият DLL е преименуван в {tempDll}");
                        
                        // Изчакване малко преди да продължим
                        await Task.Delay(1000);
                    }
                    catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                    {
                        retryCount++;
                        _logger.LogWarning($"Опит {retryCount}/{maxRetries}: Файлът все още е заключен. Изчакване...");
                        await Task.Delay(2000 * retryCount); // Exponential backoff
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Грешка при преименуване на файла: {ex.Message}");
                        retryCount++;
                        await Task.Delay(2000);
                    }
                }
                
                if (!fileUnlocked)
                {
                    // Ако не можем да преименуваме, опитваме се да изтрием директно
                    _logger.LogWarning("Неуспешно преименуване. Опит за директно изтриване...");
                    try
                    {
                        File.Delete(targetDll);
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Неуспешно изтриване на файла: {ex.Message}");
                        return new InstallResult
                        {
                            Success = false,
                            Message = $"Файлът е заключен от друг процес и не може да бъде заменен.\n\n" +
                                     $"Моля:\n" +
                                     $"1. Рестартирай компютъра\n" +
                                     $"2. Или затвори всички прозорци на Windows Explorer\n" +
                                     $"3. Или изчакай малко и опитай отново\n\n" +
                                     $"Детайли: {ex.Message}"
                        };
                    }
                }
            }

            // Стъпка 3: Копиране на DLL
            _logger.LogInfo($"Копиране на DLL в {targetDll}...");
            try
            {
                File.Copy(dllPath, targetDll, true);
                _logger.LogInfo("DLL копиран успешно");
                
                // Премахване на temp файла ако съществува
                string tempDll = targetDll + ".old";
                if (File.Exists(tempDll))
                {
                    try
                    {
                        File.Delete(tempDll);
                        _logger.LogInfo("Старият temp файл е премахнат");
                    }
                    catch
                    {
                        _logger.LogWarning("Неуспешно премахване на temp файла (ще бъде премахнат при следващ рестарт)");
                    }
                }
            }
            catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
            {
                return new InstallResult
                {
                    Success = false,
                    Message = $"Файлът е заключен от друг процес и не може да бъде заменен.\n\n" +
                             $"Моля:\n" +
                             $"1. Рестартирай компютъра\n" +
                             $"2. Или затвори всички прозорци на Windows Explorer\n" +
                             $"3. Или изчакай малко и опитай отново\n\n" +
                             $"Детайли: {ioEx.Message}"
                };
            }

            // Стъпка 4: Регистрация на DLL
            _logger.LogInfo("Регистрация на DLL...");
            bool registered = await RegisterDllAsync(targetDll);
            if (!registered)
            {
                return new InstallResult
                {
                    Success = false,
                    Message = "Грешка при регистрация на DLL! Провери Event Viewer за детайли."
                };
            }

            // Стъпка 5: Конфигуриране на Registry
            _logger.LogInfo("Конфигуриране на Registry...");
            ConfigureRegistry();

            _logger.LogInfo($"{(isUpdate ? "Обновяването" : "Инсталацията")} завърши успешно!");
            return new InstallResult
            {
                Success = true,
                Message = isUpdate 
                    ? "Credential Provider е обновен успешно! Рестартирай компютъра за да влезе в сила."
                    : "Credential Provider е инсталиран успешно! Рестартирай компютъра за да влезе в сила."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при инсталация", ex);
            return new InstallResult
            {
                Success = false,
                Message = $"Грешка при инсталация: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Регистрира DLL с regsvr32
    /// </summary>
    private async Task<bool> RegisterDllAsync(string dllPath)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "regsvr32.exe",
                Arguments = $"/s \"{dllPath}\"",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    bool success = process.ExitCode == 0;
                    _logger.LogInfo($"Регистрация {(success ? "успешна" : "неуспешна")} (Exit Code: {process.ExitCode})");
                    return success;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при регистрация на DLL", ex);
            return false;
        }
    }

    /// <summary>
    /// Отменя регистрацията на DLL
    /// </summary>
    private async Task<bool> UnregisterDllAsync(string dllPath)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "regsvr32.exe",
                Arguments = $"/u /s \"{dllPath}\"",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(psi))
            {
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return true; // Не проверяваме exit code при отмяна
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при отмяна на регистрация: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Конфигурира Registry за ServiceUrl
    /// </summary>
    private void ConfigureRegistry()
    {
        try
        {
            string registryPath = @"SOFTWARE\ADS\WindowsAuth";
            using (RegistryKey? key = Registry.LocalMachine.CreateSubKey(registryPath, true))
            {
                if (key != null)
                {
                    // Четене на API URL от конфигурация
                    string apiUrl = LoadApiUrlFromConfig();
                    key.SetValue("ServiceUrl", apiUrl, RegistryValueKind.String);
                    _logger.LogInfo($"ServiceUrl конфигуриран: {apiUrl}");
                }
            }

            // Проверка за Authentication Registry ключ
            string authPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{CLSID}";
            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(authPath, true))
            {
                if (key == null)
                {
                    // Създаване на ключа ако не съществува
                    using (RegistryKey? newKey = Registry.LocalMachine.CreateSubKey(authPath, true))
                    {
                        if (newKey != null)
                        {
                            newKey.SetValue("", "ADS Windows Auth Credential Provider", RegistryValueKind.String);
                            _logger.LogInfo("Authentication Registry ключ създаден");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при конфигуриране на Registry", ex);
            throw;
        }
    }

    /// <summary>
    /// Зарежда API URL от конфигурация
    /// </summary>
    private string LoadApiUrlFromConfig()
    {
        string? url = null;
        
        // Първо проверяваме за Development конфигурация (ако е Debug режим)
        #if DEBUG
        try
        {
            string devConfigPath = Path.Combine(Application.StartupPath, "appsettings.Development.json");
            if (File.Exists(devConfigPath))
            {
                string json = File.ReadAllText(devConfigPath);
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("ApiConfiguration", out var apiConfig))
                    {
                        if (apiConfig.TryGetProperty("BaseUrl", out var baseUrl))
                        {
                            url = baseUrl.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                _logger.LogInfo($"Зареден API URL от Development конфигурация: {url}");
                                return url;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при зареждане на Development конфигурация: {ex.Message}");
        }
        #endif
        
        // След това проверяваме основния appsettings.json
        try
        {
            string configPath = Path.Combine(Application.StartupPath, "appsettings.json");
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("ApiConfiguration", out var apiConfig))
                    {
                        if (apiConfig.TryGetProperty("BaseUrl", out var baseUrl))
                        {
                            url = baseUrl.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                _logger.LogInfo($"Зареден API URL от конфигурация: {url}");
                                return url;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при зареждане на конфигурация: {ex.Message}. Използвам default URL.");
        }

        // Default стойност - ако няма конфигурация, използваме production URL
        string defaultUrl = "https://ads-auth.nursanbulgaria.com";
        _logger.LogInfo($"Използвам default API URL: {defaultUrl}");
        return defaultUrl;
    }

    /// <summary>
    /// Проверява дали Credential Provider е инсталиран
    /// </summary>
    public bool IsInstalled()
    {
        string dllPath = Path.Combine(INSTALL_PATH, DLL_NAME);
        if (!File.Exists(dllPath))
            return false;

        // Проверка за Registry регистрация
        string clsidPath = $@"SOFTWARE\Classes\CLSID\{CLSID}";
        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(clsidPath))
        {
            return key != null;
        }
    }
}

/// <summary>
/// Резултат от инсталация
/// </summary>
public class InstallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Резултат от build операция
/// </summary>
public class BuildResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

