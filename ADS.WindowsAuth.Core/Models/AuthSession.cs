namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Представлява сесия за аутентикация
/// </summary>
public class AuthSession
{
    /// <summary>
    /// Уникален идентификатор на сесията
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Токен за достъп до сесията
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Име на компютъра/машината
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Потребителско име на Windows потребителя
    /// </summary>
    public string WindowsUsername { get; set; } = string.Empty;

    /// <summary>
    /// Домейн на потребителя
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Време на създаване на сесията
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Време на изтичане на сесията
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Статус на сесията (Pending, Approved, Rejected, Expired)
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Pending;

    /// <summary>
    /// IP адрес на мобилното устройство (ако е приложимо)
    /// </summary>
    public string? MobileDeviceIp { get; set; }

    /// <summary>
    /// Време на одобряване на сесията
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Потребител който е одобрил сесията (формат: username@domain)
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Парола на одобрилия потребител (временно запазена за автоматичен login, шифрована)
    /// ВАЖНО: Това е временно и трябва да се изтрие след login
    /// </summary>
    public string? ApprovedPassword { get; set; }

    /// <summary>
    /// Време на изтичане на паролата (за security - автоматично изтриване)
    /// </summary>
    public DateTime? PasswordExpiresAt { get; set; }

    /// <summary>
    /// Дали паролата е изтекла (за security)
    /// </summary>
    public bool IsPasswordExpired => 
        PasswordExpiresAt.HasValue && PasswordExpiresAt < DateTime.Now;

    /// <summary>
    /// Изтрива паролата от паметта (за security)
    /// </summary>
    public void ClearPassword()
    {
        if (!string.IsNullOrEmpty(ApprovedPassword))
        {
            // Overwrite with zeros for security
            ApprovedPassword = new string('\0', ApprovedPassword.Length);
            ApprovedPassword = null;
        }
        PasswordExpiresAt = null;
    }

    /// <summary>
    /// Потребителско име (алиас за WindowsUsername за по-лесен достъп)
    /// </summary>
    public string Username => WindowsUsername;
}

/// <summary>
/// Статус на сесията за аутентикация
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Очаква потвърждение
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Одобрена
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Отхвърлена
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Изтекла
    /// </summary>
    Expired = 3
}

