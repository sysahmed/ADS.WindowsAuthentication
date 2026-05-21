namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Блокиран IP адрес след брутфорс атака.
/// Може да бъде деблокиран само от администратор.
/// </summary>
public class BlockedIpEntity
{
    public int Id { get; set; }

    /// <summary>Блокираният IP адрес</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Брой неуспешни опити преди блокиране</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Дата/час на блокиране</summary>
    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Последен неуспешен опит</summary>
    public DateTime LastAttemptAt { get; set; } = DateTime.UtcNow;

    /// <summary>Дата/час на деблокиране (null = все още блокиран)</summary>
    public DateTime? UnblockedAt { get; set; }

    /// <summary>Кой администратор е деблокирал (null = все още блокиран)</summary>
    public string? UnblockedBy { get; set; }

    /// <summary>Причина за деблокиране (опционална бележка от администратора)</summary>
    public string? UnblockReason { get; set; }

    /// <summary>Дали в момента е блокиран</summary>
    public bool IsBlocked => UnblockedAt == null;
}
