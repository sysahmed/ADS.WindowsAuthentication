using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Configuration;
using ADS.WindowsAuth.API.Models;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Async authentication controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AsyncAuthController : ControllerBase
{
    private readonly IAsyncSessionService _sessionService;
    private readonly IWindowsAuthService _windowsAuthService;
    private readonly ILoggerService _logger;

    public AsyncAuthController(
        IAsyncSessionService sessionService,
        IWindowsAuthService windowsAuthService,
        ILoggerService logger)
    {
        _sessionService = sessionService;
        _windowsAuthService = windowsAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new session asynchronously
    /// </summary>
    /// <param name="request">Optional request with user and domain</param>
    /// <returns>Created session</returns>
    [HttpPost("session")]
    public async Task<IActionResult> CreateSessionAsync([FromBody] CreateSessionRequest? request = null)
    {
        try
        {
            AuthSession session;

            if (request != null && !string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Domain))
            {
                _logger.LogInfo($"Creating session for user: {request.Username}@{request.Domain}");
                session = await _sessionService.CreateSessionAsync(request.Username, request.Domain);
            }
            else
            {
                _logger.LogInfo("Creating session with current user");
                session = await _sessionService.CreateSessionAsync();
            }

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating session: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to create session" });
        }
    }

    /// <summary>
    /// Gets session by access token asynchronously
    /// </summary>
    /// <param name="accessToken">Access token</param>
    /// <returns>Session information</returns>
    [HttpGet("session/{accessToken}")]
    public async Task<IActionResult> GetSessionByTokenAsync(string accessToken)
    {
        try
        {
            var session = await _sessionService.GetSessionByTokenAsync(accessToken);

            if (session == null)
            {
                return NotFound(new { error = "Session not found or expired" });
            }

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting session by token: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to get session" });
        }
    }

    /// <summary>
    /// Approves session asynchronously
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Approval result</returns>
    [HttpPost("approve/{sessionId}")]
    public async Task<IActionResult> ApproveSessionAsync(string sessionId)
    {
        try
        {
            var result = await _sessionService.ApproveSessionAsync(sessionId);

            if (!result)
            {
                return NotFound(new { error = "Session not found" });
            }

            return Ok(new { message = "Session approved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error approving session {sessionId}: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to approve session" });
        }
    }

    /// <summary>
    /// Rejects session asynchronously
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Rejection result</returns>
    [HttpPost("reject/{sessionId}")]
    public async Task<IActionResult> RejectSessionAsync(string sessionId)
    {
        try
        {
            var result = await _sessionService.RejectSessionAsync(sessionId);

            if (!result)
            {
                return NotFound(new { error = "Session not found" });
            }

            return Ok(new { message = "Session rejected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error rejecting session {sessionId}: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to reject session" });
        }
    }

    /// <summary>
    /// Gets all active sessions asynchronously (for debugging)
    /// </summary>
    /// <returns>List of active sessions</returns>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllSessionsAsync()
    {
        try
        {
            var sessions = await _sessionService.GetAllSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting all sessions: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to get sessions" });
        }
    }

    /// <summary>
    /// Cleans up expired sessions asynchronously
    /// </summary>
    /// <returns>Cleanup result</returns>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupExpiredSessionsAsync()
    {
        try
        {
            var cleanedCount = await _sessionService.CleanupExpiredSessionsAsync();
            return Ok(new { 
                message = $"Cleaned up {cleanedCount} expired sessions",
                cleanedCount = cleanedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error cleaning up expired sessions: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to cleanup sessions" });
        }
    }

    /// <summary>
    /// Validates credentials asynchronously
    /// </summary>
    /// <param name="request">Validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCredentialsAsync([FromBody] ValidateCredentialsRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Password) || 
                string.IsNullOrWhiteSpace(request.Domain))
            {
                return BadRequest(new { error = "Username, password, and domain are required" });
            }

            var result = await _windowsAuthService.ValidateCredentialsAsync(
                request.Username, 
                request.Password, 
                request.Domain);

            return Ok(new { 
                isValid = result,
                username = request.Username,
                domain = request.Domain
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error validating credentials: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to validate credentials" });
        }
    }
}
