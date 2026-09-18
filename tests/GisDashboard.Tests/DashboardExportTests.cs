using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Application.Email;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class DashboardExportTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DashboardExportTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Default_range_is_thirty_days_and_completed_is_not_24h()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/dashboard")).ReadJsonAsync();
        json.GetProperty("volumeOverTime").GetArrayLength().Should().Be(30);
        json.GetProperty("rangeLabel").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("kpis").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == "completed")
            .GetProperty("label").GetString().Should().Be("Completed");
    }

    [Fact]
    public async Task Custom_range_changes_volume_length()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var json = await (await client.GetAsync("/api/dashboard?from=2026-09-01&to=2026-09-07")).ReadJsonAsync();
        json.GetProperty("volumeOverTime").GetArrayLength().Should().Be(7);
        json.GetProperty("volumeOverTime")[0].GetProperty("date").GetString().Should().Be("2026-09-01");
        json.GetProperty("volumeOverTime")[6].GetProperty("date").GetString().Should().Be("2026-09-07");
        json.GetProperty("rangeLabel").GetString().Should().Contain("2026");
    }

    [Fact]
    public async Task Pdf_download_is_a_pdf()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.GetAsync("/api/dashboard/pdf?from=2026-08-12&to=2026-09-10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(200);
        System.Text.Encoding.ASCII.GetString(bytes[..4]).Should().Be("%PDF");
    }

    [Fact]
    public async Task Viewer_can_download_pdf_but_cannot_email()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync("/api/dashboard/pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync("/api/dashboard/recipients")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync("/api/dashboard/email", new { userIds = Array.Empty<Guid>(), extraEmails = new[] { "a@b.com" } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unconfigured_email_fails_closed()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PostAsJsonAsync("/api/dashboard/email", new
        {
            userIds = Array.Empty<Guid>(),
            extraEmails = new[] { "ops@bisconsultants.local" }
        });
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var message = (await response.ReadJsonAsync()).GetProperty("message").GetString();
        message.Should().Contain("Email__Enabled");
        message.Should().Contain("Email__Smtp__Host");
    }

    [Fact]
    public async Task Anonymous_dashboard_export_is_unauthorized()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/dashboard/pdf")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PostAsJsonAsync("/api/dashboard/email", new { extraEmails = new[] { "a@b.com" } }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public sealed class DashboardEmailConfiguredTests : IClassFixture<DashboardEmailFactory>
{
    private readonly DashboardEmailFactory _factory;

    public DashboardEmailConfiguredTests(DashboardEmailFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Configured_send_attaches_pdf()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        CapturingEmailSender.Last = null;
        var response = await client.PostAsJsonAsync(
            "/api/dashboard/email?from=2026-09-01&to=2026-09-10",
            new { userIds = Array.Empty<Guid>(), extraEmails = new[] { "ops@bisconsultants.local" } });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("delivered").GetBoolean().Should().BeTrue();
        json.GetProperty("recipients").GetString().Should().Contain("ops@bisconsultants.local");
        CapturingEmailSender.Last.Should().NotBeNull();
        CapturingEmailSender.Last!.Attachments.Should().ContainSingle();
        CapturingEmailSender.Last.Attachments[0].ContentType.Should().Be("application/pdf");
        CapturingEmailSender.Last.Subject.Should().Contain("GIS Dashboard");
        var pdfText = GisDashboard.Infrastructure.AiFill.PdfTextExtractor.Extract(
            new MemoryStream(CapturingEmailSender.Last.Attachments[0].Content));
        pdfText.Should().Contain("Alex Rivera", "Fail if: emailed dashboard PDF lists people by username only.");
        pdfText.Should().NotContain("arivera");
    }
}

public sealed class DashboardEmailFactory : ApiFactory
{
    protected override void ExtraConfig(Dictionary<string, string?> config)
    {
        config["Email:Enabled"] = "true";
        config["Email:Smtp:Host"] = "smtp.test.local";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmailSender, CapturingEmailSender>();
        });
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    public static OutboundEmail? Last;
    public static ConcurrentBag<OutboundEmail> Sent { get; } = new();

    public bool IsConfigured => true;

    public Task<EmailSendResult> SendAsync(OutboundEmail message, CancellationToken cancellationToken = default)
    {
        Last = message;
        Sent.Add(message);
        return Task.FromResult(new EmailSendResult(true, "smtp", null));
    }
}
