namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Системна информация
/// </summary>
public class SystemInfoEntity
{
    public int Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Domain { get; set; }
    public string? OsVersion { get; set; }
    public int? ProcessorCount { get; set; }
    public long? TotalMemory { get; set; }
    public long? WorkingSet { get; set; }
    public double? UptimeSeconds { get; set; }
    public DateTime EventTime { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

