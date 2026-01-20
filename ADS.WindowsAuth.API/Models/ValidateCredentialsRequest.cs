namespace ADS.WindowsAuth.API.Models;

/// <summary>
/// Request model for credential validation
/// </summary>
public class ValidateCredentialsRequest
{
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Domain
    /// </summary>
    public string Domain { get; set; } = string.Empty;
}
