using System.DirectoryServices;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на Active Directory сервис
/// </summary>
public class AdService : IAdService
{
    private readonly ActiveDirectorySettings _settings;
    private readonly ILoggerService _logger;

    public bool IsEnabled => _settings.Enabled;

    public AdService(ActiveDirectorySettings settings, ILoggerService logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool UserExists(string username)
    {
        if (!_settings.Enabled)
        {
            return false;
        }

        try
        {
            string ldapPath = _settings.GetLdapPath();
            
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _settings.ServiceAccount, _settings.ServicePassword))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = $"(sAMAccountName={username})";
                    searcher.PropertiesToLoad.Add("sAMAccountName");
                    
                    SearchResult? result = searcher.FindOne();
                    return result != null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при проверка за съществуване на потребител {username} в AD", ex);
            return false;
        }
    }

    public bool ValidateCredentials(string username, string password)
    {
        if (!_settings.Enabled)
        {
            return false;
        }

        try
        {
            string ldapPath = _settings.GetLdapPath();
            string userDn = GetUserDistinguishedName(username);
            
            if (string.IsNullOrEmpty(userDn))
            {
                _logger.LogWarning($"Потребителят {username} не е намерен в AD");
                return false;
            }

            // Опит за свързване с потребителските credentials
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, userDn, password))
            {
                object? nativeObject = entry.NativeObject;
                _logger.LogInfo($"Успешна валидация на AD credentials за {username}");
                return true;
            }
        }
        catch (DirectoryServicesCOMException ex)
        {
            _logger.LogWarning($"Неуспешна валидация на AD credentials за {username}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при валидация на AD credentials за {username}", ex);
            return false;
        }
    }

    public AdUserInfo? GetUserInfo(string username)
    {
        if (!_settings.Enabled)
        {
            return null;
        }

        try
        {
            string ldapPath = _settings.GetLdapPath();
            
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _settings.ServiceAccount, _settings.ServicePassword))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = $"(sAMAccountName={username})";
                    searcher.PropertiesToLoad.Add("sAMAccountName");
                    searcher.PropertiesToLoad.Add("displayName");
                    searcher.PropertiesToLoad.Add("mail");
                    searcher.PropertiesToLoad.Add("distinguishedName");
                    searcher.PropertiesToLoad.Add("userAccountControl");
                    
                    SearchResult? result = searcher.FindOne();
                    
                    if (result == null)
                    {
                        return null;
                    }

                    AdUserInfo userInfo = new AdUserInfo
                    {
                        Username = GetPropertyValue(result, "sAMAccountName") ?? username,
                        DisplayName = GetPropertyValue(result, "displayName") ?? username,
                        Email = GetPropertyValue(result, "mail") ?? string.Empty,
                        DistinguishedName = GetPropertyValue(result, "distinguishedName") ?? string.Empty
                    };

                    // Проверка дали акаунтът е активиран
                    string? userAccountControl = GetPropertyValue(result, "userAccountControl");
                    if (!string.IsNullOrEmpty(userAccountControl) && int.TryParse(userAccountControl, out int uac))
                    {
                        // 0x0002 = ACCOUNTDISABLE
                        userInfo.IsEnabled = (uac & 0x0002) == 0;
                    }
                    else
                    {
                        userInfo.IsEnabled = true;
                    }

                    // Получаване на групи
                    userInfo.Groups = GetUserGroups(username);

                    return userInfo;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на информация за потребител {username} от AD", ex);
            return null;
        }
    }

    public List<AdUserInfo> GetAllUsers()
    {
        List<AdUserInfo> users = new List<AdUserInfo>();

        if (!_settings.Enabled)
        {
            return users;
        }

        try
        {
            string ldapPath = _settings.GetLdapPath();
            
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _settings.ServiceAccount, _settings.ServicePassword))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = "(&(objectClass=user)(objectCategory=person))";
                    searcher.PropertiesToLoad.Add("sAMAccountName");
                    searcher.PropertiesToLoad.Add("displayName");
                    searcher.PropertiesToLoad.Add("mail");
                    searcher.PropertiesToLoad.Add("distinguishedName");
                    
                    SearchResultCollection? results = searcher.FindAll();
                    
                    if (results != null)
                    {
                        foreach (SearchResult result in results)
                        {
                            string? username = GetPropertyValue(result, "sAMAccountName");
                            if (!string.IsNullOrEmpty(username))
                            {
                                AdUserInfo? userInfo = GetUserInfo(username);
                                if (userInfo != null)
                                {
                                    users.Add(userInfo);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при получаване на всички потребители от AD", ex);
        }

        return users;
    }

    public List<string> GetUserGroups(string username)
    {
        List<string> groups = new List<string>();

        if (!_settings.Enabled)
        {
            return groups;
        }

        try
        {
            string ldapPath = _settings.GetLdapPath();
            string userDn = GetUserDistinguishedName(username);
            
            if (string.IsNullOrEmpty(userDn))
            {
                return groups;
            }

            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _settings.ServiceAccount, _settings.ServicePassword))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = $"(&(objectClass=group)(member={userDn}))";
                    searcher.PropertiesToLoad.Add("cn");
                    
                    SearchResultCollection? results = searcher.FindAll();
                    
                    if (results != null)
                    {
                        foreach (SearchResult result in results)
                        {
                            string? groupName = GetPropertyValue(result, "cn");
                            if (!string.IsNullOrEmpty(groupName))
                            {
                                groups.Add(groupName);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на групи за потребител {username} от AD", ex);
        }

        return groups;
    }

    private string? GetUserDistinguishedName(string username)
    {
        try
        {
            string ldapPath = _settings.GetLdapPath();
            
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, _settings.ServiceAccount, _settings.ServicePassword))
            {
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = $"(sAMAccountName={username})";
                    searcher.PropertiesToLoad.Add("distinguishedName");
                    
                    SearchResult? result = searcher.FindOne();
                    
                    if (result != null)
                    {
                        return GetPropertyValue(result, "distinguishedName");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на DN за потребител {username}", ex);
        }

        return null;
    }

    private string? GetPropertyValue(SearchResult result, string propertyName)
    {
        if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
        {
            return result.Properties[propertyName][0]?.ToString();
        }
        return null;
    }
}

