namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Събитие за приложение (стартиране/затваряне)
/// </summary>
public class ApplicationEventEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public int? ProcessId { get; set; }
    public string EventType { get; set; } = string.Empty; // "Start" или "Stop"
    public DateTime EventTime { get; set; } = DateTime.Now;
    public int? DurationSeconds { get; set; } // За "Stop" събития
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

