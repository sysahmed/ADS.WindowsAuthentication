using System.DirectoryServices;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using System.Threading.Tasks;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за Windows Domain аутентикация
/// </summary>
public class WindowsAuthService : IWindowsAuthService
{
    private readonly ILoggerService _logger;

    /// <summary>
    /// Конструктор на WindowsAuthService
    /// </summary>
    /// <param name="logger">Сервис за логване</param>
    public WindowsAuthService(ILoggerService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Проверява дали потребителят е валиден в домейна
    /// </summary>
    public bool ValidateCredentials(string username, string password, string domain)
    {
        try
        {
            _logger.LogInfo($"Опит за валидация на потребител: {username}@{domain}");

            string ldapPath = $"LDAP://{domain}";
            
            // Опит за свързване с потребителските credentials
            string userDn = $"{username}@{domain}";
            
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, userDn, password))
            {
                object? nativeObject = entry.NativeObject;
                _logger.LogInfo($"Успешна валидация на потребител: {username}@{domain}");
                return true;
            }
        }
        catch (System.DirectoryServices.DirectoryServicesCOMException ex)
        {
            _logger.LogWarning($"Неуспешна валидация на потребител: {username}@{domain} - {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при валидация на потребител {username}@{domain}", ex);
            return false;
        }
    }

    /// <summary>
    /// Проверява дали потребителят е валиден в домейна (async версия)
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(string username, string password, string domain)
    {
        return await Task.Run(() => ValidateCredentials(username, password, domain));
    }

    /// <summary>
    /// Получава текущия Windows потребител (с timeout за да не блокира)
    /// </summary>
    public (string Username, string Domain) GetCurrentWindowsUser()
    {
        string fullUsername = Environment.UserName;
        string domain = Environment.UserDomainName;
        
        _logger.LogInfo($"Environment.UserName: {fullUsername}");
        _logger.LogInfo($"Environment.UserDomainName: {domain}");
        
        // Проверка дали е IIS APPPOOL потребител - проверяваме и username и domain
        bool isIisAppPool = fullUsername.Contains("IIS APPPOOL", StringComparison.OrdinalIgnoreCase) ||
                           fullUsername.StartsWith("DefaultAppPool", StringComparison.OrdinalIgnoreCase) ||
                           domain.Contains("IIS APPPOOL", StringComparison.OrdinalIgnoreCase) ||
                           domain.Equals("IIS APPPOOL", StringComparison.OrdinalIgnoreCase);
        
        if (isIisAppPool)
        {
            _logger.LogWarning($"Детектиран е IIS APPPOOL потребител: {fullUsername}@{domain}");
            try
            {
                // Опит за получаване на реалния Windows потребител с timeout (не блокира)
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(1)))
                {
                    var identityTask = Task.Run(() => 
                    {
                        try
                        {
                            return System.Security.Principal.WindowsIdentity.GetCurrent();
                        }
                        catch
                        {
                            return null;
                        }
                    }, cts.Token);
                    
                    // Не чакаме повече от 1 секунда
                    if (identityTask.Wait(TimeSpan.FromSeconds(1)))
                    {
                        var identity = identityTask.Result;
                if (identity != null && !string.IsNullOrEmpty(identity.Name))
                {
                            _logger.LogInfo($"Windows Identity: {identity.Name}");
                    var parts = identity.Name.Split('\\');
                    if (parts.Length == 2)
                    {
                        domain = parts[0];
                        fullUsername = parts[1];
                        _logger.LogInfo($"Реален Windows потребител (от Identity): {fullUsername}@{domain}");
                    }
                            else if (!string.IsNullOrEmpty(identity.Name))
                            {
                                // Ако няма обратна наклонена черта, опитай да извлечеш от името
                                // Проверяваме дали има @ символ (UPN формат)
                                if (identity.Name.Contains('@'))
                                {
                                    var upnParts = identity.Name.Split('@');
                                    if (upnParts.Length == 2)
                                    {
                                        fullUsername = upnParts[0];
                                        domain = upnParts[1];
                                        _logger.LogInfo($"Реален Windows потребител (от UPN формат): {fullUsername}@{domain}");
                                    }
                                    else
                                    {
                                        fullUsername = identity.Name;
                                        domain = Environment.UserDomainName;
                                        _logger.LogInfo($"Windows Identity (UPN формат без домейн): {fullUsername}");
                                    }
                                }
                                else
                                {
                                    fullUsername = identity.Name;
                                    // Ако domain все още е IIS APPPOOL, опитай да вземеш от машината
                                    if (domain.Equals("IIS APPPOOL", StringComparison.OrdinalIgnoreCase))
                                    {
                                        domain = Environment.MachineName;
                                        _logger.LogInfo($"Windows Identity (без домейн, използвам MachineName): {fullUsername}@{domain}");
                                    }
                                    else
                                    {
                                        _logger.LogInfo($"Windows Identity (без домейн): {fullUsername}@{domain}");
                                    }
                                }
                            }
                }
                else
                {
                    _logger.LogWarning("Windows Identity е null или празен, използвам Environment.UserName");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Timeout при получаване на Windows Identity (1 сек). Използвам Environment.UserName: {fullUsername}@{domain}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ако не успее, използвай Environment.UserName
                _logger.LogWarning($"Не може да се получи реален Windows потребител: {ex.Message}. Използва се: {fullUsername}@{domain}");
            }
        }
        else
        {
            _logger.LogInfo($"Използван е Environment.UserName (не е IIS APPPOOL): {fullUsername}@{domain}");
        }
        
        // Финален fallback - ако domain все още е "IIS APPPOOL", използваме MachineName
        if (domain.Equals("IIS APPPOOL", StringComparison.OrdinalIgnoreCase))
        {
            domain = Environment.MachineName;
            _logger.LogInfo($"Domain е все още IIS APPPOOL, използвам MachineName: {fullUsername}@{domain}");
        }
        
        _logger.LogInfo($"Текущ Windows потребител: {fullUsername}@{domain}");
        
        return (fullUsername, domain);
    }
}
