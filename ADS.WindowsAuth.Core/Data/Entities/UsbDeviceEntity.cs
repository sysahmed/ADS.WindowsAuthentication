namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// USB устройство събитие
/// </summary>
public class UsbDeviceEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Name { get; set; }
    public string EventType { get; set; } = string.Empty; // "Connected" или "Disconnected"
    public DateTime EventTime { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

