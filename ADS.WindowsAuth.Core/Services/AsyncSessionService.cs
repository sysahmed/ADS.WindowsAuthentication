using ADS.WindowsAuth.Core.Models;
using System.Collections.Concurrent;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Async implementation of session management service
/// </summary>
public class AsyncSessionService : IAsyncSessionService
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions;
    private readonly ILoggerService _logger;

    public AsyncSessionService(ILoggerService logger)
    {
        _sessions = new ConcurrentDictionary<string, AuthSession>();
        _logger = logger;
    }

    public async Task<AuthSession> CreateSessionAsync()
    {
        return await Task.Run(() => CreateSessionInternal());
    }

    public async Task<AuthSession> CreateSessionAsync(string username, string domain)
    {
        return await Task.Run(() => CreateSessionInternal(username, domain));
    }

    public async Task<AuthSession?> GetSessionByTokenAsync(string accessToken)
    {
        return await Task.Run(() => GetSessionByTokenInternal(accessToken));
    }

    public async Task<bool> ApproveSessionAsync(string sessionId)
    {
        return await Task.Run(() => ApproveSessionInternal(sessionId));
    }

    public async Task<bool> RejectSessionAsync(string sessionId)
    {
        return await Task.Run(() => RejectSessionInternal(sessionId));
    }

    public async Task<int> CleanupExpiredSessionsAsync()
    {
        return await Task.Run(() => CleanupExpiredSessionsInternal());
    }

    public async Task<IEnumerable<AuthSession>> GetAllSessionsAsync()
    {
        return await Task.Run(() => _sessions.Values.ToList());
    }

    private AuthSession CreateSessionInternal()
    {
        try
        {
            var session = new AuthSession
            {
                SessionId = Guid.NewGuid().ToString(),
                AccessToken = Guid.NewGuid().ToString(),
                MachineName = Environment.MachineName,
                WindowsUsername = Environment.UserName,
                Domain = Environment.UserDomainName,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(1),
                Status = SessionStatus.Pending
            };

            _sessions.TryAdd(session.SessionId, session);

            if (_logger is EnhancedLoggerService enhancedLogger)
            {
                enhancedLogger.LogSessionCreated(
                    session.SessionId, 
                    session.WindowsUsername, 
                    session.Domain, 
                    session.MachineName);
            }
            else
            {
                _logger.LogInfo($"Session created: {session.SessionId} for {session.WindowsUsername}@{session.Domain}");
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating session: {ex.Message}", ex);
            throw;
        }
    }

    private AuthSession CreateSessionInternal(string username, string domain)
    {
        try
        {
            var session = new AuthSession
            {
                SessionId = Guid.NewGuid().ToString(),
                AccessToken = Guid.NewGuid().ToString(),
                MachineName = Environment.MachineName,
                WindowsUsername = username,
                Domain = domain,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddHours(1),
                Status = SessionStatus.Pending
            };

            _sessions.TryAdd(session.SessionId, session);

            if (_logger is EnhancedLoggerService enhancedLogger)
            {
                enhancedLogger.LogSessionCreated(
                    session.SessionId,
                    session.WindowsUsername,
                    session.Domain,
                    session.MachineName);
            }
            else
            {
                _logger.LogInfo($"Session created: {session.SessionId} for {username}@{domain}");
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating session for {username}@{domain}: {ex.Message}", ex);
            throw;
        }
    }

    private AuthSession? GetSessionByTokenInternal(string accessToken)
    {
        try
        {
            var session = _sessions.Values.FirstOrDefault(s => s.AccessToken == accessToken);
            
            if (session == null)
            {
                _logger.LogWarning($"Session not found for access token: {accessToken}");
                return null;
            }

            // Check if session is expired
            if (DateTime.Now > session.ExpiresAt && session.Status != SessionStatus.Expired)
            {
                session.Status = SessionStatus.Expired;
                _logger.LogWarning($"Session {session.SessionId} has expired");
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting session by token: {ex.Message}", ex);
            return null;
        }
    }

    private bool ApproveSessionInternal(string sessionId)
    {
        try
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                var oldStatus = session.Status;
                session.Status = SessionStatus.Approved;

                if (_logger is EnhancedLoggerService enhancedLogger)
                {
                    enhancedLogger.LogSessionStatusChanged(sessionId, oldStatus.ToString(), SessionStatus.Approved.ToString());
                }
                else
                {
                    _logger.LogInfo($"Session {sessionId} approved");
                }

                return true;
            }

            _logger.LogWarning($"Session not found for approval: {sessionId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error approving session {sessionId}: {ex.Message}", ex);
            return false;
        }
    }

    private bool RejectSessionInternal(string sessionId)
    {
        try
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                var oldStatus = session.Status;
                session.Status = SessionStatus.Rejected;

                if (_logger is EnhancedLoggerService enhancedLogger)
                {
                    enhancedLogger.LogSessionStatusChanged(sessionId, oldStatus.ToString(), SessionStatus.Rejected.ToString());
                }
                else
                {
                    _logger.LogInfo($"Session {sessionId} rejected");
                }

                return true;
            }

            _logger.LogWarning($"Session not found for rejection: {sessionId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error rejecting session {sessionId}: {ex.Message}", ex);
            return false;
        }
    }

    private int CleanupExpiredSessionsInternal()
    {
        try
        {
            var expiredSessions = _sessions.Values
                .Where(s => DateTime.Now > s.ExpiresAt && s.Status != SessionStatus.Expired)
                .ToList();

            var cleanedCount = 0;
            foreach (var session in expiredSessions)
            {
                session.Status = SessionStatus.Expired;
                cleanedCount++;
            }

            if (cleanedCount > 0)
            {
                _logger.LogInfo($"Cleaned up {cleanedCount} expired sessions");
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error cleaning up expired sessions: {ex.Message}", ex);
            return 0;
        }
    }
}
