using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Контролер за преглед на логове и активност
/// </summary>
[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly IActivityMonitorService _activityMonitor;
    private readonly ILoggerService _logger;
    private readonly ApplicationDbContext _dbContext;

    public LogsController(
        IActivityMonitorService activityMonitor,
        ILoggerService logger,
        ApplicationDbContext dbContext)
    {
        _activityMonitor = activityMonitor;
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Получава активността за конкретна машина
    /// </summary>
    [HttpGet("machine/{machineName}")]
    public IActionResult GetMachineActivity(string machineName)
    {
        try
        {
            var activities = _activityMonitor.GetAllActivities()
                .Where(a => a.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.StartTime)
                .ToList();

            return Ok(new
            {
                MachineName = machineName,
                Activities = activities,
                TotalActivities = activities.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на активност за машина {machineName}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на активност" });
        }
    }

    /// <summary>
    /// Получава активността за конкретен потребител
    /// </summary>
    [HttpGet("user/{username}/{machineName}")]
    public IActionResult GetUserActivity(string username, string machineName)
    {
        try
        {
            var activity = _activityMonitor.GetUserActivity(username, machineName);
            
            if (activity == null)
            {
                return NotFound(new { message = "Активността не е намерена" });
            }

            return Ok(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на активност за {username}@{machineName}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на активност" });
        }
    }

    /// <summary>
    /// Получава всички машини с активност
    /// </summary>
    [HttpGet("machines")]
    public IActionResult GetMachines()
    {
        try
        {
            var machines = _activityMonitor.GetAllActivities()
                .Select(a => a.MachineName)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            return Ok(new
            {
                Machines = machines,
                TotalMachines = machines.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при получаване на списък с машини", ex);
            return StatusCode(500, new { message = "Грешка при получаване на списък" });
        }
    }

    /// <summary>
    /// Получава статистика за машина
    /// </summary>
    [HttpGet("machine/{machineName}/stats")]
    public IActionResult GetMachineStats(string machineName)
    {
        try
        {
            var activities = _activityMonitor.GetAllActivities()
                .Where(a => a.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var stats = new
            {
                MachineName = machineName,
                TotalSessions = activities.Count,
                TotalScreenTime = activities.Sum(a => a.ScreenTimeSeconds),
                TotalApplications = activities.Sum(a => a.OpenedApplications.Count),
                TotalFiles = activities.Sum(a => a.OpenedFiles.Count),
                TotalWebsites = activities.Sum(a => a.VisitedWebsites.Count),
                LastActivity = activities.OrderByDescending(a => a.StartTime).FirstOrDefault()?.StartTime,
                Applications = activities
                    .SelectMany(a => a.OpenedApplications)
                    .GroupBy(a => a.ApplicationName)
                    .Select(g => new
                    {
                        ApplicationName = g.Key,
                        UsageCount = g.Count(),
                        TotalTime = g.Sum(a => a.UsageTimeSeconds)
                    })
                    .OrderByDescending(a => a.UsageCount)
                    .Take(10)
                    .ToList()
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на статистика за {machineName}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на статистика" });
        }
    }

    /// <summary>
    /// Получава лог запис от клиент и го записва в базата данни
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadLog([FromBody] LogUploadRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest(new { message = "Message е задължителен" });
            }

            var logEntry = new LogEntryEntity
            {
                MachineName = request.MachineName ?? Environment.MachineName,
                Username = request.Username ?? Environment.UserName,
                Domain = request.Domain ?? Environment.UserDomainName,
                Level = request.Level ?? "INFO",
                Message = request.Message,
                Timestamp = request.Timestamp ?? DateTime.Now,
                Source = request.Source,
                ExceptionType = request.ExceptionType,
                StackTrace = request.StackTrace
            };

            _dbContext.LogEntries.Add(logEntry);
            await _dbContext.SaveChangesAsync();

            // Също така записваме в локален файл (ако искаме)
            _logger.LogInfo($"Log received from {logEntry.MachineName}: [{logEntry.Level}] {logEntry.Message}");

            return Ok(new { message = "Логът е записан успешно", id = logEntry.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на лог: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при запис на лог" });
        }
    }

    /// <summary>
    /// Получава логове за конкретна машина (от базата данни)
    /// </summary>
    [HttpGet("database/machine/{machineName}")]
    public async Task<IActionResult> GetMachineLogs(string machineName, [FromQuery] int? limit = 100)
    {
        try
        {
            var query = _dbContext.LogEntries
                .Where(l => l.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.Timestamp);

            if (limit.HasValue)
            {
                query = (IOrderedQueryable<LogEntryEntity>)query.Take(limit.Value);
            }

            var logs = query.ToList();

            return Ok(new
            {
                MachineName = machineName,
                Logs = logs,
                TotalCount = logs.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на логове за {machineName}", ex);
            return StatusCode(500, new { message = "Грешка при получаване на логове" });
        }
    }
}

/// <summary>
/// Заявка за качване на лог
/// </summary>
public class LogUploadRequest
{
    public string? MachineName { get; set; }
    public string? Username { get; set; }
    public string? Domain { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string? Source { get; set; }
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
}

