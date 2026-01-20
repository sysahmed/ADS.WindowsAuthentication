using Serilog;
using Serilog.Events;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Enhanced logger service using Serilog for structured logging
/// </summary>
public class EnhancedLoggerService : ILoggerService
{
    private readonly ILogger _logger;
    private readonly string _applicationDirectory;

    public EnhancedLoggerService(string applicationDirectory)
    {
        _applicationDirectory = applicationDirectory;
        _logger = Log.ForContext<EnhancedLoggerService>()
                     .ForContext("ApplicationDirectory", applicationDirectory);
    }

    public void LogInfo(string message)
    {
        _logger.Information("{@LogEvent}", new { Message = message, Timestamp = DateTime.Now });
    }

    public void LogInfo(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    public void LogWarning(string message)
    {
        _logger.Warning("{@LogEvent}", new { Message = message, Timestamp = DateTime.Now });
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    public void LogError(string message)
    {
        _logger.Error("{@LogEvent}", new { Message = message, Timestamp = DateTime.Now });
    }

    public void LogError(string message, Exception? exception = null)
    {
        _logger.Error(exception, "{@LogEvent}", new { 
            Message = message, 
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            Timestamp = DateTime.Now 
        });
    }

    public void LogError(string message, params object[] args)
    {
        _logger.Error(message, args);
    }

    public void LogDebug(string message)
    {
        _logger.Debug("{@LogEvent}", new { Message = message, Timestamp = DateTime.Now });
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }

    /// <summary>
    /// Logs authentication attempt with structured data
    /// </summary>
    public void LogAuthenticationAttempt(string username, string domain, string machineName, bool success, string? errorMessage = null)
    {
        var logEvent = new
        {
            EventType = "AuthenticationAttempt",
            Username = username,
            Domain = domain,
            MachineName = machineName,
            Success = success,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.Now,
            SourceIP = GetClientIP()
        };

        if (success)
        {
            _logger.Information("Authentication successful for {@LogEvent}", logEvent);
        }
        else
        {
            _logger.Warning("Authentication failed for {@LogEvent}", logEvent);
        }
    }

    /// <summary>
    /// Logs session creation with structured data
    /// </summary>
    public void LogSessionCreated(string sessionId, string username, string domain, string machineName)
    {
        var logEvent = new
        {
            EventType = "SessionCreated",
            SessionId = sessionId,
            Username = username,
            Domain = domain,
            MachineName = machineName,
            Timestamp = DateTime.Now
        };

        _logger.Information("Session created {@LogEvent}", logEvent);
    }

    /// <summary>
    /// Logs session status change
    /// </summary>
    public void LogSessionStatusChanged(string sessionId, string oldStatus, string newStatus, string? changedBy = null)
    {
        var logEvent = new
        {
            EventType = "SessionStatusChanged",
            SessionId = sessionId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedBy = changedBy,
            Timestamp = DateTime.Now
        };

        _logger.Information("Session status changed {@LogEvent}", logEvent);
    }

    /// <summary>
    /// Logs LDAP operations with performance metrics
    /// </summary>
    public void LogLdapOperation(string operation, string username, string domain, bool success, TimeSpan duration, string? errorMessage = null)
    {
        var logEvent = new
        {
            EventType = "LdapOperation",
            Operation = operation,
            Username = username,
            Domain = domain,
            Success = success,
            DurationMs = duration.TotalMilliseconds,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.Now
        };

        if (success)
        {
            _logger.Information("LDAP operation completed {@LogEvent}", logEvent);
        }
        else
        {
            _logger.Warning("LDAP operation failed {@LogEvent}", logEvent);
        }
    }

    /// <summary>
    /// Logs database operations
    /// </summary>
    public void LogDatabaseOperation(string operation, string? details = null, bool success = true, string? errorMessage = null)
    {
        var logEvent = new
        {
            EventType = "DatabaseOperation",
            Operation = operation,
            Details = details,
            Success = success,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.Now
        };

        if (success)
        {
            _logger.Information("Database operation completed {@LogEvent}", logEvent);
        }
        else
        {
            _logger.Error("Database operation failed {@LogEvent}", logEvent);
        }
    }

    private string? GetClientIP()
    {
        // This would need to be injected from HttpContext in a real implementation
        // For now, return null
        return null;
    }
}
