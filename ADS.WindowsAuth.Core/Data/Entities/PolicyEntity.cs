namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Политика
/// </summary>
public class PolicyEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string BlockedWebsitesJson { get; set; } = "[]"; // JSON масив от string
    public string BlockedApplicationsJson { get; set; } = "[]"; // JSON масив от string
    public string BlockedFileExtensionsJson { get; set; } = "[]"; // JSON масив от string
    public string TargetMachinesJson { get; set; } = "[]"; // JSON масив от string
    public string TargetUsersJson { get; set; } = "[]"; // JSON масив от string
    public int MaxScreenTimeSeconds { get; set; }
    public string AllowedInstallationsJson { get; set; } = "[]"; // JSON масив от string
    public string BlockedInstallationsJson { get; set; } = "[]"; // JSON масив от string
    public bool BlockUsbAccess { get; set; }
    public bool BlockPrinterAccess { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

