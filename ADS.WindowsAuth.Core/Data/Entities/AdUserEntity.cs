namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Active Directory потребител
/// </summary>
public class AdUserEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string DistinguishedName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastLogon { get; set; }
    public DateTime? LastPasswordChange { get; set; }
    public DateTime? AccountExpires { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public List<AdUserGroupEntity> UserGroups { get; set; } = new();
    public List<UserActivityEntity> UserActivities { get; set; } = new();
    public List<ApplicationEventEntity> ApplicationEvents { get; set; } = new();
    public List<FileActivityEntity> FileActivities { get; set; } = new();
    public List<NetworkActivityEntity> NetworkActivities { get; set; } = new();
    public List<UsbDeviceEntity> UsbDevices { get; set; } = new();
    public List<ScreenTimeEntity> ScreenTimes { get; set; } = new();
    public List<AuthSessionEntity> AuthSessions { get; set; } = new();
}

