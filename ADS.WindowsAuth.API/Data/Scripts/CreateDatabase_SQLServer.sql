USE [ADS_WindowsAuth]
GO

-- Таблица за потребителски активности
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserActivities]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UserActivities](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [StartTime] [datetime2](6) NOT NULL,
    [EndTime] [datetime2](6) NULL,
    [ScreenTimeSeconds] [int] NOT NULL DEFAULT 0,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_UserActivities] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_UserActivities_Username_MachineName_StartTime] ON [dbo].[UserActivities]
(
    [Username] ASC,
    [MachineName] ASC,
    [StartTime] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_UserActivities_AdUserId] ON [dbo].[UserActivities]
(
    [AdUserId] ASC
)
GO

-- Таблица за събития на приложения
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ApplicationEvents]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ApplicationEvents](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [ApplicationName] [nvarchar](500) NOT NULL,
    [ExecutablePath] [nvarchar](1000) NULL,
    [ProcessId] [int] NULL,
    [EventType] [nvarchar](50) NOT NULL,
    [EventTime] [datetime2](6) NOT NULL,
    [DurationSeconds] [int] NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_ApplicationEvents] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_ApplicationEvents_Username_MachineName_EventTime] ON [dbo].[ApplicationEvents]
(
    [Username] ASC,
    [MachineName] ASC,
    [EventTime] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_ApplicationEvents_ApplicationName] ON [dbo].[ApplicationEvents]
(
    [ApplicationName] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_ApplicationEvents_AdUserId] ON [dbo].[ApplicationEvents]
(
    [AdUserId] ASC
)
GO

-- Таблица за файлова активност
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileActivities]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[FileActivities](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [FilePath] [nvarchar](2000) NOT NULL,
    [FileName] [nvarchar](500) NOT NULL,
    [FileExtension] [nvarchar](50) NULL,
    [FileSize] [bigint] NULL,
    [ApplicationName] [nvarchar](500) NULL,
    [EventType] [nvarchar](50) NOT NULL,
    [EventTime] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_FileActivities] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_FileActivities_Username_MachineName_EventTime] ON [dbo].[FileActivities]
(
    [Username] ASC,
    [MachineName] ASC,
    [EventTime] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_FileActivities_AdUserId] ON [dbo].[FileActivities]
(
    [AdUserId] ASC
)
GO

-- Таблица за мрежова активност
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NetworkActivities]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[NetworkActivities](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [InterfaceName] [nvarchar](255) NULL,
    [InterfaceDescription] [nvarchar](500) NULL,
    [Speed] [bigint] NULL,
    [BytesReceived] [bigint] NULL,
    [BytesSent] [bigint] NULL,
    [EventTime] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_NetworkActivities] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_NetworkActivities_Username_MachineName_EventTime] ON [dbo].[NetworkActivities]
(
    [Username] ASC,
    [MachineName] ASC,
    [EventTime] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_NetworkActivities_AdUserId] ON [dbo].[NetworkActivities]
(
    [AdUserId] ASC
)
GO

-- Таблица за системна информация
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SystemInfos]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[SystemInfos](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [Username] [nvarchar](255) NULL,
    [Domain] [nvarchar](255) NULL,
    [OsVersion] [nvarchar](255) NULL,
    [ProcessorCount] [int] NULL,
    [TotalMemory] [bigint] NULL,
    [WorkingSet] [bigint] NULL,
    [UptimeSeconds] [float] NULL,
    [EventTime] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    CONSTRAINT [PK_SystemInfos] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_SystemInfos_MachineName_EventTime] ON [dbo].[SystemInfos]
(
    [MachineName] ASC,
    [EventTime] ASC
)
GO

-- Таблица за USB устройства
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UsbDevices]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[UsbDevices](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [DeviceId] [nvarchar](500) NOT NULL,
    [Description] [nvarchar](500) NULL,
    [Manufacturer] [nvarchar](255) NULL,
    [Name] [nvarchar](500) NULL,
    [EventType] [nvarchar](50) NOT NULL,
    [EventTime] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_UsbDevices] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_UsbDevices_Username_MachineName_EventTime] ON [dbo].[UsbDevices]
(
    [Username] ASC,
    [MachineName] ASC,
    [EventTime] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_UsbDevices_AdUserId] ON [dbo].[UsbDevices]
(
    [AdUserId] ASC
)
GO

-- Таблица за screen time
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ScreenTimes]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ScreenTimes](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [Seconds] [int] NOT NULL,
    [RecordedAt] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_ScreenTimes] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_ScreenTimes_Username_MachineName_RecordedAt] ON [dbo].[ScreenTimes]
(
    [Username] ASC,
    [MachineName] ASC,
    [RecordedAt] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_ScreenTimes_AdUserId] ON [dbo].[ScreenTimes]
(
    [AdUserId] ASC
)
GO

-- Таблица за сесии за аутентикация
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuthSessions]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AuthSessions](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [SessionId] [nvarchar](100) NOT NULL,
    [AccessToken] [nvarchar](500) NOT NULL,
    [WindowsUsername] [nvarchar](255) NOT NULL,
    [Domain] [nvarchar](255) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [Status] [nvarchar](50) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [ExpiresAt] [datetime2](6) NOT NULL,
    [ApprovedAt] [datetime2](6) NULL,
    [RejectedAt] [datetime2](6) NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_AuthSessions] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AuthSessions_SessionId] ON [dbo].[AuthSessions]
(
    [SessionId] ASC
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AuthSessions_AccessToken] ON [dbo].[AuthSessions]
(
    [AccessToken] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_AuthSessions_WindowsUsername_MachineName] ON [dbo].[AuthSessions]
