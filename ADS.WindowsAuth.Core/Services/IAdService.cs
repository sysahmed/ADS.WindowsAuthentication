namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за Active Directory сервис
/// </summary>
public interface IAdService
{
    /// <summary>
    /// Дали AD е активиран
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Проверява дали потребителят съществува в AD
    /// </summary>
    bool UserExists(string username);

    /// <summary>
    /// Валидира credentials срещу AD
    /// </summary>
    bool ValidateCredentials(string username, string password);

    /// <summary>
    /// Получава информация за потребител от AD
    /// </summary>
    AdUserInfo? GetUserInfo(string username);

    /// <summary>
    /// Получава всички потребители от AD
    /// </summary>
    List<AdUserInfo> GetAllUsers();

    /// <summary>
    /// Получава групите на потребител
    /// </summary>
    List<string> GetUserGroups(string username);
}

/// <summary>
/// Информация за AD потребител
/// </summary>
public class AdUserInfo
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<string> Groups { get; set; } = new();
}

