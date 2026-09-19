using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        files[0].GetProperty("sourcePath").GetString().Should().Be("workfiles/orgs/democlient/shapefiles");

        var agents = await (await client.GetAsync("/api/lan-connections")).ReadJsonAsync();
        agents[0].GetProperty("bisFolder").GetString().Should().Be("workfiles/orgs/democlient/shapefiles");
        agents[0].GetProperty("remoteFolder").GetString().Should().Be(@"C:\GIS\Outgoing");
        agents[0].GetProperty("heartbeatLabel").GetString().Should().Be("None — agent not enrolled or not running");
        agents[0].GetProperty("lastHeartbeatAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
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
        json.GetProperty("destination").GetProperty("message").GetString().Should().Contain("on ");
        Directory.Delete(dest, true);
    }

    [Fact]
    public async Task Check_folders_treats_workfiles_orgs_as_azure_not_local()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dest = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"gis-dest-{Guid.NewGuid():N}")).FullName;
        var response = await client.PostAsJsonAsync("/api/connections/check-folders", new
        {
            sourcePath = "workfiles/orgs/democlient/shapefiles",
            remoteFolder = dest
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("source").GetProperty("kind").GetString().Should().Be("azure");
        json.GetProperty("source").GetProperty("message").GetString().Should().Contain("workfiles/orgs/democlient/shapefiles");
        json.GetProperty("source").GetProperty("message").GetString().Should().NotContain("file server");
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
            bisFolder = "workfiles/orgs/democlient/empty-sync",
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
            sourcePath = "workfiles/orgs/democlient/empty-sync",
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
        check.GetProperty("destination").GetProperty("message").GetString().Should().Contain("on ");
        check.GetProperty("bothPassed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Workfiles_orgs_path_persists_as_azure_on_save()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var patch = await client.PatchAsJsonAsync($"/api/lan-connections/{SeedIds.DemoLanConnection}", new
        {
            bisFolder = "workfiles/orgs/democlient/shapefiles",
            remoteFolder = @"C:\GIS\Outgoing",
            direction = "Bidirectional"
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await patch.ReadJsonAsync();
        saved.GetProperty("bisFolder").GetString().Should().Be("workfiles/orgs/democlient/shapefiles");
        saved.GetProperty("remoteFolder").GetString().Should().Be(@"C:\GIS\Outgoing");

        var listed = await (await client.GetAsync("/api/lan-connections")).ReadJsonAsync();
        var demo = listed.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == SeedIds.DemoLanConnection);
        demo.GetProperty("bisFolder").GetString().Should().Be("workfiles/orgs/democlient/shapefiles");

        var bareOrgs = await client.PatchAsJsonAsync($"/api/lan-connections/{SeedIds.DemoLanConnection}", new
        {
            bisFolder = "/orgs/democlient/shapefiles"
        });
        (await bareOrgs.ReadJsonAsync()).GetProperty("bisFolder").GetString().Should().Be("workfiles/orgs/democlient/shapefiles");
    }

    [Fact]
    public async Task Agent_destination_exists_passes_check_even_when_api_host_cannot_see_path()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await client.PostAsJsonAsync("/api/lan-connections", new
        {
            organizationId = SeedIds.OtherClient,
            bisFolder = "workfiles/orgs/otherclient/shapefiles",
            remoteFolder = @"C:\GIS\Outgoing",
            direction = "Bidirectional"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var agent = await created.ReadJsonAsync();
        var token = agent.GetProperty("enrollToken").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        var heartbeat = await _factory.CreateClient().PostAsJsonAsync("/api/lan-connections/agent/heartbeat", new
        {
            enrollToken = token,
            machineName = "BRANDON-PC",
            windowsUserName = @"BIS\brandon",
            destinationExists = true,
            sourceExists = true,
            localFileCount = 3
        });
        heartbeat.StatusCode.Should().Be(HttpStatusCode.OK);
        var live = await heartbeat.ReadJsonAsync();
        live.GetProperty("lastHeartbeatAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
        live.GetProperty("heartbeatFresh").GetBoolean().Should().BeTrue();
        live.GetProperty("heartbeatOk").GetBoolean().Should().BeTrue();
        live.GetProperty("windowsUserName").GetString().Should().Be(@"BIS\brandon");
        live.GetProperty("heartbeatLabel").GetString().Should().NotBeNullOrWhiteSpace();
        live.GetProperty("heartbeatLabel").GetString().Should().NotBe("—");

        var check = await (await client.PostAsync($"/api/lan-connections/{agent.GetProperty("id").GetGuid()}/check-folders", null)).ReadJsonAsync();
        check.GetProperty("destination").GetProperty("result").GetString().Should().Be("Pass");
        check.GetProperty("destination").GetProperty("message").GetString().Should().Contain(@"C:\GIS\Outgoing");
        check.GetProperty("destination").GetProperty("message").GetString().Should().Contain("BRANDON-PC");
        check.GetProperty("destination").GetProperty("message").GetString().Should().Contain(@"BIS\brandon");
        check.GetProperty("source").GetProperty("kind").GetString().Should().Be("azure");
        check.GetProperty("bothPassed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Enrolled_agent_without_heartbeat_does_not_claim_path_not_found_on_api_host()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await client.PostAsJsonAsync("/api/lan-connections", new
        {
            organizationId = SeedIds.OtherClient,
            bisFolder = "workfiles/orgs/otherclient/shapefiles",
            remoteFolder = @"C:\GIS\Outgoing",
            direction = "Bidirectional"
        });
        var createdJson = await created.ReadJsonAsync();
        var id = createdJson.GetProperty("id").GetGuid();
        var token = createdJson.GetProperty("enrollToken").GetString();

        await _factory.CreateClient().PostAsJsonAsync("/api/lan-connections/agent/heartbeat", new
        {
            enrollToken = token,
            machineName = "BRANDON-PC",
            windowsUserName = @"BIS\brandon"
        });

        var run = await (await client.PostAsync($"/api/lan-connections/{id}/run-now", null)).ReadJsonAsync();
        run.GetProperty("lastErrorCode").GetString().Should().Be("CHECK_NOT_ON_AGENT");
        run.GetProperty("lastError").GetString().Should().Contain("BRANDON-PC");
        run.GetProperty("lastError").GetString().Should().Contain("BIS\\brandon");
        run.GetProperty("lastError").GetString().Should().NotContain("[PATH_NOT_FOUND]");
        run.GetProperty("heartbeatLabel").GetString().Should().NotBe("—");
        run.GetProperty("lastHeartbeatAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task File_servers_and_connections_survive_null_string_columns()
    {
        var nullServerId = Guid.Parse("12121212-0000-0000-0000-000000000099");
        var nullConnId = Guid.Parse("12121212-0000-0000-0000-000000000098");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FileServers.Add(new FileServer
            {
                Id = nullServerId,
                Name = null,
                RootPath = null,
                CreatedAt = DateTimeOffset.UtcNow
            });
            db.FileConnections.Add(new FileConnection
            {
                Id = nullConnId,
                OrganizationId = SeedIds.OtherClient,
                FileServerId = nullServerId,
                SourcePath = null,
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE FileServers SET Name = NULL, RootPath = NULL WHERE Id = {0};
                UPDATE FileConnections SET SourcePath = NULL WHERE Id = {1};
                """,
                nullServerId.ToString(),
                nullConnId.ToString());
        }

        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        (await client.GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        var serversResponse = await client.GetAsync("/api/file-servers");
        serversResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var servers = await serversResponse.ReadJsonAsync();
        servers.GetArrayLength().Should().BeGreaterThan(1);

        var demo = servers.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.LocalFileServer);
        demo.GetProperty("name").GetString().Should().Be("Local files");
        demo.GetProperty("rootPath").GetString().Should().Be("workfiles");
        demo.GetProperty("sourceRoot").GetString().Should().Be("workfiles");

        var broken = servers.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == nullServerId);
        broken.GetProperty("name").GetString().Should().BeEmpty();
        broken.GetProperty("rootPath").GetString().Should().BeEmpty();
        broken.GetProperty("sourceRoot").GetString().Should().BeEmpty();

        var filesResponse = await client.GetAsync("/api/connections");
        filesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var files = await filesResponse.ReadJsonAsync();
        files.GetArrayLength().Should().BeGreaterThan(1);

        var demoConn = files.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoFileConnection);
        demoConn.GetProperty("sourcePath").GetString().Should().Be("workfiles/orgs/democlient/shapefiles");
        demoConn.GetProperty("fileServerRoot").GetString().Should().Be("workfiles");

        var brokenConn = files.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == nullConnId);
        brokenConn.GetProperty("sourcePath").GetString().Should().BeEmpty();
        brokenConn.GetProperty("fileServerId").GetGuid().Should().Be(nullServerId);
        (brokenConn.GetProperty("sourceRoot").GetString() ?? string.Empty).Should().BeEmpty();
        (brokenConn.GetProperty("fileServerRoot").GetString() ?? string.Empty).Should().BeEmpty();
    }
}
