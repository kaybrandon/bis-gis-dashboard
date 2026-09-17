using GisDashboard.Application.Connections;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase47Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "LanConnections", "WindowsUserName", "TEXT NULL", cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.LanConnections', 'WindowsUserName') IS NULL
                    ALTER TABLE dbo.LanConnections ADD WindowsUserName nvarchar(200) NULL;
                """,
                cancellationToken);
        }

        await CanonicalizeAzurePathsAsync(db, cancellationToken);
    }

    private static async Task CanonicalizeAzurePathsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var files = await db.FileConnections.ToListAsync(cancellationToken);
        foreach (var row in files)
        {
            var next = ConnectionPath.Canonicalize(row.SourcePath);
            if (!string.Equals(row.SourcePath, next, StringComparison.Ordinal))
            {
                row.SourcePath = next;
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var agents = await db.LanConnections.ToListAsync(cancellationToken);
        foreach (var row in agents)
        {
            var source = ConnectionPath.Canonicalize(row.BisFolder);
            var dest = ConnectionPath.Canonicalize(row.RemoteFolder);
            if (!string.Equals(row.BisFolder, source, StringComparison.Ordinal)
                || !string.Equals(row.RemoteFolder, dest, StringComparison.Ordinal))
            {
                row.BisFolder = source;
                row.RemoteFolder = dest;
                row.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
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
