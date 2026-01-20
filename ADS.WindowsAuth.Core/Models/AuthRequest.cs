namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Заявка за аутентикация от мобилно устройство
/// </summary>
public class AuthRequest
{
    /// <summary>
    /// Токен за достъп до сесията
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Потребителско име в домейна
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Парола на потребителя
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Домейн на потребителя
    /// </summary>
    public string Domain { get; set; } = string.Empty;
}

/// <summary>
/// Отговор на заявка за аутентикация
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// Дали аутентикацията е успешна
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Съобщение за резултата
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Име на компютъра
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Потребителско име
    /// </summary>
    public string? Username { get; set; }
}

