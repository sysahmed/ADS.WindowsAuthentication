using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using Microsoft.AspNetCore.SignalR;
using ADS.WindowsAuth.Service.Hubs;

namespace ADS.WindowsAuth.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly IActivityMonitorService _activityMonitor;
    private readonly ILoggerService _logger;
    private readonly IHubContext<ActivityHub> _hubContext;

    public ActivityController(
        IActivityMonitorService activityMonitor,
        ILoggerService logger,
        IHubContext<ActivityHub> hubContext)
    {
        _activityMonitor = activityMonitor;
        _logger = logger;
        _hubContext = hubContext;
    }

    [HttpGet("user/{username}/{machineName}")]
    public IActionResult GetUserActivity(string username, string machineName)
    {
        UserActivity? activity = _activityMonitor.GetUserActivity(username, machineName);
        
        if (activity == null)
        {
            return NotFound(new { message = "Активността не е намерена" });
        }

        return Ok(activity);
    }

    [HttpGet("all")]
    public IActionResult GetAllActivities([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        List<UserActivity> activities = _activityMonitor.GetAllActivities(fromDate, toDate);
        return Ok(activities);
    }

    [HttpPost("start")]
    public IActionResult StartMonitoring([FromBody] StartMonitoringRequest request)
    {
        _activityMonitor.StartMonitoring(request.Username, request.Domain, request.MachineName);
        _logger.LogInfo($"Започнат мониторинг за {request.Username}@{request.Domain} на {request.MachineName}");
        
        _hubContext.Clients.Group($"machine_{request.MachineName}").SendAsync("MonitoringStarted", request.Username);
        
        return Ok(new { message = "Мониторингът е започнат" });
    }

    [HttpPost("stop")]
    public IActionResult StopMonitoring([FromBody] StopMonitoringRequest request)
    {
        _activityMonitor.StopMonitoring(request.Username, request.MachineName);
        _logger.LogInfo($"Спрян мониторинг за {request.Username} на {request.MachineName}");
        
        return Ok(new { message = "Мониторингът е спрян" });
    }

    [HttpPost("file/open")]
    public IActionResult RegisterFileOpen([FromBody] FileOpenRequest request)
    {
        _activityMonitor.RegisterFileOpen(request.Username, request.MachineName, request.FilePath, request.ApplicationName);
        return Ok();
    }

    [HttpPost("file/close")]
    public IActionResult RegisterFileClose([FromBody] FileCloseRequest request)
    {
        _activityMonitor.RegisterFileClose(request.Username, request.MachineName, request.FilePath);
        return Ok();
    }

    [HttpPost("website/visit")]
    public IActionResult RegisterWebsiteVisit([FromBody] WebsiteVisitRequest request)
    {
        _activityMonitor.RegisterWebsiteVisit(
            request.Username, 
            request.MachineName, 
            request.Url, 
            request.Title ?? "", 
            request.Browser, 
            request.DurationSeconds);
        return Ok();
    }

    [HttpPost("application/start")]
    public IActionResult RegisterApplicationStart([FromBody] ApplicationStartRequest request)
    {
        _activityMonitor.RegisterApplicationStart(
            request.Username, 
            request.MachineName, 
            request.ApplicationName, 
            request.ExecutablePath);
        return Ok();
    }

    [HttpPost("application/close")]
    public IActionResult RegisterApplicationClose([FromBody] ApplicationCloseRequest request)
    {
        _activityMonitor.RegisterApplicationClose(request.Username, request.MachineName, request.ApplicationName);
        return Ok();
    }

    [HttpPost("screentime/update")]
    public IActionResult UpdateScreenTime([FromBody] ScreenTimeUpdateRequest request)
    {
        _activityMonitor.UpdateScreenTime(request.Username, request.MachineName, request.Seconds);
        return Ok();
    }
}

public class StartMonitoringRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
}

public class StopMonitoringRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
}

public class FileOpenRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
}

public class FileCloseRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class WebsiteVisitRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Browser { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
}

public class ApplicationStartRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

public class ApplicationCloseRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
}

public class ScreenTimeUpdateRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public int Seconds { get; set; }
}

