using ADS.WindowsAuth.Core.Models;
using System.Threading.Tasks;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за управление на сесии за аутентикация
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Създава нова сесия за аутентикация
    /// </summary>
    /// <returns>Създадената сесия</returns>
    AuthSession CreateSession();

    /// <summary>
    /// Създава нова сесия за аутентикация с указан потребител и домейн
    /// </summary>
    /// <param name="username">Потребителско име</param>
    /// <param name="domain">Домейн</param>
    /// <returns>Създадената сесия</returns>
    AuthSession CreateSession(string username, string domain);

    /// <summary>
    /// Създава нова сесия за аутентикация с пълни параметри (async версия)
    /// </summary>
    /// <param name="username">Потребителско име</param>
    /// <param name="domain">Домейн</param>
    /// <param name="clientType">Тип на клиента</param>
    /// <param name="clientIp">IP адрес на клиента</param>
    /// <returns>Създадената сесия</returns>
    Task<AuthSession> CreateSessionAsync(string username, string domain, string clientType, string clientIp);

    /// <summary>
    /// Получава сесия по токен
    /// </summary>
    /// <param name="accessToken">Токен за достъп</param>
    /// <returns>Сесията или null ако не е намерена</returns>
    AuthSession? GetSessionByToken(string accessToken);

    /// <summary>
    /// Получава сесия по идентификатор
    /// </summary>
    /// <param name="sessionId">Идентификатор на сесията</param>
    /// <returns>Сесията или null ако не е намерена</returns>
    AuthSession? GetSessionById(string sessionId);

    /// <summary>
    /// Одобрява сесия
    /// </summary>
    /// <param name="sessionId">Идентификатор на сесията</param>
    /// <param name="approvedUsername">Потребител който одобрява (опционално)</param>
    /// <param name="approvedPassword">Парола за автоматичен login (опционално)</param>
    /// <param name="approvedDomain">Домейн на одобрилия потребител (опционално)</param>
    /// <returns>Дали операцията е успешна</returns>
    bool ApproveSession(string sessionId, string? approvedUsername = null, string? approvedPassword = null, string? approvedDomain = null);

    /// <summary>
    /// Отхвърля сесия
    /// </summary>
    /// <param name="sessionId">Идентификатор на сесията</param>
    /// <returns>Дали операцията е успешна</returns>
    bool RejectSession(string sessionId);

    /// <summary>
    /// Премахва изтекла сесия
    /// </summary>
    void CleanupExpiredSessions();

    /// <summary>
    /// Получава всички активни сесии (за debugging)
    /// </summary>
    /// <returns>Списък с активни сесии</returns>
    IEnumerable<AuthSession> GetAllSessions();
    
    /// <summary>
    /// Зарежда активни сесии от базата данни (при стартиране)
    /// </summary>
    /// <param name="databaseService">Database service за достъп до базата данни</param>
    Task LoadSessionsFromDatabaseAsync(IDatabaseService? databaseService);
}

