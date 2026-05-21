namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Запис за посещение на уебсайт (от Monitor или browser extension).
/// </summary>
public class VisitedWebsiteEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Browser { get; set; } = string.Empty;
    public DateTime VisitedAt { get; set; }
    public int DurationSeconds { get; set; }
}
