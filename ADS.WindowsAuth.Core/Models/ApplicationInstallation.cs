namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Заявка за инсталация на приложение
/// </summary>
public class ApplicationInstallation
{
    /// <summary>
    /// Идентификатор на инсталацията
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Име на приложението
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Път до инсталационния файл или URL
    /// </summary>
    public string InstallerPath { get; set; } = string.Empty;

    /// <summary>
    /// Тип на инсталатора (MSI, EXE, URL, и т.н.)
    /// </summary>
    public string InstallerType { get; set; } = string.Empty;

    /// <summary>
    /// Параметри за инсталация
    /// </summary>
    public string InstallParameters { get; set; } = string.Empty;

    /// <summary>
    /// Целева машина
    /// </summary>
    public string TargetMachine { get; set; } = string.Empty;

    /// <summary>
    /// Статус на инсталацията
    /// </summary>
    public InstallationStatus Status { get; set; }

    /// <summary>
    /// Съобщение за статуса
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// Време на заявка
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Време на завършване
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Потребител, който е направил заявката
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Статус на инсталация
/// </summary>
public enum InstallationStatus
{
    /// <summary>
    /// Очаква
    /// </summary>
    Pending = 0,

    /// <summary>
    /// В процес
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Успешна
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Неуспешна
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Отменена
    /// </summary>
    Cancelled = 4
}

