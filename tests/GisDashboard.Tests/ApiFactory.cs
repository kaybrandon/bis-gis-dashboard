using GisDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"gis-{Guid.NewGuid():N}.db");
    private readonly string _files = Path.Combine(Path.GetTempPath(), $"gis-files-{Guid.NewGuid():N}");

    protected virtual void ExtraConfig(Dictionary<string, string?> config)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["ConnectionStrings:AzureStorage"] = "",
                ["LocalStorage:RootPath"] = _files,
                ["AzureStorage:Enabled"] = "false",
                ["Seed:Enabled"] = "true",
                ["Seed:Recreate"] = "true",
                ["Jwt:Key"] = "LOCAL-ONLY-DEV-KEY-CHANGE-IN-AZURE-KV!!",
                ["Jwt:Issuer"] = "gis-dashboard",
                ["Jwt:Audience"] = "gis-dashboard",
                ["Uploads:MaxFileBytes"] = "52428800",
                ["Uploads:Concurrency"] = "3"
            };
            ExtraConfig(values);
            config.AddInMemoryCollection(values);
        });
        builder.ConfigureServices(services =>
        {
            var options = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (options is not null)
            {
                services.Remove(options);
            }

            services.AddDbContext<AppDbContext>(db => db.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    public string DemoPassword => DemoSeed.DemoPassword;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            if (Directory.Exists(_files))
            {
                Directory.Delete(_files, true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
