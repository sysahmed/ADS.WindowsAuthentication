using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Сервис за работа с базата данни
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly ApplicationDbContext _context;
    private readonly ILoggerService _logger;
    private readonly IAdService? _adService;

    public DatabaseService(ApplicationDbContext context, ILoggerService logger, IAdService? adService = null)
    {
        _context = context;
        _logger = logger;
        _adService = adService;
    }

    public async Task<int> SaveApplicationEventAsync(ApplicationEventEntity entity)
    {
        try
        {
            // Свързване с AD потребител ако е възможно
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            _context.ApplicationEvents.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на Application Event", ex);
            throw;
        }
    }

    public async Task<int> SaveFileActivityAsync(FileActivityEntity entity)
    {
        try
        {
            // Свързване с AD потребител
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            _context.FileActivities.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на File Activity", ex);
            throw;
        }
    }

    public async Task<int> SaveNetworkActivityAsync(NetworkActivityEntity entity)
    {
        try
        {
            // Свързване с AD потребител
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            _context.NetworkActivities.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на Network Activity", ex);
            throw;
        }
    }

    public async Task<int> SaveSystemInfoAsync(SystemInfoEntity entity)
    {
        try
        {
            _context.SystemInfos.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на System Info", ex);
            throw;
        }
    }

    public async Task<int> SaveUsbDeviceAsync(UsbDeviceEntity entity)
    {
        try
        {
            // Свързване с AD потребител
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            _context.UsbDevices.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на USB Device", ex);
            throw;
        }
    }

    public async Task<int> SaveScreenTimeAsync(ScreenTimeEntity entity)
    {
        try
        {
            // Свързване с AD потребител
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            _context.ScreenTimes.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на Screen Time", ex);
            throw;
        }
    }

    public async Task<int> SaveOrUpdateUserActivityAsync(UserActivityEntity entity)
    {
        try
        {
            // Свързване с AD потребител
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            var existing = await _context.UserActivities
                .FirstOrDefaultAsync(a => a.Username == entity.Username && 
                                          a.MachineName == entity.MachineName && 
                                          a.EndTime == null);

            if (existing != null)
            {
                // Обновяване на съществуваща активност
                existing.EndTime = entity.EndTime;
                existing.ScreenTimeSeconds = entity.ScreenTimeSeconds;
                existing.AdUserId = entity.AdUserId;
                await _context.SaveChangesAsync();
                return existing.Id;
            }
            else
            {
                // Създаване на нова активност
                _context.UserActivities.Add(entity);
                await _context.SaveChangesAsync();
                return entity.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на User Activity", ex);
            throw;
        }
    }

    public async Task<int> SaveOrUpdateAuthSessionAsync(AuthSessionEntity entity)
    {
        try
        {
            // Проверка дали context-ът е disposed
            if (_context == null)
            {
                _logger.LogWarning("ApplicationDbContext е null. Сесията няма да бъде записана.");
                return 0;
            }

            // Бърза проверка дали базата данни е достъпна (без блокиране)
            try
            {
                // Използваме кратък timeout за проверка
                using (var quickCheckCts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                {
                    var canConnect = await Task.Run(async () =>
                    {
                        try
                        {
                            return await _context.Database.CanConnectAsync(quickCheckCts.Token);
                        }
                        catch (ObjectDisposedException)
                        {
                            return false;
                        }
                        catch (OperationCanceledException)
                        {
                            return false;
                        }
                    }, quickCheckCts.Token);

                    if (!canConnect)
                    {
                        _logger.LogWarning("Базата данни не е достъпна. Сесията няма да бъде записана.");
                        return 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout при проверка на базата данни. Сесията няма да бъде записана.");
                return 0;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("ApplicationDbContext е disposed при проверка. Сесията няма да бъде записана.");
                return 0;
            }

            // Използваме timeout за основната операция
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    // Свързване с AD потребител (не блокира ако има проблем)
                    try
                    {
                        if (!string.IsNullOrEmpty(entity.WindowsUsername) && !string.IsNullOrEmpty(entity.Domain))
                        {
                            var adUserTask = GetOrCreateAdUserAsync(entity.WindowsUsername, entity.Domain);
                            var adUserTimeout = Task.Delay(3000, cts.Token);
                            var completedTask = await Task.WhenAny(adUserTask, adUserTimeout);

                            if (completedTask == adUserTask)
                            {
                                var adUser = await adUserTask;
                                if (adUser != null)
                                {
                                    entity.AdUserId = adUser.Id;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Timeout при свързване с AD потребител (продължаваме без AD свързване)");
                            }
                        }
                    }
                    catch (Exception adEx)
                    {
                        _logger.LogWarning($"Грешка при свързване с AD потребител (продължаваме без AD свързване): {adEx.Message}");
                    }

                    // Запис на сесия с timeout
                    AuthSessionEntity? existing = null;
                    try
                    {
                        var existingTask = _context.AuthSessions
                            .FirstOrDefaultAsync(s => s.SessionId == entity.SessionId, cts.Token);
                        var existingTimeout = Task.Delay(3000, cts.Token);
                        var existingCompleted = await Task.WhenAny(existingTask, existingTimeout);

                        if (existingCompleted != existingTask)
                        {
                            _logger.LogWarning("Timeout при търсене на сесия в базата данни. Пропускаме записа.");
                            cts.Cancel(); // Отменяме операцията
                            return 0;
                        }

                        existing = await existingTask;
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogWarning("ApplicationDbContext е disposed при търсене на сесия. Пропускаме записа.");
                        return 0;
                    }

                    if (existing != null)
                    {
                        // Обновяване на съществуваща сесия
                        try
                        {
                            existing.Status = entity.Status;
                            existing.ApprovedAt = entity.ApprovedAt;
                            existing.RejectedAt = entity.RejectedAt;
                            existing.AdUserId = entity.AdUserId;

                            var saveTask = _context.SaveChangesAsync(cts.Token);
                            var saveTimeout = Task.Delay(3000, cts.Token);
                            if (await Task.WhenAny(saveTask, saveTimeout) == saveTask)
                            {
                                await saveTask;
                                return existing.Id;
                            }
                            else
                            {
                                _logger.LogWarning("Timeout при запис на сесия в базата данни.");
                                cts.Cancel(); // Отменяме операцията
                                return 0;
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            _logger.LogWarning("ApplicationDbContext е disposed при запис на сесия.");
                            return 0;
                        }
                    }
                    else
                    {
                        // Създаване на нова сесия
                        try
                        {
                            _context.AuthSessions.Add(entity);

                            var saveTask = _context.SaveChangesAsync(cts.Token);
                            var saveTimeout = Task.Delay(3000, cts.Token);
                            if (await Task.WhenAny(saveTask, saveTimeout) == saveTask)
                            {
                                await saveTask;
                                return entity.Id;
                            }
                            else
                            {
                                _logger.LogWarning("Timeout при запис на нова сесия в базата данни.");
                                cts.Cancel(); // Отменяме операцията
                                return 0;
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            _logger.LogWarning("ApplicationDbContext е disposed при запис на нова сесия.");
                            return 0;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogWarning("ApplicationDbContext е disposed при основна операция за запис на сесия.");
                    return 0;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Операцията за запис на сесия е отменена поради timeout.");
            return 0;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning($"ApplicationDbContext е disposed при запис на Auth Session: {ex.Message}");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на Auth Session", ex);
            // Не хвърляме exception - сесията все пак е създадена в паметта
            return 0;
        }
    }

    public async Task<int> SaveWindowsEventAsync(WindowsEventEntity entity)
    {
        try
        {
            // Свързване с AD потребител ако има username
            if (!string.IsNullOrEmpty(entity.Username))
            {
                var parts = entity.Username.Split('\\');
                if (parts.Length == 2)
                {
                    var adUser = await GetOrCreateAdUserAsync(parts[1], parts[0]);
                    if (adUser != null)
                    {
                        entity.AdUserId = adUser.Id;
                    }
                }
            }

            _context.WindowsEvents.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на Windows Event", ex);
            throw;
        }
    }

    public async Task<AdUserEntity?> GetOrCreateAdUserAsync(string username, string domain)
    {
        try
        {
            // Проверка дали context-ът е disposed
            if (_context == null)
            {
                _logger.LogWarning($"ApplicationDbContext е null при търсене на AD потребител {username}");
                return null;
            }

            // Търсене в базата данни
            AdUserEntity? adUser;
            try
            {
                adUser = await _context.AdUsers
                    .FirstOrDefaultAsync(u => u.Username == username);
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning($"ApplicationDbContext е disposed при търсене на AD потребител {username}");
                return null;
            }

            if (adUser != null)
            {
                return adUser;
            }

            // Ако няма в базата и имаме AD сервис, опитваме се да го синхронизираме
            if (_adService != null && _adService.IsEnabled)
            {
                var adInfo = _adService.GetUserInfo(username);
                if (adInfo != null)
                {
                    adUser = new AdUserEntity
                    {
                        Username = adInfo.Username,
                        DisplayName = adInfo.DisplayName,
                        Email = adInfo.Email,
                        DistinguishedName = adInfo.DistinguishedName,
                        IsEnabled = adInfo.IsEnabled,
                        SyncedAt = DateTime.Now
                    };

                    try
                    {
                        _context.AdUsers.Add(adUser);
                        await _context.SaveChangesAsync();
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogWarning($"ApplicationDbContext е disposed при запис на AD потребител {username}");
                        return null;
                    }

                    // Добавяне на групи
                    if (adInfo.Groups.Any())
                    {
                        foreach (var groupName in adInfo.Groups)
                        {
                            var group = await GetOrCreateAdGroupAsync(groupName);
                            if (group != null)
                            {
                                var userGroup = new AdUserGroupEntity
                                {
                                    UserId = adUser.Id,
                                    GroupId = group.Id
                                };
                                _context.AdUserGroups.Add(userGroup);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    return adUser;
                }
            }

            // Ако няма в AD, създаваме базов запис
            adUser = new AdUserEntity
            {
                Username = username,
                DistinguishedName = $"CN={username},CN=Users,DC={domain.Replace(".", ",DC=")}",
                IsEnabled = true,
                SyncedAt = DateTime.Now
            };

            try
            {
                _context.AdUsers.Add(adUser);
                await _context.SaveChangesAsync();
                return adUser;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning($"ApplicationDbContext е disposed при създаване на AD потребител {username}");
                return null;
            }
        }
        catch (ObjectDisposedException)
        {
            _logger.LogWarning($"ApplicationDbContext е disposed при получаване/създаване на AD потребител {username}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване/създаване на AD потребител {username}", ex);
            return null;
        }
    }

    private async Task<AdGroupEntity?> GetOrCreateAdGroupAsync(string groupName)
    {
        try
        {
            var group = await _context.AdGroups
                .FirstOrDefaultAsync(g => g.GroupName == groupName);

            if (group == null)
            {
                group = new AdGroupEntity
                {
                    GroupName = groupName,
                    DistinguishedName = $"CN={groupName},CN=Users",
                    SyncedAt = DateTime.Now
                };
                _context.AdGroups.Add(group);
                await _context.SaveChangesAsync();
            }

            return group;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване/създаване на AD група {groupName}", ex);
            return null;
        }
    }

    public async Task SyncActiveDirectoryAsync()
    {
        if (_adService == null || !_adService.IsEnabled)
        {
            _logger.LogWarning("AD синхронизация е пропусната - AD сервисът не е активиран");
            return;
        }

        try
        {
            _logger.LogInfo("Започва синхронизация на Active Directory...");

            var adUsers = _adService.GetAllUsers();
            int syncedCount = 0;

            foreach (var adInfo in adUsers)
            {
                var adUser = await _context.AdUsers
                    .FirstOrDefaultAsync(u => u.Username == adInfo.Username);

                if (adUser == null)
                {
                    // Нов потребител
                    adUser = new AdUserEntity
                    {
                        Username = adInfo.Username,
                        DisplayName = adInfo.DisplayName,
                        Email = adInfo.Email,
                        DistinguishedName = adInfo.DistinguishedName,
                        IsEnabled = adInfo.IsEnabled,
                        SyncedAt = DateTime.Now
                    };
                    _context.AdUsers.Add(adUser);
                }
                else
                {
                    // Обновяване на съществуващ
                    adUser.DisplayName = adInfo.DisplayName;
                    adUser.Email = adInfo.Email;
                    adUser.IsEnabled = adInfo.IsEnabled;
                    adUser.UpdatedAt = DateTime.Now;
                    adUser.SyncedAt = DateTime.Now;
                }

                syncedCount++;
            }

            await _context.SaveChangesAsync();
            _logger.LogInfo($"Синхронизирани {syncedCount} AD потребители");
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при синхронизация на Active Directory", ex);
        }
    }

    public async Task<int> SaveLoginEventAsync(LoginEventEntity entity)
    {
        try
        {
            // Свързване с AD потребител
            if (!string.IsNullOrEmpty(entity.Username) && !string.IsNullOrEmpty(entity.Domain))
            {
                var adUser = await GetOrCreateAdUserAsync(entity.Username, entity.Domain);
                if (adUser != null)
                {
                    entity.AdUserId = adUser.Id;
                }
            }

            _context.LoginEvents.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на Login Event", ex);
            throw;
        }
    }

    public async Task<List<AuthSessionEntity>> GetActiveAuthSessionsAsync()
    {
        try
        {
            var now = DateTime.Now;
            var activeSessions = await _context.AuthSessions
                .Where(s => s.ExpiresAt > now && s.Status != "Expired")
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            
            _logger.LogInfo($"Получени {activeSessions.Count} активни сесии от базата данни");
            return activeSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на активни сесии от базата данни: {ex.Message}", ex);
            return new List<AuthSessionEntity>();
        }
    }
}

