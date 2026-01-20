using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// MVC контролер за главна страница и QR код аутентикация
/// </summary>
public class HomeController : Controller
{
    private readonly ISessionService _sessionService;
    private readonly IWindowsAuthService _windowsAuthService;
    private readonly ILoggerService _logger;
    private readonly IActivityMonitorService _activityMonitor;
    private readonly IPolicyService _policyService;
    private readonly HttpClient _httpClient;

    public HomeController(
        ISessionService sessionService,
        IWindowsAuthService windowsAuthService,
        ILoggerService logger,
        IActivityMonitorService activityMonitor,
        IPolicyService policyService,
        IHttpClientFactory httpClientFactory)
    {
        _sessionService = sessionService;
        _windowsAuthService = windowsAuthService;
        _logger = logger;
        _activityMonitor = activityMonitor;
        _policyService = policyService;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Главна страница
    /// </summary>
    [Route("/")]
    [Route("/Home")]
    [Route("/Home/Index")]
    public IActionResult Index()
    {
        try
        {
            _logger.LogInfo("Index page заявена");
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Index view", ex);
            return View("Error");
        }
    }

    /// <summary>
    /// Страница за преглед на активност по машина
    /// </summary>
    [HttpGet("logs")]
    public IActionResult Logs(string? machineName = null)
    {
        try
        {
            ViewBag.MachineName = machineName;

            // Получаване на списък с машини
            var machines = _activityMonitor.GetAllActivities()
                .Select(a => a.MachineName)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            ViewBag.Machines = machines;

            // Ако е избрана машина, получаваме активността
            if (!string.IsNullOrEmpty(machineName))
            {
                var activities = _activityMonitor.GetAllActivities()
                    .Where(a => a.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.StartTime)
                    .ToList();

                ViewBag.Activities = activities;
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Logs view", ex);
            return View("Error");
        }
    }

    /// <summary>
    /// Страница за аутентикация с QR код токен
    /// </summary>
    [Route("/auth")]
    [HttpGet]
    public IActionResult Auth(string? token = null)
    {
        try
        {
            _logger.LogInfo($"Auth GET заявка получена. Token: {(string.IsNullOrEmpty(token) ? "NULL" : token.Substring(0, Math.Min(8, token.Length)) + "...")}");
            _logger.LogInfo($"Общо сесии в паметта: {_sessionService.GetAllSessions().Count()}");
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Auth GET заявка без токен");
                ViewBag.Error = "Токенът не е предоставен. Моля, сканирайте QR кода отново.";
                return View();
            }

            _logger.LogInfo($"Търсене на сесия с токен: {token.Substring(0, Math.Min(8, token.Length))}...");
            
            // Проверка дали токенът е валиден
            var session = _sessionService.GetSessionByToken(token);
            if (session == null)
            {
                ViewBag.Error = "Сесията не е намерена или е изтекла. Моля, сканирайте QR кода отново.";
                ViewBag.Token = token;
                return View();
            }

            // Проверка дали сесията е изтекла
            if (session.ExpiresAt < DateTime.Now)
            {
                ViewBag.Error = "Сесията е изтекла. Моля, сканирайте QR кода отново.";
                ViewBag.Token = token;
                return View();
            }

            // Проверка дали сесията вече е одобрена
            if (session.Status == SessionStatus.Approved)
            {
                ViewBag.Error = "Тази сесия вече е одобрена.";
                ViewBag.Token = token;
                return View();
            }

            ViewBag.Token = token;
            ViewBag.Username = session.Username;
            ViewBag.Domain = session.Domain;
            ViewBag.MachineName = session.MachineName;

            _logger.LogInfo($"Auth страница заявена за токен: {token.Substring(0, Math.Min(8, token.Length))}... (Сесия: {session.SessionId})");

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Auth view", ex);
            ViewBag.Error = $"Грешка при зареждане на страницата: {ex.Message}";
            ViewBag.Token = token;
            return View();
        }
    }

    /// <summary>
    /// Обработка на POST заявка за аутентикация
    /// </summary>
    [Route("/auth")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AuthPost(string token, string username, string domain, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Токенът не е предоставен.";
                return View("Auth");
            }

            // Проверка дали токенът е валиден
            var session = _sessionService.GetSessionByToken(token);
            if (session == null)
            {
                ViewBag.Error = "Сесията не е намерена или е изтекла.";
                ViewBag.Token = token;
                return View("Auth");
            }

            // Проверка дали сесията е изтекла
            if (session.ExpiresAt < DateTime.Now)
            {
                ViewBag.Error = "Сесията е изтекла.";
                ViewBag.Token = token;
                return View("Auth");
            }

            // Проверка дали сесията вече е одобрена
            if (session.Status == SessionStatus.Approved)
            {
                ViewBag.Error = "Тази сесия вече е одобрена.";
                ViewBag.Token = token;
                return View("Auth");
            }

            // Валидация на credentials
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Моля, попълнете всички полета.";
                ViewBag.Token = token;
                ViewBag.Username = username;
                ViewBag.Domain = domain;
                return View("Auth");
            }

            // Проверка на Windows credentials
            var isValid = _windowsAuthService.ValidateCredentials(username, password, domain);
            if (!isValid)
            {
                ViewBag.Error = "Невалидни credentials. Моля, опитайте отново.";
                ViewBag.Token = token;
                ViewBag.Username = username;
                ViewBag.Domain = domain;
                return View("Auth");
            }

            // Одобряване на сесия чрез SessionService (правилният начин)
            bool approved = _sessionService.ApproveSession(session.SessionId, username, password, domain);

            if (!approved)
            {
                ViewBag.Error = "Неуспешно одобрение на сесия. Моля, опитайте отново.";
                ViewBag.Token = token;
                ViewBag.Username = username;
                ViewBag.Domain = domain;
                return View("Auth");
            }

            // Настройка таймаут за паролата (сец security - автоматично изтриване)
            // Паролата остава в паметта само 10 секунди, след което се изтрива
            if (_sessionService.GetSessionById(session.SessionId) is AuthSession approvedSession)
            {
                approvedSession.PasswordExpiresAt = DateTime.Now.AddSeconds(10);
                _logger.LogInfo($"Настроен таймаут за паролата на сесия {session.SessionId} (истича в 10 сек)");
            }

            _logger.LogInfo($"Сесия {session.SessionId} одобрена успешно от {username}@{domain}");

            // Пренасочване към страница за успех
            return RedirectToAction("Success", new { sessionId = session.SessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при обработка на Auth POST", ex);
            ViewBag.Error = $"Грешка при обработка на заявката: {ex.Message}";
            ViewBag.Token = token;
            ViewBag.Username = username;
            ViewBag.Domain = domain;
            return View("Auth");
        }
    }

    /// <summary>
    /// Страница за успешна аутентикация
    /// </summary>
    [Route("/success")]
    [HttpGet]
    public IActionResult Success(string? sessionId = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                var session = _sessionService.GetSessionById(sessionId);
                if (session != null)
                {
                    ViewBag.SessionId = session.SessionId;
                    ViewBag.Username = session.Username;
                    ViewBag.Domain = session.Domain;
                    ViewBag.MachineName = session.MachineName;
                    ViewBag.ApprovedBy = session.ApprovedBy;
                    ViewBag.ApprovedAt = session.ApprovedAt;
                }
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Success view", ex);
            return View("Error");
        }
    }

