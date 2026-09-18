using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// CR07 — floating clock is staff attendance / clock-in (presence), not a work-item timer.
/// Work-item hours stay on the Time log / time-entries API.
/// </summary>
public sealed class TimeClockTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TimeClockTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Work_item_time_log_stays_on_time_entries_not_attendance()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canLogTime").GetBoolean().Should().BeTrue();
        me.GetProperty("canSeePresence").GetBoolean().Should().BeTrue();

        var created = await client.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries", new
        {
            hours = 0,
            minutes = 12,
            note = "Manual time log 9:00 AM–9:12 AM"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await created.ReadJsonAsync();
        json.GetProperty("minutes").GetInt32().Should().Be(12);
        json.GetProperty("workItemId").GetGuid().Should().Be(SeedIds.DemoPlat);
        json.GetProperty("loggedByName").GetString().Should().Be("Alex Rivera");
    }

    [Fact]
    public async Task Viewer_cannot_log_work_item_time()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canLogTime").GetBoolean().Should().BeFalse();
        me.GetProperty("canSeePresence").GetBoolean().Should().BeFalse();

        var created = await client.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries", new
        {
            hours = 0,
            minutes = 5,
            note = "Clock"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Settings_mark_floating_clock_as_attendance_not_document_timer()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var clock = settings.GetProperty("features").GetProperty("floatingTimeClock");
        clock.GetProperty("enabled").GetBoolean().Should().BeTrue();
        var note = clock.GetProperty("note").GetString();
        note.Should().Contain("attendance");
        note.Should().Contain("Clock in");
        note.Should().Contain("not a work-item");
        note.Should().NotContain("logged on a GIS work item");

        var timeLogging = settings.GetProperty("features").GetProperty("timeLogging").GetProperty("note").GetString();
        timeLogging.Should().Contain("Time log");
        timeLogging.Should().NotContain("Floating clock start/stop");
    }

    [Fact]
    public async Task Attendance_clock_in_does_not_require_a_work_item()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PostAsJsonAsync("/api/presence", new
        {
            route = "/",
            workItemId = (Guid?)null,
            clockedIn = true,
            clockWorkItemId = (Guid?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var json = await (await client.GetAsync("/api/presence")).ReadJsonAsync();
        var self = json.GetProperty("items").EnumerateArray()
            .Single(x => Guid.Parse(x.GetProperty("userId").GetString()!) == SeedIds.EditorDemo);
        self.GetProperty("clockedIn").GetBoolean().Should().BeTrue();
        self.GetProperty("pageName").GetString().Should().Be("Dashboard");
        self.GetProperty("workItemId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }
}
