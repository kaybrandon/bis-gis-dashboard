using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class TimeAndNotesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TimeAndNotesTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Org_admin_can_see_time_logs_on_own_org()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        json.GetProperty("items").EnumerateArray().Should().OnlyContain(x => x.GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Org_admin_time_logs_on_other_org_are_visible()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}/time-entries");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Editor_time_logs_on_other_org_are_visible()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}/time-entries");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_can_read_time_logs_but_cannot_write()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var list = await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await list.ReadJsonAsync();
        json.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        json.GetProperty("items").EnumerateArray().Should().OnlyContain(x => x.GetProperty("canEdit").GetBoolean() == false);
        json.GetProperty("totalLabel").GetString().Should().Be("2h 15m");

        var create = await client.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries", new
        {
            hours = 1,
            minutes = 0
        });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var patch = await client.PatchAsJsonAsync(
            $"/api/work-items/{SeedIds.DemoPlat}/time-entries/{SeedIds.TimeEditorDemoPlat}",
            new { hours = 2, minutes = 0 });
        patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_can_crud_own_time_entry_but_not_another_users()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var created = await client.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries", new
        {
            hours = 1,
            minutes = 15,
            note = "Lot corner review"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = await created.ReadJsonAsync();
        createdJson.GetProperty("durationLabel").GetString().Should().Be("1h 15m");
        createdJson.GetProperty("canEdit").GetBoolean().Should().BeTrue();
        var entryId = createdJson.GetProperty("id").GetGuid();

        var patched = await client.PatchAsJsonAsync(
            $"/api/work-items/{SeedIds.DemoPlat}/time-entries/{entryId}",
            new { hours = 0, minutes = 40, note = "Lot corner review (revised)" });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchedJson = await patched.ReadJsonAsync();
        patchedJson.GetProperty("minutes").GetInt32().Should().Be(40);
        patchedJson.GetProperty("durationLabel").GetString().Should().Be("40m");

        var forbidden = await client.DeleteAsync(
            $"/api/work-items/{SeedIds.DemoPlat}/time-entries/{SeedIds.TimeAdminDemoPlat}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleted = await client.DeleteAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries/{entryId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Global_admin_can_manage_another_users_time_entry()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var patched = await client.PatchAsJsonAsync(
            $"/api/work-items/{SeedIds.DemoPlat}/time-entries/{SeedIds.TimeEditorDemoPlat}",
            new { hours = 1, minutes = 30, note = "West line bearing check" });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await patched.ReadJsonAsync();
        json.GetProperty("minutes").GetInt32().Should().Be(90);
        json.GetProperty("canEdit").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Org_admin_can_manage_another_users_time_entry_in_org()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var patched = await client.PatchAsJsonAsync(
            $"/api/work-items/{SeedIds.DemoPlat}/time-entries/{SeedIds.TimeEditorDemoPlat}",
            new { hours = 1, minutes = 30, note = "West line bearing check" });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await patched.ReadJsonAsync()).GetProperty("canEdit").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Org_admin_receives_notes_and_history()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var json = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        json.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeTrue();
        json.GetProperty("canSeeTimeLogs").GetBoolean().Should().BeTrue();
        json.GetProperty("canLogTime").GetBoolean().Should().BeTrue();
        json.GetProperty("internalNotes").ValueKind.Should().Be(JsonValueKind.String);
        json.GetProperty("internalNotesHistory").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Viewer_cannot_see_internal_notes()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var json = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        json.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        json.GetProperty("canEditInternalNotes").GetBoolean().Should().BeFalse();
        json.GetProperty("canSeeTimeLogs").GetBoolean().Should().BeTrue();
        json.GetProperty("canLogTime").GetBoolean().Should().BeFalse();
        json.GetProperty("internalNotes").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("internalNotesHistory").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Editor_note_patch_appends_history()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            internalNotes = "Shared note from editor — phase 2 history"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("internalNotes").GetString().Should().Be("Shared note from editor — phase 2 history");
        var history = json.GetProperty("internalNotesHistory").EnumerateArray().ToList();
        history.Should().NotBeEmpty();
        history[0].GetProperty("body").GetString().Should().Be("Shared note from editor — phase 2 history");
        history[0].GetProperty("editedByName").GetString().Should().Be("Alex Rivera");
    }

    [Fact]
    public async Task Auth_user_exposes_time_log_flags()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var adminMe = await (await admin.GetAsync("/api/auth/me")).ReadJsonAsync();
        adminMe.GetProperty("canSeeTimeLogs").GetBoolean().Should().BeTrue();
        adminMe.GetProperty("canLogTime").GetBoolean().Should().BeTrue();

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerMe = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
        viewerMe.GetProperty("canSeeTimeLogs").GetBoolean().Should().BeTrue();
        viewerMe.GetProperty("canLogTime").GetBoolean().Should().BeFalse();
        viewerMe.GetProperty("role").GetString().Should().Be("Viewer");
    }

    [Fact]
    public async Task Work_item_list_includes_assigned_to_id_for_inline_edit_and_still_returns_names()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            item.TryGetProperty("assignedToName", out var name).Should().BeTrue();
            name.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
            item.TryGetProperty("assignedToUserId", out var id).Should().BeTrue();
            id.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
        }
    }
}
