using ADS.WindowsAuth.API.Services;
using ADS.WindowsAuth.Core.Models;
using ADS.WindowsAuth.Core.Services;
using Microsoft.AspNetCore.Mvc;
using CoreUserInfo = ADS.WindowsAuth.Core.Services.UserInfo;

namespace ADS.WindowsAuth.API.Controllers;

/// <summary>
/// MVC контролер за WebAuthn/FIDO2 биометрична аутентикация
/// </summary>
public class WebAuthnController : Controller
{
    private readonly WebAuthnService _webAuthn;
    private readonly IAdService _adService;
    private readonly IJwtService _jwtService;
    private readonly ILogger<WebAuthnController> _logger;

    public WebAuthnController(
        WebAuthnService webAuthn,
        IAdService adService,
        IJwtService jwtService,
        ILogger<WebAuthnController> logger)
    {
        _webAuthn = webAuthn;
        _adService = adService;
        _jwtService = jwtService;
        _logger = logger;
    }

    // =============================================
    // VIEWS
    // =============================================

    /// <summary>Страница за регистрация на биометрично устройство</summary>
    public IActionResult Register() => View();

    /// <summary>Страница за вход с биометрия</summary>
    public IActionResult Login() => View();

    /// <summary>Управление на регистрирани устройства</summary>
    public IActionResult Manage()
    {
        var username = User.Identity?.Name ?? HttpContext.Session.GetString("webauthn_user") ?? "";
        var credentials = _webAuthn.GetCredentialsForUser(username);
        return View(credentials);
    }

    // =============================================
    // REGISTRATION API
    // =============================================

    /// <summary>
    /// Стъпка 1: Генерира опции за регистрация
    /// </summary>
    [HttpPost("api/webauthn/register/begin")]
    public IActionResult BeginRegistration([FromBody] BeginRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { error = "Username е задължителен" });

        // Проверка дали потребителят съществува в AD
        if (_adService.IsEnabled && !_adService.UserExists(request.Username))
            return BadRequest(new { error = $"Потребителят '{request.Username}' не съществува в домейна" });

        var displayName = request.DisplayName ?? request.Username;

        // Ако има AD, вземаме DisplayName
        if (_adService.IsEnabled)
        {
            var adUser = _adService.GetUserInfo(request.Username);
            if (adUser != null && !string.IsNullOrEmpty(adUser.DisplayName))
                displayName = adUser.DisplayName;
        }

