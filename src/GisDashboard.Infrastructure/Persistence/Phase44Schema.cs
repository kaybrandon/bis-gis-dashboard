using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase44Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "UserPresence" (
                    "UserId" TEXT NOT NULL CONSTRAINT "PK_UserPresence" PRIMARY KEY,
                    "LastSeen" TEXT NOT NULL,
                    "LastSeenSort" INTEGER NOT NULL,
                    "Route" TEXT NOT NULL,
                    "WorkItemId" TEXT NULL,
                    "ClockedIn" INTEGER NOT NULL DEFAULT 0,
                    "ClockWorkItemId" TEXT NULL,
                    CONSTRAINT "FK_UserPresence_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_UserPresence_LastSeenSort"
                ON "UserPresence" ("LastSeenSort");
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
            IF OBJECT_ID(N'dbo.UserPresence', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.UserPresence (
                    UserId uniqueidentifier NOT NULL CONSTRAINT PK_UserPresence PRIMARY KEY,
                    LastSeen datetimeoffset NOT NULL,
                    LastSeenSort bigint NOT NULL,
                    Route nvarchar(200) NOT NULL,
                    WorkItemId uniqueidentifier NULL,
                    ClockedIn bit NOT NULL CONSTRAINT DF_UserPresence_ClockedIn DEFAULT 0,
                    ClockWorkItemId uniqueidentifier NULL,
                    CONSTRAINT FK_UserPresence_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_UserPresence_LastSeenSort ON dbo.UserPresence (LastSeenSort);
            END
            """,
            cancellationToken);
    }
}
