using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase38Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "FullName", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "WorkPhone", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "AvatarBlobPath", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "AspNetUsers", "AvatarContentType", "TEXT NULL", cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.AspNetUsers', 'FullName') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD FullName nvarchar(200) NULL;
                IF COL_LENGTH('dbo.AspNetUsers', 'WorkPhone') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD WorkPhone nvarchar(40) NULL;
                IF COL_LENGTH('dbo.AspNetUsers', 'AvatarBlobPath') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD AvatarBlobPath nvarchar(1024) NULL;
                IF COL_LENGTH('dbo.AspNetUsers', 'AvatarContentType') IS NULL
                    ALTER TABLE dbo.AspNetUsers ADD AvatarContentType nvarchar(100) NULL;
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

        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}", cancellationToken);
    }
}
