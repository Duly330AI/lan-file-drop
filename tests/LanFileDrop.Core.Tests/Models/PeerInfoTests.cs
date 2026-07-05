using LanFileDrop.Core.Models;

namespace LanFileDrop.Core.Tests.Models;

public class PeerInfoTests
{
    [Fact]
    public void Create_WithoutEndpoint_IsValid()
    {
        var identity = DeviceIdentity.Create("Peer");

        var peer = PeerInfo.Create(identity);

        Assert.Same(identity, peer.Identity);
        Assert.Null(peer.Endpoint);
    }

    [Fact]
    public void Create_WithEndpoint_SetsEndpoint()
    {
        var identity = DeviceIdentity.Create("Peer");

        var peer = PeerInfo.Create(identity, "peer.local:5000");

        Assert.Equal("peer.local:5000", peer.Endpoint);
    }

    [Fact]
    public void Create_WithWhitespaceEndpoint_Throws()
    {
        var identity = DeviceIdentity.Create("Peer");

        Assert.Throws<ArgumentException>(() => PeerInfo.Create(identity, "   "));
    }

    [Fact]
    public void Create_WithNullIdentity_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PeerInfo.Create(null!));
    }
}
