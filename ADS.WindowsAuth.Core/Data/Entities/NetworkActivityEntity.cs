namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Мрежова активност
/// </summary>
public class NetworkActivityEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string? InterfaceName { get; set; }
    public string? InterfaceDescription { get; set; }
    public long? Speed { get; set; } // В битове в секунда
    public long? BytesReceived { get; set; }
    public long? BytesSent { get; set; }
    public DateTime EventTime { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

