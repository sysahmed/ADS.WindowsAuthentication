namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Service за управление на remote desktop сесии
/// </summary>
public interface IRemoteDesktopSessionService
{
    /// <summary>
    /// Създава нова remote desktop сесия
    /// </summary>
    /// <param name="machineName">Име на машината</param>
    /// <param name="requestedBy">Потребител който заявява</param>
    /// <returns>Session ID</returns>
    Task<string> CreateSessionAsync(string machineName, string? requestedBy = null);

    /// <summary>
    /// Получава сесия по ID
    /// </summary>
    Task<Models.RemoteDesktopSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Получава всички активни сесии
    /// </summary>
    Task<List<Models.RemoteDesktopSession>> GetActiveSessionsAsync();

    /// <summary>
    /// Получава сесия по име на машина
    /// </summary>
    Task<Models.RemoteDesktopSession?> GetSessionByMachineAsync(string machineName);

    /// <summary>
    /// Регистрира host connection
    /// </summary>
    Task RegisterHostAsync(string sessionId, string connectionId);

    /// <summary>
    /// Регистрира viewer connection
    /// </summary>
    Task RegisterViewerAsync(string sessionId, string connectionId, string userId);

    /// <summary>
    /// Одобрява контрол за сесия
    /// </summary>
    Task AuthorizeControlAsync(string sessionId);

    /// <summary>
    /// Забранява контрол за сесия
    /// </summary>
    Task DenyControlAsync(string sessionId);

    /// <summary>
    /// Обновява последна активност
    /// </summary>
    Task UpdateActivityAsync(string sessionId);

    /// <summary>
    /// Приключва сесия
    /// </summary>
    Task EndSessionAsync(string sessionId);

    /// <summary>
    /// Премахва изтекли сесии
    /// </summary>
    Task CleanupExpiredSessionsAsync();
}
