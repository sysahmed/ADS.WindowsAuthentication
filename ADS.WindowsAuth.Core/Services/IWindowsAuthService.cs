using ADS.WindowsAuth.Core.Models;
using System.Threading.Tasks;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за сервис за Windows Domain аутентикация
/// </summary>
public interface IWindowsAuthService
{
    /// <summary>
    /// Проверява дали потребителят е валиден в домейна
    /// </summary>
    /// <param name="username">Потребителско име</param>
    /// <param name="password">Парола</param>
    /// <param name="domain">Домейн</param>
    /// <returns>Дали аутентикацията е успешна</returns>
    bool ValidateCredentials(string username, string password, string domain);

    /// <summary>
    /// Проверява дали потребителят е валиден в домейна (async версия)
    /// </summary>
    /// <param name="username">Потребителско име</param>
    /// <param name="password">Парола</param>
    /// <param name="domain">Домейн</param>
    /// <returns>Дали аутентикацията е успешна</returns>
    Task<bool> ValidateCredentialsAsync(string username, string password, string domain);

    /// <summary>
    /// Получава текущия Windows потребител
    /// </summary>
    /// <returns>Име на потребителя и домейна</returns>
    (string Username, string Domain) GetCurrentWindowsUser();
}

