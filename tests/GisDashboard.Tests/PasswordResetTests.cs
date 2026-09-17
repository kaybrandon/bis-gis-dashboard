using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Application.Email;
using GisDashboard.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class PasswordResetUnavailableTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasswordResetUnavailableTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Forgot_password_fails_closed_without_smtp_and_does_not_enumerate()
    {
        var anon = _factory.CreateClient();
        var status = await (await anon.GetAsync("/api/auth/password-reset")).ReadJsonAsync();
        status.GetProperty("available").GetBoolean().Should().BeFalse();
        status.GetProperty("message").GetString().Should().Be(PasswordResetService.UnavailableMessage);

        var known = await anon.PostAsJsonAsync("/api/auth/forgot-password", new { emailOrUsername = "admin@bisconsultants.local" });
        var unknown = await anon.PostAsJsonAsync("/api/auth/forgot-password", new { emailOrUsername = "nobody@missing.example" });
        known.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        unknown.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var knownBody = await known.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        knownBody.Should().Be(unknownBody);
        knownBody.Should().Contain("Password reset");
        knownBody.Should().NotContain("admin@bisconsultants.local");
        knownBody.Should().NotContain("nobody@missing.example");
    }
}

public sealed class PasswordResetFlowTests : IClassFixture<ForgotPasswordFactory>
{
    private readonly ForgotPasswordFactory _factory;

    public PasswordResetFlowTests(ForgotPasswordFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Forgot_password_is_generic_then_reset_link_signs_in()
    {
        CapturingEmailSender.Last = null;
        var anon = _factory.CreateClient();

        var unknown = await anon.PostAsJsonAsync("/api/auth/forgot-password", new { emailOrUsername = "nobody@missing.example" });
        unknown.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unknown.ReadJsonAsync()).GetProperty("message").GetString().Should().Be(PasswordResetService.GenericMessage);
        CapturingEmailSender.Sent.Should().NotContain(x =>
            x.Subject.Contains("Reset your GIS Dashboard password", StringComparison.Ordinal)
            && x.To.Contains("nobody@missing.example"));

        var known = await anon.PostAsJsonAsync("/api/auth/forgot-password", new { emailOrUsername = "editor@bisconsultants.local" });
        known.StatusCode.Should().Be(HttpStatusCode.OK);
        var knownJson = await known.ReadJsonAsync();
        knownJson.GetProperty("message").GetString().Should().Be(PasswordResetService.GenericMessage);
        (await known.Content.ReadAsStringAsync()).Should().NotContain("editor@bisconsultants.local");

        var resetMail = ResetEmailTo("editor@bisconsultants.local");
        var token = TokenFrom(resetMail.TextBody);

        var bad = await anon.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token = "not-a-real-token",
            newPassword = "NewReset!Gis2026",
            confirmPassword = "NewReset!Gis2026"
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.ReadJsonAsync()).GetProperty("message").GetString().Should().Be(PasswordResetService.InvalidLinkMessage);

        var reset = await anon.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token,
            newPassword = "NewReset!Gis2026",
            confirmPassword = "NewReset!Gis2026"
        });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        var reuse = await anon.PostAsJsonAsync("/api/auth/reset-password", new
        {
            token,
            newPassword = "AnotherReset!Gis2026",
            confirmPassword = "AnotherReset!Gis2026"
        });
        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var login = await anon.PostAsJsonAsync("/api/auth/login", new
        {
            email = "editor@bisconsultants.local",
            password = "NewReset!Gis2026"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Username_lookup_sends_the_same_generic_result()
    {
        CapturingEmailSender.Last = null;
        var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/auth/forgot-password", new { emailOrUsername = "viewer@bisconsultants.local" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("message").GetString().Should().Be(PasswordResetService.GenericMessage);
        TokenFrom(ResetEmailTo("viewer@bisconsultants.local").TextBody).Should().NotBeNullOrWhiteSpace();
    }

    private static GisDashboard.Application.Email.OutboundEmail ResetEmailTo(string email)
    {
        var match = CapturingEmailSender.Sent.FirstOrDefault(x =>
            x.Subject.Contains("Reset your GIS Dashboard password", StringComparison.Ordinal)
            && x.To.Contains(email));
        match.Should().NotBeNull($"a password-reset email to {email}");
        return match!;
    }

    private static string TokenFrom(string body)
    {
        const string marker = "reset-password?token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var rest = body[(start + marker.Length)..];
        return rest.Split('\n', ' ', '&', '<')[0].Trim();
    }
}

public sealed class PasswordResetRateLimitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasswordResetRateLimitTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Forgot_password_is_rate_limited()
    {
        var anon = _factory.CreateClient();
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 6; i++)
        {
            last = (await anon.PostAsJsonAsync("/api/auth/forgot-password", new { emailOrUsername = $"user{i}@example.com" })).StatusCode;
        }

        last.Should().Be(HttpStatusCode.TooManyRequests);
    }
}

public sealed class ForgotPasswordFactory : ApiFactory
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
