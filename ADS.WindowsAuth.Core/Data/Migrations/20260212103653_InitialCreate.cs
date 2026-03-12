using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ADS.WindowsAuth.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DistinguishedName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DistinguishedName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastLogon = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPasswordChange = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountExpires = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    LoginTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LoginMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdUserId = table.Column<int>(type: "int", nullable: true),
                    LogonType = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitorConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ServiceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequireVpn = table.Column<bool>(type: "bit", nullable: false),
                    VpnCheckInterval = table.Column<int>(type: "int", nullable: false),
                    VpnGateways = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VpnProcessNames = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OfflineMode = table.Column<bool>(type: "bit", nullable: false),
                    OfflineDataRetention = table.Column<int>(type: "int", nullable: false),
                    ConnectionTimeout = table.Column<int>(type: "int", nullable: false),
                    RetryInterval = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitorConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BlockedWebsitesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockedApplicationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockedFileExtensionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetMachinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetUsersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxScreenTimeSeconds = table.Column<int>(type: "int", nullable: false),
                    AllowedInstallationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockedInstallationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlockUsbAccess = table.Column<bool>(type: "bit", nullable: false),
                    BlockPrinterAccess = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ProcessorCount = table.Column<int>(type: "int", nullable: true),
                    TotalMemory = table.Column<long>(type: "bigint", nullable: true),
                    WorkingSet = table.Column<long>(type: "bigint", nullable: true),
                    UptimeSeconds = table.Column<double>(type: "float", nullable: true),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdUserGroups",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdUserGroups", x => new { x.UserId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_AdUserGroups_AdGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AdGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdUserGroups_AdUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ApplicationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExecutablePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationEvents_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WindowsUsername = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthSessions_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FileActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    ApplicationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileActivities_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NetworkActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    InterfaceName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    InterfaceDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Speed = table.Column<long>(type: "bigint", nullable: true),
                    BytesReceived = table.Column<long>(type: "bigint", nullable: true),
                    BytesSent = table.Column<long>(type: "bigint", nullable: true),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkActivities_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScreenTimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Seconds = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenTimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenTimes_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsbDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsbDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsbDevices_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScreenTimeSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivities_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WindowsEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    LogName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowsEvents_AdUsers_AdUserId",
                        column: x => x.AdUserId,
                        principalTable: "AdUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdGroups_DistinguishedName",
                table: "AdGroups",
                column: "DistinguishedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdGroups_GroupName",
                table: "AdGroups",
                column: "GroupName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdUserGroups_GroupId",
                table: "AdUserGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AdUsers_DistinguishedName",
                table: "AdUsers",
                column: "DistinguishedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdUsers_Username",
                table: "AdUsers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_AdUserId",
                table: "ApplicationEvents",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_ApplicationName",
                table: "ApplicationEvents",
                column: "ApplicationName");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_Username_MachineName_EventTime",
                table: "ApplicationEvents",
                columns: new[] { "Username", "MachineName", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_AccessToken",
                table: "AuthSessions",
                column: "AccessToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_AdUserId",
                table: "AuthSessions",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_SessionId",
                table: "AuthSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_WindowsUsername_MachineName",
                table: "AuthSessions",
                columns: new[] { "WindowsUsername", "MachineName" });

            migrationBuilder.CreateIndex(
                name: "IX_FileActivities_AdUserId",
                table: "FileActivities",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileActivities_Username_MachineName_EventTime",
                table: "FileActivities",
                columns: new[] { "Username", "MachineName", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_Level",
                table: "LogEntries",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_MachineName_Timestamp",
                table: "LogEntries",
                columns: new[] { "MachineName", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEntries_Username_Timestamp",
                table: "LogEntries",
                columns: new[] { "Username", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginEvents_LoginMethod",
                table: "LoginEvents",
                column: "LoginMethod");

            migrationBuilder.CreateIndex(
                name: "IX_LoginEvents_Username_MachineName_LoginTime",
                table: "LoginEvents",
                columns: new[] { "Username", "MachineName", "LoginTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitorConfigurations_MachineName",
                table: "MonitorConfigurations",
                column: "MachineName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkActivities_AdUserId",
                table: "NetworkActivities",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkActivities_Username_MachineName_EventTime",
                table: "NetworkActivities",
                columns: new[] { "Username", "MachineName", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenTimes_AdUserId",
                table: "ScreenTimes",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenTimes_Username_MachineName_RecordedAt",
                table: "ScreenTimes",
                columns: new[] { "Username", "MachineName", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemInfos_MachineName_EventTime",
                table: "SystemInfos",
                columns: new[] { "MachineName", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UsbDevices_AdUserId",
                table: "UsbDevices",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsbDevices_Username_MachineName_EventTime",
                table: "UsbDevices",
                columns: new[] { "Username", "MachineName", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_AdUserId",
                table: "UserActivities",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_Username_MachineName_StartTime",
                table: "UserActivities",
                columns: new[] { "Username", "MachineName", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WindowsEvents_AdUserId",
                table: "WindowsEvents",
                column: "AdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WindowsEvents_EventId",
                table: "WindowsEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_WindowsEvents_MachineName_EventTime",
                table: "WindowsEvents",
                columns: new[] { "MachineName", "EventTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdUserGroups");

            migrationBuilder.DropTable(
                name: "ApplicationEvents");

            migrationBuilder.DropTable(
                name: "AuthSessions");

            migrationBuilder.DropTable(
                name: "FileActivities");

            migrationBuilder.DropTable(
                name: "LogEntries");

            migrationBuilder.DropTable(
                name: "LoginEvents");

            migrationBuilder.DropTable(
                name: "MonitorConfigurations");

            migrationBuilder.DropTable(
                name: "NetworkActivities");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "ScreenTimes");

            migrationBuilder.DropTable(
                name: "SystemInfos");

            migrationBuilder.DropTable(
                name: "UsbDevices");

            migrationBuilder.DropTable(
                name: "UserActivities");

            migrationBuilder.DropTable(
                name: "WindowsEvents");

            migrationBuilder.DropTable(
                name: "AdGroups");

            migrationBuilder.DropTable(
                name: "AdUsers");
        }
    }
}
