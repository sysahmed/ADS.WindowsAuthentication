namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Лог запис
/// </summary>
public class LogEntryEntity
{
    public int Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty; // "INFO", "WARNING", "ERROR"
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? Source { get; set; } // Източник на лога (например "Monitor", "Client", "API")
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
}

