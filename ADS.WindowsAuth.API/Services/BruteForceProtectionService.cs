using System.Collections.Concurrent;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADS.WindowsAuth.API.Services;

/// <summary>
/// Услуга за защита от брутфорс атаки.
/// След MaxFailedAttempts неуспешни опита блокира IP адреса постоянно.
/// Блокирането може да бъде премахнато само от администратор.
/// Настройките се четат от appsettings.json → секция "BruteForce".
/// </summary>
public class BruteForceProtectionService
{
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _attemptWindow;

    // IP -> брой неуспешни опити в текущия прозорец
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _attempts
        = new(StringComparer.OrdinalIgnoreCase);

    // Постоянно блокирани IP адреси (зареждат се от БД при старт)
    private readonly ConcurrentHashSet _blockedIps = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BruteForceProtectionService> _logger;

    public BruteForceProtectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<BruteForceProtectionService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxFailedAttempts = configuration.GetValue<int>("BruteForce:MaxFailedAttempts", 5);
        _attemptWindow = TimeSpan.FromMinutes(configuration.GetValue<double>("BruteForce:AttemptWindowMinutes", 10));
        _logger.LogInformation("BruteForce: MaxFailedAttempts={Max}, AttemptWindow={Win}min",
            _maxFailedAttempts, _attemptWindow.TotalMinutes);
    }

    /// <summary>Зарежда блокираните IP адреси от БД при стартиране.</summary>
    public async Task LoadBlockedIpsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var blocked = await db.BlockedIps
                .Where(b => b.UnblockedAt == null)
                .Select(b => b.IpAddress)
                .ToListAsync();

            foreach (var ip in blocked)
                _blockedIps.Add(ip);

            _logger.LogInformation("Заредени {Count} блокирани IP адреса от БД", blocked.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Не можах да заредя блокирани IP от БД: {Msg}", ex.Message);
        }
    }

    /// <summary>Проверява дали IP е постоянно блокиран.</summary>
    public bool IsBlocked(string ip) => _blockedIps.Contains(ip);

    /// <summary>
    /// Регистрира неуспешен опит за даден IP.
    /// Ако достигне прага → блокира IP постоянно.
    /// Връща true ако IP е блокиран (сега или вече).
    /// </summary>
    public async Task<bool> RegisterFailedAttemptAsync(string ip)
    {
        if (_blockedIps.Contains(ip))
            return true;

        var now = DateTime.UtcNow;
        var entry = _attempts.AddOrUpdate(
            ip,
            _ => (1, now),
            (_, existing) =>
            {
                if (now - existing.WindowStart > _attemptWindow)
                    return (1, now);
                return (existing.Count + 1, existing.WindowStart);
            });

        _logger.LogWarning("BRUTE FORCE: IP {Ip} – опит {Count}/{Max}", ip, entry.Count, _maxFailedAttempts);

        if (entry.Count >= _maxFailedAttempts)
        {
            await BlockIpAsync(ip, entry.Count);
            return true;
        }

        return false;
    }

    /// <summary>Изчиства неуспешните опити след успешен вход.</summary>
    public void ClearAttempts(string ip) => _attempts.TryRemove(ip, out _);

    /// <summary>Блокира IP адрес постоянно и го записва в БД.</summary>
    private async Task BlockIpAsync(string ip, int failedCount)
    {
        if (!_blockedIps.Add(ip))
            return; // вече е блокиран

        _logger.LogWarning("SECURITY: IP {Ip} е блокиран след {Count} неуспешни опита", ip, failedCount);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Проверяваме дали вече има запис
            var existing = await db.BlockedIps
                .Where(b => b.IpAddress == ip && b.UnblockedAt == null)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                db.BlockedIps.Add(new BlockedIpEntity
                {
                    IpAddress = ip,
                    FailedAttempts = failedCount,
                    BlockedAt = DateTime.UtcNow,
                    LastAttemptAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при запис на блокиран IP {Ip}: {Msg}", ip, ex.Message);
        }
    }

    /// <summary>
    /// Деблокира IP адрес (само за администратори).
    /// Връща false ако IP не е намерен.
    /// </summary>
    public async Task<bool> UnblockIpAsync(string ip, string adminUsername, string? reason = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var record = await db.BlockedIps
                .Where(b => b.IpAddress == ip && b.UnblockedAt == null)
                .FirstOrDefaultAsync();

            if (record == null)
                return false;

            record.UnblockedAt = DateTime.UtcNow;
            record.UnblockedBy = adminUsername;
            record.UnblockReason = reason;
            await db.SaveChangesAsync();

            _blockedIps.Remove(ip);
            _attempts.TryRemove(ip, out _);

            _logger.LogInformation("SECURITY: IP {Ip} деблокиран от администратор {Admin}. Причина: {Reason}",
                ip, adminUsername, reason ?? "не е посочена");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при деблокиране на IP {Ip}: {Msg}", ip, ex.Message);
            return false;
        }
    }

    /// <summary>Списък с всички блокирани IP адреси от БД.</summary>
    public async Task<List<BlockedIpEntity>> GetBlockedIpsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.BlockedIps
            .OrderByDescending(b => b.BlockedAt)
            .ToListAsync();
    }

    /// <summary>Брой оставащи неуспешни опити преди блокиране.</summary>
    public int RemainingAttempts(string ip)
    {
        if (_attempts.TryGetValue(ip, out var entry))
        {
            if (DateTime.UtcNow - entry.WindowStart <= _attemptWindow)
                return Math.Max(0, _maxFailedAttempts - entry.Count);
        }
        return _maxFailedAttempts;
    }
}

/// <summary>Thread-safe HashSet за IP адреси.</summary>
internal sealed class ConcurrentHashSet
{
    private readonly ConcurrentDictionary<string, byte> _dict
        = new(StringComparer.OrdinalIgnoreCase);

    public bool Add(string value) => _dict.TryAdd(value, 0);
    public bool Contains(string value) => _dict.ContainsKey(value);
    public void Remove(string value) => _dict.TryRemove(value, out _);
}
