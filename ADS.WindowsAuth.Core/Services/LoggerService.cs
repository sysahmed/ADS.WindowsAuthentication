using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за логване във файлове
/// </summary>
public class LoggerService : ILoggerService
{
    private readonly string _logsDirectory;
    private readonly HttpClient? _httpClient;
    private readonly string? _apiUrl;
    private readonly string _machineName;
    private readonly string _username;
    private readonly string _domain;

    /// <summary>
    /// Конструктор на LoggerService
    /// </summary>
    /// <param name="applicationDirectory">Директория на приложението</param>
    /// <param name="apiUrl">Опционален API URL за изпращане на логове</param>
    public LoggerService(string applicationDirectory, string? apiUrl = null)
    {
        _logsDirectory = Path.Combine(applicationDirectory, "LOGS");
        
        if (!Directory.Exists(_logsDirectory))
        {
            Directory.CreateDirectory(_logsDirectory);
        }

        _machineName = Environment.MachineName;
        _username = Environment.UserName;
        _domain = Environment.UserDomainName;

        // Ако е предоставен API URL, създаваме HttpClient
        if (!string.IsNullOrEmpty(apiUrl))
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10), // Увеличен timeout
                BaseAddress = new Uri(_apiUrl)
            };
            
            // Логване на конфигурацията (само веднъж)
            try
            {
                string configLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] LoggerService инициализиран с API URL: {_apiUrl}";
                string configFilePath = Path.Combine(_logsDirectory, $"CONFIG_{DateTime.Now:yyyyMMdd}.LOG");
                File.AppendAllText(configFilePath, configLog + Environment.NewLine);
            }
            catch { }
        }
        else
        {
            // Логване че API URL не е конфигуриран
            try
            {
                string warning = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARNING] LoggerService инициализиран БЕЗ API URL. Логовете няма да се изпращат към API.";
                string warningFilePath = Path.Combine(_logsDirectory, $"CONFIG_{DateTime.Now:yyyyMMdd}.LOG");
                File.AppendAllText(warningFilePath, warning + Environment.NewLine);
            }
            catch { }
        }
    }

    /// <summary>
    /// Логва информационно съобщение
    /// </summary>
    public void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    /// <summary>
    /// Логва предупреждение
    /// </summary>
    public void LogWarning(string message)
    {
        WriteLog("WARNING", message);
    }

    /// <summary>
    /// Логва грешка
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        string errorMessage = message;
        if (exception != null)
        {
            errorMessage += $"\nException: {exception.GetType().Name}\nMessage: {exception.Message}\nStack Trace: {exception.StackTrace}";
        }
        WriteLog("ERROR", errorMessage, exception);
    }

    private void WriteLog(string level, string message, Exception? exception = null)
    {
        try
        {
            string fileName = $"NURSAN{DateTime.Now:yyyyMMdd}.LOG";
            string filePath = Path.Combine(_logsDirectory, fileName);
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            File.AppendAllText(filePath, logEntry + Environment.NewLine);
        }
        catch
        {
            // Ако не можем да логнем, не правим нищо
        }

        // Изпращане към API (асинхронно, без да блокира)
        if (_httpClient != null && !string.IsNullOrEmpty(_apiUrl))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var logRequest = new
                    {
                        MachineName = _machineName,
                        Username = _username,
                        Domain = _domain,
                        Level = level,
                        Message = message,
                        Timestamp = DateTime.Now,
                        Source = "MonitorService", // Променено от LoggerService на MonitorService
                        ExceptionType = exception?.GetType().Name,
                        StackTrace = exception?.StackTrace
                    };

                    var url = "/api/logs/upload"; // Използваме относителен път, защото BaseAddress е зададен
                    var response = await _httpClient.PostAsJsonAsync(url, logRequest);
                    
                    // Логваме резултата в локален файл за debugging
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        string errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Failed to send log to API. Status: {response.StatusCode}, Response: {errorContent}";
                        try
                        {
                            string errorFilePath = Path.Combine(_logsDirectory, $"API_ERROR_{DateTime.Now:yyyyMMdd}.LOG");
                            File.AppendAllText(errorFilePath, errorLog + Environment.NewLine);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    // Логваме грешката в локален файл за debugging
                    try
                    {
                        string errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] Exception sending log to API: {ex.GetType().Name} - {ex.Message}";
                        string errorFilePath = Path.Combine(_logsDirectory, $"API_ERROR_{DateTime.Now:yyyyMMdd}.LOG");
                        File.AppendAllText(errorFilePath, errorLog + Environment.NewLine);
                    }
                    catch { }
                }
            });
        }
        else
        {
            // Логваме че API URL не е конфигуриран (само веднъж на ден)
            try
            {
                string checkFile = Path.Combine(_logsDirectory, $"API_CHECK_{DateTime.Now:yyyyMMdd}.LOG");
                if (!File.Exists(checkFile))
                {
                    string warning = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARNING] API URL не е конфигуриран. Логовете няма да се изпращат към API. ApiUrl: {_apiUrl ?? "NULL"}, HttpClient: {(_httpClient != null ? "OK" : "NULL")}";
                    File.AppendAllText(checkFile, warning + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Освобождаване на ресурси
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

