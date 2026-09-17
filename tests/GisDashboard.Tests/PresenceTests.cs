using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class PresenceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PresenceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_presence_is_unauthorized()
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/presence", Heartbeat("/"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/presence")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Viewer_can_heartbeat_but_cannot_list()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var post = await client.PostAsJsonAsync("/api/presence", Heartbeat("/documents"));
        post.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetAsync("/api/presence");
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canSeePresence").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Editor_sees_self_and_same_org_viewer_not_other_org_editor()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PostAsJsonAsync("/api/presence", Heartbeat("/upload-documents"))).EnsureSuccessStatusCode();

        var other = await _factory.LoginAsync("editor.other@bisconsultants.local");
        (await other.PostAsJsonAsync("/api/presence", Heartbeat("/reports"))).EnsureSuccessStatusCode();

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/settings"))).EnsureSuccessStatusCode();

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var me = await (await editor.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canSeePresence").GetBoolean().Should().BeTrue();

        var listed = await editor.GetAsync("/api/presence");
        var json = await listed.ReadJsonAsync();
        listed.IsSuccessStatusCode.Should().BeTrue($"{listed.StatusCode} {json}");
        var ids = UserIds(json);
        ids.Should().Contain(SeedIds.EditorDemo);
        ids.Should().Contain(SeedIds.ViewerDemo);
        ids.Should().Contain(SeedIds.Admin);
        ids.Should().NotContain(SeedIds.EditorOther);

        var self = Item(json, SeedIds.EditorDemo);
        self.GetProperty("presenceStatus").GetString().Should().Be("Online");
        self.GetProperty("pageName").GetString().Should().Be("Dashboard");
        self.GetProperty("clockedIn").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Global_admin_sees_every_active_user()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/documents"))).EnsureSuccessStatusCode();

        var other = await _factory.LoginAsync("editor.other@bisconsultants.local");
        (await other.PostAsJsonAsync("/api/presence", Heartbeat("/admin/organizations"))).EnsureSuccessStatusCode();

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/settings"))).EnsureSuccessStatusCode();

        var json = await (await admin.GetAsync("/api/presence")).ReadJsonAsync();
        var ids = UserIds(json);
        ids.Should().Contain(SeedIds.Admin);
        ids.Should().Contain(SeedIds.EditorDemo);
        ids.Should().Contain(SeedIds.EditorOther);

        Item(json, SeedIds.EditorOther).GetProperty("pageName").GetString().Should().Be("Organizations");
        Item(json, SeedIds.Admin).GetProperty("pageName").GetString().Should().Be("Settings");

        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();
        var afterStatus = await (await admin.GetAsync("/api/presence")).ReadJsonAsync();
        Item(afterStatus, SeedIds.Admin).GetProperty("pageName").GetString().Should().Be("Status");
    }

    [Fact]
    public async Task Heartbeat_on_work_item_includes_title_and_clock()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var route = $"/documents/{SeedIds.DemoPlat}";
        var response = await editor.PostAsJsonAsync("/api/presence", new
        {
            route,
            workItemId = SeedIds.DemoPlat,
            clockedIn = true,
            clockWorkItemId = SeedIds.DemoPlat
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var json = await (await editor.GetAsync("/api/presence")).ReadJsonAsync();
        var self = Item(json, SeedIds.EditorDemo);
        self.GetProperty("workItemId").GetString().Should().Be(SeedIds.DemoPlat.ToString());
        self.GetProperty("workItemTitle").GetString().Should().Be("Northridge Addition, Block 4");
        self.GetProperty("clockedIn").GetBoolean().Should().BeTrue();
        self.GetProperty("pageName").GetString().Should().Be("Work item");
    }

    [Fact]
    public async Task Stale_presence_is_hidden_and_recent_stale_is_away()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/profile"))).EnsureSuccessStatusCode();

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        await SetLastSeenAsync(SeedIds.ViewerDemo, TimeSpan.FromMinutes(4));
        await SetLastSeenAsync(SeedIds.EditorDemo, TimeSpan.FromSeconds(110));

        var list = await (await editor.GetAsync("/api/presence")).ReadJsonAsync();
        var ids = UserIds(list);
        ids.Should().Contain(SeedIds.EditorDemo);
        ids.Should().NotContain(SeedIds.ViewerDemo);
        Item(list, SeedIds.EditorDemo).GetProperty("presenceStatus").GetString().Should().Be("Away");
    }

    [Fact]
    public async Task Org_administrator_can_list_same_org_presence()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PostAsJsonAsync("/api/presence", Heartbeat("/documents"))).EnsureSuccessStatusCode();

        var other = await _factory.LoginAsync("editor.other@bisconsultants.local");
        (await other.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var admin = await _factory.LoginAsync("admin@democlient.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var json = await (await admin.GetAsync("/api/presence")).ReadJsonAsync();
        var ids = UserIds(json);
        ids.Should().Contain(SeedIds.OrgAdminDemo);
        ids.Should().Contain(SeedIds.ViewerDemo);
        ids.Should().NotContain(SeedIds.EditorOther);
    }

    private async Task SetLastSeenAsync(Guid userId, TimeSpan age)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.UserPresences.FirstAsync(x => x.UserId == userId);
        row.LastSeen = DateTimeOffset.UtcNow - age;
        row.LastSeenSort = row.LastSeen.ToUnixTimeMilliseconds();
        await db.SaveChangesAsync();
    }

    private static object Heartbeat(string route) => new
    {
        route,
        workItemId = (Guid?)null,
        clockedIn = false,
        clockWorkItemId = (Guid?)null
    };

    private static IReadOnlyList<Guid> UserIds(System.Text.Json.JsonElement json) =>
        json.GetProperty("items").EnumerateArray()
            .Select(x => Guid.Parse(x.GetProperty("userId").GetString()!))
            .ToList();

    private static System.Text.Json.JsonElement Item(System.Text.Json.JsonElement json, Guid userId) =>
        json.GetProperty("items").EnumerateArray()
            .Single(x => Guid.Parse(x.GetProperty("userId").GetString()!) == userId);
}
