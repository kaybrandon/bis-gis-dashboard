using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase48Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteColumnAsync(db, "UserPresence", "NeedsHelp", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
            await EnsureSqliteColumnAsync(db, "UserPresence", "NeedsHelpAt", "TEXT NULL", cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "HelpMessages" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_HelpMessages" PRIMARY KEY,
                    "FromUserId" TEXT NOT NULL,
                    "ToUserId" TEXT NOT NULL,
                    "Chip" TEXT NULL,
                    "Body" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "CreatedAtSort" INTEGER NOT NULL,
                    "ReadAt" TEXT NULL,
                    CONSTRAINT "FK_HelpMessages_AspNetUsers_FromUserId"
                        FOREIGN KEY ("FromUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_HelpMessages_AspNetUsers_ToUserId"
                        FOREIGN KEY ("ToUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_HelpMessages_ToUserId_CreatedAtSort"
                    ON "HelpMessages" ("ToUserId", "CreatedAtSort");
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_HelpMessages_FromUserId_ToUserId_CreatedAtSort"
                    ON "HelpMessages" ("FromUserId", "ToUserId", "CreatedAtSort");
                """,
                cancellationToken);
            return;
        }

        if (!db.Database.IsSqlServer())
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('dbo.UserPresence', 'NeedsHelp') IS NULL
                ALTER TABLE dbo.UserPresence ADD NeedsHelp bit NOT NULL CONSTRAINT DF_UserPresence_NeedsHelp DEFAULT 0;
            IF COL_LENGTH('dbo.UserPresence', 'NeedsHelpAt') IS NULL
                ALTER TABLE dbo.UserPresence ADD NeedsHelpAt datetimeoffset NULL;
            """,
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'dbo.HelpMessages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.HelpMessages (
                    Id uniqueidentifier NOT NULL CONSTRAINT PK_HelpMessages PRIMARY KEY,
                    FromUserId uniqueidentifier NOT NULL,
                    ToUserId uniqueidentifier NOT NULL,
                    Chip nvarchar(40) NULL,
                    Body nvarchar(280) NOT NULL,
                    CreatedAt datetimeoffset NOT NULL,
                    CreatedAtSort bigint NOT NULL,
                    ReadAt datetimeoffset NULL,
                    CONSTRAINT FK_HelpMessages_AspNetUsers_FromUserId
                        FOREIGN KEY (FromUserId) REFERENCES dbo.AspNetUsers (Id),
                    CONSTRAINT FK_HelpMessages_AspNetUsers_ToUserId
                        FOREIGN KEY (ToUserId) REFERENCES dbo.AspNetUsers (Id)
                );
                CREATE INDEX IX_HelpMessages_ToUserId_CreatedAtSort
                    ON dbo.HelpMessages (ToUserId, CreatedAtSort);
                CREATE INDEX IX_HelpMessages_FromUserId_ToUserId_CreatedAtSort
                    ON dbo.HelpMessages (FromUserId, ToUserId, CreatedAtSort);
            END
            """,
            cancellationToken);
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
