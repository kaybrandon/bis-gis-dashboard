using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Application.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class EmailSettingsTests : IClassFixture<ApiFactory>
{
    private const string SecretPassword = "DoNotEcho-Smtp-9!";
    private readonly ApiFactory _factory;

    public EmailSettingsTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_email_settings_are_unauthorized()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/settings/email")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PutAsJsonAsync("/api/settings/email", new { host = "smtp.example.com" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PostAsJsonAsync("/api/settings/email/test", new { to = "ops@bisconsultants.local" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_global_admin_can_read_or_save_smtp()
    {
        foreach (var email in new[] { "admin@democlient.local", "editor@bisconsultants.local", "viewer@bisconsultants.local" })
        {
            var client = await _factory.LoginAsync(email);
            (await client.GetAsync("/api/settings/email")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PutAsJsonAsync("/api/settings/email", new
            {
                host = "smtp.example.com",
                port = 587,
                useSsl = true,
                from = "noreply@bisconsultants.local"
            })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Global_admin_can_save_smtp_and_never_sees_the_password()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var before = await (await client.GetAsync("/api/settings/email")).ReadJsonAsync();
        before.GetProperty("status").GetString().Should().Be("not_configured");
        before.GetProperty("configured").GetBoolean().Should().BeFalse();
        before.TryGetProperty("password", out _).Should().BeFalse();

        var savedResponse = await client.PutAsJsonAsync("/api/settings/email", new
        {
            host = "smtp.office365.com",
            port = 587,
            useSsl = true,
            from = "noreply@bisconsultants.local",
            fromName = "GIS Dashboard",
            user = "smtp-user",
            password = SecretPassword,
            replyTo = "ops@bisconsultants.local"
        });
        savedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var savedBody = await savedResponse.Content.ReadAsStringAsync();
        savedBody.Should().NotContain(SecretPassword);
        savedBody.Should().NotContain("PasswordProtected");

        var saved = await savedResponse.ReadJsonAsync();
        saved.GetProperty("status").GetString().Should().Be("configured");
        saved.GetProperty("configured").GetBoolean().Should().BeTrue();
        saved.GetProperty("host").GetString().Should().Be("smtp.office365.com");
        saved.GetProperty("from").GetString().Should().Be("noreply@bisconsultants.local");
        saved.GetProperty("user").GetString().Should().Be("smtp-user");
        saved.GetProperty("replyTo").GetString().Should().Be("ops@bisconsultants.local");
        saved.GetProperty("passwordConfigured").GetBoolean().Should().BeTrue();
        saved.GetProperty("passwordSource").GetString().Should().Be("secure-store");
        saved.TryGetProperty("password", out _).Should().BeFalse();

        var again = await client.GetAsync("/api/settings/email");
        var againBody = await again.Content.ReadAsStringAsync();
        againBody.Should().NotContain(SecretPassword);
        var json = await again.ReadJsonAsync();
        json.GetProperty("passwordConfigured").GetBoolean().Should().BeTrue();
        json.TryGetProperty("password", out _).Should().BeFalse();

        var keep = await client.PutAsJsonAsync("/api/settings/email", new
        {
            host = "smtp.office365.com",
            port = 587,
            useSsl = true,
            from = "noreply@bisconsultants.local",
            fromName = "GIS Dashboard",
            user = "smtp-user",
            password = "",
            replyTo = "ops@bisconsultants.local"
        });
        (await keep.ReadJsonAsync()).GetProperty("passwordConfigured").GetBoolean().Should().BeTrue();

        var status = await (await client.GetAsync("/api/admin/status")).ReadJsonAsync();
        var checks = status.GetProperty("checks").EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString()!);
        checks["email"].GetProperty("status").GetString().Should().Be("ok");
    }
}

public sealed class EmailSettingsUnconfiguredTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EmailSettingsUnconfiguredTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Test_email_fails_closed_when_smtp_is_not_configured()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PostAsJsonAsync("/api/settings/email/test", new { to = "ops@bisconsultants.local" });
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var message = (await response.ReadJsonAsync()).GetProperty("message").GetString();
        message.Should().Contain("Email / SMTP");
        message.Should().NotContain("DoNotEcho-Smtp-9!");
    }
}

public sealed class EmailSettingsSendTests : IClassFixture<EmailSettingsFactory>
{
    private readonly EmailSettingsFactory _factory;

    public EmailSettingsSendTests(EmailSettingsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Test_email_sends_through_configured_smtp()
    {
        CapturingEmailSender.Last = null;
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        (await client.PutAsJsonAsync("/api/settings/email", new
        {
            host = "smtp.test.local",
            port = 587,
            useSsl = true,
            from = "noreply@bisconsultants.local",
            fromName = "GIS Dashboard",
            user = "smtp-user",
            password = "DoNotEcho-Smtp-9!",
            replyTo = "ops@bisconsultants.local"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync("/api/settings/email/test", new { to = "ops@bisconsultants.local" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("delivered").GetBoolean().Should().BeTrue();
        json.GetProperty("note").GetString().Should().Contain("ops@bisconsultants.local");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("DoNotEcho-Smtp-9!");

        CapturingEmailSender.Last.Should().NotBeNull();
        CapturingEmailSender.Last!.To.Should().Equal("ops@bisconsultants.local");
        CapturingEmailSender.Last.Subject.Should().Contain("SMTP test");
    }

    [Fact]
    public async Task Dashboard_send_report_uses_saved_smtp()
    {
        CapturingEmailSender.Last = null;
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        (await client.PutAsJsonAsync("/api/settings/email", new
        {
            host = "smtp.test.local",
            port = 587,
            useSsl = true,
            from = "noreply@bisconsultants.local",
            password = "DoNotEcho-Smtp-9!"
        })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            "/api/dashboard/email",
            new { userIds = Array.Empty<Guid>(), extraEmails = new[] { "ops@bisconsultants.local" } });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CapturingEmailSender.Last.Should().NotBeNull();
        CapturingEmailSender.Last!.Attachments.Should().ContainSingle();
    }
}

public sealed class EmailSettingsFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmailSender, CapturingEmailSender>();
        });
    }
}
