using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class NeedHelpTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public NeedHelpTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Raise_hand_is_visible_on_presence_and_one_click_clear_removes_it()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var raise = await editor.PostAsJsonAsync("/api/presence/need-help", new { needsHelp = true });
        raise.EnsureSuccessStatusCode();
        (await raise.ReadJsonAsync()).GetProperty("needsHelp").GetBoolean().Should().BeTrue();

        var listed = await (await editor.GetAsync("/api/presence")).ReadJsonAsync();
        Item(listed, SeedIds.EditorDemo).GetProperty("needsHelp").GetBoolean().Should().BeTrue();
        listed.GetProperty("needsHelpCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var clear = await editor.PostAsJsonAsync("/api/presence/need-help", new { needsHelp = false });
        clear.EnsureSuccessStatusCode();
        (await clear.ReadJsonAsync()).GetProperty("needsHelp").GetBoolean().Should().BeFalse();

        var after = await (await editor.GetAsync("/api/presence")).ReadJsonAsync();
        Item(after, SeedIds.EditorDemo).GetProperty("needsHelp").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Heartbeat_does_not_clear_raised_hand()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/documents"))).EnsureSuccessStatusCode();
        (await editor.PostAsJsonAsync("/api/presence/need-help", new { needsHelp = true })).EnsureSuccessStatusCode();
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/reports"))).EnsureSuccessStatusCode();

        var json = await (await editor.GetAsync("/api/presence")).ReadJsonAsync();
        var self = Item(json, SeedIds.EditorDemo);
        self.GetProperty("needsHelp").GetBoolean().Should().BeTrue();
        self.GetProperty("pageName").GetString().Should().Be("Reports");
    }

    [Fact]
    public async Task Hand_uses_same_visibility_as_whos_online()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();
        (await editor.PostAsJsonAsync("/api/presence/need-help", new { needsHelp = true })).EnsureSuccessStatusCode();

        var other = await _factory.LoginAsync("editor.other@bisconsultants.local");
        (await other.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();
        (await other.PostAsJsonAsync("/api/presence/need-help", new { needsHelp = true })).EnsureSuccessStatusCode();

        var editorList = await (await editor.GetAsync("/api/presence")).ReadJsonAsync();
        UserIds(editorList).Should().Contain(SeedIds.EditorDemo);
        UserIds(editorList).Should().NotContain(SeedIds.EditorOther);
        Item(editorList, SeedIds.EditorDemo).GetProperty("needsHelp").GetBoolean().Should().BeTrue();

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();
        var adminList = await (await admin.GetAsync("/api/presence")).ReadJsonAsync();
        Item(adminList, SeedIds.EditorDemo).GetProperty("needsHelp").GetBoolean().Should().BeTrue();
        Item(adminList, SeedIds.EditorOther).GetProperty("needsHelp").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Viewer_cannot_list_or_message()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();
        (await viewer.GetAsync("/api/presence")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync($"/api/help-messages?withUserId={SeedIds.EditorDemo}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.EditorDemo,
            chip = "need-help",
            body = (string?)null
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Online_user_can_send_chips_and_short_text_and_replies()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();

        var needHelp = await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            chip = "need-help",
            body = (string?)null
        });
        needHelp.StatusCode.Should().Be(HttpStatusCode.Created);
        (await needHelp.ReadJsonAsync()).GetProperty("body").GetString().Should().Be("Need help?");

        var look = await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            chip = "take-a-look",
            body = "Northridge plat"
        });
        look.EnsureSuccessStatusCode();
        (await look.ReadJsonAsync()).GetProperty("body").GetString().Should().Be("Northridge plat");

        var onWay = await admin.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.EditorDemo,
            chip = "on-my-way"
        });
        onWay.EnsureSuccessStatusCode();
        (await onWay.ReadJsonAsync()).GetProperty("chipLabel").GetString().Should().Be("On my way");

        (await admin.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.EditorDemo,
            chip = "ping-5"
        })).EnsureSuccessStatusCode();
        var cant = await admin.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.EditorDemo,
            chip = "cant-right-now"
        });
        cant.EnsureSuccessStatusCode();
        (await cant.ReadJsonAsync()).GetProperty("chipLabel").GetString().Should().Be("Can't right now");

        var thread = await (await editor.GetAsync($"/api/help-messages?withUserId={SeedIds.Admin}")).ReadJsonAsync();
        thread.GetProperty("canCompose").GetBoolean().Should().BeTrue();
        thread.GetProperty("items").GetArrayLength().Should().Be(5);

        (await admin.GetAsync($"/api/help-messages?withUserId={SeedIds.EditorDemo}")).EnsureSuccessStatusCode();
        var adminPresence = await (await admin.GetAsync("/api/presence")).ReadJsonAsync();
        Item(adminPresence, SeedIds.EditorDemo).GetProperty("unreadHelpCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Offline_compose_is_rejected_with_brandon_copy()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();
        await SetLastSeenAsync(SeedIds.Admin, TimeSpan.FromMinutes(4));

        var response = await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            chip = "need-help"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("message").GetString()
            .Should().Be("Offline — try when they're back.");

        var thread = await (await editor.GetAsync($"/api/help-messages?withUserId={SeedIds.Admin}")).ReadJsonAsync();
        thread.GetProperty("canCompose").GetBoolean().Should().BeFalse();
        thread.GetProperty("composeDisabledReason").GetString().Should().Be("Offline — try when they're back.");
    }

    [Fact]
    public async Task Cannot_message_other_org_or_self()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var other = await _factory.LoginAsync("editor.other@bisconsultants.local");
        (await other.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var cross = await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.EditorOther,
            chip = "need-help"
        });
        cross.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var self = await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.EditorDemo,
            chip = "need-help"
        });
        self.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Away_is_treated_as_offline_for_compose()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();
        await SetLastSeenAsync(SeedIds.Admin, TimeSpan.FromSeconds(110));

        var response = await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            body = "still there?"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("message").GetString()
            .Should().Be("Offline — try when they're back.");
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
