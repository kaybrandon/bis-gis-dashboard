using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase31Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "Organizations", "UploadToken", "TEXT NULL", cancellationToken);
            await EnsureSqliteColumnAsync(db, "Organizations", "UploadTokenCreatedAt", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Organizations_UploadToken"
                ON "Organizations" ("UploadToken");
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            // SQL Server compiles the whole batch before ALTER TABLE is visible, so the
            // filtered unique index cannot live in the same ExecuteSqlRaw as the column add.
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.Organizations', 'UploadToken') IS NULL
                    ALTER TABLE dbo.Organizations ADD UploadToken nvarchar(80) NULL;
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.Organizations', 'UploadTokenCreatedAt') IS NULL
                    ALTER TABLE dbo.Organizations ADD UploadTokenCreatedAt datetimeoffset NULL;
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Organizations_UploadToken' AND object_id = OBJECT_ID(N'dbo.Organizations'))
                    CREATE UNIQUE INDEX IX_Organizations_UploadToken ON dbo.Organizations (UploadToken) WHERE UploadToken IS NOT NULL;
                """,
                cancellationToken);
        }

        await EnsureTokenUploadUserAsync(db, cancellationToken);
        await EnsureOrgTokensAsync(db, cancellationToken);
    }

    private static async Task EnsureTokenUploadUserAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.Id == SeedIds.TokenUploadUser, cancellationToken))
        {
            return;
        }

        db.Users.Add(new ApplicationUser
        {
            Id = SeedIds.TokenUploadUser,
            UserName = "upload-token@gisdashboard.local",
            NormalizedUserName = "UPLOAD-TOKEN@GISDASHBOARD.LOCAL",
            Email = "upload-token@gisdashboard.local",
            NormalizedEmail = "UPLOAD-TOKEN@GISDASHBOARD.LOCAL",
            EmailConfirmed = true,
            DisplayName = "Token upload",
            IsActive = false,
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddYears(100),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureOrgTokensAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var missing = await db.Organizations
            .Where(x => x.UploadToken == null || x.UploadToken == "")
            .ToListAsync(cancellationToken);
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var org in missing)
        {
            org.UploadToken = Security.UploadTokens.Create();
            org.UploadTokenCreatedAt = DateTimeOffset.UtcNow;
        }

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
