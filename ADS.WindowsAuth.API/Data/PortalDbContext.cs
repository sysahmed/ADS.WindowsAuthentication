using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ADS.WindowsAuth.API.Data;

/// <summary>
/// DbContext за Portal Identity (ASP.NET Core Identity — AspNetUsers, AspNetRoles, etc.)
/// Отделен от ApplicationDbContext за да не се налага да се промени Core проекта.
/// </summary>
public class PortalDbContext : IdentityDbContext<PortalUser>
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options) { }

    /// <summary>FIDO2/WebAuthn credentials — обвързани с PortalUser</summary>
    public DbSet<StoredFido2Key> Fido2Keys { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<StoredFido2Key>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.CredentialId).IsUnique();
            e.HasIndex(k => k.UserId);
            e.Property(k => k.CredentialId).HasMaxLength(500).IsRequired();
            e.Property(k => k.UserId).HasMaxLength(450).IsRequired();
        });
    }
}

/// <summary>
/// Съхранен FIDO2 ключ в базата данни
/// </summary>
public class StoredFido2Key
{
    public int Id { get; set; }

    /// <summary>FK към AspNetUsers.Id</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Base64url credential ID от authenticator-а</summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>COSE public key (Base64)</summary>
    public string PublicKeyCose { get; set; } = string.Empty;

    /// <summary>Sign counter (replay protection)</summary>
    public uint SignCount { get; set; }

    /// <summary>Описание на устройство (напр. "Windows Hello")</summary>
    public string DeviceDescription { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
