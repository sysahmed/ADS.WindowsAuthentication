using System.Collections.Concurrent;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using System.Threading.Tasks;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// Реализация на сервис за управление на сесии
/// </summary>
public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();
    private readonly ILoggerService _logger;
    private readonly IWindowsAuthService _windowsAuthService;
    // Rate limiting за логването на не-намерени сесии (да не логваме всеки път)
    private readonly ConcurrentDictionary<string, DateTime> _lastNotFoundLog = new();

    /// <summary>
    /// Конструктор на SessionService
    /// </summary>
    public SessionService(ILoggerService logger, IWindowsAuthService windowsAuthService)
    {
        _logger = logger;
        _windowsAuthService = windowsAuthService;
    }

    /// <summary>
    /// Създава нова сесия за аутентикация
    /// </summary>
    public AuthSession CreateSession()
    {
        (string username, string domain) = _windowsAuthService.GetCurrentWindowsUser();
        return CreateSession(username, domain);
    }

    /// <summary>
    /// Създава нова сесия за аутентикация с указан потребител и домейн
    /// </summary>
    public AuthSession CreateSession(string username, string domain)
    {
        string sessionId = Guid.NewGuid().ToString();
        string accessToken = Guid.NewGuid().ToString("N");
        
        AuthSession session = new AuthSession
        {
            SessionId = sessionId,
            AccessToken = accessToken,
            MachineName = Environment.MachineName,
            WindowsUsername = username,
            Domain = domain,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddMinutes(30), // Увеличено на 30 минути за тестване
            Status = SessionStatus.Pending
        };

        _sessions.TryAdd(sessionId, session);
        
        _logger.LogInfo($"Създадена нова сесия: {sessionId} за потребител {username}@{domain} на машина {Environment.MachineName}");
        _logger.LogInfo($"Сесията изтича на: {session.ExpiresAt:yyyy-MM-dd HH:mm:ss} (след {(session.ExpiresAt - DateTime.Now).TotalMinutes:F0} минути)");
        
        return session;
    }

    /// <summary>
    /// Създава нова сесия за аутентикация с пълни параметри (async версия)
    /// </summary>
    public async Task<AuthSession> CreateSessionAsync(string username, string domain, string clientType, string clientIp)
    {
        return await Task.Run(() => CreateSession(username, domain));
    }

    /// <summary>
    /// Получава сесия по идентификатор
    /// </summary>
    public AuthSession? GetSessionById(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogWarning("Опит за достъп с празен sessionId");
            return null;
        }

        if (_sessions.TryGetValue(sessionId, out AuthSession? session))
        {
            if (session.ExpiresAt < DateTime.Now)
            {
                session.Status = SessionStatus.Expired;
                _logger.LogWarning($"Сесия {sessionId} е изтекла.");
                return null;
            }
            
            return session;
        }

        _logger.LogWarning($"Сесия не е намерена за sessionId: {sessionId}");
        return null;
    }

    /// <summary>
    /// Получава сесия по токен
    /// </summary>
    public AuthSession? GetSessionByToken(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Опит за достъп с празен токен");
            return null;
        }

        AuthSession? session = _sessions.Values.FirstOrDefault(s => s.AccessToken == accessToken);
        
        if (session == null)
        {
            // Rate limiting: Логваме само веднъж на всеки 30 секунди за същия токен
            string tokenKey = accessToken.Substring(0, Math.Min(8, accessToken.Length));
            bool shouldLog = true;
            
            if (_lastNotFoundLog.TryGetValue(tokenKey, out DateTime lastLog))
            {
                if ((DateTime.Now - lastLog).TotalSeconds < 30)
                {
                    shouldLog = false; // Не логваме ако сме логвали преди по-малко от 30 секунди
                }
            }
            
            if (shouldLog)
            {
                _lastNotFoundLog.AddOrUpdate(tokenKey, DateTime.Now, (key, oldValue) => DateTime.Now);
                
                var activeSessions = _sessions.Values.Where(s => s.ExpiresAt > DateTime.Now).ToList();
                var expiredSessions = _sessions.Values.Where(s => s.ExpiresAt <= DateTime.Now).ToList();
                
                _logger.LogWarning($"Сесия не е намерена за токен: {tokenKey}... " +
                                  $"(Общо сесии: {_sessions.Count}, Активни: {activeSessions.Count}, Изтекла: {expiredSessions.Count})");
                
                // Логване на всички активни сесии за debugging (само при първото логване)
                if (activeSessions.Any())
                {
                    _logger.LogInfo($"Активни сесии: {string.Join(", ", activeSessions.Select(s => $"{s.SessionId.Substring(0, 8)}... (expires: {s.ExpiresAt:HH:mm:ss})"))}");
                }
            }
            
            return null;
        }
        
        if (session.ExpiresAt < DateTime.Now)
        {
            session.Status = SessionStatus.Expired;
            _logger.LogWarning($"Сесия {session.SessionId} е изтекла. Създадена: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}, Изтекла: {session.ExpiresAt:yyyy-MM-dd HH:mm:ss}, Сега: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            return null; // Връщаме null за изтекла сесия
        }
        
        _logger.LogInfo($"Сесия {session.SessionId} намерена успешно. Статус: {session.Status}, Остава време: {(session.ExpiresAt - DateTime.Now).TotalSeconds:F0} секунди");
        return session;
    }

    /// <summary>
    /// Одобрява сесия
    /// </summary>
    public bool ApproveSession(string sessionId, string? approvedUsername = null, string? approvedPassword = null, string? approvedDomain = null)
    {
        if (_sessions.TryGetValue(sessionId, out AuthSession? session))
        {
            if (session.Status == SessionStatus.Pending && session.ExpiresAt > DateTime.Now)
            {
                session.Status = SessionStatus.Approved;
                session.ApprovedAt = DateTime.Now;

                // Записваме паролата и одобрилия потребител ако са предоставени
                if (!string.IsNullOrEmpty(approvedPassword))
                {
                    session.ApprovedPassword = approvedPassword;
                    _logger.LogInfo($"Сесия {sessionId} е одобрена с парола (дължина: {approvedPassword.Length})");
                }

                if (!string.IsNullOrEmpty(approvedUsername))
                {
                    string domain = approvedDomain ?? "nursan";
                    session.ApprovedBy = $"{approvedUsername}@{domain}";
                    _logger.LogInfo($"Сесия {sessionId} е одобрена от {session.ApprovedBy}");
                }
                else
                {
                    _logger.LogInfo($"Сесия {sessionId} е одобрена");
                }

                return true;
            }
            else
            {
                _logger.LogWarning($"Сесия {sessionId} не може да бъде одобрена - Status: {session.Status}, Изтекла: {session.ExpiresAt < DateTime.Now}");
            }
        }
        else
        {
            _logger.LogWarning($"Сесия {sessionId} не е намерена за одобрение");
        }

        return false;
    }

    /// <summary>
    /// Отхвърля сесия
    /// </summary>
    public bool RejectSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out AuthSession? session))
        {
            session.Status = SessionStatus.Rejected;
            _logger.LogInfo($"Сесия {sessionId} е отхвърлена");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Премахва изтекла сесия
    /// </summary>
    public void CleanupExpiredSessions()
    {
        // Очистваме изтекли сесии
        var expiredSessions = _sessions.Values
            .Where(s => s.ExpiresAt < DateTime.Now)
            .Select(s => s.SessionId)
            .ToList();

        foreach (string sessionId in expiredSessions)
        {
            if (_sessions.TryRemove(sessionId, out var session))
            {
                // Очистваме паролата при табни исход (за security)
                session?.ClearPassword();
            }
        }

        // Очистваме изтекли пароли
        var expiredPasswords = _sessions.Values
            .Where(s => s.IsPasswordExpired)
            .ToList();

        foreach (var session in expiredPasswords)
        {
            session.ClearPassword();
        }

        if (expiredSessions.Count > 0 || expiredPasswords.Count > 0)
        {
            if (expiredSessions.Count > 0)
                _logger.LogInfo($"Премахнати {expiredSessions.Count} изтекла сесии");
            if (expiredPasswords.Count > 0)
                _logger.LogInfo($"Очистени {expiredPasswords.Count} изтекли пароли");
        }
    }

    /// <summary>
    /// Получава всички активни сесии (за debugging)
    /// </summary>
    public IEnumerable<AuthSession> GetAllSessions()
    {
        return _sessions.Values.Where(s => s.ExpiresAt > DateTime.Now);
    }
    
    /// <summary>
    /// Зарежда активни сесии от базата данни (при стартиране)
    /// </summary>
    public async Task LoadSessionsFromDatabaseAsync(IDatabaseService? databaseService)
    {
        if (databaseService == null)
        {
            _logger.LogInfo("DatabaseService не е наличен - пропускаме зареждане на сесии от базата данни");
            return;
        }
        
        try
        {
            _logger.LogInfo("Започва зареждане на активни сесии от базата данни...");
            
            var activeSessionEntities = await databaseService.GetActiveAuthSessionsAsync();
            
            if (activeSessionEntities == null || activeSessionEntities.Count == 0)
            {
                _logger.LogInfo("Няма активни сесии в базата данни");
                return;
            }
            
            int loadedCount = 0;
            foreach (var entity in activeSessionEntities)
            {
                try
                {
                    // Преобразуваме entity към domain model
                    var session = new AuthSession
                    {
                        SessionId = entity.SessionId,
                        AccessToken = entity.AccessToken,
                        WindowsUsername = entity.WindowsUsername,
                        Domain = entity.Domain,
                        MachineName = entity.MachineName,
                        Status = Enum.Parse<SessionStatus>(entity.Status),
                        CreatedAt = entity.CreatedAt,
                        ExpiresAt = entity.ExpiresAt,
                        ApprovedAt = entity.ApprovedAt,
                        ApprovedBy = entity.ApprovedBy
                    };
                    
                    // Зареждаме только активни сесии (които не са изтекли)
                    if (session.ExpiresAt > DateTime.Now)
                    {
                        _sessions.TryAdd(session.SessionId, session);
                        loadedCount++;
                        _logger.LogInfo($"Заредена сесия от базата данни: {session.SessionId} (статус: {session.Status})");
                    }
                    else
                    {
                        _logger.LogInfo($"Пропусната изтекла сесия: {session.SessionId}");
                    }
                }
                catch (Exception exEntity)
                {
                    _logger.LogWarning($"Грешка при зареждане на сесия {entity.SessionId}: {exEntity.Message}");
                }
            }
            
            _logger.LogInfo($"Успешно заредени {loadedCount} активни сесии от базата данни");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при зареждане на сесии от базата данни: {ex.Message}", ex);
        }
    }
}

