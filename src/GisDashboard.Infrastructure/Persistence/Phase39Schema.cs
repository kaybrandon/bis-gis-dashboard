using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase39Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "EmailSettings" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_EmailSettings" PRIMARY KEY,
                    "Enabled" INTEGER NOT NULL,
                    "Host" TEXT NULL,
                    "Port" INTEGER NOT NULL,
                    "UseSsl" INTEGER NOT NULL,
                    "FromAddress" TEXT NULL,
                    "FromName" TEXT NULL,
                    "UserName" TEXT NULL,
                    "ReplyTo" TEXT NULL,
                    "PasswordProtected" TEXT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "UpdatedByUserId" TEXT NULL
                );
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.EmailSettings', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.EmailSettings (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_EmailSettings PRIMARY KEY,
                        Enabled bit NOT NULL,
                        Host nvarchar(200) NULL,
                        Port int NOT NULL,
                        UseSsl bit NOT NULL,
                        FromAddress nvarchar(200) NULL,
                        FromName nvarchar(200) NULL,
                        UserName nvarchar(200) NULL,
                        ReplyTo nvarchar(200) NULL,
                        PasswordProtected nvarchar(max) NULL,
                        UpdatedAt datetimeoffset NOT NULL,
                        UpdatedByUserId uniqueidentifier NULL
                    );
                END
                """,
                cancellationToken);
        }
    }
}
