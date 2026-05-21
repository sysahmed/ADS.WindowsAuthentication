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
    /// <param name="autoApprove">Автоматично одобряване на контрол при заявка от viewer</param>
    Task RegisterHostAsync(string sessionId, string connectionId, bool autoApprove = false);

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
    /// Изчиства само host connection ID (host се disconnectна, но сесията остава жива)
    /// </summary>
    Task ClearHostConnectionAsync(string sessionId);

    /// <summary>
    /// Изчиства само viewer connection ID (viewer се disconnectна, но сесията остава жива)
    /// </summary>
    Task ClearViewerConnectionAsync(string sessionId);

    /// <summary>
    /// Приключва сесия напълно (изтрива я)
    /// </summary>
    Task EndSessionAsync(string sessionId);

    /// <summary>
    /// Премахва изтекли сесии
    /// </summary>
    Task CleanupExpiredSessionsAsync();

    /// <summary>
    /// Създава сесия ако не съществува за дадена машина, иначе връща съществуващата.
    /// Връща (sessionId, дали е новосъздадена).
    /// </summary>
    Task<(string sessionId, bool created)> CreateOrGetSessionAsync(string machineName, string? requestedBy = null);
}
