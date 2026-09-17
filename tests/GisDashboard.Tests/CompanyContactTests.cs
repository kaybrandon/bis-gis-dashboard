using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace GisDashboard.Tests;

public sealed class CompanyContactTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CompanyContactTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Public_company_returns_bis_defaults()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/public/company");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("name").GetString().Should().Be("BIS Consultants");
        json.GetProperty("phone").GetString().Should().Be("800-247-9045");
        json.GetProperty("email").GetString().Should().Be("gissupport@bisconsultants.com");
        json.GetProperty("address").GetString().Should().Be("14802 Venture Dr. Farmers Branch Tx 75234");
        json.GetProperty("website").GetString().Should().Be("www.bisconsultants.com");
        json.GetProperty("updatedAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Settings_includes_company_and_status_feature()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        json.GetProperty("company").GetProperty("name").GetString().Should().Be("BIS Consultants");
        json.GetProperty("company").GetProperty("phone").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("features").GetProperty("companyContact").GetProperty("enabled").GetBoolean().Should().BeTrue();
        json.GetProperty("features").GetProperty("environmentStatus").GetProperty("note").GetString()
            .Should().Contain("Status page");
    }

    [Fact]
    public async Task Only_global_admin_can_save_company_contact()
    {
        var payload = new
        {
            phone = "214-555-0100",
            email = "ops@bisconsultants.com",
            address = "14802 Venture Dr. Farmers Branch Tx 75234",
            website = "www.bisconsultants.com"
        };

        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/settings/company")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.PutAsJsonAsync("/api/settings/company", payload)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        foreach (var email in new[] { "admin@democlient.local", "editor@bisconsultants.local", "viewer@bisconsultants.local" })
        {
            var client = await _factory.LoginAsync(email);
            (await client.GetAsync("/api/settings/company")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PutAsJsonAsync("/api/settings/company", payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Global_admin_can_save_company_contact()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var saved = await admin.PutAsJsonAsync("/api/settings/company", new
        {
            phone = "800-111-2222",
            email = "hello@bisconsultants.com",
            address = "14802 Venture Dr. Farmers Branch Tx 75234",
            website = "https://www.bisconsultants.com"
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var json = await saved.ReadJsonAsync();
        json.GetProperty("phone").GetString().Should().Be("800-111-2222");
        json.GetProperty("email").GetString().Should().Be("hello@bisconsultants.com");
        json.GetProperty("website").GetString().Should().Be("https://www.bisconsultants.com");

        var anon = await (await _factory.CreateClient().GetAsync("/api/public/company")).ReadJsonAsync();
        anon.GetProperty("phone").GetString().Should().Be("800-111-2222");
        anon.GetProperty("email").GetString().Should().Be("hello@bisconsultants.com");

        (await admin.PutAsJsonAsync("/api/settings/company", new
        {
            phone = "800-247-9045",
            email = "gissupport@bisconsultants.com",
            address = "14802 Venture Dr. Farmers Branch Tx 75234",
            website = "www.bisconsultants.com"
        })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await admin.PutAsJsonAsync("/api/settings/company", new
        {
            phone = "800-247-9045",
            email = "not-an-email",
            address = "14802 Venture Dr. Farmers Branch Tx 75234",
            website = "www.bisconsultants.com"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
