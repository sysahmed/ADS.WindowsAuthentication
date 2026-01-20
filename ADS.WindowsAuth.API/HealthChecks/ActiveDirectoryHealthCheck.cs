using Microsoft.Extensions.Diagnostics.HealthChecks;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Configuration;

namespace ADS.WindowsAuth.API.HealthChecks;

/// <summary>
/// Health check for Active Directory connectivity
/// </summary>
public class ActiveDirectoryHealthCheck : IHealthCheck
{
    private readonly IWindowsAuthService _windowsAuthService;
    private readonly ActiveDirectorySettings _adSettings;
    private readonly ILogger<ActiveDirectoryHealthCheck> _logger;

    public ActiveDirectoryHealthCheck(
        IWindowsAuthService windowsAuthService,
        ActiveDirectorySettings adSettings,
        ILogger<ActiveDirectoryHealthCheck> logger)
    {
        _windowsAuthService = windowsAuthService;
        _adSettings = adSettings;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_adSettings.Enabled)
            {
                return HealthCheckResult.Healthy("Active Directory is disabled");
            }

            // Test LDAP connectivity
            if (_windowsAuthService is ResilientWindowsAuthService resilientService)
            {
                var isConnected = await resilientService.TestLdapConnectivityAsync(_adSettings.DomainName);
                
                if (isConnected)
                {
                    return HealthCheckResult.Healthy($"Successfully connected to Active Directory domain {_adSettings.DomainName}");
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"Failed to connect to Active Directory domain {_adSettings.DomainName}");
                }
            }
            else
            {
                // Fallback for non-resilient service
                var (username, domain) = _windowsAuthService.GetCurrentWindowsUser();
                return HealthCheckResult.Healthy($"Active Directory service is running. Current user: {username}@{domain}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Active Directory health check failed");
            return HealthCheckResult.Unhealthy("Active Directory health check failed", ex);
        }
    }
}
