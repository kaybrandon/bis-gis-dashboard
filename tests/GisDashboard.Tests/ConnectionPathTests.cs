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
    public void Azure_orgs_paths_are_not_file_server_folders(string path)
    {
        ConnectionPath.IsAzureOrgPath(path).Should().BeTrue();
        ConnectionPath.InferSourceKind(path).Should().Be("azure");
        ConnectionPath.ToAzurePrefix(path).Should().Be("orgs/democlient/shapefiles");
        ConnectionPath.DisplayPath(path).Should().Be("/orgs/democlient/shapefiles");
    }

    [Fact]
    public void Local_drive_is_preferred_over_unc()
    {
        ConnectionPath.IsLocalDrivePath(@"C:\GIS\Outgoing").Should().BeTrue();
        ConnectionPath.IsUncPath(@"C:\GIS\Outgoing").Should().BeFalse();
        ConnectionPath.InferSourceKind(@"C:\GIS\Outgoing").Should().Be("local");
    }

    [Fact]
    public void Unc_is_only_for_network_shares()
    {
        ConnectionPath.IsUncPath(@"\\server\gis\outbox").Should().BeTrue();
        ConnectionPath.InferSourceKind(@"\\server\gis\outbox").Should().Be("unc");
        ConnectionPath.IsAzureOrgPath(@"\\server\gis\outbox").Should().BeFalse();
    }
}
