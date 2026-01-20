namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за управление на връзки
/// </summary>
public interface IConnectionService
{
    /// <summary>
    /// Проверява дали има връзка със сървъра
    /// </summary>
    Task<bool> CheckConnectionAsync();

    /// <summary>
    /// Проверява дали VPN е активен
    /// </summary>
    bool IsVpnConnected();

    /// <summary>
    /// Проверява дали машината е в offline режим
    /// </summary>
    bool IsOfflineMode();

    /// <summary>
    /// Синхронизира offline данни със сървъра
    /// </summary>
    Task<bool> SyncOfflineDataAsync();
}

