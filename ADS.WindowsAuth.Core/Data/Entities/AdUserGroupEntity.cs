namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Връзка между AD потребител и група (many-to-many)
/// </summary>
public class AdUserGroupEntity
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public AdUserEntity User { get; set; } = null!;
    public AdGroupEntity Group { get; set; } = null!;
}

