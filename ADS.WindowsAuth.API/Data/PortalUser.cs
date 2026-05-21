using Microsoft.AspNetCore.Identity;

namespace ADS.WindowsAuth.API.Data;

/// <summary>
/// Portal потребител — разширява ASP.NET Core Identity IdentityUser
/// </summary>
public class PortalUser : IdentityUser
{
    /// <summary>Пълно Имя</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Свързан Windows/AD username (domain\\user)</summary>
    public string? WindowsUsername { get; set; }

    /// <summary>Домейн (ако е свързан с AD)</summary>
    public string? Domain { get; set; }

    /// <summary>Роля в системата</summary>
    public string Role { get; set; } = "User";

    /// <summary>Дали акаунтът е активен</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Дата на последен вход</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Дата на създаване</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Бележки</summary>
    public string? Notes { get; set; }
}
