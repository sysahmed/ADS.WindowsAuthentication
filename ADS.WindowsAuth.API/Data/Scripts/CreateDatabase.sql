-- Създаване на базата данни
CREATE DATABASE IF NOT EXISTS ADS_WindowsAuth CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE ADS_WindowsAuth;

-- Таблица за потребителски активности
CREATE TABLE IF NOT EXISTS UserActivities (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    StartTime DATETIME(6) NOT NULL,
    EndTime DATETIME(6) NULL,
    ScreenTimeSeconds INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_UserActivities_Username_MachineName_StartTime (Username, MachineName, StartTime),
    INDEX IX_UserActivities_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за събития на приложения
CREATE TABLE IF NOT EXISTS ApplicationEvents (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    ApplicationName VARCHAR(500) NOT NULL,
    ExecutablePath VARCHAR(1000) NULL,
    ProcessId INT NULL,
    EventType VARCHAR(50) NOT NULL,
    EventTime DATETIME(6) NOT NULL,
    DurationSeconds INT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_ApplicationEvents_Username_MachineName_EventTime (Username, MachineName, EventTime),
    INDEX IX_ApplicationEvents_ApplicationName (ApplicationName),
    INDEX IX_ApplicationEvents_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за файлова активност
CREATE TABLE IF NOT EXISTS FileActivities (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    FilePath VARCHAR(2000) NOT NULL,
    FileName VARCHAR(500) NOT NULL,
    FileExtension VARCHAR(50) NULL,
    FileSize BIGINT NULL,
    ApplicationName VARCHAR(500) NULL,
    EventType VARCHAR(50) NOT NULL,
    EventTime DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_FileActivities_Username_MachineName_EventTime (Username, MachineName, EventTime),
    INDEX IX_FileActivities_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за мрежова активност
CREATE TABLE IF NOT EXISTS NetworkActivities (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    InterfaceName VARCHAR(255) NULL,
    InterfaceDescription VARCHAR(500) NULL,
    Speed BIGINT NULL,
    BytesReceived BIGINT NULL,
    BytesSent BIGINT NULL,
    EventTime DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_NetworkActivities_Username_MachineName_EventTime (Username, MachineName, EventTime),
    INDEX IX_NetworkActivities_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за системна информация
CREATE TABLE IF NOT EXISTS SystemInfos (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    MachineName VARCHAR(255) NOT NULL,
    Username VARCHAR(255) NULL,
    Domain VARCHAR(255) NULL,
    OsVersion VARCHAR(255) NULL,
    ProcessorCount INT NULL,
    TotalMemory BIGINT NULL,
    WorkingSet BIGINT NULL,
    UptimeSeconds DOUBLE NULL,
    EventTime DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    INDEX IX_SystemInfos_MachineName_EventTime (MachineName, EventTime)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за USB устройства
CREATE TABLE IF NOT EXISTS UsbDevices (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    DeviceId VARCHAR(500) NOT NULL,
    Description VARCHAR(500) NULL,
    Manufacturer VARCHAR(255) NULL,
    Name VARCHAR(500) NULL,
    EventType VARCHAR(50) NOT NULL,
    EventTime DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_UsbDevices_Username_MachineName_EventTime (Username, MachineName, EventTime),
    INDEX IX_UsbDevices_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за screen time
CREATE TABLE IF NOT EXISTS ScreenTimes (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    Seconds INT NOT NULL,
    RecordedAt DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_ScreenTimes_Username_MachineName_RecordedAt (Username, MachineName, RecordedAt),
    INDEX IX_ScreenTimes_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за сесии за аутентикация
CREATE TABLE IF NOT EXISTS AuthSessions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId VARCHAR(100) NOT NULL,
    AccessToken VARCHAR(500) NOT NULL,
    WindowsUsername VARCHAR(255) NOT NULL,
    Domain VARCHAR(255) NOT NULL,
    MachineName VARCHAR(255) NOT NULL,
    Status VARCHAR(50) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    ExpiresAt DATETIME(6) NOT NULL,
    ApprovedAt DATETIME(6) NULL,
    RejectedAt DATETIME(6) NULL,
    AdUserId INT NULL,
    UNIQUE INDEX IX_AuthSessions_SessionId (SessionId),
    UNIQUE INDEX IX_AuthSessions_AccessToken (AccessToken),
    INDEX IX_AuthSessions_WindowsUsername_MachineName (WindowsUsername, MachineName),
    INDEX IX_AuthSessions_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за политики
CREATE TABLE IF NOT EXISTS Policies (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description VARCHAR(2000) NULL,
    IsActive BOOLEAN NOT NULL DEFAULT FALSE,
    BlockedWebsitesJson TEXT NOT NULL DEFAULT '[]',
    BlockedApplicationsJson TEXT NOT NULL DEFAULT '[]',
    BlockedFileExtensionsJson TEXT NOT NULL DEFAULT '[]',
    TargetMachinesJson TEXT NOT NULL DEFAULT '[]',
    TargetUsersJson TEXT NOT NULL DEFAULT '[]',
    MaxScreenTimeSeconds INT NOT NULL DEFAULT 0,
    AllowedInstallationsJson TEXT NOT NULL DEFAULT '[]',
    BlockedInstallationsJson TEXT NOT NULL DEFAULT '[]',
    BlockUsbAccess BOOLEAN NOT NULL DEFAULT FALSE,
    BlockPrinterAccess BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedAt DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за AD потребители
CREATE TABLE IF NOT EXISTS AdUsers (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(255) NOT NULL,
    DisplayName VARCHAR(500) NULL,
    Email VARCHAR(255) NULL,
    DistinguishedName VARCHAR(1000) NOT NULL,
    IsEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    LastLogon DATETIME(6) NULL,
    LastPasswordChange DATETIME(6) NULL,
    AccountExpires DATETIME(6) NULL,
    SyncedAt DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedAt DATETIME(6) NOT NULL,
    UNIQUE INDEX IX_AdUsers_Username (Username),
    UNIQUE INDEX IX_AdUsers_DistinguishedName (DistinguishedName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за AD групи
CREATE TABLE IF NOT EXISTS AdGroups (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    GroupName VARCHAR(255) NOT NULL,
    Description VARCHAR(1000) NULL,
    DistinguishedName VARCHAR(1000) NOT NULL,
    SyncedAt DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    UpdatedAt DATETIME(6) NOT NULL,
    UNIQUE INDEX IX_AdGroups_GroupName (GroupName),
    UNIQUE INDEX IX_AdGroups_DistinguishedName (DistinguishedName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за връзка между AD потребители и групи (many-to-many)
CREATE TABLE IF NOT EXISTS AdUserGroups (
    UserId INT NOT NULL,
    GroupId INT NOT NULL,
    AddedAt DATETIME(6) NOT NULL,
    PRIMARY KEY (UserId, GroupId),
    INDEX IX_AdUserGroups_GroupId (GroupId),
    CONSTRAINT FK_AdUserGroups_AdUsers_UserId FOREIGN KEY (UserId) REFERENCES AdUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AdUserGroups_AdGroups_GroupId FOREIGN KEY (GroupId) REFERENCES AdGroups(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица за Windows Events
CREATE TABLE IF NOT EXISTS WindowsEvents (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    MachineName VARCHAR(255) NOT NULL,
    Username VARCHAR(255) NULL,
    EventId INT NOT NULL,
    LogName VARCHAR(255) NOT NULL,
    ProviderName VARCHAR(255) NULL,
    Level VARCHAR(50) NULL,
    Message VARCHAR(4000) NULL,
    EventTime DATETIME(6) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    AdUserId INT NULL,
    INDEX IX_WindowsEvents_MachineName_EventTime (MachineName, EventTime),
    INDEX IX_WindowsEvents_EventId (EventId),
    INDEX IX_WindowsEvents_AdUserId (AdUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Foreign keys за всички таблици към AdUsers
ALTER TABLE UserActivities ADD CONSTRAINT FK_UserActivities_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE ApplicationEvents ADD CONSTRAINT FK_ApplicationEvents_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE FileActivities ADD CONSTRAINT FK_FileActivities_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE NetworkActivities ADD CONSTRAINT FK_NetworkActivities_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE UsbDevices ADD CONSTRAINT FK_UsbDevices_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE ScreenTimes ADD CONSTRAINT FK_ScreenTimes_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE AuthSessions ADD CONSTRAINT FK_AuthSessions_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;
ALTER TABLE WindowsEvents ADD CONSTRAINT FK_WindowsEvents_AdUsers_AdUserId FOREIGN KEY (AdUserId) REFERENCES AdUsers(Id) ON DELETE SET NULL;

