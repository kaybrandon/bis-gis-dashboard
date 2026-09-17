using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase32Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "MonthlyReports" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_MonthlyReports" PRIMARY KEY,
                    "OrganizationId" TEXT NOT NULL,
                    "Year" INTEGER NOT NULL,
                    "Month" INTEGER NOT NULL,
                    "Version" INTEGER NOT NULL,
                    "MonthLabel" TEXT NOT NULL,
                    "PeriodStart" TEXT NOT NULL,
                    "PeriodEnd" TEXT NOT NULL,
                    "GeneratedAt" TEXT NOT NULL,
                    "GeneratedByUserId" TEXT NOT NULL,
                    "SnapshotJson" TEXT NOT NULL,
                    "LastEmailedAt" TEXT NULL,
                    "LastEmailedTo" TEXT NULL,
                    "EmailCount" INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT "FK_MonthlyReports_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_MonthlyReports_AspNetUsers_GeneratedByUserId" FOREIGN KEY ("GeneratedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_MonthlyReports_OrganizationId_Year_Month_Version"
                ON "MonthlyReports" ("OrganizationId", "Year", "Month", "Version");
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "ReportEmailLogs" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_ReportEmailLogs" PRIMARY KEY,
                    "ReportId" TEXT NOT NULL,
                    "SentAt" TEXT NOT NULL,
                    "Recipients" TEXT NOT NULL,
                    "Subject" TEXT NOT NULL,
                    "Mode" TEXT NOT NULL,
                    "Delivered" INTEGER NOT NULL,
                    "Error" TEXT NULL,
                    "SentByUserId" TEXT NOT NULL,
                    CONSTRAINT "FK_ReportEmailLogs_MonthlyReports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "MonthlyReports" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_ReportEmailLogs_ReportId_SentAt"
                ON "ReportEmailLogs" ("ReportId", "SentAt");
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
            IF OBJECT_ID(N'dbo.MonthlyReports', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MonthlyReports (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_MonthlyReports PRIMARY KEY,
                    OrganizationId uniqueidentifier NOT NULL,
                    Year int NOT NULL,
                    Month int NOT NULL,
                    Version int NOT NULL,
                    MonthLabel nvarchar(40) NOT NULL,
                    PeriodStart datetimeoffset NOT NULL,
                    PeriodEnd datetimeoffset NOT NULL,
                    GeneratedAt datetimeoffset NOT NULL,
                    GeneratedByUserId uniqueidentifier NOT NULL,
                    SnapshotJson nvarchar(max) NOT NULL,
                    LastEmailedAt datetimeoffset NULL,
                    LastEmailedTo nvarchar(2000) NULL,
                    EmailCount int NOT NULL CONSTRAINT DF_MonthlyReports_EmailCount DEFAULT(0),
                    CONSTRAINT FK_MonthlyReports_Organizations_OrganizationId FOREIGN KEY (OrganizationId) REFERENCES dbo.Organizations (Id),
                    CONSTRAINT FK_MonthlyReports_AspNetUsers_GeneratedByUserId FOREIGN KEY (GeneratedByUserId) REFERENCES dbo.AspNetUsers (Id)
                );
                CREATE INDEX IX_MonthlyReports_OrganizationId_Year_Month_Version
                    ON dbo.MonthlyReports (OrganizationId, Year, Month, Version);
            END
            """,
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.ReportEmailLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ReportEmailLogs (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_ReportEmailLogs PRIMARY KEY,
                    ReportId uniqueidentifier NOT NULL,
                    SentAt datetimeoffset NOT NULL,
                    Recipients nvarchar(2000) NOT NULL,
                    Subject nvarchar(240) NOT NULL,
                    Mode nvarchar(20) NOT NULL,
                    Delivered bit NOT NULL,
                    Error nvarchar(1000) NULL,
                    SentByUserId uniqueidentifier NOT NULL,
                    CONSTRAINT FK_ReportEmailLogs_MonthlyReports_ReportId FOREIGN KEY (ReportId) REFERENCES dbo.MonthlyReports (Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_ReportEmailLogs_ReportId_SentAt ON dbo.ReportEmailLogs (ReportId, SentAt);
            END
            """,
            cancellationToken);
    }
}
