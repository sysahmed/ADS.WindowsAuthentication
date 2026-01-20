namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Интерфейс за сервис за логване
/// </summary>
public interface ILoggerService
{
    /// <summary>
    /// Логва информационно съобщение
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Логва предупреждение
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Логва грешка
    /// </summary>
    void LogError(string message, Exception? exception = null);
}

