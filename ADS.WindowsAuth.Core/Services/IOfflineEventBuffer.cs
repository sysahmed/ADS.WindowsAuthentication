namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за буфериране на събития при липса на връзка с API.
/// Съхранява неуспешно изпратени заявки локално и ги прехвърля при възстановяване на връзката.
/// </summary>
public interface IOfflineEventBuffer
{
    /// <summary>
    /// Добавя събитие за изпращане към опашката.
    /// </summary>
    /// <param name="endpoint">API endpoint (напр. /api/activity/start)</param>
    /// <param name="payloadJson">JSON payload за POST заявката</param>
    void Enqueue(string endpoint, string payloadJson);

    /// <summary>
    /// Връща всички чакащи събития (без да ги премахва).
    /// </summary>
    IReadOnlyList<OfflineEvent> GetPending();

    /// <summary>
    /// Премахва изпратено събитие от буфера.
    /// </summary>
    void Remove(string eventId);

    /// <summary>
    /// Брой чакащи събития.
    /// </summary>
    int PendingCount { get; }
}

/// <summary>
/// Един буферирано събитие.
/// </summary>
public class OfflineEvent
{
    public string Id { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
