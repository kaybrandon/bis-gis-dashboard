using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class WorkflowCommsTests : IClassFixture<WorkflowCommsFactory>
{
    private readonly WorkflowCommsFactory _factory;

    public WorkflowCommsTests(WorkflowCommsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Status_change_emails_client_contacts_assigned_techs_and_assignee()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            statusId = SeedIds.StatusPending
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sent = LastEmail("In Progress → Pending");
        sent.Subject.Should().Contain("[Demo Client]");
        sent.Subject.Should().Contain("Northridge Addition, Block 4");
        sent.To.Should().Contain("admin@democlient.local");
        sent.To.Should().Contain("viewer@bisconsultants.local");
        sent.To.Should().Contain("editor@bisconsultants.local");
        sent.HtmlBody.Should().Contain("/documents/");
    }

    [Fact]
    public async Task Status_change_succeeds_when_smtp_is_off()
    {
        var factory = new ApiFactory();
        await using var _ = factory;
        var editor = await factory.LoginAsync("editor@bisconsultants.local");
        var response = await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoHeld}", new
        {
            statusId = SeedIds.StatusPending
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("statusId").GetGuid().Should().Be(SeedIds.StatusPending);
    }

    [Fact]
    public async Task Viewer_can_post_client_visible_comment_and_cannot_see_internal_notes()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var detail = await (await viewer.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        detail.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        detail.GetProperty("canPostComments").GetBoolean().Should().BeTrue();
        detail.GetProperty("internalNotes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        var created = await viewer.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/comments", new
        {
            body = "Client can see this. @arivera please confirm the west line."
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var sent = LastEmail("New comment");
        sent.To.Should().Contain("editor@bisconsultants.local");

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var bell = await (await editor.GetAsync("/api/notifications")).ReadJsonAsync();
        bell.GetProperty("items").EnumerateArray()
            .Should().Contain(x => x.GetProperty("kind").GetString() == "mention"
                && x.GetProperty("workItemId").GetGuid() == SeedIds.DemoPlat);
    }

    [Fact]
    public async Task Priority_needed_by_appears_on_detail_list_and_due_this_week_bucket()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var neededBy = DateTimeOffset.UtcNow.Date.AddDays(2);
        var patched = await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            isPriority = true,
            priorityNote = "County needs the exhibit",
            priorityNeededBy = neededBy
        });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await patched.ReadJsonAsync();
        detail.GetProperty("isPriority").GetBoolean().Should().BeTrue();
        DateTimeOffset.Parse(detail.GetProperty("priorityNeededBy").GetString()!).Date
            .Should().Be(neededBy.Date);

        var list = await (await editor.GetAsync("/api/work-items?bucket=duethisweek&pageSize=100")).ReadJsonAsync();
        list.GetProperty("buckets").GetProperty("dueThisWeek").GetInt32().Should().BeGreaterThan(0);
        var dueItem = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoSurvey);
        dueItem.TryGetProperty("assignedToUserId", out var assignee).Should().BeTrue();
        if (assignee.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            assignee.GetGuid().Should().Be(SeedIds.EditorDemo);
        }

        var ga = await _factory.LoginAsync("admin@bisconsultants.local");
        var notifications = await (await ga.GetAsync("/api/notifications")).ReadJsonAsync();
        notifications.GetProperty("items").EnumerateArray()
            .Should().Contain(x =>
                x.GetProperty("kind").GetString() == "priority"
                && x.GetProperty("workItemId").GetGuid() == SeedIds.DemoSurvey
                && x.GetProperty("body").GetString()!.Contains("Needed by", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Public_upload_received_emails_assigned_techs()
    {
        var anon = _factory.CreateClient();
        var link = await (await (await _factory.LoginAsync("admin@bisconsultants.local"))
            .GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString();
        var response = await anon.PostAsJsonAsync($"/api/public/uploads/{token}/received", new
        {
            fileCount = 3,
            fileNames = new[] { "a.pdf", "b.pdf", "c.pdf" }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sent = LastEmail("3 files received");
        sent.Subject.Should().Be("[Demo Client] 3 files received");
        sent.To.Should().Contain("editor@bisconsultants.local");
        sent.HtmlBody.Should().Contain("a.pdf");
    }

    private static GisDashboard.Application.Email.OutboundEmail LastEmail(string subjectPart)
    {
        var match = CapturingEmailSender.Sent
            .FirstOrDefault(x => x.Subject.Contains(subjectPart, StringComparison.Ordinal));
        match.Should().NotBeNull($"an email whose subject contains '{subjectPart}'");
        return match!;
    }

    [Fact]
    public async Task Organization_exposes_primary_assigned_tech()
    {
        var ga = await _factory.LoginAsync("admin@bisconsultants.local");
        var orgs = await (await ga.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        var demo = orgs.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoClient);
        var techs = demo.GetProperty("assignedTechs").EnumerateArray().ToList();
        techs.Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.EditorDemo
            && x.GetProperty("isPrimary").GetBoolean());
    }
}
