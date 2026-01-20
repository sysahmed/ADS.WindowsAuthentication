namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Сесия за аутентикация
/// </summary>
public class AuthSessionEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string WindowsUsername { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Pending", "Approved", "Rejected", "Expired"
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? ApprovedBy { get; set; }
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

