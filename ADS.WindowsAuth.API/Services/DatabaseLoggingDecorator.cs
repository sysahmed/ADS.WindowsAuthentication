using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data;
using ADS.WindowsAuth.Core.Data.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ADS.WindowsAuth.API.Services;

/// <summary>
/// Декоратор за ILoggerService – записва логове в Serilog и в LogEntries таблицата.
/// Позволява централизиран преглед на API логовете през /system-logs.
/// </summary>
public class DatabaseLoggingDecorator : ILoggerService
{
    private const string ApiSource = "API";
    private readonly ILoggerService _inner;
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseLoggingDecorator(ILoggerService inner, IServiceScopeFactory scopeFactory)
    {
        _inner = inner;
        _scopeFactory = scopeFactory;
    }

    public void LogInfo(string message)
    {
        _inner.LogInfo(message);
        WriteToDatabaseAsync("INFO", message, null, null, null, null, null);
    }

    public void LogWarning(string message)
    {
        _inner.LogWarning(message);
        WriteToDatabaseAsync("WARNING", message, null, null, null, null, null);
    }

    public void LogError(string message, Exception? exception = null)
    {
        _inner.LogError(message, exception);
        var exType = exception?.GetType().Name;
        var stackTrace = exception?.StackTrace;
        WriteToDatabaseAsync("ERROR", message, null, null, null, exType, stackTrace);
    }

    /// <summary>
    /// Fire-and-forget запис в LogEntries. Не блокира основния поток.
    /// </summary>
    private void WriteToDatabaseAsync(
        string level,
        string message,
        string? username,
        string? domain,
        string? machineName,
        string? exceptionType,
        string? stackTrace)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                if (db == null) return;

                var entry = new LogEntryEntity
                {
                    MachineName = machineName ?? Environment.MachineName,
                    Username = username ?? string.Empty,
                    Domain = domain ?? string.Empty,
                    Level = level,
                    Message = message.Length > 4000 ? message[..4000] + "…" : message,
                    Timestamp = DateTime.UtcNow,
                    Source = ApiSource,
                    ExceptionType = exceptionType,
                    StackTrace = stackTrace != null && stackTrace.Length > 8000 ? stackTrace[..8000] + "…" : stackTrace
                };

                db.LogEntries.Add(entry);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Логваме само във файл (inner), за да не влизаме в цикъл с DB
                _inner.LogWarning($"[LogEntries] Записът в базата не успе: {ex.Message}");
            }
        });
    }
}
