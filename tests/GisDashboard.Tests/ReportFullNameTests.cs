using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using GisDashboard.Infrastructure.AiFill;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// GIS-UI-07 — send-report and sibling exports show Full name when it is on the user record.
/// </summary>
public sealed class ReportFullNameTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReportFullNameTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Maintenance_report_generated_by_and_recipients_use_full_name()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var created = await editor.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 4
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = await created.ReadJsonAsync();
        report.GetProperty("generatedByName").GetString().Should().Be("Alex Rivera");
        report.GetProperty("generatedByName").GetString().Should().NotBe("arivera");

        var list = await (await editor.GetAsync($"/api/reports?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        list.EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == report.GetProperty("id").GetGuid())
            .GetProperty("generatedByName").GetString().Should().Be("Alex Rivera");

        var recipients = await (await editor.GetAsync($"/api/reports/recipients?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        AssertNamedPeople(recipients, "Alex Rivera", "Jordan Hale", "Riley Patel", "Casey Nguyen");
    }

    [Fact]
    public async Task Dashboard_send_recipients_use_full_name()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var recipients = await (await editor.GetAsync("/api/dashboard/recipients")).ReadJsonAsync();
        AssertNamedPeople(recipients, "Alex Rivera", "Jordan Hale", "Riley Patel", "Casey Nguyen");
    }

    [Fact]
    public async Task Dashboard_pdf_and_work_item_export_list_full_names()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var pdf = await editor.GetAsync("/api/dashboard/pdf?from=2026-08-01&to=2026-09-30");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var pdfText = PdfTextExtractor.Extract(new MemoryStream(await pdf.Content.ReadAsByteArrayAsync()));
        pdfText.Should().Contain("Alex Rivera");
        pdfText.Should().NotContain("arivera");

        var csv = await editor.GetAsync("/api/work-items/export?pageSize=100&bucket=all");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        var csvText = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        csvText.Should().Contain("Alex Rivera");
        csvText.Should().NotContain("arivera");
    }

    [Fact]
    public async Task Time_report_export_lists_full_name_not_username()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await admin.GetAsync("/api/time-report/export?from=2026-09-01&to=2026-09-30&bucket=month");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("Alex Rivera");
        csv.Should().NotContain("arivera");
        csv.Should().NotContain(SeedIds.EditorDemo.ToString());
    }

    [Fact]
    public async Task Recipient_created_with_full_name_is_not_username_only()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "morgan.lee.report@democlient.local",
            password = "Demo!Gis2026",
            displayName = "mlee",
            fullName = "Morgan Lee",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var user = await created.ReadJsonAsync();
        user.GetProperty("fullName").GetString().Should().Be("Morgan Lee");
        user.GetProperty("displayName").GetString().Should().Be("mlee");

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var reportRecipients = await (await editor.GetAsync($"/api/reports/recipients?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        var reportPerson = reportRecipients.EnumerateArray()
            .Single(x => x.GetProperty("email").GetString() == "morgan.lee.report@democlient.local");
        reportPerson.GetProperty("displayName").GetString().Should().Be("Morgan Lee");
        reportPerson.GetProperty("fullName").GetString().Should().Be("Morgan Lee");

        var dashRecipients = await (await editor.GetAsync("/api/dashboard/recipients")).ReadJsonAsync();
        var dashPerson = dashRecipients.EnumerateArray()
            .Single(x => x.GetProperty("email").GetString() == "morgan.lee.report@democlient.local");
        dashPerson.GetProperty("displayName").GetString().Should().Be("Morgan Lee");
        dashPerson.GetProperty("fullName").GetString().Should().Be("Morgan Lee");
    }

    private static void AssertNamedPeople(System.Text.Json.JsonElement recipients, params string[] fullNames)
    {
        var labels = recipients.EnumerateArray()
            .Select(x => (
                Display: x.GetProperty("displayName").GetString(),
                Full: x.TryGetProperty("fullName", out var full) ? full.GetString() : null,
                Email: x.GetProperty("email").GetString()))
            .ToList();

        foreach (var name in fullNames)
        {
            labels.Should().Contain(x => x.Display == name,
                "Fail if: send-report lists {0} by username only.", name);
            labels.Should().Contain(x => x.Full == name,
                "Fail if: Full name is blank for {0} when on file.", name);
        }

        labels.Should().NotContain(x => x.Display == "arivera");
        labels.Should().NotContain(x => x.Display == "jhale");
        labels.Should().NotContain(x => x.Display == "rpatel");
        labels.Should().NotContain(x => x.Display == "cnguyen");
    }
}

public sealed class MaintenanceReportEmailFullNameTests : IClassFixture<DashboardEmailFactory>
{
    private readonly DashboardEmailFactory _factory;

    public MaintenanceReportEmailFullNameTests(DashboardEmailFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Emailed_maintenance_report_identifies_sender_by_full_name()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var created = await (await editor.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 3
        })).ReadJsonAsync();

        CapturingEmailSender.Last = null;
        var email = await editor.PostAsJsonAsync($"/api/reports/{created.GetProperty("id").GetGuid()}/email", new
        {
            userIds = new[] { SeedIds.ViewerDemo },
            extraEmails = Array.Empty<string>()
        });
        email.StatusCode.Should().Be(HttpStatusCode.OK, await email.Content.ReadAsStringAsync());
        CapturingEmailSender.Last.Should().NotBeNull();
        CapturingEmailSender.Last!.HtmlBody.Should().Contain("Sent by Alex Rivera");
        CapturingEmailSender.Last.TextBody.Should().Contain("Sent by Alex Rivera");
        CapturingEmailSender.Last.HtmlBody.Should().NotContain("arivera");
        CapturingEmailSender.Last.TextBody.Should().NotContain("arivera");
    }
}
