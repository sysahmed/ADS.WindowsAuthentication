using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// MVC контролер за Remote Desktop viewer
/// </summary>
public class RemoteDesktopController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RemoteDesktopController> _logger;

    public RemoteDesktopController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<RemoteDesktopController> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Главна страница - списък на активни машини
    /// </summary>
    public IActionResult Index()
    {
        // Взимаме Service URL за API заявки
        var serviceUrl = _configuration["RemoteDesktop:ServiceUrl"] ?? "http://localhost:5140";
        ViewData["ServiceUrl"] = serviceUrl;
        
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
    /// <param name="sessionId">Session ID за свързване</param>
    public IActionResult Viewer(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return RedirectToAction("Connect");
        }

        ViewData["SessionId"] = sessionId;
        
        // Взимаме Service URL от конфигурацията
        var serviceUrl = _configuration["RemoteDesktop:ServiceUrl"] ?? "http://localhost:5140";
        var hubPath = _configuration["RemoteDesktop:HubPath"] ?? "/hubs/remotedesktop";
        ViewData["ServiceUrl"] = serviceUrl + hubPath;
        
        return View();
    }

    /// <summary>
    /// API endpoint - получава информация за сесия и връща директна връзка към viewer
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    [HttpGet("api/remotedesktop/session/{sessionId}")]
    public async Task<IActionResult> GetSessionInfo(string sessionId)
    {
        try
        {
            var serviceUrl = _configuration["RemoteDesktop:ServiceUrl"] ?? "http://localhost:5140";
            var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // Взимаме информация за сесията от Service API
            var response = await httpClient.GetAsync($"{serviceUrl}/api/remotedesktop/sessions/{sessionId}");
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound(new { error = "Сесията не съществува", sessionId });
                }
                
                return StatusCode((int)response.StatusCode, new { error = "Грешка при комуникация с Remote Desktop Service" });
            }
            
            var session = await response.Content.ReadFromJsonAsync<object>();
            
            // Генерираме директна връзка към viewer
            var viewerUrl = $"{apiBaseUrl}/RemoteDesktop/Viewer?sessionId={sessionId}";
            
            return Ok(new
            {
                sessionId,
                session,
                viewerUrl,
                message = "Сесия намерена успешно"
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Грешка при комуникация с Remote Desktop Service: {ex.Message}");
            return StatusCode(503, new { error = "Remote Desktop Service не е достъпен", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на сесия: {ex.Message}", ex);
            return StatusCode(500, new { error = "Вътрешна грешка", details = ex.Message });
        }
    }

    /// <summary>
    /// API endpoint - получава всички активни сесии
    /// </summary>
    [HttpGet("api/remotedesktop/sessions")]
    public async Task<IActionResult> GetActiveSessions()
    {
        try
        {
            var serviceUrl = _configuration["RemoteDesktop:ServiceUrl"] ?? "http://localhost:5140";
            var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
            
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // Взимаме активните сесии от Service API
            var response = await httpClient.GetAsync($"{serviceUrl}/api/remotedesktop/sessions");
            
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new { error = "Грешка при комуникация с Remote Desktop Service" });
            }
            
            var jsonString = await response.Content.ReadAsStringAsync();
            var sessionsJson = JsonDocument.Parse(jsonString);
            
            if (sessionsJson.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Ok(new List<object>());
            }
            
            // Добавяме viewerUrl за всяка сесия
            var resultList = new List<object>();
            
            foreach (var sessionElement in sessionsJson.RootElement.EnumerateArray())
            {
                var sessionObj = JsonSerializer.Deserialize<Dictionary<string, object>>(sessionElement.GetRawText());
                
                if (sessionObj != null && sessionObj.TryGetValue("sessionId", out var sessionIdObj))
                {
                    var sessionId = sessionIdObj?.ToString();
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        sessionObj["viewerUrl"] = $"{apiBaseUrl}/RemoteDesktop/Viewer?sessionId={sessionId}";
                    }
                }
                
                resultList.Add(sessionObj ?? new Dictionary<string, object>());
            }
            
            return Ok(resultList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Грешка при комуникация с Remote Desktop Service: {ex.Message}");
            return StatusCode(503, new { error = "Remote Desktop Service не е достъпен", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при получаване на сесии: {ex.Message}", ex);
            return StatusCode(500, new { error = "Вътрешна грешка", details = ex.Message });
        }
    }
}
