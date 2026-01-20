using System.Diagnostics;
using System.IO;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Monitor.Services;

/// <summary>
/// Сервис за инсталация и регистрация на Credential Provider DLL
/// </summary>
public class CredentialProviderInstaller
{
    private readonly ILoggerService _logger;
    private readonly string _applicationDirectory;

    public CredentialProviderInstaller(ILoggerService logger, string applicationDirectory)
    {
        _logger = logger;
        _applicationDirectory = applicationDirectory;
    }

    /// <summary>
    /// Проверява и инсталира Credential Provider ако е необходимо
    /// </summary>
    public bool CheckAndInstall()
    {
        try
        {
            const string TARGET_DLL = @"C:\ADS\ADS.WindowsAuth.CredentialProvider.dll";
            const string CLSID = "{3E879088-249C-4C83-85B6-834A3A9C6D12}";

            // Проверка дали е вече инсталиран и регистриран
            if (File.Exists(TARGET_DLL) && IsRegistered(CLSID))
            {
                // Проверка дали файлът е актуален (по размер и дата)
                var targetFileInfo = new FileInfo(TARGET_DLL);
                var sourceDllPath = FindSourceDll();
                
                if (!string.IsNullOrEmpty(sourceDllPath) && sourceDllPath != TARGET_DLL)
                {
                    var sourceFileInfo = new FileInfo(sourceDllPath);
                    
                    // Проверка дали файлът е различен (по размер и дата)
                    bool isDifferent = sourceFileInfo.Length != targetFileInfo.Length ||
                                      sourceFileInfo.LastWriteTime > targetFileInfo.LastWriteTime.AddSeconds(5);
                    
                    if (isDifferent)
                    {
                        // Проверка дали файлът е заключен (зареден в паметта)
                        if (IsFileLocked(TARGET_DLL))
                        {
                            // Файлът е заключен - Credential Provider е активен
                            // Не се опитваме да го обновяваме, защото това е нормално поведение
                            // Логваме само ако файлът е значително по-стар (повече от 1 ден)
                            if (sourceFileInfo.LastWriteTime > targetFileInfo.LastWriteTime.AddDays(1))
                            {
                                _logger.LogWarning($"Намерен по-нов DLL файл, но текущият е зареден в паметта. Обновяването ще се извърши при следващ рестарт.");
                            }
                            return true; // Всичко е наред, просто файлът е зареден
                        }
                        
                        // Файлът не е заключен - можем да го обновим
                        _logger.LogInfo("Намерен по-нов DLL файл. Обновяване...");
                        return RegisterCredentialProvider();
                    }
                }
                
                // Всичко е наред
                return true;
            }
            else
            {
                // Не е инсталиран - инсталираме
                _logger.LogInfo("Credential Provider не е инсталиран. Инсталиране...");
                return RegisterCredentialProvider();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка на Credential Provider: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Проверява дали файлът е заключен (използван от друг процес)
    /// </summary>
    private bool IsFileLocked(string filePath)
    {
        try
        {
            using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Ако можем да отворим файла с FileShare.None, значи не е заключен
                return false;
            }
        }
        catch (IOException)
        {
            // Файлът е заключен
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Нямаме права - приемаме че е заключен
            return true;
        }
    }

    /// <summary>
    /// Търси source DLL файл
    /// </summary>
    private string? FindSourceDll()
    {
        string[] possiblePaths = new[]
        {
            // 1. В папката на Monitor Service (най-предпочитано)
            Path.Combine(_applicationDirectory, "CredentialProvider", "ADS.WindowsAuth.CredentialProvider.dll"),
            Path.Combine(_applicationDirectory, "ADS.WindowsAuth.CredentialProvider.dll"),
            
            // 2. В папката на клиента (ако е инсталиран там - най-често срещано)
            @"C:\ADS\Client\ADS.WindowsAuth.CredentialProvider.dll",
            Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "Client", "ADS.WindowsAuth.CredentialProvider.dll"),
            
            // 3. В bin папките на проекта
            Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "ADS.WindowsAuth.CredentialProvider", "bin", "x64", "Release", "ADS.WindowsAuth.CredentialProvider.dll"),
            Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "bin", "x64", "Release", "ADS.WindowsAuth.CredentialProvider.dll"),
            
            // 4. Вече инсталираният (за проверка)
            @"C:\ADS\ADS.WindowsAuth.CredentialProvider.dll"
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
    /// Инсталира и регистрира Credential Provider DLL автоматично
    /// </summary>
    public bool RegisterCredentialProvider()
    {
        try
        {
            const string INSTALL_PATH = @"C:\ADS";
            string TARGET_DLL = Path.Combine(INSTALL_PATH, "ADS.WindowsAuth.CredentialProvider.dll");
            const string CLSID = "{3E879088-249C-4C83-85B6-834A3A9C6D12}";

            // Търсене на DLL
            string? sourceDllPath = FindSourceDll();

            if (string.IsNullOrEmpty(sourceDllPath))
            {
                _logger.LogWarning("Credential Provider DLL не е намерен. Пропускане на инсталация.");
                return false;
            }

            _logger.LogInfo($"Намерен Credential Provider DLL: {sourceDllPath}");

            // Стъпка 1: Създаване на директория
            if (!Directory.Exists(INSTALL_PATH))
            {
                Directory.CreateDirectory(INSTALL_PATH);
                _logger.LogInfo($"Създадена директория: {INSTALL_PATH}");
            }

            // Стъпка 2: Отмяна на стара регистрация (ако съществува)
            if (File.Exists(TARGET_DLL))
            {
                _logger.LogInfo("Отмяна на стара регистрация...");
                UnregisterDll(TARGET_DLL);
                System.Threading.Thread.Sleep(1000);
            }

            // Стъпка 3: Копиране на DLL (ако не е вече там)
            if (sourceDllPath != TARGET_DLL)
            {
                // Проверка дали файлът е заключен
                if (File.Exists(TARGET_DLL) && IsFileLocked(TARGET_DLL))
                {
                    _logger.LogWarning($"DLL файлът е зареден в паметта и не може да се обнови в момента. Обновяването ще се извърши при следващ рестарт.");
                    // Не връщаме false - файлът е инсталиран, просто не можем да го обновим сега
                    // Продължаваме с регистрацията ако е необходимо
                }
                else
                {
                    _logger.LogInfo($"Копиране на DLL в {TARGET_DLL}...");
                    try
                    {
                        if (File.Exists(TARGET_DLL))
                        {
                            // Опитваме се да изтрием файла
                            try
                            {
                                File.Delete(TARGET_DLL);
                            }
                            catch (IOException ex)
                            {
                                // Файлът може да е бил заключен между проверката и опита за изтриване
                                _logger.LogWarning($"Не може да се изтрие стар DLL файл (вероятно е зареден в паметта): {ex.Message}");
                                // Продължаваме - File.Copy с overwrite=true ще опита да презапише
                            }
                        }
                        File.Copy(sourceDllPath, TARGET_DLL, true);
                        _logger.LogInfo("DLL копиран успешно");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning($"Няма права за копиране на DLL: {ex.Message}. Обновяването ще се извърши при следващ рестарт.");
                        // Не връщаме false - файлът може да е вече инсталиран
                    }
                    catch (IOException ex)
                    {
                        // Файлът е заключен или достъпът е отказан
                        _logger.LogWarning($"DLL файлът не може да се копира (вероятно е зареден в паметта): {ex.Message}. Обновяването ще се извърши при следващ рестарт.");
                        // Не връщаме false - файлът може да е вече инсталиран
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Грешка при копиране на DLL: {ex.Message}");
                        // Само при други грешки връщаме false
                        return false;
                    }
                }
            }

            // Стъпка 4: Проверка дали е вече регистриран
            if (IsRegistered(CLSID))
            {
                _logger.LogInfo("Credential Provider е вече регистриран.");
            }
            else
            {
                // Стъпка 5: Регистрация с regsvr32
                if (!RegisterDll(TARGET_DLL))
                {
                    return false;
                }
            }

            // Стъпка 6: Конфигуриране на Registry
            ConfigureRegistry();

            _logger.LogInfo("Credential Provider е инсталиран и регистриран успешно!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при инсталация на Credential Provider: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Регистрира DLL с regsvr32
    /// </summary>
    private bool RegisterDll(string dllPath)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "regsvr32.exe",
                Arguments = $"/s \"{dllPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    _logger.LogError("Неуспешно стартиране на regsvr32.");
                    return false;
                }

                process.WaitForExit(10000);

                if (process.ExitCode == 0)
                {
                    _logger.LogInfo("DLL регистриран успешно!");
                    return true;
                }
                else
                {
                    _logger.LogError($"Грешка при регистрация. Exit code: {process.ExitCode}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при регистрация на DLL: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Отменя регистрацията на DLL
    /// </summary>
    private void UnregisterDll(string dllPath)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "regsvr32.exe",
                Arguments = $"/u /s \"{dllPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process? process = Process.Start(startInfo))
            {
                process?.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Грешка при отмяна на регистрация: {ex.Message}");
        }
    }

    /// <summary>
    /// Конфигурира Registry за ServiceUrl и Authentication ключ
    /// </summary>
    private void ConfigureRegistry()
    {
        try
        {
            const string CLSID = "{3E879088-249C-4C83-85B6-834A3A9C6D12}";
            const string SERVICE_URL = "https://ads-auth.nursanbulgaria.com";

            // Конфигуриране на ServiceUrl
            using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\ADS\WindowsAuth", true))
            {
                if (key != null)
                {
                    key.SetValue("ServiceUrl", SERVICE_URL, Microsoft.Win32.RegistryValueKind.String);
                    _logger.LogInfo($"ServiceUrl конфигуриран: {SERVICE_URL}");
                }
            }

            // Проверка/създаване на Authentication Registry ключ
            string authPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{CLSID}";
            using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(authPath, true))
            {
                if (key != null)
                {
                    key.SetValue("", "ADS Windows Auth Credential Provider", Microsoft.Win32.RegistryValueKind.String);
                    _logger.LogInfo("Authentication Registry ключ конфигуриран");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при конфигуриране на Registry: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Проверява дали Credential Provider е вече регистриран
    /// </summary>
    private bool IsRegistered(string clsid)
    {
        try
        {
            // Проверка в Registry дали CLSID-ът е регистриран
            string clsidPath = $@"SOFTWARE\Classes\CLSID\{clsid}";
            using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(clsidPath))
            {
                if (key != null)
                {
                    // Проверка за Authentication Registry ключ
                    string authPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{clsid}";
                    using (var authKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(authPath))
                    {
                        return authKey != null;
                    }
                }
            }
        }
        catch
        {
            // Ако не можем да проверим, приемаме че не е регистриран
        }

        return false;
    }

    /// <summary>
    /// Отменя регистрацията на Credential Provider
    /// </summary>
    public bool UnregisterCredentialProvider()
    {
        try
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(_applicationDirectory, "CredentialProvider", "ADS.WindowsAuth.CredentialProvider.dll"),
                Path.Combine(_applicationDirectory, "ADS.WindowsAuth.CredentialProvider.dll"),
                Path.Combine(Path.GetDirectoryName(_applicationDirectory) ?? "", "ADS.WindowsAuth.CredentialProvider", "bin", "x64", "Release", "ADS.WindowsAuth.CredentialProvider.dll"),
                @"C:\ADS\CredentialProvider\ADS.WindowsAuth.CredentialProvider.dll"
            };

            string? dllPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    dllPath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(dllPath))
            {
                _logger.LogWarning("Credential Provider DLL не е намерен за деинсталация.");
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "regsvr32.exe",
                Arguments = $"/u /s \"{dllPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Verb = "runas"
            };

            using (Process? process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit(10000);

                if (process.ExitCode == 0)
                {
                    _logger.LogInfo("Credential Provider е деинсталиран успешно.");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при деинсталация на Credential Provider: {ex.Message}");
        }

        return false;
    }
}

