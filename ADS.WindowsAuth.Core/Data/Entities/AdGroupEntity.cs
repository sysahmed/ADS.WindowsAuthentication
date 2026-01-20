namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Active Directory група
/// </summary>
public class AdGroupEntity
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DistinguishedName { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public List<AdUserGroupEntity> UserGroups { get; set; } = new();
}

