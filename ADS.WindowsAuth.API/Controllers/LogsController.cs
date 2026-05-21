using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

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
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public LogsController(
        IActivityMonitorService activityMonitor,
        ILoggerService logger,
        ApplicationDbContext dbContext,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _activityMonitor = activityMonitor;
        _logger = logger;
        _dbContext = dbContext;
        _env = env;
        _configuration = configuration;
    }

    /// <summary>
    /// Диагностика защо няма логове в базата: connection string, брой записи, тест на запис.
    /// Винаги връща 200 + JSON (никога 500).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("db-check")]
    public async Task<IActionResult> DbCheck()
    {
        try
        {
            var connStr = _configuration?.GetConnectionString("DefaultConnection");
            var connectionStringSet = !string.IsNullOrWhiteSpace(connStr);

            if (!connectionStringSet)
            {
                return Ok(new Dictionary<string, object>
                {
                    ["connectionStringSet"] = false,
                    ["hint"] = "ConnectionStrings:DefaultConnection не е зададен в appsettings – API използва in-memory база, логовете не се пазят. Задайте го в appsettings.Production.json или в средата на сървъра.",
                    ["logEntriesCount"] = -1,
                    ["writeOk"] = false,
                    ["error"] = (string?)null
                });
            }

            try
            {
                var count = await _dbContext.LogEntries.CountAsync();
                var testEntry = new LogEntryEntity
                {
                    MachineName = Environment.MachineName,
                    Username = "DbCheckTest",
                    Domain = "Test",
                    Level = "INFO",
                    Message = "[db-check] Тест – изтрива се веднага.",
                    Timestamp = DateTime.UtcNow,
                    Source = "ApiDbCheck"
                };
                _dbContext.LogEntries.Add(testEntry);
                await _dbContext.SaveChangesAsync();
                _dbContext.LogEntries.Remove(testEntry);
                await _dbContext.SaveChangesAsync();

                return Ok(new Dictionary<string, object>
                {
                    ["connectionStringSet"] = true,
                    ["hint"] = (string?)null,
                    ["logEntriesCount"] = count,
                    ["writeOk"] = true,
                    ["error"] = (string?)null
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError($"db-check: {ex.Message}", ex);
                return Ok(new Dictionary<string, object>
                {
                    ["connectionStringSet"] = true,
                    ["hint"] = (string?)null,
                    ["logEntriesCount"] = -1,
                    ["writeOk"] = false,
                    ["error"] = ex.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"db-check (outer): {ex.Message}", ex);
            return Ok(new Dictionary<string, object>
            {
                ["connectionStringSet"] = false,
                ["hint"] = (string?)null,
                ["logEntriesCount"] = -1,
                ["writeOk"] = false,
                ["error"] = ex.Message
            });
        }
    }

    /// <summary>
    /// Чете последните редове от лог файла на API сървъра (Serilog). За преглед в уеб портала при хостиране.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("server-file")]
    public IActionResult GetServerLogFile([FromQuery] int maxLines = 1500)
    {
        try
        {
            var logDir = Path.Combine(_env.ContentRootPath ?? ".", "logs");
            if (!Directory.Exists(logDir))
                return Ok(new { content = $"(Папка logs не съществува: {logDir})", fileName = "", warning = "Няма записани логове." });

            var files = Directory.GetFiles(logDir, "ads-windows-auth-*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            if (files.Count == 0)
                return Ok(new { content = "", fileName = "", warning = "Няма намерени лог файлове (ads-windows-auth-*.log)." });

            var file = files[0];
            const int maxBytes = 512 * 1024; // четем макс. 512 KB от края
            string content;
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long length = fs.Length;
                if (length <= maxBytes)
                {
                    fs.Position = 0;
                    using var reader = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    content = reader.ReadToEnd();
                }
                else
                {
                    fs.Position = length - maxBytes;
                    using var reader = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    reader.ReadLine(); // изхвърляме частично отрязан ред
                    content = reader.ReadToEnd();
                }
            }

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > maxLines)
                lines = lines.TakeLast(maxLines).ToArray();
            content = string.Join(Environment.NewLine, lines);

            return Ok(new { content, fileName = file.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при четене на лог файл: {ex.Message}", ex);
            return Ok(new { content = "", fileName = "", error = ex.Message });
        }
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
    /// Вмъква един тестов запис в LogEntries – за проверка дали таблицата и порталът работят.
    /// Отвори в браузър: /api/logs/test-add
    /// </summary>
    [AllowAnonymous]
    [HttpGet("test-add")]
    public async Task<IActionResult> AddTestLog()
    {
        try
        {
            var logEntry = new LogEntryEntity
            {
                MachineName = Environment.MachineName,
                Username = "TestUser",
                Domain = "TestDomain",
                Level = "INFO",
                Message = $"[Тест] Запис от API в {DateTime.Now:yyyy-MM-dd HH:mm:ss} – ако виждате този лог, таблицата LogEntries и порталът работят.",
                Timestamp = DateTime.UtcNow,
                Source = "ApiTestAdd"
            };
            _dbContext.LogEntries.Add(logEntry);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Тестовият лог е записан.", id = logEntry.Id, machineName = logEntry.MachineName });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на тестов лог: {ex.Message}", ex);
            // Връщаме 200 + JSON с грешка, за да не се показва generic IIS 500 страница
            return Ok(new { success = false, message = "Грешка при запис в базата. Създайте таблицата LogEntries (вижте Scripts/CreateTables-FromProgram.sql).", error = ex.Message });
        }
    }

    /// <summary>
    /// Получава лог запис от клиент и го записва в базата данни. Monitor вика без логин – AllowAnonymous.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("upload")]
    public async Task<IActionResult> UploadLog([FromBody] LogUploadRequest? request)
    {
        try
        {
            // Проверка за null request
            if (request == null)
            {
                _logger.LogWarning("API: UploadLog получи null request");
                return BadRequest(new { message = "Request е задължителен" });
            }

            // Валидация на задължителните полета
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                _logger.LogWarning("API: UploadLog получи празно Message поле");
                return BadRequest(new { message = "Message е задължителен и не може да е празен" });
            }

            try
            {
                // Санитизиране на входните данни
                var machineName = string.IsNullOrWhiteSpace(request.MachineName) 
                    ? Environment.MachineName 
                    : request.MachineName.Trim();
                
                var username = string.IsNullOrWhiteSpace(request.Username) 
                    ? Environment.UserName 
                    : request.Username.Trim();
                
                var domain = string.IsNullOrWhiteSpace(request.Domain) 
                    ? Environment.UserDomainName 
                    : request.Domain.Trim();
                
                var level = string.IsNullOrWhiteSpace(request.Level) 
                    ? "INFO" 
                    : request.Level.Trim().ToUpper();
                
                var message = request.Message.Trim();
                var source = string.IsNullOrWhiteSpace(request.Source) 
                    ? null 
                    : request.Source.Trim();
                
                var exceptionType = string.IsNullOrWhiteSpace(request.ExceptionType) 
                    ? null 
                    : request.ExceptionType.Trim();
                
                var stackTrace = string.IsNullOrWhiteSpace(request.StackTrace) 
                    ? null 
                    : request.StackTrace.Trim();

                var logEntry = new LogEntryEntity
                {
                    MachineName = machineName,
                    Username = username,
                    Domain = domain,
                    Level = level,
                    Message = message,
                    Timestamp = request.Timestamp ?? DateTime.UtcNow,
                    Source = source,
                    ExceptionType = exceptionType,
                    StackTrace = stackTrace
                };

                _dbContext.LogEntries.Add(logEntry);
                await _dbContext.SaveChangesAsync();

                // Логване на успешния запис
                _logger.LogInfo($"Log received from {logEntry.MachineName}: [{logEntry.Level}] {logEntry.Message}");

                return Ok(new { message = "Логът е записан успешно", id = logEntry.Id });
            }
            catch (DbUpdateException dbEx)
            {
                // Специална обработка за database грешки
                _logger.LogError($"Database грешка при запис на лог: {dbEx.Message}", dbEx);
                if (dbEx.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {dbEx.InnerException.Message}", dbEx.InnerException);
                }
                
                // Връщаме успешен отговор но логваме грешката (за да не блокираме клиента)
                return Ok(new { message = "Логът е получен (възможна грешка при запис в базата данни)" });
            }
            catch (ArgumentException argEx)
            {
                // Обработка на грешки при валидация на аргументи
                _logger.LogError($"Грешка при валидация на аргументи: {argEx.Message}", argEx);
                return Ok(new { message = "Логът е получен (възможна грешка при валидация)" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на лог: {ex.Message}", ex);
            _logger.LogError($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Inner exception: {ex.InnerException.Message}", ex.InnerException);
            }
            
            // Връщаме успешен отговор но логваме грешката (за да не блокираме клиента)
            return Ok(new { message = "Логът е получен (възможна грешка при обработка)" });
        }
    }

    /// <summary>
    /// Диагностика: брой записи в InputLogs и последен запис (за проверка дали API вижда данните).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("input-stats")]
    public async Task<IActionResult> GetInputLogsStats()
    {
        var total = -1;
        object? last = null;
        string? error = null;

        try
        {
            total = await _dbContext.InputLogs.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"InputLogs stats CountAsync: {ex.Message}", ex);
            error = ex.Message;
            return Ok(new { total = -1, last = (object?)null, error });
        }

        try
        {
            var lastEntity = await _dbContext.InputLogs
                .OrderByDescending(e => e.Timestamp)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (lastEntity != null)
                last = new { lastEntity.Id, lastEntity.Username, lastEntity.MachineName, lastEntity.ApplicationName, lastEntity.Timestamp };
        }
        catch (Exception ex)
        {
            _logger.LogError($"InputLogs stats FirstOrDefault: {ex.Message}", ex);
            error = ex.Message;
        }

        return Ok(new { total, last, error });
    }

    /// <summary>
    /// Вмъква един тестов запис в InputLogs – за проверка дали таблицата и страницата работят.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("input-test-add")]
    public async Task<IActionResult> AddTestInputLog()
    {
        try
        {
            var entry = new InputLogEntity
            {
                MachineName = Environment.MachineName,
                Username = "TestUser",
                Domain = "TestDomain",
                Timestamp = DateTime.UtcNow,
                LogType = "Key",
                ApplicationName = "notepad",
                WindowTitle = "Тестов запис",
                Data = "[Тест] Клавиш/клик – ако виждате този ред, таблицата InputLogs работи.",
                IsPassword = false
            };
            _dbContext.InputLogs.Add(entry);
            await _dbContext.SaveChangesAsync();
            return Ok(new { success = true, message = "Тестов запис в InputLogs е добавен.", id = entry.Id, machineName = entry.MachineName });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на тестов input log: {ex.Message}", ex);
            return Ok(new { success = false, message = "Грешка. Създайте таблицата InputLogs (Scripts/CreateTables-FromProgram.sql).", error = ex.Message });
        }
    }

    /// <summary>
    /// Получава batch от записи за въвеждане (клавиши/кликове) от Client или Monitor.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("input")]
    public async Task<IActionResult> UploadInputLogs([FromBody] InputLogUploadRequest? request)
    {
        if (request?.Items == null || !request.Items.Any())
            return BadRequest(new { message = "Items са задължителни" });
        try
        {
            var machine = (request.MachineName ?? Environment.MachineName).Trim();
            var user = (request.Username ?? Environment.UserName).Trim();
            var domain = (request.Domain ?? Environment.UserDomainName).Trim();
            foreach (var item in request.Items)
            {
                var data = (item.Data ?? "").Trim();
                if (data.Length > 2000) data = data.Substring(0, 2000);
                _dbContext.InputLogs.Add(new InputLogEntity
                {
                    MachineName = machine,
                    Username = user,
                    Domain = domain,
                    Timestamp = item.Timestamp ?? DateTime.UtcNow,
                    LogType = (item.LogType ?? "Key").Trim().ToLowerInvariant() == "click" ? "Click" : "Key",
                    ApplicationName = string.IsNullOrWhiteSpace(item.ApplicationName) ? null : item.ApplicationName.Trim().Length > 500 ? item.ApplicationName.Trim().Substring(0, 500) : item.ApplicationName.Trim(),
                    WindowTitle = string.IsNullOrWhiteSpace(item.WindowTitle) ? null : item.WindowTitle.Trim().Length > 1000 ? item.WindowTitle.Trim().Substring(0, 1000) : item.WindowTitle.Trim(),
                    Data = data,
                    IsPassword = item.IsPassword
                });
            }
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Записано", count = request.Items.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при запис на input logs: {ex.Message}", ex);
            return StatusCode(500, new { message = "Грешка при запис" });
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
            var q = _dbContext.LogEntries
                .Where(l => l.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.Timestamp);

            var logs = limit.HasValue ? q.Take(limit.Value).ToList() : q.ToList();

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

public class InputLogUploadRequest
{
    public string? MachineName { get; set; }
    public string? Username { get; set; }
    public string? Domain { get; set; }
    public List<InputLogItemDto> Items { get; set; } = new();
}

public class InputLogItemDto
{
    public string? LogType { get; set; } // "Key" or "Click"
    public string? ApplicationName { get; set; }
    public string? WindowTitle { get; set; }
    public string? Data { get; set; }
    public DateTime? Timestamp { get; set; }
    public bool IsPassword { get; set; }
}

