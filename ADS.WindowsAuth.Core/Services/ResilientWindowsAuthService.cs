using System.DirectoryServices;
using Polly;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Configuration;
using ADS.WindowsAuth.Core.Services;
using Polly.Retry;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Resilient Windows Domain authentication service with retry logic
/// </summary>
public class ResilientWindowsAuthService : IWindowsAuthService
{
    private readonly ILoggerService _logger;
    private readonly ResiliencePipeline<bool> _ldapRetryPolicy;
    private readonly ResiliencePipeline _ldapOperationPolicy;

    public ResilientWindowsAuthService(ILoggerService logger)
    {
        _logger = logger;
        
        // Configure retry policies using new Polly 8.0 API
        var retryOptions = new RetryStrategyOptions<bool>
        {
            ShouldHandle = new PredicateBuilder<bool>()
                .Handle<DirectoryServicesCOMException>()
                .Handle<System.Runtime.InteropServices.COMException>()
                .Handle<InvalidOperationException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1)
        };

        _ldapRetryPolicy = new ResiliencePipelineBuilder<bool>()
            .AddRetry(retryOptions)
            .Build();

        var retryOptionsGeneric = new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<DirectoryServicesCOMException>()
                .Handle<System.Runtime.InteropServices.COMException>()
                .Handle<InvalidOperationException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1)
        };

        _ldapOperationPolicy = new ResiliencePipelineBuilder()
            .AddRetry(retryOptionsGeneric)
            .Build();
    }

    /// <summary>
    /// Validates credentials with retry logic
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(string username, string password, string domain)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning($"Invalid credentials parameters: username={username}, domain={domain}");
            return false;
        }

        var startTime = DateTime.Now;
        
        try
        {
            var result = await _ldapRetryPolicy.ExecuteAsync(async (cancellationToken) =>
            {
                return await Task.Run(() => ValidateCredentialsInternal(username, password, domain), cancellationToken);
            });

            var duration = DateTime.Now - startTime;
            
            // Log the operation
            if (_logger is EnhancedLoggerService enhancedLogger)
            {
                enhancedLogger.LogLdapOperation("ValidateCredentials", username, domain, result, duration);
            }
            else
            {
                _logger.LogInfo($"LDAP validation completed in {duration.TotalMilliseconds}ms for user {username}@{domain}: {result}");
            }

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError($"LDAP validation failed after {duration.TotalMilliseconds}ms for user {username}@{domain}: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Synchronous version for backward compatibility
    /// </summary>
    public bool ValidateCredentials(string username, string password, string domain)
    {
        return ValidateCredentialsAsync(username, password, domain).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Internal LDAP validation logic
    /// </summary>
    private bool ValidateCredentialsInternal(string username, string password, string domain)
    {
        try
        {
            _logger.LogInfo($"Attempting LDAP validation for {username}@{domain}");

            string ldapPath = $"LDAP://{domain}";
            
            // Опит за свързване с потребителските credentials
            string userDn = $"{username}@{domain}";
            
            using (DirectoryEntry entry = new DirectoryEntry(ldapPath, userDn, password))
            {
                // Configure secure LDAP connection
                entry.AuthenticationType = AuthenticationTypes.Secure | AuthenticationTypes.SecureSocketsLayer;
                
                // Test the connection by accessing NativeObject
                object? nativeObject = entry.NativeObject;
                
                _logger.LogInfo($"Successful LDAP validation for {username}@{domain}");
                return true;
            }
        }
        catch (DirectoryServicesCOMException ex)
        {
            // Log specific LDAP error codes
            var errorCode = ex.ErrorCode;
            var errorMessage = ex.Message;
            
            _logger.LogWarning($"LDAP validation failed for {username}@{domain}. Error Code: {errorCode}, Message: {errorMessage}");
            
            // Don't retry on authentication failures (invalid credentials)
            if (IsAuthenticationFailure(errorCode))
            {
                throw; // Let Polly handle this as non-retryable
            }
            
            throw; // Retry on other LDAP errors
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error during LDAP validation for {username}@{domain}: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Gets current Windows user with fallback
    /// </summary>
    public (string Username, string Domain) GetCurrentWindowsUser()
    {
        try
        {
            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (windowsIdentity != null)
            {
                var name = windowsIdentity.Name;
                var parts = name.Split('\\');
                
                if (parts.Length == 2)
                {
                    return (parts[1], parts[0]);
                }
                else if (parts.Length == 1 && name.Contains('@'))
                {
                    var emailParts = name.Split('@');
                    return (emailParts[0], emailParts[1]);
                }
                
                return (name, string.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to get current Windows user: {ex.Message}");
        }

        // Fallback
        return (Environment.UserName, Environment.UserDomainName);
    }

    /// <summary>
    /// Checks if the error code indicates an authentication failure (non-retryable)
    /// </summary>
    private bool IsAuthenticationFailure(int errorCode)
    {
        // Common LDAP authentication failure error codes
        return errorCode switch
        {
            unchecked((int)0x8007052E) => true, // LOGON_FAILURE
            unchecked((int)0x8007052F) => true, // ACCOUNT_RESTRICTION
            unchecked((int)0x80070530) => true, // INVALID_LOGON_HOURS
            unchecked((int)0x80070531) => true, // INVALID_WORKSTATION
            unchecked((int)0x80070532) => true, // PASSWORD_EXPIRED
            unchecked((int)0x80070533) => true, // ACCOUNT_DISABLED
            unchecked((int)0x80070534) => true, // ACCOUNT_LOCKED_OUT
            _ => false
        };
    }

    /// <summary>
    /// Tests LDAP connectivity
    /// </summary>
    public async Task<bool> TestLdapConnectivityAsync(string domain)
    {
        try
        {
            await _ldapOperationPolicy.ExecuteAsync(async (cancellationToken) =>
            {
                await Task.Run(() =>
                {
                    using (var entry = new DirectoryEntry($"LDAP://{domain}"))
                    {
                        var objectName = entry.Name;
                    }
                }, cancellationToken);
            });

            _logger.LogInfo($"LDAP connectivity test successful for domain {domain}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"LDAP connectivity test failed for domain {domain}: {ex.Message}", ex);
            return false;
        }
    }
}
