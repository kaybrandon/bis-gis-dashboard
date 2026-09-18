using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// GIS-UI-10 — document difficulty (Easy / Medium / Hard) from the AI-fill pass.
/// Safe to run on every startup. EnsureCreated does not add columns to existing DBs.
/// </summary>
public static class Phase54Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "DifficultyBand", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "DifficultyWhy", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "DifficultyOverridden", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiDifficultyBand", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiDifficultyWhy", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "DifficultyOverriddenAt", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "DifficultyOverriddenByUserId", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_WorkItems_DifficultyBand"
                ON "WorkItems" ("DifficultyBand");
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.WorkItems', 'DifficultyBand') IS NULL
                    ALTER TABLE dbo.WorkItems ADD DifficultyBand nvarchar(16) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'DifficultyWhy') IS NULL
                    ALTER TABLE dbo.WorkItems ADD DifficultyWhy nvarchar(1000) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'DifficultyOverridden') IS NULL
                    ALTER TABLE dbo.WorkItems ADD DifficultyOverridden bit NOT NULL CONSTRAINT DF_WorkItems_DifficultyOverridden DEFAULT 0;
                IF COL_LENGTH('dbo.WorkItems', 'AiDifficultyBand') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiDifficultyBand nvarchar(16) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'AiDifficultyWhy') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiDifficultyWhy nvarchar(1000) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'DifficultyOverriddenAt') IS NULL
                    ALTER TABLE dbo.WorkItems ADD DifficultyOverriddenAt datetimeoffset NULL;
                IF COL_LENGTH('dbo.WorkItems', 'DifficultyOverriddenByUserId') IS NULL
                    ALTER TABLE dbo.WorkItems ADD DifficultyOverriddenByUserId uniqueidentifier NULL;
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItems_DifficultyBand' AND object_id = OBJECT_ID(N'dbo.WorkItems'))
                    CREATE INDEX IX_WorkItems_DifficultyBand ON dbo.WorkItems (DifficultyBand);
                """,
                cancellationToken);
        }
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

        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}",
            cancellationToken);
    }
}
