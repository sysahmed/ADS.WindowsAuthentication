namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Windows Event Log запис
/// </summary>
public class WindowsEventEntity
{
    public int Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public int EventId { get; set; }
    public string LogName { get; set; } = string.Empty; // Application, System, Security
    public string? ProviderName { get; set; }
    public string? Level { get; set; } // Information, Warning, Error, Critical
    public string? Message { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

