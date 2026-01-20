using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ADS.WindowsAuth.Core.Configuration;
using ADS.WindowsAuth.Core.Models;

namespace ADS.WindowsAuth.Core.Services;

/// <summary>
/// JWT token service implementation
/// </summary>
public class JwtService : IJwtService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILoggerService _logger;
    private readonly string _key;
    private readonly SigningCredentials _signingCredentials;

    public JwtService(JwtSettings jwtSettings, ILoggerService logger)
    {
        _jwtSettings = jwtSettings;
        _logger = logger;

        if (!_jwtSettings.IsValid())
        {
            throw new InvalidOperationException("JWT configuration is invalid");
        }

        _key = _jwtSettings.Key;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            SecurityAlgorithms.HmacSha256);
    }

    public string GenerateToken(UserInfo user, AuthSession session)
    {
        try
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Username),
                new Claim("domain", user.Domain),
                new Claim("machine_name", user.MachineName),
                new Claim("session_id", session.SessionId),
                new Claim("access_token", session.AccessToken),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.Now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add roles
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpiryMinutes),
                signingCredentials: _signingCredentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogInfo($"JWT token generated for user {user.Username}@{user.Domain}, Session: {session.SessionId}");

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to generate JWT token for {user.Username}: {ex.Message}", ex);
            throw;
        }
    }

    public TokenValidationResult ValidateToken(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Опит за валидация на празен токен");
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token is empty or null"
                };
            }
            
            _logger.LogInfo($"Започва валидация на JWT токен (дължина: {token.Length} символа)");
            
            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Проверка дали токенът може да се прочете (без валидация)
            if (!tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning($"Токенът не може да се прочете. Формат: {token.Substring(0, Math.Min(20, token.Length))}...");
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Token cannot be read - invalid format"
                };
            }
            
            var validationParameters = CreateValidationParameters();
            
            // Логване на параметрите за валидация (без ключа)
            _logger.LogInfo($"Валидация с Issuer: {validationParameters.ValidIssuer}, Audience: {validationParameters.ValidAudience}");

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                _logger.LogWarning("Валидираният токен не е JWT формат");
                return new TokenValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid token format"
                };
            }

            // Extract user information
            var user = ExtractUserFromPrincipal(principal, jwtToken);
            
            _logger.LogInfo($"JWT токен валидиран успешно за потребител: {user.Username}@{user.Domain}, Изтича: {jwtToken.ValidTo:yyyy-MM-dd HH:mm:ss}");

            return new TokenValidationResult
            {
                IsValid = true,
                User = user,
                Expiration = jwtToken.ValidTo
            };
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning($"JWT токен е изтекъл: {ex.Message}");
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token has expired"
            };
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogError($"Невалиден JWT подпис: {ex.Message}");
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid token signature"
            };
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            _logger.LogError($"Невалиден JWT издател: {ex.Message}");
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid token issuer"
            };
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            _logger.LogError($"Невалидна JWT аудитория: {ex.Message}");
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid token audience"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Грешка при валидация на JWT токен: {ex.Message} (Тип: {ex.GetType().Name})", ex);
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Token validation failed: {ex.Message}"
            };
        }
    }

    public UserInfo? GetUserFromToken(string token)
    {
        var validationResult = ValidateToken(token);
        return validationResult.IsValid ? validationResult.User : null;
    }

    private TokenValidationParameters CreateValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key))
        };
    }

    private UserInfo ExtractUserFromPrincipal(ClaimsPrincipal principal, JwtSecurityToken token)
    {
        var usernameClaim = principal.FindFirst(ClaimTypes.Name)?.Value ?? 
                           principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
                           string.Empty;

        var domainClaim = principal.FindFirst("domain")?.Value ?? string.Empty;
        var machineNameClaim = principal.FindFirst("machine_name")?.Value ?? string.Empty;
        var sessionIdClaim = principal.FindFirst("session_id")?.Value ?? string.Empty;
        var accessTokenClaim = principal.FindFirst("access_token")?.Value ?? string.Empty;

        var roles = principal.FindAll(ClaimTypes.Role)
                           .Select(c => c.Value)
                           .ToArray();

        return new UserInfo
        {
            Username = usernameClaim,
            Domain = domainClaim,
            MachineName = machineNameClaim,
            Roles = roles
        };
    }
}
