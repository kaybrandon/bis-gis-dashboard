using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase36Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "IsPriority", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityNote", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityRequestedAt", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityRequestedByUserId", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityAcknowledgedAt", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityAcknowledgedByUserId", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "OrganizationTechs" (
                    "OrganizationId" TEXT NOT NULL,
                    "UserId" TEXT NOT NULL,
                    CONSTRAINT "PK_OrganizationTechs" PRIMARY KEY ("OrganizationId", "UserId"),
                    CONSTRAINT "FK_OrganizationTechs_Organizations" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_OrganizationTechs_Users" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "Notifications" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "OrganizationId" TEXT NOT NULL,
                    "WorkItemId" TEXT NULL,
                    "Kind" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Body" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "CreatedAtSort" INTEGER NOT NULL,
                    "ReadAt" TEXT NULL,
                    CONSTRAINT "FK_Notifications_Users" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId_CreatedAtSort"
                ON "Notifications" ("UserId", "CreatedAtSort");
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
            IF COL_LENGTH('dbo.WorkItems', 'IsPriority') IS NULL
                ALTER TABLE dbo.WorkItems ADD IsPriority bit NOT NULL CONSTRAINT DF_WorkItems_IsPriority DEFAULT 0;
            IF COL_LENGTH('dbo.WorkItems', 'PriorityNote') IS NULL
                ALTER TABLE dbo.WorkItems ADD PriorityNote nvarchar(500) NULL;
            IF COL_LENGTH('dbo.WorkItems', 'PriorityRequestedAt') IS NULL
                ALTER TABLE dbo.WorkItems ADD PriorityRequestedAt datetimeoffset NULL;
            IF COL_LENGTH('dbo.WorkItems', 'PriorityRequestedByUserId') IS NULL
                ALTER TABLE dbo.WorkItems ADD PriorityRequestedByUserId uniqueidentifier NULL;
            IF COL_LENGTH('dbo.WorkItems', 'PriorityAcknowledgedAt') IS NULL
                ALTER TABLE dbo.WorkItems ADD PriorityAcknowledgedAt datetimeoffset NULL;
            IF COL_LENGTH('dbo.WorkItems', 'PriorityAcknowledgedByUserId') IS NULL
                ALTER TABLE dbo.WorkItems ADD PriorityAcknowledgedByUserId uniqueidentifier NULL;
            IF OBJECT_ID(N'dbo.OrganizationTechs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.OrganizationTechs (
                    OrganizationId uniqueidentifier NOT NULL,
                    UserId uniqueidentifier NOT NULL,
                    CONSTRAINT PK_OrganizationTechs PRIMARY KEY (OrganizationId, UserId),
                    CONSTRAINT FK_OrganizationTechs_Organizations FOREIGN KEY (OrganizationId) REFERENCES dbo.Organizations (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_OrganizationTechs_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
                );
            END
            IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Notifications (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
                    UserId uniqueidentifier NOT NULL,
                    OrganizationId uniqueidentifier NOT NULL,
                    WorkItemId uniqueidentifier NULL,
                    Kind nvarchar(40) NOT NULL,
                    Title nvarchar(200) NOT NULL,
                    Body nvarchar(500) NOT NULL,
                    CreatedAt datetimeoffset NOT NULL,
                    CreatedAtSort bigint NOT NULL,
                    ReadAt datetimeoffset NULL,
                    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_Notifications_UserId_CreatedAtSort ON dbo.Notifications (UserId, CreatedAtSort);
            END
            """,
            cancellationToken);
    }

    private static async Task EnsureSqliteColumnAsync(
        AppDbContext db,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        var names = new List<string>();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        var shouldClose = command.Connection?.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                names.Add(reader.GetString(1));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await db.Database.CloseConnectionAsync();
            }
        }

        if (names.Any(name => string.Equals(name, column, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}", cancellationToken);
    }
}
