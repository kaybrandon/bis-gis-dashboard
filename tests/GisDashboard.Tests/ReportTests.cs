using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class ReportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ReportTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_filtered_by_organization_id_only_returns_that_client()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var demo = await (await client.GetAsync($"/api/reports?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        demo.EnumerateArray().Should().NotBeEmpty();
        demo.EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("organizationId").GetGuid() == SeedIds.DemoClient
            && x.GetProperty("organizationName").GetString() == "Demo Client");

        var other = await (await client.GetAsync($"/api/reports?organizationId={SeedIds.OtherClient}")).ReadJsonAsync();
        other.EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("organizationId").GetGuid() == SeedIds.OtherClient);
    }

    [Fact]
    public async Task Generate_august_matches_maintenance_report_and_keeps_history()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var first = await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 8
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var v1 = await first.ReadJsonAsync();
        v1.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        v1.GetProperty("monthLabel").GetString().Should().Be("August 2026");
        v1.GetProperty("version").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        v1.GetProperty("emailed").GetBoolean().Should().BeFalse();

        var snap = v1.GetProperty("snapshot");
        snap.GetProperty("title").GetString().Should().Be("GIS Maintenance Report");
        snap.GetProperty("monthName").GetString().Should().Be("August");
        snap.GetProperty("intro").GetString().Should().Contain("month of August");
        snap.GetProperty("contact").GetProperty("phone").GetString().Should().Be("(800) 247-9045");
        snap.GetProperty("contact").GetProperty("email").GetString().Should().Be("gissupport@bisconsultants.com");
        snap.GetProperty("contact").GetProperty("company").GetString().Should().Be("BIS Consultants");
        snap.GetProperty("completed").GetInt32().Should().BeGreaterThanOrEqualTo(4);
        snap.GetProperty("parcelStatus").GetProperty("available").GetBoolean().Should().BeTrue();
        snap.GetProperty("parcelStatus").GetProperty("totalRealAccounts").GetInt32().Should().Be(12840);
        snap.GetProperty("maintenanceByType").EnumerateArray().Select(x => x.GetProperty("name").GetString())
            .Should().Contain("Corrections").And.Contain("Plats");

        var oak = snap.GetProperty("completedItems").EnumerateArray()
            .Single(x => x.GetProperty("fileName").GetString() == "Oak-Grove-Replat.pdf");
        oak.GetProperty("corrections").GetInt32().Should().Be(2);
        oak.GetProperty("plats").GetInt32().Should().Be(1);
        oak.GetProperty("annexations").GetInt32().Should().Be(0);
        oak.GetProperty("deeds").GetInt32().Should().Be(0);
        oak.GetProperty("sketch").GetBoolean().Should().BeFalse();
        oak.GetProperty("propertyIds").GetString().Should().Be("R88901;R88902");
        oak.TryGetProperty("assignedToUserId", out _).Should().BeFalse();

        var second = await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 8
        });
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var v2 = await second.ReadJsonAsync();
        v2.GetProperty("version").GetInt32().Should().BeGreaterThan(v1.GetProperty("version").GetInt32());
        v2.GetProperty("id").GetGuid().Should().NotBe(v1.GetProperty("id").GetGuid());

        var history = await (await client.GetAsync($"/api/reports?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        history.EnumerateArray().Count(x =>
            x.GetProperty("year").GetInt32() == 2026
            && x.GetProperty("month").GetInt32() == 8).Should().BeGreaterThanOrEqualTo(2);

        var stillThere = await client.GetAsync($"/api/reports/{v1.GetProperty("id").GetGuid()}");
        stillThere.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stillThere.ReadJsonAsync()).GetProperty("version").GetInt32().Should().Be(v1.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Pdf_and_csv_match_sample_columns_and_are_org_scoped()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var created = await (await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 8
        })).ReadJsonAsync();
        var reportId = created.GetProperty("id").GetGuid();

        var pdf = await client.GetAsync($"/api/reports/{reportId}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        pdfBytes.Length.Should().BeGreaterThan(200);
        pdfBytes[0].Should().Be(0x25); // %
        pdfBytes[1].Should().Be((byte)'P');
        var fileName = pdf.Content.Headers.ContentDisposition?.FileNameStar
            ?? pdf.Content.Headers.ContentDisposition?.FileName
            ?? "";
        fileName.Should().Contain("GIS Maintenance Report");
        fileName.Should().NotContain("Cad");
        fileName.Should().NotContain("CAD");

        var csv = await client.GetAsync($"/api/reports/{reportId}/csv");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        var csvText = await csv.Content.ReadAsStringAsync();
        csvText.Should().Contain("File Name,Upload Date,Worked Date,Annexations,Corrections,Plats,Deeds,Sketch,Property Ids");
        csvText.Should().Contain("Oak-Grove-Replat.pdf");
        csvText.Should().Contain("R88901;R88902");

        var other = await _factory.LoginAsync("admin@otherclient.local");
        (await other.GetAsync($"/api/reports/{reportId}/pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync($"/api/reports/{reportId}/csv")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_can_read_and_download_but_cannot_generate_or_email()
    {
        var admin = await _factory.LoginAsync("admin@democlient.local");
        var created = await (await admin.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 7
        })).ReadJsonAsync();
        var reportId = created.GetProperty("id").GetGuid();
        created.GetProperty("snapshot").GetProperty("completedItems").EnumerateArray()
            .Select(x => x.GetProperty("fileName").GetString())
            .Should().Contain("Warranty-Deed-scan.png");

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync($"/api/reports/{reportId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync($"/api/reports/{reportId}/pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync($"/api/reports?organizationId={SeedIds.DemoClient}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await viewer.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 7
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await viewer.PostAsJsonAsync($"/api/reports/{reportId}/email", new
        {
            userIds = new[] { SeedIds.ViewerDemo },
            extraEmails = Array.Empty<string>()
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Other_org_admin_can_read_demo_reports_viewer_cannot()
    {
        var demoAdmin = await _factory.LoginAsync("admin@democlient.local");
        var created = await (await demoAdmin.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 6
        })).ReadJsonAsync();
        var reportId = created.GetProperty("id").GetGuid();

        var other = await _factory.LoginAsync("admin@otherclient.local");
        (await other.GetAsync($"/api/reports/{reportId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync($"/api/reports?organizationId={SeedIds.DemoClient}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync($"/api/reports?organizationId={SeedIds.OtherClient}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Email_rejects_other_org_user_and_dry_runs_without_marking_sent()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var created = await (await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 9
        })).ReadJsonAsync();
        created.GetProperty("snapshot").GetProperty("hours").GetDecimal().Should().Be(2.25m);
        created.GetProperty("snapshot").GetProperty("hoursLabel").GetString().Should().Be("2h 15m");
        created.GetProperty("snapshot").GetProperty("completed").GetInt32().Should().BeGreaterThanOrEqualTo(5);
        var reportId = created.GetProperty("id").GetGuid();

        var forbidden = await client.PostAsJsonAsync($"/api/reports/{reportId}/email", new
        {
            userIds = new[] { SeedIds.EditorOther },
            extraEmails = Array.Empty<string>()
        });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var dry = await client.PostAsJsonAsync($"/api/reports/{reportId}/email", new
        {
            userIds = new[] { SeedIds.OrgAdminDemo, SeedIds.EditorDemo },
            extraEmails = new[] { "ops@democlient.example" }
        });
        dry.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await dry.ReadJsonAsync();
        result.GetProperty("delivered").GetBoolean().Should().BeFalse();
        result.GetProperty("mode").GetString().Should().Be("dry-run");
        result.GetProperty("recipients").GetString().Should().Contain("admin@democlient.local");
        result.GetProperty("recipients").GetString().Should().Contain("ops@democlient.example");

        var detail = await (await client.GetAsync($"/api/reports/{reportId}")).ReadJsonAsync();
        detail.GetProperty("emailed").GetBoolean().Should().BeFalse();
        detail.GetProperty("emailCount").GetInt32().Should().Be(0);
        detail.GetProperty("lastEmailedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("emails").GetArrayLength().Should().Be(1);
        detail.GetProperty("emails")[0].GetProperty("delivered").GetBoolean().Should().BeFalse();
        detail.GetProperty("emails")[0].GetProperty("mode").GetString().Should().Be("dry-run");
    }

    [Fact]
    public async Task Recipients_are_org_users_only()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var json = await (await client.GetAsync($"/api/reports/recipients?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        var emails = json.EnumerateArray().Select(x => x.GetProperty("email").GetString()).ToList();
        emails.Should().Contain("admin@democlient.local");
        emails.Should().Contain("editor@bisconsultants.local");
        emails.Should().Contain("viewer@bisconsultants.local");
        emails.Should().NotContain("admin@otherclient.local");
        emails.Should().NotContain("editor.other@bisconsultants.local");

        var other = await _factory.LoginAsync("admin@otherclient.local");
        (await other.GetAsync($"/api/reports/recipients?organizationId={SeedIds.DemoClient}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Annual_report_is_separate_history_without_parcel_status()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var monthly = await (await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            cadence = "Monthly",
            year = 2026,
            month = 8
        })).ReadJsonAsync();
        monthly.GetProperty("cadence").GetString().Should().Be("Monthly");
        monthly.GetProperty("snapshot").GetProperty("includeParcelStatus").GetBoolean().Should().BeTrue();
        monthly.GetProperty("snapshot").GetProperty("title").GetString().Should().Be("GIS Maintenance Report");

        var annual = await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            cadence = "Annual",
            year = 2026
        });
        annual.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await annual.ReadJsonAsync();
        json.GetProperty("cadence").GetString().Should().Be("Annual");
        json.GetProperty("month").GetInt32().Should().Be(0);
        json.GetProperty("monthLabel").GetString().Should().Be("Annual 2026");
        json.GetProperty("id").GetGuid().Should().NotBe(monthly.GetProperty("id").GetGuid());

        var snap = json.GetProperty("snapshot");
        snap.GetProperty("title").GetString().Should().Be("GIS Annual Maintenance Report");
        snap.GetProperty("cadence").GetString().Should().Be("Annual");
        snap.GetProperty("includeParcelStatus").GetBoolean().Should().BeFalse();
        snap.GetProperty("intro").GetString().Should().Contain("annual GIS Maintenance Report for 2026");
        snap.GetProperty("completed").GetInt32().Should().BeGreaterThanOrEqualTo(12);
        snap.GetProperty("completedItems").EnumerateArray().Select(x => x.GetProperty("fileName").GetString())
            .Should().Contain("Oak-Grove-Replat.pdf").And.Contain("Warranty-Deed-scan.png")
            .And.Contain("Cedar-Ridge-Plat.pdf");
        snap.GetProperty("maintenanceByType").EnumerateArray().Select(x => x.GetProperty("name").GetString())
            .Should().Contain("Corrections").And.Contain("Plats").And.Contain("Deeds");

        var history = await (await client.GetAsync($"/api/reports?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        history.EnumerateArray().Select(x => x.GetProperty("cadence").GetString())
            .Should().Contain("Monthly").And.Contain("Annual");

        var pdf = await client.GetAsync($"/api/reports/{json.GetProperty("id").GetGuid()}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var fileName = pdf.Content.Headers.ContentDisposition?.FileNameStar
            ?? pdf.Content.Headers.ContentDisposition?.FileName
            ?? "";
        fileName.Should().Contain("Annual");
        fileName.Should().NotContain("Cad");

        var email = await client.PostAsJsonAsync($"/api/reports/{json.GetProperty("id").GetGuid()}/email", new
        {
            userIds = new[] { SeedIds.EditorDemo },
            extraEmails = Array.Empty<string>()
        });
        email.StatusCode.Should().Be(HttpStatusCode.OK);
        (await email.ReadJsonAsync()).GetProperty("mode").GetString().Should().Be("dry-run");
    }

    [Fact]
    public async Task Parcel_inventory_on_org_appears_in_generated_report()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var saved = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            parcelTotalRealAccounts = 12840,
            parcelWithOwnership = 12416
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);

        var generated = await (await client.PostAsJsonAsync("/api/reports/generate", new
        {
            organizationId = SeedIds.DemoClient,
            year = 2026,
            month = 5
        })).ReadJsonAsync();
        var parcel = generated.GetProperty("snapshot").GetProperty("parcelStatus");
        parcel.GetProperty("available").GetBoolean().Should().BeTrue();
        parcel.GetProperty("totalRealAccounts").GetInt32().Should().Be(12840);
        parcel.GetProperty("parcelsWithOwnership").GetInt32().Should().Be(12416);
        parcel.GetProperty("missingRealAccounts").GetInt32().Should().Be(424);
        parcel.GetProperty("percentComplete").GetDecimal().Should().Be(97);

        await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            parcelTotalRealAccounts = 12840,
            parcelWithOwnership = 12416
        });
    }
}
