namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Screen time запис
/// </summary>
public class ScreenTimeEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public int Seconds { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

