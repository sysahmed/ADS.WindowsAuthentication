namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Конфигурация на Monitor Service за конкретна машина
/// </summary>
public class MonitorConfigurationEntity
{
    public int Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool RequireVpn { get; set; }
    public int VpnCheckInterval { get; set; } = 300;
    public string VpnGateways { get; set; } = string.Empty; // JSON array като string
    public string VpnProcessNames { get; set; } = string.Empty; // JSON array като string
    public bool OfflineMode { get; set; }
    public int OfflineDataRetention { get; set; } = 7;
    public int ConnectionTimeout { get; set; } = 30;
    public int RetryInterval { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

