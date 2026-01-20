using ADS.WindowsAuth.Core.Models;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// JWT token service interface
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates JWT token for authenticated user
    /// </summary>
    /// <param name="user">User information</param>
    /// <param name="session">Session information</param>
    /// <returns>JWT token</returns>
    string GenerateToken(UserInfo user, AuthSession session);

    /// <summary>
    /// Validates JWT token
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>Token validation result</returns>
    TokenValidationResult ValidateToken(string token);

    /// <summary>
    /// Gets user information from token
    /// </summary>
    /// <param name="token">JWT token</param>
    /// <returns>User information or null if invalid</returns>
    UserInfo? GetUserFromToken(string token);
}

/// <summary>
/// User information for JWT token
/// </summary>
public class UserInfo
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Token validation result
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public UserInfo? User { get; set; }
    public DateTime? Expiration { get; set; }
}
