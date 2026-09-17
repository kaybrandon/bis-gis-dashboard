using FluentAssertions;
using GisDashboard.Application.Connections;

namespace GisDashboard.Tests;

public sealed class ConnectionPathTests
{
    [Theory]
    [InlineData("/orgs/democlient/shapefiles")]
    [InlineData("orgs/democlient/shapefiles")]
    [InlineData("/orgs/democlient/shapefiles/")]
    [InlineData(@"\orgs\democlient\shapefiles")]
    [InlineData("workfiles/orgs/democlient/shapefiles")]
    [InlineData("/workfiles/orgs/democlient/shapefiles")]
    [InlineData(@"workfiles\orgs\democlient\shapefiles")]
    public void Azure_orgs_paths_are_not_file_server_folders(string path)
    {
        ConnectionPath.IsAzureOrgPath(path).Should().BeTrue();
        ConnectionPath.InferSourceKind(path).Should().Be("azure");
        ConnectionPath.ToAzurePrefix(path).Should().Be("orgs/democlient/shapefiles");
        ConnectionPath.DisplayPath(path).Should().Be("workfiles/orgs/democlient/shapefiles");
        ConnectionPath.Canonicalize(path).Should().Be("workfiles/orgs/democlient/shapefiles");
    }

    [Fact]
    public void Local_drive_is_preferred_over_unc()
    {
        ConnectionPath.IsLocalDrivePath(@"C:\GIS\Outgoing").Should().BeTrue();
        ConnectionPath.IsUncPath(@"C:\GIS\Outgoing").Should().BeFalse();
        ConnectionPath.InferSourceKind(@"C:\GIS\Outgoing").Should().Be("local");
        ConnectionPath.IsAzureOrgPath(@"C:\GIS\Outgoing").Should().BeFalse();
    }

    [Fact]
    public void Unc_is_only_for_network_shares()
    {
        ConnectionPath.IsUncPath(@"\\server\gis\outbox").Should().BeTrue();
        ConnectionPath.InferSourceKind(@"\\server\gis\outbox").Should().Be("unc");
        ConnectionPath.IsAzureOrgPath(@"\\server\gis\outbox").Should().BeFalse();
    }

    [Fact]
    public void Heartbeat_label_is_never_blank()
    {
        ConnectionPath.HeartbeatLabel(null, enrolled: true).Should().Be("Never — enrolled agent has not checked in");
        ConnectionPath.HeartbeatLabel(null, enrolled: false).Should().Be("None — agent not enrolled or not running");
        ConnectionPath.HeartbeatIsFresh(null).Should().BeFalse();
        ConnectionPath.HeartbeatIsFresh(DateTimeOffset.UtcNow.AddMinutes(-1)).Should().BeTrue();
        ConnectionPath.HeartbeatIsFresh(DateTimeOffset.UtcNow.AddMinutes(-20)).Should().BeTrue();
        ConnectionPath.HeartbeatIsFresh(DateTimeOffset.UtcNow.AddMinutes(-45)).Should().BeFalse();
    }

    [Fact]
    public void Checker_and_agent_identity_name_pc_and_user()
    {
        ConnectionPath.CheckerIdentity().Should().Contain(" on ");
        ConnectionPath.AgentIdentity("BIS\\brandon", "BRANDON-PC", null).Should().Be(@"BIS\brandon on BRANDON-PC");
    }
}
