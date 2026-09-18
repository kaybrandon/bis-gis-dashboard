using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class Qc06NeedsReviewTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Qc06NeedsReviewTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_enum_includes_exact_needs_review()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var statuses = await (await client.GetAsync("/api/lookups/statuses")).ReadJsonAsync();
        var names = statuses.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
        names.Should().Contain("Needs Review");
        names.Should().NotContain("Reviewed");
        names.Should().Contain("Pending");

        var needsReview = statuses.EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "Needs Review");
        needsReview.GetProperty("id").GetGuid().Should().Be(SeedIds.StatusNeedsReview);

        var actions = await (await client.GetAsync("/api/lookups/status-actions")).ReadJsonAsync();
        actions.GetProperty("needsReviewId").GetGuid().Should().Be(SeedIds.StatusNeedsReview);
        actions.GetProperty("completedIds").EnumerateArray().Select(x => x.GetGuid())
            .Should().NotContain(SeedIds.StatusNeedsReview);

        StatusDisplay.Label("Needs Review").Should().Be("Needs Review");
        StatusDisplay.Label("In Progress").Should().Be("Active");
    }

    [Fact]
    public async Task Editor_can_set_needs_review_and_it_persists_for_filter()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(editor, "qc06-persist.pdf");

        var marked = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusNeedsReview
        });
        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await marked.ReadJsonAsync();
        saved.GetProperty("statusId").GetGuid().Should().Be(SeedIds.StatusNeedsReview);
        saved.GetProperty("statusName").GetString().Should().Be("Needs Review");
        saved.GetProperty("isReviewed").GetBoolean().Should().BeFalse();

        var reloaded = await (await editor.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        reloaded.GetProperty("statusName").GetString().Should().Be("Needs Review");
        reloaded.GetProperty("isReviewed").GetBoolean().Should().BeFalse();

        var filtered = await (await editor.GetAsync(
            $"/api/work-items?pageSize=100&bucket=all&statusId={SeedIds.StatusNeedsReview}")).ReadJsonAsync();
        var items = filtered.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(x => x.GetProperty("statusName").GetString() == "Needs Review");
        items.Should().Contain(x => x.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Needs_review_appears_in_editor_queues_without_a_dedicated_owner_queue()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(editor, "qc06-editor-queue.pdf");
        (await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusNeedsReview
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var all = await (await editor.GetAsync("/api/work-items?pageSize=100&bucket=all")).ReadJsonAsync();
        all.GetProperty("items").EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);

        var mine = await (await editor.GetAsync("/api/work-items?pageSize=100&bucket=mine")).ReadJsonAsync();
        mine.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("id").GetGuid() == id && x.GetProperty("statusName").GetString() == "Needs Review");

        var priority = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new { isPriority = true });
        priority.StatusCode.Should().Be(HttpStatusCode.OK);
        var priorityQueue = await (await editor.GetAsync("/api/work-items?pageSize=100&bucket=priority")).ReadJsonAsync();
        priorityQueue.GetProperty("items").EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);

        var pending = await (await editor.GetAsync("/api/work-items?pageSize=100&bucket=pending")).ReadJsonAsync();
        pending.GetProperty("items").EnumerateArray()
            .Should().OnlyContain(x => x.GetProperty("statusName").GetString() == "Pending");
        pending.GetProperty("items").EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var settings = await (await editor.GetAsync("/api/settings")).ReadJsonAsync();
        settings.GetProperty("features").GetProperty("needsReviewStatus").GetProperty("note").GetString()
            .Should().Contain("No separate review-owner");
    }

    [Fact]
    public async Task Dashboard_counts_charts_and_exports_keep_needs_review()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var before = await (await editor.GetAsync("/api/dashboard")).ReadJsonAsync();
        var completedBefore = Kpi(before, "completed");

        var id = await UploadAsync(editor, "qc06-dashboard.pdf");
        (await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusNeedsReview
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var dash = await (await editor.GetAsync("/api/dashboard")).ReadJsonAsync();
        Kpi(dash, "completed").Should().Be(completedBefore);
        var statusCounts = dash.GetProperty("statusCounts").EnumerateArray().ToList();
        statusCounts.Should().Contain(x => x.GetProperty("name").GetString() == "Needs Review"
            && x.GetProperty("count").GetInt32() >= 1
            && x.GetProperty("id").GetGuid() == SeedIds.StatusNeedsReview);
        statusCounts.Should().NotContain(x => x.GetProperty("name").GetString() == "Reviewed");

        var filteredDash = await (await editor.GetAsync(
            $"/api/dashboard?statusId={SeedIds.StatusNeedsReview}")).ReadJsonAsync();
        filteredDash.GetProperty("statusCounts").EnumerateArray()
            .Should().OnlyContain(x => x.GetProperty("name").GetString() == "Needs Review");
        Kpi(filteredDash, "completed").Should().Be(0);

        var csv = await editor.GetAsync($"/api/work-items/export?pageSize=100&bucket=all&statusId={SeedIds.StatusNeedsReview}");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        text.Should().Contain("qc06-dashboard.pdf");
        text.Should().Contain("Needs Review");
        text.Should().NotContain("Reviewed Yes");

        var pdf = await editor.GetAsync($"/api/dashboard/pdf?statusId={SeedIds.StatusNeedsReview}");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        Encoding.UTF8.GetString(await pdf.Content.ReadAsByteArrayAsync()).Should().Contain("Needs Review");
    }

    [Fact]
    public async Task Moving_off_needs_review_does_not_change_reviewed()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(editor, "qc06-reviewed-independence.pdf");

        var reviewed = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusNeedsReview,
            isReviewed = true
        });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await reviewed.ReadJsonAsync();
        first.GetProperty("statusName").GetString().Should().Be("Needs Review");
        first.GetProperty("isReviewed").GetBoolean().Should().BeTrue();

        var off = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusPending
        });
        off.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterOff = await off.ReadJsonAsync();
        afterOff.GetProperty("statusName").GetString().Should().Be("Pending");
        afterOff.GetProperty("isReviewed").GetBoolean().Should().BeTrue();

        var back = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusNeedsReview
        });
        back.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBack = await back.ReadJsonAsync();
        afterBack.GetProperty("statusName").GetString().Should().Be("Needs Review");
        afterBack.GetProperty("isReviewed").GetBoolean().Should().BeTrue();

        var clearReview = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new { isReviewed = false });
        clearReview.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterClear = await clearReview.ReadJsonAsync();
        afterClear.GetProperty("statusName").GetString().Should().Be("Needs Review");
        afterClear.GetProperty("isReviewed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Viewer_cannot_set_needs_review()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            statusId = SeedIds.StatusNeedsReview
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static int Kpi(System.Text.Json.JsonElement json, string key) =>
        json.GetProperty("kpis").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == key)
            .GetProperty("count").GetInt32();

    private static async Task<Guid> UploadAsync(HttpClient client, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent("Demo Client"), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        var response = await client.PostAsync("/api/work-items", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }
}
