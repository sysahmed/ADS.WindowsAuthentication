using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Configuration;
using ADS.WindowsAuth.API.Models;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Authentication controller with JWT support
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthJwtController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IWindowsAuthService _windowsAuthService;
    private readonly IJwtService _jwtService;
    private readonly ILoggerService _logger;

    public AuthJwtController(
        ISessionService sessionService,
        IWindowsAuthService windowsAuthService,
        IJwtService jwtService,
        ILoggerService logger)
    {
        _sessionService = sessionService;
        _windowsAuthService = windowsAuthService;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new session and returns JWT token
    /// </summary>
    /// <param name="request">Optional request with user and domain</param>
    /// <returns>JWT token and session information</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ValidateCredentialsRequest? request = null)
    {
        try
        {
            AuthSession session;
            string? password = null;

            if (request != null && !string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Password))
            {
                // Validate credentials with password
                var isValid = await _windowsAuthService.ValidateCredentialsAsync(
                    request.Username,
                    request.Password,
                    request.Domain);

                if (!isValid)
                {
                    return Unauthorized(new { error = "Invalid credentials" });
                }

                // Create session with validated user
                session = await _sessionService.CreateSessionAsync(
                    request.Username,
                    request.Domain,
                    "API-JWT",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");

                // Запазваме паролата за автоматичен login
                password = request.Password;
            }
            else
            {
                // Create session with current Windows user
                var (username, domain) = _windowsAuthService.GetCurrentWindowsUser();
                session = await _sessionService.CreateSessionAsync(
                    username,
                    domain,
                    "API-JWT",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
            }

            // Create user info for JWT
            var userInfo = new UserInfo
            {
                Username = session.WindowsUsername,
                Domain = session.Domain,
                MachineName = session.MachineName,
                Roles = new[] { "User" } // Default role
            };

            // Generate JWT token
            var token = _jwtService.GenerateToken(userInfo, session);

            // Approve the session automatically for JWT flow (включително с парола ако има)
            _sessionService.ApproveSession(session.SessionId, session.WindowsUsername, password, session.Domain);

            return Ok(new
            {
                token = token,
                sessionId = session.SessionId,
                accessToken = session.AccessToken,
                user = userInfo,
                expiresAt = session.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"JWT login failed: {ex.Message}", ex);
            return StatusCode(500, new { error = "Login failed" });
        }
    }

    /// <summary>
    /// Validates JWT token
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    public IActionResult ValidateToken([FromBody] string token)
    {
        try
        {
            var result = _jwtService.ValidateToken(token);
            
            return Ok(new
            {
                isValid = result.IsValid,
                user = result.User,
                expiration = result.Expiration,
                errorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Token validation failed: {ex.Message}", ex);
            return StatusCode(500, new { error = "Token validation failed" });
        }
    }

    /// <summary>
    /// Gets current user information from JWT token
    /// </summary>
    /// <returns>User information</returns>
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        try
        {
            var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            var user = _jwtService.GetUserFromToken(token);

            if (user == null)
            {
                return Unauthorized(new { error = "Invalid token" });
            }

            return Ok(new
            {
                user = user,
                sessionId = User.FindFirst("session_id")?.Value,
                machineName = User.FindFirst("machine_name")?.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Get current user failed: {ex.Message}", ex);
            return StatusCode(500, new { error = "Failed to get user information" });
        }
    }

    /// <summary>
    /// Refreshes JWT token
    /// </summary>
    /// <returns>New JWT token</returns>
    [HttpPost("refresh")]
    [Authorize]
    public IActionResult RefreshToken()
    {
        try
        {
            var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            var user = _jwtService.GetUserFromToken(token);

            if (user == null)
            {
                return Unauthorized(new { error = "Invalid token" });
            }

            // Get session information
            var sessionId = User.FindFirst("session_id")?.Value;
            var accessToken = User.FindFirst("access_token")?.Value;

            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(accessToken))
            {
                return BadRequest(new { error = "Invalid session information" });
            }

            // Get session from session service
            var session = _sessionService.GetSessionByToken(accessToken);
            if (session == null || session.Status != SessionStatus.Approved)
            {
                return Unauthorized(new { error = "Session not found or not approved" });
            }

            // Generate new token
            var newToken = _jwtService.GenerateToken(user, session);

            return Ok(new
            {
                token = newToken,
                expiresAt = session.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Token refresh failed: {ex.Message}", ex);
            return StatusCode(500, new { error = "Token refresh failed" });
        }
    }

    /// <summary>
    /// Logs out user (invalidates session)
    /// </summary>
    /// <returns>Logout result</returns>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        try
        {
            var sessionId = User.FindFirst("session_id")?.Value;
            var accessToken = User.FindFirst("access_token")?.Value;

            if (!string.IsNullOrEmpty(sessionId))
            {
                // Reject the session
                _sessionService.RejectSession(sessionId);
                _logger.LogInfo($"JWT: User logged out from session {sessionId}");
            }

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Logout failed: {ex.Message}", ex);
            return StatusCode(500, new { error = "Logout failed" });
        }
    }
}
