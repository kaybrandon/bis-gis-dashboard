using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase46Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "FileServers" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_FileServers" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "RootPath" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "FileConnections" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_FileConnections" PRIMARY KEY,
                    "OrganizationId" TEXT NOT NULL,
                    "FileServerId" TEXT NULL,
                    "SourcePath" TEXT NOT NULL,
                    "FtpFolder" TEXT NULL,
                    "FtpUrl" TEXT NULL,
                    "FtpUserName" TEXT NULL,
                    "FtpPasswordProtected" TEXT NULL,
                    "Enabled" INTEGER NOT NULL DEFAULT 1,
                    "Status" TEXT NULL,
                    "LastError" TEXT NULL,
                    "LastPublishedAt" TEXT NULL,
                    "LastFileCount" INTEGER NOT NULL DEFAULT 0,
                    "LastZipName" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_FileConnections_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_FileConnections_FileServers_FileServerId" FOREIGN KEY ("FileServerId") REFERENCES "FileServers" ("Id") ON DELETE SET NULL
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "LanConnections" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_LanConnections" PRIMARY KEY,
                    "OrganizationId" TEXT NOT NULL,
                    "RemoteFolder" TEXT NOT NULL,
                    "BisFolder" TEXT NOT NULL,
                    "Direction" TEXT NOT NULL,
                    "ScheduleMinutes" INTEGER NOT NULL DEFAULT 15,
                    "EnrollTokenHash" TEXT NOT NULL,
                    "EnrollTokenMasked" TEXT NOT NULL,
                    "Enrolled" INTEGER NOT NULL DEFAULT 0,
                    "Status" TEXT NOT NULL,
                    "HeartbeatOk" INTEGER NOT NULL DEFAULT 0,
                    "LastHeartbeatAt" TEXT NULL,
                    "LastSyncAt" TEXT NULL,
                    "LastPullCount" INTEGER NOT NULL DEFAULT 0,
                    "LastPushCount" INTEGER NOT NULL DEFAULT 0,
                    "LastError" TEXT NULL,
                    "LastErrorCode" TEXT NULL,
                    "LastErrorAt" TEXT NULL,
                    "MachineName" TEXT NULL,
                    "LocalIp" TEXT NULL,
                    "PublicIp" TEXT NULL,
                    "AgentVersion" TEXT NULL,
                    "HostName" TEXT NULL,
                    "OsDescription" TEXT NULL,
                    "OsVersion" TEXT NULL,
                    "Arch" TEXT NULL,
                    "RuntimeVersion" TEXT NULL,
                    "FreeDiskBytes" INTEGER NULL,
                    "SourceExistsOnAgent" INTEGER NOT NULL DEFAULT 0,
                    "DestinationExistsOnAgent" INTEGER NOT NULL DEFAULT 0,
                    "AgentLocalFileCount" INTEGER NOT NULL DEFAULT 0,
                    "RunNowQueued" INTEGER NOT NULL DEFAULT 0,
                    "RestartPending" INTEGER NOT NULL DEFAULT 0,
                    "UpdatePending" INTEGER NOT NULL DEFAULT 0,
                    "RestartResult" TEXT NULL,
                    "UpdateStatus" TEXT NULL,
                    "UpdateTargetVersion" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_LanConnections_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "SyncControl" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_SyncControl" PRIMARY KEY,
                    "Paused" INTEGER NOT NULL DEFAULT 0,
                    "Message" TEXT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_FileConnections_OrganizationId" ON "FileConnections" ("OrganizationId");
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_LanConnections_OrganizationId" ON "LanConnections" ("OrganizationId");
                """,
                cancellationToken);
            return;
        }

        if (!db.Database.IsSqlServer())
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.FileServers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.FileServers (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_FileServers PRIMARY KEY,
                    Name nvarchar(200) NOT NULL,
                    RootPath nvarchar(1024) NOT NULL,
                    CreatedAt datetimeoffset NOT NULL
                );
            END
            IF OBJECT_ID(N'dbo.FileConnections', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.FileConnections (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_FileConnections PRIMARY KEY,
                    OrganizationId uniqueidentifier NOT NULL,
                    FileServerId uniqueidentifier NULL,
                    SourcePath nvarchar(1024) NOT NULL,
                    FtpFolder nvarchar(260) NULL,
                    FtpUrl nvarchar(500) NULL,
                    FtpUserName nvarchar(200) NULL,
                    FtpPasswordProtected nvarchar(4000) NULL,
                    Enabled bit NOT NULL CONSTRAINT DF_FileConnections_Enabled DEFAULT 1,
                    Status nvarchar(40) NULL,
                    LastError nvarchar(2000) NULL,
                    LastPublishedAt datetimeoffset NULL,
                    LastFileCount int NOT NULL CONSTRAINT DF_FileConnections_LastFileCount DEFAULT 0,
                    LastZipName nvarchar(260) NULL,
                    CreatedAt datetimeoffset NOT NULL,
                    UpdatedAt datetimeoffset NOT NULL,
                    CONSTRAINT FK_FileConnections_Organizations_OrganizationId FOREIGN KEY (OrganizationId) REFERENCES dbo.Organizations (Id),
                    CONSTRAINT FK_FileConnections_FileServers_FileServerId FOREIGN KEY (FileServerId) REFERENCES dbo.FileServers (Id)
                );
                CREATE INDEX IX_FileConnections_OrganizationId ON dbo.FileConnections (OrganizationId);
            END
            IF OBJECT_ID(N'dbo.LanConnections', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LanConnections (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_LanConnections PRIMARY KEY,
                    OrganizationId uniqueidentifier NOT NULL,
                    RemoteFolder nvarchar(1024) NOT NULL,
                    BisFolder nvarchar(1024) NOT NULL,
                    Direction nvarchar(20) NOT NULL,
                    ScheduleMinutes int NOT NULL CONSTRAINT DF_LanConnections_ScheduleMinutes DEFAULT 15,
                    EnrollTokenHash nvarchar(88) NOT NULL,
                    EnrollTokenMasked nvarchar(40) NOT NULL,
                    Enrolled bit NOT NULL CONSTRAINT DF_LanConnections_Enrolled DEFAULT 0,
                    Status nvarchar(40) NOT NULL,
                    HeartbeatOk bit NOT NULL CONSTRAINT DF_LanConnections_HeartbeatOk DEFAULT 0,
                    LastHeartbeatAt datetimeoffset NULL,
                    LastSyncAt datetimeoffset NULL,
                    LastPullCount int NOT NULL CONSTRAINT DF_LanConnections_LastPullCount DEFAULT 0,
                    LastPushCount int NOT NULL CONSTRAINT DF_LanConnections_LastPushCount DEFAULT 0,
                    LastError nvarchar(2000) NULL,
                    LastErrorCode nvarchar(80) NULL,
                    LastErrorAt datetimeoffset NULL,
                    MachineName nvarchar(200) NULL,
                    LocalIp nvarchar(80) NULL,
                    PublicIp nvarchar(80) NULL,
                    AgentVersion nvarchar(80) NULL,
                    HostName nvarchar(200) NULL,
                    OsDescription nvarchar(300) NULL,
                    OsVersion nvarchar(80) NULL,
                    Arch nvarchar(40) NULL,
                    RuntimeVersion nvarchar(80) NULL,
                    FreeDiskBytes bigint NULL,
                    SourceExistsOnAgent bit NOT NULL CONSTRAINT DF_LanConnections_SourceExists DEFAULT 0,
                    DestinationExistsOnAgent bit NOT NULL CONSTRAINT DF_LanConnections_DestExists DEFAULT 0,
                    AgentLocalFileCount int NOT NULL CONSTRAINT DF_LanConnections_LocalFiles DEFAULT 0,
                    RunNowQueued bit NOT NULL CONSTRAINT DF_LanConnections_RunNow DEFAULT 0,
                    RestartPending bit NOT NULL CONSTRAINT DF_LanConnections_Restart DEFAULT 0,
                    UpdatePending bit NOT NULL CONSTRAINT DF_LanConnections_Update DEFAULT 0,
                    RestartResult nvarchar(200) NULL,
                    UpdateStatus nvarchar(200) NULL,
                    UpdateTargetVersion nvarchar(80) NULL,
                    CreatedAt datetimeoffset NOT NULL,
                    UpdatedAt datetimeoffset NOT NULL,
                    CONSTRAINT FK_LanConnections_Organizations_OrganizationId FOREIGN KEY (OrganizationId) REFERENCES dbo.Organizations (Id)
                );
                CREATE INDEX IX_LanConnections_OrganizationId ON dbo.LanConnections (OrganizationId);
            END
            IF OBJECT_ID(N'dbo.SyncControl', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SyncControl (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_SyncControl PRIMARY KEY,
                    Paused bit NOT NULL CONSTRAINT DF_SyncControl_Paused DEFAULT 0,
                    Message nvarchar(500) NULL,
                    UpdatedAt datetimeoffset NOT NULL
                );
            END
            """,
            cancellationToken);
    }
}
