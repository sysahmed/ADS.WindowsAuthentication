-- =============================================================================
-- Всички SQL заявки за създаване на таблици, извлечени от Program.cs
-- Стартирай този скрипт в съответната база (виж коментарите за Application vs Portal).
-- Таблиците от EF миграции (UserActivities, ApplicationEvents, Policies, LoginEvents и др.)
-- се създават от dotnet ef или Database.MigrateAsync() – не са включени тук.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- APPLICATION DATABASE (ConnectionStrings:DefaultConnection / ApplicationDbContext)
-- -----------------------------------------------------------------------------

-- 1) История на миграциите (ако не използваш MigrateAsync)
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);

-- Опционално: запис за InitialCreate (ако вече не го има)
-- INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) 
-- VALUES (N'20260212103653_InitialCreate', N'8.0.22');

-- 2) LogEntries
IF OBJECT_ID(N'dbo.LogEntries', N'U') IS NULL
CREATE TABLE [dbo].[LogEntries] (
    [Id] int NOT NULL IDENTITY,
    [MachineName] nvarchar(255) NOT NULL,
    [Username] nvarchar(255) NOT NULL,
    [Domain] nvarchar(255) NOT NULL,
    [Level] nvarchar(50) NOT NULL,
    [Message] nvarchar(4000) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Source] nvarchar(100) NULL,
    [ExceptionType] nvarchar(255) NULL,
    [StackTrace] nvarchar(max) NULL,
    CONSTRAINT [PK_LogEntries] PRIMARY KEY ([Id])
);

