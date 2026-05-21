using ADS.WindowsAuth.Core.Models;
using PeterO.Cbor;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ADS.WindowsAuth.API.Services;

/// <summary>
/// Сервис за WebAuthn/FIDO2 регистрация и аутентикация
/// </summary>
public class WebAuthnService
{
    private readonly ILogger<WebAuthnService> _logger;
    private readonly string _rpId;
    private readonly string _rpName;

    // In-memory хранилище за credentials и pending challenges
    private readonly ConcurrentDictionary<string, Fido2Credential> _credentials = new();
    private readonly ConcurrentDictionary<string, (byte[] Challenge, DateTime Expires)> _pendingChallenges = new();

    public WebAuthnService(ILogger<WebAuthnService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _rpId = configuration["WebAuthn:RpId"] ?? "localhost";
        _rpName = configuration["WebAuthn:RpName"] ?? "ADS Windows Auth";
    }

    // =============================================
    // REGISTRATION
    // =============================================

    /// <summary>
    /// Генерира опции за регистрация на нов credential
    /// </summary>
    public RegistrationOptions BeginRegistration(string username, string displayName)
    {
        var challenge = GenerateChallenge();
        var userHandle = RandomNumberGenerator.GetBytes(16);

        // Запазваме challenge с 5-минутен timeout
        _pendingChallenges[$"reg:{username}"] = (challenge, DateTime.UtcNow.AddMinutes(5));

        return new RegistrationOptions
        {
            Challenge = Base64UrlEncode(challenge),
            Rp = new RpInfo { Id = _rpId, Name = _rpName },
            User = new WebAuthnUserInfo
            {
                Id = Base64UrlEncode(userHandle),
                Name = username,
                DisplayName = displayName
            },
            PubKeyCredParams = new[]
            {
                new { type = "public-key", alg = -7 },  // ES256 (ECDSA P-256)
                new { type = "public-key", alg = -257 } // RS256 (RSA-PKCS1-v1_5)
            },
            AuthenticatorSelection = new
            {
                userVerification = "required", // Изисква биометрия
                residentKey = "preferred"
            },
            Timeout = 60000,
            Attestation = "none"
        };
    }

    /// <summary>
    /// Верифицира и запазва нов credential след регистрация
    /// </summary>
    public async Task<(bool Success, string Error)> CompleteRegistrationAsync(
        string username,
        string credentialId,
        string attestationObjectBase64,
        string clientDataJsonBase64,
        string? deviceDescription = null)
    {
        try
        {
            // Вземаме pending challenge
            if (!_pendingChallenges.TryRemove($"reg:{username}", out var pending))
                return (false, "Няма pending регистрация или е изтекла");

            if (DateTime.UtcNow > pending.Expires)
                return (false, "Challenge е изтекъл");

            // Декодираме clientDataJSON
            var clientDataJson = Base64UrlDecode(clientDataJsonBase64);
            var clientData = JsonDocument.Parse(clientDataJson);

            // Верифицираме type
            if (clientData.RootElement.GetProperty("type").GetString() != "webauthn.create")
                return (false, "Невалиден тип операция");

            // Верифицираме challenge
            var receivedChallenge = clientData.RootElement.GetProperty("challenge").GetString() ?? "";
            var expectedChallenge = Base64UrlEncode(pending.Challenge);
            if (receivedChallenge != expectedChallenge)
                return (false, "Challenge не съвпада");

            // Верифицираме origin
            var origin = clientData.RootElement.GetProperty("origin").GetString() ?? "";
            if (!IsValidOrigin(origin))
                return (false, $"Невалиден origin: {origin}");

            // Декодираме attestationObject (CBOR)
            var attestationBytes = Base64UrlDecode(attestationObjectBase64);
            var attestation = CBORObject.DecodeFromBytes(attestationBytes);

            // Извличаме authData
            var authData = attestation["authData"].GetByteString();
            var publicKeyCose = ExtractPublicKeyFromAuthData(authData);

            if (publicKeyCose == null)
                return (false, "Не можах да извлека публичния ключ");

            // Запазваме credential
            var credIdBytes = Base64UrlDecode(credentialId);
            var credential = new Fido2Credential
            {
                Username = username,
                CredentialId = credentialId,
                PublicKeyCose = Convert.ToBase64String(publicKeyCose),
                SignCount = ExtractSignCountFromAuthData(authData),
                UserHandle = Base64UrlEncode(credIdBytes[..Math.Min(16, credIdBytes.Length)]),
                DeviceDescription = deviceDescription ?? "WebAuthn Device",
                CreatedAt = DateTime.Now,
                LastUsedAt = DateTime.Now
            };

            _credentials[credentialId] = credential;
            _logger.LogInformation("FIDO2 credential регистриран за {Username}", username);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при регистрация на FIDO2 credential");
            return (false, $"Грешка: {ex.Message}");
        }
    }

