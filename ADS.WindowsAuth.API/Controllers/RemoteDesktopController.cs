using Microsoft.AspNetCore.Authorization;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// MVC контролер за Remote Desktop viewer – изисква вход
/// </summary>
public class RemoteDesktopController : Controller
{
    private readonly IRemoteDesktopSessionService _rdSessionService;
    private readonly ILogger<RemoteDesktopController> _logger;
    private readonly ApplicationDbContext _dbContext;

    public RemoteDesktopController(
        IRemoteDesktopSessionService rdSessionService,
        ILogger<RemoteDesktopController> logger,
        ApplicationDbContext dbContext)
    {
        _rdSessionService = rdSessionService;
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Главна страница - списък на активни машини и наблюдавани машини
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Machines that sent logs in the last 7 days
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var monitoredMachines = await _dbContext.LogEntries
            .Where(l => l.Timestamp >= cutoff)
            .Select(l => l.MachineName)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        ViewBag.MonitoredMachines = monitoredMachines;
        return View();
    }

    /// <summary>
    /// Viewer страница - въвеждане на Session ID
    /// </summary>
    public IActionResult Connect()
    {
        return View();
    }

    /// <summary>
    /// Viewer страница - показване на remote screen
    /// </summary>
    public IActionResult Viewer(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return RedirectToAction("Connect");
        }

        ViewData["SessionId"] = sessionId;
        // Hub е в същото приложение - използваме относителен URL
        ViewData["HubUrl"] = "/hubs/remotedesktop";

        return View();
    }

    /// <summary>
    /// API endpoint - получава информация за конкретна сесия
    /// </summary>
    [HttpGet("api/remotedesktop/session/{sessionId}")]
    public async Task<IActionResult> GetSessionInfo(string sessionId)
    {
        try
        {
            var session = await _rdSessionService.GetSessionAsync(sessionId);

            if (session == null)
            {
                return NotFound(new { error = "Сесията не съществува", sessionId });
            }

            var viewerUrl = $"{Request.Scheme}://{Request.Host}/RemoteDesktop/Viewer?sessionId={sessionId}";

            return Ok(new
            {
                sessionId,
                session,
                viewerUrl,
                message = "Сесия намерена успешно"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при получаване на сесия {SessionId}", sessionId);
            return StatusCode(500, new { error = "Вътрешна грешка", details = ex.Message });
        }
    }

    /// <summary>
    /// Диагностика – показва всички сесии с host/viewer статус. Отвори в браузър: /api/remotedesktop/sessions/status
    /// </summary>
    [AllowAnonymous]
    [HttpGet("api/remotedesktop/sessions/status")]
    public async Task<IActionResult> GetSessionsStatus()
    {
        var sessions = await _rdSessionService.GetActiveSessionsAsync();
        var result = sessions.Select(s => new
        {
            s.SessionId,
            s.MachineName,
            hostConnected = !string.IsNullOrEmpty(s.HostConnectionId),
            viewerConnected = !string.IsNullOrEmpty(s.ViewerConnectionId),
            s.AutoApprove,
            s.ControlEnabled,
            s.IsExpired,
            s.LastActivity
        });
        return Ok(result);
    }

    /// <summary>
    /// API endpoint - получава всички активни сесии
    /// </summary>
    [HttpGet("api/remotedesktop/sessions")]
    public async Task<IActionResult> GetActiveSessions()
    {
        try
        {
            var sessions = await _rdSessionService.GetActiveSessionsAsync();
            var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";

            var result = sessions.Select(s => new
            {
                s.SessionId,
                s.MachineName,
                s.RequestedByUser,
                s.CreatedAt,
                s.LastActivity,
                s.IsAuthorized,
                s.ControlEnabled,
                isActive = !s.IsExpired,
                hostConnected   = !string.IsNullOrEmpty(s.HostConnectionId),
                viewerConnected = !string.IsNullOrEmpty(s.ViewerConnectionId),
                viewerUrl = $"{apiBaseUrl}/RemoteDesktop/Viewer?sessionId={s.SessionId}"
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при получаване на сесии");
            return StatusCode(500, new { error = "Вътрешна грешка", details = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint - създава или връща сесия за машина (за host приложението).
    /// AllowAnonymous – Host приложението се свързва без auth credentials.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("api/remotedesktop/sessions/createorget")]
    public async Task<IActionResult> CreateOrGetSession([FromBody] CreateRdSessionRequest request)
    {
        try
        {
            var (sessionId, created) = await _rdSessionService.CreateOrGetSessionAsync(request.MachineName, request.RequestedBy);
            if (created)
                _logger.LogInformation("Създадена нова Remote Desktop сесия {SessionId} за {Machine}", sessionId, request.MachineName);
            return Ok(new { sessionId, message = "Сесия готова" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при CreateOrGet Remote Desktop сесия");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint - изтрива сесия по ID
    /// </summary>
    [HttpDelete("api/remotedesktop/sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        try
        {
            var session = await _rdSessionService.GetSessionAsync(sessionId);
            if (session == null)
                return NotFound(new { error = "Сесията не съществува" });

            await _rdSessionService.EndSessionAsync(sessionId);
            _logger.LogInformation("Изтрита Remote Desktop сесия {SessionId} от уеб", sessionId);
            return Ok(new { message = "Сесията е изтрита" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при изтриване на сесия {SessionId}", sessionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint - създава нова remote desktop сесия
    /// </summary>
    [HttpPost("api/remotedesktop/sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateRdSessionRequest request)
    {
        try
        {
            var sessionId = await _rdSessionService.CreateSessionAsync(request.MachineName, request.RequestedBy);
            _logger.LogInformation("Създадена Remote Desktop сесия {SessionId} за {Machine}", sessionId, request.MachineName);
            return Ok(new { sessionId, message = "Сесия създадена успешно" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при създаване на Remote Desktop сесия");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class CreateRdSessionRequest
{
    public string MachineName { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
}
