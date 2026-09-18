using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.AiFill;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// CR10 — document counts by CAD (v1 = Organization name) and technician (Assigned to).
/// Same filters reconcile with the Manage Documents list. Unassigned is shown.
/// QC08 — Viewer/Uploader see assigned-org totals only.
/// </summary>
public sealed class Cr10DocumentVolumeTests : IClassFixture<ApiFactory>
{
    private const string From = "2026-07-01T00:00:00.000Z";
    private const string To = "2026-09-18T23:59:59.999Z";
    private const string Range = $"from={From}&to={To}";

    private readonly ApiFactory _factory;

    public Cr10DocumentVolumeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Unassigned_bucket_is_appended_when_there_is_volume()
    {
        DocumentVolume.WithUnassignedBucket([], 0).Should().BeEmpty();

        var onlyUnassigned = DocumentVolume.WithUnassignedBucket([], 3);
        onlyUnassigned.Should().ContainSingle();
        onlyUnassigned[0].Id.Should().Be(DocumentVolume.UnassignedId);
        onlyUnassigned[0].Name.Should().Be(DocumentVolume.UnassignedName);
        onlyUnassigned[0].Count.Should().Be(3);

        var assigned = new NamedCount(SeedIds.EditorDemo, "Alex Rivera", null, 2);
        var withZeroUnassigned = DocumentVolume.WithUnassignedBucket([assigned], 0);
        withZeroUnassigned.Should().HaveCount(2);
        withZeroUnassigned[^1].Name.Should().Be(DocumentVolume.UnassignedName);
        withZeroUnassigned[^1].Count.Should().Be(0,
            "Fail if: Unassigned bucket is missing from technician volume.");
    }

    [Fact]
    public async Task Dashboard_exposes_document_counts_by_cad_and_technician()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dash = await (await client.GetAsync($"/api/dashboard?{Range}")).ReadJsonAsync();

        var cad = dash.GetProperty("organizationCounts").EnumerateArray().ToList();
        var technicians = dash.GetProperty("assigneeCounts").EnumerateArray().ToList();

        cad.Should().NotBeEmpty("Fail if: Only Hours-by-assignee with no count charts.");
        technicians.Should().NotBeEmpty("Fail if: Only Hours-by-assignee with no count charts.");
        cad.Select(Name).Should().Contain(["Demo Client", "Other Client"]);
        technicians.Select(Name).Should().Contain(DocumentVolume.UnassignedName);

        UnassignedRow(technicians).GetProperty("count").GetInt32()
            .Should().BeGreaterThan(0, "Fail if: Unassigned bucket is missing from technician volume.");

