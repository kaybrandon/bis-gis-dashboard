using GisDashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Persistence;

public static class Phase45Schema
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "CompanyContact" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_CompanyContact" PRIMARY KEY,
                    "Phone" TEXT NULL,
                    "Email" TEXT NULL,
                    "Address" TEXT NULL,
                    "Website" TEXT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    "UpdatedByUserId" TEXT NULL
                );
                """,
                cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM "CompanyContact"
                WHERE lower("Id") = 'ffffffff-0000-0000-0000-000000000002'
                  AND "Id" != 'FFFFFFFF-0000-0000-0000-000000000002';
                """,
                cancellationToken);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.CompanyContact', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.CompanyContact (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_CompanyContact PRIMARY KEY,
                        Phone nvarchar(40) NULL,
                        Email nvarchar(200) NULL,
                        Address nvarchar(300) NULL,
                        Website nvarchar(200) NULL,
                        UpdatedAt datetimeoffset NOT NULL,
                        UpdatedByUserId uniqueidentifier NULL
                    );
                END
                """,
                cancellationToken);
        }
        else
        {
            return;
        }

        if (await db.CompanyContacts.AnyAsync(x => x.Id == CompanyContact.SingletonId, cancellationToken))
        {
            return;
        }

        db.CompanyContacts.Add(new CompanyContact
        {
            Id = CompanyContact.SingletonId,
            Phone = CompanyContact.DefaultPhone,
            Email = CompanyContact.DefaultEmail,
            Address = CompanyContact.DefaultAddress,
            Website = CompanyContact.DefaultWebsite,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
