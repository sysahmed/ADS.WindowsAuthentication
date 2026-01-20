using Microsoft.AspNetCore.Mvc;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data.Entities;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Контролер за мониторинг на активност
/// </summary>
[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly IActivityMonitorService _activityMonitor;
    private readonly IDatabaseService? _databaseService;
    private readonly ILoggerService _logger;

    public ActivityController(
        IActivityMonitorService activityMonitor,
        IDatabaseService? databaseService,
        ILoggerService logger)
    {
        _activityMonitor = activityMonitor;
        _databaseService = databaseService; // Може да е null ако базата данни не е достъпна
        _logger = logger;
    }

    /// <summary>
    /// Стартиране на мониторинг
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartMonitoring([FromBody] StartMonitoringRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Username))
        {
            return BadRequest(new { Error = "Invalid request data" });
        }
        
        try
        {
            _activityMonitor.StartMonitoring(request.Username, request.Domain, request.MachineName);
            
            // Записване в базата данни (ако е наличен)
            if (_databaseService != null)
            {
                try
                {
                    var activity = new UserActivityEntity
                    {
                        Username = request.Username,
                        Domain = request.Domain,
                        MachineName = request.MachineName,
                        StartTime = DateTime.Now,
                        ScreenTimeSeconds = 0
                    };
                    await _databaseService.SaveOrUpdateUserActivityAsync(activity);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на UserActivity в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            _logger.LogInfo($"API: Започнат мониторинг за {request.Username}@{request.Domain} на {request.MachineName}");
            
            return Ok(new { message = "Мониторингът е започнат" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при стартиране на мониторинг", ex);
            return StatusCode(500, new { message = "Грешка при стартиране на мониторинг" });
        }
    }

    /// <summary>
    /// Спиране на мониторинг
    /// </summary>
    [HttpPost("stop")]
    public async Task<IActionResult> StopMonitoring([FromBody] StopMonitoringRequest request)
    {
        try
        {
            var activity = _activityMonitor.GetUserActivity(request.Username, request.MachineName);
            _activityMonitor.StopMonitoring(request.Username, request.MachineName);
            
            // Обновяване в базата данни (ако е наличен)
            if (activity != null && _databaseService != null)
            {
                try
                {
                    var activityEntity = new UserActivityEntity
                    {
                        Username = request.Username,
                        Domain = activity.Domain,
                        MachineName = request.MachineName,
                        StartTime = activity.StartTime,
                        EndTime = DateTime.Now,
                        ScreenTimeSeconds = activity.ScreenTimeSeconds
                    };
                    await _databaseService.SaveOrUpdateUserActivityAsync(activityEntity);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на UserActivity в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            _logger.LogInfo($"API: Спрян мониторинг за {request.Username} на {request.MachineName}");
            
            return Ok(new { message = "Мониторингът е спрян" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при спиране на мониторинг", ex);
            return StatusCode(500, new { message = "Грешка при спиране на мониторинг" });
        }
    }

    /// <summary>
    /// Регистрация на стартиране на приложение
    /// </summary>
    [HttpPost("application/start")]
    public async Task<IActionResult> RegisterApplicationStart([FromBody] ApplicationStartRequest request)
    {
        try
        {
            _activityMonitor.RegisterApplicationStart(
                request.Username, 
                request.MachineName, 
                request.ApplicationName, 
                request.ExecutablePath ?? "");
            
            // Записване в базата данни (ако е наличен)
            if (_databaseService != null)
            {
                try
                {
                    var appEvent = new ApplicationEventEntity
                    {
                        Username = request.Username,
                        Domain = request.Domain ?? "",
                        MachineName = request.MachineName,
                        ApplicationName = request.ApplicationName,
                        ExecutablePath = request.ExecutablePath,
                        ProcessId = request.ProcessId,
                        EventType = "Start",
                        EventTime = request.Timestamp ?? DateTime.Now
                    };
                    await _databaseService.SaveApplicationEventAsync(appEvent);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на ApplicationEvent в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            _logger.LogInfo($"API: Стартирано приложение {request.ApplicationName} от {request.Username}@{request.Domain} на {request.MachineName}");
            
            return Ok(new { message = "Приложението е регистрирано" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на стартиране на приложение", ex);
            return StatusCode(500, new { message = "Грешка при регистрация" });
        }
    }

    /// <summary>
    /// Регистрация на спиране на приложение
    /// </summary>
    [HttpPost("application/stop")]
    public async Task<IActionResult> RegisterApplicationStop([FromBody] ApplicationStopRequest request)
    {
        try
        {
            _activityMonitor.RegisterApplicationClose(
                request.Username, 
                request.MachineName, 
                request.ApplicationName);
            
            // Записване в базата данни (ако е наличен)
            if (_databaseService != null)
            {
                try
                {
                    var appEvent = new ApplicationEventEntity
                    {
                        Username = request.Username,
                        Domain = "",
                        MachineName = request.MachineName,
                        ApplicationName = request.ApplicationName,
                        EventType = "Stop",
                        DurationSeconds = request.DurationSeconds,
                        EventTime = DateTime.Now
                    };
                    await _databaseService.SaveApplicationEventAsync(appEvent);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на ApplicationEvent в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            _logger.LogInfo($"API: Спирано приложение {request.ApplicationName} от {request.Username} на {request.MachineName} (продължителност: {request.DurationSeconds} сек)");
            
            return Ok(new { message = "Спирането на приложението е регистрирано" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на спиране на приложение", ex);
            return StatusCode(500, new { message = "Грешка при регистрация" });
        }
    }

    /// <summary>
    /// Обновяване на screen time
    /// </summary>
    [HttpPost("screentime/update")]
    public async Task<IActionResult> UpdateScreenTime([FromBody] ScreenTimeUpdateRequest request)
    {
        try
        {
            _activityMonitor.UpdateScreenTime(request.Username, request.MachineName, request.Seconds);
            
            // Записване в базата данни (ако е наличен)
            if (_databaseService != null)
            {
                try
                {
                    var screenTime = new ScreenTimeEntity
                    {
                        Username = request.Username,
                        Domain = "",
                        MachineName = request.MachineName,
                        Seconds = request.Seconds,
                        RecordedAt = DateTime.Now
                    };
                    await _databaseService.SaveScreenTimeAsync(screenTime);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на ScreenTime в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            return Ok(new { message = "Screen time е обновен" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при обновяване на screen time", ex);
            return StatusCode(500, new { message = "Грешка при обновяване" });
        }
    }

    /// <summary>
    /// Регистрация на мрежова активност
    /// </summary>
    [HttpPost("network")]
    public async Task<IActionResult> RegisterNetworkActivity([FromBody] NetworkActivityRequest request)
    {
        try
        {
            _logger.LogInfo($"API: Мрежова активност от {request.Username}@{request.Domain} на {request.MachineName} - {request.NetworkInterfaces?.Count ?? 0} интерфейса");
            
            // Записване на всеки интерфейс в базата данни (ако е наличен)
            if (_databaseService != null && request.NetworkInterfaces != null)
            {
                foreach (var iface in request.NetworkInterfaces)
                {
                    if (iface is System.Text.Json.JsonElement jsonElement)
                    {
                        try
                        {
                            var networkActivity = new NetworkActivityEntity
                            {
                                Username = request.Username,
                                Domain = request.Domain ?? "",
                                MachineName = request.MachineName,
                                InterfaceName = jsonElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null,
                                InterfaceDescription = jsonElement.TryGetProperty("Description", out var descProp) ? descProp.GetString() : null,
                                Speed = jsonElement.TryGetProperty("Speed", out var speedProp) ? speedProp.GetInt64() : null,
                                BytesReceived = jsonElement.TryGetProperty("BytesReceived", out var recvProp) ? recvProp.GetInt64() : null,
                                BytesSent = jsonElement.TryGetProperty("BytesSent", out var sentProp) ? sentProp.GetInt64() : null,
                                EventTime = request.Timestamp ?? DateTime.Now
                            };
                            await _databaseService.SaveNetworkActivityAsync(networkActivity);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogError($"Грешка при запис на NetworkActivity в базата данни: {dbEx.Message}", dbEx);
                        }
                    }
                }
            }
            
            return Ok(new { message = "Мрежовата активност е регистрирана" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на мрежова активност", ex);
            return StatusCode(500, new { message = "Грешка при регистрация" });
        }
    }

    /// <summary>
    /// Регистрация на системна информация
    /// </summary>
    [HttpPost("system")]
    public async Task<IActionResult> RegisterSystemInfo([FromBody] SystemInfoRequest request)
    {
        try
        {
            _logger.LogInfo($"API: Системна информация от {request.Username}@{request.Domain} на {request.MachineName} - OS: {request.SystemInfo?.OsVersion}, CPU: {request.SystemInfo?.ProcessorCount}");
            
            // Записване в базата данни (ако е наличен)
            if (_databaseService != null && request.SystemInfo != null)
            {
                try
                {
                    var systemInfo = new SystemInfoEntity
                    {
                        MachineName = request.MachineName,
                        Username = request.Username,
                        Domain = request.Domain ?? "",
                        OsVersion = request.SystemInfo.OsVersion,
                        ProcessorCount = request.SystemInfo.ProcessorCount,
                        TotalMemory = request.SystemInfo.TotalMemory,
                        WorkingSet = request.SystemInfo.WorkingSet,
                        UptimeSeconds = request.SystemInfo.Uptime,
                        EventTime = request.Timestamp ?? DateTime.Now
                    };
                    await _databaseService.SaveSystemInfoAsync(systemInfo);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на SystemInfo в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            return Ok(new { message = "Системната информация е регистрирана" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на системна информация", ex);
            return StatusCode(500, new { message = "Грешка при регистрация" });
        }
    }

    /// <summary>
    /// Регистрация на USB устройства
    /// </summary>
    [HttpPost("usb")]
    public async Task<IActionResult> RegisterUsbDevices([FromBody] UsbDevicesRequest request)
    {
        try
        {
            _logger.LogInfo($"API: USB устройства от {request.Username}@{request.Domain} на {request.MachineName} - {request.UsbDevices?.Count ?? 0} устройства");
            
            // Записване на всяко USB устройство в базата данни (ако е наличен)
            if (_databaseService != null && request.UsbDevices != null)
            {
                foreach (var device in request.UsbDevices)
                {
                    if (device is System.Text.Json.JsonElement jsonElement)
                    {
                        try
                        {
                            var usbDevice = new UsbDeviceEntity
                            {
                                Username = request.Username,
                                Domain = request.Domain ?? "",
                                MachineName = request.MachineName,
                                DeviceId = jsonElement.TryGetProperty("DeviceId", out var idProp) ? idProp.GetString() ?? "" : "",
                                Description = jsonElement.TryGetProperty("Description", out var descProp) ? descProp.GetString() : null,
                                Manufacturer = jsonElement.TryGetProperty("Manufacturer", out var manProp) ? manProp.GetString() : null,
                                Name = jsonElement.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null,
                                EventType = "Connected",
                                EventTime = request.Timestamp ?? DateTime.Now
                            };
                            await _databaseService.SaveUsbDeviceAsync(usbDevice);
                        }
                        catch (Exception dbEx)
                        {
                            _logger.LogError($"Грешка при запис на UsbDevice в базата данни: {dbEx.Message}", dbEx);
                        }
                    }
                }
            }
            
            return Ok(new { message = "USB устройствата са регистрирани" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на USB устройства", ex);
            return StatusCode(500, new { message = "Грешка при регистрация" });
        }
    }

    /// <summary>
    /// Регистрация на файлова активност
    /// </summary>
    [HttpPost("files")]
    public async Task<IActionResult> RegisterFileActivity([FromBody] FileActivityRequest request)
    {
        try
        {
            _logger.LogInfo($"API: Файлова активност от {request.Username}@{request.Domain} на {request.MachineName} - {request.RecentFiles?.Count ?? 0} файла");
            
            // Регистрация на файловете
            if (request.RecentFiles != null)
            {
                foreach (var file in request.RecentFiles)
                {
                    if (file is System.Text.Json.JsonElement jsonElement)
                    {
                        string? fileName = jsonElement.TryGetProperty("FileName", out var fnProp) ? fnProp.GetString() : null;
                        string? filePath = jsonElement.TryGetProperty("FilePath", out var fpProp) ? fpProp.GetString() : null;
                        
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            _activityMonitor.RegisterFileOpen(
                                request.Username, 
                                request.MachineName, 
                                filePath, 
                                fileName ?? "");
                            
                            // Записване в базата данни (ако е наличен)
                            if (_databaseService != null)
                            {
                                try
                                {
                                    var fileActivity = new FileActivityEntity
                                    {
                                        Username = request.Username,
                                        Domain = request.Domain ?? "",
                                        MachineName = request.MachineName,
                                        FilePath = filePath,
                                        FileName = fileName ?? Path.GetFileName(filePath),
                                        FileExtension = Path.GetExtension(filePath),
                                        FileSize = jsonElement.TryGetProperty("Size", out var sizeProp) ? sizeProp.GetInt64() : null,
                                        ApplicationName = "",
                                        EventType = "Open",
                                        EventTime = jsonElement.TryGetProperty("LastModified", out var modProp) && modProp.TryGetDateTime(out var modDate) ? modDate : DateTime.Now
                                    };
                                    await _databaseService.SaveFileActivityAsync(fileActivity);
                                }
                                catch (Exception dbEx)
                                {
                                    _logger.LogError($"Грешка при запис на FileActivity в базата данни: {dbEx.Message}", dbEx);
                                }
                            }
                        }
                    }
                }
            }
            
            return Ok(new { message = "Файловата активност е регистрирана" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на файлова активност", ex);
            return StatusCode(500, new { message = "Грешка при регистрация" });
        }
    }

    /// <summary>
    /// Получаване на активност на потребител
    /// </summary>
    [HttpGet("user/{username}/{machineName}")]
    public IActionResult GetUserActivity(string username, string machineName)
    {
        try
        {
            UserActivity? activity = _activityMonitor.GetUserActivity(username, machineName);
            
            if (activity == null)
            {
                return NotFound(new { message = "Активността не е намерена" });
            }

            return Ok(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при получаване на активност", ex);
            return StatusCode(500, new { message = "Грешка при получаване на активност" });
        }
    }

    /// <summary>
    /// Получаване на всички активности
    /// </summary>
    [HttpGet("all")]
    public IActionResult GetAllActivities([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        try
        {
            List<UserActivity> activities = _activityMonitor.GetAllActivities(fromDate, toDate);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при получаване на всички активности", ex);
            return StatusCode(500, new { message = "Грешка при получаване на активности" });
        }
    }

    /// <summary>
    /// Регистрация на user login
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> RegisterUserLogin([FromBody] UserLoginRequest request)
    {
        try
        {
            _logger.LogInfo($"API: User login от {request.Username}@{request.Domain} на {request.MachineName} чрез {request.LoginMethod ?? "Password"}");
            
            // Записване в базата данни (ако е наличен)
            if (_databaseService != null)
            {
                try
                {
                    var loginEvent = new LoginEventEntity
                    {
                        Username = request.Username,
                        Domain = request.Domain,
                        MachineName = request.MachineName,
                        LoginTime = request.LoginTime ?? DateTime.Now,
                        LoginMethod = request.LoginMethod ?? "Password",
                        SessionId = request.SessionId,
                        Success = true,
                        IpAddress = request.IpAddress,
                        LogonType = request.LogonType
                    };
                    await _databaseService.SaveLoginEventAsync(loginEvent);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError($"Грешка при запис на LoginEvent в базата данни: {dbEx.Message}", dbEx);
                }
            }
            
            return Ok(new { message = "Login event регистриран" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при регистрация на login event", ex);
            return StatusCode(500, new { message = "Грешка при регистрация на login" });
        }
    }
}

// Request модели
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

public class ApplicationStartRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public int? ProcessId { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class ApplicationStopRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
}

public class ScreenTimeUpdateRequest
{
    public string Username { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public int Seconds { get; set; }
}

public class NetworkActivityRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<object>? NetworkInterfaces { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class SystemInfoRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public SystemInfo? SystemInfo { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class SystemInfo
{
    public string? OsVersion { get; set; }
    public int? ProcessorCount { get; set; }
    public long? TotalMemory { get; set; }
    public long? WorkingSet { get; set; }
    public double? Uptime { get; set; }
}

public class UsbDevicesRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<object>? UsbDevices { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class FileActivityRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<object>? RecentFiles { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class UserLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public DateTime? LoginTime { get; set; }
    public string? LoginMethod { get; set; } // "QRCode", "Password", "SmartCard"
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public int? LogonType { get; set; }
}

