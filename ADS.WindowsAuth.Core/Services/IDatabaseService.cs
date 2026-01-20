using ADS.WindowsAuth.Core.Data.Entities;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за работа с базата данни
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Записва Application Event
    /// </summary>
    Task<int> SaveApplicationEventAsync(ApplicationEventEntity entity);

    /// <summary>
    /// Записва File Activity
    /// </summary>
    Task<int> SaveFileActivityAsync(FileActivityEntity entity);

    /// <summary>
    /// Записва Network Activity
    /// </summary>
    Task<int> SaveNetworkActivityAsync(NetworkActivityEntity entity);

    /// <summary>
    /// Записва System Info
    /// </summary>
    Task<int> SaveSystemInfoAsync(SystemInfoEntity entity);

    /// <summary>
    /// Записва USB Device Event
    /// </summary>
    Task<int> SaveUsbDeviceAsync(UsbDeviceEntity entity);

    /// <summary>
    /// Записва Screen Time
    /// </summary>
    Task<int> SaveScreenTimeAsync(ScreenTimeEntity entity);

    /// <summary>
    /// Записва или обновява User Activity
    /// </summary>
    Task<int> SaveOrUpdateUserActivityAsync(UserActivityEntity entity);

    /// <summary>
    /// Записва или обновява Auth Session
    /// </summary>
    Task<int> SaveOrUpdateAuthSessionAsync(AuthSessionEntity entity);

    /// <summary>
    /// Записва Windows Event
    /// </summary>
    Task<int> SaveWindowsEventAsync(WindowsEventEntity entity);

    /// <summary>
    /// Получава или създава AD потребител
    /// </summary>
    Task<AdUserEntity?> GetOrCreateAdUserAsync(string username, string domain);

    /// <summary>
    /// Синхронизира AD потребители и групи
    /// </summary>
    Task SyncActiveDirectoryAsync();
    
    /// <summary>
    /// Записва Login Event
    /// </summary>
    Task<int> SaveLoginEventAsync(LoginEventEntity entity);
    
    /// <summary>
    /// Получава активни Auth сесии от базата данни
    /// </summary>
    Task<List<AuthSessionEntity>> GetActiveAuthSessionsAsync();
}

