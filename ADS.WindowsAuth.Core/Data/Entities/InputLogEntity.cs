namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Запис за въвеждане от клавиатура или клик – от Monitor (input capture).
/// </summary>
public class InputLogEntity
{
    public int Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    /// <summary>Key или Click</summary>
    public string LogType { get; set; } = string.Empty;
    public string? ApplicationName { get; set; }
    public string? WindowTitle { get; set; }
    /// <summary>Въведен символ/текст или "X,Y,Button" за клик</summary>
    public string Data { get; set; } = string.Empty;
    public bool IsPassword { get; set; }
}
