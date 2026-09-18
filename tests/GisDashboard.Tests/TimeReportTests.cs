using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class TimeReportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TimeReportTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Editor_sees_only_own_hours()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canViewTimeReport").GetBoolean().Should().BeTrue();
        me.GetProperty("canViewTeamTimeReport").GetBoolean().Should().BeFalse();

        var json = await GetReportAsync(client);
        json.GetProperty("totalMinutes").GetInt32().Should().Be(90);
        json.GetProperty("totalLabel").GetString().Should().Be("1h 30m");
        json.GetProperty("peopleCount").GetInt32().Should().Be(1);
        json.GetProperty("byPerson").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("name").GetString() == "Alex Rivera");
        json.GetProperty("entries").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("loggedByName").GetString() == "Alex Rivera");
        foreach (var row in json.GetProperty("entries").EnumerateArray())
        {
            row.TryGetProperty("loggedByUserId", out _).Should().BeFalse();
        }
    }

    [Fact]
    public async Task Global_admin_sees_team_hours_regardless_of_toggle()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canViewTimeReport").GetBoolean().Should().BeTrue();
        me.GetProperty("canViewTeamTimeReport").GetBoolean().Should().BeTrue();

        var json = await GetReportAsync(client);
        json.GetProperty("canViewTeam").GetBoolean().Should().BeTrue();
        json.GetProperty("totalMinutes").GetInt32().Should().Be(135);
        json.GetProperty("totalLabel").GetString().Should().Be("2h 15m");
        json.GetProperty("peopleCount").GetInt32().Should().Be(2);
        json.GetProperty("byClient").EnumerateArray().Select(x => x.GetProperty("name").GetString())
            .Should().Equal("Demo Client");
        json.GetProperty("byPeriod").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Viewer_is_forbidden_when_client_toggle_is_off()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canViewTimeReport").GetBoolean().Should().BeFalse();
        me.GetProperty("canViewTeamTimeReport").GetBoolean().Should().BeFalse();

        var response = await client.GetAsync(ReportPath());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_sees_org_hours_when_toggle_is_on()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        await SetToggleAsync(admin, SeedIds.DemoClient, true);
        try
        {
            var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
            var me = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
            me.GetProperty("canViewTimeReport").GetBoolean().Should().BeTrue();
            me.GetProperty("canViewTeamTimeReport").GetBoolean().Should().BeTrue();

            var json = await GetReportAsync(viewer);
            json.GetProperty("totalMinutes").GetInt32().Should().Be(135);
            json.GetProperty("entries").GetArrayLength().Should().Be(2);
        }
        finally
        {
            await SetToggleAsync(admin, SeedIds.DemoClient, false);
        }
    }

    [Fact]
    public async Task Org_admin_without_toggle_sees_only_own_hours()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canViewTimeReport").GetBoolean().Should().BeTrue();
        me.GetProperty("canViewTeamTimeReport").GetBoolean().Should().BeFalse();

        var json = await GetReportAsync(client);
        json.GetProperty("totalMinutes").GetInt32().Should().Be(0);
        json.GetProperty("entries").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Org_admin_can_open_other_client_time_report()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        await SetToggleAsync(admin, SeedIds.DemoClient, true);
        try
        {
            var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
            var other = await orgAdmin.GetAsync($"{ReportPath()}&organizationId={SeedIds.OtherClient}");
            other.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await SetToggleAsync(admin, SeedIds.DemoClient, false);
        }
    }

    [Fact]
    public async Task Editor_can_open_other_org_time_report()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.GetAsync($"{ReportPath()}&organizationId={SeedIds.OtherClient}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Editor_cannot_filter_another_persons_hours()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.GetAsync($"{ReportPath()}&userId={SeedIds.Admin}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_global_admin_can_set_the_client_toggle()
    {
        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        var denied = await orgAdmin.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            timeReportCardsVisible = true
        });
        denied.StatusCode.Should().Be(HttpStatusCode.OK);
        (await denied.ReadJsonAsync()).GetProperty("timeReportCardsVisible").GetBoolean().Should().BeFalse();

        var global = await _factory.LoginAsync("admin@bisconsultants.local");
        var enabled = await SetToggleAsync(global, SeedIds.DemoClient, true);
        enabled.GetProperty("timeReportCardsVisible").GetBoolean().Should().BeTrue();
        await SetToggleAsync(global, SeedIds.DemoClient, false);
    }

    [Fact]
    public async Task Export_csv_lists_names_not_user_ids()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.GetAsync($"/api/time-report/export?from=2026-09-01&to=2026-09-30&bucket=month");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("Logged By");
        csv.Should().Contain("Alex Rivera");
        csv.Should().NotContain("arivera");
        csv.Should().Contain("West line bearing check");
        csv.Should().Contain("QC pass on bearings");
        csv.Should().NotContain(SeedIds.EditorDemo.ToString());
        csv.Should().NotContain(SeedIds.Admin.ToString());
    }

    [Fact]
    public async Task Invalid_bucket_is_rejected()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.GetAsync("/api/time-report?from=2026-09-01&to=2026-09-30&bucket=year");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string ReportPath() => "/api/time-report?from=2026-09-01&to=2026-09-30&bucket=month";

    private static async Task<System.Text.Json.JsonElement> GetReportAsync(HttpClient client)
    {
        var response = await client.GetAsync(ReportPath());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private static async Task<System.Text.Json.JsonElement> SetToggleAsync(HttpClient client, Guid organizationId, bool visible)
    {
        var response = await client.PutAsJsonAsync($"/api/admin/organizations/{organizationId}", new
        {
            name = organizationId == SeedIds.DemoClient ? "Demo Client" : "Other Client",
            code = organizationId == SeedIds.DemoClient ? "DEMOCLIENT" : "OTHERCLIENT",
            isActive = true,
            timeReportCardsVisible = visible
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }
}
