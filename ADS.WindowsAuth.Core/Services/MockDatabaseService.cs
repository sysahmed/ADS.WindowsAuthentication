using ADS.WindowsAuth.Core.Data.Entities;
using ADS.WindowsAuth.Core.Services;
using System.Threading.Tasks;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Mock Database Service implementation that works without database
/// </summary>
public class MockDatabaseService : IDatabaseService
{
    private readonly ILoggerService _logger;

    public MockDatabaseService(ILoggerService logger)
    {
        _logger = logger;
        _logger.LogInfo("MockDatabaseService инициализиран - работи без база данни");
    }

    public async Task<int> SaveApplicationEventAsync(ApplicationEventEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на ApplicationEvent {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveFileActivityAsync(FileActivityEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на FileActivity {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveNetworkActivityAsync(NetworkActivityEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на NetworkActivity {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveSystemInfoAsync(SystemInfoEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на SystemInfo {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveUsbDeviceAsync(UsbDeviceEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на UsbDevice {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveScreenTimeAsync(ScreenTimeEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на ScreenTime {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveOrUpdateUserActivityAsync(UserActivityEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на UserActivity {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveOrUpdateAuthSessionAsync(AuthSessionEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на AuthSession {entity.SessionId} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<int> SaveWindowsEventAsync(WindowsEventEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на WindowsEvent {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<AdUserEntity?> GetOrCreateAdUserAsync(string username, string domain)
    {
        _logger.LogInfo($"Mock: Търсене на AdUser {username}@{domain} - връщам null (няма база данни)");
        return await Task.FromResult<AdUserEntity?>(null);
    }

    public async Task SyncActiveDirectoryAsync()
    {
        _logger.LogInfo("Mock: SyncActiveDirectory - пропуснат (няма база данни)");
        await Task.CompletedTask;
    }

    public async Task<int> SaveLoginEventAsync(LoginEventEntity entity)
    {
        _logger.LogInfo($"Mock: Запис на LoginEvent {entity.Id} - пропуснат (няма база данни)");
        return await Task.FromResult(1);
    }

    public async Task<List<AuthSessionEntity>> GetActiveAuthSessionsAsync()
    {
        _logger.LogInfo("Mock: GetActiveAuthSessionsAsync - връщам празен списък (няма база данни)");
        return await Task.FromResult(new List<AuthSessionEntity>());
    }
}
