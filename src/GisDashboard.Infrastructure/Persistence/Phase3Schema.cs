using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase3Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "IsSplit", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "IsSketch", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "WorkedOn", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "WorkedOnSort", "INTEGER NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "FirstDeadline", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "FirstDeadlineSort", "INTEGER NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "FinalDeadline", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "FinalDeadlineSort", "INTEGER NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AnnexationCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "CorrectionCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "DeedCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PlatCount", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PropertyIds", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "WorkItemComments" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkItemComments" PRIMARY KEY,
                    "WorkItemId" TEXT NOT NULL,
                    "AuthorUserId" TEXT NOT NULL,
                    "Body" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "CreatedAtSort" INTEGER NOT NULL,
                    CONSTRAINT "FK_WorkItemComments_WorkItems_WorkItemId" FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_WorkItemComments_AspNetUsers_AuthorUserId" FOREIGN KEY ("AuthorUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_WorkItemComments_WorkItemId_CreatedAtSort"
                ON "WorkItemComments" ("WorkItemId", "CreatedAtSort");
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
            IF COL_LENGTH('dbo.WorkItems', 'IsSplit') IS NULL ALTER TABLE dbo.WorkItems ADD IsSplit bit NOT NULL CONSTRAINT DF_WorkItems_IsSplit DEFAULT(0);
            IF COL_LENGTH('dbo.WorkItems', 'IsSketch') IS NULL ALTER TABLE dbo.WorkItems ADD IsSketch bit NOT NULL CONSTRAINT DF_WorkItems_IsSketch DEFAULT(0);
            IF COL_LENGTH('dbo.WorkItems', 'WorkedOn') IS NULL ALTER TABLE dbo.WorkItems ADD WorkedOn datetimeoffset NULL;
            IF COL_LENGTH('dbo.WorkItems', 'WorkedOnSort') IS NULL ALTER TABLE dbo.WorkItems ADD WorkedOnSort bigint NULL;
            IF COL_LENGTH('dbo.WorkItems', 'FirstDeadline') IS NULL ALTER TABLE dbo.WorkItems ADD FirstDeadline datetimeoffset NULL;
            IF COL_LENGTH('dbo.WorkItems', 'FirstDeadlineSort') IS NULL ALTER TABLE dbo.WorkItems ADD FirstDeadlineSort bigint NULL;
            IF COL_LENGTH('dbo.WorkItems', 'FinalDeadline') IS NULL ALTER TABLE dbo.WorkItems ADD FinalDeadline datetimeoffset NULL;
            IF COL_LENGTH('dbo.WorkItems', 'FinalDeadlineSort') IS NULL ALTER TABLE dbo.WorkItems ADD FinalDeadlineSort bigint NULL;
            IF COL_LENGTH('dbo.WorkItems', 'AnnexationCount') IS NULL ALTER TABLE dbo.WorkItems ADD AnnexationCount int NOT NULL CONSTRAINT DF_WorkItems_AnnexationCount DEFAULT(0);
            IF COL_LENGTH('dbo.WorkItems', 'CorrectionCount') IS NULL ALTER TABLE dbo.WorkItems ADD CorrectionCount int NOT NULL CONSTRAINT DF_WorkItems_CorrectionCount DEFAULT(0);
            IF COL_LENGTH('dbo.WorkItems', 'DeedCount') IS NULL ALTER TABLE dbo.WorkItems ADD DeedCount int NOT NULL CONSTRAINT DF_WorkItems_DeedCount DEFAULT(0);
            IF COL_LENGTH('dbo.WorkItems', 'PlatCount') IS NULL ALTER TABLE dbo.WorkItems ADD PlatCount int NOT NULL CONSTRAINT DF_WorkItems_PlatCount DEFAULT(0);
            IF COL_LENGTH('dbo.WorkItems', 'PropertyIds') IS NULL ALTER TABLE dbo.WorkItems ADD PropertyIds nvarchar(4000) NULL;
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.WorkItemComments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.WorkItemComments (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_WorkItemComments PRIMARY KEY,
                    WorkItemId uniqueidentifier NOT NULL,
                    AuthorUserId uniqueidentifier NOT NULL,
                    Body nvarchar(2000) NOT NULL,
                    CreatedAt datetimeoffset NOT NULL,
                    CreatedAtSort bigint NOT NULL,
                    CONSTRAINT FK_WorkItemComments_WorkItems_WorkItemId FOREIGN KEY (WorkItemId) REFERENCES dbo.WorkItems (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_WorkItemComments_AspNetUsers_AuthorUserId FOREIGN KEY (AuthorUserId) REFERENCES dbo.AspNetUsers (Id)
                );
                CREATE INDEX IX_WorkItemComments_WorkItemId_CreatedAtSort ON dbo.WorkItemComments (WorkItemId, CreatedAtSort);
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
