using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// CR03 — Active tile count matches the destination Active list (full total, not one page).
/// CR05 — Staff default scope is the signed-in assignee; Viewer/Uploader stay QC08.
/// </summary>
public sealed class Cr03Cr05Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Cr03Cr05Tests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Active_tile_equals_destination_list_total_beyond_first_page()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var ids = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            ids.Add(await UploadActiveAsync(editor, SeedIds.EditorDemo, $"cr03-page-{i}.pdf"));
        }

        var dash = await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync();
        var tile = Kpi(dash, "active");

        var page = await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorDemo}&page=1&pageSize=1")).ReadJsonAsync();
        page.GetProperty("items").GetArrayLength().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(1);
        page.GetProperty("total").GetInt32().Should().Be(tile);
        tile.Should().BeGreaterThan(page.GetProperty("items").GetArrayLength(),
            "Fail if: tile count ignores pagination and only matches the first page.");
        tile.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task Active_tile_ignores_dashboard_date_window_and_status_dropdown()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        await UploadActiveAsync(editor, SeedIds.EditorDemo, "cr03-old-window.pdf");

        var dest = await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorDemo}&pageSize=100")).ReadJsonAsync();
        var listTotal = dest.GetProperty("total").GetInt32();

        var windowed = await (await editor.GetAsync(
            $"/api/dashboard?from=2020-01-01&to=2020-01-31&assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync();
        Kpi(windowed, "active").Should().Be(listTotal,
            "Fail if: Active tile uses the chart date window and disagrees with the destination list.");

        var pendingStatus = await (await editor.GetAsync(
            $"/api/dashboard?statusId={SeedIds.StatusPending}&assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync();
        Kpi(pendingStatus, "active").Should().Be(listTotal,
            "Fail if: dashboard Status dropdown shrinks Active below the destination Active queue.");
    }

    [Fact]
    public async Task Status_and_assignee_changes_keep_tile_and_list_consistent()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadActiveAsync(editor, SeedIds.EditorDemo, "cr03-refresh.pdf");

        var beforeTile = Kpi(await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync(), "active");
        var beforeList = (await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorDemo}&pageSize=1")).ReadJsonAsync())
            .GetProperty("total").GetInt32();
        beforeTile.Should().Be(beforeList);

        (await editor.PatchAsJsonAsync($"/api/work-items/{id}", new { statusId = SeedIds.StatusWorked }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var afterStatusTile = Kpi(await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync(), "active");
        var afterStatusList = (await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorDemo}&pageSize=1")).ReadJsonAsync())
            .GetProperty("total").GetInt32();
        afterStatusTile.Should().Be(afterStatusList);
        afterStatusTile.Should().Be(beforeTile - 1);

        var moved = await UploadActiveAsync(editor, SeedIds.EditorDemo, "cr03-reassign.pdf");
        (await editor.PatchAsJsonAsync($"/api/work-items/{moved}", new { assignedToUserId = SeedIds.EditorOther }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var mineTile = Kpi(await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync(), "active");
        var mineList = (await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorDemo}&pageSize=1")).ReadJsonAsync())
            .GetProperty("total").GetInt32();
        mineTile.Should().Be(mineList);

        var otherTile = Kpi(await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorOther}")).ReadJsonAsync(), "active");
        var otherList = (await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorOther}&pageSize=1")).ReadJsonAsync())
            .GetProperty("total").GetInt32();
        otherTile.Should().Be(otherList);
        otherTile.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Staff_can_switch_between_personal_and_all_assignee_scope()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        await UploadActiveAsync(editor, SeedIds.EditorDemo, "cr05-mine.pdf");
        await UploadActiveAsync(editor, SeedIds.EditorOther, "cr05-other.pdf");

        var mine = Kpi(await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync(), "active");
        var other = Kpi(await (await editor.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorOther}")).ReadJsonAsync(), "active");
        var all = Kpi(await (await editor.GetAsync("/api/dashboard")).ReadJsonAsync(), "active");

        all.Should().BeGreaterThan(mine, "Fail if: staff cannot switch off personal scope to see all assignees.");
        all.Should().BeGreaterThanOrEqualTo(mine + other);
        mine.Should().NotBe(other);

        var mineList = (await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorDemo}&pageSize=1")).ReadJsonAsync())
            .GetProperty("total").GetInt32();
        var allList = (await (await editor.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&pageSize=1")).ReadJsonAsync())
            .GetProperty("total").GetInt32();
        mine.Should().Be(mineList);
        all.Should().Be(allList);
    }

    [Fact]
    public async Task Queue_sort_is_pending_first_then_oldest_upload()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var olderPending = await UploadPendingAsync(admin, SeedIds.Admin, "cr05-pending-older.pdf");
        await Task.Delay(25);
        var newerPending = await UploadPendingAsync(admin, SeedIds.Admin, "cr05-pending-newer.pdf");
        var active = await UploadActiveAsync(admin, SeedIds.Admin, "cr05-active.pdf");

        var json = await (await admin.GetAsync(
            $"/api/work-items?assignedToUserId={SeedIds.Admin}&sortBy=queue&sortDir=asc&pageSize=100")).ReadJsonAsync();
        var rows = json.GetProperty("items").EnumerateArray()
            .Select(x => (
                Id: x.GetProperty("id").GetGuid(),
                Status: x.GetProperty("statusName").GetString(),
                Uploaded: x.GetProperty("uploadedAt").GetDateTimeOffset()))
            .ToList();

        var ours = rows.Where(x => x.Id == newerPending || x.Id == olderPending || x.Id == active).ToList();
        ours.Should().HaveCount(3);
        ours[0].Id.Should().Be(olderPending);
        ours[0].Status.Should().Be("Pending");
        ours[1].Id.Should().Be(newerPending);
        ours[1].Status.Should().Be("Pending");
        ours[2].Id.Should().Be(active);
        ours[0].Uploaded.Should().BeOnOrBefore(ours[1].Uploaded);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.local")]
    [InlineData("uploader@bisconsultants.local")]
    public async Task Viewer_and_uploader_active_count_is_assigned_org_only(string email)
    {
        var staff = await _factory.LoginAsync("admin@bisconsultants.local");
        await UploadActiveAsync(staff, SeedIds.EditorOther, "cr05-other-org.pdf");

        var client = await _factory.LoginAsync(email);
        var dash = await (await client.GetAsync(
            $"/api/dashboard?assignedToUserId={SeedIds.EditorOther}")).ReadJsonAsync();
        OrgNames(dash.GetProperty("organizationCounts")).Should().Equal("Demo Client");

        var tile = Kpi(dash, "active");
        var list = await (await client.GetAsync(
            $"/api/work-items?statusId={SeedIds.StatusInProgress}&assignedToUserId={SeedIds.EditorOther}&pageSize=1")).ReadJsonAsync();
        list.GetProperty("total").GetInt32().Should().Be(tile);
        list.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("organizationName").GetString())
            .Should().OnlyContain(name => name == "Demo Client");

        (await client.GetAsync($"/api/dashboard?organizationId={SeedIds.OtherClient}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Settings_describe_cr03_and_cr05()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var dashboard = settings.GetProperty("features").GetProperty("dashboard").GetProperty("note").GetString();
        dashboard.Should().Contain("CR03");
        dashboard.Should().Contain("CR05");
        dashboard.Should().Contain("Active");

        var queue = settings.GetProperty("features").GetProperty("myQueue").GetProperty("note").GetString();
        queue.Should().Contain("CR05");
        queue.Should().Contain("Pending");
        queue.Should().Contain("QC08");

        var assignee = settings.GetProperty("features").GetProperty("dashboardAssignee").GetProperty("note").GetString();
        assignee.Should().Contain("CR05");
        assignee.Should().Contain("QC08");
    }

    private static async Task<Guid> UploadActiveAsync(HttpClient client, Guid assignee, string fileName)
    {
        var id = await UploadPendingAsync(client, assignee, fileName);
        var patch = await client.PatchAsJsonAsync($"/api/work-items/{id}", new { statusId = SeedIds.StatusInProgress });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private static async Task<Guid> UploadPendingAsync(HttpClient client, Guid assignee, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent("Demo Client"), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent(assignee.ToString()), "assignedToUserId");
        form.Add(new StringContent(fileName), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        var uploaded = await client.PostAsync("/api/work-items", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        return (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    private static int Kpi(JsonElement json, string key) =>
        json.GetProperty("kpis").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == key)
            .GetProperty("count").GetInt32();

    private static IReadOnlyList<string?> OrgNames(JsonElement counts) =>
        counts.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
}
