using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// GIS Users — last successful login, optional job title, and archive soft-delete.
/// Safe to run on every startup. Existing Azure SQL / SQLite files need this
/// because EnsureCreated does not add columns.
/// </summary>
public static class Phase51Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "JobTitle", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "IsArchived", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "ArchivedAt", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "LastLoginAt", "TEXT NULL", cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.AspNetUsers', 'JobTitle') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD JobTitle nvarchar(200) NULL;
                IF COL_LENGTH('dbo.AspNetUsers', 'IsArchived') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD IsArchived bit NOT NULL CONSTRAINT DF_AspNetUsers_IsArchived DEFAULT 0;
                IF COL_LENGTH('dbo.AspNetUsers', 'ArchivedAt') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD ArchivedAt datetimeoffset NULL;
                IF COL_LENGTH('dbo.AspNetUsers', 'LastLoginAt') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD LastLoginAt datetimeoffset NULL;
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
