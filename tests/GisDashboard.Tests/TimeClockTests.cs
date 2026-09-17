using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class TimeClockTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TimeClockTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Editor_can_search_work_items_and_log_clock_time()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canLogTime").GetBoolean().Should().BeTrue();

        var search = await (await client.GetAsync("/api/work-items?search=N-14-042&pageSize=8&sortBy=updatedAt")).ReadJsonAsync();
        search.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Should().Contain(SeedIds.DemoPlat);

        var created = await client.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries", new
        {
            hours = 0,
            minutes = 12,
            note = "Clock 9:00 AM–9:12 AM"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await created.ReadJsonAsync();
        json.GetProperty("minutes").GetInt32().Should().Be(12);
        json.GetProperty("workItemId").GetGuid().Should().Be(SeedIds.DemoPlat);
        json.GetProperty("loggedByName").GetString().Should().Be("Alex Rivera");

        var report = await (await client.GetAsync("/api/time-report?from=2026-09-01&to=2026-09-30&bucket=month")).ReadJsonAsync();
        report.GetProperty("entries").EnumerateArray()
            .Select(x => x.GetProperty("note").GetString())
            .Should().Contain("Clock 9:00 AM–9:12 AM");
        report.GetProperty("byWorkItem").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.DemoPlat);
    }

    [Fact]
    public async Task Viewer_cannot_use_the_clock_api()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canLogTime").GetBoolean().Should().BeFalse();

        var created = await client.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries", new
        {
            hours = 0,
            minutes = 5,
            note = "Clock"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Settings_marks_floating_clock_enabled()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        settings.GetProperty("features").GetProperty("floatingTimeClock").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }
}
