using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Контролер за управление на конфигурацията на Monitor Service
/// </summary>
[ApiController]
[Route("api/monitor")]
public class MonitorConfigurationController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILoggerService _logger;

    public MonitorConfigurationController(ApplicationDbContext dbContext, ILoggerService logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Получава конфигурацията за конкретна машина
    /// </summary>
    [HttpGet("configuration/{machineName}")]
    public async Task<IActionResult> GetConfiguration(string machineName)
    {
        try
        {
            var config = await _dbContext.MonitorConfigurations
                .FirstOrDefaultAsync(c => c.MachineName == machineName);

            if (config == null)
            {
                // Връщаме default конфигурация
                var defaultConfig = new
                {
                    ServiceUrl = "https://ads-auth.nursanbulgaria.com",
                    ApiKey = (string?)null,
                    RequireVpn = false,
                    VpnCheckInterval = 300,
                    VpnGateways = "[]",
                    VpnProcessNames = "[\"FortiClient\",\"rasdial\"]",
                    OfflineMode = false,
                    OfflineDataRetention = 7,
                    ConnectionTimeout = 30,
                    RetryInterval = 60,
                    MaxRetries = 3,
                    ScreenshotEnabled = false,
                    ScreenshotIntervalMinutes = 5
                };
                return Ok(defaultConfig);
            }

            // Конвертиране на Entity към Response формат
            var response = new
            {
                ServiceUrl = config.ServiceUrl,
                ApiKey = config.ApiKey,
                RequireVpn = config.RequireVpn,
                VpnCheckInterval = config.VpnCheckInterval,
                VpnGateways = config.VpnGateways,
                VpnProcessNames = config.VpnProcessNames,
                OfflineMode = config.OfflineMode,
                OfflineDataRetention = config.OfflineDataRetention,
                ConnectionTimeout = config.ConnectionTimeout,
                RetryInterval = config.RetryInterval,
                MaxRetries = config.MaxRetries,
                ScreenshotEnabled = config.ScreenshotEnabled,
                ScreenshotIntervalMinutes = config.ScreenshotIntervalMinutes
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на конфигурация за {machineName}: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на конфигурация" });
        }
    }

    /// <summary>
    /// Създава или обновява конфигурация за машина
    /// </summary>
    [HttpPost("configuration")]
    public async Task<IActionResult> SetConfiguration([FromBody] MonitorConfigurationEntity configuration)
    {
        try
        {
            if (configuration == null || string.IsNullOrEmpty(configuration.MachineName))
            {
                return BadRequest(new { message = "MachineName е задължителен" });
            }

            var existing = await _dbContext.MonitorConfigurations
                .FirstOrDefaultAsync(c => c.MachineName == configuration.MachineName);

            if (existing != null)
            {
                // Обновяване
                existing.ServiceUrl = configuration.ServiceUrl;
                existing.ApiKey = configuration.ApiKey;
                existing.RequireVpn = configuration.RequireVpn;
                existing.VpnCheckInterval = configuration.VpnCheckInterval;
                existing.VpnGateways = configuration.VpnGateways;
                existing.VpnProcessNames = configuration.VpnProcessNames;
                existing.OfflineMode = configuration.OfflineMode;
                existing.OfflineDataRetention = configuration.OfflineDataRetention;
                existing.ConnectionTimeout = configuration.ConnectionTimeout;
                existing.RetryInterval = configuration.RetryInterval;
                existing.MaxRetries = configuration.MaxRetries;
                existing.ScreenshotEnabled = configuration.ScreenshotEnabled;
                existing.ScreenshotIntervalMinutes = configuration.ScreenshotIntervalMinutes;
                existing.UpdatedAt = DateTime.Now;

                _dbContext.MonitorConfigurations.Update(existing);
            }
            else
            {
                // Създаване
                configuration.CreatedAt = DateTime.Now;
                configuration.UpdatedAt = DateTime.Now;
                _dbContext.MonitorConfigurations.Add(configuration);
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInfo($"Конфигурацията за {configuration.MachineName} е запазена");

            return Ok(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запазване на конфигурация: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при запазване на конфигурация" });
        }
    }

    /// <summary>
    /// Получава всички конфигурации
    /// </summary>
    [HttpGet("configurations")]
    public async Task<IActionResult> GetAllConfigurations()
    {
        try
        {
            var configurations = await _dbContext.MonitorConfigurations.ToListAsync();
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на конфигурации: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на конфигурации" });
        }
    }
}

