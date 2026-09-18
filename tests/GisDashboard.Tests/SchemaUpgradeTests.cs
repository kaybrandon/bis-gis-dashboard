using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Tests;

public sealed class SchemaUpgradeTests
{
    [Fact]
    public void Phase51_is_applied_before_Phase42_in_SchemaUpgrade()
    {
        var source = File.ReadAllText(FindSchemaUpgradeSource());
        var phase51 = source.IndexOf("await Phase51Schema.ApplyAsync", StringComparison.Ordinal);
        var phase42 = source.IndexOf("await Phase42Schema.ApplyAsync", StringComparison.Ordinal);

        phase51.Should().BeGreaterThan(-1, "Phase51 must still run");
        phase42.Should().BeGreaterThan(-1, "Phase42 must still run");
        phase51.Should().BeLessThan(phase42, "Phase51 ALTERs AspNetUsers before Phase42 SELECTs Users");
        source.Split("await Phase51Schema.ApplyAsync", StringSplitOptions.None).Length.Should().Be(2);
        source.IndexOf("await Phase52Schema.ApplyAsync", StringComparison.Ordinal).Should().BeGreaterThan(phase42);
        var phase53 = source.IndexOf("await Phase53Schema.ApplyAsync", StringComparison.Ordinal);
        var phase31 = source.IndexOf("await Phase31Schema.ApplyAsync", StringComparison.Ordinal);
        phase53.Should().BeGreaterThan(-1, "Phase53 must still run");
        phase31.Should().BeGreaterThan(-1, "Phase31 must still run");
        phase53.Should().BeLessThan(phase31, "Phase53 ALTERs Organizations before Phase31 SELECTs orgs");
        source.Split("await Phase53Schema.ApplyAsync", StringSplitOptions.None).Length.Should().Be(2);
        var phase54 = source.IndexOf("await Phase54Schema.ApplyAsync", StringComparison.Ordinal);
        var phase52 = source.IndexOf("await Phase52Schema.ApplyAsync", StringComparison.Ordinal);
        phase54.Should().BeGreaterThan(-1, "Phase54 must still run");
        phase52.Should().BeGreaterThan(-1, "Phase52 must still run");
        phase54.Should().BeGreaterThan(phase52, "Phase54 ALTERs WorkItems after prior phases");
        source.Split("await Phase54Schema.ApplyAsync", StringSplitOptions.None).Length.Should().Be(2);
    }