        dash.GetProperty("hoursByAssignee").GetArrayLength()
            .Should().BeGreaterThan(0, "Hours-by-assignee stays; CR10 adds count charts beside it.");
    }

    [Fact]
    public async Task Cad_and_technician_totals_match_filtered_document_list()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dash = await (await client.GetAsync($"/api/dashboard?{Range}")).ReadJsonAsync();
        var list = await ListTotalAsync(client, $"uploadedFrom={From}&uploadedTo={To}");

        SumCounts(dash.GetProperty("organizationCounts")).Should().Be(list,
            "Fail if: chart totals disagree with filtered list.");
        SumCounts(dash.GetProperty("assigneeCounts")).Should().Be(list,
            "Fail if: chart totals disagree with filtered list.");
        list.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Each_cad_and_technician_slice_matches_the_same_filters()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dash = await (await client.GetAsync($"/api/dashboard?{Range}")).ReadJsonAsync();

        foreach (var row in dash.GetProperty("organizationCounts").EnumerateArray())
        {
            var id = row.GetProperty("id").GetGuid();
            var expected = await ListTotalAsync(client,
                $"organizationId={id}&uploadedFrom={From}&uploadedTo={To}");
            row.GetProperty("count").GetInt32().Should().Be(expected,
                $"Fail if: CAD {Name(row)} chart disagrees with filtered list.");
        }

        foreach (var row in dash.GetProperty("assigneeCounts").EnumerateArray())
        {
            var id = row.GetProperty("id").GetGuid();
            var assigneeQs = id == DocumentVolume.UnassignedId
                ? "unassignedOnly=true"
                : $"assignedToUserId={id}";
            var expected = await ListTotalAsync(client, $"{assigneeQs}&uploadedFrom={From}&uploadedTo={To}");
            row.GetProperty("count").GetInt32().Should().Be(expected,
                $"Fail if: technician {Name(row)} chart disagrees with filtered list.");
        }
    }

    [Fact]
    public async Task Organization_status_and_assignee_filters_stay_reconciled()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");

        var orgDash = await (await client.GetAsync(
            $"/api/dashboard?{Range}&organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        var orgList = await ListTotalAsync(client,
            $"organizationId={SeedIds.DemoClient}&uploadedFrom={From}&uploadedTo={To}");
        SumCounts(orgDash.GetProperty("organizationCounts")).Should().Be(orgList,
            "Fail if: chart totals disagree with filtered list.");
        OrgNames(orgDash.GetProperty("organizationCounts")).Should().Equal("Demo Client");

        var statusDash = await (await client.GetAsync(
            $"/api/dashboard?{Range}&statusId={SeedIds.StatusPending}")).ReadJsonAsync();
        var statusList = await ListTotalAsync(client,
            $"statusId={SeedIds.StatusPending}&uploadedFrom={From}&uploadedTo={To}");
        SumCounts(statusDash.GetProperty("organizationCounts")).Should().Be(statusList,
            "Fail if: chart totals disagree with filtered list.");
        SumCounts(statusDash.GetProperty("assigneeCounts")).Should().Be(statusList,
            "Fail if: chart totals disagree with filtered list.");

        var assigneeDash = await (await client.GetAsync(
            $"/api/dashboard?{Range}&assignedToUserId={SeedIds.EditorDemo}")).ReadJsonAsync();
        var assigneeList = await ListTotalAsync(client,
            $"assignedToUserId={SeedIds.EditorDemo}&uploadedFrom={From}&uploadedTo={To}");
        SumCounts(assigneeDash.GetProperty("organizationCounts")).Should().Be(assigneeList,
            "Fail if: chart totals disagree with filtered list.");
        SumCounts(assigneeDash.GetProperty("assigneeCounts")).Should().Be(assigneeList,
            "Fail if: chart totals disagree with filtered list.");
        assigneeDash.GetProperty("assigneeCounts").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Should().Contain(SeedIds.EditorDemo)
            .And.NotContain(SeedIds.EditorOther);
    }

    [Fact]
    public async Task Technician_is_document_assigned_to_not_org_assigned_technician()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dash = await (await client.GetAsync(
            $"/api/dashboard?{Range}&organizationId={SeedIds.DemoClient}")).ReadJsonAsync();

        var unassigned = await ListTotalAsync(client,
            $"organizationId={SeedIds.DemoClient}&unassignedOnly=true&uploadedFrom={From}&uploadedTo={To}");
        unassigned.Should().BeGreaterThan(0,
            "seed Demo Client has an unassigned survey even though the org has an Assigned technician.");
        UnassignedRow(dash.GetProperty("assigneeCounts").EnumerateArray().ToList())
            .GetProperty("count").GetInt32()
            .Should().Be(unassigned,
                "Fail if: Technician is org Assigned technician instead of document Assigned to.");
    }

    [Fact]
    public async Task Unassigned_filter_matches_the_unassigned_bucket()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var dash = await (await client.GetAsync($"/api/dashboard?{Range}&unassignedOnly=true")).ReadJsonAsync();
        var list = await ListTotalAsync(client, $"unassignedOnly=true&uploadedFrom={From}&uploadedTo={To}");

        list.Should().BeGreaterThan(0);
        SumCounts(dash.GetProperty("assigneeCounts")).Should().Be(list,
            "Fail if: chart totals disagree with filtered list.");
        dash.GetProperty("assigneeCounts").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("name").GetString().Should().Be(DocumentVolume.UnassignedName);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.local")]
    [InlineData("uploader@bisconsultants.local")]
    public async Task Viewer_and_uploader_volume_is_assigned_org_only(string email)
    {
        var client = await _factory.LoginAsync(email);
        var dash = await (await client.GetAsync($"/api/dashboard?{Range}")).ReadJsonAsync();

        OrgNames(dash.GetProperty("organizationCounts")).Should().Equal("Demo Client");
        SumCounts(dash.GetProperty("organizationCounts")).Should().BeGreaterThan(0);
        dash.GetProperty("assigneeCounts").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Should().NotContain(SeedIds.EditorOther,
                "Fail if: Viewer sees other orgs’ volume.");

        var list = await ListTotalAsync(client, $"uploadedFrom={From}&uploadedTo={To}");
        SumCounts(dash.GetProperty("organizationCounts")).Should().Be(list,
            "Fail if: chart totals disagree with filtered list.");
        SumCounts(dash.GetProperty("assigneeCounts")).Should().Be(list,
            "Fail if: chart totals disagree with filtered list.");

        (await client.GetAsync($"/api/dashboard?{Range}&organizationId={SeedIds.OtherClient}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound,
                "Fail if: Viewer sees other orgs’ volume.");
    }

    [Fact]
    public async Task Pdf_includes_document_count_tables()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.GetAsync($"/api/dashboard/pdf?{Range}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(200);
        Encoding.ASCII.GetString(bytes[..4]).Should().Be("%PDF");
        var text = PdfTextExtractor.Extract(new MemoryStream(bytes));
        text.Should().Contain(DocumentVolume.CadTitle, "Fail if: Only Hours-by-assignee with no count charts.");
        text.Should().Contain(DocumentVolume.TechnicianTitle, "Fail if: Only Hours-by-assignee with no count charts.");
        text.Should().Contain(DocumentVolume.UnassignedName);
        text.Should().Contain("Hours by assignee");
    }

    [Fact]
    public async Task Settings_describe_cr10_document_volume()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var note = settings.GetProperty("features").GetProperty("documentVolume").GetProperty("note").GetString();
        note.Should().Contain("CR10");
        note.Should().Contain("CAD");
        note.Should().Contain("Assigned to");
        note.Should().Contain("Unassigned");
        note.Should().Contain("QC08");
        settings.GetProperty("features").GetProperty("dashboard").GetProperty("note").GetString()
            .Should().Contain("CR10");
    }

    private static async Task<int> ListTotalAsync(HttpClient client, string query)
    {
        var json = await (await client.GetAsync($"/api/work-items?pageSize=1&{query}")).ReadJsonAsync();
        return json.GetProperty("total").GetInt32();
    }

    private static int SumCounts(JsonElement counts) =>
        counts.EnumerateArray().Sum(x => x.GetProperty("count").GetInt32());

    private static IReadOnlyList<string?> OrgNames(JsonElement counts) =>
        counts.EnumerateArray().Select(Name).ToList();

    private static string? Name(JsonElement row) => row.GetProperty("name").GetString();

    private static JsonElement UnassignedRow(IReadOnlyList<JsonElement> rows) =>
        rows.Single(x => x.GetProperty("id").GetGuid() == DocumentVolume.UnassignedId);
}
