using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Tests;

/// <summary>
/// CR11 — one approved status set everywhere. Needs Review stays (QC06).
/// Existing IDs are preserved; QC'd is not silently remapped.
/// </summary>
public sealed class Cr11StatusVocabularyTests : IClassFixture<ApiFactory>
{
    private static readonly string[] Canonical =
    [
        "Active", "Pending", "Complete", "On-Hold", "Cancelled", "Needs Review"
    ];

    private readonly ApiFactory _factory;

    public Cr11StatusVocabularyTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lookups_and_actions_use_canonical_set_and_keep_needs_review()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var statuses = await (await client.GetAsync("/api/lookups/statuses")).ReadJsonAsync();
        var names = statuses.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();

        names.Should().Equal(Canonical);
        names.Should().Contain("Needs Review");
        names.Should().NotContain(["In Progress", "Held", "Worked", "QC'd", "Reviewed", "On Hold", "Completed"]);

        var byName = statuses.EnumerateArray().ToDictionary(
            x => x.GetProperty("name").GetString()!,
            x => x.GetProperty("id").GetGuid());
        byName["Active"].Should().Be(SeedIds.StatusInProgress);
        byName["Pending"].Should().Be(SeedIds.StatusPending);
        byName["Complete"].Should().Be(SeedIds.StatusWorked);
        byName["On-Hold"].Should().Be(SeedIds.StatusHeld);
        byName["Cancelled"].Should().Be(SeedIds.StatusCancelled);
        byName["Needs Review"].Should().Be(SeedIds.StatusNeedsReview);

