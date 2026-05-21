using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.API.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Collections.Concurrent;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// MVC контролер за главна страница и QR код аутентикация
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly ISessionService _sessionService;
    private readonly IWindowsAuthService _windowsAuthService;
    private readonly ILoggerService _logger;
    private readonly IActivityMonitorService _activityMonitor;
    private readonly IPolicyService _policyService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;
    private readonly BruteForceProtectionService _bruteForce;

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public HomeController(
        ISessionService sessionService,
        IWindowsAuthService windowsAuthService,
        ILoggerService logger,
        IActivityMonitorService activityMonitor,
        IPolicyService policyService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        BruteForceProtectionService bruteForce)
    {
        _sessionService = sessionService;
        _windowsAuthService = windowsAuthService;
        _logger = logger;
        _activityMonitor = activityMonitor;
        _policyService = policyService;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _dbContext = dbContext;
        _bruteForce = bruteForce;
    }

    /// <summary>
    /// Главна страница – изисква вход (достъпна на /Home или /Home/Index)
    /// </summary>
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
    /// Страница за реално-времеви мониторинг на машина (процеси, инсталирани програми)
    /// </summary>
    [Route("/machine/{machineName}")]
    [HttpGet]
    public IActionResult Machine(string machineName)
    {
        ViewBag.MachineName = machineName;
        return View();
    }

    /// <summary>
    /// Страница за преглед на активност и логове по машина.
    /// Машините идват от активност (in-memory) и от LogEntries в БД, за да има винаги какво да се избере.
    /// </summary>
    [Route("/logs")]
    [HttpGet]
    public async Task<IActionResult> Logs(string? machineName = null, [FromQuery] int logPage = 1, [FromQuery] int logPageSize = 50)
    {
        try
        {
            ViewBag.MachineName = machineName;
            ViewBag.LogPage = logPage;
            ViewBag.LogPageSize = logPageSize;

            // Машини от активност (in-memory)
            var machinesFromActivity = _activityMonitor.GetAllActivities()
                .Select(a => a.MachineName)
                .Distinct()
                .ToList();

            // Машини от БД (LogEntries) – така има машини дори когато няма активност в паметта
            var machinesFromDb = new List<string>();
            try
            {
                machinesFromDb = await _dbContext.LogEntries
                    .Select(l => l.MachineName)
                    .Distinct()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Logs: не може да се заредят машини от LogEntries: {ex.Message}");
            }

            var machines = machinesFromActivity
                .Union(machinesFromDb, StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ViewBag.Machines = machines;

            // Ако е избрана машина – активност от паметта
            if (!string.IsNullOrEmpty(machineName))
            {
                var activities = _activityMonitor.GetAllActivities()
                    .Where(a => a.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.StartTime)
                    .ToList();
                ViewBag.Activities = activities;

                // Логове от БД за избраната машина (EF Core не превежда StringComparison в SQL – използваме ToLower)
                var dbLogs = new List<ADS.WindowsAuth.Core.Data.Entities.LogEntryEntity>();
                var totalLogCount = 0;
                try
                {
                    var machineLower = machineName.ToLower();
                    var logQuery = _dbContext.LogEntries
                        .Where(l => l.MachineName.ToLower() == machineLower)
                        .OrderByDescending(l => l.Timestamp);
                    totalLogCount = await logQuery.CountAsync();
                    ViewBag.TotalLogCount = totalLogCount;
                    ViewBag.TotalLogPages = (int)Math.Ceiling(totalLogCount / (double)logPageSize);
                    dbLogs = await logQuery
                        .Skip((logPage - 1) * logPageSize)
                        .Take(logPageSize)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Logs: не може да се заредят логове от БД: {ex.Message}");
                }
                ViewBag.DbLogs = dbLogs;
            }
            else
            {
                ViewBag.Activities = new List<UserActivity>();
                ViewBag.DbLogs = new List<ADS.WindowsAuth.Core.Data.Entities.LogEntryEntity>();
                ViewBag.TotalLogCount = 0;
                ViewBag.TotalLogPages = 0;
            }

            // Общ брой записи в LogEntries (за диагностика "защо няма логове")
            try
            {
                ViewBag.TotalLogsInDb = await _dbContext.LogEntries.CountAsync();
            }
            catch { ViewBag.TotalLogsInDb = 0; }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Logs view: " + ex.Message, ex);
            ViewBag.Machines = new List<string>();
            ViewBag.Activities = new List<UserActivity>();
            ViewBag.DbLogs = new List<ADS.WindowsAuth.Core.Data.Entities.LogEntryEntity>();
            ViewBag.TotalLogCount = 0;
            ViewBag.TotalLogPages = 0;
            ViewBag.TotalLogsInDb = 0;
            ViewBag.LogsError = ex.Message;
            ViewBag.MachineName = machineName;
            ViewBag.LogPage = logPage;
            ViewBag.LogPageSize = logPageSize;
            return View();
        }
    }

    /// <summary>
    /// Страница за аутентикация с QR код токен (публична — Windows машини)
    /// </summary>
    [AllowAnonymous]
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

            // Домейнът идва от appsettings (ActiveDirectory:DomainName), НЕ от сесията
            // session.Domain може да е machine name ("AHMEDITDESK") ако е създадена от Credential Provider
            string defaultDomain = _configuration["ActiveDirectory:DomainName"] ?? string.Empty;

            ViewBag.Token = token;
            ViewBag.Username = session.Username;
            ViewBag.Domain = defaultDomain;       // Домейн от appsettings - може да се смени от потребителя
            ViewBag.DefaultDomain = defaultDomain; // За JavaScript - за да не се презаписва с machine name
            ViewBag.MachineName = session.MachineName;

            _logger.LogInfo($"Auth страница заявена за токен: {token.Substring(0, Math.Min(8, token.Length))}... (Сесия: {session.SessionId}, Domain: {defaultDomain})");

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
    /// Обработка на POST заявка за аутентикация (публична — Windows машини)
    /// </summary>
    [AllowAnonymous]
    [Route("/auth")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuthPost(string token, string username, string domain, string password)
    {
        try
        {
            // ── Brute Force Protection: проверяваме дали IP е блокиран ─────────
            string clientIp = GetClientIp();
            if (_bruteForce.IsBlocked(clientIp))
            {
                _logger.LogWarning($"SECURITY: Блокиран IP {clientIp} се опитва да влезе в /auth");
                ViewBag.Error = "Вашият IP адрес е блокиран поради многобройни неуспешни опити. Свържете се с администратора за деблокиране.";
                ViewBag.Token = token;
                return View("Auth");
            }

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

            // Проверка на Windows credentials (async - не блокира нишката)
            var isValid = await _windowsAuthService.ValidateCredentialsAsync(username, password, domain);
            if (!isValid)
            {
                bool nowBlocked = await _bruteForce.RegisterFailedAttemptAsync(clientIp);
                if (nowBlocked)
                {
                    _logger.LogWarning($"SECURITY: IP {clientIp} е блокиран след многобройни неуспешни опити");
                    ViewBag.Error = "Вашият IP адрес е блокиран поради многобройни неуспешни опити. Свържете се с администратора за деблокиране.";
                }
                else
                {
                    int remaining = _bruteForce.RemainingAttempts(clientIp);
                    ViewBag.Error = remaining > 0
                        ? $"Невалидни credentials. Остават {remaining} опит(а) преди блокиране на IP адреса."
                        : "Невалидни credentials. Моля, опитайте отново.";
                }
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
            _bruteForce.ClearAttempts(clientIp);

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
    /// Страница за успешна аутентикация (публична — Windows машини)
    /// </summary>
    [AllowAnonymous]
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
    [Route("/policies")]
    [HttpGet]
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
    [AllowAnonymous]
    /// <summary>
    /// Системни логове – изисква вход
    /// </summary>
    [Route("/system-logs")]
    [HttpGet]
    public async Task<IActionResult> SystemLogs(string? machineName = null, [FromQuery] string? level = null, [FromQuery] string? source = null, [FromQuery] int page = 1)
    {
        try
        {
            ViewBag.MachineName = machineName;
            ViewBag.Level = level;
            ViewBag.Source = source;
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

                    // Получаване на списък с източници (Source)
                    var sources = await dbContext.LogEntries
                        .Where(l => l.Source != null && l.Source != "")
                        .Select(l => l.Source!)
                        .Distinct()
                        .OrderBy(s => s)
                        .ToListAsync();
                    ViewBag.Sources = sources;

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

                    if (!string.IsNullOrEmpty(source))
                    {
                        query = query.Where(l => l.Source == source);
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
                    ViewBag.Sources = new List<string>();
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
                ViewBag.Sources = new List<string>();
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
    /// Windows влизания (Login events) – от Security Event Log, записани в LoginEvents.
    /// </summary>
    [Route("/login-events")]
    [HttpGet]
    public async Task<IActionResult> LoginEvents(string? machineName = null, string? username = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            ViewBag.MachineName = machineName;
            ViewBag.Username = username;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;

            var query = _dbContext.LoginEvents.AsQueryable();
            if (!string.IsNullOrEmpty(machineName))
                query = query.Where(e => e.MachineName == machineName);
            if (!string.IsNullOrEmpty(username))
                query = query.Where(e => e.Username.Contains(username));

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var events = await query
                .OrderByDescending(e => e.LoginTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.Events = events;

            var machines = await _dbContext.LoginEvents.Select(e => e.MachineName).Distinct().OrderBy(m => m).ToListAsync();
            ViewBag.Machines = machines;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при зареждане на LoginEvents: {ex.Message}", ex);
            ViewBag.Error = ex.Message;
            return View("Error");
        }
    }

    /// <summary>
    /// Логове въвеждане и кликове (клавиатура + мишка) от Monitor.
    /// </summary>
    [Route("/input-logs")]
    [HttpGet]
    public async Task<IActionResult> InputLogs(string? machineName = null, string? username = null, string? logType = null, [FromQuery] string? dateFrom = null, [FromQuery] string? dateTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        ViewBag.MachineName = machineName;
        ViewBag.Username = username;
        ViewBag.LogType = logType;
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.ErrorMessage = (string?)null;

        try
        {
            var query = _dbContext.InputLogs.AsQueryable();
            if (!string.IsNullOrEmpty(machineName))
                query = query.Where(e => e.MachineName.ToLower() == machineName.ToLower());
            if (!string.IsNullOrEmpty(username))
                query = query.Where(e => e.Username.ToLower().Contains(username.ToLower()));
            if (!string.IsNullOrEmpty(logType) && (logType == "Key" || logType == "Click"))
                query = query.Where(e => e.LogType == logType);
            if (DateTime.TryParse(dateFrom, out var from))
                query = query.Where(e => e.Timestamp >= from);
            if (DateTime.TryParse(dateTo, out var to))
                query = query.Where(e => e.Timestamp < to.AddDays(1));

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            var items = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.Items = items;

            var machines = await _dbContext.InputLogs.Select(e => e.MachineName).Distinct().OrderBy(m => m).ToListAsync();
            ViewBag.Machines = machines;

            // Показваме бележка когато всички записи са тестови (от /api/logs/input-test-add)
            bool isOnlyTestData = items.Count > 0 && items.All(e =>
                string.Equals(e.Username, "TestUser", StringComparison.OrdinalIgnoreCase) &&
                (e.Data?.Contains("Тест", StringComparison.OrdinalIgnoreCase) == true || e.Data?.Contains("InputLogs работи", StringComparison.OrdinalIgnoreCase) == true));
            ViewBag.IsOnlyTestData = isOnlyTestData;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при зареждане на InputLogs: {ex.Message}", ex);
            ViewBag.Items = new List<ADS.WindowsAuth.Core.Data.Entities.InputLogEntity>();
            ViewBag.Machines = new List<string>();
            ViewBag.TotalCount = 0;
            ViewBag.TotalPages = 1;
            ViewBag.ErrorMessage = "Таблицата InputLogs не е налична или има проблем с базата данни. Уверете се, че API е стартиран поне веднъж за създаване на таблиците.";
            ViewBag.IsOnlyTestData = false;
        }

        return View();
    }

    /// <summary>
    /// Страница за анализ на продуктивност – изисква вход
    /// </summary>
    [Route("/performance")]
    [HttpGet]
    public async Task<IActionResult> Performance(string? username = null, string? machineName = null, [FromQuery] string? dateStr = null)
    {
        try
        {
            var date = DateTime.Today;
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsed))
                date = parsed.Date;

            ViewBag.SelectedUsername = username;
            ViewBag.SelectedMachine = machineName;
            ViewBag.SelectedDate = date.ToString("yyyy-MM-dd");
            ViewBag.SelectedDateDisplay = date.ToString("dd.MM.yyyy");

            // Потребители и машини от БД (UserActivities, ApplicationEvents, LogEntries) + от паметта, за да има винаги кого/какъв да избереш
            var usersFromDb = new List<string>();
            var machinesFromDb = new List<string>();
            try
            {
                usersFromDb = (await _dbContext.UserActivities.Select(a => a.Username).Distinct().ToListAsync())
                    .Union(await _dbContext.ApplicationEvents.Select(e => e.Username).Distinct().ToListAsync())
                    .ToList();
                machinesFromDb = (await _dbContext.UserActivities.Select(a => a.MachineName).Distinct().ToListAsync())
                    .Union(await _dbContext.ApplicationEvents.Select(e => e.MachineName).Distinct().ToListAsync())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Performance: не може да се заредят потребители/машини от БД: {ex.Message}");
            }
            try
            {
                var logUsers = await _dbContext.LogEntries.Select(l => l.Username).Distinct().ToListAsync();
                var logMachines = await _dbContext.LogEntries.Select(l => l.MachineName).Distinct().ToListAsync();
                usersFromDb = usersFromDb.Union(logUsers, StringComparer.OrdinalIgnoreCase).ToList();
                machinesFromDb = machinesFromDb.Union(logMachines, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { /* LogEntries може да не съществува */ }
            var usersFromMemory = _activityMonitor.GetAllActivities().Select(a => a.Username).Distinct().ToList();
            var machinesFromMemory = _activityMonitor.GetAllActivities().Select(a => a.MachineName).Distinct().ToList();
            var users = usersFromDb.Union(usersFromMemory, StringComparer.OrdinalIgnoreCase).OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList();
            var machines = machinesFromDb.Union(machinesFromMemory, StringComparer.OrdinalIgnoreCase).OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();
            ViewBag.Users = users;
            ViewBag.Machines = machines;

            ViewBag.VisitedWebsites = new List<ADS.WindowsAuth.Core.Data.Entities.VisitedWebsiteEntity>();
            ViewBag.AllApplications = new List<(string App, int Minutes, int Sessions)>();

            var startOfDay = date;
            var endOfDay = date.AddDays(1);

            int totalSeconds = 0, activeSeconds = 0, focusBlocks = 0, longestFocusSeconds = 0;
            DateTime? firstActivity = null, lastActivity = null;
            var hourlyActive = new int[24];
            var hourlyPassive = new int[24];
            var topApps = new List<(string App, int Minutes)>();

            if (!string.IsNullOrEmpty(username))
            {
                var activitiesQuery = _dbContext.UserActivities
                    .Where(a => a.Username == username && a.StartTime < endOfDay && (a.EndTime == null || a.EndTime >= startOfDay));
                if (!string.IsNullOrEmpty(machineName))
                    activitiesQuery = activitiesQuery.Where(a => a.MachineName == machineName);

                var activities = await activitiesQuery.ToListAsync();
                foreach (var a in activities)
                {
                    var start = a.StartTime < startOfDay ? startOfDay : a.StartTime;
                    var end = (a.EndTime ?? DateTime.Now) > endOfDay ? endOfDay : (a.EndTime ?? DateTime.Now);
                    totalSeconds += (int)(end - start).TotalSeconds;
                    if (!firstActivity.HasValue || a.StartTime < firstActivity) firstActivity = a.StartTime;
                    if (!lastActivity.HasValue || (a.EndTime ?? DateTime.Now) > lastActivity) lastActivity = a.EndTime ?? DateTime.Now;
                }

                var appEventsQuery = _dbContext.ApplicationEvents
                    .Where(e => e.Username == username && e.EventType == "Stop" && e.EventTime >= startOfDay && e.EventTime < endOfDay && e.DurationSeconds.HasValue);
                if (!string.IsNullOrEmpty(machineName))
                    appEventsQuery = appEventsQuery.Where(e => e.MachineName == machineName);

                var appEvents = await appEventsQuery.ToListAsync();
                foreach (var e in appEvents)
                {
                    var dur = e.DurationSeconds ?? 0;
                    activeSeconds += dur;
                    if (dur >= 1500) focusBlocks++;
                    if (dur > longestFocusSeconds) longestFocusSeconds = dur;
                    var hour = e.EventTime.Hour;
                    if (hour >= 0 && hour < 24) hourlyActive[hour] += dur / 60;
                }

                var appGroups = await appEventsQuery
                    .GroupBy(e => e.ApplicationName)
                    .Select(g => new { App = g.Key, Minutes = g.Sum(e => (e.DurationSeconds ?? 0) / 60), Sessions = g.Count() })
                    .OrderByDescending(x => x.Minutes)
                    .ToListAsync();
                topApps = appGroups.Take(10).Select(x => (x.App, x.Minutes)).ToList();
                ViewBag.AllApplications = appGroups.Select(x => (x.App, x.Minutes, x.Sessions)).ToList();

                // Посетени уебсайтове за деня (от extension или бъдещ Monitor код)
                var visitedWebsites = new List<ADS.WindowsAuth.Core.Data.Entities.VisitedWebsiteEntity>();
                try
                {
                    var visitedQuery = _dbContext.VisitedWebsites
                        .Where(v => v.Username == username && v.VisitedAt >= startOfDay && v.VisitedAt < endOfDay);
                    if (!string.IsNullOrEmpty(machineName))
                        visitedQuery = visitedQuery.Where(v => v.MachineName == machineName);
                    visitedWebsites = await visitedQuery.OrderByDescending(v => v.VisitedAt).Take(200).ToListAsync();
                }
                catch { /* VisitedWebsites таблицата може да липсва */ }
                ViewBag.VisitedWebsites = visitedWebsites;

                for (int h = 0; h < 24; h++)
                {
                    var sessionMinutesInHour = 0;
                    foreach (var a in activities)
                    {
                        var hourStart = date.AddHours(h);
                        var hourEnd = hourStart.AddHours(1);
                        var sessStart = a.StartTime < hourStart ? hourStart : a.StartTime;
                        var sessEnd = (a.EndTime ?? DateTime.Now) > hourEnd ? hourEnd : (a.EndTime ?? DateTime.Now);
                        if (sessStart < sessEnd)
                            sessionMinutesInHour += (int)(sessEnd - sessStart).TotalMinutes;
                    }
                    hourlyPassive[h] = Math.Max(0, sessionMinutesInHour - hourlyActive[h]);
                }
            }

            var passiveSeconds = Math.Max(0, totalSeconds - activeSeconds);
            var productivityPercent = totalSeconds > 0 ? (double)activeSeconds / totalSeconds * 100 : 0;

            ViewBag.TotalSeconds = totalSeconds;
            ViewBag.ActiveSeconds = activeSeconds;
            ViewBag.PassiveSeconds = passiveSeconds;
            ViewBag.ProductivityPercent = Math.Round(productivityPercent, 2);
            ViewBag.FocusBlocks = focusBlocks;
            ViewBag.LongestFocusSeconds = longestFocusSeconds;
            ViewBag.FirstActivity = firstActivity?.ToString("HH:mm");
            ViewBag.LastActivity = lastActivity?.ToString("HH:mm");
            ViewBag.HourlyActive = hourlyActive;
            ViewBag.HourlyPassive = hourlyPassive;
            ViewBag.TopApps = topApps;
            ViewBag.HasData = totalSeconds > 0 || activeSeconds > 0;

            // Брой записи за клавиши/кликове за избрания потребител и дата (за видима секция в Продуктивност)
            int inputLogsCountForUserAndDay = 0;
            try
            {
                var inputQuery = _dbContext.InputLogs
                    .Where(e => e.Timestamp >= startOfDay && e.Timestamp < endOfDay);
                if (!string.IsNullOrEmpty(username))
                    inputQuery = inputQuery.Where(e => e.Username == username);
                if (!string.IsNullOrEmpty(machineName))
                    inputQuery = inputQuery.Where(e => e.MachineName != null && e.MachineName.ToLower() == machineName.ToLower());
                inputLogsCountForUserAndDay = await inputQuery.CountAsync();
            }
            catch { /* InputLogs може да липсва */ }
            ViewBag.InputLogsCountForUserAndDay = inputLogsCountForUserAndDay;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при зареждане на Performance view: {ex.Message}", ex);
            ViewBag.Error = ex.Message;
            return View("Error");
        }
    }

    /// <summary>
    /// Дейности (клавиши/кликове) за конкретно приложение – от Performance страницата (Chrome, SAP и др.).
    /// </summary>
    [Route("/performance/app-activity")]
    [HttpGet]
    public async Task<IActionResult> PerformanceAppActivity(string username, string app, string? machineName = null, [FromQuery] string? dateStr = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(app))
        {
            return RedirectToAction(nameof(Performance));
        }

        var date = DateTime.Today;
        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsed))
            date = parsed.Date;
        var startOfDay = date;
        var endOfDay = date.AddDays(1);

        ViewBag.SelectedUsername = username;
        ViewBag.SelectedApp = app;
        ViewBag.SelectedMachine = machineName;
        ViewBag.SelectedDate = date.ToString("yyyy-MM-dd");
        ViewBag.SelectedDateDisplay = date.ToString("dd.MM.yyyy");
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.BackUrl = $"/performance?username={Uri.EscapeDataString(username)}&machineName={Uri.EscapeDataString(machineName ?? "")}&dateStr={date:yyyy-MM-dd}";

        var items = new List<ADS.WindowsAuth.Core.Data.Entities.InputLogEntity>();
        var totalCount = 0;
        int totalInputLogsForDay = 0;
        var usernamesWithData = new List<string>();
        var appsWithData = new List<string>();
        try
        {
            var appLower = app.ToLower();
            var query = _dbContext.InputLogs
                .Where(e => e.Username == username
                    && e.Timestamp >= startOfDay
                    && e.Timestamp < endOfDay
                    && ((e.ApplicationName != null && e.ApplicationName.ToLower().Contains(appLower))
                        || (e.WindowTitle != null && e.WindowTitle.ToLower().Contains(appLower))));
            if (!string.IsNullOrEmpty(machineName))
                query = query.Where(e => e.MachineName != null && e.MachineName.ToLower() == machineName.ToLower());

            totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            items = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Диагностика при 0 записа: има ли изобщо данни за този ден и от кой потребител/приложение
            totalInputLogsForDay = await _dbContext.InputLogs
                .Where(e => e.Timestamp >= startOfDay && e.Timestamp < endOfDay)
                .CountAsync();
            if (totalInputLogsForDay > 0)
            {
                usernamesWithData = await _dbContext.InputLogs
                    .Where(e => e.Timestamp >= startOfDay && e.Timestamp < endOfDay)
                    .Select(e => e.Username)
                    .Distinct()
                    .OrderBy(u => u)
                    .Take(20)
                    .ToListAsync();
                appsWithData = await _dbContext.InputLogs
                    .Where(e => e.Timestamp >= startOfDay && e.Timestamp < endOfDay && e.ApplicationName != null)
                    .Select(e => e.ApplicationName)
                    .Distinct()
                    .OrderBy(a => a)
                    .Take(30)
                    .ToListAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"PerformanceAppActivity: {ex.Message}");
            ViewBag.ErrorMessage = "Таблицата InputLogs не е налична или има грешка при зареждане.";
        }

        ViewBag.Items = items;
        ViewBag.ErrorMessage = ViewBag.ErrorMessage ?? (string?)null;
        ViewBag.TotalInputLogsForDay = totalInputLogsForDay;
        ViewBag.UsernamesWithData = usernamesWithData;
        ViewBag.AppsWithData = appsWithData;
        return View();
    }

    /// <summary>
    /// Excel/CSV експорт за анализ на продуктивност
    /// </summary>
    [HttpGet("performance/export")]
    public async Task<IActionResult> PerformanceExport(string username, string? machineName, [FromQuery] string? dateStr = null)
    {
        var date = DateTime.Today;
        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsed))
            date = parsed.Date;
        var startOfDay = date;
        var endOfDay = date.AddDays(1);

        var appEventsQuery = _dbContext.ApplicationEvents
            .Where(e => e.Username == username && e.EventType == "Stop" && e.EventTime >= startOfDay && e.EventTime < endOfDay);
        if (!string.IsNullOrEmpty(machineName))
            appEventsQuery = appEventsQuery.Where(e => e.MachineName == machineName);

        var events = await appEventsQuery.OrderBy(e => e.EventTime).ToListAsync();
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ApplicationName;EventTime;DurationSeconds;DurationMinutes;MachineName");
        foreach (var e in events)
        {
            var min = (e.DurationSeconds ?? 0) / 60;
            csv.AppendLine($"{e.ApplicationName};{e.EventTime:yyyy-MM-dd HH:mm:ss};{e.DurationSeconds ?? 0};{min};{e.MachineName}");
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"performance_{username}_{date:yyyyMMdd}.csv");
    }

    /// <summary>
    /// Страница за преглед на всички машини (Monitor) – изисква вход
    /// Използва in-memory + БД (UserActivities, LogEntries) за да показва вързаните машини дори след рестарт
    /// </summary>
    [AllowAnonymous]
    [Route("/monitor")]
    [HttpGet]
    public async Task<IActionResult> Monitor()
    {
        try
        {
            const int activeThresholdMinutes = 10; // Свързан = активност/лог преди последните 10 мин
            var cutoff = DateTime.Now.AddMinutes(-activeThresholdMinutes);
            var machineDict = new Dictionary<string, MachineMonitorInfo>(StringComparer.OrdinalIgnoreCase);

            // 1. In-memory активност (реално време)
            var memActivities = _activityMonitor.GetAllActivities();
            foreach (var g in memActivities.GroupBy(a => a.MachineName, StringComparer.OrdinalIgnoreCase))
            {
                var first = g.OrderByDescending(a => a.StartTime).First();
                machineDict[g.Key] = new MachineMonitorInfo
                {
                    MachineName = g.Key,
                    Username = first.Username,
                    Domain = first.Domain,
                    LastSeen = g.Max(a => a.EndTime ?? a.StartTime),
                    StartTime = g.OrderByDescending(a => a.StartTime).First().StartTime,
                    IsActive = g.Any(a => !a.EndTime.HasValue),
                    TotalUsers = g.Select(a => a.Username).Distinct().Count(),
                    ScreenTimeSeconds = g.Sum(a => a.ScreenTimeSeconds),
                    ApplicationsCount = g.Sum(a => a.OpenedApplications.Count),
                    FilesCount = g.Sum(a => a.OpenedFiles.Count)
                };
            }

            // 2. UserActivities от БД (машини, които са изпратили /api/activity/start)
            try
            {
                var allActivities = await _dbContext.UserActivities.ToListAsync();
                var dbActivities = allActivities
                    .GroupBy(a => a.MachineName, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var latest = g.OrderByDescending(x => x.StartTime).First();
                        var lastActivity = g.Max(a => a.EndTime ?? a.StartTime);
                        return new { MachineName = g.Key, LastActivity = lastActivity, Latest = latest };
                    })
                    .ToList();

                foreach (var a in dbActivities)
                {
                    if (!machineDict.ContainsKey(a.MachineName))
                    {
                        machineDict[a.MachineName] = new MachineMonitorInfo
                        {
                            MachineName = a.MachineName,
                            Username = a.Latest.Username,
                            Domain = a.Latest.Domain,
                            LastSeen = a.LastActivity,
                            StartTime = a.Latest.StartTime,
                            IsActive = a.LastActivity >= cutoff,
                            TotalUsers = 1,
                            ScreenTimeSeconds = 0,
                            ApplicationsCount = 0,
                            FilesCount = 0
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Monitor: UserActivities недостъпни: {ex.Message}");
            }

            // 3. LogEntries от БД (машини, които са изпращали логове, но няма UserActivity)
            try
            {
                var machinesFromLogs = await _dbContext.LogEntries
                    .GroupBy(l => l.MachineName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { MachineName = g.Key, LastLog = g.Max(l => l.Timestamp) })
                    .ToListAsync();

                foreach (var m in machinesFromLogs)
                {
                    if (!machineDict.ContainsKey(m.MachineName))
                    {
                        machineDict[m.MachineName] = new MachineMonitorInfo
                        {
                            MachineName = m.MachineName,
                            Username = "-",
                            Domain = "-",
                            LastSeen = m.LastLog,
                            StartTime = m.LastLog,
                            IsActive = m.LastLog >= cutoff,
                            TotalUsers = 1,
                            ScreenTimeSeconds = 0,
                            ApplicationsCount = 0,
                            FilesCount = 0
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Monitor: LogEntries недостъпни: {ex.Message}");
            }

            var machines = machineDict.Values
                .OrderByDescending(m => m.IsActive)
                .ThenByDescending(m => m.LastSeen)
                .ToList();

            ViewBag.Machines = machines;
            ViewBag.TotalMachines = machines.Count;
            ViewBag.ActiveMachines = machines.Count(m => m.IsActive);

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError("Грешка при зареждане на Monitor view", ex);
            return View("Error");
        }
    }

    /// <summary>
    /// Страница за управление на настройки на Monitor Service
    /// </summary>
    [Route("/monitor-settings")]
    [HttpGet]
    public async Task<IActionResult> MonitorSettings(string? machineName = null)
    {
        try
        {
            var machinesFromActivity = _activityMonitor.GetAllActivities()
                .Select(a => a.MachineName)
                .Distinct()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // MonitorConfigurations може да липсва или да има различна schema
            List<string> machinesFromDb;
            try
            {
                machinesFromDb = await _dbContext.MonitorConfigurations
                    .Select(c => c.MachineName)
                    .ToListAsync();
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning($"MonitorConfigurations таблица недостъпна: {dbEx.Message}");
                machinesFromDb = new List<string>();
            }
            
            var machines = machinesFromActivity
                .Union(machinesFromDb, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!string.IsNullOrEmpty(machineName) && !machines.Contains(machineName, StringComparer.OrdinalIgnoreCase))
            {
                machines.Add(machineName);
                machines = machines.OrderBy(m => m).ToList();
            }
            else
            {
                machines = machines.OrderBy(m => m).ToList();
            }

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
    public bool ScreenshotEnabled { get; set; } = false;
    public int ScreenshotIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Модел за статистика по нива
/// </summary>
public class LevelStat
{
    public string Level { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// Модел за информация за машина в Monitor изгледа
/// </summary>
public class MachineMonitorInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; }
    public int TotalUsers { get; set; }
    public int ScreenTimeSeconds { get; set; }
    public int ApplicationsCount { get; set; }
    public int FilesCount { get; set; }

    public string ScreenTimeFormatted =>
        TimeSpan.FromSeconds(ScreenTimeSeconds).ToString(ScreenTimeSeconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");

    public string LastSeenAgo
    {
        get
        {
            var diff = DateTime.Now - LastSeen;
            if (diff.TotalMinutes < 1) return "преди малко";
            if (diff.TotalMinutes < 60) return $"преди {(int)diff.TotalMinutes} мин.";
            if (diff.TotalHours < 24) return $"преди {(int)diff.TotalHours} ч.";
            return $"преди {(int)diff.TotalDays} дни";
        }
    }
}

// ... existing code ...