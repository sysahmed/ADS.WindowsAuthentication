namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Политика за управление на машина
/// </summary>
public class Policy
{
    /// <summary>
    /// Идентификатор на политиката
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Име на политиката
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Описание
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Дали политиката е активна
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Блокирани уебсайтове
    /// </summary>
    public List<string> BlockedWebsites { get; set; } = new();

    /// <summary>
    /// Блокирани приложения
    /// </summary>
    public List<string> BlockedApplications { get; set; } = new();

    /// <summary>
    /// Блокирани файлови разширения
    /// </summary>
    public List<string> BlockedFileExtensions { get; set; } = new();

    /// <summary>
    /// Максимално време на екрана в секунди (0 = неограничено)
    /// </summary>
    public int MaxScreenTimeSeconds { get; set; }

    /// <summary>
    /// Разрешени приложения за инсталация
    /// </summary>
    public List<string> AllowedInstallations { get; set; } = new();

    /// <summary>
    /// Блокирани приложения за инсталация
    /// </summary>
    public List<string> BlockedInstallations { get; set; } = new();

    /// <summary>
    /// Дали да се блокира достъпът до USB устройства
    /// </summary>
    public bool BlockUsbAccess { get; set; }

    /// <summary>
    /// Дали да се блокира достъпът до принтери
    /// </summary>
    public bool BlockPrinterAccess { get; set; }

    /// <summary>
    /// Дали да се блокира достъпът до Bluetooth
    /// </summary>
    public bool BlockBluetoothAccess { get; set; }

    /// <summary>
    /// Машини, към които се прилага политиката
    /// </summary>
    public List<string> TargetMachines { get; set; } = new();

    /// <summary>
    /// Потребители, към които се прилага политиката
    /// </summary>
    public List<string> TargetUsers { get; set; } = new();

    /// <summary>
    /// Време на създаване
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Време на обновяване
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

