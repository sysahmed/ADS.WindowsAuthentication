using System.Text;
using System.Text.Json;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;

namespace ADS.WindowsAuth.Client.Services;

/// <summary>
/// Клиент за комуникация с API
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private readonly ILoggerService? _logger;

    /// <summary>
    /// Базов URL на API-то
    /// </summary>
    public string BaseAddress => _apiBaseUrl;

    /// <summary>
    /// Конструктор на ApiClient
    /// </summary>
    /// <param name="apiBaseUrl">Базов URL на API-то</param>
    /// <param name="logger">Logger за логване на грешки</param>
    public ApiClient(string apiBaseUrl = "https://ads-auth.nursanbulgaria.com", ILoggerService? logger = null)
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _logger = logger;
        
        // Създаване на HttpClientHandler с по-либерални SSL настройки за development
        var handler = new HttpClientHandler();
        
        // Ако е localhost или development URL, игнорираме SSL грешки
        if (_apiBaseUrl.Contains("localhost") || _apiBaseUrl.Contains("127.0.0.1") || _apiBaseUrl.StartsWith("http://"))
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }
        
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        _logger?.LogInfo($"ApiClient инициализиран с BaseUrl: {_apiBaseUrl}");
    }

    /// <summary>
    /// Създава нова сесия за аутентикация
    /// </summary>
    /// <param name="username">Опционално потребителско име (ако не е предоставено, използва се текущия потребител)</param>
    /// <param name="domain">Опционален домейн (ако не е предоставен, използва се текущия домейн)</param>
    /// <returns>Създадената сесия</returns>
    public async Task<AuthSession?> CreateSessionAsync(string? username = null, string? domain = null)
    {
        string endpoint = "/api/Auth/session";
        try
        {
            // Ако потребителят и домейнът са предоставени, изпращаме ги
            HttpContent? content = null;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(domain))
            {
                var request = new CreateSessionRequest { Username = username, Domain = domain };
                string json = JsonSerializer.Serialize(request);
                content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                _logger?.LogInfo($"Опит за създаване на сесия с потребител: {username}@{domain}");
            }
            else
            {
                _logger?.LogInfo("Опит за създаване на сесия с автоматична детекция на потребител (няма да се изпраща body)");
            }
            
            _logger?.LogInfo($"Опит за създаване на сесия: {_apiBaseUrl}{endpoint}");
            
            // Retry логика - 3 опита с изчакване
            HttpResponseMessage? response = null;
            int maxRetries = 3;
            int retryDelay = 1000; // 1 секунда
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger?.LogInfo($"[Attempt {attempt}/{maxRetries}] Изпращане на POST заявка към {_apiBaseUrl}{endpoint}...");
                    response = await _httpClient.PostAsync(endpoint, content);
                    _logger?.LogInfo($"[Attempt {attempt}/{maxRetries}] Получен отговор: {(int)response.StatusCode}");
                    break; // Успех - излизаме от цикъла
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    _logger?.LogWarning($"[Attempt {attempt}/{maxRetries}] HttpRequestException: {ex.Message}. Опитвам отново след {retryDelay}ms...");
                    await Task.Delay(retryDelay);
                    retryDelay *= 2; // Увеличаваме забавянето при всеки опит
                }
                catch (TaskCanceledException ex) when (attempt < maxRetries)
                {
                    _logger?.LogWarning($"[Attempt {attempt}/{maxRetries}] TaskCanceledException (таймаут): {ex.Message}. Опитвам отново след {retryDelay}ms...");
                    await Task.Delay(retryDelay);
                    retryDelay *= 2;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger?.LogWarning($"[Attempt {attempt}/{maxRetries}] Exception ({ex.GetType().Name}): {ex.Message}. Опитвам отново след {retryDelay}ms...");
                    await Task.Delay(retryDelay);
                    retryDelay *= 2;
                }
            }
            
            if (response == null)
            {
                _logger?.LogError($"Неуспешни опити за свързване с API след {maxRetries} опита");
                throw new HttpRequestException($"Неуспешни опити за свързване с API след {maxRetries} опита");
            }
            
            _logger?.LogInfo($"Отговор от API: {(int)response.StatusCode} {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                _logger?.LogInfo($"Получен JSON отговор: {json.Substring(0, Math.Min(100, json.Length))}...");
                
                AuthSession? session = JsonSerializer.Deserialize<AuthSession>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (session != null)
                {
                    _logger?.LogInfo($"Сесия създадена успешно: {session.SessionId}");
                }
                else
                {
                    _logger?.LogError("Грешка при десериализация на сесия - резултатът е null");
                }
                
                return session;
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError($"Неуспешен отговор от API: {(int)response.StatusCode} {response.StatusCode}. Съдържание: {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError($"Мрежова грешка при създаване на сесия: {ex.Message}. URL: {_apiBaseUrl}{endpoint}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError($"Таймаут при създаване на сесия. URL: {_apiBaseUrl}{endpoint}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Грешка при създаване на сесия: {ex.Message}. URL: {_apiBaseUrl}{endpoint}", ex);
        }

        return null;
    }

    /// <summary>
    /// Проверява статуса на сесия
    /// </summary>
    /// <param name="sessionId">Идентификатор на сесията</param>
    /// <returns>Статус на сесията</returns>
    public async Task<SessionStatus?> GetSessionStatusAsync(string sessionId)
    {
        string endpoint = $"/api/Auth/session/{sessionId}/status";
        try
        {
            _logger?.LogInfo($"Заявка за статус: {_apiBaseUrl}{endpoint}");
            HttpResponseMessage response = await _httpClient.GetAsync(endpoint);
            
            _logger?.LogInfo($"Отговор от API: {(int)response.StatusCode} {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                _logger?.LogInfo($"Получен JSON отговор: {json}");
                
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (result != null)
                {
                    _logger?.LogInfo($"Десериализиран резултат. Keys: {string.Join(", ", result.Keys)}");
                    
                    if (result.ContainsKey("status"))
                    {
                        string statusString = result["status"].ToString() ?? "";
                        _logger?.LogInfo($"Статус string: '{statusString}'");
                        
                        if (Enum.TryParse<SessionStatus>(statusString, ignoreCase: true, out SessionStatus status))
                        {
                            _logger?.LogInfo($"Успешно парсиран статус: {status}");
                            return status;
                        }
                        else
                        {
                            _logger?.LogError($"Неуспешно парсиране на статус '{statusString}'. Възможни стойности: {string.Join(", ", Enum.GetNames<SessionStatus>())}");
                        }
                    }
                    else
                    {
                        _logger?.LogError($"JSON отговорът няма ключ 'status'. Налични ключове: {string.Join(", ", result.Keys)}");
                    }
                }
                else
                {
                    _logger?.LogError("Десериализацията върна null");
                }
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogWarning($"Неуспешен отговор при проверка на статус: {(int)response.StatusCode} {response.StatusCode}. Съдържание: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Грешка при проверка на статус: {ex.Message}. URL: {_apiBaseUrl}{endpoint}", ex);
        }

        return null;
    }
}

