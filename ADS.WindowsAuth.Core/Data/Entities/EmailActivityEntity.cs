namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Имейл събитие (получен, изпратен, отговорен, препратен).
/// Записва се когато Outlook е активен и потребителят отваря/изпраща имейл.
/// </summary>
public class EmailActivityEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;

    /// <summary>Тема на имейла.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Подател (за получени) или получател (за изпратени/отговорени).
    /// Може да е имейл или дисплейно име.
    /// </summary>
    public string? SenderOrRecipient { get; set; }

    /// <summary>Тип на събитието: Received | Replied | Composed | Forwarded | Opened</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Откъде е засечено (WindowTitle | FolderScan).</summary>
    public string? DetectionSource { get; set; }

    public DateTime EventTime { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