        var options = _webAuthn.BeginRegistration(request.Username, displayName);
        return Ok(options);
    }

    /// <summary>
    /// Стъпка 2: Завършва регистрацията и запазва credential
    /// </summary>
    [HttpPost("api/webauthn/register/complete")]
    public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationRequest request)
    {
        var (success, error) = await _webAuthn.CompleteRegistrationAsync(
            request.Username,
            request.CredentialId,
            request.AttestationObject,
            request.ClientDataJson,
            request.DeviceDescription);

        if (!success)
        {
            _logger.LogWarning("FIDO2 регистрация неуспешна за {Username}: {Error}", request.Username, error);
            return BadRequest(new { error });
        }

        return Ok(new { message = "Биометричното устройство е регистрирано успешно!" });
    }

    // =============================================
    // AUTHENTICATION API
    // =============================================

    /// <summary>
    /// Стъпка 1: Генерира challenge за аутентикация
    /// </summary>
    [HttpPost("api/webauthn/authenticate/begin")]
    public IActionResult BeginAuthentication([FromBody] BeginAuthRequest request)
    {
        var options = _webAuthn.BeginAuthentication(request.Username);

        // Запазваме challengeKey в сесията за верификация
        HttpContext.Session.SetString("webauthn_challenge_key", options.ChallengeKey);

        return Ok(options);
    }

    /// <summary>
    /// Стъпка 2: Верифицира assertion и издава JWT
    /// </summary>
    [HttpPost("api/webauthn/authenticate/complete")]
    public async Task<IActionResult> CompleteAuthentication([FromBody] CompleteAuthRequest request)
    {
        // Вземаме challengeKey от сесията
        var challengeKey = HttpContext.Session.GetString("webauthn_challenge_key") ?? request.ChallengeKey;

        if (string.IsNullOrEmpty(challengeKey))
            return BadRequest(new { error = "Няма активна аутентикационна сесия" });

        var (success, username, error) = await _webAuthn.CompleteAuthenticationAsync(
            challengeKey,
            request.CredentialId,
            request.AuthenticatorData,
            request.ClientDataJson,
            request.Signature);

        if (!success)
        {
            _logger.LogWarning("FIDO2 аутентикация неуспешна: {Error}", error);
            return Unauthorized(new { error });
        }

        // Проверка в AD дали потребителят е активен
        if (_adService.IsEnabled)
        {
            if (!_adService.UserExists(username))
                return Unauthorized(new { error = "Потребителят не съществува в домейна" });

            var adUser = _adService.GetUserInfo(username);
            if (adUser != null && !adUser.IsEnabled)
                return Unauthorized(new { error = "Потребителският акаунт е деактивиран в AD" });
        }

        // Запазваме username в сесията
        HttpContext.Session.SetString("webauthn_user", username);
        HttpContext.Session.Remove("webauthn_challenge_key");

        // Издаваме JWT токен
        var groups = _adService.IsEnabled ? _adService.GetUserGroups(username) : new List<string>();
        var userInfo = new CoreUserInfo
        {
            Username = username,
            Domain = string.Empty,
            MachineName = Environment.MachineName,
            Roles = groups.ToArray()
        };
        var token = _jwtService.GenerateToken(userInfo, new AuthSession
        {
            WindowsUsername = username,
            Domain = string.Empty,
            MachineName = Environment.MachineName,
            SessionId = Guid.NewGuid().ToString(),
            AccessToken = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddHours(8),
            Status = SessionStatus.Approved
        });

        _logger.LogInformation("Успешна биометрична аутентикация за {Username}", username);

        return Ok(new
        {
            message = "Аутентикацията е успешна!",
            username,
            token,
            redirectUrl = "/"
        });
    }

    // =============================================
    // AD USERS MANAGEMENT
    // =============================================

    /// <summary>
    /// Списък на потребителите от AD домейна
    /// </summary>
    [HttpGet("api/ad/users")]
    public IActionResult GetAdUsers()
    {
        if (!_adService.IsEnabled)
            return Ok(new { message = "AD не е конфигуриран", users = Array.Empty<object>() });

        try
        {
            var users = _adService.GetAllUsers();
            return Ok(users.Select(u => new
            {
                u.Username,
                u.DisplayName,
                u.Email,
                u.IsEnabled,
                u.Groups,
                hasFido2 = _webAuthn.HasCredentials(u.Username)
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при зареждане на AD потребители");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Информация за конкретен потребител от AD
    /// </summary>
    [HttpGet("api/ad/users/{username}")]
    public IActionResult GetAdUser(string username)
    {
        if (!_adService.IsEnabled)
            return NotFound(new { error = "AD не е конфигуриран" });

        var user = _adService.GetUserInfo(username);
        if (user == null)
            return NotFound(new { error = $"Потребителят '{username}' не е намерен" });

        return Ok(new
        {
            user.Username,
            user.DisplayName,
            user.Email,
            user.IsEnabled,
            user.Groups,
            hasFido2 = _webAuthn.HasCredentials(username),
            fido2Credentials = _webAuthn.GetCredentialsForUser(username)
                .Select(c => new { c.DeviceDescription, c.CreatedAt, c.LastUsedAt })
        });
    }

    /// <summary>
    /// Изтрива FIDO2 credential
    /// </summary>
    [HttpDelete("api/webauthn/credentials/{credentialId}")]
    public IActionResult DeleteCredential(string credentialId, [FromQuery] string username)
    {
        var deleted = _webAuthn.DeleteCredential(credentialId, username);
        return deleted
            ? Ok(new { message = "Credential изтрит" })
            : NotFound(new { error = "Credential не е намерен" });
    }
}

// =============================================
// REQUEST MODELS
// =============================================

public class BeginRegistrationRequest
{
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public class CompleteRegistrationRequest
{
    public string Username { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AttestationObject { get; set; } = string.Empty;
    public string ClientDataJson { get; set; } = string.Empty;
    public string? DeviceDescription { get; set; }
}

public class BeginAuthRequest
{
    public string? Username { get; set; }
}

public class CompleteAuthRequest
{
    public string ChallengeKey { get; set; } = string.Empty;
    public string CredentialId { get; set; } = string.Empty;
    public string AuthenticatorData { get; set; } = string.Empty;
    public string ClientDataJson { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}
