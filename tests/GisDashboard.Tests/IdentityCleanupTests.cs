using FluentAssertions;
using GisDashboard.Domain;

namespace GisDashboard.Tests;

public sealed class IdentityCleanupTests
{
    [Fact]
    public void Report_name_uses_full_name_when_on_file()
    {
        UserIdentity.ReportName("Alex Rivera", "arivera").Should().Be("Alex Rivera");
        UserIdentity.ReportName("  Jordan Hale  ", "jhale").Should().Be("Jordan Hale");
        UserIdentity.ReportName("   ", "admin", "admin", "admin@bisconsultants.local").Should().Be("admin");
        UserIdentity.ReportName(null, "arivera").Should().Be("arivera");
    }

    [Fact]
    public void Person_name_becomes_title_case_and_initial_last_username()
    {
        UserIdentity.LooksLikePersonName("Alex Rivera").Should().BeTrue();
        UserIdentity.ToTitleCase("alex rivera").Should().Be("Alex Rivera");
        UserIdentity.FromPersonName("Alex Rivera").Should().Be("arivera");
    }

    [Fact]
    public void Role_labels_and_emails_are_not_person_names()
    {
        UserIdentity.LooksLikePersonName("Global Administrator").Should().BeFalse();
        UserIdentity.LooksLikePersonName("Demo Client Administrator").Should().BeFalse();
        UserIdentity.LooksLikePersonName("admin@bisconsultants.local").Should().BeFalse();
        UserIdentity.LooksLikePersonName("Token upload").Should().BeFalse();
        UserIdentity.LooksLikePersonName("Uploader").Should().BeFalse();
    }

    [Fact]
    public void Username_from_email_local_part_and_collision_suffix()
    {
        UserIdentity.FromEmail("admin@bisconsultants.local").Should().Be("admin");
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "admin" };
        UserIdentity.UniqueUsername("admin", taken).Should().Be("admin2");
        taken.Add("admin2");
        UserIdentity.UniqueUsername("admin", taken).Should().Be("admin3");
    }

    [Fact]
    public void Greeting_first_name_is_full_name_first_token_only()
    {
        UserIdentity.GreetingFirstName("Global Admin").Should().Be("Global");
        UserIdentity.GreetingFirstName("Alex Rivera").Should().Be("Alex");
        UserIdentity.GreetingFirstName("  Jordan Hale  ").Should().Be("Jordan");
        UserIdentity.GreetingFirstName(null).Should().BeNull();
        UserIdentity.GreetingFirstName("").Should().BeNull();
        UserIdentity.GreetingFirstName("admin@bisconsultants.local").Should().BeNull();
        UserIdentity.GreetingFirstName("arivera").Should().Be("arivera");
    }

    [Fact]
    public void Needs_rewrite_only_when_username_is_still_an_email()
    {
        UserIdentity.NeedsUsernameRewrite("admin@bisconsultants.local", "admin@bisconsultants.local").Should().BeTrue();
        UserIdentity.NeedsUsernameRewrite("arivera", "editor@bisconsultants.local").Should().BeFalse();
    }
}
