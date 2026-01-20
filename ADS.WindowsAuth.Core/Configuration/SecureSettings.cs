using System.ComponentModel.DataAnnotations;

namespace ADS.WindowsAuth.Core.Configuration;

/// <summary>
/// Secure configuration settings that should not be stored in appsettings.json
/// </summary>
public class SecureSettings
{
    /// <summary>
    /// Active Directory service account password
    /// </summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string ActiveDirectoryServicePassword { get; set; } = string.Empty;
    
    /// <summary>
    /// JWT signing key
    /// </summary>
    [Required]
    [StringLength(256, MinimumLength = 32)]
    public string JwtKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Database connection string (if different from appsettings)
    /// </summary>
    [StringLength(1024)]
    public string? DatabaseConnectionString { get; set; }
}

/// <summary>
/// Active Directory configuration with validation
/// </summary>
public class ActiveDirectorySettings
{
    public bool Enabled { get; set; }
    
    [RequiredIf("Enabled", true)]
    [StringLength(256, MinimumLength = 1)]
    public string DomainName { get; set; } = string.Empty;
    
    [StringLength(512)]
    public string LdapPath { get; set; } = string.Empty;
    
    [RequiredIf("Enabled", true)]
    [StringLength(256, MinimumLength = 1)]
    public string ServiceAccount { get; set; } = string.Empty;
    
    [StringLength(256)]
    public string ServicePassword { get; set; } = string.Empty;
    
    [Range(1, 1440)]
    public int SyncIntervalMinutes { get; set; } = 60;
    
    /// <summary>
    /// Validates the configuration
    /// </summary>
    public bool IsValid()
    {
        if (!Enabled) return true;
        
        return !string.IsNullOrWhiteSpace(DomainName) && 
               !string.IsNullOrWhiteSpace(ServiceAccount);
    }
}

/// <summary>
/// JWT configuration
/// </summary>
public class JwtSettings
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Issuer { get; set; } = string.Empty;
    
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Audience { get; set; } = string.Empty;
    
    [Required]
    [StringLength(256, MinimumLength = 32)]
    public string Key { get; set; } = string.Empty;
    
    [Range(5, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
    
    /// <summary>
    /// Validates the JWT configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Issuer) && 
               !string.IsNullOrWhiteSpace(Audience) && 
               !string.IsNullOrWhiteSpace(Key) && 
               Key.Length >= 32;
    }
}
