using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase40Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "PasswordResetTokens" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "TokenHash" TEXT NOT NULL,
                    "ExpiresAt" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UsedAt" TEXT NULL,
                    CONSTRAINT "FK_PasswordResetTokens_AspNetUsers_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_PasswordResetTokens_TokenHash"
                    ON "PasswordResetTokens" ("TokenHash");
                CREATE INDEX IF NOT EXISTS "IX_PasswordResetTokens_UserId_CreatedAt"
                    ON "PasswordResetTokens" ("UserId", "CreatedAt");
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.PasswordResetTokens (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
                        UserId uniqueidentifier NOT NULL,
                        TokenHash nvarchar(88) NOT NULL,
                        ExpiresAt datetimeoffset NOT NULL,
                        CreatedAt datetimeoffset NOT NULL,
                        UsedAt datetimeoffset NULL,
                        CONSTRAINT FK_PasswordResetTokens_AspNetUsers_UserId
                            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IX_PasswordResetTokens_TokenHash ON dbo.PasswordResetTokens (TokenHash);
                    CREATE INDEX IX_PasswordResetTokens_UserId_CreatedAt ON dbo.PasswordResetTokens (UserId, CreatedAt);
                END
                """,
                cancellationToken);
        }
    }
}
