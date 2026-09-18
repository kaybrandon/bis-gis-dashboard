using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// GIS-UI-09 — soft-archive organizations. Safe to run on every startup.
/// Existing Azure SQL / SQLite files need this because EnsureCreated does not add columns.
/// </summary>
public static class Phase53Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "Organizations", "IsArchived", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "Organizations", "ArchivedAt", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Organizations_IsArchived"
                ON "Organizations" ("IsArchived");
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.Organizations', 'IsArchived') IS NULL
                    ALTER TABLE dbo.Organizations ADD IsArchived bit NOT NULL CONSTRAINT DF_Organizations_IsArchived DEFAULT 0;
                IF COL_LENGTH('dbo.Organizations', 'ArchivedAt') IS NULL
                    ALTER TABLE dbo.Organizations ADD ArchivedAt datetimeoffset NULL;
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Organizations_IsArchived' AND object_id = OBJECT_ID(N'dbo.Organizations'))
                    CREATE INDEX IX_Organizations_IsArchived ON dbo.Organizations (IsArchived);
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
