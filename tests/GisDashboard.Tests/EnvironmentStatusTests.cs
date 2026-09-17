using System.Net;
using FluentAssertions;

namespace GisDashboard.Tests;

public sealed class EnvironmentStatusTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EnvironmentStatusTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Global_admin_sees_live_database_and_storage_checks()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.GetAsync("/api/admin/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Password=");
        body.Should().NotContain("AccountKey=");
        body.Should().NotContain("SharedAccessSignature=");

        var json = await response.ReadJsonAsync();
        json.GetProperty("overall").GetString().Should().Be("ok");
        json.GetProperty("checkedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        json.GetProperty("note").GetString().Should().Contain("Live checks");

        var checks = json.GetProperty("checks").EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString()!);
        checks.Should().ContainKeys("api", "database", "storage", "keyVault", "appInsights", "email", "azureOpenAI");

        checks["api"].GetProperty("status").GetString().Should().Be("ok");
        checks["api"].GetProperty("mode").GetString().Should().Be("live");
        checks["api"].GetProperty("detail").GetString().Should().Contain("Process is up");

        checks["database"].GetProperty("status").GetString().Should().Be("ok");
        checks["database"].GetProperty("mode").GetString().Should().Be("live");
        checks["database"].GetProperty("detail").GetString().Should().Contain("SELECT 1");
        checks["database"].GetProperty("detail").GetString().Should().Contain("SQLite");

        checks["storage"].GetProperty("status").GetString().Should().Be("ok");
        checks["storage"].GetProperty("mode").GetString().Should().Be("live");
        checks["storage"].GetProperty("detail").GetString().Should().Contain("Local");

        checks["keyVault"].GetProperty("status").GetString().Should().Be("not_configured");
        checks["keyVault"].GetProperty("mode").GetString().Should().Be("configured");
        checks["appInsights"].GetProperty("mode").GetString().Should().Be("configured");
        checks["email"].GetProperty("mode").GetString().Should().Be("configured");
        checks["azureOpenAI"].GetProperty("status").GetString().Should().Be("not_configured");
        checks["azureOpenAI"].GetProperty("mode").GetString().Should().Be("configured");
    }

    [Fact]
    public async Task Org_admin_and_editor_cannot_read_environment_status()
    {
        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        (await orgAdmin.GetAsync("/api/admin/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.GetAsync("/api/admin/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync("/api/admin/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_status_is_unauthorized()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/admin/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
