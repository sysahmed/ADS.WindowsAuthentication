namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Файлова активност (отваряне/затваряне/създаване/изтриване)
/// </summary>
public class FileActivityEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public string? ApplicationName { get; set; }
    public string EventType { get; set; } = string.Empty; // "Open", "Close", "Create", "Delete", "Modify"
    public DateTime EventTime { get; set; } = DateTime.Now;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Връзка с AD потребител
    public int? AdUserId { get; set; }
    public AdUserEntity? AdUser { get; set; }
}

