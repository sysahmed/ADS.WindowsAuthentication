using ADS.WindowsAuth.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADS.WindowsAuth.Service.Controllers;

/// <summary>
/// API контролер за Remote Desktop функционалност
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RemoteDesktopController : ControllerBase
{
    private readonly IRemoteDesktopSessionService _sessionService;
    private readonly ILoggerService _logger;

    public RemoteDesktopController(
        IRemoteDesktopSessionService sessionService,
        ILoggerService logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Получава всички активни сесии
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetActiveSessions()
    {
        try
        {
            var sessions = await _sessionService.GetActiveSessionsAsync();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при вземане на сесии: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Създава нова remote desktop сесия
    /// </summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var sessionId = await _sessionService.CreateSessionAsync(
                request.MachineName, 
                request.RequestedBy);
            
            _logger.LogInfo($"Създадена сесия {sessionId} за {request.MachineName}");
            
            return Ok(new { sessionId, message = "Сесия създадена успешно" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при създаване на сесия: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получава информация за конкретна сесия
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                return NotFound(new { error = "Сесията не съществува" });
            }

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при вземане на сесия: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Одобрява достъп за сесия
    /// </summary>
    [HttpPost("sessions/{sessionId}/authorize")]
    public async Task<IActionResult> AuthorizeSession(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                return NotFound(new { error = "Сесията не съществува" });
            }

            await _sessionService.AuthorizeControlAsync(sessionId);
            _logger.LogInfo($"Сесия {sessionId} одобрена");
            
            return Ok(new { message = "Достъп одобрен" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при одобрение: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отказва достъп за сесия
    /// </summary>
    [HttpPost("sessions/{sessionId}/deny")]
    public async Task<IActionResult>DenySession(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                return NotFound(new { error = "Сесията не съществува" });
            }

            await _sessionService.DenyControlAsync(sessionId);
            _logger.LogInfo($"Сесия {sessionId} отказана");
            
            return Ok(new { message = "Достъп отказан" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при отказ: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Приключва сесия
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> EndSession(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            
            if (session == null)
            {
                return NotFound(new { error = "Сесията не съществува" });
            }

            await _sessionService.EndSessionAsync(sessionId);
            _logger.LogInfo($"Сесия {sessionId} приключена");
            
            return Ok(new { message = "Сесия приключена" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при край на сесия: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получава сесия по име намашина
    /// </summary>
    [HttpGet("sessions/by-machine/{machineName}")]
    public async Task<IActionResult> GetSessionByMachine(string machineName)
    {
        try
        {
            var session = await _sessionService.GetSessionByMachineAsync(machineName);
            
            if (session == null)
            {
                return NotFound(new { error = "Няма активна сесия за тази машина" });
            }

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при търсене на сесия: {ex.Message}", ex);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request модел за създаване на сесия
/// </summary>
public class CreateSessionRequest
{
    public string MachineName { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
}
