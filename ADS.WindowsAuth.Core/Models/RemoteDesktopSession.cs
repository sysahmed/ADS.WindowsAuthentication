namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Модел на remote desktop сесия
/// </summary>
public class RemoteDesktopSession
{
    /// <summary>
    /// Уникален ID на сесията (6-символен код)
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Име на машината (host)
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// SignalR Connection ID на host-а
    /// </summary>
    public string? HostConnectionId { get; set; }

    /// <summary>
    /// SignalR Connection ID на viewer-а
    /// </summary>
    public string? ViewerConnectionId { get; set; }

    /// <summary>
    /// Потребител който е заявил control
    /// </summary>
    public string? RequestedByUser { get; set; }

    /// <summary>
    /// Дали контролът е одобрен
    /// </summary>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Дали viewer има control права
    /// </summary>
    public bool ControlEnabled { get; set; }

    /// <summary>
    /// Автоматично одобряване на контрол без потвърждение от host
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// Кога е създадена сесията
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Последна активност
    /// </summary>
    public DateTime LastActivity { get; set; }

    /// <summary>
    /// Timeout в минути
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Дали сесията е изтекла
    /// </summary>
    public bool IsExpired => DateTime.Now > LastActivity.AddMinutes(TimeoutMinutes);

    /// <summary>
    /// Дали сесията е активна (има свързан viewer)
    /// </summary>
    public bool IsActive => !string.IsNullOrEmpty(ViewerConnectionId) && !IsExpired;

    /// <summary>
    /// Frame rate (FPS)
    /// </summary>
    public int FrameRate { get; set; } = 30;

    /// <summary>
    /// JPEG качество (1-100)
    /// </summary>
    public int Quality { get; set; } = 60;

    /// <summary>
    /// Брой изпратени frames
    /// </summary>
    public long FramesSent { get; set; }

    /// <summary>
    /// Брой получени input commands
    /// </summary>
    public long InputCommandsReceived { get; set; }
}
