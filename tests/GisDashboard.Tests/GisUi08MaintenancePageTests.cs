using FluentAssertions;

namespace GisDashboard.Tests;

/// <summary>
/// GIS-UI-08 — zipdeploy / prod push holding page. Locked copy + coffee mug
/// at deploy/app_offline.htm (Azure site-root name). Fail if the file is
/// missing, the copy is rewritten, Alex is unsigned, or the tone goes scary.
/// </summary>
public sealed class GisUi08MaintenancePageTests
{
    private static readonly string PagePath = FindPage();

    [Fact]
    public void App_offline_htm_lives_at_the_known_deploy_path()
    {
        File.Exists(PagePath).Should().BeTrue(
            "Fail if: no message during deploy — deploy/app_offline.htm must exist for the pack zip.");
        Path.GetFileName(PagePath).Should().Be("app_offline.htm");
    }

    [Fact]
    public void Page_uses_the_locked_copy_coffee_mug_and_alex_signature()
    {
        var html = File.ReadAllText(PagePath);

        html.Should().Contain("GIS is grabbing a coffee while we ship an update.",
            "Fail if: copy rewritten away from lock.");
        html.Should().Contain("Back in a few minutes — try again shortly.",
            "Fail if: copy implies tomorrow / all-day outage or was rewritten.");
        html.Should().Contain("— Alex",
            "Fail if: missing Alex signature.");
        html.Should().Contain("<svg",
            "Fail if: coffee mug visual is missing.");
        html.Should().Contain("Coffee mug",
            "Fail if: coffee mug visual is missing.");
        html.Length.Should().BeGreaterThan(512,
            "IE/legacy friendly-error pages replace tiny 503 bodies; keep the file self-contained and substantial.");
    }

    [Fact]
    public void Page_stays_self_contained_and_light()
    {
        var html = File.ReadAllText(PagePath);

        html.Should().NotContain("https://",
            "Fail if: page references external assets — IIS serves only app_offline.htm while it is present.");
        html.Should().NotContain("http://");
        html.Should().NotContain("<link ");
        html.Should().NotContain("<script ");
        html.Should().NotContain("<img ");

        html.Should().NotContain("tomorrow",
            "Fail if: copy implies tomorrow / all-day outage.");
        html.Should().NotContain("all-day");
        html.Should().NotContain("all day");
        html.Should().NotContain("outage");
        html.Should().NotContain("unavailable");
        html.Should().NotContain("emergency");
        html.Should().NotContain("critical");
        html.Should().NotContain("sorry for the inconvenience");
    }

    private static string FindPage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "deploy", "app_offline.htm");
            if (File.Exists(candidate) || File.Exists(Path.Combine(dir.FullName, "GisDashboard.slnx")))
                return candidate;
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "deploy", "app_offline.htm"));
    }
}
