namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Конфигурация за комуникация със сървъра
/// </summary>
public class ServiceConfiguration
{
    /// <summary>
    /// URL на централния сървър
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// API ключ за автентикация
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Уникален идентификатор на машината
    /// </summary>
    public string MachineId { get; set; } = string.Empty;

    /// <summary>
    /// Дали се изисква VPN връзка
    /// </summary>
    public bool RequireVpn { get; set; }

    /// <summary>
    /// Интервал за проверка на VPN връзка (в секунди)
    /// </summary>
    public int VpnCheckInterval { get; set; } = 300;

    /// <summary>
    /// VPN gateway адреси за проверка
    /// </summary>
    public List<string> VpnGateways { get; set; } = new();

    /// <summary>
    /// Имена на VPN процеси
    /// </summary>
    public List<string> VpnProcessNames { get; set; } = new() { "FortiClient", "rasdial" };

    /// <summary>
    /// Дали да работи в offline режим
    /// </summary>
    public bool OfflineMode { get; set; }

    /// <summary>
    /// Дни за съхранение на offline данни
    /// </summary>
    public int OfflineDataRetention { get; set; } = 7;

    /// <summary>
    /// Път за съхранение на offline данни
    /// </summary>
    public string OfflineStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Timeout за връзка (в секунди)
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Интервал между опити за свързване (в секунди)
    /// </summary>
    public int RetryInterval { get; set; } = 60;

    /// <summary>
    /// Максимален брой опити
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

