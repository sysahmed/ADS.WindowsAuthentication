using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using ADS.WindowsAuth.Core.Data.Entities;
using ADS.WindowsAuth.API.Services;
using System.Net;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// Контролер за аутентикация
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IWindowsAuthService _windowsAuthService;
    private readonly IDatabaseService? _databaseService;
    private readonly ILoggerService _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly BruteForceProtectionService _bruteForce;

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Проверява дали IP е от вътрешна мрежа (RFC-1918 + loopback)</summary>
    private static bool IsInternalIp(string ip)
    {
        if (ip == "unknown") return false;
        if (ip == "::1" || ip == "127.0.0.1") return true;
        if (!System.Net.IPAddress.TryParse(ip, out var addr)) return false;
        var bytes = addr.GetAddressBytes();
        if (bytes.Length == 4) // IPv4
        {
            return bytes[0] == 10 ||                          // 10.0.0.0/8
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.0.0/12
                   (bytes[0] == 192 && bytes[1] == 168);      // 192.168.0.0/16
        }
        return false; // IPv6 — считаме за external
    }

    /// <summary>
    /// Конструктор на AuthController
    /// </summary>
    public AuthController(
        ISessionService sessionService,
        IWindowsAuthService windowsAuthService,
        IDatabaseService? databaseService,
        ILoggerService logger,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        BruteForceProtectionService bruteForce)
    {
        _sessionService = sessionService;
        _windowsAuthService = windowsAuthService;
        _databaseService = databaseService; // Може да е null ако базата данни не е достъпна
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _bruteForce = bruteForce;
    }

    /// <summary>
    /// Създава нова сесия за аутентикация
    /// </summary>
    /// <param name="request">Опционална заявка с потребител и домейн (ако не е предоставена, използва се текущия потребител)</param>
    /// <returns>Създадената сесия</returns>
    [HttpPost("session")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest? request = null)
    {
        try
        {
            _logger.LogInfo("API: Започва създаване на сесия...");
            
            AuthSession session;
            
            // Ако клиентът е предоставил потребител и домейн, използваме ги
            if (request != null && !string.IsNullOrEmpty(request.Username) && !string.IsNullOrEmpty(request.Domain))
            {
                _logger.LogInfo($"API: Създаване на сесия с предоставен потребител: {request.Username}@{request.Domain}");
                session = _sessionService.CreateSession(request.Username, request.Domain);
            }
            else
            {
                // Иначе използваме текущия потребител (може да е IIS APPPOOL)
                _logger.LogInfo("API: Създаване на сесия с текущ потребител (от сървъра)");
                session = _sessionService.CreateSession();
            }
            
            _logger.LogInfo($"API: Сесията е създадена успешно: {session.SessionId}");

            // ── IP Binding: записваме IP-а на машината, създала сесията ──────
            // Само тя (или вътрешна мрежа) ще може да получи паролата след одобрение
            session.MobileDeviceIp = GetClientIp();
            
            // Записване в базата данни (fire-and-forget с timeout - не блокира заявката)
            // Използваме IServiceScopeFactory за да създадем нов scope в Task.Run
                    _logger.LogInfo("API: Опит за запис на сесия в базата данни...");
                    var sessionEntity = new AuthSessionEntity
                    {
                        SessionId = session.SessionId,
                        AccessToken = session.AccessToken,
                        WindowsUsername = session.WindowsUsername,
                        Domain = session.Domain,
                        MachineName = session.MachineName,
                        Status = session.Status.ToString(),
                        CreatedAt = session.CreatedAt,
                        ExpiresAt = session.ExpiresAt
                    };
            
            // Fire-and-forget с timeout - създаваме нов scope за да не използваме disposed DbContext
            _ = Task.Run(async () =>
            {
                // Създаваме нов scope за да не използваме disposed DbContext
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    try
                    {
                        var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                        if (databaseService == null)
                        {
                            _logger.LogWarning("API: DatabaseService не е наличен в новия scope");
                            return;
                        }

                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        {
                            var saveTask = databaseService.SaveOrUpdateAuthSessionAsync(sessionEntity);
                            var timeoutTask = Task.Delay(5000, cts.Token);
                            
                            var completedTask = await Task.WhenAny(saveTask, timeoutTask);
                            if (completedTask == saveTask)
                            {
                                await saveTask;
                    _logger.LogInfo($"API: Сесията е записана в базата данни: {session.SessionId}");
                            }
                            else
                            {
                                _logger.LogWarning($"API: Timeout при запис на сесия в базата данни: {session.SessionId}");
                            }
                        }
                }
                catch (Exception dbEx)
                {
                    // Логваме грешката но не спираме заявката
                    _logger.LogError($"API: Грешка при запис на сесия в базата данни (сесията все пак е създадена): {dbEx.Message}", dbEx);
                }
            }
            });
            
            _logger.LogInfo($"API: Връщане на успешен резултат за сесия {session.SessionId} за {session.WindowsUsername}@{session.Domain}");
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при създаване на сесия: {ex.Message}", ex);
            _logger.LogError($"API: Stack trace: {ex.StackTrace}");
            _logger.LogError($"API: Inner exception: {ex.InnerException?.Message}");
            return StatusCode(500, new { message = $"Грешка при създаване на сесия: {ex.Message}" });
        }
    }

    /// <summary>
    /// Получава статус на сесия (SessionId или AccessToken). Връща винаги HTTP 200 с JSON status,
    /// за да работи Credential Provider (при 404/500 получава празен отговор и остава в Pending).
    /// </summary>
    [HttpGet("session/{sessionId}/status")]
    public IActionResult GetSessionStatus(string sessionId)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("API: GetSessionStatus called with empty sessionId");
                return Ok(new { status = "Expired" });
            }

            AuthSession? session = null;

            try
            {
                session = _sessionService.GetSessionById(sessionId);
            }
            catch (Exception ex1)
            {
                _logger.LogWarning($"API: GetSessionById error for {sessionId}: {ex1.Message}");
            }

            if (session == null)
            {
                try
                {
                    session = _sessionService.GetSessionByToken(sessionId);
                }
                catch (Exception ex2)
                {
                    _logger.LogWarning($"API: GetSessionByToken error for {sessionId}: {ex2.Message}");
                }
            }

            if (session == null)
            {
                string key = sessionId.Length > 8 ? sessionId.Substring(0, 8) : sessionId;
                _logger.LogWarning($"API: Session not found: {key}... (returning Expired for Credential Provider)");
                return Ok(new { status = "Expired" });
            }

            if (session.Status == SessionStatus.Approved)
            {
                string username = session.WindowsUsername ?? string.Empty;
                string domain = _configuration["ActiveDirectory:DomainName"] ?? string.Empty;
                if (!string.IsNullOrEmpty(session.ApprovedBy) && session.ApprovedBy.Contains('@'))
                {
                    var parts = session.ApprovedBy.Split('@');
                    if (parts.Length == 2)
                    {
                        username = parts[0];
                        domain = parts[1];
                    }
                }

                // ── SECURITY: Паролата се връща САМО на машината, която е създала сесията ──
                // Credential Provider polling-ва с IP-а на машината (MobileDeviceIp = IP при QR-сканиране)
                // За обратна съвместимост с Credential Provider, проверяваме дали заявката идва от
                // записания IP или от вътрешна мрежа (192.168.x.x / 10.x.x.x / 172.16-31.x.x)
                string clientIp = GetClientIp();
                bool isInternalNetwork = IsInternalIp(clientIp);
                bool isSessionCreator = !string.IsNullOrEmpty(session.MobileDeviceIp) &&
                                        session.MobileDeviceIp == clientIp;

                string? passwordToReturn = null;
                if ((isSessionCreator || isInternalNetwork) && !session.IsPasswordExpired)
                {
                    passwordToReturn = session.ApprovedPassword;
                    // Изтриваме паролата след еднократно предаване
                    session.ClearPassword();
                }
                else if (session.IsPasswordExpired)
                {
                    session.ClearPassword();
                }

                return Ok(new
                {
                    status = session.Status.ToString(),
                    username = username,
                    domain = domain,
                    password = passwordToReturn ?? string.Empty,
                    sessionId = session.SessionId
                });
            }

            return Ok(new { status = session.Status.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: GetSessionStatus exception for {sessionId}: {ex.Message}", ex);
            if (ex.InnerException != null)
                _logger.LogError($"API: Inner: {ex.InnerException.Message}");
            return Ok(new { status = "Expired" });
        }
    }

    /// <summary>
    /// Аутентикира потребител с домейн credentials
    /// </summary>
    /// <param name="request">Заявка за аутентикация</param>
    /// <returns>Резултат от аутентикацията</returns>
    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] AuthRequest request)
    {
        try
        {
            // ── Brute Force Protection ──────────────────────────────────────────
            string clientIp = GetClientIp();
            if (_bruteForce.IsBlocked(clientIp))
            {
                _logger.LogWarning($"SECURITY: Блокиран IP {clientIp} се опитва да аутентикира");
                return StatusCode(403, new { message = "Вашият IP адрес е блокиран поради многобройни неуспешни опити. Свържете се с администратора." });
            }

            if (string.IsNullOrEmpty(request.AccessToken))
            {
                return BadRequest(new { message = "AccessToken е задължителен" });
            }

            AuthSession? session = _sessionService.GetSessionByToken(request.AccessToken);
            
            if (session == null)
            {
                return NotFound(new { message = "Сесията не е намерена" });
            }

            if (session.Status != SessionStatus.Pending)
            {
                return BadRequest(new { message = $"Сесията вече е обработена. Статус: {session.Status}" });
            }

            if (session.ExpiresAt < DateTime.Now)
            {
                session.Status = SessionStatus.Expired;
                
                // Записване в базата данни
                if (_databaseService != null)
                {
                var sessionEntity = new AuthSessionEntity
                {
                    SessionId = session.SessionId,
                    AccessToken = session.AccessToken,
                    WindowsUsername = session.WindowsUsername,
                    Domain = session.Domain,
                    MachineName = session.MachineName,
                    Status = SessionStatus.Expired.ToString(),
                    CreatedAt = session.CreatedAt,
                    ExpiresAt = session.ExpiresAt
                };
                await _databaseService.SaveOrUpdateAuthSessionAsync(sessionEntity);
                }
                
                return BadRequest(new { message = "Сесията е изтекла" });
            }

            // Валидация на credentials срещу домейна
            bool isValid = _windowsAuthService.ValidateCredentials(
                request.Username,
                request.Password,
                request.Domain);

            if (isValid)
            {
                // Ако сесията е създадена от IIS APPPOOL, приемаме всеки валиден потребител
                bool isIISAppPool = session.WindowsUsername.Contains("IIS APPPOOL", StringComparison.OrdinalIgnoreCase) || 
                                   session.WindowsUsername.Contains("DefaultAppPool", StringComparison.OrdinalIgnoreCase);
                
                _logger.LogInfo($"API: Проверка за IIS APPPOOL - WindowsUsername: '{session.WindowsUsername}', isIISAppPool: {isIISAppPool}");
                
                // Проверка дали потребителят отговаря на сесията (само ако не е IIS APPPOOL)
                bool userMatches = session.WindowsUsername.Equals(request.Username, StringComparison.OrdinalIgnoreCase) &&
                                  session.Domain.Equals(request.Domain, StringComparison.OrdinalIgnoreCase);
                
                _logger.LogInfo($"API: Потребителят съвпада: {userMatches} (Session: {session.WindowsUsername}@{session.Domain}, Input: {request.Username}@{request.Domain})");
                
                if (isIISAppPool || userMatches)
                {
                    _logger.LogInfo($"API: Условието за одобрение е изпълнено (isIISAppPool: {isIISAppPool}, userMatches: {userMatches})");
                    // Одобряваме сесията И записваме паролата за автоматичен login
                    bool approved = _sessionService.ApproveSession(session.SessionId, request.Username, request.Password, request.Domain);

                    if (approved)
                    {
                        // Записване в базата данни
                        if (_databaseService != null)
                        {
                        var sessionEntity = new AuthSessionEntity
                        {
                            SessionId = session.SessionId,
                            AccessToken = session.AccessToken,
                            WindowsUsername = request.Username, // Използваме реалния потребител
                            Domain = request.Domain,
                            MachineName = session.MachineName,
                            Status = SessionStatus.Approved.ToString(),
                            CreatedAt = session.CreatedAt,
                            ExpiresAt = session.ExpiresAt,
                            ApprovedAt = DateTime.Now
                        };
                        await _databaseService.SaveOrUpdateAuthSessionAsync(sessionEntity);
                        }
                        
                        _bruteForce.ClearAttempts(clientIp);
                        _logger.LogInfo($"API: Успешна аутентикация за сесия {session.SessionId} - User: {request.Username}@{request.Domain}");
                        return Ok(new AuthResponse
                        {
                            Success = true,
                            Message = "Аутентикацията е успешна",
                            MachineName = session.MachineName,
                            Username = request.Username // Използваме реалния потребител
                        });
                    }
                }
                else
                {
                    _logger.LogWarning($"API: Опит за аутентикация с различен потребител. Очакван: {session.WindowsUsername}@{session.Domain}, Получен: {request.Username}@{request.Domain}");
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Потребителят не отговаря на сесията"
                    });
                }
            }

            await _bruteForce.RegisterFailedAttemptAsync(clientIp);
            _sessionService.RejectSession(session.SessionId);

            // Записване в базата данни
            if (_databaseService != null)
            {
            var rejectedSessionEntity = new AuthSessionEntity
            {
                SessionId = session.SessionId,
                AccessToken = session.AccessToken,
                WindowsUsername = session.WindowsUsername,
                Domain = session.Domain,
                MachineName = session.MachineName,
                Status = SessionStatus.Rejected.ToString(),
                CreatedAt = session.CreatedAt,
                ExpiresAt = session.ExpiresAt,
                RejectedAt = DateTime.Now
            };
            await _databaseService.SaveOrUpdateAuthSessionAsync(rejectedSessionEntity);
            }
            
            _logger.LogWarning($"API: Неуспешна аутентикация за сесия {session.SessionId}");
            
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Невалидни credentials"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("API: Грешка при аутентикация", ex);
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "Грешка при аутентикация"
            });
        }
    }

    /// <summary>
    /// Получава информация за сесия по токен
    /// </summary>
    /// <param name="token">Токен за достъп</param>
    /// <returns>Информация за сесията</returns>
    [HttpGet("session/token/{token}")]
    public IActionResult GetSessionByToken(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { message = "Токенът е задължителен" });
            }

            AuthSession? session = _sessionService.GetSessionByToken(token);
            
            if (session == null)
            {
                _logger.LogWarning($"API: Сесия не е намерена за токен: {token.Substring(0, Math.Min(8, token.Length))}...");
                return NotFound(new { 
                    message = "Сесията не е намерена или е изтекла",
                    tokenPreview = token.Substring(0, Math.Min(8, token.Length)) + "..."
                });
            }

            // Домейнът ВИНАГИ идва от appsettings (ActiveDirectory:DomainName), не от сесията
            // Сесията може да съдържа machine name ("AHMEDITDESK") ако е създадена от Credential Provider
            string username = session.WindowsUsername ?? string.Empty;
            string configuredDomain = _configuration["ActiveDirectory:DomainName"] ?? string.Empty;
            string domain = !string.IsNullOrWhiteSpace(configuredDomain)
                ? configuredDomain
                : (session.Domain ?? Environment.MachineName);

            _logger.LogInfo($"API: GetSessionByToken - configuredDomain='{configuredDomain}', session.Domain='{session.Domain}', returning domain='{domain}'");
            
            // Ако username съдържа @ символ, опитваме се да го разделим
            if (username.Contains('@') && string.IsNullOrWhiteSpace(domain))
            {
                var parts = username.Split('@');
                if (parts.Length == 2)
                {
                    username = parts[0];
                    domain = parts[1];
                    _logger.LogInfo($"API: Разделен username с @ символ: {username}@{domain}");
                }
            }
            
            _logger.LogInfo($"API: Връщане на данни за сесия {session.SessionId}: username={username}, domain={domain}");

            // Не връщаме чувствителна информация
            return Ok(new
            {
                sessionId = session.SessionId,
                machineName = session.MachineName,
                username = username,
                domain = domain,
                status = session.Status.ToString(),
                expiresAt = session.ExpiresAt,
                expiresInSeconds = (int)(session.ExpiresAt - DateTime.Now).TotalSeconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"API: Грешка при получаване на сесия по токен: {token?.Substring(0, Math.Min(8, token?.Length ?? 0))}...", ex);
            return StatusCode(500, new { message = "Грешка при получаване на сесия" });
        }
    }

    /// <summary>
    /// Получава всички активни сесии (само за Admin)
    /// </summary>
    [HttpGet("sessions/debug")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAllSessions()
    {
        try
        {
            var sessions = _sessionService.GetAllSessions();
            return Ok(new
            {
                count = sessions.Count(),
                sessions = sessions.Select(s => new
                {
                    sessionId = s.SessionId,
                    machineName = s.MachineName,
                    username = s.WindowsUsername,
                    domain = s.Domain,
                    status = s.Status.ToString(),
                    createdAt = s.CreatedAt,
                    expiresAt = s.ExpiresAt,
                    expiresInSeconds = (int)(s.ExpiresAt - DateTime.Now).TotalSeconds,
                    tokenPreview = s.AccessToken.Substring(0, Math.Min(8, s.AccessToken.Length)) + "..."
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("API: Грешка при получаване на всички сесии", ex);
            return StatusCode(500, new { message = "Грешка при получаване на сесии" });
        }
    }
}

