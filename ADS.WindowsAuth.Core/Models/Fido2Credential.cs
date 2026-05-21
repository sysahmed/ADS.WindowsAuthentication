namespace ADS.WindowsAuth.Core.Models;

/// <summary>
/// Съхранен FIDO2/WebAuthn credential за потребител
/// </summary>
public class Fido2Credential
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Потребителско име (domain\\username или username)</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Base64url-encoded credential ID от authenticator-а</summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>Публичен ключ (COSE формат, Base64)</summary>
    public string PublicKeyCose { get; set; } = string.Empty;

    /// <summary>Sign count за replay protection</summary>
    public uint SignCount { get; set; }

    /// <summary>User handle (Base64url)</summary>
    public string UserHandle { get; set; } = string.Empty;

    /// <summary>Устройство/authenticator описание</summary>
    public string DeviceDescription { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastUsedAt { get; set; } = DateTime.Now;
}
