using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// Adds Phase 2 tables on existing Phase 1 databases. EnsureCreated does not alter
/// an already-created schema, so Azure SQL / existing SQLite files need this.
/// </summary>
public static class SchemaUpgrade
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "TimeEntries" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TimeEntries" PRIMARY KEY,
                    "WorkItemId" TEXT NOT NULL,
                    "LoggedByUserId" TEXT NOT NULL,
                    "Minutes" INTEGER NOT NULL,
                    "WorkedOn" TEXT NOT NULL,
                    "WorkedOnSort" INTEGER NOT NULL,
                    "Note" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "CreatedAtSort" INTEGER NOT NULL,
                    CONSTRAINT "FK_TimeEntries_WorkItems_WorkItemId" FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimeEntries_AspNetUsers_LoggedByUserId" FOREIGN KEY ("LoggedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_TimeEntries_WorkItemId_WorkedOnSort"
                ON "TimeEntries" ("WorkItemId", "WorkedOnSort");
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "InternalNoteRevisions" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_InternalNoteRevisions" PRIMARY KEY,
                    "WorkItemId" TEXT NOT NULL,
                    "Body" TEXT NOT NULL,
                    "EditedByUserId" TEXT NOT NULL,
                    "EditedAt" TEXT NOT NULL,
                    "EditedAtSort" INTEGER NOT NULL,
                    CONSTRAINT "FK_InternalNoteRevisions_WorkItems_WorkItemId" FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_InternalNoteRevisions_AspNetUsers_EditedByUserId" FOREIGN KEY ("EditedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_InternalNoteRevisions_WorkItemId_EditedAtSort"
                ON "InternalNoteRevisions" ("WorkItemId", "EditedAtSort");
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.TimeEntries', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.TimeEntries (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_TimeEntries PRIMARY KEY,
                        WorkItemId uniqueidentifier NOT NULL,
                        LoggedByUserId uniqueidentifier NOT NULL,
                        Minutes int NOT NULL,
                        WorkedOn datetimeoffset NOT NULL,
                        WorkedOnSort bigint NOT NULL,
                        Note nvarchar(500) NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        UpdatedAt datetimeoffset NOT NULL,
                        CreatedAtSort bigint NOT NULL,
                        CONSTRAINT FK_TimeEntries_WorkItems_WorkItemId FOREIGN KEY (WorkItemId) REFERENCES dbo.WorkItems (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_TimeEntries_AspNetUsers_LoggedByUserId FOREIGN KEY (LoggedByUserId) REFERENCES dbo.AspNetUsers (Id)
                    );
                    CREATE INDEX IX_TimeEntries_WorkItemId_WorkedOnSort ON dbo.TimeEntries (WorkItemId, WorkedOnSort);
                END
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.InternalNoteRevisions', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.InternalNoteRevisions (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_InternalNoteRevisions PRIMARY KEY,
                        WorkItemId uniqueidentifier NOT NULL,
                        Body nvarchar(4000) NOT NULL,
                        EditedByUserId uniqueidentifier NOT NULL,
                        EditedAt datetimeoffset NOT NULL,
                        EditedAtSort bigint NOT NULL,
                        CONSTRAINT FK_InternalNoteRevisions_WorkItems_WorkItemId FOREIGN KEY (WorkItemId) REFERENCES dbo.WorkItems (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_InternalNoteRevisions_AspNetUsers_EditedByUserId FOREIGN KEY (EditedByUserId) REFERENCES dbo.AspNetUsers (Id)
                    );
                    CREATE INDEX IX_InternalNoteRevisions_WorkItemId_EditedAtSort ON dbo.InternalNoteRevisions (WorkItemId, EditedAtSort);
                END
                """,
                cancellationToken);
        }

        await Phase3Schema.ApplyAsync(db, cancellationToken);
        await RoleModelUpgrade.ApplyAsync(db, cancellationToken);
        await Phase33Schema.ApplyAsync(db, cancellationToken);
        await Phase35Schema.ApplyAsync(db, cancellationToken);
        // Phase31 loads Organization via EF (SELECT IsArchived/ArchivedAt).
        await Phase53Schema.ApplyAsync(db, cancellationToken);
        await Phase31Schema.ApplyAsync(db, cancellationToken);
        await Phase32Schema.ApplyAsync(db, cancellationToken);
        await Phase34Schema.ApplyAsync(db, cancellationToken);
        await Phase36Schema.ApplyAsync(db, cancellationToken);
        await Phase37Schema.ApplyAsync(db, cancellationToken);
        await Phase38Schema.ApplyAsync(db, cancellationToken);
        // Phase42 loads ApplicationUser via EF (SELECT JobTitle/IsArchived/ArchivedAt/LastLoginAt).
        // Those columns must exist on existing Azure SQL before that query, or startup is 500.30.
        await Phase51Schema.ApplyAsync(db, cancellationToken);
        await Phase39Schema.ApplyAsync(db, cancellationToken);
        await Phase40Schema.ApplyAsync(db, cancellationToken);
        await Phase41Schema.ApplyAsync(db, cancellationToken);
        await Phase42Schema.ApplyAsync(db, cancellationToken);
        await Phase43Schema.ApplyAsync(db, cancellationToken);
        await Phase44Schema.ApplyAsync(db, cancellationToken);
        await Phase45Schema.ApplyAsync(db, cancellationToken);
        await Phase46Schema.ApplyAsync(db, cancellationToken);
        await Phase47Schema.ApplyAsync(db, cancellationToken);
        await Phase48Schema.ApplyAsync(db, cancellationToken);
        await Phase49Schema.ApplyAsync(db, cancellationToken);
        await Phase50Schema.ApplyAsync(db, cancellationToken);
        await Phase52Schema.ApplyAsync(db, cancellationToken);
        await Phase54Schema.ApplyAsync(db, cancellationToken);
    }
}
