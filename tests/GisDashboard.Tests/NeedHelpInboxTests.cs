using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class NeedHelpInboxTests
{
    [Fact]
    public async Task Inbox_lists_waiting_thread_with_preview_and_unread()
    {
        using var factory = new ApiFactory();
        var editor = await factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();
        var admin = await factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();

        (await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            chip = "need-help"
        })).EnsureSuccessStatusCode();

        var inbox = await (await admin.GetAsync("/api/help-messages/inbox")).ReadJsonAsync();
        inbox.GetProperty("unreadCount").GetInt32().Should().Be(1);
        var thread = InboxItem(inbox, SeedIds.EditorDemo);
        thread.GetProperty("withDisplayName").GetString().Should().Be("Alex Rivera");
        thread.GetProperty("preview").GetString().Should().Be("Need help?");
        thread.GetProperty("unread").GetBoolean().Should().BeTrue();
        thread.GetProperty("unreadCount").GetInt32().Should().Be(1);
        thread.TryGetProperty("lastAt", out var lastAt).Should().BeTrue();
        lastAt.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Opening_thread_marks_inbox_read_and_keeps_it_listed()
    {
        using var factory = new ApiFactory();
        var editor = await factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();
        var admin = await factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();

        (await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            body = "Northridge plat"
        })).EnsureSuccessStatusCode();

        var before = await (await admin.GetAsync("/api/help-messages/inbox")).ReadJsonAsync();
        before.GetProperty("unreadCount").GetInt32().Should().Be(1);
        InboxItem(before, SeedIds.EditorDemo).GetProperty("unread").GetBoolean().Should().BeTrue();

        (await admin.GetAsync($"/api/help-messages?withUserId={SeedIds.EditorDemo}")).EnsureSuccessStatusCode();

        var after = await (await admin.GetAsync("/api/help-messages/inbox")).ReadJsonAsync();
        InboxItem(after, SeedIds.EditorDemo).GetProperty("unread").GetBoolean().Should().BeFalse();
        after.GetProperty("unreadCount").GetInt32().Should().Be(0);
        InboxItem(after, SeedIds.EditorDemo).GetProperty("preview").GetString().Should().Be("Northridge plat");
    }

    [Fact]
    public async Task Inbox_still_lists_offline_sender_and_compose_stays_disabled()
    {
        using var factory = new ApiFactory();
        var editor = await factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();
        var admin = await factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/presence", Heartbeat("/status"))).EnsureSuccessStatusCode();

        (await editor.PostAsJsonAsync("/api/help-messages", new
        {
            toUserId = SeedIds.Admin,
            chip = "take-a-look"
        })).EnsureSuccessStatusCode();

        await SetLastSeenAsync(factory, SeedIds.EditorDemo, TimeSpan.FromMinutes(4));

        var inbox = await (await admin.GetAsync("/api/help-messages/inbox")).ReadJsonAsync();
        var thread = InboxItem(inbox, SeedIds.EditorDemo);
        thread.GetProperty("unread").GetBoolean().Should().BeTrue();
        thread.GetProperty("presenceStatus").GetString().Should().Be("Offline");
        thread.GetProperty("canCompose").GetBoolean().Should().BeFalse();
        thread.GetProperty("composeDisabledReason").GetString().Should().Be("Offline — try when they're back.");
        thread.GetProperty("preview").GetString().Should().Be("Can you take a look?");

        var opened = await (await admin.GetAsync($"/api/help-messages?withUserId={SeedIds.EditorDemo}")).ReadJsonAsync();
        opened.GetProperty("canCompose").GetBoolean().Should().BeFalse();
        opened.GetProperty("composeDisabledReason").GetString().Should().Be("Offline — try when they're back.");
    }

    [Fact]
    public async Task Inbox_hides_users_outside_whos_online_visibility()
    {
        using var factory = new ApiFactory();
        var editor = await factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsJsonAsync("/api/presence", Heartbeat("/"))).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.HelpMessages.Add(new GisDashboard.Domain.HelpMessage
            {
                Id = Guid.NewGuid(),
                FromUserId = SeedIds.TokenUploadUser,
                ToUserId = SeedIds.EditorDemo,
                Chip = "need-help",
                Body = "Need help?",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedAtSort = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            await db.SaveChangesAsync();
        }

        var inbox = await (await editor.GetAsync("/api/help-messages/inbox")).ReadJsonAsync();
        inbox.GetProperty("items").EnumerateArray()
            .Select(x => Guid.Parse(x.GetProperty("withUserId").GetString()!))
            .Should().NotContain(SeedIds.TokenUploadUser);
        inbox.GetProperty("unreadCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Viewer_and_uploader_cannot_list_inbox()
    {
        using var factory = new ApiFactory();
        var viewer = await factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync("/api/help-messages/inbox")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var uploader = await factory.LoginAsync("uploader@bisconsultants.local");
        (await uploader.GetAsync("/api/help-messages/inbox")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task SetLastSeenAsync(ApiFactory factory, Guid userId, TimeSpan age)
    {
        using var scope = factory.Services.CreateScope();
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

    private static System.Text.Json.JsonElement InboxItem(System.Text.Json.JsonElement json, Guid userId) =>
        json.GetProperty("items").EnumerateArray()
            .Single(x => Guid.Parse(x.GetProperty("withUserId").GetString()!) == userId);
}