    /// <summary>
    /// Страница за грешки
    /// </summary>
    [HttpGet("Home/Error")]
    public IActionResult Error(int? statusCode = null)
    {
        ViewBag.StatusCode = statusCode ?? 500;
        return View("Error");
    }

    /// <summary>
    /// Страница за управление на политики
    /// </summary>
    [HttpGet("policies")]
    public async Task<IActionResult> Policies()
    {
        try
        {
            var policies = _policyService.GetAllPolicies();
            ViewBag.Policies = policies;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Policies view", ex);
            return View("Error");
        }
    }

    /// <summary>
    /// Страница за преглед на логове от базата данни
    /// </summary>
    /// <summary>
    /// Страница за преглед на логове от базата данни
    /// </summary>
    [HttpGet("system-logs")]
    public async Task<IActionResult> SystemLogs(string? machineName = null, [FromQuery] string? level = null, [FromQuery] int page = 1)
    {
        try
        {
            ViewBag.MachineName = machineName;
            ViewBag.Level = level;
            ViewBag.Page = page;
            ViewBag.PageSize = 50;

            // Опит за получаване на ApplicationDbContext чрез scope
            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
                
                if (dbContext != null)
                {
                    // Получаване на списък с машини
                    var machines = await dbContext.LogEntries
                        .Select(l => l.MachineName)
                        .Distinct()
                        .OrderBy(m => m)
                        .ToListAsync();

                    ViewBag.Machines = machines;

                    // Получаване на логове
                    var query = dbContext.LogEntries.AsQueryable();

                    if (!string.IsNullOrEmpty(machineName))
                    {
                        query = query.Where(l => l.MachineName == machineName);
                    }

                    if (!string.IsNullOrEmpty(level))
                    {
                        query = query.Where(l => l.Level == level);
                    }

                    var totalCount = await query.CountAsync();
                    ViewBag.TotalCount = totalCount;
                    ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)ViewBag.PageSize);

                    var logs = await query
                        .OrderByDescending(l => l.Timestamp)
                        .Skip((page - 1) * (int)ViewBag.PageSize)
                        .Take((int)ViewBag.PageSize)
                        .ToListAsync();

                    ViewBag.Logs = logs;

                    // Получаване на статистика по нива
                    var levelStats = await dbContext.LogEntries
                        .GroupBy(l => l.Level)
                        .Select(g => new LevelStat { Level = g.Key, Count = g.Count() })
                        .ToListAsync();
                    ViewBag.LevelStats = levelStats;
                }
                else
                {
                    // Базата данни не е налична - показваме празен списък
                    ViewBag.Machines = new List<string>();
                    ViewBag.Logs = new List<ADS.WindowsAuth.Core.Data.Entities.LogEntryEntity>();
                    ViewBag.LevelStats = new List<LevelStat>();
                    ViewBag.TotalCount = 0;
                    ViewBag.TotalPages = 0;
                    _logger.LogWarning("ApplicationDbContext не е наличен в SystemLogs");
                }
            }
            catch (Exception dbEx)
            {
                // Грешка при достъп до базата данни - показваме празен списък
                _logger.LogError($"Грешка при достъп до базата данни в SystemLogs: {dbEx.Message}", dbEx);
                ViewBag.Machines = new List<string>();
                ViewBag.Logs = new List<ADS.WindowsAuth.Core.Data.Entities.LogEntryEntity>();
                    ViewBag.LevelStats = new List<LevelStat>();
                ViewBag.TotalCount = 0;
                ViewBag.TotalPages = 0;
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при зареждане на SystemLogs view: {ex.Message}", ex);
            ViewBag.Error = ex.Message;
            return View("Error");
        }
    }

    /// <summary>
    /// Страница за управление на настройки на Monitor Service
    /// </summary>
    [HttpGet("monitor-settings")]
    public async Task<IActionResult> MonitorSettings(string? machineName = null)
    {
        try
        {
            // Получаване на списък с машини
            var machines = _activityMonitor.GetAllActivities()
                .Select(a => a.MachineName)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            ViewBag.Machines = machines;
            ViewBag.SelectedMachine = machineName;

            // Ако е избрана машина, получаваме конфигурацията
            if (!string.IsNullOrEmpty(machineName))
            {
                try
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var response = await _httpClient.GetFromJsonAsync<MonitorConfigurationResponse>($"{baseUrl}/api/monitor/configuration/{machineName}");
                    if (response != null)
                    {
                        ViewBag.Configuration = response;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Грешка при получаване на конфигурация за {machineName}: {ex.Message}");
                }
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на MonitorSettings view", ex);
            return View("Error");
        }
    }
}

/// <summary>
/// Response модел за Monitor Configuration
/// </summary>
public class MonitorConfigurationResponse
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool RequireVpn { get; set; }
    public int VpnCheckInterval { get; set; } = 300;
    public string VpnGateways { get; set; } = "[]";
    public string VpnProcessNames { get; set; } = "[]";
    public bool OfflineMode { get; set; }
    public int OfflineDataRetention { get; set; } = 7;
    public int ConnectionTimeout { get; set; } = 30;
    public int RetryInterval { get; set; } = 60;
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Модел за статистика по нива
/// </summary>
public class LevelStat
{
    public string Level { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ... existing code ...