namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Активност на потребител в базата данни
/// </summary>
public class UserActivityEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ScreenTimeSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