        var actions = await (await client.GetAsync("/api/lookups/status-actions")).ReadJsonAsync();
        actions.GetProperty("activeId").GetGuid().Should().Be(SeedIds.StatusInProgress);
        actions.GetProperty("pendingId").GetGuid().Should().Be(SeedIds.StatusPending);
        actions.GetProperty("completeId").GetGuid().Should().Be(SeedIds.StatusWorked);
        actions.GetProperty("onHoldId").GetGuid().Should().Be(SeedIds.StatusHeld);
        actions.GetProperty("cancelledId").GetGuid().Should().Be(SeedIds.StatusCancelled);
        actions.GetProperty("needsReviewId").GetGuid().Should().Be(SeedIds.StatusNeedsReview);
        actions.GetProperty("completedIds").EnumerateArray().Select(x => x.GetGuid())
            .Should().Equal(SeedIds.StatusWorked, SeedIds.StatusQcd);

        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        settings.GetProperty("features").GetProperty("statusVocabulary").GetProperty("note").GetString()
            .Should().Contain("Needs Review");
        settings.GetProperty("features").GetProperty("needsReviewStatus").GetProperty("note").GetString()
            .Should().Contain("Distinct from Reviewed");
    }

    [Fact]
    public async Task Approved_mapping_relabels_existing_ids_without_rewriting_qcd()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");

        AssertStatus(await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync(),
            SeedIds.StatusInProgress, "Active");
        AssertStatus(await (await client.GetAsync($"/api/work-items/{SeedIds.DemoHeld}")).ReadJsonAsync(),
            SeedIds.StatusHeld, "On-Hold");
        AssertStatus(await (await client.GetAsync($"/api/work-items/{SeedIds.DemoOakGrove}")).ReadJsonAsync(),
            SeedIds.StatusWorked, "Complete");
        AssertStatus(await (await client.GetAsync($"/api/work-items/{SeedIds.DemoDeed}")).ReadJsonAsync(),
            SeedIds.StatusQcd, "QC'd");

        var qcd = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoDeed}")).ReadJsonAsync();
        qcd.GetProperty("statusName").GetString().Should().NotBe("Complete");
        qcd.GetProperty("statusName").GetString().Should().NotBe("Reviewed");
        qcd.GetProperty("isReviewed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Filters_tiles_charts_and_exports_use_canonical_labels()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");

        var active = await (await client.GetAsync(
            $"/api/work-items?pageSize=100&statusId={SeedIds.StatusInProgress}")).ReadJsonAsync();
        active.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("statusId").GetGuid() == SeedIds.StatusInProgress &&
            x.GetProperty("statusName").GetString() == "Active");

        var hold = await (await client.GetAsync("/api/work-items?pageSize=100&bucket=hold")).ReadJsonAsync();
        hold.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("statusName").GetString() == "On-Hold");

        var reviewId = await UploadAsync(client, "cr11-needs-review-chart.pdf");
        (await client.PatchAsJsonAsync($"/api/work-items/{reviewId}", new
        {
            statusId = SeedIds.StatusNeedsReview
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var dash = await (await client.GetAsync("/api/dashboard")).ReadJsonAsync();
        var chartNames = dash.GetProperty("statusCounts").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToList();
        chartNames.Should().OnlyContain(name => Canonical.Contains(name!));
        chartNames.Should().NotContain(["In Progress", "Worked", "QC'd", "Held", "Reviewed"]);
        chartNames.Should().Contain("Needs Review");
        dash.GetProperty("statusCounts").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == "Needs Review")
            .GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        var csv = await client.GetAsync($"/api/work-items/export?pageSize=100&statusId={SeedIds.StatusInProgress}");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeText = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        activeText.Should().Contain("Active");
        activeText.Should().NotContain("In Progress");

        var qcdCsv = await client.GetAsync($"/api/work-items/export?pageSize=100&statusId={SeedIds.StatusQcd}");
        var qcdText = Encoding.UTF8.GetString(await qcdCsv.Content.ReadAsByteArrayAsync());
        qcdText.Should().Contain("QC'd");
        qcdText.Should().Contain("Warranty-Deed-scan.png");
        qcdText.Should().NotContain("Reviewed Yes");
    }

    [Fact]
    public async Task Setting_complete_and_needs_review_keeps_ids_and_does_not_touch_reviewed()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(editor, "cr11-status.pdf");

        var complete = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusWorked,
            isReviewed = false
        });
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertStatus(await complete.ReadJsonAsync(), SeedIds.StatusWorked, "Complete");

        var review = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusNeedsReview
        });
        review.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await review.ReadJsonAsync();
        AssertStatus(after, SeedIds.StatusNeedsReview, "Needs Review");
        after.GetProperty("isReviewed").GetBoolean().Should().BeFalse();

        var filtered = await (await editor.GetAsync(
            $"/api/work-items?pageSize=100&statusId={SeedIds.StatusNeedsReview}")).ReadJsonAsync();
        filtered.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("id").GetGuid() == id && x.GetProperty("statusName").GetString() == "Needs Review");
    }

    [Fact]
    public async Task Search_finds_active_by_canonical_or_legacy_label()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var byCanonical = await (await client.GetAsync("/api/work-items?pageSize=100&search=Active")).ReadJsonAsync();
        byCanonical.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("id").GetGuid() == SeedIds.DemoPlat &&
            x.GetProperty("statusName").GetString() == "Active");

        var byLegacy = await (await client.GetAsync("/api/work-items?pageSize=100&search=In%20Progress")).ReadJsonAsync();
        byLegacy.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("id").GetGuid() == SeedIds.DemoPlat);
    }

    [Fact]
    public void Display_mapping_is_explicit_and_does_not_equate_qcd()
    {
        StatusDisplay.CanonicalNames.Should().Equal(Canonical);
        StatusDisplay.Label("In Progress").Should().Be("Active");
        StatusDisplay.Label("Held").Should().Be("On-Hold");
        StatusDisplay.Label("Worked").Should().Be("Complete");
        StatusDisplay.Label("Needs Review").Should().Be("Needs Review");
        StatusDisplay.Label("QC'd").Should().Be("QC'd");
        StatusDisplay.IsCanonical("In Progress").Should().BeTrue();
        StatusDisplay.IsCanonical("Needs Review").Should().BeTrue();
        StatusDisplay.IsCanonical("QC'd").Should().BeFalse();
        SeedIds.IsCanonicalStatus(SeedIds.StatusQcd).Should().BeFalse();
        SeedIds.IsCanonicalStatus(SeedIds.StatusNeedsReview).Should().BeTrue();
    }

    [Fact]
    public async Task Phase52_renames_approved_aliases_and_leaves_qcd_and_item_ids()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gis-cr11-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.WorkItemStatuses.AddRange(
                new WorkItemStatus { Id = SeedIds.StatusPending, Name = "Pending", Color = "#faad14", SortOrder = 1 },
                new WorkItemStatus { Id = SeedIds.StatusInProgress, Name = "In Progress", Color = "#1890ff", SortOrder = 2 },
                new WorkItemStatus { Id = SeedIds.StatusHeld, Name = "Held", Color = "#fa8c16", SortOrder = 3 },
                new WorkItemStatus { Id = SeedIds.StatusNeedsReview, Name = "Needs Review", Color = "#eb2f96", SortOrder = 4 },
                new WorkItemStatus { Id = SeedIds.StatusWorked, Name = "Worked", Color = "#52c41a", SortOrder = 5 },
                new WorkItemStatus { Id = SeedIds.StatusQcd, Name = "QC'd", Color = "#722ed1", SortOrder = 6 },
                new WorkItemStatus { Id = SeedIds.StatusCancelled, Name = "Cancelled", Color = "#8c8c8c", SortOrder = 7 });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await SchemaUpgrade.ApplyAsync(db);

            var after = await db.WorkItemStatuses.AsNoTracking().ToDictionaryAsync(x => x.Id);
            after[SeedIds.StatusInProgress].Name.Should().Be("Active");
            after[SeedIds.StatusPending].Name.Should().Be("Pending");
            after[SeedIds.StatusWorked].Name.Should().Be("Complete");
            after[SeedIds.StatusHeld].Name.Should().Be("On-Hold");
            after[SeedIds.StatusCancelled].Name.Should().Be("Cancelled");
            after[SeedIds.StatusNeedsReview].Name.Should().Be("Needs Review");
            after[SeedIds.StatusQcd].Name.Should().Be("QC'd");
            after.Keys.Should().BeEquivalentTo(new[]
            {
                SeedIds.StatusPending, SeedIds.StatusInProgress, SeedIds.StatusHeld,
                SeedIds.StatusNeedsReview, SeedIds.StatusWorked, SeedIds.StatusQcd, SeedIds.StatusCancelled
            });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void AssertStatus(System.Text.Json.JsonElement json, Guid id, string name)
    {
        json.GetProperty("statusId").GetGuid().Should().Be(id);
        json.GetProperty("statusName").GetString().Should().Be(name);
    }

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