(
    [WindowsUsername] ASC,
    [MachineName] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_AuthSessions_AdUserId] ON [dbo].[AuthSessions]
(
    [AdUserId] ASC
)
GO

-- Таблица за политики
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Policies]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Policies](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Name] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](2000) NULL,
    [IsActive] [bit] NOT NULL DEFAULT 0,
    [BlockedWebsitesJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [BlockedApplicationsJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [BlockedFileExtensionsJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [TargetMachinesJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [TargetUsersJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [MaxScreenTimeSeconds] [int] NOT NULL DEFAULT 0,
    [AllowedInstallationsJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [BlockedInstallationsJson] [nvarchar](max) NOT NULL DEFAULT '[]',
    [BlockUsbAccess] [bit] NOT NULL DEFAULT 0,
    [BlockPrinterAccess] [bit] NOT NULL DEFAULT 0,
    [CreatedAt] [datetime2](6) NOT NULL,
    [UpdatedAt] [datetime2](6) NOT NULL,
    CONSTRAINT [PK_Policies] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

-- Таблица за AD потребители
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdUsers]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AdUsers](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Username] [nvarchar](255) NOT NULL,
    [DisplayName] [nvarchar](500) NULL,
    [Email] [nvarchar](255) NULL,
    [DistinguishedName] [nvarchar](1000) NOT NULL,
    [IsEnabled] [bit] NOT NULL DEFAULT 1,
    [LastLogon] [datetime2](6) NULL,
    [LastPasswordChange] [datetime2](6) NULL,
    [AccountExpires] [datetime2](6) NULL,
    [SyncedAt] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [UpdatedAt] [datetime2](6) NOT NULL,
    CONSTRAINT [PK_AdUsers] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AdUsers_Username] ON [dbo].[AdUsers]
(
    [Username] ASC
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AdUsers_DistinguishedName] ON [dbo].[AdUsers]
(
    [DistinguishedName] ASC
)
GO

-- Таблица за AD групи
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdGroups]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AdGroups](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [GroupName] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](1000) NULL,
    [DistinguishedName] [nvarchar](1000) NOT NULL,
    [SyncedAt] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [UpdatedAt] [datetime2](6) NOT NULL,
    CONSTRAINT [PK_AdGroups] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AdGroups_GroupName] ON [dbo].[AdGroups]
(
    [GroupName] ASC
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_AdGroups_DistinguishedName] ON [dbo].[AdGroups]
(
    [DistinguishedName] ASC
)
GO

-- Таблица за връзка между AD потребители и групи (many-to-many)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdUserGroups]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[AdUserGroups](
    [UserId] [int] NOT NULL,
    [GroupId] [int] NOT NULL,
    [AddedAt] [datetime2](6) NOT NULL,
    CONSTRAINT [PK_AdUserGroups] PRIMARY KEY CLUSTERED ([UserId] ASC, [GroupId] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_AdUserGroups_GroupId] ON [dbo].[AdUserGroups]
(
    [GroupId] ASC
)
GO

-- Таблица за Windows Events
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WindowsEvents]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[WindowsEvents](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [MachineName] [nvarchar](255) NOT NULL,
    [Username] [nvarchar](255) NULL,
    [EventId] [int] NOT NULL,
    [LogName] [nvarchar](255) NOT NULL,
    [ProviderName] [nvarchar](255) NULL,
    [Level] [nvarchar](50) NULL,
    [Message] [nvarchar](4000) NULL,
    [EventTime] [datetime2](6) NOT NULL,
    [CreatedAt] [datetime2](6) NOT NULL,
    [AdUserId] [int] NULL,
    CONSTRAINT [PK_WindowsEvents] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
END
GO

CREATE NONCLUSTERED INDEX [IX_WindowsEvents_MachineName_EventTime] ON [dbo].[WindowsEvents]
(
    [MachineName] ASC,
    [EventTime] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_WindowsEvents_EventId] ON [dbo].[WindowsEvents]
(
    [EventId] ASC
)
GO

CREATE NONCLUSTERED INDEX [IX_WindowsEvents_AdUserId] ON [dbo].[WindowsEvents]
(
    [AdUserId] ASC
)
GO

-- Foreign keys за всички таблици към AdUsers
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_UserActivities_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[UserActivities] ADD CONSTRAINT [FK_UserActivities_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ApplicationEvents_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[ApplicationEvents] ADD CONSTRAINT [FK_ApplicationEvents_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FileActivities_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[FileActivities] ADD CONSTRAINT [FK_FileActivities_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_NetworkActivities_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[NetworkActivities] ADD CONSTRAINT [FK_NetworkActivities_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_UsbDevices_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[UsbDevices] ADD CONSTRAINT [FK_UsbDevices_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ScreenTimes_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[ScreenTimes] ADD CONSTRAINT [FK_ScreenTimes_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AuthSessions_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[AuthSessions] ADD CONSTRAINT [FK_AuthSessions_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_WindowsEvents_AdUsers_AdUserId')
BEGIN
ALTER TABLE [dbo].[WindowsEvents] ADD CONSTRAINT [FK_WindowsEvents_AdUsers_AdUserId] FOREIGN KEY([AdUserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE SET NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AdUserGroups_AdUsers_UserId')
BEGIN
ALTER TABLE [dbo].[AdUserGroups] ADD CONSTRAINT [FK_AdUserGroups_AdUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AdUsers] ([Id]) ON DELETE CASCADE
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AdUserGroups_AdGroups_GroupId')
BEGIN
ALTER TABLE [dbo].[AdUserGroups] ADD CONSTRAINT [FK_AdUserGroups_AdGroups_GroupId] FOREIGN KEY([GroupId])
REFERENCES [dbo].[AdGroups] ([Id]) ON DELETE CASCADE
END
GO

