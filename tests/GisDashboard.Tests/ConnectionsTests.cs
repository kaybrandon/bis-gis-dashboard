using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class ConnectionsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ConnectionsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Viewer_cannot_see_connections()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync("/api/connections")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync("/api/lan-connections")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Global_admin_lists_demo_client_azure_source()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canSeeConnections").GetBoolean().Should().BeTrue();
        me.GetProperty("canManageConnections").GetBoolean().Should().BeTrue();

        var files = await (await client.GetAsync("/api/connections")).ReadJsonAsync();
        files.GetArrayLength().Should().BeGreaterThan(0);
        files[0].GetProperty("sourcePath").GetString().Should().Be("/orgs/democlient/shapefiles");

        var agents = await (await client.GetAsync("/api/lan-connections")).ReadJsonAsync();
        agents[0].GetProperty("bisFolder").GetString().Should().Be("/orgs/democlient/shapefiles");
        agents[0].GetProperty("remoteFolder").GetString().Should().Be(@"C:\GIS\Outgoing");
    }

    [Fact]
    public async Task Check_folders_treats_orgs_path_as_azure_not_file_server()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dest = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"gis-dest-{Guid.NewGuid():N}")).FullName;
        var response = await client.PostAsJsonAsync("/api/connections/check-folders", new
        {
            sourcePath = "/orgs/democlient/shapefiles",
            remoteFolder = dest
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("source").GetProperty("kind").GetString().Should().Be("azure");
        json.GetProperty("source").GetProperty("message").GetString().Should().NotContain("file server");
        json.GetProperty("source").GetProperty("result").GetString().Should().BeOneOf("Pass", "Fail");
        json.GetProperty("destination").GetProperty("result").GetString().Should().Be("Pass");
        Directory.Delete(dest, true);
    }

    [Fact]
    public async Task Bidirectional_run_now_uploads_local_files_to_empty_azure_prefix()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dest = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"gis-out-{Guid.NewGuid():N}")).FullName;
        await File.WriteAllTextAsync(Path.Combine(dest, "parcel.shp"), "shapefile");

        var created = await client.PostAsJsonAsync("/api/lan-connections", new
        {
            organizationId = SeedIds.DemoClient,
            bisFolder = "/orgs/democlient/empty-sync",
            remoteFolder = dest,
            direction = "Bidirectional",
            scheduleMinutes = 15
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var agent = await created.ReadJsonAsync();
        var id = agent.GetProperty("id").GetGuid();

        var run = await client.PostAsync($"/api/lan-connections/{id}/run-now", null);
        run.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await run.ReadJsonAsync();
        result.GetProperty("lastError").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        result.GetProperty("status").GetString().Should().NotBe("Error");
        result.GetProperty("lastPullCount").GetInt32().Should().BeGreaterThan(0);

        var check = await (await client.PostAsJsonAsync("/api/connections/check-folders", new
        {
            sourcePath = "/orgs/democlient/empty-sync",
            remoteFolder = dest
        })).ReadJsonAsync();
        check.GetProperty("source").GetProperty("result").GetString().Should().Be("Pass");
        check.GetProperty("source").GetProperty("message").GetString().Should().Contain("1 file");
        Directory.Delete(dest, true);
    }

    [Fact]
    public async Task Missing_local_destination_is_visible_error()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await client.PostAsJsonAsync("/api/lan-connections", new
        {
            organizationId = SeedIds.DemoClient,
            bisFolder = "/orgs/democlient/shapefiles",
            remoteFolder = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"),
            direction = "Bidirectional"
        });
        var id = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();
        var run = await (await client.PostAsync($"/api/lan-connections/{id}/run-now", null)).ReadJsonAsync();
        run.GetProperty("status").GetString().Should().Be("Error");
        run.GetProperty("lastError").GetString().Should().Contain("not found");
        run.GetProperty("lastErrorCode").GetString().Should().Be("PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Check_folders_pass_fail_on_saved_demo_connection()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var check = await (await client.PostAsync($"/api/lan-connections/{SeedIds.DemoLanConnection}/check-folders", null)).ReadJsonAsync();
        check.GetProperty("source").GetProperty("kind").GetString().Should().Be("azure");
        check.GetProperty("destination").GetProperty("result").GetString().Should().Be("Fail");
        check.GetProperty("destination").GetProperty("message").GetString().Should().Contain("C:\\GIS\\Outgoing");
        check.GetProperty("bothPassed").GetBoolean().Should().BeFalse();
    }
}
