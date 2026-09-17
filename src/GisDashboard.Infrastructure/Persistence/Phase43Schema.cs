using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase43Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityNeededBy", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "PriorityNeededBySort", "INTEGER NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "OrganizationTechs", "IsPrimary", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.WorkItems', 'PriorityNeededBy') IS NULL
                    ALTER TABLE dbo.WorkItems ADD PriorityNeededBy datetimeoffset NULL;
                IF COL_LENGTH('dbo.WorkItems', 'PriorityNeededBySort') IS NULL
                    ALTER TABLE dbo.WorkItems ADD PriorityNeededBySort bigint NULL;
                IF COL_LENGTH('dbo.OrganizationTechs', 'IsPrimary') IS NULL
                    ALTER TABLE dbo.OrganizationTechs ADD IsPrimary bit NOT NULL CONSTRAINT DF_OrganizationTechs_IsPrimary DEFAULT 0;
                """,
                cancellationToken);
        }

        await BackfillPrimaryTechsAsync(db, cancellationToken);
    }

    private static async Task BackfillPrimaryTechsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var orgs = await db.OrganizationTechs
            .GroupBy(x => x.OrganizationId)
            .Where(g => !g.Any(x => x.IsPrimary))
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);
        foreach (var orgId in orgs)
        {
            var first = await db.OrganizationTechs
                .Where(x => x.OrganizationId == orgId)
                .OrderBy(x => x.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (first is not null)
            {
                first.IsPrimary = true;
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
