using Microsoft.EntityFrameworkCore;
using ADS.WindowsAuth.Core.Data.Entities;

namespace ADS.WindowsAuth.Core.Data;

/// <summary>
/// DbContext за приложението
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Активности
    public DbSet<UserActivityEntity> UserActivities { get; set; }
    public DbSet<ApplicationEventEntity> ApplicationEvents { get; set; }
    public DbSet<FileActivityEntity> FileActivities { get; set; }
    public DbSet<NetworkActivityEntity> NetworkActivities { get; set; }
    public DbSet<SystemInfoEntity> SystemInfos { get; set; }
    public DbSet<UsbDeviceEntity> UsbDevices { get; set; }
    public DbSet<ScreenTimeEntity> ScreenTimes { get; set; }

    // Сесии
    public DbSet<AuthSessionEntity> AuthSessions { get; set; }

    // Политики
    public DbSet<PolicyEntity> Policies { get; set; }

    // Active Directory
    public DbSet<AdUserEntity> AdUsers { get; set; }
    public DbSet<AdGroupEntity> AdGroups { get; set; }
    public DbSet<AdUserGroupEntity> AdUserGroups { get; set; }

    // Windows Events
    public DbSet<WindowsEventEntity> WindowsEvents { get; set; }

    // Логове
    public DbSet<LogEntryEntity> LogEntries { get; set; }

    // Monitor конфигурации
    public DbSet<MonitorConfigurationEntity> MonitorConfigurations { get; set; }

    // Login събития
    public DbSet<LoginEventEntity> LoginEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UserActivity конфигурация
        modelBuilder.Entity<UserActivityEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.StartTime });
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Domain).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
        });

        // ApplicationEvent конфигурация
        modelBuilder.Entity<ApplicationEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.EventTime });
            entity.HasIndex(e => e.ApplicationName);
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ApplicationName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ExecutablePath).HasMaxLength(1000);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        });

        // FileActivity конфигурация
        modelBuilder.Entity<FileActivityEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.EventTime });
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ApplicationName).HasMaxLength(500);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        });

        // NetworkActivity конфигурация
        modelBuilder.Entity<NetworkActivityEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.EventTime });
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.InterfaceName).HasMaxLength(255);
            entity.Property(e => e.InterfaceDescription).HasMaxLength(500);
        });

        // SystemInfo конфигурация
        modelBuilder.Entity<SystemInfoEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineName, e.EventTime });
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.OsVersion).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(255);
        });

        // UsbDevice конфигурация
        modelBuilder.Entity<UsbDeviceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.EventTime });
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DeviceId).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Manufacturer).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        });

        // ScreenTime конфигурация
        modelBuilder.Entity<ScreenTimeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.RecordedAt });
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
        });

        // AuthSession конфигурация
        modelBuilder.Entity<AuthSessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.AccessToken).IsUnique();
            entity.HasIndex(e => new { e.WindowsUsername, e.MachineName });
            entity.Property(e => e.SessionId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccessToken).HasMaxLength(500).IsRequired();
            entity.Property(e => e.WindowsUsername).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Domain).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
        });

        // Policy конфигурация
        modelBuilder.Entity<PolicyEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
        });

        // AdUser конфигурация
        modelBuilder.Entity<AdUserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.DistinguishedName).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.DistinguishedName).HasMaxLength(1000).IsRequired();
        });

        // AdGroup конфигурация
        modelBuilder.Entity<AdGroupEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.GroupName).IsUnique();
            entity.HasIndex(e => e.DistinguishedName).IsUnique();
            entity.Property(e => e.GroupName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.DistinguishedName).HasMaxLength(1000).IsRequired();
        });

        // AdUserGroup конфигурация (many-to-many)
        modelBuilder.Entity<AdUserGroupEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.GroupId });
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserGroups)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Group)
                .WithMany(g => g.UserGroups)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WindowsEvent конфигурация
        modelBuilder.Entity<WindowsEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineName, e.EventTime });
            entity.HasIndex(e => e.EventId);
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(255);
            entity.Property(e => e.LogName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ProviderName).HasMaxLength(255);
            entity.Property(e => e.Level).HasMaxLength(50);
            entity.Property(e => e.Message).HasMaxLength(4000);
        });

        // LogEntry конфигурация
        modelBuilder.Entity<LogEntryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MachineName, e.Timestamp });
            entity.HasIndex(e => new { e.Username, e.Timestamp });
            entity.HasIndex(e => e.Level);
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Domain).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Level).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(100);
            entity.Property(e => e.ExceptionType).HasMaxLength(255);
        });

        // MonitorConfiguration конфигурация
        modelBuilder.Entity<MonitorConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MachineName).IsUnique();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ServiceUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ApiKey).HasMaxLength(500);
            entity.Property(e => e.VpnGateways).HasMaxLength(2000);
            entity.Property(e => e.VpnProcessNames).HasMaxLength(1000);
        });

        // LoginEvent конфигурация
        modelBuilder.Entity<LoginEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.MachineName, e.LoginTime });
            entity.HasIndex(e => e.LoginMethod);
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Domain).HasMaxLength(255).IsRequired();
            entity.Property(e => e.MachineName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.LoginMethod).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
        });
    }
}

