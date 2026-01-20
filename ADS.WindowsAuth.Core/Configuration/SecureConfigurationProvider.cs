using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System;

namespace ADS.WindowsAuth.Core.Configuration;

/// <summary>
/// Provides secure configuration from environment variables and other secure sources
/// </summary>
public static class SecureConfigurationProvider
{
    /// <summary>
    /// Binds secure settings from various sources with fallback
    /// </summary>
    public static SecureSettings GetSecureSettings(IConfiguration configuration, bool? isDevelopment = null)
    {
        var secureSettings = new SecureSettings();
        
        // Try environment variables first (most secure)
        secureSettings.ActiveDirectoryServicePassword = 
            Environment.GetEnvironmentVariable("ADS_AD_SERVICE_PASSWORD") ?? 
            configuration["ActiveDirectory:ServicePassword"] ?? 
            string.Empty;
            
        secureSettings.JwtKey = 
            Environment.GetEnvironmentVariable("ADS_JWT_KEY") ?? 
            configuration["Jwt:Key"] ?? 
            string.Empty;
            
        secureSettings.DatabaseConnectionString = 
            Environment.GetEnvironmentVariable("ADS_CONNECTION_STRING") ?? 
            configuration["ConnectionStrings:DefaultConnection"];
        
        // Check if we're in development environment - проверяваме различни начини
        bool isDev = isDevelopment ?? 
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true ||
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true ||
            configuration["ASPNETCORE_ENVIRONMENT"]?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true ||
            configuration["Environment"]?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true ||
            configuration["EnvironmentName"]?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true;
        
        // Validate critical settings
        ValidateSecureSettings(secureSettings, isDev);
        
        return secureSettings;
    }
    
    /// <summary>
    /// Validates that critical secure settings are available
    /// </summary>
    private static void ValidateSecureSettings(SecureSettings settings, bool isDevelopment = false)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(settings.ActiveDirectoryServicePassword))
        {
            if (isDevelopment)
            {
                // In development, use a default password or skip validation
                settings.ActiveDirectoryServicePassword = "DevPassword123!";
            }
            else
            {
                errors.Add("Active Directory service password is not configured. Set ADS_AD_SERVICE_PASSWORD environment variable.");
            }
        }
        
        if (string.IsNullOrWhiteSpace(settings.JwtKey) || settings.JwtKey.Length < 32)
        {
            if (isDevelopment)
            {
                // In development, use a default JWT key
                settings.JwtKey = "ThisIsADevelopmentJwtKeyForTestingOnly1234567890";
            }
            else
            {
                errors.Add("JWT key is not configured or too short. Set ADS_JWT_KEY environment variable with at least 32 characters.");
            }
        }
        
        if (errors.Any())
        {
            throw new InvalidOperationException($"Secure configuration validation failed:\n{string.Join("\n", errors)}");
        }
    }
    
    /// <summary>
    /// Creates a secure configuration builder
    /// </summary>
    public static IConfigurationBuilder AddSecureConfiguration(this IConfigurationBuilder builder)
    {
        // Add environment variables with ADS_ prefix
        builder.AddEnvironmentVariables("ADS_");
        
        // Try to add Azure Key Vault if available
        var keyVaultUri = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URI");
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            // This would require Azure.Identity and Azure.Security.KeyVault.Secrets packages
            // builder.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
        }
        
        return builder;
    }
}
