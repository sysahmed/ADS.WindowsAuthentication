using ADS.WindowsAuth.Core.Models;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Async interface for session management
/// </summary>
public interface IAsyncSessionService
{
    /// <summary>
    /// Creates a new authentication session asynchronously
    /// </summary>
    /// <returns>The created session</returns>
    Task<AuthSession> CreateSessionAsync();

    /// <summary>
    /// Creates a new authentication session with specified user and domain asynchronously
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="domain">Domain</param>
    /// <returns>The created session</returns>
    Task<AuthSession> CreateSessionAsync(string username, string domain);

    /// <summary>
    /// Gets session by access token asynchronously
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <returns>The session or null if not found</returns>
    Task<AuthSession?> GetSessionByTokenAsync(string accessToken);

    /// <summary>
    /// Approves session asynchronously
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Whether the operation was successful</returns>
    Task<bool> ApproveSessionAsync(string sessionId);

    /// <summary>
    /// Rejects session asynchronously
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <returns>Whether the operation was successful</returns>
    Task<bool> RejectSessionAsync(string sessionId);

    /// <summary>
    /// Cleans up expired sessions asynchronously
    /// </summary>
    /// <returns>Number of cleaned up sessions</returns>
    Task<int> CleanupExpiredSessionsAsync();

    /// <summary>
    /// Gets all active sessions asynchronously (for debugging)
    /// </summary>
    /// <returns>List of active sessions</returns>
    Task<IEnumerable<AuthSession>> GetAllSessionsAsync();
}
