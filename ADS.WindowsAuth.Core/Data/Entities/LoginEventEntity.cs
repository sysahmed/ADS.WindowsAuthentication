namespace ADS.WindowsAuth.Core.Data.Entities;

/// <summary>
/// Ентити за съхранение на събития за влизане на потребители
/// </summary>
public class LoginEventEntity
{
    public int Id { get; set; }
    
    /// <summary>
    /// Потребителско име
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Домейн
    /// </summary>
    public string Domain { get; set; } = string.Empty;
    
    /// <summary>
    /// Име на машината
    /// </summary>
    public string MachineName { get; set; } = string.Empty;
    
    /// <summary>
    /// Време на влизане
    /// </summary>
    public DateTime LoginTime { get; set; }
    
    /// <summary>
    /// Метод на влизане (QRCode, Password, SmartCard)
    /// </summary>
    public string LoginMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// ID на QR сесията (ако е приложимо)
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Дали влизането е успешно
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// IP адрес (ако е наличен)
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// ID на AD потребител (ако е свързан)
    /// </summary>
    public int? AdUserId { get; set; }
    
    /// <summary>
    /// Windows logon type (2=Interactive, 10=RemoteInteractive, и т.н.)
    /// </summary>
    public int? LogonType { get; set; }
}