    // =============================================
    // AUTHENTICATION
    // =============================================

    /// <summary>
    /// Генерира опции за аутентикация (assertion)
    /// </summary>
    public AssertionOptions BeginAuthentication(string? username = null)
    {
        var challenge = GenerateChallenge();
        var challengeKey = username != null ? $"auth:{username}" : $"auth:anon:{Guid.NewGuid()}";

        _pendingChallenges[challengeKey] = (challenge, DateTime.UtcNow.AddMinutes(5));

        // Намираме credentials за потребителя ако е подаден
        var allowCredentials = username != null
            ? _credentials.Values
                .Where(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                .Select(c => new { type = "public-key", id = c.CredentialId })
                .ToArray()
            : Array.Empty<object>();

        return new AssertionOptions
        {
            Challenge = Base64UrlEncode(challenge),
            ChallengeKey = challengeKey,
            RpId = _rpId,
            AllowCredentials = allowCredentials,
            UserVerification = "required",
            Timeout = 60000
        };
    }

    /// <summary>
    /// Верифицира assertion и аутентикира потребителя
    /// </summary>
    public async Task<(bool Success, string Username, string Error)> CompleteAuthenticationAsync(
        string challengeKey,
        string credentialId,
        string authenticatorDataBase64,
        string clientDataJsonBase64,
        string signatureBase64)
    {
        try
        {
            // Вземаме pending challenge
            if (!_pendingChallenges.TryRemove(challengeKey, out var pending))
                return (false, string.Empty, "Няма pending аутентикация или е изтекла");

            if (DateTime.UtcNow > pending.Expires)
                return (false, string.Empty, "Challenge е изтекъл");

            // Намираме credential
            if (!_credentials.TryGetValue(credentialId, out var credential))
                return (false, string.Empty, "Credential не е намерен");

            // Декодираме clientDataJSON
            var clientDataBytes = Base64UrlDecode(clientDataJsonBase64);
            var clientData = JsonDocument.Parse(clientDataBytes);

            // Верифицираме type
            if (clientData.RootElement.GetProperty("type").GetString() != "webauthn.get")
                return (false, string.Empty, "Невалиден тип операция");

            // Верифицираме challenge
            var receivedChallenge = clientData.RootElement.GetProperty("challenge").GetString() ?? "";
            var expectedChallenge = Base64UrlEncode(pending.Challenge);
            if (receivedChallenge != expectedChallenge)
                return (false, string.Empty, "Challenge не съвпада");

            // Верифицираме origin
            var origin = clientData.RootElement.GetProperty("origin").GetString() ?? "";
            if (!IsValidOrigin(origin))
                return (false, string.Empty, $"Невалиден origin: {origin}");

            // Декодираме authData и signature
            var authenticatorData = Base64UrlDecode(authenticatorDataBase64);
            var signature = Base64UrlDecode(signatureBase64);

            // Верифицираме rpIdHash
            var rpIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(_rpId));
            if (!rpIdHash.SequenceEqual(authenticatorData[..32]))
                return (false, string.Empty, "rpId не съвпада");

            // Верифицираме user presence и user verification флагове
            byte flags = authenticatorData[32];
            bool userPresent = (flags & 0x01) != 0;
            bool userVerified = (flags & 0x04) != 0;

            if (!userPresent)
                return (false, string.Empty, "User Presence не е потвърдено");

            if (!userVerified)
                return (false, string.Empty, "User Verification (биометрия) не е потвърдено");

            // Верифицираме signature
            var clientDataHash = SHA256.HashData(clientDataBytes);
            var verificationData = authenticatorData.Concat(clientDataHash).ToArray();

            var publicKeyBytes = Convert.FromBase64String(credential.PublicKeyCose);
            bool signatureValid = VerifySignature(publicKeyBytes, verificationData, signature);

            if (!signatureValid)
                return (false, string.Empty, "Невалиден подпис");

            // Обновяваме sign count
            var newSignCount = ExtractSignCountFromAuthData(authenticatorData);
            if (newSignCount <= credential.SignCount && newSignCount != 0)
            {
                _logger.LogWarning("Sign count регресия за {Username} - евентуален replay attack", credential.Username);
                return (false, string.Empty, "Sign count регресия - евентуален replay attack");
            }

            credential.SignCount = newSignCount;
            credential.LastUsedAt = DateTime.Now;

            _logger.LogInformation("FIDO2 аутентикация успешна за {Username}", credential.Username);
            return (true, credential.Username, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Грешка при FIDO2 аутентикация");
            return (false, string.Empty, $"Грешка: {ex.Message}");
        }
    }

    // =============================================
    // УПРАВЛЕНИЕ НА CREDENTIALS
    // =============================================

    public IEnumerable<Fido2Credential> GetCredentialsForUser(string username) =>
        _credentials.Values.Where(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    public bool HasCredentials(string username) =>
        _credentials.Values.Any(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    public bool DeleteCredential(string credentialId, string username)
    {
        if (_credentials.TryGetValue(credentialId, out var cred) &&
            cred.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
        {
            return _credentials.TryRemove(credentialId, out _);
        }
        return false;
    }

    // =============================================
    // HELPERS
    // =============================================

    private static byte[] GenerateChallenge()
    {
        var challenge = new byte[32];
        RandomNumberGenerator.Fill(challenge);
        return challenge;
    }

    private bool IsValidOrigin(string origin)
    {
        // Позволяваме localhost и конфигурирания rpId
        return origin.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || origin.Contains(_rpId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Извлича COSE-encoded публичен ключ от authData
    /// </summary>
    private static byte[]? ExtractPublicKeyFromAuthData(byte[] authData)
    {
        try
        {
            // authData layout:
            // 32 bytes - rpIdHash
            // 1 byte  - flags
            // 4 bytes - signCount (big-endian)
            // 16 bytes - aaguid
            // 2 bytes - credentialIdLength
            // N bytes - credentialId
            // rest    - credentialPublicKey (CBOR COSE key)

            int offset = 37; // rpIdHash(32) + flags(1) + signCount(4)
            if (authData.Length < offset + 18) return null;

            // aaguid (16 bytes)
            offset += 16;

            // credentialIdLength (big-endian uint16)
            int credIdLen = (authData[offset] << 8) | authData[offset + 1];
            offset += 2;

            // Skip credentialId
            offset += credIdLen;

            // Rest is COSE key
            var coseKeyBytes = authData[offset..];
            return coseKeyBytes;
        }
        catch
        {
            return null;
        }
    }

    private static uint ExtractSignCountFromAuthData(byte[] authData)
    {
        if (authData.Length < 37) return 0;
        // signCount at bytes 33-36, big-endian
        return (uint)((authData[33] << 24) | (authData[34] << 16) | (authData[35] << 8) | authData[36]);
    }

    /// <summary>
    /// Верифицира ECDSA P-256 или RSA подпис
    /// </summary>
    private static bool VerifySignature(byte[] coseKeyBytes, byte[] data, byte[] signature)
    {
        try
        {
            var coseKey = CBORObject.DecodeFromBytes(coseKeyBytes);

            // kty: 1=OKP, 2=EC2, 3=RSA
            int kty = coseKey[CBORObject.FromObject(1)].AsInt32();

            if (kty == 2) // EC2 (ECDSA)
            {
                // alg: -7 = ES256 (P-256)
                var xBytes = coseKey[CBORObject.FromObject(-2)].GetByteString();
                var yBytes = coseKey[CBORObject.FromObject(-3)].GetByteString();

                using var ecdsa = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = xBytes, Y = yBytes }
                });

                // Signature is DER-encoded
                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
            }
            else if (kty == 3) // RSA
            {
                var nBytes = coseKey[CBORObject.FromObject(-1)].GetByteString();
                var eBytes = coseKey[CBORObject.FromObject(-2)].GetByteString();

                var rsaParams = new RSAParameters
                {
                    Modulus = nBytes,
                    Exponent = eBytes
                };

                using var rsa = RSA.Create(rsaParams);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}

// =============================================
// DTO Classes
// =============================================

public class RegistrationOptions
{
    public string Challenge { get; set; } = string.Empty;
    public RpInfo Rp { get; set; } = new();
    public WebAuthnUserInfo User { get; set; } = new();
    public object[] PubKeyCredParams { get; set; } = Array.Empty<object>();
    public object AuthenticatorSelection { get; set; } = new();
    public int Timeout { get; set; }
    public string Attestation { get; set; } = "none";
}

public class RpInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class WebAuthnUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class AssertionOptions
{
    public string Challenge { get; set; } = string.Empty;
    public string ChallengeKey { get; set; } = string.Empty;
    public string RpId { get; set; } = string.Empty;
    public object[] AllowCredentials { get; set; } = Array.Empty<object>();
    public string UserVerification { get; set; } = "required";
    public int Timeout { get; set; }
}
