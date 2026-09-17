using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase37Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "IsReviewed", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.WorkItems', 'IsReviewed') IS NULL
                    ALTER TABLE dbo.WorkItems ADD IsReviewed bit NOT NULL CONSTRAINT DF_WorkItems_IsReviewed DEFAULT 0;
                """,
                cancellationToken);
        }

        if (await db.DocumentTypes.AnyAsync(cancellationToken)
            && !await db.DocumentTypes.AnyAsync(x => x.Id == SeedIds.TypeSubdivision, cancellationToken))
        {
            db.DocumentTypes.Add(new DocumentType
            {
                Id = SeedIds.TypeSubdivision,
                Name = "Subdivision",
                SortOrder = 4
            });
        }

        if (await db.WorkItemStatuses.AnyAsync(cancellationToken)
            && !await db.WorkItemStatuses.AnyAsync(x => x.Id == SeedIds.StatusCancelled, cancellationToken))
        {
            db.WorkItemStatuses.Add(new WorkItemStatus
            {
                Id = SeedIds.StatusCancelled,
                Name = "Cancelled",
                Color = "#8c8c8c",
                SortOrder = 6
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var deed = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == SeedIds.TypeDeed, cancellationToken);
        var plat = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == SeedIds.TypePlat, cancellationToken);
        var survey = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == SeedIds.TypeSurvey, cancellationToken);
        var subdivision = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == SeedIds.TypeSubdivision, cancellationToken);
        var other = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Id == SeedIds.TypeOther, cancellationToken);
        if (deed is not null) deed.SortOrder = 1;
        if (plat is not null) plat.SortOrder = 2;
        if (survey is not null) survey.SortOrder = 3;
        if (subdivision is not null) subdivision.SortOrder = 4;
        if (other is not null) other.SortOrder = 5;
        await db.SaveChangesAsync(cancellationToken);
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
