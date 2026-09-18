using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// P1 — auto AI-scan on upload. Safe to run on every startup.
/// EnsureCreated does not add columns to existing DBs.
/// </summary>
public static class Phase55Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiScanStatus", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiScanMessage", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiScanStartedAt", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiScanCompletedAt", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiScanResultJson", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AiScanBaselineJson", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_WorkItems_AiScanStatus"
                ON "WorkItems" ("AiScanStatus");
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.WorkItems', 'AiScanStatus') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiScanStatus nvarchar(32) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'AiScanMessage') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiScanMessage nvarchar(500) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'AiScanStartedAt') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiScanStartedAt datetimeoffset NULL;
                IF COL_LENGTH('dbo.WorkItems', 'AiScanCompletedAt') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiScanCompletedAt datetimeoffset NULL;
                IF COL_LENGTH('dbo.WorkItems', 'AiScanResultJson') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiScanResultJson nvarchar(max) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'AiScanBaselineJson') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AiScanBaselineJson nvarchar(max) NULL;
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WorkItems_AiScanStatus' AND object_id = OBJECT_ID(N'dbo.WorkItems'))
                    CREATE INDEX IX_WorkItems_AiScanStatus ON dbo.WorkItems (AiScanStatus);
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
