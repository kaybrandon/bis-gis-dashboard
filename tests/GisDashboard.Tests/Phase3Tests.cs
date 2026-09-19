using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class Phase3Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Phase3Tests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_kpis_are_scoped_and_named()
    {
        var global = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await global.GetAsync("/api/dashboard")).ReadJsonAsync();
        var kpis = json.GetProperty("kpis").EnumerateArray().ToDictionary(
            x => x.GetProperty("key").GetString()!,
            x => x.GetProperty("count").GetInt32());
        kpis["active"].Should().BeGreaterThanOrEqualTo(1);
        kpis["pending"].Should().BeGreaterThanOrEqualTo(2);
        json.GetProperty("kpis")[0].GetProperty("label").GetString().Should().Be("Active");
        json.GetProperty("organizationCounts").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Contain(["Demo Client", "Other Client"]);

        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        var scoped = await (await orgAdmin.GetAsync("/api/dashboard")).ReadJsonAsync();
        scoped.GetProperty("organizationCounts").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Contain(["Demo Client", "Other Client"]);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerDash = await (await viewer.GetAsync("/api/dashboard")).ReadJsonAsync();
        viewerDash.GetProperty("organizationCounts").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Equal("Demo Client");
    }

    [Fact]
    public async Task Completing_an_item_increments_dashboard_completed_kpi()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var before = Kpi((await (await client.GetAsync("/api/dashboard")).ReadJsonAsync()), "completed");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent("Demo Client"), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent("KPI complete check"), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "kpi-complete.pdf");
        var uploaded = await client.PostAsync("/api/work-items", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var patch = await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusWorked
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await patch.ReadJsonAsync();
        detail.GetProperty("workedOn").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);

        var after = Kpi((await (await client.GetAsync("/api/dashboard")).ReadJsonAsync()), "completed");
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task Work_item_buckets_and_hours_match_seed()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var buckets = json.GetProperty("buckets");
        buckets.GetProperty("pending").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        buckets.GetProperty("onHold").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        buckets.GetProperty("completed").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        buckets.GetProperty("firstDeadline").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        buckets.GetProperty("mine").GetInt32().Should().Be(0);

        var pending = await (await client.GetAsync("/api/work-items?bucket=pending&pageSize=100")).ReadJsonAsync();
        pending.GetProperty("items").EnumerateArray().Should().OnlyContain(x => x.GetProperty("statusName").GetString() == "Pending");

        var plat = json.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoPlat);
        plat.GetProperty("hoursLabel").GetString().Should().Be("2h 15m");
        plat.GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorDemo);
        plat.GetProperty("assignedToName").GetString().Should().Be("Alex Rivera");
    }

    [Fact]
    public async Task Export_is_excel_csv_without_assigned_to_user_id()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.GetAsync("/api/work-items/export?pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.ms-excel");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes[0].Should().Be(0xEF);
        var text = Encoding.UTF8.GetString(bytes);
        text.Should().Contain("FileName,Client Name,Status,Assigned To,Upload Date,Worked Date,Hours,Priority,Needed By,Survey,Abstract,Lot/Block,Subdivision,Legal Description");
        text.Should().Contain("N-14-042 Plat.pdf");
        text.Should().NotContain("assignedToUserId");
        text.Should().NotContain(SeedIds.EditorDemo.ToString());
    }

    [Fact]
    public async Task Detail_exposes_phase3_flags_counters_and_property_ids()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var json = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        json.GetProperty("isSketch").GetBoolean().Should().BeTrue();
        json.GetProperty("isSplit").GetBoolean().Should().BeFalse();
        json.GetProperty("annexationCount").GetInt32().Should().Be(1);
        json.GetProperty("correctionCount").GetInt32().Should().Be(2);
        json.GetProperty("platCount").GetInt32().Should().Be(1);
        json.GetProperty("propertyIds").GetString().Should().Contain("R12345");
        json.GetProperty("canPostComments").GetBoolean().Should().BeTrue();
        json.GetProperty("hoursLabel").GetString().Should().Be("2h 15m");
    }

    [Fact]
    public async Task Editor_can_patch_flags_counters_and_property_ids()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoHeld}", new
        {
            isSplit = true,
            isSketch = true,
            annexationCount = 3,
            correctionCount = 1,
            deedCount = 2,
            platCount = 4,
            propertyIds = "R9\nR10"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("isSplit").GetBoolean().Should().BeTrue();
        json.GetProperty("isSketch").GetBoolean().Should().BeTrue();
        json.GetProperty("annexationCount").GetInt32().Should().Be(3);
        json.GetProperty("propertyIds").GetString().Should().Be("R9\nR10");
    }

    [Fact]
    public async Task Viewer_cannot_patch_phase3_fields()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var response = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            isSketch = false,
            annexationCount = 99
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Comments_are_readable_and_idor_safe()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var list = await editor.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/comments");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var existing = await list.ReadJsonAsync();
        existing.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.CommentDemoPlat);

        var created = await editor.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/comments", new
        {
            body = "West line looks good after the bearing check."
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/comments")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/comments", new { body = "Viewer stays comment-free." }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        (await uploader.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/comments")).StatusCode.Should().Be(HttpStatusCode.OK);
        var posted = await uploader.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/comments", new { body = "Client-visible comment from Riley." });
        posted.StatusCode.Should().Be(HttpStatusCode.Created);

        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        (await orgAdmin.GetAsync($"/api/work-items/{SeedIds.OtherPlat}/comments")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await orgAdmin.PostAsJsonAsync($"/api/work-items/{SeedIds.OtherPlat}/comments", new { body = "cross" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await viewer.GetAsync($"/api/work-items/{SeedIds.OtherPlat}/comments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_actions_and_settings_mark_later_v1_phases()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var actions = await (await client.GetAsync("/api/lookups/status-actions")).ReadJsonAsync();
        actions.GetProperty("activeId").GetGuid().Should().Be(SeedIds.StatusInProgress);
        actions.GetProperty("onHoldId").GetGuid().Should().Be(SeedIds.StatusHeld);
        actions.GetProperty("completeId").GetGuid().Should().Be(SeedIds.StatusWorked);
        actions.GetProperty("cancelledId").GetGuid().Should().Be(SeedIds.StatusCancelled);

        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        settings.GetProperty("phase").GetString().Should().Be("Phase 3.1");
        settings.GetProperty("features").GetProperty("dashboard").GetProperty("enabled").GetBoolean().Should().BeTrue();
        settings.GetProperty("features").GetProperty("headerGreeting").GetProperty("enabled").GetBoolean().Should().BeTrue();
        settings.GetProperty("features").GetProperty("comments").GetProperty("enabled").GetBoolean().Should().BeTrue();
        settings.GetProperty("features").GetProperty("deedAiLinking").GetProperty("enabled").GetBoolean().Should().BeFalse();
        settings.GetProperty("features").GetProperty("entraSso").GetProperty("enabled").GetBoolean().Should().BeFalse();
        settings.GetProperty("features").GetProperty("persistedPdfMarkup").GetProperty("enabled").GetBoolean().Should().BeFalse();
        settings.GetProperty("features").GetProperty("deedAiLinking").GetProperty("note").GetString().Should().Be("Later v1 phase");
    }

    private static int Kpi(System.Text.Json.JsonElement json, string key) =>
        json.GetProperty("kpis").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == key)
            .GetProperty("count").GetInt32();
}
