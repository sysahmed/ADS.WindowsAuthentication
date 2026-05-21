using ADS.WindowsAuth.Core.Models;
using System.Collections.Concurrent;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Имплементация на remote desktop session service
/// </summary>
public class RemoteDesktopSessionService : IRemoteDesktopSessionService
{
    private readonly ILoggerService _logger;
    private readonly ConcurrentDictionary<string, RemoteDesktopSession> _sessions;

    public RemoteDesktopSessionService(ILoggerService logger)
    {
        _logger = logger;
        _sessions = new ConcurrentDictionary<string, RemoteDesktopSession>();
    }

    /// <inheritdoc/>
    public Task<string> CreateSessionAsync(string machineName, string? requestedBy = null)
    {
        try
        {
            // Генерираме 6-символен session ID
            string sessionId = GenerateSessionId();

            var session = new RemoteDesktopSession
            {
                SessionId = sessionId,
                MachineName = machineName,
                RequestedByUser = requestedBy,
                CreatedAt = DateTime.Now,
                LastActivity = DateTime.Now,
                IsAuthorized = false,
                ControlEnabled = false
            };

            if (_sessions.TryAdd(sessionId, session))
            {
                _logger.LogInfo($"Създадена Remote Desktop сесия: {sessionId} за {machineName}");
                return Task.FromResult(sessionId);
            }

            // Ако има collision, опитваме отново
            return CreateSessionAsync(machineName, requestedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при създаване на сесия: {ex.Message}", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<RemoteDesktopSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public Task<List<RemoteDesktopSession>> GetActiveSessionsAsync()
    {
        var activeSessions = _sessions.Values
            .Where(s => !s.IsExpired)
            .ToList();
        
        return Task.FromResult(activeSessions);
    }

    /// <inheritdoc/>
    public Task<RemoteDesktopSession?> GetSessionByMachineAsync(string machineName)
    {
        var session = _sessions.Values
            .FirstOrDefault(s => s.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) 
                              && !s.IsExpired);
        
        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public Task RegisterHostAsync(string sessionId, string connectionId, bool autoApprove = false)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.HostConnectionId = connectionId;
            session.AutoApprove = autoApprove;
            session.LastActivity = DateTime.Now;
            _logger.LogInfo($"Host регистриран за сесия {sessionId}: {connectionId}, AutoApprove={autoApprove}");
        }
        else
        {
            _logger.LogWarning($"Опит за регистрация на host за несъществуваща сесия: {sessionId}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<(string sessionId, bool created)> CreateOrGetSessionAsync(string machineName, string? requestedBy = null)
    {
        var existing = await GetSessionByMachineAsync(machineName);
        if (existing != null)
        {
            existing.LastActivity = DateTime.Now;
            return (existing.SessionId, false);
        }
        var sessionId = await CreateSessionAsync(machineName, requestedBy);
        return (sessionId, true);
    }

    /// <inheritdoc/>
    public Task RegisterViewerAsync(string sessionId, string connectionId, string userId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ViewerConnectionId = connectionId;
            session.RequestedByUser = userId;
            session.LastActivity = DateTime.Now;
            _logger.LogInfo($"Viewer регистриран за сесия {sessionId}: {userId} ({connectionId})");
        }
        else
        {
            _logger.LogWarning($"Опит за регистрация на viewer за несъществуваща сесия: {sessionId}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task AuthorizeControlAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsAuthorized = true;
            session.ControlEnabled = true;
            session.LastActivity = DateTime.Now;
            _logger.LogInfo($"Контрол одобрен за сесия {sessionId}");
        }
        else
        {
            _logger.LogWarning($"Опит за одобрение на несъществуваща сесия: {sessionId}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DenyControlAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsAuthorized = false;
            session.ControlEnabled = false;
            _logger.LogInfo($"Контрол отказан за сесия {sessionId}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateActivityAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivity = DateTime.Now;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearHostConnectionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.HostConnectionId = null;
            session.ControlEnabled = false;
            _logger.LogInfo($"Host connection изчистен за сесия {sessionId}");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearViewerConnectionAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.ViewerConnectionId = null;
            session.ControlEnabled = false;
            _logger.LogInfo($"Viewer connection изчистен за сесия {sessionId}");
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EndSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            _logger.LogInfo($"Сесия приключена: {sessionId} ({session.MachineName})");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = _sessions.Values
            .Where(s => s.IsExpired)
            .Select(s => s.SessionId)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            _sessions.TryRemove(sessionId, out _);
            _logger.LogInfo($"Премахната изтекла сесия: {sessionId}");
        }

        return Task.CompletedTask;
    }

    private static string GenerateSessionId()
    {
        // Генерираме 6-символен код (само главни букви и цифри)
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Избягваме объркващи символи като 0, O, 1, I
        var random = new Random();
        var sessionId = new char[6];

        for (int i = 0; i < 6; i++)
        {
            sessionId[i] = chars[random.Next(chars.Length)];
        }

        return new string(sessionId);
    }
}
