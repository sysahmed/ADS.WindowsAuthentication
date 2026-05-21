using ADS.WindowsAuth.Core.Services;
using System.IO;

namespace ADS.WindowsAuth.RemoteDesktopHost.Services;

/// <summary>
/// Прост file-based logger за Remote Desktop Host
/// </summary>
public class HostLoggerService : ILoggerService
{
    private readonly string _logPath;

    public HostLoggerService()
    {
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGS");
        if (!Directory.Exists(_logPath))
            Directory.CreateDirectory(_logPath);
    }

    public void LogInfo(string message) => Write("INFO", message);
    public void LogWarning(string message) => Write("WARNING", message);
    public void LogError(string message, Exception? ex = null) => Write("ERROR", ex != null ? $"{message}\n{ex}" : message);

    private void Write(string level, string message)
    {
        try
        {
            var file = Path.Combine(_logPath, $"RDHost_{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}\n");
        }
        catch { }
    }
}
