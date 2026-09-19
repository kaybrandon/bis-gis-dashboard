using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

/// <summary>
/// QC4-06 — editable deed/plat data fields. Safe to run on every startup.
/// EnsureCreated does not add columns to existing DBs.
/// </summary>
public static class Phase56Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "WorkItems", "Survey", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "Abstract", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "LotBlock", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "Subdivision", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "LegalDescription", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "SurveyManual", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "AbstractManual", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "LotBlockManual", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "SubdivisionManual", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "WorkItems", "LegalDescriptionManual", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.WorkItems', 'Survey') IS NULL
                    ALTER TABLE dbo.WorkItems ADD Survey nvarchar(500) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'Abstract') IS NULL
                    ALTER TABLE dbo.WorkItems ADD Abstract nvarchar(500) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'LotBlock') IS NULL
                    ALTER TABLE dbo.WorkItems ADD LotBlock nvarchar(500) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'Subdivision') IS NULL
                    ALTER TABLE dbo.WorkItems ADD Subdivision nvarchar(500) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'LegalDescription') IS NULL
                    ALTER TABLE dbo.WorkItems ADD LegalDescription nvarchar(max) NULL;
                IF COL_LENGTH('dbo.WorkItems', 'SurveyManual') IS NULL
                    ALTER TABLE dbo.WorkItems ADD SurveyManual bit NOT NULL CONSTRAINT DF_WorkItems_SurveyManual DEFAULT 0;
                IF COL_LENGTH('dbo.WorkItems', 'AbstractManual') IS NULL
                    ALTER TABLE dbo.WorkItems ADD AbstractManual bit NOT NULL CONSTRAINT DF_WorkItems_AbstractManual DEFAULT 0;
                IF COL_LENGTH('dbo.WorkItems', 'LotBlockManual') IS NULL
                    ALTER TABLE dbo.WorkItems ADD LotBlockManual bit NOT NULL CONSTRAINT DF_WorkItems_LotBlockManual DEFAULT 0;
                IF COL_LENGTH('dbo.WorkItems', 'SubdivisionManual') IS NULL
                    ALTER TABLE dbo.WorkItems ADD SubdivisionManual bit NOT NULL CONSTRAINT DF_WorkItems_SubdivisionManual DEFAULT 0;
                IF COL_LENGTH('dbo.WorkItems', 'LegalDescriptionManual') IS NULL
                    ALTER TABLE dbo.WorkItems ADD LegalDescriptionManual bit NOT NULL CONSTRAINT DF_WorkItems_LegalDescriptionManual DEFAULT 0;
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