-- 3) MonitorConfigurations
IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NULL
CREATE TABLE [dbo].[MonitorConfigurations] (
    [Id] int NOT NULL IDENTITY,
    [MachineName] nvarchar(255) NOT NULL,
    [ServiceUrl] nvarchar(500) NOT NULL,
    [ApiKey] nvarchar(500) NULL,
    [RequireVpn] bit NOT NULL DEFAULT 0,
    [VpnCheckInterval] int NOT NULL DEFAULT 300,
    [VpnGateways] nvarchar(2000) NOT NULL DEFAULT '[]',
    [VpnProcessNames] nvarchar(1000) NOT NULL DEFAULT '[]',
    [OfflineMode] bit NOT NULL DEFAULT 0,
    [OfflineDataRetention] int NOT NULL DEFAULT 7,
    [ConnectionTimeout] int NOT NULL DEFAULT 30,
    [RetryInterval] int NOT NULL DEFAULT 60,
    [MaxRetries] int NOT NULL DEFAULT 3,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_MonitorConfigurations] PRIMARY KEY ([Id])
);
IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MonitorConfigurations_MachineName' AND object_id = OBJECT_ID(N'dbo.MonitorConfigurations'))
CREATE UNIQUE INDEX [IX_MonitorConfigurations_MachineName] ON [dbo].[MonitorConfigurations] ([MachineName]);
IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MonitorConfigurations') AND name = N'ScreenshotEnabled')
ALTER TABLE [dbo].[MonitorConfigurations] ADD [ScreenshotEnabled] bit NOT NULL DEFAULT 0;
IF OBJECT_ID(N'dbo.MonitorConfigurations', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MonitorConfigurations') AND name = N'ScreenshotIntervalMinutes')
ALTER TABLE [dbo].[MonitorConfigurations] ADD [ScreenshotIntervalMinutes] int NOT NULL DEFAULT 5;

-- 4) VisitedWebsites
IF OBJECT_ID(N'dbo.VisitedWebsites', N'U') IS NULL
CREATE TABLE [dbo].[VisitedWebsites] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(255) NOT NULL,
    [MachineName] nvarchar(255) NOT NULL,
    [Url] nvarchar(2000) NOT NULL,
    [Title] nvarchar(500) NULL,
    [Browser] nvarchar(100) NOT NULL,
    [VisitedAt] datetime2 NOT NULL,
    [DurationSeconds] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_VisitedWebsites] PRIMARY KEY ([Id])
);
IF OBJECT_ID(N'dbo.VisitedWebsites', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VisitedWebsites_Username_MachineName_VisitedAt' AND object_id = OBJECT_ID(N'dbo.VisitedWebsites'))
CREATE INDEX [IX_VisitedWebsites_Username_MachineName_VisitedAt] ON [dbo].[VisitedWebsites] ([Username], [MachineName], [VisitedAt]);

-- 5) InputLogs
IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NULL
CREATE TABLE [dbo].[InputLogs] (
    [Id] int NOT NULL IDENTITY,
    [MachineName] nvarchar(255) NOT NULL,
    [Username] nvarchar(255) NOT NULL,
    [Domain] nvarchar(255) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [LogType] nvarchar(20) NOT NULL,
    [ApplicationName] nvarchar(500) NULL,
    [WindowTitle] nvarchar(1000) NULL,
    [Data] nvarchar(2000) NOT NULL,
    [IsPassword] bit NOT NULL DEFAULT 0,
    CONSTRAINT [PK_InputLogs] PRIMARY KEY ([Id])
);
IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InputLogs_MachineName_Timestamp' AND object_id = OBJECT_ID(N'dbo.InputLogs'))
CREATE INDEX [IX_InputLogs_MachineName_Timestamp] ON [dbo].[InputLogs] ([MachineName], [Timestamp]);
IF OBJECT_ID(N'dbo.InputLogs', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InputLogs_Username_Timestamp' AND object_id = OBJECT_ID(N'dbo.InputLogs'))
CREATE INDEX [IX_InputLogs_Username_Timestamp] ON [dbo].[InputLogs] ([Username], [Timestamp]);

-- 6) BlockedIps
IF OBJECT_ID(N'dbo.BlockedIps', N'U') IS NULL
CREATE TABLE [dbo].[BlockedIps] (
    [Id] int NOT NULL IDENTITY,
    [IpAddress] nvarchar(50) NOT NULL,
    [FailedAttempts] int NOT NULL DEFAULT 0,
    [BlockedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [LastAttemptAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [UnblockedAt] datetime2 NULL,
    [UnblockedBy] nvarchar(255) NULL,
    [UnblockReason] nvarchar(500) NULL,
    CONSTRAINT [PK_BlockedIps] PRIMARY KEY ([Id])
);
IF OBJECT_ID(N'dbo.BlockedIps', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BlockedIps_IpAddress' AND object_id = OBJECT_ID(N'dbo.BlockedIps'))
CREATE INDEX [IX_BlockedIps_IpAddress] ON [dbo].[BlockedIps] ([IpAddress]);

-- 7) EmailActivities
IF OBJECT_ID(N'dbo.EmailActivities', N'U') IS NULL
CREATE TABLE [dbo].[EmailActivities] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(255) NOT NULL,
    [Domain] nvarchar(255) NOT NULL DEFAULT '',
    [MachineName] nvarchar(255) NOT NULL,
    [Subject] nvarchar(1000) NOT NULL DEFAULT '',
    [SenderOrRecipient] nvarchar(500) NULL,
    [EventType] nvarchar(50) NOT NULL DEFAULT 'Opened',
    [DetectionSource] nvarchar(100) NULL,
    [EventTime] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_EmailActivities] PRIMARY KEY ([Id])
);
IF OBJECT_ID(N'dbo.EmailActivities', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmailActivities_User' AND object_id = OBJECT_ID(N'dbo.EmailActivities'))
CREATE INDEX [IX_EmailActivities_User] ON [dbo].[EmailActivities] ([Username], [MachineName], [EventTime]);

-- 8) Policies – само допълнителни колони (таблицата Policies идва от EF миграции)
IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'AllowedApplicationsJson')
ALTER TABLE [dbo].[Policies] ADD [AllowedApplicationsJson] nvarchar(2000) NOT NULL DEFAULT '[]';
IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'AppWhitelistMode')
ALTER TABLE [dbo].[Policies] ADD [AppWhitelistMode] bit NOT NULL DEFAULT 0;
IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'AllowedWebsitesJson')
ALTER TABLE [dbo].[Policies] ADD [AllowedWebsitesJson] nvarchar(2000) NOT NULL DEFAULT '[]';
IF OBJECT_ID(N'dbo.Policies', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Policies') AND name = N'WebWhitelistMode')
ALTER TABLE [dbo].[Policies] ADD [WebWhitelistMode] bit NOT NULL DEFAULT 0;

-- =============================================================================
-- PORTAL DATABASE (ConnectionStrings:PortalConnection / PortalDbContext – Identity)
-- Стартирай следващите блокове в базата за портала (AspNetUsers и др.).
-- =============================================================================

-- 9) AspNetRoles
IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
CREATE TABLE [dbo].[AspNetRoles] (
    [Id]               nvarchar(450) NOT NULL,
    [Name]             nvarchar(256) NULL,
    [NormalizedName]   nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'RoleNameIndex' AND object_id = OBJECT_ID(N'dbo.AspNetRoles'))
CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

-- 10) AspNetUsers
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUsers] (
    [Id]                   nvarchar(450)  NOT NULL,
    [DisplayName]          nvarchar(max)  NOT NULL DEFAULT '',
    [WindowsUsername]      nvarchar(max)  NULL,
    [Domain]               nvarchar(max)  NULL,
    [Role]                 nvarchar(max)  NOT NULL DEFAULT 'User',
    [IsActive]             bit            NOT NULL DEFAULT 1,
    [LastLoginAt]          datetime2      NULL,
    [CreatedAt]            datetime2      NOT NULL DEFAULT GETUTCDATE(),
    [Notes]                nvarchar(max)  NULL,
    [UserName]             nvarchar(256)  NULL,
    [NormalizedUserName]   nvarchar(256)  NULL,
    [Email]                nvarchar(256)  NULL,
    [NormalizedEmail]      nvarchar(256)  NULL,
    [EmailConfirmed]       bit            NOT NULL DEFAULT 0,
    [PasswordHash]         nvarchar(max)  NULL,
    [SecurityStamp]        nvarchar(max)  NULL,
    [ConcurrencyStamp]     nvarchar(max)  NULL,
    [PhoneNumber]          nvarchar(max)  NULL,
    [PhoneNumberConfirmed] bit            NOT NULL DEFAULT 0,
    [TwoFactorEnabled]     bit            NOT NULL DEFAULT 0,
    [LockoutEnd]           datetimeoffset NULL,
    [LockoutEnabled]       bit            NOT NULL DEFAULT 1,
    [AccessFailedCount]    int            NOT NULL DEFAULT 0,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UserNameIndex' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'EmailIndex' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);

-- 11) AspNetRoleClaims
IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
CREATE TABLE [dbo].[AspNetRoleClaims] (
    [Id]         int           NOT NULL IDENTITY,
    [RoleId]     nvarchar(450) NOT NULL,
    [ClaimType]  nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetRoleClaims_RoleId' AND object_id = OBJECT_ID(N'dbo.AspNetRoleClaims'))
CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);

-- 12) AspNetUserClaims
IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserClaims] (
    [Id]         int           NOT NULL IDENTITY,
    [UserId]     nvarchar(450) NOT NULL,
    [ClaimType]  nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserClaims_UserId' AND object_id = OBJECT_ID(N'dbo.AspNetUserClaims'))
CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);

-- 13) AspNetUserLogins
IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserLogins] (
    [LoginProvider]       nvarchar(128) NOT NULL,
    [ProviderKey]         nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId]              nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserLogins_UserId' AND object_id = OBJECT_ID(N'dbo.AspNetUserLogins'))
CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);

-- 14) AspNetUserRoles
IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserRoles_RoleId' AND object_id = OBJECT_ID(N'dbo.AspNetUserRoles'))
CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);

-- 15) AspNetUserTokens
IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserTokens] (
    [UserId]        nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name]          nvarchar(128) NOT NULL,
    [Value]         nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
);

-- 16) Fido2Keys
IF OBJECT_ID(N'dbo.Fido2Keys', N'U') IS NULL
CREATE TABLE [dbo].[Fido2Keys] (
    [Id]                int           NOT NULL IDENTITY,
    [UserId]            nvarchar(450) NOT NULL,
    [CredentialId]      nvarchar(500) NOT NULL,
    [PublicKeyCose]     nvarchar(max) NOT NULL,
    [SignCount]         decimal(20,0) NOT NULL DEFAULT 0,
    [DeviceDescription] nvarchar(max) NOT NULL DEFAULT '',
    [CreatedAt]         datetime2     NOT NULL DEFAULT GETUTCDATE(),
    [LastUsedAt]        datetime2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Fido2Keys] PRIMARY KEY ([Id])
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fido2Keys_CredentialId' AND object_id = OBJECT_ID(N'dbo.Fido2Keys'))
CREATE UNIQUE INDEX [IX_Fido2Keys_CredentialId] ON [dbo].[Fido2Keys] ([CredentialId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fido2Keys_UserId' AND object_id = OBJECT_ID(N'dbo.Fido2Keys'))
CREATE INDEX [IX_Fido2Keys_UserId] ON [dbo].[Fido2Keys] ([UserId]);