    [Fact]
    public async Task SchemaUpgrade_adds_missing_user_columns_before_loading_users()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gis-schema-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;

            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Users.Add(new ApplicationUser
                {
                    Id = SeedIds.TokenUploadUser,
                    UserName = "upload-token@gisdashboard.local",
                    NormalizedUserName = "UPLOAD-TOKEN@GISDASHBOARD.LOCAL",
                    Email = "upload-token@gisdashboard.local",
                    NormalizedEmail = "UPLOAD-TOKEN@GISDASHBOARD.LOCAL",
                    DisplayName = "Token upload",
                    IsActive = false,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    CreatedAt = DateTimeOffset.UtcNow
                });
                db.Users.Add(new ApplicationUser
                {
                    Id = Guid.Parse("dddddddd-0000-0000-0000-00000000aa01"),
                    UserName = "alex.rivera@bisconsultants.local",
                    NormalizedUserName = "ALEX.RIVERA@BISCONSULTANTS.LOCAL",
                    Email = "alex.rivera@bisconsultants.local",
                    NormalizedEmail = "ALEX.RIVERA@BISCONSULTANTS.LOCAL",
                    DisplayName = "Alex Rivera",
                    FullName = "Alex Rivera",
                    IsActive = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();

                await db.Database.ExecuteSqlRawAsync("""
                    DROP INDEX IF EXISTS "IX_AspNetUsers_IsArchived";
                    DROP INDEX IF EXISTS "IX_AspNetUsers_LastLoginAt";
                    ALTER TABLE "AspNetUsers" DROP COLUMN "JobTitle";
                    ALTER TABLE "AspNetUsers" DROP COLUMN "IsArchived";
                    ALTER TABLE "AspNetUsers" DROP COLUMN "ArchivedAt";
                    ALTER TABLE "AspNetUsers" DROP COLUMN "LastLoginAt";
                    """);

                var loadBeforeUpgrade = async () => await db.Users.AsNoTracking().ToListAsync();
                await loadBeforeUpgrade.Should().ThrowAsync<SqliteException>()
                    .Where(ex => ex.Message.Contains("JobTitle", StringComparison.OrdinalIgnoreCase)
                                 || ex.Message.Contains("IsArchived", StringComparison.OrdinalIgnoreCase)
                                 || ex.Message.Contains("ArchivedAt", StringComparison.OrdinalIgnoreCase)
                                 || ex.Message.Contains("LastLoginAt", StringComparison.OrdinalIgnoreCase));

                await SchemaUpgrade.ApplyAsync(db);

                var users = await db.Users.AsNoTracking().ToListAsync();
                users.Should().Contain(x => x.Id == SeedIds.TokenUploadUser);
                users.Should().Contain(x => x.Email == "alex.rivera@bisconsultants.local");
                ColumnNames(db, "AspNetUsers").Should().Contain(["JobTitle", "IsArchived", "ArchivedAt", "LastLoginAt"]);
                ColumnNames(db, "Organizations").Should().Contain(["IsArchived", "ArchivedAt"]);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SchemaUpgrade_adds_missing_organization_archive_columns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gis-schema-org-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Organizations.Add(new Organization
            {
                Id = SeedIds.DemoClient,
                Name = "Demo Client",
                Code = "DEMOCLIENT",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX IF EXISTS "IX_Organizations_IsArchived";
                ALTER TABLE "Organizations" DROP COLUMN "IsArchived";
                ALTER TABLE "Organizations" DROP COLUMN "ArchivedAt";
                """);

            var loadBeforeUpgrade = async () => await db.Organizations.AsNoTracking().ToListAsync();
            await loadBeforeUpgrade.Should().ThrowAsync<SqliteException>()
                .Where(ex => ex.Message.Contains("IsArchived", StringComparison.OrdinalIgnoreCase)
                             || ex.Message.Contains("ArchivedAt", StringComparison.OrdinalIgnoreCase));

            await SchemaUpgrade.ApplyAsync(db);

            var orgs = await db.Organizations.AsNoTracking().ToListAsync();
            orgs.Should().Contain(x => x.Id == SeedIds.DemoClient && !x.IsArchived);
            ColumnNames(db, "Organizations").Should().Contain(["IsArchived", "ArchivedAt"]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SchemaUpgrade_adds_missing_difficulty_columns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gis-schema-diff-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.ChangeTracker.Clear();

            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX IF EXISTS "IX_WorkItems_DifficultyBand";
                ALTER TABLE "WorkItems" DROP COLUMN "DifficultyBand";
                ALTER TABLE "WorkItems" DROP COLUMN "DifficultyWhy";
                ALTER TABLE "WorkItems" DROP COLUMN "DifficultyOverridden";
                ALTER TABLE "WorkItems" DROP COLUMN "AiDifficultyBand";
                ALTER TABLE "WorkItems" DROP COLUMN "AiDifficultyWhy";
                ALTER TABLE "WorkItems" DROP COLUMN "DifficultyOverriddenAt";
                ALTER TABLE "WorkItems" DROP COLUMN "DifficultyOverriddenByUserId";
                """);

            await SchemaUpgrade.ApplyAsync(db);

            ColumnNames(db, "WorkItems").Should().Contain([
                "DifficultyBand",
                "DifficultyWhy",
                "DifficultyOverridden",
                "AiDifficultyBand",
                "AiDifficultyWhy",
                "DifficultyOverriddenAt",
                "DifficultyOverriddenByUserId"
            ]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static List<string> ColumnNames(AppDbContext db, string table)
    {
        var names = new List<string>();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        var shouldClose = command.Connection?.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            db.Database.OpenConnection();
        }

        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(1));
            }
        }
        finally
        {
            if (shouldClose)
            {
                db.Database.CloseConnection();
            }
        }

        return names;
    }

    private static string FindSchemaUpgradeSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "GisDashboard.Infrastructure",
                "Persistence",
                "SchemaUpgrade.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate SchemaUpgrade.cs from the test output directory.");
    }
}
